using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Shell;

namespace UKTrains
{
    public static class LazyAsyncExtensions
    {
        public static void Display<T>(this FSharp.Control.LazyAsync<T[]> lazyAsync, Page target, string loadingMessage, string emptyMessage, TextBlock messageTextBlock, Action<T[]> display, Action onFinished)
        {
            var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = loadingMessage };
            SystemTray.SetProgressIndicator(target, indicator);
            messageTextBlock.Text = loadingMessage;
            messageTextBlock.Visibility = Visibility.Visible;
            lazyAsync.GetValueAsync(
                values =>
                {
                    if (values.Length == 0)
                    {
                        messageTextBlock.Text = emptyMessage;
                    }
                    else
                    {
                        messageTextBlock.Visibility = Visibility.Collapsed;
                        messageTextBlock.Text = "";
                        display(values);
                    }
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    onFinished();
                },
                exn =>
                {
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    messageTextBlock.Text = exn.Message;
                    onFinished();
                });
        }
    }
}
