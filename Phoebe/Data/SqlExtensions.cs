using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Data
{
    /// <summary>
    /// This extension class contains everything that deals with raw SQL.
    /// </summary>
    public static class SqlExtensions
    {
        public static long? GetRemoteId<T> (this IDataStoreContext ctx, Guid id)
        where T : CommonData
        {
            var tbl = ctx.Connection.GetMapping<T> ().TableName;
            var q = String.Concat ("SELECT RemoteId FROM ", tbl, " WHERE Id=?");
            try {
                var remoteId = ctx.Connection.ExecuteScalar<long> (q, id);
                if (remoteId == 0) {
                    // Relation doesn't exist or RemoteId is set to zero
                    return null;
                }
                return remoteId;
            } catch (NullReferenceException) {
                // This happens when the RemoteId of the relation is set to null
                return null;
            }
        }

        public static Guid GetLocalId<T> (this IDataStoreContext ctx, long remoteId)
        where T : CommonData
        {
            var tbl = ctx.Connection.GetMapping<T> ().TableName;
            var q = String.Concat ("SELECT Id FROM ", tbl, " WHERE RemoteId=?");
            return ctx.Connection.ExecuteScalar<Guid> (q, remoteId);
        }

        public static List<string> GetTimeEntryTagNames (this IDataStoreContext ctx, Guid timeEntryId)
        {
            var tagTbl = ctx.Connection.GetMapping<TagData> ().TableName;
            var timeEntryTagTbl = ctx.Connection.GetMapping<TimeEntryTagData> ().TableName;
            var q = String.Concat (
                        "SELECT t.Name AS Value FROM ", tagTbl, " AS t ",
                        "LEFT JOIN ", timeEntryTagTbl, " AS tet ON tet.TagId=t.Id ",
                        "WHERE tet.DeletedAt IS NULL AND tet.TimeEntryId=?");
            var res = ctx.Connection.Query<ColumnRow<string>> (q, timeEntryId);
            return res.Select (v => v.Value).ToList ();
        }

        public static Guid GetTagIdFromName (this IDataStoreContext ctx, Guid workspaceId, string name)
        {
            var con = ctx.Connection;
            var tagTbl = con.GetMapping (typeof (TagData)).TableName;
            var q = String.Concat ("SELECT Id AS Value FROM ", tagTbl, " WHERE WorkspaceId=? AND Name=?");
            return con.ExecuteScalar<Guid> (q, workspaceId, name);
        }

        public static int GetProjectColorFromName (this IDataStoreContext ctx, Guid workspaceId, string name)
        {
            var con = ctx.Connection;
            var tagTbl = con.GetMapping (typeof (ProjectData)).TableName;
            var q = String.Concat ("SELECT Color AS Value FROM ", tagTbl, " WHERE WorkspaceId=? AND Name=?");
            return con.ExecuteScalar<int> (q, workspaceId, name);
        }

        public async static Task<List<ProjectData>> GetUserAccessibleProjects (this IDataStore ds, Guid userId)
        {
            var projectTbl = await ds.GetTableNameAsync<ProjectData>();
            var projectUserTbl = await ds.GetTableNameAsync<ProjectUserData>();
            var q = String.Concat (
                        "SELECT p.* FROM ", projectTbl, " AS p ",
                        "LEFT JOIN ", projectUserTbl, " AS pu ON pu.ProjectId = p.Id AND pu.UserId=? ",
                        "WHERE p.DeletedAt IS NULL AND p.IsActive != 0 AND ",
                        "(p.IsPrivate == 0 OR pu.UserId IS NOT NULL)",
                        "ORDER BY p.Name ");
            return await ds.QueryAsync<ProjectData> (q, userId);
        }

        public async static Task<List<ProjectData>> GetMostUsedProjects (this IDataStore ds, Guid userId)
        {
            var timeEntryTbl = await ds.GetTableNameAsync<TimeEntryData>();
            var projectTbl = await ds.GetTableNameAsync<ProjectData>();
            var q = String.Concat (
                        "SELECT project.* ",
                        "FROM ", timeEntryTbl, " AS entry INNER JOIN ", projectTbl, " AS project ON entry.ProjectId = project.Id ",
                        "WHERE entry.ProjectId != '" + Guid.Empty + "'",
                        "AND project.WorkspaceId != '" + Guid.Empty + "'",
                        "AND project.WorkspaceId IS NOT NULL ",
                        "GROUP BY entry.ProjectId ORDER BY COUNT(*) DESC"
                    );
            return await ds.QueryAsync<ProjectData> (q, userId);
        }

        public async static Task<long> CountUserAccessibleProjects (this IDataStore ds, Guid userId)
        {
            var projectTbl = await ds.GetTableNameAsync<ProjectData>();
            var projectUserTbl = await ds.GetTableNameAsync<ProjectUserData>();
            var q = String.Concat (
                        "SELECT COUNT(*) FROM ", projectTbl, " AS p ",
                        "LEFT JOIN ", projectUserTbl, " AS pu ON pu.ProjectId = p.Id AND pu.UserId=? ",
                        "WHERE p.DeletedAt IS NULL AND p.IsActive != 0 AND ",
                        "(p.IsPrivate == 0 OR pu.UserId IS NOT NULL)");
            return await ds.ExecuteScalarAsync<long> (q, userId);
        }

        public async static Task<List<TagData>> GetTimeEntryTags (this IDataStore ds, Guid timeEntryId)
        {
            /* TODO: Review why this query is not working.
            var tagTbl = await ds.GetTableNameAsync<TagData>();
            var timeEntryTagTbl = await ds.GetTableNameAsync<TimeEntryTagData>();
            var q = string.Concat (
                        "SELECT t.* FROM ", tagTbl, " AS t ",
                        "INNER JOIN ", timeEntryTagTbl, " AS tet ON tet.TagId = t.Id ",
                        "WHERE t.DeletedAt IS NULL AND tet.DeletedAt IS NULL ",
                        "ORDER BY t.Name ",
                "AND tet.TimeEntryId=?");
            */

            var tags = await ds.Table<TagData> ().ToListAsync ();
            var tagRelations = await ds.Table<TimeEntryTagData> ().Where ((arg) => arg.TimeEntryId == timeEntryId).ToListAsync ();
            var tagList = tagRelations.Select (r => tags.FirstOrDefault (tag => tag.Id == r.TagId));
            return tagList.ToList ();
        }

        public static Task ResetAllModificationTimes (this IDataStore ds)
        {
            return ds.ExecuteInTransactionAsync (ctx => {
                foreach (var type in SqliteDataStore.DiscoverDataObjectTypes()) {
                    var con = ctx.Connection;
                    var tbl = con.GetMapping (type).TableName;

                    if (type.IsSubclassOf (typeof (CommonData))) {
                        var q = String.Concat (
                                    "UPDATE ", tbl, " SET ModifiedAt = ? ",
                                    "WHERE RemoteId IS NOT NULL AND DeletedAt IS NULL ",
                                    "AND (IsDirty = 0 OR RemoteRejected = 1)");
                        ctx.Connection.Execute (q, DateTime.MinValue);
                    }
                }
            });
        }

        public static int PurgeDatedTimeCorrections (this IDataStoreContext ctx, DateTime time)
        {
            var tbl = ctx.Connection.GetMapping<TimeCorrectionData> ().TableName;
            var q = String.Concat ("DELETE FROM ", tbl, " WHERE MeasuredAt < ?");
            return ctx.Connection.Execute (q, time);
        }

        public async static Task<DateTime> GetDatesByDays (this IDataStore ds, DateTime initialDate, int daysToLoad)
        {
            var result = DateTime.MinValue;
            var teTable = await ds.GetTableNameAsync<TimeEntryData>();
            var q = String.Concat (
                        "SELECT t.* FROM ", teTable, " AS t ",
                        "WHERE t.DeletedAt IS NULL AND t.State > 0 AND t.StartTime < ? ",
                        "GROUP BY t.StartTime");

            var entries = await ds.QueryAsync<TimeEntryData> (q, initialDate);
            if (entries.Count > 0) {
                var dates = entries.Select (t => t.StartTime.Date).GroupBy (x => x.Date).Take (daysToLoad);
                result = dates.LastOrDefault ().Key;
            }
            return result;
        }


        public static int UpdateTable<T> (IDataStoreContext ctx, params Tuple<string, object>[] args)
        {
            var query = string.Format (
                            "UPDATE {0} SET {1}",
                            ctx.Connection.GetMapping<T> ().TableName,
                            string.Join (",", args.Select (x => x.Item1 + "=?"))
                        );
            return ctx.Connection
                   .CreateCommand (query, args.Select (x => x.Item2).ToArray ())
                   .ExecuteNonQuery ();
        }

        public static int DeleteTable<T> (IDataStoreContext ctx, params Tuple<string, object>[] conditions)
        {
            var query = string.Format (
                            "DELETE FROM {0} WHERE {1}",
                            ctx.Connection.GetMapping<T> ().TableName,
                            string.Join (" AND ", conditions.Select (x => x.Item1 + "=?"))
                        );
            return ctx.Connection
                   .CreateCommand (query, conditions.Select (x => x.Item2).ToArray ())
                   .ExecuteNonQuery ();
        }

        private class ColumnRow<T>
        {
            public T Value { get; set; }
        }
    }
}
