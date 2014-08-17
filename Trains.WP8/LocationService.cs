using System;
using System.Device.Location;
using System.Windows.Threading;
using Common.WP8;
using FSharp.GeoUtils;

namespace Trains.WP8
{
    public static class LocationService
    {
        public static event Action PositionChanged;
        public static GeoCoordinate CurrentPosition { get; private set; }
        private static DateTimeOffset currentPositionTimestamp;
        private static GeoCoordinateWatcher watcher;
        private static DispatcherTimer timer;

        static LocationService()
        {
            var latitude = Settings.GetDouble(Setting.CurrentLat);
            var longitude = Settings.GetDouble(Setting.CurrentLong);
            if (!double.IsNaN(latitude) && !double.IsNaN(longitude))
            {
                try
                {
                    CurrentPosition = new GeoCoordinate(latitude, longitude);
                }
                catch 
                { 
                    //previous versions stored this wrong
                }
            }
            watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High)
            {
                MovementThreshold = 20
            };
            watcher.PositionChanged += OnGeoPositionChanged;
            Setup();
        }

        public static void Setup()
        {
            watcher.Stop();
            if (Settings.GetBool(Setting.LocationServicesEnabled))
            {                
                watcher.Start();
                StartTimer(Setup);
            }
        }

        public static void StartTimer(Action action)
        {
            StopTimer();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(60);
            timer.Tick += (sender, args) => action();
            timer.Start();
        }

        public static void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        private static void OnGeoPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            if (e.Position.Timestamp > currentPositionTimestamp)
            {       
                var previousPosition = CurrentPosition;
                var newPosition = e.Position.Location;
                if (previousPosition == null || previousPosition.IsUnknown || currentPositionTimestamp == default(DateTimeOffset) ||
                    LatLong.Create(previousPosition.Latitude, previousPosition.Longitude) - LatLong.Create(newPosition.Latitude, newPosition.Longitude) >= 0.050)
                {
                    CurrentPosition = newPosition;
                    Settings.Set(Setting.CurrentLat, CurrentPosition.Latitude);
                    Settings.Set(Setting.CurrentLong, CurrentPosition.Longitude);
                    if (PositionChanged != null && !AppMetadata.Current.RunningInBackground)
                    {
                        PositionChanged();
                    }
                }
                currentPositionTimestamp = e.Position.Timestamp;
            }
        }
    }
}