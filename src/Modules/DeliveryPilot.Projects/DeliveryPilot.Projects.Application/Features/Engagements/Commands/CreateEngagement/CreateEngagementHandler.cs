using System.Text.Json;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.CreateEngagement;

public sealed class CreateEngagementHandler(
    IEngagementCreationStore store,
    IWorkspaceAuthority authority,
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider) : ICommandHandler<CreateEngagementCommand, CreateEngagementResult>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<CreateEngagementResult>> HandleAsync(CreateEngagementCommand request, CancellationToken cancellationToken)
    {
        CurrentUser? principal = currentUserProvider.GetCurrentUser();
        if (!Guid.TryParse(principal?.Id, out Guid userId) || userId == Guid.Empty
            || !Guid.TryParse(principal.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        EngagementCreationDetails details = EngagementCreationDetails.Normalize(request);
        await authority.RequireAsync(userId, request.OrganizationId, sessionId, WorkspacePermissions.EngagementsCreate, cancellationToken);
        await store.LockAttemptAsync(request.OrganizationId, userId, request.IdempotencyKey, cancellationToken);
        await store.LockReferenceAsync(request.OrganizationId, details.AgreementReference, cancellationToken);

        // Locks can wait behind another request. Recheck current authority before any receipt/data read.
        await authority.RequireAsync(userId, request.OrganizationId, sessionId, WorkspacePermissions.EngagementsCreate, cancellationToken);
        EngagementCreationSnapshot? replay = await store.FindReplayAsync(request.OrganizationId, userId, request.IdempotencyKey, details, cancellationToken);
        if (replay is not null)
        {
            return Result.Success(new CreateEngagementResult(replay, true));
        }

        bool duplicate = await store.HasActiveReferenceAsync(request.OrganizationId, details.AgreementReference, cancellationToken);
        DateTimeOffset clock = timeProvider.GetUtcNow().ToUniversalTime();
        var now = new DateTimeOffset(clock.Ticks - (clock.Ticks % 10), TimeSpan.Zero);
        var engagement = Engagement.Create(
            request.OrganizationId,
            userId,
            now,
            details.ClientDisplayName,
            details.AgreementReference,
            details.AgreementSignedOn,
            details.ClientLegalName,
            details.ClientCountryCode,
            details.EngagementName,
            details.DocumentUrl);
        var snapshot = new EngagementCreationSnapshot(
            engagement.Id,
            engagement.Status,
            engagement.Version,
            engagement.ClientDisplayName,
            engagement.AgreementReference,
            engagement.AgreementSignedOn,
            engagement.ClientLegalName,
            engagement.ClientCountryCode,
            engagement.EngagementName,
            engagement.DocumentUrl,
            engagement.CreatedAt,
            duplicate);
        var entry = EngagementEvent.Create(request.OrganizationId, engagement.Id, "engagement.created", "Engagement", engagement.Id, userId, request.OrganizationId, now, 1, JsonSerializer.Serialize(snapshot, _jsonOptions));
        store.Stage(engagement, entry, userId, request.IdempotencyKey, details, now);
        return Result.Success(new CreateEngagementResult(snapshot, false));
    }
}
