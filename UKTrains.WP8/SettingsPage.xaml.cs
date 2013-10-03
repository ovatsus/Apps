using System;
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

        private async void OnShowPlatformOnLockScreenClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnShowPlatformOnLockScreenClick");
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