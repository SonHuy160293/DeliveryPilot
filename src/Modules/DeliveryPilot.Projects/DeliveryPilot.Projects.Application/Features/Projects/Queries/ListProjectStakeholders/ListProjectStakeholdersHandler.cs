using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectStakeholders;

public sealed class ListProjectStakeholdersHandler(ProjectAccessEvaluator access, IProjectReadStore store) : IQueryHandler<ListProjectStakeholdersQuery, IReadOnlyList<ProjectStakeholderView>>
{
    public async Task<Result<IReadOnlyList<ProjectStakeholderView>>> HandleAsync(ListProjectStakeholdersQuery request, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        ProjectAccess allowed = await access.RequireAsync(request.OrganizationId, request.ProjectId, identity, ProjectPermissions.ReadProject, cancellationToken);
        if (allowed.Participant.Kind == ProjectParticipant.GuestKind && !allowed.Permissions.Contains(ProjectPermissions.EditStakeholders))
        {
            throw new ForbiddenException("Project.Forbidden", "You do not have permission for this project action.");
        }

        IReadOnlyList<ProjectStakeholder> items = await store.ListStakeholdersAsync(request.OrganizationId, request.ProjectId, request.IncludeRetired, cancellationToken);
        return Result.Success<IReadOnlyList<ProjectStakeholderView>>(items.Select(ProjectStakeholderView.From).ToArray());
    }
}
