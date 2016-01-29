using System;
using System.IO;
using Android.Content;
using Android.OS;
using Toggl.Joey.UI.Fragments;
using Toggl.Phoebe;
using Toggl.Phoebe.Net;
using XPlatUtils;
using Activity = Android.Support.V7.App.AppCompatActivity;
using FragmentManager = Android.Support.V4.App.FragmentManager;

namespace Toggl.Joey.UI.Activities
{
    public abstract class BaseActivity : Activity
    {
        public static readonly string IntentProjectIdArgument = "project_id_param";
        public static readonly string IntentTaskIdArgument = "task_id_param";
        public static readonly string IntentWorkspaceIdArgument = "workspace_id_param";

        private const int SyncErrorMenuItemId = 0;
        protected readonly Handler Handler = new Handler ();
        private Subscription<SyncStartedMessage> subscriptionSyncStarted;
        private Subscription<SyncFinishedMessage> subscriptionSyncFinished;
        private Subscription<TogglHttpResponseMessage> subscriptionTogglHttpResponse;
        private int syncCount;

        /// <summary>
        /// The activity that is currently in the foreground.
        /// </summary>
        public static BaseActivity CurrentActivity { get; private set; }

        private void OnSyncStarted (SyncStartedMessage msg)
        {
            if (Handle == IntPtr.Zero) {
                return;
            }

            // Make sure we only show sync progress bar after 2.5 seconds from the start of the latest sync.
            var currentSync = ++syncCount;
            Handler.PostDelayed (delegate {
                if (currentSync == syncCount) {
                    ResetSyncProgressBar ();
                }
            }, 2500);
        }

        private void OnSyncFinished (SyncFinishedMessage msg)
        {
            if (Handle == IntPtr.Zero) {
                return;
            }
            ToggleProgressBar (false);
        }

        private void OnTogglHttpResponse (TogglHttpResponseMessage msg)
        {
            if (Handle == IntPtr.Zero) {
                return;
            }
            if (msg.StatusCode == System.Net.HttpStatusCode.Gone) {
                new ForcedUpgradeDialogFragment ().Show (FragmentManager, "upgrade_dialog");
            }
        }

        private void ResetSyncProgressBar ()
        {
            var syncManager = ServiceContainer.Resolve<ISyncManager> ();
            ToggleProgressBar (syncManager.IsRunning);
        }

        private void ToggleProgressBar (bool switchOn)
        {
            SetProgressBarIndeterminate (true);
            SetProgressBarVisibility (switchOn);
        }

        protected virtual bool StartAuthActivity ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (!authManager.IsAuthenticated) {
                var intent = new Intent (this, typeof (IntroActivity));
                intent.AddFlags (ActivityFlags.ClearTop);
                StartActivity (intent);
                Finish ();
                return true;
            }
            return false;
        }

        protected sealed override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            CurrentActivity = this;

            if (!StartAuthActivity ()) {
                OnCreateActivity (savedInstanceState);
            }
        }

        protected virtual void OnCreateActivity (Bundle state)
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSyncStarted = bus.Subscribe<SyncStartedMessage> (OnSyncStarted);
            subscriptionSyncFinished = bus.Subscribe<SyncFinishedMessage> (OnSyncFinished);
            subscriptionTogglHttpResponse = bus.Subscribe<TogglHttpResponseMessage> (OnTogglHttpResponse);

        }

        protected sealed override void OnResume ()
        {
            base.OnResume ();

            if (!StartAuthActivity ()) {
                OnResumeActivity ();
            }
        }

        protected virtual void OnResumeActivity ()
        {
            ResetSyncProgressBar ();

            // Make sure that the components are initialized (and that this initialisation wouldn't cause a lag)
            var app = (AndroidApp)Application;

            if (!app.ComponentsInitialized) {
                Handler.PostDelayed (delegate {
                    app.InitializeComponents ();
                }, 5000);
            }
            app.MarkLaunched ();
        }

        protected override void OnDestroy ()
        {
            base.OnDestroy ();

            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionSyncStarted != null) {
                bus.Unsubscribe (subscriptionSyncStarted);
                subscriptionSyncStarted = null;
            }
            if (subscriptionSyncFinished != null) {
                bus.Unsubscribe (subscriptionSyncFinished);
                subscriptionSyncFinished = null;
            }
            if (subscriptionTogglHttpResponse != null) {
                bus.Unsubscribe (subscriptionTogglHttpResponse);
                subscriptionTogglHttpResponse = null;
            }
        }

        public new FragmentManager FragmentManager
        {
            get { return SupportFragmentManager; }
        }

        public static Intent CreateDataIntent<TActivity, T> (Context context, T dataObject, string id)
        {
            var intent = new Intent (context, typeof (TActivity));

            // User json serializer for fast process?
            var serializer = new System.Xml.Serialization.XmlSerializer (typeof (T));
            using (var listStream = new MemoryStream ()) {
                serializer.Serialize (listStream, dataObject);
                intent.PutExtra (id, listStream.ToArray ());
            }
            return intent;
        }

        public static T GetDataFromIntent<T> (Intent intent, string id) where T : new ()
        {
            // Get the person object from the intent
            T dataObject;
            if (intent.HasExtra (id)) {
                var serializer = new System.Xml.Serialization.XmlSerializer (typeof (T));
                var byteArray = intent.GetByteArrayExtra (id);
                dataObject = (T)serializer.Deserialize (new MemoryStream (byteArray));
            } else {
                dataObject = new T ();
            }
            return dataObject;
        }
    }
}