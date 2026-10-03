# Observability

```text
Logs:    ILogger → stdout/stderr → Docker logs → Grafana Alloy → Loki → Grafana
Traces:  ASP.NET Core / HttpClient / SqlClient → OTLP HTTP(protobuf) → Tempo → Grafana
```

NestFlow API and Worker use `Library.Observability` and `Library.Logging` as the single
pipeline. The application never talks to Loki; it only writes to the console through
`ILogger<T>` (Serilog console provider, exceptions keep their stack trace) and exports
traces to the OTLP endpoint. Metrics and OTLP log export are disabled
(`EnableMetrics=false`, `WriteToOtlp=false`) because Tempo only accepts traces and logs
reach Loki through Docker + Alloy.

Errors and warnings are recorded by `ILogger`:
unhandled exceptions by `LibraryExceptionHandler` (5xx → Error, 4xx → Warning),
model-validation failures by a warning in `Program.cs` (field names only, never values),
and the rest by the services / controllers that catch them.

## Environment variables (docker-compose / `.env`)

| Variable | Maps to | Notes |
|---|---|---|
| `ENABLE_OPEN_TELEMETRY` | `Library:Observability:Enabled` | default `true` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `Library:Observability:OtlpEndpoint` | `http://tempo:4318`; empty = instrumentation only, no exporter |
| `OTEL_SERVICE_NAME` | `Library:Observability:ServiceName` | default `nestflow-api` |
| `OTEL_WORKER_SERVICE_NAME` | worker `ServiceName` | default `nestflow-worker` |

The exporter uses HTTP/protobuf; the Library appends `/v1/traces` to a root endpoint.
An unreachable Tempo never blocks startup — the exporter retries in the background.
The container must share a Docker network (`observability`) with Tempo.

## Loki queries

```logql
{container="nestflow-api"}
{container="nestflow-api"} |= "Exception"
{container="nestflow-api"} |= "WRN"
```
