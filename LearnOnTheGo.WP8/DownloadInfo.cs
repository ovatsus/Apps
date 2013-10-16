using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Phone.BackgroundTransfer;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo.WP8
{
    public class DownloadInfo : INotifyPropertyChanged, IDownloadInfo
    {
        private readonly int _courseId;
        private readonly string _courseTopicName;
        private readonly int _lectureId;
        private readonly string _lectureTitle;

        private const string TransfersFolder = "shared/transfers/";
        private const string DoneSuffix = ".done";
        private const string CourseTopicNameSuffix = ".courseTopicName";
        private const string LectureTitleSuffix = ".lectureTitle";

        private DownloadInfo(int courseId, string courseTopicName, int lectureId, string lectureTitle)
        {
            _courseId = courseId;
            _courseTopicName = courseTopicName;
            _lectureId = lectureId;
            _lectureTitle = lectureTitle;
            RefreshStatus();

            var filename = GetBaseFilename();
            var existingRequest = BackgroundTransferService.Requests.FirstOrDefault(req => req.Tag == filename);
            if (existingRequest != null)
            {
                StartDownload(existingRequest);
            }
        }

        public static IDownloadInfo Create(int courseId, string courseTopicName, int lectureId, string lectureTitle)
        {
            return new DownloadInfo(courseId, courseTopicName, lectureId, lectureTitle);
        }

        public int CourseId { get { return _courseId; } }
        public string CourseTopicName { get { return _courseTopicName; } }
        public int LectureId { get { return _lectureId; } }
        public string LectureTitle { get { return _lectureTitle; } }

        public override int GetHashCode()
        {
            return CourseId ^ LectureId;
        }

        public override bool Equals(object obj)
        {
            var other = obj as DownloadInfo;
            return other != null && CourseId == other.CourseId && LectureId == other.LectureId;
        }

        private TransferMonitor _monitor;
        public TransferMonitor Monitor
        {
            get { return _monitor; }
            set
            {
                SetAndNotify(ref _monitor, value);
                Downloading = value != null;
            }
        }

        private bool _downloading;
        public bool Downloading
        {
            get { return _downloading; }
            set { SetAndNotify(ref _downloading, value); }
        }

        private bool _downloaded;
        public bool Downloaded
        {
            get { return _downloaded; }
            set { SetAndNotify(ref _downloaded, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetAndNotify<T>(ref T prop, T value, [CallerMemberName] string propertyName = "")
        {
            if (!Equals(prop, value))
            {
                prop = value;
                var propertyChanged = PropertyChanged;
                if (propertyChanged != null)
                {
                    propertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        public Uri VideoLocation
        {
            get { return new Uri(GetBaseFilename() + DoneSuffix, UriKind.Relative); }
        }

        private static string GetBaseFilename(int courseId, int lectureId)
        {
            return TransfersFolder + courseId + "_" + lectureId;
        }

        private string GetBaseFilename()
        {
            return GetBaseFilename(CourseId, LectureId);
        }

        public void RefreshStatus()
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                Downloaded = isolatedStorage.FileExists(GetBaseFilename() + DoneSuffix);
            }
        }

        private static void IsolatedStorageDelete(string filename)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.FileExists(filename))
                {
                    isolatedStorage.DeleteFile(filename);
                }
            }
        }

        private static void IsolatedStorageMove(string from, string to)
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

        private static void IsolatedStorageWriteAllText(string filename, string content)
        {
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.FileExists(filename))
                {
                    isolatedStorage.DeleteFile(filename);
                }
                using (var stream = isolatedStorage.CreateFile(filename))
                {
                    using (var streamWriter = new StreamWriter(stream))
                    {
                        streamWriter.Write(content);
                    }
                }
            }
        }

        private static string IsolatedStorageReadAllText(string filename)
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
            }
            return null;
        }

        private static void SafeRemoveRequest(BackgroundTransferRequest request)
        {
            try
            {
                BackgroundTransferService.Remove(request);
            }
            catch { }
            request.Dispose();
        }

        public void DeleteVideo()
        {
            IsolatedStorageDelete(GetBaseFilename() + DoneSuffix);
            IsolatedStorageDelete(GetBaseFilename() + CourseTopicNameSuffix);
            IsolatedStorageDelete(GetBaseFilename() + LectureTitleSuffix);
            RefreshStatus();
        }

        // this might be called more than once
        private void OnCompletion(BackgroundTransferRequest request)
        {
            lock (typeof(DownloadInfo))
            {
                Monitor = null;
                IsolatedStorageMove(GetBaseFilename(), GetBaseFilename() + DoneSuffix);
                RefreshStatus();
                SafeRemoveRequest(request);
            }
        }

        // this might be called more than once
        private void OnFailure(BackgroundTransferRequest request)
        {
            lock (typeof(DownloadInfo))
            {
                Monitor = null;
                IsolatedStorageDelete(GetBaseFilename());
                IsolatedStorageDelete(GetBaseFilename() + CourseTopicNameSuffix);
                IsolatedStorageDelete(GetBaseFilename() + LectureTitleSuffix);
            }
        }

        private void StartDownload(BackgroundTransferRequest request)
        {
            if (request.TransferStatus == TransferStatus.Completed)
            {
                OnCompletion(request);
            }
            else if (request.TransferStatus == TransferStatus.Unknown)
            {
                OnFailure(request);
            }
            else
            {
                Monitor = new TransferMonitor(request);
                Monitor.Complete += (_, args) => { OnCompletion(request); };
                Monitor.Failed += (_, args) => { OnFailure(request); };
                if (request.TransferStatus == TransferStatus.None)
                {
                    IsolatedStorageWriteAllText(GetBaseFilename() + CourseTopicNameSuffix, CourseTopicName);
                    IsolatedStorageWriteAllText(GetBaseFilename() + LectureTitleSuffix, LectureTitle);
                    Monitor.RequestStart();
                }
            }
        }

        public void QueueDowload(string videoUrl)
        {
            if (!Downloaded)
            {
                var filename = GetBaseFilename();
                var request = new BackgroundTransferRequest(new Uri(videoUrl), new Uri(filename, UriKind.Relative))
                {
                    Tag = filename,
                    TransferPreferences = TransferPreferences.AllowCellularAndBattery,
                };
                StartDownload(request);
            }
        }

        private static IDownloadInfo Get(string filename)
        {
            var parts = filename.Replace(DoneSuffix, null).Substring(filename.LastIndexOf('/') + 1).Split('_');
            var courseId = int.Parse(parts[0]);
            var lectureId = int.Parse(parts[1]);
            var courseTopicName = IsolatedStorageReadAllText(GetBaseFilename(courseId, lectureId) + CourseTopicNameSuffix) ?? "<Unknown Course>";
            var lectureTitle = IsolatedStorageReadAllText(GetBaseFilename(courseId, lectureId) + LectureTitleSuffix) ?? "<Unknown Lecture>";
            return Create(courseId, courseTopicName, lectureId, lectureTitle);
        }

        public static IEnumerable<IDownloadInfo> GetAll()
        {
            foreach (var request in BackgroundTransferService.Requests)
            {
                yield return Get(request.Tag);
            }
            using (var isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                foreach (var filename in isolatedStorage.GetFileNames(TransfersFolder + "*" + DoneSuffix))
                {
                    yield return Get(filename);
                }
            }
        }
    }
}