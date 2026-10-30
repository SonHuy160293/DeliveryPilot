using DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;
using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.ListInvitations;

public sealed record ListInvitationsQuery(Guid OrganizationId, string Status, int Limit, string? Cursor) : IQuery<OrganizationPageResponse<InvitationSummary>>;
