using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginUserRequest(string? Email, string? Password)
{
    public override string ToString() => nameof(LoginUserRequest);
}
