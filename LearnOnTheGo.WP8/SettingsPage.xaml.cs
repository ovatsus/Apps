using System;
using System.Windows;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo.WP8
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
            ErrorReporting.Log("OnSaveClick");
            if (email.Text != Settings.GetString(Setting.Email))
            {
                Cache.DeleteAllFiles();
            }
            Settings.Set(Setting.Email, email.Text);
            Settings.Set(Setting.Password, password.Password);
            App.Crawler = null;
            NavigationService.GoBack();
        }

        private void OnCreateAccountClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnCreateAccountClick");
            var task = new WebBrowserTask();
            task.Uri = new Uri("https://accounts.coursera.org/signup");
            task.Show();
        }
    }
}