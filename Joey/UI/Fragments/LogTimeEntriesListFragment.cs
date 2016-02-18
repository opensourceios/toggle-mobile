using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V7.Widget;
using Android.Support.V7.Widget.Helper;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using Toggl.Joey.Data;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Adapters;
using Toggl.Joey.UI.Components;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Data.ViewModels;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.UI.Fragments
{
    public class LogTimeEntriesListFragment : Fragment,
        SwipeDismissCallback.IDismissListener,
        ItemTouchListener.IItemTouchListener,
        SwipeRefreshLayout.IOnRefreshListener
    {
        public static bool NewTimeEntryStartedByFAB;

        private RecyclerView recyclerView;
        private SwipeRefreshLayout swipeLayout;
        private View emptyMessageView;
        private View experimentEmptyView;
        private View layoverView;
        private Button layoverDismissButton;
        private LogTimeEntriesAdapter logAdapter;
        private CoordinatorLayout coordinatorLayout;
        private Subscription<SyncFinishedMessage> drawerSyncFinished;
        private TimerComponent timerComponent;
        private TextView welcomeMessage;
        private TextView noItemsMessage;

        // Recycler setup
        private DividerItemDecoration dividerDecoration;
        private ShadowItemDecoration shadowDecoration;
        private ItemTouchListener itemTouchListener;

        // binding references
        private Binding<bool, bool> hasItemsBinding, newMenuBinding, hasMoreBinging, hasErrorBinding;
        private Binding<ObservableCollection<IHolder>, ObservableCollection<IHolder>> collectionBinding;
        private Binding<bool, FABButtonState> fabBinding;

        #region Binding objects and properties.

        public LogTimeEntriesViewModel ViewModel { get; private set;}
        public IMenuItem AddNewMenuItem { get; private set; }
        public StartStopFab StartStopBtn { get; private set;}

        #endregion

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.LogTimeEntriesListFragment, container, false);
            view.FindViewById<TextView> (Resource.Id.EmptyTextTextView).SetFont (Font.RobotoLight);

            experimentEmptyView = view.FindViewById<View> (Resource.Id.ExperimentEmptyMessageView);
            emptyMessageView = view.FindViewById<View> (Resource.Id.EmptyMessageView);
            welcomeMessage = view.FindViewById<TextView> (Resource.Id.WelcomeTextView);
            noItemsMessage = view.FindViewById<TextView> (Resource.Id.EmptyTitleTextView);
            layoverView = view.FindViewById<View> (Resource.Id.LayoverView);
            layoverDismissButton = view.FindViewById<Button> (Resource.Id.LayoverButton);
            layoverDismissButton.Click += OnAllrightButtonClicked;
            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.LogRecyclerView);
            recyclerView.SetLayoutManager (new LinearLayoutManager (Activity));
            swipeLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.LogSwipeContainer);
            swipeLayout.SetOnRefreshListener (this);
            coordinatorLayout = view.FindViewById<CoordinatorLayout> (Resource.Id.logCoordinatorLayout);
            StartStopBtn = view.FindViewById<StartStopFab> (Resource.Id.StartStopBtn);
            timerComponent = ((MainDrawerActivity)Activity).Timer; // TODO: a better way to do this?
            HasOptionsMenu = true;

            var settingsStore = ServiceContainer.Resolve<SettingsStore> ();
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (settingsStore.GotWelcomeMessage || !authManager.OfflineMode) {
                layoverView.Visibility = ViewStates.Gone;
            }

            return view;
        }

        private void OnAllrightButtonClicked (object sender, EventArgs e)
        {
            var settingsStore = ServiceContainer.Resolve<SettingsStore> ();
            settingsStore.GotWelcomeMessage = true;
            layoverView.Visibility = ViewStates.Gone;
        }

        public async override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            // init viewModel
            ViewModel = LogTimeEntriesViewModel.Init ();

            collectionBinding = this.SetBinding (() => ViewModel.Collection).WhenSourceChanges (() => {
                logAdapter = new LogTimeEntriesAdapter (recyclerView, ViewModel);
                recyclerView.SetAdapter (logAdapter);
            });
            hasMoreBinging = this.SetBinding (()=> ViewModel.HasMoreItems).WhenSourceChanges (SetFooterState);
            hasErrorBinding = this.SetBinding (()=> ViewModel.HasLoadErrors).WhenSourceChanges (SetFooterState);
            hasItemsBinding = this.SetBinding (()=> ViewModel.HasItems).WhenSourceChanges (SetFooterState);
            
            fabBinding = this.SetBinding (() => ViewModel.IsTimeEntryRunning, () => StartStopBtn.ButtonAction)
                         .ConvertSourceToTarget (isRunning => isRunning ? FABButtonState.Stop : FABButtonState.Start);

            newMenuBinding = this.SetBinding (() => ViewModel.IsTimeEntryRunning)
            .WhenSourceChanges (() => {
                if (AddNewMenuItem != null) {
                    AddNewMenuItem.SetVisible (!ViewModel.IsTimeEntryRunning);
                }
            });

            // Pass ViewModel to TimerComponent.
            timerComponent.SetViewModel (ViewModel);
            StartStopBtn.Click += StartStopClick;
            SetupRecyclerView (ViewModel);

            // TODO: Review this line.
            // Get data to fill the list. For the moment,
            // until a screenloader is added to the screen
            // is better to load the items after create
            // the viewModel and show the loader from RecyclerView
            await ViewModel.LoadMore ();

            // Subscribe to sync messages
            var bus = ServiceContainer.Resolve<MessageBus> ();
            drawerSyncFinished = bus.Subscribe<SyncFinishedMessage> (SyncFinished);
        }


        public async void StartStopClick (object sender, EventArgs e)
        {
            // Send experiment data.
            ViewModel.ReportExperiment (OBMExperimentManager.AndroidExperimentNumber,
                                        OBMExperimentManager.StartButtonActionKey,
                                        OBMExperimentManager.ClickActionValue);

            var timeEntryData = await ViewModel.StartStopTimeEntry ();
            if (timeEntryData.State == TimeEntryState.Running) {
                NewTimeEntryStartedByFAB = true;
                var ids = new List<string> { timeEntryData.Id.ToString () };
                var intent = new Intent (Activity, typeof (EditTimeEntryActivity));
                intent.PutStringArrayListExtra (EditTimeEntryActivity.ExtraGroupedTimeEntriesGuids, ids);
                intent.PutExtra (EditTimeEntryActivity.IsGrouped,  false);
                StartActivity (intent);
            }
        }

        public override void OnDestroyView ()
        {
            // Protect against Java side being GCed
            if (Handle == IntPtr.Zero) {
                return;
            }

            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (drawerSyncFinished != null) {
                bus.Unsubscribe (drawerSyncFinished);
                drawerSyncFinished = null;
            }

            // TODO: Remove bindings to ViewModel
            // check if it is needed or not.
            timerComponent.DetachBindind ();
            ReleaseRecyclerView ();
            ViewModel.Dispose ();
            base.OnDestroyView ();
        }

        private void SetFooterState ()
        {
            if (ViewModel.HasMoreItems && !ViewModel.HasLoadErrors) {
                logAdapter.SetFooterState (RecyclerCollectionDataAdapter<IHolder>.RecyclerLoadState.Loading);
            } else if (ViewModel.HasMoreItems && ViewModel.HasLoadErrors) {
                logAdapter.SetFooterState (RecyclerCollectionDataAdapter<IHolder>.RecyclerLoadState.Retry);
            } else if (!ViewModel.HasMoreItems && !ViewModel.HasLoadErrors) {
                if (ViewModel.HasItems) {
                    logAdapter.SetFooterState (RecyclerCollectionDataAdapter<IHolder>.RecyclerLoadState.Finished);
                } else {
                    View emptyView = emptyMessageView;
                    // According to settings, show welcome message or no.
                    welcomeMessage.Visibility = ServiceContainer.Resolve<ISettingsStore> ().ShowWelcome ? ViewStates.Visible : ViewStates.Gone;
                    noItemsMessage.Visibility = ServiceContainer.Resolve<ISettingsStore> ().ShowWelcome ? ViewStates.Gone : ViewStates.Visible;
                    if (OBMExperimentManager.IncludedInExperiment (OBMExperimentManager.AndroidExperimentNumber)) {
                        emptyView = experimentEmptyView;
                    }
                    emptyView.Visibility = ViewModel.HasItems ? ViewStates.Gone : ViewStates.Visible;
                }
            }
            recyclerView.Visibility = ViewModel.HasItems ? ViewStates.Visible : ViewStates.Gone;
        }

        #region Menu setup
        public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
        {
            inflater.Inflate (Resource.Menu.NewItemMenu, menu);
            AddNewMenuItem = menu.FindItem (Resource.Id.newItem);
            ConfigureOptionMenu ();
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            var i = new Intent (Activity, typeof (EditTimeEntryActivity));
            i.PutStringArrayListExtra (EditTimeEntryActivity.ExtraGroupedTimeEntriesGuids, new List<string> { ViewModel.GetActiveTimeEntry ().Id.ToString ()});
            Activity.StartActivity (i);

            return base.OnOptionsItemSelected (item);
        }

        // Because the viewModel needs time to be created,
        // this method is called from two points
        private void ConfigureOptionMenu ()
        {
            if (ViewModel != null && AddNewMenuItem != null) {
                AddNewMenuItem.SetVisible (!ViewModel.IsTimeEntryRunning);
            }
        }
        #endregion

        #region IDismissListener implementation
        public bool CanDismiss (RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
        {
            var adapter = recyclerView.GetAdapter ();
            return adapter.GetItemViewType (viewHolder.LayoutPosition) == RecyclerCollectionDataAdapter<IHolder>.ViewTypeContent;
        }
        #endregion

        #region IRecyclerViewOnItemClickListener implementation
        public void OnItemClick (RecyclerView parent, View clickedView, int position)
        {
            var undoAdapter = (IUndoAdapter)parent.GetAdapter ();
            if (undoAdapter.IsUndo (position)) {
                return;
            }

            var intent = new Intent (Activity, typeof (EditTimeEntryActivity));
            IList<string> guids = ((ITimeEntryHolder)ViewModel.Collection.ElementAt (position)).Guids;
            intent.PutStringArrayListExtra (EditTimeEntryActivity.ExtraGroupedTimeEntriesGuids, guids);
            intent.PutExtra (EditTimeEntryActivity.IsGrouped, guids.Count > 1);

            StartActivity (intent);
        }

        public void OnItemLongClick (RecyclerView parent, View clickedView, int position)
        {
            OnItemClick (parent, clickedView, position);
        }

        public bool CanClick (RecyclerView view, int position)
        {
            var adapter = recyclerView.GetAdapter ();
            return adapter.GetItemViewType (position) == RecyclerCollectionDataAdapter<IHolder>.ViewTypeContent;
        }
        #endregion

        #region Sync
        public void OnRefresh ()
        {
            if (!ServiceContainer.Resolve<AuthManager> ().OfflineMode) {
                ViewModel.TriggerFullSync ();
            } else {
                swipeLayout.Refreshing = false;
            }

        }

        private void SyncFinished (SyncFinishedMessage msg)
        {
            if (!swipeLayout.Refreshing) {
                return;
            }

            swipeLayout.Refreshing = false;

            if (msg.HadErrors) {
                int msgId = Resource.String.LastSyncHadErrors;

                if (msg.FatalError.IsNetworkFailure ()) {
                    msgId = Resource.String.LastSyncNoConnection;
                } else if (msg.FatalError is TaskCanceledException) {
                    msgId = Resource.String.LastSyncFatalError;
                }
                Snackbar.Make (coordinatorLayout, Resources.GetString (msgId), Snackbar.LengthLong).Show ();
            }
        }
        #endregion

        private void SetupRecyclerView (LogTimeEntriesViewModel viewModel)
        {
            // Touch listeners.
            itemTouchListener = new ItemTouchListener (recyclerView, this);
            recyclerView.AddOnItemTouchListener (itemTouchListener);

            // Scroll listener
            recyclerView.AddOnScrollListener (
                new ScrollListener ((LinearLayoutManager)recyclerView.GetLayoutManager (), viewModel));

            var touchCallback = new SwipeDismissCallback (ItemTouchHelper.Up | ItemTouchHelper.Down, ItemTouchHelper.Left, this);
            var touchHelper = new ItemTouchHelper (touchCallback);
            touchHelper.AttachToRecyclerView (recyclerView);

            // Decorations.
            dividerDecoration = new DividerItemDecoration (Activity, DividerItemDecoration.VerticalList);
            shadowDecoration = new ShadowItemDecoration (Activity);
            recyclerView.AddItemDecoration (dividerDecoration);
            recyclerView.AddItemDecoration (shadowDecoration);
            recyclerView.GetItemAnimator ().ChangeDuration = 0;
        }

        private void ReleaseRecyclerView ()
        {
            recyclerView.RemoveItemDecoration (shadowDecoration);
            recyclerView.RemoveItemDecoration (dividerDecoration);
            recyclerView.RemoveOnItemTouchListener (itemTouchListener);

            recyclerView.GetAdapter ().Dispose ();
            recyclerView.Dispose ();

            itemTouchListener.Dispose ();
            dividerDecoration.Dispose ();
            shadowDecoration.Dispose ();
        }

        class ScrollListener : RecyclerView.OnScrollListener
        {
            private const int visibleThreshold = 5; // The minimum amount of items to have below your current scroll position before loading more.
            private int previousTotal; // The total number of items in the dataset after the last load
            private bool loading = true; // True if we are still waiting for the last set of data to load.
            private int firstVisibleItem, visibleItemCount, totalItemCount;
            private readonly LinearLayoutManager linearLayoutManager;
            private readonly LogTimeEntriesViewModel viewModel;

            public ScrollListener (LinearLayoutManager linearLayoutManager, LogTimeEntriesViewModel viewModel)
            {
                this.linearLayoutManager = linearLayoutManager;
                this.viewModel = viewModel;
            }

            public async override void OnScrolled (RecyclerView recyclerView, int dx, int dy)
            {
                base.OnScrolled (recyclerView, dx, dy);

                visibleItemCount = recyclerView.ChildCount;
                totalItemCount = linearLayoutManager.ItemCount;
                firstVisibleItem = linearLayoutManager.FindFirstVisibleItemPosition ();

                if (loading) {
                    if (totalItemCount > previousTotal) {
                        loading = false;
                        previousTotal = totalItemCount;
                    }
                }

                if (!loading && (totalItemCount - visibleItemCount) <= (firstVisibleItem + visibleThreshold)) {
                    loading = true;
                    // Request more entries.
                    await viewModel.LoadMore ();
                }
            }

            public override void OnScrollStateChanged (RecyclerView recyclerView, int newState)
            {
                base.OnScrollStateChanged (recyclerView, newState);
                if (newState == 1) {
                    var adapter = (IUndoAdapter) recyclerView.GetAdapter ();
                    adapter.SetItemsToNormalPosition ();
                }
            }
        }
    }
}
