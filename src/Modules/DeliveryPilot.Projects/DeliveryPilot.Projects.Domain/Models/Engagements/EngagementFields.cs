using System.Net.Mail;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Engagements;

/// <summary>Creation rules shared by engagement entities and their future mutation methods.</summary>
public static class EngagementFields
{
    public static string RequiredText(string? value, int maximumLength, string field)
    {
        string? normalized = OptionalText(value, maximumLength, field);
        return normalized ?? throw new BadRequestException("Engagement.InvalidField", $"{field} is required.");
    }

    public static string? OptionalText(string? value, int maximumLength, string field)
    {
        string? normalized = value?.Trim();
        if (normalized is { Length: > 0 } && normalized.Length > maximumLength)
        {
            throw new BadRequestException("Engagement.InvalidField", $"{field} exceeds its maximum length.");
        }

        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    public static string? CountryCode(string? value)
    {
        string? code = OptionalText(value, 2, "ClientCountryCode")?.ToUpperInvariant();
        if (code is not null && !EngagementIsoCodes.Countries.Contains(code))
        {
            throw new BadRequestException("Engagement.InvalidCountry", "A valid ISO 3166-1 alpha-2 country code is required.");
        }

        return code;
    }

    public static string? CurrencyCode(string? value)
    {
        string? code = OptionalText(value, 3, "ContractCurrency")?.ToUpperInvariant();
        if (code is not null && !EngagementIsoCodes.Currencies.Contains(code))
        {
            throw new BadRequestException("Engagement.InvalidCurrency", "A valid ISO 4217 currency code is required.");
        }

        return code;
    }

    public static decimal? ContractAmount(decimal? value)
        => NonNegativeDecimal(value, 2, "ContractAmount");

    public static decimal? PlannedEffortPersonDays(decimal? value)
        => NonNegativeDecimal(value, 1, "PlannedEffortPersonDays");

    public static string? DocumentUrl(string? value)
    {
        string? normalized = OptionalText(value, 2000, "DocumentUrl");
        if (normalized is not null &&
            (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host)))
        {
            throw new BadRequestException("Engagement.InvalidDocumentUrl", "An absolute HTTPS document URL is required.");
        }

        return normalized;
    }

    public static string Email(string? value)
    {
        string email = RequiredText(value, 320, "Email").ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out MailAddress? address) || !string.Equals(address.Address, email, StringComparison.Ordinal))
        {
            throw new BadRequestException("Engagement.InvalidEmail", "A valid contact email is required.");
        }

        return email;
    }

    public static Guid NonEmptyId(Guid value, string field)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A nonempty identifier is required.", field);
        }

        return value;
    }

    public static DateOnly CalendarDate(DateOnly value, string field)
    {
        if (value == default)
        {
            throw new BadRequestException("Engagement.InvalidDate", $"{field} is required.");
        }

        return value;
    }

    private static decimal? NonNegativeDecimal(decimal? value, int decimals, string field)
    {
        if (value is < 0 || (value is not null && decimal.Round(value.Value, decimals) != value.Value))
        {
            throw new BadRequestException("Engagement.InvalidValue", $"{field} must be non-negative with at most {decimals} decimal places.");
        }

        return value;
    }
}
