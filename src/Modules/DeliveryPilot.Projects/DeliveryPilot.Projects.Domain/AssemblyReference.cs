using System.Reflection;

namespace DeliveryPilot.Projects.Domain;

/// <summary>
/// Marker for the Projects domain. Aggregates are added per internal context (Engagement, Project,
/// Backlog, Sprint, Approval; ADR-001 D-01) by the feature tasks that need them.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
