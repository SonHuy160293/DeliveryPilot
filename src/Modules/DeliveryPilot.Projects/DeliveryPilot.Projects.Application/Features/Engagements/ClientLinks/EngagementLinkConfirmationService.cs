using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public sealed class EngagementLinkConfirmationService(ClientLinkAuthorization authorization, IClientLinkStore store, ILinkAttemptLimiter limiter, TimeProvider timeProvider)
{
    public async Task<EngagementLinkPreview> ExecuteAsync(Guid clientOrganizationId, string code, bool confirm, CancellationToken cancellationToken)
    {
        Guid userId = await authorization.RequireAsync(clientOrganizationId, cancellationToken);
        await using IAsyncDisposable lease = await limiter.AcquireAsync(userId, cancellationToken);
        await authorization.RequireAsync(clientOrganizationId, cancellationToken);
        if (await limiter.IsLimitedAsync(userId, timeProvider.GetUtcNow(), cancellationToken))
        {
            throw new EngagementLinkRateLimitException(limiter.RetryAfterSeconds);
        }

        try
        {
            byte[] verifier = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            EngagementLinkCode initial = await store.FindCodeByVerifierAsync(verifier, cancellationToken) ?? throw InvalidCode();
            Engagement engagement = await store.LockEngagementForLinkAsync(initial.OrganizationId, initial.EngagementId, cancellationToken) ?? throw InvalidCode();
            await authorization.RequireAsync(clientOrganizationId, cancellationToken);
            EngagementLinkCode current = await store.ReloadCodeAsync(initial.OrganizationId, initial.EngagementId, initial.Id, cancellationToken) ?? throw InvalidCode();
            DateTimeOffset now = timeProvider.GetUtcNow().ToUniversalTime();
            now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
            if (!current.IsUsableAt(now))
            {
                throw InvalidCode();
            }

            engagement.EnsureCanConfirmClientLink(clientOrganizationId);
            if (confirm)
            {
                engagement.ConfirmClientLink(clientOrganizationId, userId, now);
                current.Consume(userId, clientOrganizationId, now);
                store.AddEvent(EngagementEvent.Create(
                    engagement.OrganizationId,
                    engagement.Id,
                    "link.confirmed",
                    "Engagement",
                    engagement.Id,
                    userId,
                    clientOrganizationId,
                    now,
                    engagement.Version,
                    JsonSerializer.Serialize(new { clientOrganizationId, linkCodeId = current.Id })));
            }

            return new EngagementLinkPreview(engagement.EngagementName, engagement.ClientDisplayName);
        }
        catch (NotFoundException exception) when (exception.Code == "EngagementLink.InvalidCode")
        {
            // A completed invalid verdict still counts if the caller disconnects before it is recorded.
            // The independent database command remains bounded by its command timeout.
            await limiter.RecordFailureAsync(userId, clientOrganizationId, timeProvider.GetUtcNow(), CancellationToken.None);
            throw;
        }
    }

    private static NotFoundException InvalidCode() => new("EngagementLink.InvalidCode", "The link code is invalid or unavailable.");
}
