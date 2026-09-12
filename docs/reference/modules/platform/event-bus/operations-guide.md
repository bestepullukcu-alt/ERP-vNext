# Event Bus / Internal Events Operations Guide

## Startup Checks

1. Confirm Platform API starts with eventing options loaded.
2. Confirm MongoDB is available for outbox and consumed-event records.
3. If RabbitMQ is enabled, confirm RabbitMQ readiness health check passes.
4. Review outbox pending count metrics.

## Failure Handling

- Outbox publication failures should remain queued for retry.
- Consumers must be idempotent through consumed-event tracking.
- Poison-message and DLQ behavior depends on the enabled transport and should be validated per environment.

## Monitoring

Use Platform observability metrics for publish/consume counts, failure counts, and pending outbox volume. Operational dashboards are tracked under MOD-0041 observability docs.

