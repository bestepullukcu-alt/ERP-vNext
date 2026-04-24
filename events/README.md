# Event Schema Registry (Build-Time)

This folder is the Faz 1 build-time schema registry.

## Structure
- `events/schemas/{event_name}/v{N}.json`

## Validation
Run:

```bash
./scripts/validate_event_schemas.sh
```

This validates JSON syntax and guarantees schema artifacts exist in repo for event contracts.
