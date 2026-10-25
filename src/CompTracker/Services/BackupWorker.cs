namespace CompTracker.Services;

// Takes a snapshot every interval, but only if something was saved since the last one, plus a final one on shutdown.
public class BackupWorker(BackupService backups, ChangeNotifier notifier, ILogger<BackupWorker> log) : BackgroundService
{
    private volatile bool dirty;

    private void MarkDirty() => dirty = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        notifier.Changed += MarkDirty;
        try
        {
            using var timer = new PeriodicTimer(backups.Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await BackUpIfDirtyAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            notifier.Changed -= MarkDirty;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await BackUpIfDirtyAsync(CancellationToken.None);
    }

    private async Task BackUpIfDirtyAsync(CancellationToken token)
    {
        if (!dirty)
            return;
        dirty = false;
        try
        {
            var file = await backups.CreateAsync(token);
            log.LogInformation("Backed up the database to {File}", Path.Combine(backups.Folder, file.Name));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            dirty = true; // try again next time
            log.LogError(ex, "Backup to {Folder} failed", backups.Folder);
        }
    }
}
