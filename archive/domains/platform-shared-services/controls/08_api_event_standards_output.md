# 08_api_event_standards_output.md — API & Event Standards

**Status:** Ready baseline

## API conventions
- Versioning: `/api/v{n}/...`
- Error envelope: `{ code, message, details[], correlation_id }`
- Pagination: `{ items, page, pageSize, total }`
- Auth context: `tenant_id`, `actor_id`, roles/claims, `correlation_id`

## Contract envelope v1 (mandatory)
For APIs/events introduced or updated by Platform modules, use:
- `correlation_id` (required)
- `tenant_id` (required where applicable)
- `actor_id` (required for UI-triggered actions)
- `object_refs[]` (required when applicable)
- `schema_version` (required)
- `occurred_at` (required for events/audit payloads)
- `payload` (required)

## Event envelope v1 (recommended shape)
```json
{
  "event_id": "<uuid>",
  "event_type": "<string>",
  "schema_version": "v1",
  "occurred_at": "<iso>",
  "correlation_id": "<string>",
  "tenant_id": "<string>",
  "actor_id": "<string|null>",
  "object_refs": [{"type":"<string>","id":"<string>"}],
  "payload": {}
}
```

## Current environment eventing stance
- Event Bus mode: native lightweight internal mode via MediatR
- Delivery model: in-process only
- DLQ policy: N/A in current MVP environment
- Replay policy: N/A in current MVP environment
- External broker assumptions: do not apply unless explicitly approved later

## Idempotency / retries
- Commands should be idempotent where feasible.
- Handlers must tolerate repeat invocation where feasible.
- Do not design broker-specific retry/DLQ patterns into the current MediatR-only environment.

## Hard rules
- Do not invent a new error envelope.
- Do not emit breaking contract changes without schema/version review.
- Correlation IDs must flow end-to-end across request, service, audit, and any event/handler invocation.
