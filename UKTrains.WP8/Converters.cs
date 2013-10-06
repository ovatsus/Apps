using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NationalRail;

namespace UKTrains
{
    public class DistanceToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var distance = (double)value;
            if (Settings.GetBool(Setting.UseMilesInsteadOfKMs))
            {
                return string.Format("{0,1:F1} mi", distance * 0.621371192);
            }
            else
            {
                return string.Format("{0,1:F1} km", distance);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToColorConverter : IValueConverter
    {
        public Brush Cancelled { get; set; }
        public Brush Delayed { get; set; }
        public Brush OnTime { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (Status)value;
            if (status.IsCancelled)
            {
                return Cancelled;
            }
            else if (status.IsDelayed)
            {
                return Delayed;
            }
            else 
            {
                return OnTime;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (Status)value;
            return status.IsOnTime ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class JourneyElementStatusToColorConverter : IValueConverter
    {
        public Brush Cancelled { get; set; }
        public Brush Delayed { get; set; }
        public Brush NoReport { get; set; }
        public Brush OnTime { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (JourneyElementStatus)value;
            if (status.IsCancelled)
            {
                return Cancelled;
            }
            else if (status.IsDelayed)
            {
                return Delayed;
            }
            else if (status.IsNoReport)
            {
                return NoReport;
            }
            else
            {
                return OnTime;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HasDepartedToColorConverter : IValueConverter
    {
        public Brush Departed { get; set; }
        public Brush NotDeparted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (JourneyElementStatus)value;
            return status.HasDeparted ? Departed : NotDeparted;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class JourneyElementStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = (JourneyElementStatus)value;
            return status.IsOnTime ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ArrivalInformationToMessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var arrivalInformation = (ArrivalInformation)value;
            return "Arrival at " + arrivalInformation.Destination + " " + arrivalInformation.Status.ToString().ToLower();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}