using System.Windows;
using Common.WP8;

namespace Trains.WP8.IE
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "Irish Trains", "irishtrains@codebeside.org", true, "rAyhw_lHxiqJnHo7CLUAOA", "61787");
            Stations.Country = Country.Ireland;
        }
    }
}