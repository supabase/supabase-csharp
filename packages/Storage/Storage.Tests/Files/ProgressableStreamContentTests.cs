using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.Extensions;
using Supabase.Storage.Extensions;
using UploadState = Supabase.Storage.Extensions.ProgressableStreamContent.UploadState;

namespace Storage.Tests.Files;

/// <summary>
/// Covers <see cref="ProgressableStreamContent"/>, the upload body: it copies the source verbatim, reporting
/// cumulative percentage progress and upload-state transitions in step with each buffer written.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class ProgressableStreamContentTests
{
    [TestMethod]
    public async Task CopyToAsync_ShouldReportProgressAndStatesInOrderEndingAt100_GivenAMultiBufferSource()
    {
        var source = new byte[10_000];
        new Random(42).NextBytes(source);
        var events = new List<object>();
        var progress = Substitute.For<IProgress<float>>();
        progress.When(p => p.Report(Arg.Any<float>())).Do(call => events.Add(call.Arg<float>()));
        var sut = new ProgressableStreamContent(new MemoryStream(source), 4096, progress);
        sut.StateChanged += (_, state) => events.Add(state);
        using var destination = new MemoryStream();
        await sut.CopyToAsync(destination);
        using (new AssertionScope())
        {
            destination.ToArray().Should().Equal(source, "the upload body is the source stream, byte for byte");
            events.Should().Equal(new object[]
            {
                UploadState.PendingUpload,
                40.96f, UploadState.InProgress,
                81.92f, UploadState.InProgress,
                100f, UploadState.InProgress,
                UploadState.PendingResponse,
            }, "each buffer reports cumulative progress before it is written, then InProgress, and the upload ends at 100% before PendingResponse");
        }
    }

    [TestMethod]
    public void Constructor_ShouldThrow_GivenNullContent() =>
        FluentActions.Invoking(() => new ProgressableStreamContent(null!, 4096))
            .Should().Throw<ArgumentNullException>().WithParameterName("content");

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_ShouldThrow_GivenANonPositiveBufferSize(int bufferSize) =>
        FluentActions.Invoking(() => new ProgressableStreamContent(new MemoryStream(), bufferSize))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("bufferSize");

    [TestMethod]
    public void Constructor_ShouldDefaultTheProgressReporter_GivenNone() =>
        new ProgressableStreamContent(new MemoryStream(), 4096).Progress.Should().NotBeNull("a caller that passes no reporter still gets one to report to");

    [TestMethod]
    public void Headers_ShouldReportTheSourceLengthAsContentLength_GivenANonEmptySource() =>
        new ProgressableStreamContent(new MemoryStream(new byte[10]), 4096).Headers.ContentLength.Should().Be(10);

    [TestMethod]
    public void Headers_ShouldLeaveContentLengthUnknown_GivenAnEmptySource() =>
        new ProgressableStreamContent(new MemoryStream(), 4096).Headers.ContentLength.Should().BeNull("an empty source has no length to declare");

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task CopyToAsync_ShouldNeverResumeOnTheCapturedContext(bool sourceCompletesLater, bool destinationCompletesLater)
    {
        var sut = new ProgressableStreamContent(StreamOf(sourceCompletesLater, new byte[10_000]), 4096);
        using var destination = StreamOf(destinationCompletesLater, Array.Empty<byte>());
        var context = Substitute.ForPartsOf<SynchronizationContext>();
        await CopyUnder(context, sut, destination).ConfigureAwait(false);
        using (new AssertionScope())
        {
            destination.Length.Should().Be(10_000, "every byte was copied");
            context.ReceivedCalls().Should().BeEmpty(
                "library code must not resume on the caller's context: a caller blocking on the upload from that context (.Result on a UI or Unity main thread) would deadlock");
        }
    }

    private static async Task CopyUnder(SynchronizationContext context, ProgressableStreamContent sut, Stream destination)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            await sut.CopyToAsync(destination).ConfigureAwait(false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static MemoryStream StreamOf(bool completesLater, byte[] data)
    {
        var stream = Substitute.ForPartsOf<MemoryStream>();
        stream.Write(data, 0, data.Length);
        stream.Position = 0;
        if (!completesLater)
            return stream;

        stream.Configure().ReadAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Later(() => stream.Read(call.ArgAt<byte[]>(0), call.ArgAt<int>(1), call.ArgAt<int>(2))));
        stream.Configure().WriteAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Later(() => stream.Write(call.ArgAt<byte[]>(0), call.ArgAt<int>(1), call.ArgAt<int>(2))));
        return stream;
    }

    private static async Task<int> Later(Func<int> operation)
    {
        await Task.Delay(1).ConfigureAwait(false);
        return operation();
    }

    private static async Task Later(Action operation)
    {
        await Task.Delay(1).ConfigureAwait(false);
        operation();
    }
}
