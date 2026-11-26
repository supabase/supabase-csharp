using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Storage;

namespace Storage.Tests.Observability;

/// <summary>
/// Covers the transfer metrics on their own: each records as soon as something listens to it, whether or not the
/// other one is listened to, and a failed transfer carries its error type.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public class TransferMetricsTests
{
    private const string TransferDuration = "supabase.storage.transfer.duration";
    private const string TransferSize = "supabase.storage.transfer.size";
    private const long TransferredBytes = 42;
    private const int ServerError = 500;
    private static readonly Uri ObjectUri = new("http://localhost/storage/v1/object/bucket/file.txt");

    private readonly List<(string Name, Dictionary<string, object?> Tags)> measurements = new();
    private MeterListener listener = null!;

    [TestCleanup]
    public void TearDown() => this.listener.Dispose();

    [TestMethod]
    public void RecordTransfer_ShouldRecordTheDuration_GivenOnlyTheDurationIsListenedTo()
    {
        this.ListenTo(TransferDuration);
        RecordDownload(null);
        this.measurements.Should().ContainSingle(m => m.Name == TransferDuration,
            "the duration is recorded whenever it has a listener, even when the size has none");
    }

    [TestMethod]
    public void RecordTransfer_ShouldRecordTheSize_GivenOnlyTheSizeIsListenedTo()
    {
        this.ListenTo(TransferSize);
        RecordDownload(null);
        this.measurements.Should().ContainSingle(m => m.Name == TransferSize,
            "the size is recorded whenever it has a listener, even when the duration has none");
    }

    [TestMethod]
    public void RecordTransfer_ShouldCarryTheErrorType_GivenAFailedTransfer()
    {
        this.ListenTo(TransferDuration);
        RecordDownload(ServerError.ToString());
        this.measurements.Should().ContainSingle().Which.Tags.Should().Contain("error.type", ServerError.ToString(),
            "a failed transfer says why it failed");
    }

    private static void RecordDownload(string? errorType) =>
        StorageInstrumentation.RecordTransfer(StorageInstrumentation.DirectionDownload, HttpMethod.Get, ObjectUri,
            TransferredBytes, ServerError, errorType, Stopwatch.GetTimestamp());

    private void ListenTo(string instrumentName)
    {
        this.listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == StorageDiagnostics.SourceName && instrument.Name == instrumentName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        this.listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => this.Capture(instrument.Name, tags));
        this.listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => this.Capture(instrument.Name, tags));
        this.listener.Start();
    }

    private void Capture(string name, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var values = new Dictionary<string, object?>();
        foreach (var tag in tags)
            values[tag.Key] = tag.Value;
        this.measurements.Add((name, values));
    }
}
