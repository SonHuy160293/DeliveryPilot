using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Mutations;

public sealed record ProjectMutationContext(Project Project, Guid UserId, DateTimeOffset OccurredAt);
