using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR3b — a comment on a project, milestone or task. One table for all three rather than three
/// near-identical ones: the rules (who may edit, how replies nest, how mentions resolve) are the same
/// everywhere, and splitting them would mean maintaining that logic in triplicate.
/// </summary>
public class ProjectComment : BaseEntity
{
    /// <summary>
    /// Always set, even for a comment on a task. Comments are read and permissioned per project, and
    /// carrying the project here means the common query does not have to walk task → milestone →
    /// project to find out who is allowed to see it.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    public CommentTargetType TargetType { get; set; }
    public string TargetId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string  AuthorUserId { get; set; } = string.Empty;
    /// <summary>Denormalised so a thread renders without a user lookup per row.</summary>
    public string? AuthorName   { get; set; }

    /// <summary>
    /// Replies nest one level only, the same cap the module already applies to subtasks. Deeper
    /// threads are hard to follow in a side panel and nothing here needs them.
    /// </summary>
    public string? ParentCommentId { get; set; }

    /// <summary>Set on edit so a changed comment is visibly a changed comment.</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// A deleted comment that has replies is kept as a tombstone rather than removed, so the replies
    /// underneath it do not lose the thing they were answering.
    /// </summary>
    public bool IsRedacted { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<CommentMention> Mentions { get; set; } = new List<CommentMention>();
}

/// <summary>
/// PR3b — one person mentioned in one comment. A row per mention rather than a parsed-on-read body:
/// "what needs my attention" has to be answerable by a query, and re-parsing every comment body to
/// find out would not survive the first project with a real comment history.
/// </summary>
public class CommentMention : BaseEntity
{
    public string CommentId       { get; set; } = string.Empty;
    public string MentionedUserId { get; set; } = string.Empty;

    /// <summary>Denormalised so the cross-project mentions inbox is a single indexed read.</summary>
    public string ProjectId { get; set; } = string.Empty;

    public DateTime? ReadAt { get; set; }

    public ProjectComment Comment { get; set; } = null!;
}
