using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.ChangeRoleAssignment;

public sealed record ChangeRoleAssignmentCommand(Guid OrganizationId, Guid MembershipId, Guid RoleId, long ExpectedMembershipVersion, string? Reason, bool Grant, string CorrelationId) : ICommand<ChangeRoleAssignmentResponse>;
