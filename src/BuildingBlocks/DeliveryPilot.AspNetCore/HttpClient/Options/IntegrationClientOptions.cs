using DeliveryPilot.AspNetCore.HttpClient.Options.Resilience;

namespace DeliveryPilot.AspNetCore.HttpClient.Options;

public sealed class IntegrationClientOptions
{
    public Uri? BaseUrl { get; init; }

    public string ApiKeyHeaderName { get; init; } = "X-Integration-Api-Key";

    public string ApiKey { get; init; } = string.Empty;

    public IntegrationResilienceOptions Resilience { get; init; } = new();
}
