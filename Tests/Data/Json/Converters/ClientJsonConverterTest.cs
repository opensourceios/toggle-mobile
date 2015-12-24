using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class ClientJsonConverterTest : Test
    {
        private ClientJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new ClientJsonConverter ();
        }

        [Test]
        public async Task ExportExisting ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, clientData));
            Assert.AreEqual (2, json.Id);
            Assert.AreEqual ("Github", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
            Assert.AreEqual (1, json.WorkspaceId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportInvalidWorkspace ()
        {
            ClientData clientData = null;

            clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, clientData));
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
            var clientData = await DataStore.PutAsync (new ClientData () {
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, clientData));
            Assert.IsNull (json.Id);
            Assert.AreEqual ("Github", json.Name);
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
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreNotEqual (Guid.Empty, clientData.Id);
            Assert.AreEqual (2, clientData.RemoteId);
            Assert.AreEqual ("Github", clientData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), clientData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, clientData.WorkspaceId);
            Assert.IsFalse (clientData.IsDirty);
            Assert.IsFalse (clientData.RemoteRejected);
            Assert.IsNull (clientData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreNotEqual (Guid.Empty, clientData.Id);
            Assert.AreEqual (2, clientData.RemoteId);
            Assert.AreEqual ("Github", clientData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), clientData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, clientData.WorkspaceId);
            Assert.IsFalse (clientData.IsDirty);
            Assert.IsFalse (clientData.RemoteRejected);
            Assert.IsNull (clientData.DeletedAt);

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
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreEqual ("Github", clientData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), clientData.ModifiedAt);
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
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreEqual ("Github", clientData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), clientData.ModifiedAt);
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
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreEqual ("Github", clientData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), clientData.ModifiedAt);
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
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreEqual ("", clientData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), clientData.ModifiedAt);
        }

        [Test]
        public async Task ImportMissingWorkspace ()
        {
            var clientJson = new ClientJson () {
                Id = 2,
                Name = "Github",
                WorkspaceId = 1,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var clientData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.AreNotEqual (Guid.Empty, clientData.WorkspaceId);

            var rows = await DataStore.Table<WorkspaceData> ().Where (m => m.Id == clientData.WorkspaceId).ToListAsync ();
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
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var clientJson = new ClientJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 4),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<ClientData> ().Where (m => m.Id == clientData.Id).ToListAsync ();
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
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var clientJson = new ClientJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 2),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, clientJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<ClientData> ().Where (m => m.Id == clientData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
