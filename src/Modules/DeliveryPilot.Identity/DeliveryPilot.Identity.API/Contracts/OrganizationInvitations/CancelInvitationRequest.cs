using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OrganizationInvitations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelInvitationRequest(long ExpectedVersion, string? Reason);
