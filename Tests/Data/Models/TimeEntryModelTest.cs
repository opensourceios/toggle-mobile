using System;
using Moq;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using XPlatUtils;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Models
{
    [TestFixture]
    public class TimeEntryModelTest : ModelTest<TimeEntryModel>
    {
        private UserModel user;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            user = new UserModel () {
                Name = "User",
                Email = "user@test.net",
                DefaultWorkspace = new WorkspaceModel () {
                    Name = "Workspace",
                },
            };

            await user.DefaultWorkspace.SaveAsync ();
            await user.SaveAsync ();
            await SetUpFakeUser (user.Id);
        }

        [Test]
        public void TestChangeTimeNormalization ()
        {
            var entry = new TimeEntryModel () {
                User = user,
                StartTime = new DateTime (2013, 01, 01, 10, 0, 0, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 01, 01, 12, 0, 0, DateTimeKind.Utc),
                State = TimeEntryState.Finished,
            };

            entry.StartTime = new DateTime (2013, 01, 01, 11, 0, 0, 100, DateTimeKind.Local);
            Assert.AreEqual (DateTimeKind.Utc, entry.StartTime.Kind);
            Assert.AreEqual (0, entry.StartTime.Millisecond);

            entry.StopTime = new DateTime (2013, 01, 01, 13, 0, 0, 100, DateTimeKind.Local);
            Assert.AreEqual (DateTimeKind.Utc, entry.StopTime.Value.Kind);
            Assert.AreEqual (0, entry.StopTime.Value.Millisecond);
        }

        [Test]
        public async Task TestStart ()
        {
            var entry = new TimeEntryModel () {
                User = user,
            };

            await entry.StartAsync ();
            Assert.IsNull (entry.Task);
            Assert.IsNull (entry.Project);
            Assert.AreEqual (user.DefaultWorkspace.Id, entry.Workspace.Id);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
            Assert.AreNotEqual (new DateTime (), entry.StartTime);
            Assert.IsNull (entry.StopTime);
        }

        [Test]
        public async Task TestStore ()
        {
            var entry = new TimeEntryModel () {
                User = user,
                StartTime = new DateTime (2013, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 1, 2, 5, 4, 3, DateTimeKind.Utc),
            };

            await entry.StoreAsync ();
            Assert.IsNull (entry.Task);
            Assert.IsNull (entry.Project);
            Assert.AreEqual (user.DefaultWorkspace.Id, entry.Workspace.Id);
            Assert.AreEqual (TimeEntryState.Finished, entry.State);
            Assert.AreEqual (new DateTime (2013, 1, 2, 3, 4, 5, DateTimeKind.Utc), entry.StartTime);
            Assert.AreEqual (new DateTime (2013, 1, 2, 5, 4, 3, DateTimeKind.Utc), entry.StopTime);
        }

        [Test]
        public async Task TestContinueStartStop ()
        {
            var parent = new TimeEntryModel () {
                User = user,
                StartTime = new DateTime (2013, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 1, 2, 5, 4, 3, DateTimeKind.Utc),
            };
            await parent.StoreAsync ();

            var entry = await parent.ContinueAsync ();
            Assert.NotNull (entry);
            Assert.AreNotSame (parent, entry);
            Assert.AreNotEqual (parent.Id, entry.Id);
            Assert.IsNull (entry.StopTime);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
        }

        [Test]
        public async Task TestContinueDurationOtherDay ()
        {
            var parent = new TimeEntryModel () {
                User = user,
                StartTime = new DateTime (2013, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 1, 2, 5, 4, 3, DateTimeKind.Utc),
                DurationOnly = true,
            };
            await parent.StoreAsync ();

            var entry = await parent.ContinueAsync ();
            Assert.NotNull (entry);
            Assert.AreNotSame (parent, entry);
            Assert.AreNotEqual (parent.Id, entry.Id);
            Assert.IsNull (entry.StopTime);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
        }

        [Test]
        public async Task TestContinueDurationSameDay ()
        {
            var startTime = Time.Now.Date.AddHours (1);
            var stopTime = startTime + TimeSpan.FromHours (2);
            var parent = new TimeEntryModel () {
                User = user,
                StartTime = startTime,
                StopTime = stopTime,
                DurationOnly = true,
            };
            await parent.StoreAsync ();

            var entry = await parent.ContinueAsync ();
            Assert.AreSame (parent, entry);
            Assert.IsNull (entry.StopTime);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
        }

        [Test]
        public async Task TestGetDraft ()
        {
            var entry = await TimeEntryModel.GetDraftAsync ();
            Assert.IsNotNull (entry);
            Assert.AreNotEqual (Guid.Empty, entry.Id);
            Assert.AreEqual (user.DefaultWorkspace.Id, entry.Workspace.Id);
            Assert.AreEqual (TimeEntryState.New, entry.State);
        }

        [Test]
        public async Task TestDraftDefaultTag ()
        {
            ServiceContainer.Register<ISettingsStore> (Mock.Of<ISettingsStore> (
                        (store) => store.ApiToken == "test" &&
                        store.UserId == user.Id &&
                        store.UseDefaultTag == true));

            var entry = await TimeEntryModel.GetDraftAsync ();

            var rows = await DataStore.Table<TimeEntryTagData> ()
                       .Where (r => r.TimeEntryId == entry.Id).ToListAsync ();
            Assert.That (rows, Has.Count.EqualTo (1));
            var firstTagId = rows [0].TagId;

            // Change user default workspace, to make sure that the draft uses the correct workspace default tag
            user.DefaultWorkspace = new WorkspaceModel () {
                Name = "Other workspace",
            };

            await user.DefaultWorkspace.SaveAsync ();
            await user.SaveAsync ();

            // Check data again:
            await entry.DeleteAsync ();
            entry = await TimeEntryModel.GetDraftAsync ();

            rows = await DataStore.Table<TimeEntryTagData> ()
                   .Where (r => r.TimeEntryId == entry.Id).ToListAsync ();
            Assert.That (rows, Has.Count.EqualTo (1));
            Assert.AreNotEqual (firstTagId, rows [0].TagId);

            // Make sure that we don't add duplicate tags when starting:
            await entry.StartAsync ();
            rows = await DataStore.Table<TimeEntryTagData> ()
                   .Where (r => r.TimeEntryId == entry.Id).ToListAsync ();
            Assert.That (rows, Has.Count.EqualTo (1));
        }

        [Test]
        public async Task TestGetDraftNotLoadedUser ()
        {
            // Pretend that the data isn't loaded yet
            ServiceContainer.Resolve<AuthManager> ().User.DefaultWorkspaceId = Guid.Empty;

            var entry = await TimeEntryModel.GetDraftAsync ();
            Assert.IsNotNull (entry);
            Assert.AreNotEqual (Guid.Empty, entry.Id);
            Assert.AreEqual (user.DefaultWorkspace.Id, entry.Workspace.Id);
            Assert.AreEqual (TimeEntryState.New, entry.State);
        }

        [Test]
        public async Task TestGetDraftUpdateGetAgain ()
        {
            var entry = await TimeEntryModel.GetDraftAsync ();
            entry.Description = "Hello, world!";
            await entry.SaveAsync ();

            entry = await TimeEntryModel.GetDraftAsync ();
            Assert.IsNotNull (entry);
            Assert.AreNotEqual (Guid.Empty, entry.Id);
            Assert.AreEqual ("Hello, world!", entry.Description);
            Assert.AreEqual (user.DefaultWorkspace.Id, entry.Workspace.Id);
            Assert.AreEqual (TimeEntryState.New, entry.State);
        }

        [Test]
        public async Task TestCreateFinished ()
        {
            var entry = await TimeEntryModel.CreateFinishedAsync (TimeSpan.FromMinutes (95));
            Assert.IsNotNull (entry);
            Assert.AreNotEqual (Guid.Empty, entry.Id);
            Assert.AreEqual (user.DefaultWorkspace.Id, entry.Workspace.Id);
            Assert.AreEqual (TimeEntryState.Finished, entry.State);
        }

        [Test]
        public void TestIsBillableFlag ()
        {
            var entry = new TimeEntryModel () {
                User = user,
                IsBillable = false,
            };

            Assert.IsFalse (entry.IsBillable);

            entry.Project = new ProjectModel (new ProjectData () {
                Id = Guid.NewGuid (),
                IsBillable = true,
            });
            Assert.IsTrue (entry.IsBillable);

            entry.Project = null;
            Assert.IsTrue (entry.IsBillable);

            entry.Project = new ProjectModel (new ProjectData () {
                Id = Guid.NewGuid (),
                IsBillable = false,
            });
            Assert.IsFalse (entry.IsBillable);
        }

        [Test]
        public void TestNewStartChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.New,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc),
            });

            entry.StartTime = new DateTime (2013, 10, 1, 11, 12, 30, DateTimeKind.Utc);
            Assert.AreEqual (new DateTime (2013, 10, 1, 14, 12, 30, DateTimeKind.Utc), entry.StopTime);
        }

        [Test]
        public void TestNewStartChangeNoStop ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.New,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
            });

            entry.StartTime = Time.UtcNow.AddHours (-1);
            Assert.AreEqual (entry.StartTime.AddHours (1), entry.StopTime);
        }

        [Test]
        public void TestNewStartChange3DaysAgoNoStop ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.New,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
            });

            entry.StartTime = Time.UtcNow.AddDays (-3).AddHours (-1);
            Assert.AreEqual (entry.StartTime.AddHours (1), entry.StopTime);
        }

        [Test]
        public void TestNewStopChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.New,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc),
            });

            entry.StopTime = new DateTime (2013, 10, 1, 11, 12, 30, DateTimeKind.Utc);
            Assert.AreEqual (new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc), entry.StartTime);
        }

        [Test]
        public void TestNewDurationChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.New,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc),
            });

            entry.SetDuration (TimeSpan.FromHours (1));
            Assert.AreEqual (new DateTime (2013, 10, 1, 12, 12, 30, DateTimeKind.Utc), entry.StartTime);
            Assert.AreEqual (new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc), entry.StopTime);
        }

        [Test]
        public void TestRunningStartChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Running,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
            });
            var oldDuration = (long)entry.GetDuration ().TotalSeconds;
            Assert.AreNotEqual (0, oldDuration);

            entry.StartTime = new DateTime (2013, 10, 1, 12, 12, 30, DateTimeKind.Utc);
            var newDuration = (long)entry.GetDuration ().TotalSeconds;
            Assert.AreEqual ((long)TimeSpan.FromHours (2).TotalSeconds, oldDuration - newDuration);
            Assert.AreEqual (null, entry.StopTime);
        }

        [Test]
        public void TestRunningDurationChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Running,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
            });
            var oldDuration = entry.GetDuration ();
            Assert.AreNotEqual (0, oldDuration);

            entry.SetDuration (oldDuration + TimeSpan.FromHours (1));
            Assert.AreEqual (new DateTime (2013, 10, 1, 9, 12, 30, DateTimeKind.Utc), entry.StartTime.Truncate (TimeSpan.TicksPerSecond));
            Assert.AreEqual (null, entry.StopTime);
        }

        [Test]
        public void TestRunningNullForDescription ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Running,
                UserId = user.Id,
                Description = null,
            });

            Assert.AreEqual (String.Empty, entry.Description);
        }

        [Test]
        public void TestRunningChangeDescriptionToNull ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Running,
                UserId = user.Id,
                Description = String.Empty,
            });

            entry.Description = null;
            Assert.AreEqual (String.Empty, entry.Description);
        }

        [Test]
        public void TestNullVsEmptyComparison ()
        {
            var model = new TimeEntryModel (new TimeEntryData () {
                Description = String.Empty,
            });

            model.PropertyChanged += delegate {
                Assert.Fail ("Property shouldn't change");
            };

            model.Data = new TimeEntryData () {
                Description = null,
            };
        }

        [Test]
        public void TestStoppedStartChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Finished,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc),
            });

            // Changing start time should keep the duration constant and adjust stop time
            entry.StartTime = new DateTime (2013, 10, 1, 11, 12, 30, DateTimeKind.Utc);
            Assert.AreEqual (new DateTime (2013, 10, 1, 14, 12, 30, DateTimeKind.Utc), entry.StopTime);
        }

        [Test]
        public void TestStoppedStopChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Finished,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc),
            });

            entry.StopTime = new DateTime (2013, 10, 1, 11, 12, 30, DateTimeKind.Utc);
            Assert.AreEqual (new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc), entry.StartTime);
        }

        [Test]
        public void TestStoppedDurationChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Finished,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 13, 12, 30, DateTimeKind.Utc),
            });

            entry.SetDuration (TimeSpan.FromHours (1));
            Assert.AreEqual (new DateTime (2013, 10, 1, 10, 12, 30, DateTimeKind.Utc), entry.StartTime);
            Assert.AreEqual (new DateTime (2013, 10, 1, 11, 12, 30, DateTimeKind.Utc), entry.StopTime);
        }

        [Test]
        public void TestDuronlyNewDurationChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.New,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1).ToUtc (),
                DurationOnly = true,
            });
            Assert.IsNull (entry.StopTime);

            entry.SetDuration (TimeSpan.FromHours (1));
            Assert.IsNotNull (entry.StopTime);
            Assert.AreNotEqual (new DateTime (2013, 10, 1).ToUtc (), entry.StartTime);
        }

        [Test]
        public void TestDuronlyRunningDurationChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Running,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1).ToUtc (),
                DurationOnly = true,
            });
            Assert.IsNull (entry.StopTime);

            entry.SetDuration (TimeSpan.FromHours (1));
            Assert.IsNull (entry.StopTime);
            Assert.AreNotEqual (new DateTime (2013, 10, 1).ToUtc (), entry.StartTime);
        }

        [Test]
        public void TestDuronlyStoppedDurationChange ()
        {
            var entry = new TimeEntryModel (new TimeEntryData () {
                State = TimeEntryState.Finished,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspace.Id,
                StartTime = new DateTime (2013, 10, 1, 10, 12, 0, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 10, 1, 12, 12, 0, DateTimeKind.Utc),
                DurationOnly = true,
            });
            Assert.AreEqual (TimeSpan.FromHours (2), entry.GetDuration ());

            entry.SetDuration (TimeSpan.FromHours (1));
            Assert.AreEqual (new DateTime (2013, 10, 1, 10, 12, 0, DateTimeKind.Utc), entry.StartTime);
            Assert.AreEqual (new DateTime (2013, 10, 1, 11, 12, 0, DateTimeKind.Utc), entry.StopTime);
        }

        [Test]
        public async Task TestStaticContinueStartStop ()
        {
            var parent = new TimeEntryModel () {
                User = user,
                StartTime = new DateTime (2013, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 1, 2, 5, 4, 3, DateTimeKind.Utc),
            };
            await parent.StoreAsync ();

            var entry = await TimeEntryModel.ContinueTimeEntryDataAsync (parent.Data);
            Assert.NotNull (entry);
            Assert.AreNotSame (parent.Data, entry);
            Assert.AreNotEqual (parent.Data.Id, entry.Id);
            Assert.IsNull (entry.StopTime);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
        }

        [Test]
        public async Task TestStaticContinueDurationOtherDay ()
        {
            var parent = new TimeEntryModel {
                User = user,
                StartTime = new DateTime (2013, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                StopTime = new DateTime (2013, 1, 2, 5, 4, 3, DateTimeKind.Utc),
                DurationOnly = true,
            };
            await parent.StoreAsync ();

            var entry = await TimeEntryModel.ContinueTimeEntryDataAsync (parent.Data);
            Assert.NotNull (entry);
            Assert.AreNotSame (parent.Data, entry);
            Assert.AreNotEqual (parent.Data.Id, entry.Id);
            Assert.IsNull (entry.StopTime);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
        }

        [Test]
        public async Task TestStaticContinueDurationSameDay ()
        {
            var startTime = Time.Now.Date.AddHours (1);
            var stopTime = startTime + TimeSpan.FromHours (2);
            var parent = new TimeEntryModel {
                User = user,
                StartTime = startTime,
                StopTime = stopTime,
                DurationOnly = true,
            };
            await parent.StoreAsync ();

            var entry = await TimeEntryModel.ContinueTimeEntryDataAsync (parent.Data);
            Assert.NotNull (entry);
            Assert.AreNotSame (parent.Data, entry);
            Assert.AreEqual (parent.Data.Id, entry.Id);
            Assert.IsNull (entry.StopTime);
            Assert.AreEqual (TimeEntryState.Running, entry.State);
        }

        [Test]
        public async Task TestStaticSaving ()
        {
            var parent = new TimeEntryData {
                UserId = user.Id
            };

            var entry = await TimeEntryModel.SaveTimeEntryDataAsync (parent);
            Assert.NotNull (entry);
            Assert.AreNotSame (parent, entry);
            Assert.AreNotEqual (parent.Id, entry.Id);
            Assert.AreNotEqual (Guid.Empty, entry.Id, "Id should've been updated after save.");
        }

        [Test]
        public async Task TestStaticStop ()
        {
            var parent = new TimeEntryData {
                UserId = user.Id
            };
            var model = new TimeEntryModel (parent);
            await model.StartAsync ();

            // Check if running.
            Assert.AreEqual (TimeEntryState.Running, model.State);

            var entry = await TimeEntryModel.StopAsync (model.Data);
            Assert.NotNull (entry);
            Assert.AreEqual (model.Data.Id, entry.Id);
            Assert.AreEqual (TimeEntryState.Finished, entry.State);
        }
    }
}
