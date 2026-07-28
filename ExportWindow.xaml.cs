using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ComboBox = System.Windows.Controls.ComboBox;
using Grid = System.Windows.Controls.Grid;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using TextBox = System.Windows.Controls.TextBox;
using WpfVisibility = System.Windows.Visibility;

namespace KrakhmalovSheets;

public partial class ExportWindow : Window
{
    private readonly Document _document;
    private readonly ObservableCollection<ExportItem> _items;
    private readonly ICollectionView _itemsView;
    private readonly LocalSettings _settings;
    private readonly Dictionary<string, HashSet<string>> _sets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ViewSheetSet> _revitSets = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingProfile;
    private ExportProfile _advancedProfile = new();
    private ExportRunResult? _lastResult;
    private string _paperSize = "Default";
    private string _paperOrientation = "Auto";
    private bool _disableTemporaryHideIsolate = true;
    private bool _disableWorksharingDisplay = true;
    private bool _disableRevealHiddenElements = true;
    private bool _disableRevealConstraints = true;

    public ExportWindow(Document document, IEnumerable<ViewSheet> sheets)
    {
        InitializeComponent();
        _document = document;
        _settings = LocalSettings.Load();

        List<View> views = new FilteredElementCollector(document)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(view => !view.IsTemplate && view.CanBePrinted)
            .OrderBy(view => view is ViewSheet ? 0 : 1)
            .ThenBy(view => view is ViewSheet sheet ? sheet.SheetNumber : view.ViewType.ToString())
            .ThenBy(view => view.Name)
            .ToList();

        _items = new ObservableCollection<ExportItem>(views.Select((view, index) => new ExportItem(document, view) { Order = index + 1 }));
        foreach (ExportItem item in _items)
        {
            item.PropertyChanged += ItemChanged;
        }

        _itemsView = CollectionViewSource.GetDefaultView(_items);
        _itemsView.Filter = FilterItem;
        ItemsGrid.ItemsSource = _itemsView;
        DocumentText.Text = $"{document.Title}  |  {_items.Count(item => item.IsSheet)} sheets  |  {_items.Count(item => !item.IsSheet)} views";

        LoadExportSetups();
        LoadPrinters();
        LoadSelectionSets();
        LoadProfiles();
        ScheduleDate.SelectedDate = DateTime.Today;
        ScheduleTimeBox.Text = DateTime.Now.AddHours(1).ToString("HH:mm");
        RepeatCombo.SelectedIndex = 0;
        ReportModeCombo.SelectedIndex = 0;
        UpdateState();
    }

    private void LoadExportSetups()
    {
        DwgSetupCombo.Items.Add("Default");
        foreach (string name in new FilteredElementCollector(_document).OfClass(typeof(ExportDWGSettings)).Cast<ExportDWGSettings>().Select(item => item.Name).OrderBy(name => name))
        {
            DwgSetupCombo.Items.Add(name);
        }

        DgnSetupCombo.Items.Add("Default");
        foreach (string name in new FilteredElementCollector(_document).OfClass(typeof(ExportDGNSettings)).Cast<ExportDGNSettings>().Select(item => item.Name).OrderBy(name => name))
        {
            DgnSetupCombo.Items.Add(name);
        }
    }

    private void LoadPrinters()
    {
        PrinterCombo.Items.Clear();
        PrinterCombo.Items.Add("Revit native PDF");
        foreach (string printer in PrinterService.GetInstalledPrinters()) PrinterCombo.Items.Add(printer);
        PrinterCombo.SelectedIndex = 0;
    }

    private void PrinterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrinterPaperSizeCombo == null || PrinterPaperSourceCombo == null) return;
        LoadPrinterCapabilities(PrinterCombo.SelectedItem as string ?? "Revit native PDF", null, null);
        UpdateState();
    }

    private void LoadPrinterCapabilities(string printer, string? selectedPaper, string? selectedSource)
    {
        PrinterPaperSizeCombo.Items.Clear();
        PrinterPaperSourceCombo.Items.Clear();
        if (printer.Equals("Revit native PDF", StringComparison.OrdinalIgnoreCase)) return;

        PrinterCapabilities capabilities = PrinterService.GetCapabilities(_document, printer);
        foreach (string value in capabilities.PaperSizes) PrinterPaperSizeCombo.Items.Add(value);
        foreach (string value in capabilities.PaperSources) PrinterPaperSourceCombo.Items.Add(value);
        PrinterPaperSizeCombo.Text = selectedPaper ?? string.Empty;
        PrinterPaperSourceCombo.Text = selectedSource ?? string.Empty;
        PrinterToFileCheck.IsChecked = capabilities.IsVirtual;
    }

    private void LoadSelectionSets()
    {
        _sets.Clear();
        _revitSets.Clear();
        foreach (ViewSheetSet set in new FilteredElementCollector(_document).OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>())
        {
            _sets[$"Revit: {set.Name}"] = set.Views.Cast<View>().Select(view => view.UniqueId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _revitSets[$"Revit: {set.Name}"] = set;
        }

        foreach (SelectionSetRecord set in _settings.SelectionSets)
        {
            _sets[$"Local: {set.Name}"] = set.ItemUniqueIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        SelectionSetCombo.ItemsSource = new[] { "All items" }.Concat(_sets.Keys.OrderBy(name => name)).ToList();
        SelectionSetCombo.SelectedIndex = 0;
    }

    private void LoadProfiles()
    {
        ProfileCombo.ItemsSource = _settings.Profiles.Select(profile => profile.Name).ToList();
        string name = _settings.Profiles.Any(profile => profile.Name == _settings.ActiveProfile) ? _settings.ActiveProfile : _settings.Profiles[0].Name;
        ProfileCombo.SelectedItem = name;
        ApplyProfile(_settings.Profiles.First(profile => profile.Name == name));
    }

    private bool FilterItem(object value)
    {
        if (value is not ExportItem item) return false;
        if (SheetsMode.IsChecked == true && !item.IsSheet) return false;
        if (ViewsMode.IsChecked == true && item.IsSheet) return false;
        if (ActiveOnlyCheck.IsChecked == true && item.Id != _document.ActiveView?.Id) return false;
        string search = SearchBox.Text.Trim();
        return string.IsNullOrWhiteSpace(search)
               || item.Number.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || item.Kind.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void SelectionFilterChanged(object sender, RoutedEventArgs e) => _itemsView?.Refresh();

    private void SearchChanged(object sender, TextChangedEventArgs e) => _itemsView?.Refresh();

    private void SelectVisible(object sender, RoutedEventArgs e)
    {
        foreach (ExportItem item in _itemsView.Cast<ExportItem>()) item.IsSelected = true;
    }

    private void ClearSelection(object sender, RoutedEventArgs e)
    {
        foreach (ExportItem item in _items) item.IsSelected = false;
    }

    private void SelectionSetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectionSetCombo.SelectedItem is not string name || name == "All items" || !_sets.TryGetValue(name, out HashSet<string>? ids)) return;
        foreach (ExportItem item in _items) item.IsSelected = ids.Contains(item.UniqueId);
    }

    private void SaveSelectionSet(object sender, RoutedEventArgs e)
    {
        string? name = PromptDialog.Ask(this, "Save selection set", "Set name");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_revitSets.Keys.Any(key => key[7..].Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            TaskDialog.Show("BIMLEADERS Sheets", "A Revit View/Sheet Set with this name already exists.");
            return;
        }

        try
        {
            RevitSelectionSetService.Create(_document, name, _items.Where(item => item.IsSelected).Select(item => item.Id));
            LoadSelectionSets();
            SelectionSetCombo.SelectedItem = $"Revit: {name.Trim()}";
            StatusText.Text = $"Revit View/Sheet Set '{name.Trim()}' created.";
        }
        catch (Exception exception)
        {
            TaskDialog.Show("BIMLEADERS Sheets", exception.Message);
        }
    }

    private void OpenSelectionSetMenu(object sender, RoutedEventArgs e)
    {
        SelectionSetMenuButton.ContextMenu.PlacementTarget = SelectionSetMenuButton;
        SelectionSetMenuButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        SelectionSetMenuButton.ContextMenu.IsOpen = true;
    }

    private void AddToSelectionSet(object sender, RoutedEventArgs e)
    {
        if (SelectionSetCombo.SelectedItem is not string name || !_revitSets.TryGetValue(name, out ViewSheetSet? set))
        {
            TaskDialog.Show("BIMLEADERS Sheets", "Select a Revit View/Sheet Set first.");
            return;
        }

        try
        {
            RevitSelectionSetService.Add(_document, set, _items.Where(item => item.IsSelected).Select(item => item.Id));
            string selectedName = name;
            LoadSelectionSets();
            SelectionSetCombo.SelectedItem = selectedName;
            StatusText.Text = $"Selected items were added to '{set.Name}'.";
        }
        catch (Exception exception)
        {
            TaskDialog.Show("BIMLEADERS Sheets", exception.Message);
        }
    }

    private void RemoveSelectionSet(object sender, RoutedEventArgs e)
    {
        if (SelectionSetCombo.SelectedItem is not string name) return;
        try
        {
            if (_revitSets.TryGetValue(name, out ViewSheetSet? set))
            {
                RevitSelectionSetService.Delete(_document, set);
            }
            else if (name.StartsWith("Local: ", StringComparison.Ordinal))
            {
                string localName = name[7..];
                _settings.SelectionSets.RemoveAll(item => item.Name.Equals(localName, StringComparison.OrdinalIgnoreCase));
                _settings.Save();
            }
            LoadSelectionSets();
        }
        catch (Exception exception)
        {
            TaskDialog.Show("BIMLEADERS Sheets", exception.Message);
        }
    }

    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfile || ProfileCombo.SelectedItem is not string name) return;
        ExportProfile? profile = _settings.Profiles.FirstOrDefault(item => item.Name == name);
        if (profile == null) return;
        _settings.ActiveProfile = profile.Name;
        _settings.Save();
        ApplyProfile(profile);
    }

    private void NewProfile(object sender, RoutedEventArgs e)
    {
        string? name = PromptDialog.Ask(this, "New profile", "Profile name");
        if (string.IsNullOrWhiteSpace(name) || _settings.Profiles.Any(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        ExportProfile profile = ReadProfileFromControls();
        profile.Name = name.Trim();
        _settings.Profiles.Add(profile);
        _settings.ActiveProfile = profile.Name;
        _settings.Save();
        LoadProfiles();
    }

    private void SaveProfile(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not string name) return;
        ExportProfile replacement = ReadProfileFromControls();
        replacement.Name = name;
        int index = _settings.Profiles.FindIndex(profile => profile.Name == name);
        if (index >= 0) _settings.Profiles[index] = replacement;
        _settings.ActiveProfile = name;
        _settings.Save();
        StatusText.Text = $"Profile '{name}' saved locally.";
    }

    private void DeleteProfile(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not string name || _settings.Profiles.Count <= 1) return;
        _settings.Profiles.RemoveAll(profile => profile.Name == name);
        _settings.ActiveProfile = _settings.Profiles[0].Name;
        _settings.Save();
        LoadProfiles();
    }

    private void ImportProfile(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Import BIMLEADERS Sheets profile", Filter = "BIMLEADERS Sheets profile (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ExportProfile profile = JsonSerializer.Deserialize<ExportProfile>(File.ReadAllText(dialog.FileName))
                                    ?? throw new InvalidDataException("The profile file is empty.");
            if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
            string baseName = profile.Name;
            int suffix = 2;
            while (_settings.Profiles.Any(item => item.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase))) profile.Name = $"{baseName} {suffix++}";
            _settings.Profiles.Add(profile);
            _settings.ActiveProfile = profile.Name;
            _settings.Save();
            LoadProfiles();
            StatusText.Text = $"Profile '{profile.Name}' imported.";
        }
        catch (Exception exception)
        {
            TaskDialog.Show("BIMLEADERS Sheets", $"Could not import the profile.\n\n{exception.Message}");
        }
    }

    private void ExportProfileFile(object sender, RoutedEventArgs e)
    {
        ExportProfile profile = ReadProfileFromControls();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export BIMLEADERS Sheets profile",
            Filter = "BIMLEADERS Sheets profile (*.json)|*.json",
            FileName = NamingService.Sanitize(profile.Name) + ".json"
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"Profile exported to {dialog.FileName}";
    }

    private void ApplyProfile(ExportProfile profile)
    {
        _loadingProfile = true;
        OutputFolderBox.Text = profile.OutputFolder;
        NamingPatternBox.Text = profile.NamingPattern;
        SplitByFormatCheck.IsChecked = profile.SplitByFormat;
        SplitFolderRadio.IsChecked = profile.SplitByFormat;
        SameFolderRadio.IsChecked = !profile.SplitByFormat;
        CreateReportCheck.IsChecked = profile.CreateReport;
        SelectReportMode(profile.ReportFormat, profile.CreateReport);
        PdfCheck.IsChecked = profile.Pdf;
        DwgCheck.IsChecked = profile.Dwg;
        DgnCheck.IsChecked = profile.Dgn;
        DwfCheck.IsChecked = profile.Dwf;
        NwcCheck.IsChecked = profile.Nwc;
        IfcCheck.IsChecked = profile.Ifc;
        ImageCheck.IsChecked = profile.Image;
        XmlCheck.IsChecked = profile.Xml;
        CombinedFilesRadio.IsChecked = profile.CombinePdf;
        SeparateFilesRadio.IsChecked = !profile.CombinePdf;
        CombinePdfCheck.IsChecked = false;
        CombinePdfCheck.IsChecked = profile.KeepPaperSizeAndOrientation;
        CombinedPdfNameBox.Text = profile.CombinedPdfName;
        string printer = profile.PdfEngine.Equals("Revit native PDF", StringComparison.OrdinalIgnoreCase)
            ? "Revit native PDF"
            : profile.PrinterName;
        PrinterCombo.SelectedItem = PrinterCombo.Items.Cast<object>().FirstOrDefault(item => string.Equals(item.ToString(), printer, StringComparison.OrdinalIgnoreCase))
                                    ?? PrinterCombo.Items[0];
        LoadPrinterCapabilities(PrinterCombo.SelectedItem?.ToString() ?? "Revit native PDF", profile.PrinterPaperSize, profile.PrinterPaperSource);
        PrinterCopiesBox.Text = Math.Clamp(profile.PrinterCopies, 1, 99).ToString(CultureInfo.CurrentCulture);
        PrinterCollateCheck.IsChecked = profile.PrinterCollate;
        PrinterReverseCheck.IsChecked = profile.PrinterReverseOrder;
        PrinterToFileCheck.IsChecked = profile.PrinterPrintToFile;
        AlwaysRasterCheck.IsChecked = profile.AlwaysRaster;
        RasterRadio.IsChecked = profile.AlwaysRaster;
        VectorRadio.IsChecked = !profile.AlwaysRaster;
        LinksBlueCheck.IsChecked = profile.ViewLinksInBlue;
        HideReferenceCheck.IsChecked = profile.HideReferencePlanes;
        HideTagsCheck.IsChecked = profile.HideUnreferencedTags;
        HideScopeCheck.IsChecked = profile.HideScopeBoxes;
        HideCropCheck.IsChecked = profile.HideCropBoundaries;
        HalftoneCheck.IsChecked = profile.ReplaceHalftone;
        MaskLinesCheck.IsChecked = profile.MaskCoincidentLines;
        CombineDwfCheck.IsChecked = profile.CombineDwf;
        SelectComboValue(RasterQualityCombo, profile.RasterQuality);
        SelectComboValue(ColorModeCombo, profile.ColorMode);
        SelectComboValue(ImageFormatCombo, profile.ImageFormat);
        SelectComboValue(ImageResolutionCombo, profile.ImageResolution);
        DwgSetupCombo.SelectedItem = DwgSetupCombo.Items.Contains(profile.DwgSetup) ? profile.DwgSetup : "Default";
        DgnSetupCombo.SelectedItem = DgnSetupCombo.Items.Contains(profile.DgnSetup) ? profile.DgnSetup : "Default";
        _paperSize = profile.PaperSize;
        _paperOrientation = profile.PaperOrientation;
        foreach (ExportItem item in _items)
        {
            item.OutputPaperSize = profile.PaperSize;
            item.OutputOrientation = profile.PaperOrientation;
        }
        PaperSizeButton.Content = $"Set Paper Size: {PaperSizeLabel(_paperSize)}  v";
        OrientationButton.Content = $"Set Orientation: {_paperOrientation}  v";
        CenterPlacement.IsChecked = !profile.PaperPlacement.Equals("Offset", StringComparison.OrdinalIgnoreCase);
        OffsetPlacement.IsChecked = profile.PaperPlacement.Equals("Offset", StringComparison.OrdinalIgnoreCase);
        OffsetCornerCombo.SelectedIndex = 0;
        OffsetXBox.Text = profile.OriginOffsetXmm.ToString("0.##", CultureInfo.CurrentCulture);
        OffsetYBox.Text = profile.OriginOffsetYmm.ToString("0.##", CultureInfo.CurrentCulture);
        FitToPageRadio.IsChecked = !profile.ZoomMode.Equals("Zoom", StringComparison.OrdinalIgnoreCase);
        ZoomRadio.IsChecked = profile.ZoomMode.Equals("Zoom", StringComparison.OrdinalIgnoreCase);
        ZoomPercentBox.Text = profile.ZoomPercentage.ToString(CultureInfo.CurrentCulture);
        _disableTemporaryHideIsolate = profile.DisableTemporaryHideIsolate;
        _disableWorksharingDisplay = profile.DisableWorksharingDisplay;
        _disableRevealHiddenElements = profile.DisableRevealHiddenElements;
        _disableRevealConstraints = profile.DisableRevealConstraints;
        _advancedProfile = ExportService.CloneProfile(profile);
        _loadingProfile = false;
        UpdateState();
    }

    private ExportProfile ReadProfileFromControls()
    {
        var profile = new ExportProfile
        {
            Name = ProfileCombo.SelectedItem as string ?? "Default",
            OutputFolder = OutputFolderBox.Text.Trim(),
            NamingPattern = NamingPatternBox.Text.Trim(),
            SplitByFormat = SplitFolderRadio.IsChecked == true,
            CreateReport = ReportModeCombo.SelectedIndex > 0,
            ReportFormat = SelectedReportMode(),
            Pdf = PdfCheck.IsChecked == true,
            Dwg = DwgCheck.IsChecked == true,
            Dgn = DgnCheck.IsChecked == true,
            Dwf = DwfCheck.IsChecked == true,
            Nwc = NwcCheck.IsChecked == true,
            Ifc = IfcCheck.IsChecked == true,
            Image = ImageCheck.IsChecked == true,
            Xml = XmlCheck.IsChecked == true,
            CombinePdf = CombinedFilesRadio.IsChecked == true,
            CombinedPdfName = CombinedPdfNameBox.Text.Trim(),
            PdfEngine = (PrinterCombo.SelectedItem?.ToString() ?? "Revit native PDF").Equals("Revit native PDF", StringComparison.OrdinalIgnoreCase) ? "Revit native PDF" : "Windows Printer",
            PrinterName = (PrinterCombo.SelectedItem?.ToString() ?? string.Empty).Equals("Revit native PDF", StringComparison.OrdinalIgnoreCase) ? string.Empty : PrinterCombo.SelectedItem?.ToString() ?? string.Empty,
            PrinterPaperSize = PrinterPaperSizeCombo.Text.Trim(),
            PrinterPaperSource = PrinterPaperSourceCombo.Text.Trim(),
            PrinterCopies = Math.Clamp((int)Math.Round(ParseNumber(PrinterCopiesBox.Text, 1)), 1, 99),
            PrinterCollate = PrinterCollateCheck.IsChecked == true,
            PrinterReverseOrder = PrinterReverseCheck.IsChecked == true,
            PrinterPrintToFile = PrinterToFileCheck.IsChecked == true,
            DwgSetup = DwgSetupCombo.SelectedItem as string ?? "Default",
            DgnSetup = DgnSetupCombo.SelectedItem as string ?? "Default",
            RasterQuality = ComboText(RasterQualityCombo, "High"),
            ColorMode = ComboText(ColorModeCombo, "Color"),
            AlwaysRaster = RasterRadio.IsChecked == true || AlwaysRasterCheck.IsChecked == true,
            ViewLinksInBlue = LinksBlueCheck.IsChecked == true,
            HideReferencePlanes = HideReferenceCheck.IsChecked == true,
            HideUnreferencedTags = HideTagsCheck.IsChecked == true,
            HideScopeBoxes = HideScopeCheck.IsChecked == true,
            HideCropBoundaries = HideCropCheck.IsChecked == true,
            ReplaceHalftone = HalftoneCheck.IsChecked == true,
            MaskCoincidentLines = MaskLinesCheck.IsChecked == true,
            CombineDwf = CombineDwfCheck.IsChecked == true,
            ImageFormat = ComboText(ImageFormatCombo, "PNG"),
            ImageResolution = ComboText(ImageResolutionCombo, "DPI_300"),
            PaperSize = _paperSize,
            PaperOrientation = _paperOrientation,
            PaperPlacement = OffsetPlacement.IsChecked == true ? "Offset" : "Center",
            OriginOffsetXmm = ParseNumber(OffsetXBox.Text),
            OriginOffsetYmm = ParseNumber(OffsetYBox.Text),
            ZoomMode = ZoomRadio.IsChecked == true ? "Zoom" : "FitToPage",
            ZoomPercentage = Math.Clamp((int)Math.Round(ParseNumber(ZoomPercentBox.Text, 100)), 10, 400),
            DisableTemporaryHideIsolate = _disableTemporaryHideIsolate,
            DisableWorksharingDisplay = _disableWorksharingDisplay,
            DisableRevealHiddenElements = _disableRevealHiddenElements,
            DisableRevealConstraints = _disableRevealConstraints,
            KeepPaperSizeAndOrientation = CombinePdfCheck.IsChecked == true
        };
        CopyAdvanced(_advancedProfile, profile);
        return profile;
    }

    private void FormatChanged(object sender, RoutedEventArgs e) => UpdateState();

    private void OptionStateChanged(object sender, RoutedEventArgs e) => UpdateState();

    private void StepChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SummaryGrid == null) return;
        SummaryGrid.ItemsSource = BuildPreviewRows();
        UpdateState();
    }

    private void BackStep(object sender, RoutedEventArgs e)
    {
        if (Steps.SelectedIndex > 0) Steps.SelectedIndex--;
    }

    private void NextStep(object sender, RoutedEventArgs e)
    {
        if (Steps.SelectedIndex < 2) Steps.SelectedIndex++;
    }

    private void BrowseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select export folder",
            InitialDirectory = Directory.Exists(OutputFolderBox.Text) ? OutputFolderBox.Text : null
        };
        if (dialog.ShowDialog(this) == true) OutputFolderBox.Text = dialog.FolderName;
    }

    private void RefreshView(object sender, RoutedEventArgs e)
    {
        _itemsView.Refresh();
        UpdateState();
    }

    private void ManageSchedules(object sender, RoutedEventArgs e)
    {
        var dialog = new ScheduleManagerWindow(_settings) { Owner = this };
        dialog.ShowDialog();
        StatusText.Text = $"{_settings.ScheduledJobs.Count(job => job.Enabled)} scheduled export(s) enabled.";
    }

    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new ExportSettingsWindow(ReadProfileFromControls()) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _disableTemporaryHideIsolate = dialog.DisableTemporaryHideIsolate;
        _disableWorksharingDisplay = dialog.DisableWorksharingDisplay;
        _disableRevealHiddenElements = dialog.DisableRevealHiddenElements;
        _disableRevealConstraints = dialog.DisableRevealConstraints;
        SaveProfile(sender, e);
    }

    private void OpenAdvancedFormatSettings(object sender, RoutedEventArgs e)
    {
        ExportProfile current = ReadProfileFromControls();
        var dialog = new AdvancedFormatSettingsWindow(current) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _advancedProfile = ExportService.CloneProfile(dialog.Profile);
        StatusText.Text = "Advanced format settings updated. Save the profile to keep them.";
    }

    private void OpenPaperSizeMenu(object sender, RoutedEventArgs e)
    {
        PaperSizeButton.ContextMenu.PlacementTarget = PaperSizeButton;
        PaperSizeButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        PaperSizeButton.ContextMenu.IsOpen = true;
    }

    private void SetPaperSize(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem item || item.Tag is not string value) return;
        _paperSize = value;
        foreach (ExportItem exportItem in TargetItemsForRowCommand()) exportItem.OutputPaperSize = value;
        PaperSizeButton.Content = $"Set Paper Size: {PaperSizeLabel(value)}  v";
        UpdateState();
    }

    private void OpenOrientationMenu(object sender, RoutedEventArgs e)
    {
        OrientationButton.ContextMenu.PlacementTarget = OrientationButton;
        OrientationButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OrientationButton.ContextMenu.IsOpen = true;
    }

    private void SetOrientation(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem item || item.Tag is not string value) return;
        _paperOrientation = value;
        foreach (ExportItem exportItem in TargetItemsForRowCommand()) exportItem.OutputOrientation = value;
        OrientationButton.Content = $"Set Orientation: {value}  v";
        UpdateState();
    }

    private void OpenNamingTokens(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        string[] builtIns = { "<Number>", "<Name>", "<Revision>", "<Type>", "<SheetSize>", "<ProjectName>", "<ProjectNumber>", "<UserName>", "%Y%", "%m%", "%d%", "%H%", "%M%", "%S%" };
        foreach (string token in builtIns) menu.Items.Add(TokenMenuItem(token));
        menu.Items.Add(new Separator());
        foreach (string token in NamingService.CollectParameterTokens(_document, _items.Where(item => item.IsSelected))) menu.Items.Add(TokenMenuItem(token));
        menu.PlacementTarget = NamingTokensButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        NamingTokensButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private WpfMenuItem TokenMenuItem(string token)
    {
        var item = new WpfMenuItem { Header = token, Tag = token };
        item.Click += (_, _) =>
        {
            int index = NamingPatternBox.CaretIndex;
            NamingPatternBox.Text = NamingPatternBox.Text.Insert(index, token);
            NamingPatternBox.CaretIndex = index + token.Length;
            NamingPatternBox.Focus();
        };
        return item;
    }

    private IEnumerable<ExportItem> TargetItemsForRowCommand()
    {
        List<ExportItem> rows = SummaryGrid?.SelectedItems.Cast<object>().OfType<ExportPreviewRow>().Select(row => row.Item).Distinct().ToList() ?? new List<ExportItem>();
        return rows.Count > 0 ? rows : _items.Where(item => item.IsSelected).ToList();
    }

    private void MoveRowsUp(object sender, RoutedEventArgs e) => MoveSelectedRows(-1);

    private void MoveRowsDown(object sender, RoutedEventArgs e) => MoveSelectedRows(1);

    private void MoveSelectedRows(int direction)
    {
        HashSet<ExportItem> moving = TargetItemsForRowCommand().ToHashSet();
        List<ExportItem> ordered = _items.Where(item => item.IsSelected).OrderBy(item => item.Order).ToList();
        if (direction < 0)
        {
            for (int i = 1; i < ordered.Count; i++)
            {
                if (!moving.Contains(ordered[i]) || moving.Contains(ordered[i - 1])) continue;
                (ordered[i - 1], ordered[i]) = (ordered[i], ordered[i - 1]);
            }
        }
        else
        {
            for (int i = ordered.Count - 2; i >= 0; i--)
            {
                if (!moving.Contains(ordered[i]) || moving.Contains(ordered[i + 1])) continue;
                (ordered[i], ordered[i + 1]) = (ordered[i + 1], ordered[i]);
            }
        }

        for (int i = 0; i < ordered.Count; i++) ordered[i].Order = i + 1;
        SummaryGrid.ItemsSource = BuildPreviewRows();
    }

    private void RemoveRows(object sender, RoutedEventArgs e)
    {
        List<ExportItem> selectedRows = SummaryGrid.SelectedItems.Cast<object>().OfType<ExportPreviewRow>().Select(row => row.Item).Distinct().ToList();
        foreach (ExportItem item in selectedRows) item.IsSelected = false;
        SummaryGrid.ItemsSource = BuildPreviewRows();
    }

    private void CreateExport(object sender, RoutedEventArgs e)
    {
        List<ExportItem> selected = _items.Where(item => item.IsSelected).OrderBy(item => item.Order).ToList();
        ExportProfile profile = ReadProfileFromControls();
        if (selected.Count == 0)
        {
            TaskDialog.Show("BIMLEADERS Sheets", "Select at least one sheet or view.");
            return;
        }

        if (!AnyFormat(profile))
        {
            TaskDialog.Show("BIMLEADERS Sheets", "Select at least one export format.");
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.OutputFolder))
        {
            TaskDialog.Show("BIMLEADERS Sheets", "Select an output folder.");
            return;
        }

        SaveProfile(sender, e);
        if (ScheduleCheck.IsChecked == true)
        {
            ScheduleExport(selected, profile);
            return;
        }

        SetBusy(true);
        try
        {
            foreach (ExportItem item in selected) item.Status = "Exporting";
            ExportRunResult result = ExportService.Execute(_document, selected, profile, (done, total, format) =>
            {
                ExportProgress.Maximum = total;
                ExportProgress.Value = done;
                StatusText.Text = $"{format}: {done} / {total}";
                Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            });
            _lastResult = result;
            ApplyResultStatuses(result, selected);
            RetryButton.IsEnabled = result.FailureCount > 0;
            SummaryGrid.ItemsSource = BuildPreviewRows();
            StatusText.Text = $"Completed: {result.SuccessCount} successful, {result.FailureCount} failed.";
            CreateSummaryText.Text = "Completed 100%";
            ProgressDetailsText.Text = $"{result.SuccessCount} successful, {result.FailureCount} failed";
            string message = $"Completed.\n\nSuccessful: {result.SuccessCount}\nFailed: {result.FailureCount}";
            if (!string.IsNullOrWhiteSpace(result.ReportPath)) message += $"\n\nReport: {result.ReportPath}";
            if (result.FailureCount > 0) message += "\n\n" + string.Join("\n", result.Entries.Where(entry => !entry.Success).Take(8).Select(entry => $"{entry.Format} {entry.Item}: {entry.Message}"));
            TaskDialog.Show("BIMLEADERS Sheets", message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RetryFailed(object sender, RoutedEventArgs e)
    {
        if (_lastResult == null) return;
        HashSet<string> failedIds = _lastResult.Entries.Where(entry => !entry.Success && !string.IsNullOrWhiteSpace(entry.ItemUniqueId)).Select(entry => entry.ItemUniqueId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> formats = _lastResult.Entries.Where(entry => !entry.Success).Select(entry => entry.Format).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<ExportItem> items = failedIds.Count == 0
            ? _items.Where(item => item.IsSelected).OrderBy(item => item.Order).ToList()
            : _items.Where(item => failedIds.Contains(item.UniqueId)).OrderBy(item => item.Order).ToList();
        if (items.Count == 0 || formats.Count == 0) return;

        SetBusy(true);
        try
        {
            ExportProfile profile = ReadProfileFromControls();
            foreach (ExportItem item in items) item.Status = "Retrying";
            ExportRunResult result = ExportService.Execute(_document, items, profile, (done, total, format) =>
            {
                ExportProgress.Maximum = total;
                ExportProgress.Value = done;
                StatusText.Text = $"Retry {format}: {done} / {total}";
                Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            }, formats);
            _lastResult = result;
            ApplyResultStatuses(result, items);
            RetryButton.IsEnabled = result.FailureCount > 0;
            SummaryGrid.ItemsSource = BuildPreviewRows();
            TaskDialog.Show("BIMLEADERS Sheets", $"Retry completed.\n\nSuccessful: {result.SuccessCount}\nFailed: {result.FailureCount}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static void ApplyResultStatuses(ExportRunResult result, IEnumerable<ExportItem> items)
    {
        bool combinedFailure = result.Entries.Any(entry => !entry.Success && string.IsNullOrWhiteSpace(entry.ItemUniqueId));
        foreach (ExportItem item in items)
        {
            List<ExportLogEntry> entries = result.Entries.Where(entry => entry.ItemUniqueId.Equals(item.UniqueId, StringComparison.OrdinalIgnoreCase)).ToList();
            item.Status = combinedFailure || entries.Any(entry => !entry.Success) ? "Failed" : "Completed";
        }
    }

    private void ScheduleExport(List<ExportItem> selected, ExportProfile profile)
    {
        DateTime date = ScheduleDate.SelectedDate ?? DateTime.Today;
        if (!TimeSpan.TryParse(ScheduleTimeBox.Text, CultureInfo.CurrentCulture, out TimeSpan time)
            && !TimeSpan.TryParse(ScheduleTimeBox.Text, CultureInfo.InvariantCulture, out time))
        {
            TaskDialog.Show("BIMLEADERS Sheets", "Enter a valid schedule time, for example 18:30.");
            return;
        }

        DateTime nextRun = date.Date + time;
        if (nextRun <= DateTime.Now)
        {
            TaskDialog.Show("BIMLEADERS Sheets", "The scheduled time must be in the future.");
            return;
        }

        _settings.ScheduledJobs.Add(new ScheduledExportJob
        {
            DocumentKey = ExportService.GetDocumentKey(_document),
            DocumentTitle = _document.Title,
            NextRun = nextRun,
            Repeat = ComboText(RepeatCombo, "Once"),
            Weekdays = SelectedWeekdays(),
            ItemUniqueIds = selected.Select(item => item.UniqueId).ToList(),
            Profile = ExportService.CloneProfile(profile)
        });
        _settings.Save();
        StatusText.Text = $"Export scheduled for {nextRun:g}.";
        TaskDialog.Show("BIMLEADERS Sheets", $"Export scheduled for {nextRun:g}. Keep Revit open with this project available.");
    }

    private void RepeatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WeekdayPanel == null) return;
        WeekdayPanel.IsEnabled = ComboText(RepeatCombo, "Once") == "Weekly";
    }

    private List<DayOfWeek> SelectedWeekdays()
    {
        var days = new List<DayOfWeek>();
        if (MonCheck.IsChecked == true) days.Add(DayOfWeek.Monday);
        if (TueCheck.IsChecked == true) days.Add(DayOfWeek.Tuesday);
        if (WedCheck.IsChecked == true) days.Add(DayOfWeek.Wednesday);
        if (ThuCheck.IsChecked == true) days.Add(DayOfWeek.Thursday);
        if (FriCheck.IsChecked == true) days.Add(DayOfWeek.Friday);
        if (SatCheck.IsChecked == true) days.Add(DayOfWeek.Saturday);
        if (SunCheck.IsChecked == true) days.Add(DayOfWeek.Sunday);
        if (ComboText(RepeatCombo, "Once") == "Weekly" && days.Count == 0) days.Add((ScheduleDate.SelectedDate ?? DateTime.Today).DayOfWeek);
        return days;
    }

    private void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExportItem.IsSelected)) UpdateState();
    }

    private void UpdateState()
    {
        if (BackButton == null) return;
        int selected = _items.Count(item => item.IsSelected);
        int selectedSheets = _items.Count(item => item.IsSelected && item.IsSheet);
        int selectedViews = selected - selectedSheets;
        int formats = new[] { PdfCheck, DwgCheck, DgnCheck, DwfCheck, NwcCheck, IfcCheck, ImageCheck, XmlCheck }.Count(check => check.IsChecked == true);
        StatusText.Text = $"{selectedSheets} sheet(s) and {selectedViews} view(s) selected. Total: {selected}";
        CreateSummaryText.Text = ExportProgress.Value > 0 && ExportProgress.Maximum > 0
            ? $"Completed {Math.Round(ExportProgress.Value / ExportProgress.Maximum * 100)}%"
            : "Completed 0%";
        ProgressDetailsText.Text = $"{selected} item(s), {formats} format(s)";
        ScheduleStatusText.Text = ScheduleCheck.IsChecked == true ? "The Scheduling Assistant is on." : "The Scheduling Assistant is off.";
        GlobalScheduleText.Text = ScheduleCheck.IsChecked == true ? "The scheduling assistant is: On" : "The scheduling assistant is: Off";
        BackButton.IsEnabled = Steps.SelectedIndex > 0;
        NextButton.Visibility = Steps.SelectedIndex < 2 ? WpfVisibility.Visible : WpfVisibility.Collapsed;
        CreateButton.Visibility = Steps.SelectedIndex == 2 ? WpfVisibility.Visible : WpfVisibility.Collapsed;
        CombinedPdfNameBox.IsEnabled = PdfCheck.IsChecked == true && CombinedFilesRadio.IsChecked == true;
        bool windowsPrinter = PdfCheck.IsChecked == true && !(PrinterCombo.SelectedItem?.ToString() ?? "Revit native PDF").Equals("Revit native PDF", StringComparison.OrdinalIgnoreCase);
        PrinterPaperSizeCombo.IsEnabled = windowsPrinter;
        PrinterPaperSourceCombo.IsEnabled = windowsPrinter;
        PrinterCopiesBox.IsEnabled = windowsPrinter;
        PrinterCollateCheck.IsEnabled = windowsPrinter;
        PrinterReverseCheck.IsEnabled = windowsPrinter;
        PrinterToFileCheck.IsEnabled = windowsPrinter;
        ScheduleDate.IsEnabled = ScheduleCheck.IsChecked == true;
        ScheduleTimeBox.IsEnabled = ScheduleCheck.IsChecked == true;
        RepeatCombo.IsEnabled = ScheduleCheck.IsChecked == true;
        WeekdayPanel.IsEnabled = ScheduleCheck.IsChecked == true && ComboText(RepeatCombo, "Once") == "Weekly";
        OffsetCornerCombo.IsEnabled = OffsetPlacement.IsChecked == true;
        OffsetXBox.IsEnabled = OffsetPlacement.IsChecked == true;
        OffsetYBox.IsEnabled = OffsetPlacement.IsChecked == true;
        ZoomPercentBox.IsEnabled = ZoomRadio.IsChecked == true;
        if (Steps.SelectedIndex == 2) SummaryGrid.ItemsSource = BuildPreviewRows();
    }

    private List<ExportPreviewRow> BuildPreviewRows()
    {
        string[] formats = GetSelectedFormats().ToArray();
        return _items
            .Where(item => item.IsSelected)
            .OrderBy(item => item.Order)
            .SelectMany(item => formats.Select(format => new ExportPreviewRow
            {
                Item = item,
                Order = item.Order,
                Number = item.Number,
                Name = item.Name,
                Format = format,
                Size = item.DisplayPaperSize,
                Orientation = item.DisplayOrientation,
                Progress = item.Status
            }))
            .ToList();
    }

    private IEnumerable<string> GetSelectedFormats()
    {
        if (PdfCheck.IsChecked == true) yield return "PDF";
        if (DwgCheck.IsChecked == true) yield return "DWG";
        if (DgnCheck.IsChecked == true) yield return "DGN";
        if (DwfCheck.IsChecked == true) yield return "DWF";
        if (NwcCheck.IsChecked == true) yield return "NWC";
        if (IfcCheck.IsChecked == true) yield return "IFC";
        if (ImageCheck.IsChecked == true) yield return "IMG";
        if (XmlCheck.IsChecked == true) yield return "XML";
    }

    private void SetBusy(bool busy)
    {
        CreateButton.IsEnabled = !busy;
        BackButton.IsEnabled = !busy && Steps.SelectedIndex > 0;
        NextButton.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
        if (!busy) ExportProgress.Value = 0;
    }

    private static bool AnyFormat(ExportProfile profile) => profile.Pdf || profile.Dwg || profile.Dgn || profile.Dwf || profile.Nwc || profile.Ifc || profile.Image || profile.Xml;

    private static void SelectComboValue(ComboBox combo, string value)
    {
        ComboBoxItem? item = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(entry => string.Equals(entry.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = item ?? combo.Items.Cast<object>().FirstOrDefault();
    }

    private static string ComboText(ComboBox combo, string fallback) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? combo.SelectedItem?.ToString() ?? fallback;

    private void SelectReportMode(string mode, bool legacyCreateReport)
    {
        string expected = string.IsNullOrWhiteSpace(mode) ? (legacyCreateReport ? "CSV" : "None") : mode;
        ReportModeCombo.SelectedItem = ReportModeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), expected, StringComparison.OrdinalIgnoreCase))
            ?? ReportModeCombo.Items[legacyCreateReport ? 1 : 0];
    }

    private string SelectedReportMode() => (ReportModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";

    private static void CopyAdvanced(ExportProfile source, ExportProfile target)
    {
        target.DwgMergedViews = source.DwgMergedViews;
        target.DwgBindImages = source.DwgBindImages;
        target.DwgCleanPcp = source.DwgCleanPcp;
        target.DgnMergedViews = source.DgnMergedViews;
        target.CombinedDwfName = source.CombinedDwfName;
        target.DwfFileType = source.DwfFileType;
        target.DwfCropBoxVisible = source.DwfCropBoxVisible;
        target.DwfImageQuality = source.DwfImageQuality;
        target.DwfImageFormat = source.DwfImageFormat;
        target.DwfExportObjectData = source.DwfExportObjectData;
        target.DwfExportTextures = source.DwfExportTextures;
        target.DwfExportAreas = source.DwfExportAreas;
        target.NwcWholeModel = source.NwcWholeModel;
        target.NwcConvertLinkedCad = source.NwcConvertLinkedCad;
        target.NwcConvertLights = source.NwcConvertLights;
        target.NwcFacetingFactor = source.NwcFacetingFactor;
        target.NwcDivideIntoLevels = source.NwcDivideIntoLevels;
        target.NwcElementProperties = source.NwcElementProperties;
        target.NwcFindMissingMaterials = source.NwcFindMissingMaterials;
        target.NwcRoomGeometry = source.NwcRoomGeometry;
        target.NwcCoordinates = source.NwcCoordinates;
        target.NwcExportUrls = source.NwcExportUrls;
        target.NwcRoomAsAttribute = source.NwcRoomAsAttribute;
        target.NwcExportLinks = source.NwcExportLinks;
        target.NwcExportElementIds = source.NwcExportElementIds;
        target.NwcExportParts = source.NwcExportParts;
        target.NwcParameters = source.NwcParameters;
        target.IfcVersion = source.IfcVersion;
        target.IfcSpaceBoundaryLevel = source.IfcSpaceBoundaryLevel;
        target.IfcFamilyMappingFile = source.IfcFamilyMappingFile;
        target.IfcBaseQuantities = source.IfcBaseQuantities;
        target.IfcWallAndColumnSplitting = source.IfcWallAndColumnSplitting;
        target.IfcVisibleElementsOnly = source.IfcVisibleElementsOnly;
        target.IfcExport2DElements = source.IfcExport2DElements;
        target.IfcExportLinkedFiles = source.IfcExportLinkedFiles;
        target.IfcExportPartsAsBuildingElements = source.IfcExportPartsAsBuildingElements;
        target.IfcExportInternalRevitPropertySets = source.IfcExportInternalRevitPropertySets;
        target.IfcExportIfcCommonPropertySets = source.IfcExportIfcCommonPropertySets;
        target.IfcUseActiveViewGeometry = source.IfcUseActiveViewGeometry;
        target.IfcFileType = source.IfcFileType;
        target.IfcPhaseId = source.IfcPhaseId;
        target.IfcSitePlacement = source.IfcSitePlacement;
        target.IfcIncludeSteelElements = source.IfcIncludeSteelElements;
        target.IfcExportRoomsInView = source.IfcExportRoomsInView;
        target.IfcExportSchedulesAsPsets = source.IfcExportSchedulesAsPsets;
        target.IfcExportSpecificSchedules = source.IfcExportSpecificSchedules;
        target.IfcExportUserDefinedPsets = source.IfcExportUserDefinedPsets;
        target.IfcUserDefinedPsetsFile = source.IfcUserDefinedPsetsFile;
        target.IfcExportParameterMapping = source.IfcExportParameterMapping;
        target.IfcParameterMappingFile = source.IfcParameterMappingFile;
        target.IfcTessellationLevel = source.IfcTessellationLevel;
        target.IfcExportSolidModelRep = source.IfcExportSolidModelRep;
        target.IfcUseFamilyAndTypeName = source.IfcUseFamilyAndTypeName;
        target.IfcUse2dRoomBoundaries = source.IfcUse2dRoomBoundaries;
        target.IfcIncludeSiteElevation = source.IfcIncludeSiteElevation;
        target.IfcStoreGuid = source.IfcStoreGuid;
        target.IfcExportBoundingBox = source.IfcExportBoundingBox;
        target.IfcUseOnlyTriangulation = source.IfcUseOnlyTriangulation;
        target.IfcUseTypeNameOnly = source.IfcUseTypeNameOnly;
        target.IfcUseVisibleName = source.IfcUseVisibleName;
        target.IfcCategoryMapping = source.IfcCategoryMapping;
        target.ImageFitDirection = source.ImageFitDirection;
        target.ImageZoomType = source.ImageZoomType;
        target.ImagePixelSize = source.ImagePixelSize;
        target.ImageZoom = source.ImageZoom;
        target.ImageCreateWebsite = source.ImageCreateWebsite;
        target.ImageShadowFormat = source.ImageShadowFormat;
        target.ImageWebsiteName = source.ImageWebsiteName;
        target.XmlIncludeParameters = source.XmlIncludeParameters;
        target.XmlIncludeProjectParameters = source.XmlIncludeProjectParameters;
        target.XmlOneFilePerItem = source.XmlOneFilePerItem;
    }

    private static string PaperSizeLabel(string value) => value == "Default" ? "Automatic" : value.Replace("ISO_", string.Empty).Replace("ANSI_", "ANSI ");

    private static double ParseNumber(string value, double fallback = 0)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            ? result
            : fallback;
    }
}

internal sealed class ExportPreviewRow
{
    public required ExportItem Item { get; init; }
    public int Order { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Orientation { get; init; } = string.Empty;
    public string Progress { get; init; } = string.Empty;
}

internal sealed class PromptDialog : Window
{
    private readonly TextBox _textBox = new();

    private PromptDialog(Window owner, string title, string label)
    {
        Owner = owner;
        Title = title;
        Width = 380;
        Height = 155;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new Grid { Margin = new Thickness(14) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(new TextBlock { Text = label });
        _textBox.Margin = new Thickness(0, 7, 0, 12);
        Grid.SetRow(_textBox, 1);
        panel.Children.Add(_textBox);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", Width = 85, IsDefault = true };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Cancel", Width = 85, Margin = new Thickness(7, 0, 0, 0), IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public static string? Ask(Window owner, string title, string label)
    {
        var dialog = new PromptDialog(owner, title, label);
        return dialog.ShowDialog() == true ? dialog._textBox.Text.Trim() : null;
    }
}
