using System.Text.Json;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationAuthority;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckSession;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationMembership;
using DeliveryPilot.Identity.Application.Features.Authority.Queries.MatchUserEmail;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
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
    public async Task NewChecksValidateAndRedactTheirRecords()
    {
        Guid id = Guid.NewGuid();
        var membership = new CheckOrganizationMembershipQueryValidator();
        foreach (CheckOrganizationMembershipQuery query in new CheckOrganizationMembershipQuery[]
        {
            new(null, [id]), new(Guid.Empty, [id]), new(id, null), new(id, []), new(id, [Guid.Empty]),
            new(id, Enumerable.Repeat(id, 101).ToArray()),
        })
        {
            await Assert.ThrowsAsync<CqrsValidationException>(() => membership.ValidateAsync(query, CancellationToken.None));
        }

        await membership.ValidateAsync(new(id, Enumerable.Repeat(id, 100).ToArray()), CancellationToken.None);
        var email = new MatchUserEmailQueryValidator();
        foreach (MatchUserEmailQuery query in new MatchUserEmailQuery[]
        {
            new(null, "abc"), new(Guid.Empty, "abc"), new(id, null), new(id, "  "), new(id, " ab "), new(id, new string('a', 321)),
        })
        {
            CqrsValidationException error = await Assert.ThrowsAsync<CqrsValidationException>(() => email.ValidateAsync(query, CancellationToken.None));
            Assert.DoesNotContain(query.Email ?? "unavailable-secret", string.Join(" ", error.Errors), StringComparison.Ordinal);
        }

        await email.ValidateAsync(new(id, "abc"), CancellationToken.None);
        object[] records = [new OrganizationMembershipCheckRequest(id, [id]), new OrganizationMembershipCheckResult(DateTimeOffset.UtcNow, []), new OrganizationMembershipCheckItem(id, true), new UserEmailMatchRequest(id, "secret@example.test"), new UserEmailMatchResult(true, DateTimeOffset.UtcNow), new CheckOrganizationMembershipQuery(id, [id]), new MatchUserEmailQuery(id, "secret@example.test")];
        foreach (object record in records)
        {
            Assert.Equal(record.GetType().Name, record.ToString());
        }
    }

    [Fact]
    public async Task NewStoresFailClosedWhenDatabaseIsUnavailable()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=none;Password=none;Timeout=1;Pooling=false").Options;
        await using var context = new IdentityDbContext(options);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => new OrganizationAuthorizationService(context).GetActiveMemberUserIdsAsync(Guid.NewGuid(), [Guid.NewGuid()]));
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => new UserEmailMatchStore(context).IsActiveUserWithPrimaryEmailAsync(Guid.NewGuid(), "secret@example.test", CancellationToken.None));
    }

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
