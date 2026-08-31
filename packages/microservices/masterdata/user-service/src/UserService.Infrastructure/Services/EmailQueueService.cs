using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserService.Core.Interfaces.Emails;

namespace UserService.Infrastructure.Services;

public record EmailQueueItem(string To, string Subject, string Body);

public class EmailQueueService : IEmailQueueService
{
    private readonly ConcurrentQueue<EmailQueueItem> _queue = new();

    public Task EnqueueEmailAsync(string to, string subject, string body)
    {
        _queue.Enqueue(new EmailQueueItem(to, subject, body));
        return Task.CompletedTask;
    }

    public bool TryDequeue(out EmailQueueItem? item) => _queue.TryDequeue(out item);
}

public class EmailProcessorService(
    EmailQueueService queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailProcessorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (queue.TryDequeue(out var item) && item != null)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendEmailAsync(item.To, item.Subject, item.Body);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send email to {To}", item.To);
                }
            }
            else
            {
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}
