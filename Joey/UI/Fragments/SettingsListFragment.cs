using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Adapters;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Net;
using XPlatUtils;
using ListFragment = Android.Support.V4.App.ListFragment;

namespace Toggl.Joey.UI.Fragments
{
    // Need to use ordinary ListFragment here as PreferenceFragment isn't available in support library.
    public class SettingsListFragment : ListFragment
    {
        private Button DeleteDataButton;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SettingsListFragment, container, false);
            DeleteDataButton = view.FindViewById<Button> (Resource.Id.DeleteDataButton);
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            DeleteDataButton.Visibility = authManager.OfflineMode ? ViewStates.Visible : ViewStates.Gone;
            DeleteDataButton.Click += (object sender, System.EventArgs e) => ((MainDrawerActivity)Activity).ForgetCurrentUser ();
            return view;
        }

        public override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            base.OnViewCreated (view, savedInstanceState);

            ListView.SetClipToPadding (false);
            ListAdapter = new SettingsAdapter ();
        }

        public override void OnListItemClick (ListView l, View v, int position, long id)
        {
            ((SettingsAdapter)ListAdapter).OnItemClicked (position);
        }

        public override void OnStart ()
        {
            base.OnStart ();
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Settings";
        }
    }
}
