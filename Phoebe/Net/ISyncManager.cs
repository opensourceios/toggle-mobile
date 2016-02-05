using System;


namespace Toggl.Phoebe.Net
{
    public interface ISyncManager
    {
        void Run (SyncMode mode = SyncMode.Full);

        void RunUpload ();

        void RunTimeEntriesUpdate (DateTime startFrom, int daysLoad);

        bool IsRunning { get; }
    }
}
