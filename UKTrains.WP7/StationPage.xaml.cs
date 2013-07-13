using System;
using System.Device.Location;
using System.Linq;
using System.Threading;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using NationalRail;
#if WP8
using Microsoft.Phone.Maps;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Maps.Toolkit;
using System.Windows;
using Windows.System;
#endif

namespace UKTrains
{
    public partial class StationPage : PhoneApplicationPage
    {
        public StationPage()
        {
            InitializeComponent();
#if WP8
            AddLockScreenItem();
#endif
            CommonMenuItems.Init(ApplicationBar, NavigationService);
        }

        private DeparturesTable departuresTable;
        private DispatcherTimer refreshTimer;
        private CancellationTokenSource departuresCts;
        private CancellationTokenSource arrivalsCts;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string stationStr;
            if (!NavigationContext.QueryString.TryGetValue("station", out stationStr))
            {
                stationStr = NavigationContext.QueryString["stationCode"]; // old version
            }
            var from = Stations.Get(stationStr);
            var to = NavigationContext.QueryString.ContainsKey("callingAt") ? Stations.Get(NavigationContext.QueryString["callingAt"]) : null;
            var removeBackEntry = NavigationContext.QueryString.ContainsKey("removeBackEntry");
            departuresTable = DeparturesTable.Create(from, to);
            pivot.Title = departuresTable.ToString();

            LoadDepartures();

            if (removeBackEntry)
            {
                NavigationService.RemoveBackEntry();
            }

            while (ApplicationBar.Buttons.Count > 2)
            {
                // remove directions and pin to start
                ApplicationBar.Buttons.RemoveAt(ApplicationBar.Buttons.Count - 1);
            }

            while (pivot.Items.Count > 2)
            {
                // remove directions
                pivot.Items.RemoveAt(pivot.Items.Count - 1);
            }

            ApplicationBar.MenuItems.Cast<ApplicationBarMenuItem>().Single(item => item.Text == "Clear filter").IsEnabled = departuresTable.HasDestinationFilter;
            CreateDirectionsItem();
            CreatePinToStartItem();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
#if WP8
            if (e.NavigationMode == NavigationMode.New && e.Uri.OriginalString == "app://external/")
            {
                //running in background
                return;
            }
#endif
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }
            if (departuresCts != null)
            {
                departuresCts.Cancel();
                departuresCts = null;
            }
            if (arrivalsCts != null)
            {
                arrivalsCts.Cancel();
                arrivalsCts = null;
            }
        }

        private void LoadDepartures()
        {
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }
            bool refreshing = this.departures.ItemsSource != null;
            departuresCts = departuresTable.GetDepartures(DepartureType.Departure).Display(
                this,
                refreshing ? "Refreshing departures..." : "Loading departures...",
                refreshing,
                "No more trains today",
                departuresMessageTextBlock,
                UpdateDepartures,
                () => {
                    departuresCts = null;
                    LoadArrivals();
                });
        }

        private void LoadArrivals()
        {
            bool refreshing = this.arrivals.ItemsSource != null;
            arrivalsCts = departuresTable.GetDepartures(DepartureType.Arrival).Display(
                this,
                refreshing ? "Refreshing arrivals..." : "Loading arrivals...",
                refreshing,
                "No more trains today",
                arrivalsMessageTextBlock,
                arrivals => this.arrivals.ItemsSource = arrivals,
                () => arrivalsCts = null);
        }

        private void UpdateDepartures(Departure[] departures)
        {
            this.departures.ItemsSource = departures;

            if (refreshTimer != null)
            {
                refreshTimer.Stop();
            }
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(60);
            refreshTimer.Tick += (sender, args) => LoadDepartures();
            refreshTimer.Start();

            UpdateTiles();
        }

        private void UpdateTiles()
        {
            var primaryTile = ShellTile.ActiveTiles.First();
            primaryTile.Update(GetTileData(forPrimaryTile: true));

            var secondaryTileUri = GetUri(departuresTable, false);
            var secondaryTile = ShellTile.ActiveTiles.FirstOrDefault(tile => tile.NavigationUri == secondaryTileUri);
            if (secondaryTile != null)
            {
                secondaryTile.Update(GetTileData(forPrimaryTile: false));
            }                
        }

        private ShellTileData GetTileData(bool forPrimaryTile)
        {
            var firstDeparture = departures.ItemsSource == null ? null : departures.ItemsSource.Cast<Departure>().FirstOrDefault();

            var departuresTableHeader =
                departuresTable.ToString() +
                (departuresTable.HasDestinationFilter || firstDeparture == null ? "" : " to " + firstDeparture.Destination);

            string content;
            string wideContent;
            if (firstDeparture == null)
            {
                content = (forPrimaryTile ? departuresTableHeader + "\n" : "") + 
                          "No more trains today";
                wideContent = departuresTableHeader + "\n" + "No more trains today";
            }
            else
            {
                content = (forPrimaryTile ? departuresTableHeader + "\n" : "") +
                          (firstDeparture.PlatformIsKnown ? "Platform " + firstDeparture.Platform.Value : "Platform not available") +
                          (forPrimaryTile ? "" : "\n" + firstDeparture.Due + " " + firstDeparture.Status);
                wideContent = departuresTableHeader + "\n" +
                              (firstDeparture.PlatformIsKnown ? "Platform " + firstDeparture.Platform.Value : "Platform not available") + " " +
                              firstDeparture.Due + " " + firstDeparture.Status;
            }

#if WP8
            return new FlipTileData
            {
                Title = forPrimaryTile ? "UK Trains" : departuresTable.ToString(),
                BackTitle = "UK Trains",
                BackContent = content,
                WideBackContent = wideContent,
                SmallBackgroundImage = new Uri("Assets/Tiles/FlipCycleTileSmall.png", UriKind.Relative),
                BackgroundImage = new Uri("Assets/Tiles/FlipCycleTileMedium.png", UriKind.Relative),
                WideBackgroundImage = new Uri("Assets/Tiles/FlipCycleTileLarge.png", UriKind.Relative),
            };
#else
            return new StandardTileData
            {
                Title = forPrimaryTile ? "UK Trains" : departuresTable.ToString(),
                BackTitle = "UK Trains",
                BackContent = content,
                BackgroundImage = new Uri("Tile.png", UriKind.Relative),
            };
#endif
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
            var uri = GetUri(departuresTable, false);
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
            CreateTile(GetUri(departuresTable, false), GetTileData(forPrimaryTile: false));
        }

        private void CreateTile(Uri uri, ShellTileData tileData)
        {
#if WP8
            ShellTile.Create(uri, tileData, true);
#else
            ShellTile.Create(uri, tileData);
#endif
        }

        public static Uri GetUri(DeparturesTable departuresTable, bool removeBackEntry)
        {
            return new Uri(
                departuresTable.Match(
                    station => "/StationPage.xaml?station=" + station.Code,
                    (station, callingAt) => "/StationPage.xaml?station=" + station.Code + "&callingAt=" + callingAt.Code)
                + (removeBackEntry ? "&removeBackEntry" : ""),
                UriKind.Relative);
        }

#if WP8
        private void AddLockScreenItem() 
        {
            var menuItem = new ApplicationBarMenuItem("Show departures on lock screen");
            menuItem.Click += OnShowPlatformOnLockScreenClick;
            ApplicationBar.MenuItems.Add(menuItem);
        }

        private async void OnShowPlatformOnLockScreenClick(object sender, EventArgs e) 
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings-lock:"));
        }
#endif

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (departuresCts == null && arrivalsCts == null)
            {
                LoadDepartures();
            }
        }       

        private void OnFilterClick(object sender, EventArgs e)
        {
            var uri = "/MainAndFilterPage.xaml?fromStation=" + departuresTable.Station.Code;
            if (departuresTable.HasDestinationFilter)
            {
                uri += "&excludeStation=" + departuresTable.CallingAt.Value.Code;
            }
            NavigationService.Navigate(new Uri(uri, UriKind.Relative));
        }

        private void OnClearFilterClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(GetUri(departuresTable.WithoutFilter, false));
        }
    }
}