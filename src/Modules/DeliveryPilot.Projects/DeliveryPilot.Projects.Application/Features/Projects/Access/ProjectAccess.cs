using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Access;

public sealed record ProjectAccess(ProjectParticipant Participant, IReadOnlyList<string> Roles, IReadOnlySet<string> Permissions);
