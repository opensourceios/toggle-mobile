using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Views;

namespace Toggl.Phoebe.Tests.Views
{
    [TestFixture]
    public class TimeEntryTagsViewTest : DataViewTest
    {
        const string DefaultTag = "mobile";
        TimeEntryData timeEntry;
        TagData tag1;
        TagData tag2;
        TagData tag3;

        public override async Task SetUp ()
        {
            await base.SetUp ();
            await CreateTestData ();
        }

        [Test]
        public async Task TestInitialState ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            Assert.AreEqual (2, view.Count);
            Assert.AreEqual (
                MakeTagsArray (DefaultTag, "Tag #2"),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestInvalidTimeEntry ()
        {
            var view = new TimeEntryTagsView (Guid.Empty);
            await WaitForLoaded (view);

            Assert.AreEqual (0, view.Count);
        }

        [Test]
        public async Task TestAddInvalidManyToMany ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            await DataStore.PutAsync (new TimeEntryTagData () {
                TimeEntryId = timeEntry.Id,
                TagId = Guid.Empty,
            });

            Assert.AreEqual (2, view.Count);
        }

        [Test]
        public async Task TestAddManyToMany ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            var updateTask = WaitForUpdates (view);

            await DataStore.PutAsync (new TimeEntryTagData () {
                TimeEntryId = timeEntry.Id,
                TagId = tag3.Id,
            });

            // Wait for the tag to be loaded and the view be updated:
            await updateTask;

            Assert.AreEqual (3, view.Count);
            Assert.AreEqual (
                MakeTagsArray (DefaultTag, "Tag #2", "Tag #3"),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestAddManyToManyForOther ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            var updateTask = WaitForUpdates (view);

            await DataStore.PutAsync (new TimeEntryTagData () {
                TimeEntryId = Guid.NewGuid (),
                TagId = tag3.Id,
            });

            // Wait to be sure the view has had a chance to ignore this relation:
            await Task.WhenAny (updateTask, Task.Delay (10));

            Assert.AreEqual (2, view.Count);
            Assert.AreEqual (
                MakeTagsArray (DefaultTag, "Tag #2"),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestReplaceManyToMany ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            var inter = await GetByRemoteId<TimeEntryTagData> (2);
            await DataStore.DeleteAsync (inter);
            await DataStore.PutAsync (new TimeEntryTagData () {
                TimeEntryId = timeEntry.Id,
                TagId = tag2.Id,
            });

            // We're not awaitng on the updated event as the view should update immediatelly as the TagData
            // should still be cached.

            Assert.AreEqual (2, view.Count);
            Assert.AreEqual (
                MakeTagsArray (DefaultTag, "Tag #2"),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestDeleteManyToMany ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            var inter = await GetByRemoteId<TimeEntryTagData> (1);
            await DataStore.DeleteAsync (inter);

            Assert.AreEqual (1, view.Count);
            Assert.AreEqual (
                MakeTagsArray ("Tag #2"),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestDeleteTag ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            await DataStore.DeleteAsync (tag2);

            Assert.AreEqual (1, view.Count);
            Assert.AreEqual (
                MakeTagsArray (DefaultTag),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestRenameTag ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            tag2.Name = "A tag";
            await DataStore.PutAsync (tag2);

            Assert.AreEqual (2, view.Count);
            Assert.AreEqual (
                MakeTagsArray ("A tag", DefaultTag),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestMarkDeletedTag ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            tag2.DeletedAt = Time.UtcNow;
            await DataStore.PutAsync (tag2);

            Assert.AreEqual (1, view.Count);
            Assert.AreEqual (
                MakeTagsArray (DefaultTag),
                view.Data.ToArray ()
            );
        }


        [Test]
        public async Task TestMarkDeletedManyToMany ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            var inter = await GetByRemoteId<TimeEntryTagData> (1);
            inter.DeletedAt = Time.UtcNow;
            await DataStore.PutAsync (inter);

            Assert.AreEqual (1, view.Count);
            Assert.AreEqual (
                MakeTagsArray ("Tag #2"),
                view.Data.ToArray ()
            );
        }

        [Test]
        public async Task TestNonDefault ()
        {
            var view = new TimeEntryTagsView (timeEntry.Id);
            await WaitForLoaded (view);

            Assert.IsTrue (view.HasNonDefault);

            var inter = await GetByRemoteId<TimeEntryTagData> (2);
            await DataStore.DeleteAsync (inter);

            Assert.IsFalse (view.HasNonDefault);

            inter = await GetByRemoteId<TimeEntryTagData> (1);
            await DataStore.DeleteAsync (inter);

            Assert.IsFalse (view.HasNonDefault);
        }

        private string[] MakeTagsArray (params string[] tags)
        {
            Array.Sort (tags, (a, b) => String.Compare (a, b, StringComparison.Ordinal));
            return tags;
        }

        private async Task CreateTestData ()
        {
            var workspace = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Unit Testing",
            });

            var user = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "Tester",
                DefaultWorkspaceId = workspace.Id,
            });

            tag1 = await DataStore.PutAsync (new TagData () {
                RemoteId = 1,
                Name = DefaultTag,
                WorkspaceId = workspace.Id,
            });

            tag2 = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "Tag #2",
                WorkspaceId = workspace.Id,
            });

            tag3 = await DataStore.PutAsync (new TagData () {
                RemoteId = 3,
                Name = "Tag #3",
                WorkspaceId = workspace.Id,
            });

            timeEntry = await DataStore.PutAsync (new TimeEntryData () {
                RemoteId = 1,
                Description = "Initial concept",
                State = TimeEntryState.Finished,
                StartTime = MakeTime (09, 12),
                StopTime = MakeTime (10, 1),
                WorkspaceId = workspace.Id,
                UserId = user.Id,
            });

            await DataStore.PutAsync (new TimeEntryTagData () {
                RemoteId = 1,
                TimeEntryId = timeEntry.Id,
                TagId = tag1.Id,
            });

            await DataStore.PutAsync (new TimeEntryTagData () {
                RemoteId = 2,
                TimeEntryId = timeEntry.Id,
                TagId = tag2.Id,
            });
        }
    }
}
