using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class ProjectsDbContextFactory : IDesignTimeDbContextFactory<ProjectsDbContext>
{
    public ProjectsDbContext CreateDbContext(string[] args)
    {
        // Scaffolding and scripts do not need a live database or API dependencies.
        // Database-changing EF commands must receive an explicit connection.
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ApplicationDb")
            ?? "Host=localhost;Database=DeliveryPilotProjects;Username=deliverypilot_projects";
        var options = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ProjectsDbContext(options);
    }
}
