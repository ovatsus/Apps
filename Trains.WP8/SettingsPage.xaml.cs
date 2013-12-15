using System;
using System.Windows;
using System.Windows.Navigation;
using Common.WP8;
using Microsoft.Phone.Controls;
using Windows.System;

namespace Trains.WP8
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            enableLocationServices.IsChecked = Settings.GetBool(Setting.LocationServicesEnabled);
            autoRefresh.IsChecked = Settings.GetBool(Setting.AutoRefresh);
            pivot.Title = AppMetadata.Current.Name;
        }

        private async void OnShowDeparturesOnLockScreenClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnShowDeparturesOnLockScreenClick");
            MessageBox.Show("Please select " + AppMetadata.Current.Name + " as the detailed status app in the notifications section", "Show departures on lock screen", MessageBoxButton.OK);
            await Launcher.LaunchUriAsync(new Uri("ms-settings-lock:"));
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnSaveClick");
            Settings.Set(Setting.LocationServicesEnabled, enableLocationServices.IsChecked == true);
            Settings.Set(Setting.AutoRefresh, autoRefresh.IsChecked == true);
            LocationService.Setup();
            NavigationService.GoBack();
        }
    }
}