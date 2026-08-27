# Serilog.Sinks.Signal

A non-durable [Serilog](https://serilog.net/) sink that sends log events through the [signal-cli-rest-api](https://github.com/bbernhard/signal-cli-rest-api) `/v2/send` endpoint.

**Package:** [Serilog.Sinks.Signal](https://www.nuget.org/packages/Serilog.Sinks.Signal)

## Compatibility

* .NET Framework 4.6.2 or later (`net462`)
* .NET Standard 2.0, including modern .NET applications

The package intentionally retains the old `SignalSettings.RrequestUri` property as an obsolete alias. New code should use `RequestUri`.

## Signal API setup

This sink requires a running Signal REST API.

The upstream `signal-cli-rest-api` project supports multiple deployment modes. The following instructions provide a minimal local Docker setup for Linux and Windows.

### Linux

Make sure Docker and Docker Compose are installed.

Clone the repository:

```bash
git clone https://github.com/bbernhard/signal-cli-rest-api.git
cd signal-cli-rest-api
```

Start the Signal REST API:

```bash
docker compose up -d
```

Verify that the container is running:

```bash
docker compose ps
```

You can also inspect all running Docker containers:

```bash
docker ps
```

The API should then be available at:

```text
http://localhost:8080
```

Persist the Signal configuration volume so that recreating or updating the container does not remove the linked Signal device and its cryptographic keys.

### Windows

Install and start Docker Desktop before starting the Signal REST API.

Open PowerShell and clone the repository:

```powershell
git clone https://github.com/bbernhard/signal-cli-rest-api.git
cd signal-cli-rest-api
```

Start the Signal REST API:

```powershell
docker compose up -d
```

Verify that the container is running:

```powershell
docker compose ps
```

You can also inspect all running Docker containers:

```powershell
docker ps
```

The API should then be available at:

```text
http://localhost:8080
```

Persist the Signal configuration volume so that recreating or updating the container does not remove the linked Signal device and its cryptographic keys.

## Link Signal account

The Signal REST API must be linked to a Signal account before it can send messages.

Open the following URL in a browser:

```text
http://localhost:8080/v1/qrcodelink?device_name=signal-api
```

A QR code will be displayed.

On your phone, open Signal and go to:

**Settings → Linked devices → Link new device**

Scan the displayed QR code.

![QR code linking](https://raw.githubusercontent.com/husam-ebish/Serilog.Sinks.Signal/main/readme/login-qr-code.png)

After linking succeeds, the container acts as a secondary device for that Signal account.

## Test the Signal API

Before configuring Serilog, verify that the Signal REST API can successfully send a message.

Replace:

* `+41000000000` with the Signal account linked to `signal-cli-rest-api`.
* `+41111111111` with the intended Signal recipient.

Phone numbers should be supplied in international E.164 format.

### Linux

Run:

```bash
curl -X POST \
  -H "Content-Type: application/json" \
  "http://localhost:8080/v2/send" \
  -d '{"message":"Test via Signal API!","number":"+41000000000","recipients":["+41111111111"]}'
```

The same request can be sent on one line:

```bash
curl -X POST -H "Content-Type: application/json" "http://localhost:8080/v2/send" -d '{"message":"Test via Signal API!","number":"+41000000000","recipients":["+41111111111"]}'
```

### Windows PowerShell

Using `Invoke-RestMethod` with `ConvertTo-Json` is recommended on Windows PowerShell.

Create the request body:

```powershell
$body = @{
    message    = "Test via Signal API!"
    number     = "+41000000000"
    recipients = @("+41111111111")
} | ConvertTo-Json
```

Send the request:

```powershell
Invoke-RestMethod `
    -Method POST `
    -Uri "http://localhost:8080/v2/send" `
    -ContentType "application/json" `
    -Body $body
```

The complete request can also be executed as a single PowerShell statement:

```powershell
$body = @{ message = "Test via Signal API!"; number = "+41000000000"; recipients = @("+41111111111") } | ConvertTo-Json; Invoke-RestMethod -Method POST -Uri "http://localhost:8080/v2/send" -ContentType "application/json" -Body $body
```

### Multiple recipients

The `recipients` property is an array and can contain multiple Signal numbers.

Linux:

```bash
curl -X POST \
  -H "Content-Type: application/json" \
  "http://localhost:8080/v2/send" \
  -d '{"message":"Test via Signal API!","number":"+41000000000","recipients":["+41111111111","+41222222222"]}'
```

Windows PowerShell:

```powershell
$body = @{
    message = "Test via Signal API!"
    number = "+41000000000"
    recipients = @(
        "+41111111111",
        "+41222222222"
    )
} | ConvertTo-Json

Invoke-RestMethod `
    -Method POST `
    -Uri "http://localhost:8080/v2/send" `
    -ContentType "application/json" `
    -Body $body
```

### Successful response

A successful request normally returns a response containing a timestamp.

PowerShell may display:

```text
timestamp
---------
1787857633637
```

Receiving a timestamp confirms that the Signal API accepted the send request.

If the API returns:

```json
{
  "error": "Couldn't process request - invalid request"
}
```

verify that:

1. The Signal account has been successfully linked.
2. `number` contains the linked sender number.
3. `recipients` contains valid Signal recipient numbers.
4. All phone numbers use international E.164 format.
5. The request uses `Content-Type: application/json`.
6. The JSON body is valid.

On Windows PowerShell, prefer `Invoke-RestMethod` with `ConvertTo-Json` instead of manually escaping JSON in a `curl.exe` command.

## Deployment security

The Signal API can send messages as the linked account and its persisted volume contains sensitive keys.

Keep a local instance bound to a trusted interface.

For remote deployments:

* Put the Signal API behind authenticated HTTPS.
* Restrict network access to trusted systems.
* Protect the Signal configuration volume.
* Protect backups containing Signal configuration.
* Do not expose port `8080` directly to the public Internet.
* Do not place credentials, secrets, or unnecessary private data in log events.

## Installation

Install the package using the .NET CLI:

```bash
dotnet add package Serilog.Sinks.Signal
```

The .NET CLI command works on Windows, Linux, and macOS when the .NET SDK is installed.

### Visual Studio Package Manager Console

Visual Studio users can alternatively run:

```powershell
Install-Package Serilog.Sinks.Signal
```

The NuGet package selects the compatible target-framework asset and restores the sink's dependencies automatically. After installation, use the following quick start to test delivery.

## Quick start with Program.cs

The following complete `Program.cs` example configures `Serilog.Sinks.Signal`, enables sink diagnostics, reports when the application starts, demonstrates every standard Serilog log level and structured logging, and flushes pending Signal messages when the application exits.

Replace the sender and recipient numbers before running the application.

```csharp
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Signal;

// Show sink and HTTP delivery failures that Serilog otherwise reports internally.
SelfLog.Enable(Console.Error);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Signal(
        options: new Options
        {
            SignalSettings = new SignalSettings
            {
                RequestUri = "http://localhost:8080/v2/send",

                // Signal account linked to signal-cli-rest-api.
                SenderNumber = "+41000000000",

                // One or more Signal recipients.
                Recipients = new[]
                {
                    "+41111111111"
                }
            },
            TimeStampInUtc = true,
            TimeFormat = "yyyy-MM-dd HH:mm:ss"
        },
        queueLimitBytes: 10 * ByteSize.MB,
        logEventLimitBytes: 256 * ByteSize.KB,

        // Verbose allows every standard Serilog level
        // in this example to be delivered to Signal.
        restrictedToMinimumLevel: LogEventLevel.Verbose)
    .CreateLogger();

try
{
    Log.Information("Application started");

    // Standard Serilog log levels.
    Log.Verbose("Verbose diagnostic message");
    Log.Debug("Debug message");
    Log.Information("Information message");
    Log.Warning("Warning message");
    Log.Error("Error message");
    Log.Fatal("Fatal message");

    // Structured logging.
    var applicationName = "MyApplication";
    var version = "1.0.0";

    Log.Information(
        "{ApplicationName} version {Version} is running",
        applicationName,
        version);

    // Your application code starts here.
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Application shutting down");

    // Flush queued events and dispose HTTP resources.
    Log.CloseAndFlush();
}
```

### Log levels

Serilog provides the following standard log levels, from least to most severe:

| Level       | Method                 | Typical use                                                            |
| ----------- | ---------------------- | ---------------------------------------------------------------------- |
| Verbose     | `Log.Verbose(...)`     | Very detailed diagnostic information                                   |
| Debug       | `Log.Debug(...)`       | Development and debugging information                                  |
| Information | `Log.Information(...)` | Normal application events, startup, shutdown, and completed operations |
| Warning     | `Log.Warning(...)`     | Unexpected situations that do not stop the application                 |
| Error       | `Log.Error(...)`       | Failed operations and handled exceptions                               |
| Fatal       | `Log.Fatal(...)`       | Critical failures that may terminate the application                   |

The example uses:

```csharp
.MinimumLevel.Verbose()
```

and:

```csharp
restrictedToMinimumLevel: LogEventLevel.Verbose
```

so that every standard Serilog level can be sent to Signal.

### Recommended production log level

Sending `Verbose` and `Debug` events to Signal can generate a large number of messages.

For most production applications, a more practical configuration is:

```csharp
.MinimumLevel.Debug()
```

with:

```csharp
restrictedToMinimumLevel: LogEventLevel.Information
```

This allows the application to accept `Debug` events for other configured sinks while Signal receives only:

* Information
* Warning
* Error
* Fatal

For example:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Signal(
        options: new Options
        {
            SignalSettings = new SignalSettings
            {
                RequestUri = "http://localhost:8080/v2/send",
                SenderNumber = "+41000000000",
                Recipients = new[]
                {
                    "+41111111111"
                }
            },
            TimeStampInUtc = true,
            TimeFormat = "yyyy-MM-dd HH:mm:ss"
        },
        queueLimitBytes: 10 * ByteSize.MB,
        logEventLimitBytes: 256 * ByteSize.KB,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();
```

With this configuration:

```csharp
Log.Debug("Debug information");
// Not sent to Signal.

Log.Information("Application started");
// Sent to Signal.

Log.Warning("A warning occurred");
// Sent to Signal.

Log.Error("An operation failed");
// Sent to Signal.

Log.Fatal("Application encountered a critical failure");
// Sent to Signal.
```

### Logging exceptions

Pass the exception as the first argument so the exception information is available to the formatter:

```csharp
try
{
    // Application operation.
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to process the operation");
}
```

For an unrecoverable application-level failure:

```csharp
try
{
    // Application startup and execution.
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

### Structured logging

Serilog message templates should be preferred over string interpolation.

Recommended:

```csharp
Log.Information(
    "User {UserId} completed operation {Operation}",
    userId,
    operationName);
```

Instead of:

```csharp
Log.Information(
    $"User {userId} completed operation {operationName}");
```

Structured properties such as `UserId` and `Operation` remain available to Serilog formatters and any additional configured sinks.

### Application startup and shutdown

A common application pattern is:

```csharp
try
{
    Log.Information("Application started");

    // Run application.
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Application shutting down");
    Log.CloseAndFlush();
}
```

`Log.CloseAndFlush()` is important because `Serilog.Sinks.Signal` uses an in-memory queue.

It gives the sink an opportunity to perform its final delivery pass and release its HTTP resources before the process exits.

A process crash, forced termination, or failed final request can still result in queued events being lost. Use an additional durable logging destination when log retention is required.

## Code configuration

A minimal production-oriented configuration can be created directly in code:

```csharp
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Signal;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Signal(
        options: new Options
        {
            SignalSettings = new SignalSettings
            {
                RequestUri = "http://localhost:8080/v2/send",
                SenderNumber = "+41000000000",
                Recipients = new[]
                {
                    "+41111111111",
                    "+41222222222"
                }
            },
            TimeStampInUtc = true,
            TimeFormat = "yyyy-MM-dd HH:mm:ss"
        },
        queueLimitBytes: 10 * ByteSize.MB,
        logEventLimitBytes: 256 * ByteSize.KB,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

try
{
    Log.Information(
        "Hello from {Application}",
        "Serilog.Sinks.Signal");
}
finally
{
    Log.CloseAndFlush();
}
```

Use:

```csharp
queueLimitBytes: null
```

only when an unbounded in-memory queue is acceptable.

A bounded queue prevents a prolonged Signal API outage from consuming memory without limit.

## Gzip requests

To send gzip-compressed request bodies when the deployed API or proxy supports them:

```csharp
using Serilog.Sinks.Signal.HttpClients;

// Add to WriteTo.Signal(...)
httpClient: new JsonGzipHttpClient()
```

## JSON configuration

Install [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration):

```bash
dotnet add package Serilog.Settings.Configuration
```

Then configure the sink in `appsettings.json`:

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
              "RequestUri": "http://localhost:8080/v2/send",
              "SenderNumber": "+41000000000",
              "Recipients": [
                "+41111111111",
                "+41222222222"
              ]
            },
            "TimeStampInUtc": true,
            "TimeFormat": "yyyy-MM-dd HH:mm:ss"
          },
          "queueLimitBytes": 10485760,
          "logEventLimitBytes": 262144,
          "logEventsInBatchLimit": 1,
          "batchSizeLimitBytes": null,
          "period": "00:00:02"
        }
      }
    ]
  }
}
```

## Configuration reference

| Setting                       | Default                       | Description                                                                              |
| ----------------------------- | ----------------------------- | ---------------------------------------------------------------------------------------- |
| `SignalSettings.RequestUri`   | required                      | Signal `/v2/send` endpoint.                                                              |
| `SignalSettings.SenderNumber` | required                      | Linked sender number in international format.                                            |
| `SignalSettings.Recipients`   | required                      | One or more recipient numbers.                                                           |
| `TimeStampInUtc`              | `true`                        | Render timestamps as UTC.                                                                |
| `TimeFormat`                  | `o`                           | .NET date/time format string.                                                            |
| `queueLimitBytes`             | required argument             | Maximum in-memory queued UTF-8 bytes; `null` is unbounded.                               |
| `logEventLimitBytes`          | `null`                        | Maximum rendered event size; larger events are dropped.                                  |
| `logEventsInBatchLimit`       | `1`                           | Maximum events combined into one Signal message; `null` is unlimited.                    |
| `batchSizeLimitBytes`         | `null`                        | Approximate maximum UTF-8 event bytes in one message.                                    |
| `period`                      | 2 seconds                     | Normal queue polling interval. Failed requests use exponential backoff up to 10 minutes. |
| `textFormatter`               | `NormalRenderedTextFormatter` | Formats each Serilog event.                                                              |
| `batchFormatter`              | `ArrayBatchFormatter`         | Combines rendered events into one message.                                               |
| `httpClient`                  | `JsonHttpClient`              | Sends the Signal API request. Custom implementations are supported.                      |
| `restrictedToMinimumLevel`    | Serilog minimum               | Minimum level accepted by this sink.                                                     |

Limits must be positive when specified, the period must be greater than zero, and sender/recipient values cannot be empty.

Invalid configuration is rejected when the sink is created instead of failing repeatedly in the background.

## Delivery and failure behavior

* Events are formatted synchronously and queued in memory.
* No database or durable storage is used.
* The queue drops new events when `queueLimitBytes` is reached.
* Oversized events are dropped when they exceed the configured event limit.
* Queue-limit and oversized-event conditions are reported through Serilog's `SelfLog`.
* Non-success HTTP responses retain the dequeued batch for retry.
* Request exceptions retain the dequeued batch for retry.
* Failed requests use exponential backoff.
* Disposing the logger stops the timer.
* Disposal performs one final delivery pass.
* HTTP resources are released when the logger is disposed.
* Process crashes or forced termination can still lose queued events.
* A failed final request can still lose queued events.

Use a durable logging destination alongside this sink when logs must be retained.

Multiple events can be combined in one request by increasing `logEventsInBatchLimit`.

Because Signal messages have practical length limits, keep both event and batch byte limits bounded when enabling batching.

### Troubleshooting delivery

Enable Serilog's internal diagnostics before creating the logger:

```csharp
using Serilog.Debugging;

SelfLog.Enable(Console.Error);
```

If no Signal message arrives:

1. Send the test request from the **Test the Signal API** section to confirm that the REST API, linked sender and recipient work independently of Serilog.
2. Check the `SelfLog` output for connection errors or non-success HTTP responses.
3. Confirm that normal shutdown reaches `Log.CloseAndFlush()`. Events are queued in memory, and stopping the debugger before the `finally` block prevents the final delivery pass.
4. If Visual Studio pauses on an exception inside a `try` block, continue execution when the exception is expected and handled. Alternatively, review **Debug > Windows > Exception Settings** and disable **Break when thrown** for that exception type.

Avoid deliberately throwing an exception in the initial delivery test. First confirm delivery with a normal `Log.Information(...)` event, then test exception logging with exceptions raised by real application operations.

## Output format

The default formatter produces a human-readable message containing:

* Timestamp
* Level
* Message template
* Rendered message
* Exception
* Properties
* Explicitly formatted property renderings

For example:

```text
Timestamp: 2026-01-18 13:22:36

Level: ⛔Error

MessageTemplate:
"Order {OrderId} failed"

RenderedMessage:
"Order 42 failed"

Properties:
"OrderId":42
```

## Development

The repository pins the .NET 10 SDK feature band in `global.json`.

Install a compatible .NET 10 SDK before building the project.

### Debugging a local build

End users should install and test the NuGet package as described in **Installation**. Referencing a DLL from `bin` is intended only for local sink development and debugging.

When debugging a modern .NET application, reference the `netstandard2.0` build. When debugging a .NET Framework 4.6.2-or-later application, reference the `net462` build. Using the `net462` DLL from a modern .NET application can cause `MSB3277` or `CS1705` dependency-version conflicts.

Prefer a `ProjectReference` while developing the sink because MSBuild selects the compatible target automatically. The **Test the package locally** section describes how to verify the actual NuGet consumer experience before publishing.

### Linux

Restore dependencies:

```bash
dotnet restore --locked-mode
```

Build:

```bash
dotnet build --configuration Release --no-restore
```

Run the tests:

```bash
dotnet test --configuration Release --no-build
```

Create the package:

```bash
dotnet pack src/Serilog.Sinks.Signal/Serilog.Sinks.Signal.csproj --configuration Release --no-build
```

The commands can also be run sequentially:

```bash
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet pack src/Serilog.Sinks.Signal/Serilog.Sinks.Signal.csproj --configuration Release --no-build
```

### Windows PowerShell

Restore dependencies:

```powershell
dotnet restore --locked-mode
```

Build:

```powershell
dotnet build --configuration Release --no-restore
```

Run the tests:

```powershell
dotnet test --configuration Release --no-build
```

Create the package:

```powershell
dotnet pack src/Serilog.Sinks.Signal/Serilog.Sinks.Signal.csproj --configuration Release --no-build
```

The commands can also be run sequentially:

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet pack src/Serilog.Sinks.Signal/Serilog.Sinks.Signal.csproj --configuration Release --no-build
```

The solution contains the library under `src/` and unit plus loopback HTTP integration tests under `tests/`.

The integration tests exercise the complete logger-to-HTTP pipeline without requiring a Signal account or external service.

GitHub Actions performs:

* Locked restore
* Formatting check
* Build
* Complete test suite
* Coverage collection
* Package creation

Dependabot checks both NuGet and GitHub Actions dependencies.

## Test the package locally

Create a uniquely versioned prerelease package without publishing it externally.

### Windows PowerShell

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/pack-local.ps1
```

The script performs a locked restore, Release build, complete test run, and package creation.

By default, it writes a package such as:

```text
Serilog.Sinks.Signal.1.2.0-local.20260827153000.nupkg
```

to:

```text
artifacts/local-feed
```

The script prints the exact installation command.

The timestamp prevents NuGet's global package cache from reusing an older local build.

Install the generated package into another project, replacing the version and project path with the values printed by the script:

```powershell
dotnet add ../MyTestApp/MyTestApp.csproj package Serilog.Sinks.Signal `
  --version 1.2.0-local.20260827153000 `
  --no-restore

dotnet restore ../MyTestApp/MyTestApp.csproj `
  --configfile ./NuGet.Local.Config
```

`NuGet.Local.Config` selects `artifacts/local-feed` for the locally built package and NuGet.org for its dependencies.

This does not upload or deploy anything outside the machine.

A specific version or output directory can also be supplied:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/pack-local.ps1 `
  -Version 1.2.0-local.manual1 `
  -OutputDirectory C:\NuGetLocal
```

When using a different output directory, configure that directory as a package source in a temporary NuGet configuration.

## Versioning and publishing

The latest package published to NuGet.org is [`1.1.0`](https://www.nuget.org/packages/Serilog.Sinks.Signal/1.1.0), published on January 18, 2026.

The project currently declares `1.2.0` in `VersionPrefix`. Version `1.2.0` has not been published yet.

Package versions use semantic versioning:

* Patch (`1.2.1`): backward-compatible bug fixes.
* Minor (`1.3.0`): backward-compatible features and improvements.
* Major (`2.0.0`): intentional breaking changes.

## One-time NuGet.org setup

Releases use NuGet.org trusted publishing, so no long-lived API key is stored in GitHub.

1. On NuGet.org, open **Account settings → Trusted Publishing** and add a GitHub Actions policy.
2. Set the repository owner to `husam-ebish`.
3. Set the repository to `Serilog.Sinks.Signal`.
4. Set the workflow file to `release.yml`.
5. Leave the environment empty because the workflow does not declare a GitHub environment.
6. In the GitHub repository, add an Actions secret named `NUGET_USER` containing the NuGet.org profile name that owns `Serilog.Sinks.Signal`.

## Publish a new version

1. Update `VersionPrefix` in:

   ```text
   src/Serilog.Sinks.Signal/Serilog.Sinks.Signal.csproj
   ```

2. Keep `PackageValidationBaselineVersion` set to the most recent published stable version.

3. Move the pending changelog entries to the new version heading and add its release date.

4. If dependencies changed, regenerate and commit the package lock files.

5. Run the commands in the **Development** section.

6. Commit the release and merge it into `main`.

7. Create and push an annotated Git tag whose value exactly matches `VersionPrefix`.

### Linux

```bash
git tag -a v1.2.0 -m "Serilog.Sinks.Signal 1.2.0"
git push origin v1.2.0
```

### Windows PowerShell

```powershell
git tag -a v1.2.0 -m "Serilog.Sinks.Signal 1.2.0"
git push origin v1.2.0
```

Pushing the tag starts:

```text
.github/workflows/release.yml
```

The workflow validates that the tag and project versions match and then performs:

* Locked restore
* Formatting check
* Build
* Tests
* Package creation
* NuGet.org publishing
* Symbol package publishing
* GitHub release creation

NuGet package versions cannot be overwritten.

If publishing a version succeeds, any correction must use a new version number.

Consumers can then select the new version explicitly:

```bash
dotnet add package Serilog.Sinks.Signal --version 1.2.0
```

The command is the same on Linux and Windows when using the .NET CLI.

## Changelog

### 1.2.0 (2026-08-27)

* Added unit and loopback HTTP integration tests, coverage collection, CI, Dependabot, locked restores, package validation, automated tag-based publishing, and local package-feed testing.
* Updated the supported targets to .NET Framework 4.6.2 and .NET Standard 2.0.
* Updated `Microsoft.Extensions.Configuration.Abstractions` and `System.Text.Json` to `10.0.11`; removed unused and deprecated package references while retaining Serilog `2.12.0` for consumer compatibility.
* Added the correctly spelled `SignalSettings.RequestUri`; retained `RrequestUri` as an obsolete compatibility alias.
* Fixed Signal API JSON property casing, gzip message posting, failed-batch retention, HTTP response disposal, shutdown behavior, formatter output, invariant formatting, and invalid configuration handling.
* Added bounded-failure logging, deterministic cross-platform builds, security guidance, and complete configuration and delivery documentation.
* Expanded Signal API setup and testing documentation with Linux and Windows PowerShell commands.
* Added a PowerShell-native `Invoke-RestMethod` example using `ConvertTo-Json`.
* Added Signal API troubleshooting guidance and clarified sender and recipient requirements.
* Added a complete `Program.cs` quick-start example demonstrating application startup, shutdown, structured logging, sink diagnostics, and all standard Serilog log levels.
* Documented local debugging target selection, enabled `SelfLog` in the quick start, and removed the deliberately thrown sample exception that could interrupt final queue flushing under a debugger.

Compatibility notes: the minimum explicit .NET Framework target changed from 4.6.1 to 4.6.2, dependency minimums were raised, and invalid zero, negative, or incomplete configurations now fail during logger creation.

### 1.1.0 (2026-01-18)

* Updated package dependencies, including `System.Text.Json` `8.0.5`.
* Published assets for .NET Framework 4.6.1, .NET Standard 2.0, and .NET Standard 2.1.
* Refreshed package documentation.

### 1.0.0 (2023-01-15)

* Initial NuGet release with Signal `/v2/send` delivery, in-memory queueing, batching, and text formatting.

## Credits

The original queueing and HTTP sink design was based on [Serilog.Sinks.Http](https://github.com/FantasticFiasco/serilog-sinks-http) by FantasticFiasco.
