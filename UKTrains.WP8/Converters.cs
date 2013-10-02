using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NationalRail;

namespace UKTrains
{
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
            if (status.IsOnTime)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
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
            if (status.HasDeparted)
            {
                return Departed;
            }
            else
            {
                return NotDeparted;
            }
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
            if (status.IsOnTime)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}