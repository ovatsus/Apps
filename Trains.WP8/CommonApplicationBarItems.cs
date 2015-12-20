using System;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

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

            var aboutMenuItem = new ApplicationBarMenuItem("About");
            aboutMenuItem.Click += delegate
            {
                ErrorReporting.Log("OnAboutClick");
                page.NavigationService.Navigate(page.GetUri<AboutPage>());
            };
            page.ApplicationBar.MenuItems.Add(aboutMenuItem);
        }
    }
}
