using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeSprintLength;

public sealed record ChangeSprintLengthCommand(Guid OrganizationId, Guid ProjectId, long? ExpectedVersion, short? SprintLengthWeeks) : ICommand<ProjectSettingsResult>;
