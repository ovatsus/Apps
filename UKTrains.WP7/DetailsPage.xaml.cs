using System;
using System.Windows.Navigation;
using FSharp.Control;
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

        private LazyBlock<JourneyElement> journeyElementsLazyBlock;

        //TODO: remove the static and pass in the page parameters
        public static void SetTarget(Departure departure) 
        {
            DetailsPage.departure = departure;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode != NavigationMode.New)
            {
                return;
            }

            if (departure == null)
            {
                NavigationService.GoBack();
            }
            else
            {
                journeyElementsLazyBlock = new LazyBlock<JourneyElement>(
                    "live progress",
                    "No information available",
                    departure.Details,
                    new LazyBlockUI(this, journeyElements, journeyElementsMessageTextBlock, journeyElementsLastUpdatedTextBlock),
                    true,
                    null,
                    null);
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
            if (journeyElementsLazyBlock != null)
            {
                journeyElementsLazyBlock.Cancel();
            }
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            journeyElementsLazyBlock.Refresh();
        }       
    }
}
