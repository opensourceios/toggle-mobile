using System;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using GalaSoft.MvvmLight;
using PropertyChanged;
using Toggl.Phoebe._Data;
using Toggl.Phoebe._Helpers;
using Toggl.Phoebe._Reactive;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe._ViewModels
{
    [ImplementPropertyChanged]
    public class LoginVM : ViewModelBase, IDisposable
    {
        private const string LogTag = "LoginViewModel";
        private const string ValidateEmailRegexp = "^[^<>\\\\#$@\\s]+@[^<>\\\\#$@\\s]*[^<>\\\\#$\\.\\s@]{1}?\\.{1}?[^<>\\\\#$\\.@\\s]{1}?[^<>\\\\#$@\\s]+$";

        IDisposable subscription;

        public enum LoginMode {
            Login,
            Signup
        }

        public LoginVM ()
        {
            // Set initia state.
            AuthResult = AuthResult.None;
            IsAuthenticated = false;
            IsAuthenticating = false;
            CurrentLoginMode = LoginMode.Login;

            subscription = StoreManager.Singleton
                           .Observe (x => x.TimerState.AuthResult)
                           .DistinctUntilChanged ()
            .SubscribeSimple (state => {
                ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                    AuthResult = state;
                    IsAuthenticating = state == AuthResult.Authenticating;
                });
            });
        }

        public static LoginVM Init ()
        {
            return new LoginVM ();
        }

        public void Dispose ()
        {
            subscription.Dispose ();
            subscription = null;
        }

        #region Properties for ViewModel binding

        public bool IsAuthenticating { get; private set; }

        public bool IsAuthenticated { get; private set; }

        public LoginMode CurrentLoginMode { get; private set; }

        public AuthResult AuthResult { get; private set; }

        #endregion


        #region public ViewModel methods

        public void ChangeLoginMode ()
        {
            CurrentLoginMode = (CurrentLoginMode == LoginMode.Login) ? LoginMode.Signup : LoginMode.Login;
            var screenStr = (CurrentLoginMode == LoginMode.Login) ? "Login" : "Signup";
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = screenStr;
        }

        public void TryLogin (string email, string password)
        {
            if (CurrentLoginMode == LoginMode.Login) {
                RxChain.Send (new DataMsg.Request (new ServerRequest.Authenticate (email, password)));
            } else {
                RxChain.Send (new DataMsg.Request (new ServerRequest.SignUp (email, password)));
            }
        }

        public void TryLoginWithGoogle (string token)
        {
            if (CurrentLoginMode == LoginMode.Login) {
                RxChain.Send (new DataMsg.Request (new ServerRequest.AuthenticateWithGoogle (token)));
            } else {
                RxChain.Send (new DataMsg.Request (new ServerRequest.SignUpWithGoogle (token)));
            }

        }

        public bool IsEmailValid (string email)
        {
            return Regex.IsMatch (email ?? "", ValidateEmailRegexp);
        }

        public bool IsPassValid (string pass)
        {
            return (pass ?? "").Length >= 6;
        }

        #endregion
    }
}

