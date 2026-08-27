using System;

namespace Serilog.Sinks.Signal;

/// <summary>
/// Configures the Signal REST API endpoint and message addresses.
/// </summary>
public class SignalSettings
{
    private string? requestUri;

    /// <summary>
    /// Gets or sets the Signal REST API endpoint URI.
    /// </summary>
    public string? RequestUri
    {
        get => requestUri;
        set => requestUri = value;
    }

    /// <summary>
    /// Gets or sets the Signal REST API endpoint URI.
    /// </summary>
    /// <remarks>Use <see cref="RequestUri"/>. This alias is retained for compatibility.</remarks>
    [Obsolete("Use RequestUri instead.")]
    public string? RrequestUri
    {
        get => requestUri;
        set => requestUri = value;
    }

    /// <summary>
    /// Gets or sets the Signal sender number in international format.
    /// </summary>
    public string? SenderNumber { get; set; }

    /// <summary>
    /// Gets or sets the Signal recipient numbers in international format.
    /// </summary>
    public string[]? Recipients { get; set; }
}
