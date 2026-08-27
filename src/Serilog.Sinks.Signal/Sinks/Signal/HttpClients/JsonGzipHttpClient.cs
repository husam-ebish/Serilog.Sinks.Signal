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
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Serilog.Sinks.Signal.HttpClients;

/// <summary>
/// HTTP client sending gzip-compressed JSON over the network.
/// </summary>
/// <remarks>
/// Compression reduces network traffic at the expense of additional CPU and memory use. The Signal API endpoint
/// must accept gzip request content.
/// </remarks>
/// <seealso cref="JsonHttpClient"/>
/// <seealso cref="IHttpClient"/>
public class JsonGzipHttpClient : IHttpClient
{
    private const string JsonContentType = "application/json";
    private const string GzipContentEncoding = "gzip";

    private readonly HttpClient httpClient;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance using the fastest compression level.
    /// </summary>
    public JsonGzipHttpClient()
        : this(CompressionLevel.Fastest)
    {
    }

    /// <summary>
    /// Initializes a new instance using the specified compression level.
    /// </summary>
    public JsonGzipHttpClient(CompressionLevel compressionLevel)
        : this(new HttpClient(), compressionLevel)
    {
    }

    /// <summary>
    /// Initializes a new instance using the specified HTTP client and compression level. The client is disposed
    /// with this instance.
    /// </summary>
    public JsonGzipHttpClient(HttpClient httpClient, CompressionLevel compressionLevel)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        CompressionLevel = compressionLevel;
    }

    /// <summary>
    /// Gets or sets the compression level.
    /// </summary>
    protected CompressionLevel CompressionLevel { get; set; }

    /// <inheritdoc />
    public virtual void Configure(IConfiguration configuration)
    {
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Compresses and sends a JSON stream to the specified URI.
    /// </summary>
    public virtual async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream)
    {
        if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));
        if (contentStream == null) throw new ArgumentNullException(nameof(contentStream));
        if (disposed) throw new ObjectDisposedException(nameof(JsonGzipHttpClient));

        using var output = new MemoryStream();
        using (var gzipStream = new GZipStream(output, CompressionLevel, leaveOpen: true))
        {
            await contentStream.CopyToAsync(gzipStream).ConfigureAwait(false);
        }

        output.Position = 0;
        using var content = new StreamContent(output);
        content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
        content.Headers.ContentEncoding.Add(GzipContentEncoding);

        return await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<HttpResponseMessage> PostAsync(string requestUri, SignalLogMessage requestContent)
    {
        if (requestContent == null) throw new ArgumentNullException(nameof(requestContent));

        using var input = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(requestContent), writable: false);
        return await PostAsync(requestUri, input).ConfigureAwait(false);
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
