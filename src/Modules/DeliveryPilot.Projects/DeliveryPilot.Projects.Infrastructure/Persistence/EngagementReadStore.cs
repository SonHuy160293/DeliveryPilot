using System.Text.Json;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class EngagementReadStore(ProjectsDbContext dbContext) : IEngagementReadStore
{
    public async Task<EngagementPage<EngagementDetail>> ListAsync(Guid organizationId, ReadAccess access, string status, int limit, EngagementPageKey? after, CancellationToken cancellationToken)
    {
        IQueryable<Engagement> query = Visible(organizationId, access);
        if (status != "all")
        {
            string state = status == "active" ? Engagement.ActiveStatus : Engagement.ClosedStatus;
            query = query.Where(x => x.Status == state);
        }

        if (after is not null)
        {
            query = query.Where(x => x.CreatedAt < after.Time || (x.CreatedAt == after.Time && x.Id.CompareTo(after.Id) > 0));
        }

        List<Engagement> rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken);
        bool more = rows.Count > limit;
        if (more)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        IReadOnlyList<EngagementDetail> details = await BuildDetailsAsync(organizationId, rows, cancellationToken);
        string? next = more ? EngagementCursor.Encode(organizationId, Guid.Empty, status, rows[^1].CreatedAt, rows[^1].Id) : null;
        return new EngagementPage<EngagementDetail>(details, next);
    }

    public async Task<EngagementDetail?> GetAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken)
    {
        Engagement? row = await Visible(organizationId, access).SingleOrDefaultAsync(x => x.Id == engagementId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        return (await BuildDetailsAsync(organizationId, [row], cancellationToken))[0];
    }

    public Task<bool> CanReadAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken)
        => Visible(organizationId, access).AnyAsync(x => x.Id == engagementId, cancellationToken);

    public async Task<EngagementPage<EngagementHistoryEntry>> HistoryAsync(Guid organizationId, Guid engagementId, int limit, EngagementPageKey? after, CancellationToken cancellationToken)
    {
        IQueryable<EngagementEvent> query = dbContext.EngagementEvents.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.EngagementId == engagementId);
        if (after is not null)
        {
            query = query.Where(x => x.OccurredAt > after.Time || (x.OccurredAt == after.Time && x.Id.CompareTo(after.Id) > 0));
        }

        List<EngagementEvent> rows = await query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken);
        bool more = rows.Count > limit;
        if (more)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var entries = rows.Select(x => new EngagementHistoryEntry(x.Id, x.EventType, x.SubjectType, x.SubjectId, x.ActorUserId, x.ActorOrganizationId, x.OccurredAt, x.Reason, ParseDetails(x.Details), x.EngagementVersion)).ToArray();
        string? next = more ? EngagementCursor.Encode(organizationId, engagementId, "history", rows[^1].OccurredAt, rows[^1].Id) : null;
        return new EngagementPage<EngagementHistoryEntry>(entries, next);
    }

    private IQueryable<Engagement> Visible(Guid organizationId, ReadAccess access)
        => dbContext.Engagements.AsNoTracking().Where(x => x.OrganizationId == organizationId && (access.CanReadAll || x.CreatedByUserId == access.UserId));

    private async Task<IReadOnlyList<EngagementDetail>> BuildDetailsAsync(Guid organizationId, IReadOnlyList<Engagement> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        Guid[] ids = rows.Select(x => x.Id).ToArray();
        var contacts = await dbContext.EngagementContacts.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && ids.Contains(x.EngagementId) && x.RemovedAt == null)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.EngagementId, View = new EngagementContactView(x.Id, x.Name, x.Email, x.ContactRole, x.CreatedByUserId, x.CreatedAt, x.UpdatedAt, x.Version) })
            .ToListAsync(cancellationToken);
        var milestones = await dbContext.EngagementMilestones.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && ids.Contains(x.EngagementId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.EngagementId, View = new EngagementMilestoneView(x.Id, x.Name, x.Description, x.OriginalTargetDate, x.CurrentTargetDate, x.Status, x.CreatedByUserId, x.CreatedAt, x.UpdatedAt, x.Version) })
            .ToListAsync(cancellationToken);

        // Project only expiry and ownership; never load the code verifier for a read response.
        var codes = await dbContext.EngagementLinkCodes.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && ids.Contains(x.EngagementId) && x.Status == EngagementLinkCode.OutstandingStatus)
            .Select(x => new { x.EngagementId, x.ExpiresAt }).ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            var code = codes.SingleOrDefault(item => item.EngagementId == x.Id);
            return new EngagementDetail(
                x.Id,
                x.OrganizationId,
                x.ClientDisplayName,
                x.ClientLegalName,
                x.ClientCountryCode,
                x.EngagementName,
                x.AgreementReference,
                x.AgreementSignedOn,
                x.DocumentUrl,
                x.ContractAmount,
                x.ContractCurrency,
                x.PlannedEffortPersonDays,
                x.Status,
                x.ClosedAt,
                x.ClosedByUserId,
                x.ClientOrganizationId,
                x.ClientLinkedAt,
                x.ClientLinkedByUserId,
                code is not null,
                code?.ExpiresAt,
                x.CreatedByUserId,
                x.CreatedAt,
                x.UpdatedAt,
                x.Version,
                contacts.Where(item => item.EngagementId == x.Id).Select(item => item.View).ToArray(),
                milestones.Where(item => item.EngagementId == x.Id).Select(item => item.View).ToArray());
        }).ToArray();
    }

    private static JsonElement ParseDetails(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
