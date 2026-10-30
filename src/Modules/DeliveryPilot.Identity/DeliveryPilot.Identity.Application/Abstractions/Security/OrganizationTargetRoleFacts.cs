namespace DeliveryPilot.Identity.Application.Abstractions.Security;

// Target protection describes current grants; it never authorizes an inactive account to act.
public sealed record OrganizationTargetRoleFacts(Guid MembershipId, bool HasOwnerGrant, bool HasAdminGrant);
