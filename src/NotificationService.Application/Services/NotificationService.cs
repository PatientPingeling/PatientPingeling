using System.Text.Json;
using Microsoft.Extensions.Logging;

using NotificationService.Domain.Entities;
using NotificationService.Application.Interfaces;
using NotificationService.Domain;
// using NotificationService.Application.Results;

namespace NotificationService.Application.Services;

public sealed class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
    private readonly ILogger<NotificationService> _logger = logger;

    public async Task<Result> ProcessNotificationAsync(string message)
    {
        try
        {
            //
            // =========================================================
            // 1. VALIDATION / EXPECTED FAILURES
            // =========================================================
            //
            // Use Result.Failure(...)
            // DO NOT throw exceptions here.
            //

            if (string.IsNullOrWhiteSpace(message))
            {
                return Result.Failure("Notification message was empty.");
            }

            var notification = JsonSerializer.Deserialize<Notification>(message);
            if (notification is null)
            {
                return Result.Failure("Notification couldnt be dezerialized.");
            }

            //
            // Example:
            //
            // if (!email.Contains("@"))
            // {
            //     return Result.Failure("Invalid email address.");
            // }
            //

            _logger.LogInformation("Notification validation succeeded.");

            //
            // =========================================================
            // 2. BUSINESS LOGIC
            // =========================================================
            //

            // Example:
            //
            // var notification =
            //     JsonSerializer.Deserialize<NotificationDto>(message);
            //
            // if (notification is null)
            // {
            //     return Result.Failure("Invalid notification payload.");
            // }
            //

            //
            // =========================================================
            // 3. DATABASE OPERATIONS
            // =========================================================
            //
            // Infrastructure can throw exceptions naturally.
            // That's okay.
            //

            // await _dbContext.Notifications.AddAsync(entity);
            // await _dbContext.SaveChangesAsync();

            //
            // =========================================================
            // 4. EXTERNAL SERVICES
            // =========================================================
            //

            // await _emailProvider.SendAsync(...);

            //
            // =========================================================
            // SUCCESS
            // =========================================================
            //

            _logger.LogInformation("Notification processed successfully.");

            return Result.Success();
        }
        catch (Exception ex)
        {
            //
            // =========================================================
            // MEGACRASH()
            // =========================================================
            //
            // Unexpected infrastructure/runtime failure.
            //
            // Examples:
            // - PostgreSQL died
            // - SMTP timed out
            // - NullReferenceException
            // - Serialization exploded
            // - Network failure
            //
            // Here exceptions ARE correct.
            //
            // RabbitMQ listener layer will catch this
            // and decide:
            // ACK / NACK / retry / DLQ
            //
            // =========================================================
            //

            _logger.LogError(
                ex,
                "Unexpected crash while processing notification.");

            throw;
        }
    }
}