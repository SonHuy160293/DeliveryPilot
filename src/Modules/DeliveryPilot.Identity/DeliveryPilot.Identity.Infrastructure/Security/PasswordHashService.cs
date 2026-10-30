using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace DeliveryPilot.Identity.Infrastructure.Security;

public sealed class PasswordHashService : IPasswordHashService
{
    private static readonly User _dummyUser = User.Create("timing-equalization@example.invalid", "Timing Equalization");
    private static readonly string _dummyHash = new PasswordHasher<User>().HashPassword(_dummyUser, "timing-equalization-dummy-secret");

    private readonly PasswordHasher<User> _hasher = new();

    public Task<string> HashAsync(User user, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_hasher.HashPassword(user, password));
    }

    public Task<bool> VerifyAsync(User? user, string? passwordHash, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(password);

        if (user is null || string.IsNullOrWhiteSpace(passwordHash))
        {
            _ = _hasher.VerifyHashedPassword(_dummyUser, _dummyHash, password);
            return Task.FromResult(false);
        }

        try
        {
            PasswordVerificationResult result = _hasher.VerifyHashedPassword(user, passwordHash, password);
            return Task.FromResult(result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded);
        }
        catch (FormatException)
        {
            return Task.FromResult(false);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(false);
        }
    }
}
