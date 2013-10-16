using System;
using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using Windows.ApplicationModel.Store;

namespace Common.WP8
{
    public partial class AboutPage : PhoneApplicationPage
    {
        public AboutPage()
        {
            InitializeComponent();
            version.Text = "Version " + AppMetadata.Current.Version;
            headerTextBlock.Text = AppMetadata.Current.Name;
            if (AppMetadata.Current.Name.Length > 14)
            {
                headerTextBlock.FontSize = 50;
                bigImage.Visibility = Visibility.Collapsed;
                smallImage.Visibility = Visibility.Visible;
                tellYourFriendsTextBlock.Text = "Tell your friends about " + AppMetadata.Current.Name;
            }
            else
            {                
                bigImage.Visibility = Visibility.Visible;
                smallImage.Visibility = Visibility.Collapsed;
                tellYourFriendsTextBlock.Text = "Tell your friends about the " + AppMetadata.Current.Name + " app";
            }
        }

        private void OnRateAndReviewClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnRateAndReviewClick");
            new MarketplaceReviewTask().Show();
            Settings.Set(Setting.RatingDone, true);
        }

        private void OnSendFeedbackClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnSendFeedbackClick");
            new EmailComposeTask
            {
                To = AppMetadata.Current.Email,
                Subject = "Feedback for " + AppMetadata.Current.Name + " " + AppMetadata.Current.Version,
                Body = ErrorReporting.GetMailBody("")
            }.Show();
        }

        private async void OnDonateClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnDonateCick");
            try
            {
                await CurrentApp.RequestProductPurchaseAsync("Donate", false);
                if (CurrentApp.LicenseInformation.ProductLicenses["Donate"].IsActive)
                {
                    CurrentApp.ReportProductFulfillment("Donate");
                }
            }
            catch { }
        }

        private static readonly string _appLink = "http://windowsphone.com/s?appid=" + AppMetadata.Current.AppId.ToString("D");
        private static readonly string _title = "Check out \"" + AppMetadata.Current.Name + "\" for Windows Phone";
        private static readonly string _fullMessage = _title + " " + _appLink;


        private void OnShareBySMSClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnShareBySMSClick");
            new SmsComposeTask { Body = _fullMessage }.Show();
        }

        private void OnShareByEmailClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnShareByEmailClick");
            new EmailComposeTask { Subject = _title, Body = _appLink }.Show();
        }

        private void OnShareBySocialNetworksClick(object sender, EventArgs e)
        {
            ErrorReporting.Log("OnShareBySocialNetworksClick");
            new ShareLinkTask { Title = _title, LinkUri = new Uri(_appLink) }.Show();
        }
    }
}