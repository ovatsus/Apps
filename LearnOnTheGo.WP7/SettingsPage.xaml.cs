using System;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            email.Text = Settings.Get(Setting.Email);
            password.Password = Settings.Get(Setting.Password);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            if (email.Text != Settings.Get(Setting.Email))
            {
                Cache.DeleteAllFiles();
            }
            Settings.Set(Setting.Email, email.Text);
            Settings.Set(Setting.Password, password.Password);
            App.Crawler = null;
            NavigationService.GoBack();
        }        
    }
}