namespace UserService.Core.Interfaces.Emails;

public interface IEmailQueueService
{
    Task EnqueueEmailAsync(string to, string subject, string body);
}
