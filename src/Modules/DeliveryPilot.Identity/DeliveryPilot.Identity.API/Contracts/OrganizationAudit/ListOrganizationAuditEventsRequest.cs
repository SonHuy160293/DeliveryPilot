namespace DeliveryPilot.Identity.API.Contracts.OrganizationAudit;

public sealed record ListOrganizationAuditEventsRequest(
    string? ActorUserId = null,
    string? MembershipId = null,
    string? TargetId = null,
    string? Action = null,
    string? From = null,
    string? To = null,
    int Limit = 50,
    string? Cursor = null);
