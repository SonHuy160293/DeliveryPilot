namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

public sealed record InvitationRoleSummary(Guid RoleId, string Name, long PermissionSetVersion);
