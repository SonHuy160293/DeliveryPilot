using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OrganizationInvitations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReissueInvitationRequest(long ExpectedVersion, IReadOnlyList<Guid>? RoleIds, string? Reason);
