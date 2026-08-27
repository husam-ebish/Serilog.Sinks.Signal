using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Serilog.Sinks.Signal.HttpClients;

namespace Serilog.Sinks.Signal.Tests;

public class HttpClientTests
{
    [Fact]
    public async Task JsonClientUsesSignalApiContract()
    {
        var handler = new RecordingHandler();
        using var client = new JsonHttpClient(new HttpClient(handler));
        using var response = await client.PostAsync(
            "https://signal.example/v2/send",
            new SignalLogMessage("hello", "+41000000000", ["+41111111111"]));

        Assert.Equal("application/json", handler.ContentType);
        Assert.Empty(handler.ContentEncodings);

        using var document = JsonDocument.Parse(handler.Body);
        var root = document.RootElement;
        Assert.Equal("hello", root.GetProperty("message").GetString());
        Assert.Equal("+41000000000", root.GetProperty("number").GetString());
        Assert.Equal("+41111111111", root.GetProperty("recipients")[0].GetString());
        Assert.False(root.TryGetProperty("Message", out _));
    }

    [Fact]
    public async Task GzipClientCompressesSignalApiContract()
    {
        var handler = new RecordingHandler();
        using var client = new JsonGzipHttpClient(new HttpClient(handler), CompressionLevel.Optimal);
        using var response = await client.PostAsync(
            "https://signal.example/v2/send",
            new SignalLogMessage("compressed", "+41000000000", ["+41111111111"]));

        Assert.Equal("application/json", handler.ContentType);
        Assert.Equal(["gzip"], handler.ContentEncodings);

        using var compressed = new MemoryStream(handler.Body);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var document = await JsonDocument.ParseAsync(
            gzip,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("compressed", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ClientRejectsUseAfterDisposal()
    {
        var client = new JsonHttpClient(new HttpClient(new RecordingHandler()));
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.PostAsync(
            "https://signal.example/v2/send",
            new SignalLogMessage("hello", "+41000000000", ["+41111111111"])));
    }

    [Fact]
    public async Task JsonClientSendsStreamWithoutChangingItsBytes()
    {
        var handler = new RecordingHandler();
        using var client = new JsonHttpClient(new HttpClient(handler));
        await using var input = new MemoryStream("{\"value\":42}"u8.ToArray());

        using var response = await client.PostAsync("https://signal.example/custom", input);

        Assert.Equal("https://signal.example/custom", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("{\"value\":42}"u8.ToArray(), handler.Body);
        Assert.Equal("application/json", handler.ContentType);
    }

    [Fact]
    public async Task GzipClientCompressesStream()
    {
        var handler = new RecordingHandler();
        using var client = new JsonGzipHttpClient(new HttpClient(handler), CompressionLevel.SmallestSize);
        await using var input = new MemoryStream("stream payload"u8.ToArray());

        using var response = await client.PostAsync("https://signal.example/custom", input);

        using var compressed = new MemoryStream(handler.Body);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output, TestContext.Current.CancellationToken);
        Assert.Equal("stream payload"u8.ToArray(), output.ToArray());
        Assert.Equal(["gzip"], handler.ContentEncodings);
    }

    [Fact]
    public async Task ClientsValidateArgumentsAndDisposal()
    {
        using var jsonClient = new JsonHttpClient(new HttpClient(new RecordingHandler()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => jsonClient.PostAsync(null!, Stream.Null));
        await Assert.ThrowsAsync<ArgumentNullException>(() => jsonClient.PostAsync("https://signal.example", (Stream)null!));
        Assert.Throws<ArgumentNullException>(() => jsonClient.Configure(null!));

        var gzipClient = new JsonGzipHttpClient(new HttpClient(new RecordingHandler()), CompressionLevel.Fastest);
        gzipClient.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            gzipClient.PostAsync("https://signal.example", Stream.Null));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            gzipClient.PostAsync("https://signal.example", (SignalLogMessage)null!));
        Assert.Throws<ArgumentNullException>(() => gzipClient.Configure(null!));
    }

    [Fact]
    public void ClientsRejectNullHttpClient()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonHttpClient(null!));
        Assert.Throws<ArgumentNullException>(() => new JsonGzipHttpClient(null!, CompressionLevel.Fastest));
    }

    [Fact]
    public void ClientsDisposeTheirUnderlyingHandlersIdempotently()
    {
        var jsonHandler = new RecordingHandler();
        var jsonClient = new JsonHttpClient(new HttpClient(jsonHandler));
        jsonClient.Dispose();
        jsonClient.Dispose();
        Assert.True(jsonHandler.Disposed);

        var gzipHandler = new RecordingHandler();
        var gzipClient = new JsonGzipHttpClient(new HttpClient(gzipHandler), CompressionLevel.Fastest);
        gzipClient.Dispose();
        gzipClient.Dispose();
        Assert.True(gzipHandler.Disposed);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public byte[] Body { get; private set; } = [];

        public string? ContentType { get; private set; }

        public string[] ContentEncodings { get; private set; } = [];

        public Uri? RequestUri { get; private set; }

        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.NotNull(request.Content);
            RequestUri = request.RequestUri;
            Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            ContentType = request.Content.Headers.ContentType?.MediaType;
            ContentEncodings = [.. request.Content.Headers.ContentEncoding];
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
