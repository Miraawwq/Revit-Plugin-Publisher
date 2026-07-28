using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MiraSHA.Sheets;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class ExportSheetsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Document? document = commandData.Application.ActiveUIDocument?.Document;
        if (document == null || document.IsFamilyDocument)
        {
            TaskDialog.Show("MiraSHA Sheets", "Open a Revit project before exporting sheets.");
            return Result.Cancelled;
        }

        List<ViewSheet> sheets = new FilteredElementCollector(document)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Where(sheet => !sheet.IsPlaceholder)
            .OrderBy(sheet => sheet.SheetNumber)
            .ThenBy(sheet => sheet.Name)
            .ToList();

        if (sheets.Count == 0)
        {
            TaskDialog.Show("MiraSHA Sheets", "The active project does not contain printable sheets.");
            return Result.Cancelled;
        }

        var window = new ExportWindow(document, sheets);
        new WindowInteropHelper(window)
        {
            Owner = commandData.Application.MainWindowHandle
        };
        window.ShowDialog();
        return Result.Succeeded;
    }
}
