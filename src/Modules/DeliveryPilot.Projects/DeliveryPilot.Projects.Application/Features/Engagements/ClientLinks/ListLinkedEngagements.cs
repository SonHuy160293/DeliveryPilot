using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public sealed record ListLinkedEngagementsQuery(Guid OrganizationId) : IQuery<IReadOnlyList<LinkedEngagement>>;

public sealed class ListLinkedEngagementsHandler(ClientLinkAuthorization authorization, IClientLinkStore store) : IQueryHandler<ListLinkedEngagementsQuery, IReadOnlyList<LinkedEngagement>>
{
    public async Task<Result<IReadOnlyList<LinkedEngagement>>> HandleAsync(ListLinkedEngagementsQuery request, CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        return Result.Success(await store.ListAsync(request.OrganizationId, cancellationToken));
    }
}
