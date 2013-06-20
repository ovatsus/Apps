using FSharp;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using NationalRail;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TombstoneHelper;

namespace UKTrains
{
    public partial class MainAndFilterPage : PhoneApplicationPage
    {
        public MainAndFilterPage()
        {
            InitializeComponent();
            recentStationsList = new ObservableCollection<Station>(
                Settings.GetString(Setting.RecentStations)
                  .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(stationCode => LiveDepartures.getStation(stationCode)));
        }

        private bool loadingNearest;
        private ObservableCollection<Station> recentStationsList;
        private Station fromStation;

        private void AddToRecent(Station station)
        {
            recentStationsList.Remove(station);
            recentStationsList.Insert(0, station);
        }

        private void SaveRecent()
        {
            Settings.Set(Setting.RecentStations, string.Join(",", recentStationsList.Select(st => st.Code)));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            fromStation = NavigationContext.QueryString.ContainsKey("fromStation") ? LiveDepartures.getStation(NavigationContext.QueryString["fromStation"]) : null;

            if (fromStation == null)
            {
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
                pivot.Title = "Rail Stations";
                allStations.ItemsSource = LiveDepartures.getAllStations();
                recentStations.ItemsSource = recentStationsList;
                nearest.Header = "Near me";
            }
            else
            {
                pivot.Title = fromStation.Name + " calling at";
                allStations.ItemsSource = LiveDepartures.getAllStations().Except(new [] { fromStation });
                recentStations.ItemsSource = recentStationsList.Except(new[] { fromStation });
                nearest.Header = "Near " + fromStation.Name;
            }

            LocationService.PositionChanged += LoadNearestStations;
            LoadNearestStations();

            if (e.NavigationMode == NavigationMode.New)
            {
                if (recentStationsList.Count != 0)
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

            ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>().First().IsEnabled = recentStationsList.Count != 0;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (fromStation == null)
            {
                this.SaveState(e); // save pivot and scroll state
            }
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (!loadingNearest)
            {
                LoadNearestStations();
            }
        }

        private void LoadNearestStations()
        {
            loadingNearest = true;
            GeoUtils.LatLong from;
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
                from = GeoUtils.LatLong.Create(currentPosition.Latitude, currentPosition.Longitude);
            }
            else
            {
                from = fromStation.LatLong;
            }

            bool refreshing = nearestStations.ItemsSource != null;
            LiveDepartures.getNearestStations(from, 150).Display(
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

        private void OnStationClick(object sender, RoutedEventArgs e)
        {
            var dataContext = ((Button)sender).DataContext;
            var station = dataContext as Station ?? ((Tuple<string, Station>)dataContext).Item2;
            AddToRecent(station);
            if (fromStation == null)
            {
                NavigationService.Navigate(new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative));
            }
            else
            {
                NavigationService.Navigate(new Uri("/StationPage.xaml?stationCode=" + fromStation.Code + "&callingAt=" + station.Code, UriKind.Relative));
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

        private void OnClearRecentItemsClick(object sender, EventArgs e)
        {
            recentStationsList.Clear();
            SaveRecent();
            ApplicationBar.MenuItems.OfType<ApplicationBarMenuItem>().First().IsEnabled = false;
        }
    }
}