using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ReissueInvitation;

public sealed record ReissueInvitationCommand(Guid OrganizationId, Guid InvitationId, long ExpectedVersion, IReadOnlyList<Guid>? RoleIds, string? Reason, string CorrelationId) : ICommand<InvitationSummary>;
