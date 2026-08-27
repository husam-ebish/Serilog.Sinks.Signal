// Copyright 2015-2023 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Signal.Private.Time;

namespace Serilog.Sinks.Signal.Private.NonDurable
{
    public class HttpSink : ILogEventSink, IDisposable
    {
        private const int MaximumLoggedResponseBodyLength = 4096;

        private readonly long? logEventLimitBytes;
        private readonly int? logEventsInBatchLimit;
        private readonly long? batchSizeLimitBytes;
        private readonly ITextFormatter textFormatter;
        private readonly IBatchFormatter batchFormatter;
        private readonly IHttpClient httpClient;
        private readonly ExponentialBackoffConnectionSchedule connectionSchedule;
        private readonly PortableTimer timer;
        private readonly object syncRoot = new();
        private readonly LogEventQueue queue;
        private readonly string requestUri;
        private readonly string senderNumber;
        private readonly string[] recipients;

        private Batch? unsentBatch;
        private volatile bool disposed;

        public HttpSink(
            long? queueLimitBytes,
            Options? options,
            long? logEventLimitBytes,
            int? logEventsInBatchLimit,
            long? batchSizeLimitBytes,
            TimeSpan period,
            ITextFormatter textFormatter,
            IBatchFormatter batchFormatter,
            IHttpClient httpClient)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (logEventLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(logEventLimitBytes));
            if (logEventsInBatchLimit <= 0) throw new ArgumentOutOfRangeException(nameof(logEventsInBatchLimit));
            if (batchSizeLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(batchSizeLimitBytes));

            var settings = options.SignalSettings ?? throw new ArgumentNullException(nameof(options));
            requestUri = settings.RequestUri ?? throw new ArgumentNullException(nameof(options));
            senderNumber = settings.SenderNumber ?? throw new ArgumentNullException(nameof(options));
            recipients = settings.Recipients == null
                ? throw new ArgumentNullException(nameof(options))
                : (string[])settings.Recipients.Clone();

            this.logEventLimitBytes = logEventLimitBytes;
            this.logEventsInBatchLimit = logEventsInBatchLimit;
            this.batchSizeLimitBytes = batchSizeLimitBytes;
            this.textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
            this.batchFormatter = batchFormatter ?? throw new ArgumentNullException(nameof(batchFormatter));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            connectionSchedule = new ExponentialBackoffConnectionSchedule(period);
            timer = new PortableTimer(OnTick);
            queue = new LogEventQueue(queueLimitBytes);

            SetTimer();
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            string formattedLogEvent;
            using (var writer = new StringWriter())
            {
                textFormatter.Format(logEvent, writer);
                formattedLogEvent = writer.ToString();
            }

            if (ByteSize.From(formattedLogEvent) > logEventLimitBytes)
            {
                SelfLog.WriteLine(
                    "Log event exceeds the size limit of {0} bytes set for this sink and will be dropped; data: {1}",
                    logEventLimitBytes,
                    formattedLogEvent);
                return;
            }

            lock (syncRoot)
            {
                if (disposed)
                {
                    SelfLog.WriteLine("Cannot emit a log event because the Signal sink has been disposed.");
                    return;
                }

                var result = queue.TryEnqueue(formattedLogEvent);
                if (result == LogEventQueue.EnqueueResult.QueueFull)
                {
                    SelfLog.WriteLine("Queue has reached its limit and the log event will be dropped; data: {0}", formattedLogEvent);
                }
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
            }

            try
            {
                timer.Dispose();
                OnTick().GetAwaiter().GetResult();
            }
            finally
            {
                httpClient.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        private void SetTimer()
        {
            // Called under syncRoot after construction.
            timer.Start(connectionSchedule.NextInterval);
        }

        private async Task OnTick()
        {
            try
            {
                Batch batch;

                do
                {
                    batch = unsentBatch ?? LogEventQueueReader.Read(queue, logEventsInBatchLimit, batchSizeLimitBytes);

                    if (batch.LogEvents.Count == 0)
                    {
                        connectionSchedule.MarkSuccess();
                        continue;
                    }

                    // Retain the dequeued batch before any formatting or network operation can fail.
                    unsentBatch = batch;

                    string payload;
                    using (var payloadWriter = new StringWriter())
                    {
                        batchFormatter.Format(batch.LogEvents, payloadWriter);
                        payload = payloadWriter.ToString();
                    }

                    if (payload.Length == 0)
                    {
                        connectionSchedule.MarkSuccess();
                        unsentBatch = null;
                        continue;
                    }

                    using (HttpResponseMessage response = await httpClient
                        .PostAsync(requestUri, new SignalLogMessage(payload, senderNumber, recipients))
                        .ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            connectionSchedule.MarkSuccess();
                            unsentBatch = null;
                            continue;
                        }

                        connectionSchedule.MarkFailure();

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (body.Length > MaximumLoggedResponseBodyLength)
                            body = body.Substring(0, MaximumLoggedResponseBodyLength) + "…";

                        SelfLog.WriteLine("Received failed HTTP shipping result {0}: {1}", response.StatusCode, body);
                        break;
                    }
                } while (batch.HasReachedLimit);
            }
            catch (Exception exception)
            {
                SelfLog.WriteLine("Exception while emitting periodic batch from {0}: {1}", this, exception);
                connectionSchedule.MarkFailure();
            }
            finally
            {
                lock (syncRoot)
                {
                    if (!disposed)
                        SetTimer();
                }
            }
        }
    }
}
