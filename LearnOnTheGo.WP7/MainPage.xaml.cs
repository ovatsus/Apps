using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (App.Crawler != null)
            {
                return;
            }

            var email = Settings.Get(Setting.Email);
            var password = Settings.Get(Setting.Password);

            if (e.NavigationMode != NavigationMode.Back && (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)))
            {
                OnSettingsClick(null, null);
            }
            else
            {
                LoadCourses(email, password, false);
            }
        }

        private bool busy;

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }

            var email = Settings.Get(Setting.Email);
            var password = Settings.Get(Setting.Password);

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                OnSettingsClick(null, null);
            }
            else
            {
                busy = true;
                LoadCourses(email, password, true);
            }
        }

        private void LoadCourses(string email, string password, bool forceRefreshOfCourseList)
        {
            App.Crawler = new Coursera.Crawler(email, password, Cache.GetFiles(), Cache.SaveFile, forceRefreshOfCourseList);

            activeCourses.ItemsSource = null;
            upcomingCourses.ItemsSource = null;
            completedCourses.ItemsSource = null;
            activeCoursesEmptyMessage.Visibility = Visibility.Collapsed;
            upcomingCoursesEmptyMessage.Visibility = Visibility.Collapsed;
            completedCoursesEmptyMessage.Visibility = Visibility.Collapsed;
            App.Crawler.Courses.Display(
                this,
                "Loading courses...",
                "No courses",
                messageTextBlock,
                courses =>
                {
                    var active = new List<Coursera.Course>();
                    var upcoming = new List<Coursera.Course>();
                    var completed = new List<Coursera.Course>();
                    foreach (var course in courses)
                    {
                        if (course.HasFinished)
                            completed.Add(course);
                        else if (course.HasStarted)
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
                () => busy = false);
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
                Body = "Put your feedback here"
            };
            task.Show();
        }
    }
}