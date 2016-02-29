using System;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using PropertyChanged;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class LoginViewModel : ViewModelBase, IDisposable
    {

        private const string LogTag = "LoginViewModel";

        public LoginViewModel ()
        {
            IsAuthenticated = false;

            IsAuthenticating = false;
        }
        public static LoginViewModel Init ()
        {
            return new LoginViewModel ();
        }

        #region Properties for ViewModel binding

        public bool IsAuthenticating { get; private set; }

        public bool IsAuthenticated { get; private set; }

        #endregion


        #region public ViewModel methods

        public async Task<AuthResult> TryLoginPasswordAsync (string email, string password)
        {
            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.AuthenticateAsync (email, password);
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to authenticate user with password.");
                return AuthResult.SystemError;
            } finally {
                IsAuthenticating = false;
            }

            if (authRes == AuthResult.Success) {
                IsAuthenticated = true;
            }
            return authRes;
        }

        public async Task<AuthResult> TrySignupPasswordAsync (string email, string password)
        {
            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.SignupAsync (email, password);
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to signup user with password.");
                return AuthResult.SystemError;
            } finally {
                IsAuthenticating = false;
            }
            if (authRes == AuthResult.Success) {
                IsAuthenticated = true;
            }
            return authRes;
        }

        public async Task<AuthResult> TrySignupWithGoogleAsync (string token)
        {

            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.SignupWithGoogleAsync (token);
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to signup user with Google.");
                return AuthResult.SystemError;
            } finally {
                IsAuthenticating = false;
            }

            if (authRes == AuthResult.Success) {
                IsAuthenticated = true;
            }
            return authRes;
        }

        public async Task<AuthResult> TryLoginWithGoogleAsync (string token)
        {
            IsAuthenticating = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.AuthenticateWithGoogleAsync (token);
            } catch (InvalidOperationException ex) {
                var log = ServiceContainer.Resolve<ILogger> ();
                log.Info (LogTag, ex, "Failed to login user with Google.");
                return AuthResult.SystemError;
            } finally {
                IsAuthenticating = false;
            }

            if (authRes == AuthResult.Success) {
                IsAuthenticated = true;
            }
            return authRes;
        }

        #endregion

        public void Dispose ()
        {
        }
    }
}
