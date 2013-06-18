using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Shell;

namespace LearnOnTheGo
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
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    messageTextBlock.Text = message;
                    onFinished();
                });
        }

        public static void Display<T>(this FSharp.Control.LazyAsync<T> lazyAsync, Page target, string loadingMessage, Action<T> display, Action onFinished)
        {
            var indicator = new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = loadingMessage };
            SystemTray.SetProgressIndicator(target, indicator);
            lazyAsync.GetValueAsync(
                value =>
                {
                    display(value);
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    onFinished();
                },
                exn =>
                {
                    indicator.IsVisible = false;
                    indicator.IsIndeterminate = false;
                    onFinished();
                });
        }
    }
}
