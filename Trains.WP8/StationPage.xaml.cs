using System;
using System.Device.Location;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Common.WP8;
using FSharp.Control;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Maps;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Maps.Toolkit;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Nokia.Phone.HereLaunchers;

namespace Trains.WP8
{
    public partial class StationPage : PhoneApplicationPage
    {
        private readonly int mapIndex;

        public StationPage()
        {
            InitializeComponent();
            CommonApplicationBarItems.Init(this);
            if (Stations.Country.SupportsArrivals)
            {
                mapIndex = 2;
            }
            else
            {
                pivot.Items.Remove(arrivalsPivotItem);
            }
            pivot.SelectionChanged += OnPivotSelectionChanged;
        }

        private DeparturesAndArrivalsTable departuresAndArrivalsTable;
        private LazyBlock<Departure[]> departuresLazyBlock;
        private LazyBlock<Arrival[]> arrivalsLazyBlock;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (departuresAndArrivalsTable != null)
            {
                ErrorReporting.Log("departuresAndArrivalsTable is not null");
                Load();
                return;
            }

            var stationStr = NavigationContext.QueryString["station"];
            var from = Stations.Get(stationStr);
            var to = NavigationContext.QueryString.ContainsKey("callingAt") ? Stations.Get(NavigationContext.QueryString["callingAt"]) : null;
            var removeBackEntry = NavigationContext.QueryString.ContainsKey("removeBackEntry");
            departuresAndArrivalsTable = DeparturesAndArrivalsTable.Create(from, to);
            title.Text = departuresAndArrivalsTable.ToString();
            if (title.Text.Length > 40)
                title.Text = title.Text.Replace(" calling at ", "\ncalling at ");

            if (e.NavigationMode == NavigationMode.New)
            {
                RecentItems.Add(departuresAndArrivalsTable);
            }

            if (removeBackEntry)
            {
                NavigationService.RemoveBackEntry();
            }

            if (departuresAndArrivalsTable.HasDestinationFilter)
            {
                var filterOrClearFilterItem = ApplicationBar.Buttons.Cast<ApplicationBarIconButton>().Single(button => button.Text == "Filter");
                filterOrClearFilterItem.Text = "Clear Filter";
                filterOrClearFilterItem.IconUri = new Uri("/Assets/Icons/dark/appbar.filter.clear.png", UriKind.Relative);

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

            if (!Stations.Country.SupportsArrivals) { 
                // when supportsArrivals is false PivotSelectionChanged won't be triggered
                Load();
            }
        }

        private void Load()
        {
            if (pivot.SelectedIndex == 0)
            {
                LoadDepartures();
            }
            else if (pivot.SelectedIndex != mapIndex)
            {
                LoadArrivals();
            }
        }

        private void LoadDepartures()
        {
            if (departuresLazyBlock == null)
            {
                departuresLazyBlock = new LazyBlock<Departure[]>(
                    "departures",
                    "No more trains today",
                    departuresAndArrivalsTable.GetDepartures(),
                    items => items.Length == 0,
                    new LazyBlockUI<Departure>(this, departures, departuresMessageTextBlock, departuresLastUpdatedTextBlock),
                    Settings.GetBool(Setting.AutoRefresh),
                    null,
                    success => {
                        if (success)
                        {
                            UpdateTiles();
                        }
                    },
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
                arrivalsLazyBlock = new LazyBlock<Arrival[]>(
                    "arrivals",
                    "No more trains today",
                    departuresAndArrivalsTable.GetArrivals(),
                    items => items.Length == 0,
                    new LazyBlockUI<Arrival>(this, arrivals, arrivalsMessageTextBlock, arrivalsLastUpdatedTextBlock),
                    Settings.GetBool(Setting.AutoRefresh),
                    null,
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

            var secondaryTileUri = GetUri(departuresAndArrivalsTable, removeBackEntry: false);
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
                departuresAndArrivalsTable.ToSmallString() +
                (departuresAndArrivalsTable.HasDestinationFilter || firstDeparture == null ? "" : " to " + firstDeparture.Destination);

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
                content = (forPrimaryTile ? departuresTableHeader : (departuresAndArrivalsTable.HasDestinationFilter ? "" : firstDeparture.Destination)) +
                          (firstDeparture.PlatformIsKnown ? "\nPlatform " + firstDeparture.Platform.Value : "") +
                          (forPrimaryTile ? "" : "\n" + firstDeparture.Due + " " + firstDeparture.Status);
                wideContent = departuresTableHeader + "\n" +
                              (firstDeparture.PlatformIsKnown ? "Platform " + firstDeparture.Platform.Value + " " : "") + 
                              firstDeparture.Due + " " + firstDeparture.Status;
            }

            return new FlipTileData
            {
                Title = forPrimaryTile ? AppMetadata.Current.Name : departuresAndArrivalsTable.ToSmallString(),
                BackTitle = AppMetadata.Current.Name,
                BackContent = content,
                WideBackContent = wideContent,
                SmallBackgroundImage = new Uri("Assets/Tiles/dark/FlipCycleTileSmall.png", UriKind.Relative),
                BackgroundImage = new Uri("Assets/Tiles/dark/FlipCycleTileMedium.png", UriKind.Relative),
                WideBackgroundImage = new Uri("Assets/Tiles/dark/FlipCycleTileLarge.png", UriKind.Relative),
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
                    new GeoCoordinate(departuresAndArrivalsTable.Station.Location.Lat, departuresAndArrivalsTable.Station.Location.Long) },
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
                    ZoomLevel = 20,
                    PedestrianFeaturesEnabled = true,
                    LandmarksEnabled = true,
                };
                map.Loaded += delegate
                {
                    MapsSettings.ApplicationContext.ApplicationId = AppMetadata.Current.AppId.ToString("D");
                    MapsSettings.ApplicationContext.AuthenticationToken = AppMetadata.Current.MapAuthenticationToken;
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
                        Content = departuresAndArrivalsTable.Station.Name + " Station",
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
                    ErrorReporting.Log("OnMapClick");
                    new DirectionsRouteDestinationTask
                    {
                        Origin = start,
                        Destination = end,
                        Mode = RouteMode.Pedestrian,
                    }.Show();
                };
                pivot.Items.Add(pivotItem);
                bool mapCentered = false;
                pivot.SelectionChanged += delegate
                {
                    if (!mapCentered && pivot.SelectedIndex == mapIndex)
                    {
                        map.SetView(e.Result.BoundingBox);
                        mapCentered = true;
                    }
                };
            }
        }

        private ApplicationBarIconButton GetPinToStartButton()
        {
            return ApplicationBar.Buttons.Cast<ApplicationBarIconButton>().Single(button => button.Text == "Pin to Start");
        }

        private bool IsStationPinnedToStart()
        {
            var uri = GetUri(departuresAndArrivalsTable, removeBackEntry: false);
            return ShellTile.ActiveTiles.Any(tile => tile.NavigationUri == uri);
        }

        private void OnPinToStartClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnPinToStartClick");
            var uri = GetUri(departuresAndArrivalsTable, removeBackEntry: false);
            if (!IsStationPinnedToStart())
            {
                ShellTile.Create(GetUri(departuresAndArrivalsTable, removeBackEntry: false), GetTileData(forPrimaryTile: false), true);
            }
            GetPinToStartButton().IsEnabled = false;
        }

        private Uri GetUri(DeparturesAndArrivalsTable departuresAndArrivalsTable, bool removeBackEntry)
        {
            return GetUri(this, departuresAndArrivalsTable, removeBackEntry);
        }

        public static Uri GetUri(PhoneApplicationPage page, DeparturesAndArrivalsTable departuresAndArrivalsTable, bool removeBackEntry)
        {
            return page.GetUri<StationPage>().WithParameters("station", departuresAndArrivalsTable.Station.Code)
                                             .WithParametersIf(departuresAndArrivalsTable.HasDestinationFilter, () => "callingAt", () => departuresAndArrivalsTable.CallingAt.Value.Code)
                                             .WithParametersIf(removeBackEntry, "removeBackEntry");
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnRefreshClick");
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
            ErrorReporting.Log("OnFilterOrClearFilterClick");
            if (departuresAndArrivalsTable.HasDestinationFilter)
            {
                NavigationService.Navigate(GetUri(departuresAndArrivalsTable.WithoutFilter, false));
            }
            else
            {
                NavigationService.Navigate(this.GetUri<MainAndFilterPage>().WithParameters("fromStation", departuresAndArrivalsTable.Station.Code));
            }
        }

        private void OnFilterByAnotherDestinationClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnFilterByAnotherDestinationClick");
            NavigationService.Navigate(this.GetUri<MainAndFilterPage>().WithParameters("fromStation", departuresAndArrivalsTable.Station.Code, "excludeStation", departuresAndArrivalsTable.CallingAt.Value.Code));
        }

        private void OnReverseJourneyClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnReverseJourneyClick");
            NavigationService.Navigate(GetUri(departuresAndArrivalsTable.Reversed, false));
        }

        private void OnAnotherRailStationClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnRailStationsClick");
            NavigationService.Navigate(this.GetUri<MainAndFilterPage>());
        }

        private void OnLiveProgressFromDeparturesClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnLiveProgressFromDeparturesClick");
            var departure = (Departure)((Button)sender).DataContext;
            var title = departuresAndArrivalsTable.Station.Name + " to " + departuresAndArrivalsTable.Match(_ => departure.Destination, (_, destination) => destination.Name);
            LiveProgressPage.SetDetails(title, departure.Details);
            NavigationService.Navigate(this.GetUri<LiveProgressPage>());
        }

        private void OnLiveProgressFromArrivalsClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnLiveProgressFromArrivalsClick");
            var arrival = (Arrival)((Button)sender).DataContext;
            var title = departuresAndArrivalsTable.Match(_ => arrival.Origin, (_, origin) => origin.Name) + " to " + departuresAndArrivalsTable.Station.Name;
            LiveProgressPage.SetDetails(title, arrival.Details);
            NavigationService.Navigate(this.GetUri<LiveProgressPage>());
        }

        private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ErrorReporting.Log("OnPivotSelectionChanged " + pivot.SelectedIndex);
            Load();
        }

        private void OnSendTextMessage(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnSendTextMessage");
            var departure = (Departure)((MenuItem)sender).DataContext;
            var body = 
                "I'm taking the "
                + departure.Due
                + " train from "
                + departuresAndArrivalsTable.Station.Name
                + " to "
                + departuresAndArrivalsTable.Match(_ => departure.Destination, (_, destination) => destination.Name);
            if (departure.ArrivalIsKnown) {
                body += ". I'll be there at " + departure.Arrival.Value.Value.Expected.Value;
            }
            new SmsComposeTask { Body = body }.Show();
        }
    }
}
