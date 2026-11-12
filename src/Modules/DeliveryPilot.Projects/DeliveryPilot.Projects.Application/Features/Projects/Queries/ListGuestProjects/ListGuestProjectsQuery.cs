using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListGuestProjects;

public sealed record ListGuestProjectsQuery(string? Status, int Limit, string? Cursor) : IQuery<ProjectPage>;
