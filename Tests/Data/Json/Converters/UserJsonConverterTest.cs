using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class UserJsonConverterTest : Test
    {
        private UserJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new UserJsonConverter ();
        }

        [Test]
        public async Task ExportExisting ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "John",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 4),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, userData));
            Assert.AreEqual (1, json.Id);
            Assert.AreEqual ("John", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 4), json.ModifiedAt);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportInvalidWorkspace ()
        {
            UserData userData = null;

            userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "John",
                DefaultWorkspaceId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, userData));
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
            var userData = await DataStore.PutAsync (new UserData () {
                Name = "John",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 4),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, userData));
            Assert.IsNull (json.Id);
            Assert.AreEqual ("John", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 4), json.ModifiedAt);
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
            var userJson = new UserJson () {
                Id = 1,
                Name = "John",
                DefaultWorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 4),
            };

            var userData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.AreNotEqual (Guid.Empty, userData.Id);
            Assert.AreEqual (1, userData.RemoteId);
            Assert.AreEqual ("John", userData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 4), userData.ModifiedAt);
            Assert.IsFalse (userData.IsDirty);
            Assert.IsFalse (userData.RemoteRejected);
            Assert.IsNull (userData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var userJson = new UserJson () {
                Id = 1,
                Name = "John",
                DefaultWorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            userData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.AreNotEqual (Guid.Empty, userData.Id);
            Assert.AreEqual (1, userData.RemoteId);
            Assert.AreEqual ("John", userData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), userData.ModifiedAt);
            Assert.IsFalse (userData.IsDirty);
            Assert.IsFalse (userData.RemoteRejected);
            Assert.IsNull (userData.DeletedAt);

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
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var userJson = new UserJson () {
                Id = 1,
                Name = "John",
                DefaultWorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            userData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.AreEqual ("John", userData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), userData.ModifiedAt);
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
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var userJson = new UserJson () {
                Id = 1,
                Name = "John",
                DefaultWorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            userData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.AreEqual ("John", userData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), userData.ModifiedAt);
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
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var userJson = new UserJson () {
                Id = 1,
                Name = "John",
                DefaultWorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            userData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.AreEqual ("John", userData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), userData.ModifiedAt);
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
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "",
                DefaultWorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var userJson = new UserJson () {
                Id = 1,
                Name = "John",
                DefaultWorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            userData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.AreEqual ("", userData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), userData.ModifiedAt);
        }

        [Test]
        public async Task ImportDeleted ()
        {
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "John",
                ModifiedAt = new DateTime (2014, 1, 4),
            });

            var userJson = new UserJson () {
                Id = 1,
                DeletedAt = new DateTime (2014, 1, 6),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<UserData> ().Where (m => m.Id == userData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }

        [Test]
        public async Task ImportPastDeleted ()
        {
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 1,
                Name = "John",
                ModifiedAt = new DateTime (2014, 1, 4),
            });

            var userJson = new UserJson () {
                Id = 1,
                DeletedAt = new DateTime (2014, 1, 2),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, userJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<UserData> ().Where (m => m.Id == userData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
