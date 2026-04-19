using MentorMatch.Data;
using Microsoft.EntityFrameworkCore;

namespace MentorMatch.Services;

public class NotificationCleanupService(IServiceProvider serviceProvider, ILogger<NotificationCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Delete notifications that are older than 7 days and marked as read
                var cutoff = DateTime.UtcNow.AddDays(-7);
                var toDelete = await context.Notifications
                    .Where(n => n.IsRead && n.Timestamp < cutoff)
                    .ToListAsync(stoppingToken);

                if (toDelete.Count > 0)
                {
                    context.Notifications.RemoveRange(toDelete);
                    await context.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Cleaned up {Count} old notifications.", toDelete.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during notification cleanup.");
            }

            // Run once every 24 hours
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
