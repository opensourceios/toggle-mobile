using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class WorkspaceUserJsonConverterTest : Test
    {
        private WorkspaceUserJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new WorkspaceUserJsonConverter ();
        }

        [Test]
        public async Task ExportExisting ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 3,
                Name = "Hosting",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 5,
                Name = "John",
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 4,
                WorkspaceId = workspaceData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, workspaceUserData));
            Assert.AreEqual (4, json.Id);
            Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
            Assert.AreEqual (3, json.WorkspaceId);
            Assert.AreEqual (5, json.UserId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportInvalidWorkspaceAndUser ()
        {
            WorkspaceUserData workspaceUserData = null;

            workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 4,
                WorkspaceId = Guid.NewGuid (),
                UserId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, workspaceUserData));
            }, Throws.Exception.TypeOf<InvalidOperationException> ());
        }

        [Test]
        public async Task ExportNew ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                WorkspaceId = workspaceData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, workspaceUserData));
            Assert.IsNull (json.Id);
            Assert.AreEqual (1, json.WorkspaceId);
            Assert.AreEqual (2, json.UserId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ImportNew ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreNotEqual (Guid.Empty, workspaceUserData.Id);
            Assert.AreEqual (2, workspaceUserData.RemoteId);
            Assert.AreEqual (new DateTime (2014, 1, 3), workspaceUserData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, workspaceUserData.WorkspaceId);
            Assert.AreEqual (userData.Id, workspaceUserData.UserId);
            Assert.IsFalse (workspaceUserData.IsDirty);
            Assert.IsFalse (workspaceUserData.RemoteRejected);
            Assert.IsNull (workspaceUserData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreNotEqual (Guid.Empty, workspaceUserData.Id);
            Assert.AreEqual (2, workspaceUserData.RemoteId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), workspaceUserData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, workspaceUserData.WorkspaceId);
            Assert.AreEqual (userData.Id, workspaceUserData.UserId);
            Assert.IsFalse (workspaceUserData.IsDirty);
            Assert.IsFalse (workspaceUserData.RemoteRejected);
            Assert.IsNull (workspaceUserData.DeletedAt);

            // Warn the user that the test result might be invalid
            if (TimeZone.CurrentTimeZone.GetUtcOffset (DateTime.Now).TotalMinutes >= 0) {
                Assert.Inconclusive ("The test machine timezone should be set to GTM-1 or less to test datetime comparison.");
            }
        }

        [Test]
        [Description ("Overwrite local non-dirty data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteNonDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreEqual (workspaceData.Id, workspaceUserData.WorkspaceId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), workspaceUserData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite dirty local data if imported data has a modification time greater than local.")]
        public async Task ImportUpdatedOverwriteDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreEqual (workspaceData.Id, workspaceUserData.WorkspaceId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), workspaceUserData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite local dirty-but-rejected data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteRejectedLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreEqual (workspaceData.Id, workspaceUserData.WorkspaceId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), workspaceUserData.ModifiedAt);
        }

        [Test]
        [Description ("Keep local dirty data when imported data has same or older modification time.")]
        public async Task ImportUpdatedKeepDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreEqual (Guid.Empty, workspaceUserData.WorkspaceId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), workspaceUserData.ModifiedAt);
        }

        [Test]
        public async Task ImportMissingWorkspaceAndUser ()
        {
            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                WorkspaceId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var workspaceUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.AreNotEqual (Guid.Empty, workspaceUserData.WorkspaceId);
            Assert.AreNotEqual (Guid.Empty, workspaceUserData.UserId);

            var projectRows = await DataStore.Table<WorkspaceData> ().Where (m => m.Id == workspaceUserData.WorkspaceId).ToListAsync ();
            var workspaceData = projectRows.FirstOrDefault ();
            Assert.IsNotNull (workspaceData);
            Assert.IsNotNull (workspaceData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, workspaceData.ModifiedAt);

            var userRows = await DataStore.Table<UserData> ().Where (m => m.Id == workspaceUserData.UserId).ToListAsync ();
            var userData = userRows.FirstOrDefault ();
            Assert.IsNotNull (userData);
            Assert.IsNotNull (userData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, userData.ModifiedAt);
        }

        [Test]
        public async Task ImportDeleted ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = workspaceData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 4),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<WorkspaceUserData> ().Where (m => m.Id == workspaceUserData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }

        [Test]
        public async Task ImportPastDeleted ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var workspaceUserData = await DataStore.PutAsync (new WorkspaceUserData () {
                RemoteId = 2,
                WorkspaceId = workspaceData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var workspaceUserJson = new WorkspaceUserJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 2),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceUserJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<WorkspaceUserData> ().Where (m => m.Id == workspaceUserData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
