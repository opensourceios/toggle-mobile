using System;


namespace Toggl.Phoebe.Net
{
    public interface ISyncManager
    {
        void Run (SyncMode mode = SyncMode.Full);

        void UploadUserData ();

        void RunTimeEntriesUpdate (DateTime startFrom, int daysLoad);

        bool IsRunning { get; }
    }
}
