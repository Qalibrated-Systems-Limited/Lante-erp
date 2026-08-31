using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IDriverProfileService : IService<DriverProfile>
{
    Task<IEnumerable<DriverProfile>> GetByDriverIdAsync(string driverId);
    Task<DriverProfile?> GetCurrentByDriverIdAsync(string driverId);
    Task<IEnumerable<DriverProfile>> GetExpiringAsync(int daysThreshold);
    Task<IEnumerable<DriverProfileChange>> GetHistoryByDriverIdAsync(string driverId);
    Task<DriverProfile?> SubmitAsync(string id);
    Task<DriverProfile?> ApproveAsync(string id, string reviewedBy, string? notes);
    Task<DriverProfile?> RejectAsync(string id, string reviewedBy, string? reason);
    Task<(DriverProfile? profile, string? oldUrl)> UpdatePhotoUrlAsync(string id, string field, string url);
}

public class DriverProfileService(IDriverProfileRepository repository) : Service<DriverProfile>(repository), IDriverProfileService
{
    private readonly IDriverProfileRepository _profileRepo = repository;

    public Task<IEnumerable<DriverProfile>> GetByDriverIdAsync(string driverId)
        => _profileRepo.GetByDriverIdAsync(driverId);

    public Task<DriverProfile?> GetCurrentByDriverIdAsync(string driverId)
        => _profileRepo.GetCurrentByDriverIdAsync(driverId);

    public Task<IEnumerable<DriverProfile>> GetExpiringAsync(int daysThreshold)
        => _profileRepo.GetExpiringAsync(DateTime.UtcNow.AddDays(daysThreshold));

    public Task<IEnumerable<DriverProfileChange>> GetHistoryByDriverIdAsync(string driverId)
        => _profileRepo.GetHistoryByDriverIdAsync(driverId);

    public async Task<DriverProfile?> SubmitAsync(string id)
    {
        var profile = await _repository.GetByIdAsync(id);
        if (profile == null) return null;
        profile.Status = DriverProfileStatus.Pending;
        profile.SubmittedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(profile);
    }

    public async Task<DriverProfile?> ApproveAsync(string id, string reviewedBy, string? notes)
    {
        var profile = await _repository.GetByIdAsync(id);
        if (profile == null) return null;
        profile.Status = DriverProfileStatus.Approved;
        profile.ReviewedAt = DateTime.UtcNow;
        profile.ReviewedBy = reviewedBy;
        profile.ApprovalNotes = notes;
        return await _repository.UpdateAsync(profile);
    }

    public async Task<DriverProfile?> RejectAsync(string id, string reviewedBy, string? reason)
    {
        var profile = await _repository.GetByIdAsync(id);
        if (profile == null) return null;
        profile.Status = DriverProfileStatus.Rejected;
        profile.ReviewedAt = DateTime.UtcNow;
        profile.ReviewedBy = reviewedBy;
        profile.RejectionReason = reason;
        return await _repository.UpdateAsync(profile);
    }

    public async Task<(DriverProfile? profile, string? oldUrl)> UpdatePhotoUrlAsync(string id, string field, string url)
    {
        var profile = await _repository.GetByIdAsync(id);
        if (profile == null) return (null, null);
        string? oldUrl = field switch
        {
            "profile" => profile.ProfilePhotoUrl,
            "licenceFront" => profile.LicenseFrontImageUrl,
            "licenceBack" => profile.LicenseBackImageUrl,
            "idFront" => profile.IdFrontImageUrl,
            "idBack" => profile.IdBackImageUrl,
            _ => null
        };
        switch (field)
        {
            case "profile": profile.ProfilePhotoUrl = url; break;
            case "licenceFront": profile.LicenseFrontImageUrl = url; break;
            case "licenceBack": profile.LicenseBackImageUrl = url; break;
            case "idFront": profile.IdFrontImageUrl = url; break;
            case "idBack": profile.IdBackImageUrl = url; break;
        }
        return (await _repository.UpdateAsync(profile), oldUrl);
    }
}
