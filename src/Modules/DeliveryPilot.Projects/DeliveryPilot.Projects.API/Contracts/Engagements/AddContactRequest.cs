using System.Text.Json.Serialization;

namespace DeliveryPilot.Projects.API.Contracts.Engagements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddContactRequest(string? Name, string? Email, string? ContactRole)
{
    public override string ToString() => nameof(AddContactRequest);
}
