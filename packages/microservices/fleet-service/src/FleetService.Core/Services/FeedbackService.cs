using FleetService.Core.Entities;
using FleetService.Core.Interfaces;

namespace FleetService.Core.Services;

public interface IFeedbackService : IService<Feedback>
{
    Task<IEnumerable<Feedback>> GetFilteredAsync(string? userId, FleetFeedbackStatus? status);
    Task<Feedback?> RespondAsync(string id, string response, string respondedByUserId, FleetFeedbackStatus? status);
}

public class FeedbackService(IRepository<Feedback> repository) : Service<Feedback>(repository), IFeedbackService
{
    public async Task<IEnumerable<Feedback>> GetFilteredAsync(string? userId, FleetFeedbackStatus? status)
    {
        if (!string.IsNullOrEmpty(userId) && status.HasValue)
            return await _repository.FindAsync(f => f.UserId == userId && f.Status == status.Value);
        if (!string.IsNullOrEmpty(userId))
            return await _repository.FindAsync(f => f.UserId == userId);
        if (status.HasValue)
            return await _repository.FindAsync(f => f.Status == status.Value);
        return await _repository.GetAllAsync();
    }

    public async Task<Feedback?> RespondAsync(string id, string response, string respondedByUserId, FleetFeedbackStatus? status)
    {
        var feedback = await _repository.GetByIdAsync(id);
        if (feedback == null) return null;
        feedback.AdminResponse = response;
        feedback.RespondedByUserId = respondedByUserId;
        feedback.ResponseDate = DateTime.UtcNow;
        feedback.Status = status ?? FleetFeedbackStatus.Reviewed;
        return await _repository.UpdateAsync(feedback);
    }
}
