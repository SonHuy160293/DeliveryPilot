using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Users.Queries.GetUser;

public sealed class GetUserQueryHandler(IUserRepository users) : IQueryHandler<GetUserQuery, UserIntegrationDto>
{
    public async Task<Result<UserIntegrationDto>> HandleAsync(
        GetUserQuery request,
        CancellationToken cancellationToken)
    {
        User user = await users.GetByIdAsync(request.UserId, cancellationToken);
        return Result.Success(new UserIntegrationDto(user.Id, user.DisplayName, user.AvatarUrl, user.Status));
    }
}
