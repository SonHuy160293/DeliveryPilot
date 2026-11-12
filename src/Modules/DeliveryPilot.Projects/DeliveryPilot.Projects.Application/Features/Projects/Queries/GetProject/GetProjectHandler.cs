using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProject;

public sealed class GetProjectHandler(ProjectAccessEvaluator access, IProjectReadStore store) : IQueryHandler<GetProjectQuery, ProjectDetailView>
{
    public async Task<Result<ProjectDetailView>> HandleAsync(GetProjectQuery request, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        ProjectAccess allowed = await access.RequireAsync(request.OrganizationId, request.ProjectId, identity, ProjectPermissions.ReadProject, cancellationToken);
        Project project = await store.FindProjectAsync(request.OrganizationId, request.ProjectId, cancellationToken) ?? throw ProjectAccessEvaluator.NotFound();
        bool member = allowed.Participant.Kind == ProjectParticipant.MemberKind;
        var view = new ProjectDetailView(project.Id, project.OrganizationId, project.Name, project.Purpose, project.Kind, project.Status, project.SprintLengthWeeks, project.ClientProductOwnerParticipantId, project.ActivatedAt, project.ActivatedByUserId, project.Version, allowed.Roles, allowed.Permissions.Order(StringComparer.Ordinal).ToArray())
        {
            EngagementId = member ? project.EngagementId : null,
            ClientPoWaiverReason = member ? project.ClientPoWaiverReason : null,
        };
        return Result.Success(view);
    }
}
