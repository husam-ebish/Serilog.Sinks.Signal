using System.Net;
using Microsoft.Extensions.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Signal.BatchFormatters;
using Serilog.Sinks.Signal.Private.NonDurable;

namespace Serilog.Sinks.Signal.Tests;

public class QueueAndReliabilityTests
{
    [Fact]
    public void QueueLimitUsesUtf8ByteSize()
    {
        var queue = new LogEventQueue(queueLimitBytes: 4);

        Assert.Equal(LogEventQueue.EnqueueResult.Ok, queue.TryEnqueue("\u00e9\u00e9"));
        Assert.Equal(LogEventQueue.EnqueueResult.QueueFull, queue.TryEnqueue("a"));
        Assert.Equal(LogEventQueue.DequeueResult.Ok, queue.TryDequeue(null, out var value));
        Assert.Equal("\u00e9\u00e9", value);
    }

    [Fact]
    public void QueueReaderRespectsEventAndByteLimits()
    {
        var queue = new LogEventQueue();
        queue.Enqueue("one");
        queue.Enqueue("two");
        queue.Enqueue("three");

        var first = LogEventQueueReader.Read(queue, logEventsInBatchLimit: 2, batchSizeLimitBytes: null);
        var second = LogEventQueueReader.Read(queue, logEventsInBatchLimit: null, batchSizeLimitBytes: 5);

        Assert.Equal(["one", "two"], first.LogEvents);
        Assert.True(first.HasReachedLimit);
        Assert.Equal(["three"], second.LogEvents);
    }

    [Fact]
    public void QueueReaderRejectsInvalidLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LogEventQueueReader.Read(new LogEventQueue(), 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogEventQueueReader.Read(new LogEventQueue(), null, 0));
    }

    [Fact]
    public async Task RequestExceptionDoesNotLoseDequeuedBatch()
    {
        var client = new RetryClient();
        using var sink = new HttpSink(
            queueLimitBytes: null,
            ConfigurationAndFormattingTests.CreateOptions(),
            logEventLimitBytes: null,
            logEventsInBatchLimit: 1,
            batchSizeLimitBytes: null,
            period: TimeSpan.FromMilliseconds(10),
            textFormatter: new FixedFormatter("event"),
            batchFormatter: new ArrayBatchFormatter(),
            httpClient: client);

        sink.Emit(ConfigurationAndFormattingTests.CreateLogEvent());
        await client.SecondAttempt.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, client.Messages.Count);
        Assert.Equal("event", client.Messages[0].Message);
        Assert.Equal(client.Messages[0].Message, client.Messages[1].Message);
    }

    [Fact]
    public async Task FailedResponseDoesNotLoseDequeuedBatch()
    {
        var client = new StatusRetryClient();
        using var sink = new HttpSink(
            queueLimitBytes: null,
            ConfigurationAndFormattingTests.CreateOptions(),
            logEventLimitBytes: null,
            logEventsInBatchLimit: 1,
            batchSizeLimitBytes: null,
            period: TimeSpan.FromMilliseconds(10),
            textFormatter: new FixedFormatter("event"),
            batchFormatter: new ArrayBatchFormatter(),
            httpClient: client);

        sink.Emit(ConfigurationAndFormattingTests.CreateLogEvent());
        await client.SecondAttempt.Task.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(["event", "event"], client.Messages.Select(message => message.Message));
        Assert.True(client.FirstResponse.Disposed);
    }

    [Fact]
    public void SinkDisposesHttpResponsesAndClient()
    {
        var client = new SuccessfulClient();
        var sink = new HttpSink(
            queueLimitBytes: null,
            ConfigurationAndFormattingTests.CreateOptions(),
            logEventLimitBytes: null,
            logEventsInBatchLimit: 1,
            batchSizeLimitBytes: null,
            period: TimeSpan.FromMinutes(1),
            textFormatter: new FixedFormatter("event"),
            batchFormatter: new ArrayBatchFormatter(),
            httpClient: client);

        sink.Emit(ConfigurationAndFormattingTests.CreateLogEvent());
        sink.Dispose();

        Assert.True(client.Response.Disposed);
        Assert.True(client.Disposed);
    }

    [Fact]
    public void SinkDropsEventThatExceedsItsLimit()
    {
        var client = new SuccessfulClient();
        using var sink = new HttpSink(
            queueLimitBytes: null,
            ConfigurationAndFormattingTests.CreateOptions(),
            logEventLimitBytes: 4,
            logEventsInBatchLimit: 1,
            batchSizeLimitBytes: null,
            period: TimeSpan.FromMinutes(1),
            textFormatter: new FixedFormatter("five!"),
            batchFormatter: new ArrayBatchFormatter(),
            httpClient: client);

        sink.Emit(ConfigurationAndFormattingTests.CreateLogEvent());

        Assert.Equal(0, client.PostCount);
    }

    [Fact]
    public void SinkSnapshotsAddressSettings()
    {
        var options = ConfigurationAndFormattingTests.CreateOptions();
        var client = new SuccessfulClient();
        var sink = new HttpSink(
            queueLimitBytes: null,
            options,
            logEventLimitBytes: null,
            logEventsInBatchLimit: 1,
            batchSizeLimitBytes: null,
            period: TimeSpan.FromMinutes(1),
            textFormatter: new FixedFormatter("event"),
            batchFormatter: new ArrayBatchFormatter(),
            httpClient: client);

        options.SignalSettings!.RequestUri = "https://changed.example/v2/send";
        options.SignalSettings.SenderNumber = "+41999999999";
        options.SignalSettings.Recipients![0] = "+41888888888";
        sink.Emit(ConfigurationAndFormattingTests.CreateLogEvent());
        sink.Dispose();

        Assert.Equal("https://signal.example/v2/send", client.RequestUri);
        Assert.Equal("+41000000000", client.LastMessage!.Number);
        Assert.Equal(["+41111111111"], client.LastMessage.Recipients);
    }

    [Fact]
    public void SinkRejectsNullEventsAndIgnoresEventsAfterDisposal()
    {
        var client = new SuccessfulClient();
        var sink = new HttpSink(
            queueLimitBytes: null,
            ConfigurationAndFormattingTests.CreateOptions(),
            logEventLimitBytes: null,
            logEventsInBatchLimit: 1,
            batchSizeLimitBytes: null,
            period: TimeSpan.FromMinutes(1),
            textFormatter: new FixedFormatter("event"),
            batchFormatter: new ArrayBatchFormatter(),
            httpClient: client);

        Assert.Throws<ArgumentNullException>(() => sink.Emit(null!));
        sink.Dispose();
        sink.Dispose();
        sink.Emit(ConfigurationAndFormattingTests.CreateLogEvent());

        Assert.Equal(0, client.PostCount);
    }

    private sealed class FixedFormatter(string text) : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output) => output.Write(text);
    }

    private sealed class RetryClient : IHttpClient
    {
        public List<SignalLogMessage> Messages { get; } = [];

        public TaskCompletionSource SecondAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Configure(IConfiguration configuration)
        {
        }

        public Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent)
        {
            Messages.Add(requestContent);
            if (Messages.Count == 1)
                return Task.FromException<HttpResponseMessage>(new HttpRequestException("Transient failure"));

            SecondAttempt.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        public void Dispose()
        {
        }
    }

    private sealed class SuccessfulClient : IHttpClient
    {
        public TrackingResponse Response { get; } = new();

        public bool Disposed { get; private set; }

        public int PostCount { get; private set; }

        public string? RequestUri { get; private set; }

        public SignalLogMessage? LastMessage { get; private set; }

        public void Configure(IConfiguration configuration)
        {
        }

        public Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent)
        {
            PostCount++;
            RequestUri = requestUri;
            LastMessage = requestContent;
            return Task.FromResult<HttpResponseMessage>(Response);
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class StatusRetryClient : IHttpClient
    {
        public List<SignalLogMessage> Messages { get; } = [];

        public TrackingResponse FirstResponse { get; } = new(HttpStatusCode.BadGateway);

        public TaskCompletionSource SecondAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Configure(IConfiguration configuration)
        {
        }

        public Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent)
        {
            Messages.Add(requestContent);
            if (Messages.Count == 1)
                return Task.FromResult<HttpResponseMessage>(FirstResponse);

            SecondAttempt.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        public void Dispose()
        {
        }
    }

    private sealed class TrackingResponse : HttpResponseMessage
    {
        public TrackingResponse(HttpStatusCode statusCode = HttpStatusCode.OK)
            : base(statusCode)
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
