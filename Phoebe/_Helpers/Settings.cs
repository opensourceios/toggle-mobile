using System;
using Plugin.Settings;
using Plugin.Settings.Abstractions;

namespace Toggl.Phoebe._Helpers
{
    /// <summary>
    /// This is the Settings static class that can be used in your Core solution or in any
    /// of your client applications. All settings are laid out the same exact way with getters
    /// and setters.
    /// </summary>
    public static class Settings
    {
        private static ISettings AppSettings { get { return CrossSettings.Current; }}

        #region Setting Constants

        //
        // Common keys
        //
        private const string UserIdKey = "phoebeUserId";
        private const string ApiTokenKey = "phoebeApiToken";
        private const string SyncLastRunKey = "phoebeSyncLastRun";
        private const string UseDefaultTagKey = "phoebeUseDefaultTag";
        private const string LastAppVersionKey = "phoebeLastAppVersion";
        private const string ExperimentIdKey = "phoebeExperimentId";
        private const string LastReportZoomKey = "lastReportZoomKey";
        private const string GroupedEntriesKey = "groupedEntriesKey";
        private const string ChooseProjectForNewKey = "chooseProjectForNewKey";
        private const string ReportsCurrentItemKey = "reportsCurrentItem";
        private const string ProjectSortKey = "projectSortKey";
        private const string InstallIdKey = "installId";

        // iOS only keys
        private const string RossPreferredStartViewKey = "rossPreferredStartView";
        private const string RossReadDurOnlyNoticeKey = "rossReadDurOnlyNotice";

        // Android only keys
        private const string GcmRegistrationIdKey = "joeyGcmRegistrationId";
        private const string GcmAppVersionKey = "joeyGcmAppVersion";
        private const string IdleNotificationKey = "idleNotification";
        private const string ShowNotificationKey = "disableNotificationKey";

        //
        // Common Default values
        //
        private static readonly Guid UserIdDefault = Guid.Empty;
        private static readonly string ApiTokenDefault = string.Empty;
        private static readonly DateTime SyncLastRunDefault = DateTime.MinValue;
        private static readonly bool UseDefaultTagDefault = true;
        private static readonly string LastAppVersionDefault = string.Empty;
        private static readonly int LastReportZoomDefault = 0;
        private static readonly bool GroupedEntriesDefault = false;
        private static readonly bool ChooseProjectForNewDefault = false;
        private static readonly int ReportsCurrentItemDefault = 0;
        private static readonly string ProjectSortDefault = string.Empty;
        private static readonly string InstallIdDefault = string.Empty;

        // iOS only Default values
        private static readonly string RossPreferredStartViewDefault = string.Empty;
        private static readonly bool RossReadDurOnlyNoticeDefault = false;
        private static readonly DateTime RossIgnoreSyncErrorsUntilDefault = DateTime.MinValue;

        // Android only Default values
        private static readonly string GcmRegistrationIdDefault = string.Empty;
        private static readonly string GcmAppVersionDefault = string.Empty;
        private static readonly bool IdleNotificationDefault = true;
        private static readonly bool ShowNotificationDefault = true;

        #endregion

        #region Setting properties

        public static Guid UserId
        {
            get { return AppSettings.GetValueOrDefault (UserIdKey, UserIdDefault); }
            set {  AppSettings.AddOrUpdateValue (UserIdKey, value); }
        }

        public static string ApiToken
        {
            get { return AppSettings.GetValueOrDefault (ApiTokenKey, ApiTokenDefault); }
            set {  AppSettings.AddOrUpdateValue (ApiTokenKey, value); }
        }

        public static DateTime SyncLastRun
        {
            get { return AppSettings.GetValueOrDefault (SyncLastRunKey, SyncLastRunDefault); }
            set {  AppSettings.AddOrUpdateValue (SyncLastRunKey, value); }
        }

        public static bool UseDefaultTag
        {
            get { return AppSettings.GetValueOrDefault (UseDefaultTagKey, UseDefaultTagDefault); }
            set {  AppSettings.AddOrUpdateValue (UseDefaultTagKey, value); }
        }

        public static string LastAppVersion
        {
            get { return AppSettings.GetValueOrDefault (LastAppVersionKey, LastAppVersionDefault); }
            set {  AppSettings.AddOrUpdateValue (LastAppVersionKey, value); }
        }

        public static int LastReportZoom
        {
            get { return AppSettings.GetValueOrDefault (LastReportZoomKey, LastReportZoomDefault); }
            set {  AppSettings.AddOrUpdateValue (LastReportZoomKey, value); }
        }

        public static bool GroupedEntries
        {
            get { return AppSettings.GetValueOrDefault (GroupedEntriesKey, GroupedEntriesDefault); }
            set {  AppSettings.AddOrUpdateValue (GroupedEntriesKey, value); }
        }

        public static bool ChooseProjectForNew
        {
            get { return AppSettings.GetValueOrDefault (ChooseProjectForNewKey, ChooseProjectForNewDefault); }
            set {  AppSettings.AddOrUpdateValue (ChooseProjectForNewKey, value); }
        }

        public static int ReportsCurrentItem
        {
            get { return AppSettings.GetValueOrDefault (ReportsCurrentItemKey, ReportsCurrentItemDefault); }
            set {  AppSettings.AddOrUpdateValue (ReportsCurrentItemKey, value); }
        }

        public static string ProjectSort
        {
            get { return AppSettings.GetValueOrDefault (ProjectSortKey, ProjectSortDefault); }
            set {  AppSettings.AddOrUpdateValue (ProjectSortKey, value); }
        }

        public static string InstallId
        {
            get { return AppSettings.GetValueOrDefault (InstallIdKey, InstallIdDefault); }
            set {  AppSettings.AddOrUpdateValue (InstallIdKey, value); }
        }

        // iOS only Settings

        public static string RossPreferredStartView
        {
            get { return AppSettings.GetValueOrDefault (RossPreferredStartViewKey, RossPreferredStartViewDefault); }
            set {  AppSettings.AddOrUpdateValue (RossPreferredStartViewKey, value); }
        }

        public static bool RossReadDurOnlyNotice
        {
            get { return AppSettings.GetValueOrDefault (RossReadDurOnlyNoticeKey, RossReadDurOnlyNoticeDefault); }
            set {  AppSettings.AddOrUpdateValue (RossReadDurOnlyNoticeKey, value); }
        }

        // Android only Settings

        public static string GcmRegistrationId
        {
            get { return AppSettings.GetValueOrDefault (GcmRegistrationIdKey, GcmRegistrationIdDefault); }
            set {  AppSettings.AddOrUpdateValue (GcmRegistrationIdKey, value); }
        }

        public static string GcmAppVersion
        {
            get { return AppSettings.GetValueOrDefault (GcmAppVersionKey, GcmAppVersionDefault); }
            set {  AppSettings.AddOrUpdateValue (GcmAppVersionKey, value); }
        }

        public static bool IdleNotification
        {
            get { return AppSettings.GetValueOrDefault (IdleNotificationKey, IdleNotificationDefault); }
            set {  AppSettings.AddOrUpdateValue (IdleNotificationKey, value); }
        }

        public static bool ShowNotification
        {
            get { return AppSettings.GetValueOrDefault (ShowNotificationKey, ShowNotificationDefault); }
            set {  AppSettings.AddOrUpdateValue (ShowNotificationKey, value); }
        }

        #endregion

    }
}