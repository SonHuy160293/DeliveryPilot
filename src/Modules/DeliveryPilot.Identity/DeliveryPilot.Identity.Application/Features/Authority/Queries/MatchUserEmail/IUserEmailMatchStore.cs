namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.MatchUserEmail;

public interface IUserEmailMatchStore
{
    Task<bool> IsActiveUserWithPrimaryEmailAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken);
}
