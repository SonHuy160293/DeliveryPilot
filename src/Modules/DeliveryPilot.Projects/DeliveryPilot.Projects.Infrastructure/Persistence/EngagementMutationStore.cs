using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class EngagementMutationStore(ProjectsDbContext dbContext) : IEngagementMutationStore
{
    public Task LockReferenceAsync(Guid organizationId, string reference, CancellationToken cancellationToken)
        => EngagementReferenceQueries.LockAsync(dbContext, organizationId, reference, cancellationToken);

    public Task<bool> HasOtherActiveReferenceAsync(Guid organizationId, string reference, Guid excludeEngagementId, CancellationToken cancellationToken)
        => EngagementReferenceQueries.HasOtherActiveAsync(dbContext, organizationId, reference, excludeEngagementId, cancellationToken);

    public Task<Engagement?> LockAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Engagement mutation requires the pipeline transaction.");
        }

        return dbContext.Engagements.FromSqlInterpolated($"""
            SELECT * FROM engagement."Engagements"
            WHERE "OrganizationId" = {organizationId} AND "Id" = {engagementId}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<EngagementMilestone?> FindMilestoneAsync(Guid organizationId, Guid engagementId, Guid milestoneId, CancellationToken cancellationToken)
        => dbContext.EngagementMilestones.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.EngagementId == engagementId && x.Id == milestoneId, cancellationToken);

    public Task<EngagementContact?> FindContactAsync(Guid organizationId, Guid engagementId, Guid contactId, CancellationToken cancellationToken)
        => dbContext.EngagementContacts.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.EngagementId == engagementId && x.Id == contactId && x.RemovedAt == null, cancellationToken);

    public async Task<Guid?> DemoteClientProductOwnerAsync(Guid organizationId, Guid engagementId, Guid exceptContactId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Contact designation requires the pipeline transaction.");
        }

        IQueryable<EngagementContact> current = dbContext.EngagementContacts.Where(x => x.OrganizationId == organizationId && x.EngagementId == engagementId
            && x.Id != exceptContactId && x.RemovedAt == null && x.ContactRole == EngagementContact.ClientProductOwnerRole);
        Guid? id = await current.Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (id is not null)
        {
            int updated = await current.ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ContactRole, EngagementContact.OtherRole)
                    .SetProperty(x => x.UpdatedAt, occurredAt)
                    .SetProperty(x => x.Version, x => x.Version + 1),
                cancellationToken);
            if (updated != 1)
            {
                throw new InvalidOperationException("The designated contact changed while the engagement was locked.");
            }
        }

        return id;
    }

    public Task<EngagementLinkCode?> FindOutstandingCodeAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
        => dbContext.EngagementLinkCodes.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.EngagementId == engagementId && x.Status == EngagementLinkCode.OutstandingStatus, cancellationToken);

    public async Task<Guid?> InvalidateOutstandingCodeAsync(Guid organizationId, Guid engagementId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Code rotation requires the pipeline transaction.");
        }

        IQueryable<EngagementLinkCode> outstanding = dbContext.EngagementLinkCodes.Where(x => x.OrganizationId == organizationId && x.EngagementId == engagementId && x.Status == EngagementLinkCode.OutstandingStatus);
        Guid? id = await outstanding.Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (id is not null)
        {
            int updated = await outstanding.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, EngagementLinkCode.InvalidatedStatus)
                    .SetProperty(x => x.InvalidatedAt, occurredAt)
                    .SetProperty(x => x.Version, x => x.Version + 1),
                cancellationToken);
            if (updated != 1)
            {
                throw new InvalidOperationException("The outstanding code changed while the engagement was locked.");
            }
        }

        return id;
    }

    public void AddLinkCode(EngagementLinkCode code) => dbContext.EngagementLinkCodes.Add(code);

    public void AddMilestone(EngagementMilestone milestone) => dbContext.EngagementMilestones.Add(milestone);

    public void AddContact(EngagementContact contact) => dbContext.EngagementContacts.Add(contact);

    public void AddEvent(EngagementEvent entry) => dbContext.EngagementEvents.Add(entry);
}
