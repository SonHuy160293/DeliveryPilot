namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.SetEngagementContractValue;

public sealed record EngagementContractValueResult(Guid Id, decimal? ContractAmount, string? ContractCurrency, decimal? PlannedEffortPersonDays, long Version)
{
    public override string ToString() => nameof(EngagementContractValueResult);
}
