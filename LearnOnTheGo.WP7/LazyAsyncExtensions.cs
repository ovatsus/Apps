using System;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Shell;

namespace LearnOnTheGo
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
                    var message = exn.Message;
                    var webException = exn as WebException;
                    if (webException != null)
                    {
                        if (webException.Response != null && webException.Response.ResponseUri.IsAbsoluteUri && webException.Response.ResponseUri.AbsoluteUri == "https://www.coursera.org/maestro/api/user/login")
                        {
                            message = "Login did not work, please check your email and password in the Settings page and try again";
                        }
                        else if (message.Contains("Sorry, you are not allowed to access this course site at the moment. Please contact a system administrator for more information"))
                        {
                            // happens to some courses that have already finished, like introduction to finance
                            message = emptyMessage;
                        }
                    }
                    else
                    {
                        LittleWatson.ReportException(exn, loadingMessage);
                        LittleWatson.CheckForPreviousException(false);
                    }
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    if (!refreshing)
                    {
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

        public static CancellationTokenSource Display<T>(this FSharp.Control.LazyAsync<T> lazyAsync, Page target, string loadingMessage, Action<T> display, Action onFinished)
        {
            var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = loadingMessage };
            SystemTray.SetProgressIndicator(target, indicator);
            return lazyAsync.GetValueAsync(
                value =>
                {
                    display(value);
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
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
                    onFinished();
                },
                true);
        }
    }
}
