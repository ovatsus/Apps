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
        private TimeSpan? position;

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

            // only restore position for downloaded videos
            if (stateFile != null)
            {
                if (!position.HasValue && IsolatedStorage.FileExists(stateFile))
                {
                    position = TimeSpan.FromTicks(long.Parse(IsolatedStorage.ReadAllText(stateFile), CultureInfo.InvariantCulture));
                }
                if (position.HasValue)
                {
                    mediaPlayer.RestoreMediaState(new MediaState
                    {
                        IsPlaying = true,
                        IsStarted = true,
                        Position = position.Value,
                    });
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            position = mediaPlayer.Position;
            if (stateFile != null)
            {
                if (mediaPlayer.NaturalDuration.HasTimeSpan && (mediaPlayer.NaturalDuration.TimeSpan - position.Value).TotalSeconds < 5)
                {
                    IsolatedStorage.Delete(stateFile);
                }
                else
                {
                    IsolatedStorage.WriteAllText(stateFile, position.Value.Ticks.ToString(CultureInfo.InvariantCulture));
                }
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