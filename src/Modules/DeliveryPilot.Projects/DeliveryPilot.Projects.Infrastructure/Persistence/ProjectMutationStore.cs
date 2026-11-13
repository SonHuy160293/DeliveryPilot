using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Projects.Infrastructure.Persistence;

public sealed class ProjectMutationStore(ProjectsDbContext dbContext) : IProjectMutationStore
{
    public Task<Project?> LockProjectAsync(Guid organizationId, Guid projectId, bool exclusive, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        return exclusive
            ? dbContext.Projects.FromSqlInterpolated($"SELECT * FROM project.\"Projects\" WHERE \"OrganizationId\" = {organizationId} AND \"Id\" = {projectId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : dbContext.Projects.FromSqlInterpolated($"SELECT * FROM project.\"Projects\" WHERE \"OrganizationId\" = {organizationId} AND \"Id\" = {projectId} FOR SHARE").SingleOrDefaultAsync(cancellationToken);
    }

    public Task<ProjectBrief?> LockBriefAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        return dbContext.ProjectBriefs.FromSqlInterpolated($"SELECT * FROM project.\"ProjectBriefs\" WHERE \"OrganizationId\" = {organizationId} AND \"ProjectId\" = {projectId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    }

    public void AddEvent(ProjectEvent entry) => dbContext.ProjectEvents.Add(entry);

    public Task<Engagement?> LockEngagementAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        return dbContext.Engagements.FromSqlInterpolated($"SELECT * FROM engagement.\"Engagements\" WHERE \"OrganizationId\" = {organizationId} AND \"Id\" = {engagementId} FOR SHARE").SingleOrDefaultAsync(cancellationToken);
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Project mutation requires the pipeline transaction.");
        }
    }
}
