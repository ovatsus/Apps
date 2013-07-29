using System;
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

namespace UKTrains
{
    public static class LittleWatson
    {
        private const string filename = "LittleWatson.txt";

        public static void ReportException(Exception ex, string header)
        {
            try
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var output = new StreamWriter(store.OpenFile(filename, FileMode.Append, FileAccess.Write)))
                    {
                        output.WriteLine(header);
                        if (ex != null)
                        {
                            output.WriteLine(ex.ToString());
                        }
                        output.WriteLine();
                    }
                }
            }
            catch
            {
            }
        }

        public static void CheckForPreviousException(bool startingUp)
        {
            try
            {
                string contents = null;
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(filename))
                    {
                        using (var reader = new StreamReader(store.OpenFile(filename, FileMode.Open, FileAccess.Read, FileShare.None)))
                        {
                            contents = reader.ReadToEnd();
                        }
                        SafeDeleteFile(store);
                    }
                }
                if (contents != null)
                {
                    var title = "A problem occurred" + (startingUp ? " the last time you ran this application" : "") + ". Would you like to send an email to report it?";
                    if (MessageBox.Show(title, "Problem Report", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        var email = new EmailComposeTask();
                        email.To = "uktrains@codebeside.org";
                        email.Subject = "UK Trains auto-generated problem report";
                        if (contents.Length > 32000)
                        {
                            contents = contents.Substring(0, 32000) + " ...";
                        }
                        email.Body = GetMailBody(contents);
                        email.Show();
                    }
                }
            }
            catch
            {
            }
        }

        public static string GetMailBody(string contents)
        {
            return "\n\n" + contents + "\n\n" +
                   "App Version: " + new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version + "\n" +
                   "OS Version: " + Environment.OSVersion.Version + "\n" +
                   "Phone: " + DeviceStatus.DeviceManufacturer + " " + DeviceStatus.DeviceName + "\n" +
                   "Culture: " + CultureInfo.CurrentCulture + "/" + CultureInfo.CurrentUICulture;
        }

        private static void SafeDeleteFile(IsolatedStorageFile store)
        {
            try
            {
                store.DeleteFile(filename);
            }
            catch
            {
            }
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