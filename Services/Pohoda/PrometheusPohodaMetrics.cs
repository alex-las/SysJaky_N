using System;
using Prometheus;

namespace SysJaky_N.Services.Pohoda;

public sealed class PrometheusPohodaMetrics : IPohodaMetrics
{
    private static readonly Counter ExportSuccessCounter = Metrics.CreateCounter(
        "pohoda_export_success_total",
        "Total number of successful Pohoda invoice exports.");

    private static readonly Counter ExportFailureCounter = Metrics.CreateCounter(
        "pohoda_export_failure_total",
        "Total number of failed Pohoda invoice exports.");

    private static readonly Histogram ExportDurationHistogram = Metrics.CreateHistogram(
        "pohoda_export_duration_seconds",
        "Duration of Pohoda invoice exports.",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.5, width: 0.5, count: 20)
        });

    public void ObserveExportSuccess(TimeSpan duration)
    {
        ExportSuccessCounter.Inc();
        ExportDurationHistogram.Observe(duration.TotalSeconds);
    }

    public void ObserveExportFailure(TimeSpan duration)
    {
        ExportFailureCounter.Inc();
        ExportDurationHistogram.Observe(duration.TotalSeconds);
    }
}
