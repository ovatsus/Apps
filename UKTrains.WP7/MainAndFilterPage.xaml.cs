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
using TombstoneHelper;

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
            CommonMenuItems.Init(this);
        }

        private LazyBlock<Tuple<string, Station>> nearestLazyBlock;
        private List<DeparturesTable> allRecentItems;
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
                }
                pivot.Title = "Rail Stations";
                nearest.Header = "Near me";
            }
            else
            {
                pivot.Title = fromStation.Name + " calling at";
                nearest.Header = "Near " + fromStation.Name;
            }

            LocationService.PositionChanged += LoadNearestStations;
            LoadNearestStations();

            LoadRecentItems(excludeStation);

            if (e.NavigationMode == NavigationMode.New)
            {
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

            if (fromStation == null && !IsReset(e.NavigationMode))
            {
                try
                {
                    this.RestoreState(); // restore pivot and scroll state
                }
                catch { }
            }
        }

        private bool IsReset(NavigationMode navigationMode)
        {
#if WP8
            return navigationMode == NavigationMode.Reset;
#else
            return false;
#endif
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
            if (nearestLazyBlock != null)
            {
                nearestLazyBlock.Cancel();
            }
            if (fromStation == null)
            {
                try
                {
                    this.SaveState(e); // save pivot and scroll state
                }
                catch { }
            }
        }

        private void LoadNearestStations()
        {
            var lazyBlockUI = new LazyBlockUI(this, nearestStations, nearestStationsMessageTextBlock, null);

            LatLong from;
            if (fromStation == null)
            {
                var currentPosition = LocationService.CurrentPosition;
                if (!Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    lazyBlockUI.SetItems<object>(null);
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

            nearestLazyBlock = new LazyBlock<Tuple<string, Station>>(
                "nearest stations",
                "You're outside of the UK",
                Stations.GetNearest(from, 150, Settings.GetBool(Setting.UseMilesInsteadOfKMs)),
                lazyBlockUI,
                false,
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

        private void LoadRecentItems(string excludeStation)
        {
            allRecentItems =
                Settings.GetString(Setting.RecentStations)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(DeparturesTable.Parse)
                    .ToList();

            var recentItemsToDisplay = fromStation == null ? allRecentItems :
                (from item in allRecentItems
                 let target = item.HasDestinationFilter && item.Station.Code == fromStation.Code && item.CallingAt.Value.Code != excludeStation ? item.CallingAt.Value :
                              item.Station.Code != excludeStation && item.Station.Code != fromStation.Code ? item.Station :
                              null
                 where target != null
                 select DeparturesTable.Create(target)).Distinct().ToList();

            hasRecentItemsToDisplay = recentItemsToDisplay.Count != 0;
            recentStations.ItemsSource = recentItemsToDisplay;

            ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>().Single(item => item.Text == "Clear recent items").IsEnabled = hasRecentItemsToDisplay;
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (nearestLazyBlock == null || nearestLazyBlock.CanRefresh)
            {
                LoadNearestStations();
            }
        }

        private void OnStationClick(object sender, RoutedEventArgs e)
        {
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
                var station = dataContext as Station ?? ((Tuple<string, Station>)dataContext).Item2;
                target = fromStation == null ? DeparturesTable.Create(station) :
                         DeparturesTable.Create(fromStation, station);
            }
            AddToRecentItems(target);
            SaveRecentItems();
            NavigationService.Navigate(StationPage.GetUri(target, removeBackEntry: fromStation != null));
        }

        private void AddToRecentItems(DeparturesTable recentItem)
        {
            if (recentItem.HasDestinationFilter && 
                allRecentItems.Count > 0 && 
                !allRecentItems[0].HasDestinationFilter && 
                allRecentItems[0].Station.Code == recentItem.Station.Code)
            {
                allRecentItems.RemoveAt(0);
            }
            allRecentItems.Remove(recentItem);
            allRecentItems.Insert(0, recentItem);            
        }

        private void SaveRecentItems()
        {
            Settings.Set(Setting.RecentStations, string.Join(",", allRecentItems.Select(item => item.Serialize())));
        }

        private void OnClearRecentItemsClick(object sender, EventArgs e)
        {
            allRecentItems.Clear();
            SaveRecentItems();
            LoadRecentItems(excludeStation);
        }

        private void OnRecentItemRemoveClick(object sender, RoutedEventArgs e)
        {
            var dataContext = (DeparturesTable)((MenuItem)sender).DataContext;
            allRecentItems.Remove(dataContext);
            SaveRecentItems();
            LoadRecentItems(excludeStation);
        }
    }
}
