using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NokiaFeedbackDemo.Helpers
{
    public class StorageHelper
    {
        /// <summary>
        /// Stored a setting, returns true if success
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public static bool StoreSetting(string key, object value, bool overwrite)
        {
            if (overwrite || IsolatedStorageSettings.ApplicationSettings.Contains(key))
            {
                IsolatedStorageSettings.ApplicationSettings[key] = value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get a setting from storage, returns default value if it does not exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T GetSetting<T>(string key)
        {
            if (IsolatedStorageSettings.ApplicationSettings.Contains(key))
            {
                return (T)IsolatedStorageSettings.ApplicationSettings[key];
            }

            return default(T);
        }

        public static T GetSetting<T>(string key, T defaultVal)
        {
            if (IsolatedStorageSettings.ApplicationSettings.Contains(key))
            {
                return (T)IsolatedStorageSettings.ApplicationSettings[key];
            }

            return defaultVal;
        }

        public static void RemoveSetting(string key)
        {
            IsolatedStorageSettings.ApplicationSettings.Remove(key);
        }
    }
}
