using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public sealed record OrganizationRoleRead(OrganizationRole Role, IReadOnlyList<OrganizationRolePermission> Permissions);
