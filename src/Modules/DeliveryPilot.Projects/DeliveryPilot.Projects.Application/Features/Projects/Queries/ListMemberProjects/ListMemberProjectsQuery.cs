using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListMemberProjects;

public sealed record ListMemberProjectsQuery(Guid OrganizationId, string? Status, int Limit, string? Cursor) : IQuery<ProjectPage>;
