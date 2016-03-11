using System;
using Android.Content;
using Android.Graphics;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Views;
using XPlatUtils;
using System.Collections.Generic;
using Toggl.Phoebe.Data.ViewModels;

namespace Toggl.Joey.UI.Adapters
{
    public class ProjectListAdapter : RecyclerCollectionDataAdapter<CommonData>
    {
        protected const int ViewTypeProject = ViewTypeContent;
        protected const int ViewTypeClient = ViewTypeContent + 1;
        protected const int ViewTypeTask = ViewTypeContent + 2;
        protected const int ViewTypeTopProjects = ViewTypeContent + 3;

        protected ProjectsCollection collectionView;
        protected ProjectListViewModel viewModel;
        public Action<CommonData> HandleItemSelection { get; set; }

        public ProjectListAdapter (RecyclerView owner, ProjectListViewModel viewModel) : base (owner, viewModel.ProjectList)
        {
            this.viewModel = viewModel;
            collectionView = viewModel.ProjectList;
        }

        protected override RecyclerView.ViewHolder GetViewHolder (ViewGroup parent, int viewType)
        {
            View view;
            RecyclerView.ViewHolder holder;
            var inflater = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ());

            switch (viewType) {
            case ViewTypeTopProjects:
                view = inflater.Inflate (Resource.Layout.ProjectListMostUsedList, parent, false);
                holder = new TopProjectsHolder (this, view);
                break;
            case ViewTypeClient:
                view = inflater.Inflate (Resource.Layout.ProjectListClientItem, parent, false);
                holder = new ClientItemHolder (view);
                break;
            case ViewTypeTask:
                view = inflater.Inflate (Resource.Layout.ProjectListTaskItem, parent, false);
                holder = new TaskItemHolder (this, view);
                break;
            default:
                view = inflater.Inflate (Resource.Layout.ProjectListProjectItem, parent, false);
                holder = new ProjectItemHolder (this, view);
                break;
            }
            return holder;
        }

        protected override void BindHolder (RecyclerView.ViewHolder holder, int position)
        {
            var viewType = GetItemViewType (position);

            if (viewType == ViewTypeTopProjects) {
                ((TopProjectsHolder)holder).Bind (viewModel.MostUsedProjects);
            } else if (viewType == ViewTypeTask) {
                ((TaskItemHolder)holder).Bind ((TaskData)GetItem (position - 1));
            } else if (viewType == ViewTypeClient) {
                ((ClientItemHolder)holder).Bind (((ClientData)GetItem (position - 1)).Name);
            } else if (viewType == ViewTypeProject) {
                var showClientName = collectionView.SortBy == ProjectsCollection.SortProjectsBy.Projects;
                ((ProjectItemHolder)holder).Bind ((ProjectsCollection.SuperProjectData)GetItem (position - 1), showClientName);
            }
        }

        public override int GetItemViewType (int position)
        {
            if (position == 0) {
                return ViewTypeTopProjects;
            }
            var type = base.GetItemViewType (position - 1);

            if (type == ViewTypeLoaderPlaceholder) {
                return type;
            }
            var dataObject = GetItem (position - 1);

            if (dataObject is ProjectsCollection.SuperProjectData) {
                return ViewTypeProject;
            }

            if (dataObject is ClientData) {
                return ViewTypeClient;
            }

            if (dataObject is TaskData) {
                return ViewTypeTask;
            }
            return type;
        }

        public override int ItemCount
        {
            get {
                return base.ItemCount + 1;
            }
        }
        #region View holders

        public class TopProjectsHolder :  RecyclerView.ViewHolder
        {
            readonly TextView HeaderTextView;
            readonly LinearLayout ProjectsContainer;
            private List<LinearLayout> ProjectListLayouts;

            private ViewGroup parent;
            private View Root;

            private ProjectListAdapter adapter;

            public TopProjectsHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                HeaderTextView = root.FindViewById<TextView> (Resource.Id.HeaderTextView).SetFont (Font.RobotoMedium);
                ProjectsContainer = root.FindViewById<LinearLayout> (Resource.Id.ProjectsContainer);
            }

            public void Bind (List<CommonProjectData> projects)
            {
                HeaderTextView.Visibility = projects.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
                ProjectsContainer.Visibility = projects.Count == 0 ? ViewStates.Gone : ViewStates.Visible;

                ProjectsContainer.RemoveAllViews ();
                var inflater = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ());
                foreach (var project in projects) {

                    var view = inflater.Inflate (Resource.Layout.ProjectListUsedProjectItem, parent, false);

                    var projectTextView = view.FindViewById<TextView> (Resource.Id.ProjectTextView);
                    var clientTextView = view.FindViewById<TextView> (Resource.Id.ClientTextView);
                    var taskTextView = view.FindViewById<TextView> (Resource.Id.TaskTextView);
                    var colorView = view.FindViewById<View> (Resource.Id.ColorView);

                    projectTextView.Text = project.Name;
                    clientTextView.Visibility = String.IsNullOrEmpty (project.ClientName) ? ViewStates.Gone : ViewStates.Visible;
                    clientTextView.Text = project.ClientName;

                    taskTextView.Text = project.Task == null ? String.Empty : project.Task.Name;

                    var color = Color.ParseColor (ProjectModel.HexColors [project.Color % ProjectModel.HexColors.Length]);
                    colorView.SetBackgroundColor (color);
                    projectTextView.SetTextColor (color);

                    view.Click += (sender, e) => {
                        if (project.Task == null) {
                            adapter.HandleItemSelection.Invoke (project);
                        } else {
                            adapter.HandleItemSelection.Invoke (project.Task);
                        }
                    };
                    ProjectsContainer.AddView (view);
                }
            }
        }

        protected class ProjectItemHolder : RecyclerView.ViewHolder, View.IOnClickListener
        {
            protected View ColorView { get; private set; }
            protected TextView ProjectTextView { get; private set; }
            protected TextView ClientTextView { get; private set; }
            protected Button TasksButton { get; private set; }
            protected ImageView TasksImageView { get; private set; }

            private ProjectListAdapter adapter;
            private ProjectsCollection.SuperProjectData projectData;

            public ProjectItemHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                ColorView = root.FindViewById<View> (Resource.Id.ColorView);
                ProjectTextView = root.FindViewById<TextView> (Resource.Id.ProjectTextView).SetFont (Font.Roboto);
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.RobotoLight);
                TasksButton = root.FindViewById<Button> (Resource.Id.TasksButton);
                TasksButton.Click += (sender, e) => adapter.collectionView.AddTasks (projectData);
                root.SetOnClickListener (this);
            }

            public void OnClick (View v)
            {
                if (v == TasksButton) {
                    return;
                }

                if (adapter.HandleItemSelection != null) {
                    adapter.HandleItemSelection.Invoke (projectData);
                }
            }

            public void Bind (ProjectsCollection.SuperProjectData projectData, bool showClient)
            {
                this.projectData = projectData;

                if (projectData.IsEmpty) {
                    var emptyColor = ColorView.Resources.GetColor (Resource.Color.dark_gray_text);
                    ColorView.SetBackgroundColor (emptyColor);
                    ProjectTextView.SetTextColor (emptyColor);
                    ClientTextView.SetTextColor (emptyColor);

                    ProjectTextView.SetText (Resource.String.ProjectsNoProject);
                    ClientTextView.Visibility = ViewStates.Gone;
                    TasksButton.Visibility = ViewStates.Gone;
                    return;
                }

                var color = Color.ParseColor (ProjectModel.HexColors [projectData.Color % ProjectModel.HexColors.Length]);
                ColorView.SetBackgroundColor (color);
                ProjectTextView.SetTextColor (color);
                ClientTextView.SetTextColor (color);

                ProjectTextView.Text = projectData.Name;
                ClientTextView.Text = projectData.ClientName;
                ClientTextView.Visibility = showClient ? ViewStates.Visible : ViewStates.Gone;
                TasksButton.Visibility = projectData.TaskNumber > 0 ? ViewStates.Visible : ViewStates.Gone;
                TasksButton.Selected = false;
            }
        }

        protected class TaskItemHolder : RecyclerView.ViewHolder, View.IOnClickListener
        {
            private readonly ProjectListAdapter adapter;
            private readonly TextView taskTextView;
            private TaskData taskData;

            public TaskItemHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                taskTextView = root.FindViewById<TextView> (Resource.Id.TaskTextView).SetFont (Font.RobotoLight);
                root.SetOnClickListener (this);
            }

            public void Bind (TaskData data)
            {
                taskData = data;
                taskTextView.Text = data.Name;
            }

            public void OnClick (View v)
            {
                if (adapter.HandleItemSelection != null) {
                    adapter.HandleItemSelection.Invoke (taskData);
                }
            }
        }

        protected class ClientItemHolder : RecyclerView.ViewHolder
        {
            readonly TextView clientTextView;

            public ClientItemHolder (View root) : base (root)
            {
                clientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.Roboto);

            }

            public void Bind (string name)
            {
                if (string.IsNullOrEmpty (name)) {
                    clientTextView.SetText (Resource.String.ProjectsNoClient);
                } else {
                    clientTextView.Text = name;
                }
            }
        }

        protected class HeaderHolder : RecyclerView.ViewHolder
        {
            readonly TextView titleTextView;

            public HeaderHolder (View root) : base (root)
            {
                titleTextView = root.FindViewById<TextView> (Resource.Id.HeaderTextView).SetFont (Font.RobotoMedium);
            }

            public void Bind (string text)
            {
                if (String.IsNullOrEmpty (text)) {
                    titleTextView.SetText (Resource.String.ProjectsTop);
                } else {
                    titleTextView.Text = text;
                }
            }
        }
        #endregion
    }
}

