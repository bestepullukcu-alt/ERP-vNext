---
id: MOD-0041
name: Logging / Monitoring
title: Central Logging and Monitoring - Full Module Pack with MVP Batches
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: approved
owner: Ops
branch: feature/event-bus
started: 2026-05-15
target: 2026-05-30
completed: false
form_field_count: 0
---

# MOD-0041 - Logging / Monitoring

## 1. Module Summary
- **Purpose:** Define the full central observability scope for ERP-vNext and deliver it through explicit implementation batches.
- **MVP rule:** Only Batch 1 is ready-for-dev candidate now. Later batches are planned/deferred and must not be implemented until their dependency gates are satisfied.
- **Batch 1 proof target:** `Diten.Platform.API` emits structured, correlated logs; logs are visible in Seq; health endpoints work; Prometheus metrics are exposed; OpenTelemetry is configured or explicitly disabled with reason; no secrets/PII leak.
- **Full module target:** Platform API, AuthService, Gateway/Ocelot, Event Bus/RabbitMQ/Outbox, Background Jobs/Hangfire, Prometheus, OpenTelemetry, Seq, Grafana, health standards, correlation propagation, redaction, and handoff signals for alerting/SIEM.
- **Master-plan status:** `Partial` because current runtime has `ILogger` and correlation hooks, but no complete structured logging/monitoring proof.
- **Not a CRUD/DataTable module:** `shell: none`, `golden_reference: none`, `form_field_count: 0`.
- **Draft gate:** This pack is planning-only until status is changed to `approved` or `ready-for-dev`.

## 2. Full MOD-0041 Target Scope
- Platform API observability.
- AuthService observability rollout.
- API Gateway / Ocelot observability.
- Event Bus / RabbitMQ / Outbox observability.
- Background Job / Hangfire observability.
- Prometheus metrics standard.
- OpenTelemetry tracing standard.
- Seq structured logging standard.
- Grafana baseline dashboards.
- Health/readiness/liveness standard.
- Correlation propagation standard.
- Redaction / PII / secret masking standard.
- Alerting handoff to MOD-0042.
- SIEM/APM provider handoff to MOD-0265.

## 3. Ownership and Boundaries
### In-scope across the full module
- Serilog structured logging conventions.
- Seq sink/configuration standard.
- `X-Correlation-Id` generation and propagation.
- OpenTelemetry resource metadata and ASP.NET Core tracing baseline.
- Prometheus scrape-compatible metrics.
- Health endpoints:
  - `/health`
  - `/health/live`
  - `/health/ready`
- MongoDB readiness health check for services that depend on MongoDB.
- Gateway correlation propagation once Batch 3 is approved.
- Event Bus metrics/correlation through MOD-0035 public hooks once Batch 4 is approved.
- Hangfire/job metrics/correlation through MOD-0026 public hooks once Batch 5 is approved.
- Grafana dashboard baseline documentation/provisioning when repo deployment convention exists.
- Redaction rules for logs, traces, metrics, health output, and operational reports.

### Out-of-scope for this module
- Alert rules, escalation channels, deduplication, and incident runbooks. These belong to MOD-0042.
- SIEM/APM vendor adapters such as Datadog, New Relic, Splunk, or ELK. These belong to MOD-0265.
- Custom Platform Admin monitoring UI.
- Frontend changes unless a separate future pack is approved.
- Business audit trail implementation. This belongs to MOD-0021.
- Event Bus mechanics changes. These belong to MOD-0035.
- Hangfire scheduler mechanics changes. These belong to MOD-0026.
- Storing telemetry as MongoDB business entities.
- Master-plan updates as part of module-pack preparation.

### Ownership rule
- MOD-0041 owns observability conventions and exposure.
- MOD-0026 owns scheduler behavior and Hangfire Dashboard protection.
- MOD-0035 owns Event Bus behavior, RabbitMQ publish/consume mechanics, outbox/inbox, and event contracts.
- MOD-0042 owns alerting and incident runbooks.
- MOD-0265 owns SIEM/APM provider adapters.
- No batch may change another module's internals to work around missing public observability hooks.

## 4. Implementation Batch Plan
| Batch | Name | Status | Implementation gate |
|---|---|---|---|
| Batch 1 | Platform API Observability Baseline | ready-for-dev candidate | May start after this pack is approved/ready-for-dev. |
| Batch 2 | AuthService Observability Rollout | planned | Starts after Batch 1 PASS or accepted PARTIAL. |
| Batch 3 | Gateway / Ocelot Observability | planned | Starts after Batch 1 PASS and stable correlation standard. |
| Batch 4 | Event Bus / RabbitMQ / Outbox Observability | planned | Starts after MOD-0035 public hooks exist or missing hooks are accepted as blockers. |
| Batch 5 | Background Job / Hangfire Observability | planned | Starts after MOD-0026 public hooks exist or missing hooks are accepted as blockers. |
| Batch 6 | Grafana Dashboard Baseline | planned | Starts after metrics are emitted by at least Platform API. |
| Batch 7 | Alerting / SIEM Handoff Readiness | planned | Starts after reliable logs/metrics/traces exist. |

## 5. Batch Dependency Rules
- Batch 2 starts only after Batch 1 PASS or accepted PARTIAL.
- Batch 3 starts only after Batch 1 PASS and the correlation standard is stable.
- Batch 4 starts only after MOD-0035 public observability hooks exist or missing hooks are accepted as blockers.
- Batch 5 starts only after MOD-0026 public observability hooks exist or missing hooks are accepted as blockers.
- Batch 6 starts only after metrics are emitted by at least Platform API.
- Batch 7 starts only after reliable logs/metrics/traces exist.
- Later batches must not broaden Batch 1 during the first implementation slice.
- If a required MOD-0035 or MOD-0026 public observability hook does not exist, the related batch must report PARTIAL/BLOCKED with the exact missing hook contract proposal. Do not silently skip the metric and do not modify module internals.

## 6. Owned Objects
### Shared observability contracts/configuration
- `ObservabilityOptions`
- `SeqOptions`
- `TracingOptions`
- `MetricsOptions`
- `HealthCheckOptions`
- `CorrelationOptions`
- `SensitiveDataRedactionOptions`
- Shared registration extension such as `AddDitenObservability(...)`.
- Shared middleware extension such as `UseDitenObservability(...)`.

### Correlation objects
- `ICorrelationContext`
- `CorrelationContext`
- `CorrelationIdMiddleware`
- `CorrelationIdLogEnricher`

### Platform API objects for Batch 1
- Serilog host/bootstrap configuration.
- Seq sink registration.
- Correlation id middleware/enrichment.
- Sanitized health endpoint response writer.
- Prometheus endpoint mapping.
- OpenTelemetry resource and instrumentation setup.

### Later batch objects
- AuthService observability wiring.
- Gateway request/correlation logging.
- Event Bus publish/consume/outbox metrics through public MOD-0035 hooks.
- Hangfire job metrics through public MOD-0026 hooks.
- Grafana baseline dashboard/provisioning files only if repo convention exists.

### Persistence records
- No new MongoDB business entity is owned by this module.
- `entity_base: BaseEntity` is retained only as the Platform default if a future Platform-owned status/configuration record is approved.

## 7. Telemetry Field Contracts
No CRUD entity is owned by MOD-0041. The following are telemetry contracts, not business entities.

### Log event fields
| Field | Type | Required | Rule |
|---|---|---|---|
| TimestampUtc | `DateTimeOffset` | Yes | UTC timestamp emitted by Serilog. |
| Level | `string` | Yes | Serilog level. |
| MessageTemplate | `string` | Yes | Structured template; no payload/body/entity dumps. |
| ServiceName | `string` | Yes | Stable service name, e.g. `Diten.Platform`. |
| Environment | `string` | Yes | Deployment environment. |
| CorrelationId | `string` | Yes | Generated when missing; propagated to response/logs/traces. |
| TraceId | `string?` | Trace enabled | OpenTelemetry trace id. |
| SpanId | `string?` | Trace enabled | OpenTelemetry span id. |
| RequestPath | `string?` | HTTP only | Path without sensitive query-string values. |
| StatusCode | `int?` | HTTP only | HTTP response status. |
| Exception | `object?` | Error only | Redacted and truncated. |

### Metric label fields
| Field | Type | Required | Rule |
|---|---|---|---|
| service | `string` | Yes | Low-cardinality service name. |
| environment | `string` | Yes | Low-cardinality environment name. |
| method | `string?` | HTTP metrics | HTTP method. |
| endpoint | `string?` | HTTP metrics | Route template preferred; no raw ids. |
| status_code | `string?` | HTTP metrics | Status code/class. |
| job_name | `string?` | Batch 5 | Stable Hangfire/logical job name only. |
| event_name | `string?` | Batch 4 | Stable MOD-0035 event name only. |

### Trace attributes
| Field | Type | Required | Rule |
|---|---|---|---|
| service.name | `string` | Yes | Service name. |
| deployment.environment | `string` | Yes | Matches log environment. |
| correlation.id | `string` | Yes | Mirrors correlation context. |
| trace.id | `string` | Yes when tracing enabled | Exported trace id. |

## 8. Repo Scope
### Batch 1 allowed scope
- `execution/domains/platform-shared-services/module-packs/MOD-0041-logging-monitoring.md`
- `services/Diten.Platform/src/Diten.Platform.API/**`
- `services/Diten.Platform.Common/**` or the existing shared/common location for service-neutral observability helpers.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` only if needed for health check/exporter setup.

### Later batch scope by approval only
- `services/Diten.AuthService/**` for Batch 2.
- `gateway/Diten.ApiGateway/**` for Batch 3, excluding protected `ocelot.json` unless explicitly approved.
- MOD-0035 public hook integration for Batch 4.
- MOD-0026 public hook integration for Batch 5.
- Existing deployment/provisioning files for Batch 6 when repo convention exists.

### Always out of scope unless separately approved
- `frontend/Diten.Web/**`
- ERP/tenant service folders.
- `docs/platform/master-plan.md`
- MOD-0026 internals.
- MOD-0035 internals.

## 9. Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless a separate integration-agent task is approved.
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- `services/Diten.AuthService/**` during Batch 1.
- MOD-0026 implementation internals.
- MOD-0035 implementation internals.
- Any custom monitoring UI in `frontend/Diten.Web`.
- Any runtime code outside the currently approved batch scope.

## 10. Dependencies
- **NEW-001 Secrets Management:** Seq URL/API key, OTLP endpoint, exporter credentials, and dashboard credentials must not be hardcoded. Until NEW-001 is complete, appsettings + environment variables are the temporary path.
- **MongoDB:** Required readiness dependency for Platform API and other Mongo-backed services.
- **Seq:** Structured log visibility.
- **Prometheus:** Metrics scrape target.
- **Grafana:** Dashboard consumer of Prometheus metrics.
- **OpenTelemetry:** Trace/resource metadata and optional export.
- **MOD-0026 Background Job Scheduler:** Batch 5 depends on public job metadata/observability hooks or accepted blocker.
- **MOD-0035 Event Bus / Internal Events:** Batch 4 depends on public event/outbox observability hooks or accepted blocker.
- **MOD-0042 Alerting / Incident Runbooks:** Consumes MOD-0041 signals after Batch 7 handoff readiness.
- **MOD-0265 SIEM / Observability Provider:** Consumes MOD-0041 export seams after Batch 7 handoff readiness.

## 11. Runtime Constraints
- Batch 1 runtime target is Platform API only.
- Serilog is the structured logging pipeline.
- Logs must be structured and JSON-compatible.
- Seq sink is configuration-driven and may be disabled only by explicit configuration.
- If `Seq.Enabled=true` and Seq URL is missing, implementation must fail fast or disable Seq only when an explicit safe-disable configuration exists.
- Correlation id header is `X-Correlation-Id`.
- Missing `X-Correlation-Id` must generate a new correlation id.
- Valid inbound `X-Correlation-Id` must be preserved.
- Malformed or oversized `X-Correlation-Id` must be replaced or rejected safely.
- Response must include `X-Correlation-Id`.
- `/health/live` must not depend on MongoDB, Seq, Prometheus, OTLP, RabbitMQ, or Hangfire.
- `/health/ready` must include the service's required readiness dependencies.
- `/health` must return sanitized health information.
- `/metrics` must return Prometheus scrape-compatible output when metrics are enabled.
- OpenTelemetry must include service resource metadata even if exporter is disabled.
- OpenTelemetry exporter must be configuration-driven; if disabled, the reason must be reported.
- Prometheus metric labels must remain low-cardinality.
- No secrets/PII/payloads may appear in logs, traces, metrics, or health responses.
- Event Bus and Hangfire internals must not be changed to force observability hooks.

## 12. Layout & Shell Contract
- `shell: none`.
- No Razor layout is required.
- No `Diten.Web` view is created.
- No DataTable or Platform Admin CRUD screen is created.
- No custom monitoring page is created.
- Operational surfaces are external/local tools: Seq, Prometheus, Grafana, Hangfire Dashboard, and RabbitMQ Management Plugin.

## 13. Backend File Convention
This is a backend/infrastructure module, not a CRUD/DataTable module. `golden_reference: none` is intentional.

### Expected Batch 1 implementation shape
- `Diten.Platform.API`:
  - Serilog bootstrap.
  - Seq sink configuration.
  - Observability DI registration.
  - Correlation middleware registration.
  - Health endpoint mapping.
  - Prometheus endpoint mapping.
  - OpenTelemetry resource/instrumentation/exporter setup.
- `Diten.Platform.Common` or existing shared/common location:
  - Options.
  - Correlation context.
  - Redaction helpers.
  - Log enrichers.
  - Service registration extensions.
- `Diten.Platform.Infrastructure` only if needed:
  - MongoDB health check registration.
  - Exporter or sink setup that belongs outside API.

### Concrete package expectations
Final package choice must follow existing repo conventions. Candidate packages:
- `Serilog.AspNetCore`
- `Serilog.Sinks.Seq`
- `Serilog.Enrichers.Environment`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `prometheus-net.AspNetCore` or repo-approved equivalent.
- `AspNetCore.HealthChecks.MongoDb` or repo-approved equivalent.

### Forbidden implementation shape
- No `Features/LoggingMonitoring/Commands` CRUD surface.
- No Platform Admin MVC controller/view.
- No public telemetry ingestion endpoint.
- No direct RabbitMQ/MassTransit calls.
- No Event Bus internal changes.
- No Hangfire scheduler internal changes.
- No new background job scheduler abstraction.
- No telemetry-as-business-entity persistence.

## 14. Frontend File Contract
- No frontend files are in scope.
- No DataTable v2 contract applies.
- No `Index.cshtml`, partials, or JS assets are created.
- No Platform Admin menu entry or ops link is added by this module.

## 15. Validation Rules
| Field | Required | Format/Rule | Runtime-level | Pre-check |
|---|---|---|---|---|
| X-Correlation-Id | No inbound, Yes outbound | Non-empty safe string; max length implementation-defined | Request, response, log scope, trace attribute | Validate length and characters; replace or reject unsafe values. |
| ServiceName | Yes | Stable service name | Log, metric, trace resource | Must not be empty. |
| Environment | Yes | Stable environment name | Log, metric, trace resource | Must not contain secrets or machine-specific noise. |
| Seq.Enabled | Yes | Boolean | Serilog sink | If true, Seq URL required unless explicit safe-disable exists. |
| Seq.Url | Conditional | Absolute HTTP/HTTPS URL | Serilog sink | Required when Seq enabled. |
| Seq.ApiKey | No | Secret from config/env | Serilog sink | Never logged. |
| Otlp.Enabled | Yes | Boolean | OpenTelemetry exporter | If false, disabled reason must be reported. |
| Otlp.Endpoint | Conditional | Absolute HTTP/gRPC endpoint | OpenTelemetry exporter | Required when OTLP enabled. |
| HealthLivePath | Yes | `/health/live` | ASP.NET endpoint | Must not check external dependencies. |
| HealthReadyPath | Yes | `/health/ready` | ASP.NET endpoint | Must include required dependencies. |
| HealthPath | Yes | `/health` | ASP.NET endpoint | Sanitized output only. |
| MetricsPath | Yes | `/metrics` | ASP.NET endpoint | Prometheus scrape-compatible. |
| MongoConnection | Service-dependent | Config/env only | Readiness health check | Redact from output and logs. |
| MetricLabels | Yes | Low-cardinality only | Prometheus | No raw ids, user ids, tenant ids, exception text, payload fields. |
| LogException | Error only | Redacted and truncated | Serilog | No secret/PII/payload leakage. |

## 16. PII / Secret Redaction Rules
- Do not log full email addresses.
- Do not log phone numbers.
- Do not log passwords.
- Do not log tokens.
- Do not log API keys.
- Do not log connection strings.
- Do not log JWTs.
- Do not log request bodies.
- Do not log response bodies.
- Do not log serialized entities.
- Do not log patient/person data.
- Do not log arbitrary payloads.
- Prefer stable operation metadata and low-risk IDs only where necessary.
- Never use raw user ids, tenant ids, entity ids, email, phone, exception text, or payload values as metric labels.

## 17. Batch 1 - Platform API Observability Baseline
### Status
- `ready-for-dev candidate`

### Objective
Prove the observability baseline in `Diten.Platform.API` without touching AuthService, Gateway, frontend, Event Bus internals, or Hangfire internals.

### Scope
- `Diten.Platform.API` only.
- Serilog + Seq.
- `X-Correlation-Id`.
- `/health`, `/health/live`, `/health/ready`.
- `/metrics`.
- OpenTelemetry resource metadata.
- ASP.NET Core instrumentation.
- MongoDB readiness.
- Redaction proof.

### Out of scope
- AuthService rollout.
- Gateway/Ocelot changes.
- ERP service rollout.
- Event Bus mechanics changes.
- Hangfire scheduler mechanics changes.
- Custom admin UI.
- Alerting/SIEM.

### Dependencies
- MongoDB readiness configuration.
- Seq local/runtime configuration.
- Prometheus scrape support.
- OpenTelemetry packages/configuration.
- NEW-001 future secrets provider; appsettings + environment variables are temporary.

### Golden flow
Platform API starts with observability enabled.

1. Request arrives without `X-Correlation-Id`.
2. Middleware generates a new `CorrelationId`.
3. Response includes `X-Correlation-Id`.
4. Structured request log appears in Seq.
5. Seq log includes `ServiceName`, `Environment`, `CorrelationId`, request metadata, and trace id when tracing is enabled.
6. `/health/live` returns healthy without external dependency checks.
7. `/health/ready` includes MongoDB readiness.
8. `/health` returns sanitized aggregate health output.
9. `/metrics` returns Prometheus scrape-compatible metrics.
10. OpenTelemetry trace contains `service.name`, environment, trace id, and correlation id, or exporter is explicitly disabled with documented reason.
11. No secret/PII appears in logs, metrics, traces, or health output.

### Failure path
- **Missing Seq URL while `Seq.Enabled=true`**
  - Expected: fail fast with clear startup error, or disable Seq only if explicit safe-disable configuration is present and documented.
- **Malformed or oversized `X-Correlation-Id`**
  - Expected: replaced with a safe generated id or rejected safely; no log injection.
- **MongoDB unavailable**
  - Expected: `/health/live` remains healthy; `/health/ready` becomes unhealthy.
- **Missing `/metrics` endpoint**
  - Expected: result is PARTIAL or FAIL, never PASS.
- **Secret/PII leakage**
  - Expected: result is FAIL.
- **OpenTelemetry exporter disabled**
  - Expected: result may be PARTIAL when service resource metadata exists and disabled reason is documented.
- **Application cannot start**
  - Expected: FAIL unless the only blocker is a package/runtime incompatibility requiring user decision, then BLOCKED.

### Acceptance criteria
- [ ] Platform API uses Serilog as the structured logging pipeline.
- [ ] Platform logs are JSON-compatible.
- [ ] Platform request logs include `ServiceName`, `Environment`, `CorrelationId`, request path, status code, and trace id when tracing is enabled.
- [ ] Seq sink is configuration/env driven.
- [ ] Missing Seq URL while `Seq.Enabled=true` fails fast or safe-disables only by explicit configuration.
- [ ] Runtime smoke proves a structured Platform API request log appears in Seq.
- [ ] `X-Correlation-Id` is generated when inbound header is missing.
- [ ] Valid inbound `X-Correlation-Id` is preserved.
- [ ] Malformed/oversized `X-Correlation-Id` is replaced or rejected safely.
- [ ] Response includes `X-Correlation-Id`.
- [ ] `/health/live` exists and does not depend on external systems.
- [ ] `/health/ready` exists and includes MongoDB readiness.
- [ ] `/health` exists and returns sanitized output.
- [ ] MongoDB unavailable keeps `/health/live` healthy and marks `/health/ready` unhealthy.
- [ ] `/metrics` exists and returns Prometheus scrape-compatible output.
- [ ] Prometheus metrics use low-cardinality labels only.
- [ ] OpenTelemetry service resource metadata includes service name and environment.
- [ ] ASP.NET Core instrumentation is enabled when tracing is enabled.
- [ ] OTLP exporter is config-driven or explicitly disabled with documented reason.
- [ ] Grafana local usage/provisioning expectation is documented.
- [ ] No full email, phone, password, token, API key, connection string, JWT, request/response body, serialized entity, patient/person data, or arbitrary payload appears in logs/traces/metrics/health output.
- [ ] No AuthService files are touched.
- [ ] No Gateway files are touched.
- [ ] No frontend files are touched.
- [ ] No MOD-0026 internals are touched.
- [ ] No MOD-0035 internals are touched.
- [ ] Platform API build passes.

### Validation commands/proof
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj -c Debug` if shared/common observability code is added there.
- Run relevant Platform/API/Common tests if present.
- Start Platform API.
- `curl http://localhost:5057/health/live`
- `curl http://localhost:5057/health/ready`
- `curl http://localhost:5057/health`
- `curl http://localhost:5057/metrics`
- Send a request without `X-Correlation-Id` and verify response includes `X-Correlation-Id`.
- Verify Seq contains structured log with `CorrelationId`.
- Verify no secret/PII appears in log, health, metrics, or trace output.

### PASS / PARTIAL / FAIL rules
- **PASS:** Platform API starts; correlation id works; Seq log proof exists; health endpoints work; `/metrics` works; redaction proof passes; OpenTelemetry resource metadata exists.
- **PARTIAL:** Core Platform API observability works, but OpenTelemetry exporter or Grafana local provisioning is disabled due to missing local infrastructure and documented.
- **FAIL:** App cannot start; health/metrics endpoints are missing; correlation id is absent; Seq proof is missing while enabled; secrets/PII leak.
- **BLOCKED:** Package/runtime incompatibility prevents implementation and requires user decision.

## 18. Batch 2 - AuthService Observability Rollout
### Status
- `planned`

### Objective
Apply the Batch 1 logging/correlation/health/metrics/tracing standard to `Diten.AuthService` without redesigning authentication or authorization behavior.

### Scope
- `Diten.AuthService` API observability.
- Serilog + Seq with shared convention.
- `X-Correlation-Id` generation/preservation.
- Health endpoints matching the standard.
- Prometheus metrics.
- OpenTelemetry resource metadata.
- Sanitized auth/login logs.
- Prove login/auth request correlation and sanitized logs.

### Out of scope
- Auth flow redesign.
- Permission model changes.
- JWT claim changes unless required only for safe observability context.
- Gateway changes.
- Frontend changes.

### Dependencies
- Batch 1 PASS or accepted PARTIAL.
- Shared/common observability helpers stable enough to reuse.
- Existing AuthService startup/configuration patterns.

### Golden flow
AuthService starts with observability enabled -> login/auth request arrives with or without `X-Correlation-Id` -> correlation id is preserved/generated -> response/logs contain correlation id -> Seq contains sanitized auth log -> health and metrics endpoints work -> no password/JWT/token/email leakage.

### Failure path
- Missing correlation id in AuthService logs: FAIL.
- Full email/password/JWT/token logged: FAIL.
- Auth flow behavior changes due to observability: FAIL.
- Health/metrics endpoint missing: FAIL or PARTIAL according to accepted scope.

### Acceptance criteria
- [ ] AuthService uses the shared observability standard.
- [ ] Login/auth request logs are structured and sanitized.
- [ ] Correlation id survives AuthService request handling.
- [ ] No password, JWT, token, API key, or full email is logged.
- [ ] Health and metrics endpoints follow the standard.
- [ ] Auth behavior remains unchanged.

### Validation commands/proof
- `dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug`
- Run AuthService tests if present.
- Start AuthService.
- Call health/metrics endpoints.
- Send auth/login request and verify correlation id + sanitized Seq log.

### PASS / PARTIAL / FAIL rules
- **PASS:** AuthService observability matches Batch 1 standard with sanitized auth proof.
- **PARTIAL:** Core logging/correlation works but metrics/tracing exporter is deferred with reason.
- **FAIL:** Auth behavior changes, health/metrics missing without accepted deferral, or sensitive auth data leaks.

## 19. Batch 3 - Gateway / Ocelot Observability
### Status
- `planned`

### Objective
Add Gateway-level request logging and correlation propagation without changing routes unless explicitly approved.

### Scope
- Gateway request logging.
- Gateway health/metrics decision.
- Upstream/downstream correlation propagation.
- Prove correlation id survives Gateway -> Platform API/AuthService.
- No `ocelot.json` change unless explicitly approved.

### Out of scope
- Route restructuring.
- API Gateway hardening beyond observability.
- Quota/rate-limit implementation.
- Frontend changes.

### Dependencies
- Batch 1 PASS.
- Stable correlation standard.
- Batch 2 recommended before full AuthService proof through Gateway.
- MOD-0032 remains owner for broader gateway hardening.

### Golden flow
Request enters Gateway without `X-Correlation-Id` -> Gateway generates or preserves correlation id -> downstream Platform API/AuthService receives the same id -> downstream response/logs include same id -> Gateway log and downstream log can be correlated in Seq.

### Failure path
- Correlation id changes unexpectedly between Gateway and downstream: FAIL.
- `ocelot.json` changed without explicit approval: FAIL.
- Gateway observability breaks routing: FAIL.
- Gateway health/metrics decision unclear: PARTIAL.

### Acceptance criteria
- [ ] Gateway logs request metadata with correlation id.
- [ ] Gateway propagates `X-Correlation-Id` downstream.
- [ ] Gateway does not expose sensitive headers/tokens in logs.
- [ ] No `ocelot.json` change unless explicitly approved.
- [ ] Gateway -> Platform API correlation proof exists.
- [ ] Gateway -> AuthService correlation proof exists if Batch 2 is complete.

### Validation commands/proof
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`
- Start Gateway and downstream target service.
- Send request through Gateway without `X-Correlation-Id`.
- Verify Gateway response/log/downstream log share correlation id.

### PASS / PARTIAL / FAIL rules
- **PASS:** Gateway and downstream logs correlate with same id and routing remains intact.
- **PARTIAL:** Gateway logging works but health/metrics endpoint is explicitly deferred.
- **FAIL:** Routing breaks, correlation is lost, secrets leak, or protected route config changes without approval.

## 20. Batch 4 - Event Bus / RabbitMQ / Outbox Observability
### Status
- `planned`

### Objective
Expose eventing and outbox observability through MOD-0035 public hooks without changing Event Bus mechanics.

### Scope
- Event publish success/failure metrics.
- Event consume success/failure metrics.
- Outbox pending count.
- Outbox publish duration.
- RabbitMQ connection/readiness signal.
- Correlation id propagation in event metadata.
- Use only MOD-0035 public hooks.
- Do not change Event Bus mechanics.
- If Event Bus public hooks are unavailable, provide a proposed public hook contract such as:
  - `OnEventPublishStarted`
  - `OnEventPublishSucceeded`
  - `OnEventPublishFailed`
  - `OnEventConsumed`
  - `GetOutboxPendingCount`
- Do not implement these hooks inside MOD-0041.

### Out of scope
- Event contract redesign.
- Outbox/inbox behavior changes.
- RabbitMQ publish/consume mechanics changes.
- DLQ/replay UI.
- Public event publish endpoints.

### Dependencies
- MOD-0035 public observability hooks exist, or missing hooks are accepted as blockers.
- Batch 1 correlation standard.
- RabbitMQ configuration where broker-backed proof is expected.

### Golden flow
Application publishes event through MOD-0035 public abstraction -> correlation id is present in event metadata -> outbox pending/published metrics update -> publish duration metric records -> consumer success/failure metric records -> RabbitMQ readiness signal is visible when broker config is enabled.

### Failure path
- MOD-0035 public hook missing: report blocker, do not modify internals.
- Publish failure metric missing: PARTIAL/FAIL depending on accepted hook state.
- Payload logged: FAIL.
- Correlation id missing from event metadata: FAIL if hook exists.

### Acceptance criteria
- [ ] Event publish success metric exists through public hook.
- [ ] Event publish failure metric exists through public hook.
- [ ] Event consume success/failure metrics exist through public hook.
- [ ] Outbox pending count is visible.
- [ ] Outbox publish duration is visible.
- [ ] RabbitMQ readiness signal is visible when broker config is enabled.
- [ ] Correlation id is preserved in event metadata.
- [ ] No MOD-0035 internals are changed.
- [ ] Event payloads are not logged.

### Validation commands/proof
- Build affected Platform/Eventing projects.
- Run MOD-0035 tests if present.
- Trigger approved eventing smoke flow.
- Verify metrics for publish/consume/outbox.
- Verify correlation id in event metadata/logs.
- Verify no payload/secrets in logs/metrics.

### PASS / PARTIAL / FAIL rules
- **PASS:** Metrics and correlation work through public hooks.
- **PARTIAL:** Public hook missing but exact blocker is reported and no internals changed.
- **FAIL:** Event Bus internals changed, payloads leak, or correlation breaks despite hook availability.

## 21. Batch 5 - Background Job / Hangfire Observability
### Status
- `planned`

### Objective
Expose job execution observability through MOD-0026 public hooks without changing scheduler mechanics.

### Scope
- Job started/succeeded/failed/retried metrics.
- Job duration metrics.
- Correlation id/job id metadata.
- Hangfire storage readiness signal.
- Dashboard remains owned/protected by MOD-0026.
- Use only MOD-0026 public hooks.
- Do not change scheduler mechanics.
- If Background Job public hooks are unavailable, provide a proposed public hook contract such as:
  - `OnJobStarted`
  - `OnJobSucceeded`
  - `OnJobFailed`
  - `OnJobRetried`
  - `GetSchedulerStorageHealth`
- Do not implement these hooks inside MOD-0041.

### Out of scope
- Job scheduling behavior changes.
- Hangfire Dashboard authorization changes.
- New public trigger endpoints.
- Business job implementation.

### Dependencies
- MOD-0026 public observability hooks exist, or missing hooks are accepted as blockers.
- Batch 1 correlation standard.
- Scheduler/Hangfire config available for runtime proof.

### Golden flow
Approved smoke job starts -> job metric records started -> job succeeds or fails -> succeeded/failed/retried metric records -> duration metric records -> log contains correlation/job metadata -> Hangfire storage readiness signal is visible.

### Failure path
- MOD-0026 public hook missing: report blocker, do not modify internals.
- Job failure metric missing despite hook availability: FAIL.
- Dashboard authorization weakened: FAIL.
- Job payload logged: FAIL.

### Acceptance criteria
- [ ] Job started metric exists through public hook.
- [ ] Job succeeded metric exists through public hook.
- [ ] Job failed/retried metric exists through public hook.
- [ ] Job duration metric exists.
- [ ] Correlation id/job id metadata is present.
- [ ] Hangfire storage readiness signal is visible when scheduler config is enabled.
- [ ] Hangfire Dashboard remains MOD-0026-owned and protected.
- [ ] No MOD-0026 internals are changed.
- [ ] Job payloads are not logged.

### Validation commands/proof
- Build affected Platform/Scheduler projects.
- Run MOD-0026 tests if present.
- Trigger approved scheduler smoke flow.
- Verify job metrics/log metadata.
- Verify storage readiness signal.
- Verify dashboard authorization remains unchanged.

### PASS / PARTIAL / FAIL rules
- **PASS:** Job metrics and correlation work through public hooks.
- **PARTIAL:** Public hook missing but exact blocker is reported and no internals changed.
- **FAIL:** Scheduler internals changed, dashboard security weakens, payloads leak, or metrics fail despite hook availability.

## 22. Batch 6 - Grafana Dashboard Baseline
### Status
- `planned`

### Objective
Provide a baseline Grafana dashboard documentation/provisioning layer for metrics already emitted by prior batches.

### Scope
- Baseline dashboard documentation/provisioning if repo convention exists.
- Panels for API health, request rate, error rate, latency, job failures, event failures, and outbox pending count.
- No custom Platform Admin UI.
- If repo provisioning convention exists, Grafana deliverables should use dashboard JSON and datasource/provisioning YAML according to existing repo conventions.
- If no convention exists, document dashboard requirements only.

### Out of scope
- Production Grafana hosting.
- Alert rule implementation.
- Custom frontend dashboard.
- SIEM/APM provider adapter.

### Dependencies
- Metrics emitted by at least Platform API.
- Batch 4/5 metrics for event/job panels, or documented empty/deferred panels.
- Existing repo deployment/provisioning convention, if any.

### Golden flow
Prometheus scrapes metrics -> Grafana data source reads Prometheus -> baseline dashboard shows API health/request/error/latency panels -> event/job panels appear when batches 4/5 are complete or are documented as deferred.

### Failure path
- No repo provisioning convention: document deployment follow-up; PARTIAL allowed if metrics work.
- Dashboard claims unavailable metrics as live: FAIL.
- Custom Platform Admin UI added: FAIL.

### Acceptance criteria
- [ ] Grafana baseline documentation exists.
- [ ] Provisioning files are added only if repo convention exists.
- [ ] API health/request/error/latency panels are defined.
- [ ] Event/job panels are defined only when metrics exist or clearly marked deferred.
- [ ] No custom Platform Admin UI is added.

### Validation commands/proof
- Verify Prometheus can scrape metrics.
- Verify Grafana can read Prometheus data source when local Grafana exists.
- Capture/document dashboard panel availability.

### PASS / PARTIAL / FAIL rules
- **PASS:** Baseline dashboard/provisioning works with available metrics.
- **PARTIAL:** Grafana provisioning absent due to missing repo convention, but dashboard requirements and Prometheus metrics are documented.
- **FAIL:** Dashboard misrepresents unavailable metrics or custom admin UI is added.

## 23. Batch 7 - Alerting / SIEM Handoff Readiness
### Status
- `planned`

### Objective
Document which MOD-0041 logs, metrics, and traces are ready for MOD-0042 alerting and MOD-0265 SIEM/APM provider expansion.

### Scope
- Define logs/metrics/traces ready for MOD-0042 alerting.
- Define export seams ready for MOD-0265 SIEM/APM provider.
- Do not implement alert rules.
- Do not implement SIEM/APM adapter.

### Out of scope
- Alert thresholds.
- Notification channels.
- Runbook pages.
- Datadog/New Relic/Splunk/ELK adapters.

### Dependencies
- Reliable logs/metrics/traces from earlier batches.
- MOD-0042 and MOD-0265 target requirements, when available.

### Golden flow
MOD-0041 lists stable signals -> each signal has name, source, labels/fields, redaction status, and intended consumer -> MOD-0042 can define alert rules without changing observability producers -> MOD-0265 can define provider adapters without changing core signal semantics.

### Failure path
- Alert rules implemented inside MOD-0041: FAIL.
- SIEM provider adapter implemented inside MOD-0041: FAIL.
- Signal list includes unproven or sensitive fields: FAIL.

### Acceptance criteria
- [ ] MOD-0042-ready signals are documented.
- [ ] MOD-0265 export seams are documented.
- [ ] Signal names/labels/fields are stable and redacted.
- [ ] No alert rules are implemented.
- [ ] No SIEM/APM adapter is implemented.

### Validation commands/proof
- Review signal inventory against actual emitted logs/metrics/traces.
- Confirm redaction status.
- Confirm no MOD-0042/MOD-0265 implementation files are created.

### PASS / PARTIAL / FAIL rules
- **PASS:** Handoff inventory is accurate, stable, and redacted.
- **PARTIAL:** Some future signals remain deferred with explicit dependency.
- **FAIL:** Alerting/SIEM implementation leaks into MOD-0041 or signal inventory contains sensitive/unproven fields.

## 24. Full Module Completion Criteria
MOD-0041 is not fully DONE until:
- Platform API observability passes.
- AuthService observability passes or is explicitly deferred.
- Gateway observability passes or is explicitly deferred.
- Event Bus/outbox metrics are visible.
- Hangfire/job metrics are visible.
- Health endpoints are standardized.
- Metrics are scrapeable.
- Logs are queryable.
- Traces are correlated.
- Redaction is proven.
- Grafana baseline exists or is documented as deployment follow-up.
- MOD-0042 handoff signals are documented.
- MOD-0265 handoff seams are documented.
- If Event Bus/Hangfire public observability hooks are unavailable, MOD-0041 remains PARTIAL and cannot be marked fully DONE.
- No custom admin UI was added.
- No protected module internals were changed outside approved public hooks.

## 25. Authorization Convention
- This module does not expose user-facing CRUD APIs.
- `/health/live` may be anonymous for load balancers/orchestrators.
- `/health/ready` may be anonymous inside trusted infrastructure or protected by deployment policy; implementation must document the decision.
- `/health` must be sanitized regardless of auth.
- `/metrics` is intended for Prometheus scrape in a trusted local/deployment network.
- No Gateway exposure is added in Batch 1.
- No frontend permission is required.
- Future diagnostics APIs, if separately approved, must use `[Authorize(Policy = "PlatformActor")]` and `Platform.Observability.*` permissions.

## 26. Gateway / API Routing Decision
- Karar: Gateway değişikliği Batch 1 için **gereksiz**.
- Frontend bu pack kapsamında API çağrısı yapmaz.
- Health and metrics are exposed by service runtime for local/deployment probes.
- `gateway/Diten.ApiGateway/**/ocelot.json` protected path olarak kalır.
- Gateway exposure requires separate user approval and integration-agent work.

## 27. Ready-for-dev Checklist
- [ ] Status remains `draft` until user review.
- [ ] Batch 1 scope is Platform API only.
- [ ] Batch 2-7 are documented as planned/deferred.
- [ ] AuthService rollout is deferred to Batch 2.
- [ ] Gateway/Ocelot changes are deferred to Batch 3.
- [ ] Event Bus observability is deferred to Batch 4 and requires public hooks.
- [ ] Hangfire observability is deferred to Batch 5 and requires public hooks.
- [ ] Grafana baseline is deferred to Batch 6.
- [ ] Alerting/SIEM handoff readiness is deferred to Batch 7.
- [ ] Alerting/runbook scope is owned by MOD-0042.
- [ ] SIEM/APM provider scope is owned by MOD-0265.
- [ ] Custom admin UI is excluded.
- [ ] Package expectations are listed.
- [ ] Batch dependency rules are documented.
- [ ] Batch 1 Golden Flow is documented.
- [ ] Batch 1 Failure Path is documented.
- [ ] PASS/PARTIAL/FAIL/BLOCKED rules are documented.
- [ ] Validation commands are documented.
- [ ] PII/secret redaction rules are explicit.

## 28. Implementation Notes
- This pack defines full MOD-0041 scope but only Batch 1 is ready-for-dev candidate now.
- Later batches must be started as explicit follow-up work after their dependency gates are met.
- Domain-config currently says Observability is `ILogger` + correlation middleware; this pack is the module-level decision for the fuller observability roadmap.
- Grafana is not a Batch 1 PASS blocker if Prometheus metrics work and missing Grafana local provisioning is documented.
- OpenTelemetry exporter may be PARTIAL in Batch 1 if local infrastructure is missing, but service resource metadata must still be configured.
- Runtime proof is required; package installation alone is not completion.
- No code was written as part of module-pack preparation.
- Master-plan must not be updated by this module-pack refinement.

## 29. Follow-up Items
- [ ] After user approval, change status to `approved` or `ready-for-dev` for Batch 1 only.
- [ ] Batch 1 implementation pre-audit: locate existing logging, correlation middleware, health endpoints, and metrics/tracing packages.
- [ ] Decide exact package versions according to existing repo conventions.
- [ ] Decide local Seq/Prometheus/Grafana startup/provisioning approach based on existing repo deployment files.
- [ ] After Batch 1 proof, decide whether Batch 2 AuthService or Batch 3 Gateway comes next.
- [ ] Confirm MOD-0035 public observability hooks before Batch 4.
- [ ] Confirm MOD-0026 public observability hooks before Batch 5.
- [ ] Prepare MOD-0042 Alerting / Incident Runbooks after reliable signals exist.
- [ ] Prepare MOD-0265 SIEM / Observability Provider only if external provider export becomes required.

## Output Contract
Each batch implementation final report must use this format:
- Batch number/name
- Batch status: PASS / PARTIAL / FAIL / BLOCKED
- Chosen package list and versions
- Shared/common location decision
- Changed files
- Seq proof, if applicable
- Correlation id proof
- Health endpoint proof
- Prometheus metrics proof
- OpenTelemetry proof or disabled reason
- Grafana local usage/provisioning note, if applicable
- Event Bus public hook status, if applicable
- Hangfire public hook status, if applicable
- Redaction proof
- Validation commands and results
- Boundary check
- Open blockers / assumptions
- Next recommended step
