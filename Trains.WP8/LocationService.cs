using System;
using System.Device.Location;
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
                MovementThreshold = 50
            };
            watcher.PositionChanged += OnGeoPositionChanged;
            Setup();
        }

        public static void Setup()
        {
            if (Settings.GetBool(Setting.LocationServicesEnabled))
            {                
                watcher.Start();
            }
            else
            {
                watcher.Stop();    
            }
        }

        private static void OnGeoPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            if (e.Position.Timestamp > currentPositionTimestamp)
            {       
                var previousPosition = CurrentPosition;
                var newPosition = e.Position.Location;
                if (previousPosition == null || previousPosition.IsUnknown || currentPositionTimestamp == default(DateTimeOffset) ||
                    LatLong.Create(previousPosition.Latitude, previousPosition.Longitude) - LatLong.Create(newPosition.Latitude, newPosition.Longitude) >= 0.1)
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