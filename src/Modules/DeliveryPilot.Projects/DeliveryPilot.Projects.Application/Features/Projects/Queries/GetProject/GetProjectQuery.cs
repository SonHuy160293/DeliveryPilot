using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.GetProject;

public sealed record GetProjectQuery(Guid OrganizationId, Guid ProjectId) : IQuery<ProjectDetailView>;
