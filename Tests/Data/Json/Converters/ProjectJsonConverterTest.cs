﻿using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Json.Converters;

namespace Toggl.Phoebe.Tests.Data.Json.Converters
{
    public class ProjectJsonConverterTest : Test
    {
        private ProjectJsonConverter converter;

        public override void SetUp ()
        {
            base.SetUp ();

            converter = new ProjectJsonConverter ();
        }

        [Test]
        public void ExportExisting ()
        {
            RunAsync (async delegate {
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

                var json = await converter.Export (projectData);
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
            });
        }

        [Test]
        public void ExportInvalidClient ()
        {
            ProjectData projectData = null;

            RunAsync (async delegate {
                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 1,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    Color = 2,
                    IsActive = true,
                    ClientId = Guid.NewGuid (),
                    WorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 3),
                });
            });

            Assert.That (() => converter.Export (projectData).GetAwaiter ().GetResult (),
                Throws.Exception.TypeOf<InvalidOperationException> ());
        }

        [Test]
        public void ExportInvalidWorkspace ()
        {
            ProjectData projectData = null;

            RunAsync (async delegate {
                projectData = await DataStore.PutAsync (new ProjectData () {
                    RemoteId = 3,
                    Name = "Hosting",
                    Color = 2,
                    IsActive = true,
                    WorkspaceId = Guid.NewGuid (),
                    ModifiedAt = new DateTime (2014, 1, 3),
                });
            });

            Assert.That (() => converter.Export (projectData).GetAwaiter ().GetResult (),
                Throws.Exception.TypeOf<InvalidOperationException> ());
        }

        [Test]
        public void ExportNew ()
        {
            RunAsync (async delegate {
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

                var json = await converter.Export (projectData);
                Assert.IsNull (json.Id);
                Assert.AreEqual ("Hosting", json.Name);
                Assert.AreEqual (new DateTime (2014, 1, 3), json.ModifiedAt);
                Assert.AreEqual (1, json.WorkspaceId);
                Assert.IsNull (json.DeletedAt);
            });
        }

        [Test]
        public void ImportNew ()
        {
            RunAsync (async delegate {
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

                var projectData = await converter.Import (projectJson);
                Assert.AreNotEqual (Guid.Empty, projectData.Id);
                Assert.AreEqual (3, projectData.RemoteId);
                Assert.AreEqual ("Hosting", projectData.Name);
                Assert.AreEqual (new DateTime (2014, 1, 3), projectData.ModifiedAt);
                Assert.AreEqual (workspaceData.Id, projectData.WorkspaceId);
                Assert.AreEqual (clientData.Id, projectData.ClientId);
                Assert.IsFalse (projectData.IsDirty);
                Assert.IsFalse (projectData.RemoteRejected);
                Assert.IsNull (projectData.DeletedAt);
            });
        }

        [Test]
        public void ImportMissingWorkspaceAndClient ()
        {
            RunAsync (async delegate {
                var projectJson = new ProjectJson () {
                    Id = 3,
                    Name = "Github",
                    WorkspaceId = 1,
                    ClientId = 2,
                    ModifiedAt = new DateTime (2014, 1, 3),
                };

                var projectData = await converter.Import (projectJson);
                Assert.AreNotEqual (Guid.Empty, projectData.WorkspaceId);

                var workspaceRows = await DataStore.Table<WorkspaceData> ().QueryAsync (m => m.Id == projectData.WorkspaceId);
                var workspaceData = workspaceRows.FirstOrDefault ();
                Assert.IsNotNull (workspaceData);
                Assert.IsNotNull (workspaceData.RemoteId);
                Assert.AreEqual (DateTime.MinValue, workspaceData.ModifiedAt);

                var clientRows = await DataStore.Table<ClientData> ().QueryAsync (m => m.Id == projectData.ClientId);
                var clientData = clientRows.FirstOrDefault ();
                Assert.IsNotNull (clientData);
                Assert.IsNotNull (clientData.RemoteId);
                Assert.AreEqual (DateTime.MinValue, clientData.ModifiedAt);
            });
        }

        [Test]
        public void ImportDeleted ()
        {
            RunAsync (async delegate {
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

                var ret = await converter.Import (projectJson);
                Assert.IsNull (ret);

                var rows = await DataStore.Table<ProjectData> ().QueryAsync (m => m.Id == projectData.Id);
                Assert.That (rows, Has.Exactly (0).Count);
            });
        }

        [Test]
        public void ImportPastDeleted ()
        {
            RunAsync (async delegate {
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

                var ret = await converter.Import (projectJson);
                Assert.IsNull (ret);

                var rows = await DataStore.Table<ProjectData> ().QueryAsync (m => m.Id == projectData.Id);
                Assert.That (rows, Has.Exactly (0).Count);
            });
        }
    }
}