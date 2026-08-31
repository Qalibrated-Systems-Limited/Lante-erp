using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/platform")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Platform Admin")]
public class BackupsController(IBackupOperationsService backupOperationsService, ILogger<BackupsController> logger)
    : BaseController
{
    // ── Backups ────────────────────────────────────────────────────────────────

    [HttpGet("backups")]
    public async Task<IActionResult> GetBackups(CancellationToken cancellationToken)
    {
        try
        {
            var statuses = await backupOperationsService.GetStatusAsync(cancellationToken);
            return OkResult(statuses);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Backups status unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("backups/{name}/trigger")]
    public async Task<IActionResult> TriggerBackup(string name, CancellationToken cancellationToken)
    {
        try
        {
            var jobName = await backupOperationsService.TriggerAsync(name, cancellationToken);
            return OkResult(new { jobName }, $"Triggered '{name}'.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResult(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Backup trigger unavailable for {CronJobName}", name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = ex.Message });
        }
    }
}
