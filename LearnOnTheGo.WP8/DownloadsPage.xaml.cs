using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Common.WP8;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo.WP8
{
    public partial class DownloadsPage : PhoneApplicationPage
    {
        public DownloadsPage()
        {
            InitializeComponent();
            CommonApplicationBarItems.Init(this);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            var downloads = DownloadInfo.GetAll().OrderByDescending(x => x.CourseId).ThenBy(x => x.LectureTitle);
            var inProgress = new List<IDownloadInfo>();
            var completed = new List<IDownloadInfo>();
            foreach (var download in downloads)
            {
                download.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == "Downloaded")
                    {
                        Refresh();
                    }
                };
                if (download.Downloaded)
                    completed.Add(download);
                else
                    inProgress.Add(download);
            }
            inProgressDownloads.ItemsSource = inProgress;
            completedDownloads.ItemsSource = completed;
            inProgressDownloadsEmptyMessage.Visibility = inProgress.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            completedDownloadsEmptyMessage.Visibility = completed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (inProgress.Count == 0 && completed.Count != 0)
            {
                pivot.SelectedIndex = 1;
            }
        }

        private void OnPlayClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnPlayClick");
            
            var downloadInfo = (DownloadInfo)((Button)sender).DataContext;
            ErrorReporting.Log("Course = " + downloadInfo.CourseTopicName + " [" + downloadInfo.CourseId + "] Lecture = " + downloadInfo.LectureTitle + " [" + downloadInfo.LectureId + "]");
            
            CoursePage.LaunchVideo(downloadInfo.VideoLocation);
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnDeleteClick");
            
            var downloadInfo = (DownloadInfo)((Button)sender).DataContext;
            ErrorReporting.Log("Course = " + downloadInfo.CourseTopicName + " [" + downloadInfo.CourseId + "] Lecture = " + downloadInfo.LectureTitle + " [" + downloadInfo.LectureId + "]");

            var monitor = downloadInfo.Monitor;
            if (monitor != null)
            {
                monitor.RequestCancel();
            }
            else
            {
                downloadInfo.DeleteVideo();
            }
            Refresh();
        }
    }
}