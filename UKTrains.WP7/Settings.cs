using System.IO.IsolatedStorage;
using System.Security.Cryptography;
using System.Text;

namespace UKTrains
{
    public enum Setting
    {
        LocationServicesEnabled,
        LocationServicesPromptShown,
        CurrentLat,
        CurrentLong,
        RecentStations,
        UseMilesInsteadOfKMs,
        RatingDone,
        InstallationDate,
    }

    public static class Settings
    {
        public static string GetString(Setting setting)
        {
            var settingName = setting.ToString();
            if (IsolatedStorageSettings.ApplicationSettings.Contains(settingName))
            {
                var encryptedBytes = (byte[])IsolatedStorageSettings.ApplicationSettings[settingName];
                var bytes = ProtectedData.Unprotect(encryptedBytes, null);
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }
            else
            {
                return "";
            }
        }

        public static bool GetBool(Setting setting)
        {
            return GetString(setting) == "true";
        }

        public static double GetDouble(Setting setting)
        {
            var value = GetString(setting);
            return value == "" ? double.NaN : double.Parse(value);
        }

        public static void Set(Setting setting, string value)
        {
            var settingName = setting.ToString();
            var bytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectedData.Protect(bytes, null);
            IsolatedStorageSettings.ApplicationSettings[settingName] = encryptedBytes;
        }

        public static void Set(Setting setting, bool value)
        {
            Set(setting, value ? "true" : "false");
        }

        public static void Set(Setting setting, double value)
        {
            Set(setting, double.IsNaN(value) ? "" : value.ToString());
        }
    }
}
