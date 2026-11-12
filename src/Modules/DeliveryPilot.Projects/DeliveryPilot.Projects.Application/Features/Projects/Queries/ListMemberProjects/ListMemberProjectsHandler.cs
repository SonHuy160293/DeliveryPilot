using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListMemberProjects;

public sealed class ListMemberProjectsHandler(ProjectAccessEvaluator access, IProjectReadStore store) : IQueryHandler<ListMemberProjectsQuery, ProjectPage>
{
    public async Task<Result<ProjectPage>> HandleAsync(ListMemberProjectsQuery request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty) { throw new BadRequestException("Project.InvalidOrganization", "A nonempty workspace identifier is required."); }
        ProjectIdentity identity = access.CurrentIdentity();
        await access.RequireMemberWorkspaceAsync(request.OrganizationId, identity, cancellationToken);
        string status = ProjectQueryParameters.Status(request.Status);
        ProjectPageKey? after = ProjectCursor.Parse(request.Cursor, request.OrganizationId, identity.UserId, ProjectParticipant.MemberKind, status, request.Limit);
        return Result.Success(await store.ListAsync(request.OrganizationId, identity.UserId, ProjectParticipant.MemberKind, status, request.Limit, after, cancellationToken));
    }
}
