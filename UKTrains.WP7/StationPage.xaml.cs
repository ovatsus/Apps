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

        private DeparturesTable departuresTable;
        private bool loadingDepartures;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var from = Stations.Get(NavigationContext.QueryString["station"]);
            var to = NavigationContext.QueryString.ContainsKey("callingAt") ? Stations.Get(NavigationContext.QueryString["callingAt"]) : null;
            departuresTable = DeparturesTable.Create(from, to);
            pivot.Title = departuresTable.ToString();

            loadingDepartures = true;
            LoadDepartures();

            if (departuresTable.HasDestinationFilter && e.NavigationMode == NavigationMode.New)
            {
                // remove the intermediate page to select the filter
                NavigationService.RemoveBackEntry();
            }

            while (ApplicationBar.Buttons.Count > 2)
            {
                // remove directions and pin to start
                ApplicationBar.Buttons.RemoveAt(ApplicationBar.Buttons.Count - 1);
            }

            ApplicationBar.MenuItems.Cast<ApplicationBarMenuItem>().Single(item => item.Text == "Clear filter").IsEnabled = departuresTable.HasDestinationFilter;

            CreateDirectionsItem();
            CreatePinToStartItem();
            CreateAds();
        }

        private void LoadDepartures()
        {
            loadingDepartures = true;
            bool refreshing = this.departures.ItemsSource != null;
            departuresTable.GetDepartures().Display(
                this,
                refreshing ? "Refreshing departures..." : "Loading departures...",
                refreshing,
                "No more departures from this station today",
                messageTextBlock,
                departures => this.departures.ItemsSource = departures,
                () => loadingDepartures = false);
        }

        private void CreateDirectionsItem()
        {
            bool createButton;
#if WP8
            var currentPosition = LocationService.CurrentPosition;
            if (currentPosition != null && !currentPosition.IsUnknown && Settings.GetBool(Setting.LocationServicesEnabled))
            {
                var routeQuery = new RouteQuery
                {
                    RouteOptimization = RouteOptimization.MinimizeTime,
                    TravelMode = TravelMode.Walking,
                    Waypoints = new[] { 
                    currentPosition, 
                    new GeoCoordinate(departuresTable.Station.Location.Lat, departuresTable.Station.Location.Long) },
                };

                routeQuery.QueryCompleted += OnRouteQueryCompleted;
                routeQuery.QueryAsync();
                createButton = false;
            }
            else
            {
                createButton = true;
            }
#else
            createButton = true;
#endif

            if (createButton)
            {
                var mapButton = new ApplicationBarIconButton(new Uri("/Icons/dark/appbar.map.png", UriKind.Relative))
                {
                    Text = "Directions",
                };
                mapButton.Click += OnDirectionsClick;
                ApplicationBar.Buttons.Add(mapButton);
            }
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
                        Content = departuresTable.Station.Name + " Station",
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

        private void OnDirectionsClick(object sender, EventArgs e)
        {
            var task = new BingMapsDirectionsTask();
            task.End = new LabeledMapLocation(departuresTable.Station.Name + " Station", new GeoCoordinate(departuresTable.Station.Location.Lat, departuresTable.Station.Location.Long));
            task.Show();
        }

        private void CreatePinToStartItem()
        {
            var uri = GetUri(departuresTable);
            var pinButton = new ApplicationBarIconButton(new Uri("/Icons/dark/appbar.pin.png", UriKind.Relative))
            {
                IsEnabled = !ShellTile.ActiveTiles.Any(tile => tile.NavigationUri == uri),
                Text = "Pin to Start",
            };
            pinButton.Click += OnPinClick;
            ApplicationBar.Buttons.Add(pinButton);
        }

        private void OnPinClick(object sender, EventArgs e)
        {
#if WP8
            var tileData = new FlipTileData()
            {
                Title = departuresTable.ToString(),
                SmallBackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileSmall.png", UriKind.Relative),
                BackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileMedium.png", UriKind.Relative),
                WideBackgroundImage = new Uri("/Assets/Tiles/FlipCycleTileLarge.png", UriKind.Relative),
            };
            ShellTile.Create(GetUri(departuresTable), tileData, true);
#else
            var tileData = new StandardTileData()
            {
                Title = departuresTable.ToString(),
                BackgroundImage = new Uri("Tile.png", UriKind.Relative),
            };
            ShellTile.Create(GetUri(departuresTable), tileData);
#endif
        }

        public static Uri GetUri(DeparturesTable departuresTable)
        {
            return new Uri(
                departuresTable.Match(
                    station => "/StationPage.xaml?station=" + station.Code,
                    (station, callingAt) => "/StationPage.xaml?station=" + station.Code + "&callingAt=" + callingAt.Code),
                UriKind.Relative);
        }

        private void CreateAds()
        {
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
                var currentPosition = LocationService.CurrentPosition;
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

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (!loadingDepartures)
            {
                LoadDepartures();
            }
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
            NavigationService.Navigate(new Uri("/MainAndFilterPage.xaml?fromStation=" + departuresTable.Station.Code, UriKind.Relative));
        }

        private void OnClearFilterClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(GetUri(departuresTable.WithoutFilter));
        }
    }
}