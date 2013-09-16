using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Coursera;
using FSharp.Control;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

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
        }

        private LazyBlock<Course[]> coursesLazyBlock;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            LittleWatson.CheckForPreviousException(true);

            if (App.Crawler != null)
            {
                return;
            }

            var email = Settings.GetString(Setting.Email);
            var password = Settings.GetString(Setting.Password);

            if (e.NavigationMode != NavigationMode.Back && (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)))
            {
                OnSettingsClick(null, null);
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
                        var completed = new List<Coursera.Course>();
                        foreach (var course in courses)
                        {
                            if (course.HasFinished)
                                completed.Add(course);
                            else if (course.Active)
                                active.Add(course);
                            else
                                upcoming.Add(course);
                        }
                        activeCourses.ItemsSource = active;
                        upcomingCourses.ItemsSource = upcoming;
                        completedCourses.ItemsSource = completed;
                        activeCoursesEmptyMessage.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        upcomingCoursesEmptyMessage.Visibility = upcoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                        completedCoursesEmptyMessage.Visibility = completed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
                    completedCoursesEmptyMessage.Visibility = Visibility.Collapsed;
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
            if (coursesLazyBlock != null && !coursesLazyBlock.CanRefresh)
            {
                return;
            }

            var email = Settings.GetString(Setting.Email);
            var password = Settings.GetString(Setting.Password);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                OnSettingsClick(null, null);
            }
            else
            {
                LoadCourses(email, password, true);
            }
        }

        private void OnCourseClick(object sender, RoutedEventArgs e)
        {
            var course = (Coursera.Course)((Button)sender).DataContext;
            NavigationService.Navigate(new Uri("/CoursePage.xaml?courseId=" + course.Id, UriKind.Relative));
        }

        private void OnSettingsClick(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private void OnRateAndReviewClick(object sender, EventArgs e)
        {
            var task = new MarketplaceReviewTask();
            task.Show();
        }

        private void OnGiveFeedbackClick(object sender, EventArgs e)
        {
            var task = new EmailComposeTask
            {
                To = "learnonthego@codebeside.org",
                Subject = "Feedback for Learn On The Go",
                Body = LittleWatson.GetMailBody("")
            };
            task.Show();
        }
    }
}