using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Services;

namespace UserService.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/password-policy")]
public class PasswordPolicyController(
    PasswordPolicyService passwordPolicyService)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetCurrentPolicy()
    {
        var policy = await passwordPolicyService.GetCurrentPolicyAsync();
        if (policy == null) return NotFoundResult("No password policy configured.");
        return OkResult(policy);
    }
}
