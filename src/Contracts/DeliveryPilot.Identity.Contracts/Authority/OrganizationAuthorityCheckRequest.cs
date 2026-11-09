namespace DeliveryPilot.Identity.Contracts.Authority;

/// <summary>
/// Internal request asking Identity for the caller's current workspace authority.
/// Supply <see cref="SessionId"/> for interactive requests; omit it only when reauthorizing queued work.
/// </summary>
public sealed record OrganizationAuthorityCheckRequest(
    Guid UserId,
    Guid OrganizationId,
    Guid? SessionId = null,
    string? RequiredPermissionCode = null);
