using System;
using System.Globalization;
using System.Net;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Common.WP8
{
    /// <summary>
    /// Caches the image that gets downloaded as part of Image control Source property.
    /// </summary>
    public class ImageCacheConverter : IValueConverter
    {
        private const string CacheFolder = "ImageCache";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var uri = new Uri((string)value);
            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                var filename = CacheFolder + "/" + uri.AbsoluteUri.GetHashCode() + ".img";
                try
                {
                    if (IsolatedStorage.FileExists(filename))
                    {
                        var bm = new BitmapImage();
                        using (var stream = IsolatedStorage.OpenFileToRead(filename))
                        {
                            bm.SetSource(stream);
                        }
                        return bm;
                    }
                    else
                    {
                        return DownloadFromWeb(uri, filename);
                    }
                }
                catch
                {
                    // crashes in the VS preview when trying to access isolated storage
                    return new BitmapImage(uri);
                }
            }
            else
            {
                return new BitmapImage(uri);
            }
        }

        private static BitmapImage DownloadFromWeb(Uri uri, string filename)
        {
            var webClient = new WebClient();
            var bm = new BitmapImage();

            webClient.OpenReadCompleted += (o, e) =>
            {
                if (e.Error != null || e.Cancelled)
                {
                    return;
                }
                using (var inputStream = e.Result)
                {
                    IsolatedStorage.WriteToFile(filename, outputStream =>
                    {
                        byte[] buffer = new byte[32768];
                        int read;
                        while ((read = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outputStream.Write(buffer, 0, read);
                        }
                    });
                }
            };
            webClient.OpenReadAsync(uri);
            return bm;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}