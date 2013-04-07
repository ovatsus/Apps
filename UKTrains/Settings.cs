using System.IO.IsolatedStorage;
using System.Security.Cryptography;
using System.Text;

namespace UKTrains
{
    public enum Setting
    {
        LocationServicesEnabled,
        LocationServicesPromptShown,
    }

    public static class Settings
    {
        public static bool Get(Setting setting)
        {
            var settingName = setting.ToString();
            if (IsolatedStorageSettings.ApplicationSettings.Contains(settingName))
            {
                var encryptedBytes = (byte[])IsolatedStorageSettings.ApplicationSettings[settingName];
                var bytes = ProtectedData.Unprotect(encryptedBytes, null);
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length) == "true";
            }
            else
            {
                return false;
            }
        }

        public static void Set(Setting setting, bool value)
        {
            var settingName = setting.ToString();
            var bytes = Encoding.UTF8.GetBytes(value ? "true" : "false");
            var encryptedBytes = ProtectedData.Protect(bytes, null);
            IsolatedStorageSettings.ApplicationSettings[settingName] = encryptedBytes;
        }
    }
}
