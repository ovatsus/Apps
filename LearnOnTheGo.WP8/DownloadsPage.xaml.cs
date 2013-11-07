using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

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
            var downloads = DownloadInfo.GetAll().OrderBy(x => x.CourseId).ThenBy(x => x.Index);
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

        private void OnCancelAllClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnCancelAllClick");
            if (inProgressDownloads.ItemsSource != null)
            {
                foreach (var downloadInfo in inProgressDownloads.ItemsSource.Cast<DownloadInfo>().ToArray())
                {
                    downloadInfo.Monitor.RequestCancel();
                }
                Refresh();
            }
        }

        private void OnDeleteAllClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnDeleteAllClick");
            if (completedDownloads.ItemsSource != null)
            {
                foreach (var downloadInfo in completedDownloads.ItemsSource.Cast<DownloadInfo>().ToArray())
                {
                    downloadInfo.DeleteVideo();
                }
                Refresh();
            }
        }

        private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            while (ApplicationBar.MenuItems.Count > 1)
            {
                ApplicationBar.MenuItems.RemoveAt(0);
            }
            if (pivot.SelectedIndex == 0)
            {
                var menuItem = new ApplicationBarMenuItem("Cancel all");
                menuItem.Click += OnCancelAllClick;
                ApplicationBar.MenuItems.Insert(0, menuItem);
            }
            else
            {
                var menuItem = new ApplicationBarMenuItem("Delete all");
                menuItem.Click += OnDeleteAllClick;
                ApplicationBar.MenuItems.Insert(0, menuItem);
            }            
        }
    }
}