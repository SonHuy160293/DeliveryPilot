using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckSession;

public sealed class CheckSessionQueryHandler(IOrganizationAuthorization authorization)
    : IQueryHandler<CheckSessionQuery, SessionCheckResult>
{
    public async Task<Result<SessionCheckResult>> HandleAsync(
        CheckSessionQuery request,
        CancellationToken cancellationToken)
    {
        bool active = await authorization.IsSessionActiveAsync(request.UserId, request.SessionId, cancellationToken);
        return Result.Success(new SessionCheckResult(active));
    }
}
