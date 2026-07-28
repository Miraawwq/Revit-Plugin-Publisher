using System.Collections;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace MiraSHA.Sheets;

public static class SchedulerService
{
    private static bool _running;
    private static DateTime _lastCheck = DateTime.MinValue;

    public static void Initialize(UIControlledApplication application)
    {
        application.Idling += OnIdling;
    }

    public static void Shutdown(UIControlledApplication application)
    {
        application.Idling -= OnIdling;
    }

    private static void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (_running || DateTime.Now - _lastCheck < TimeSpan.FromSeconds(30) || sender is not UIApplication uiApplication)
        {
            return;
        }

        _lastCheck = DateTime.Now;
        LocalSettings settings = LocalSettings.Load();
        List<ScheduledExportJob> dueJobs = settings.ScheduledJobs
            .Where(job => job.Enabled && job.NextRun <= DateTime.Now)
            .OrderBy(job => job.NextRun)
            .ToList();
        if (dueJobs.Count == 0)
        {
            return;
        }

        _running = true;
        try
        {
            List<Document> documents = ((IEnumerable)uiApplication.Application.Documents).Cast<Document>().ToList();
            foreach (ScheduledExportJob job in dueJobs)
            {
                Document? document = documents.FirstOrDefault(item => ExportService.GetDocumentKey(item) == job.DocumentKey);
                if (document == null || document.IsModifiable || document.IsReadOnly)
                {
                    continue;
                }

                List<ExportItem> items = job.ItemUniqueIds
                    .Select(document.GetElement)
                    .OfType<View>()
                    .Where(view => !view.IsTemplate && view.CanBePrinted)
                    .Select(view => new ExportItem(document, view) { IsSelected = true })
                    .ToList();
                if (items.Count == 0)
                {
                    job.Enabled = false;
                    continue;
                }

                try
                {
                    ExportRunResult result = ExportService.Execute(document, items, ExportService.CloneProfile(job.Profile));
                    job.LastRun = DateTime.Now;
                    job.RunCount++;
                    if (result.FailureCount > 0)
                    {
                        job.LastError = string.Join(" | ", result.Entries.Where(entry => !entry.Success).Take(5).Select(entry => $"{entry.Format}: {entry.Message}"));
                        if (job.Repeat == "Once") job.Enabled = false;
                        else Advance(job);
                        continue;
                    }

                    job.LastError = string.Empty;
                    Advance(job);
                }
                catch (Exception exception)
                {
                    job.LastError = exception.Message;
                    // Keep repeating jobs active so a transient export failure can be retried later.
                    if (job.Repeat == "Once") job.Enabled = false;
                }
            }

            settings.Save();
        }
        finally
        {
            _running = false;
        }
    }

    private static void Advance(ScheduledExportJob job)
    {
        switch (job.Repeat)
        {
            case "Daily":
                do job.NextRun = job.NextRun.AddDays(1); while (job.NextRun <= DateTime.Now);
                break;
            case "Weekly":
                if (job.Weekdays.Count == 0)
                {
                    do job.NextRun = job.NextRun.AddDays(7); while (job.NextRun <= DateTime.Now);
                }
                else
                {
                    do job.NextRun = job.NextRun.AddDays(1); while (job.NextRun <= DateTime.Now || !job.Weekdays.Contains(job.NextRun.DayOfWeek));
                }
                break;
            case "Monthly":
                do job.NextRun = job.NextRun.AddMonths(1); while (job.NextRun <= DateTime.Now);
                break;
            default:
                job.Enabled = false;
                break;
        }
    }
}
