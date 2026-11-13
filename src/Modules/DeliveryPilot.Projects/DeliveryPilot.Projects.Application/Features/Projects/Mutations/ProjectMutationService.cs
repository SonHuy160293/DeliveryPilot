using System.Text.Json;
using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;

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
        return (new ProjectMutationContext(project, identity.UserId, now), brief);
    }

    public void Record(ProjectMutationContext context, string eventType, string subjectType, Guid subjectId, object details, string? reason = null)
    {
        Project project = context.Project;
        store.AddEvent(ProjectEvent.Create(project.OrganizationId, project.Id, eventType, subjectType, subjectId, context.UserId, project.OrganizationId, context.OccurredAt, project.Version, JsonSerializer.Serialize(details, _jsonOptions), reason));
    }
}
