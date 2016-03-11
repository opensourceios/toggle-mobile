using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using Toggl.Phoebe.Logging;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    public interface IOnProjectSelectedHandler
    {
        void OnProjectSelected (Guid projectId, Guid taskId);
    }

    [ImplementPropertyChanged]
    public class ProjectListViewModel : ViewModelBase, IDisposable
    {
        ProjectListViewModel (Guid workspaceId)
        {
            CurrentWorkspaceId = workspaceId;
            MostUsedProjects = new List<CommonProjectData> ();
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Select Project";
        }

        public static async Task<ProjectListViewModel> Init (Guid workspaceId)
        {
            var vm = new ProjectListViewModel (workspaceId);

            var store = ServiceContainer.Resolve<IDataStore> ();
            var settingsStore = ServiceContainer.Resolve<ISettingsStore> ();

            // Change settings for a better library like
            // James Montemagno version and define default
            // values to avoid this code.
            if (string.IsNullOrEmpty (settingsStore.SortProjectsBy)) {
                settingsStore.SortProjectsBy = ProjectsCollection.SortProjectsBy.Clients.ToString ();
            }
            var savedSort = Enum.Parse (typeof (ProjectsCollection.SortProjectsBy), settingsStore.SortProjectsBy);

            vm.ProjectList = await ProjectsCollection.Init ((ProjectsCollection.SortProjectsBy)savedSort, workspaceId);
            vm.WorkspaceList = await store.Table<WorkspaceData> ().Where (r => r.DeletedAt == null)
                               .OrderBy (r => r.Name).ToListAsync ();
            vm.CurrentWorkspaceIndex = vm.WorkspaceList.IndexOf (p => p.Id == workspaceId);

            await vm.PopulateMostUsedProjects ();

            // Search stream
            Observable.FromEventPattern<string> (ev => vm.onSearch += ev, ev => vm.onSearch -= ev)
            .Throttle (TimeSpan.FromMilliseconds (300))
            .DistinctUntilChanged ()
            .Subscribe
            (p => ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                vm.ProjectList.ProjectNameFilter = p.EventArgs;
            }),
            ex => ServiceContainer.Resolve<ILogger> ().Error ("Search", ex, null));

            return vm;
        }

        public async Task PopulateMostUsedProjects ()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            var settingsStore = ServiceContainer.Resolve<ISettingsStore> ();

            var recentEntries = await store.Table<TimeEntryData> ()
                                .OrderByDescending (r => r.StartTime)
                                .Where (r => r.DeletedAt == null
                                        && r.UserId == settingsStore.UserId
                                        && r.State != TimeEntryState.New
                                        && r.ProjectId != null
                                        && r.WorkspaceId == CurrentWorkspaceId )
                                .ToListAsync ()
                                .ConfigureAwait (false);

            var uniqueRows = recentEntries
            .GroupBy (p => new {p.ProjectId, p.TaskId})
            .Select (g => g.First ())
            .Take (3)
            .ToList ();

            MostUsedProjects.Clear ();
            foreach (var entry in uniqueRows) {

                var project = new ProjectModel (entry.ProjectId ?? Guid.Empty);
                var task = new TaskModel (entry.TaskId ?? Guid.Empty);

                await project.LoadAsync ();
                await task.LoadAsync ();
                var client = project.Client == null ? String.Empty : project.Client.Name;
                MostUsedProjects.Add (new CommonProjectData (project.Data, client, task.Data ?? null));
            }
        }

        public void Dispose ()
        {
        }

        #region Observable properties

        public List<WorkspaceData> WorkspaceList { get; private set; }

        public ProjectsCollection ProjectList { get; private set; }

        public List<CommonProjectData> MostUsedProjects { get; private set; }

        public int CurrentWorkspaceIndex { get; private set; }

        public Guid CurrentWorkspaceId { get; private set; }

        #endregion

        private event EventHandler<string> onSearch;

        public void SearchByProjectName (string token)
        {
            onSearch.Invoke (this, token);
        }

        public void ChangeListSorting (ProjectsCollection.SortProjectsBy sortBy)
        {
            ProjectList.SortBy = sortBy;
            var settingsStore = ServiceContainer.Resolve<ISettingsStore> ();
            settingsStore.SortProjectsBy = sortBy.ToString ();
        }

        public void ChangeWorkspaceByIndex (int newIndex)
        {
            CurrentWorkspaceId = WorkspaceList [newIndex].Id;
            PopulateMostUsedProjects ();
            ProjectList.WorkspaceId = WorkspaceList [newIndex].Id;
            CurrentWorkspaceIndex = newIndex;
        }
    }

    public class CommonProjectData : ProjectData
    {
        public string ClientName { get; private set; }
        public TaskData Task { get; private set; }

        public CommonProjectData (ProjectData dataObject, string clientName, TaskData task = null) : base (dataObject)
        {
            Task = task;
            ClientName = clientName;
        }
    }
}
