namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public sealed record ProjectPage(IReadOnlyList<ProjectListEntry> Items, string? NextCursor);
