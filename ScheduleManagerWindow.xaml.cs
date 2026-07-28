using System.Collections.ObjectModel;
using System.Windows;

namespace MiraSHA.Sheets;

public partial class ScheduleManagerWindow : Window
{
    private readonly LocalSettings _settings;
    private readonly ObservableCollection<ScheduledExportJob> _jobs;

    public ScheduleManagerWindow(LocalSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _jobs = new ObservableCollection<ScheduledExportJob>(settings.ScheduledJobs.OrderBy(job => job.NextRun));
        JobsGrid.ItemsSource = _jobs;
    }

    private void DeleteJob(object sender, RoutedEventArgs e)
    {
        if (JobsGrid.SelectedItem is ScheduledExportJob job) _jobs.Remove(job);
    }

    private void SaveJobs(object sender, RoutedEventArgs e)
    {
        JobsGrid.CommitEdit();
        _settings.ScheduledJobs = _jobs.ToList();
        _settings.Save();
        DialogResult = true;
    }
}
