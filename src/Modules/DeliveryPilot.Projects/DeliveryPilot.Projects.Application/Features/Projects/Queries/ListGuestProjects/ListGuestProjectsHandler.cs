using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListGuestProjects;

public sealed class ListGuestProjectsHandler(ProjectAccessEvaluator access, IProjectReadStore store) : IQueryHandler<ListGuestProjectsQuery, ProjectPage>
{
    public async Task<Result<ProjectPage>> HandleAsync(ListGuestProjectsQuery request, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        string status = ProjectQueryParameters.Status(request.Status);
        ProjectPageKey? after = ProjectCursor.Parse(request.Cursor, Guid.Empty, identity.UserId, ProjectParticipant.GuestKind, status, request.Limit);
        return Result.Success(await store.ListAsync(Guid.Empty, identity.UserId, ProjectParticipant.GuestKind, status, request.Limit, after, cancellationToken));
    }
}
