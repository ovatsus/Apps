using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;
using Common.WP8;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace LearnOnTheGo.WP8
{
    public partial class DownloadsPage : PhoneApplicationPage
    {
        private ObservableCollection<DownloadInfo> inProgress = new ObservableCollection<DownloadInfo>();
        private ObservableCollection<DownloadInfo> completed = new ObservableCollection<DownloadInfo>();
        
        public DownloadsPage()
        {
            InitializeComponent();
            CommonApplicationBarItems.Init(this);
            var inProgressView = new CollectionViewSource { Source = inProgress }.View;
            var completedView = new CollectionViewSource { Source = completed }.View;
            inProgressView.SortDescriptions.Add(new SortDescription("CourseId", ListSortDirection.Ascending));
            inProgressView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Ascending));
            completedView.SortDescriptions.Add(new SortDescription("CourseId", ListSortDirection.Ascending));
            completedView.SortDescriptions.Add(new SortDescription("Index", ListSortDirection.Ascending));
            inProgressDownloads.ItemsSource = inProgressView;
            completedDownloads.ItemsSource = completedView;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var downloads = DownloadInfo.GetAll();
            inProgress.Clear();
            completed.Clear();
            foreach (DownloadInfo download_ in downloads)
            {
                var download = download_;
                download.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == "Downloaded")
                    {                        
                        if (inProgress.Remove(download) && download.Downloaded)
                        {
                            completed.Add(download);
                        }
                        RefreshEmptyMessagesVisibility();
                    }
                };
                if (download.Downloaded)
                    completed.Add(download);
                else
                    inProgress.Add(download);
            }
            RefreshEmptyMessagesVisibility();
            if (inProgress.Count == 0 && completed.Count != 0)
            {
                pivot.SelectedIndex = 1;
            }
        }

        private void RefreshEmptyMessagesVisibility()
        {
            inProgressDownloadsEmptyMessage.Visibility = inProgress.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            completedDownloadsEmptyMessage.Visibility = completed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnPlayClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnPlayClick");
            
            var downloadInfo = (DownloadInfo)((Button)sender).DataContext;
            ErrorReporting.Log("Course = " + downloadInfo.CourseTopicName + " [" + downloadInfo.CourseId + "] Lecture = " + downloadInfo.LectureTitle + " [" + downloadInfo.LectureId + "]");
            
            CoursePage.LaunchVideo(downloadInfo.VideoLocation);
        }

        private void OnCancelOrDeleteClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnCancelOrDeleteClick");

            var downloadInfo = (DownloadInfo)((Button)sender).DataContext;
            ErrorReporting.Log("Course = " + downloadInfo.CourseTopicName + " [" + downloadInfo.CourseId + "] Lecture = " + downloadInfo.LectureTitle + " [" + downloadInfo.LectureId + "]");

            var monitor = downloadInfo.Monitor;
            if (monitor != null)
            {
                ErrorReporting.Log("Cancelling download");
                monitor.RequestCancel();
                inProgress.Remove(downloadInfo);
            }
            else
            {
                ErrorReporting.Log("Deleting Video");
                downloadInfo.DeleteVideo();
                completed.Remove(downloadInfo);
            }
            RefreshEmptyMessagesVisibility();
        }

        private void OnCancelAllClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnCancelAllClick");
            foreach (var downloadInfo in inProgress)
            {
                downloadInfo.Monitor.RequestCancel();
            }
            inProgress.Clear();
            RefreshEmptyMessagesVisibility();
        }

        private void OnDeleteAllClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnDeleteAllClick");
            foreach (var downloadInfo in completed)
            {
                downloadInfo.DeleteVideo();
            }
            completed.Clear();
            RefreshEmptyMessagesVisibility();
        }

        private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            while (ApplicationBar.MenuItems.Count > 1)
            {
                ApplicationBar.MenuItems.RemoveAt(0);
            }
            if (pivot.SelectedIndex == 0)
            {
                var menuItem = new ApplicationBarMenuItem("Cancel all downloads");
                menuItem.Click += OnCancelAllClick;
                ApplicationBar.MenuItems.Insert(0, menuItem);
            }
            else
            {
                var menuItem = new ApplicationBarMenuItem("Delete all videos");
                menuItem.Click += OnDeleteAllClick;
                ApplicationBar.MenuItems.Insert(0, menuItem);
            }            
        }
    }
}