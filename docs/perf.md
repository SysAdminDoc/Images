# Decode Pipeline Observability

Images ships an ETW / EventPipe event source (`Images-Decode`) that exposes
real-time counters and structured events from the decode pipeline.

## Live counters with dotnet-counters

```
dotnet-counters monitor --process-id <PID> --counters Images-Decode
```

Available counters:

| Counter                   | Type         | Description                        |
|---------------------------|--------------|------------------------------------|
| `images-decoded`          | Incrementing | Total decode attempts              |
| `decode-duration-ms`      | Mean/Min/Max | Time per decode in milliseconds    |
| `wic-decodes`             | Incrementing | Decodes handled by WIC             |
| `magick-fallback-decodes` | Incrementing | Decodes that fell back to Magick.NET |
| `thumbnail-writes`        | Incrementing | Thumbnail cache write operations   |
| `decode-failures`         | Incrementing | Decode attempts that failed        |

## Structured events with dotnet-trace

```
dotnet-trace collect --process-id <PID> --providers Images-Decode
```

Events:

| ID | Name             | Level         | Fields                          |
|----|------------------|---------------|---------------------------------|
| 1  | DecodeStarted    | Informational | path, decoder                   |
| 2  | DecodeCompleted  | Informational | path, decoder, durationMs       |
| 3  | DecodeFailed     | Warning       | path, error                     |

## Finding the process ID

```powershell
Get-Process Images | Select-Object Id
```

Or pass the process name directly:

```
dotnet-counters monitor --name Images --counters Images-Decode
```
