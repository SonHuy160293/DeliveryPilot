using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DeliveryPilot.SharedKernel.Behaviors;

internal static class CqrsTelemetry
{
    private static readonly Meter _meter = new("DeliveryPilot.Cqrs");
    private static readonly Counter<long> _requests = _meter.CreateCounter<long>("deliverypilot.cqrs.requests");
    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>("deliverypilot.cqrs.duration", "s");

    internal static void Record(string requestType, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList { { "cqrs.request_type", requestType }, { "cqrs.outcome", outcome } };
        _requests.Add(1, tags);
        _duration.Record(elapsed.TotalSeconds, tags);
    }
}
