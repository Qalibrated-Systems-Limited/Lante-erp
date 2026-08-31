using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Training;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-005: HSE training register — first aid, fire safety, working at heights — certificates
// with expiry dates. Renewal alerts are handled by HseAlertsBackgroundService.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-training-records")]
public class HseTrainingRecordsController(IHseCrudService<HseTrainingRecord> trainingRecords, IHseTrainingRecordRepository trainingRecordRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<HseTrainingRecordReadDto>>>> GetAll([FromQuery] HseTrainingRecordFilterParameters parameters)
    {
        var paged = await trainingRecordRepository.GetPagedAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<HseTrainingRecordReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<HseTrainingRecordReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<HseTrainingRecordReadDto>>> GetById(string id)
    {
        var record = await trainingRecords.GetByIdAsync(id);
        if (record == null) return NotFound(ApiResponse<HseTrainingRecordReadDto>.Fail("Training record not found.", 404));
        return Ok(ApiResponse<HseTrainingRecordReadDto>.Ok(ToReadDto(record)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<HseTrainingRecordReadDto>>> Create([FromBody] CreateHseTrainingRecordDto dto)
    {
        var record = new HseTrainingRecord
        {
            EmployeeUserId = dto.EmployeeUserId,
            EmployeeName = dto.EmployeeName,
            Course = dto.Course,
            CompletedOn = dto.CompletedOn,
            ExpiresOn = dto.ExpiresOn,
            CertificateUrl = dto.CertificateUrl,
        };
        await trainingRecords.CreateAsync(record);
        return CreatedAtAction(nameof(GetById), new { id = record.Id, version = "1" },
            ApiResponse<HseTrainingRecordReadDto>.Ok(ToReadDto(record), "Training record added."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<HseTrainingRecordReadDto>>> Update(string id, [FromBody] UpdateHseTrainingRecordDto dto)
    {
        var record = await trainingRecords.GetByIdAsync(id);
        if (record == null) return NotFound(ApiResponse<HseTrainingRecordReadDto>.Fail("Training record not found.", 404));

        record.Course = dto.Course;
        record.CompletedOn = dto.CompletedOn;
        record.ExpiresOn = dto.ExpiresOn;
        record.CertificateUrl = dto.CertificateUrl;
        await trainingRecords.UpdateAsync(record);

        return Ok(ApiResponse<HseTrainingRecordReadDto>.Ok(ToReadDto(record), "Training record updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await trainingRecords.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Training record not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Training record deleted successfully.", StatusCode = 200 });
    }

    private static HseTrainingRecordReadDto ToReadDto(HseTrainingRecord t) => new()
    {
        Id = t.Id,
        EmployeeUserId = t.EmployeeUserId,
        EmployeeName = t.EmployeeName,
        Course = t.Course,
        CompletedOn = t.CompletedOn,
        ExpiresOn = t.ExpiresOn,
        CertificateUrl = t.CertificateUrl,
    };
}
