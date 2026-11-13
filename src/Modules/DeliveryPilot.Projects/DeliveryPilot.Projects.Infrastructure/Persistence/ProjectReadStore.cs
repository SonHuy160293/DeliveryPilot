using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class ProjectReadStore(ProjectsDbContext dbContext) : IProjectReadStore
{
    public async Task<IReadOnlyList<ProjectStakeholder>> ListStakeholdersAsync(Guid organizationId, Guid projectId, bool includeRetired, CancellationToken cancellationToken)
        => await dbContext.ProjectStakeholders.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ProjectId == projectId && (includeRetired || x.RetiredAt == null)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public Task<ProjectBrief?> FindBriefAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        => dbContext.ProjectBriefs.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyList<ProductGoalHistoryEntry>> ListProductGoalHistoryAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        var events = await dbContext.ProjectEvents.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ProjectId == projectId && x.EventType == "brief.product-goal-changed").OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => new { x.Id, x.ActorUserId, x.OccurredAt, x.Details }).ToListAsync(cancellationToken);
        return events.Select(entry =>
        {
            using JsonDocument document = JsonDocument.Parse(entry.Details);
            JsonElement goal = document.RootElement.GetProperty("productGoal");
            return new ProductGoalHistoryEntry(entry.Id, entry.ActorUserId, entry.OccurredAt, goal.GetProperty("previous").GetString(), goal.GetProperty("new").GetString());
        }).ToArray();
    }
    public async Task<ProjectParticipation?> FindParticipationAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        ProjectParticipant? participant = await dbContext.ProjectParticipants.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.ProjectId == projectId && x.UserId == userId && x.Status == ProjectParticipant.ActiveStatus, cancellationToken);
        if (participant is null) { return null; }
        string[] roles = await dbContext.ProjectParticipantRoles.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ProjectId == projectId && x.ParticipantId == participant.Id && x.RevokedAt == null).Select(x => x.RoleCode).ToArrayAsync(cancellationToken);
        return new ProjectParticipation(participant, roles);
    }

    public Task<Project?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        => dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == projectId, cancellationToken);

    public async Task<ProjectPage> ListAsync(Guid organizationId, Guid userId, string kind, string status, int limit, ProjectPageKey? after, CancellationToken cancellationToken)
    {
        IQueryable<Row> query = from participant in dbContext.ProjectParticipants.AsNoTracking()
                                join project in dbContext.Projects.AsNoTracking()
                                    on new { participant.OrganizationId, participant.ProjectId } equals new { project.OrganizationId, ProjectId = project.Id }
                                where participant.UserId == userId && participant.Kind == kind && participant.Status == ProjectParticipant.ActiveStatus
                                    && (kind == ProjectParticipant.GuestKind || participant.OrganizationId == organizationId)
                                    && dbContext.ProjectParticipantRoles.Any(role => role.OrganizationId == participant.OrganizationId && role.ProjectId == participant.ProjectId && role.ParticipantId == participant.Id && role.RevokedAt == null && (kind != ProjectParticipant.GuestKind || role.RoleCode != ProjectRoles.ProjectAdministrator))
                                select new Row { Project = project, Participant = participant };
        if (status != "all")
        {
            string state = status == "active" ? Project.ActiveStatus : Project.DraftStatus;
            query = query.Where(x => x.Project.Status == state);
        }

        if (after is not null)
        {
            query = query.Where(x => x.Project.CreatedAt < after.CreatedAt || (x.Project.CreatedAt == after.CreatedAt && x.Project.Id.CompareTo(after.Id) > 0));
        }

        List<Row> rows = await query.OrderByDescending(x => x.Project.CreatedAt).ThenBy(x => x.Project.Id).Take(limit + 1).ToListAsync(cancellationToken);
        bool more = rows.Count > limit;
        if (more) { rows.RemoveAt(rows.Count - 1); }
        Guid[] ids = rows.Select(x => x.Participant.Id).ToArray();
        var roles = await dbContext.ProjectParticipantRoles.AsNoTracking().Where(x => ids.Contains(x.ParticipantId) && x.RevokedAt == null).Select(x => new { x.OrganizationId, x.ProjectId, x.ParticipantId, x.RoleCode }).ToListAsync(cancellationToken);
        ProjectListEntry[] entries = rows.Select(row => new ProjectListEntry(row.Project.Id, row.Project.OrganizationId, row.Project.Name, row.Project.Status, ProjectAccessEvaluator.FilterRoles(kind, roles.Where(role => role.OrganizationId == row.Project.OrganizationId && role.ProjectId == row.Project.Id && role.ParticipantId == row.Participant.Id).Select(role => role.RoleCode)))).ToArray();
        string? next = more ? ProjectCursor.Encode(organizationId, userId, kind, status, rows[^1].Project.CreatedAt, rows[^1].Project.Id) : null;
        return new ProjectPage(entries, next);
    }

    public async Task<IReadOnlyList<ProjectParticipation>> ListParticipantsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        List<ProjectParticipant> participants = await dbContext.ProjectParticipants.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ProjectId == projectId && x.Status == ProjectParticipant.ActiveStatus).OrderBy(x => x.FirstJoinedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var roles = await dbContext.ProjectParticipantRoles.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ProjectId == projectId && x.RevokedAt == null).Select(x => new { x.ParticipantId, x.RoleCode }).ToListAsync(cancellationToken);
        ILookup<Guid, string> byParticipant = roles.ToLookup(x => x.ParticipantId, x => x.RoleCode);
        return participants.Select(participant => new ProjectParticipation(participant, ProjectAccessEvaluator.FilterRoles(participant.Kind, byParticipant[participant.Id]))).ToArray();
    }

    private sealed class Row
    {
        public Project Project { get; init; } = null!;
        public ProjectParticipant Participant { get; init; } = null!;
    }
}
