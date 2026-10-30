using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.API.Contracts.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterUserRequest(string? DisplayName, string? Email, string? Password)
{
    public override string ToString() => nameof(RegisterUserRequest);
}
