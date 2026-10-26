using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Moorkyz.Tools.MediaRecover.Core;

namespace Moorkyz.Tools.MediaRecover.Providers.Local;

public sealed class LocalFolderProvider : ISourceProvider
{
    public string Kind => "LocalFolder";
    public SourceCapabilities Capabilities => new(true, false, true);

    public async IAsyncEnumerable<InventoryEntry> InventoryAsync(SourceSpec source, Settings settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(source.Id);
        PathSafety.NoLinks(root);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerator<string>? enumerator = null;
            ScanIssue? issue = null;
            try { PathSafety.NoLinks(directory); enumerator = Directory.EnumerateFileSystemEntries(directory).GetEnumerator(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { issue = Issue(directory, e); }
            if (issue is not null) { yield return new(null, issue); continue; }
            using (enumerator)
            {
                while (true)
                {
                    string? path = null;
                    issue = null;
                    try { if (enumerator!.MoveNext()) path = enumerator.Current; }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { issue = Issue(directory, e); }
                    if (issue is not null) { yield return new(null, issue); break; }
                    if (path is null) break;
                    cancellationToken.ThrowIfCancellationRequested();
                    SourceItem? item = null;
                    try
                    {
                        var relative = Path.GetRelativePath(root, path);
                        if (PathSafety.Within(path, settings.Destination) || PathSafety.Within(path, Settings.StateDirectory) || relative.Split('\\').Any(p => p.Equals(".mediarecover", StringComparison.OrdinalIgnoreCase)) || Filter.Excluded(relative, settings)) continue;
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            issue = new(path, "LINK_SKIPPED", "Reparse point not traversed.", "Select the underlying real directory separately if appropriate.");
                        }
                        else if (!settings.IncludeHidden && (attributes & FileAttributes.Hidden) != 0) continue;
                        else if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                        else
                        {
                            var info = new FileInfo(path);
                            var media = Filter.Media(path, settings);
                            var dates = new List<DateValue> { Date("FileSystem.CreationTimeUtc", info.CreationTimeUtc), Date("FileSystem.LastWriteTimeUtc", info.LastWriteTimeUtc) };
                            item = new(PathSafety.Key(Kind + "|" + path.ToUpperInvariant()), source, path, relative, info.Length,
                                Version(info), dates, media ?? "ExcludedType");
                        }
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { issue = Issue(path, e); }
                    if (issue is not null) yield return new(null, issue);
                    if (item is not null) yield return new(item, null);
                    await Task.Yield();
                }
            }
        }
    }
    private static string Version(FileInfo info) => $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{info.CreationTimeUtc.Ticks}";
    private static DateValue Date(string property, DateTime utc) => new(property, utc.ToString("O"), new DateTimeOffset(utc, TimeSpan.Zero), "UTC (filesystem fallback, not capture time)");
    private static ScanIssue Issue(string path, Exception e)
    {
        var error = Errors.Describe(e);
        return new(path, error.Code, error.Message, error.Remedy);
    }
    public Task<bool> IsUnchangedAsync(SourceItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(item.Source.Id))
            throw new RecoveryException("STORAGE_UNAVAILABLE", "Selected source folder/drive is unavailable.", "Reconnect the drive or restore folder access, then retry the run.");
        PathSafety.NoLinks(item.ObjectId);
        var info = new FileInfo(item.ObjectId);
        return Task.FromResult(info.Exists && Version(info) == item.Version);
    }
    public Task<Stream> OpenReadAsync(SourceItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PathSafety.NoLinks(item.ObjectId);
        var nativePath = item.ObjectId.StartsWith(@"\\?\", StringComparison.Ordinal) ? item.ObjectId :
            item.ObjectId.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + item.ObjectId[2..] : @"\\?\" + item.ObjectId;
        var handle = CreateFile(nativePath, 0x80000000, 1, IntPtr.Zero, 3, 0x00200000 | 0x08000000, IntPtr.Zero);
        if (handle.IsInvalid) { var error = new Win32Exception(Marshal.GetLastWin32Error()); handle.Dispose(); throw new IOException(error.Message, unchecked((int)(0x80070000u | (uint)error.NativeErrorCode))); }
        if (!GetFileInformationByHandleEx(handle, 9, out var tag, 8))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error()); handle.Dispose(); throw new IOException(error.Message, error);
        }
        if ((tag.Attributes & 0x400) != 0) { handle.Dispose(); throw new RecoveryException("LINK_BLOCKED", "Source became a reparse point.", "Rescan the source."); }
        return Task.FromResult<Stream>(new FileStream(handle, FileAccess.Read, 1024 * 1024, false));
    }
    [StructLayout(LayoutKind.Sequential)] private struct AttributeTag { public uint Attributes; public uint Tag; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int infoClass, out AttributeTag info, uint size);
}
