using System;
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
        private bool userInteracted;
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
            pivot.Title = course.Topic.Name;
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
                userInteracted = false;
                lecturesLazyBlock = new LazyBlock<LectureSection[]>(
                    "lectures",
                    "No lectures available. Make sure you have accepted the honor code.",
                    course.LectureSections,
                    a => a.Length == 0,
                    new LazyBlockUI<LectureSection[]>(
                        this,
                        lectureSections =>
                        {
                            pivot.ItemsSource = lectureSections;
                            var lastCompleted = lectureSections.Zip(Enumerable.Range(0, lectureSections.Length), (section, index) => Tuple.Create(index, section))
                                                               .LastOrDefault(tuple => tuple.Item2.Completed);
                            if (!userInteracted && lastCompleted != null && lastCompleted.Item1 < lectureSections.Length - 1)
                            {
                                pivot.SelectedIndex = lastCompleted.Item1 + 1;
                            }
                        },
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

        private void OnLectureVideoClick(object sender, RoutedEventArgs e)
        {
            ErrorReporting.Log("OnLectureVideoClick");

            var lecture = (Lecture)((Button)sender).DataContext;
            ErrorReporting.Log("Lecture = " + lecture.Title + " [" + lecture.Id + "]");

            if (videoLazyBlocks.ContainsKey(lecture.Id))
            {
                ErrorReporting.Log("Already fetching video url");
                return;
            }

            if (lecture.DownloadInfo.Downloading)
            {
                ErrorReporting.Log("Cancelling download");
                ((DownloadInfo)lecture.DownloadInfo).Monitor.RequestCancel();
                return;
            }

            if (lecture.DownloadInfo.Downloaded)
            {
                ErrorReporting.Log("Launching video");
                VideoPage.LaunchVideo(this, lecture.DownloadInfo.VideoLocation);
            }
            else
            {
                StartDownload(lecture);
            }
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

            var task = new WebBrowserTask();
            task.Uri = new Uri(lecture.LectureNotesUrl);
            task.Show();
        }

        private void OnOpenInBrowserClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnOpenInBrowserClick");

            var course = App.Crawler.GetCourse(courseId);

            var task = new WebBrowserTask();
            task.Uri = new Uri(course.HomeLink);
            task.Show();
        }

        private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            userInteracted = true;
        }

        private void OnScrollViewerManipulationStarted(object sender, System.Windows.Input.ManipulationStartedEventArgs e)
        {
            userInteracted = true;
        }

        private void OnDownloadAllClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnDownloadAllClick");
            if (pivot.ItemsSource != null)
            {
                foreach (var lecture in pivot.ItemsSource.Cast<LectureSection>().ElementAt(pivot.SelectedIndex).Lectures)
                {
                    if (!videoLazyBlocks.ContainsKey(lecture.Id) && !lecture.DownloadInfo.Downloading && !lecture.DownloadInfo.Downloaded)
                    {
                        StartDownload(lecture);
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