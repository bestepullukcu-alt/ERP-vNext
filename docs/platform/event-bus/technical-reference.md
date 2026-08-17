# Event Bus / Internal Events Technical Reference

Module: MOD-0035  
Runtime foundation: Platform event contracts, outbox, inbox/idempotency, and transport adapters

## Purpose

The event bus foundation standardizes internal domain events, outbox publication, consumed-event idempotency, and optional RabbitMQ transport integration.

## Current Components

- Event envelope contracts in Platform application contracts.
- Outbox publisher processor and worker.
- In-memory event bus for lightweight local publishing.
- MassTransit/RabbitMQ publisher adapter.
- Consumed event records for idempotency.
- Eventing observability metrics and RabbitMQ readiness health check.

## Event Boundary

Modules own their business events. The event bus foundation owns the transport, envelope, outbox, retry, and observability mechanics.

## Security

Internal event consumers must validate configured internal credentials where endpoints are exposed across services. Event payloads must not contain secrets.

