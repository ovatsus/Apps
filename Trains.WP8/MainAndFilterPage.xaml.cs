﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using Common.WP8;
using FSharp.Control;
using FSharp.GeoUtils;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Reactive;
using Microsoft.Phone.Shell;
using Windows.Phone.Speech.VoiceCommands;

namespace Trains.WP8
{
    public partial class MainAndFilterPage : PhoneApplicationPage
    {
        public MainAndFilterPage()
        {
            InitializeComponent();
            var allStationsView = new CollectionViewSource { Source = Stations.GetAll() }.View;
            allStationsView.Filter = x => Filter(filter.Text, fromStation, excludeStation, (Station)x);
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
                    if (Stations.Country == Country.UK)
                    {
                        Task.Run(() => InstallVoiceCommands());
                    }

                    ErrorReporting.CheckForPreviousException(true);
                    AppMetadata.CheckForNewVersion();
                    if (!Settings.GetBool(Setting.LocationServicesEnabled) && !Settings.GetBool(Setting.LocationServicesPromptShown))
                    {
                        Settings.Set(Setting.LocationServicesPromptShown, true);
                        if (Extensions.ShowMessageBox("Location Services", "This application uses your current location to improve the experience. Do you wish to give it permission to use your location?",
                                                      "Use location", "No thanks"))
                        {
                            Settings.Set(Setting.LocationServicesEnabled, true);
                        }
                    }
                }
            }
            else
            {
                pivot.Title = fromStation.Name + " calling at";
                nearest.Header = "Near " + fromStation.Name;
            }

            var removeBackEntry = NavigationContext.QueryString.ContainsKey("removeBackEntry");
            if (removeBackEntry)
            {
                NavigationService.RemoveBackEntry();
            }

            LocationService.PositionChanged += LoadNearestStations;
            LoadNearestStations();

            RefreshRecentItemsList();

            if (NavigationContext.QueryString.ContainsKey("initialFilter"))
            {
                filter.Text = NavigationContext.QueryString["initialFilter"];
                pivot.SelectedIndex = 2;
            }
            else if (hasRecentItemsToDisplay)
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

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            base.OnBackKeyPress(e);
            if (!NavigationService.CanGoBack)
            {
                AppMetadata.CheckForReview(this);
            }
        }

        public static bool Filter(string filter, Station fromStation, string excludeStation, Station station)
        {
            if (fromStation != null && station.Code == fromStation.Code)
            {
                return false;
            }
            if (excludeStation != null && station.Code == excludeStation)
            {
                return false;
            }
            return string.IsNullOrEmpty(filter) ||
                   TextMatchesQuery(filter, station.Name) ||
                   TextMatchesQuery(filter, station.Code);
        }

        private static bool TextMatchesQuery(string query, string text)
        {
            return query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .All(queryPart => text.IndexOf(queryPart, StringComparison.OrdinalIgnoreCase) >= 0);
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
            if (nearestLazyBlock != null)
            {
                nearestLazyBlock.Cancel();
            }

            var lazyBlockUI = new LazyBlockUI<Tuple<double, Station>>(this, nearestStations, nearestStationsMessageTextBlock, null);

            LatLong from;
            if (fromStation == null)
            {
                var currentPosition = LocationService.CurrentPosition;
                if (!Settings.GetBool(Setting.LocationServicesEnabled))
                {
                    lazyBlockUI.SetItems(null);
                    lazyBlockUI.SetLocalProgressMessage("Locations Services are disabled.\nYou can enable them in the Settings.");
                    nearestLazyBlock = null;
                    return;
                }
                else if (currentPosition == null || currentPosition.IsUnknown)
                {
                    lazyBlockUI.SetLocalProgressMessage("Acquiring position...");
                    lazyBlockUI.SetGlobalProgressMessage("Acquiring position...");
                    nearestLazyBlock = null;
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

            nearestLazyBlock = new LazyBlock<Tuple<double, Station>[]>(
                "nearest stations",
                "No nearby stations",
                Stations.GetNearestAsync(from, 150),
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

            ApplicationBar.Buttons.OfType<ApplicationBarIconButton>().Single(item => item.Text == "Clear recent").IsEnabled = hasRecentItemsToDisplay;
        }

        private void OnStationClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnStationClick");
            var dataContext = ((Button)sender).DataContext;
            GoToStation(dataContext);
        }

        private void GoToStation(object dataContext)
        {
            var target = dataContext as DeparturesAndArrivalsTable;
            if (target != null)
            {
                if (fromStation != null)
                {
                    Debug.Assert(!target.HasDestinationFilter);
                    target = DeparturesAndArrivalsTable.Create(fromStation, target.Station);
                }
            }
            else
            {
                var station = dataContext as Station ?? ((Tuple<double, Station>)dataContext).Item2;
                target = fromStation == null ? DeparturesAndArrivalsTable.Create(station) :
                         DeparturesAndArrivalsTable.Create(fromStation, station);
            }
            NavigationService.Navigate(StationPage.GetUri(this, target, removeBackEntry: fromStation != null));
        }

        private void OnClearRecentItemsClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnClearRecentItemsClick");
            RecentItems.Clear();
            RefreshRecentItemsList();
        }

        private void OnRecentItemRemoveClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnRecentItemRemoveClick");
            var dataContext = (DeparturesAndArrivalsTable)((MenuItem)sender).DataContext;
            RecentItems.Remove(dataContext);
            RefreshRecentItemsList();
        }

        private async void InstallVoiceCommands()
        {
            await VoiceCommandService.InstallCommandSetsFromFileAsync(new Uri("ms-appx:///VoiceCommandDefinition_8.1.xml"));
        }

        public static Uri GetUri(PhoneApplicationPage page, Station fromStation = null, Station excludeStation = null, string initialFilter = null, bool removeBackEntry = false)
        {
            return page.GetUri<MainAndFilterPage>().WithParametersIf(fromStation != null, () => "fromStation", () => fromStation.Code)
                                                   .WithParametersIf(excludeStation != null, () => "excludeStation", () => excludeStation.Code)
                                                   .WithParametersIf(!string.IsNullOrEmpty(initialFilter), () => "initialFilter", () => initialFilter)
                                                   .WithParametersIf(removeBackEntry, "removeBackEntry");
        }
    }
}
