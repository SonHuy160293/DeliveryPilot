namespace DeliveryPilot.SharedKernel.Integrations;

public sealed record IntegrationError(
    string Code,
    string Message,
    IntegrationFailureCategory Category,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
