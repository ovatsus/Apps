using System.Linq;
using System.Windows.Controls;
using Microsoft.Phone.Shell;

#if WP8
using System;
using Windows.ApplicationModel.Store;
#endif

namespace UKTrains
{
    public partial class AdControl : UserControl
    {
        public AdControl()
        {
            InitializeComponent();
        }

        public static void InitAds(Grid grid, IApplicationBar applicationBar)
        {
            HideAds(grid, applicationBar);
#if WP8
            var showAds = true;//!CurrentApp.LicenseInformation.ProductLicenses["RemoveAds"].IsActive;
            if (showAds)
            {
                var menuItem = new ApplicationBarMenuItem("Remove ads");
                menuItem.Click += async delegate {
                    await CurrentApp.RequestProductPurchaseAsync("RemoveAds", false);
                    if (CurrentApp.LicenseInformation.ProductLicenses["RemoveAds"].IsActive)
                    {
                        HideAds(grid, applicationBar);
                    }
                };
                applicationBar.MenuItems.Add(menuItem);
            }
#else
            var showAds = true;
#endif

            if (showAds)
            {
                var adControl = new AdControl();
                adControl.adControl.AdError += (sender, args) => grid.Dispatcher.BeginInvoke(() => HideAds(grid, applicationBar));
                adControl.adControl.NoAd += (sender, args) => grid.Dispatcher.BeginInvoke(() => HideAds(grid, applicationBar));
                grid.Children.Add(adControl);
                grid.Width = 480;
                grid.Height = 80;
            }
            else
            {
                HideAds(grid, applicationBar);
            }                
        }

        private static void HideAds(Grid grid, IApplicationBar applicationBar)
        {
            var item = applicationBar.MenuItems.Cast<ApplicationBarMenuItem>().SingleOrDefault(i => i.Text == "Remove ads");
            if (item != null)
            {
                applicationBar.MenuItems.Remove(item);
            }
            grid.Children.Clear();
            grid.Width = 0;
            grid.Height = 0;
        }
    }
}
