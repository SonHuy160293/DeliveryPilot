using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.CancelInvitation;

public sealed record CancelInvitationCommand(Guid OrganizationId, Guid InvitationId, long ExpectedVersion, string? Reason, string CorrelationId) : ICommand<InvitationSummary>;
