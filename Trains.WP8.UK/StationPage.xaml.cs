using System;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;

namespace Trains.WP8.UK
{
    public partial class StationPage : PhoneApplicationPage
    {
        public StationPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var uri = e.Uri.OriginalString.Replace("/StationPage.xaml", "/Trains.WP8;component/StationPage.xaml")
                                          .Replace("stationCode", "station");
            NavigationService.Navigate(new Uri(uri, UriKind.Relative));
        }
    }
}