using System;
using System.Windows.Navigation;
using Common.WP8;
using FSharp.Control;
using Microsoft.Phone.Controls;

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
        private static LazyAsync<JourneyElement[]> journeyElementsLazyAsync;

        private LazyBlock<JourneyElement[]> journeyElementsLazyBlock;

        public static void SetDetails(string title, LazyAsync<JourneyElement[]> journeyElementsLazyAsync)
        {
            LiveProgressPage.journeyElementsLazyAsync = journeyElementsLazyAsync;
            LiveProgressPage.title = title;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (journeyElementsLazyAsync == null)
            {
                ErrorReporting.Log("journeyElementsLazyAsync is null");
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    ErrorReporting.Log("Can not go back");
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
                    journeyElementsLazyAsync,
                    items => items.Length == 0,
                    new LazyBlockUI<JourneyElement>(this, journeyElements, journeyElementsMessageTextBlock, journeyElementsLastUpdatedTextBlock),
                    Settings.GetBool(Setting.AutoRefresh),
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
            ErrorReporting.Log("OnRefreshClick");
            if (journeyElementsLazyBlock != null)
            {
                journeyElementsLazyBlock.Refresh();
            }
        }
    }
}
