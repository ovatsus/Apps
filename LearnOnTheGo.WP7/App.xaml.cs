using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Navigation;

namespace LearnOnTheGo
{
    public partial class App : Application
    {
        public PhoneApplicationFrame RootFrame { get; private set; }

        public static Coursera.Crawler Crawler { get; set; }

        public App()
        {
            UnhandledException += Application_UnhandledException;

            InitializeComponent();

            InitializePhoneApplication();

            WebRequest.RegisterPrefix("http://", SharpGIS.WebRequestCreator.GZip);
            WebRequest.RegisterPrefix("https://", SharpGIS.WebRequestCreator.GZip);

            if (Debugger.IsAttached)
            {
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }
        }

        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
        }

        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
        }

        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
        }

        private void Application_Closing(object sender, ClosingEventArgs e)
        {
        }

        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            LittleWatson.ReportException(e.Exception, "NavigationFailed: " + e.Uri);
        }

        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            LittleWatson.ReportException(e.ExceptionObject, "UnhandledException");
            LittleWatson.CheckForPreviousException(false);
            e.Handled = true;
        }

        private bool phoneApplicationInitialized = false;

        private void InitializePhoneApplication()
        {
            if (phoneApplicationInitialized)
                return;

            RootFrame = new TransitionFrame();
            RootFrame.Navigated += CompleteInitializePhoneApplication;

            RootFrame.NavigationFailed += RootFrame_NavigationFailed;

            phoneApplicationInitialized = true;
        }

        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            if (RootVisual != RootFrame)
                RootVisual = RootFrame;

            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }
    }
}