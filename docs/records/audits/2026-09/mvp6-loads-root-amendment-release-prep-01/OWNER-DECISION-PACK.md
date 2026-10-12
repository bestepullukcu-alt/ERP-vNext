# Copyable owner decision package — pending

The following text contains only the missing release decisions. None is recorded as approved by this preparation.

## A — Final version and exact consumer consent

> I select SHIPMENT-BUNDLE **3.1.0 / wire v1** for the Loads root-read amendment. I reviewed and consent to the exact final-proposed bytes:
>
> - YAML SHA-256 `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`
> - Loads annex SHA-256 `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034`
>
> I acknowledge that `queryLoads` adds optional/nullable `LoadSummary.lifecycleCorrelationId` on the existing route while wire `contractVersion` remains `v1`; the version number alone does not protect strict unknown-property consumers.
>
> I am accountable for the consumer inventory. Repository-external applications, SDKs or integrations using this contract are: **[none / exact list]**. For every listed consumer, exact-hash consent and any required strict-parser migration are recorded before publication. Repo-local current state remains: Loads producer requires later uptake; Loads UI is design-only; no current Loads gateway route was evidenced.

## B — Canonical publication, separately grantable

> After A is recorded with a complete external-consumer declaration, I authorize the single publication owner to apply only publication patch SHA-256 `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5` to canonical preimages YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` and Loads v2 annex `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1`.
>
> The required targets are YAML `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` and new annex `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034`. The existing Loads v2 annex remains unchanged. This publication authorization does not authorize runtime, UI, gateway, pack promotion, rollout, commit or push.

## C — Runtime uptake, separately grantable after publication

> After exact publication is independently verified, I authorize a separately scoped Loads producer uptake package to emit the persisted `LoadPlan.CorrelationRoot` through `LoadSummary.lifecycleCorrelationId` under the approved missing/null/malformed/valid/nil policy, with no derivation or backfill. This authority must name exact owned source/test paths and requires independent runtime verification. It does not authorize Loads UI, gateway, rollout, pack promotion, commit or push.

