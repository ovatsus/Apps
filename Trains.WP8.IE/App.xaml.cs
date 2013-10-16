using System.Windows;
using Trains;

namespace Trains.WP8.IE
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "IE Trains", "ietrains@codebeside.org");
            Stations.Country = Country.Ireland;
        }
    }
}