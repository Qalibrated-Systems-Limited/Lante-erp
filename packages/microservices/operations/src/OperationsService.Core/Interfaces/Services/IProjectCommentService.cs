using OperationsService.Core.DTOs.Governance;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// PR3b — comment threads with mentions on projects, milestones and tasks.
///
/// <para>Mentions are recorded as rows and surfaced through an inbox rather than pushed. Operations
/// has no per-user notification channel — its alerts are role-based — and inventing one for this
/// would be a larger piece of work than the comments themselves.</para>
/// </summary>
public interface IProjectCommentService
{
    /// <summary>Top-level comments for one target, each with its replies nested one level.</summary>
    Task<List<ProjectCommentDto>> GetThreadAsync(string projectId, string? targetType, string? targetId);

    Task<ProjectCommentDto> AddAsync(string projectId, CreateCommentDto dto, string userId, string? userName);
    Task<ProjectCommentDto> UpdateAsync(string commentId, UpdateCommentDto dto, string userId);
    Task DeleteAsync(string commentId, string userId);

    /// <summary>Every mention of this user, newest first, across all projects.</summary>
    Task<List<MentionDto>> GetMyMentionsAsync(string userId, bool unreadOnly = false);
    Task MarkMentionReadAsync(string mentionId, string userId);
    Task<int> MarkAllMentionsReadAsync(string userId);
}
