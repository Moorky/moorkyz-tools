namespace Moorkyz.Tools.MediaRecover.Core;

public interface ISourceProvider
{
    string Kind { get; }
    SourceCapabilities Capabilities { get; }
    IAsyncEnumerable<InventoryEntry> InventoryAsync(SourceSpec source, Settings settings, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(SourceItem item, CancellationToken cancellationToken);
    Task<bool> IsUnchangedAsync(SourceItem item, CancellationToken cancellationToken);
}
public interface IManifestStore : IDisposable
{
    void SaveRun(RunRecord run);
    RunRecord? Run(string id);
    IEnumerable<RunRecord> Runs();
    void SaveInventory(string run, FileRecord record);
    IEnumerable<FileRecord> Inventory(string run);
    void SaveFile(FileRecord record);
    FileRecord? File(string key);
    IEnumerable<FileRecord> Files();
    void AddIssue(string run, ScanIssue issue);
    IEnumerable<ScanIssue> Issues(string run);
}
public interface ISettingsStore { Settings Load(); void Save(Settings settings); }
public interface IIntegrityService { Task<string> HashAsync(string path, CancellationToken token); }
public interface IReportWriter { Task WriteAsync(RunRecord run, IManifestStore store, CancellationToken token); }
public interface IInventoryService { Task<RunRecord> ScanAsync(Settings settings, CancellationToken token); }
public interface ITransferEngine { Task<RunRecord> RecoverAsync(RunRecord run, CancellationToken token); }
public interface IRecoveryCoordinator : IInventoryService, ITransferEngine { void Pause(); void Resume(); }
