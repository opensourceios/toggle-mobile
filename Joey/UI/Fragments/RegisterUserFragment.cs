using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.Json;
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
        private Button submitButton;
        private bool isAuthenticating;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.RegisterUserFragment, container, false);
            EmailEditText = view.FindViewById<EditText> (Resource.Id.CreateUserEmailEditText).SetFont (Font.Roboto);
            PasswordEditText = view.FindViewById<EditText> (Resource.Id.CreateUserPasswordEditText).SetFont (Font.Roboto);

            submitButton = view.FindViewById<Button> (Resource.Id.CreateUserButton).SetFont (Font.Roboto);
            submitButton.Click += OnRegisterClick;

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
            await TrySignupPasswordAsync ();
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
