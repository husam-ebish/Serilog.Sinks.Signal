namespace Serilog.Sinks.Signal;

public class Options
{
    public SignalSettings? SignalSettings { get; set; }

    public bool TimeStampInUtc { get; set; } = true;

    public string TimeFormat { get; set; } = "o";
}
