using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.GetMyOrganizations;

public sealed record GetMyOrganizationsQuery(int Limit, string? Cursor) : IQuery<OrganizationPageResponse<OrganizationWorkspaceResponse>>;
