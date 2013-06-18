using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;

namespace LearnOnTheGo
{
    public static class Cache
    {
        private static readonly IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication();

        public static void SaveFile(string filename, string contents)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                lock (isolatedStorage)
                {
                    if (!isolatedStorage.DirectoryExists("Cache"))
                    {
                        isolatedStorage.CreateDirectory("Cache");
                    }
                    using (var stream = isolatedStorage.CreateFile(Path.Combine("Cache", filename)))
                    {
                        using (var streamWriter = new StreamWriter(stream))
                        {
                            streamWriter.Write(contents);
                        }
                    }
                }
            });
        }

        public static IDictionary<string, string> GetFiles()
        {
            lock (isolatedStorage)
            {
                var files = new Dictionary<string, string>();
                if (isolatedStorage.DirectoryExists("Cache"))
                {
                    foreach (var filename in isolatedStorage.GetFileNames("Cache\\*"))
                    {
                        using (var stream = isolatedStorage.OpenFile(Path.Combine("Cache", filename), FileMode.Open))
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

        public static void DeleteAllFiles()
        {
            lock (isolatedStorage)
            {
                if (isolatedStorage.DirectoryExists("Cache"))
                {
                    foreach (var filename in isolatedStorage.GetFileNames("Cache\\*"))
                    {
                        isolatedStorage.DeleteFile(Path.Combine("Cache", filename));
                    }
                }
            }
        }
    }
}
