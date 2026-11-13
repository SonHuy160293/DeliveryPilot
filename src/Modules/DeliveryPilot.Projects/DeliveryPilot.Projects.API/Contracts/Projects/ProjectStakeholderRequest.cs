using System.Text.Json.Serialization;
using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.API.Contracts.Projects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProjectStakeholderRequest(long? ExpectedVersion, string? Name, string? Side, string? OrganizationName, string? Responsibilities, string? DecisionAreas, string? Email, Guid? ParticipantId)
{
    public ProjectStakeholderValues ToValues() => new(Name, Side, OrganizationName, Responsibilities, DecisionAreas, Email, ParticipantId);
    public override string ToString() => nameof(ProjectStakeholderRequest);
}
