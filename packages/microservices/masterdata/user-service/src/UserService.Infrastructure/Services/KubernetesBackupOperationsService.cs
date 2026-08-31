using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UserService.Core.Interfaces.Services;

namespace UserService.Infrastructure.Services;

/// <summary>
/// Talks to the in-cluster Kubernetes API to report on and trigger the platform's backup
/// CronJobs. Kubernetes RBAC cannot restrict *which* Job name the `create` verb is allowed to
/// make, so <see cref="KnownCronJobNames"/> is the real security boundary — anything not on this
/// list is rejected before it ever reaches the API.
/// </summary>
public class KubernetesBackupOperationsService : IBackupOperationsService
{
    public static readonly IReadOnlyList<string> KnownCronJobNames = new[]
    {
        "lante-backup-pgbackrest",
        "lante-backup-logical-dumps",
        "lante-backup-verify",
        "lante-backup-secrets-snapshot",
    };

    private readonly string _namespace;
    private readonly ILogger<KubernetesBackupOperationsService> _logger;

    public KubernetesBackupOperationsService(IConfiguration configuration, ILogger<KubernetesBackupOperationsService> logger)
    {
        _namespace = configuration["Kubernetes:Namespace"] ?? "new-erp";
        _logger = logger;
    }

    public async Task<IReadOnlyList<BackupJobStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var statuses = new List<BackupJobStatus>();

        foreach (var name in KnownCronJobNames)
        {
            V1CronJob cronJob;
            try
            {
                cronJob = await client.BatchV1.ReadNamespacedCronJobAsync(name, _namespace, cancellationToken: cancellationToken);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Not deployed in this environment (e.g. backups.enabled=false) — omit rather than fail the whole page.
                continue;
            }

            statuses.Add(new BackupJobStatus(
                Name: name,
                Schedule: cronJob.Spec?.Schedule,
                Suspended: cronJob.Spec?.Suspend ?? false,
                Active: cronJob.Status?.Active is { Count: > 0 },
                LastScheduleTime: cronJob.Status?.LastScheduleTime,
                LastSuccessfulTime: cronJob.Status?.LastSuccessfulTime));
        }

        return statuses;
    }

    public async Task<string> TriggerAsync(string cronJobName, CancellationToken cancellationToken = default)
    {
        if (!KnownCronJobNames.Contains(cronJobName))
            throw new ArgumentException($"'{cronJobName}' is not a recognized backup job.", nameof(cronJobName));

        var client = CreateClient();

        V1CronJob cronJob;
        try
        {
            cronJob = await client.BatchV1.ReadNamespacedCronJobAsync(cronJobName, _namespace, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"CronJob '{cronJobName}' is not deployed in this environment.");
        }

        var jobName = $"{cronJobName}-manual-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var job = new V1Job
        {
            ApiVersion = "batch/v1",
            Kind = "Job",
            Metadata = new V1ObjectMeta
            {
                Name = jobName,
                NamespaceProperty = _namespace,
                Labels = new Dictionary<string, string>
                {
                    ["app.kubernetes.io/name"] = cronJobName,
                    ["lante.io/triggered-by"] = "platform-admin",
                },
                OwnerReferences = new List<V1OwnerReference>
                {
                    new(apiVersion: "batch/v1", kind: "CronJob", name: cronJob.Metadata.Name, uid: cronJob.Metadata.Uid)
                    {
                        Controller = false,
                        BlockOwnerDeletion = false,
                    },
                },
            },
            Spec = cronJob.Spec!.JobTemplate.Spec,
        };

        var created = await client.BatchV1.CreateNamespacedJobAsync(job, _namespace, cancellationToken: cancellationToken);
        _logger.LogInformation("Manually triggered backup job {JobName} from CronJob {CronJobName}", created.Metadata.Name, cronJobName);
        return created.Metadata.Name;
    }

    private Kubernetes CreateClient()
    {
        try
        {
            return new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not build an in-cluster Kubernetes client config.");
            throw new InvalidOperationException("The backups API is only reachable from inside the cluster.", ex);
        }
    }
}
