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
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Signal.HttpClients;

namespace Serilog.Sinks.Signal
{
    /// <summary>
    /// Interface responsible for posting HTTP requests.
    /// </summary>
    /// <seealso cref="JsonHttpClient"/>
    /// <seealso cref="JsonGzipHttpClient"/>
    public interface IHttpClient : IDisposable
    {
        /// <summary>
        /// Configures the HTTP client.
        /// </summary>
        /// <param name="configuration">The application configuration properties.</param>
        void Configure(IConfiguration configuration);

        /// <summary>
        /// Sends a POST request to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="requestUri">The Uri the request is sent to.</param>
        /// <param name="requestContent">Signal message content of the request.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent);
    }
}
