using System;
using Microsoft.Phone.Controls;
using System.Windows.Navigation;

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