using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectStakeholderView(Guid Id, string Name, string Side, string? OrganizationName, string? Responsibilities, string? DecisionAreas, string? Email, Guid? ParticipantId, DateTimeOffset? RetiredAt, long Version)
{
    public static ProjectStakeholderView From(ProjectStakeholder value) => new(value.Id, value.Name, value.Side, value.OrganizationName, value.Responsibilities, value.DecisionAreas, value.Email, value.ParticipantId, value.RetiredAt, value.Version);
    public override string ToString() => nameof(ProjectStakeholderView);
}
