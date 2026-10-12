# Owner decision — Q83 text-correction patches P1…P5 (Q85) — 2026-09-26

Recorded by the Q86 chat lane (single ledger and pack writer) at 2026-09-26T19:14+03:00 on CT instruction (CT writes no files).
Decisions given by the owner through the question tool, 2026-09-26 ~19:15 +03:00 (CT conversation), on the options in
`docs/roadmap/plans/mvp6-text-patch-q83-01/SIGN-OFF.md` (SHA-256 `37bf248d227899205e7a5eb72c2f08fbc891689c4ccd04b78676f544aa596797`; package `SHA256SUMS` `9b156f75cb9a6b35859cba43f48fe507f20984b0e2eb2ff8b7cf0811e671b817`, 9/9 OK at 19:13).

This is the fixed record path named in SIGN-OFF.md.

| Decision | Patch | Option | Target | Preimage SHA-256 | Patch SHA-256 | Postimage SHA-256 (patch alone) |
|---|---|---|---|---|---|---|
| 1 — MOD-0190 §22 wording | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/01-P1-MOD-0190-s22-wording.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` | `e475c74a4c51343dc94c0034abe1851c460f5b992f8e57a11a6ad65fcfa6fd8b` | `44a377c2c9c903b8647a9fc479122ffcbd997f95eb514b804a2ec3bf8f639ac3` |
| 2 — MOD-0192 §22 wording | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/02-P2-MOD-0192-s22-wording.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` | `7c3678bc5cbd646c8c2b6f9b9e99089cba9e03b34aed4e74359d52078975171f` | `2f0d2d0f8d7ba893cc30a7f22a44094fdff8a16a6a74bfa05c960cd08747d4ec` | `f6b4d0f3ae971666ea630e23b1edae501c226503aad8ed2b7a7ca804e314e445` |
| 3 — DCP-009 §21.1 exclusion | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/03-P3-DCP-009-s21.1-exclusion.patch` | **A — approve** | `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md` | `6b12ce685114248e89b78729546f43e86836d52f337599cb399de2993920046b` | `7bda0123d8190237b2ce69397b0b974dd904ee5516654f34cd89e78a7b4c34c0` | `ab728d67662037cd6bb73a8f5ad89f82c8fac64f2faf364d72d874cd21328fdf` |
| 4 — MOD-0190 contract pin 2.0.0 → 3.0.0 | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/04-P4-MOD-0190-contract-pin-3.0.0.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` | `2bdd533f7e558fc7bd18670d23db1bdf51aedd61d5dfa28e8dbd73b20757017f` | `f0c4662d68cfc7df4616a9b2b3d637ffb80cace60071e986aaf283a64b1b2eb3` | `0f0649112899a9f1fa4f58d523bcecf1bc2fd9df3cdbef6d3fd77359120b2766` |
| 5 — MOD-0187 §32 intro + §32.14 wording | `docs/roadmap/plans/mvp6-text-patch-q83-01/patches/05-P5-MOD-0187-s32.14-wording.patch` | **A — approve** | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | `8ed42fad66b7739b8c56778a57e0ead84cc8ce3678ea4868306263ac501a94a2` | `c82deca593d9cfd0b9410b19d87607a42ebc5dda0a6474742e51fb0fd8853bc5` | `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` |

**Combined MOD-0190 postimage after P1 and P4 (either order):** `003aba70b984cecd7bd2348daf46f0093101f68d5c172c935029444516b89913`.

Approved text (option A, per decision): as written in SIGN-OFF.md under "Exact text (option A)" for Decisions 1–5 — apply the named patch only if the target's SHA-256 before it is the preimage above and after it is the postimage above (MOD-0190: the combined postimage when both P1 and P4 are applied).

## Scope

Text only (pack and DCP wording). **Not approved:** UI code, contract, gateway, permission, navigation, localization, registry or status-tracker changes, `MANIFESTS.md` edits, `done` status, commit, push or stash. One named writer applies (Q86); an independent VER follows (Q87), started only after the Q86 hand-off line exists in MILESTONE-EVENTS.
