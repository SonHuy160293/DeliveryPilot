using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.RetireProjectStakeholder;

public sealed record RetireProjectStakeholderCommand(Guid OrganizationId, Guid ProjectId, Guid StakeholderId, long? ExpectedVersion) : ICommand<ProjectStakeholderView>;
