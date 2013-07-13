using System;
using System.Windows.Navigation;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
#if WP8
using Windows.ApplicationModel.Store;
#endif

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

            AddMenuItem(applicationBar, "Rate and Review", () => new MarketplaceReviewTask().Show());

            AddMenuItem(applicationBar, "Give Feedback", () =>
            {
                var task = new EmailComposeTask
                {
                    To = "uktrains@codebeside.org",
                    Subject = "Feedback for UK Trains",
                    Body = LittleWatson.GetMailBody("")
                };
                task.Show();
            });

#if WP8
            AddMenuItem(applicationBar, "Donate", async () =>
            {
                try
                {
                    await CurrentApp.RequestProductPurchaseAsync("Donate", false);
                    if (CurrentApp.LicenseInformation.ProductLicenses["Donate"].IsActive)
                    {
                        CurrentApp.ReportProductFulfillment("Donate");
                    }
                }
                catch { }
            });
#endif
        }
    }
}
