using System;
using System.Collections.Generic;
using System.IO;
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
}
