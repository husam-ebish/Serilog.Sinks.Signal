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
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Signal.Private;

namespace Serilog.Sinks.Signal.HttpClients;

/// <summary>
/// HTTP client sending JSON over the network.
/// </summary>
/// <seealso cref="JsonGzipHttpClient"/>
/// <seealso cref="IHttpClient"/>
public class JsonHttpClient : IHttpClient
{
    private const string JsonContentType = "application/json";

    private readonly HttpClient httpClient;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonHttpClient"/> class.
    /// </summary>
    public JsonHttpClient()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance using the specified HTTP client. The client is disposed with this instance.
    /// </summary>
    public JsonHttpClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public virtual void Configure(IConfiguration configuration)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Sends a JSON stream to the specified URI.
    /// </summary>
    public virtual async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream)
    {
        if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));
        if (contentStream == null) throw new ArgumentNullException(nameof(contentStream));
        if (disposed) throw new ObjectDisposedException(nameof(JsonHttpClient));

        using var content = new StreamContent(contentStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);

        return await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent)
    {
        if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));
        if (requestContent == null) throw new ArgumentNullException(nameof(requestContent));
        if (disposed) throw new ObjectDisposedException(nameof(JsonHttpClient));

        using var content = new StringContent(
            JsonSerializer.Serialize(requestContent),
            Encoding.UTF8WithoutBom,
            JsonContentType);

        return await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources owned by this client.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || disposed)
            return;

        httpClient.Dispose();
        disposed = true;
    }
}
