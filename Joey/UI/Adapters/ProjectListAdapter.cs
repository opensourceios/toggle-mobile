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

namespace Toggl.Joey.UI.Adapters
{
    public class ProjectListAdapter : RecyclerCollectionDataAdapter<CommonData>
    {
        protected const int ViewTypeProject = ViewTypeContent;
        protected const int ViewTypeClient = ViewTypeContent + 1;
        protected const int ViewTypeTask = ViewTypeContent + 2;
        protected const int ViewTypeUsedProject = ViewTypeContent + 3;
        protected const int ViewTypeHeading = ViewTypeContent + 4;

        protected ProjectsCollection collectionView;
        public Action<CommonData> HandleItemSelection { get; set; }

        public ProjectListAdapter (RecyclerView owner, ProjectsCollection collectionView) : base (owner, collectionView)
        {
            this.collectionView = collectionView;
        }

        protected override RecyclerView.ViewHolder GetViewHolder (ViewGroup parent, int viewType)
        {
            View view;
            RecyclerView.ViewHolder holder;
            var inflater = LayoutInflater.FromContext (ServiceContainer.Resolve<Context> ());

            switch (viewType) {
            case ViewTypeClient:
                view = inflater.Inflate (Resource.Layout.ProjectListClientItem, parent, false);
                holder = new ClientItemHolder (view);
                break;
            case ViewTypeTask:
                view = inflater.Inflate (Resource.Layout.ProjectListTaskItem, parent, false);
                holder = new TaskItemHolder (this, view);
                break;
            case ViewTypeUsedProject:
                view = inflater.Inflate (Resource.Layout.ProjectListUsedProjectItem, parent, false);
                holder = new ProjectItemHolder (this, view);
                break;
            case ViewTypeHeading:
                view = inflater.Inflate (Resource.Layout.ProjectListHeaderItem, parent, false);
                holder = new HeaderHolder (view);
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

            if (viewType == ViewTypeTask) {
                ((TaskItemHolder) holder).Bind ((TaskData)GetItem (position));
            } else if (viewType == ViewTypeClient) {
                ((ClientItemHolder) holder).Bind (((ClientData)GetItem (position)).Name);
            } else if (viewType == ViewTypeHeading) {
                ((HeaderHolder) holder).Bind (((ProjectsCollection.Heading)GetItem (position)).Text);
            } else if (viewType == ViewTypeProject) {
                var showClientName = collectionView.SortBy == ProjectsCollection.SortProjectsBy.Projects;
                ((ProjectItemHolder) holder).Bind ((ProjectsCollection.CommonProjectData)GetItem (position), showClientName);
            } else if (viewType == ViewTypeUsedProject) {
                var showClientName = collectionView.SortBy == ProjectsCollection.SortProjectsBy.Projects;
                ((ProjectItemHolder) holder).Bind ((ProjectsCollection.CommonProjectData)GetItem (position), showClientName);
            }
        }

        public override int GetItemViewType (int position) //TODO: refactor
        {
            var type = base.GetItemViewType (position);

            if (type == ViewTypeLoaderPlaceholder) {
                return type;
            }

            var dataObject = GetItem (position);

            if (dataObject is ProjectsCollection.Heading) {
                return ViewTypeHeading;
            }

            if (dataObject is ProjectsCollection.UsedProjectData) {
                return ViewTypeUsedProject;
            }

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

        #region View holders
        protected class ProjectItemHolder : RecyclerView.ViewHolder, View.IOnClickListener
        {
            protected View ColorView { get; private set; }
            protected TextView ProjectTextView { get; private set; }
            protected TextView ClientTextView { get; private set; }
            protected ImageButton TasksButton { get; private set; }
            protected ImageView TasksImageView { get; private set; }

            private ProjectListAdapter adapter;
            private ProjectsCollection.CommonProjectData projectData;

            public ProjectItemHolder (ProjectListAdapter adapter, View root) : base (root)
            {
                this.adapter = adapter;
                ColorView = root.FindViewById<View> (Resource.Id.ColorView);
                ProjectTextView = root.FindViewById<TextView> (Resource.Id.ProjectTextView).SetFont (Font.Roboto);
                ClientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.RobotoLight);
                TasksButton = root.FindViewById<ImageButton> (Resource.Id.TasksButton);
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

            public void Bind (ProjectsCollection.CommonProjectData projectData, bool showClient)
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
                clientTextView = root.FindViewById<TextView> (Resource.Id.ClientTextView).SetFont (Font.RobotoMedium);

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

