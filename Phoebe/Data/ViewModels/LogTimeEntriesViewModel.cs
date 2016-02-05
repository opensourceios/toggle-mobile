using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using GalaSoft.MvvmLight;
using PropertyChanged;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Data.Utils;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.ViewModels
{
    [ImplementPropertyChanged]
    public class LogTimeEntriesViewModel : ViewModelBase, IDisposable
    {
        private readonly Timer durationTimer;
        private Subscription<SettingChangedMessage> subscriptionSettingChanged;
        private Subscription<SyncFinishedMessage> subscriptionSyncFinished;
        private Subscription<UpdateFinishedMessage> subscriptionUpdateFinished;
        private TimeEntriesFeed collectionFeed;
        private readonly ActiveTimeEntryManager activeTimeEntryManager;

        LogTimeEntriesViewModel ()
        {
            // durationTimer will update the Duration value if ActiveTimeEntry is running
            durationTimer = new Timer ();
            durationTimer.Elapsed += DurationTimerCallback;

            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "TimeEntryList Screen";
            activeTimeEntryManager = ServiceContainer.Resolve<ActiveTimeEntryManager> ();
            activeTimeEntryManager.PropertyChanged += OnActiveTimeEntryChanged;

            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionSettingChanged = bus.Subscribe<SettingChangedMessage> (OnSettingChanged);
            subscriptionSyncFinished = bus.Subscribe<SyncFinishedMessage> (OnSyncFinished);
            subscriptionUpdateFinished = bus.Subscribe<UpdateFinishedMessage> (OnUpdateItemsFinished);

            HasMoreItems = true;
            HasLoadErrors = false;
            HasItems = false;

            UpdateView (activeTimeEntryManager.ActiveTimeEntry);
            SyncCollectionView ();
        }

        public static LogTimeEntriesViewModel Init ()
        {
            var vm = new LogTimeEntriesViewModel ();
            return vm;
        }

        public void Dispose ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            if (subscriptionSettingChanged != null) {
                bus.Unsubscribe (subscriptionSettingChanged);
                subscriptionSettingChanged = null;
            }
            if (subscriptionSyncFinished != null) {
                bus.Unsubscribe (subscriptionSyncFinished);
                subscriptionSyncFinished = null;
            }
            if (subscriptionUpdateFinished != null) {
                bus.Unsubscribe (subscriptionUpdateFinished);
                subscriptionUpdateFinished = null;
            }

            activeTimeEntryManager.PropertyChanged -= OnActiveTimeEntryChanged;
            durationTimer.Elapsed -= DurationTimerCallback;
            DisposeCollection ();
        }

        private void DisposeCollection ()
        {
            if (collectionFeed != null) {
                collectionFeed.Dispose ();
            }

            if (Collection != null) {
                Collection.CollectionChanged -= OnDetectHasItems;
                if (Collection is TimeEntriesCollection<TimeEntryHolder>) {
                    ((TimeEntriesCollection<TimeEntryHolder>)Collection).Dispose ();
                } else {
                    ((TimeEntriesCollection<TimeEntryGroup>)Collection).Dispose ();
                }
            }
        }

        #region Properties for ViewModel binding
        public bool IsProcessingAction { get; private set; }

        public bool IsAppSyncing { get; private set; }

        public bool IsTimeEntryRunning { get; private set; }

        public bool IsGroupedMode { get; private set; }

        public bool HasMoreItems { get; private set; }

        public bool HasLoadErrors { get; private set; }

        public bool HasItems { get; private set; }

        public string Description { get; set; }

        public string ProjectName { get; set; }

        public string Duration { get; private set; }

        public ObservableCollection<IHolder> Collection { get; private set; }
        #endregion

        #region Sync operations
        public void TriggerFullSync ()
        {
            IsAppSyncing = true;

            var syncManager = ServiceContainer.Resolve<ISyncManager> ();
            syncManager.Run ();
        }

        public async Task LoadMore ()
        {

            HasMoreItems = true;
            HasLoadErrors = false;

            var startDate = await collectionFeed.LoadMore ();
            var syncManager = ServiceContainer.Resolve<ISyncManager> ();
            syncManager.RunTimeEntriesUpdate (startDate, TimeEntriesFeed.DaysLoad);
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (authManager.OfflineMode) {
                HasMoreItems = Collection.Count > 0;
                return;
            }
        }
        #endregion

        #region Time entry operations
        public async Task<TimeEntryData> ContinueTimeEntryAsync (int index)
        {
            var newTimeEntry = new TimeEntryData ();
            var timeEntryHolder = Collection.ElementAt (index) as ITimeEntryHolder;

            if (timeEntryHolder == null) {
                return newTimeEntry;
            }

            if (timeEntryHolder.Data.State == TimeEntryState.Running) {
                newTimeEntry = await TimeEntryModel.StopAsync (timeEntryHolder.Data);
                ServiceContainer.Resolve<ITracker>().SendTimerStopEvent (TimerStopSource.App);
            } else {
                newTimeEntry = await TimeEntryModel.ContinueAsync (timeEntryHolder.Data);
                ServiceContainer.Resolve<ITracker>().SendTimerStartEvent (TimerStartSource.AppContinue);
            }

            return newTimeEntry;
        }

        public async Task<TimeEntryData> StartStopTimeEntry ()
        {
            // Protect from double clicks?
            if (IsProcessingAction) {
                return activeTimeEntryManager.ActiveTimeEntry;
            }

            IsProcessingAction = true;

            var active = activeTimeEntryManager.ActiveTimeEntry;
            active = active.State == TimeEntryState.Running ? await TimeEntryModel.StopAsync (active) : await TimeEntryModel.StartAsync (active);

            IsProcessingAction = false;

            if (active.State == TimeEntryState.Running) {
                ServiceContainer.Resolve<ITracker>().SendTimerStartEvent (TimerStartSource.AppNew);
            } else {
                ServiceContainer.Resolve<ITracker>().SendTimerStopEvent (TimerStopSource.App);
            }

            // Welcome wizard isn't needed after a time entry is started / stopped.
            ServiceContainer.Resolve<ISettingsStore> ().ShowWelcome = false;

            return active;
        }

        public Task RemoveTimeEntryAsync (int index)
        {
            var te = Collection.ElementAt (index) as ITimeEntryHolder;
            return TimeEntryModel.DeleteTimeEntryDataAsync (te.Data);
        }

        public TimeEntryData GetActiveTimeEntry ()
        {
            return activeTimeEntryManager.ActiveTimeEntry;
        }
        #endregion

        #region Extra operations
        public void ReportExperiment (int number, string actionKey, string actionValue)
        {
            if (!HasItems && ServiceContainer.Resolve<ISettingsStore> ().ShowWelcome) {
                OBMExperimentManager.Send (number, actionKey, actionValue);
            }
        }
        #endregion

        private void SyncCollectionView ()
        {
            DisposeCollection ();
            IsGroupedMode = ServiceContainer.Resolve<ISettingsStore> ().GroupedTimeEntries;

            collectionFeed = new TimeEntriesFeed ();
            Collection = IsGroupedMode
                         ?        (ObservableCollection<IHolder>)new TimeEntriesCollection<TimeEntryGroup> (collectionFeed)
                         : new TimeEntriesCollection<TimeEntryHolder> (collectionFeed);
            Collection.CollectionChanged += OnDetectHasItems;
        }

        private void UpdateView (TimeEntryData data)
        {
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (async () => {

                if (data.State == TimeEntryState.Running) {
                    Description = string.IsNullOrEmpty (data.Description) ? string.Empty : data.Description;
                    if (data.ProjectId != null) {
                        var prj = await TimeEntryModel.GetProjectDataAsync (data.ProjectId.Value);
                        ProjectName = prj.Name;
                    } else {
                        ProjectName = string.Empty;
                    }
                    IsTimeEntryRunning = true;
                    durationTimer.Start ();
                } else {
                    IsTimeEntryRunning = false;
                    Description = string.Empty;
                    ProjectName = string.Empty;
                    durationTimer.Stop ();
                    Duration = TimeSpan.FromSeconds (0).ToString ().Substring (0, 8);
                }
            });
        }

        private void OnActiveTimeEntryChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == ActiveTimeEntryManager.PropertyActiveTimeEntry) {
                UpdateView (activeTimeEntryManager.ActiveTimeEntry);
            }
        }

        private void OnSettingChanged (SettingChangedMessage msg)
        {
            // Implement a GetPropertyName
            if (msg.Name == "GroupedTimeEntries") {
                SyncCollectionView ();
            }
        }

        private void OnSyncFinished (SyncFinishedMessage msg)
        {
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                IsAppSyncing = false;
            });
        }

        private void OnUpdateItemsFinished (UpdateFinishedMessage msg)
        {
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                HasMoreItems = msg.HadMore;
                HasLoadErrors = msg.HadErrors;
            });
        }

        private void OnDetectHasItems (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                HasItems = Collection.Count > 0;
            });
        }

        private void DurationTimerCallback (object sender, ElapsedEventArgs e)
        {
            var duration = TimeEntryModel.GetDuration (activeTimeEntryManager.ActiveTimeEntry, Time.UtcNow);  //model.GetDuration ();
            durationTimer.Interval = 1000 - duration.Milliseconds;

            // Update on UI Thread
            ServiceContainer.Resolve<IPlatformUtils> ().DispatchOnUIThread (() => {
                Duration = TimeSpan.FromSeconds (duration.TotalSeconds).ToString ().Substring (0, 8);
            });
        }
    }
}