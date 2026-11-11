namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public interface ILinkAttemptLimiter
{
    int RetryAfterSeconds { get; }
    Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsLimitedAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task RecordFailureAsync(Guid userId, Guid actingOrganizationId, DateTimeOffset now, CancellationToken cancellationToken);
}
