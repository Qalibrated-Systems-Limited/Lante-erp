using HSEService.Core.DTOs.Rams;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Core.Services;

public class RamsWorkflowService(IHseCrudService<Rams> rams) : IRamsWorkflowService
{
    public async Task<Rams> UploadNewVersionAsync(CreateRamsDto dto, string uploadedByUserId)
    {
        var existing = await rams.FindAsync(r => r.SiteId == dto.SiteId && r.Title == dto.Title);
        var nextVersion = existing.Count == 0 ? 1 : existing.Max(r => r.Version) + 1;

        var record = new Rams
        {
            SiteId = dto.SiteId,
            SiteName = dto.SiteName,
            SubcontractorId = dto.SubcontractorId,
            SubcontractorName = dto.SubcontractorName,
            Title = dto.Title,
            Version = nextVersion,
            FileUrl = dto.FileUrl,
            IssueNotes = dto.IssueNotes,
            UploadedByUserId = uploadedByUserId,
        };
        return await rams.CreateAsync(record);
    }

    public async Task<List<Rams>> GetVersionsForSiteAsync(string siteId) =>
        (await rams.FindAsync(r => r.SiteId == siteId)).OrderByDescending(r => r.CreatedAt).ToList();
}
