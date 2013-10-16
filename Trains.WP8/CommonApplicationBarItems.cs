using System;
using System.Windows;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace Trains.WP8
{
    public static class CommonApplicationBarItems
    {
        public static void Init(PhoneApplicationPage page)
        {
            if (!(page is SettingsPage))
            {
                var settingsMenuItem = new ApplicationBarMenuItem("Settings");
                settingsMenuItem.Click += delegate
                {
                    ErrorReporting.Log("OnSettingsClick");
                    page.NavigationService.Navigate(page.GetUri<SettingsPage>());
                };
                page.ApplicationBar.MenuItems.Add(settingsMenuItem);
            }

            var button = new ApplicationBarIconButton(new Uri("/Assets/Icons/appbar.information.png", UriKind.Relative)) { Text = "About" };
            button.Click += delegate
            {
                ErrorReporting.Log("OnAboutClick");
                page.NavigationService.Navigate(page.GetUri<AboutPage>());
            };
            page.ApplicationBar.Buttons.Insert(0, button);

            var installationDate = Settings.GetDateTime(Setting.InstallationDate);
            if (!installationDate.HasValue)
            {
                Settings.Set(Setting.InstallationDate, DateTime.UtcNow);
            }
            else if (!Settings.GetBool(Setting.RatingDone))
            {
                if ((DateTime.UtcNow - installationDate.Value).TotalDays >= 1)
                {
                    var result = MessageBox.Show("Would you mind reviewing the " + AppMetadata.Current.Name + " app?", "Rate and Review", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK)
                    {
                        ErrorReporting.Log("MarketplaceReviewTaskShow from Prompt");
                        new MarketplaceReviewTask().Show();
                    }
                    Settings.Set(Setting.RatingDone, true);
                }
            }
        }
    }
}
