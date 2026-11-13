using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProjectBrief;

public sealed record GetProjectBriefQuery(Guid OrganizationId, Guid ProjectId) : IQuery<ProjectBriefView>;
