using Coursera;
using FSharp.Control;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace LearnOnTheGo
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
        private LazyBlock<string> videoLazyBlock;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            courseId = int.Parse(NavigationContext.QueryString["courseId"]);

            // app was tombstoned or settings changed
            if (App.Crawler == null)
            {
                LittleWatson.Log("App.Crawler is null");

                var email = Settings.GetString(Setting.Email);
                var password = Settings.GetString(Setting.Password);
                if ((string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)))
                {
                    if (NavigationService.CanGoBack)
                    {
                        NavigationService.GoBack();
                    }
                    else
                    {
                        LittleWatson.Log("Can not go back");
                    }
                    return;
                }

                App.Crawler = new Crawler(email, password, Cache.GetFiles(), Cache.SaveFile);
                new LazyBlock<Course[]>(
                    null,
                    null,
                    App.Crawler.Courses,
                    _ => false,
                    new LazyBlockUI<Course[]>(
                        this,
                        _ => Setup(),
                        () => false,
                        null),
                    false,
                    null,
                    success =>
                    {
                        if (!success)
                        {
                            LittleWatson.Log("Failed to get courses");
                            App.Crawler = null;
                            if (NavigationService.CanGoBack)
                            {
                                NavigationService.GoBack();
                            }
                            else
                            {
                                LittleWatson.Log("Can not go back");
                            }
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
                LittleWatson.Log("App.Crawler.HasCourse is false");

                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    LittleWatson.Log("Can not go back");
                }
                return;
            }

            var course = App.Crawler.GetCourse(courseId);
            pivot.Title = course.Topic.Name;
            LittleWatson.Log(courseId + " = " + course.Topic.Name + " [" + course.Name + "]");

            Load(false);
        }

        private void Load(bool refresh)
        {
            if (lecturesLazyBlock != null)
            {
                lecturesLazyBlock.Cancel();
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
                        lectureSections =>
                        {
                            pivot.ItemsSource = lectureSections;
                            pivot.SelectedIndex = lectureSections.Count(x => x.Completed) % lectureSections.Length;
                        },
                        () => pivot.ItemsSource != null,
                        messageTextBlock),
                    false,
                    null,
                    success =>
                    {
                        if (success && !refresh)
                        {
                            Load(true);
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
            if (videoLazyBlock != null)
            {
                videoLazyBlock.Cancel();
            }
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRefreshClick");
            Load(true);
        }

        private void OnLectureVideoClick(object sender, RoutedEventArgs e)
        {
            LittleWatson.Log("OnLectureVideoClick");

            if (videoLazyBlock != null)
            {
                LittleWatson.Log("videoLazyBlock is not null");
                return;
            }

            var lecture = (Coursera.Lecture)((Button)sender).DataContext;

            videoLazyBlock = new LazyBlock<string>(
                "video",
                null,
                lecture.VideoUrl,
                _ => false,
                new LazyBlockUI<string>(
                    this,
                    videoUrl =>
                    {
                        try
                        {
                            var launcher = new MediaPlayerLauncher();
                            launcher.Media = new Uri(videoUrl, UriKind.Absolute);
                            launcher.Show();
                        }
                        catch (Exception ex)
                        {
                            LittleWatson.ReportException(ex, string.Format("Opening video for lecture '{0}' of course '{1}' ({2})", lecture.Title, pivot.Title, videoUrl));
                            LittleWatson.CheckForPreviousException(false);
                        }
                    },
                    () => false,
                    null),
                false,
                null,
                _ => videoLazyBlock = null,
                null);
        }

        private void OnLectureNotesClick(object sender, RoutedEventArgs e)
        {
            LittleWatson.Log("OnLectureNotesClick");

            var lecture = (Coursera.Lecture)((Button)sender).DataContext;

            var task = new WebBrowserTask();
            task.Uri = new Uri(lecture.LectureNotesUrl, UriKind.Absolute);
            task.Show();
        }

        private void OnOpenInBrowserClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnOpenInBrowserClick");

            var course = App.Crawler.GetCourse(courseId);

            var task = new WebBrowserTask();
            task.Uri = new Uri(course.HomeLink, UriKind.Absolute);
            task.Show();
        }
    }
}