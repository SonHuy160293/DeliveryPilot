namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

public sealed record InvitationDecline(string Outcome, string Status, long Version);
