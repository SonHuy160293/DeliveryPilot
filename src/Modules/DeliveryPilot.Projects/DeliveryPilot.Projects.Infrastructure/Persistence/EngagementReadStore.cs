using System.Text.Json;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class EngagementReadStore(ProjectsDbContext dbContext) : IEngagementReadStore
{
    public async Task<EngagementPage<EngagementListEntry>> ListAsync(Guid organizationId, ReadAccess access, string status, int limit, EngagementPageKey? after, CancellationToken cancellationToken)
    {
        IQueryable<VisibleEngagement> query = Visible(organizationId, access);
        if (status != "all")
        {
            string state = status == "active" ? Engagement.ActiveStatus : Engagement.ClosedStatus;
            query = query.Where(x => x.Engagement.Status == state);
        }

        if (after is not null)
        {
            query = query.Where(x => x.Engagement.CreatedAt < after.Time || (x.Engagement.CreatedAt == after.Time && x.Engagement.Id.CompareTo(after.Id) > 0));
        }

        List<VisibleEngagement> rows = await query.OrderByDescending(x => x.Engagement.CreatedAt).ThenBy(x => x.Engagement.Id).Take(limit + 1).ToListAsync(cancellationToken);
        bool more = rows.Count > limit;
        if (more)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        IReadOnlyList<EngagementListEntry> details = await BuildEntriesAsync(organizationId, rows, cancellationToken);
        string? next = more ? EngagementCursor.Encode(organizationId, Guid.Empty, status, rows[^1].Engagement.CreatedAt, rows[^1].Engagement.Id) : null;
        return new EngagementPage<EngagementListEntry>(details, next);
    }

    public async Task<EngagementView?> GetAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken)
    {
        VisibleEngagement? row = await Visible(organizationId, access).SingleOrDefaultAsync(x => x.Engagement.Id == engagementId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        return (await BuildEntriesAsync(organizationId, [row], cancellationToken))[0];
    }

    public Task<bool> CanReadAsync(Guid organizationId, Guid engagementId, ReadAccess access, CancellationToken cancellationToken)
        => dbContext.Engagements.AsNoTracking().AnyAsync(
            x => x.OrganizationId == organizationId && x.Id == engagementId && (access.CanReadAll || x.CreatedByUserId == access.UserId),
            cancellationToken);

    public async Task<IReadOnlyList<EngagementProjectView>> ListProjectsAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects.AsNoTracking()
            .Where(project => project.OrganizationId == organizationId && project.EngagementId == engagementId)
            .OrderBy(project => project.CreatedAt).ThenBy(project => project.Id)
            .Select(project => new { project.Id, project.Name, project.Status }).ToListAsync(cancellationToken);
        if (projects.Count == 0)
        {
            return [];
        }

        Guid[] ids = projects.Select(project => project.Id).ToArray();
        var administrators = await (
            from participant in dbContext.ProjectParticipants.AsNoTracking()
            join role in dbContext.ProjectParticipantRoles.AsNoTracking()
                on new { participant.OrganizationId, participant.ProjectId, ParticipantId = participant.Id }
                equals new { role.OrganizationId, role.ProjectId, role.ParticipantId }
            where participant.OrganizationId == organizationId && ids.Contains(participant.ProjectId)
                && participant.Status == ProjectParticipant.ActiveStatus && role.RevokedAt == null
                && role.RoleCode == ProjectRoles.ProjectAdministrator
            select new { participant.ProjectId, participant.UserId }).Distinct().ToListAsync(cancellationToken);
        ILookup<Guid, EngagementAdministratorView> byProject = administrators.ToLookup(admin => admin.ProjectId, admin => new EngagementAdministratorView(admin.UserId, null));
        return projects.Select(project => new EngagementProjectView(project.Id, project.Name, project.Status, byProject[project.Id].ToArray())).ToArray();
    }

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

    private IQueryable<VisibleEngagement> Visible(Guid organizationId, ReadAccess access)
        => dbContext.Engagements.AsNoTracking()
            .Where(engagement => engagement.OrganizationId == organizationId)
            .Where(engagement => access.CanReadAll || engagement.CreatedByUserId == access.UserId
                || dbContext.Projects.Any(project => project.OrganizationId == organizationId && project.EngagementId == engagement.Id
                    && dbContext.ProjectParticipants.Any(participant => participant.OrganizationId == organizationId
                        && participant.ProjectId == project.Id && participant.UserId == access.UserId
                        && participant.Kind == ProjectParticipant.MemberKind && participant.Status == ProjectParticipant.ActiveStatus)))
            .Select(engagement => new VisibleEngagement { Engagement = engagement, Full = access.CanReadAll || engagement.CreatedByUserId == access.UserId });

    private async Task<IReadOnlyList<EngagementListEntry>> BuildEntriesAsync(Guid organizationId, IReadOnlyList<VisibleEngagement> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        Guid[] ids = rows.Select(x => x.Engagement.Id).ToArray();
        Guid[] fullIds = rows.Where(x => x.Full).Select(x => x.Engagement.Id).ToArray();
        var contacts = fullIds.Length == 0 ? [] : await dbContext.EngagementContacts.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && fullIds.Contains(x.EngagementId) && x.RemovedAt == null)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.EngagementId, View = new EngagementContactView(x.Id, x.Name, x.Email, x.ContactRole, x.CreatedByUserId, x.CreatedAt, x.UpdatedAt, x.Version) })
            .ToListAsync(cancellationToken);
        var milestones = await dbContext.EngagementMilestones.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && ids.Contains(x.EngagementId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.EngagementId, View = new EngagementMilestoneView(x.Id, x.Name, x.Description, x.OriginalTargetDate, x.CurrentTargetDate, x.Status, x.CreatedByUserId, x.CreatedAt, x.UpdatedAt, x.Version) })
            .ToListAsync(cancellationToken);

        // Project only expiry and ownership; never load the code verifier for a read response.
        var codes = fullIds.Length == 0 ? [] : await dbContext.EngagementLinkCodes.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && fullIds.Contains(x.EngagementId) && x.Status == EngagementLinkCode.OutstandingStatus)
            .Select(x => new { x.EngagementId, x.ExpiresAt }).ToListAsync(cancellationToken);

        ILookup<Guid, EngagementContactView> contactsByEngagement = contacts.ToLookup(x => x.EngagementId, x => x.View);
        ILookup<Guid, EngagementMilestoneView> milestonesByEngagement = milestones.ToLookup(x => x.EngagementId, x => x.View);
        var codesByEngagement = codes.ToDictionary(x => x.EngagementId);
        return rows.Select<VisibleEngagement, EngagementListEntry>(row =>
        {
            Engagement x = row.Engagement;
            EngagementMilestoneView[] milestoneViews = milestonesByEngagement[x.Id].ToArray();
            if (!row.Full)
            {
                return new EngagementSummary(x.Id, x.ClientDisplayName, x.EngagementName, x.AgreementReference, x.Status, milestoneViews, x.Version);
            }

            var code = codesByEngagement.GetValueOrDefault(x.Id);
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
                contactsByEngagement[x.Id].ToArray(),
                milestoneViews);
        }).ToArray();
    }

    private static JsonElement ParseDetails(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class VisibleEngagement
    {
        public Engagement Engagement { get; init; } = null!;
        public bool Full { get; init; }
    }
}
