using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;

namespace Toggl.Joey.UI.Activities
{
    [Activity (
         Exported = false,
         ScreenOrientation = ScreenOrientation.Portrait,
         WindowSoftInputMode = SoftInput.StateHidden,
         Theme = "@style/Theme.Toggl.Intro")]
    public class IntroActivity : BaseActivity
    {
        private const string LogTag = "IntroActivity";

        private Button LoginButton;
        private Button StartNowButton;
        private TextView LoginIntroText;
        private TextView IntroText;


        protected override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);

            SetContentView (Resource.Layout.IntroLayout);
            IntroText = FindViewById<TextView> (Resource.Id.IntroTitle).SetFont (Font.DINMedium);
            LoginButton = FindViewById<Button> (Resource.Id.LoginButton).SetFont (Font.DINMedium);
            LoginIntroText = FindViewById<TextView> (Resource.Id.IntroLoginText).SetFont (Font.DINMedium);
            StartNowButton = FindViewById<Button> (Resource.Id.StartNowButton).SetFont (Font.DINMedium);
            LoginButton.Click += LoginButtonClick;
            StartNowButton.Click += StartNowButtonClick;
        }

        private void LoginButtonClick (object sender, EventArgs e)
        {
            StartActivity (new Intent (this, typeof (LoginActivity)));
        }

        private async void StartNowButtonClick (object sender, EventArgs e)
        {
            await SetUpNoUserAccountAsync ();
        }

        private async Task SetUpNoUserAccountAsync ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.NoUserSetupAsync ();
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to set up offline mode.");
                return;
            } finally {
            }

            StartAuthActivity ();
        }

        protected override bool StartAuthActivity ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (authManager.IsAuthenticated) {
                // Try to avoid flickering of buttons during activity transition by
                // faking that we're still authenticating

                var intent = new Intent (this, typeof (MainDrawerActivity));
                intent.AddFlags (ActivityFlags.ClearTop);
                StartActivity (intent);
                Finish ();
                return true;
            }

            return false;
        }
    }
}
