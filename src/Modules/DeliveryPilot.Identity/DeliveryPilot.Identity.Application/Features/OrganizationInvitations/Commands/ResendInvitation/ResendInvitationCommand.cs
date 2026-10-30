using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ResendInvitation;

public sealed record ResendInvitationCommand(Guid OrganizationId, Guid InvitationId, long ExpectedVersion, string? Reason, string CorrelationId) : ICommand<InvitationSummary>;
