using System;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace UKTrains
{
    public static class CommonMenuItems
    {
        private static void AddMenuItem(IApplicationBar applicationBar, string text, Action action) 
        {
            var menuItem = new ApplicationBarMenuItem(text);
            menuItem.Click += delegate { action(); };
            applicationBar.MenuItems.Add(menuItem);
        }

        public static void Init(IApplicationBar applicationBar, NavigationService navigationService)
        {
            AddMenuItem(applicationBar, "Settings", () => navigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative)));

            AddMenuItem(applicationBar, "Rate and Review", () =>
            {
                new MarketplaceReviewTask().Show();
                Settings.Set(Setting.RatingDone, true);
            });

            AddMenuItem(applicationBar, "Give Feedback", () =>
            {
                new EmailComposeTask
                {
                    To = "uktrains@codebeside.org",
                    Subject = "Feedback for UK Trains",
                    Body = LittleWatson.GetMailBody("")
                }.Show();
            });

            var installationDateStr = Settings.GetString(Setting.InstallationDate);
            if (installationDateStr == "")
            {
                Settings.Set(Setting.InstallationDate, DateTime.UtcNow.ToString());
            }
            else if (!Settings.GetBool(Setting.RatingDone))
            {
                var installationDate = DateTime.Parse(installationDateStr);
                if ((DateTime.UtcNow - installationDate).TotalDays >= 1)
                {
                    var result = MessageBox.Show("Would you mind reviewing the UK Trains app?", "Rate and Review", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.OK)
                    {
                        new MarketplaceReviewTask().Show();
                    }
                    Settings.Set(Setting.RatingDone, true);
                }                
            }
        }
    }
}
