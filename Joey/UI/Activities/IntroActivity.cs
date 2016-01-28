using System;
using System.Collections.Generic;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;
using Android.Content;
using System.Threading.Tasks;
using XPlatUtils;
using Toggl.Phoebe.Net;
using Toggl.Phoebe.Logging;

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

        private ViewPager IntroPager;
        private Button LoginButton;
        private Button StartNowButton;
        private readonly Handler handler = new Handler ();
        private bool IsAuthenticating;

        protected override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);

            SetContentView (Resource.Layout.IntroLayout);
            IntroPager = FindViewById<ViewPager> (Resource.Id.IntroPager);
            LoginButton = FindViewById<Button> (Resource.Id.LoginButton).SetFont (Font.DINMedium);;
            StartNowButton = FindViewById<Button> (Resource.Id.StartNowButton).SetFont (Font.DINMedium);
            LoginButton.Click += LoginButtonClick;
            StartNowButton.Click += StartNowButtonClick;

            var fragments = GetFragments ();
            IntroPager.Adapter = new IntroAdapter (FragmentManager, fragments);
            IntroPager.CurrentItem = 3;
            SwitchPager();
        }

        private void LoginButtonClick (object sender, EventArgs e)
        {
            var intent = new Intent (this, typeof (LoginActivity));
            intent.AddFlags (ActivityFlags.ClearTop);
            StartActivity (intent);
            Finish ();
        }

        private async void StartNowButtonClick (object sender, EventArgs e)
        {
            await SetUpNoUserAccountAsync ();
        }

        private async Task SetUpNoUserAccountAsync ()
        {
            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.SetupNoUserAsync ();
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to set up offline mode.");
                return;
            } finally {
                IsAuthenticating = false;
            }

            StartAuthActivity ();
        }

        protected override bool StartAuthActivity ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (authManager.IsAuthenticated) {
                // Try to avoid flickering of buttons during activity transition by
                // faking that we're still authenticating
                IsAuthenticating = true;

                var intent = new Intent (this, typeof (MainDrawerActivity));
                intent.AddFlags (ActivityFlags.ClearTop);
                StartActivity (intent);
                Finish ();
                return true;
            }

            return false;
        }

        private void SwitchPager ()
        {
            var pageNumber = IntroPager.CurrentItem;
            pageNumber++;

            if (pageNumber == 4) {
                pageNumber = 0;
            }
            IntroPager.SetCurrentItem (pageNumber, true);

            handler.RemoveCallbacks (SwitchPager);
            handler.PostDelayed (SwitchPager, 5000);
        }

        private List<Fragment> GetFragments ()
        {
            var fr = new List<Fragment> ();
            fr.Add (IntroScreenFragment.NewInstance (Resource.String.IntroSlideTitle1));
            fr.Add (IntroScreenFragment.NewInstance (Resource.String.IntroSlideTitle2));
            fr.Add (IntroScreenFragment.NewInstance (Resource.String.IntroSlideTitle3));
            fr.Add (IntroScreenFragment.NewInstance (Resource.String.IntroSlideTitle4));
            return fr;
        }

        private class IntroAdapter : FragmentPagerAdapter
        {
            private readonly List<Fragment> fragments;

            public IntroAdapter (FragmentManager fm, List<Fragment> fragments) : base (fm)
            {
                this.fragments = fragments;
            }

            public override int Count
            {
                get {
                    return fragments.Count;
                }
            }

            public override Fragment GetItem (int position)
            {
                return fragments[position];
            }
        }

        private class IntroScreenFragment : Fragment
        {
            private TextView TitleView;
            private readonly int titleResource;

            public static IntroScreenFragment NewInstance (int title)
            {
                return new IntroScreenFragment (title);
            }

            public IntroScreenFragment (int title)
            {
                titleResource = title;
            }

            public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
            {
                var view = inflater.Inflate (Resource.Layout.IntroScreen, container, false);
                TitleView = view.FindViewById<TextView> (Resource.Id.IntroTitle).SetFont (Font.DINMedium);;
                TitleView.SetText (titleResource);
                return view;
            }
        }
    }
}
