using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.Signal.HttpClients;

namespace Serilog.Sinks.Signal.Tests;

[Trait("Category", "Integration")]
public class SignalSinkIntegrationTests
{
    [Fact]
    public async Task LoggerPostsBatchedSignalPayloadOverHttp()
    {
        await using var api = await LoopbackSignalApi.StartAsync();
        var options = CreateOptions(api.Endpoint);
        var logger = new LoggerConfiguration()
            .WriteTo.Signal(
                options,
                queueLimitBytes: ByteSize.MB,
                logEventsInBatchLimit: 10,
                period: TimeSpan.FromMinutes(1))
            .CreateLogger();

        logger.Information("Processed order {OrderId}", 42);
        logger.Warning("Inventory for {Sku} is low", "ABC-123");
        logger.Dispose();

        var request = await api.ReadRequestAsync(TestContext.Current.CancellationToken);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/v2/send", request.Path);
        Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Null(request.ContentEncoding);

        using var document = JsonDocument.Parse(request.Body);
        var payload = document.RootElement;
        Assert.Equal("+41000000000", payload.GetProperty("number").GetString());
        Assert.Equal(["+41111111111", "+41222222222"],
            payload.GetProperty("recipients").EnumerateArray().Select(value => value.GetString()));

        var message = payload.GetProperty("message").GetString();
        Assert.Contains("Processed order 42", message);
        Assert.Contains("Inventory for", message);
        Assert.Contains("ABC-123", message);
        Assert.Equal(1, api.RequestCount);
    }

    [Fact]
    public async Task LoggerCanSendGzipPayloadOverHttp()
    {
        await using var api = await LoopbackSignalApi.StartAsync();
        var logger = new LoggerConfiguration()
            .WriteTo.Signal(
                CreateOptions(api.Endpoint),
                queueLimitBytes: ByteSize.MB,
                period: TimeSpan.FromMinutes(1),
                httpClient: new JsonGzipHttpClient())
            .CreateLogger();

        logger.Error("Compressed failure {Code}", 503);
        logger.Dispose();

        var request = await api.ReadRequestAsync(TestContext.Current.CancellationToken);
        Assert.Equal("gzip", request.ContentEncoding);

        using var document = JsonDocument.Parse(request.Body);
        Assert.Contains("Compressed failure 503", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SinkRetriesFailedHttpRequestWithoutLosingPayload()
    {
        await using var api = await LoopbackSignalApi.StartAsync(attempt =>
            attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted);
        var logger = new LoggerConfiguration()
            .WriteTo.Signal(
                CreateOptions(api.Endpoint),
                queueLimitBytes: ByteSize.MB,
                period: TimeSpan.FromMilliseconds(20))
            .CreateLogger();

        logger.Error("Retry this event");
        var first = await api.ReadRequestAsync(TestContext.Current.CancellationToken);
        var second = await api.ReadRequestAsync(TestContext.Current.CancellationToken);
        logger.Dispose();

        Assert.Equal(first.Body, second.Body);
        Assert.Equal(2, api.RequestCount);
    }

    private static Options CreateOptions(string endpoint) => new()
    {
        TimeStampInUtc = true,
        TimeFormat = "yyyy-MM-dd HH:mm:ss 'UTC'",
        SignalSettings = new SignalSettings
        {
            RequestUri = endpoint,
            SenderNumber = "+41000000000",
            Recipients = ["+41111111111", "+41222222222"]
        }
    };

    private sealed record CapturedRequest(
        string Method,
        string Path,
        string? ContentType,
        string? ContentEncoding,
        byte[] Body);

    private sealed class LoopbackSignalApi : IAsyncDisposable
    {
        private readonly WebApplication application;
        private readonly Channel<CapturedRequest> requests = Channel.CreateUnbounded<CapturedRequest>();
        private readonly Func<int, HttpStatusCode> responseStatus;
        private int requestCount;

        private LoopbackSignalApi(WebApplication application, Func<int, HttpStatusCode> responseStatus)
        {
            this.application = application;
            this.responseStatus = responseStatus;
        }

        public string Endpoint { get; private set; } = string.Empty;

        public int RequestCount => Volatile.Read(ref requestCount);

        public static async Task<LoopbackSignalApi> StartAsync(
            Func<int, HttpStatusCode>? responseStatus = null)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

            var application = builder.Build();
            var api = new LoopbackSignalApi(application, responseStatus ?? (_ => HttpStatusCode.OK));
            application.MapPost("/v2/send", api.HandleRequestAsync);
            await application.StartAsync(TestContext.Current.CancellationToken);

            var addresses = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                ?.Addresses;
            var address = Assert.Single(addresses!);
            api.Endpoint = new Uri(new Uri(address), "/v2/send").AbsoluteUri;
            return api;
        }

        public async Task<CapturedRequest> ReadRequestAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            return await requests.Reader.ReadAsync(timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await application.StopAsync();
            await application.DisposeAsync();
        }

        private async Task HandleRequestAsync(HttpContext context)
        {
            await using var body = new MemoryStream();
            if (string.Equals(context.Request.Headers.ContentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
            {
                using var gzip = new GZipStream(context.Request.Body, CompressionMode.Decompress, leaveOpen: true);
                await gzip.CopyToAsync(body, context.RequestAborted);
            }
            else
            {
                await context.Request.Body.CopyToAsync(body, context.RequestAborted);
            }

            var request = new CapturedRequest(
                context.Request.Method,
                context.Request.Path,
                context.Request.ContentType,
                context.Request.Headers.ContentEncoding.FirstOrDefault(),
                body.ToArray());
            await requests.Writer.WriteAsync(request, context.RequestAborted);

            var attempt = Interlocked.Increment(ref requestCount);
            context.Response.StatusCode = (int)responseStatus(attempt);
        }
    }
}
