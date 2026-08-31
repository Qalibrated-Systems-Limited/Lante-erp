using Microsoft.AspNetCore.Mvc;
using UserService.Core.DTOs.Common;

namespace UserService.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected IActionResult OkResult<T>(T data, string? message = null)
    {
        return base.Ok(new ApiResponse<T> { Success = true, Data = data, Message = message, StatusCode = 200 });
    }

    protected IActionResult CreatedResult<T>(T data, string? message = null)
    {
        return StatusCode(201, new ApiResponse<T> { Success = true, Data = data, Message = message, StatusCode = 201 });
    }

    protected IActionResult BadRequestResult(string message, List<string>? errors = null)
    {
        return base.BadRequest(new ApiResponse { Success = false, Message = message, Errors = errors, StatusCode = 400 });
    }

    protected IActionResult UnauthorizedResult(string message = "Unauthorized")
    {
        return base.Unauthorized(new ApiResponse { Success = false, Message = message, StatusCode = 401 });
    }

    protected IActionResult ForbiddenResult(string message = "Forbidden")
    {
        return StatusCode(403, new ApiResponse { Success = false, Message = message, StatusCode = 403 });
    }

    protected IActionResult NotFoundResult(string message = "Not found")
    {
        return base.NotFound(new ApiResponse { Success = false, Message = message, StatusCode = 404 });
    }

    protected IActionResult ConflictResult(string message = "Conflict")
    {
        return StatusCode(409, new ApiResponse { Success = false, Message = message, StatusCode = 409 });
    }

    protected IActionResult InternalServerErrorResult(string message = "Internal server error")
    {
        return StatusCode(500, new ApiResponse { Success = false, Message = message, StatusCode = 500 });
    }
}
