using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class ClientLinkStore(ProjectsDbContext db) : IClientLinkStore
{
    // The only verifier lookup without a tenant filter, authorized by FR-DP-025-009 / PRJ-10.
    public Task<EngagementLinkCode?> FindCodeByVerifierAsync(byte[] verifier, CancellationToken cancellationToken)
        => db.EngagementLinkCodes.AsNoTracking().SingleOrDefaultAsync(x => x.CodeVerifier == verifier, cancellationToken);

    public Task<Engagement?> LockEngagementForLinkAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Link confirmation requires the pipeline transaction.");
        }

        return db.Engagements.FromSqlInterpolated($"""
            SELECT * FROM engagement."Engagements"
            WHERE "OrganizationId" = {organizationId} AND "Id" = {engagementId}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<EngagementLinkCode?> ReloadCodeAsync(Guid organizationId, Guid engagementId, Guid codeId, CancellationToken cancellationToken)
        => db.EngagementLinkCodes.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.EngagementId == engagementId && x.Id == codeId, cancellationToken);

    public Task<Engagement?> LockLinkedAsync(Guid clientOrganizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Link removal requires the pipeline transaction.");
        }

        return db.Engagements.FromSqlInterpolated($"""
            SELECT * FROM engagement."Engagements"
            WHERE "ClientOrganizationId" = {clientOrganizationId} AND "Id" = {engagementId}
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedEngagement>> ListAsync(Guid clientOrganizationId, CancellationToken cancellationToken)
        => await db.Engagements.AsNoTracking().Where(x => x.ClientOrganizationId == clientOrganizationId)
            .OrderBy(x => x.Id).Select(x => new LinkedEngagement(x.Id, x.EngagementName, x.ClientDisplayName)).ToArrayAsync(cancellationToken);

    public void AddEvent(EngagementEvent entry) => db.EngagementEvents.Add(entry);
}
