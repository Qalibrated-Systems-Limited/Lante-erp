using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Governance;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>PR3b — see <see cref="IProjectCommentService"/>.</summary>
public class ProjectCommentService(
    IGenericRepository<Project> projects,
    IGenericRepository<ProjectComment> comments,
    IGenericRepository<CommentMention> mentions) : IProjectCommentService
{
    public async Task<List<ProjectCommentDto>> GetThreadAsync(string projectId, string? targetType, string? targetId)
    {
        var q = comments.Query().Where(c => c.ProjectId == projectId && !c.IsDeleted);

        // No target filter means the whole project's activity, which is what the project-level view
        // wants; a target narrows it to one milestone or task.
        if (!string.IsNullOrWhiteSpace(targetType) &&
            Enum.TryParse<CommentTargetType>(targetType, true, out var tt))
            q = q.Where(c => c.TargetType == tt);
        if (!string.IsNullOrWhiteSpace(targetId))
            q = q.Where(c => c.TargetId == targetId);

        var all = await q.OrderBy(c => c.CreatedAt).ToListAsync();
        if (all.Count == 0) return [];

        var ids = all.Select(c => c.Id).ToList();
        var mentionRows = await mentions.Query().Where(m => ids.Contains(m.CommentId) && !m.IsDeleted).ToListAsync();
        var byComment = mentionRows.GroupBy(m => m.CommentId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.MentionedUserId).ToList());

        var roots = all.Where(c => c.ParentCommentId is null).Select(c => Map(c, byComment)).ToList();
        var repliesByParent = all.Where(c => c.ParentCommentId is not null).GroupBy(c => c.ParentCommentId!);

        foreach (var group in repliesByParent)
        {
            var parent = roots.FirstOrDefault(r => r.Id == group.Key);
            // A reply whose parent was hard-removed would otherwise vanish silently; keeping it at
            // top level is better than dropping someone's message on the floor.
            if (parent is null)
                roots.AddRange(group.Select(c => Map(c, byComment)));
            else
                parent.Replies.AddRange(group.OrderBy(c => c.CreatedAt).Select(c => Map(c, byComment)));
        }

        return roots.OrderBy(r => r.CreatedAt).ToList();
    }

    public async Task<ProjectCommentDto> AddAsync(string projectId, CreateCommentDto dto, string userId, string? userName)
    {
        _ = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        if (string.IsNullOrWhiteSpace(dto.Body))
            throw new InvalidOperationException("A comment needs something in it.");

        var targetType = Enum.TryParse<CommentTargetType>(dto.TargetType, true, out var tt)
            ? tt : CommentTargetType.Project;
        var targetId = string.IsNullOrWhiteSpace(dto.TargetId) ? projectId : dto.TargetId;

        if (dto.ParentCommentId is not null)
        {
            var parent = await comments.GetByIdAsync(dto.ParentCommentId)
                ?? throw new KeyNotFoundException("The comment being replied to no longer exists.");
            if (parent.ProjectId != projectId)
                throw new InvalidOperationException("That comment belongs to a different project.");
            // Depth-1 only, matching the subtask rule. Replying to a reply attaches to the same root
            // rather than erroring — the user's intent is obvious and refusing would just annoy them.
            if (parent.ParentCommentId is not null)
            {
                dto.ParentCommentId = parent.ParentCommentId;
                targetType = parent.TargetType;
                targetId   = parent.TargetId;
            }
            else
            {
                targetType = parent.TargetType;
                targetId   = parent.TargetId;
            }
        }

        var comment = await comments.CreateAsync(new ProjectComment
        {
            ProjectId       = projectId,
            TargetType      = targetType,
            TargetId        = targetId,
            Body            = dto.Body.Trim(),
            AuthorUserId    = userId,
            AuthorName      = userName,
            ParentCommentId = dto.ParentCommentId,
            CreatedBy       = userId,
            UpdatedBy       = userId,
        });

        await SyncMentionsAsync(comment, dto.MentionedUserIds, userId);
        return Map(comment, new Dictionary<string, List<string>>
        {
            [comment.Id] = Clean(dto.MentionedUserIds, userId),
        });
    }

    public async Task<ProjectCommentDto> UpdateAsync(string commentId, UpdateCommentDto dto, string userId)
    {
        var comment = await comments.GetByIdAsync(commentId)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");
        // Editing someone else's words is not an ownership question that a permission should be able
        // to override — nobody edits another person's comment.
        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only edit your own comments.");
        if (comment.IsRedacted)
            throw new InvalidOperationException("This comment was deleted.");
        if (string.IsNullOrWhiteSpace(dto.Body))
            throw new InvalidOperationException("A comment needs something in it.");

        comment.Body      = dto.Body.Trim();
        comment.EditedAt  = DateTime.UtcNow;
        comment.UpdatedBy = userId;
        comment.UpdatedAt = DateTime.UtcNow;
        await comments.UpdateAsync(comment);

        if (dto.MentionedUserIds is not null)
            await SyncMentionsAsync(comment, dto.MentionedUserIds, userId);

        var current = await mentions.Query()
            .Where(m => m.CommentId == comment.Id && !m.IsDeleted)
            .Select(m => m.MentionedUserId).ToListAsync();
        return Map(comment, new Dictionary<string, List<string>> { [comment.Id] = current });
    }

    public async Task DeleteAsync(string commentId, string userId)
    {
        var comment = await comments.GetByIdAsync(commentId)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");
        if (comment.AuthorUserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own comments.");

        var hasReplies = await comments.Query()
            .AnyAsync(c => c.ParentCommentId == comment.Id && !c.IsDeleted);

        if (hasReplies)
        {
            // Tombstone: the replies underneath still need something to hang off.
            comment.IsRedacted = true;
            comment.Body       = string.Empty;
        }
        else
        {
            comment.IsDeleted = true;
        }

        comment.UpdatedBy = userId;
        comment.UpdatedAt = DateTime.UtcNow;
        await comments.UpdateAsync(comment);

        // A deleted comment's mentions should stop showing up in anyone's inbox.
        foreach (var m in await mentions.Query().Where(m => m.CommentId == comment.Id && !m.IsDeleted).ToListAsync())
        {
            m.IsDeleted = true;
            m.UpdatedBy = userId;
            m.UpdatedAt = DateTime.UtcNow;
            await mentions.UpdateAsync(m);
        }
    }

    public async Task<List<MentionDto>> GetMyMentionsAsync(string userId, bool unreadOnly = false)
    {
        var q = mentions.Query().Where(m => m.MentionedUserId == userId && !m.IsDeleted);
        if (unreadOnly) q = q.Where(m => m.ReadAt == null);

        var rows = await q.OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync();
        if (rows.Count == 0) return [];

        var commentIds = rows.Select(r => r.CommentId).Distinct().ToList();
        var projectIds = rows.Select(r => r.ProjectId).Distinct().ToList();

        var commentRows = await comments.Query().Where(c => commentIds.Contains(c.Id) && !c.IsDeleted).ToListAsync();
        var projectRows = await projects.Query().Where(p => projectIds.Contains(p.Id)).ToListAsync();

        return rows
            .Select(m =>
            {
                var c = commentRows.FirstOrDefault(x => x.Id == m.CommentId);
                if (c is null) return null;   // comment gone — nothing useful to show
                return new MentionDto
                {
                    Id = m.Id, CommentId = m.CommentId, ProjectId = m.ProjectId,
                    ProjectName = projectRows.FirstOrDefault(p => p.Id == m.ProjectId)?.Name,
                    TargetType = c.TargetType.ToString(), TargetId = c.TargetId,
                    Body = c.Body, AuthorName = c.AuthorName,
                    CreatedAt = m.CreatedAt, ReadAt = m.ReadAt,
                };
            })
            .Where(x => x is not null)!
            .ToList()!;
    }

    public async Task MarkMentionReadAsync(string mentionId, string userId)
    {
        var mention = await mentions.GetByIdAsync(mentionId)
            ?? throw new KeyNotFoundException("Mention not found.");
        if (mention.MentionedUserId != userId)
            throw new UnauthorizedAccessException("That mention is not yours.");
        if (mention.ReadAt != null) return;

        mention.ReadAt    = DateTime.UtcNow;
        mention.UpdatedBy = userId;
        mention.UpdatedAt = DateTime.UtcNow;
        await mentions.UpdateAsync(mention);
    }

    public async Task<int> MarkAllMentionsReadAsync(string userId)
    {
        var unread = await mentions.Query()
            .Where(m => m.MentionedUserId == userId && !m.IsDeleted && m.ReadAt == null)
            .ToListAsync();
        foreach (var m in unread)
        {
            m.ReadAt    = DateTime.UtcNow;
            m.UpdatedBy = userId;
            m.UpdatedAt = DateTime.UtcNow;
            await mentions.UpdateAsync(m);
        }
        return unread.Count;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Brings the mention rows in line with the list the client sent. Additions are inserted and
    /// removals are soft-deleted; an existing mention is left untouched so re-editing a comment does
    /// not resurface it as unread for someone who has already read it.
    /// </summary>
    private async Task SyncMentionsAsync(ProjectComment comment, List<string> wanted, string userId)
    {
        var target = Clean(wanted, comment.AuthorUserId);
        var existing = await mentions.Query().Where(m => m.CommentId == comment.Id && !m.IsDeleted).ToListAsync();

        foreach (var stale in existing.Where(m => !target.Contains(m.MentionedUserId)))
        {
            stale.IsDeleted = true;
            stale.UpdatedBy = userId;
            stale.UpdatedAt = DateTime.UtcNow;
            await mentions.UpdateAsync(stale);
        }

        foreach (var add in target.Where(id => existing.All(m => m.MentionedUserId != id)))
        {
            await mentions.CreateAsync(new CommentMention
            {
                CommentId       = comment.Id,
                MentionedUserId = add,
                ProjectId       = comment.ProjectId,
                CreatedBy       = userId,
                UpdatedBy       = userId,
            });
        }
    }

    /// <summary>Dedupes, drops blanks, and never mentions the author to themselves.</summary>
    private static List<string> Clean(List<string>? ids, string authorUserId) =>
        (ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => id != authorUserId)
            .Distinct()
            .ToList();

    private static ProjectCommentDto Map(ProjectComment c, IReadOnlyDictionary<string, List<string>> mentionsByComment) => new()
    {
        Id = c.Id, ProjectId = c.ProjectId,
        TargetType = c.TargetType.ToString(), TargetId = c.TargetId,
        Body = c.IsRedacted ? "[deleted]" : c.Body,
        AuthorUserId = c.AuthorUserId, AuthorName = c.AuthorName,
        ParentCommentId = c.ParentCommentId,
        CreatedAt = c.CreatedAt, EditedAt = c.EditedAt, IsRedacted = c.IsRedacted,
        MentionedUserIds = mentionsByComment.TryGetValue(c.Id, out var m) ? m : [],
    };
}
