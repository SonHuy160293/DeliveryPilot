namespace DeliveryPilot.Projects.Tests.Support;

/// <summary>Non-secret, test-only host settings.</summary>
internal static class TestConfiguration
{
    public const string IdentityBaseUrl = "http://identity.test/";
    public const string IntegrationApiKey = "projects-test-integration-key";

    public static Dictionary<string, string?> Host() => new()
    {
        ["ConnectionStrings:ApplicationDb"] = "Host=localhost;Database=DeliveryPilotProjectsUnused;Username=unused",
        ["Jwt:SecretKey"] = "projects-test-only-signing-key-0123456789abcdef",
        ["Jwt:Issuer"] = "DeliveryPilot.Identity",
        ["Jwt:Audience"] = "DeliveryPilot",
        ["Jwt:AccessTokenExpirationMinutes"] = "10",
        ["Cors:AllowedOrigins:0"] = "https://app.example.test",
        ["Cors:AllowedMethods:0"] = "GET",
        ["Cors:AllowedHeaders:0"] = "Authorization",
        ["Integrations:Identity:BaseUrl"] = IdentityBaseUrl,
        ["Integrations:Identity:ApiKeyHeaderName"] = "X-Integration-Key",
        ["Integrations:Identity:ApiKey"] = IntegrationApiKey,
        ["IntegrationApi:ApiKeys:0"] = "projects-test-inbound-integration-key",
    };
}
