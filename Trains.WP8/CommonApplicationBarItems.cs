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

            var aboutButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/dark/appbar.information.png", UriKind.Relative)) { Text = "About" };
            aboutButton.Click += delegate
            {
                ErrorReporting.Log("OnAboutClick");
                page.NavigationService.Navigate(page.GetUri<AboutPage>());
            };
            page.ApplicationBar.Buttons.Insert(0, aboutButton);
        }
    }
}
