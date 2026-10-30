using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetInvitation;

public sealed record GetInvitationQuery(Guid OrganizationId, Guid InvitationId) : IQuery<InvitationSummary>;
