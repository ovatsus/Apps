using System;
using System.Globalization;
using System.Windows;
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

        private string stateFile;
        private MediaState mediaState;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string url;
            if (NavigationContext.QueryString.TryGetValue("url", out url))
            {
                mediaPlayer.Source = new Uri(url);
            }
            else
            {
                var filename = NavigationContext.QueryString["filename"];
                stateFile = DownloadInfo.GetStateFile(filename);
                mediaPlayer.SetSource(IsolatedStorage.OpenFileToRead(filename));
            }

            if (mediaState != null)
            {
                mediaPlayer.RestoreMediaState(mediaState);
                mediaState = null;
            }
            else if (stateFile != null && IsolatedStorage.FileExists(stateFile))
            {
                var ticks = long.Parse(IsolatedStorage.ReadAllText(stateFile), CultureInfo.InvariantCulture);
                mediaPlayer.RestoreMediaState(new MediaState
                {
                    IsPlaying = true,
                    IsStarted = true,
                    Position = TimeSpan.FromTicks(ticks),
                });
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            mediaState = mediaPlayer.GetMediaState();
            if (stateFile != null)
            {
                IsolatedStorage.WriteAllText(stateFile, mediaState.Position.Ticks.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void LaunchDownloadedVideo(PhoneApplicationPage source, IDownloadInfo downloadInfo)
        {
            source.NavigationService.Navigate(source.GetUri<VideoPage>().WithParameters("filename", downloadInfo.VideoLocation.OriginalString));
        }

        public static void LaunchVideoFromUrl(PhoneApplicationPage source, string videoUrl)
        {
            source.NavigationService.Navigate(source.GetUri<VideoPage>().WithParameters("url", videoUrl));
        }

        private void OnPlayerStateChanged(object sender, RoutedPropertyChangedEventArgs<PlayerState> e)
        {
            if (e.NewValue == PlayerState.Ending)
            {
                NavigationService.GoBack();
            }
        }
    }
}