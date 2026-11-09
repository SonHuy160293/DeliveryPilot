namespace DeliveryPilot.Projects.Tests.Support;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlTheoryAttribute : TheoryAttribute
{
    public PostgreSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(PostgreSqlFactAttribute.ConnectionString))
        {
            Skip = $"Set {PostgreSqlFactAttribute.VariableName} to an isolated, empty PostgreSQL 18 database.";
        }
    }
}
