using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;

namespace LearnOnTheGo.WP8
{
    public static class Cache
    {
        public static void SaveFile(string filename, string contents)
        {
            lock (typeof(Cache))
            {
                using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!isolatedStorage.DirectoryExists("Cache"))
                    {
                        isolatedStorage.CreateDirectory("Cache");
                    }
                    using (var stream = isolatedStorage.CreateFile("Cache/" + filename))
                    {
                        using (var streamWriter = new StreamWriter(stream))
                        {
                            streamWriter.Write(contents);
                        }
                    }
                }
            }
        }

        public static IDictionary<string, string> GetFiles()
        {
            lock (typeof(Cache))
            {
                using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    var files = new Dictionary<string, string>();
                    if (isolatedStorage.DirectoryExists("Cache"))
                    {
                        foreach (var filename in isolatedStorage.GetFileNames("Cache/*"))
                        {
                            using (var stream = isolatedStorage.OpenFile("Cache/" + filename, FileMode.Open))
                            {
                                using (var streamReader = new StreamReader(stream))
                                {
                                    var content = streamReader.ReadToEnd();
                                    files.Add(filename, content);
                                }
                            }
                        }
                    }
                    return files;
                }
            }
        }

        public static void DeleteAllFiles()
        {
            lock (typeof(Cache))
            {
                using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (isolatedStorage.DirectoryExists("Cache"))
                    {
                        foreach (var filename in isolatedStorage.GetFileNames("Cache/*"))
                        {
                            isolatedStorage.DeleteFile("Cache/" + filename);
                        }
                    }
                }
            }
        }
    }
}
