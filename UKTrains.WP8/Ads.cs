using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Inneractive.Nokia.Ad;
using Microsoft.Phone.Shell;
using Windows.ApplicationModel.Store;

namespace UKTrains
{
    public static class Ads
    {        
        public static void Init(Grid grid, IApplicationBar applicationBar)
        {
            HideAds(grid, applicationBar);
#if DEBUG
            var showAds = true;
#else
            var showAds = !CurrentApp.LicenseInformation.ProductLicenses["RemoveAds"].IsActive;
#endif
            if (showAds)
            {
                var menuItem = new ApplicationBarMenuItem("Remove ads");
                menuItem.Click += async delegate
                {
                    await CurrentApp.RequestProductPurchaseAsync("RemoveAds", false);
                    if (CurrentApp.LicenseInformation.ProductLicenses["RemoveAds"].IsActive)
                    {
                        HideAds(grid, applicationBar);
                    }
                };
                applicationBar.MenuItems.Add(menuItem);
            }

            if (showAds)
            {
                var parameters = new Dictionary<InneractiveAd.IaOptionalParams, string>();
                var currentPosition = LocationService.CurrentPosition;
                if (currentPosition != null && !currentPosition.IsUnknown && Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    parameters.Add(InneractiveAd.IaOptionalParams.Key_Gps_Coordinates, currentPosition.Latitude.ToString("0.0000") + "," + currentPosition.Longitude.ToString("0.0000"));
                }
                parameters.Add(InneractiveAd.IaOptionalParams.Key_OptionalAdWidth, "480");
                parameters.Add(InneractiveAd.IaOptionalParams.Key_OptionalAdHeight, "80");
                parameters.Add(InneractiveAd.IaOptionalParams.Key_Location, "UK");
                grid.Width = 480;
                grid.Height = 80;
                var eventHandlers = new Handlers(() => HideAds(grid, applicationBar));
                InneractiveAd.DisplayAd("CodeBeside_UKTrains_WP", InneractiveAd.IaAdType.IaAdType_Text, grid, 60, parameters, eventHandlers);
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

        private class Handlers : IaAdEventHandlers
        {
            private readonly Action _hideAds;

            public Handlers(Action hideAds)
            {
                _hideAds = hideAds;
            }

            public override void AdClickedEventHandler(object sender)
            {
            }

            public override void AdFailedEventHandler(object sender)
            {
                _hideAds();
            }

            public override void AdReceivedEventHandler(object sender)
            {
            }

            public override void DefaultAdReceivedEventHandler(object sender)
            {
            }
        }
    }
}
