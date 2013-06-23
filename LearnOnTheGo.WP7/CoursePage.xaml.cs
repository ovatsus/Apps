using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using System;
using System.Threading;
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
        }

        private int courseId;
        private CancellationTokenSource lecturesCts;
        private CancellationTokenSource videoCts;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (App.Crawler == null)
            {
                // app was tombstoned
                NavigationService.GoBack();
                return;
            }

            if (pivot.Items.Count != 0)
            {
                return;
            }

            courseId = int.Parse(NavigationContext.QueryString["courseId"]);
            var course = App.Crawler.GetCourse(courseId);
            pivot.Title = course.Topic.Name;

            Load();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (lecturesCts != null)
            {
                lecturesCts.Cancel();
                lecturesCts = null;
            }
            if (videoCts != null)
            {
                videoCts.Cancel();
                videoCts = null;
            }
        }

        private void Load()
        {
            var refreshing = pivot.ItemsSource != null;
            lecturesCts = App.Crawler.GetCourse(courseId).LectureSections.Display(
                this,
                refreshing ? "Refreshing lectures..." : "Loading lectures...",
                refreshing,
                "No lectures",
                messageTextBlock,
                lectureSections => pivot.ItemsSource = lectureSections,
                () => lecturesCts = null);
        }

        private void OnRefreshClick(object sender, EventArgs e)
        {
            if (lecturesCts != null)
            {
                return;
            }

            App.Crawler.RefreshCourse(courseId);
            Load();
        }

        private void OnLectureVideoClick(object sender, RoutedEventArgs e)
        {
            if (videoCts != null)
            {
                return;
            }

            var lecture = (Coursera.Lecture)((Button)sender).DataContext;

            videoCts = lecture.VideoUrl.Display(
                this,
                "Loading video...",
                videoUrl =>
                {
                    var launcher = new MediaPlayerLauncher();
                    launcher.Media = new Uri(videoUrl, UriKind.Absolute);
                    launcher.Show();
                },
                () => videoCts = null);
        }

        private void OnLecturePdfClick(object sender, RoutedEventArgs e)
        {
            if (videoCts != null)
            {
                return;
            }

            var lecture = (Coursera.Lecture)((Button)sender).DataContext;

            var task = new WebBrowserTask();
            task.Uri = new Uri(lecture.PdfUrl, UriKind.Absolute);
            task.Show();
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