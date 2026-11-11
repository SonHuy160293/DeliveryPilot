using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UserLookupRequest(IReadOnlyList<Guid>? UserIds)
{
    public override string ToString() => nameof(UserLookupRequest);
}
