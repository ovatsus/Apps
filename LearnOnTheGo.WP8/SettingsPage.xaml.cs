using System;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            email.Text = Settings.GetString(Setting.Email);
            password.Password = Settings.GetString(Setting.Password);
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnSaveClick");
            if (email.Text != Settings.GetString(Setting.Email))
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