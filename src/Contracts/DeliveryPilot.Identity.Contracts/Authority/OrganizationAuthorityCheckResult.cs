using System.Text.Json.Serialization;

namespace DeliveryPilot.Identity.Contracts.Authority;

/// <summary>
/// Workspace authority facts for an eligible caller. A denied result carries only <see cref="Decision"/>
/// and <see cref="EvaluatedAt"/>. Workspace authority never grants project or resource access by itself;
/// resource-owning modules decide their own authorization.
/// </summary>
public sealed record OrganizationAuthorityCheckResult(
    string Decision,
    DateTimeOffset EvaluatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OrganizationId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? OrganizationVersion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? MembershipId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MembershipVersion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsOwner = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsAdmin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? EffectivePermissions = null)
{
    public static OrganizationAuthorityCheckResult Denied(DateTimeOffset evaluatedAt)
        => new(AuthorityDecision.Denied, evaluatedAt);
}
