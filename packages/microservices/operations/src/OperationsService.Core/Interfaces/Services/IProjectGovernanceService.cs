using OperationsService.Core.DTOs.Governance;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// PR3 — RAID (risks + issues) and change control for a project. Change requests are the only
/// sanctioned way to move an approved baseline, which is what stops PR1's baseline being just another
/// overwritable field.
/// </summary>
public interface IProjectGovernanceService
{
    // Risks
    Task<List<ProjectRiskDto>> GetRisksAsync(string projectId, bool includeClosed = false);
    Task<ProjectRiskDto> AddRiskAsync(string projectId, UpsertProjectRiskDto dto, string userId);
    Task<ProjectRiskDto> UpdateRiskAsync(string riskId, UpsertProjectRiskDto dto, string userId);
    Task DeleteRiskAsync(string riskId, string userId);

    /// <summary>Turns a live risk into an issue, closing the risk as Realised and linking the two.</summary>
    Task<ProjectIssueDto> RealiseRiskAsync(string riskId, RealiseRiskDto dto, string userId);

    // Issues
    Task<List<ProjectIssueDto>> GetIssuesAsync(string projectId, bool includeClosed = false);
    Task<ProjectIssueDto> AddIssueAsync(string projectId, UpsertProjectIssueDto dto, string userId);
    Task<ProjectIssueDto> UpdateIssueAsync(string issueId, UpsertProjectIssueDto dto, string userId);
    Task<ProjectIssueDto> ResolveIssueAsync(string issueId, ResolveIssueDto dto, string userId);

    // Change requests
    Task<List<ChangeRequestDto>> GetChangeRequestsAsync(string projectId);
    Task<ChangeRequestDto> CreateChangeRequestAsync(string projectId, UpsertChangeRequestDto dto, string userId);
    Task<ChangeRequestDto> UpdateChangeRequestAsync(string crId, UpsertChangeRequestDto dto, string userId);
    Task<ChangeRequestDto> SubmitChangeRequestAsync(string crId, string userId);
    Task<ChangeRequestDto> WithdrawChangeRequestAsync(string crId, string userId);

    /// <summary>
    /// Approving re-baselines: milestone baselines shift by the approved days, and a linked budget
    /// version is approved (which is what moves the budget baseline). The previous baseline is
    /// snapshotted onto the request first.
    /// </summary>
    Task<ChangeRequestDto> DecideChangeRequestAsync(string crId, DecideChangeRequestDto dto, string userId);

    Task<GovernanceSummaryDto> GetSummaryAsync(string projectId);
}
