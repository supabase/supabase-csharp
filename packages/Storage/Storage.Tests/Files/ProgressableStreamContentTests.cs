using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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

    // Each await is exercised on its own: once an asynchronous read resumes on a pool thread no context is
    // current any more, so the write is only reachable with a context while the source reads synchronously.
    [TestMethod]
    [DataRow(true, false, DisplayName = "asynchronous source")]
    [DataRow(false, true, DisplayName = "asynchronous destination")]
    [DataRow(true, true, DisplayName = "asynchronous source and destination")]
    public async Task CopyToAsync_ShouldNeverResumeOnTheCapturedContext(bool asynchronousSource, bool asynchronousDestination)
    {
        var source = new AsynchronousStream(asynchronousSource, new byte[10_000]);
        var sut = new ProgressableStreamContent(source, 4096);
        using var destination = new AsynchronousStream(asynchronousDestination);
        var context = new CountingSynchronizationContext();
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

        using (new AssertionScope())
        {
            destination.Written.Should().Be(10_000, "every byte was copied");
            context.Posts.Should().Be(0, "a single-threaded host (Unity WebGL) cannot hop back onto a context, so the copy must not capture one");
        }
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int posts;

        public int Posts => Volatile.Read(ref this.posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref this.posts);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }

    /// <summary>A stream whose reads and writes complete on a later turn, as a real file or socket does, or at once.</summary>
    private sealed class AsynchronousStream : Stream
    {
        private readonly bool completesLater;
        private readonly MemoryStream inner;

        public AsynchronousStream(bool completesLater, byte[]? data = null)
        {
            this.completesLater = completesLater;
            this.inner = data == null ? new MemoryStream() : new MemoryStream(data);
        }

        public long Written { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => this.inner.Length;

        public override long Position
        {
            get => this.inner.Position;
            set => this.inner.Position = value;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.completesLater)
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            return this.inner.Read(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.completesLater)
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            this.Written += count;
        }

        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) => this.Written += count;

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => this.inner.Seek(offset, origin);

        public override void SetLength(long value) => this.inner.SetLength(value);
    }
}
