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
using GalaSoft.MvvmLight.Helpers;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.ViewModels;
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

        private Binding<bool, bool> isAuthenticatedBinding;

        public IntroViewModel ViewModel { get; private set; }

        protected override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);

            SetContentView (Resource.Layout.IntroLayout);
            FindViewById<TextView> (Resource.Id.IntroTitle).SetFont (Font.DINMedium);
            FindViewById<TextView> (Resource.Id.IntroLoginText).SetFont (Font.DINMedium);
            LoginButton = FindViewById<Button> (Resource.Id.LoginButton).SetFont (Font.DINMedium);
            StartNowButton = FindViewById<Button> (Resource.Id.StartNowButton).SetFont (Font.DINMedium);
            LoginButton.Click += LoginButtonClick;
            StartNowButton.Click += StartNowButtonClick;

            ViewModel = IntroViewModel.Init ();
            isAuthenticatedBinding = this.SetBinding (() => ViewModel.IsAuthenticated).WhenSourceChanges (StartAuth);
        }

        private void LoginButtonClick (object sender, EventArgs e)
        {
            StartActivity (new Intent (this, typeof (LoginActivity)));
        }

        private async void StartNowButtonClick (object sender, EventArgs e)
        {
            await ViewModel.SetUpNoUserAccountAsync ();
        }

        private void StartAuth ()
        {
            StartAuthActivity ();
        }

        protected override bool StartAuthActivity ()
        {
            if (ViewModel == null) {
                return false;
            }

            if (ViewModel.IsAuthenticated) {
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
