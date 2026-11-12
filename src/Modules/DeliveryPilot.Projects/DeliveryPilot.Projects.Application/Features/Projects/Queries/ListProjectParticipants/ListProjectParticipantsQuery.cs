using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries.ListProjectParticipants;

public sealed record ListProjectParticipantsQuery(Guid OrganizationId, Guid ProjectId) : IQuery<IReadOnlyList<ProjectParticipantView>>;
