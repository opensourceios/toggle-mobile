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
    public class IntroViewModel : ViewModelBase, IDisposable
    {
        public IntroViewModel ()
        {
            IsAuthenticated = false;
        }

        public static IntroViewModel Init ()
        {
            return new IntroViewModel ();
        }

        #region Properties for ViewModel binding

        public bool IsAuthenticated { get; private set; }

        #endregion

        public async Task<AuthResult> SetUpNoUserAccountAsync ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            AuthResult authRes;
            try {
                authRes = await authManager.NoUserSetupAsync ();
            } catch (InvalidOperationException ex) {
                IsAuthenticated = false;
                return AuthResult.SystemError;
            }
            IsAuthenticated = true;
            return authRes;
        }

        public void Dispose ()
        {
        }
    }
}

