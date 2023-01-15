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
using Microsoft.Extensions.Configuration;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Signal;
using Serilog.Sinks.Signal.BatchFormatters;
using Serilog.Sinks.Signal.HttpClients;
using Serilog.Sinks.Signal.Private.NonDurable;
using Serilog.Sinks.Signal.TextFormatters;

namespace Serilog
{
    /// <summary>
    /// Class containing extension methods to <see cref="LoggerConfiguration"/>, configuring sinks
    /// sending log events over the network using HTTP.
    /// </summary>
    public static class LoggerSinkConfigurationExtensions
    {
        /// <summary>
        /// Adds a non-durable sink that sends log events using HTTP POST over the network. The log
        /// events are stored in memory in the case that the log server cannot be reached.
        /// <para />
        /// The maximum number of log events stored in memory is configurable, and given that we
        /// reach this limit the sink will drop new log events in favor of keeping the old.
        /// <para />
        /// A non-durable sink will lose data after a system or process restart.
        /// </summary>
        /// <param name="sinkConfiguration">The logger configuration.</param>
        /// <param name="options">Signal options.</param>
        /// <param name="queueLimitBytes">
        /// The maximum size, in bytes, of events stored in memory, waiting to be sent over the
        /// network. Specify null for no limit.
        /// </param>
        /// <param name="logEventLimitBytes">
        /// The maximum size, in bytes, for a serialized representation of a log event. Log events
        /// exceeding this size will be dropped. Specify null for no limit. Default value is null.
        /// </param>
        /// <param name="logEventsInBatchLimit">
        /// The maximum number of log events sent as a single batch over the network. Default
        /// value is 1000.
        /// </param>
        /// <param name="batchSizeLimitBytes">
        /// The approximate maximum size, in bytes, for a single batch. The value is an
        /// approximation because only the size of the log events are considered. The extra
        /// characters added by the batch formatter, where the sequence of serialized log events
        /// are transformed into a payload, are not considered. Please make sure to accommodate for
        /// those.
        /// <para />
        /// Another thing to mention is that although the sink does its best to optimize for this
        /// limit, if you decide to use an implementation of <seealso cref="IHttpClient"/> that is
        /// compressing the payload, e.g. <seealso cref="JsonGzipHttpClient"/>, this parameter
        /// describes the uncompressed size of the log events. The compressed size might be
        /// significantly smaller depending on the compression algorithm and the repetitiveness of
        /// the log events.
        /// <para />
        /// Default value is null.
        /// </param>
        /// <param name="period">
        /// The time to wait between checking for event batches. Default value is 2 seconds.
        /// </param>
        /// <param name="textFormatter">
        /// The formatter rendering individual log events into text, for example JSON. Default
        /// value is <see cref="NormalRenderedTextFormatter"/>.
        /// </param>
        /// <param name="batchFormatter">
        /// The formatter batching multiple log events into a payload that can be sent over the
        /// network. Default value is <see cref="ArrayBatchFormatter"/>.
        /// </param>
        /// <param name="restrictedToMinimumLevel">
        /// The minimum level for events passed through the sink. Default value is
        /// <see cref="LevelAlias.Minimum"/>.
        /// </param>
        /// <param name="httpClient">
        /// A custom <see cref="IHttpClient"/> implementation. Default value is
        /// <see cref="JsonHttpClient"/>.
        /// </param>
        /// <param name="configuration">
        /// Configuration passed to <paramref name="httpClient"/>. Parameter is either manually
        /// specified when configuring the sink in source code or automatically passed in when
        /// configuring the sink using
        /// <see href="https://www.nuget.org/packages/Serilog.Settings.Configuration">Serilog.Settings.Configuration</see>.
        /// </param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        public static LoggerConfiguration Signal(
            this LoggerSinkConfiguration sinkConfiguration,
            Options options,
            long? queueLimitBytes,
            long? logEventLimitBytes = null,
            int? logEventsInBatchLimit = 1,
            long? batchSizeLimitBytes = null,
            TimeSpan? period = null,
            ITextFormatter? textFormatter = null,
            IBatchFormatter? batchFormatter = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            IHttpClient? httpClient = null,
            IConfiguration? configuration = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException(nameof(sinkConfiguration));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.SignalSettings == null) throw new ArgumentNullException(nameof(options.SignalSettings));
            if (options.SignalSettings.RrequestUri == null) throw new ArgumentNullException(nameof(options.SignalSettings.RrequestUri));
            if (options.SignalSettings.SenderNumber == null) throw new ArgumentNullException(nameof(options.SignalSettings.SenderNumber));
            if (options.SignalSettings.Recipients == null) throw new ArgumentNullException(nameof(options.SignalSettings.Recipients));

            // Default values
            period ??= TimeSpan.FromSeconds(2);
            textFormatter ??= new NormalRenderedTextFormatter(options);
            batchFormatter ??= new ArrayBatchFormatter();
            httpClient ??= new JsonHttpClient();

            if (configuration != null)
            {
                httpClient.Configure(configuration);
            }

            var sink = new HttpSink(
                options: options,
                queueLimitBytes: queueLimitBytes,
                logEventLimitBytes: logEventLimitBytes,
                logEventsInBatchLimit: logEventsInBatchLimit,
                batchSizeLimitBytes: batchSizeLimitBytes,
                period: period.Value,
                textFormatter: textFormatter,
                batchFormatter: batchFormatter,
                httpClient: httpClient);

            return sinkConfiguration.Sink(sink, restrictedToMinimumLevel);
        }
    }
}
