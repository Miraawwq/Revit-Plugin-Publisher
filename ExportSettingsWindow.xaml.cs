using System.Windows;

namespace KrakhmalovSheets;

public partial class ExportSettingsWindow : Window
{
    public ExportSettingsWindow(ExportProfile profile)
    {
        InitializeComponent();
        DisableTemporaryHideRadio.IsChecked = profile.DisableTemporaryHideIsolate;
        KeepTemporaryHideRadio.IsChecked = !profile.DisableTemporaryHideIsolate;
        DisableWorksharingRadio.IsChecked = profile.DisableWorksharingDisplay;
        KeepWorksharingRadio.IsChecked = !profile.DisableWorksharingDisplay;
        DisableRevealHiddenRadio.IsChecked = profile.DisableRevealHiddenElements;
        KeepRevealHiddenRadio.IsChecked = !profile.DisableRevealHiddenElements;
        DisableRevealConstraintsRadio.IsChecked = profile.DisableRevealConstraints;
        KeepRevealConstraintsRadio.IsChecked = !profile.DisableRevealConstraints;
    }

    public bool DisableTemporaryHideIsolate => DisableTemporaryHideRadio.IsChecked == true;
    public bool DisableWorksharingDisplay => DisableWorksharingRadio.IsChecked == true;
    public bool DisableRevealHiddenElements => DisableRevealHiddenRadio.IsChecked == true;
    public bool DisableRevealConstraints => DisableRevealConstraintsRadio.IsChecked == true;

    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
