using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using SharpGIS;

namespace Trains.WP8
{
    public class AppMetadata
    {
        public readonly string Name;
        public readonly string Email;

        public bool RunningInBackground { get; private set; }

        public static AppMetadata Current { get; private set; }

        public AppMetadata(Application application, string name, string email)
        {
            Name = name;
            Email = email;

            Current = this;

            var phoneApplicationService = new PhoneApplicationService();
            phoneApplicationService.Activated += Application_Activated;
            phoneApplicationService.Closing += Application_Closing;
            phoneApplicationService.Deactivated += Application_Deactivated;
            phoneApplicationService.Launching += Application_Launching;
            phoneApplicationService.RunningInBackground += Application_RunningInBackground;
            application.ApplicationLifetimeObjects.Add(phoneApplicationService);

            application.UnhandledException += Application_UnhandledException;

            InitializePhoneApplication();

            WebRequest.RegisterPrefix("http://", WebRequestCreator.GZip);
            WebRequest.RegisterPrefix("https://", WebRequestCreator.GZip);

            if (Debugger.IsAttached)
            {
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }
        }

        public static PhoneApplicationFrame RootFrame { get; private set; }

        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
            LittleWatson.Log("Launching");
        }

        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
            LittleWatson.Log("Activated IsApplicationInstancePreserved=" + e.IsApplicationInstancePreserved);
            AppMetadata.Current.RunningInBackground = false;
        }

        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
            LittleWatson.Log("Deactivated Reason=" + e.Reason);
        }

        private void Application_Closing(object sender, ClosingEventArgs e)
        {
            LittleWatson.Log("Closing");
        }

        private void Application_RunningInBackground(object sender, RunningInBackgroundEventArgs args)
        {
            LittleWatson.Log("RunningInBackground");
            AppMetadata.Current.RunningInBackground = true;
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
            RootFrame.Navigated += RootFrame_Navigated;
            phoneApplicationInitialized = true;
        }

        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            if (Application.Current.RootVisual != RootFrame)
                Application.Current.RootVisual = RootFrame;

            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            LittleWatson.Log("Navigated " + e.NavigationMode + " " + e.Uri);
            if (e.NavigationMode == NavigationMode.Reset)
                RootFrame.Navigated += ClearBackStackAfterReset;
        }

        private void ClearBackStackAfterReset(object sender, NavigationEventArgs e)
        {
            RootFrame.Navigated -= ClearBackStackAfterReset;

            if (e.NavigationMode != NavigationMode.New && e.NavigationMode != NavigationMode.Refresh)
                return;

            while (RootFrame.RemoveBackEntry() != null);
        }
    }
}