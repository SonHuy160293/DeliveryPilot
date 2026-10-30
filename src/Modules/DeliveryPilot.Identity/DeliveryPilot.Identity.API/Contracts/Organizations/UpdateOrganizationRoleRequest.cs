using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.Organizations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateOrganizationRoleRequest(string? Name, string? Description, IReadOnlyList<string>? PermissionCodes, long ExpectedVersion);
