using System.Windows;
using Trains;

namespace Trains.WP8.UK
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "UK Trains", "uktrains@codebeside.org");
            Stations.Country = Country.UK;
        }
    }
}