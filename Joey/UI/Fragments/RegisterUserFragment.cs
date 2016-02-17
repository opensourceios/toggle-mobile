using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Accounts;
using Android.App;
using Android.Content;
using Android.Gms.Auth;
using Android.Gms.Common;
using Android.OS;
using Android.Text;
using Android.Text.Style;
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
using DialogFragment = Android.Support.V4.App.DialogFragment;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;


namespace Toggl.Joey.UI.Fragments
{
    public class RegisterUserFragment : Fragment
    {
        private const string LogTag = "RegisterUserFragment";

        private LinearLayout RegisterFormLayout;
        private EditText EmailEditText;
        private EditText PasswordEditText;
        private Button PasswordToggleButton;
        private Button RegisterButton;
        private Button GoogleRegisterButton;
        private ImageView SpinningImage;
        private LinearLayout RegisterSuccessLayout;
        private Button SuccessTimerButton;
        private TextView LegalTextView;

        private bool isAuthenticating;
        private bool showPassword;
        private ISpannable formattedLegalText;
        private bool hasGoogleAccounts;

        private void FindViews (View v)
        {
            EmailEditText = v.FindViewById<EditText> (Resource.Id.CreateUserEmailEditText).SetFont (Font.Roboto);
            PasswordEditText = v.FindViewById<EditText> (Resource.Id.CreateUserPasswordEditText).SetFont (Font.Roboto);
            RegisterButton = v.FindViewById<Button> (Resource.Id.CreateUserButton).SetFont (Font.Roboto);
            SpinningImage = v.FindViewById<ImageView> (Resource.Id.RegisterLoadingImageView);
            RegisterFormLayout = v.FindViewById<LinearLayout> (Resource.Id.RegisterForm);
            RegisterSuccessLayout = v.FindViewById<LinearLayout> (Resource.Id.RegisterSuccessScreen);
            PasswordToggleButton = v.FindViewById<Button> (Resource.Id.RegisterPasswordToggleButton).SetFont (Font.Roboto);
            SuccessTimerButton = v.FindViewById<Button> (Resource.Id.GoToTimerButton);
            LegalTextView = v.FindViewById<TextView> (Resource.Id.RegisterLegalTextView);
            GoogleRegisterButton = v.FindViewById<Button> (Resource.Id.GoogleRegisterButton);
            var spinningImageAnimation = AnimationUtils.LoadAnimation (Activity.BaseContext, Resource.Animation.SpinningAnimation);
            SpinningImage.StartAnimation (spinningImageAnimation);
            SpinningImage.ImageAlpha = 0;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.RegisterUserFragment, container, false);
            FindViews (view);

            LegalTextView.SetText (FormattedLegalText, TextView.BufferType.Spannable);
            LegalTextView.MovementMethod = Android.Text.Method.LinkMovementMethod.Instance;

            hasGoogleAccounts = GoogleAccounts.Count > 0;
            GoogleRegisterButton.Visibility = hasGoogleAccounts ? ViewStates.Visible : ViewStates.Gone;

            EmailEditText.TextChanged += OnEmailEditTextTextChanged;
            PasswordEditText.TextChanged += OnPasswordEditTextTextChanged;
            GoogleRegisterButton.Click += OnGoogleRegisterButtonClick;
            SuccessTimerButton.Click += GoToTimerButtonClick;
            RegisterButton.Click += OnRegisterButtonClick;
            PasswordToggleButton.Click += OnPasswordToggleButtonClick;

            SyncRegisterButton ();
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

        private void SyncRegisterButton ()
        {
            var enabled = !isAuthenticating;

            if (String.IsNullOrWhiteSpace (EmailEditText.Text) || !EmailEditText.Text.Contains ('@')) {
                enabled = false;
            } else if (String.IsNullOrWhiteSpace (PasswordEditText.Text) || PasswordEditText.Text.Length < 6) {
                enabled = false;
            }

            RegisterButton.Enabled = enabled;
        }

        private void SyncPasswordVisibility ()
        {
            if (PasswordEditText.Text.Length == 0) {
                // Reset buttons and mask
                PasswordToggleButton.Visibility = ViewStates.Gone;
                showPassword = false;
            } else if (showPassword) {
                PasswordToggleButton.SetText (Resource.String.LoginHideButtonText);
                PasswordToggleButton.Visibility = ViewStates.Visible;
            } else {
                PasswordToggleButton.SetText (Resource.String.LoginShowButtonText);
                PasswordToggleButton.Visibility = ViewStates.Visible;
            }

            int selectionStart = PasswordEditText.SelectionStart;
            int selectionEnd = PasswordEditText.SelectionEnd;

            var passwordInputType = PasswordEditText.InputType;
            if (showPassword) {
                passwordInputType = (passwordInputType & ~InputTypes.TextVariationPassword) | InputTypes.TextVariationVisiblePassword;
            } else {
                passwordInputType = (passwordInputType & ~InputTypes.TextVariationVisiblePassword) | InputTypes.TextVariationPassword;
            }
            if (PasswordEditText.InputType != passwordInputType) {
                PasswordEditText.InputType = passwordInputType;

                // Need to reset font after changing input type
                PasswordEditText.SetFont (Font.RobotoLight);

                // Restore cursor position:
                PasswordEditText.SetSelection (selectionStart, selectionEnd);
            }
        }

        private void OnEmailEditTextTextChanged (object sender, TextChangedEventArgs e)
        {
            SyncRegisterButton ();
        }

        private void OnPasswordEditTextTextChanged (object sender, TextChangedEventArgs e)
        {
            SyncPasswordVisibility ();
            SyncRegisterButton ();
        }

        private void OnPasswordToggleButtonClick (object sender, EventArgs e)
        {
            showPassword = !showPassword;
            SyncPasswordVisibility ();
        }

        private void OnGoogleRegisterButtonClick (object sender, EventArgs e)
        {
            var accounts = GoogleAccounts;
            if (accounts.Count == 1) {
                GoogleAuthFragment.Start (FragmentManager, accounts [0]);
            } else if (accounts.Count > 1) {
                var dia = new GoogleAccountSelectionDialogFragment ();
                dia.Show (FragmentManager, "accounts_dialog");
            }
        }

        private List<string> GoogleAccounts
        {
            get {
                var am = AccountManager.Get (Activity.BaseContext);
                return am.GetAccounts ()
                       .Where (a => a.Type == GoogleAuthUtil.GoogleAccountType)
                       .Select (a => a.Name)
                       .Distinct ()
                       .ToList ();
            }
        }

        private bool IsAuthenticating
        {
            get {
                return isAuthenticating;
            } set {
                if (isAuthenticating == value) {
                    return;
                }
                isAuthenticating = value;
                SyncContent ();
            }
        }

        private void SyncContent ()
        {
            // Views not loaded yet/anymore?
            if (RegisterButton == null) {
                return;
            }

            EmailEditText.Enabled = !isAuthenticating;
            PasswordEditText.Enabled = !isAuthenticating;
            GoogleRegisterButton.Enabled = !isAuthenticating;

            SyncRegisterButton ();
        }

        private async void OnRegisterButtonClick (object sender, EventArgs e)
        {
            RegisterButton.Text = String.Empty;
            SpinningImage.ImageAlpha = 255;

            await TrySignupPasswordAsync ();
            RegisterButton.SetText (Resource.String.CreateUserButtonText);
            SpinningImage.ImageAlpha = 0;
        }

        private void GoToTimerButtonClick (object sender, EventArgs e)
        {
            var intent = new Intent (Activity, typeof (MainDrawerActivity));
            intent.AddFlags (ActivityFlags.ClearTop);
            StartActivity (intent);
            Activity.Finish ();
        }

        private async Task TrySignupPasswordAsync ()
        {
            if (IsAuthenticating) {
                return;
            }

            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.RegisterNoUserEmailAsync (EmailEditText.Text, PasswordEditText.Text);
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to signup user with password.");
                return;
            } finally {
                IsAuthenticating = false;
            }
            if (authRes != AuthResult.Success) {
                EmailEditText.RequestFocus ();
                ShowAuthError (EmailEditText.Text, authRes);
            } else {
                ServiceContainer.Resolve<ISyncManager> ().RunUpload ();
                RegisterSuccessLayout.Visibility = ViewStates.Visible;
                RegisterFormLayout.Visibility = ViewStates.Gone;
            }
        }

        private void ShowAuthError (string email, AuthResult res, bool googleAuth=false)
        {
            DialogFragment dia = null;

            switch (res) {
            case AuthResult.InvalidCredentials:
                if (!googleAuth) {
                    dia = new SignupFailedDialogFragment ();
                }
                break;
            case AuthResult.NetworkError:
                dia = new NetworkErrorDialogFragment ();
                break;
            default:
                dia = new SystemErrorDialogFragment ();
                break;
            }

            if (dia != null) {
                dia.Show (FragmentManager, "auth_result_dialog");
            }
        }

        //Google login section
        public class GoogleAuthFragment : Fragment
        {
            private static readonly int GoogleAuthRequestCode = 1;
            private static readonly string GoogleOAuthScope =
                "oauth2:https://www.googleapis.com/auth/userinfo.profile " +
                "https://www.googleapis.com/auth/userinfo.email";
            private static readonly string EmailArgument = "com.toggl.timer.email";

            public static void Start (FragmentManager mgr, string email)
            {
                // Find old fragment to replace
                var frag = mgr.FindFragmentByTag ("google_auth");
                if (frag != null) {
                    var authFrag = frag as GoogleAuthFragment;
                    if (authFrag != null && authFrag.IsAuthenticating) {
                        // Authentication going on still, do nothing.
                        return;
                    }
                }

                var tx = mgr.BeginTransaction ();
                if (frag != null) {
                    tx.Remove (frag);
                }
                tx.Add (new GoogleAuthFragment (email), "google_auth");
                tx.Commit ();
            }

            public GoogleAuthFragment (string email)
            {
                var args = new Bundle ();
                args.PutString (EmailArgument, email);
                Arguments = args;
            }

            public GoogleAuthFragment ()
            {
            }

            public GoogleAuthFragment (IntPtr javaRef, Android.Runtime.JniHandleOwnership transfer) : base (javaRef, transfer)
            {
            }

            public async override void OnCreate (Bundle savedInstanceState)
            {
                base.OnCreate (savedInstanceState);

                RetainInstance = true;
                await StartAuthAsync ();
            }

            public async override void OnActivityResult (int requestCode, int resultCode, Intent data)
            {
                base.OnActivityResult (requestCode, resultCode, data);

                if (requestCode == GoogleAuthRequestCode) {
                    if (resultCode == (int)Result.Ok) {
                        await StartAuthAsync ();
                    }
                }
            }

            private async Task StartAuthAsync ()
            {
                if (IsAuthenticating) {
                    return;
                }
                IsAuthenticating = true;

                try {
                    var log = ServiceContainer.Resolve<ILogger> ();
                    var authManager = ServiceContainer.Resolve<AuthManager> ();
                    var ctx = Activity;

                    // No point in trying to reauth when old authentication is still running.
                    if (authManager.IsAuthenticating) {
                        return;
                    }

                    // Workaround for Android linker bug which forgets to register JNI types
                    Java.Interop.TypeManager.RegisterType ("com/google/android/gms/auth/GoogleAuthException", typeof (GoogleAuthException));
                    Java.Interop.TypeManager.RegisterType ("com/google/android/gms/auth/GooglePlayServicesAvailabilityException", typeof (GooglePlayServicesAvailabilityException));
                    Java.Interop.TypeManager.RegisterType ("com/google/android/gms/auth/UserRecoverableAuthException", typeof (UserRecoverableAuthException));
                    Java.Interop.TypeManager.RegisterType ("com/google/android/gms/auth/UserRecoverableNotifiedException", typeof (UserRecoverableNotifiedException));

                    String token = null;
                    try {
                        token = await Task.Factory.StartNew (() => GoogleAuthUtil.GetToken (ctx, Email, GoogleOAuthScope));
                    } catch (GooglePlayServicesAvailabilityException exc) {
                        var dia = GooglePlayServicesUtil.GetErrorDialog (
                                      exc.ConnectionStatusCode, ctx, GoogleAuthRequestCode);
                        dia.Show ();
                    } catch (UserRecoverableAuthException exc) {
                        StartActivityForResult (exc.Intent, GoogleAuthRequestCode);
                    } catch (Java.IO.IOException exc) {
                        // Connectivity error.. nothing to do really?
                        log.Info (LogTag, exc, "Failed to login with Google due to network issues.");
                    } catch (Exception exc) {
                        log.Error (LogTag, exc, "Failed to get access token for '{0}'.", Email);
                    }

                    // Failed to get token
                    if (token == null) {
                        return;
                    }

                    try {
                        var authRes = await authManager.RegisterNoUserGoogleAsync (token);

                        if (authRes != AuthResult.Success) {
                            ClearGoogleToken (ctx, token);
                            var dia = new SignupFailedDialogFragment ();
                            dia.Show (FragmentManager, "auth_result_dialog");
                        }

                    } catch (InvalidOperationException ex) {
                        log.Info (LogTag, ex, "Failed to authenticate user with Google login.");
                    }
                } finally {
                    IsAuthenticating = false;
                }

                // Clean up self:
                if (Activity != null) {
                    FragmentManager.BeginTransaction ()
                    .Remove (this)
                    .Commit ();
                }
            }

            private void ClearGoogleToken (Context ctx, string token)
            {
                var log = ServiceContainer.Resolve<ILogger> ();

                ThreadPool.QueueUserWorkItem (delegate {
                    try {
                        GoogleAuthUtil.ClearToken (ctx, token);
                    } catch (Exception ex) {
                        log.Warning (LogTag, ex, "Failed to authenticate user with Google login.");
                    }
                });
            }

            private string Email
            {
                get {
                    return Arguments != null ? Arguments.GetString (EmailArgument) : null;
                }
            }
            private bool IsAuthenticating { get; set; }
        }

        public class GoogleAccountSelectionDialogFragment : DialogFragment
        {
            private ArrayAdapter<string> accountsAdapter;

            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                accountsAdapter = MakeAccountsAdapter ();

                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginAccountsDialogTitle)
                       .SetAdapter (MakeAccountsAdapter (), OnAccountSelected)
                       .SetNegativeButton (Resource.String.LoginAccountsDialogCancelButton, OnCancelButtonClicked)
                       .Create ();
            }

            private void OnAccountSelected (object sender, DialogClickEventArgs args)
            {
                var email = accountsAdapter.GetItem (args.Which);
                GoogleAuthFragment.Start (FragmentManager, email);
            }

            private void OnCancelButtonClicked (object sender, DialogClickEventArgs args)
            {
            }

            private ArrayAdapter<string> MakeAccountsAdapter ()
            {
                var am = AccountManager.Get (Activity);
                var emails = am.GetAccounts ()
                             .Where ((a) => a.Type == GoogleAuthUtil.GoogleAccountType)
                             .Select ((a) => a.Name)
                             .Distinct ()
                             .ToList ();

                return new ArrayAdapter<string> (Activity, Android.Resource.Layout.SimpleListItem1, emails);
            }
        }

        public class InvalidCredentialsDialogFragment : DialogFragment
        {
            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginInvalidCredentialsDialogTitle)
                       .SetMessage (Resource.String.LoginInvalidCredentialsDialogText)
                       .SetPositiveButton (Resource.String.LoginInvalidCredentialsDialogOk, OnOkButtonClicked)
                       .Create ();
            }

            private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
            {
            }
        }

        public class SignupFailedDialogFragment : DialogFragment
        {
            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginSignupFailedDialogTitle)
                       .SetMessage (Resource.String.LoginSignupFailedDialogText)
                       .SetPositiveButton (Resource.String.LoginSignupFailedDialogOk, OnOkButtonClicked)
                       .Create ();
            }

            private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
            {
            }
        }

        public class NoAccountDialogFragment : DialogFragment
        {
            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginNoAccountDialogTitle)
                       .SetMessage (Resource.String.LoginNoAccountDialogText)
                       .SetPositiveButton (Resource.String.LoginNoAccountDialogOk, OnOkButtonClicked)
                       .Create ();
            }

            private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
            {
            }
        }

        public class NetworkErrorDialogFragment : DialogFragment
        {
            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginNetworkErrorDialogTitle)
                       .SetMessage (Resource.String.LoginNetworkErrorDialogText)
                       .SetPositiveButton (Resource.String.LoginNetworkErrorDialogOk, OnOkButtonClicked)
                       .Create ();
            }

            private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
            {
            }
        }

        public class SystemErrorDialogFragment : DialogFragment
        {
            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginSystemErrorDialogTitle)
                       .SetMessage (Resource.String.LoginSystemErrorDialogText)
                       .SetPositiveButton (Resource.String.LoginSystemErrorDialogOk, OnOkButtonClicked)
                       .Create ();
            }

            private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
            {
            }
        }

        private ISpannable FormattedLegalText
        {
            get {
                if (formattedLegalText == null) {
                    var template = Resources.GetText (Resource.String.LoginSignupLegalText);
                    var arg0 = Resources.GetText (Resource.String.LoginSignupLegalTermsText);
                    var arg1 = Resources.GetText (Resource.String.LoginSignupLegalPrivacyText);

                    var arg0idx = String.Format (template, "{0}", arg1).IndexOf ("{0}", StringComparison.Ordinal);
                    var arg1idx = String.Format (template, arg0, "{1}").IndexOf ("{1}", StringComparison.Ordinal);

                    var s = formattedLegalText = new SpannableString (String.Format (template, arg0, arg1));
                    var mode = SpanTypes.InclusiveExclusive;
                    s.SetSpan (
                        new TogglURLSPan (Phoebe.Build.TermsOfServiceUrl.ToString ()),
                        arg0idx,
                        arg0idx + arg0.Length,
                        mode
                    );

                    s.SetSpan (
                        new TogglURLSPan (Phoebe.Build.PrivacyPolicyUrl.ToString ()),
                        arg1idx,
                        arg1idx + arg1.Length,
                        mode
                    );
                }

                return formattedLegalText;
            }
        }

        private class TogglURLSPan : URLSpan
        {
            public TogglURLSPan (String url) : base (url)
            {
            }

            public override void UpdateDrawState (TextPaint ds)
            {
                base.UpdateDrawState (ds);
                ds.UnderlineText = false;
                ds.SetTypeface (Android.Graphics.Typeface.DefaultBold);
            }
        }
    }
}
