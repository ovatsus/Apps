using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NokiaFeedbackDemo.Helpers
{
    public enum FeedbackState
    {
        Inactive,
        FirstReview,
        SecondReview,
        Feedback
    }

    /// <summary>
    /// This helper class controls the behaviour of the FeedbackOverlay control
    /// When the app has been launched 5 times the initial prompt is shown
    /// If the user reviews no more prompts are shown
    /// When the app has bee launched 10 times and not been reviewed, the prompt is shown
    /// </summary>
    public class FeedbackHelper
    {
        private const string LAUNCH_COUNT = "LAUNCH_COUNT";
        private const string REVIEWED = "REVIEWED";
        private const int FIRST_COUNT = 5;
        private const int SECOND_COUNT = 10;

        private int _launchCount = 0;
        private bool _reviewed = false;

        public static readonly FeedbackHelper Default = new FeedbackHelper();

        private FeedbackState _state = FeedbackState.Inactive;

        public FeedbackState State
        {
            get { return this._state; }
            set { this._state = value; }
        }

        private FeedbackHelper()
        {

        }

        /// <summary>
        /// This should only be called when the app is Launching
        /// </summary>
        public void Launching()
        {
            var license = new Microsoft.Phone.Marketplace.LicenseInformation();

            // Only load state if not trial
            if(!license.IsTrial())
                this.LoadState();

            // Uncomment for testing
            // this._state = FeedbackState.FirstReview;
            // this._state = FeedbackState.SecondReview;
        }

        /// <summary>
        /// Loads last state from storage and works out the new state
        /// </summary>
        private void LoadState()
        {
            try
            {
                this._launchCount = StorageHelper.GetSetting<int>(LAUNCH_COUNT);
                this._reviewed = StorageHelper.GetSetting<bool>(REVIEWED);

                if (!this._reviewed)
                {
                    this._launchCount++;

                    if (this._launchCount == FIRST_COUNT)
                        this._state = FeedbackState.FirstReview;
                    else if (this._launchCount == SECOND_COUNT)
                        this._state = FeedbackState.SecondReview;

                    this.StoreState();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("FeedbackHelper.LoadState - Failed to load state, Exception: {0}", ex.ToString()));
            }
        }

        /// <summary>
        /// Stores current state
        /// </summary>
        private void StoreState()
        {
            try
            {
                StorageHelper.StoreSetting(LAUNCH_COUNT, this._launchCount, true);
                StorageHelper.StoreSetting(REVIEWED, this._reviewed, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("FeedbackHelper.StoreState - Failed to store state, Exception: {0}", ex.ToString()));
            }
        }

        /// <summary>
        /// Call when user has reviewed
        /// </summary>
        public void Reviewed()
        {
            this._reviewed = true;

            this.StoreState();
        }
    }
}
