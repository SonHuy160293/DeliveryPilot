using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        // Scaffolding and scripts do not need a live database or API dependencies.
        // Database-changing EF commands must receive an explicit connection.
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ApplicationDb")
            ?? "Host=localhost;Database=DeliveryPilotIdentity;Username=deliverypilot_identity";
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new IdentityDbContext(options);
    }
}
