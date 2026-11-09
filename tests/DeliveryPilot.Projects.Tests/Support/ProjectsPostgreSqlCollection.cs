namespace DeliveryPilot.Projects.Tests.Support;

// Schema rollback must not overlap another test's migration or query.
[CollectionDefinition("Projects PostgreSQL", DisableParallelization = true)]
public sealed class ProjectsPostgreSqlCollection
{
}
