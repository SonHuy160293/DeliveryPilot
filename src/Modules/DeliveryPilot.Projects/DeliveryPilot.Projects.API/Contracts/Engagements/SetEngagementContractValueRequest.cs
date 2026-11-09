using System.Text.Json.Serialization;
using DeliveryPilot.Projects.API.Contracts.Engagements.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetEngagementContractValueRequest(
    long? ExpectedVersion,
    [property: JsonConverter(typeof(ContractAmountJsonConverter))] decimal? ContractAmount,
    string? ContractCurrency,
    [property: JsonConverter(typeof(PlannedEffortJsonConverter))] decimal? PlannedEffortPersonDays,
    string? Reason)
{
    public override string ToString() => nameof(SetEngagementContractValueRequest);
}
