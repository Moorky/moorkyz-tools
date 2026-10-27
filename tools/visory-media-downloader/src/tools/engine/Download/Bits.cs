using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MoorkszVisoryMediaDownloader.Core.Download;

public enum BitsJobState { Queued = 0, Connecting = 1, Transferring = 2, Suspended = 3, Error = 4, TransientError = 5, Transferred = 6, Acknowledged = 7, Cancelled = 8, Missing = 100 }

public enum BitsPriority { Foreground = 0, High = 1, Normal = 2, Low = 3 }

public sealed record BitsStatus(BitsJobState State, long BytesTransferred, long? BytesTotal, int? HttpStatus, int? ErrorCode, string? ErrorText);

public interface IBitsClient
{
    bool IsAvailable { get; }
    Guid Create(string displayName, Uri remote, string localPath, BitsPriority priority, TimeSpan noProgressTimeout);
    BitsStatus Query(Guid job);
    void Resume(Guid job);
    void Suspend(Guid job);
    void Cancel(Guid job);
    void Complete(Guid job);
}

/// <summary>Background Intelligent Transfer Service through its COM API (no external dependency).</summary>
[SupportedOSPlatform("windows")]
public sealed class BitsClient : IBitsClient
{
    private const int HttpErrorFacilityMask = unchecked((int)0xFFFF0000);
    private const int HttpErrorBase = unchecked((int)0x80190000);
    private readonly Lazy<IBackgroundCopyManager?> manager = new(() =>
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("4991d34b-80a1-4291-83b6-3328366b9097"), throwOnError: false);
            return type is null ? null : (IBackgroundCopyManager?)Activator.CreateInstance(type);
        }
        catch (COMException)
        {
            return null;
        }
    });

    public bool IsAvailable => manager.Value is not null;

    private IBackgroundCopyManager Manager => manager.Value ?? throw new InvalidOperationException("Windows BITS is not available.");

    public Guid Create(string displayName, Uri remote, string localPath, BitsPriority priority, TimeSpan noProgressTimeout)
    {
        Manager.CreateJob(displayName, 0, out var id, out var job);
        try
        {
            job.SetDescription("Moorksz Visory Media Downloader recording transfer");
            job.SetPriority((int)priority);
            job.SetNoProgressTimeout((uint)Math.Clamp(noProgressTimeout.TotalSeconds, 30, 86400));
            job.SetMinimumRetryDelay(30);
            if (job is IBackgroundCopyJobHttpOptions options)
            {
                const uint DisallowRedirects = 0x200;
                options.SetSecurityFlags(DisallowRedirects);
            }
            job.AddFile(remote.AbsoluteUri, localPath);
            return id;
        }
        catch
        {
            try { job.Cancel(); } catch (COMException) { }
            throw;
        }
        finally
        {
            Marshal.ReleaseComObject(job);
        }
    }

    public BitsStatus Query(Guid id)
    {
        var job = TryGet(id);
        if (job is null) return new BitsStatus(BitsJobState.Missing, 0, null, null, null, null);
        try
        {
            job.GetState(out var state);
            job.GetProgress(out var progress);
            long? total = progress.BytesTotal == ulong.MaxValue ? null : (long)progress.BytesTotal;
            int? http = null;
            int? code = null;
            string? text = null;
            if ((BitsJobState)state is BitsJobState.Error or BitsJobState.TransientError)
            {
                try
                {
                    job.GetError(out var error);
                    try
                    {
                        error.GetError(out _, out var hr);
                        code = hr;
                        if ((hr & HttpErrorFacilityMask) == HttpErrorBase) http = hr & 0xFFFF;
                        try { error.GetErrorDescription(0x0409, out var description); text = description?.Trim(); } catch (COMException) { }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(error);
                    }
                }
                catch (COMException) { }
            }
            return new BitsStatus((BitsJobState)state, (long)progress.BytesTransferred, total, http, code, text);
        }
        finally
        {
            Marshal.ReleaseComObject(job);
        }
    }

    public void Resume(Guid id) => With(id, job => job.Resume());
    public void Suspend(Guid id) => With(id, job => job.Suspend());
    public void Cancel(Guid id) => With(id, job => job.Cancel());
    public void Complete(Guid id) => With(id, job => job.Complete());

    private void With(Guid id, Action<IBackgroundCopyJob> action)
    {
        var job = TryGet(id);
        if (job is null) return;
        try { action(job); }
        finally { Marshal.ReleaseComObject(job); }
    }

    private IBackgroundCopyJob? TryGet(Guid id)
    {
        try
        {
            Manager.GetJob(ref id, out var job);
            return job;
        }
        catch (COMException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BgJobProgress
    {
        public ulong BytesTotal;
        public ulong BytesTransferred;
        public uint FilesTotal;
        public uint FilesTransferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BgJobTimes
    {
        public long CreationTime;
        public long ModificationTime;
        public long TransferCompletionTime;
    }

    [ComImport, Guid("5ce34c0d-0dc9-4c1f-897c-daa1b78cee7c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBackgroundCopyManager
    {
        void CreateJob([MarshalAs(UnmanagedType.LPWStr)] string displayName, int type, out Guid jobId, out IBackgroundCopyJob job);
        void GetJob(ref Guid jobId, out IBackgroundCopyJob job);
        void EnumJobs(uint flags, out IntPtr jobs);
        void GetErrorDescription(int hr, uint languageId, [MarshalAs(UnmanagedType.LPWStr)] out string description);
    }

    [ComImport, Guid("37668d37-507e-4160-9316-26306d150b12"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBackgroundCopyJob
    {
        void AddFileSet(uint count, IntPtr files);
        void AddFile([MarshalAs(UnmanagedType.LPWStr)] string remoteUrl, [MarshalAs(UnmanagedType.LPWStr)] string localName);
        void EnumFiles(out IntPtr files);
        void Suspend();
        void Resume();
        void Cancel();
        void Complete();
        void GetId(out Guid id);
        void GetType(out int type);
        void GetProgress(out BgJobProgress progress);
        void GetTimes(out BgJobTimes times);
        void GetState(out int state);
        void GetError(out IBackgroundCopyError error);
        void GetOwner(out IntPtr owner);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetDisplayName(out IntPtr name);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetDescription(out IntPtr description);
        void SetPriority(int priority);
        void GetPriority(out int priority);
        void SetNotifyFlags(uint flags);
        void GetNotifyFlags(out uint flags);
        void SetNotifyInterface([MarshalAs(UnmanagedType.IUnknown)] object? callback);
        void GetNotifyInterface([MarshalAs(UnmanagedType.IUnknown)] out object callback);
        void SetMinimumRetryDelay(uint seconds);
        void GetMinimumRetryDelay(out uint seconds);
        void SetNoProgressTimeout(uint seconds);
        void GetNoProgressTimeout(out uint seconds);
        void GetErrorCount(out uint errors);
        void SetProxySettings(int usage, IntPtr list, IntPtr bypass);
        void GetProxySettings(out int usage, out IntPtr list, out IntPtr bypass);
        void TakeOwnership();
    }

    [ComImport, Guid("19c613a0-fcb8-4f28-81ae-897c3d078f81"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBackgroundCopyError
    {
        void GetError(out int context, out int code);
        void GetFile(out IntPtr file);
        void GetErrorDescription(uint languageId, [MarshalAs(UnmanagedType.LPWStr)] out string description);
        void GetErrorContextDescription(uint languageId, [MarshalAs(UnmanagedType.LPWStr)] out string description);
        void GetProtocol([MarshalAs(UnmanagedType.LPWStr)] out string protocol);
    }

    [ComImport, Guid("f1bd1079-9f01-4bdc-8036-f09b70095066"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBackgroundCopyJobHttpOptions
    {
        void SetClientCertificateByID(int storeLocation, [MarshalAs(UnmanagedType.LPWStr)] string storeName, IntPtr hash);
        void SetClientCertificateByName(int storeLocation, [MarshalAs(UnmanagedType.LPWStr)] string storeName, [MarshalAs(UnmanagedType.LPWStr)] string subjectName);
        void RemoveClientCertificate();
        void GetClientCertificate(out int storeLocation, out IntPtr storeName, out IntPtr hash, out IntPtr subjectName);
        void SetCustomHeaders([MarshalAs(UnmanagedType.LPWStr)] string headers);
        void GetCustomHeaders(out IntPtr headers);
        void SetSecurityFlags(uint flags);
        void GetSecurityFlags(out uint flags);
    }
}
