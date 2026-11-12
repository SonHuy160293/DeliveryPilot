namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries;

public sealed record EngagementProjectView(Guid Id, string Name, string Status, IReadOnlyList<EngagementAdministratorView> Administrators);
