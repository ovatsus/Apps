using System;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Shell;

namespace UKTrains
{
    public static class LazyAsyncExtensions
    {
        public static CancellationTokenSource Display<T>(this FSharp.Control.LazyAsync<T[]> lazyAsync, Page target, string loadingMessage, bool refreshing, string emptyMessage, TextBlock messageTextBlock, Action<T[]> display, Action onFinished)
        {
            var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = loadingMessage };
            SystemTray.SetProgressIndicator(target, indicator);
            if (!refreshing)
            {
                messageTextBlock.Text = loadingMessage;
                messageTextBlock.Visibility = Visibility.Visible;
            }
            return lazyAsync.GetValueAsync(
                values =>
                {
                    if (values.Length == 0)
                    {
                        messageTextBlock.Text = emptyMessage;
                        messageTextBlock.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        messageTextBlock.Visibility = Visibility.Collapsed;
                        messageTextBlock.Text = "";
                    }
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    display(values);
                    onFinished();
                },
                exn =>
                {
                    if (!(exn is WebException))
                    {
                        LittleWatson.ReportException(exn, loadingMessage);
                        LittleWatson.CheckForPreviousException(false);
                    }
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    if (!refreshing)
                    {
                        var message = exn.Message;
                        if (message.Length > 500)
                        {
                            message = message.Substring(0, 500) + " ...";
                        }
                        messageTextBlock.Text = message;
                    }
                    onFinished();
                },
                true);
        }
    }
}
