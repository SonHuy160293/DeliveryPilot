namespace DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;

public sealed record OrganizationCreationSnapshot(
    Guid OrganizationId,
    string Name,
    string Status,
    Guid MembershipId,
    IReadOnlyList<string> Roles,
    long Version,
    long MembershipVersion,
    string ResultKind,
    DateTimeOffset SnapshotAt);
