using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FSharp.Control;
using Microsoft.Phone.Shell;

namespace UKTrains
{
    public class LazyBlockUI : ILazyBlockUI
    {
        private Page page;
        private ItemsControl itemsControl;
        private TextBlock messageTextBlock;
        private TextBlock lastUpdatedTextBlock;

        public LazyBlockUI(Page page, ItemsControl itemsControl, TextBlock messageTextBlock, TextBlock lastUpdatedTextBlock)
        {
            this.page = page;
            this.itemsControl = itemsControl;
            this.messageTextBlock = messageTextBlock;
            this.lastUpdatedTextBlock = lastUpdatedTextBlock;
        }

        private ProgressIndicator GetIndicator()
        {
            return SystemTray.GetProgressIndicator(page) ?? new ProgressIndicator();
        }

        public string GlobalProgressMessage
        {
            get { return GetIndicator().Text ?? ""; }
            set
            {
                var indicator = GetIndicator();
                indicator.Text = value;
                if (value != "")
                {                    
                    indicator.IsVisible = true;
                    indicator.IsIndeterminate = true;
                    SystemTray.SetProgressIndicator(page, indicator);
                }
                else
                {
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    SystemTray.SetProgressIndicator(page, null);
                }
            }
        }

        public string LocalProgressMessage
        {
            set
            {
                messageTextBlock.Text = value;
                messageTextBlock.Visibility = value != "" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool HasItems
        {
            get { return itemsControl.ItemsSource != null; }
        }

        public void SetItems<T>(T[] items)
        {
            itemsControl.ItemsSource = items;
        }

        public string LastUpdated
        {
            set {
                if (lastUpdatedTextBlock != null)
                {
                    lastUpdatedTextBlock.Text = value;
                }
            }
        }

        private DispatcherTimer timer;

        public void StartTimer(Action action)
        {
            StopTimer();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(60);
            timer.Tick += (sender, args) => action();
            timer.Start();
        }

        public void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        public void OnException(string message, Exception e)
        {
            LittleWatson.ReportException(e, message);
            LittleWatson.CheckForPreviousException(false);
        }
    }
}
