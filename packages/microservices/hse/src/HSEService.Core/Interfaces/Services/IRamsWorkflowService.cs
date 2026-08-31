using HSEService.Core.DTOs.Rams;
using HSEService.Core.Entities;

namespace HSEService.Core.Interfaces.Services;

// HSE-002: uploading against an existing Site+Title auto-increments Version rather than
// overwriting — the version history stays queryable via GetVersionsForSiteAsync.
public interface IRamsWorkflowService
{
    Task<Rams> UploadNewVersionAsync(CreateRamsDto dto, string uploadedByUserId);
    Task<List<Rams>> GetVersionsForSiteAsync(string siteId);
}
