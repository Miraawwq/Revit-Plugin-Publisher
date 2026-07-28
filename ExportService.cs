using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Autodesk.Revit.DB;

namespace MiraSHA.Sheets;

public static class ExportService
{
    public static ExportRunResult Execute(
        Document document,
        IReadOnlyList<ExportItem> items,
        ExportProfile profile,
        Action<int, int, string>? progress = null,
        IReadOnlySet<string>? formatFilter = null)
    {
        var result = new ExportRunResult();
        PrepareViewModes(document, items, profile);
        profile.OutputFolder = ExpandOutputFolder(profile.OutputFolder);
        Directory.CreateDirectory(profile.OutputFolder);

        List<string> enabledFormats = EnabledFormats(profile, formatFilter);
        int formatCount = enabledFormats.Count;
        int total = Math.Max(1, items.Count * Math.Max(1, formatCount));
        int completed = 0;

        if (enabledFormats.Contains("PDF"))
        {
            ExportPdf(document, items, profile, result, () => Report("PDF"));
        }

        if (enabledFormats.Contains("DWG"))
        {
            foreach (ExportItem item in items)
            {
                ExportCad(document, item, profile, result, "DWG");
                Report("DWG");
            }
        }

        if (enabledFormats.Contains("DGN"))
        {
            foreach (ExportItem item in items)
            {
                ExportCad(document, item, profile, result, "DGN");
                Report("DGN");
            }
        }

        if (enabledFormats.Contains("DWF"))
        {
            ExportDwf(document, items, profile, result, () => Report("DWF"));
        }

        if (enabledFormats.Contains("NWC"))
        {
            if (profile.NwcWholeModel)
            {
                ExportNwc(document, null, profile, result);
                foreach (ExportItem _ in items) Report("NWC");
            }
            else
            {
                foreach (ExportItem item in items)
                {
                    ExportNwc(document, item, profile, result);
                    Report("NWC");
                }
            }
        }

        if (enabledFormats.Contains("IFC"))
        {
            ExportIfc(document, items, profile, result);
            completed += items.Count;
            progress?.Invoke(Math.Min(completed, total), total, "IFC");
        }

        if (enabledFormats.Contains("IMG"))
        {
            if (profile.ImageCreateWebsite)
            {
                ExportImageWebsite(document, items, profile, result);
                foreach (ExportItem _ in items) Report("IMG");
            }
            else
            {
                foreach (ExportItem item in items)
                {
                    ExportImage(document, item, profile, result);
                    Report("IMG");
                }
            }
        }

        if (enabledFormats.Contains("XML"))
        {
            ExportXml(document, items, profile, result);
            completed += items.Count;
            progress?.Invoke(Math.Min(completed, total), total, "XML");
        }

        if (profile.CreateReport && formatFilter == null)
        {
            foreach (string path in ReportService.Write(profile, result)) result.ReportPaths.Add(path);
            result.ReportPath = result.ReportPaths.FirstOrDefault();
        }

        return result;

        void Report(string format)
        {
            progress?.Invoke(Math.Min(++completed, total), total, format);
        }
    }

    public static ExportProfile CloneProfile(ExportProfile profile)
    {
        string json = JsonSerializer.Serialize(profile);
        return JsonSerializer.Deserialize<ExportProfile>(json) ?? new ExportProfile();
    }

    public static string GetDocumentKey(Document document)
    {
        try
        {
            if (document.IsModelInCloud)
            {
                ModelPath cloudPath = document.GetCloudModelPath();
                return $"cloud:{cloudPath.GetProjectGUID()}:{cloudPath.GetModelGUID()}";
            }
        }
        catch
        {
        }

        return string.IsNullOrWhiteSpace(document.PathName)
            ? $"title:{document.Title}"
            : $"path:{Path.GetFullPath(document.PathName).ToLowerInvariant()}";
    }

    public static string ExpandOutputFolder(string value)
    {
        string expanded = Environment.ExpandEnvironmentVariables(value?.Trim().Trim('"') ?? string.Empty);
        if (expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            expanded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[2..]);
        return Path.GetFullPath(expanded);
    }

    private static List<string> EnabledFormats(ExportProfile profile, IReadOnlySet<string>? filter = null)
    {
        var formats = new List<string>();
        if (profile.Pdf) formats.Add("PDF");
        if (profile.Dwg) formats.Add("DWG");
        if (profile.Dgn) formats.Add("DGN");
        if (profile.Dwf) formats.Add("DWF");
        if (profile.Nwc) formats.Add("NWC");
        if (profile.Ifc) formats.Add("IFC");
        if (profile.Image) formats.Add("IMG");
        if (profile.Xml) formats.Add("XML");
        return filter == null ? formats : formats.Where(filter.Contains).ToList();
    }

    private static void ExportPdf(
        Document document,
        IReadOnlyList<ExportItem> items,
        ExportProfile profile,
        ExportRunResult result,
        Action progress)
    {
        if (!profile.PdfEngine.Equals("Revit native PDF", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(profile.PrinterName))
        {
            ExportWithPrintManager(document, items, profile, result, progress);
            return;
        }

        string folder = GetFormatFolder(profile, "PDF");
        if (profile.CombinePdf)
        {
            string fileName = EnsureExtension(Sanitize(profile.CombinedPdfName), ".pdf");
            try
            {
                using PDFExportOptions options = CreatePdfOptions(profile, null);
                options.Combine = true;
                options.FileName = fileName;
                bool success = document.Export(folder, items.Select(item => item.Id).ToList(), options);
                result.Add("PDF", "Combined", Path.Combine(folder, fileName), success, success ? string.Empty : "Revit returned false.");
            }
            catch (Exception exception)
            {
                result.Add("PDF", "Combined", Path.Combine(folder, fileName), false, exception.Message);
            }

            foreach (ExportItem _ in items)
            {
                progress();
            }

            return;
        }

        foreach (ExportItem item in items)
        {
            string fileName = EnsureExtension(GetFileName(document, item, profile), ".pdf");
            try
            {
                using PDFExportOptions options = CreatePdfOptions(profile, item);
                options.Combine = true;
                options.FileName = fileName;
                bool success = document.Export(folder, new List<ElementId> { item.Id }, options);
                result.Add("PDF", DisplayName(item), Path.Combine(folder, fileName), success, success ? string.Empty : "Revit returned false.", item.UniqueId);
            }
            catch (Exception exception)
            {
                result.Add("PDF", DisplayName(item), Path.Combine(folder, fileName), false, exception.Message, item.UniqueId);
            }

            progress();
        }
    }

    private static void ExportWithPrintManager(
        Document document,
        IReadOnlyList<ExportItem> items,
        ExportProfile profile,
        ExportRunResult result,
        Action progress)
    {
        string folder = GetFormatFolder(profile, "PDF");
        if (profile.CombinePdf)
        {
            string fileName = EnsureExtension(Sanitize(profile.CombinedPdfName), ".pdf");
            try
            {
                bool success = PrintViews(document, items, profile, Path.Combine(folder, fileName), null, true);
                result.Add("PDF", "Combined", Path.Combine(folder, fileName), success, success ? string.Empty : "The printer returned false.");
            }
            catch (Exception exception)
            {
                result.Add("PDF", "Combined", Path.Combine(folder, fileName), false, exception.Message);
            }

            foreach (ExportItem _ in items) progress();
            return;
        }

        foreach (ExportItem item in items)
        {
            string fileName = EnsureExtension(GetFileName(document, item, profile), ".pdf");
            try
            {
                bool success = PrintViews(document, new[] { item }, profile, Path.Combine(folder, fileName), item, false);
                result.Add("PDF", DisplayName(item), Path.Combine(folder, fileName), success, success ? string.Empty : "The printer returned false.", item.UniqueId);
            }
            catch (Exception exception)
            {
                result.Add("PDF", DisplayName(item), Path.Combine(folder, fileName), false, exception.Message, item.UniqueId);
            }
            progress();
        }
    }

    private static bool PrintViews(Document document, IReadOnlyList<ExportItem> items, ExportProfile profile, string outputPath, ExportItem? item, bool combined)
    {
        PrintManager manager = document.PrintManager;
        manager.SelectNewPrintDriver(profile.PrinterName);
        manager.PrintRange = PrintRange.Select;
        manager.CopyNumber = Math.Clamp(profile.PrinterCopies, 1, 99);
        manager.PrintOrderReverse = profile.PrinterReverseOrder;
        if (items.Count > 1 && manager.CopyNumber > 1) manager.Collate = profile.PrinterCollate;

        var views = new ViewSet();
        foreach (ExportItem exportItem in items)
        {
            if (document.GetElement(exportItem.Id) is View view) views.Insert(view);
        }
        manager.ViewSheetSetting.CurrentViewSheetSet.Views = views;

        PrintParameters parameters = manager.PrintSetup.CurrentPrintSetting.PrintParameters;
        ApplyPrintParameters(manager, parameters, profile, item);

        bool printToFile = manager.IsVirtual != VirtualPrinterType.None && profile.PrinterPrintToFile;
        manager.PrintToFile = printToFile;
        if (printToFile)
        {
            manager.CombinedFile = combined;
            manager.PrintToFileName = outputPath;
        }
        manager.Apply();
        return manager.SubmitPrint();
    }

    private static void ApplyPrintParameters(PrintManager manager, PrintParameters parameters, ExportProfile profile, ExportItem? item)
    {
        string requestedSize = !string.IsNullOrWhiteSpace(profile.PrinterPaperSize)
            ? profile.PrinterPaperSize
            : item?.DisplayPaperSize ?? string.Empty;
        PaperSize? paperSize = FindNamed(manager.PaperSizes.Cast<PaperSize>(), value => value.Name, requestedSize);
        if (paperSize != null) parameters.PaperSize = paperSize;

        PaperSource? source = FindNamed(manager.PaperSources.Cast<PaperSource>(), value => value.Name, profile.PrinterPaperSource);
        if (source != null) parameters.PaperSource = source;

        string orientation = item?.DisplayOrientation ?? profile.PaperOrientation;
        parameters.PageOrientation = orientation.Equals("Portrait", StringComparison.OrdinalIgnoreCase)
            ? PageOrientationType.Portrait
            : orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase) ? PageOrientationType.Landscape : PageOrientationType.Auto;
        parameters.PaperPlacement = profile.PaperPlacement.Equals("Offset", StringComparison.OrdinalIgnoreCase) ? PaperPlacementType.Margins : PaperPlacementType.Center;
        if (parameters.PaperPlacement == PaperPlacementType.Margins)
        {
            parameters.MarginType = MarginType.UserDefined;
            parameters.OriginOffsetX = UnitUtils.ConvertToInternalUnits(profile.OriginOffsetXmm, UnitTypeId.Millimeters);
            parameters.OriginOffsetY = UnitUtils.ConvertToInternalUnits(profile.OriginOffsetYmm, UnitTypeId.Millimeters);
        }
        parameters.ZoomType = profile.ZoomMode.Equals("Zoom", StringComparison.OrdinalIgnoreCase) ? ZoomType.Zoom : ZoomType.FitToPage;
        if (parameters.ZoomType == ZoomType.Zoom) parameters.Zoom = Math.Clamp(profile.ZoomPercentage, 1, 1000);
        parameters.HiddenLineViews = profile.AlwaysRaster ? HiddenLineViewsType.RasterProcessing : HiddenLineViewsType.VectorProcessing;
        parameters.RasterQuality = ParseEnum(profile.RasterQuality, RasterQualityType.High);
        parameters.ColorDepth = ParseEnum(profile.ColorMode, ColorDepthType.Color);
        parameters.ViewLinksinBlue = profile.ViewLinksInBlue;
        parameters.HideReforWorkPlanes = profile.HideReferencePlanes;
        parameters.HideUnreferencedViewTags = profile.HideUnreferencedTags;
        parameters.HideScopeBoxes = profile.HideScopeBoxes;
        parameters.HideCropBoundaries = profile.HideCropBoundaries;
        parameters.ReplaceHalftoneWithThinLines = profile.ReplaceHalftone;
        parameters.MaskCoincidentLines = profile.MaskCoincidentLines;
    }

    private static T? FindNamed<T>(IEnumerable<T> values, Func<T, string> name, string requested) where T : class
    {
        if (string.IsNullOrWhiteSpace(requested)) return null;
        string normalized = NormalizePaperName(requested);
        return values.FirstOrDefault(value => name(value).Equals(requested, StringComparison.OrdinalIgnoreCase))
            ?? values.FirstOrDefault(value => NormalizePaperName(name(value)).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                                              || normalized.Contains(NormalizePaperName(name(value)), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePaperName(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static PDFExportOptions CreatePdfOptions(ExportProfile profile, ExportItem? item)
    {
        string paperSize = item?.OutputPaperSize ?? (profile.KeepPaperSizeAndOrientation ? "Default" : profile.PaperSize);
        string orientation = item?.OutputOrientation ?? (profile.KeepPaperSizeAndOrientation ? "Auto" : profile.PaperOrientation);
        return new PDFExportOptions
        {
            StopOnError = false,
            AlwaysUseRaster = profile.AlwaysRaster,
            RasterQuality = ParseEnum(profile.RasterQuality, RasterQualityType.High),
            ColorDepth = ParseEnum(profile.ColorMode, ColorDepthType.Color),
            ViewLinksInBlue = profile.ViewLinksInBlue,
            HideReferencePlane = profile.HideReferencePlanes,
            HideUnreferencedViewTags = profile.HideUnreferencedTags,
            HideScopeBoxes = profile.HideScopeBoxes,
            HideCropBoundaries = profile.HideCropBoundaries,
            ReplaceHalftoneWithThinLines = profile.ReplaceHalftone,
            MaskCoincidentLines = profile.MaskCoincidentLines,
            PaperPlacement = profile.PaperPlacement.Equals("Offset", StringComparison.OrdinalIgnoreCase) ? PaperPlacementType.LowerLeft : PaperPlacementType.Center,
            OriginOffsetX = UnitUtils.ConvertToInternalUnits(profile.OriginOffsetXmm, UnitTypeId.Millimeters),
            OriginOffsetY = UnitUtils.ConvertToInternalUnits(profile.OriginOffsetYmm, UnitTypeId.Millimeters),
            ZoomType = profile.ZoomMode.Equals("Zoom", StringComparison.OrdinalIgnoreCase) ? ZoomType.Zoom : ZoomType.FitToPage,
            ZoomPercentage = Math.Clamp(profile.ZoomPercentage, 10, 400),
            PaperFormat = ParseEnum(paperSize, ExportPaperFormat.Default),
            PaperOrientation = ParseEnum(orientation, PageOrientationType.Auto)
        };
    }

    private static void PrepareViewModes(Document document, IReadOnlyList<ExportItem> items, ExportProfile profile)
    {
        if (document.IsReadOnly || document.IsModifiable) return;
        using var transaction = new Transaction(document, "MiraSHA Sheets - Prepare Views");
        try
        {
            transaction.Start();
            foreach (ExportItem item in items)
            {
                if (document.GetElement(item.Id) is not View view) continue;

                try
                {
                    if (profile.DisableTemporaryHideIsolate && view.IsTemporaryHideIsolateActive())
                    {
                        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    }
                }
                catch
                {
                }

                try
                {
                    if (profile.DisableWorksharingDisplay && view.GetWorksharingDisplayMode() != WorksharingDisplayMode.Off)
                    {
                        view.SetWorksharingDisplayMode(WorksharingDisplayMode.Off);
                    }
                }
                catch
                {
                }

                TemporaryViewModes? modes;
                try
                {
                    modes = view.TemporaryViewModes;
                }
                catch
                {
                    continue;
                }
                if (modes == null) continue;
                DisableModeIfRequested(modes, TemporaryViewMode.RevealHiddenElements, profile.DisableRevealHiddenElements);
                DisableModeIfRequested(modes, TemporaryViewMode.RevealConstraints, profile.DisableRevealConstraints);
            }
            transaction.Commit();
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
        }
    }

    private static void DisableModeIfRequested(TemporaryViewModes modes, TemporaryViewMode mode, bool requested)
    {
        if (!requested) return;
        try
        {
            if (modes.IsModeAvailable(mode) && modes.IsModeEnabled(mode) && modes.IsModeActive(mode))
            {
                modes.DeactivateMode(mode);
            }
        }
        catch
        {
        }
    }

    private static void ExportCad(Document document, ExportItem item, ExportProfile profile, ExportRunResult result, string format)
    {
        string folder = GetFormatFolder(profile, format);
        string fileName = GetFileName(document, item, profile);
        try
        {
            bool success;
            if (format == "DWG")
            {
                using DWGExportOptions options = profile.DwgSetup == "Default"
                    ? new DWGExportOptions()
                    : DWGExportOptions.GetPredefinedOptions(document, profile.DwgSetup) ?? new DWGExportOptions();
                options.MergedViews = profile.DwgMergedViews;
                success = document.Export(folder, fileName, new List<ElementId> { item.Id }, options);
                if (success && profile.DwgCleanPcp) CleanPcpFiles(folder, fileName);
            }
            else
            {
                using DGNExportOptions options = profile.DgnSetup == "Default"
                    ? new DGNExportOptions()
                    : DGNExportOptions.GetPredefinedOptions(document, profile.DgnSetup) ?? new DGNExportOptions();
                options.MergedViews = profile.DgnMergedViews;
                success = document.Export(folder, fileName, new List<ElementId> { item.Id }, options);
            }

            result.Add(format, DisplayName(item), Path.Combine(folder, fileName), success, success ? string.Empty : "Revit returned false.", item.UniqueId);
        }
        catch (Exception exception)
        {
            result.Add(format, DisplayName(item), Path.Combine(folder, fileName), false, exception.Message, item.UniqueId);
        }
    }

    private static void ExportDwf(
        Document document,
        IReadOnlyList<ExportItem> items,
        ExportProfile profile,
        ExportRunResult result,
        Action progress)
    {
        string folder = GetFormatFolder(profile, "DWF");
        bool dwfx = profile.DwfFileType.Equals("DWFX", StringComparison.OrdinalIgnoreCase);
        string extension = dwfx ? ".dwfx" : ".dwf";
        if (profile.CombineDwf)
        {
            var set = new ViewSet();
            foreach (ExportItem item in items)
            {
                if (document.GetElement(item.Id) is View view)
                {
                    set.Insert(view);
                }
            }

            string combinedName = string.IsNullOrWhiteSpace(profile.CombinedDwfName) ? "Sheets" : profile.CombinedDwfName;
            string fileName = Sanitize(Path.GetFileNameWithoutExtension(combinedName)) + extension;
            try
            {
                DWFExportOptions options = CreateDwfOptions(profile, null, true, dwfx);
                bool success = document.Export(folder, fileName, set, options);
                result.Add("DWF", "Combined", Path.Combine(folder, fileName), success, success ? string.Empty : "Revit returned false.");
            }
            catch (Exception exception)
            {
                result.Add("DWF", "Combined", Path.Combine(folder, fileName), false, exception.Message);
            }

            foreach (ExportItem _ in items) progress();
            return;
        }

        foreach (ExportItem item in items)
        {
            string fileName = GetFileName(document, item, profile) + extension;
            try
            {
                var set = new ViewSet();
                if (document.GetElement(item.Id) is View view)
                {
                    set.Insert(view);
                }

                DWFExportOptions options = CreateDwfOptions(profile, item, false, dwfx);
                bool success = document.Export(folder, fileName, set, options);
                result.Add("DWF", DisplayName(item), Path.Combine(folder, fileName), success, success ? string.Empty : "Revit returned false.", item.UniqueId);
            }
            catch (Exception exception)
            {
                result.Add("DWF", DisplayName(item), Path.Combine(folder, fileName), false, exception.Message, item.UniqueId);
            }

            progress();
        }
    }

    private static DWFExportOptions CreateDwfOptions(ExportProfile profile, ExportItem? item, bool merged, bool dwfx)
    {
        string paper = item?.OutputPaperSize ?? profile.PaperSize;
        string orientation = item?.OutputOrientation ?? profile.PaperOrientation;
        DWFExportOptions options = dwfx ? new DWFXExportOptions() : new DWFExportOptions();
        options.MergedViews = merged;
        options.StopOnError = !merged;
        options.CropBoxVisible = profile.DwfCropBoxVisible;
        options.ExportObjectData = profile.DwfExportObjectData;
        options.ExportTexture = profile.DwfExportTextures;
        options.ExportingAreas = profile.DwfExportAreas;
        options.ImageQuality = ParseEnum(profile.DwfImageQuality, DWFImageQuality.Medium);
        options.ImageFormat = ParseEnum(profile.DwfImageFormat, DWFImageFormat.Lossless);
        options.PortraitLayout = orientation.Equals("Portrait", StringComparison.OrdinalIgnoreCase);
        options.PaperFormat = ParseEnum(paper, ExportPaperFormat.Default);
        return options;
    }

    private static void ExportNwc(Document document, ExportItem? item, ExportProfile profile, ExportRunResult result)
    {
        string folder = GetFormatFolder(profile, "NWC");
        string fileName = item == null ? Sanitize(document.Title) : GetFileName(document, item, profile);
        if (item != null && !item.IsThreeDimensional)
        {
            result.Add("NWC", DisplayName(item), folder, false, "NWC export requires a selected 3D view.", item.UniqueId);
            return;
        }

        try
        {
            using var options = new NavisworksExportOptions
            {
                ExportScope = item == null ? NavisworksExportScope.Model : NavisworksExportScope.View,
                ViewId = item?.Id ?? ElementId.InvalidElementId,
                ExportLinks = profile.NwcExportLinks,
                ConvertElementProperties = profile.NwcElementProperties,
                ConvertLinkedCADFormats = profile.NwcConvertLinkedCad,
                ConvertLights = profile.NwcConvertLights,
                FacetingFactor = Math.Clamp(profile.NwcFacetingFactor, 0.01, 10),
                DivideFileIntoLevels = profile.NwcDivideIntoLevels,
                FindMissingMaterials = profile.NwcFindMissingMaterials,
                ExportRoomGeometry = profile.NwcRoomGeometry,
                Coordinates = ParseEnum(profile.NwcCoordinates, NavisworksCoordinates.Internal),
                ExportUrls = profile.NwcExportUrls,
                ExportRoomAsAttribute = profile.NwcRoomAsAttribute,
                Parameters = ParseEnum(profile.NwcParameters, NavisworksParameters.All),
                ExportElementIds = profile.NwcExportElementIds,
                ExportParts = profile.NwcExportParts
            };
            document.Export(folder, fileName, options);
            result.Add("NWC", item == null ? document.Title : DisplayName(item), Path.Combine(folder, fileName + ".nwc"), true, string.Empty, item?.UniqueId ?? string.Empty);
        }
        catch (Exception exception)
        {
            result.Add("NWC", item == null ? document.Title : DisplayName(item), Path.Combine(folder, fileName + ".nwc"), false, exception.Message, item?.UniqueId ?? string.Empty);
        }
    }

    private static void ExportIfc(Document document, IReadOnlyList<ExportItem> items, ExportProfile profile, ExportRunResult result)
    {
        string folder = GetFormatFolder(profile, "IFC");
        List<ExportItem> views = items.Where(item => item.IsThreeDimensional).ToList();
        if (views.Count == 0)
        {
            string fileName = Sanitize(document.Title);
            TryExportIfc(document, null, folder, fileName, profile, result);
            return;
        }

        foreach (ExportItem item in views)
        {
            TryExportIfc(document, item, folder, GetFileName(document, item, profile), profile, result);
        }
    }

    private static void TryExportIfc(Document document, ExportItem? item, string folder, string fileName, ExportProfile profile, ExportRunResult result)
    {
        Transaction? transaction = null;
        try
        {
            if (!document.IsModifiable)
            {
                transaction = new Transaction(document, "MiraSHA Sheets - IFC Export");
                transaction.Start();
            }
            using var options = new IFCExportOptions
            {
                ExportBaseQuantities = profile.IfcBaseQuantities,
                WallAndColumnSplitting = profile.IfcWallAndColumnSplitting,
                SpaceBoundaryLevel = Math.Clamp(profile.IfcSpaceBoundaryLevel, 0, 2),
                FileVersion = ParseEnum(profile.IfcVersion, IFCVersion.IFC2x3)
            };
            if (!string.IsNullOrWhiteSpace(profile.IfcFamilyMappingFile)) options.FamilyMappingFile = profile.IfcFamilyMappingFile;
            options.AddOption("Export2DElements", profile.IfcExport2DElements.ToString());
            options.AddOption("ExportLinkedFiles", profile.IfcExportLinkedFiles.ToString());
            options.AddOption("ExportPartsAsBuildingElements", profile.IfcExportPartsAsBuildingElements.ToString());
            options.AddOption("ExportInternalRevitPropertySets", profile.IfcExportInternalRevitPropertySets.ToString());
            options.AddOption("ExportIFCCommonPropertySets", profile.IfcExportIfcCommonPropertySets.ToString());
            options.AddOption("VisibleElementsOfCurrentView", profile.IfcVisibleElementsOnly.ToString());
            options.AddOption("UseActiveViewGeometry", profile.IfcUseActiveViewGeometry.ToString());
            options.AddOption("IFCFileType", profile.IfcFileType);
            options.AddOption("SitePlacement", profile.IfcSitePlacement);
            options.AddOption("IncludeSteelElements", profile.IfcIncludeSteelElements.ToString());
            options.AddOption("ExportRoomsInView", profile.IfcExportRoomsInView.ToString());
            options.AddOption("ExportSchedulesAsPsets", profile.IfcExportSchedulesAsPsets.ToString());
            options.AddOption("ExportSpecificSchedules", profile.IfcExportSpecificSchedules.ToString());
            options.AddOption("ExportUserDefinedPsets", profile.IfcExportUserDefinedPsets.ToString());
            options.AddOption("ExportUserDefinedPsetsFileName", profile.IfcUserDefinedPsetsFile);
            options.AddOption("ExportUserDefinedParameterMapping", profile.IfcExportParameterMapping.ToString());
            options.AddOption("ExportUserDefinedParameterMappingFileName", profile.IfcParameterMappingFile);
            options.AddOption("TessellationLevelOfDetail", profile.IfcTessellationLevel);
            options.AddOption("ExportSolidModelRep", profile.IfcExportSolidModelRep.ToString());
            options.AddOption("UseFamilyAndTypeNameForReference", profile.IfcUseFamilyAndTypeName.ToString());
            options.AddOption("Use2DRoomBoundaryForVolume", profile.IfcUse2dRoomBoundaries.ToString());
            options.AddOption("IncludeSiteElevation", profile.IfcIncludeSiteElevation.ToString());
            options.AddOption("StoreIFCGUID", profile.IfcStoreGuid.ToString());
            options.AddOption("ExportBoundingBox", profile.IfcExportBoundingBox.ToString());
            options.AddOption("UseOnlyTriangulation", profile.IfcUseOnlyTriangulation.ToString());
            options.AddOption("UseTypeNameOnlyForIfcType", profile.IfcUseTypeNameOnly.ToString());
            options.AddOption("UseVisibleRevitNameAsEntityName", profile.IfcUseVisibleName.ToString());
            if (!string.IsNullOrWhiteSpace(profile.IfcPhaseId)) options.AddOption("ActivePhaseId", profile.IfcPhaseId);
            if (!string.IsNullOrWhiteSpace(profile.IfcCategoryMapping)) options.AddOption("CategoryMapping", profile.IfcCategoryMapping);
            if (item != null)
            {
                options.FilterViewId = item.Id;
            }

            bool success = document.Export(folder, fileName, options);
            if (transaction?.GetStatus() == TransactionStatus.Started) transaction.Commit();
            result.Add("IFC", item == null ? document.Title : DisplayName(item), Path.Combine(folder, fileName + ".ifc"), success, success ? string.Empty : "Revit returned false.", item?.UniqueId ?? string.Empty);
        }
        catch (Exception exception)
        {
            if (transaction?.GetStatus() == TransactionStatus.Started) transaction.RollBack();
            result.Add("IFC", item == null ? document.Title : DisplayName(item), Path.Combine(folder, fileName + ".ifc"), false, exception.Message, item?.UniqueId ?? string.Empty);
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private static void ExportImage(Document document, ExportItem item, ExportProfile profile, ExportRunResult result)
    {
        string folder = GetFormatFolder(profile, "IMG");
        string fileName = GetFileName(document, item, profile);
        try
        {
            ImageFileType fileType = ParseEnum(profile.ImageFormat, ImageFileType.PNG);
            ImageFileType shadowFileType = ParseEnum(profile.ImageShadowFormat, fileType);
            using var options = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = Path.Combine(folder, fileName),
                HLRandWFViewsFileType = fileType,
                ShadowViewsFileType = shadowFileType,
                ImageResolution = ParseEnum(profile.ImageResolution, ImageResolution.DPI_300),
                FitDirection = ParseEnum(profile.ImageFitDirection, FitDirectionType.Horizontal),
                ZoomType = ParseEnum(profile.ImageZoomType, ZoomFitType.FitToPage),
                PixelSize = Math.Clamp(profile.ImagePixelSize, 100, 15000),
                Zoom = Math.Clamp(profile.ImageZoom, 1, 1000),
                ShouldCreateWebSite = profile.ImageCreateWebsite
            };
            options.SetViewsAndSheets(new List<ElementId> { item.Id });
            document.ExportImage(options);
            result.Add("IMG", DisplayName(item), Path.Combine(folder, fileName), true, string.Empty, item.UniqueId);
        }
        catch (Exception exception)
        {
            result.Add("IMG", DisplayName(item), Path.Combine(folder, fileName), false, exception.Message, item.UniqueId);
        }
    }

    private static void ExportImageWebsite(Document document, IReadOnlyList<ExportItem> items, ExportProfile profile, ExportRunResult result)
    {
        string folder = GetFormatFolder(profile, "IMG");
        string name = Sanitize(string.IsNullOrWhiteSpace(profile.ImageWebsiteName) ? "Images" : profile.ImageWebsiteName);
        string path = Path.Combine(folder, name);
        try
        {
            ImageFileType fileType = ParseEnum(profile.ImageFormat, ImageFileType.PNG);
            using var options = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = path,
                HLRandWFViewsFileType = fileType,
                ShadowViewsFileType = ParseEnum(profile.ImageShadowFormat, fileType),
                ImageResolution = ParseEnum(profile.ImageResolution, ImageResolution.DPI_300),
                FitDirection = ParseEnum(profile.ImageFitDirection, FitDirectionType.Horizontal),
                ZoomType = ParseEnum(profile.ImageZoomType, ZoomFitType.FitToPage),
                PixelSize = Math.Clamp(profile.ImagePixelSize, 100, 15000),
                Zoom = Math.Clamp(profile.ImageZoom, 1, 1000),
                ShouldCreateWebSite = true
            };
            options.SetViewsAndSheets(items.Select(item => item.Id).ToList());
            document.ExportImage(options);
            result.Add("IMG", "Combined website", path + ".htm", true, string.Empty);
        }
        catch (Exception exception)
        {
            result.Add("IMG", "Combined website", path, false, exception.Message);
        }
    }

    private static void ExportXml(Document document, IReadOnlyList<ExportItem> items, ExportProfile profile, ExportRunResult result)
    {
        string folder = GetFormatFolder(profile, "XML");
        string path = Path.Combine(folder, Sanitize(document.Title) + "_sheets.xml");
        try
        {
            if (profile.XmlOneFilePerItem)
            {
                foreach (ExportItem item in items)
                {
                    XElement itemRoot = CreateXmlRoot(document, profile);
                    itemRoot.Add(CreateXmlItem(document, item, profile));
                    string itemPath = Path.Combine(folder, EnsureExtension(GetFileName(document, item, profile), ".xml"));
                    new XDocument(itemRoot).Save(itemPath);
                    result.Add("XML", DisplayName(item), itemPath, true, string.Empty, item.UniqueId);
                }
                return;
            }

            XElement root = CreateXmlRoot(document, profile);

            foreach (ExportItem item in items) root.Add(CreateXmlItem(document, item, profile));

            new XDocument(root).Save(path);
            result.Add("XML", document.Title, path, true, string.Empty);
        }
        catch (Exception exception)
        {
            result.Add("XML", document.Title, path, false, exception.Message);
        }
    }

    private static XElement CreateXmlRoot(Document document, ExportProfile profile)
    {
        var root = new XElement("MiraSHASheets",
                new XAttribute("document", document.Title),
                new XAttribute("exportedAt", DateTimeOffset.Now.ToString("O")));
        if (profile.XmlIncludeProjectParameters)
        {
            var project = new XElement("ProjectParameters");
            foreach (Parameter parameter in document.ProjectInformation.Parameters.Cast<Parameter>().OrderBy(parameter => parameter.Definition?.Name))
            {
                string name = parameter.Definition?.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name)) project.Add(new XElement("Parameter", new XAttribute("name", name), new XAttribute("value", GetParameterValue(parameter))));
            }
            root.Add(project);
        }
        return root;
    }

    private static XElement CreateXmlItem(Document document, ExportItem item, ExportProfile profile)
    {
        Element? element = document.GetElement(item.Id);
        var node = new XElement(item.IsSheet ? "Sheet" : "View",
            new XAttribute("uniqueId", item.UniqueId), new XAttribute("number", item.Number),
            new XAttribute("name", item.Name), new XAttribute("type", item.Kind));
        if (element != null && profile.XmlIncludeParameters)
        {
            foreach (Parameter parameter in element.Parameters.Cast<Parameter>().OrderBy(parameter => parameter.Definition?.Name))
            {
                string name = parameter.Definition?.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name)) node.Add(new XElement("Parameter", new XAttribute("name", name), new XAttribute("value", GetParameterValue(parameter))));
            }
        }
        return node;
    }

    private static void CleanPcpFiles(string folder, string fileName)
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(folder, Path.GetFileNameWithoutExtension(fileName) + "*.pcp")) File.Delete(path);
        }
        catch
        {
        }
    }

    private static string GetParameterValue(Parameter parameter)
    {
        try
        {
            return parameter.AsValueString()
                   ?? parameter.StorageType switch
                   {
                       StorageType.String => parameter.AsString() ?? string.Empty,
                       StorageType.Integer => parameter.AsInteger().ToString(CultureInfo.InvariantCulture),
                       StorageType.Double => parameter.AsDouble().ToString(CultureInfo.InvariantCulture),
                       StorageType.ElementId => parameter.AsElementId().Value.ToString(CultureInfo.InvariantCulture),
                       _ => string.Empty
                   };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void WriteReport(ExportProfile profile, ExportRunResult result)
    {
        try
        {
            string path = Path.Combine(profile.OutputFolder, $"MiraSHA_Sheets_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var csv = new StringBuilder("Time,Format,Item,Success,Path,Message\r\n");
            foreach (ExportLogEntry entry in result.Entries)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(entry.Time.ToString("O")), Csv(entry.Format), Csv(entry.Item), Csv(entry.Success.ToString()), Csv(entry.Path), Csv(entry.Message)
                }));
            }

            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
            result.ReportPath = path;
        }
        catch
        {
        }
    }

    private static string GetFormatFolder(ExportProfile profile, string format)
    {
        string folder = profile.SplitByFormat ? Path.Combine(profile.OutputFolder, format) : profile.OutputFolder;
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string GetFileName(Document document, ExportItem item, ExportProfile profile) => NamingService.Build(document, item, profile);

    private static string DisplayName(ExportItem item) => string.IsNullOrWhiteSpace(item.Number) ? item.Name : $"{item.Number} - {item.Name}";

    private static string Sanitize(string value)
    {
        string result = value.Trim();
        foreach (char character in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(character, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "Export" : result.TrimEnd('.', ' ');
    }

    private static string EnsureExtension(string value, string extension) => value.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? value : value + extension;

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse(value, true, out T result) ? result : fallback;

    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}

public sealed class ExportRunResult
{
    public List<ExportLogEntry> Entries { get; } = new();
    public string? ReportPath { get; set; }
    public List<string> ReportPaths { get; } = new();
    public int SuccessCount => Entries.Count(entry => entry.Success);
    public int FailureCount => Entries.Count(entry => !entry.Success);

    public void Add(string format, string item, string path, bool success, string message, string itemUniqueId = "")
    {
        Entries.Add(new ExportLogEntry(DateTimeOffset.Now, format, item, path, success, message, itemUniqueId));
    }
}

public sealed record ExportLogEntry(DateTimeOffset Time, string Format, string Item, string Path, bool Success, string Message, string ItemUniqueId);
