namespace DeliveryPilot.Projects.Tests.Support;

/// <summary>
/// Runs only against a dedicated, empty Projects test database. Do not reuse the Identity test database:
/// both modules own a <c>reliability</c> schema.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public const string VariableName = "DELIVERYPILOT_TEST_PROJECTS_POSTGRES";

    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Skip = $"Set {VariableName} to an isolated, empty PostgreSQL 18 database.";
        }
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(VariableName);
}
