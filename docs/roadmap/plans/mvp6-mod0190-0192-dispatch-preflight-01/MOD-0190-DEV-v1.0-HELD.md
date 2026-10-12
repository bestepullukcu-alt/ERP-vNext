# HELD — MOD-0190 core DEV v1.0

**Do not dispatch until SOP-22 gate table is closed and a new CT release marks this prompt active.** This file grants no runtime authority.

WP: MVP6-MOD0190-CORE-DEV-01 · Lane: isolated SandopPlans writer · Profile: bounded backend core · Required evidence: E4 where runnable.

Repository `/Users/natig/Projects/ERP-vNext-recovery`, branch `feature/mvp6-logistics`, prepared HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Actual dispatch HEAD/worktree and dirty baseline **must be freshly recorded**. Use a registered isolated worktree under GIT-002, not a filesystem copy presented as one.

Before start read AGENTS.md, `.antigravity/agents/orchestrator.md`, `.antigravity/workflows/add-module.md`, relevant security/multi-tenancy/repository/Mongo rules, the **then promoted** MOD-0190 pack, this preflight SOP-22, `MOD-0190-OWNED.tsv`, `MOD-0190-INPUTS.tsv`, archive, published SANDOP-CAPACITY YAML+annex with exact hashes, approved D190 decisions and Phase 1.5 record. Verify archive extraction hashes, then pin published contract separately; this archive contains only a proposed final contract. Stop if pack is draft, contract differs, source drift is unexplained or runtime owner authorization is absent.

**NE:** Implement only the approved six-operation SandopPlans core within the 38 exact prospective feature/test paths, adjusted only by a separately approved path delta. Keep plan/snapshot/sign-off, scoped receipt, audit and Pending outbox atomic. Preserve exact parsed Idempotency-Key and schema-valid names, current response correlation versus original event/audit correlation, original-result replay and approved error precedence. DEMAND ID/version/checksum is a tenant/LE-scoped **test fixture**, not a new DEMAND HTTP endpoint or live producer proof. Sign-off actor is the trusted JWT actor; no Workflow call, automatic plan approval or Event Bus publisher.

**YAPMA:** Write Program.cs, common DI/project/pipeline, shared permissions, gateway, peer CapacityPlans, canonical/guard, DEMAND producer, UI, worker/publisher, commit/push/stash. A missing shared registration is an integration-owner handoff, not a local bypass.

**DOĞRULA:** Build and run feature-local unit/contract tests; then isolated DB-010 replica-set and composed HTTP/JWT tests only after an authorized integration-owner composition target exists. Prove required/null/error/header parity, tenant/LE/soft-delete/RBAC, unique races, immutable snapshot/sign-off, receipt replay/change conflict, atomic rollback, unknown commit recovery, restart persistence and Pending-only outbox. Distinguish core PASS from HTTP/E5. Deliver exact changed-file/source→binary→process manifest, commands/exits/raw evidence, gap list and SOP §22 writer-complete. Independent VER follows in a separate snapshot.

**Reservation only:** API 56190, Mongo 57190, fixed `DitenSupplyChain_Mod0190_Test`; recheck free ports and separate replica/data/evidence at dispatch. Program.cs integration remains one CT-assigned integration-agent lane after feature types compile.
