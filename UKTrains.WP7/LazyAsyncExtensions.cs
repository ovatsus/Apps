using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Shell;

namespace UKTrains
{
    public static class LazyAsyncExtensions
    {
        public static void Display<T>(this FSharp.Control.LazyAsync<T[]> lazyAsync, Page target, string loadingMessage, bool refreshing, string emptyMessage, TextBlock messageTextBlock, Action<T[]> display, Action onFinished)
        {
            var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = loadingMessage };
            SystemTray.SetProgressIndicator(target, indicator);
            if (!refreshing)
            {
                messageTextBlock.Text = loadingMessage;
                messageTextBlock.Visibility = Visibility.Visible;
            }
            lazyAsync.GetValueAsync(
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
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    if (!refreshing)
                    {
                        messageTextBlock.Text = exn.Message;
                    }
                    onFinished();
                });
        }
    }
}
