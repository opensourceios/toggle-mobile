using System;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;

namespace Toggl.Joey.UI.Fragments
{
    public class RegisterUserFragment : Fragment
    {
        private const string LogTag = "RegisterUserFragment";

        private EditText EmailEditText;
        private EditText PasswordEditText;
        private Button RegisterButton;
        private bool isAuthenticating;
        private ImageView SpinningImage;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.RegisterUserFragment, container, false);
            EmailEditText = view.FindViewById<EditText> (Resource.Id.CreateUserEmailEditText).SetFont (Font.Roboto);
            PasswordEditText = view.FindViewById<EditText> (Resource.Id.CreateUserPasswordEditText).SetFont (Font.Roboto);



            RegisterButton = view.FindViewById<Button> (Resource.Id.CreateUserButton).SetFont (Font.Roboto);
            SpinningImage = view.FindViewById<ImageView> (Resource.Id.RegisterLoadingImageView);
            Animation spinningImageAnimation = AnimationUtils.LoadAnimation (Activity.BaseContext, Resource.Animation.SpinningAnimation);
            SpinningImage.StartAnimation (spinningImageAnimation);
            SpinningImage.ImageAlpha = 0;
            RegisterButton.Click += OnRegisterClick;
            return view;
        }

        public override void OnCreate (Bundle state)
        {
            base.OnCreate (state);
            RetainInstance = true;
        }

        public override void OnStart ()
        {
            base.OnStart ();
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "RegisterUser";
        }

        private async void OnRegisterClick (object sender, EventArgs e)
        {
            RegisterButton.Text = "";
            SpinningImage.ImageAlpha = 255;
            await TrySignupPasswordAsync ();
            SpinningImage.ImageAlpha = 0;
            RegisterButton.SetText (Resource.String.CreateUserButtonText);
        }

        private bool IsAuthenticating
        {
            set {
                if (isAuthenticating == value) {
                    return;
                }
                isAuthenticating = value;
            }
        }

        private async Task TrySignupPasswordAsync ()
        {
            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            try {
                await authManager.RegisterFromNouserAsync (EmailEditText.Text, PasswordEditText.Text);
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to signup user with password.");
                return;
            } finally {
                ServiceContainer.Resolve<ISyncManager> ().UploadUserData ();
                IsAuthenticating = false;

                var intent = new Intent (Activity, typeof (MainDrawerActivity));
                intent.AddFlags (ActivityFlags.ClearTop);
                StartActivity (intent);
                Activity.Finish ();
            }
        }
    }
}
