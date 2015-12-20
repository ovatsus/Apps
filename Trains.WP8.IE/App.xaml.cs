using System.Windows;
using Common.WP8;

namespace Trains.WP8.IE
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "Irish Trains", "1.7.0.0", "irishtrains@functionalflow.co.uk", true, "rAyhw_lHxiqJnHo7CLUAOA");
            Stations.Country = Country.Ireland;
        }
    }
}