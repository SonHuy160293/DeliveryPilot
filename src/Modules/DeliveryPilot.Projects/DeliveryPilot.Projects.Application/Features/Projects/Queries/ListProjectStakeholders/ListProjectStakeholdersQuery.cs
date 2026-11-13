using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectStakeholders;

public sealed record ListProjectStakeholdersQuery(Guid OrganizationId, Guid ProjectId, bool IncludeRetired) : IQuery<IReadOnlyList<ProjectStakeholderView>>;
