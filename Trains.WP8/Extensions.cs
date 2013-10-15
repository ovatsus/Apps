using System;
using System.Linq;
using Microsoft.Phone.Controls;

namespace Trains.WP8
{
    public static class Extensions
    {
        public static Uri GetUri<T>(this PhoneApplicationPage currentPage) where T : PhoneApplicationPage
        {
            var targetPageType = typeof(T);
            if (currentPage.GetType().Namespace != targetPageType.Namespace)
            {
                //TODO: include folder path
                throw new NotImplementedException();
            }
            else
            {
                return new Uri("/Trains.WP8;component/" + targetPageType.Name + ".xaml", UriKind.Relative);
            }
        }

        public static Uri WithParameters(this Uri originalUri, params string[] args)
        {            
            string uri = originalUri.OriginalString;
            bool hadQuery = uri.Contains("?");
            for (int i = 0; i < args.Length; ++i)
            {
                uri += i == 0 && !hadQuery ? "?" : i % 2 == 0 ? "&" : "=";
                uri += args[i];
            }
            return new Uri(uri, UriKind.Relative);
        }

        public static Uri WithParametersIf(this Uri originalUri, bool condition, params string[] args)
        {
            return condition ? originalUri.WithParameters(args) : originalUri;
        }

        public static Uri WithParametersIf(this Uri originalUri, bool condition, params Func<string>[] args)
        {
            return condition ? originalUri.WithParameters(args.Select(arg => arg()).ToArray()) : originalUri;
        }
    }
}
