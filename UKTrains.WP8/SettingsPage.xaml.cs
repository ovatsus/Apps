using System;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Windows.System;

namespace UKTrains
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            enableLocationServices.IsChecked = Settings.GetBool(Setting.LocationServicesEnabled);
            useMilesInsteadOfKms.IsChecked = Settings.GetBool(Setting.UseMilesInsteadOfKMs);
        }

        private async void OnShowDeparturesOnLockScreenClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnShowDeparturesOnLockScreenClick");
            MessageBox.Show("Please select " + App.Name + " as the detailed status app in the notifications section", "Show departures on lock screen", MessageBoxButton.OK);
            await Launcher.LaunchUriAsync(new Uri("ms-settings-lock:"));
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnSaveClick");
            Settings.Set(Setting.LocationServicesEnabled, enableLocationServices.IsChecked == true);
            Settings.Set(Setting.UseMilesInsteadOfKMs, useMilesInsteadOfKms.IsChecked == true);
            LocationService.Setup();
            NavigationService.GoBack();
        }
    }
}