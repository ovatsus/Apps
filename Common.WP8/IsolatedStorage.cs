using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;

namespace Common.WP8
{
    public class IsolatedStorage
    {
        public static void Delete(string filename)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.FileExists(filename))
                {
                    isolatedStorage.DeleteFile(filename);
                }
            }
        }

        public static void Move(string from, string to)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.FileExists(from))
                {
                    if (isolatedStorage.FileExists(to))
                    {
                        isolatedStorage.DeleteFile(to);
                    }
                    isolatedStorage.MoveFile(from, to);
                }
            }
        }

        public static void WriteToFile(string filename, Action<Stream> f)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                var folder = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(folder))
                {
                    if (!isolatedStorage.DirectoryExists(folder))
                    {
                        isolatedStorage.CreateDirectory(folder);
                    }
                }
                if (isolatedStorage.FileExists(filename))
                {
                    isolatedStorage.DeleteFile(filename);
                }
                using (var stream = isolatedStorage.CreateFile(filename))
                {
                    f(stream);
                }
            }
        }

        public static void WriteAllText(string filename, string content)
        {
            WriteToFile(filename, stream =>
            {
                using (var streamWriter = new StreamWriter(stream))
                {
                    streamWriter.Write(content);
                }
            });
        }

        public static void AppendText(string filename, string content)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                var folder = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(folder))
                {
                    if (!isolatedStorage.DirectoryExists(folder))
                    {
                        isolatedStorage.CreateDirectory(folder);
                    }
                }
                using (var stream = isolatedStorage.OpenFile(filename, FileMode.Append, FileAccess.Write))
                {
                    using (var streamWriter = new StreamWriter(stream))
                    {
                        streamWriter.Write(content);
                    }
                }
            }
        }

        public static Stream OpenFileToRead(string filename)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                return isolatedStorage.OpenFile(filename, FileMode.Open);
            }
        }
        
        public static string ReadAllText(string filename)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.FileExists(filename))
                {
                    using (var stream = isolatedStorage.OpenFile(filename, FileMode.Open))
                    {
                        using (var streamReader = new StreamReader(stream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public static IEnumerable<string> GetFiles(string folder, string extensionFilter = null)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.DirectoryExists(folder))
                {
                    foreach (var filename in isolatedStorage.GetFileNames(folder + "/*" + extensionFilter))
                    {
                        yield return folder + "/" + filename;
                    }
                }
            }
        }

        public static bool FileExists(string filename)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                return isolatedStorage.FileExists(filename);
            }
        }
    }
}
