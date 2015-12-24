using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;
using System.Threading.Tasks;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class ProjectUserJsonConverterTest : Test
    {
        private ProjectUserJsonConverter converter;

        public override async Task SetUp ()
        {
            await base.SetUp ();

            converter = new ProjectUserJsonConverter ();
        }

        [Test]
        public async Task ExportExisting ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 3,
                Name = "Hosting",
                WorkspaceId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 5,
                Name = "John",
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 4,
                ProjectId = projectData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectUserData));
            Assert.AreEqual (4, json.Id);
            Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
            Assert.AreEqual (3, json.ProjectId);
            Assert.AreEqual (5, json.UserId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ExportInvalidProjectAndUser ()
        {
            ProjectUserData projectUserData = null;

            projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 4,
                ProjectId = Guid.NewGuid (),
                UserId = Guid.NewGuid (),
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            Assert.That (async () => {
                await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectUserData));
            }, Throws.Exception.TypeOf<RelationRemoteIdMissingException> ());
        }

        [Test]
        public async Task ExportNew ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                ProjectId = projectData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var json = await DataStore.ExecuteInTransactionAsync (ctx => converter.Export (ctx, projectUserData));
            Assert.IsNull (json.Id);
            Assert.AreEqual (1, json.ProjectId);
            Assert.AreEqual (2, json.UserId);
            Assert.IsNull (json.DeletedAt);
        }

        [Test]
        public async Task ImportNew ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreNotEqual (Guid.Empty, projectUserData.Id);
            Assert.AreEqual (2, projectUserData.RemoteId);
            Assert.AreEqual (new DateTime (2014, 1, 3), projectUserData.ModifiedAt);
            Assert.AreEqual (projectData.Id, projectUserData.ProjectId);
            Assert.AreEqual (userData.Id, projectUserData.UserId);
            Assert.IsFalse (projectUserData.IsDirty);
            Assert.IsFalse (projectUserData.RemoteRejected);
            Assert.IsNull (projectUserData.DeletedAt);
        }

        [Test]
        public async Task ImportUpdated ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc).ToLocalTime (), // JSON deserialized to local
            };

            projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreNotEqual (Guid.Empty, projectUserData.Id);
            Assert.AreEqual (2, projectUserData.RemoteId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc), projectUserData.ModifiedAt);
            Assert.AreEqual (projectData.Id, projectUserData.ProjectId);
            Assert.AreEqual (userData.Id, projectUserData.UserId);
            Assert.IsFalse (projectUserData.IsDirty);
            Assert.IsFalse (projectUserData.RemoteRejected);
            Assert.IsNull (projectUserData.DeletedAt);

            // Warn the user that the test result might be invalid
            if (TimeZone.CurrentTimeZone.GetUtcOffset (DateTime.Now).TotalMinutes >= 0) {
                Assert.Inconclusive ("The test machine timezone should be set to GTM-1 or less to test datetime comparison.");
            }
        }

        [Test]
        [Description ("Overwrite local non-dirty data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteNonDirtyLocal ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
            });
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc).ToLocalTime (), // Remote modified is less than local
            };

            projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreEqual (projectData.Id, projectUserData.ProjectId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 9, 59, 0, DateTimeKind.Utc), projectUserData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite dirty local data if imported data has a modification time greater than local.")]
        public async Task ImportUpdatedOverwriteDirtyLocal ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 9, 59, 59, DateTimeKind.Utc),
                IsDirty = true,
            });
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreEqual (projectData.Id, projectUserData.ProjectId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), projectUserData.ModifiedAt);
        }

        [Test]
        [Description ("Overwrite local dirty-but-rejected data regardless of the modification times.")]
        public async Task ImportUpdatedOverwriteRejectedLocal ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 1, 0, DateTimeKind.Utc),
                IsDirty = true,
                RemoteRejected = true,
            });
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreEqual (projectData.Id, projectUserData.ProjectId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), projectUserData.ModifiedAt);
        }

        [Test]
        [Description ("Keep local dirty data when imported data has same or older modification time.")]
        public async Task ImportUpdatedKeepDirtyLocal ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = Guid.Empty,
                UserId = Guid.Empty,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                IsDirty = true,
            });
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc).ToLocalTime (),
            };

            projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreEqual (Guid.Empty, projectUserData.ProjectId);
            Assert.AreEqual (new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc), projectUserData.ModifiedAt);
        }

        [Test]
        public async Task ImportMissingProjectAndUser ()
        {
            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                ProjectId = 1,
                UserId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            };

            var projectUserData = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.AreNotEqual (Guid.Empty, projectUserData.ProjectId);
            Assert.AreNotEqual (Guid.Empty, projectUserData.UserId);

            var projectRows = await DataStore.Table<ProjectData> ().Where (m => m.Id == projectUserData.ProjectId).ToListAsync ();
            var projectData = projectRows.FirstOrDefault ();
            Assert.IsNotNull (projectData);
            Assert.IsNotNull (projectData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, projectData.ModifiedAt);

            var userRows = await DataStore.Table<UserData> ().Where (m => m.Id == projectUserData.UserId).ToListAsync ();
            var userData = userRows.FirstOrDefault ();
            Assert.IsNotNull (userData);
            Assert.IsNotNull (userData.RemoteId);
            Assert.AreEqual (DateTime.MinValue, userData.ModifiedAt);
        }

        [Test]
        public async Task ImportDeleted ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = projectData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 4),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<ProjectUserData> ().Where (m => m.Id == projectUserData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }

        [Test]
        public async Task ImportPastDeleted ()
        {
            var projectData = await DataStore.PutAsync (new ProjectData () {
                RemoteId = 1,
                ModifiedAt = new DateTime (2014, 1, 2),
            });
            var userData = await DataStore.PutAsync (new UserData () {
                RemoteId = 2,
                ModifiedAt = new DateTime (2014, 1, 3),
            });
            var projectUserData = await DataStore.PutAsync (new ProjectUserData () {
                RemoteId = 2,
                ProjectId = projectData.Id,
                UserId = userData.Id,
                ModifiedAt = new DateTime (2014, 1, 3),
            });

            var projectUserJson = new ProjectUserJson () {
                Id = 2,
                DeletedAt = new DateTime (2014, 1, 2),
            };

            var ret = await DataStore.ExecuteInTransactionAsync (ctx => converter.Import (ctx, projectUserJson));
            Assert.IsNull (ret);

            var rows = await DataStore.Table<ProjectUserData> ().Where (m => m.Id == projectUserData.Id).ToListAsync ();
            Assert.That (rows, Has.Exactly (0).Count);
        }
    }
}
