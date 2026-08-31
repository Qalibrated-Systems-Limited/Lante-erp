using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Governance;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// PR3b — comment threads on projects, milestones and tasks, plus the mentions inbox.
///
/// <para>Writing a comment sits behind read permission, not write: commenting is how someone flags a
/// problem on work they can see, and requiring edit rights would silence exactly the people whose
/// observations are worth having. Authorship, not permission, governs editing and deleting.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class ProjectCommentsController(IProjectCommentService comments) : ControllerBase
{
    private string UserId   => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    private static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    [HttpGet("api/v{version:apiVersion}/projects/{projectId:guid}/comments")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<ProjectCommentDto>>>> GetThread(
        Guid projectId, [FromQuery] string? targetType, [FromQuery] string? targetId)
        => Ok(await comments.GetThreadAsync(projectId.ToString(), targetType, targetId));

    [HttpPost("api/v{version:apiVersion}/projects/{projectId:guid}/comments")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectCommentDto>>> Add(
        Guid projectId, [FromBody] CreateCommentDto dto)
        => Ok(await comments.AddAsync(projectId.ToString(), dto, UserId, UserName));

    [HttpPut("api/v{version:apiVersion}/comments/{commentId:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectCommentDto>>> Update(
        Guid commentId, [FromBody] UpdateCommentDto dto)
        => Ok(await comments.UpdateAsync(commentId.ToString(), dto, UserId));

    [HttpDelete("api/v{version:apiVersion}/comments/{commentId:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<IActionResult> Delete(Guid commentId)
    {
        await comments.DeleteAsync(commentId.ToString(), UserId);
        return NoContent();
    }

    // ── Mentions inbox ───────────────────────────────────────────────────────
    // Not project-scoped: the useful question is "where have I been mentioned", across everything.

    [HttpGet("api/v{version:apiVersion}/my/mentions")]
    public async Task<ActionResult<ApiResponse<List<MentionDto>>>> MyMentions([FromQuery] bool unreadOnly = false)
        => Ok(await comments.GetMyMentionsAsync(UserId, unreadOnly));

    [HttpPost("api/v{version:apiVersion}/my/mentions/{mentionId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid mentionId)
    {
        await comments.MarkMentionReadAsync(mentionId.ToString(), UserId);
        return NoContent();
    }

    [HttpPost("api/v{version:apiVersion}/my/mentions/read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllRead()
        => Ok(await comments.MarkAllMentionsReadAsync(UserId));
}
