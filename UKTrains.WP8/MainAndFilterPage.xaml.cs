using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using FSharp.Control;
using FSharp.GeoUtils;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Reactive;
using Microsoft.Phone.Shell;
using NationalRail;

namespace UKTrains
{
    public partial class MainAndFilterPage : PhoneApplicationPage
    {
        public MainAndFilterPage()
        {
            InitializeComponent();
            var allStationsView = new CollectionViewSource { Source = Stations.GetAll() }.View;
            allStationsView.Filter = x => Filter((Station)x);
            allStations.ItemsSource = allStationsView;
            Observable.FromEvent<TextChangedEventArgs>(filter, "TextChanged")
                .Throttle(TimeSpan.FromMilliseconds(300))
                .Subscribe(_ => Dispatcher.BeginInvoke(() => allStationsView.Refresh()));
            Observable.FromEvent<KeyEventArgs>(filter, "KeyDown")
                .Where(x => x.EventArgs.Key == Key.Enter)
                .Subscribe(_ => Dispatcher.BeginInvoke(() =>
                {
                    var stations = allStationsView.Cast<Station>().ToArray();
                    if (stations.Length == 1)
                    {
                        GoToStation(stations[0]);
                    }
                }));
            CommonApplicationBarItems.Init(this);
        }

        private LazyBlock<Tuple<double, Station>[]> nearestLazyBlock;
        private Station fromStation;
        private string excludeStation;
        private bool hasRecentItemsToDisplay;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            fromStation = NavigationContext.QueryString.ContainsKey("fromStation") ? Stations.Get(NavigationContext.QueryString["fromStation"]) : null;
            excludeStation = NavigationContext.QueryString.ContainsKey("excludeStation") ? NavigationContext.QueryString["excludeStation"] : null;

            if (fromStation == null)
            {
                if (e.NavigationMode == NavigationMode.New)
                {
                    LittleWatson.CheckForPreviousException(true);
                    if (!Settings.GetBool(Setting.LocationServicesEnabled) && !Settings.GetBool(Setting.LocationServicesPromptShown))
                    {
                        Settings.Set(Setting.LocationServicesPromptShown, true);
                        var result = MessageBox.Show("This application uses your current location to improve the experience. Do you wish to give it permission to use your location?",
                                                     "Location Services",
                                                     MessageBoxButton.OKCancel);
                        if (result == MessageBoxResult.OK)
                        {
                            Settings.Set(Setting.LocationServicesEnabled, true);
                        }
                    }
                    LittleWatson.CheckForNewVersion(this);
                }
            }
            else
            {
                pivot.Title = fromStation.Name + " calling at";
                nearest.Header = "Near " + fromStation.Name;
            }

            LocationService.PositionChanged += LoadNearestStations;
            LoadNearestStations();

            RefreshRecentItemsList();

            if (hasRecentItemsToDisplay)
            {
                pivot.SelectedIndex = 1;
            }
            else if (fromStation == null && Settings.GetBool(Setting.LocationServicesEnabled))
            {
                pivot.SelectedIndex = 0;
            }
            else
            {
                pivot.SelectedIndex = 2;
            }
        }

        private bool Filter(Station station)
        {
            if (fromStation != null && station.Code == fromStation.Code)
            {
                return false;
            }
            if (excludeStation != null && station.Code == excludeStation)
            {
                return false;
            }
            return string.IsNullOrEmpty(filter.Text) ||
                   station.Name.IndexOf(filter.Text, StringComparison.OrdinalIgnoreCase) != -1 ||
                   station.Code.IndexOf(filter.Text, StringComparison.OrdinalIgnoreCase) != -1;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            
            LocationService.PositionChanged -= LoadNearestStations;
            
            if (nearestLazyBlock != null)
            {
                nearestLazyBlock.Cancel();
            }
        }

        private void LoadNearestStations()
        {
            var lazyBlockUI = new LazyBlockUI<Tuple<double, Station>>(this, nearestStations, nearestStationsMessageTextBlock, null);

            LatLong from;
            if (fromStation == null)
            {
                var currentPosition = LocationService.CurrentPosition;
                if (!Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    lazyBlockUI.SetItems(null);
                    lazyBlockUI.SetLocalProgressMessage("Locations Services are disabled");
                    return;
                }
                else if (currentPosition == null || currentPosition.IsUnknown)
                {
                    lazyBlockUI.SetLocalProgressMessage("Acquiring position...");
                    lazyBlockUI.SetGlobalProgressMessage("Acquiring position...");
                    return;
                }
                from = LatLong.Create(currentPosition.Latitude, currentPosition.Longitude);
            }
            else
            {
                from = fromStation.Location;
            }

            lazyBlockUI.SetLocalProgressMessage("");
            lazyBlockUI.SetGlobalProgressMessage("");

            if (nearestLazyBlock != null)
            {
                nearestLazyBlock.Cancel();
            }

            nearestLazyBlock = new LazyBlock<Tuple<double, Station>[]>(
                "nearest stations",
                "No nearby stations",
                Stations.GetNearest(from, 150),
                items => items.Length == 0,
                lazyBlockUI,
                false,
                null,
                null,
                nearestUnfiltered => 
                {
                    var nearestFiltered = nearestUnfiltered.AsEnumerable();
                    if (fromStation != null)
                    {
                        nearestFiltered = nearestFiltered.Where(t => t.Item2.Code != fromStation.Code);
                    }
                    if (excludeStation != null)
                    {
                        nearestFiltered = nearestFiltered.Where(t => t.Item2.Code != excludeStation);
                    }
                    return nearestFiltered.ToArray();
                });
        }

        private void RefreshRecentItemsList()
        {
            var recentItemsToDisplay = RecentItems.GetItemsToDisplay(fromStation, excludeStation);

            hasRecentItemsToDisplay = recentItemsToDisplay.Count != 0;
            recentStations.ItemsSource = recentItemsToDisplay;

            ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>().Single(item => item.Text == "Clear recent items").IsEnabled = hasRecentItemsToDisplay;
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRefreshClick");
            if (nearestLazyBlock == null || nearestLazyBlock.CanRefresh)
            {
                LoadNearestStations();
            }
        }

        private void OnStationClick(object sender, RoutedEventArgs e)
        {
            LittleWatson.Log("OnStationClick");
            var dataContext = ((Button)sender).DataContext;
            GoToStation(dataContext);
        }

        private void GoToStation(object dataContext)
        {            
            var target = dataContext as DeparturesTable;
            if (target != null)
            {
                if (fromStation != null)
                {
                    Debug.Assert(!target.HasDestinationFilter);
                    target = DeparturesTable.Create(fromStation, target.Station);
                }
            }
            else
            {
                var station = dataContext as Station ?? ((Tuple<double, Station>)dataContext).Item2;
                target = fromStation == null ? DeparturesTable.Create(station) :
                         DeparturesTable.Create(fromStation, station);
            }
            NavigationService.Navigate(StationPage.GetUri(this, target, removeBackEntry: fromStation != null));
        }

        private void OnClearRecentItemsClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnClearRecentItemsClick");
            RecentItems.Clear();
            RefreshRecentItemsList();
        }

        private void OnRecentItemRemoveClick(object sender, RoutedEventArgs e)
        {
            LittleWatson.Log("OnRecentItemRemoveClick");
            var dataContext = (DeparturesTable)((MenuItem)sender).DataContext;
            RecentItems.Remove(dataContext);
            RefreshRecentItemsList();
        }
    }
}
