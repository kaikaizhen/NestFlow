# Observability

NestFlow API and Worker use `Library.Observability` and `Library.Logging` for a
single OpenTelemetry/Serilog pipeline. Both processes keep console logging enabled
and attach W3C trace context to HTTP requests and outbound HTTP calls.

No exporter endpoint is configured by default, so the application does not require
Grafana, Loki, Tempo, or Alloy to start.

## When an OTLP collector is available

Configure the API and Worker with the same environment variables. The endpoint must
be an OTLP collector (for example Grafana Alloy), not Grafana, Loki, or Tempo
directly.

```text
Library__Observability__OtlpEndpoint=http://alloy:4318
Library__Observability__Protocol=HttpProtobuf
Library__Observability__SamplingRatio=1.0
Library__Logging__WriteToOtlp=true
```

The collector can then route traces to Tempo, logs to Loki, and metrics to Prometheus
or Mimir. `Library` redacts common credentials, tokens, cookies, and sensitive query
parameters before telemetry is exported. Health endpoints are excluded from tracing.

Use `Library__Observability__SamplingRatio` to reduce trace volume in production;
for example `0.1` retains roughly ten percent of root traces while preserving child
spans for sampled requests.
