using System.Text.Json;
using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Mutations;

public sealed class ProjectMutationService(IProjectMutationStore store, ProjectAccessEvaluator access, TimeProvider clock)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(ProjectMutationContext Context, ProjectBrief Brief)> LockBriefAsync(Guid organizationId, Guid projectId, string permission, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        await access.RequireAsync(organizationId, projectId, identity, permission, cancellationToken);
        Project? project = await store.LockProjectAsync(organizationId, projectId, false, cancellationToken);
        ProjectBrief? brief = await store.LockBriefAsync(organizationId, projectId, cancellationToken);
        await access.RequireAsync(organizationId, projectId, identity, permission, cancellationToken);
        if (project is null || brief is null)
        {
            throw ProjectAccessEvaluator.NotFound();
        }

        DateTimeOffset now = clock.GetUtcNow().ToUniversalTime();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        return (new ProjectMutationContext(project, identity.UserId, identity.SessionId, now), brief);
    }

    public async Task<(ProjectMutationContext Context, Engagement? Engagement)> LockProjectAsync(Guid organizationId, Guid projectId, string permission, Guid? engagementId, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        await access.RequireAsync(organizationId, projectId, identity, permission, cancellationToken);
        Project? project = await store.LockProjectAsync(organizationId, projectId, true, cancellationToken);
        Engagement? engagement = engagementId.HasValue ? await store.LockEngagementAsync(organizationId, engagementId.Value, cancellationToken) : null;
        await access.RequireAsync(organizationId, projectId, identity, permission, cancellationToken);
        if (project is null)
        {
            throw ProjectAccessEvaluator.NotFound();
        }

        DateTimeOffset now = clock.GetUtcNow().ToUniversalTime();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        return (new ProjectMutationContext(project, identity.UserId, identity.SessionId, now), engagement);
    }

    public async Task<(ProjectMutationContext Context, ProjectStakeholder? Stakeholder)> LockStakeholderAsync(Guid organizationId, Guid projectId, Guid? stakeholderId, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        await access.RequireAsync(organizationId, projectId, identity, ProjectPermissions.EditStakeholders, cancellationToken);
        Project? project = await store.LockProjectAsync(organizationId, projectId, false, cancellationToken);
        ProjectStakeholder? stakeholder = stakeholderId.HasValue ? await store.LockStakeholderAsync(organizationId, projectId, stakeholderId.Value, cancellationToken) : null;
        await access.RequireAsync(organizationId, projectId, identity, ProjectPermissions.EditStakeholders, cancellationToken);
        if (project is null || (stakeholderId.HasValue && stakeholder is null))
        {
            throw new NotFoundException("ProjectStakeholder.NotFound", "Stakeholder was not found.");
        }

        DateTimeOffset now = clock.GetUtcNow().ToUniversalTime();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % 10), TimeSpan.Zero);
        return (new ProjectMutationContext(project, identity.UserId, identity.SessionId, now), stakeholder);
    }

    public async Task RequireActiveParticipantAsync(ProjectMutationContext context, Guid? participantId, CancellationToken cancellationToken)
    {
        if (participantId.HasValue && !await store.IsActiveParticipantAsync(context.Project.OrganizationId, context.Project.Id, participantId.Value, cancellationToken))
        {
            throw new ConflictException("ProjectStakeholder.InvalidParticipant", "The participant is not active in this project.");
        }
    }

    public void AddStakeholder(ProjectStakeholder stakeholder) => store.AddStakeholder(stakeholder);

    public void Record(ProjectMutationContext context, string eventType, string subjectType, Guid subjectId, object details, string? reason = null)
    {
        Project project = context.Project;
        store.AddEvent(ProjectEvent.Create(project.OrganizationId, project.Id, eventType, subjectType, subjectId, context.UserId, project.OrganizationId, context.OccurredAt, project.Version, JsonSerializer.Serialize(details, _jsonOptions), reason));
    }
}
