using System.Diagnostics;
using DeliveryPilot.AspNetCore.HttpClient.Abstractions;

namespace DeliveryPilot.AspNetCore.HttpClient.Correlation;

internal sealed class ActivityCorrelationIdAccessor : ICorrelationIdAccessor
{
    public Guid? GetCorrelationId()
    {
        Activity? activity = Activity.Current;
        string? value = activity?.GetBaggageItem("CorrelationId")
            ?? activity?.GetTagItem("CorrelationId")?.ToString();

        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }
}
