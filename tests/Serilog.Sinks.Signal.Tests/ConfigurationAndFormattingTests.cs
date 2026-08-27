using System.Globalization;
using Microsoft.Extensions.Configuration;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Signal.TextFormatters;

namespace Serilog.Sinks.Signal.Tests;

public class ConfigurationAndFormattingTests
{
    [Fact]
    public void LegacyRequestUriAliasRemainsCompatible()
    {
        var settings = new SignalSettings();

#pragma warning disable CS0618
        settings.RrequestUri = "https://signal.example/v2/send";
        Assert.Equal(settings.RequestUri, settings.RrequestUri);
#pragma warning restore CS0618

        Assert.Equal("https://signal.example/v2/send", settings.RequestUri);
    }

    [Fact]
    public void SignalRejectsAnEmptyRecipientList()
    {
        var options = CreateOptions();
        options.SignalSettings!.Recipients = [];

        Assert.Throws<ArgumentException>(() => new LoggerConfiguration().WriteTo.Signal(options, null));
    }

    [Fact]
    public void SignalRejectsInvalidLimits()
    {
        var options = CreateOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoggerConfiguration().WriteTo.Signal(options, null, logEventsInBatchLimit: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoggerConfiguration().WriteTo.Signal(options, null, logEventLimitBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoggerConfiguration().WriteTo.Signal(options, null, batchSizeLimitBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoggerConfiguration().WriteTo.Signal(options, null, period: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LoggerConfiguration().WriteTo.Signal(options, queueLimitBytes: -1));
    }

    [Fact]
    public void SignalRejectsNullConfigurationArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LoggerSinkConfigurationExtensions.Signal(null!, CreateOptions(), null));
        Assert.Throws<ArgumentNullException>(() =>
            new LoggerConfiguration().WriteTo.Signal(null!, null));
    }

    [Fact]
    public void SignalRejectsInvalidTimestampFormat()
    {
        var options = CreateOptions();
        options.TimeFormat = "%";

        Assert.Throws<ArgumentException>(() => new LoggerConfiguration().WriteTo.Signal(options, null));
    }

    [Fact]
    public void SignalRejectsIncompleteSettings()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoggerConfiguration().WriteTo.Signal(new Options(), null));

        var missingUri = CreateOptions();
        missingUri.SignalSettings!.RequestUri = " ";
        Assert.Throws<ArgumentException>(() => new LoggerConfiguration().WriteTo.Signal(missingUri, null));

        var missingSender = CreateOptions();
        missingSender.SignalSettings!.SenderNumber = null;
        Assert.Throws<ArgumentException>(() => new LoggerConfiguration().WriteTo.Signal(missingSender, null));

        var missingRecipients = CreateOptions();
        missingRecipients.SignalSettings!.Recipients = null;
        Assert.Throws<ArgumentNullException>(() => new LoggerConfiguration().WriteTo.Signal(missingRecipients, null));

        var blankRecipient = CreateOptions();
        blankRecipient.SignalSettings!.Recipients = ["+41111111111", " "];
        Assert.Throws<ArgumentException>(() => new LoggerConfiguration().WriteTo.Signal(blankRecipient, null));
    }

    [Fact]
    public void SignalConfiguresAndOwnsCustomHttpClient()
    {
        var client = new ConfigurableClient();
        var configuration = new ConfigurationManager();
        configuration["Signal:Marker"] = "configured";

        var logger = new LoggerConfiguration()
            .WriteTo.Signal(CreateOptions(), null, httpClient: client, configuration: configuration)
            .CreateLogger();

        Assert.Same(configuration, client.Configuration);
        logger.Dispose();
        Assert.True(client.Disposed);
    }

    [Fact]
    public void SignalLogMessageValidatesRequiredValues()
    {
        Assert.Throws<ArgumentNullException>(() => new SignalLogMessage(null!, "+41000000000", []));
        Assert.Throws<ArgumentNullException>(() => new SignalLogMessage("message", null!, []));
        Assert.Throws<ArgumentNullException>(() => new SignalLogMessage("message", "+41000000000", null!));
    }

    [Fact]
    public void FormatterSeparatesRenderingsAndDoesNotWriteAnUnmatchedBrace()
    {
        var formatter = new NormalRenderedTextFormatter(new Options
        {
            TimeStampInUtc = true,
            TimeFormat = "yyyy-MM-dd HH:mm:ss"
        });
        var template = new MessageTemplateParser().Parse("Value {Value:000}");
        var logEvent = new LogEvent(
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            LogEventLevel.Warning,
            exception: null,
            template,
            [new LogEventProperty("Value", new ScalarValue(7))]);
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        formatter.Format(logEvent, output);

        var text = output.ToString();
        Assert.Contains("Timestamp: 2026-01-02 03:04:05", text);
        Assert.Contains("Warning", text);
        Assert.Contains("RenderedMessage:\r\n\"Value 007\"", text.ReplaceLineEndings("\r\n"));
        Assert.Contains("\r\n\r\nRenderings:\"Value\":[{\"Format\":\"000\",\"Rendering\":\"007\"}]", text.ReplaceLineEndings("\r\n"));
        Assert.DoesNotContain("}]}", text);
    }

    [Fact]
    public void FormatterValidatesArguments()
    {
        var formatter = new NormalTextFormatter(new Options());
        var logEvent = CreateLogEvent();

        Assert.Throws<ArgumentNullException>(() => formatter.Format(null!, TextWriter.Null));
        Assert.Throws<ArgumentNullException>(() => formatter.Format(logEvent, null!));
        Assert.Throws<ArgumentNullException>(() => new NormalTextFormatter(null!));
    }

    [Fact]
    public void NormalFormatterIncludesPropertiesAndExceptionButOmitsRenderedMessage()
    {
        var formatter = new NormalTextFormatter(new Options
        {
            TimeStampInUtc = true,
            TimeFormat = "O"
        });
        var logEvent = new LogEvent(
            new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero),
            LogEventLevel.Error,
            new InvalidOperationException("broken"),
            new MessageTemplateParser().Parse("Order {OrderId}"),
            [new LogEventProperty("OrderId", new ScalarValue(42))]);
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        formatter.Format(logEvent, output);

        var text = output.ToString();
        Assert.Contains("Error", text);
        Assert.Contains("MessageTemplate:", text);
        Assert.Contains("\"Order {OrderId}\"", text);
        Assert.Contains("Properties:", text);
        Assert.Contains("\"OrderId\":42", text);
        Assert.Contains("Exception:", text);
        Assert.Contains("broken", text);
        Assert.DoesNotContain("RenderedMessage:", text);
    }

    internal static Options CreateOptions() => new()
    {
        SignalSettings = new SignalSettings
        {
            RequestUri = "https://signal.example/v2/send",
            SenderNumber = "+41000000000",
            Recipients = ["+41111111111"]
        }
    };

    internal static LogEvent CreateLogEvent() => new(
        DateTimeOffset.UtcNow,
        LogEventLevel.Information,
        exception: null,
        new MessageTemplateParser().Parse("Test"),
        []);

    private sealed class ConfigurableClient : IHttpClient
    {
        public IConfiguration? Configuration { get; private set; }

        public bool Disposed { get; private set; }

        public void Configure(IConfiguration configuration) => Configuration = configuration;

        public Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent) =>
            Task.FromResult(new HttpResponseMessage());

        public void Dispose() => Disposed = true;
    }
}
