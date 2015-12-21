using System;
using System.Linq.Expressions;
using Android.Content;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using XPlatUtils;

namespace Toggl.Joey.Data
{
    /// <summary>
    /// Settings store for storing Android runtime settings. It also stores settings for Phoebe, but the
    /// <see cref="ISettingsStore"/> interface is implemented explicitly.
    /// </summary>
    public class SettingsStore : ISettingsStore
    {
        private const string PreferenceName = "togglSettings";
        private const string PhoebeUserIdKey = "phoebeUserId";
        private const string PhoebeOfflineModeKey = "phoebeOfflineMode";
        private const string PhoebeApiTokenKey = "phoebeApiToken";
        private const string PhoebeSyncLastRunKey = "phoebeSyncLastRun";
        private const string PhoebeUseDefaultTagKey = "phoebeUseDefaultTag";
        private const string PhoebeLastAppVersionKey = "phoebeLastAppVersion";
        private const string PhoebeExperimentIdKey = "phoebeExperimentId";
        private const string PhoebeLastReportZoomKey = "lastReportZoomKey";
        private const string PhoebeGroupedEntriesKey = "groupedEntriesKey";
        private const string JoeyInstallIdKey = "joeyInstallId";
        private const string JoeyGcmRegistrationIdKey = "joeyGcmRegistrationId";
        private const string JoeyGcmAppVersionKey = "joeyGcmAppVersion";
        private const string IdleNotificationKey = "idleNotification";
        private const string ChooseProjectForNewKey = "chooseProjectForNewKey";
        private const string ReportsCurrentItemKey = "reportsCurrentItem";
        private const string JoeyShowNotificationKey = "disableNotificationKey";
        private const string PhoebeProjectSortKey = "projectSortKey";
        private const string PhoebeIsStagingKey = "isStagingKey";
        private const string PhoebeShowWelcomeKey = "showWelcomeKey";

        private static string GetPropertyName<T> (Expression<Func<SettingsStore, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        private readonly ISharedPreferences prefs;

        public SettingsStore (Context ctx)
        {
            prefs = ctx.GetSharedPreferences (PreferenceName, FileCreationMode.Private);
        }

        protected Guid? GetGuid (string key)
        {
            var val = prefs.GetString (key, null);
            if (String.IsNullOrEmpty (val)) {
                return null;
            }
            return Guid.Parse (val);
        }

        protected void SetGuid (string key, Guid? value)
        {
            if (value != null) {
                prefs.Edit ().PutString (key, value.Value.ToString ()).Commit ();
            } else {
                prefs.Edit ().Remove (key).Commit ();
            }
        }

        protected string GetString (string key)
        {
            return prefs.GetString (key, null);
        }

        protected void SetString (string key, string value)
        {
            if (value != null) {
                prefs.Edit ().PutString (key, value).Commit ();
            } else {
                prefs.Edit ().Remove (key).Commit ();
            }
        }

        protected int? GetInt (string key)
        {
            if (!prefs.Contains (key)) {
                return null;
            } else {
                return prefs.GetInt (key, 0);
            }
        }

        protected void SetInt (string key, int? value)
        {
            if (value != null) {
                prefs.Edit ().PutInt (key, value.Value).Commit ();
            } else {
                prefs.Edit ().Remove (key).Commit ();
            }
        }

        protected DateTime? GetDateTime (string key)
        {
            if (!prefs.Contains (key)) {
                return null;
            } else {
                return DateTime.FromBinary (prefs.GetLong (key, 0)).ToUtc ();
            }
        }

        protected void SetDateTime (string key, DateTime? value)
        {
            if (value != null) {
                prefs.Edit ().PutLong (key, value.Value.ToBinary ()).Commit ();
            } else {
                prefs.Edit ().Remove (key).Commit ();
            }
        }

        protected void OnSettingChanged (string name)
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            bus.Send (new SettingChangedMessage (this, name));
        }

        public string InstallId
        {
            get {
                var val = GetString (JoeyInstallIdKey);
                if (String.IsNullOrEmpty (val)) {
                    val = Guid.NewGuid ().ToString ();
                    SetString (JoeyInstallIdKey, val);
                }
                return val;
            }
        }

        public static readonly string PropertyGcmRegistrationId = GetPropertyName (s => s.GcmRegistrationId);

        public string GcmRegistrationId
        {
            get { return GetString (JoeyGcmRegistrationIdKey); }
            set {
                SetString (JoeyGcmRegistrationIdKey, value);
                OnSettingChanged (PropertyGcmRegistrationId);
            }
        }

        public static readonly string PropertyGcmAppVersion = GetPropertyName (s => s.GcmAppVersion);

        public int? GcmAppVersion
        {
            get { return GetInt (JoeyGcmAppVersionKey); }
            set {
                SetInt (JoeyGcmAppVersionKey, value);
                OnSettingChanged (PropertyGcmAppVersion);
            }
        }

        public static readonly string PropertyUserId = GetPropertyName (s => s.UserId);

        public Guid? UserId
        {
            get { return GetGuid (PhoebeUserIdKey); }
            set {
                SetGuid (PhoebeUserIdKey, value);
                OnSettingChanged (PropertyUserId);
            }
        }

        public static readonly string PropertyOfflineMode = GetPropertyName (s => s.OfflineMode);

        public bool OfflineMode
        {
            get { return GetInt (PhoebeOfflineModeKey) == 1; }
            set {
                SetInt (PhoebeOfflineModeKey, value ? 1 : 0);
                OnSettingChanged (PropertyOfflineMode);
            }
        }

        public static readonly string PropertyApiToken = GetPropertyName (s => s.ApiToken);

        public string ApiToken
        {
            get { return GetString (PhoebeApiTokenKey); }
            set {
                SetString (PhoebeApiTokenKey, value);
                OnSettingChanged (PropertyApiToken);
            }
        }

        public static readonly string PropertySyncLastRun = GetPropertyName (s => s.SyncLastRun);

        public DateTime? SyncLastRun
        {
            get { return GetDateTime (PhoebeSyncLastRunKey); }
            set {
                SetDateTime (PhoebeSyncLastRunKey, value);
                OnSettingChanged (PropertySyncLastRun);
            }
        }

        public static readonly string PropertyExperimentId = GetPropertyName (s => s.ExperimentId);

        public string ExperimentId
        {
            get { return GetString (PhoebeExperimentIdKey); }
            set {
                SetString (PhoebeExperimentIdKey, value);
                OnSettingChanged (PropertyExperimentId);
            }
        }

        public static readonly string PropertyIdleNotification = GetPropertyName (s => s.IdleNotification);

        public bool IdleNotification
        {
            get { return GetInt (IdleNotificationKey) == 1; }
            set {
                SetInt (IdleNotificationKey, value ? 1 : 0);
                OnSettingChanged (PropertyIdleNotification);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.IdleNotification);
            }
        }

        public static readonly string PropertyChooseProjectForNew = GetPropertyName (s => s.ChooseProjectForNew);

        public bool ChooseProjectForNew
        {
            get { return GetInt (ChooseProjectForNewKey) == 1; }
            set {
                SetInt (ChooseProjectForNewKey, value ? 1 : 0);
                OnSettingChanged (PropertyChooseProjectForNew);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.AskForProject);
            }
        }

        public static readonly string PropertyUseDefaultTag = GetPropertyName (s => s.UseDefaultTag);

        public bool UseDefaultTag
        {
            get { return (GetInt (PhoebeUseDefaultTagKey) ?? 1) == 1; }
            set {
                SetInt (PhoebeUseDefaultTagKey, value ? 1 : 0);
                OnSettingChanged (PropertyUseDefaultTag);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.DefaultMobileTag);
            }
        }

        public static readonly string PropertyLastAppVersion = GetPropertyName (s => s.LastAppVersion);

        public string LastAppVersion
        {
            get { return GetString (PhoebeLastAppVersionKey); }
            set { SetString (PhoebeLastAppVersionKey, value); }
        }

        public static readonly string PropertyLastReportZoomViewed = GetPropertyName (s => s.LastReportZoomViewed);

        public int? LastReportZoomViewed
        {
            get { return GetInt (PhoebeLastReportZoomKey); }
            set {
                SetInt (PhoebeLastReportZoomKey, value);
                OnSettingChanged (PropertyLastReportZoomViewed);
            }
        }

        public static readonly string PropertyReportsCurrentItem = GetPropertyName (s => s.ReportsCurrentItem);

        public int? ReportsCurrentItem
        {
            get { return GetInt (ReportsCurrentItemKey); }
            set {
                SetInt (ReportsCurrentItemKey, value);
                OnSettingChanged (PropertyReportsCurrentItem);
            }
        }

        public static readonly string PropertyGroupedTimeEntries = GetPropertyName (s => s.GroupedTimeEntries);

        public bool GroupedTimeEntries
        {
            get { return GetInt (PhoebeGroupedEntriesKey) == 1; }
            set {
                SetInt (PhoebeGroupedEntriesKey, value ? 1 : 0);
                OnSettingChanged (PropertyGroupedTimeEntries);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.GroupedTimeEntries);
            }
        }

        public static readonly string PropertyShowNotification = GetPropertyName (s => s.ShowNotification);

        public bool ShowNotification
        {
            get { return (GetInt (JoeyShowNotificationKey) ?? 1) == 1; }
            set {
                SetInt (JoeyShowNotificationKey, value ? 1 : 0);
                OnSettingChanged (PropertyShowNotification);
                ServiceContainer.Resolve<ITracker> ().SendSettingsChangeEvent (SettingName.ShowNotification);
            }
        }

        public static readonly string PropertyProjectSort = GetPropertyName (s => s.SortProjectsBy);

        public string SortProjectsBy
        {
            get { return (GetString (PhoebeProjectSortKey)); }
            set {
                SetString (PhoebeProjectSortKey, value);
            }
        }

        public static readonly string PropertyIsStagingMode = GetPropertyName (s => s.IsStagingMode);

        public bool IsStagingMode
        {
            get { return GetInt (PhoebeIsStagingKey) == 1; }
            set {
                SetInt (PhoebeIsStagingKey, value ? 1 : 0);
                OnSettingChanged (PropertyIsStagingMode);
            }
        }

        public static readonly string PropertyShowWelcome = GetPropertyName (s => s.ShowWelcome);

        public bool ShowWelcome
        {
            get { return (GetInt (PhoebeShowWelcomeKey) ?? 1) == 1; }
            set {
                SetInt (PhoebeShowWelcomeKey, value ? 1 : 0);
            }
        }
    }
}
