using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.OrganizationInvitations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IssueInvitationRequest(string? RecipientEmail, IReadOnlyList<Guid>? RoleIds);
