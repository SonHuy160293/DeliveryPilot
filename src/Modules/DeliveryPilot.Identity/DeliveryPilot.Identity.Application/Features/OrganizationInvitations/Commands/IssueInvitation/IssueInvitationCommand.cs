using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.IssueInvitation;

public sealed record IssueInvitationCommand(Guid OrganizationId, string? RecipientEmail, IReadOnlyList<Guid>? RoleIds, string CorrelationId) : ICommand<InvitationSummary>;
