using Microsoft.Phone.Info;
using Microsoft.Phone.Tasks;
using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Windows;

namespace LearnOnTheGo
{
    public class LittleWatson
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
                            output.WriteLine(ex.Message);
                            output.WriteLine(ex.StackTrace);
                        }
                        output.WriteLine();
                    }
                }
            }
            catch
            {
            }
        }

        public static void CheckForPreviousException()
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
                    if (MessageBox.Show("A problem occurred the last time you ran this application. Would you like to send an email to report it?", "Problem Report", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        var email = new EmailComposeTask();
                        email.To = "learnonthego@codebeside.org";
                        email.Subject = "Learn On The Go auto-generated problem report";
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
            return "\n\n" + contents + "\n" +
                   "App Version: " + new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version + "\n" +
                   "OS Version: " + Environment.OSVersion.Version + "\n" +
                   "Phone: " + DeviceStatus.DeviceManufacturer + " " + DeviceStatus.DeviceName;
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
    }
}