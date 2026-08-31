namespace OperationsService.Core.DTOs.Governance;

// PR3b — comment threads with mentions on projects, milestones and tasks.

public class ProjectCommentDto
{
    public string  Id           { get; set; } = string.Empty;
    public string  ProjectId    { get; set; } = string.Empty;
    public string  TargetType   { get; set; } = string.Empty;
    public string  TargetId     { get; set; } = string.Empty;
    public string  Body         { get; set; } = string.Empty;
    public string  AuthorUserId { get; set; } = string.Empty;
    public string? AuthorName   { get; set; }
    public string? ParentCommentId { get; set; }
    public DateTime  CreatedAt  { get; set; }
    public DateTime? EditedAt   { get; set; }
    public bool      IsRedacted { get; set; }
    public List<string> MentionedUserIds { get; set; } = new();
    /// <summary>Replies, one level deep. Empty on a reply itself.</summary>
    public List<ProjectCommentDto> Replies { get; set; } = new();
}

public class CreateCommentDto
{
    public string  TargetType { get; set; } = "Project";
    public string  TargetId   { get; set; } = string.Empty;
    public string  Body       { get; set; } = string.Empty;
    public string? ParentCommentId { get; set; }

    /// <summary>
    /// Sent explicitly by the client rather than parsed out of the body. The composer's picker
    /// already knows which user it inserted; re-deriving that server-side by matching names in prose
    /// would mis-tag anyone whose name appears in ordinary text and break on duplicates.
    /// </summary>
    public List<string> MentionedUserIds { get; set; } = new();
}

public class UpdateCommentDto
{
    public string Body { get; set; } = string.Empty;
    public List<string>? MentionedUserIds { get; set; }
}

/// <summary>One row in the "what needs my attention" inbox, across every project.</summary>
public class MentionDto
{
    public string  Id         { get; set; } = string.Empty;   // the mention row, not the comment
    public string  CommentId  { get; set; } = string.Empty;
    public string  ProjectId  { get; set; } = string.Empty;
    public string? ProjectName{ get; set; }
    public string  TargetType { get; set; } = string.Empty;
    public string  TargetId   { get; set; } = string.Empty;
    public string  Body       { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public DateTime  CreatedAt { get; set; }
    public DateTime? ReadAt    { get; set; }
}
