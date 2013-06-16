using System;
using Microsoft.Phone.Controls;

namespace UKTrains
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            enableLocationServices.IsChecked = Settings.GetBool(Setting.LocationServicesEnabled);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            Settings.Set(Setting.LocationServicesEnabled, enableLocationServices.IsChecked == true);
            NavigationService.GoBack();
        }
    }
}