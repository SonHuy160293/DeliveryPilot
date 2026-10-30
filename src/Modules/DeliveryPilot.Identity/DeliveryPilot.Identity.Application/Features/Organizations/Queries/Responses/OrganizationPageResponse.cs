namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;

public sealed record OrganizationPageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
