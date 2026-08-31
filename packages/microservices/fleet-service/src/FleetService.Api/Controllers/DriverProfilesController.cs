using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Services;
using FleetService.Core.Entities;
using FleetService.Core.Services;

namespace FleetService.Api.Controllers;

[Authorize(Policy = "fleet.read")]
[ApiController]
[Route("api/v1/[controller]")]
public class DriverProfilesController(IDriverProfileService driverProfileService, LocalFileStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? driverId = null)
    {
        var profiles = string.IsNullOrEmpty(driverId)
            ? await driverProfileService.GetAllAsync()
            : await driverProfileService.GetByDriverIdAsync(driverId);
        return Ok(new { success = true, data = profiles });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var profile = await driverProfileService.GetByIdAsync(id);
        if (profile == null) return NotFound(new { success = false, message = "Driver profile not found" });
        return Ok(new { success = true, data = profile });
    }

    [HttpGet("driver/{driverId}/current")]
    public async Task<IActionResult> GetCurrentByDriver(string driverId)
    {
        var profile = await driverProfileService.GetCurrentByDriverIdAsync(driverId);
        if (profile == null) return NotFound(new { success = false, message = "No current profile found" });
        return Ok(new { success = true, data = profile });
    }

    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiringLicenses([FromQuery] int daysThreshold = 30)
        => Ok(new { success = true, data = await driverProfileService.GetExpiringAsync(daysThreshold) });

    [HttpGet("driver/{driverId}/history")]
    public async Task<IActionResult> GetHistory(string driverId)
        => Ok(new { success = true, data = await driverProfileService.GetHistoryByDriverIdAsync(driverId) });

    [Authorize(Policy = "fleet.write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDriverProfileRequest req)
    {
        var existing = await driverProfileService.GetCurrentByDriverIdAsync(req.DriverId);
        if (existing != null)
        {
            existing.IsCurrent = false;
            await driverProfileService.UpdateAsync(existing);
        }

        var profile = new DriverProfile
        {
            DriverId = req.DriverId,
            FullName = req.FullName,
            PhoneNumber = req.PhoneNumber,
            IdNumber = req.IdNumber,
            LicenseNumber = req.LicenseNumber,
            LicenseExpiryDate = req.LicenseExpiryDate,
            IsCurrent = true,
            VersionNumber = (existing?.VersionNumber ?? 0) + 1,
            Status = DriverProfileStatus.Draft
        };

        var created = await driverProfileService.CreateAsync(profile);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { success = true, data = created });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDriverProfileRequest req)
    {
        var profile = await driverProfileService.GetByIdAsync(id);
        if (profile == null) return NotFound(new { success = false, message = "Driver profile not found" });
        profile.FullName = req.FullName;
        profile.PhoneNumber = req.PhoneNumber;
        profile.LicenseNumber = req.LicenseNumber;
        profile.LicenseExpiryDate = req.LicenseExpiryDate;
        var updated = await driverProfileService.UpdateAsync(profile);
        return Ok(new { success = true, data = updated });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(string id)
    {
        var profile = await driverProfileService.SubmitAsync(id);
        if (profile == null) return NotFound(new { success = false, message = "Driver profile not found" });
        return Ok(new { success = true, data = profile });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, [FromBody] ReviewRequest req)
    {
        var profile = await driverProfileService.ApproveAsync(id, req.ReviewedBy, req.Notes);
        if (profile == null) return NotFound(new { success = false, message = "Driver profile not found" });
        return Ok(new { success = true, data = profile });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(string id, [FromBody] ReviewRequest req)
    {
        var profile = await driverProfileService.RejectAsync(id, req.ReviewedBy, req.Notes);
        if (profile == null) return NotFound(new { success = false, message = "Driver profile not found" });
        return Ok(new { success = true, data = profile });
    }

    [HttpGet("{id}/photos")]
    public async Task<IActionResult> GetPhotos(string id)
    {
        var profile = await driverProfileService.GetByIdAsync(id);
        if (profile == null) return NotFound(new { success = false, message = "Driver profile not found" });
        return Ok(new { success = true, data = new { profileId = id, profilePhoto = profile.ProfilePhotoUrl, licenceFront = profile.LicenseFrontImageUrl, licenceBack = profile.LicenseBackImageUrl, idFront = profile.IdFrontImageUrl, idBack = profile.IdBackImageUrl } });
    }

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/profile-photo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfilePhoto(string id, IFormFile? image)
        => await UploadPhoto(id, image, "profile", "profilePhoto", $"drivers/{id}");

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/licence-front")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLicenceFront(string id, IFormFile? image)
        => await UploadPhoto(id, image, "licenceFront", "licenceFront", $"drivers/{id}");

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/licence-back")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLicenceBack(string id, IFormFile? image)
        => await UploadPhoto(id, image, "licenceBack", "licenceBack", $"drivers/{id}");

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/id-front")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadIdFront(string id, IFormFile? image)
        => await UploadPhoto(id, image, "idFront", "idFront", $"drivers/{id}");

    [Authorize(Policy = "fleet.write")]
    [HttpPost("{id}/id-back")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadIdBack(string id, IFormFile? image)
        => await UploadPhoto(id, image, "idBack", "idBack", $"drivers/{id}");

    [Authorize(Policy = "fleet.delete")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await driverProfileService.DeleteAsync(id);
        if (!deleted) return NotFound(new { success = false, message = "Driver profile not found" });
        return NoContent();
    }

    private async Task<IActionResult> UploadPhoto(string id, IFormFile? image, string field, string responseKey, string folder)
    {
        var file = storage.ResolveFile(image);
        if (file == null) return BadRequest(new { success = false, message = "No image file provided" });
        var url = await storage.SaveAsync(file, folder);
        var (profile, oldUrl) = await driverProfileService.UpdatePhotoUrlAsync(id, field, url);
        if (profile == null) { storage.Delete(url); return NotFound(new { success = false, message = "Driver profile not found" }); }
        storage.Delete(oldUrl);
        return Ok(new { success = true, data = new Dictionary<string, string> { [responseKey] = url } });
    }
}

public record CreateDriverProfileRequest(string DriverId, string FullName, string PhoneNumber, string IdNumber, string LicenseNumber, DateTime LicenseExpiryDate);
public record UpdateDriverProfileRequest(string FullName, string PhoneNumber, string LicenseNumber, DateTime LicenseExpiryDate);
public record ReviewRequest(string ReviewedBy, string? Notes);
