using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListMyOwnershipNominations;

public sealed record ListMyOwnershipNominationsQuery(string Status, int Limit, string? Cursor) : IQuery<OrganizationReadPage<NominationSummary>>;
