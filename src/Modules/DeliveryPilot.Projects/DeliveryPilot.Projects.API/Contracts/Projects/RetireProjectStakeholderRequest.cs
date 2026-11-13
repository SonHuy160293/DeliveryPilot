using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Projects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RetireProjectStakeholderRequest(long? ExpectedVersion);
