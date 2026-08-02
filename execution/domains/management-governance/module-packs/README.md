# Management & Governance module-pack routing

This directory currently contains no parent module pack. The scaffold is planning-only and grants no runtime,
service, port, Gateway, migration or production authority.

| Canonical module | Future pack path | Initial state | Routing rule |
|---|---|---|---|
| MOD-0007 — Decision & Rationale Log | `MOD-0007-decision-rationale-log.md` | `draft` authoring pending | Enterprise Control Points / Enterprise Governance Registry Layer / Authoritative Registries; physical service `TBD` |
| MOD-0354 — Decomposition & Work Structuring Engine | Separate canonical pack | Not materialized here | OD-02 conditional modular monolith + isolation/split gate; structural-only |
| MOD-0355 — Business Process Architecture & Modeling | Separate canonical pack | Authoring pending | OD-02 conditional modular monolith + isolation/split gate; process-model-only |

MOD-0007 authoring must preserve the Enterprise Governance Process Owner role and the ownership boundaries in
[domain-config.md](../domain-config.md). MOD-0023 remains a separate PSS-owned amendment. A Gate I-A MOD-0117
consumer is non-executable until producer approval, matching consumer amendment approval and explicit runtime
authority.

Use `/prepare-module-pack`; start each new parent pack as `draft`, run its canonical preflight and obtain human
approval before any implementation work.
