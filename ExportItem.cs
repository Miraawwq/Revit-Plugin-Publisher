using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace KrakhmalovSheets;

public sealed class ExportItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _customFileName = string.Empty;
    private string _outputPaperSize = "Default";
    private string _outputOrientation = "Auto";
    private string _status = "Ready";
    private int _order;

    public ExportItem(Document document, View view)
    {
        Id = view.Id;
        UniqueId = view.UniqueId;
        IsSheet = view is ViewSheet;
        IsThreeDimensional = view is View3D;
        Kind = IsSheet ? "Sheet" : view.ViewType.ToString();
        Name = view.Name;
        Number = view is ViewSheet sheet ? sheet.SheetNumber : string.Empty;
        Revision = view is ViewSheet
            ? view.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? string.Empty
            : string.Empty;
        (Size, Orientation) = view is ViewSheet sizedSheet
            ? GetSheetInfo(sizedSheet)
            : (string.Empty, string.Empty);
        CanExport = view.CanBePrinted;
    }

    public ElementId Id { get; }

    public string UniqueId { get; }

    public bool IsSheet { get; }

    public bool IsThreeDimensional { get; }

    public bool CanExport { get; }

    public string Kind { get; }

    public string Number { get; }

    public string Name { get; }

    public string Revision { get; }

    public string Size { get; }

    public string Orientation { get; }

    public string OutputPaperSize
    {
        get => _outputPaperSize;
        set
        {
            if (_outputPaperSize == value) return;
            _outputPaperSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPaperSize));
        }
    }

    public string OutputOrientation
    {
        get => _outputOrientation;
        set
        {
            if (_outputOrientation == value) return;
            _outputOrientation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayOrientation));
        }
    }

    public string DisplayPaperSize => OutputPaperSize == "Default" ? Size : OutputPaperSize.Replace("ISO_", string.Empty).Replace("ANSI_", "ANSI ");

    public string DisplayOrientation => OutputOrientation == "Auto" ? Orientation : OutputOrientation;

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public int Order
    {
        get => _order;
        set
        {
            if (_order == value) return;
            _order = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string CustomFileName
    {
        get => _customFileName;
        set
        {
            if (_customFileName == value)
            {
                return;
            }

            _customFileName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static (string Size, string Orientation) GetSheetInfo(ViewSheet sheet)
    {
        try
        {
            BoundingBoxUV outline = sheet.Outline;
            double width = UnitUtils.ConvertFromInternalUnits(outline.Max.U - outline.Min.U, UnitTypeId.Millimeters);
            double height = UnitUtils.ConvertFromInternalUnits(outline.Max.V - outline.Min.V, UnitTypeId.Millimeters);
            string orientation = width >= height ? "Landscape" : "Portrait";
            string size = MatchIsoPaper(width, height) ?? $"{Math.Round(width)} x {Math.Round(height)} mm";
            return (size, orientation);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string? MatchIsoPaper(double width, double height)
    {
        double shortSide = Math.Min(width, height);
        double longSide = Math.Max(width, height);
        (string Name, double Short, double Long)[] formats =
        {
            ("A0", 841, 1189),
            ("A1", 594, 841),
            ("A2", 420, 594),
            ("A3", 297, 420),
            ("A4", 210, 297)
        };

        return formats.FirstOrDefault(format => Math.Abs(shortSide - format.Short) <= 8 && Math.Abs(longSide - format.Long) <= 8).Name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
