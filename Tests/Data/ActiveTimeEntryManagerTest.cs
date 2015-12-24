using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Data
{
    [TestFixture]
    public class ActiveTimeEntryManagerTest : Test
    {
        private UserData user;
        private WorkspaceData workspace;
        private bool syncManagerRunning;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            workspace = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Test",
            });

            user = await DataStore.PutAsync (new UserData () {
                Name = "John Doe",
                TrackingMode = TrackingMode.StartNew,
                DefaultWorkspaceId = workspace.Id,
            });

            await SetUpFakeUser (user.Id);

            ServiceContainer.Register<ISyncManager> (Mock.Of<ISyncManager> (
                        (mgr) => mgr.IsRunning == syncManagerRunning));
            ServiceContainer.Register<ActiveTimeEntryManager> (new ActiveTimeEntryManager ());
        }

        private ActiveTimeEntryManager ActiveManager
        {
            get { return ServiceContainer.Resolve<ActiveTimeEntryManager> (); }
        }

        private void StartSync ()
        {
            syncManagerRunning = true;
            MessageBus.Send (new SyncStartedMessage (
                                 ServiceContainer.Resolve<ISyncManager> (),
                                 SyncMode.Full));
        }

        private void StopSync ()
        {
            syncManagerRunning = false;
            MessageBus.Send (new SyncFinishedMessage (
                                 ServiceContainer.Resolve<ISyncManager> (),
                                 SyncMode.Full, false, null));
        }

        private async Task<TimeEntryData> GetEntry (Guid id)
        {
            return (await DataStore.Table<TimeEntryData> ().Where (r => r.Id == id).ToListAsync ()).Single ();
        }

        private async Task WhenDataStoreIdle ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();

            TaskCompletionSource<object> tcs = null;
            Subscription<DataStoreIdleMessage> idleSubscription = null;

            idleSubscription = bus.Subscribe<DataStoreIdleMessage> (delegate {
                if (tcs != null) {
                    tcs.TrySetResult (null);
                }
            });

            while (true) {
                tcs = new TaskCompletionSource<object> ();
                var idleTask = Task.Delay (TimeSpan.FromMilliseconds (10));

                var completedTask = await Task.WhenAny (tcs.Task, idleTask);
                if (completedTask == idleTask) {
                    // Datastore has been idle for 10 milliseconds

                    if (idleSubscription != null) {
                        bus.Unsubscribe (idleSubscription);
                        idleSubscription = null;
                    }

                    return;
                }
            }
        }

        [Test]
        public async Task TestSingleRunningTimer ()
        {
            var startTime = Time.UtcNow - TimeSpan.FromHours (1);
            var te1 = await DataStore.PutAsync (new TimeEntryData () {
                Description = "Morning coffee",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });
            Assert.AreEqual (te1.Id, ActiveManager.Running.Id);

            startTime = Time.UtcNow - TimeSpan.FromMinutes (10);
            var te2 = await DataStore.PutAsync (new TimeEntryData () {
                Description = "Morning meeting",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });
            Assert.AreEqual (te2.Id, ActiveManager.Running.Id);

            StopSync ();

            // Check against latest data:
            await WhenDataStoreIdle ();
            te1 = await GetEntry (te1.Id);
            Assert.AreEqual (TimeEntryState.Finished, te1.State);
        }

        [Test]
        public async Task TestSyncMultipleRunningTimers ()
        {
            StartSync ();

            var startTime = Time.UtcNow - TimeSpan.FromHours (1);
            var te1 = await DataStore.PutAsync (new TimeEntryData () {
                Description = "Morning coffee",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });
            Assert.AreEqual (te1.Id, ActiveManager.Running.Id);

            startTime = Time.UtcNow - TimeSpan.FromMinutes (10);
            var te2 = await DataStore.PutAsync (new TimeEntryData () {
                Description = "Morning meeting",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            // Check against latest data:
            te1 = await GetEntry (te1.Id);
            te2 = await GetEntry (te2.Id);
            Assert.AreEqual (TimeEntryState.Running, te1.State);
            Assert.AreEqual (TimeEntryState.Running, te2.State);
            Assert.AreEqual (te2.Id, ActiveManager.Running.Id);

            StopSync ();

            // Check against latest data:
            await WhenDataStoreIdle ();
            te1 = await GetEntry (te1.Id);
            te2 = await GetEntry (te2.Id);
            Assert.AreEqual (TimeEntryState.Finished, te1.State);
            Assert.AreEqual (TimeEntryState.Running, te2.State);
            Assert.AreEqual (te2.Id, ActiveManager.Running.Id);
        }

        [Test]
        public async Task TestUserChange ()
        {
            var startTime = Time.UtcNow - TimeSpan.FromHours (1);
            var te1 = await DataStore.PutAsync (new TimeEntryData () {
                Description = "Morning coffee",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await WhenDataStoreIdle ();
            Assert.AreEqual (te1.Id, ActiveManager.Running.Id);
            Assert.AreEqual (user.Id, ActiveManager.Draft.UserId);

            // Fake user change:
            var newUser = await DataStore.PutAsync (new UserData () {
                Name = "Jane Doe",
                TrackingMode = TrackingMode.StartNew,
                DefaultWorkspaceId = workspace.Id,
            });
            await SetUpFakeUser (newUser.Id);

            // Check that the active time entry manager reacted
            await WhenDataStoreIdle ();
            Assert.IsNull (ActiveManager.Running);
            Assert.AreEqual (newUser.Id, ActiveManager.Draft.UserId);
        }

        [Test]
        public async Task TestInitialDraft ()
        {
            await WhenDataStoreIdle ();
            Assert.AreEqual (user.Id, ActiveManager.Draft.UserId);
        }

        [Test]
        public async Task TestDraftStart ()
        {
            await WhenDataStoreIdle ();

            // Start draft
            var startTime = Time.UtcNow - TimeSpan.FromHours (1);
            var data = await DataStore.PutAsync (new TimeEntryData (ActiveManager.Draft) {
                Description = "Morning coffee",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
            });

            await WhenDataStoreIdle ();
            Assert.AreEqual (data.Id, ActiveManager.Running.Id);
            Assert.IsNotNull (ActiveManager.Draft);
            Assert.AreNotEqual (data.Id, ActiveManager.Draft.Id);
            Assert.AreEqual (user.Id, ActiveManager.Draft.UserId);
        }

        [Test]
        public async Task TestStopRunning ()
        {
            var startTime = Time.UtcNow - TimeSpan.FromHours (1);
            var data = await DataStore.PutAsync (new TimeEntryData () {
                Description = "Morning coffee",
                State = TimeEntryState.Running,
                StartTime = startTime,
                ModifiedAt = startTime,
                IsDirty = true,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });
            Assert.AreEqual (data.Id, ActiveManager.Running.Id);

            data = await DataStore.PutAsync (new TimeEntryData (data) {
                State = TimeEntryState.Finished,
                StopTime = Time.UtcNow,
                ModifiedAt = Time.UtcNow,
            });

            await WhenDataStoreIdle ();
            Assert.IsNull (ActiveManager.Running);
        }
    }
}
