using System;
using System.Windows.Navigation;
using FSharp.Control;
using Microsoft.Phone.Controls;
using Trains;

namespace Trains.WP8
{
    public partial class LiveProgressPage : PhoneApplicationPage
    {
        public LiveProgressPage()
        {
            InitializeComponent();
            CommonApplicationBarItems.Init(this);
        }

        private static string title;
        private static Departure departure;

        private LazyBlock<JourneyElement[]> journeyElementsLazyBlock;

        //TODO: remove the static and pass in the page parameters
        public static void SetTarget(string title, Departure departure)
        {
            LiveProgressPage.departure = departure;
            LiveProgressPage.title = title;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (departure == null)
            {
                LittleWatson.Log("Departure is null");
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    LittleWatson.Log("Can not go back");
                }
                return;
            }

            if (journeyElementsLazyBlock != null)
            {
                if (journeyElements.ItemsSource == null)
                {
                    journeyElementsLazyBlock.Refresh();
                }
            }
            else
            {
                pivot.Title = title;
                journeyElementsLazyBlock = new LazyBlock<JourneyElement[]>(
                    "live progress",
                    "No information available",
                    departure.Details,
                    items => items.Length == 0,
                    new LazyBlockUI<JourneyElement>(this, journeyElements, journeyElementsMessageTextBlock, journeyElementsLastUpdatedTextBlock),
                    true,
                    null,
                    null,
                    null);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (e.NavigationMode == NavigationMode.New && e.Uri.OriginalString == "app://external/")
            {
                //running in background
                return;
            }
            if (journeyElementsLazyBlock != null)
            {
                journeyElementsLazyBlock.Cancel();
            }
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRefreshClick");
            if (journeyElementsLazyBlock != null)
            {
                journeyElementsLazyBlock.Refresh();
            }
        }
    }
}
