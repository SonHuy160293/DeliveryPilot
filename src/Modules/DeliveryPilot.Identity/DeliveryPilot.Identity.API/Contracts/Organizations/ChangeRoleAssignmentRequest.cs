using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.Organizations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeRoleAssignmentRequest(long ExpectedMembershipVersion, string? Reason);
