using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace KrakhmalovSheets;

public sealed class App : IExternalApplication
{
    private const string TabName = "BIMLEADERS";
    private const string PanelName = "Sheets";

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch
            {
                // The tab can already exist when another BIMLEADERS tool is installed.
            }

            RibbonPanel panel = application.GetRibbonPanels(TabName)
                .FirstOrDefault(item => item.Name == PanelName)
                ?? application.CreateRibbonPanel(TabName, PanelName);

            var buttonData = new PushButtonData(
                "BIMLEADERS.Sheets.Export",
                "BIMLEADERS\nSheets",
                Assembly.GetExecutingAssembly().Location,
                typeof(ExportSheetsCommand).FullName);

            if (panel.AddItem(buttonData) is PushButton button)
            {
                button.ToolTip = "Batch export Revit sheets and views.";
                button.LongDescription = "Export sheets and views to PDF, CAD, DWF, NWC, IFC, images, and XML with local profiles and schedules.";
                button.LargeImage = LoadIcon("PublishIcon32.png");
                button.Image = LoadIcon("PublishIcon16.png");
            }

            SchedulerService.Initialize(application);

            return Result.Succeeded;
        }
        catch (System.Exception exception)
        {
            TaskDialog.Show("BIMLEADERS Sheets", exception.Message);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        SchedulerService.Shutdown(application);
        return Result.Succeeded;
    }

    private static BitmapImage LoadIcon(string fileName)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(
            $"pack://application:,,,/BIMLEADERS.Sheets;component/Assets/{fileName}",
            UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
