namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

public sealed record InvitationDeliverySummary(string Status, string? FailureCode, DateTimeOffset UpdatedAt);
