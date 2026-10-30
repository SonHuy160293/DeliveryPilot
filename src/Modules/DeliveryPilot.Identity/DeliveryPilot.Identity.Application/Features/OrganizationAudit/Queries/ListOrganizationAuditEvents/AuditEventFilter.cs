using System.Globalization;
using System.Text.RegularExpressions;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public sealed record AuditEventFilter(Guid OrganizationId, Guid? ActorUserId, Guid? MembershipId, Guid? TargetId,
    string? Action, DateTimeOffset? From, DateTimeOffset? To)
{
    public string Mode => MembershipId.HasValue ? "membership" : "time";

    public static AuditEventFilter Parse(ListOrganizationAuditEventsQuery query)
    {
        if (query.OrganizationId == Guid.Empty || query.Limit is < 1 or > 100 || query.Cursor is { Length: > 1024 } || query.Cursor == string.Empty)
        {
            throw Invalid();
        }

        string? action = query.Action?.Trim();
        if (action is not null && !OrganizationAuditProjection.Actions.Contains(action, StringComparer.Ordinal))
        {
            throw Invalid();
        }

        DateTimeOffset? from = Time(query.From);
        DateTimeOffset? to = Time(query.To);
        if (from.HasValue && to.HasValue && from >= to)
        {
            throw Invalid();
        }

        return new AuditEventFilter(query.OrganizationId, Id(query.ActorUserId), Id(query.MembershipId), Id(query.TargetId), action, from, to);
    }

    private static Guid? Id(string? text)
    {
        if (text is null)
        {
            return null;
        }

        return Guid.TryParse(text, out Guid id) && id != Guid.Empty ? id : throw Invalid();
    }

    private static DateTimeOffset? Time(string? text)
    {
        if (text is null)
        {
            return null;
        }

        // Require an ISO date/time and an explicit offset; never infer the host timezone.
        if (text.Length > 40 || !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)) ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset value))
        {
            throw Invalid();
        }

        return value.ToUniversalTime();
    }

    private static CqrsValidationException Invalid() => new(["Invalid audit filters or pagination."]);
}
