namespace Serilog.Sinks.Signal;

public class SignalLogMessage
{
    internal SignalLogMessage(string message, string number, string[] recipients)
    {
        Message = message;
        Number = number;
        Recipients = recipients;
    }

    public string Message { get; }

    public string Number { get; }

    public string[] Recipients { get; }
}
