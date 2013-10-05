using Coursera;
using FSharp.Control;
using Microsoft.Phone.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace LearnOnTheGo
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
#if DEBUG
            if (string.IsNullOrEmpty(Settings.GetString(Setting.Email)) || string.IsNullOrEmpty(Settings.GetString(Setting.Password)))
            {
                Settings.Set(Setting.Email, "ovatsus@outlook.com");
                Settings.Set(Setting.Password, "abc123");
            }
#endif
            CommonApplicationBarItems.Init(this);
        }

        private LazyBlock<Course[]> coursesLazyBlock;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.New)
            {
                LittleWatson.CheckForPreviousException(true);
                LittleWatson.CheckForNewVersion(this);
            }

            if (coursesLazyBlock != null)
            {
                LittleWatson.Log("coursesLazyBlock is not null");
                return;
            }

            var email = Settings.GetString(Setting.Email);
            var password = Settings.GetString(Setting.Password);

            if (e.NavigationMode != NavigationMode.Back && (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)))
            {
                this.NavigateToSettings();
            }
            else
            {
                LoadCourses(email, password, false);
            }
        }

        private void LoadCourses(string email, string password, bool refresh)
        {
            if (App.Crawler == null)
            {
                App.Crawler = new Crawler(email, password, Cache.GetFiles(), Cache.SaveFile);
            }
            if (refresh)
            {
                App.Crawler.RefreshCourses();
            }
            coursesLazyBlock = new LazyBlock<Course[]>(
                "courses",
                "No courses",
                App.Crawler.Courses,
                a => a.Length == 0,
                new LazyBlockUI<Course[]>(
                    this,
                    courses =>
                    {
                        var active = new List<Coursera.Course>();
                        var upcoming = new List<Coursera.Course>();
                        var finished = new List<Coursera.Course>();
                        foreach (var course in courses)
                        {
                            if (course.HasFinished)
                                finished.Add(course);
                            else if (course.Active)
                                active.Add(course);
                            else
                                upcoming.Add(course);
                        }
                        activeCourses.ItemsSource = active;
                        upcomingCourses.ItemsSource = upcoming;
                        finishedCourses.ItemsSource = finished;
                        activeCoursesEmptyMessage.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        upcomingCoursesEmptyMessage.Visibility = upcoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        finishedCoursesEmptyMessage.Visibility = finished.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        if (active.Count == 0)
                        {
                            if (upcoming.Count == 0)
                            {
                                pivot.SelectedIndex = 2;
                            }
                            else
                            {
                                pivot.SelectedIndex = 1;
                            }
                        }
                    },
                    () => activeCourses.ItemsSource != null,
                    messageTextBlock),
                false,
                _ =>
                {
                    activeCoursesEmptyMessage.Visibility = Visibility.Collapsed;
                    upcomingCoursesEmptyMessage.Visibility = Visibility.Collapsed;
                    finishedCoursesEmptyMessage.Visibility = Visibility.Collapsed;
                },
                success =>
                {
                    if (success && !refresh)
                    {
                        LoadCourses(email, password, true);
                    }
                },
                null);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (coursesLazyBlock != null)
            {
                coursesLazyBlock.Cancel();
            }          
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRefreshClick");

            if (coursesLazyBlock != null && !coursesLazyBlock.CanRefresh)
            {
                LittleWatson.Log("coursesLazyBlock can not refresh");
                return;
            }

            var email = Settings.GetString(Setting.Email);
            var password = Settings.GetString(Setting.Password);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                this.NavigateToSettings();
            }
            else
            {
                LoadCourses(email, password, true);
            }
        }

        private void OnCourseClick(object sender, RoutedEventArgs e)
        {
            LittleWatson.Log("OnCourseClick");
            var course = (Coursera.Course)((Button)sender).DataContext;
            NavigationService.Navigate(new Uri("/CoursePage.xaml?courseId=" + course.Id, UriKind.Relative));
        }
    }
}