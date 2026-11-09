using System.Text.Json;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Tests.Domain;

public sealed class EngagementCreationTests
{
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly DateOnly _signedOn = new(2026, 11, 1);
    private static readonly DateTimeOffset _instant = new(2026, 11, 10, 10, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void EngagementNormalizesFieldsAndStartsActiveWithUnknownValues()
    {
        var engagement = Engagement.Create(
            _organizationId,
            _userId,
            _instant,
            "  Client  ",
            " FP-1 ",
            _signedOn,
            clientLegalName: " Legal ",
            clientCountryCode: " vn ",
            engagementName: "  ",
            documentUrl: " https://example.com/agreement ");

        Assert.Equal(7, engagement.Id.Version);
        Assert.Equal(_organizationId, engagement.OrganizationId);
        Assert.Equal(_userId, engagement.CreatedByUserId);
        Assert.Equal("Client", engagement.ClientDisplayName);
        Assert.Equal("Legal", engagement.ClientLegalName);
        Assert.Equal("VN", engagement.ClientCountryCode);
        Assert.Null(engagement.EngagementName);
        Assert.Equal("FP-1", engagement.AgreementReference);
        Assert.Equal(_signedOn, engagement.AgreementSignedOn);
        Assert.Equal("https://example.com/agreement", engagement.DocumentUrl);
        Assert.Equal(Engagement.ActiveStatus, engagement.Status);
        Assert.Equal(1, engagement.Version);
        Assert.Equal(TimeSpan.Zero, engagement.CreatedAt.Offset);
        Assert.Equal(_instant.ToUniversalTime(), engagement.CreatedAt);
        Assert.Equal(engagement.CreatedAt, engagement.UpdatedAt);
        Assert.Null(engagement.ContractAmount);
        Assert.Null(engagement.ContractCurrency);
        Assert.Null(engagement.PlannedEffortPersonDays);
        Assert.Null(engagement.ClosedAt);
        Assert.Null(engagement.ClosedByUserId);
        Assert.Null(engagement.ClientOrganizationId);
        Assert.Null(engagement.ClientLinkedAt);
        Assert.Null(engagement.ClientLinkedByUserId);
    }

    [Theory]
    [InlineData("client-name")]
    [InlineData("agreement")]
    [InlineData("signed-date")]
    [InlineData("legal-name")]
    [InlineData("engagement-name")]
    [InlineData("document-length")]
    [InlineData("http")]
    [InlineData("relative-url")]
    [InlineData("country")]
    [InlineData("currency")]
    [InlineData("missing-currency")]
    [InlineData("amount-precision")]
    [InlineData("effort-precision")]
    [InlineData("negative-amount")]
    [InlineData("negative-effort")]
    public void EngagementRejectsInvalidFields(string scenario)
    {
        string? documentUrl = scenario switch
        {
            "document-length" => "https://example.com/" + new string('x', 2000),
            "http" => "http://example.com",
            "relative-url" => "/agreement",
            _ => null,
        };
        decimal? amount = scenario switch { "missing-currency" => 1m, "amount-precision" => 1.001m, "negative-amount" => -1m, _ => null };
        decimal? effort = scenario switch { "effort-precision" => 1.25m, "negative-effort" => -1m, _ => null };
        Assert.Throws<BadRequestException>(() => Engagement.Create(
            _organizationId,
            _userId,
            _instant,
            scenario == "client-name" ? new string('x', 201) : "Client",
            scenario == "agreement" ? " " : "FP-1",
            scenario == "signed-date" ? default : _signedOn,
            clientLegalName: scenario == "legal-name" ? new string('x', 201) : null,
            engagementName: scenario == "engagement-name" ? new string('x', 201) : null,
            documentUrl: documentUrl,
            clientCountryCode: scenario == "country" ? "ZZ" : null,
            contractAmount: amount,
            contractCurrency: scenario == "currency" ? "XXA" : scenario == "missing-currency" ? null : "USD",
            plannedEffortPersonDays: effort));
    }

    [Fact]
    public void FieldBoundariesAndZeroValuesAreAcceptedWithoutRounding()
    {
        var engagement = Engagement.Create(
            _organizationId,
            _userId,
            _instant,
            new string('x', 200),
            new string('x', 100),
            _signedOn,
            contractAmount: decimal.MaxValue,
            contractCurrency: " usd ",
            plannedEffortPersonDays: 0.0m);

        Assert.Equal(decimal.MaxValue, engagement.ContractAmount);
        Assert.Equal("USD", engagement.ContractCurrency);
        Assert.Equal(0m, engagement.PlannedEffortPersonDays);
        Assert.Equal(0m, EngagementFields.ContractAmount(0.000m));
        Assert.Equal(1.23m, EngagementFields.ContractAmount(1.230m));
        Assert.Equal(1.2m, EngagementFields.PlannedEffortPersonDays(1.200m));
    }

    [Theory]
    [InlineData("AQ")]
    [InlineData("TW")]
    [InlineData("BQ")]
    public void CountryCatalogIncludesTerritoriesWithoutDotNetCultures(string country)
        => Assert.Equal(country, EngagementFields.CountryCode(country));

    [Fact]
    public void MilestoneAndContactInitializeTheirOwnVersionsAndNormalizeData()
    {
        var engagementId = Guid.CreateVersion7();
        var milestone = EngagementMilestone.Create(_organizationId, engagementId, _userId, _instant, " Launch ", _signedOn, " Description ");
        var contact = EngagementContact.Create(_organizationId, engagementId, _userId, _instant, " Ada ", " ADA@EXAMPLE.COM ", EngagementContact.ClientProductOwnerRole);

        Assert.Equal(7, milestone.Id.Version);
        Assert.Equal(7, contact.Id.Version);
        Assert.Equal("Launch", milestone.Name);
        Assert.Equal("Description", milestone.Description);
        Assert.Equal(_signedOn, milestone.OriginalTargetDate);
        Assert.Equal(_signedOn, milestone.CurrentTargetDate);
        Assert.Equal(EngagementMilestone.PlannedStatus, milestone.Status);
        Assert.Equal(1, milestone.Version);
        Assert.Equal("Ada", contact.Name);
        Assert.Equal("ada@example.com", contact.Email);
        Assert.Equal(EngagementContact.ClientProductOwnerRole, contact.ContactRole);
        Assert.Equal(1, contact.Version);
        Assert.Null(contact.RemovedAt);
        Assert.Null(contact.RemovedByUserId);
        Assert.Equal(TimeSpan.Zero, milestone.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, contact.CreatedAt.Offset);
    }

    [Fact]
    public void ChildFactoriesRejectInvalidFieldsAndEmptyIdentifiers()
    {
        var engagementId = Guid.CreateVersion7();
        Assert.Throws<BadRequestException>(() => EngagementMilestone.Create(_organizationId, engagementId, _userId, _instant, " ", _signedOn));
        Assert.Throws<BadRequestException>(() => EngagementMilestone.Create(_organizationId, engagementId, _userId, _instant, "Name", default));
        Assert.Throws<BadRequestException>(() => EngagementMilestone.Create(_organizationId, engagementId, _userId, _instant, "Name", _signedOn, new string('x', 2001)));
        Assert.Throws<BadRequestException>(() => EngagementContact.Create(_organizationId, engagementId, _userId, _instant, "Ada", "not-email", EngagementContact.OtherRole));
        Assert.Throws<BadRequestException>(() => EngagementContact.Create(_organizationId, engagementId, _userId, _instant, "Ada", "Ada <ada@example.com>", EngagementContact.OtherRole));
        Assert.Throws<BadRequestException>(() => EngagementContact.Create(_organizationId, engagementId, _userId, _instant, "Ada", "ada@example.com", "Unknown"));
        Assert.Throws<ArgumentException>(() => Engagement.Create(Guid.Empty, _userId, _instant, "Client", "FP-1", _signedOn));
        Assert.Throws<ArgumentException>(() => EngagementLinkAttempt.Create(Guid.Empty, _organizationId, _instant));
    }

    [Fact]
    public void LinkCodeCopiesVerifierAndUsesExactlySevenElapsedDays()
    {
        byte[] verifier = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();
        var code = EngagementLinkCode.Create(_organizationId, Guid.CreateVersion7(), _userId, _instant, verifier);
        verifier[0] = 255;
        byte[] exposed = code.CodeVerifier;
        exposed[1] = 255;

        Assert.Equal(7, code.Id.Version);
        Assert.Equal(0, code.CodeVerifier[0]);
        Assert.Equal(1, code.CodeVerifier[1]);
        Assert.Equal(TimeSpan.FromDays(7), code.ExpiresAt - code.IssuedAt);
        Assert.Equal(TimeSpan.Zero, code.IssuedAt.Offset);
        Assert.Equal(EngagementLinkCode.OutstandingStatus, code.Status);
        Assert.Equal(1, code.Version);
        Assert.Null(code.ConsumedAt);
        Assert.Null(code.ConsumedByUserId);
        Assert.Null(code.ConsumedForOrganizationId);
        Assert.Null(code.InvalidatedAt);
        Assert.Throws<ArgumentException>(() => EngagementLinkCode.Create(_organizationId, Guid.CreateVersion7(), _userId, _instant, new byte[31]));
    }

    [Fact]
    public void AttributionAllowsClientOrganizationAndRequiresObjectDetails()
    {
        var engagementId = Guid.CreateVersion7();
        var clientOrganizationId = Guid.NewGuid();
        var entry = EngagementEvent.Create(_organizationId, engagementId, "link.confirmed", "Engagement", engagementId, _userId, clientOrganizationId, _instant, 2, "{}", " Reason ");
        var attempt = EngagementLinkAttempt.Create(_userId, clientOrganizationId, _instant);

        Assert.Equal(7, entry.Id.Version);
        Assert.Equal(7, attempt.Id.Version);
        Assert.Equal(clientOrganizationId, entry.ActorOrganizationId);
        Assert.Equal("Reason", entry.Reason);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
        Assert.Equal(TimeSpan.Zero, attempt.AttemptedAt.Offset);
        Assert.Throws<ArgumentException>(() => EngagementEvent.Create(_organizationId, engagementId, "test", "Engagement", engagementId, _userId, _organizationId, _instant, 1, "[]"));
        Assert.ThrowsAny<JsonException>(() => EngagementEvent.Create(_organizationId, engagementId, "test", "Engagement", engagementId, _userId, _organizationId, _instant, 1, "invalid"));
        Assert.Throws<ArgumentOutOfRangeException>(() => EngagementEvent.Create(_organizationId, engagementId, "test", "Engagement", engagementId, _userId, _organizationId, _instant, 0, "{}"));
    }
}
