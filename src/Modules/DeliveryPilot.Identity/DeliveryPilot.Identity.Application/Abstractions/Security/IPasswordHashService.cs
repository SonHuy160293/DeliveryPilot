using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public interface IPasswordHashService
{
    Task<string> HashAsync(User user, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies <paramref name="password"/> against <paramref name="passwordHash"/> for <paramref name="user"/>.
    /// When <paramref name="user"/> or <paramref name="passwordHash"/> is null, performs a dummy PBKDF2
    /// verification to equalize timing and returns false. Accepts <c>SuccessRehashNeeded</c> as valid
    /// without mutating stored credentials.
    /// </summary>
    Task<bool> VerifyAsync(User? user, string? passwordHash, string password, CancellationToken cancellationToken);
}
