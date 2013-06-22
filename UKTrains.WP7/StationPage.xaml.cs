using Inneractive.Ad;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using NationalRail;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Windows.Navigation;
#if WP8
using Microsoft.Phone.Maps;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Maps.Toolkit;
using System.Windows;
using Windows.ApplicationModel.Store;
#endif

namespace UKTrains
{
    public partial class StationPage : PhoneApplicationPage
    {
        public StationPage()
        {
            InitializeComponent();
        }

        private Station station;
        private Station callingAt;
        private bool loadingDepartures;

        private Uri GetUriForThisPage()
        {
            return callingAt == null ?
                new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative) :
                new Uri("/StationPage.xaml?stationCode=" + station.Code + "&callingAt=" + callingAt.Name, UriKind.Relative);
        }

        private string GetTitle()
        {
            return station.Name + (callingAt == null ? "" : " calling at " + callingAt.Name);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            station = LiveDepartures.getStation(NavigationContext.QueryString["stationCode"]);
            callingAt = NavigationContext.QueryString.ContainsKey("callingAt") ? LiveDepartures.getStation(NavigationContext.QueryString["callingAt"]) : null;

            loadingDepartures = true;
            LoadDepartures();

            pivot.Title = GetTitle();

            if (callingAt != null && e.NavigationMode == NavigationMode.New)
            {
                NavigationService.RemoveBackEntry();
            }

            if (ApplicationBar.MenuItems.Count == 2)
            {
                ApplicationBar.MenuItems.RemoveAt(1);
            }

            var currentPosition = LocationService.CurrentPosition;

            bool createDirectionsButton;
#if WP8
            if (currentPosition != null && !currentPosition.IsUnknown && Settings.GetBool(Setting.LocationServicesEnabled))
            {
                var routeQuery = new RouteQuery
                {
                    RouteOptimization = RouteOptimization.MinimizeTime,
                    TravelMode = TravelMode.Walking,
                    Waypoints = new[] { 
                    currentPosition, 
                    new GeoCoordinate(station.LatLong.Lat, station.LatLong.Long) },
                };

                routeQuery.QueryCompleted += OnRouteQueryCompleted;
                routeQuery.QueryAsync();
                createDirectionsButton = false;
            }
            else
            {
                createDirectionsButton = true;
            }
#else
            createDirectionsButton = true;
#endif
            while (ApplicationBar.Buttons.Count > 2)
            {
                // remove directions and pin to start
                ApplicationBar.Buttons.RemoveAt(ApplicationBar.Buttons.Count - 1);
            }
            if (createDirectionsButton)
            {
                var mapButton = new ApplicationBarIconButton(new Uri("/Icons/dark/appbar.map.png", UriKind.Relative))
                {
                    Text = "Directions",
                };
                mapButton.Click += OnDirectionsClick;
                ApplicationBar.Buttons.Add(mapButton);
            }

            var uri = GetUriForThisPage();
            var pinButton = new ApplicationBarIconButton(new Uri("/Icons/dark/appbar.pin.png", UriKind.Relative))
            {
                IsEnabled = !ShellTile.ActiveTiles.Any(tile => tile.NavigationUri == uri),
                Text = "Pin to Start",
            };
            pinButton.Click += OnPinClick;
            ApplicationBar.Buttons.Add(pinButton);

            var showAds = false;
#if WP8
            //var showAds = !CurrentApp.LicenseInformation.ProductLicenses["RemoveAds"].IsActive;
            if (showAds)
            {
                var menuItem = new ApplicationBarMenuItem("Remove ads");
                menuItem.Click += async delegate {
                    await CurrentApp.RequestProductPurchaseAsync("RemoveAds", false);
                    if (CurrentApp.LicenseInformation.ProductLicenses["RemoveAds"].IsActive)
                    {
                        // remove buy option
                        ApplicationBar.MenuItems.RemoveAt(ApplicationBar.Buttons.Count - 1);
                    }
                };
                ApplicationBar.MenuItems.Add(menuItem);
            }
#else
            //var showAds = true;
#endif            

            if (showAds)
            {
                var parameters = new Dictionary<InneractiveAd.IaOptionalParams, string>();
                if (currentPosition != null && !currentPosition.IsUnknown && Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    parameters.Add(InneractiveAd.IaOptionalParams.Key_Gps_Coordinates, currentPosition.Latitude.ToString("0.0000") + "," + currentPosition.Longitude.ToString("0.0000"));
                }
                parameters.Add(InneractiveAd.IaOptionalParams.Key_OptionalAdWidth, "480");
                parameters.Add(InneractiveAd.IaOptionalParams.Key_OptionalAdHeight, "80");
                parameters.Add(InneractiveAd.IaOptionalParams.Key_Location, "UK");
                adGrid.Width = 480;
                adGrid.Height = 80;
                InneractiveAd.DisplayAd("CodeBeside_UKTrains_WP", InneractiveAd.IaAdType.IaAdType_Text, adGrid, 60, parameters);
            }
            else
            {
                adGrid.Width = 0;
                adGrid.Height = 0;
            }
        }

        private void LoadDepartures()
        {
            loadingDepartures = true;
            bool refreshing = this.departures.ItemsSource != null;
            LiveDepartures.getDepartures(callingAt, station).Display(
                this,
                refreshing ? "Refreshing departures..." : "Loading departures...",
                refreshing,
                "No more departures from this station today",
                messageTextBlock,
                departures => this.departures.ItemsSource = departures,
                () => loadingDepartures = false);
        }

#if WP8
        private void OnRouteQueryCompleted(object sender, QueryCompletedEventArgs<Route> e)
        {
            if (e.Error == null)
            {
                var geometry = e.Result.Legs[0].Geometry;
                var start = geometry[0];
                var end = geometry[geometry.Count - 1];
                var center = new GeoCoordinate((start.Latitude + end.Latitude) / 2, (start.Longitude + end.Longitude) / 2);
                var map = new Map
                {
                    Center = center,
                    ZoomLevel = 15,
                    PedestrianFeaturesEnabled = true,
                };
                map.Loaded += delegate
                {
                    MapsSettings.ApplicationContext.ApplicationId = "ef62d461-861c-4a9f-9198-8768532cc6aa";
                    MapsSettings.ApplicationContext.AuthenticationToken = "r4y7eZta5Pa32rpsho9CFA";
                };
                var mapLayer = new MapLayer();
                mapLayer.Add(new MapOverlay
                {
                    GeoCoordinate = start,
                    PositionOrigin = new Point(0, 1),
                    Content = new Pushpin
                    {
                        Content = "Me",
                    }
                });
                mapLayer.Add(new MapOverlay
                {
                    GeoCoordinate = end,
                    PositionOrigin = new Point(0, 1),
                    Content = new Pushpin
                    {
                        Content = station.Name + " Station",
                    }
                });
                map.Layers.Add(mapLayer);

                map.AddRoute(new MapRoute(e.Result));

                pivot.Items.Add(new PivotItem
                {
                    Header = "Directions",
                    Content = map
                });
            }
        }
#endif
        
        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (!loadingDepartures)
            {
                LoadDepartures();
            }
        }

        private void OnDirectionsClick(object sender, EventArgs e)
        {
            var task = new BingMapsDirectionsTask();
            task.End = new LabeledMapLocation(station.Name + " Station", new GeoCoordinate(station.LatLong.Lat, station.LatLong.Long));
            task.Show();
        }

        private void OnPinClick(object sender, EventArgs e)
        {
#if WP8
            var tileData = new FlipTileData()
            {
                Title = GetTitle(),
                SmallBackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileSmall.png", UriKind.Relative),
                BackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileMedium.png", UriKind.Relative),
                WideBackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileLarge.png", UriKind.Relative),
            };
            ShellTile.Create(GetUriForThisPage(), tileData, true);
#else
            var tileData = new StandardTileData()
            {
                Title = GetTitle(),
                BackgroundImage = new Uri("Tile.png", UriKind.Relative),
            };
            ShellTile.Create(GetUriForThisPage(), tileData);
#endif
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private void OnRateAndReviewClick(object sender, EventArgs e)
        {
            var task = new MarketplaceReviewTask();
            task.Show();
        }

        private void OnGiveFeedbackClick(object sender, EventArgs e)
        {
            var task = new EmailComposeTask
            {
                To = "uktrains@codebeside.org",
                Subject = "Feedback for UK Trains",
                Body = "Put your feedback here"
            };
            task.Show();
        }

        private void OnFilterClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/MainAndFilterPage.xaml?fromStation=" + station.Code, UriKind.Relative));
        }

        private void OnClearFilterClick(object sender, EventArgs e)
        {
            var uri = new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative);
            NavigationService.Navigate(uri);
        }
    }
}