using System.Windows;
using Trains;
using Trains.WP8;

namespace IrishTrains.WP8
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "Irish Trains", "eiretrains@codebeside.org");
            Stations.Country = Country.Ireland;
        }
    }
}