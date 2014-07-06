using System.Linq;
using System.Windows.Navigation;
using FSharp.GeoUtils;
using Microsoft.Phone.Controls;

namespace Trains.WP8
{
    public partial class VoiceCommandsPage : PhoneApplicationPage
    {
        public VoiceCommandsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string voiceCommandName;
            if (e.NavigationMode == NavigationMode.New && NavigationContext.QueryString.TryGetValue("voiceCommandName", out voiceCommandName))
            {
                HandleVoiceCommand(voiceCommandName);
            }
            else
            {
                OnError();
            }
        }

        private void OnError()
        {
            NavigationService.Navigate(MainAndFilterPage.GetUri(this, removeBackEntry: true));
        }

        private void HandleVoiceCommand(string voiceCommandName)
        {
            bool actSilently = NavigationContext.QueryString.ContainsKey("commandMode")
                && NavigationContext.QueryString["commandMode"] == "text";

            string from = null;
            string to = null;

            switch (voiceCommandName)
            {
                case "DeparturesFromTo":
                    if (NavigationContext.QueryString.TryGetValue("from", out from)
                        && !string.IsNullOrEmpty(from)
                        && from != ".."
                        && NavigationContext.QueryString.TryGetValue("to", out to)
                        && !string.IsNullOrEmpty(to)
                        && to != "...")
                    {
                        ShowDepartures(CleanStationName(from), CleanStationName(to), actSilently);
                        return;
                    }
                    break;
                case "DeparturesTo":
                    if (NavigationContext.QueryString.TryGetValue("to", out to)
                        && !string.IsNullOrEmpty(to)
                        && to != "...")
                    {
                        ShowDepartures(CleanStationName(to), actSilently);
                        return;
                    }
                    break;
                case "DeparturesFrom":
                    if (NavigationContext.QueryString.TryGetValue("from", out from)
                        && !string.IsNullOrEmpty(from)
                        && from != "...")
                    {
                        ShowDepartures(CleanStationName(from), null, actSilently);
                        return;
                    }
                    break;
            }

            OnError();
        }

        private static string CleanStationName(string station)
        {
            return station.Replace("train", null).Replace("rail", null).Replace("station", null).Trim();
        }

        private void ShowDepartures(string to, bool actSilently)
        {
            var currentPosition = LocationService.CurrentPosition;
            if (currentPosition != null && !currentPosition.IsUnknown)
            {
                var from = Stations.GetNearest(LatLong.Create(currentPosition.Latitude, currentPosition.Longitude), 1)[0];
                ShowDepartures(Stations.GetAll(), from.Item2, null, to, actSilently);
            }
            else
            {
                OnError();
            }
        }

        private void ShowDepartures(string from, string to, bool actSilently)
        {
            var allStations = Stations.GetAll();
            var fromStationCandidates = allStations.Where(x => MainAndFilterPage.Filter(from, null, null, x)).ToArray();
            var fromStation = fromStationCandidates.Length == 1 ? fromStationCandidates[0] : null;
            ShowDepartures(allStations, fromStation, from, to, actSilently);
        }

        private void ShowDepartures(Station[] allStations, Station fromStation, string from, string to, bool actSilently)
        {
            var toStationCandidates = allStations.Where(x => MainAndFilterPage.Filter(to, fromStation, null, x)).ToArray();
            var toStation = toStationCandidates.Length == 1 ? toStationCandidates[0] : null;
            if (fromStation != null)
            {
                if (toStation != null)
                {
                    var target = DeparturesAndArrivalsTable.Create(fromStation, toStation);
                    NavigationService.Navigate(StationPage.GetUri(this, target, removeBackEntry: true));
                    return;
                }
                else
                {
                    if (string.IsNullOrEmpty(to))
                    {
                        var target = DeparturesAndArrivalsTable.Create(fromStation);
                        NavigationService.Navigate(StationPage.GetUri(this, target));
                    }
                    else
                    {
                        NavigationService.Navigate(MainAndFilterPage.GetUri(this, fromStation, initialFilter: to, removeBackEntry: true));
                    }
                    return;
                }
            }
            else
            {
                NavigationService.Navigate(MainAndFilterPage.GetUri(this, null, initialFilter: from, removeBackEntry: true));
            }
        }
    }
}