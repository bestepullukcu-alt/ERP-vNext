# PH15-185-UPTAKE — Phase 1.5 table for the Loads producer uptake (MOD-0185)

🤖 Applying knowledge of @orchestrator (Phase 1.5 table author) + @backend-architect.

**NOT APPROVED — prepared text only.** Row wording: `.antigravity/workflows/add-module.md` Phase 1.5 (sha256 `baf455c5…`), rows 1–9 in order. Scope: the backend change in WORK-BREAKDOWN.tsv only (no UI, no new endpoint, no backfill).
Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md` (sha256 `45b5dd33…`, ready-for-dev; backend Phase 1.5 approved 2026-09-18, §28).

| # | Kontrol | Cevap |
|---|---------|-------|
| 1 | Module pack'teki TÜM alanlar Entity'ye eklendi mi? | **Evet.** Plan: no entity change. `LoadPlan.cs:15` already persists `CorrelationRoot`; the new wire field is a read projection of it (annex 3.1.0 line 207). |
| 2 | Alan isimleri global ERP standartlarına uygun mu? (örn `PlateCode → Code`) | **Evet.** Plan: wire name `lifecycleCorrelationId` exactly as published (YAML line 3197), same name Shipment uses (YAML line 3101); storage name `CorrelationRoot` unchanged. |
| 3 | Repository izolasyon/Soft-Delete garantisi var mı? Tenant-owned ise TenantId filtresi, module pack'te açık global katalog istisnası varsa `GlobalEntity` + RBAC + `IsDeleted=false` filtresi doğrulandı mı? | **Evet.** Plan: the raw-document query keeps `Visible()` = TenantId + LegalEntityId + `IsDeleted=false` (`LoadRepository.cs:11-14`); tenant-owned, no global exception. |
| 4 | Entity base type doğru mu? Tenant-owned modülde `BaseEntity`/TenantId; onaylı Platform global katalogda `GlobalEntity`. | **Evet.** Plan: `LoadPlan : EntityBase` (TenantId, IsDeleted), unchanged. |
| 5 | CQRS yapısı (Command, Query, Handler, Validator — her biri ayrı dosya) planlandı mı? | **Evet.** Plan: existing `GetLoadListQuery.cs`, `GetLoadListHandler.cs` (QueryHandlers), `GetLoadListValidator.cs` stay one per file; only the handler projection changes; no new command/query (add-endpoint-cqrs). |
| 6 | DataTable ise `golden_reference` kararı doğru mu? (≤8 slim, >8 compact) | **Yok (N/A).** Backend-only change; the Loads UI is a separate package (pack §29, LOADS-UI-SCOPE). |
| 7 | Compact DataTable ise Create/Edit/Details logical section haritası planlandı mı? | **Yok (N/A).** No UI. |
| 8 | Required alan kontratı Backend Validator + Web ViewModel + Razor + tracker için aynı mı? | **Evet (response only).** Plan: no request field changes; the new response field is optional/nullable in YAML (not in `required`), emitted on every item as UUID or null; validators unchanged. |
| 9 | Platform lookup dependency checked mi? Dropdown/filter/select/default alanları PSS `/api/lookups/{key}` kullanıyor mu, yeni lookup key pack'te açık mı, MDM/reference boundary korunuyor mu? | **Yok.** No lookup; no reference read added to `queryLoads` (Carrier/Shipment reads stay only in create/transition). |

Design choices for the owner to confirm with this table: (a) invalid stored values are emitted as `null` and do not fail the list (ASSUMPTION A3); (b) finding F-1 (missing stored root vs nil inbound correlation in the transition path) is **not** fixed in this package.

## Owner question (one line)

Do you approve the Phase 1.5 table for the Loads producer uptake so a Mac writer can implement it in the isolated environment?

**Recommended answer: Yes.**

## Exact decision text (NOT APPROVED — prepared text only)

> I approve the Phase 1.5 table in `docs/roadmap/plans/mvp6-loads-uptake-prep-01/PH15-185-UPTAKE.md` for the MOD-0185 producer uptake granted as decision C / option A: `queryLoads` emits the persisted `LoadPlan.CorrelationRoot` as `LoadSummary.lifecycleCorrelationId` using a presence-aware read (stored UUID incl. nil → value; missing, null or invalid → null; the list is not failed), with no derivation, no backfill, no new endpoint and no change to create or transition. One writer may change only the 11 paths in OWNED-PATHS.md in the isolated environment of ISOLATED-ENV.md, followed by independent runtime verification on the Mac. This does not approve UI, Gateway, Program.cs, contracts, packs, the transition-path finding F-1, commit or push.
