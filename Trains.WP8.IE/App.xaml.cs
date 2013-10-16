using System.Windows;
using Common.WP8;

namespace Trains.WP8.IE
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            new AppMetadata(this, "Ireland Trains", "irelandtrains@codebeside.org", true, "rAyhw_lHxiqJnHo7CLUAOA");
            Stations.Country = Country.Ireland;
        }
    }
}