using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.DTOs.Departments;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/departments")]
public class DepartmentsController(
    IDepartmentService departmentService)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var depts = await departmentService.GetAllAsync();
        return OkResult(depts);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "departments.manage")]
    public async Task<IActionResult> GetById(string id)
    {
        var dept = await departmentService.GetByIdAsync(id);
        if (dept == null) return NotFoundResult($"Department with ID {id} not found.");
        return OkResult(dept);
    }

    [HttpGet("{id}/users")]
    [Authorize(Policy = "departments.manage")]
    public async Task<IActionResult> GetUsers(string id)
    {
        var users = await departmentService.GetUsersByDepartmentAsync(id);
        return OkResult(users);
    }

    [HttpPost]
    [Authorize(Policy = "departments.manage")]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var dept = await departmentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = dept.Id }, new { Success = true, Data = dept, StatusCode = 201 });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "departments.manage")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDepartmentDto dto)
    {
        var dept = await departmentService.UpdateAsync(id, dto);
        if (dept == null) return NotFoundResult($"Department with ID {id} not found.");
        return OkResult(dept);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "departments.manage")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await departmentService.DeleteAsync(id);
        if (!result) return NotFoundResult($"Department with ID {id} not found.");
        return OkResult(new { message = "Department deleted." });
    }
}
