namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public sealed record OrganizationReadPage<T>(IReadOnlyList<T> Items, string? NextCursor);
