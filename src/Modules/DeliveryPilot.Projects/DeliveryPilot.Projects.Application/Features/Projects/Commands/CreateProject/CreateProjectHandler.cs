using System.Text.Json;
using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;

public sealed class CreateProjectHandler(IProjectCreationStore store, IWorkspaceAuthority authority, ICurrentUserProvider users, TimeProvider timeProvider) : ICommandHandler<CreateProjectCommand, CreateProjectResult>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<CreateProjectResult>> HandleAsync(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        CurrentUser? principal = users.GetCurrentUser();
        if (!Guid.TryParse(principal?.Id, out Guid userId) || userId == Guid.Empty || !Guid.TryParse(principal.SessionId, out Guid sessionId) || sessionId == Guid.Empty)
        {
            throw new UnauthorizedException("Authentication.Required", "Authentication is required.");
        }

        ProjectCreationDetails details = ProjectCreationDetails.Normalize(request);
        await authority.RequireAsync(userId, request.OrganizationId, sessionId, WorkspacePermissions.ProjectsCreate, cancellationToken);
        await store.LockAttemptAsync(request.OrganizationId, userId, request.IdempotencyKey, cancellationToken);
        await authority.RequireAsync(userId, request.OrganizationId, sessionId, WorkspacePermissions.ProjectsCreate, cancellationToken);
        ProjectCreationSnapshot? replay = await store.FindReplayAsync(request.OrganizationId, userId, request.IdempotencyKey, details, cancellationToken);
        if (replay is not null) { return Result.Success(new CreateProjectResult(replay, true)); }
        if (details.EngagementId is Guid engagementId)
        {
            Engagement? engagement = await store.LockEngagementAsync(request.OrganizationId, engagementId, cancellationToken);
            WorkspaceAuthority facts = await authority.RequireAsync(userId, request.OrganizationId, sessionId, WorkspacePermissions.ProjectsCreate, cancellationToken);
            if (engagement is null || !(engagement.CreatedByUserId == userId || facts.HasPermission(WorkspacePermissions.EngagementsReadAll) || facts.HasPermission(WorkspacePermissions.EngagementsManage)))
            {
                throw new NotFoundException("Engagement.NotFound", "Engagement was not found.");
            }
            if (engagement.Status != Engagement.ActiveStatus) { throw new ConflictException("Engagement.Closed", "Engagement is closed."); }
        }

        DateTimeOffset clock = timeProvider.GetUtcNow().ToUniversalTime();
        var now = new DateTimeOffset(clock.Ticks - (clock.Ticks % 10), TimeSpan.Zero);
        ProjectCreation creation = Project.CreateWithAdministrator(request.OrganizationId, userId, now, details.Name, details.Purpose, details.Kind, details.EngagementId);
        Project project = creation.Project;
        var snapshot = new ProjectCreationSnapshot(project.Id, project.Status, project.Kind, project.EngagementId, project.SprintLengthWeeks, project.Version);
        var entry = ProjectEvent.Create(request.OrganizationId, project.Id, "project.created", "Project", project.Id, userId, request.OrganizationId, now, project.Version, JsonSerializer.Serialize(new { snapshot.Id, snapshot.Status, snapshot.Kind, snapshot.EngagementId, snapshot.SprintLengthWeeks, snapshot.Version, project.Name, project.Purpose, project.CreatedByUserId, project.CreatedAt }, _jsonOptions));
        store.Stage(creation, entry, userId, request.IdempotencyKey, details, now);
        return Result.Success(new CreateProjectResult(snapshot, false));
    }
}
