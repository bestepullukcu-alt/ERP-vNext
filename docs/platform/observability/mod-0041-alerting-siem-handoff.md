# MOD-0041 Alerting / SIEM Handoff Readiness

## Scope

This is the MOD-0041 Batch 7 handoff inventory for MOD-0042 Alerting / Incident Runbooks and MOD-0265 SIEM / Observability Provider.

This document does not implement alert rules, thresholds, incident runbooks, notification channels, SIEM/APM adapters, custom Platform Admin UI, or provider integrations.

## Completion Position

MOD-0041 can be considered functionally complete with follow-ups after Batch 7 because Batches 1-7 are PASS and the stable signals are inventoried here.

Follow-ups remain outside MOD-0041 implementation scope:

- Prometheus/Grafana runtime provisioning convention is absent; Batch 6 provides dashboard JSON and manual import guidance.
- RabbitMQ and Hangfire storage readiness are exposed through `/health/ready`, not dedicated Prometheus metric families.
- OTLP export is configuration-driven and may be disabled until an OpenTelemetry collector exists.
- Event and job metric families appear after the first event/job execution path exercises the relevant public hook.

## Signal Inventory

| Signal name | Type | Source | Batch | Labels / fields | Cardinality risk | Redaction status | Caveats | Consumer | Readiness |
|---|---|---|---:|---|---|---|---|---|---|
| `http_requests_received_total` | metric | Gateway, Platform API, AuthService | 1-3 | `job`, `method`, `code`, `endpoint`, `controller`, `action` | Medium if raw endpoint templates contain IDs; current prometheus-net endpoint label should prefer route templates | Safe; no bodies or headers | Requires Prometheus scrape target | Both | ready |
| `http_request_duration_seconds` / `_bucket` | metric | Gateway, Platform API, AuthService | 1-3 | `job`, `method`, `code`, `endpoint`, `le` | Medium if endpoint cardinality grows | Safe | Histogram family used for p95/p99 | Both | ready |
| `up` | metric | Prometheus scrape target | 6 | `job`, `instance` | Low to medium by scrape topology | Safe | Exists only in Prometheus, not service runtime | MOD-0042 | ready when Prometheus is deployed |
| `/health/live` | health | Gateway, Platform API, AuthService | 1-3 | HTTP status, sanitized JSON | Low | Safe | Must not check external dependencies | MOD-0042 | ready |
| `/health/ready` | health | Gateway, Platform API, AuthService | 1-5 | HTTP status, sanitized check names/status/duration | Low | Safe; no connection strings | Includes dependency readiness according to service config | MOD-0042 | ready |
| `/health` | health | Gateway, Platform API, AuthService | 1-3 | HTTP status, sanitized aggregate checks | Low | Safe | Operational endpoint for trusted probes | MOD-0042 | ready |
| MongoDB readiness | health | Platform API, AuthService where configured | 1-2 | check name `mongodb`, status, duration | Low | Safe; no Mongo connection string | Visible through `/health/ready` | MOD-0042 | ready |
| RabbitMQ readiness | health | Platform API | 4 | check name `rabbitmq`, status, duration | Low | Safe; no credentials | Only registered when Eventing transport is RabbitMQ | MOD-0042 | ready |
| Hangfire storage readiness | health | Platform API | 5 | check name `hangfire_storage`, status, duration | Low | Safe; no storage credentials | Only registered when BackgroundJobs or dashboard is enabled | MOD-0042 | ready |
| `event_publish_started_total` | metric | Platform API eventing publisher decorator | 4 | `service`, `environment`, `event_name` | Low if event names remain stable | Safe; no event payload/correlation id labels | Appears after first event publish | Both | ready |
| `event_publish_succeeded_total` | metric | Platform API eventing publisher decorator | 4 | `service`, `environment`, `event_name` | Low | Safe | Appears after first successful publish | Both | ready |
| `event_publish_failed_total` | metric | Platform API eventing publisher decorator | 4 | `service`, `environment`, `event_name` | Low | Safe; exception text excluded | Appears after first failed publish | Both | ready |
| `event_publish_duration_seconds` / `_bucket` | metric | Platform API eventing publisher decorator | 4 | `service`, `environment`, `event_name`, `result`, `le` | Low | Safe | Appears after first publish attempt | Both | ready |
| `event_consume_succeeded_total` | metric | Platform API eventing observability sink | 4 | `service`, `environment`, `event_name` | Low | Safe | Appears after first consume success callback | Both | ready |
| `event_consume_failed_total` | metric | Platform API eventing observability sink | 4 | `service`, `environment`, `event_name` | Low | Safe | Appears after first consume failure callback | Both | ready |
| `event_consume_skipped_total` | metric | Platform API eventing observability sink | 4 | `service`, `environment`, `event_name`, `result` | Low; `result` is limited to skipped/duplicate | Safe | Appears after skipped/duplicate callback | Both | ready |
| `event_consume_duration_seconds` / `_bucket` | metric | Platform API eventing observability sink | 4 | `service`, `environment`, `event_name`, `result`, `le` | Low | Safe | Appears after first consume callback | Both | ready |
| `outbox_pending_count` | metric | Platform API outbox observability reader | 4 | `service`, `environment` | Low | Safe; count only | No payload/event body exposed | MOD-0042 | ready |
| `background_job_started_total` | metric | Platform API job log writer decorator | 5 | `service`, `environment`, `job_name` | Low if job names remain stable | Safe; no job id/correlation id labels | Appears after first job start | Both | ready |
| `background_job_succeeded_total` | metric | Platform API job log writer decorator | 5 | `service`, `environment`, `job_name` | Low | Safe | Appears after first job success | Both | ready |
| `background_job_failed_total` | metric | Platform API job log writer decorator | 5 | `service`, `environment`, `job_name` | Low | Safe; exception text excluded from labels | Appears after first job failure | Both | ready |
| `background_job_retried_total` | metric | Platform API job log writer decorator | 5 | `service`, `environment`, `job_name` | Low | Safe | Uses existing retry count metadata from public log seam | Both | ready |
| `background_job_duration_seconds` / `_bucket` | metric | Platform API job log writer decorator | 5 | `service`, `environment`, `job_name`, `result`, `le` | Low | Safe | Appears after first completed job attempt | Both | ready |
| Structured HTTP request log | log | Gateway, Platform API, AuthService | 1-3 | `ServiceName`, `Environment`, `CorrelationId`, `RequestPath`, `StatusCode`, `TraceId`, elapsed/duration | Low to medium if `RequestPath` contains raw IDs | Redacted by structured logging and sensitive-data enricher | Query strings and bodies must remain excluded | Both | ready |
| Gateway correlation propagation log/signal | log / trace | Gateway -> downstream services | 3 | `CorrelationId`, `TraceId`, request metadata | Low | Safe | Proves cross-service correlation when downstream is reachable | Both | ready |
| Event publish log | log | Platform API | 4 | `EventName`, `EventVersion`, `Operation`, `Result`, `DurationMs`, `CorrelationId`, sanitized `ErrorType` | Low | Safe; payload excluded | Correlation id is log/trace only, not metric label | Both | ready |
| Event consume log | log | Platform API | 4 | `EventName`, `EventVersion`, `ConsumerName`, `Operation`, `Result`, `DurationMs`, `CorrelationId` | Low | Safe; payload excluded | Consumer names should remain stable | Both | ready |
| Background job execution log | log | Platform API | 5 | `JobName`, `JobId`, `Operation`, `Result`, `DurationMs`, `RetryCount`, `CorrelationId`, sanitized `ErrorType` | Medium if job id is used broadly; log only, never metric label | Safe; payload/arguments excluded | Job id is allowed only as operational metadata in logs | Both | ready |
| OpenTelemetry resource metadata | trace | Gateway, Platform API, AuthService | 1-3 | `service.name`, `deployment.environment` | Low | Safe | Export requires configured collector | MOD-0265 | ready |
| ASP.NET Core trace instrumentation | trace | Gateway, Platform API, AuthService | 1-3 | trace id, span id, route/request metadata | Medium if route attributes are raw paths | Safe when bodies/headers are excluded | OTLP exporter may be disabled locally | MOD-0265 | ready |
| HttpClient trace instrumentation | trace | Gateway, Platform API, AuthService | 1-3 | outbound HTTP metadata, trace context | Medium by URL cardinality | Must not include Authorization/Cookie headers | Export requires configured collector | MOD-0265 | ready |
| `correlation.id` trace/log linkage | trace / log | Gateway, Platform API, AuthService, Eventing, Background Jobs | 1-5 | `CorrelationId` / `correlation.id` | High if used as metric label; acceptable as trace/log field | Safe as correlation metadata | Never use as metric label | Both | ready |

## Structured Log Field Inventory

Safe fields for logs and SIEM normalization:

- `ServiceName`
- `Environment`
- `CorrelationId`
- `TraceId`
- `SpanId` where available
- `RequestPath` without sensitive query-string values
- `StatusCode`
- `EventName`
- `EventVersion`
- `ConsumerName`
- `JobName`
- `JobId` for operational logs only
- `Operation`
- `Result`
- `DurationMs` / elapsed duration
- `RetryCount`
- Sanitized `ErrorType` or bounded error reason

Forbidden log content:

- request body
- response body
- serialized entity
- event payload
- job payload or serialized job arguments
- `Authorization` header
- `Cookie` header
- JWT
- bearer token
- refresh token
- API key
- password
- OTP
- connection string
- full email address
- phone number
- patient/person data
- raw exception text as a metric label

## Trace Readiness

Trace readiness is provider-neutral:

- OpenTelemetry resource metadata is configured with `service.name` and `deployment.environment`.
- ASP.NET Core instrumentation is enabled where tracing is enabled.
- HttpClient instrumentation is enabled where tracing is enabled.
- `TraceId` is written into request logs.
- Correlation id links Gateway, Platform API, AuthService, Event Bus, and Background Job observability paths.
- OTLP exporter is configuration-driven and may be disabled when no local collector exists.

MOD-0265 owns collector/provider selection and any Datadog, New Relic, Splunk, ELK, Loki, Tempo, or other vendor adapter work.

## MOD-0042 Alerting Handoff Candidates

These are future candidates only. No alert rules or thresholds are implemented here.

| Candidate | Source signal | Why it matters | Required labels / fields | Data sensitivity | Readiness | Owner |
|---|---|---|---|---|---|---|
| High HTTP 5xx rate | `http_requests_received_total{code=~"5.."}` | Indicates service or dependency failure | `job`, `code`, `endpoint` | Low | ready | MOD-0042 |
| High request latency p95/p99 | `http_request_duration_seconds_bucket` | Indicates degraded UX or dependency slowness | `job`, `endpoint`, `le` | Low | ready | MOD-0042 |
| Service scrape down | `up` | Indicates Prometheus cannot reach a service | `job`, `instance` | Low | ready when Prometheus exists | MOD-0042 |
| Readiness unhealthy | `/health/ready` | Indicates service cannot safely receive traffic | check name/status/duration | Low | ready | MOD-0042 |
| MongoDB readiness unhealthy | `/health/ready` check `mongodb` | Core persistence unavailable | status/duration | Low | ready | MOD-0042 |
| RabbitMQ readiness unhealthy | `/health/ready` check `rabbitmq` | Event broker unavailable | status/duration | Low | ready when RabbitMQ transport enabled | MOD-0042 |
| Hangfire storage readiness unhealthy | `/health/ready` check `hangfire_storage` | Scheduler storage unavailable | status/duration | Low | ready when scheduler enabled | MOD-0042 |
| Event publish failures | `event_publish_failed_total` | Events may not leave service boundary | `service`, `environment`, `event_name` | Low | ready after first event path | MOD-0042 |
| Event consume failures | `event_consume_failed_total` | Consumers may be failing or rejecting events | `service`, `environment`, `event_name` | Low | ready after first consume path | MOD-0042 |
| Outbox backlog increasing | `outbox_pending_count` | Event delivery may be stalled | `service`, `environment` | Low | ready | MOD-0042 |
| Background job failures | `background_job_failed_total` | Scheduled work may be failing | `service`, `environment`, `job_name` | Low | ready after first job path | MOD-0042 |
| Background job retries increasing | `background_job_retried_total` | Work is unstable even if eventually successful | `service`, `environment`, `job_name` | Low | ready after retry metadata path | MOD-0042 |

Any future threshold values must be chosen in MOD-0042 using production baselines and environment-specific SLOs.

## MOD-0265 SIEM / APM Handoff Seams

| Seam | Data source | Provider mapping notes | Redaction requirements | Owner |
|---|---|---|---|---|
| Structured logs | Serilog console JSON and optional Seq sink | Map `ServiceName`, `Environment`, `CorrelationId`, `TraceId`, operation/result fields to provider attributes | Sensitive-data enricher remains mandatory; no bodies/tokens/secrets | MOD-0265 |
| Metrics | Prometheus `/metrics` scrape | Map Prometheus metric names and low-cardinality labels into provider metric model | Do not add tenant/user/event id/correlation id labels | MOD-0265 |
| Traces | OpenTelemetry OTLP exporter | Map `service.name`, `deployment.environment`, trace/span ids, and correlation attributes | Do not export sensitive headers/body attributes | MOD-0265 |
| Health probes | `/health/live`, `/health/ready`, `/health` | Map sanitized check status to uptime/synthetic monitor model | No connection strings or credentials in health output | MOD-0265 |
| Correlation linkage | `X-Correlation-Id`, logs, traces, event/job metadata | Use as cross-signal join key in logs/traces only | Never use as metric label | MOD-0265 |
| Grafana dashboard baseline | `observability/grafana/dashboards/mod-0041-platform-observability.json` | Provider-neutral visual reference for panels and queries | No credentials or private URLs | MOD-0265 |

## Redaction and Cardinality Rules

Allowed low-cardinality metric labels:

- `service`
- `environment`
- `job`
- `method`
- `code`
- route-template-like `endpoint`
- `event_name`
- `job_name`
- bounded `result`
- histogram `le`

Forbidden metric labels:

- tenant id
- user id
- event id
- raw job id
- correlation id
- exception text
- payload value
- request body field
- response body field
- email
- phone
- token/API key/secret value
- raw URL containing IDs or query strings

Safe operational fields may be logged or traced only after redaction and bounded formatting. Secret and PII masking must remain enabled for local and production configurations. Production credentials, provider API keys, Seq API keys, OTLP credentials, RabbitMQ passwords, MongoDB connection strings, and dashboard credentials must be supplied through approved configuration/secret channels and never committed.

## Deferred / Partial Signals

| Signal | Status | Reason |
|---|---|---|
| Dedicated RabbitMQ Prometheus readiness metric | deferred | Current readiness is exposed through `/health/ready`; no dedicated metric family exists. |
| Dedicated Hangfire storage Prometheus readiness metric | deferred | Current readiness is exposed through `/health/ready`; no dedicated metric family exists. |
| Prometheus/Grafana provisioning YAML | deferred | No repo-owned local observability provisioning convention exists. |
| Runtime Grafana datasource/dashboard validation | deferred | Batch 6 artifact validates; local Grafana/Prometheus runtime is optional and was not required for Batch 7. |
| OTLP provider export proof | partial | Exporter is configuration-driven and may be disabled without a collector. MOD-0265 owns provider/collector integration. |
| Event/job metric presence on cold startup | partial | Metric families are emitted after first event/job execution path. Tests prove the public hook paths. |

## Boundary Confirmation

Batch 7 is documentation-only. It adds no alert rules, runbooks, notification channels, SIEM/APM adapters, provider SDKs, custom admin UI, runtime code, Event Bus mechanics, Hangfire mechanics, or master-plan updates.
