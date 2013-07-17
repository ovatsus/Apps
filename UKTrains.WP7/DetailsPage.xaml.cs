using System;
using System.Threading;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Phone.Controls;
using NationalRail;

namespace UKTrains
{
    public partial class DetailsPage : PhoneApplicationPage
    {
        public DetailsPage()
        {
            InitializeComponent();
            CommonMenuItems.Init(this);
        }

        private static Departure departure;

        private DispatcherTimer refreshTimer;
        private CancellationTokenSource journeyElementsCts;

        //TODO: remove the static and pass in the page parameters
        public static void SetTarget(Departure departure) 
        {
            DetailsPage.departure = departure;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (departure == null)
            {
                NavigationService.GoBack();
            }
            else
            {
                LoadJourneyElements();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
#if WP8
            if (e.NavigationMode == NavigationMode.New && e.Uri.OriginalString == "app://external/")
            {
                //running in background
                return;
            }
#endif
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }
            if (journeyElementsCts != null)
            {
                journeyElementsCts.Cancel();
                journeyElementsCts = null;
            }
        }

        private void LoadJourneyElements()
        {
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }
            bool refreshing = this.journeyElements.ItemsSource != null;
            if (refreshing)
            {
                departure.Details.Reset();
            }
            journeyElementsCts = departure.Details.Display(
                this,
                refreshing ? "Refreshing live progress..." : "Loading live progress...",
                refreshing,
                "No information available",
                journeyElementsMessageTextBlock,
                UpdateJourneyElements,
                () => journeyElementsCts = null);
        }

        private void UpdateJourneyElements(JourneyElement[] journeyElements)
        {
            this.journeyElements.ItemsSource = journeyElements;

            if (refreshTimer != null)
            {
                refreshTimer.Stop();
            }
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(60);
            refreshTimer.Tick += (sender, args) => LoadJourneyElements();
            refreshTimer.Start();
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (journeyElementsCts == null)
            {
                LoadJourneyElements();
            }
        }       
    }
}