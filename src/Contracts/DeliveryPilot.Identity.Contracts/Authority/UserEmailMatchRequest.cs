using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.Contracts.Authority;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UserEmailMatchRequest(Guid? UserId, string? Email)
{
    public override string ToString() => nameof(UserEmailMatchRequest);
}
