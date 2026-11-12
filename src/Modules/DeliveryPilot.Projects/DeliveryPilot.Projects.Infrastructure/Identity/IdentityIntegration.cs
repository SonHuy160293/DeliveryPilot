namespace DeliveryPilot.Projects.Infrastructure.Identity;

/// <summary>
/// Names and routes for the internal, non-Gateway Identity authority endpoints (TASK-DP-003-019).
/// </summary>
public static class IdentityIntegration
{
    public const string ClientName = "Projects.Identity";
    public const string ConfigurationSection = "Integrations:Identity";
    public const string SessionChecksEndpoint = "api/v1/integrations/authority/session-checks";
    public const string OrganizationChecksEndpoint = "api/v1/integrations/authority/organization-checks";
    public const string UserLookupEndpoint = "api/v1/integrations/users/lookup";
}
