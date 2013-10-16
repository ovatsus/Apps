using System;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using Windows.ApplicationModel.Store;

namespace Trains.WP8
{
    public partial class AboutPage : PhoneApplicationPage
    {
        public AboutPage()
        {
            InitializeComponent();
            version.Text = "Version " + LittleWatson.AppVersion;
            pivotItem.Header = AppMetadata.Current.Name;
            run.Text = AppMetadata.Current.Name;
        }

        private void OnRateAndReviewClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnRateAndReviewClick");
            new MarketplaceReviewTask().Show();
            Settings.Set(Setting.RatingDone, true);
        }

        private void OnSendFeedbackClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnSendFeedbackClick");
            new EmailComposeTask
            {
                To = AppMetadata.Current.Email,
                Subject = "Feedback for " + AppMetadata.Current.Name + " " + LittleWatson.AppVersion,
                Body = LittleWatson.GetMailBody("")
            }.Show();
        }

        private async void OnDonateClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnDonateCick");
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

        private static readonly string _appLink = "http://windowsphone.com/s?appid=" + LittleWatson.AppId.ToString("D");
        private static readonly string _title = "Check out \"" + AppMetadata.Current.Name + "\" for Windows Phone";
        private static readonly string _fullMessage = _title + " " + _appLink;


        private void OnShareBySMSClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnShareBySMSClick");
            new SmsComposeTask { Body = _fullMessage }.Show();
        }

        private void OnShareByEmailClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnShareByEmailClick");
            new EmailComposeTask { Subject = _title, Body = _appLink }.Show();
        }

        private void OnShareBySocialNetworksClick(object sender, EventArgs e)
        {
            LittleWatson.Log("OnShareBySocialNetworksClick");
            new ShareLinkTask { Title = _title, LinkUri = new Uri(_appLink) }.Show();
        }
    }
}