# Scope transition

| Decision | Accepted behavior | Explicit exclusion |
|---|---|---|
| Prior `mvp6-root-r2-producer-ct-accept-01` | Read-only scoped raw detail read, detached materialization, explicit missing/null/invalid/present handling and nullable detail projection | Mutation, create-time persistence and historical producer emission |
| This successor | Exact three-file create-time persistence of authoritative `LifecycleCorrelationId`, with independent runtime verification | Browser/PNG, canonical publication, common-checkout uptake, migration/backfill, full module and rollout |

The decisions are cumulative only within their exact source boundaries. The prior read-only acceptance is not evidence that create emission already worked. This successor supplies the missing mutation evidence and does not broaden the older decision retroactively.
