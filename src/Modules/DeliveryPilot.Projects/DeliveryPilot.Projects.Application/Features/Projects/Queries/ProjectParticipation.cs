using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectParticipation(ProjectParticipant Participant, IReadOnlyList<string> Roles);
