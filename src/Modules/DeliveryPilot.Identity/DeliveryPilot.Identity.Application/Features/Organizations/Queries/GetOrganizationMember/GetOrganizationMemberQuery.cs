using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetOrganizationMember;

public sealed record GetOrganizationMemberQuery(Guid OrganizationId, Guid MembershipId) : IQuery<OrganizationMemberResponse>;
