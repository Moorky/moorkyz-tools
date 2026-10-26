using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Channels;
using Moorkyz.Tools.MediaRecover.Core;

namespace Moorkyz.Tools.MediaRecover.Providers.Wpd;

public sealed class WpdProvider : ISourceProvider
{
    public string Kind => "WindowsPortableDevice";
    public SourceCapabilities Capabilities => new(false, false, true);
    public async Task<IReadOnlyList<DeviceInfo>> DevicesAsync()
    {
        using var worker = new StaWorker();
        return await worker.Invoke<IReadOnlyList<DeviceInfo>>(() =>
        {
            var manager = Com.New<IDeviceManager>("0AF10CEC-2ECD-4B92-9581-34F6AE0637F3");
            IntPtr ids = IntPtr.Zero;
            uint allocated = 0;
            try
            {
                manager.RefreshDeviceList();
                uint count = 0; manager.GetDevices(IntPtr.Zero, ref count);
                if (count > 1024) throw new IOException("Unexpected WPD device count.");
                if (count == 0) return Array.Empty<DeviceInfo>();
                allocated = count;
                ids = Marshal.AllocCoTaskMem(checked((int)count * IntPtr.Size));
                for (var i = 0; i < count; i++) Marshal.WriteIntPtr(ids, i * IntPtr.Size, IntPtr.Zero);
                manager.GetDevices(ids, ref count);
                var result = new List<DeviceInfo>();
                for (var i = 0; i < Math.Min(count, allocated); i++)
                {
                    var id = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ids, i * IntPtr.Size))!;
                    uint chars = 0; manager.GetDeviceFriendlyName(id, IntPtr.Zero, ref chars);
                    if (chars is 0 or > 32768) throw new IOException("Invalid WPD friendly-name buffer size.");
                    var name = Marshal.AllocCoTaskMem(checked((int)chars * 2));
                    try { manager.GetDeviceFriendlyName(id, name, ref chars); result.Add(new(id, Marshal.PtrToStringUni(name) ?? "Portable device")); }
                    finally { Marshal.FreeCoTaskMem(name); }
                }
                return result;
            }
            finally
            {
                if (ids != IntPtr.Zero)
                {
                    for (var i = 0; i < allocated; i++) Marshal.FreeCoTaskMem(Marshal.ReadIntPtr(ids, i * IntPtr.Size));
                    Marshal.FreeCoTaskMem(ids);
                }
                Com.Release(manager);
            }
        });
    }
    public async IAsyncEnumerable<InventoryEntry> InventoryAsync(SourceSpec source, Settings settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;
        var channel = Channel.CreateBounded<InventoryEntry>(100);
        using var worker = new StaWorker();
        var producer = worker.Invoke(() =>
        {
            try
            {
                using var session = new Session(source.Id);
                var pending = new Stack<(string Id, string Path)>(); pending.Push(("DEVICE", ""));
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (pending.TryPop(out var parent))
                {
                    token.ThrowIfCancellationRequested();
                    IObjectIds? objects = null;
                    try
                    {
                        session.Content.EnumObjects(0, parent.Id, IntPtr.Zero, out objects);
                        while (true)
                        {
                            var ids = new string[16];
                            var hr = objects.Next(16, ids, out var fetched);
                            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                            if (fetched == 0) break;
                            for (var i = 0; i < fetched; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                if (!seen.Add(ids[i])) continue;
                                IValues? values = null;
                                try
                                {
                                    session.Properties.GetValues(ids[i], IntPtr.Zero, out values);
                                    var name = Com.String(values, 12) ?? Com.String(values, 4) ?? ids[i];
                                    var path = parent.Path.Length == 0 ? PathSafety.SafeName(name) : parent.Path + "\\" + PathSafety.SafeName(name);
                                    if (Filter.Excluded(path, settings)) continue;
                                    if (!settings.IncludeHidden && Com.Hidden(values)) continue;
                                    var type = Com.ContentType(values);
                                    if (type == Com.Folder || type == Com.Functional)
                                    {
                                        pending.Push((ids[i], path)); continue;
                                    }
                                    var dates = new List<DateValue> { Com.Date(values, 18, "WPD_OBJECT_DATE_CREATED"), Com.Date(values, 19, "WPD_OBJECT_DATE_MODIFIED") };
                                    var persistent = Com.String(values, 5);
                                    var size = Com.Size(values);
                                    if (type == Guid.Empty && size is null && Filter.Media(name, settings) is null)
                                    {
                                        // Missing content-type metadata must not silently truncate an unknown subtree.
                                        pending.Push((ids[i], path)); continue;
                                    }
                                    var version = Version(size, dates, persistent);
                                    var key = PathSafety.Key(Kind + "|" + source.Id + "|" + (persistent ?? ids[i]));
                                    var item = new SourceItem(key, source, ids[i], path, size, version, dates, Filter.Media(name, settings) ?? "ExcludedType");
                                    channel.Writer.WriteAsync(new(item, null), token).AsTask().GetAwaiter().GetResult();
                                }
                                catch (COMException e) { WriteIssue(ids[i], e); }
                                finally { Com.Release(values); }
                            }
                        }
                    }
                    catch (COMException e) { WriteIssue(parent.Path.Length == 0 ? source.Id : parent.Path, e); }
                    finally { Com.Release(objects); }
                }
                channel.Writer.TryComplete();
                void WriteIssue(string path, Exception e) => channel.Writer.WriteAsync(new(null, new(path, "WPD_ENUMERATION_FAILED", e.Message, "Unlock phone, authorize this PC, choose USB File Transfer, reconnect and rescan. Internal storage/SD may be unavailable.")), token).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception e) { channel.Writer.TryComplete(Map(e)); }
            return true;
        });
        try { await foreach (var entry in channel.Reader.ReadAllAsync(cancellationToken)) yield return entry; }
        finally { linked.Cancel(); await producer.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
    public async Task<bool> IsUnchangedAsync(SourceItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var worker = new StaWorker();
        return await worker.Invoke(() =>
        {
            try
            {
                using var session = new Session(item.Source.Id);
                session.Properties.GetValues(item.ObjectId, IntPtr.Zero, out var values);
                try
                {
                    var dates = new[] { Com.Date(values, 18, "WPD_OBJECT_DATE_CREATED"), Com.Date(values, 19, "WPD_OBJECT_DATE_MODIFIED") };
                    var version = Version(Com.Size(values), dates, Com.String(values, 5));
                    return version == item.Version;
                }
                finally { Com.Release(values); }
            }
            catch (COMException e) { throw Map(e); }
        }).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }
    public async Task<Stream> OpenReadAsync(SourceItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var worker = new StaWorker();
        Task<Stream>? opening = null;
        try
        {
            opening = worker.Invoke<Stream>(() =>
            {
                Session? session = null;
                IResources? resources = null;
                try
                {
                    session = new Session(item.Source.Id);
                    session.Content.Transfer(out resources);
                    var key = new PropertyKey(new Guid("E81E79BE-34F0-41BF-B53F-F1A06AE87842"), 0);
                    uint buffer = 0;
                    resources.GetStream(item.ObjectId, ref key, 0, ref buffer, out var stream);
                    return new WpdReadStream(worker, session, resources, stream, Math.Clamp(buffer, 4096, 1024 * 1024));
                }
                catch { Com.Release(resources); session?.Dispose(); throw; }
            });
            return await opening.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (Exception e)
        {
            if (opening is null) worker.Dispose();
            else _ = opening.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully) task.Result.Dispose();
                else { _ = task.Exception; worker.Dispose(); }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw Map(e);
        }
    }
    private static Exception Map(Exception e) => e is not COMException ? e :
        e.HResult == unchecked((int)0x80070005)
            ? new RecoveryException("DEVICE_LOCKED_OR_UNAUTHORIZED", $"WPD access denied (0x{e.HResult:X8}): {e.Message}", "Unlock phone and authorize this PC. Select USB File Transfer, then rescan. Protected storage is not bypassed.", false, e)
            : new RecoveryException("DEVICE_DISCONNECTED", $"WPD access failed (0x{e.HResult:X8}): {e.Message}", "Unlock phone, authorize PC, select USB File Transfer, reconnect and rescan. Stale object IDs require a new scan.", true, e);
    private static string Version(long? size, IEnumerable<DateValue> dates, string? persistent)
    {
        var values = dates.ToArray();
        var provisional = persistent is null || size is null || !values.Any(d => d.Value is not null && d.Value.Value.Year is >= 1970 and <= 2200);
        return $"{size}:{string.Join('|', values.Select(d => d.Raw))}" + (provisional ? ":provisional" : "");
    }
    private sealed class Session : IDisposable
    {
        private IDevice? device;
        public IContent Content { get; private set; } = null!;
        public IProperties Properties { get; private set; } = null!;
        public Session(string id)
        {
            var values = Com.New<IValues>("0C15D503-D017-47CE-9016-7B3F978721CC");
            try
            {
                var client = new Guid("204D9F0C-2292-4080-9F42-40664E70F859");
                var key = new PropertyKey(client, 2); values.SetStringValue(ref key, "Moorkyz Media Recover");
                key = new(client, 3); values.SetUnsignedIntegerValue(ref key, 0);
                key = new(client, 4); values.SetUnsignedIntegerValue(ref key, 1);
                key = new(client, 9); values.SetUnsignedIntegerValue(ref key, 0x80000000); // GENERIC_READ, never request write access.
                device = Com.New<IDevice>("728A21C5-3D9E-48D7-9810-864848F0F404");
                device.Open(id, values); device.Content(out var content); Content = content;
                content.Properties(out var properties); Properties = properties;
            }
            catch { Dispose(); throw; }
            finally { Com.Release(values); }
        }
        public void Dispose()
        {
            Com.Release(Properties); Com.Release(Content);
            if (device is not null)
            {
                try { device.Close(); } catch (COMException) { /* Disconnected handles still need releasing. */ }
                Com.Release(device); device = null;
            }
        }
    }
    private sealed class WpdReadStream(StaWorker worker, Session session, IResources resources, IStream native, uint optimalBuffer) : Stream
    {
        private bool disposed;
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var chunk = new byte[Math.Min(buffer.Length, (int)optimalBuffer)];
                var read = await worker.Invoke(() =>
                {
                    var pointer = Marshal.AllocCoTaskMem(4);
                    try { Marshal.WriteInt32(pointer, 0); native.Read(chunk, chunk.Length, pointer); return Marshal.ReadInt32(pointer); }
                    finally { Marshal.FreeCoTaskMem(pointer); }
                }).WaitAsync(cancellationToken);
                chunk.AsMemory(0, read).CopyTo(buffer);
                return read;
            }
            catch (Exception e) { throw Map(e); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposed) return; disposed = true;
            // The owning STA releases all native handles; a stuck driver cannot block the UI indefinitely.
            _ = worker.Invoke(() => { Com.Release(native); Com.Release(resources); session.Dispose(); return true; });
            worker.Dispose();
            base.Dispose(disposing);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
