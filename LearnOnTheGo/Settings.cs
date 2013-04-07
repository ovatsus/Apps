using System.IO.IsolatedStorage;
using System.Security.Cryptography;
using System.Text;

namespace LearnOnTheGo
{
    public enum Setting
    {
        Email,
        Password,
    }

    public static class Settings
    {
        public static string Get(Setting setting)
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

        public static void Set(Setting setting, string value)
        {
            var settingName = setting.ToString();
            var bytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectedData.Protect(bytes, null);
            IsolatedStorageSettings.ApplicationSettings[settingName] = encryptedBytes;
        }
    }
}
