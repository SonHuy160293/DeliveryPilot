using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Engagements;

public sealed class Engagement
{
    public const string ActiveStatus = "Active";
    public const string ClosedStatus = "Closed";

    private Engagement() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ClientDisplayName { get; private set; } = null!;
    public string? ClientLegalName { get; private set; }
    public string? ClientCountryCode { get; private set; }
    public string? EngagementName { get; private set; }
    public string AgreementReference { get; private set; } = null!;
    public DateOnly AgreementSignedOn { get; private set; }
    public string? DocumentUrl { get; private set; }
    public decimal? ContractAmount { get; private set; }
    public string? ContractCurrency { get; private set; }
    public decimal? PlannedEffortPersonDays { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public Guid? ClientOrganizationId { get; private set; }
    public DateTimeOffset? ClientLinkedAt { get; private set; }
    public Guid? ClientLinkedByUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public void EnsureActive()
    {
        if (Status != ActiveStatus)
        {
            throw new ConflictException("Engagement.Closed", "The engagement is closed.");
        }
    }

    public void EnsureCanIssueLinkCode()
    {
        EnsureActive();
        if (ClientOrganizationId is not null)
        {
            throw new ConflictException("Engagement.AlreadyLinked", "The engagement is already linked.");
        }
    }

    public void RequireVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new ConflictException("Engagement.VersionConflict", "The engagement has changed.");
        }
    }

    public bool SetContractValue(decimal? amount, string? currency, decimal? effort, DateTimeOffset occurredAt)
    {
        EnsureActive();
        amount = EngagementFields.ContractAmount(amount);
        currency = EngagementFields.CurrencyCode(currency);
        effort = EngagementFields.PlannedEffortPersonDays(effort);
        if (amount is not null && currency is null)
        {
            throw new BadRequestException("Engagement.CurrencyRequired", "Contract currency is required when an amount is present.");
        }

        if (ContractAmount is not null && currency is not null && currency != ContractCurrency)
        {
            throw new ConflictException("Engagement.CurrencyLocked", "Clear the existing amount before changing currency.");
        }

        if (ContractAmount == amount && ContractCurrency == currency && PlannedEffortPersonDays == effort)
        {
            return false;
        }

        ContractAmount = amount;
        ContractCurrency = currency;
        PlannedEffortPersonDays = effort;
        RecordChange(occurredAt);
        return true;
    }

    public IReadOnlyDictionary<string, EngagementFieldChange> UpdateDetails(
        string? clientDisplayName,
        string? clientLegalName,
        string? clientCountryCode,
        string? engagementName,
        string? agreementReference,
        DateOnly agreementSignedOn,
        string? documentUrl,
        string? reason,
        DateTimeOffset occurredAt)
    {
        EnsureActive();
        string display = EngagementFields.RequiredText(clientDisplayName, 200, nameof(clientDisplayName));
        string? legal = EngagementFields.OptionalText(clientLegalName, 200, nameof(clientLegalName));
        string? country = EngagementFields.CountryCode(clientCountryCode);
        string? name = EngagementFields.OptionalText(engagementName, 200, nameof(engagementName));
        string reference = EngagementFields.RequiredText(agreementReference, 100, nameof(agreementReference));
        DateOnly signedOn = EngagementFields.CalendarDate(agreementSignedOn, nameof(agreementSignedOn));
        string? url = EngagementFields.DocumentUrl(documentUrl);
        EngagementFields.OptionalText(reason, 2000, nameof(reason));
        if (reference != AgreementReference || signedOn != AgreementSignedOn)
        {
            EngagementFields.RequiredText(reason, 2000, nameof(reason));
        }

        var changed = new Dictionary<string, EngagementFieldChange>();
        AddChange("clientDisplayName", ClientDisplayName, display);
        AddChange("clientLegalName", ClientLegalName, legal);
        AddChange("clientCountryCode", ClientCountryCode, country);
        AddChange("engagementName", EngagementName, name);
        AddChange("agreementReference", AgreementReference, reference);
        AddChange("agreementSignedOn", AgreementSignedOn, signedOn);
        AddChange("documentUrl", DocumentUrl, url);
        if (changed.Count > 0)
        {
            ClientDisplayName = display;
            ClientLegalName = legal;
            ClientCountryCode = country;
            EngagementName = name;
            AgreementReference = reference;
            AgreementSignedOn = signedOn;
            DocumentUrl = url;
            AdvanceVersion(occurredAt);
        }

        return changed;

        void AddChange<T>(string field, T previous, T current)
        {
            if (!EqualityComparer<T>.Default.Equals(previous, current))
            {
                changed.Add(field, new EngagementFieldChange(previous, current));
            }
        }
    }

    public void RecordChange(DateTimeOffset occurredAt)
    {
        EnsureActive();
        AdvanceVersion(occurredAt);
    }

    public Guid RemoveClientLink(DateTimeOffset occurredAt)
    {
        Guid clientOrganizationId = ClientOrganizationId
            ?? throw new ConflictException("Engagement.NotLinked", "The engagement is not linked.");
        ClientOrganizationId = null;
        ClientLinkedAt = null;
        ClientLinkedByUserId = null;
        AdvanceVersion(occurredAt);
        return clientOrganizationId;
    }

    public void EnsureCanConfirmClientLink(Guid clientOrganizationId)
    {
        if (Status != ActiveStatus || ClientOrganizationId is not null || clientOrganizationId == OrganizationId)
        {
            throw new NotFoundException("EngagementLink.InvalidCode", "The link code is invalid or unavailable.");
        }
    }

    public void ConfirmClientLink(Guid clientOrganizationId, Guid userId, DateTimeOffset now)
    {
        EnsureCanConfirmClientLink(clientOrganizationId);
        ClientOrganizationId = EngagementFields.NonEmptyId(clientOrganizationId, nameof(clientOrganizationId));
        ClientLinkedByUserId = EngagementFields.NonEmptyId(userId, nameof(userId));
        ClientLinkedAt = now.ToUniversalTime();
        AdvanceVersion(now);
    }

    public bool Close(Guid userId, string? reason, DateTimeOffset occurredAt)
    {
        EngagementFields.RequiredText(reason, 2000, nameof(reason));
        if (Status == ClosedStatus)
        {
            return false;
        }

        ClosedByUserId = EngagementFields.NonEmptyId(userId, nameof(userId));
        ClosedAt = occurredAt.ToUniversalTime();
        Status = ClosedStatus;
        AdvanceVersion(occurredAt);
        return true;
    }

    public bool Reopen(string? reason, DateTimeOffset occurredAt)
    {
        EngagementFields.RequiredText(reason, 2000, nameof(reason));
        if (Status == ActiveStatus)
        {
            return false;
        }

        Status = ActiveStatus;
        ClosedByUserId = null;
        ClosedAt = null;
        AdvanceVersion(occurredAt);
        return true;
    }

    public static Engagement Create(
        Guid organizationId,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        string clientDisplayName,
        string agreementReference,
        DateOnly agreementSignedOn,
        string? clientLegalName = null,
        string? clientCountryCode = null,
        string? engagementName = null,
        string? documentUrl = null,
        decimal? contractAmount = null,
        string? contractCurrency = null,
        decimal? plannedEffortPersonDays = null)
    {
        decimal? amount = EngagementFields.ContractAmount(contractAmount);
        string? currency = EngagementFields.CurrencyCode(contractCurrency);
        if (amount is not null && currency is null)
        {
            throw new BadRequestException("Engagement.CurrencyRequired", "Contract currency is required when an amount is present.");
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new Engagement
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = EngagementFields.NonEmptyId(organizationId, nameof(organizationId)),
            CreatedByUserId = EngagementFields.NonEmptyId(createdByUserId, nameof(createdByUserId)),
            ClientDisplayName = EngagementFields.RequiredText(clientDisplayName, 200, nameof(clientDisplayName)),
            ClientLegalName = EngagementFields.OptionalText(clientLegalName, 200, nameof(clientLegalName)),
            ClientCountryCode = EngagementFields.CountryCode(clientCountryCode),
            EngagementName = EngagementFields.OptionalText(engagementName, 200, nameof(engagementName)),
            AgreementReference = EngagementFields.RequiredText(agreementReference, 100, nameof(agreementReference)),
            AgreementSignedOn = EngagementFields.CalendarDate(agreementSignedOn, nameof(agreementSignedOn)),
            DocumentUrl = EngagementFields.DocumentUrl(documentUrl),
            ContractAmount = amount,
            ContractCurrency = currency,
            PlannedEffortPersonDays = EngagementFields.PlannedEffortPersonDays(plannedEffortPersonDays),
            Status = ActiveStatus,
            CreatedAt = instant,
            UpdatedAt = instant,
            Version = 1,
        };
    }

    private void AdvanceVersion(DateTimeOffset occurredAt)
    {
        UpdatedAt = occurredAt.ToUniversalTime();
        Version = checked(Version + 1);
    }
}
