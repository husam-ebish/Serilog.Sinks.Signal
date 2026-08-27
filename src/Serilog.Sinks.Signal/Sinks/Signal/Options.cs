namespace Serilog.Sinks.Signal;

/// <summary>
/// Configures Signal delivery and log-event rendering.
/// </summary>
public class Options
{
    /// <summary>
    /// Gets or sets the Signal REST API settings.
    /// </summary>
    public SignalSettings? SignalSettings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether timestamps are rendered in UTC.
    /// </summary>
    public bool TimeStampInUtc { get; set; } = true;

    /// <summary>
    /// Gets or sets the .NET date and time format used for event timestamps.
    /// </summary>
    public string TimeFormat { get; set; } = "o";
}
