using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListOwnershipNominations;

public sealed record ListOwnershipNominationsQuery(Guid OrganizationId, string Status, int Limit, string? Cursor) : IQuery<OrganizationReadPage<NominationSummary>>;
