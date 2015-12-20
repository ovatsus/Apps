using System.Windows;
using Common.WP8;

namespace Trains.WP8.UK
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "UK Trains", "2.16.0.0", "uktrains@functionalflow.co.uk", true, "r4y7eZta5Pa32rpsho9CFA", () => LiveDepartures.UK.LastHtml);
            Stations.Country = Country.UK;
        }
    }
}