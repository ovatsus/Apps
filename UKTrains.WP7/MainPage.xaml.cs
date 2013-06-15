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
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
            allStations.ItemsSource = LiveDepartures.getAllStations();
            recentStationsList = new ObservableCollection<Station>(
                Settings.GetString(Setting.RecentStations)
                  .Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(stationCode => LiveDepartures.getStation(stationCode)));
            if (recentStationsList.Count == 0)
            {
                if (Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    pivot.SelectedIndex = 1;
                }
                else
                {
                    pivot.SelectedIndex = 2;
                }
            }
            recentStations.ItemsSource = recentStationsList;
        }

        private bool busy;
        private ObservableCollection<Station> recentStationsList;

        private void AddToRecent(Station station)
        {
            recentStationsList.Remove(station);
            recentStationsList.Insert(0, station);
            Settings.Set(Setting.RecentStations, string.Join(",", recentStationsList.Select(st => st.Code)));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.RestoreState();
            
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

            LocationService.LocationChanged += LoadNearestStations;
            LoadNearestStations();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            this.SaveState(e); // save pivot and scroll state
        }

        private void OnRefreshButtonClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }

            busy = true;
            LoadNearestStations();
        }

        private void LoadNearestStations()
        {
            var currentLocation = LocationService.CurrentPosition;
            if (!Settings.GetBool(Setting.LocationServicesEnabled))
            {
                nearestStations.ItemsSource = null;
                nearestStationsMessageTextBlock.Visibility = Visibility.Visible;
                nearestStationsMessageTextBlock.Text = "Locations Services are disabled";
                busy = false;
            }
            else if (currentLocation == null || currentLocation.IsUnknown)
            {
                nearestStationsMessageTextBlock.Visibility = Visibility.Visible;
                nearestStationsMessageTextBlock.Text = "Acquiring position...";
                var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = "Acquiring position..." };
                SystemTray.SetProgressIndicator(this, indicator);
                busy = true;
            }
            else
            {
                var gpsPosition = GeoUtils.LatLong.Create(currentLocation.Latitude, currentLocation.Longitude);
                bool refreshing = nearestStations.ItemsSource != null;
                LiveDepartures.getNearestStations(gpsPosition, 150).Display(
                    this,
                    refreshing ? "Refreshing stations... " : "Loading stations...",
                    refreshing,
                    "You're outside of the UK",
                    nearestStationsMessageTextBlock,
                    nearest => nearestStations.ItemsSource = nearest,
                    () => busy = false);
            }
        }

        private void OnSettingsButtonClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private void OnStationButtonClick(object sender, RoutedEventArgs e)
        {
            var dataContext = ((Button)sender).DataContext;
            var station = dataContext as Station ?? ((Tuple<string, Station>)dataContext).Item2;
            AddToRecent(station);
            NavigationService.Navigate(new Uri("/StationPage.xaml?stationCode=" + station.Code, UriKind.Relative));
        }

        private void OnRateAndReviewButtonClick(object sender, EventArgs e)
        {
            var task = new MarketplaceReviewTask();
            task.Show();
        }
    }
}