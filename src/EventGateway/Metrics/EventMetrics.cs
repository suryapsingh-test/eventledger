using System.Diagnostics.Metrics;

namespace EventGateway.Metrics;

public sealed class EventMetrics
{
    public const string MeterName = "EventLedger.Gateway";

    private readonly Counter<long> _processed;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _throttled;

    public EventMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _processed = meter.CreateCounter<long>("eventledger.events.processed");
        _failed = meter.CreateCounter<long>("eventledger.events.failed");
        _throttled = meter.CreateCounter<long>("eventledger.inbound.throttled");
    }

    public void RecordProcessed() => _processed.Add(1);

    public void RecordFailed() => _failed.Add(1);

    public void RecordThrottled() => _throttled.Add(1);
}
