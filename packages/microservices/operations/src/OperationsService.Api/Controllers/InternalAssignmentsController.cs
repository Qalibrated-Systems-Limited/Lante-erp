using Microsoft.AspNetCore.Mvc;
using OperationsService.Api.Authorization;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// Service-to-service assignment lookup — other services (e.g. fleet-service composing a vehicle
/// dispatch notification) know an AssignmentId but have no local copy of the assignment itself and
/// no end-user JWT to call the normal Authorize-gated endpoint with. Guarded by X-Internal-Key,
/// same pattern as InternalServiceRequestsController.
/// </summary>
[ApiController]
[Route("internal/assignments")]
[ServiceKeyAuthorize]
public class InternalAssignmentsController(IAssignmentService assignments) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var assignment = await assignments.GetByIdAsync(id);
        if (assignment == null) return NotFound(new { message = "Assignment not found." });

        return Ok(new
        {
            success = true,
            data = new
            {
                id = assignment.Id,
                title = assignment.Title,
                locationName = assignment.LocationName,
            }
        });
    }
}
