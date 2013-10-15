using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Microsoft.Phone.Info;
using Microsoft.Phone.Tasks;

namespace Trains.WP8
{
    public static class LittleWatson
    {
        private const string filename = "LittleWatson.txt";

        private static string ToString(DateTime d)
        {
            return d.TimeOfDay.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
        }

        public static void ReportException(Exception ex, string header)
        {
            try
            {
                using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var output = new StreamWriter(isolatedStorage.OpenFile(filename, FileMode.Append, FileAccess.Write)))
                    {
                        output.WriteLine("[" + ToString(DateTime.UtcNow) + "]" + " " + header);
                        if (ex != null)
                        {
                            output.WriteLine(ex.ToString());
                        }
                        output.WriteLine();
                    }
                }
            }
            catch { }
        }

        private static List<Tuple<DateTime, string>> log = new List<Tuple<DateTime, string>>();

        public static void Log(string message)
        {
            log.Add(Tuple.Create(DateTime.UtcNow, message));
        }

        public static void CheckForPreviousException(bool startingUp)
        {
            string contents = null;
            try
            {
                using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (isolatedStorage.FileExists(filename))
                    {
                        using (var reader = new StreamReader(isolatedStorage.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.None)))
                        {
                            contents = reader.ReadToEnd();
                        }
                        isolatedStorage.DeleteFile(filename);
                    }
                }
            }
            catch { }
            if (contents != null)
            {
                var title = "A problem occurred" + (startingUp ? " the last time you ran this application" : "") + ". Would you like to send an email to report it?";
                if (MessageBox.Show(title, "Problem Report", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    var email = new EmailComposeTask();
                    email.To = AppMetadata.Current.Email;
                    email.Subject = AppMetadata.Current.Name + " " + AppVersion + " auto-generated problem report";
                    email.Body = GetMailBody(contents);
                    email.Show();
                }
            }
        }

        public static string AppVersion
        {
            get { return new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version.ToString(); }
        }

        public static string GetMailBody(string contents)
        {
            if (log.Count > 0)
            {
                if (contents.Length > 0)
                {
                    contents += "\n\n";
                }
                foreach (var item in log)
                {
                    contents += "[" + ToString(item.Item1) + "]" + " " + item.Item2 + "\n";
                }
            }
            if (contents.Length > 32000)
            {
                contents = contents.Substring(0, 32000) + " ...";
            }
            return "\n\n" + contents + "\n\n" +
                   "App Version: " + AppVersion + "\n" +
                   "OS Version: " + Environment.OSVersion.Version + "\n" +
                   "Phone: " + DeviceStatus.DeviceManufacturer + " " + DeviceStatus.DeviceName + "\n" +
                   "Culture: " + CultureInfo.CurrentCulture + "/" + CultureInfo.CurrentUICulture + "\n" +
                   "Location Services Enabled: " + Settings.GetBool(Setting.LocationServicesEnabled);
        }

        public static Guid AppId
        {
            get { return Guid.Parse(GetManifestAttributeValue("ProductID")); }
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

        public static void CheckForNewVersion(Page page)
        {
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
                    GetManifestAttributeValue("ProductID"),
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
                                    var currentVersion = new Version(GetManifestAttributeValue("Version"));

                                    Settings.Set(Setting.LastNewVersionCheck, DateTime.UtcNow);

                                    if (updatedVersion > currentVersion)
                                    {
                                        page.Dispatcher.BeginInvoke(() =>
                                        {
                                            if (MessageBox.Show("Do you want to install the new version now?", "Update Available", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                                            {
                                                LittleWatson.Log("MarketplaceDetailTaskShow from Prompt");
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