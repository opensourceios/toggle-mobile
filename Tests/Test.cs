using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using SQLite.Net.Platform.Generic;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Tests
{
    public abstract class Test
    {
        private string databasePath;

        [SetUp]
        public virtual async Task SetUp ()
        {
            await Task.Delay (0);

            // Create MessageBus egerly to avoid it being created in the background thread with invalid synchronization context.
            ServiceContainer.Register<MessageBus> (new MessageBus ());
            ServiceContainer.Register<ITimeProvider> (() => new DefaultTimeProvider ());
            ServiceContainer.Register<TimeCorrectionManager> ();
            ServiceContainer.Register<IDataStore> (delegate {
                databasePath = Path.GetTempFileName ();
                return new SqliteDataStore (databasePath, new SQLitePlatformGeneric ());
            });
            ServiceContainer.Register<LogStore> ((LogStore)null);
            ServiceContainer.Register<ILoggerClient> ((ILoggerClient)null);
            ServiceContainer.Register<ILogger> (() => new VoidLogger());
        }

        [TearDown]
        public virtual async Task TearDown ()
        {
            // Use an empty transaction to ensure that the SQLiteDataStore has completed all scheduled jobs:
            await DataStore.ExecuteInTransactionAsync (ctx => {});

            ServiceContainer.Clear ();

            if (databasePath != null) {
                File.Delete (databasePath);
                databasePath = null;
            }
        }

        protected async Task SetUpFakeUser (Guid userId)
        {
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ApiToken == "test" &&
                        store.UserId == userId));
            var authManager = new AuthManager ();
            ServiceContainer.Register<AuthManager> (authManager);

            // Wait for the auth manager to load user data:
            var tcs = new TaskCompletionSource<object> ();
            Action checkUser = delegate {
                if (authManager.User != null && authManager.User.DefaultWorkspaceId != Guid.Empty) {
                    tcs.TrySetResult (null);
                }
            };
            authManager.PropertyChanged += (sender, e) => {
                if (e.PropertyName == AuthManager.PropertyUser) {
                    checkUser ();
                }
            };

            checkUser ();
            await tcs.Task;

            MessageBus.Send (new AuthChangedMessage (authManager, AuthChangeReason.Login));
        }

        protected MessageBus MessageBus
        {
            get { return ServiceContainer.Resolve<MessageBus> (); }
        }

        protected IDataStore DataStore
        {
            get { return ServiceContainer.Resolve<IDataStore> (); }
        }
    }
}
