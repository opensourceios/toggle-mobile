using System;
using System.Collections.Generic;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.UI.Adapters
{
    class DrawerListAdapter : BaseAdapter
    {
        protected static readonly int ViewTypeDrawerItem = 0;
        protected static readonly int ViewTypeDrawerSubItem = 1;
        public static readonly int TimerPageId = 0;
        public static readonly int ReportsPageId = 1;
        public static readonly int ReportsWeekPageId = 6;
        public static readonly int ReportsMonthPageId = 7;
        public static readonly int ReportsYearPageId = 8;
        public static readonly int SettingsPageId = 2;
        public static readonly int LogoutPageId = 3;
        public static readonly int FeedbackPageId = 4;
        public static readonly int RegisterUserPageId = 5;
        private List<DrawerItem> rowItems;
        private readonly List<DrawerItem> collapsedRowItems;
        private readonly AuthManager authManager;

        public DrawerListAdapter ()
        {
            authManager = ServiceContainer.Resolve<AuthManager> ();
            rowItems = new List<DrawerItem> () {
                new DrawerItem () {
                    Id = TimerPageId,
                    TextResId = Resource.String.MainDrawerTimer,
                    ImageResId = Resource.Drawable.IcNavTimer,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = ReportsPageId,
                    TextResId = Resource.String.MainDrawerReports,
                    ImageResId = Resource.Drawable.IcNavReports,
                    IsEnabled = true,
                    SubItems = new List<DrawerItem> () {
                        new DrawerItem () {
                            Id = ReportsWeekPageId,
                            ChildOf = ReportsPageId,
                            TextResId = Resource.String.MainDrawerReportsWeek,
                            ImageResId = 0,
                            IsEnabled = true,
                            VMode = VisibilityMode.Normal,
                        },
                        new DrawerItem () {
                            Id = ReportsMonthPageId,
                            ChildOf = ReportsPageId,
                            TextResId = Resource.String.MainDrawerReportsMonth,
                            ImageResId = 0,
                            IsEnabled = true,
                            VMode = VisibilityMode.Normal,
                        },
                        new DrawerItem () {
                            Id = ReportsYearPageId,
                            ChildOf = ReportsPageId,
                            TextResId = Resource.String.MainDrawerReportsYear,
                            ImageResId = 0,
                            IsEnabled = true,
                            VMode = VisibilityMode.Normal,
                        }
                    }
                },
                new DrawerItem () {
                    Id = SettingsPageId,
                    TextResId = Resource.String.MainDrawerSettings,
                    ImageResId = Resource.Drawable.IcNavSettings,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = FeedbackPageId,
                    TextResId = Resource.String.MainDrawerFeedback,
                    ImageResId = Resource.Drawable.IcNavFeedback,
                    IsEnabled = true,
                },
                new DrawerItem () {
                    Id = LogoutPageId,
                    TextResId = Resource.String.MainDrawerLogout,
                    ImageResId = Resource.Drawable.IcNavLogout,
                    IsEnabled = true,
                    VMode = VisibilityMode.Normal,
                },
                new DrawerItem () {
                    Id = RegisterUserPageId,
                    TextResId = Resource.String.MainDrawerSignup,
                    ImageResId = Resource.Drawable.IcNavLogout,
                    IsEnabled = true,
                    VMode = VisibilityMode.Offline,
                }
            };
            collapsedRowItems = rowItems;
            rowItems = FilterVisible (rowItems);
        }

        public override int ViewTypeCount
        {
            get { return 3; }
        }

        public int GetParentPosition (int pos)
        {
            if (rowItems [pos].ChildOf == 0) {
                return -1;
            }
            return GetItemPosition (rowItems [pos].ChildOf);
        }

        public override int GetItemViewType (int position)
        {
            if (rowItems [position].ChildOf > 0) {
                return ViewTypeDrawerSubItem;
            } else {
                return ViewTypeDrawerItem;
            }
        }

        private List<DrawerItem> FilterVisible (List<DrawerItem> list)
        {
            var newList = new List<DrawerItem> ();
            foreach (var item in list) {
                if (item.VMode == VisibilityMode.Normal && authManager.OfflineMode || item.VMode == VisibilityMode.Offline && !authManager.OfflineMode) {
                    continue;
                }
                newList.Add (item);
                if (item.SubItems != null) {
                    var subItems = new List<DrawerItem> ();
                    foreach (var sub in item.SubItems) {
                        if (sub.VMode == VisibilityMode.Normal && authManager.OfflineMode || sub.VMode == VisibilityMode.Offline && !authManager.OfflineMode) {
                            continue;
                        }
                        subItems.Add (sub);
                    }
                    item.SubItems = subItems.Count > 0 ? subItems : null;
                }
            }
            return newList;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView;

            if (GetItemViewType (position) == ViewTypeDrawerSubItem) {

                if (view == null) {
                    view = LayoutInflater.FromContext (parent.Context).Inflate (
                               Resource.Layout.MainDrawerSubListItem, parent, false);
                    view.Tag = new DrawerSubItemViewHolder (view);
                }

                var holder = (DrawerSubItemViewHolder)view.Tag;
                holder.Bind (GetDrawerItem (position));
            } else {

                if (view == null) {
                    view = LayoutInflater.FromContext (parent.Context).Inflate (
                               Resource.Layout.MainDrawerListItem, parent, false);
                    view.Tag = new DrawerItemViewHolder (view);
                }

                var holder = (DrawerItemViewHolder)view.Tag;
                holder.Bind (GetDrawerItem (position));
            }

            return view;
        }

        public override int Count
        {
            get { return rowItems.Count; }
        }

        public override Java.Lang.Object GetItem (int position)
        {
            return null;
        }

        public int GetItemPosition (int id)
        {
            var idx = rowItems.FindIndex (i => i.Id == id);
            return idx >= 0 ? idx + 1 : -1;
        }

        private DrawerItem GetDrawerItem (int position)
        {
            return rowItems [position];
        }

        public override long GetItemId (int position)
        {
            return GetDrawerItem (position).Id;
        }

        public void ExpandCollapse (int position)
        {
            rowItems = FilterVisible (collapsedRowItems);

            if (collapsedRowItems [position].SubItems == null) {
                return;
            }
            if (rowItems [position].SubItems.Count > 0) {
                var newList = new List<DrawerItem> ();
                int pos = 0;
                foreach (var row in rowItems) {
                    newList.Add (row);
                    if (pos == position) {
                        foreach (var sub in rowItems[position].SubItems) {
                            newList.Add (sub);
                        }
                    }
                    pos++;
                }
                rowItems = newList;
            }
        }

        public override bool IsEnabled (int position)
        {
            var item = GetDrawerItem (position);
            return item != null && item.IsEnabled;
        }

        private class DrawerItem
        {
            public int Id;
            public int TextResId;
            public int ImageResId;
            public int ChildOf = 0;
            public bool IsEnabled;
            public bool Expanded = false;
            public VisibilityMode VMode = VisibilityMode.Both;
            public List<DrawerItem> SubItems;
        }

        private class DrawerItemViewHolder : BindableViewHolder<DrawerItem>
        {
            public ImageView IconImageView { get; private set; }

            public TextView TitleTextView { get; private set; }

            public DrawerItemViewHolder (View root) : base (root)
            {
                IconImageView = root.FindViewById<ImageView> (Resource.Id.IconImageView);
                TitleTextView = root.FindViewById<TextView> (Resource.Id.TitleTextView).SetFont (Font.RobotoLight);
            }

            protected override void Rebind ()
            {
                if (DataSource == null) {
                    return;
                }

                IconImageView.SetImageResource (DataSource.ImageResId);
                TitleTextView.SetText (DataSource.TextResId);
                TitleTextView.Enabled = DataSource.IsEnabled;
            }
        }

        private class DrawerSubItemViewHolder : BindableViewHolder<DrawerItem>
        {
            public TextView TitleTextView { get; private set; }

            public DrawerSubItemViewHolder (View root) : base (root)
            {
                TitleTextView = root.FindViewById<TextView> (Resource.Id.TitleTextView).SetFont (Font.RobotoLight);
            }

            protected override void Rebind ()
            {
                if (DataSource == null) {
                    return;
                }
                TitleTextView.SetText (DataSource.TextResId);
                TitleTextView.Enabled = DataSource.IsEnabled;
            }
        }

        public enum VisibilityMode {
            Normal,
            Offline,
            Both
        }
    }
}
