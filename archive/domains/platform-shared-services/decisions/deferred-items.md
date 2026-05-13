# deferred-items.md — Platform & Shared Services

## Purpose
List the explicit items deferred from the current MVP baseline so they are not silently reintroduced during coding.

## Deferred modules / module modes

| Module | Current MVP status | Deferred scope |
|---|---|---|
| MOD-0032 API Gateway | Ocelot runtime in production; hardening deferred | Ocelot at `gateway/Diten.ApiGateway` is live (port 5000). Hardening features (rate limiting, policy engine, credential console, quota enforcement) are the deferred scope of MOD-0032/MOD-0033. |
| MOD-0037 Integration Monitoring & Reconciliation | Deferred | No integration-ops workbench, replay queue, or reconciliation console. |
| MOD-0042 Alerting & Incident Runbooks | Deferred as dedicated surface | No alerting subsystem or runbook operations console in current MVP. |
| MOD-0035 Event Bus | Thin internal seam only | No external broker, DLQ, replay, or provider-console behavior. |
| MOD-0041 Logging & Monitoring | Thin lightweight seam only | No SIEM/APM product clone; keep internal surface minimal. |

## Deferred capability depth inside active modules
- MOD-0018: complex policy DSL, heavyweight ABAC logic, analytics-heavy access reporting
- MOD-0021: advanced analytics, full enterprise audit lake, arbitrary data mutation support
- MOD-0023: BPMN-complete workflow engine, advanced simulation/modeling, heavy orchestration patterns
- MOD-0024: advanced scheduling/optimization, non-generic domain task semantics
- MOD-0028: complex DLP/records-retention stack, heavyweight content services
- MOD-0031: graph analytics and advanced bundle orchestration unless re-scoped
- MOD-0012: external vault providers, advanced secrets lifecycle management beyond thin abstraction

## Re-activation rule
A deferred item may move into active scope only when:
1. the decision is changed in `runtime-decisions.md`,
2. the domain package and affected module pack are updated,
3. the relevant batch prompt is revised,
4. the change does not violate protected paths or ownership rules.
