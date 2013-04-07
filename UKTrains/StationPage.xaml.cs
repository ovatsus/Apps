using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using NationalRail;

namespace UKTrains
{
    public partial class StationPage : PhoneApplicationPage
    {
        public StationPage()
        {
            InitializeComponent();
        }

        private Station station;
        private bool busy;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            busy = true;

            var stationCode = NavigationContext.QueryString["stationCode"];
            station = LiveDepartures.getStation(stationCode);
            pivot.Title = station.Name;

            Load();
        }

        private void Load()
        {
            LiveDepartures.getDepartures(null, station).Display(
                this,
                "Loading departures...",
                "No more departures from this station today",
                messageTextBlock,
                departures => this.departures.ItemsSource = departures,
                () => busy = false);
        }

        private void OnRefreshButtonClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }
            busy = true;

            Load();
        }
    }
}