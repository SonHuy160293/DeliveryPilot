using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class OrganizationAuditReadStore(IdentityDbContext db) : IOrganizationAuditReadStore
{
    public async Task<OrganizationReadPage<OrganizationAuditEventResponse>> ListAsync(AuditEventFilter filter, int limit, AuditPageKey? after, CancellationToken cancellationToken)
    {
        // Keep store failures separate from payload projection failures, which use a sanitized 500.
        List<AuditRow> rows = await LoadAsync();
        bool more = rows.Count > limit;
        AuditRow[] page = rows.Take(limit).ToArray();
        OrganizationAuditEventResponse[] items = page.Select(row => new OrganizationAuditEventResponse(
            row.Id,
            row.ActorUserId,
            OrganizationAuditProjection.SafeText(row.ActorDisplayName, 160),
            filter.OrganizationId,
            row.AggregateType,
            row.AggregateId,
            row.EventType,
            row.OccurredAt.ToUniversalTime(),
            row.Outcome,
            row.AggregateVersion,
            row.MembershipVersion,
            row.EventOrdinal,
            OrganizationAuditProjection.SafeText(row.Reason, 1000),
            OrganizationAuditProjection.Changes(row.AggregateType, row.EventType, row.DetailsSchemaVersion, row.Details))).ToArray();
        AuditPageKey? key = page.Length == 0 ? null : new AuditPageKey(
            page[^1].Id,
            filter.MembershipId.HasValue ? null : page[^1].OccurredAt.ToUniversalTime(),
            filter.MembershipId.HasValue ? page[^1].MembershipVersion : null,
            filter.MembershipId.HasValue ? page[^1].EventOrdinal : null);
        return new OrganizationReadPage<OrganizationAuditEventResponse>(items, more ? AuditEventCursor.Encode(filter, key!) : null);

        async Task<List<AuditRow>> LoadAsync()
        {
            try
            {
                IQueryable<IdentityAuditEvent> query = db.IdentityAuditEvents.AsNoTracking().Where(item => item.OrganizationId == filter.OrganizationId && item.Outcome == "succeeded");
                if (filter.ActorUserId.HasValue)
                {
                    query = query.Where(item => item.ActorUserId == filter.ActorUserId.Value);
                }

                if (filter.MembershipId.HasValue)
                {
                    query = query.Where(item => item.MembershipId == filter.MembershipId.Value);
                }

                if (filter.TargetId.HasValue)
                {
                    query = query.Where(item => item.AggregateId == filter.TargetId.Value);
                }

                if (filter.Action is not null)
                {
                    query = query.Where(item => item.EventType == filter.Action);
                }

                if (filter.From.HasValue)
                {
                    query = query.Where(item => item.OccurredAt >= filter.From.Value);
                }

                if (filter.To.HasValue)
                {
                    query = query.Where(item => item.OccurredAt < filter.To.Value);
                }

                if (after is not null)
                {
                    query = filter.MembershipId.HasValue
                        ? query.Where(item => item.MembershipVersion > after.MembershipVersion ||
                            (item.MembershipVersion == after.MembershipVersion && item.EventOrdinal > after.EventOrdinal) ||
                            (item.MembershipVersion == after.MembershipVersion && item.EventOrdinal == after.EventOrdinal && item.Id.CompareTo(after.Id) > 0))
                        : query.Where(item => item.OccurredAt > after.OccurredAt || (item.OccurredAt == after.OccurredAt && item.Id.CompareTo(after.Id) > 0));
                }

                IOrderedQueryable<IdentityAuditEvent> ordered = filter.MembershipId.HasValue
                    ? query.OrderBy(item => item.MembershipVersion).ThenBy(item => item.EventOrdinal).ThenBy(item => item.Id)
                    : query.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id);

                // Limit before the optional actor join; inactive historical actors remain attributable.
                var pageQuery = from item in ordered.Take(limit + 1)
                                join user in db.Users.AsNoTracking() on item.ActorUserId equals user.Id into actors
                                from actor in actors.DefaultIfEmpty()
                                select new { Item = item, ActorDisplayName = actor == null ? null : actor.DisplayName };

                // Reassert the outer order after the join, independent of planner join strategy.
                var sorted = filter.MembershipId.HasValue
                    ? pageQuery.OrderBy(row => row.Item.MembershipVersion).ThenBy(row => row.Item.EventOrdinal).ThenBy(row => row.Item.Id)
                    : pageQuery.OrderBy(row => row.Item.OccurredAt).ThenBy(row => row.Item.Id);
                return await sorted.Select(row => new AuditRow(
                    row.Item.Id,
                    row.Item.ActorUserId,
                    row.ActorDisplayName,
                    row.Item.AggregateType,
                    row.Item.AggregateId,
                    row.Item.EventType,
                    row.Item.OccurredAt,
                    row.Item.Outcome,
                    row.Item.AggregateVersion,
                    row.Item.MembershipVersion,
                    row.Item.EventOrdinal,
                    row.Item.Reason,
                    row.Item.DetailsSchemaVersion,
                    row.Item.Details)).ToListAsync(cancellationToken);
            }
            catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
            {
                throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
            }
        }
    }

    private sealed record AuditRow(Guid Id, Guid? ActorUserId, string? ActorDisplayName, string AggregateType, Guid AggregateId,
        string EventType, DateTimeOffset OccurredAt, string Outcome, long AggregateVersion, long? MembershipVersion,
        int EventOrdinal, string? Reason, short DetailsSchemaVersion, string Details)
    {
        public override string ToString() => nameof(AuditRow);
    }
}
