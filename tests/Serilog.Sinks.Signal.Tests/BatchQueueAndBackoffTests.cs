using Serilog.Sinks.Signal.BatchFormatters;
using Serilog.Sinks.Signal.Private.NonDurable;
using Serilog.Sinks.Signal.Private.Time;

namespace Serilog.Sinks.Signal.Tests;

public class BatchQueueAndBackoffTests
{
    [Fact]
    public void ByteSizeCountsUtf8Bytes()
    {
        Assert.Equal(0, ByteSize.From(string.Empty));
        Assert.Equal(5, ByteSize.From("ASCII"));
        Assert.Equal(2, ByteSize.From("\u00e9"));
        Assert.Equal(4, ByteSize.From("\u00e9\u00e9"));
    }

    [Fact]
    public void BatchFormatterCombinesNonBlankEventsInOrder()
    {
        var formatter = new ArrayBatchFormatter();
        using var output = new StringWriter();

        formatter.Format(["first", string.Empty, "  ", "second"], output);

        Assert.Equal("first,second", output.ToString());
    }

    [Fact]
    public void BatchFormatterValidatesArguments()
    {
        var formatter = new ArrayBatchFormatter();

        Assert.Throws<ArgumentNullException>(() => formatter.Format(null!, TextWriter.Null));
        Assert.Throws<ArgumentNullException>(() => formatter.Format([], null!));
    }

    [Fact]
    public void QueueRejectsInvalidInputAndReportsEmptyState()
    {
        Assert.Throws<ArgumentException>(() => new LogEventQueue(0));

        var queue = new LogEventQueue();
        Assert.Throws<ArgumentNullException>(() => queue.TryEnqueue(null!));
        Assert.Equal(LogEventQueue.DequeueResult.QueueEmpty, queue.TryDequeue(null, out var value));
        Assert.Empty(value);
    }

    [Fact]
    public void OversizedEventRemainsQueuedUntilItCanBeRead()
    {
        var queue = new LogEventQueue();
        queue.Enqueue("large");

        Assert.Equal(LogEventQueue.DequeueResult.MaxSizeViolation, queue.TryDequeue(4, out var rejected));
        Assert.Empty(rejected);
        Assert.Equal(LogEventQueue.DequeueResult.Ok, queue.TryDequeue(5, out var accepted));
        Assert.Equal("large", accepted);
    }

    [Fact]
    public void QueueReaderDropsSingleEventThatExceedsBatchLimitAndContinues()
    {
        var queue = new LogEventQueue();
        queue.Enqueue("oversized");
        queue.Enqueue("ok");

        var batch = LogEventQueueReader.Read(queue, logEventsInBatchLimit: null, batchSizeLimitBytes: 2);

        Assert.Equal(["ok"], batch.LogEvents);
        Assert.False(batch.HasReachedLimit);
        Assert.Equal(LogEventQueue.DequeueResult.QueueEmpty, queue.TryDequeue(null, out _));
    }

    [Fact]
    public void QueueReaderLeavesNextEventForTheFollowingBatch()
    {
        var queue = new LogEventQueue();
        queue.Enqueue("123");
        queue.Enqueue("456");

        var first = LogEventQueueReader.Read(queue, logEventsInBatchLimit: null, batchSizeLimitBytes: 5);
        var second = LogEventQueueReader.Read(queue, logEventsInBatchLimit: null, batchSizeLimitBytes: 5);

        Assert.Equal(["123"], first.LogEvents);
        Assert.True(first.HasReachedLimit);
        Assert.Equal(["456"], second.LogEvents);
    }

    [Fact]
    public void BackoffIncreasesAfterRepeatedFailuresAndResetsAfterSuccess()
    {
        var schedule = new ExponentialBackoffConnectionSchedule(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(1), schedule.NextInterval);
        schedule.MarkFailure();
        Assert.Equal(TimeSpan.FromSeconds(1), schedule.NextInterval);
        schedule.MarkFailure();
        Assert.Equal(TimeSpan.FromSeconds(10), schedule.NextInterval);
        schedule.MarkFailure();
        Assert.Equal(TimeSpan.FromSeconds(20), schedule.NextInterval);

        schedule.MarkSuccess();
        Assert.Equal(TimeSpan.FromSeconds(1), schedule.NextInterval);
    }

    [Fact]
    public void BackoffIsCappedAndRejectsInvalidPeriods()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoffConnectionSchedule(TimeSpan.Zero));

        var schedule = new ExponentialBackoffConnectionSchedule(TimeSpan.FromSeconds(1));
        for (var i = 0; i < 100; i++)
            schedule.MarkFailure();

        Assert.Equal(ExponentialBackoffConnectionSchedule.MaximumBackoffInterval, schedule.NextInterval);
    }

    [Fact]
    public async Task PortableTimerRunsCallbackAndEnforcesItsLifecycle()
    {
        var ticked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new PortableTimer(() =>
        {
            ticked.TrySetResult();
            return Task.CompletedTask;
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => timer.Start(TimeSpan.FromMilliseconds(-1)));
        timer.Start(TimeSpan.FromMilliseconds(1));
        await ticked.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        timer.Dispose();
        timer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => timer.Start(TimeSpan.FromMilliseconds(1)));
    }
}
