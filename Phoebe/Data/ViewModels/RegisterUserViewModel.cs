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
    public class RegisterUserViewModel : ViewModelBase, IDisposable
    {
        public RegisterUserViewModel ()
        {
            IsRegistering = false;
            IsSuccesful = false;
        }

        public static RegisterUserViewModel Init ()
        {
            return new RegisterUserViewModel ();
        }

        public void Dispose ()
        {
        }

        #region Properties for ViewModel binding
        public bool IsRegistering { get; set; }

        public bool IsSuccesful { get; set; }
        #endregion

        public async Task<AuthResult> TrySignupPasswordAsync (string email, string password)
        {
            IsRegistering = true;
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.RegisterNoUserEmailAsync (email, password);
            } catch (InvalidOperationException ex) {
                return AuthResult.SystemError;
            }
            IsRegistering = false;

            if (authRes == AuthResult.Success) {
                IsSuccesful = true;
            }
            return authRes;
        }

        public async Task<AuthResult> TrySignupGoogleAsync (string token)
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            return await authManager.RegisterNoUserGoogleAsync (token);
        }
    }
}

