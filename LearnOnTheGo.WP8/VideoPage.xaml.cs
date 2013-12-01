using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows.Navigation;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.PlayerFramework;

namespace LearnOnTheGo.WP8
{
    public partial class VideoPage : PhoneApplicationPage
    {
        public VideoPage()
        {
            InitializeComponent();
        }

        private MediaState mediaState;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string url;
            if (NavigationContext.QueryString.TryGetValue("url", out url))
            {
                mediaPlayer.Source = new Uri(url);
                if (mediaState != null)
                {
                    mediaPlayer.RestoreMediaState(mediaState);
                    mediaState = null;
                }
                return;
            }

            var filename = NavigationContext.QueryString["filename"];
            using (var file = IsolatedStorageFile.GetUserStoreForApplication())
            {
                var stream = file.OpenFile(filename, FileMode.Open, FileAccess.Read);
                mediaPlayer.SetSource(stream);
                if (mediaState != null)
                {
                    mediaPlayer.RestoreMediaState(mediaState);
                    mediaState = null;
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            mediaState = mediaPlayer.GetMediaState();
        }

        public static void LaunchDownloadedVideo(PhoneApplicationPage source, IDownloadInfo downloadInfo)
        {
            source.NavigationService.Navigate(source.GetUri<VideoPage>().WithParameters("filename", downloadInfo.VideoLocation.OriginalString));
        }

        public static void LaunchVideoFromUrl(PhoneApplicationPage source, string videoUrl)
        {
            source.NavigationService.Navigate(source.GetUri<VideoPage>().WithParameters("url", videoUrl));
        }
    }
}