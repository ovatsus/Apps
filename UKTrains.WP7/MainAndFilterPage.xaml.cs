using FSharp.GeoUtils;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Reactive;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using NationalRail;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
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
        }

        private bool loadingNearest;
        private List<DeparturesTable> allRecentItems;
        private Station fromStation;
        private bool hasRecentItemsToDisplay;
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            fromStation = NavigationContext.QueryString.ContainsKey("fromStation") ? Stations.Get(NavigationContext.QueryString["fromStation"]) : null;

            if (fromStation == null)
            {
                if (e.NavigationMode == NavigationMode.New)
                {
                    LittleWatson.CheckForPreviousException();
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

            LoadRecentItems();

            if (e.NavigationMode == NavigationMode.New)
            {
                if (hasRecentItemsToDisplay)
                {
                    pivot.SelectedIndex = 1;
                }
                else if (Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    pivot.SelectedIndex = 0;
                }
                else
                {
                    pivot.SelectedIndex = 2;
                }
            }

            if (fromStation == null)
            {
                this.RestoreState(); // restore pivot and scroll state
            }

            AdControl.InitAds(adGrid, ApplicationBar);
        }

        private bool Filter(Station station)
        {
            if (fromStation != null && station.Code == fromStation.Code) {
                return false;
            }
            return string.IsNullOrEmpty(filter.Text) ||
                   station.Name.IndexOf(filter.Text, StringComparison.OrdinalIgnoreCase) != -1 ||
                   station.Code.IndexOf(filter.Text, StringComparison.OrdinalIgnoreCase) != -1;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (fromStation == null)
            {
                this.SaveState(e); // save pivot and scroll state
            }
        }
       
        private void LoadNearestStations()
        {
            loadingNearest = true;
            LatLong from;
            if (fromStation == null)
            {
                var currentPosition = LocationService.CurrentPosition;
                if (!Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    nearestStations.ItemsSource = null;
                    nearestStationsMessageTextBlock.Visibility = Visibility.Visible;
                    nearestStationsMessageTextBlock.Text = "Locations Services are disabled";
                    loadingNearest = false;
                    return;
                }
                else if (currentPosition == null || currentPosition.IsUnknown)
                {
                    nearestStationsMessageTextBlock.Visibility = Visibility.Visible;
                    nearestStationsMessageTextBlock.Text = "Acquiring position...";
                    var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = "Acquiring position..." };
                    SystemTray.SetProgressIndicator(this, indicator);
                    return;
                }
                from = LatLong.Create(currentPosition.Latitude, currentPosition.Longitude);
            }
            else
            {
                from = fromStation.Location;
            }

            bool refreshing = nearestStations.ItemsSource != null;
            Stations.GetNearest(from, 150, Settings.GetBool(Setting.UseMilesInsteadOfKMs)).Display(
                this,
                refreshing ? "Refreshing stations... " : "Loading stations...",
                refreshing,
                "You're outside of the UK",
                nearestStationsMessageTextBlock,
                nearest =>
                {
                    if (fromStation == null)
                    {
                        nearestStations.ItemsSource = nearest;
                    }
                    else
                    {
                        nearestStations.ItemsSource = nearest.Where(t => t.Item2.Code != fromStation.Code);
                    }
                },
                () => loadingNearest = false);
        }

        private void LoadRecentItems()
        {
            allRecentItems =
                Settings.GetString(Setting.RecentStations)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(DeparturesTable.Parse)
                    .ToList();

            var recentItemsToDisplay = fromStation == null ? allRecentItems :
                (from item in allRecentItems
                    where item.CallingAt != null && item.Station.Code == fromStation.Code
                    select item.HasDestinationFilter ? DeparturesTable.Create(item.CallingAt.Value) : item).ToList();

            hasRecentItemsToDisplay = recentItemsToDisplay.Count != 0;
            recentStations.ItemsSource = recentItemsToDisplay;

            ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>().Single(item => item.Text == "Clear recent items").IsEnabled = hasRecentItemsToDisplay;
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (!loadingNearest)
            {
                LoadNearestStations();
            }
        }

        private void OnStationClick(object sender, RoutedEventArgs e)
        {        
            var dataContext = ((Button)sender).DataContext;
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
            NavigationService.Navigate(StationPage.GetUri(target));
        }

        private void AddToRecentItems(DeparturesTable recentItem)
        {
            allRecentItems.Remove(recentItem);
            allRecentItems.Insert(0, recentItem);
        }

        private void SaveRecentItems()
        {
            Settings.Set(Setting.RecentStations, string.Join(",", allRecentItems.Select(item => item.Serialize())));
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

        private void OnClearRecentItemsClick(object sender, EventArgs e)
        {
            allRecentItems.Clear();
            SaveRecentItems();
            LoadRecentItems();
        }
    }
}