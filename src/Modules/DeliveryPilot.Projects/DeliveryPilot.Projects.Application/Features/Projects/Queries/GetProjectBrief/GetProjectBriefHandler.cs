using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProjectBrief;

public sealed class GetProjectBriefHandler(ProjectAccessEvaluator access, IProjectReadStore store) : IQueryHandler<GetProjectBriefQuery, ProjectBriefView>
{
    public async Task<Result<ProjectBriefView>> HandleAsync(GetProjectBriefQuery request, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        ProjectAccess allowed = await access.RequireAsync(request.OrganizationId, request.ProjectId, identity, ProjectPermissions.ReadProject, cancellationToken);
        ProjectBrief brief = await store.FindBriefAsync(request.OrganizationId, request.ProjectId, cancellationToken) ?? throw ProjectAccessEvaluator.NotFound();
        bool full = allowed.Participant.Kind == ProjectParticipant.MemberKind || allowed.Permissions.Contains(ProjectPermissions.EditBrief);
        return Result.Success(ProjectBriefView.From(brief, full));
    }
}
