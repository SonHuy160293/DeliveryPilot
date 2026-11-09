using DeliveryPilot.Projects.Domain.Models.Engagements;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Mutations;

public sealed record ContactResult(Guid Id, string Name, string Email, string ContactRole, Guid CreatedByUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, long Version)
{
    public static ContactResult From(EngagementContact contact)
        => new(contact.Id, contact.Name, contact.Email, contact.ContactRole, contact.CreatedByUserId, contact.CreatedAt, contact.UpdatedAt, contact.Version);

    public override string ToString() => nameof(ContactResult);
}
