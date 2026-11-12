using DeliveryPilot.Projects.Application.Abstractions.Identity;
using DeliveryPilot.Projects.Application.Features.Projects.Access;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectParticipants;

public sealed class ListProjectParticipantsHandler(ProjectAccessEvaluator access, IProjectReadStore store, IUserDirectory directory) : IQueryHandler<ListProjectParticipantsQuery, IReadOnlyList<ProjectParticipantView>>
{
    public async Task<Result<IReadOnlyList<ProjectParticipantView>>> HandleAsync(ListProjectParticipantsQuery request, CancellationToken cancellationToken)
    {
        ProjectIdentity identity = access.CurrentIdentity();
        await access.RequireAsync(request.OrganizationId, request.ProjectId, identity, ProjectPermissions.ReadProject, cancellationToken);
        IReadOnlyList<ProjectParticipation> participants = await store.ListParticipantsAsync(request.OrganizationId, request.ProjectId, cancellationToken);
        IReadOnlyDictionary<Guid, string> names;
        try { names = await directory.LookupDisplayNamesAsync(participants.Select(x => x.Participant.UserId).Distinct().ToArray(), cancellationToken); }
        catch (ServiceUnavailableException) { names = new Dictionary<Guid, string>(); }
        IReadOnlyList<ProjectParticipantView> result = participants.Select(x => new ProjectParticipantView(x.Participant.UserId, x.Participant.Kind, x.Roles, names.GetValueOrDefault(x.Participant.UserId))).ToArray();
        return Result.Success(result);
    }
}
