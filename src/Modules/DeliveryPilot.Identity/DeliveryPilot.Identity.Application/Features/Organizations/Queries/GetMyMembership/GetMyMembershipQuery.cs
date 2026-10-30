using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyMembership;

public sealed record GetMyMembershipQuery(Guid OrganizationId) : IQuery<OrganizationMemberResponse>;
