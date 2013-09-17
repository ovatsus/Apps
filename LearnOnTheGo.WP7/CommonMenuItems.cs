using System;
using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo
{
    public static class CommonMenuItems
    {
        private static void AddButton(PhoneApplicationPage page, string text, string iconUri, Action action) 
        {
            var button = new ApplicationBarIconButton(new Uri("/Icons/dark/" + iconUri, UriKind.Relative)) { Text = text };
            button.Click += delegate { action(); };
            page.ApplicationBar.MenuItems.Add(button);
        }

        public static void NavigateToSettings(this PhoneApplicationPage page)
        {
            page.NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        public static void Init(PhoneApplicationPage page)
        {
            AddButton(page, "Settings", "appbar.settings.png", () => page.NavigateToSettings());

            AddButton(page, "Rate and Review", "appbar.star.png", () =>
            {
                new MarketplaceReviewTask().Show();
                Settings.Set(Setting.RatingDone, true);
            });

            AddButton(page, "Give Feedback", "appbar.reply.email.png", () =>
            {
                new EmailComposeTask
                {
                    To = "learnonthego@codebeside.org",
                    Subject = "Feedback for Learn On The Go " + LittleWatson.AppVersion,
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
                    var result = MessageBox.Show("Would you mind reviewing the Learn On The Go app?", "Rate and Review", MessageBoxButton.OKCancel);
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
