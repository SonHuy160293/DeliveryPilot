namespace DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;

public sealed record CreateOrganizationResult(OrganizationCreationSnapshot Snapshot, bool IsReplay);
