using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using Toggl.Phoebe.Logging;
using XPlatUtils;

namespace Toggl.Phoebe.Net
{
    [ImplementPropertyChanged]
    public class AuthManager : ObservableObject
    {
        public static readonly string PropertyIsAuthenticating = GetPropertyName (m => m.IsAuthenticating);
        public static readonly string PropertyIsAuthenticated = GetPropertyName (m => m.IsAuthenticated);
        public static readonly string PropertyUser = GetPropertyName (m => m.User);
        public static readonly string PropertyToken = GetPropertyName (m => m.Token);

        private readonly Subscription<DataChangeMessage> subscriptionDataChange;
        private static readonly string Tag = "AuthManager";

        private static string GetPropertyName<T> (Expression<Func<AuthManager, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        public AuthManager ()
        {
            var credStore = ServiceContainer.Resolve<ISettingsStore> ();
            try {
                if (credStore.UserId.HasValue) {
                    User = new UserData () {
                        Id = credStore.UserId.Value,
                    };
                    // Load full user data:
                    ReloadUser ();
                }
                Token = credStore.ApiToken;
                OfflineMode = credStore.OfflineMode;
                IsAuthenticated = !String.IsNullOrEmpty (Token) || OfflineMode;
            } catch (ArgumentException) {

                // When data is corrupt and cannot find user
                credStore.UserId = null;
                credStore.ApiToken = null;
            }

            // Listen for global data changes
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
        }

        private async void ReloadUser ()
        {
            if (User == null) {
                return;
            }
            var store = ServiceContainer.Resolve<IDataStore> ();
            var rows = await store.Table<UserData> ()
                       .Where (r => r.Id == User.Id).ToListAsync();
            User = rows.FirstOrDefault ();
        }

        private async Task<AuthResult> AuthenticateAsync (Func<Task<UserJson>> getUser, AuthChangeReason reason, AccountCredentials credentialsType)
        {
            if (IsAuthenticated) {
                throw new InvalidOperationException ("Cannot authenticate when old credentials still present.");
            }
            if (IsAuthenticating) {
                throw new InvalidOperationException ("Another authentication is still in progress.");
            }

            IsAuthenticating = true;

            try {
                UserJson userJson;
                try {
                    userJson = await getUser ();
                    if (userJson == null) {
                        ServiceContainer.Resolve<MessageBus> ().Send (
                            new AuthFailedMessage (this, AuthResult.InvalidCredentials));
                        return AuthResult.InvalidCredentials;
                    } else if (userJson.DefaultWorkspaceId == 0) {
                        ServiceContainer.Resolve<MessageBus> ().Send (
                            new AuthFailedMessage (this, AuthResult.NoDefaultWorkspace));
                        return AuthResult.NoDefaultWorkspace;
                    }
                } catch (Exception ex) {
                    var reqEx = ex as UnsuccessfulRequestException;
                    if (reqEx != null && (reqEx.IsForbidden || reqEx.IsValidationError)) {
                        ServiceContainer.Resolve<MessageBus> ().Send (
                            new AuthFailedMessage (this, AuthResult.InvalidCredentials));
                        return AuthResult.InvalidCredentials;
                    }

                    var log = ServiceContainer.Resolve<ILogger> ();
                    if (ex.IsNetworkFailure () || ex is TaskCanceledException) {
                        log.Info (Tag, ex, "Failed authenticate user.");
                    } else {
                        log.Warning (Tag, ex, "Failed to authenticate user.");
                    }

                    ServiceContainer.Resolve<MessageBus> ().Send (
                        new AuthFailedMessage (this, AuthResult.NetworkError, ex));
                    return AuthResult.NetworkError;
                }

                // Import the user into our database:
                UserData userData;
                try {
                    var dataStore = ServiceContainer.Resolve<IDataStore> ();
                    userData = await dataStore.ExecuteInTransactionAsync (ctx => userJson.Import (ctx));
                } catch (Exception ex) {
                    var log = ServiceContainer.Resolve<ILogger> ();
                    log.Error (Tag, ex, "Failed to import authenticated user.");

                    ServiceContainer.Resolve<MessageBus> ().Send (
                        new AuthFailedMessage (this, AuthResult.SystemError, ex));
                    return AuthResult.SystemError;
                }

                var credStore = ServiceContainer.Resolve<ISettingsStore> ();
                credStore.UserId = userData.Id;
                credStore.ApiToken = userJson.ApiToken;
                credStore.OfflineMode = userData.Name == "offlineUser";

                User = userData;
                Token = userJson.ApiToken;
                OfflineMode = userData.Name == "offlineUser";
                IsAuthenticated = true;

                ServiceContainer.Resolve<MessageBus> ().Send (
                    new AuthChangedMessage (this, reason));
            } finally {
                IsAuthenticating = false;
            }

            // Ping analytics service
            var tracker = ServiceContainer.Resolve<ITracker> ();
            switch (reason) {
            case AuthChangeReason.Login:
                tracker.SendAccountLoginEvent (credentialsType);
                break;
            case AuthChangeReason.Signup:
                tracker.SendAccountCreateEvent (credentialsType);
                break;
            }

            return AuthResult.Success;
        }

        public Task<AuthResult> AuthenticateAsync (string username, string password)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            var client = ServiceContainer.Resolve<ITogglClient> ();

            log.Info (Tag, "Authenticating with email ({0}).", username);
            return AuthenticateAsync (() => client.GetUser (username, password), AuthChangeReason.Login, AccountCredentials.Password);
        }

        public Task<AuthResult> AuthenticateWithGoogleAsync (string accessToken)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            var client = ServiceContainer.Resolve<ITogglClient> ();

            log.Info (Tag, "Authenticating with Google access token.");
            return AuthenticateAsync (() => client.GetUser (accessToken), AuthChangeReason.Login, AccountCredentials.Google);
        }

        public Task<AuthResult> SignupAsync (string email, string password)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            var client = ServiceContainer.Resolve<ITogglClient> ();

            log.Info (Tag, "Signing up with email ({0}).", email);
            return AuthenticateAsync (() => client.Create (new UserJson () {
                Email = email,
                Password = password,
                Timezone = Time.TimeZoneId,
            }), AuthChangeReason.Signup, AccountCredentials.Password);
        }

        public Task<AuthResult> SignupWithGoogleAsync (string accessToken)
        {
            var log = ServiceContainer.Resolve<ILogger> ();
            var client = ServiceContainer.Resolve<ITogglClient> ();

            log.Info (Tag, "Signing up with email Google access token.");
            return AuthenticateAsync (() => client.Create (new UserJson () {
                GoogleAccessToken = accessToken,
                Timezone = Time.TimeZoneId,
            }), AuthChangeReason.Signup, AccountCredentials.Google);
        }

        public Task<AuthResult> SetupNoUserAsync ()
        {
            OfflineMode = true;
            var client = ServiceContainer.Resolve<ITogglClient> ();
            var usr =  new UserJson () {
                Id = 100,
                Name = "offlineUser",
                StartOfWeek = DayOfWeek.Monday,
                Locale = "",
                Email = "nouser@toggl.com",
                Password = "no-password",
                Timezone = Time.TimeZoneId,
                DefaultWorkspaceId = 1000
            };
            return AuthenticateAsync (() => UserTask (usr), AuthChangeReason.NoUser, AccountCredentials.NoUser);
        }

        private async Task<UserJson> UserTask (UserJson user)
        {
            return user;
        }

        public void Forget ()
        {
            if (!IsAuthenticated) {
                throw new InvalidOperationException ("Cannot forget credentials which don't exist.");
            }

            var log = ServiceContainer.Resolve<ILogger> ();
            log.Info (Tag, "Forgetting current user.");

            var credStore = ServiceContainer.Resolve<ISettingsStore> ();
            credStore.UserId = null;
            credStore.ApiToken = null;

            IsAuthenticated = false;
            Token = null;
            User = null;

            ServiceContainer.Resolve<MessageBus> ().Send (
                new AuthChangedMessage (this, AuthChangeReason.Logout));

            // Ping analytics
            ServiceContainer.Resolve<ITracker> ().SendAccountLogoutEvent ();
        }

        private void OnDataChange (DataChangeMessage msg)
        {
            if (User.Matches (msg.Data)) {
                User = new UserData ((UserData)msg.Data);
            }
        }

        public bool IsAuthenticating { get; private set; }

        private bool offlineMode;
        public static readonly string PropertyOfflineMode = GetPropertyName ((m) => m.OfflineMode);

        public bool OfflineMode
        {
            get { return offlineMode; }
            private set {
                if (offlineMode == value) {
                    return;
                }

                ChangePropertyAndNotify (PropertyOfflineMode, delegate {
                    offlineMode = value;
                });
            }
        }

        public UserData User { get; private set; }

        public string Token { get; private set; }

        public Guid? GetUserId ()
        {
            if (User == null) {
                return null;
            }
            return User.Id;
        }
    }
}
