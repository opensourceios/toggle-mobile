using System;

namespace Toggl.Phoebe.Data
{
    public interface ISettingsStore
    {
        Guid? UserId { get; set; }

        bool OfflineMode { get; set; }

        string ApiToken { get; set; }

        DateTime? SyncLastRun { get; set; }

        bool UseDefaultTag { get; set; }

        string LastAppVersion { get; set; }

        string ExperimentId { get; set; }

        int? LastReportZoomViewed { get; set; }

        bool GroupedTimeEntries { get; set; }

        string SortProjectsBy { get; set; }

        bool IsStagingMode { get; set; }

        bool ShowWelcome { get; set; }
    }
}
