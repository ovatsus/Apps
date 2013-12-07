using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Xml;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using SharpGIS;

namespace Common.WP8
{
    public class AppMetadata
    {
        public readonly string Name;
        public readonly string Email;
        public readonly bool UsesLocation;
        public readonly string MapAuthenticationToken;

        public string Version
        {
            get { return GetManifestAttributeValue("Version"); }
        }

        public Guid AppId
        {
            get { return Guid.Parse(GetManifestAttributeValue("ProductID")); }
        }

        public bool RunningInBackground { get; private set; }

        public static AppMetadata Current { get; private set; }

        public AppMetadata(Application application, string name, string email, bool usesLocation = false, string mapAuthenticationToken = null)
        {
            Name = name;
            Email = email;
            UsesLocation = usesLocation;
            MapAuthenticationToken = mapAuthenticationToken;

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
            ErrorReporting.Log("Navigated " + e.NavigationMode + " " + e.Uri);
            if (e.NavigationMode == NavigationMode.Reset)
                RootFrame.Navigated += ClearBackStackAfterReset;
        }

        private void ClearBackStackAfterReset(object sender, NavigationEventArgs e)
        {
            RootFrame.Navigated -= ClearBackStackAfterReset;

            if (e.NavigationMode != NavigationMode.New && e.NavigationMode != NavigationMode.Refresh)
                return;

            ErrorReporting.Log("Clearing back stack");
            while (RootFrame.RemoveBackEntry() != null);
        }

        private static string GetManifestAttributeValue(string attributeName)
        {
            var xmlReaderSettings = new XmlReaderSettings
            {
                XmlResolver = new XmlXapResolver()
            };

            using (var xmlReader = XmlReader.Create("WMAppManifest.xml", xmlReaderSettings))
            {
                xmlReader.ReadToDescendant("App");
                return xmlReader.GetAttribute(attributeName);
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
