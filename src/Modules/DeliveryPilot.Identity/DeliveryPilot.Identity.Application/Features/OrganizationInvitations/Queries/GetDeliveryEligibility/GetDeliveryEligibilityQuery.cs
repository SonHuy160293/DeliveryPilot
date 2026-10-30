using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetDeliveryEligibility;

public sealed record GetDeliveryEligibilityQuery(Guid DeliveryMessageId) : IQuery<InvitationDeliveryEligibility>;
