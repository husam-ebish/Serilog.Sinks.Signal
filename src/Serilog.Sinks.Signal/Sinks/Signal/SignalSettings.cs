namespace Serilog.Sinks.Signal;

public class SignalSettings
{
    public string? RrequestUri { get; set; }

    public string? SenderNumber { get; set; }

    public string[]? Recipients { get; set; }
}
