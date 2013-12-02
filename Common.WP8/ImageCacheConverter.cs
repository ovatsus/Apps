using System;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
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
                using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    var filename = CacheFolder + "/" + uri.AbsoluteUri.GetHashCode() + ".img";
                    if (isolatedStorage.FileExists(filename))
                    {
                        using (var sourceFile = isolatedStorage.OpenFile(filename, FileMode.Open))
                        {
                            var bm = new BitmapImage();
                            bm.SetSource(sourceFile);
                            return bm;
                        }
                    }
                    else
                    {
                        return DownloadFromWeb(uri, filename);
                    }
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
                using (var stream = e.Result)
                {
                    using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        try
                        {
                            if (!isolatedStorage.DirectoryExists(CacheFolder))
                            {
                                isolatedStorage.CreateDirectory(CacheFolder);
                            }
                            if (isolatedStorage.FileExists(filename))
                            {
                                isolatedStorage.DeleteFile(filename);
                            }
                            using (var outputStream = isolatedStorage.CreateFile(filename))
                            {
                                byte[] buffer = new byte[32768];
                                int read;
                                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    outputStream.Write(buffer, 0, read);
                                }
                            }
                            using (var sourceFile = isolatedStorage.OpenFile(filename, FileMode.Open))
                            {
                                bm.SetSource(sourceFile);
                            }
                        }
                        catch
                        {
                        }
                    }
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