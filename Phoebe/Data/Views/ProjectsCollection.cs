using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Views
{
    public class ProjectsCollection : ObservableRangeCollection<CommonData>
    {
        private List<ClientData> clients;
        private List<SuperProjectData> projects;
        private List<UsedProjectData> mostUsedProjects;
        private List<TaskData> tasks;
        private SortProjectsBy sortBy;
        private Guid workspaceId;
        private string projectNameFilter;

        public enum SortProjectsBy {
            Projects,
            Clients
        }

        ProjectsCollection (SortProjectsBy sortBy, Guid workspaceId)
        {
            this.sortBy = sortBy;
            this.workspaceId = workspaceId;
        }

        public static async Task<ProjectsCollection> Init (SortProjectsBy sortBy, Guid workspaceId)
        {
            var v = new ProjectsCollection (sortBy, workspaceId);
            var store = ServiceContainer.Resolve<IDataStore> ();
            var userData = ServiceContainer.Resolve<AuthManager> ().User;

            var projectsTask = store.GetUserAccessibleProjects (userData.Id);
            var mostUsedProjectsTask = store.GetMostUsedProjects (userData.Id);
            var clientsTask = store.Table<ClientData> ().Where (r => r.DeletedAt == null)
                              .OrderBy (r => r.Name).ToListAsync ();
            var tasksTask = store.Table<TaskData> ().Where (r => r.DeletedAt == null && r.IsActive)
                            .OrderBy (r => r.Name).ToListAsync();
            await Task.WhenAll (projectsTask, mostUsedProjectsTask, tasksTask, clientsTask);

            v.clients = clientsTask.Result;
            v.tasks = tasksTask.Result;
            v.projects = projectsTask.Result.Select (p =>
                         new SuperProjectData (p,
                                               v.clients.FirstOrDefault (c => c.Id == p.ClientId),
                                               v.tasks.Count (t => t.ProjectId == p.Id))
                                                    ).ToList ();
            v.mostUsedProjects = mostUsedProjectsTask.Result.Select (p =>
                                 new UsedProjectData (p,
                                         v.clients.FirstOrDefault (c => c.Id == p.ClientId),
                                         v.tasks.Count (t => t.ProjectId == p.Id))
                                                                    ).ToList ();
            // Create collection
            v.CreateSortedCollection (v.projects);
            return v;
        }

        #region List operations
        public SortProjectsBy SortBy
        {
            get {
                return sortBy;
            } set {
                if (sortBy == value) {
                    return;
                }
                sortBy = value;
                CreateSortedCollection (projects);
            }
        }

        public Guid WorkspaceId
        {
            get {
                return workspaceId;
            } set {
                if (workspaceId == value) {
                    return;
                }
                workspaceId = value;
                CreateSortedCollection (projects);
            }
        }

        public string ProjectNameFilter
        {
            get {
                return projectNameFilter;
            } set {
                if (projectNameFilter == value) {
                    return;
                }
                projectNameFilter = value;
                var prjs = string.IsNullOrEmpty (value) ? projects : projects.Where (p => p.Name.ToLower ().Contains (projectNameFilter.ToLower ()));
                CreateSortedCollection (prjs);
            }
        }
        #endregion

        private void CreateSortedCollection (IEnumerable<SuperProjectData> projectList)
        {
            var enumerable = projectList as IList<SuperProjectData> ?? projectList.ToList ();
            var data = new List<CommonData> ();

            // Add most used projects on top of the list.
            var mostUsed = mostUsedProjects.Where (p => p.WorkspaceId == workspaceId).Take (5).ToList();
            if (mostUsed.Count > 0 && enumerable.Count > 8) { // only add if more than 8 projects total
                data.Add (new Heading ());
                data.AddRange (mostUsed);
            }

            // TODO: Maybe group using linq is clearer
            if (sortBy == SortProjectsBy.Clients) {

                // Add section without client
                data.Add (new ClientData ());
                data.Add (GetEmptyProject ());
                enumerable.Where (p => p.ClientId == null && p.WorkspaceId == workspaceId).ForEach (data.Add);

                // Add normal sections
                var sectionHeaders = clients.Where (p => p.WorkspaceId == workspaceId);
                foreach (var header in sectionHeaders) {
                    var sectionItems = enumerable.Where (p => p.ClientId == header.Id && p.WorkspaceId == workspaceId).ToList ();
                    if (sectionItems.Count > 0) {
                        data.Add (header);
                        sectionItems.ForEach (data.Add);
                    }
                }
            } else {
                data.Add (GetEmptyProject ());
                enumerable.Where (p => p.WorkspaceId == workspaceId).ForEach (data.Add);
            }

            // ObservableRange method :)
            Reset (data);
        }

        public void AddTasks (ProjectData project)
        {
            // Remove previous tasks
            var oldTaskIndex = this.IndexOf (p => p is TaskData);
            if (oldTaskIndex != -1) {
                RemoveRange (this.OfType<TaskData> ());
            }

            // Insert new tasks
            var newTaskIndex = this.IndexOf (p => p.Id == project.Id) + 1;
            if (oldTaskIndex != newTaskIndex) {
                InsertRange (tasks.Where (p => p.ProjectId == project.Id), newTaskIndex);
            }
        }


        public class CommonProjectData : ProjectData
        {
            public string ClientName { get; private set; }
            public int TaskNumber { get; private set; }
            public bool IsEmpty { get; private set; }

            public CommonProjectData (ProjectData dataObject,
                                      ClientData client, int taskNumber, bool isEmpty = false) : base (dataObject)
            {
                TaskNumber = taskNumber;
                IsEmpty = isEmpty;
                ClientName = client != null ? client.Name : "";
            }
        }

        public class SuperProjectData : CommonProjectData
        {
            public SuperProjectData (ProjectData d, ClientData c, int n, bool e = false) : base (d, c, n, e) {}
        }

        public class UsedProjectData : CommonProjectData
        {
            public UsedProjectData (ProjectData d, ClientData c, int n, bool e = false) : base (d, c, n, e) {}
        }

        public class Heading : CommonData
        {
            public string Text { get; private set; }

            public Heading (string text = null)
            {
                Text = text;
            }
        }

        private SuperProjectData GetEmptyProject ()
        {
            return new SuperProjectData (new ProjectData (), null, 0, true);
        }
    }
}
