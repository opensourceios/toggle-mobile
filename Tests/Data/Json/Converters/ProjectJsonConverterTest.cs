using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class ProjectJsonConverterTest : Test
    {
        private ProjectJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new ProjectJsonConverter ();
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
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                ClientId = clientData.Id,
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectData));
            Assert.AreEqual (3, json.Id);
            Assert.AreEqual ("Hosting", json.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
            Assert.AreEqual (1, json.WorkspaceId);
            Assert.AreEqual (2, json.ClientId);
            Assert.IsTrue (json.IsActive);
            Assert.IsFalse (json.IsBillable);
            Assert.IsFalse (json.IsPrivate);
            Assert.IsFalse (json.IsTemplate);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportInvalidClient ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                ClientId = Guid.NewGuid (),
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            await DataStore.ExecuteInTransactionAsync (ctx => {
                var json = converter.Export (ctx, projectData);
                Assert.IsNull (json.ClientId);
            });
        }

        [Test]
        public async Task ExportMissingClientRemoteId ()
        {
            ProjectData projectData = null;

            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                Name = "Test",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                ClientId = clientData.Id,
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectData));
            }, Throws.Exception.TypeOf<RelationRemoteIdMissingException> ());
        }

        [Test]
        public async Task ExportInvalidWorkspace ()
        {
            ProjectData projectData = null;

            projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                WorkspaceId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectData));
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
            var projectData = await DataStore.PutAsync (new ProjectData () {
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectData));
            Assert.IsNull (json.Id);
            Assert.AreEqual ("Hosting", json.Name);
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
                ModifiedAt = new
                DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Hosting",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreNotEqual (Guid.Empty, projectData.Id);
            Assert.AreEqual (3, projectData.RemoteId);
            Assert.AreEqual ("Hosting", projectData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 3), projectData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, projectData.WorkspaceId);
            Assert.AreEqual (clientData.Id, projectData.ClientId);
            Assert.IsFalse (projectData.IsDirty);
            Assert.IsFalse (projectData.RemoteRejected);
            Assert.IsNull (projectData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new
                DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ClientId = clientData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Hosting",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreNotEqual (Guid.Empty, projectData.Id);
            Assert.AreEqual (3, projectData.RemoteId);
            Assert.AreEqual ("Hosting", projectData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), projectData.ModifiedAt);
            Assert.AreEqual (workspaceData.Id, projectData.WorkspaceId);
            Assert.AreEqual (clientData.Id, projectData.ClientId);
            Assert.IsFalse (projectData.IsDirty);
            Assert.IsFalse (projectData.RemoteRejected);
            Assert.IsNull (projectData.DeletedAt);

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
                ModifiedAt = new
                DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ClientId = clientData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Hosting",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreEqual ("Hosting", projectData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), projectData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite dirty local data if imported data has a modification time greater than local.")]
        public async Task ImportUpdatedOverwriteDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new
                DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ClientId = clientData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Hosting",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreEqual ("Hosting", projectData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), projectData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite local dirty-but-rejected data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteRejectedLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new
                DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 2,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ClientId = clientData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Hosting",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreEqual ("Hosting", projectData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), projectData.ModifiedAt);
        }

        [Test]
        [Description ("Keep local dirty data when imported data has same or older modification time.")]
        public async Task ImportUpdatedKeepDirtyLocal ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new
                DateTime (2014, 1, 2),
            });
            var clientData = await DataStore.PutAsync (new ClientData () {
                RemoteId = 2,
                Name = "Github",
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "",
                WorkspaceId = workspaceData.Id,
                ClientId = clientData.Id,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Hosting",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreEqual ("", projectData.Name);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), projectData.ModifiedAt);
        }

        [Test]
        public async Task ImportMissingWorkspaceAndClient ()
        {
            var projectJson = new ProjectJson () {
                Id = 3,
                Name = "Github",
                WorkspaceId = 1,
                ClientId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var projectData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.AreNotEqual (Guid.Empty, projectData.WorkspaceId);

            var workspaceRows = await DataStore.Table<WorkspaceData> ().Where (m => m.Id == projectData.WorkspaceId).ToListAsync ();
            var workspaceData = workspaceRows.FirstOrDefault ();
            Assert.IsNotNull (workspaceData);
            Assert.IsNotNull (workspaceData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, workspaceData.ModifiedAt);

            var clientRows = await DataStore.Table<ClientData> ().Where (m => m.Id == projectData.ClientId).ToListAsync ();
            var clientData = clientRows.FirstOrDefault ();
            Assert.IsNotNull (clientData);
            Assert.IsNotNull (clientData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, clientData.ModifiedAt);
        }

        [Test]
        public async Task ImportDeleted ()
        {
            var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                RemoteId = 1,
                Name = "Test",
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var projectJson = new ProjectJson () {
                Id = 3,
                DeletedAt = new DateTime (2014, 1, 4),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<ProjectData> ().Where (m => m.Id == projectData.Id).ToListAsync ();
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
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                Color = 2,
                IsActive = true,
                WorkspaceId = workspaceData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var projectJson = new ProjectJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 2),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<ProjectData> ().Where (m => m.Id == projectData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
