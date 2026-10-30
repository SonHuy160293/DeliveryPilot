using DeliveryPilot.Identity.Domain.Models;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Persistence;

public sealed class RegistrationModelTests
{
    [Fact]
    public void UserCreationNormalizesIdentityAndKeepsActivationExplicit()
    {
        var user = User.Create(" Alice@EXAMPLE.test ", " Alice ");
        Assert.Equal("alice@example.test", user.PrimaryEmail);
        Assert.Equal("Alice", user.DisplayName);
        Assert.Equal("pending", user.Status);
        Assert.Equal("active", User.Create("alice@example.test", "Alice", status: "active").Status);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Null(user.LastLoginAt);
        Assert.Throws<ArgumentException>(() => User.Create("  ", "Alice"));
        Assert.Throws<ArgumentException>(() => User.Create("alice@example.test", "  "));
        Assert.Throws<ArgumentException>(() => User.Create(new string('a', 321), "Alice"));
        Assert.Throws<ArgumentException>(() => User.Create("alice@example.test", new string('a', 161)));
    }

    [Fact]
    public void CredentialRequiresOwnerAndProtectedVerifierAndRedactsDiagnostics()
    {
        Guid id = Guid.CreateVersion7();
        const string verifier = "test-version-1$opaque-protected-verifier";
        var createdAt = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(7));
        var credential = PasswordCredential.Create(id, verifier, createdAt);
        Assert.Equal(id, credential.UserId);
        Assert.Equal(verifier, credential.PasswordHash);
        Assert.Equal(createdAt.ToUniversalTime(), credential.CreatedAt);
        Assert.Equal(TimeSpan.Zero, credential.CreatedAt.Offset);
        Assert.DoesNotContain(verifier, credential.ToString());
        Assert.Throws<ArgumentException>(() => PasswordCredential.Create(Guid.Empty, verifier, createdAt));
        Assert.Throws<ArgumentException>(() => PasswordCredential.Create(id, " ", createdAt));
    }
}
