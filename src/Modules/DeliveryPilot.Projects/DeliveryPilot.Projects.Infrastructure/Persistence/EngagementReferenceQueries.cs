using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

internal static class EngagementReferenceQueries
{
    public static Task LockAsync(ProjectsDbContext db, Guid organizationId, string reference, CancellationToken cancellationToken)
    {
        EnsureTransaction(db);

        // Database lower() matches both the expression index and the duplicate comparison.
        string scope = $"engagement.reference:v1:{organizationId:D}:";
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope} || lower({reference}), 0))", cancellationToken);
    }

    public static Task<bool> HasOtherActiveAsync(ProjectsDbContext db, Guid organizationId, string reference, Guid excludeId, CancellationToken cancellationToken)
    {
        EnsureTransaction(db);
        return db.Engagements.FromSqlInterpolated($"""
            SELECT * FROM engagement."Engagements"
            WHERE "OrganizationId" = {organizationId} AND "Status" = 'Active' AND "Id" <> {excludeId}
              AND lower("AgreementReference") = lower({reference})
            """).AsNoTracking().AnyAsync(cancellationToken);
    }

    private static void EnsureTransaction(ProjectsDbContext db)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Agreement reference serialization requires the pipeline transaction.");
        }
    }
}
