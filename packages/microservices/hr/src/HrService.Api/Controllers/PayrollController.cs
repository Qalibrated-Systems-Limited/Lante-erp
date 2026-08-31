using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>
/// H5 (HR-007/HR-008, P7 + P8) — pay configuration: grades, salary structures, payroll periods, salary
/// assignments, statutory rate tables and the deduction catalogue.
/// <para><b>Everything here sits behind the ring-fenced payroll permissions</b> (<c>hr.payroll.read</c> /
/// <c>hr.payroll.write</c> / <c>hr.payroll.approve</c>), not the general <c>hr.*</c> tier: what someone earns
/// must not be visible to everyone who can read an employee record. Approving a salary needs
/// <c>hr.payroll.approve</c>, and the service additionally refuses to let the proposer approve their own
/// proposal.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class PayrollController(IPayrollService service, ISalaryIncrementService increments) : ControllerBase
{
    private string? Schema => User.FindFirstValue("schema");

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

    [HttpGet("payroll/summary")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── Job grades ──
    [HttpGet("payroll/grades")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListGrades([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListGradesAsync(includeInactive) });

    [HttpPost("payroll/grades")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreateGrade([FromBody] SaveJobGradeDto dto)
        => Act(await service.CreateGradeAsync(dto, UserId));

    [HttpPut("payroll/grades/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdateGrade(string id, [FromBody] SaveJobGradeDto dto)
        => Act(await service.UpdateGradeAsync(id, dto, UserId));

    // ── Salary structures and components (P7 steps 7.1–7.3) ──
    [HttpGet("payroll/structures")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListStructures([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListStructuresAsync(includeInactive) });

    [HttpGet("payroll/structures/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> GetStructure(string id)
    {
        var s = await service.GetStructureAsync(id);
        return s is null ? NotFound(new { message = "Salary structure not found." }) : Ok(new { data = s });
    }

    [HttpPost("payroll/structures")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreateStructure([FromBody] SaveSalaryStructureDto dto)
        => Act(await service.CreateStructureAsync(dto, UserId));

    [HttpPut("payroll/structures/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdateStructure(string id, [FromBody] SaveSalaryStructureDto dto)
        => Act(await service.UpdateStructureAsync(id, dto, UserId));

    /// <summary>Installs a standard structure with its statutory lines, GL-mapped where finance has the account.
    /// Idempotent.</summary>
    [HttpPost("payroll/structures/seed-default")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SeedStructure() => Act(await service.SeedDefaultStructureAsync(UserId));

    [HttpPost("payroll/structures/{id}/components")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> AddComponent(string id, [FromBody] SaveSalaryComponentDto dto)
        => Act(await service.AddComponentAsync(id, dto, UserId));

    [HttpPut("payroll/components/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdateComponent(string id, [FromBody] SaveSalaryComponentDto dto)
        => Act(await service.UpdateComponentAsync(id, dto, UserId));

    /// <summary>Deactivates a component. It is never deleted — a component that has been paid is pay history.</summary>
    [HttpPost("payroll/components/{id}/deactivate")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> DeactivateComponent(string id)
        => Act(await service.DeactivateComponentAsync(id, UserId));

    // ── GL mapping (P7 step 7.5) ──
    /// <summary>Finance's posting accounts, read live. Empty when finance is unreachable.</summary>
    [HttpGet("payroll/gl-accounts")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListGlAccounts(CancellationToken ct)
        => Ok(new { data = await service.ListGlAccountsAsync(ct) });

    [HttpPost("payroll/components/{id}/gl-account")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> MapComponent(string id, [FromBody] MapGlAccountDto dto, CancellationToken ct)
        => Act(await service.MapComponentAccountAsync(id, dto, UserId, ct));

    [HttpPost("payroll/statutory-rates/{id}/gl-account")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> MapStatutory(string id, [FromBody] MapGlAccountDto dto, CancellationToken ct)
        => Act(await service.MapStatutoryAccountAsync(id, dto, UserId, ct));

    [HttpPost("payroll/deduction-types/{id}/gl-account")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> MapDeductionType(string id, [FromBody] MapGlAccountDto dto, CancellationToken ct)
        => Act(await service.MapDeductionTypeAccountAsync(id, dto, UserId, ct));

    // ── Payroll periods ──
    [HttpGet("payroll/periods")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListPeriods([FromQuery] int? year)
        => Ok(new { data = await service.ListPeriodsAsync(year) });

    /// <summary>Creates the twelve monthly periods for a year. Idempotent per period.</summary>
    [HttpPost("payroll/periods/generate")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> GeneratePeriods([FromBody] GeneratePeriodsDto dto)
        => Act(await service.GeneratePeriodsAsync(dto, UserId));

    // ── Employee salary assignments (P7 step 7.4) ──
    [HttpGet("payroll/salaries")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListSalaries(
        [FromQuery] string? employeeId, [FromQuery] string? status, [FromQuery] bool currentOnly = false)
        => Ok(new { data = await service.ListSalariesAsync(employeeId, status, currentOnly) });

    [HttpPost("payroll/salaries")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> ProposeSalary([FromBody] ProposeSalaryDto dto)
        => Act(await service.ProposeSalaryAsync(dto, UserId));

    /// <summary>MD approval or rejection. The proposer cannot approve their own proposal (P7 step 7.4).</summary>
    [HttpPost("payroll/salaries/{id}/decide")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> DecideSalary(string id, [FromBody] DecideSalaryDto dto)
        => Act(await service.DecideSalaryAsync(id, dto, UserId, UserName));

    // ── PAYE bands (P7 step 7.3) ──
    [HttpGet("payroll/paye-bands")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListPayeBands([FromQuery] DateTime? asOf, [FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListPayeBandsAsync(asOf, includeInactive) });

    /// <summary>Installs the PAYE scale, every band flagged as needing confirmation. Idempotent.</summary>
    [HttpPost("payroll/paye-bands/seed")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SeedPayeBands() => Act(await service.SeedPayeBandsAsync(UserId));

    [HttpPost("payroll/paye-bands")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreatePayeBand([FromBody] SavePayeTaxBandDto dto)
        => Act(await service.SavePayeBandAsync(null, dto, UserId));

    [HttpPut("payroll/paye-bands/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdatePayeBand(string id, [FromBody] SavePayeTaxBandDto dto)
        => Act(await service.SavePayeBandAsync(id, dto, UserId));

    // ── Statutory rates ──
    [HttpGet("payroll/statutory-rates")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListStatutoryRates([FromQuery] DateTime? asOf, [FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListStatutoryRatesAsync(asOf, includeInactive) });

    /// <summary>Installs NSSF, SHA, the Housing Levy, HELB and personal relief, all flagged for confirmation.</summary>
    [HttpPost("payroll/statutory-rates/seed")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SeedStatutoryRates() => Act(await service.SeedStatutoryRatesAsync(UserId));

    [HttpPost("payroll/statutory-rates")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreateStatutoryRate([FromBody] SaveStatutoryRateDto dto)
        => Act(await service.SaveStatutoryRateAsync(null, dto, UserId));

    [HttpPut("payroll/statutory-rates/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdateStatutoryRate(string id, [FromBody] SaveStatutoryRateDto dto)
        => Act(await service.SaveStatutoryRateAsync(id, dto, UserId));

    /// <summary>Records that a human has checked seeded figures against the current Finance Act. Needs the
    /// approver tier — signing off the tax rates the whole payroll runs on is not a clerical act.</summary>
    [HttpPost("payroll/statutory-rates/confirm")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> ConfirmRates([FromBody] ConfirmRatesDto? dto)
        => Act(await service.ConfirmRatesAsync(dto ?? new ConfirmRatesDto(), UserId, UserName));

    // ── Deduction catalogue and applications (P8) ──
    [HttpGet("payroll/deduction-types")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListDeductionTypes([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListDeductionTypesAsync(includeInactive) });

    [HttpPost("payroll/deduction-types/seed-defaults")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> SeedDeductionTypes()
        => Act(await service.SeedDefaultDeductionTypesAsync(UserId));

    [HttpPost("payroll/deduction-types")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> CreateDeductionType([FromBody] SavePayrollDeductionTypeDto dto)
        => Act(await service.CreateDeductionTypeAsync(dto, UserId));

    [HttpPut("payroll/deduction-types/{id}")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> UpdateDeductionType(string id, [FromBody] SavePayrollDeductionTypeDto dto)
        => Act(await service.UpdateDeductionTypeAsync(id, dto, UserId));

    [HttpGet("payroll/deductions")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListDeductions([FromQuery] string? employeeId, [FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListDeductionsAsync(employeeId, includeInactive) });

    [HttpPost("payroll/deductions")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> AddDeduction([FromBody] AddDeductionDto dto)
        => Act(await service.AddDeductionAsync(dto, UserId));

    /// <summary>Stops a deduction. The row is closed, never deleted (P8).</summary>
    [HttpPost("payroll/deductions/{id}/stop")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> StopDeduction(string id, [FromBody] StopDeductionDto? dto)
        => Act(await service.StopDeductionAsync(id, dto ?? new StopDeductionDto(), UserId));

    // ── Salary increments (H8, P13, HR-013) ──
    // HR proposes, the MD approves. Two hard gates (HR-029 mandatory training, HR-035 professional
    // certification) are enforced by the service through H7 — they are not the UI's to skip.

    [HttpGet("payroll/increments")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> ListIncrements([FromQuery] string? status, [FromQuery] string? employeeId)
        => Ok(new { data = await increments.ListAsync(status, employeeId) });

    /// <summary>Current salary, the eligibility gates, and the earliest period a rise could start from.</summary>
    [HttpGet("payroll/increments/preview")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> PreviewIncrement([FromQuery] string employeeId, CancellationToken ct)
    {
        var preview = await increments.PreviewAsync(employeeId, ct);
        return preview is null ? NotFound(new { message = "Employee not found." }) : Ok(new { data = preview });
    }

    [HttpPost("payroll/increments")]
    [Authorize(Policy = "Permission:hr.payroll.write")]
    public async Task<IActionResult> ProposeIncrement([FromBody] ProposeIncrementDto dto, CancellationToken ct)
        => Act(await increments.ProposeAsync(dto, UserId, ct));

    /// <summary>MD decision. Approval re-checks the gates and writes the new salary assignment.</summary>
    [HttpPost("payroll/increments/{id}/decide")]
    [Authorize(Policy = "Permission:hr.payroll.approve")]
    public async Task<IActionResult> DecideIncrement(string id, [FromBody] DecideIncrementDto dto, CancellationToken ct)
        => Act(await increments.DecideAsync(id, dto, Schema, UserId, UserName, ct));

    private IActionResult Act(PayrollActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
