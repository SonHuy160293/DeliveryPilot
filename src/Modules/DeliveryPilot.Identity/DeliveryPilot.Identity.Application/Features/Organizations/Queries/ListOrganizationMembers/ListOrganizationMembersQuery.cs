using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.ListOrganizationMembers;

public sealed record ListOrganizationMembersQuery(Guid OrganizationId, string Status, int Limit, string? Cursor) : IQuery<OrganizationPageResponse<OrganizationMemberResponse>>;
