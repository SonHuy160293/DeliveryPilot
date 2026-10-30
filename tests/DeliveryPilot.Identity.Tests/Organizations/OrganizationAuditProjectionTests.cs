using System.Text;
using System.Text.Json;
using DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;
using DeliveryPilot.Identity.Domain.Auditing;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Organizations;

public sealed class OrganizationAuditProjectionTests
{
    [Fact]
    public void EveryRegisteredAuditSchemaHasAnExplicitTypedProjection()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var schemas = new (string Aggregate, string Action, object Data)[]
        {
            ("Organization", IdentityAuditSchemaCatalog.OrganizationCreated, new { name = "Workspace", status = "active", createdByUserId = id }),
            ("OrganizationMembership", IdentityAuditSchemaCatalog.MembershipCreated, new { userId = id, previousStatus = (string?)null, newStatus = "active" }),
            ("OrganizationMembership", IdentityAuditSchemaCatalog.MembershipStatusChanged, new { userId = id, previousStatus = "active", newStatus = "removed" }),
            ("OrganizationRoleAssignment", IdentityAuditSchemaCatalog.RoleAssignmentGranted, new { roleId = id, grantVersion = 1, grantedByUserId = id, grantedAt = now, expiresAt = (DateTimeOffset?)null, permissionSetVersion = 1 }),
            ("OrganizationRoleAssignment", IdentityAuditSchemaCatalog.RoleAssignmentRevoked, new { roleId = id, grantVersion = 1, grantedAt = now, expiresAt = (DateTimeOffset?)null, revokedAt = now }),
            ("OrganizationRoleAssignment", IdentityAuditSchemaCatalog.RoleAssignmentRegranted, new { roleId = id, previousGrantVersion = 1, grantVersion = 2, grantedByUserId = id, grantedAt = now, expiresAt = (DateTimeOffset?)null, permissionSetVersion = 1 }),
            ("OrganizationRole", IdentityAuditSchemaCatalog.RoleCreated, new { origin = "custom", templateVersion = (int?)null, previousPermissionSetVersion = (long?)null, permissionSetVersion = 1, addedPermissionCodes = new[] { "organization.members.read" }, removedPermissionCodes = Array.Empty<string>(), permissionCodes = new[] { "organization.members.read" } }),
            ("OrganizationRole", IdentityAuditSchemaCatalog.RolePermissionsChanged, new { origin = "custom", templateVersion = (int?)null, previousPermissionSetVersion = 1, permissionSetVersion = 2, addedPermissionCodes = new[] { "organization.audit.read" }, removedPermissionCodes = Array.Empty<string>(), permissionCodes = new[] { "organization.members.read", "organization.audit.read" } }),
            ("OrganizationRole", IdentityAuditSchemaCatalog.RoleUpdated, new { previousName = "Before", newName = "After", descriptionChanged = true }),
            ("OrganizationRole", IdentityAuditSchemaCatalog.RoleRetired, new { code = "custom-reader", name = "Reader", retiredAt = now, permissionSetVersion = 1 }),
            ("OrganizationInvitation", IdentityAuditSchemaCatalog.InvitationIssued, new { issuedByUserId = id, offeredRoleIds = new[] { id }, expiresAt = now.AddDays(7), replacesInvitationId = (Guid?)null }),
            ("OrganizationInvitation", IdentityAuditSchemaCatalog.InvitationResent, new { issuedByUserId = id, offeredRoleIds = new[] { id }, expiresAt = now.AddDays(7), replacesInvitationId = (Guid?)null }),
            ("OrganizationInvitation", IdentityAuditSchemaCatalog.InvitationReissued, new { issuedByUserId = id, offeredRoleIds = new[] { id }, expiresAt = now.AddDays(7), replacesInvitationId = (Guid?)id }),
            ("OrganizationInvitation", IdentityAuditSchemaCatalog.InvitationClosed, new { previousStatus = "pending", newStatus = "accepted", closedByUserId = (Guid?)id, closedAt = now }),
            ("OrganizationOwnershipNomination", IdentityAuditSchemaCatalog.OwnershipNominationCreated, new { nominatedByUserId = id, previousStatus = (string?)null, newStatus = "pending", expiresAt = now.AddDays(7), closedByUserId = (Guid?)null, closedAt = (DateTimeOffset?)null }),
            ("OrganizationOwnershipNomination", IdentityAuditSchemaCatalog.OwnershipNominationClosed, new { nominatedByUserId = id, previousStatus = "pending", newStatus = "accepted", expiresAt = now.AddDays(7), closedByUserId = (Guid?)id, closedAt = (DateTimeOffset?)now }),
        };
        Assert.Equal(OrganizationAuditProjection.Actions.Order(), schemas.Select(schema => schema.Action).Order());
        foreach (var schema in schemas)
        {
            JsonElement changes = OrganizationAuditProjection.Changes(schema.Aggregate, schema.Action, 1, JsonSerializer.Serialize(schema.Data));
            Assert.Equal(JsonValueKind.Object, changes.ValueKind);
            Assert.NotEmpty(changes.EnumerateObject());
            Assert.DoesNotContain("details", changes.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CorruptUnsupportedAndOversizedDetailsFailWithoutEchoingHistoricalMaterial()
    {
        const string marker = "private@example.test";
        foreach (string json in new[] { "{", "[]", "{}", $"{{\"password\":\"{marker}\"}}", new string('a', 32769), "{\"previousName\":5,\"newName\":\"After\",\"descriptionChanged\":true}" })
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => OrganizationAuditProjection.Changes("OrganizationRole", IdentityAuditSchemaCatalog.RoleUpdated, 1, json));
            Assert.Null(exception.InnerException);
            Assert.DoesNotContain(marker, exception.ToString(), StringComparison.Ordinal);
        }

        Assert.Throws<InvalidOperationException>(() => OrganizationAuditProjection.Changes("OrganizationRole", IdentityAuditSchemaCatalog.RoleUpdated, 2, "{}"));
        Assert.Throws<InvalidOperationException>(() => OrganizationAuditProjection.Changes("OrganizationRole", "unregistered", 1, "{}"));
        Assert.Null(OrganizationAuditProjection.SafeText(marker, 1000));
        Assert.Null(OrganizationAuditProjection.SafeText("Bearer private-token", 1000));
        Assert.Null(OrganizationAuditProjection.SafeText(new string('a', 1001), 1000));
        Assert.Equal("Contract completed", OrganizationAuditProjection.SafeText(" Contract completed ", 1000));
        JsonElement safe = OrganizationAuditProjection.Changes(
            "OrganizationRole",
            IdentityAuditSchemaCatalog.RoleUpdated,
            1,
            JsonSerializer.Serialize(new { previousName = marker, newName = "After", descriptionChanged = true }));
        Assert.Equal(JsonValueKind.Null, safe.GetProperty("previousName").ValueKind);
    }

    [Fact]
    public void CursorBindsBothModesAndEveryNormalizedFilterWithoutGrantingAuthority()
    {
        Guid organizationId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var query = new ListOrganizationAuditEventsQuery(
            organizationId,
            Guid.CreateVersion7().ToString(),
            null,
            Guid.CreateVersion7().ToString(),
            IdentityAuditSchemaCatalog.MembershipStatusChanged,
            now.AddDays(-1).ToString("O"),
            now.AddDays(1).ToString("O"));
        AuditEventFilter filter = AuditEventFilter.Parse(query);
        var key = new AuditPageKey(Guid.CreateVersion7(), now);
        string cursor = AuditEventCursor.Encode(filter, key);
        Assert.True(cursor.Length <= 1024);
        Assert.Equal(key, AuditEventCursor.Decode(cursor, filter));
        foreach (AuditEventFilter changed in new[]
        {
            filter with { OrganizationId = Guid.CreateVersion7() }, filter with { ActorUserId = null }, filter with { TargetId = null },
            filter with { MembershipId = Guid.CreateVersion7() }, filter with { Action = null }, filter with { From = null }, filter with { To = null },
        })
        {
            Assert.Throws<CqrsValidationException>(() => AuditEventCursor.Decode(cursor, changed));
        }

        AuditEventFilter member = filter with { MembershipId = Guid.CreateVersion7() };
        var revision = new AuditPageKey(Guid.CreateVersion7(), MembershipVersion: 17, EventOrdinal: 2);
        string memberCursor = AuditEventCursor.Encode(member, revision);
        Assert.Equal(revision, AuditEventCursor.Decode(memberCursor, member));
        foreach (string invalid in new[] { string.Empty, "bad", cursor + "=", new string('a', 1025), Base64("[]"), Base64("[2,\"time\",null,null,null]"), Base64("{\"v\":1}"), cursor[..^1] })
        {
            Assert.Throws<CqrsValidationException>(() => AuditEventCursor.Decode(invalid, filter));
        }

        string wrongKey = AuditEventCursor.Encode(member, revision with { MembershipVersion = 0 });
        Assert.Throws<CqrsValidationException>(() => AuditEventCursor.Decode(wrongKey, member));
        string wrongTime = AuditEventCursor.Encode(filter, key with { OccurredAt = now.AddDays(2) });
        Assert.Throws<CqrsValidationException>(() => AuditEventCursor.Decode(wrongTime, filter));
    }

    [Fact]
    public void FiltersRequireExplicitOffsetsAndNormalizeEquivalentWindows()
    {
        Guid organizationId = Guid.CreateVersion7();
        var query = new ListOrganizationAuditEventsQuery(organizationId, null, null, null, null, "2026-11-05T12:00:00+07:00", "2026-11-05T06:00:00Z");
        AuditEventFilter parsed = AuditEventFilter.Parse(query);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 5, 0, 0, TimeSpan.Zero), parsed.From);
        Assert.Equal(TimeSpan.Zero, parsed.From!.Value.Offset);
        foreach (string invalid in new[] { string.Empty, "2026-11-05", "2026-11-05T05:00:00", "11/05/2026", "invalid", "2026-13-05T05:00:00Z" })
        {
            Assert.Throws<CqrsValidationException>(() => AuditEventFilter.Parse(query with { From = invalid }));
        }

        Assert.Throws<CqrsValidationException>(() => AuditEventFilter.Parse(query with { To = query.From }));
        Assert.Throws<CqrsValidationException>(() => AuditEventFilter.Parse(query with { ActorUserId = Guid.Empty.ToString() }));
        Assert.Throws<CqrsValidationException>(() => AuditEventFilter.Parse(query with { Action = "not-registered" }));
    }

    private static string Base64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
