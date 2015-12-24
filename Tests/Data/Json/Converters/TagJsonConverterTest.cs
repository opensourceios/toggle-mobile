using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class TagJsonConverterTest : Test
    {
        private TagJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new TagJsonConverter ();
        }

        [Test]
        public async Task ExportExisting ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "Mobile",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, tagData));
            Assert.AreEqual (2, json.Id);
            Assert.AreEqual ("Mobile", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
            Assert.AreEqual (1, json.WorkspaceId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportInvalidWorkspace ()
        {
            TagData tagData = null;

            tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "Mobile",
                WorkspaceId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, tagData));
            }, Throws.Exception.TypeOf<RelationRemoteIdMissingException> ());
        }

        [Test]
        public async Task ExportNew ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                Name = "Mobile",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, tagData));
            Assert.IsNull (json.Id);
            Assert.AreEqual ("Mobile", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
            Assert.AreEqual (1, json.WorkspaceId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ImportNew ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreNotEqual (Guid.Empty, tagData.Id);
            Assert.AreEqual (2, tagData.RemoteId);
            Assert.AreEqual ("Mobile", tagData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), tagData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, tagData.WorkspaceId);
            Assert.IsFalse (tagData.IsDirty);
            Assert.IsFalse (tagData.RemoteRejected);
            Assert.IsNull (tagData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreNotEqual (Guid.Empty, tagData.Id);
            Assert.AreEqual (2, tagData.RemoteId);
            Assert.AreEqual ("Mobile", tagData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), tagData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, tagData.WorkspaceId);
            Assert.IsFalse (tagData.IsDirty);
            Assert.IsFalse (tagData.RemoteRejected);
            Assert.IsNull (tagData.DeletedAt);

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
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreEqual ("Mobile", tagData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), tagData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite dirty local data if imported data has a modification time greater than local.")]
        public async Task ImportUpdatedOverwriteDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreEqual ("Mobile", tagData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), tagData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite local dirty-but-rejected data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteRejectedLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreEqual ("Mobile", tagData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), tagData.ModifiedAt);
        }

        [Test]
        [Description ("Keep local dirty data when imported data has same or older modification time.")]
        public async Task ImportUpdatedKeepDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreEqual ("", tagData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), tagData.ModifiedAt);
        }

        [Test]
        public async Task ImportMissingWorkspace ()
        {
            var tagJson = new TagJson () {
                Id = 2,
                Name = "Mobile",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var tagData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.AreNotEqual (Guid.Empty, tagData.WorkspaceId);

            var rows = await DataStore.Table<WorkspaceData> ().Where (m => m.Id == tagData.WorkspaceId).ToListAsync ();
            var workspaceData = rows.FirstOrDefault ();
            Assert.IsNotNull (workspaceData);
            Assert.IsNotNull (workspaceData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, workspaceData.ModifiedAt);
        }

        [Test]
        public async Task ImportDeleted ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "Mobile",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var tagJson = new TagJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 4),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<TagData> ().Where (m => m.Id == tagData.Id).ToListAsync ();
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
            var tagData = await DataStore.PutAsync (new TagData () {
                RemoteId = 2,
                Name = "Mobile",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var tagJson = new TagJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 2),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, tagJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<TagData> ().Where (m => m.Id == tagData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
