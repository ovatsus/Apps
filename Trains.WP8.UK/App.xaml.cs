using System.Windows;
using Common.WP8;

namespace Trains.WP8.UK
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "UK Trains", "uktrains@codebeside.org", true);
            Stations.Country = Country.UK;
        }
    }
}