using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class WorkspaceJsonConverterTest : Test
    {
        private WorkspaceJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new WorkspaceJsonConverter ();
        }

        [Test]
        public async Task ExportExisting ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, workspaceData));
            Assert.AreEqual (1, json.Id);
            Assert.AreEqual ("Test", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2), json.ModifiedAt);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportNew ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, workspaceData));
            Assert.IsNull (json.Id);
            Assert.AreEqual ("Test", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2), json.ModifiedAt);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ImportNew ()
        {
            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            };

            var workspaceData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.AreNotEqual (Guid.Empty, workspaceData.Id);
            Assert.AreEqual (1, workspaceData.RemoteId);
            Assert.AreEqual ("Test", workspaceData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2), workspaceData.ModifiedAt);
            Assert.IsFalse (workspaceData.IsDirty);
            Assert.IsFalse (workspaceData.RemoteRejected);
            Assert.IsNull (workspaceData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            workspaceData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.AreNotEqual (Guid.Empty, workspaceData.Id);
            Assert.AreEqual (1, workspaceData.RemoteId);
            Assert.AreEqual ("Test", workspaceData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), workspaceData.ModifiedAt);
            Assert.IsFalse (workspaceData.IsDirty);
            Assert.IsFalse (workspaceData.RemoteRejected);
            Assert.IsNull (workspaceData.DeletedAt);

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
                Name = "",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            workspaceData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.AreEqual ("Test", workspaceData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), workspaceData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite dirty local data if imported data has a modification time greater than local.")]
        public async Task ImportUpdatedOverwriteDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "",
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            workspaceData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.AreEqual ("Test", workspaceData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), workspaceData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite local dirty-but-rejected data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteRejectedLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            workspaceData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.AreEqual ("Test", workspaceData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), workspaceData.ModifiedAt);
        }

        [Test]
        [Description ("Keep local dirty data when imported data has same or older modification time.")]
        public async Task ImportUpdatedKeepDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            workspaceData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.AreEqual ("", workspaceData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), workspaceData.ModifiedAt);
        }

        [Test]
        public async Task ImportDeleted ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });

            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                DeletedAt = new DateTime (2014, 1, 4),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<WorkspaceData> ().Where (m => m.Id == workspaceData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }

        [Test]
        public async Task ImportPastDeleted ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });

            var workspaceJson = new WorkspaceJson () {
                Id = 1,
                DeletedAt = new DateTime (2014, 1, 1),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, workspaceJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<WorkspaceData> ().Where (m => m.Id == workspaceData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
