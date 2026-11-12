using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.MatchUserEmail;

public sealed record MatchUserEmailQuery(Guid? UserId, string? Email) : IQuery<UserEmailMatchResult>
{
    public override string ToString() => nameof(MatchUserEmailQuery);
}
