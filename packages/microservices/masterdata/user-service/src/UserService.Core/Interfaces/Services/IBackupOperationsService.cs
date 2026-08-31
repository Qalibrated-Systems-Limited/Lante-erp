namespace UserService.Core.Interfaces.Services;

/// <summary>Point-in-time status of one backup CronJob, as reported by the Kubernetes API.</summary>
public record BackupJobStatus(
    string Name,
    string? Schedule,
    bool Suspended,
    bool Active,
    DateTimeOffset? LastScheduleTime,
    DateTimeOffset? LastSuccessfulTime);

/// <summary>
/// Reads status of, and manually triggers, the cluster's backup CronJobs
/// (pgBackRest, logical dumps, restore-verify, secrets-snapshot) for the Platform Admin
/// backups control page. Talks to the Kubernetes API directly — there is no other channel.
/// </summary>
public interface IBackupOperationsService
{
    Task<IReadOnlyList<BackupJobStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <returns>The name of the ad-hoc Job created to run this CronJob's template immediately.</returns>
    /// <exception cref="ArgumentException">cronJobName is not one of the known backup jobs.</exception>
    Task<string> TriggerAsync(string cronJobName, CancellationToken cancellationToken = default);
}
