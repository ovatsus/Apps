using System.Collections.Generic;
using System.IO;
using Common.WP8;

namespace LearnOnTheGo.WP8
{
    public static class Cache
    {
        private const string CacheFolder = "Cache";

        public static void SaveFile(string filename, string contents)
        {
            lock (typeof(Cache))
            {
                IsolatedStorage.WriteAllText(CacheFolder + "/" + filename, contents);
            }
        }

        public static IDictionary<string, string> GetFiles()
        {
            lock (typeof(Cache))
            {
                var files = new Dictionary<string, string>();
                foreach (var filename in IsolatedStorage.GetFiles(CacheFolder))
                {
                    files.Add(Path.GetFileName(filename), IsolatedStorage.ReadAllText(filename));
                }
                return files;
            }
        }

        public static void DeleteAllFiles()
        {
            lock (typeof(Cache))
            {
                foreach (var filename in IsolatedStorage.GetFiles(CacheFolder))
                {
                    IsolatedStorage.Delete(filename);
                }
            }
        }
    }
}
