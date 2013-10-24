using System.Windows;
using Common.WP8;

namespace Trains.WP8.UK
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "UK Trains", "uktrains@codebeside.org", true, "r4y7eZta5Pa32rpsho9CFA", "57899");
            Stations.Country = Country.UK;
        }
    }
}