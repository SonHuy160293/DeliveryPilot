using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.SetEngagementContractValue;

public sealed record SetEngagementContractValueCommand(Guid OrganizationId, Guid EngagementId, long? ExpectedVersion, decimal? ContractAmount, string? ContractCurrency, decimal? PlannedEffortPersonDays, string? Reason) : ICommand<EngagementContractValueResult>
{
    public override string ToString() => nameof(SetEngagementContractValueCommand);
}
