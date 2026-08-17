# MOD-0041 Grafana Dashboard Baseline

## Scope

This document covers MOD-0041 Batch 6 only. It provides the baseline Grafana dashboard artifact for metrics emitted by Batches 1-5.

No custom Platform Admin UI, alert rules, notification channels, SIEM adapter, APM adapter, or application runtime behavior change is included.

## Pre-Audit Result

No existing repo-owned Grafana, Prometheus, Docker Compose, or deployment provisioning convention was found. The repo has service-level `/metrics` endpoints and generated execution dashboard tooling, but no observability stack folder to extend.

Because there is no provisioning convention, Batch 6 adds:

- Dashboard JSON: `observability/grafana/dashboards/mod-0041-platform-observability.json`
- Manual import and local setup guidance in this document

Provisioning YAML is deferred until a repo-owned local observability stack convention exists.

## Expected Metric Sources

Prometheus should scrape the trusted service-local `/metrics` endpoints:

| Component | Default local URL | Notes |
|---|---:|---|
| Gateway | `http://host.docker.internal:5000/metrics` | Batch 3 metrics/correlation surface |
| AuthService | `http://host.docker.internal:5056/metrics` | Batch 2 metrics/correlation surface |
| Platform API | `http://host.docker.internal:5057/metrics` | Batch 1, 4, and 5 metrics |

Use `localhost` instead of `host.docker.internal` when Prometheus runs directly on the host.

## Dashboard Import

1. Start Grafana and configure a Prometheus datasource named `Prometheus`.
2. In Grafana, open Dashboards -> New -> Import.
3. Upload `observability/grafana/dashboards/mod-0041-platform-observability.json`.
4. Select the Prometheus datasource when prompted.
5. Set the `job` dashboard variable to the Prometheus scrape jobs you want to view, for example `.*`, `.*Platform.*`, `.*Auth.*`, or `.*Gateway.*`.

## Optional Local Prometheus Scrape Shape

This is a local example only. Do not treat it as production provisioning.

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: gateway
    metrics_path: /metrics
    static_configs:
      - targets: ["host.docker.internal:5000"]

  - job_name: authservice
    metrics_path: /metrics
    static_configs:
      - targets: ["host.docker.internal:5056"]

  - job_name: platform-api
    metrics_path: /metrics
    static_configs:
      - targets: ["host.docker.internal:5057"]
```

## Panel Coverage

The dashboard includes:

- API health and scrape status through Prometheus `up`.
- HTTP request rate using `http_requests_received_total`.
- HTTP 5xx error rate using `http_requests_received_total`.
- HTTP p95/p99 latency using `http_request_duration_seconds_bucket`.
- HTTP status code distribution.
- Gateway request visibility.
- Platform API request visibility.
- AuthService request visibility.
- Event publish started/succeeded/failed counters.
- Event publish duration p95.
- Event consume succeeded/failed/skipped counters.
- Event consume duration p95.
- Outbox pending count.
- RabbitMQ readiness note panel using scrape/blackbox status when configured.
- Background job started/succeeded/failed/retried counters.
- Background job duration p95.
- Hangfire storage readiness note panel using scrape/blackbox status when configured.

## Metric Names Used

HTTP metrics:

- `http_requests_received_total`
- `http_request_duration_seconds_bucket`
- `up`

Event Bus and Outbox metrics:

- `event_publish_started_total`
- `event_publish_succeeded_total`
- `event_publish_failed_total`
- `event_publish_duration_seconds_bucket`
- `event_consume_succeeded_total`
- `event_consume_failed_total`
- `event_consume_skipped_total`
- `event_consume_duration_seconds_bucket`
- `outbox_pending_count`

Background Job metrics:

- `background_job_started_total`
- `background_job_succeeded_total`
- `background_job_failed_total`
- `background_job_retried_total`
- `background_job_duration_seconds_bucket`

## Known Caveats

Event and job counter families are cold-start dependent. They appear after the first publish, consume, or job execution path exercises the Batch 4/5 observability hooks.

RabbitMQ readiness and Hangfire storage readiness are exposed through `/health/ready`; this dashboard does not invent separate readiness metrics. To visualize those checks as first-class Grafana panels, add a Prometheus blackbox exporter or another approved health endpoint scrape convention in a future deployment/provisioning task.

Alert rules are deferred to MOD-0042. SIEM/APM provider export is deferred to MOD-0265.
