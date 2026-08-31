namespace UserService.Core.Interfaces.Emails;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}
