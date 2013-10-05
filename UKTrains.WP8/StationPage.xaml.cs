using System;
using System.Device.Location;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using FSharp.Control;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Maps;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Maps.Toolkit;
using Microsoft.Phone.Shell;
using NationalRail;
using Nokia.Phone.HereLaunchers;

namespace UKTrains
{
    public partial class StationPage : PhoneApplicationPage
    {
        public StationPage()
        {
            InitializeComponent();
            CommonApplicationBarItems.Init(this);
        }

        private DeparturesTable departuresTable;
        private LazyBlock<Departure> departuresLazyBlock;
        private LazyBlock<Departure> arrivalsLazyBlock;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (departuresTable != null)
            {
                LittleWatson.Log("departuresTable is not null");
                OnPivotSelectionChanged(null, null);
                return;
            }

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

            if (e.NavigationMode == NavigationMode.New)
            {
                RecentItems.Add(departuresTable);
            }

            if (removeBackEntry)
            {
                NavigationService.RemoveBackEntry();
            }

            if (departuresTable.HasDestinationFilter)
            {
                var filterOrClearFilterItem = ApplicationBar.Buttons.Cast<ApplicationBarIconButton>().Single(button => button.Text == "Filter");
                filterOrClearFilterItem.Text = "Clear Filter";
                filterOrClearFilterItem.IconUri = new Uri("/Assets/Icons/appbar.filter.clear.png", UriKind.RelativeOrAbsolute);

                var filterByAnotherDestinationItem = new ApplicationBarMenuItem("Filter by another destination");
                filterByAnotherDestinationItem.Click += OnFilterByAnotherDestinationClick;
                ApplicationBar.MenuItems.Insert(0, filterByAnotherDestinationItem);

                var reverseJourneyItem = new ApplicationBarMenuItem("Reverse journey");
                reverseJourneyItem.Click += OnReverseJourneyClick;
                ApplicationBar.MenuItems.Insert(1, reverseJourneyItem);
            }

            if (!NavigationService.CanGoBack)
            {
                var anotherRailStationItem = new ApplicationBarMenuItem("Another rail station");
                anotherRailStationItem.Click += OnAnotherRailStationClick;
                ApplicationBar.MenuItems.Insert(0, anotherRailStationItem);
            }

            GetPinToStartButton().IsEnabled = !IsStationPinnedToStart();

            CreateDirectionsPivotItem();
        }

        private void LoadDepartures()
        {
            if (departuresLazyBlock == null)
            {
                departuresLazyBlock = new LazyBlock<Departure>(
                    "departures",
                    "No more trains today",
                    departuresTable.GetDepartures(DepartureType.Departure),
                    new LazyBlockUI(this, departures, departuresMessageTextBlock, departuresLastUpdatedTextBlock),
                    true,
                    () => UpdateTiles(),
                    null);
            }
            else if (departures.ItemsSource == null)
            {
                departuresLazyBlock.Refresh();
            }
        }

        private void LoadArrivals()
        {
            if (arrivalsLazyBlock == null)
            {
                arrivalsLazyBlock = new LazyBlock<Departure>(
                    "arrivals",
                    "No more trains today",
                    departuresTable.GetDepartures(DepartureType.Arrival),
                    new LazyBlockUI(this, arrivals, arrivalsMessageTextBlock, arrivalsLastUpdatedTextBlock),
                    true,
                    null,
                    null);
            }
            else if (arrivals.ItemsSource == null)
            {
                arrivalsLazyBlock.Refresh();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (e.NavigationMode == NavigationMode.New && e.Uri.OriginalString == "app://external/")
            {
                //running in background
                return;
            }
            if (departuresLazyBlock != null)
            {
                departuresLazyBlock.Cancel();
            }
            if (arrivalsLazyBlock != null)
            {
                arrivalsLazyBlock.Cancel();
            }
        }

        private void UpdateTiles()
        {
            var primaryTile = ShellTile.ActiveTiles.First();
            primaryTile.Update(GetTileData(forPrimaryTile: true));

            var secondaryTileUri = GetUri(departuresTable, removeBackEntry: false);
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
                departuresTable.ToSmallString() +
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
                content = (forPrimaryTile ? departuresTableHeader + "\n" : (departuresTable.HasDestinationFilter ? "" : firstDeparture.Destination + "\n")) +
                          (firstDeparture.PlatformIsKnown ? "Platform " + firstDeparture.Platform.Value : "Platform not available") +
                          (forPrimaryTile ? "" : "\n" + firstDeparture.Due + " " + firstDeparture.Status);
                wideContent = departuresTableHeader + "\n" +
                              (firstDeparture.PlatformIsKnown ? "Platform " + firstDeparture.Platform.Value : "Platform not available") + " " +
                              firstDeparture.Due + " " + firstDeparture.Status;
            }

            return new FlipTileData
            {
                Title = forPrimaryTile ? App.Name : departuresTable.ToSmallString(),
                BackTitle = App.Name,
                BackContent = content,
                WideBackContent = wideContent,
                SmallBackgroundImage = new Uri("Assets/Tiles/FlipCycleTileSmall.png", UriKind.Relative),
                BackgroundImage = new Uri("Assets/Tiles/FlipCycleTileMedium.png", UriKind.Relative),
                WideBackgroundImage = new Uri("Assets/Tiles/FlipCycleTileLarge.png", UriKind.Relative),
            };
        }

        private void CreateDirectionsPivotItem()
        {
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
            }
        }

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
                    IsEnabled = false,
                    Center = center,
                    ZoomLevel = 16,
                    PedestrianFeaturesEnabled = true,
                    LandmarksEnabled = true,
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

                var pivotItem = new PivotItem
                {
                    Header = "Directions",
                    Content = map
                };
                pivotItem.Tap += delegate
                {
                    LittleWatson.Log("OnMapClick");
                    new DirectionsRouteDestinationTask
                    {
                        Origin = start,
                        Destination = end,
                        Mode = RouteMode.Pedestrian,
                    }.Show();
                };
                pivot.Items.Add(pivotItem);
            }
        }

        private ApplicationBarIconButton GetPinToStartButton()
        {
            return ApplicationBar.Buttons.Cast<ApplicationBarIconButton>().Single(button => button.Text == "Pin to Start");
        }

        private bool IsStationPinnedToStart()
        {
            var uri = GetUri(departuresTable, removeBackEntry: false);
            return ShellTile.ActiveTiles.Any(tile => tile.NavigationUri == uri);
        }

        private void OnPinToStartClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnPinToStartClick");
            var uri = GetUri(departuresTable, removeBackEntry: false);
            if (!IsStationPinnedToStart())
            {
                ShellTile.Create(GetUri(departuresTable, removeBackEntry: false), GetTileData(forPrimaryTile: false), true);
            }
            GetPinToStartButton().IsEnabled = false;
        }

        private Uri GetUri(DeparturesTable departuresTable, bool removeBackEntry)
        {
            return GetUri(this, departuresTable, removeBackEntry);
        }

        public static Uri GetUri(PhoneApplicationPage page, DeparturesTable departuresTable, bool removeBackEntry)
        {
            return page.GetUri<StationPage>().WithParameters("station", departuresTable.Station.Code)
                                             .WithParametersIf(departuresTable.HasDestinationFilter, () => "callingAt", () => departuresTable.CallingAt.Value.Code)
                                             .WithParametersIf(removeBackEntry, "removeBackEntry");
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRefreshClick");
            if (pivot.SelectedIndex != 1 && departuresLazyBlock != null)
            {
                departuresLazyBlock.Refresh();
            }
            if (pivot.SelectedIndex != 0 && arrivalsLazyBlock != null)
            {
                arrivalsLazyBlock.Refresh();
            }
        }

        private void OnFilterOrClearFilterClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnFilterOrClearFilterClick");
            if (departuresTable.HasDestinationFilter)
            {
                NavigationService.Navigate(GetUri(departuresTable.WithoutFilter, false));
            }
            else
            {
                NavigationService.Navigate(this.GetUri<MainAndFilterPage>().WithParameters("fromStation", departuresTable.Station.Code));
            }
        }

        private void OnFilterByAnotherDestinationClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnFilterByAnotherDestinationClick");
            NavigationService.Navigate(this.GetUri<MainAndFilterPage>().WithParameters("fromStation", departuresTable.Station.Code, "excludeStation", departuresTable.CallingAt.Value.Code));
        }

        private void OnReverseJourneyClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnReverseJourneyClick");
            NavigationService.Navigate(GetUri(departuresTable.Reversed, false));
        }

        private void OnAnotherRailStationClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRailStationsClick");
            NavigationService.Navigate(this.GetUri<MainAndFilterPage>());
        }

        private void OnLiveProgressClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnLiveProgressClick");
            var departure = (Departure)((Button)sender).DataContext;
            var title = departuresTable.Station.Name + " to " + departuresTable.Match(_ => departure.Destination, (_, destination) => destination.Name);
            LiveProgressPage.SetTarget(title, departure);
            NavigationService.Navigate(this.GetUri<LiveProgressPage>());
        }

        private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LittleWatson.Log("OnPivotSelectionChanged " + pivot.SelectedIndex);
            if (pivot.SelectedIndex == 0)
            {
                LoadDepartures();
            }
            if (pivot.SelectedIndex == 1)
            {
                LoadArrivals();
            }
        }
    }
}
