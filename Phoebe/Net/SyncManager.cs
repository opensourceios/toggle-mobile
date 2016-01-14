using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SQLite.Net.Async;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Data.Json.Converters;
using Toggl.Phoebe.Logging;
using XPlatUtils;

namespace Toggl.Phoebe.Net
{
    public class SyncManager : ISyncManager
    {
        private static readonly string Tag = "SyncManager";
#pragma warning disable 0414
        private readonly Subscription<AuthChangedMessage> subscriptionAuthChanged;
#pragma warning restore 0414
        private Subscription<DataChangeMessage> subscriptionDataChange;

        public SyncManager ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            subscriptionAuthChanged = bus.Subscribe<AuthChangedMessage> (OnAuthChanged);
            subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
        }

        private void OnDataChange (DataChangeMessage msg)
        {
            var dataObject = msg.Data as CommonData;
            if (dataObject != null) {
                if (!dataObject.IsDirty || dataObject.RemoteRejected) {
                    return;
                }
            }

            Run (SyncMode.Auto);
        }

        private void OnAuthChanged (AuthChangedMessage msg)
        {
            if (msg.AuthManager.IsAuthenticated) {
                return;
            }

            // Reset last run on logout
            LastRun = null;
        }

        public async void Run (SyncMode mode = SyncMode.Full)
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (!authManager.IsAuthenticated || authManager.OfflineMode) {
                return;
            }
            if (IsRunning) {
                return;
            }

            var bus = ServiceContainer.Resolve<MessageBus> ();
            var network = ServiceContainer.Resolve<INetworkPresence> ();

            if (!network.IsNetworkPresent) {
                network.RegisterSyncWhenNetworkPresent ();

                // Notify views that sync ended because a network failure.
                bus.Send (new SyncFinishedMessage (this, mode, true, new System.Net.Http.HttpRequestException ()));
                return;
            }

            network.UnregisterSyncWhenNetworkPresent ();
            IsRunning = true;

            // Unsubscribe from models commited messages (our actions trigger them as well,
            // so need to ignore them to prevent infinite recursion.
            if (subscriptionDataChange != null) {
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
            }

            try {
                // Make sure that the RunInBackground is actually started on a background thread
                LastRun = await await Task.Factory.StartNew (() => RunInBackground (mode, LastRun));
            } finally {
                IsRunning = false;
                subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
            }
        }

        public async void UploadUserData ()
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (!authManager.IsAuthenticated) {
                return;
            }
            if (IsRunning) {
                return;
            }

            var bus = ServiceContainer.Resolve<MessageBus> ();
            var network = ServiceContainer.Resolve<INetworkPresence> ();

            if (!network.IsNetworkPresent) {
                return;
            }

            IsRunning = true;

            // Unsubscribe from models commited messages (our actions trigger them as well,
            // so need to ignore them to prevent infinite recursion.
            if (subscriptionDataChange != null) {
                bus.Unsubscribe (subscriptionDataChange);
                subscriptionDataChange = null;
            }

            try {
                // Make sure that the RunInBackground is actually started on a background thread
                await await Task.Factory.StartNew (() => MergeNoUserData ());
            } finally {
                IsRunning = false;
                subscriptionDataChange = bus.Subscribe<DataChangeMessage> (OnDataChange);
            }

        }

        private async Task<DateTime?> RunInBackground (SyncMode mode, DateTime? lastRun)
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            var log = ServiceContainer.Resolve<ILogger> ();

            var syncDuration = Stopwatch.StartNew ();

            // Resolve automatic sync mode to actual mode
            if (mode == SyncMode.Auto) {
                if (lastRun != null && lastRun > Time.UtcNow - TimeSpan.FromMinutes (5)) {
                    mode = SyncMode.Push;
                } else {
                    mode = SyncMode.Full;
                }
                log.Info (Tag, "Starting automatic ({0}) sync.", mode.ToString ());
            } else {
                log.Info (Tag, "Starting {0} sync.", mode.ToString ());
            }

            bus.Send (new SyncStartedMessage (this, mode));

            bool hasErrors = false;
            Exception ex = null;
            try {
                if (mode == SyncMode.Full) {
                    await CollectGarbage ().ConfigureAwait (false);
                }

                if (mode.HasFlag (SyncMode.Pull)) {
                    lastRun = await PullChanges (lastRun).ConfigureAwait (false);
                }

                // Resolve conflicts in client (not server)
                await ResolveConflicts ();

                if (mode.HasFlag (SyncMode.Push)) {
                    hasErrors = await PushChanges ().ConfigureAwait (false);
                }
            } catch (Exception e) {
                if (e.IsNetworkFailure () || e is TaskCanceledException) {
                    log.Info (Tag, e, "Sync ({0}) failed.", mode);
                    if (e.IsNetworkFailure ()) {
                        ServiceContainer.Resolve<INetworkPresence> ().RegisterSyncWhenNetworkPresent ();
                    }
                } else {
                    log.Warning (Tag, e, "Sync ({0}) failed.", mode);
                }

                hasErrors = true;
                ex = e;
            } finally {
                syncDuration.Stop ();
                log.Info (Tag, "Sync finished in {0}ms{1}.", syncDuration.ElapsedMilliseconds, hasErrors ? " (with errors)" : String.Empty);
                bus.Send (new SyncFinishedMessage (this, mode, hasErrors, ex));
            }

            return lastRun;
        }

        private async Task MergeNoUserData ()
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            var log = ServiceContainer.Resolve<ILogger> ();
            var client = ServiceContainer.Resolve<ITogglClient> ();
            var store = ServiceContainer.Resolve<IDataStore> ();

            var syncDuration = Stopwatch.StartNew ();

            bool hasErrors = false;
            Exception ex = null;
            try {

                var serverData = await client.GetChanges (null).ConfigureAwait (false);

                var user = await store.ExecuteInTransactionAsync (ctx => serverData.User.Import (ctx));

                var workspace = store.ExecuteInTransactionAsync (ctx => serverData.Workspaces.ElementAt (0).Import (ctx));


                //Lift data from the local objects to server objects
                var prjTbl = await store.GetTableNameAsync<ProjectData>();
                var cltTbl = await store.GetTableNameAsync<ClientData>();
                var tagsTbl = await store.GetTableNameAsync<TagData>();
                var teTbl = await store.GetTableNameAsync<TimeEntryData>();
                var pudTbl = await store.GetTableNameAsync<ProjectUserData>();

                await store.QueryAsync<ProjectData> (
                    String.Concat ("UPDATE ", cltTbl, " SET WorkspaceId = ?")
                    , workspace.Result.Id);
                await store.QueryAsync<ProjectData> (
                    String.Concat ("UPDATE ", prjTbl, " SET WorkspaceId = ?")
                    , workspace.Result.Id);
                await store.QueryAsync<TagData> (
                    String.Concat ("UPDATE ", tagsTbl, " SET WorkspaceId = ?")
                    , workspace.Result.Id);
                await store.QueryAsync<TimeEntryData> (
                    String.Concat ("UPDATE ", teTbl, " SET WorkspaceId = ?, UserId = ?")
                    , workspace.Result.Id, user.Id);
                await store.QueryAsync<ProjectUserData> (
                    String.Concat ("UPDATE ", pudTbl, " SET UserId = ?")
                    ,user.Id);


                // Delete old data.
                var usrTbl = await store.GetTableNameAsync<UserData>();
                var wsTbl = await store.GetTableNameAsync<WorkspaceData>();
                await store.QueryAsync<WorkspaceData> (String.Concat ("DELETE FROM ", wsTbl ," WHERE Name='Workspace'"), workspace.Result.Id );
                await store.QueryAsync<WorkspaceData> (String.Concat ("DELETE FROM ", usrTbl ," WHERE Name='offlineUser'"), workspace.Result.Id );
            } catch (Exception e) {
                if (e.IsNetworkFailure () || e is TaskCanceledException) {
                    if (e.IsNetworkFailure ()) {
                        ServiceContainer.Resolve<INetworkPresence> ().RegisterSyncWhenNetworkPresent ();
                    }
                } else {
                }

                hasErrors = true;
                ex = e;
            } finally {
                syncDuration.Stop ();
                log.Info (Tag, "Sync finished in {0}ms{1}.", syncDuration.ElapsedMilliseconds, hasErrors ? " (with errors)" : String.Empty);
                bus.Send (new SyncFinishedMessage (this, SyncMode.Full, hasErrors, ex));
            }
        }

        private static async Task CollectGarbage ()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();

            // TODO: Purge data which isn't related to us

            // Purge excess time entries. Do it 200 items at a time, to avoid allocating too much memory to the
            // models to be deleted. If there are more than 200 entries, they will be removed in the next purge.
            var timeEntryRows = await store.Table<TimeEntryData> ()
                                .Where (r => (!r.IsDirty && r.RemoteId != null)
                                        || (r.RemoteId == null && r.DeletedAt != null))
                                .OrderByDescending (r => r.StartTime)
                                .Skip (1000)
                                .Take (200)
                                .ToListAsync ()
                                .ConfigureAwait (false);
            await Task.WhenAll (timeEntryRows.Select (store.DeleteAsync)).ConfigureAwait (false);
        }

        private static async Task<DateTime> PullChanges (DateTime? lastRun)
        {
            if (ServiceContainer.Resolve<AuthManager> ().OfflineMode) {
                return DateTime.Now;
            }
            var client = ServiceContainer.Resolve<ITogglClient> ();
            var store = ServiceContainer.Resolve<IDataStore> ();
            var log = ServiceContainer.Resolve<ILogger> ();

            if (lastRun == null) {
                log.Info (Tag, "Importing all user data from server.");
            } else {
                log.Info (Tag, "Importing changes from server (since {0}).", lastRun);
            }

            var changes = await client.GetChanges (lastRun).ConfigureAwait (false);

            // Import data (in parallel batches)
            var userData = await store.ExecuteInTransactionAsync (ctx => changes.User.Import (ctx));
            await store.ExecuteInTransactionAsync (ctx => {
                foreach (var json in changes.Workspaces) {
                    var workspaceData = json.Import (ctx);

                    // Make sure that the user relation exists and is up to date
                    if (workspaceData != null) {
                        var workspaceUserRows = ctx.Connection.Table<WorkspaceUserData> ()
                                                .Where (r => r.WorkspaceId == workspaceData.Id && r.UserId == userData.Id && r.DeletedAt == null);

                        var workspaceUserData = workspaceUserRows.FirstOrDefault ();
                        if (workspaceUserData == null) {
                            workspaceUserData = new WorkspaceUserData () {
                                WorkspaceId = workspaceData.Id,
                                UserId = userData.Id,
                                IsAdmin = json.IsAdmin,
                            };
                        } else {
                            workspaceUserData.IsAdmin = json.IsAdmin;
                        }

                        ctx.Put (workspaceUserData);
                    }
                }
            });
            await store.ExecuteInTransactionAsync (ctx => {
                foreach (var json in changes.Tags) {
                    json.Import (ctx);
                }
            });
            await store.ExecuteInTransactionAsync (ctx => {
                foreach (var json in changes.Clients) {
                    json.Import (ctx);
                }
            });
            await store.ExecuteInTransactionAsync (ctx => {
                foreach (var json in changes.Projects) {
                    var projectData = json.Import (ctx);

                    // Make sure that the user relation exists
                    if (projectData != null) {
                        var projectUserRows = ctx.Connection.Table<ProjectUserData> ()
                                              .Where (r => r.ProjectId == projectData.Id && r.UserId == userData.Id && r.DeletedAt == null);

                        var projectUserData = projectUserRows.FirstOrDefault ();
                        if (projectUserData == null) {
                            projectUserData = new ProjectUserData () {
                                ProjectId = projectData.Id,
                                UserId = userData.Id,
                            };

                            ctx.Put (projectUserData);
                        }
                    }
                }
            });
            await store.ExecuteInTransactionAsync (ctx => {
                foreach (var json in changes.Tasks) {
                    json.Import (ctx);
                }
            });
            await store.ExecuteInTransactionAsync (ctx => {
                foreach (var json in changes.TimeEntries) {
                    json.Import (ctx);
                }
            });

            return changes.Timestamp;
        }

        private static async Task<bool> PushChanges ()
        {
            if (ServiceContainer.Resolve<AuthManager> ().OfflineMode) {
                return true;
            }
            var log = ServiceContainer.Resolve<ILogger> ();
            var hasErrors = false;

            log.Info (Tag, "Pushing local changes to server.");

            // Construct dependency graph:
            var allDirtyData = await GetAllDirtyData ().ConfigureAwait (false);
            var graph = await RelatedDataGraph.FromDirty (allDirtyData).ConfigureAwait (false);

            // Start pushing the dependencies from the end nodes up
            var tasks = new List<PushTask> ();
            while (true) {
                tasks.Clear ();

                var dirtyDataObjects = graph.EndNodes.ToList ();
                if (dirtyDataObjects.Count == 0) {
                    break;
                }

                foreach (var dataObject in dirtyDataObjects) {
                    if (dataObject.RemoteRejected) {
                        if (dataObject.RemoteId == null) {
                            // Creation has failed, so remove the whole branch.
                            graph.RemoveBranch (dataObject);
                            log.Info (Tag, "Skipping {0} and everything that depends on it.", dataObject.ToIdString ());
                        } else {
                            graph.Remove (dataObject);
                            log.Info (Tag, "Skipping {0}.", dataObject.ToIdString ());
                        }
                    } else {
                        tasks.Add (new PushTask () {
                            Task = PushDataObject (dataObject),
                            Data = dataObject,
                        });
                    }
                }

                // Nothing was pushed this round
                if (tasks.Count < 1) {
                    continue;
                }

                await Task.WhenAll (tasks.Select (p => p.Task)).ConfigureAwait (false);

                foreach (var pushTask in tasks) {
                    var dataObject = pushTask.Data;
                    var error = pushTask.Task.Result;

                    if (error != null) {
                        if (dataObject.RemoteId == null) {
                            // When creation fails, remove branch as there are models that depend on this
                            // one, so there is no point in continuing with the branch.
                            graph.RemoveBranch (dataObject);
                        } else {
                            graph.Remove (dataObject);
                        }
                        hasErrors = true;
                    } else {
                        graph.Remove (dataObject);
                    }
                }
            }

            return hasErrors;
        }

        private static async Task<IEnumerable<CommonData>> GetAllDirtyData ()
        {
            return Enumerable.Empty<CommonData> ()
                   .Concat (await GetDirtyData<WorkspaceData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<WorkspaceUserData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<TagData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<ClientData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<ProjectData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<ProjectUserData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<TaskData> ().ConfigureAwait (false))
                   .Concat (await GetDirtyData<TimeEntryData> ().ConfigureAwait (false));
        }

        private static Task<List<T>> GetDirtyData<T> ()
        where T : CommonData, new()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();
            object query;

            if (typeof (T) == typeof (WorkspaceUserData)) {
                // Exclude intermediate models which we've created from assumptions (for current user
                // and without remote id) from returned models.
                var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
                query = store.Table<WorkspaceUserData> ()
                        .Where (r => r.UserId != userId || r.RemoteId != null);
            } else if (typeof (T) == typeof (ProjectUserData)) {
                // Exclude intermediate models which we've created from assumptions (for current user
                // and without remote id) from returned models.
                var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
                query = store.Table<ProjectUserData> ()
                        .Where (r => r.UserId != userId || r.RemoteId != null);
            } else if (typeof (T) == typeof (TimeEntryData)) {
                // Only sync non-draft time entries for current user:
                var userId = ServiceContainer.Resolve<AuthManager> ().GetUserId ();
                query = store.Table<TimeEntryData> ()
                        .Where (r => r.UserId == userId && r.State != TimeEntryState.New);
            } else {
                query = store.Table<T> ();
            }

            return ((AsyncTableQuery<T>)query)
                   .Where (r => r.IsDirty || r.RemoteId == null || r.DeletedAt != null)
                   .ToListAsync ();
        }

        private static async Task<Exception> PushDataObject (CommonData dataObject)
        {
            var client = ServiceContainer.Resolve<ITogglClient> ();
            var store = ServiceContainer.Resolve<IDataStore> ();
            var log = ServiceContainer.Resolve<ILogger> ();

            Exception error = null;

            try {
                if (dataObject.DeletedAt != null) {
                    if (dataObject.RemoteId != null) {
                        // Delete model
                        log.Info (Tag, "Deleting {0} from server.", dataObject.ToIdString ());

                        var json = await store.ExecuteInTransactionAsync (ctx => dataObject.Export (ctx));
                        await client.Delete (json).ConfigureAwait (false);
                        await store.DeleteAsync (dataObject).ConfigureAwait (false);
                    } else {
                        // Some weird combination where the DeletedAt exists and remote Id doesn't:
                        log.Info (Tag, "Deleting {0} from local store.", dataObject.ToIdString ());

                        await store.DeleteAsync (dataObject).ConfigureAwait (false);
                    }
                } else if (dataObject.RemoteId != null) {
                    log.Info (Tag, "Pushing {0} changes to server.", dataObject.ToIdString ());

                    var json = await store.ExecuteInTransactionAsync (ctx => dataObject.Export (ctx));
                    json = await client.Update (json).ConfigureAwait (false);
                    await store.ExecuteInTransactionAsync (ctx => json.Import (
                            ctx,
                            mergeBase: dataObject
                                                           )).ConfigureAwait (false);
                } else {
                    log.Info (Tag, "Pushing {0} to server.", dataObject.ToIdString ());

                    var json = await store.ExecuteInTransactionAsync (ctx => dataObject.Export (ctx));
                    json = await client.Create (json).ConfigureAwait (false);
                    await store.ExecuteInTransactionAsync (ctx => json.Import (
                            ctx,
                            localIdHint: dataObject.Id,
                            mergeBase: dataObject
                                                           )).ConfigureAwait (false);
                }
            } catch (Exception ex) {
                error = ex;
            }

            // Log & handle errors
            var reqError = error as UnsuccessfulRequestException;

            if (reqError != null && reqError.IsNonExistent) {
                log.Info (Tag, error, "Failed to update {0} as it has been deleted.", dataObject.ToIdString ());
                await store.DeleteAsync (dataObject).ConfigureAwait (false);

            } else if (reqError != null && reqError.IsValidationError) {
                log.Info (Tag, error, "Server rejected {0}.", dataObject.ToIdString ());
                dataObject.RemoteRejected = true;
                await store.PutDataAsync (dataObject).ConfigureAwait (false);

            } else if (error != null && error.IsNetworkFailure ()) {
                log.Info (Tag, error, "Failed to sync {0}.", dataObject.ToIdString ());

            } else if (error != null) {
                log.Warning (Tag, error, "Failed to sync {0}.", dataObject.ToIdString ());
            }

            return error;
        }

        private async Task ResolveConflicts ()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();

            await store.ExecuteInTransactionAsync (ctx => {
                var runningEntries = ctx.Connection.Table <TimeEntryData> ()
                                     .Where (r => r.State == TimeEntryState.Running && r.DeletedAt == null)
                                     .OrderByDescending (r => r.StartTime).ToList ();

                // Try enforce single running.
                if (runningEntries.Count > 1)
                    runningEntries.Skip (1).ForEach (te => {
                    // Stop time entry
                    te.StopTime = Time.UtcNow;
                    te.State = TimeEntryState.Finished;
                    Toggl.Phoebe.Data.Models.Model<TimeEntryData>.MarkDirty (te);
                    ctx.Put (te);
                });
            });
        }

        public bool IsRunning { get; private set; }

        private DateTime? LastRun
        {
            get { return ServiceContainer.Resolve<ISettingsStore> ().SyncLastRun; }
            set { ServiceContainer.Resolve<ISettingsStore> ().SyncLastRun = value; }
        }

        private class PushTask
        {
            public Task<Exception> Task;
            public CommonData Data;
        }

        public void RunTimeEntriesUpdate (DateTime startFrom, int daysLoad)
        {
            var authManager = ServiceContainer.Resolve<AuthManager> ();
            if (!authManager.OfflineMode) {
                Task.Run (async () => await RunTimeEntriesUpdateAsync (startFrom, daysLoad));
            }
        }

        private async Task RunTimeEntriesUpdateAsync (DateTime startFrom, int daysLoad)
        {
            var bus = ServiceContainer.Resolve<MessageBus> ();
            bool hadErrors = false;
            bool hasMore = true;
            var endDate = DateTime.MinValue;

            // Try to update with latest data from server
            try {
                bus.Send (new UpdateStartedMessage (this, startFrom));

                // Download new Entries
                var client = ServiceContainer.Resolve<ITogglClient> ();
                var jsonEntries = await client.ListTimeEntries (startFrom, daysLoad);

                // Store them in the local data store
                var dataStore = ServiceContainer.Resolve<IDataStore> ();
                var entries = await dataStore.ExecuteInTransactionAsync (ctx =>
                              jsonEntries.Select (json => json.Import (ctx)).ToList ());
                hasMore = entries.Any ();
                endDate = hasMore ? entries.Min (p => p.StartTime) : startFrom;
            } catch (Exception exc) {
                hadErrors = true;
                var tag = GetType ().Name;
                var log = ServiceContainer.Resolve<ILogger> ();
                const string msg = "Failed to fetch time entries {1} days up to {0}";
                if (exc.IsNetworkFailure () || exc is TaskCanceledException) {
                    log.Info (tag, exc, msg, startFrom, daysLoad);
                } else {
                    log.Warning (tag, exc, msg, startFrom, daysLoad);
                }
            } finally {
                bus.Send (new UpdateFinishedMessage (this, startFrom, endDate, hasMore, hadErrors));
            }
        }
    }
}
