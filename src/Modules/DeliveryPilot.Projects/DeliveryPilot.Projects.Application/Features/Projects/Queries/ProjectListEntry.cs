namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectListEntry(Guid Id, Guid OrganizationId, string Name, string Status, IReadOnlyList<string> Roles);
