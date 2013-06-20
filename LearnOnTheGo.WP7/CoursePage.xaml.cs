using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

namespace LearnOnTheGo
{
    public partial class CoursePage : PhoneApplicationPage
    {
        public CoursePage()
        {
            InitializeComponent();
        }

        private int courseId;
        private bool busy;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (App.Crawler == null)
            {
                // app was tombstoned
                NavigationService.GoBack();
                return;
            }

            if (pivot.Items.Count != 0) {
                return;
            }

            busy = true;

            courseId = int.Parse(NavigationContext.QueryString["courseId"]);
            var course = App.Crawler.GetCourse(courseId);
            pivot.Title = course.Topic.Name;

            Load();
        }

        private void Load()
        {
            pivot.ItemsSource = null;
            App.Crawler.GetCourse(courseId).LectureSections.Display(
                this,
                "Loading lectures...",
                "No lectures",
                messageTextBlock,
                lectureSections => pivot.ItemsSource = lectureSections,
                () => busy = false);
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (busy)
            {
                return;
            }
            busy = true;

            App.Crawler.RefreshCourse(courseId);
            Load();
        }

        private void OnLectureVideoClick(object sender, RoutedEventArgs e)
        {
            if (busy)
            {
                return;
            }
            busy = true;

            var lecture = (Coursera.Lecture)((Button)sender).DataContext;

            lecture.VideoUrl.Display(
                this,
                "Loading video...",
                videoUrl =>
                {
                    var launcher = new MediaPlayerLauncher();
                    launcher.Media = new Uri(videoUrl, UriKind.Absolute);
                    launcher.Show();
                },
                () => busy = false);
        }

        private void OnLecturePdfClick(object sender, RoutedEventArgs e)
        {
            if (busy)
            {
                return;
            }
            busy = true;

            var lecture = (Coursera.Lecture)((Button)sender).DataContext;

            var task = new WebBrowserTask();
            task.Uri = new Uri(lecture.PdfUrl, UriKind.Absolute);
            task.Show();

            busy = false;
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