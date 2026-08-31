using LicenseService.Core.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace LicenseService.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected IActionResult OkResult<T>(T data, string? message = null) =>
        Ok(new ApiResponse<T> { Success = true, Data = data, Message = message, StatusCode = 200 });

    protected IActionResult CreatedResult<T>(T data, string? message = null) =>
        StatusCode(201, new ApiResponse<T> { Success = true, Data = data, Message = message, StatusCode = 201 });

    protected IActionResult BadRequestResult(string message, List<string>? errors = null) =>
        BadRequest(new ApiResponse { Success = false, Message = message, Errors = errors, StatusCode = 400 });

    protected IActionResult UnauthorizedResult(string message = "Unauthorized") =>
        Unauthorized(new ApiResponse { Success = false, Message = message, StatusCode = 401 });

    protected IActionResult NotFoundResult(string message = "Not found") =>
        NotFound(new ApiResponse { Success = false, Message = message, StatusCode = 404 });

    protected IActionResult InternalServerErrorResult(string message = "Internal server error") =>
        StatusCode(500, new ApiResponse { Success = false, Message = message, StatusCode = 500 });
}
