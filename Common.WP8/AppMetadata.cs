using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Xml;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using Windows.ApplicationModel.Store;

namespace Common.WP8
{
    public class AppMetadata
    {
        public readonly string Name;
        public readonly string Version;
        public readonly string Email;
        public readonly bool UsesLocation;
        public readonly string MapAuthenticationToken;
        public readonly Func<string> GetExtraErrorReportingInfo;
        
        public Guid AppId
        {
            get { return CurrentApp.AppId; }
        }

        public bool RunningInBackground { get; private set; }

        public static AppMetadata Current { get; private set; }

        public AppMetadata(Application application, string name, string version, string email, bool usesLocation = false, string mapAuthenticationToken = null, Func<string> getExtraErrorReportingInfo = null)
        {
            Resources.getResourceStreamFunc = (resourceName, assemblyName) => 
                AppDomain.CurrentDomain.GetAssemblies().First(asm => asm.GetName().Name == assemblyName).GetManifestResourceStream(resourceName);

            Name = name;
            Version = version;
            Email = email;
            UsesLocation = usesLocation;
            MapAuthenticationToken = mapAuthenticationToken;
            GetExtraErrorReportingInfo = getExtraErrorReportingInfo;

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

            if (Debugger.IsAttached)
            {
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }
        }

        public static PhoneApplicationFrame RootFrame { get; private set; }

        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
            ErrorReporting.Log("Launching");
        }

        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
            ErrorReporting.Log("Activated IsApplicationInstancePreserved=" + e.IsApplicationInstancePreserved);
            AppMetadata.Current.RunningInBackground = false;
        }

        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
            ErrorReporting.Log("Deactivated Reason=" + e.Reason);
        }

        private void Application_Closing(object sender, ClosingEventArgs e)
        {
            ErrorReporting.Log("Closing");
        }

        private void Application_RunningInBackground(object sender, RunningInBackgroundEventArgs args)
        {
            ErrorReporting.Log("RunningInBackground");
            AppMetadata.Current.RunningInBackground = true;
        }

        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            ErrorReporting.ReportException(e.Exception, "NavigationFailed: " + e.Uri);
        }

        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            ErrorReporting.ReportException(e.ExceptionObject, "UnhandledException");
            ErrorReporting.CheckForPreviousException(false);
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
            RootFrame.Navigating += RootFrame_Navigating;
            RootFrame.Navigated += RootFrame_Navigated;
            phoneApplicationInitialized = true;
        }

        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            if (Application.Current.RootVisual != RootFrame)
                Application.Current.RootVisual = RootFrame;

            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }

        private bool resetting;

        private void RootFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            ErrorReporting.Log("Navigating " + e.NavigationMode + " " + e.Uri);
            if (resetting)
            {
                resetting = false;
                if (e.Uri.OriginalString.Contains("?"))
                {
                    // respect live tile destination
                }
                else
                {
                    // just resume, cancel navigation
                    ErrorReporting.Log("Cancelling navigation");
                    e.Cancel = true;
                }
            }
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            ErrorReporting.Log("Navigated " + e.NavigationMode + " " + e.Uri);
            if (e.NavigationMode == NavigationMode.Reset)
            {
                resetting = true;
            }
        }

        public static void CheckForReview(Page page)
        {
            var installationDate = Settings.GetDateTime(Setting.InstallationDate);
            if (!installationDate.HasValue)
            {
                Settings.Set(Setting.InstallationDate, DateTime.UtcNow);
            }
            else if (!Settings.GetBool(Setting.RatingDone))
            {
                bool shouldAskForReview;
                var daysSinceInstallation = (DateTime.UtcNow - installationDate.Value).TotalDays;
                var lastAskForReviewDate = Settings.GetDateTime(Setting.LastAskForReviewDate);
                // ask on day 2^n + 1, for n >= 0 (2, 3, 5, 9, 17, 33, etc...)
                if (!lastAskForReviewDate.HasValue)
                {
                    shouldAskForReview = daysSinceInstallation >= 1;
                }
                else
                {
                    var daysSinceLastAskForReview = (DateTime.UtcNow - lastAskForReviewDate.Value).TotalDays;
                    shouldAskForReview = daysSinceLastAskForReview * 2 >= daysSinceInstallation;
                }
                if (shouldAskForReview)
                {
                    Settings.Set(Setting.LastAskForReviewDate, DateTime.UtcNow);
                    if (Extensions.ShowMessageBox("Enjoying " + AppMetadata.Current.Name + "?", "If you find this app useful please rate it. Reviews encourage me to keep improving.",
                                                  "Rate and Review", "Maybe later"))
                    {
                        ErrorReporting.Log("MarketplaceReviewTaskShow from Prompt");
                        new MarketplaceReviewTask().Show();
                        Settings.Set(Setting.RatingDone, true);
                    }
                }
            }
        }

        public static void CheckForNewVersion()
        {
#if DEBUG
            return;
#endif
            var lastNewVersionCheck = Settings.GetDateTime(Setting.LastNewVersionCheck);
            if (!lastNewVersionCheck.HasValue)
            {
                Settings.Set(Setting.LastNewVersionCheck, DateTime.UtcNow);
                return;
            }
            if ((DateTime.UtcNow - lastNewVersionCheck.Value).TotalDays < 7)
            {
                //only check once a week
                return;
            }
            try
            {
                var cultureInfoName = CultureInfo.CurrentUICulture.Name;

                var url = string.Format("http://marketplaceedgeservice.windowsphone.com/v8/catalog/apps/{0}?os={1}&cc={2}&oc=&lang={3}​",
                    AppMetadata.Current.AppId.ToString("D"),
                    Environment.OSVersion.Version,
                    cultureInfoName.Substring(cultureInfoName.Length - 2).ToUpperInvariant(),
                    cultureInfoName);

                var request = WebRequest.Create(url);
                request.BeginGetResponse(result =>
                {
                    try
                    {
                        var response = (HttpWebResponse)request.EndGetResponse(result);
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            using (var outputStream = response.GetResponseStream())
                            {
                                using (var reader = XmlReader.Create(outputStream))
                                {
                                    reader.MoveToContent();

                                    var aNamespace = reader.LookupNamespace("a");

                                    reader.ReadToFollowing("entry", aNamespace);

                                    reader.ReadToDescendant("version");

                                    var updatedVersion = new Version(reader.ReadElementContentAsString());
                                    var currentVersion = new Version(AppMetadata.Current.Version);

                                    Settings.Set(Setting.LastNewVersionCheck, DateTime.UtcNow);

                                    if (updatedVersion > currentVersion)
                                    {
                                        AppMetadata.RootFrame.Dispatcher.BeginInvoke(() =>
                                        {
                                            if (Extensions.ShowMessageBox("Update Available", "A new version of " + AppMetadata.Current.Name + " is available. Do you want to install it now?", "Install Update", "Maybe later"))
                                            {
                                                ErrorReporting.Log("MarketplaceDetailTaskShow from Prompt");
                                                new MarketplaceDetailTask().Show();
                                            }
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                },
                null);
            }
            catch { }
        }
    }
}
