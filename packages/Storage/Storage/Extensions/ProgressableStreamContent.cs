using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Supabase.Storage.Interfaces;

namespace Supabase.Storage.Extensions;

internal class ProgressableStreamContent : HttpContent, IProgressableStreamContent
{
    private const int DefaultBufferSize = 4096;

    private Stream content;
    private int bufferSize;

    public EventHandler<UploadState>? StateChanged;
    public IProgress<float>? Progress { get; private set; }

    public enum UploadState
    {
        PendingUpload,
        InProgress,
        PendingResponse,
        Complete
    }

    public ProgressableStreamContent(Stream content) : this(content, DefaultBufferSize) { }

    public ProgressableStreamContent(Stream content, int bufferSize, IProgress<float>? progress = null)
    {
        if (content == null)
        {
            throw new ArgumentNullException("content");
        }

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException("bufferSize");
        }

        if (progress == null)
        {
            progress = new Progress<float>();
        }

        this.content = content;
        this.bufferSize = bufferSize;

        this.Progress = progress;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        Contract.Assert(stream != null);

        var buffer = new byte[this.bufferSize];
        decimal size = this.content.Length;
        decimal uploaded = 0;

        this.StateChanged?.Invoke(this, UploadState.PendingUpload);

        using (this.content)
        {
            int length;
            while ((length = await this.content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                uploaded += length;
                this.Progress?.Report(PercentOf(uploaded, size));

                await stream!.WriteAsync(buffer, 0, length).ConfigureAwait(false);

                this.StateChanged?.Invoke(this, UploadState.InProgress);
            }
        }

        this.StateChanged?.Invoke(this, UploadState.PendingResponse);
    }

    private static float PercentOf(decimal uploaded, decimal size) => (float) ((uploaded / size) * 100);

    protected override bool TryComputeLength(out long length)
    {
        length = this.content.Length;
        return length > 0;
    }
}
