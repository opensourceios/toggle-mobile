using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Animation;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.Data;
using Toggl.Joey.UI.Fragments;
using Toggl.Joey.UI.Utils;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Reports;
using Toggl.Phoebe.Net;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;
using FragmentPagerAdapter = Android.Support.V4.App.FragmentPagerAdapter;
using FragmentTransaction = Android.Support.V4.App.FragmentTransaction;
using ViewPager = Android.Support.V4.View.ViewPager;

namespace Toggl.Joey.UI.Fragments
{
    public class ReportsPagerFragment : Fragment
    {
        private const string ExtraCurrentItem = "com.toggl.timer.current_item";
        private const int PagesCount = 500;
        private const int StartPage = PagesCount - 2;

        private ViewPager viewPager;
        private View previousPeriod;
        private View nextPeriod;
        private TextView timePeriod;
        private ZoomLevel zoomLevel = ZoomLevel.Week;
        private int backDate;
        private Context ctx;
        private Pool<View> projectListItemPool;
        private Pool<ReportsFragment.Controller> reportsControllerPool;
        private FrameLayout syncErrorBar;
        private ImageButton syncRetry;
        private Animator currentAnimation;
        private LinearLayout reportsContainer;
        private LinearLayout nouserDisclaimer;

        public ZoomLevel ZoomLevel
        {
            get {
                return zoomLevel;
            } set {
                if (value == zoomLevel) {
                    return;
                }
                zoomLevel = value;
                ResetAdapter ();
                UpdatePeriod ();
                SummaryReportView.SaveReportsState ( zoomLevel);
                if (IsResumed) {
                    TrackScreenView ();
                }
            }
        }

        public Pool<View> ProjectListItems
        {
            get { return projectListItemPool; }
        }

        public Pool<ReportsFragment.Controller> ReportsControllers
        {
            get { return reportsControllerPool; }
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            ctx = Activity;
            projectListItemPool = new Pool<View> (CreateProjectListItem) {
                Count = 3 /*controller count*/ * 7 /*list items per controller*/,
            };
            reportsControllerPool = new Pool<ReportsFragment.Controller> (CreateController, ResetController) {
                Count = 3,
            };
            zoomLevel = SummaryReportView.GetLastZoomViewed ();
        }

        private ReportsFragment.Controller CreateController()
        {
            return new ReportsFragment.Controller (ctx, projectListItemPool);
        }

        private void ResetController (ReportsFragment.Controller inst)
        {
            // Remove from parent
            var parent = inst.View.Parent as ViewGroup;
            if (parent != null) {
                parent.RemoveView (inst.View);
            }

            // Reset data
            inst.Data = null;
            inst.SnapPosition = 0;
        }

        private View CreateProjectListItem()
        {
            var view = LayoutInflater.From (ctx).Inflate (Resource.Layout.ReportsProjectListItem, null, false);
            view.Tag = new ReportsFragment.ProjectListItemHolder (view);
            return view;
        }

        public ReportsPagerFragment ()
        {
            zoomLevel = SummaryReportView.GetLastZoomViewed ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ReportsPagerFragment, container, false);
            viewPager = view.FindViewById<ViewPager> (Resource.Id.ReportsViewPager);
            viewPager.PageSelected += OnPageSelected;

            timePeriod = view.FindViewById<TextView> (Resource.Id.TimePeriodLabel);
            previousPeriod = view.FindViewById (Resource.Id.PreviousFrameLayout);
            nextPeriod = view.FindViewById (Resource.Id.NextFrameLayout);
            syncErrorBar = view.FindViewById<FrameLayout> (Resource.Id.ReportsSyncBar);
            syncRetry = view.FindViewById<ImageButton> (Resource.Id.ReportsSyncRetryButton);
            reportsContainer = view.FindViewById<LinearLayout> (Resource.Id.ReportsContainer);
            nouserDisclaimer = view.FindViewById<LinearLayout> (Resource.Id.NoUserDisclaimer);

            previousPeriod.Click += (sender, e) => NavigatePage (-1);
            nextPeriod.Click += (sender, e) => NavigatePage (1);
            syncRetry.Click += async (sender, e) => await ReloadCurrent ();

            ResetAdapter ();
            UpdatePeriod ();

            var settings = ServiceContainer.Resolve<SettingsStore> ();
            if (settings.ReportsCurrentItem.HasValue) {
                viewPager.CurrentItem = settings.ReportsCurrentItem.Value;
            }

            if (ServiceContainer.Resolve<AuthManager> ().OfflineMode) {
                nouserDisclaimer.Visibility = ViewStates.Visible;
                reportsContainer.Visibility = ViewStates.Gone;
            }

            return view;
        }

        public override void OnDestroyView ()
        {
            viewPager.PageSelected -= OnPageSelected;
            var adapter = (MainPagerAdapter)viewPager.Adapter;
            adapter.LoadReady -= OnLoadReady;
            base.OnDestroyView ();
        }

        public override void OnPause()
        {
            var settings = ServiceContainer.Resolve<SettingsStore> ();
            settings.ReportsCurrentItem = viewPager.CurrentItem;
            base.OnPause();
        }

        public override void OnStart ()
        {
            base.OnStart ();

            TrackScreenView ();
        }

        private void TrackScreenView()
        {
            var screen = "Reports";
            switch (ZoomLevel) {
            case ZoomLevel.Week:
                screen = "Reports (Week)";
                break;
            case ZoomLevel.Month:
                screen = "Reports (Month)";
                break;
            case ZoomLevel.Year:
                screen = "Reports (Year)";
                break;
            }

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = screen;
        }

        public void NavigatePage (int direction)
        {
            var newItem = viewPager.CurrentItem + direction;
            newItem = Math.Max (0, Math.Min (newItem, PagesCount - 1));

            if (newItem != viewPager.CurrentItem) {
                viewPager.SetCurrentItem (newItem, true);
                backDate = newItem - StartPage;
                UpdatePeriod ();
            }
        }

        private void ResetAdapter()
        {
            var adapter = new MainPagerAdapter (ChildFragmentManager, zoomLevel);
            viewPager.Adapter = adapter;
            adapter.LoadReady += OnLoadReady;
            viewPager.CurrentItem = StartPage;
            backDate = 0;
        }

        private async Task ReloadCurrent()
        {
            var adapter = (MainPagerAdapter)viewPager.Adapter;
            var frag = (ReportsFragment)adapter.GetItem (viewPager.CurrentItem);
            await frag.ReloadData ();
        }

        private void UpdatePeriod ()
        {
            timePeriod.Text = FormattedDateSelector ();
        }

        private void OnLoadReady (object sender, ReportsFragment.LoadReadyEventArgs e)
        {
            ShowSyncError (e.IsError);
        }

        private async void OnPageSelected (object sender, ViewPager.PageSelectedEventArgs e)
        {
            var adapter = (MainPagerAdapter)viewPager.Adapter;
            var frag = (ReportsFragment)adapter.GetItem (e.Position);
            if (frag.IsError) {
                await frag.ReloadData();
            }
            frag.UserVisibleHint = true;
            backDate = e.Position - StartPage;
            UpdatePeriod ();
        }

        private void ShowSyncError (bool visible)
        {
            if (currentAnimation != null) {
                currentAnimation.Cancel();
                currentAnimation = null;
            }

            if (visible && syncErrorBar.Visibility == ViewStates.Gone) {
                var slideIn = ObjectAnimator.OfFloat (syncErrorBar, "translationY", 100f, 0f).SetDuration (500);
                slideIn.AnimationStart += delegate {
                    syncErrorBar.Visibility = ViewStates.Visible;
                };
                currentAnimation = slideIn;
                currentAnimation.Start ();
            } else if (!visible && syncErrorBar.Visibility == ViewStates.Visible) {
                var slideOut = ObjectAnimator.OfFloat (syncErrorBar, "translationY", syncErrorBar.TranslationY, 100f).SetDuration (500);
                slideOut.AnimationEnd += delegate {
                    syncErrorBar.Visibility = ViewStates.Gone;
                };
                currentAnimation = slideOut;
                currentAnimation.Start ();
            }
        }

        private string FormattedDateSelector ()
        {
            if (backDate == 0) {
                if (ZoomLevel == ZoomLevel.Week) {
                    return Resources.GetString (Resource.String.ReportsThisWeek);
                } else if (ZoomLevel == ZoomLevel.Month) {
                    return Resources.GetString (Resource.String.ReportsThisMonth);
                } else {
                    return Resources.GetString (Resource.String.ReportsThisYear);
                }
            } else if (backDate == -1) {
                if (ZoomLevel == ZoomLevel.Week) {
                    return Resources.GetString (Resource.String.ReportsLastWeek);
                } else if (ZoomLevel == ZoomLevel.Month) {
                    return Resources.GetString (Resource.String.ReportsLastMonth);
                } else {
                    return Resources.GetString (Resource.String.ReportsLastYear);
                }
            } else {
                var startDate = ResolveStartDate (backDate);
                if (ZoomLevel == ZoomLevel.Week) {
                    var endDate = ResolveEndDate (startDate);
                    return String.Format ("{0:MMM dd}th - {1:MMM dd}th", startDate, endDate);
                } else if (ZoomLevel == ZoomLevel.Month) {
                    return String.Format ("{0:M}", startDate);
                }
                return startDate.Year.ToString ();
            }
        }

        private DateTime ResolveStartDate (int back)
        {
            var current = DateTime.Today;

            if (ZoomLevel == ZoomLevel.Week) {
                var user = ServiceContainer.Resolve<AuthManager> ().User;
                var startOfWeek = user.StartOfWeek;
                var date = current.StartOfWeek (startOfWeek).AddDays (back * 7);
                return date;
            }

            if (ZoomLevel == ZoomLevel.Month) {
                current = current.AddMonths (back);
                return new DateTime (current.Year, current.Month, 1);
            }

            return new DateTime (current.Year + back, 1, 1);
        }

        private DateTime ResolveEndDate (DateTime start)
        {
            if (ZoomLevel == ZoomLevel.Week) {
                return start.AddDays (6);
            }

            if (ZoomLevel == ZoomLevel.Month) {
                return start.AddMonths (1).AddDays (-1);
            }

            return start.AddYears (1).AddDays (-1);
        }

        private class MainPagerAdapter : FragmentPagerAdapter
        {
            private readonly List<ReportsFragment> currentFragments = new List<ReportsFragment>();
            private readonly ZoomLevel zoomLevel;
            private int snapPosition;
            public event EventHandler<ReportsFragment.LoadReadyEventArgs> LoadReady;

            public MainPagerAdapter (FragmentManager fragmentManager, ZoomLevel zoomLevel) : base (fragmentManager)
            {
                this.zoomLevel = zoomLevel;
            }

            public override int Count
            {
                get { return PagesCount; }
            }

            public override Java.Lang.Object InstantiateItem (ViewGroup container, int position)
            {
                var frag = (ReportsFragment)base.InstantiateItem (container, position);
                frag.Position = snapPosition;
                frag.PositionChanged += ChangeReportsPosition;
                frag.LoadReady += ShowSyncError;
                currentFragments.Add (frag);
                return frag;
            }

            public override void DestroyItem (ViewGroup container, int position, Java.Lang.Object @objectValue)
            {
                var frag = (ReportsFragment)@objectValue;
                frag.PositionChanged -= ChangeReportsPosition;
                frag.LoadReady -= ShowSyncError;
                currentFragments.Remove (frag);
                base.DestroyItem (container, position, frag);
            }

            public override long GetItemId (int position)
            {
                // The item Id needs to be dependent on zoom level. Otherwise the Android fragment system will
                // try to restore old fragment data (Arguments) to new ones when switching zoom level.
                return PagesCount * (long)zoomLevel + position;
            }

            public override Fragment GetItem (int position)
            {
                var period = position - StartPage;

                // TODO: when the adapter is define the first time
                // the position used is 0 and 1. This position generate
                // a wrong date calculation.
                // A solution could be don't reset but rehuse the
                // FragmentPagerAdapter.
                return currentFragments.Find (frag => frag.Period == period)
                       ?? new ReportsFragment (period, zoomLevel);
            }

            private void ShowSyncError (object sender, ReportsFragment.LoadReadyEventArgs args)
            {
                if (LoadReady != null) {
                    LoadReady (this, args);
                }
            }

            private void ChangeReportsPosition (object sender, EventArgs args )
            {
                var pos = ((ReportsFragment)sender).Position;
                if (snapPosition == pos) {
                    return;
                }

                snapPosition = pos;
                foreach (var frag in currentFragments) {
                    frag.Position = pos;
                }
            }
        }
    }
}
