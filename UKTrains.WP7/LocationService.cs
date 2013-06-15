using System;
using System.Device.Location;

namespace UKTrains
{
    public static class LocationService
    {
        public static event Action LocationChanged;
        public static GeoCoordinate CurrentPosition { get; private set; }
        private static DateTimeOffset currentPositionTimestamp;

        static LocationService()
        {
            var latitude = Settings.GetDouble(Setting.CurrentLat);
            var longitude = Settings.GetDouble(Setting.CurrentLong);
            if (!double.IsNaN(latitude) && !double.IsNaN(longitude))
            {
                CurrentPosition = new GeoCoordinate(latitude, longitude);
            }

            if (Settings.GetBool(Setting.LocationServicesEnabled))
            {
                var watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High)
                {
                    MovementThreshold = 50
                };

                watcher.PositionChanged += OnGeoPositionChanged;
                watcher.Start();
            }
        }

        private static void OnGeoPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            if (e.Position.Timestamp > currentPositionTimestamp)
            {
                if (CurrentPosition != e.Position.Location)
                {
                    bool previousEmpty = CurrentPosition == null || CurrentPosition.IsUnknown || currentPositionTimestamp == default(DateTimeOffset);
                    CurrentPosition = e.Position.Location;
                    Settings.Set(Setting.CurrentLat, CurrentPosition.Latitude);
                    Settings.Set(Setting.CurrentLong, CurrentPosition.Longitude);
                    if (previousEmpty && LocationChanged != null)
                    {
                        LocationChanged();
                    }
                }
                currentPositionTimestamp = e.Position.Timestamp;
            }
        }
    }
}