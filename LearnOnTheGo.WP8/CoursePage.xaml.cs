﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Common.WP8;
using FSharp.Control;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo.WP8
{
    public partial class CoursePage : PhoneApplicationPage
    {
        public CoursePage()
        {
            InitializeComponent();
            CommonApplicationBarItems.Init(this);
        }

        private int courseId;
        private LazyBlock<LectureSection[]> lecturesLazyBlock;
        private Dictionary<int, LazyBlock<string>> videoLazyBlocks = new Dictionary<int, LazyBlock<string>>();
        private string lastEmail;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            courseId = int.Parse(NavigationContext.QueryString["courseId"]);

            // settings changed
            if (lastEmail != null && lastEmail != Settings.GetString(Setting.Email))
            {
                ErrorReporting.Log("Settings changed");
                pivot.ItemsSource = null;
            }

            lastEmail = Settings.GetString(Setting.Email);

            // app was tombstoned or settings changed
            if (App.Crawler == null)
            {
                ErrorReporting.Log("App.Crawler is null");

                var email = Settings.GetString(Setting.Email);
                var password = Settings.GetString(Setting.Password);
                if ((string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)))
                {
                    SafeGoBack();
                    return;
                }

                App.Crawler = new Crawler(email, password, Cache.GetFiles(), Cache.SaveFile, DownloadInfo.Create);
                new LazyBlock<Course[]>(
                    "courses",
                    "No courses",
                    App.Crawler.Courses,
                    a => a.Length == 0,
                    new LazyBlockUI<Course[]>(
                        this,
                        _ => Setup(),
                        () => false,
                        messageTextBlock),
                    false,
                    null,
                    success =>
                    {
                        if (!success)
                        {
                            ErrorReporting.Log("Failed to get courses");
                            App.Crawler = null;
                            SafeGoBack();
                        }
                    },
                    null);
            }
            else
            {
                Setup();
            }
        }

        private void Setup()
        {
            if (!App.Crawler.HasCourse(courseId))
            {
                ErrorReporting.Log("App.Crawler.HasCourse is false");
                SafeGoBack();
                return;
            }

            var course = App.Crawler.GetCourse(courseId);
            title.Text = course.Topic.Name;
            ErrorReporting.Log(courseId + " = " + course.Topic.Name + " [" + course.Name + "]");

            if (pivot.ItemsSource == null)
            {
                Load(false);
            }
            else
            {
                foreach (var lectureSection in pivot.ItemsSource.Cast<LectureSection>())
                {
                    foreach (var lecture in lectureSection.Lectures)
                    {
                        lecture.DownloadInfo.RefreshStatus();
                    }
                }
            }
        }

        private void SafeGoBack()
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                ErrorReporting.Log("Can not go back");
            }
        }

        private void Load(bool refresh)
        {
            if (lecturesLazyBlock != null)
            {
                lecturesLazyBlock.Cancel();
                lecturesLazyBlock = null;
            }
            var course = App.Crawler.GetCourse(courseId);
            if (!course.Active)
            {
                if (course.HasFinished)
                {
                    messageTextBlock.Text = "Lectures no longer available";
                }
                else
                {
                    messageTextBlock.Text = "Lectures not available yet";
                }
            }
            else
            {
                if (refresh)
                {
                    course = App.Crawler.RefreshCourse(course.Id);
                }
                lecturesLazyBlock = new LazyBlock<LectureSection[]>(
                    "lectures",
                    "No lectures available. Make sure you have accepted the honor code.",
                    course.LectureSections,
                    a => a.Length == 0,
                    new LazyBlockUI<LectureSection[]>(
                        this,
                        lectureSections => pivot.ItemsSource = lectureSections,
                        () => pivot.ItemsSource != null,
                        messageTextBlock),
                    false,
                    null,
                    success =>
                    {
                        if (!success)
                        {
                            ErrorReporting.Log("Failed to get lectures");
                        }
                    },
                    null);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (lecturesLazyBlock != null)
            {
                lecturesLazyBlock.Cancel();
            }
            foreach (var videoLazyBlock in videoLazyBlocks.Values)
            {
                videoLazyBlock.Cancel();
            }
            videoLazyBlocks.Clear();
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnRefreshClick");
            Load(true);
        }

        private void OnPlayOrDownloadOrCancelClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnPlayOrDownloadOrCancelClick");

            var lecture = (Lecture)((Button)sender).DataContext;
            ErrorReporting.Log("Lecture = " + lecture.Title + " [" + lecture.Id + "]");

            if (videoLazyBlocks.ContainsKey(lecture.Id))
            {
                ErrorReporting.Log("Already fetching video url");
            }
            else if (lecture.DownloadInfo.Downloading)
            {
                ErrorReporting.Log("Cancelling download");
                ((DownloadInfo)lecture.DownloadInfo).Monitor.RequestCancel();

            }
            else if (lecture.DownloadInfo.Downloaded)
            {
                ErrorReporting.Log("Launching downloaded video");
                VideoPage.LaunchDownloadedVideo(this, lecture.DownloadInfo);
            }
            else
            {
                StartDownload(lecture);
            }
        }

        private void OnStreamClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnStreamClick");

            var lecture = (Lecture)((Button)sender).DataContext;
            ErrorReporting.Log("Lecture = " + lecture.Title + " [" + lecture.Id + "]");

            if (videoLazyBlocks.ContainsKey(lecture.Id))
            {
                ErrorReporting.Log("Already fetching video url");
            }
            else 
            {
                videoLazyBlocks.Add(lecture.Id, new LazyBlock<string>(
                    "video location",
                    null,
                    lecture.VideoUrl,
                    _ => false,
                    new LazyBlockUI<string>(
                        this,
                        videoUrl =>
                        {
                            ErrorReporting.Log("Launching video from url");
                            VideoPage.LaunchVideoFromUrl(this, videoUrl);
                        },
                        () => false,
                        null),
                    false,
                    null,
                    _ => videoLazyBlocks.Remove(lecture.Id),
                    null));
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnDeleteClick");

            var lecture = (Lecture)((MenuItem)sender).DataContext;
            ErrorReporting.Log("Lecture = " + lecture.Title + " [" + lecture.Id + "]");

            lecture.DownloadInfo.DeleteVideo();
        }

        private void StartDownload(Lecture lecture)
        {
            videoLazyBlocks.Add(lecture.Id, new LazyBlock<string>(
                "video location",
                null,
                lecture.VideoUrl,
                _ => false,
                new LazyBlockUI<string>(
                    this,
                    videoUrl =>
                    {
                        ErrorReporting.Log("Queued download");
                        lecture.DownloadInfo.QueueDowload(videoUrl);
                    },
                    () => false,
                    null),
                false,
                null,
                _ => videoLazyBlocks.Remove(lecture.Id),
                null));
        }

        private void OnLectureNotesClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnLectureNotesClick");

            var lecture = (Lecture)((Button)sender).DataContext;
            ErrorReporting.Log("Lecture = " + lecture.Title + " [" + lecture.Id + "]");

            new WebBrowserTask { Uri = new Uri(lecture.LectureNotesUrl) }.Show();
        }

        private void OnOpenInBrowserClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnOpenInBrowserClick");

            var course = App.Crawler.GetCourse(courseId);
            new WebBrowserTask { Uri = new Uri(course.HomeLink) }.Show();
        }

        private void OnDownloadAllClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnDownloadAllClick");
            if (pivot.SelectedIndex != -1 && pivot.ItemsSource != null)
            {
                var lectureSections = (LectureSection[])pivot.ItemsSource;
                if (pivot.SelectedIndex < lectureSections.Length)
                {
                    foreach (var lecture in lectureSections[pivot.SelectedIndex].Lectures)
                    {
                        if (!videoLazyBlocks.ContainsKey(lecture.Id) && !lecture.DownloadInfo.Downloading && !lecture.DownloadInfo.Downloaded)
                        {
                            StartDownload(lecture);
                        }
                    }
                }
            }
        }

        private void OnViewAllDownloadsClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnVideoDownloadsClick");
            NavigationService.Navigate(this.GetUri<DownloadsPage>());
        }
    }
}