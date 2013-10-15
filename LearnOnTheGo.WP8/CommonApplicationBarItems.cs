using System;
using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo
{
    public static class CommonApplicationBarItems
    {
        public static void NavigateToSettings(this PhoneApplicationPage page)
        {
            page.NavigationService.Navigate(page.GetUri<SettingsPage>());
        }

        public static void Init(PhoneApplicationPage page)
        {
            if (!(page is SettingsPage))
            {
                var settingsMenuItem = new ApplicationBarMenuItem("Settings");
                settingsMenuItem.Click += delegate
                {
                    LittleWatson.Log("OnSettingsClick");
                    page.NavigateToSettings();
                };
                page.ApplicationBar.MenuItems.Add(settingsMenuItem);
            }

            var aboutButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/appbar.information.png", UriKind.Relative)) { Text = "About" };
            aboutButton.Click += delegate
            {
                LittleWatson.Log("OnAboutClick");
                page.NavigationService.Navigate(page.GetUri<AboutPage>());
            };
            page.ApplicationBar.Buttons.Insert(0, aboutButton);

            if (!(page is DownloadsPage))
            {
                var downloadsButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/appbar.download.png", UriKind.Relative)) { Text = "Video Downloads" };
                downloadsButton.Click += delegate
                {
                    LittleWatson.Log("OnVideoDownloadsClick");
                    page.NavigationService.Navigate(page.GetUri<DownloadsPage>());
                };
                page.ApplicationBar.Buttons.Add(downloadsButton);
            }

            var installationDate = Settings.GetDateTime(Setting.InstallationDate);
            if (!installationDate.HasValue)
            {
                Settings.Set(Setting.InstallationDate, DateTime.UtcNow);
            }
            else if (!Settings.GetBool(Setting.RatingDone))
            {
                if ((DateTime.UtcNow - installationDate.Value).TotalDays >= 1)
                {
                    var result = MessageBox.Show("Would you mind reviewing the " + App.Name + " app?", "Rate and Review", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK)
                    {
                        LittleWatson.Log("MarketplaceReviewTaskShow from Prompt");
                        new MarketplaceReviewTask().Show();
                    }
                    Settings.Set(Setting.RatingDone, true);
                }
            }
        }
    }
}
