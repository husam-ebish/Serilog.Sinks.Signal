using System;
using System.Text.Json.Serialization;

namespace Serilog.Sinks.Signal;

/// <summary>
/// Represents the JSON request accepted by the Signal REST API.
/// </summary>
public class SignalLogMessage
{
    /// <summary>
    /// Initializes a Signal message.
    /// </summary>
    public SignalLogMessage(string message, string number, string[] recipients)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Number = number ?? throw new ArgumentNullException(nameof(number));
        Recipients = recipients ?? throw new ArgumentNullException(nameof(recipients));
    }

    /// <summary>
    /// Gets the message body.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; }

    /// <summary>
    /// Gets the sender number.
    /// </summary>
    [JsonPropertyName("number")]
    public string Number { get; }

    /// <summary>
    /// Gets the recipient numbers.
    /// </summary>
    [JsonPropertyName("recipients")]
    public string[] Recipients { get; }
}
