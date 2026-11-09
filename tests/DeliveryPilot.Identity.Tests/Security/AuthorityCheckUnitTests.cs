using System.Text.Json;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationAuthority;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckSession;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class AuthorityCheckUnitTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SessionCheckValidatorRejectsEmptyIdentifiers()
    {
        var validator = new CheckSessionQueryValidator();
        CqrsValidationException error = await Assert.ThrowsAsync<CqrsValidationException>(() =>
            validator.ValidateAsync(new CheckSessionQuery(Guid.Empty, Guid.Empty), CancellationToken.None));
        Assert.Equal(2, error.Errors.Count);
        await validator.ValidateAsync(new CheckSessionQuery(Guid.CreateVersion7(), Guid.CreateVersion7()), CancellationToken.None);
    }

    [Theory]
    [InlineData("Organization.Members.Read")]
    [InlineData("organization members")]
    [InlineData("")]
    public async Task OrganizationCheckValidatorRejectsMalformedPermissionCodes(string code)
    {
        var validator = new CheckOrganizationAuthorityQueryValidator();
        await Assert.ThrowsAsync<CqrsValidationException>(() => validator.ValidateAsync(
            new CheckOrganizationAuthorityQuery(Guid.CreateVersion7(), Guid.CreateVersion7(), null, code),
            CancellationToken.None));
    }

    [Fact]
    public async Task OrganizationCheckValidatorRejectsEmptyIdsAndOversizeCodes()
    {
        var validator = new CheckOrganizationAuthorityQueryValidator();
        CqrsValidationException error = await Assert.ThrowsAsync<CqrsValidationException>(() => validator.ValidateAsync(
            new CheckOrganizationAuthorityQuery(Guid.Empty, Guid.Empty, Guid.Empty, new string('a', CheckOrganizationAuthorityQueryValidator.MaximumPermissionCodeLength + 1)),
            CancellationToken.None));
        Assert.Equal(4, error.Errors.Count);
        await validator.ValidateAsync(
            new CheckOrganizationAuthorityQuery(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "organization.members.read"),
            CancellationToken.None);
    }

    [Fact]
    public void DeniedResultSerializesOnlyDecisionAndTime()
    {
        string json = JsonSerializer.Serialize(
            OrganizationAuthorityCheckResult.Denied(DateTimeOffset.UnixEpoch),
            WebJson);
        using JsonDocument document = JsonDocument.Parse(json);
        string[] names = document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["decision", "evaluatedAt"], names);
        Assert.Equal(AuthorityDecision.Denied, document.RootElement.GetProperty("decision").GetString());
    }

    [Fact]
    public async Task UnavailableStoreFailsClosedForBothChecks()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=none;Password=none;Timeout=1;Pooling=false")
            .Options;
        await using var context = new IdentityDbContext(options);
        var authorization = new OrganizationAuthorizationService(context);

        ServiceUnavailableException session = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            authorization.IsSessionActiveAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));
        Assert.Equal(OrganizationAuthorizationService.AuthorityUnavailableErrorCode, session.Code);

        ServiceUnavailableException organization = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            authorization.EvaluateActorAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null));
        Assert.Equal(OrganizationAuthorizationService.AuthorityUnavailableErrorCode, organization.Code);
    }

    [Fact]
    public async Task EmptyIdentifiersAreDeniedWithoutTouchingTheStore()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=none;Password=none;Timeout=1;Pooling=false")
            .Options;
        await using var context = new IdentityDbContext(options);
        var authorization = new OrganizationAuthorizationService(context);

        Assert.False(await authorization.IsSessionActiveAsync(Guid.Empty, Guid.CreateVersion7()));
        Assert.Null(await authorization.EvaluateActorAsync(Guid.Empty, Guid.CreateVersion7(), null, null));
        Assert.Null(await authorization.EvaluateActorAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, null));
    }
}
