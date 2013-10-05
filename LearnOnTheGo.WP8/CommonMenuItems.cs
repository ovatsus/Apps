using System;
using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo
{
    public static class CommonMenuItems
    {
        private static void AddMenuItem(PhoneApplicationPage page, string text, Action action) 
        {
            var menuItem = new ApplicationBarMenuItem(text);
            menuItem.Click += delegate { LittleWatson.Log("On" + text.Replace(" ", null) + "Click"); action(); };
            page.ApplicationBar.MenuItems.Add(menuItem);
        }

        public static void NavigateToSettings(this PhoneApplicationPage page)
        {
            page.NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        public static void Init(PhoneApplicationPage page)
        {
            AddMenuItem(page, "Settings", () => page.NavigateToSettings());

            AddMenuItem(page, "Rate And Review", () =>
            {
                new MarketplaceReviewTask().Show();
                Settings.Set(Setting.RatingDone, true);
            });

            AddMenuItem(page, "Give Feedback", () =>
            {
                new EmailComposeTask
                {
                    To = App.Email,
                    Subject = "Feedback for " + App.Name + " " + LittleWatson.AppVersion,
                    Body = LittleWatson.GetMailBody("")
                }.Show();
            });

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
