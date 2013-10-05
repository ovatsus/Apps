using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LearnOnTheGo
{
    public class BooleanToColorConverter : IValueConverter
    {
        public Brush True { get; set; }
        public Brush False { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
            {
                return True;
            }
            else
            {
                return False;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}