using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Views
{
    [TestFixture]
    public class RecentTimeEntriesViewTest : DataViewTest
    {
        private WorkspaceData workspace;
        private UserData user;

        private AuthManager AuthManager
        {
            get { return ServiceContainer.Resolve<AuthManager> (); }
        }

        public override async Task SetUp ()
        {
            await base.SetUp ();

            await CreateTestData ();

            ServiceContainer.Register<ISyncManager> (Mock.Of<ISyncManager> (
                        (mgr) => mgr.IsRunning == false));
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ApiToken == "test" &&
                        store.UserId == user.Id));
            ServiceContainer.Register<AuthManager> (new AuthManager ());
        }

        [Test]
        public async Task TestInitialGrouping ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestInitialReload ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            view.Reload ();
            await WaitForLoaded (view);

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestChangeGroupBottom ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (1, m => m.Description += " and some");

            Assert.AreEqual (5, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2, 1 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestChangeGroupTop ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (3, m => m.Description += " and some");

            Assert.AreEqual (5, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2, 1 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestChangeGroupBottomToTop ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (1, m => {
                m.StopTime += TimeSpan.FromDays (1);
                m.StartTime += TimeSpan.FromDays (1);
            });

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 1, 5, 4, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestChangeGroupTopToBottom ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (3, m => {
                m.StopTime -= TimeSpan.FromDays (1);
                m.StartTime -= TimeSpan.FromDays (1);
            });

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 2, 1 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestMarkDeletedGroup ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (2, m => {
                m.DeletedAt = Time.UtcNow;
            });

            Assert.AreEqual (3, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestMarkDeletedGroupTop ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (1, m => {
                m.DeletedAt = Time.UtcNow;
            });

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestMarkDeletedGroupBottom ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await ChangeData<TimeEntryData> (3, m => {
                m.DeletedAt = Time.UtcNow;
            });

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 2, 1 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestDeleteGroup ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            var model = await GetByRemoteId<TimeEntryData> (2);
            await DataStore.DeleteAsync (model);

            Assert.AreEqual (3, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestDeleteGroupTop ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            var model = await GetByRemoteId<TimeEntryData> (1);
            await DataStore.DeleteAsync (model);

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestDeleteGroupBottom ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            var model = await GetByRemoteId<TimeEntryData> (3);
            await DataStore.DeleteAsync (model);

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 2, 1 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestDeletedEntry ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 6,
                State = TimeEntryState.Finished,
                StartTime = MakeTime (16, 0),
                StopTime = MakeTime (16, 36),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
                DeletedAt = MakeTime (16, 39),
            });

            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        public async Task TestPastEntryHidden ()
        {
            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 6,
                State = TimeEntryState.Finished,
                StartTime = MakeTime (16, 0).AddDays (-10),
                StopTime = MakeTime (16, 36).AddDays (-10),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);
            Assert.AreEqual (4, view.Count);
            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );
            view.Dispose ();
        }

        [Test]
        [Description ("Make sure that the view updates it's internal data objects when no reordering happens.")]
        public async Task TestDataUpdate ()
        {
            var view = new RecentTimeEntriesView ();
            await WaitForLoaded (view);

            Assert.AreEqual (
                new long[] { 5, 4, 3, 2 },
                view.Data.Select ((entry) => entry.RemoteId.Value).ToArray ()
            );

            // Update data
            var workspaceId = Guid.NewGuid ();
            var data = await GetByRemoteId<TimeEntryData> (5);
            data.WorkspaceId = workspaceId;
            await DataStore.PutAsync (data);

            Assert.AreEqual (workspaceId, view.Data.ElementAt (0).WorkspaceId,
                             "View failed to update internal data with latest information.");
        }

        private async Task CreateTestData ()
        {
            workspace = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Unit Testing",
            });

            user = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "Tester",
                DefaultWorkspaceId = workspace.Id,
            });

            var project = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                Name = "Ad design",
                WorkspaceId = workspace.Id,
            });

            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 1,
                Description = "Initial concept",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (09, 12),
                StopTime = MakeTime (10, 1),
                ProjectId = project.Id,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 2,
                Description = "Breakfast",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (10, 5),
                StopTime = MakeTime (10, 30),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 3,
                Description = "Initial concept",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (10, 35),
                StopTime = MakeTime (12, 21),
                ProjectId = project.Id,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 4,
                State = TimeEntryState.Finished,
                StartTime = MakeTime (12, 25),
                StopTime = MakeTime (13, 57),
                ProjectId = project.Id,
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 5,
                State = TimeEntryState.Finished,
                StartTime = MakeTime (14, 0),
                StopTime = MakeTime (14, 36),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });
        }
    }
}
