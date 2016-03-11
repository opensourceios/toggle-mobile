using System;
using System.Collections.Generic;
using System.Linq;
using Android.Accounts;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Auth;
using Android.Gms.Common;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;
using DialogFragment = Android.Support.V4.App.DialogFragment;
using Toggl.Phoebe._ViewModels;
using GalaSoft.MvvmLight.Helpers;
using Android.Gms.Common.Apis;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Auth.Api;
using Android.Support.V4.App;

namespace Toggl.Joey.UI.Activities
{
    [Activity (
         Exported = false,
         ScreenOrientation = ScreenOrientation.Portrait,
         WindowSoftInputMode = SoftInput.StateHidden,
         Theme = "@style/Theme.Toggl.Login")]
    public class LoginActivity : FragmentActivity, ViewTreeObserver.IOnGlobalLayoutListener,
        GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener
    {
        const string LogTag = "LoginActivity";

        const int RC_SIGN_IN = 9001;
        const string KEY_IS_RESOLVING = "is_resolving";
        const string KEY_SHOULD_RESOLVE = "should_resolve";
        const string ExtraShowPassword = "com.toggl.timer.show_password";

        private GoogleApiClient mGoogleApiClient;
        private bool mIsResolving;
        private bool mShouldResolve;
        private bool hasGoogleAccounts;
        private bool showPassword;
        private ISpannable formattedLegalText;
        private int topLogoPosition;
        private ImageView bigLogo;

        protected LoginVM ViewModel { get; private set; }
        protected ScrollView ScrollView { get; private set; }
        protected Button SwitchModeButton { get; private set; }
        protected AutoCompleteTextView EmailEditText { get; private set; }
        protected EditText PasswordEditText { get; private set; }
        protected Button PasswordToggleButton { get; private set; }
        protected Button LoginButton { get; private set; }
        protected TextView LegalTextView { get; private set; }
        protected Button GoogleLoginButton { get; private set; }

        private Binding<bool, bool> isAuthencticatedBinding, isAuthenticatingBinding;
        private Binding<LoginVM.LoginMode, LoginVM.LoginMode> modeBinding;
        private Binding<AuthResult, AuthResult> resultBinding;

        private ArrayAdapter<string> MakeEmailsAdapter ()
        {
            var am = AccountManager.Get (this);
            var emails = am.GetAccounts ()
                         .Select (a => a.Name)
                         .Where (a => a.Contains ("@"))
                         .Distinct ()
                         .ToList ();
            return new ArrayAdapter<string> (this, Android.Resource.Layout.SelectDialogItem, emails);
        }

        void ViewTreeObserver.IOnGlobalLayoutListener.OnGlobalLayout ()
        {
            // Move scroll and let the logo visible.
            var position = new int[2];
            bigLogo.GetLocationInWindow (position);
            if (topLogoPosition == 0 && position[1] != 0) {
                topLogoPosition = position[1] + Convert.ToInt32 (bigLogo.Height * 0.2);
            }
            ScrollView.SmoothScrollTo (0, topLogoPosition);
        }

        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            SetContentView (Resource.Layout.LoginActivity);
            ScrollView = FindViewById<ScrollView> (Resource.Id.ScrollView);
            FindViewById<TextView> (Resource.Id.SwitchViewText).SetFont (Font.RobotoLight);
            bigLogo = FindViewById<ImageView> (Resource.Id.MainLogoLoginScreen);
            SwitchModeButton = FindViewById<Button> (Resource.Id.SwitchViewButton);
            EmailEditText = FindViewById<AutoCompleteTextView> (Resource.Id.EmailAutoCompleteTextView).SetFont (Font.RobotoLight);
            PasswordEditText = FindViewById<EditText> (Resource.Id.PasswordEditText).SetFont (Font.RobotoLight);
            PasswordToggleButton = FindViewById<Button> (Resource.Id.PasswordToggleButton).SetFont (Font.Roboto);
            LoginButton = FindViewById<Button> (Resource.Id.LoginButton).SetFont (Font.Roboto);
            LegalTextView = FindViewById<TextView> (Resource.Id.LegalTextView).SetFont (Font.RobotoLight);
            GoogleLoginButton = FindViewById<Button> (Resource.Id.GoogleLoginButton).SetFont (Font.Roboto);

            ScrollView.ViewTreeObserver.AddOnGlobalLayoutListener (this);

            LoginButton.Click += OnLoginButtonClick;
            GoogleLoginButton.Click += OnGoogleLoginButtonClick;
            EmailEditText.Adapter = MakeEmailsAdapter ();
            EmailEditText.Threshold = 1;
            EmailEditText.TextChanged += OnEmailEditTextTextChanged;
            PasswordEditText.TextChanged += OnPasswordEditTextTextChanged;
            PasswordToggleButton.Click += OnPasswordToggleButtonClick;
            SwitchModeButton.Click += OnModeToggleButtonClick;
            hasGoogleAccounts = GoogleAccounts.Count > 0;
            GoogleLoginButton.Visibility = hasGoogleAccounts ? ViewStates.Visible : ViewStates.Gone;

            if (savedInstanceState != null) {
                showPassword = savedInstanceState.GetBoolean (ExtraShowPassword);
                mIsResolving = savedInstanceState.GetBoolean (KEY_IS_RESOLVING);
                mShouldResolve = savedInstanceState.GetBoolean (KEY_SHOULD_RESOLVE);
            }

            // Google API client
            GoogleSignInOptions gso = new GoogleSignInOptions.Builder (GoogleSignInOptions.DefaultSignIn).RequestEmail ().Build ();
            mGoogleApiClient = new GoogleApiClient.Builder (this)
            .EnableAutoManage (this, this)
            .AddConnectionCallbacks (this)
            .AddOnConnectionFailedListener (this)
            .AddApi (Auth.GOOGLE_SIGN_IN_API, gso)
            .Build ();
            ViewModel = LoginVM.Init ();
        }

        protected override void OnStart ()
        {
            base.OnStart ();

            isAuthenticatingBinding = this.SetBinding (() => ViewModel.IsAuthenticating).WhenSourceChanges (SetViewState);
            modeBinding = this.SetBinding (() => ViewModel.CurrentLoginMode).WhenSourceChanges (SetViewState);
            resultBinding = this.SetBinding (() => ViewModel.AuthResult).WhenSourceChanges (() => {
                switch (ViewModel.AuthResult) {
                case AuthResult.None:
                case AuthResult.Authenticating:
                    SetViewState ();
                    break;

                case AuthResult.Success:
                    // TODO RX: Start the initial sync for the user
                    //ServiceContainer.Resolve<ISyncManager> ().Run ();
                    var intent = new Intent (this, typeof (MainDrawerActivity));
                    intent.AddFlags (ActivityFlags.ClearTop);
                    StartActivity (intent);
                    Finish ();
                    break;

                // Error cases
                default:
                    if (ViewModel.CurrentLoginMode == LoginVM.LoginMode.Login) {
                        if (ViewModel.AuthResult == AuthResult.InvalidCredentials) {
                            PasswordEditText.Text = string.Empty;
                        }
                        PasswordEditText.RequestFocus ();
                    } else {
                        EmailEditText.RequestFocus ();
                    }
                    ShowAuthError (EmailEditText.Text, ViewModel.AuthResult, ViewModel.CurrentLoginMode);
                    break;
                }
            });
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutBoolean (ExtraShowPassword, showPassword);
            outState.PutBoolean (KEY_IS_RESOLVING, mIsResolving);
            outState.PutBoolean (KEY_SHOULD_RESOLVE, mIsResolving);
        }

        #region View state utils
        private void SetViewState ()
        {
            // Views not loaded yet/anymore?
            if (LoginButton == null) {
                return;
            }

            if (ViewModel.CurrentLoginMode == LoginVM.LoginMode.Login) {
                LoginButton.SetText (ViewModel.IsAuthenticating ? Resource.String.LoginButtonProgressText : Resource.String.LoginButtonText);
                LegalTextView.Visibility = ViewStates.Gone;
                GoogleLoginButton.SetText (Resource.String.LoginGoogleButtonText);
                SwitchModeButton.SetText (Resource.String.SignupViewButtonText);
            } else {
                LoginButton.SetText (ViewModel.IsAuthenticating ? Resource.String.LoginButtonSignupProgressText : Resource.String.LoginSignupButtonText);
                LegalTextView.SetText (FormattedLegalText, TextView.BufferType.Spannable);
                LegalTextView.MovementMethod = Android.Text.Method.LinkMovementMethod.Instance;
                LegalTextView.Visibility = ViewStates.Visible;
                GoogleLoginButton.SetText (Resource.String.LoginSignupGoogleButtonText);
                SwitchModeButton.SetText (Resource.String.LoginViewButtonText);
            }

            EmailEditText.Enabled = !ViewModel.IsAuthenticating;
            PasswordEditText.Enabled = !ViewModel.IsAuthenticating;
            GoogleLoginButton.Enabled = !ViewModel.IsAuthenticating;

            SetLoginBtnState ();
            SetPasswordVisibility ();
        }

        private void SetLoginBtnState ()
        {
            if (ViewModel.CurrentLoginMode == LoginVM.LoginMode.Login) {
                return;
            }

            LoginButton.Enabled = !ViewModel.IsAuthenticating &&
                                  ViewModel.IsEmailValid (EmailEditText.Text) &&
                                  ViewModel.IsPassValid (PasswordEditText.Text);
        }

        private void SetPasswordVisibility ()
        {
            if (PasswordEditText.Text.Length == 0) {
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
                PasswordEditText.SetFont (Font.RobotoLight);
                PasswordEditText.SetSelection (selectionStart, selectionEnd);
            }
        }
        #endregion

        #region Btn events
        private void OnLoginButtonClick (object sender, EventArgs e)
        {
            // Small UI trick to permit OBM testers
            // interact with the staging API
            if (EmailEditText.Text == "staging") {
                var isStaging = !ServiceContainer.Resolve<ISettingsStore> ().IsStagingMode;
                ServiceContainer.Resolve<ISettingsStore> ().IsStagingMode = isStaging;
                var msg = !isStaging ? "You're in Normal Mode" : "You're in Staging Mode";
                new AlertDialog.Builder (this)
                .SetTitle ("Staging Mode")
                .SetMessage (msg + "\nRestart the app to continue.")
                .SetPositiveButton ("Ok", (EventHandler<DialogClickEventArgs>)null)
                .Show ();
                return;
            }

            ViewModel.TryLogin (EmailEditText.Text, PasswordEditText.Text);
        }

        private void OnGoogleLoginButtonClick (object sender, EventArgs e)
        {
            mShouldResolve = true;
            Intent signInIntent = Auth.GoogleSignInApi.GetSignInIntent (mGoogleApiClient);
            StartActivityForResult (signInIntent, RC_SIGN_IN);
        }

        private void OnModeToggleButtonClick (object sender, EventArgs e)
        {
            ViewModel.ChangeLoginMode ();
        }

        private void OnEmailEditTextTextChanged (object sender, TextChangedEventArgs e)
        {
            SetLoginBtnState ();
        }

        private void OnPasswordEditTextTextChanged (object sender, TextChangedEventArgs e)
        {
            SetPasswordVisibility ();
            SetLoginBtnState ();
        }

        private void OnPasswordToggleButtonClick (object sender, EventArgs e)
        {
            showPassword = !showPassword;
            SetPasswordVisibility ();
        }
        #endregion

        #region Google Login methods
        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            if (requestCode == RC_SIGN_IN) {
                if (resultCode != Result.Ok) {
                    mShouldResolve = false;
                    GoogleSignInResult result = Auth.GoogleSignInApi.GetSignInResultFromIntent (data);
                    if (result.IsSuccess) {
                        // Signed in successfully, show authenticated UI.
                        GoogleSignInAccount acct = result.SignInAccount;
                        ViewModel.TryLoginWithGoogle (acct.IdToken);
                    } else {
                        // Signed out, show unauthenticated UI.
                        // Report bug
                    }
                }
            }
        }

        public void OnConnected (Bundle connectionHint)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            log.Info (LogTag, "Login with Google. Success : " + connectionHint);
        }

        public void OnConnectionSuspended (int cause)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            log.Info (LogTag, new Exception (), "Failed to login with Google. onConnectionSuspended:" + cause);
        }

        public void OnConnectionFailed (ConnectionResult result)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            log.Info (LogTag, "Failed to login with Google due to network issues.onConnectionFailed:" + result);

            if (!mIsResolving && mShouldResolve) {
                if (result.HasResolution) {
                    try {
                        result.StartResolutionForResult (this, RC_SIGN_IN);
                        mIsResolving = true;
                    } catch (IntentSender.SendIntentException e) {
                        log.Error (LogTag, e, "Could not resolve ConnectionResult.");
                        mIsResolving = false;
                        mGoogleApiClient.Connect ();
                    }
                } else {
                    //ShowErrorDialog (result);
                }
            } else {
                //UpdateUI (false);
            }
        }

        private List<string> GoogleAccounts
        {
            get {
                var am = AccountManager.Get (this);
                return am.GetAccounts ()
                       .Where (a => a.Type == GoogleAuthUtil.GoogleAccountType)
                       .Select (a => a.Name)
                       .Distinct ()
                       .ToList ();
            }
        }
        #endregion

        private void ShowAuthError (string email, AuthResult res, LoginVM.LoginMode mode, bool googleAuth=false)
        {
            DialogFragment dia = null;

            switch (res) {
            case AuthResult.InvalidCredentials:
                if (mode == LoginVM.LoginMode.Login && !googleAuth) {
                    ShowMsgDialog (Resource.String.LoginInvalidCredentialsDialogTitle, Resource.String.LoginInvalidCredentialsDialogText);
                } else if (mode == LoginVM.LoginMode.Signup) {
                    ShowMsgDialog (Resource.String.LoginSignupFailedDialogTitle, Resource.String.LoginSignupFailedDialogText);
                } else if (mode == LoginVM.LoginMode.Login && googleAuth) {
                    ShowMsgDialog (Resource.String.LoginNoAccountDialogTitle, Resource.String.LoginNoAccountDialogText);
                }
                break;
            case AuthResult.NoDefaultWorkspace:
                dia = new NoWorkspaceDialogFragment (email);
                break;
            case AuthResult.NetworkError:
                ShowMsgDialog (Resource.String.LoginNetworkErrorDialogTitle, Resource.String.LoginNetworkErrorDialogText);
                break;
            default:
                ShowMsgDialog (Resource.String.LoginSystemErrorDialogTitle, Resource.String.LoginSystemErrorDialogText);
                break;
            }

            if (dia != null) {
                dia.Show (SupportFragmentManager, "auth_result_dialog");
            }
        }

        private void ShowMsgDialog (int title, int message)
        {
            var dialog = new AlertDialog.Builder (this)
            .SetTitle (title)
            .SetMessage (message)
            .SetPositiveButton (Resource.String.LoginInvalidCredentialsDialogOk, delegate {})
            .Create ();
            dialog.Show ();
        }

        public class NoWorkspaceDialogFragment : DialogFragment
        {
            private const string EmailKey = "com.toggl.timer.email";

            public NoWorkspaceDialogFragment ()
            {
            }

            public NoWorkspaceDialogFragment (string email)
            {
                var args = new Bundle();
                args.PutString (EmailKey, email);

                Arguments = args;
            }

            private string Email
            {
                get {
                    if (Arguments == null) {
                        return String.Empty;
                    }
                    return Arguments.GetString (EmailKey);
                }
            }

            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                return new AlertDialog.Builder (Activity)
                       .SetTitle (Resource.String.LoginNoWorkspaceDialogTitle)
                       .SetMessage (Resource.String.LoginNoWorkspaceDialogText)
                       .SetPositiveButton (Resource.String.LoginNoWorkspaceDialogOk, OnOkButtonClicked)
                       .SetNegativeButton (Resource.String.LoginNoWorkspaceDialogCancel, OnCancelButtonClicked)
                       .Create ();
            }

            private void OnOkButtonClicked (object sender, DialogClickEventArgs args)
            {
                var intent = new Intent (Intent.ActionSend);
                intent.SetType ("message/rfc822");
                intent.PutExtra (Intent.ExtraEmail, new[] { Resources.GetString (Resource.String.LoginNoWorkspaceDialogEmail) });
                intent.PutExtra (Intent.ExtraSubject, Resources.GetString (Resource.String.LoginNoWorkspaceDialogSubject));
                intent.PutExtra (Intent.ExtraText, string.Format (Resources.GetString (Resource.String.LoginNoWorkspaceDialogBody), Email));
                StartActivity (Intent.CreateChooser (intent, (string)null));
            }

            private void OnCancelButtonClicked (object sender, DialogClickEventArgs args)
            {
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


        private ISpannable FormattedLegalText
        {
            get {
                if (formattedLegalText == null) {
                    var template = Resources.GetText (Resource.String.LoginSignupLegalText);
                    var arg0 = Resources.GetText (Resource.String.LoginSignupLegalTermsText);
                    var arg1 = Resources.GetText (Resource.String.LoginSignupLegalPrivacyText);

                    var arg0idx = string.Format (template, "{0}", arg1).IndexOf ("{0}", StringComparison.Ordinal);
                    var arg1idx = string.Format (template, arg0, "{1}").IndexOf ("{1}", StringComparison.Ordinal);

                    var s = formattedLegalText = new SpannableString (string.Format (template, arg0, arg1));
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
    }
}
