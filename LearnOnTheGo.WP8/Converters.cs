using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LearnOnTheGo.WP8
{
    public class DownloadInfoToImageSourceConverter : IValueConverter
    {
        public ImageSource Downloaded { get; set; }
        public ImageSource Downloading { get; set; }
        public ImageSource None { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var downloadInfo = (IDownloadInfo)value;
            if (downloadInfo.Downloaded)
            {
                return Downloaded;
            }
            else if (downloadInfo.Downloading)
            {
                return Downloading;
            }
            else
            {
                return None;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}