using System;
using System.ComponentModel;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using Coursera;
using Microsoft.Phone.BackgroundTransfer;
using Microsoft.Phone.Controls;

namespace LearnOnTheGo
{
    public class LectureExtraInfo : INotifyPropertyChanged
    {
        public static LectureExtraInfo Get(Lecture lecture)
        {
            return (LectureExtraInfo)lecture.ExtraInfo;
        }

        private int _courseId;
        public int CourseId
        {
            get { return _courseId; }
            set { SetAndNotify(ref _courseId, value); }
        }

        private Uri _downloadLocation;
        public Uri DownloadLocation
        {
            get { return _downloadLocation; }
            set { SetAndNotify(ref _downloadLocation, value); }
        }

        private TransferMonitor _monitor;
        public TransferMonitor Monitor
        {
            get { return _monitor; }
            set { SetAndNotify(ref _monitor, value); }
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

        public static void Init(Lecture lecture, int courseId)
        {
            LectureExtraInfo.Get(lecture).SetProperties(lecture, courseId);
        }

        private void SetProperties(Lecture lecture, int courseId)
        {
            var filename = GetVideoFilename(lecture, courseId);
            CourseId = courseId;
            Downloaded = IsolatedStorageFile.GetUserStoreForApplication().GetFileNames(filename).Length > 0;
            DownloadLocation = new Uri(filename, UriKind.Relative);
            var existingRequest = BackgroundTransferService.Requests.FirstOrDefault(req => req.Tag != null && req.Tag == filename);
            if (existingRequest != null)
            {
                SetMonitor(existingRequest);
            }
        }

        private void SetMonitor(BackgroundTransferRequest request) {
            Monitor = new TransferMonitor(request);
            Downloading = true;
            Monitor.Complete += delegate
            {
                BackgroundTransferService.Remove(request);
                Downloaded = true;
                Downloading = false;
                Monitor = null;
            };
            Monitor.Failed += delegate
            {
                BackgroundTransferService.Remove(request);
                Downloading = false;
                Monitor = null;
            };
        }

        private static string GetVideoFilename(Lecture lecture, int courseId)
        {
            return "shared/transfers/" + courseId + "_" + lecture.Id;
        }

        public void QueueDowload(string videoUrl)
        {
            var request = new BackgroundTransferRequest(new Uri(videoUrl), DownloadLocation)
            {
                TransferPreferences = TransferPreferences.AllowCellularAndBattery,
            };
            SetMonitor(request);
            Monitor.RequestStart();
        }
    }
}