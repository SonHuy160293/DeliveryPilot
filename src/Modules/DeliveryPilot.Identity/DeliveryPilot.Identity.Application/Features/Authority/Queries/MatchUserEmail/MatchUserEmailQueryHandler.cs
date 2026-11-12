using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.MatchUserEmail;

public sealed class MatchUserEmailQueryHandler(IUserEmailMatchStore users, TimeProvider timeProvider) : IQueryHandler<MatchUserEmailQuery, UserEmailMatchResult>
{
    public async Task<Result<UserEmailMatchResult>> HandleAsync(MatchUserEmailQuery request, CancellationToken cancellationToken)
    {
        bool matches = await users.IsActiveUserWithPrimaryEmailAsync(request.UserId!.Value, User.NormalizeEmail(request.Email!), cancellationToken);
        return Result.Success(new UserEmailMatchResult(matches, timeProvider.GetUtcNow()));
    }
}
