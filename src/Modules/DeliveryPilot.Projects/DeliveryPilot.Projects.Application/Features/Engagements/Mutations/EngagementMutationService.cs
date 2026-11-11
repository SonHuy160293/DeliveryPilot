using System.Text.Json;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public sealed class EngagementMutationService(IEngagementMutationStore store, IWorkspaceAuthority authority, ICurrentUserProvider currentUserProvider, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EngagementMutationContext> LockAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        CurrentUser? principal = currentUserProvider.GetCurrentUser();
        if (!Guid.TryParse(principal?.Id, out Guid userId) || userId == Guid.Empty
            || !Guid.TryParse(principal.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        await authority.RequireAsync(userId, organizationId, sessionId, WorkspacePermissions.EngagementsManage, cancellationToken);
        Engagement? engagement = await store.LockAsync(organizationId, engagementId, cancellationToken);
        await authority.RequireAsync(userId, organizationId, sessionId, WorkspacePermissions.EngagementsManage, cancellationToken);
        if (engagement is null)
        {
            throw new NotFoundException("Engagement.NotFound", "The engagement was not found.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow().ToUniversalTime();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        return new EngagementMutationContext(engagement, userId, now);
    }

    public void Record(EngagementMutationContext context, string eventType, string subjectType, Guid subjectId, object details, string? reason)
    {
        Engagement engagement = context.Engagement;
        var entry = EngagementEvent.Create(
            engagement.OrganizationId,
            engagement.Id,
            eventType,
            subjectType,
            subjectId,
            context.UserId,
            engagement.OrganizationId,
            context.OccurredAt,
            engagement.Version,
            JsonSerializer.Serialize(details, _jsonOptions),
            reason);
        store.AddEvent(entry);
    }

    public async Task<EngagementMilestone> FindMilestoneAsync(EngagementMutationContext context, Guid milestoneId, CancellationToken cancellationToken)
        => await store.FindMilestoneAsync(context.Engagement.OrganizationId, context.Engagement.Id, milestoneId, cancellationToken)
            ?? throw new NotFoundException("Milestone.NotFound", "The milestone was not found.");

    public void AddMilestone(EngagementMilestone milestone) => store.AddMilestone(milestone);

    public async Task<EngagementContact> FindContactAsync(EngagementMutationContext context, Guid contactId, CancellationToken cancellationToken)
        => await store.FindContactAsync(context.Engagement.OrganizationId, context.Engagement.Id, contactId, cancellationToken)
            ?? throw new NotFoundException("Contact.NotFound", "The contact was not found.");

    public Task<Guid?> DemoteClientProductOwnerAsync(EngagementMutationContext context, Guid exceptContactId, CancellationToken cancellationToken)
        => store.DemoteClientProductOwnerAsync(context.Engagement.OrganizationId, context.Engagement.Id, exceptContactId, context.OccurredAt, cancellationToken);

    public void AddContact(EngagementContact contact) => store.AddContact(contact);

    public Task<EngagementLinkCode?> FindOutstandingCodeAsync(EngagementMutationContext context, CancellationToken cancellationToken)
        => store.FindOutstandingCodeAsync(context.Engagement.OrganizationId, context.Engagement.Id, cancellationToken);

    public Task<Guid?> InvalidateOutstandingCodeAsync(EngagementMutationContext context, CancellationToken cancellationToken)
        => store.InvalidateOutstandingCodeAsync(context.Engagement.OrganizationId, context.Engagement.Id, context.OccurredAt, cancellationToken);

    public void AddLinkCode(EngagementLinkCode code) => store.AddLinkCode(code);

    public async Task<bool> CheckDuplicateReferenceAsync(EngagementMutationContext context, CancellationToken cancellationToken)
    {
        Engagement engagement = context.Engagement;
        await store.LockReferenceAsync(engagement.OrganizationId, engagement.AgreementReference, cancellationToken);
        CurrentUser? principal = currentUserProvider.GetCurrentUser();
        if (!Guid.TryParse(principal?.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        await authority.RequireAsync(context.UserId, engagement.OrganizationId, sessionId, WorkspacePermissions.EngagementsManage, cancellationToken);
        return await store.HasOtherActiveReferenceAsync(engagement.OrganizationId, engagement.AgreementReference, engagement.Id, cancellationToken);
    }
}
