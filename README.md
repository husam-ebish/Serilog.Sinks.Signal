# Serilog.Sinks.Signal
A Serilog sink sending log events via Signal. It is developed based on the [Serilog.Sinks.Http](https://www.nuget.org/packages/serilog.sinks.http) package. 
In order to be able to send Signal messages, you need first to configure your machine. You can use a [Dockerized Signal Messenger REST API](https://github.com/bbernhard/signal-cli-rest-api) for this purpose.

---
**Package**: [Serilog.Sinks.Signal](https://www.nuget.org/packages/serilog.sinks.signal) 

**Platforms:**
- .NET 4.6.1
- .NET Standard 2.0
- .NET Standard 2.1

---
## Introduction

This project started out with a wish to send log events via the Signal chat app. 

Since the log events are sent through HTTP requests, I relied on the [Serilog.Skins.Http](https://github.com/FantasticFiasco/serilog-sinks-http)  to develop this package.

## Getting started

In the following example, the sink will POST log events to a user or multiple users that you specify in the options, this will be done by adding the numbers to which log events are to be sent. You will also need to add the sender number and the API link for sending messages to Signal chats.

```csharp
Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Signal(
            options: new Options
            {
                SignalSettings = new SignalSettings
                {
                    RrequestUri = "http://localhost:9000/v2/send",
                    SenderNumber = "+41000000000",
                    Recipients = new[] { "+41111111111", "+41222222222" }
                },
                TimeStampInUtc = false,
                TimeFormat = "dd.MM.yyyy hh:mm:ss"
            },
            null,
            restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

Log.Information("Hello Serilog Signal");
```

Used in conjunction with [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration) the same sink can be configured in the following way:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Signal",
        "Args": {
          "options": {
            "SignalSettings": {
              "RrequestUri": "http://localhost:9000/v2/send",
              "SenderNumber": "+41000000000",
              "Recipients": [ "+41111111111", "+41222222222" ]
            },
            "TimeStampInUtc": true,
            "TimeFormat": "dd.MM.yyyy hh:mm:ss"
          },
          "queueLimitBytes": null
        }
      }
    ]
  }
}

```

Like `Serilog.Sinks.Http`, the sink is batching multiple log events into a single request, just set `logEventsInBatchLimit` to the size you want. The following hypothetical payload is sent over the network.

The log appears as follows in the Signal chat app:
```
Timestamp: 15.01.2023 01:22:36  
  
Level:🔴Fatal  
  
MessageTemplate:  
"Object reference not set to an instance of an object."  
  
RenderedMessage:  
"Object reference not set to an instance of an object."

Exception:  
"System.NullReferenceException: Object reference not set to an instance of an object.\r\n at Program.<Main>$(String[] args) in C:\\TestProjects\\source\\code\\Test.Store.Api\\Program.cs:line 27"

Properties:  
MachineName: "Husam"  
ProcessId: 5512  
ThreadId: 1  
ExceptionDetail: [("Type": "System.NullReferenceException"), ("HResult": -2147467261), ("Message": "Object reference not set to an instance of an object."), ("Source": "Test.Store.Api"), ("StackTrace": " at Program.<Main>$(String[] args) in C:\TestProjects\source\code\Test.Store.Api\Program.cs:line 27"), ("TargetSite": "Void <Main>$(System.String[])")]
```

## Install via NuGet

If you want to include the HTTP sink in your project, you can [install it directly from NuGet](https://www.nuget.org/packages/Serilog.Sinks.Signal/).

To install the sink, run the following command in the Package Manager Console:

```
PM> Install-Package Serilog.Sinks.Signal
```
