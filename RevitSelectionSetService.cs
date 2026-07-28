using Autodesk.Revit.DB;

namespace KrakhmalovSheets;

public static class RevitSelectionSetService
{
    public static void Create(Document document, string name, IEnumerable<ElementId> ids)
    {
        ViewSet views = BuildViewSet(document, ids);
        if (views.Size == 0) throw new InvalidOperationException("The selection does not contain printable views or sheets.");

        using var transaction = new Transaction(document, "BIMLEADERS Sheets - Create View/Sheet Set");
        transaction.Start();
        PrintManager manager = document.PrintManager;
        manager.PrintRange = PrintRange.Select;
        ViewSheetSetting setting = manager.ViewSheetSetting;
        setting.CurrentViewSheetSet.Views = views;
        if (!setting.SaveAs(name.Trim())) throw new InvalidOperationException("Revit could not create the View/Sheet Set.");
        transaction.Commit();
    }

    public static void Add(Document document, ViewSheetSet existing, IEnumerable<ElementId> ids)
    {
        var allIds = existing.Views.Cast<View>().Select(view => view.Id).Concat(ids).Distinct().ToList();
        ViewSet views = BuildViewSet(document, allIds);

        using var transaction = new Transaction(document, "BIMLEADERS Sheets - Update View/Sheet Set");
        transaction.Start();
        ViewSheetSetting setting = document.PrintManager.ViewSheetSetting;
        setting.CurrentViewSheetSet = existing;
        setting.CurrentViewSheetSet.Views = views;
        if (!setting.Save()) throw new InvalidOperationException("Revit could not update the View/Sheet Set.");
        transaction.Commit();
    }

    public static void Delete(Document document, ViewSheetSet existing)
    {
        using var transaction = new Transaction(document, "BIMLEADERS Sheets - Delete View/Sheet Set");
        transaction.Start();
        ViewSheetSetting setting = document.PrintManager.ViewSheetSetting;
        setting.CurrentViewSheetSet = existing;
        if (!setting.Delete()) throw new InvalidOperationException("Revit could not delete the View/Sheet Set.");
        transaction.Commit();
    }

    private static ViewSet BuildViewSet(Document document, IEnumerable<ElementId> ids)
    {
        var set = new ViewSet();
        foreach (ElementId id in ids)
        {
            if (document.GetElement(id) is View { IsTemplate: false, CanBePrinted: true } view) set.Insert(view);
        }

        return set;
    }
}
