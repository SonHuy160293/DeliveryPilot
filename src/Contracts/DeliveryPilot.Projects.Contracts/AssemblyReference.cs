using System.Reflection;

namespace DeliveryPilot.Projects.Contracts;

/// <summary>
/// Marker for the Projects cross-module contracts. Versioned messages and internal read DTOs are added
/// under <c>&lt;Area&gt;/V1/</c> once a consumer exists (ADR-001 D-10).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
