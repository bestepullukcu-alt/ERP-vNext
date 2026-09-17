---
id: DCP-010
slug: procurement-p2p
name: Procurement — Procure-to-Pay
type: Delivery Capability Pack
standard: CAP-001
status: approved
status_note: "approved (product owner Ali via CT, 2026-09-16, per CAP-001 §5). Owner decisions RESOLVED (scope=Procurement P2P 0140-0145; service=Diten.ProcurementService; port=5062; separate procurement domain). runtime_code_allowed flips PER-MODULE (CAP-001 §7): MOD-0140 ready-for-dev + code authorized (FAZ 1 scaffold); 0141/0142/0143/0144/0145 remain draft until each reaches its own ready-for-dev gate. Contract-first vs frozen mocks: GRN→INVENTORY posts against frozen INVENTORY-BUNDLE (mock now, real MOD-0173 later — progressive integration; GRN code unchanged)."
approved_by: "product-owner (Ali) via control-tower"
approved_on: 2026-09-16
owner_domain: procurement
owner: procurement / control-tower
created: 2026-09-16
authoring_branch: feature/procurement-mvp-2
canonical_source: "docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx#Blueprint_Data (Procurement Suite / Procure-to-Pay (P2P), MOD-0140..0148)"
canonical_modules: [MOD-0140, MOD-0141, MOD-0142, MOD-0143, MOD-0144, MOD-0145]
runtime_code_allowed: true
runtime_code_scope: "ALL members 0140-0145 ready-for-dev (2026-09-17). Done: 0140 (slice 1), 0145 (slice 2), 0141 (slice 3) — each backend+tests+frontend+gateway CT-verified. Authorized to build sequentially (Ali, 2026-09-17): 0142 Receiving/GRN (slice 4, W-2, CRITICAL: posts to INVENTORY frozen contract, shadow-stock forbidden, G2A golden flow) → 0143 Invoice 3-Way Match (slice 5, consumes GRN+PO) → 0144 Contracting (slice 6, consumes SUPPLIER+SOURCING). Build order respects dependencies."
inputs:
  - "docs/analysis/inventory-capability-scope-and-dependency-report.md (§13.1/§14.6/§15.1/§20.2/§21 Procurement lane)"
  - "docs/analysis/contracts/*.openapi.yaml (frozen INVENTORY-BUNDLE, PRODUCT-MASTER-BUNDLE, LOCATION consumed; GRN-EVENT owned-frozen; SUPPLIER front-loaded slice owned)"
  - "execution/domains/procurement/domain-config.md"
  - "execution/registries/module-id-registry.md (MOD-0140…0145 rows, Procurement — Reserved block)"
  - ".antigravity/scripts/verify_module_id.py (DCP-002 gate — MOD-0140/0141/0142/0143/0144/0145 exit 0, 2026-09-16)"
  - ".antigravity/rules/capability-pack-standard.md (CAP-001)"
  - "docs/reference/blueprint/…master 8.1.xlsx#Blueprint_Data (canonical names + Min Integration Contract Codes)"
---

# DCP-010 — Procurement (Procure-to-Pay) (Delivery Capability Pack)

> **Artifact guard.** CAP-001 Delivery Capability Pack — governance/orchestration contract. **NOT** a runtime entity, **NOT** a Module Pack, **NOT** a Capability Group.
> **`status: approved`** (product owner Ali via CT, 2026-09-16). **`runtime_code_allowed: true` — TÜM üyeler 0140-0145 ready-for-dev** (Ali, 2026-09-17). 0140/0145/0141 done (CT-verified); 0142/0143/0144 sırayla build yetkili (slices 4-6, bağımlılık sırası: 0142→0143→0144).
> **Identity guard.** Yeni `MOD-xxxx` basmaz. Üyeler Blueprint-canonical MOD'lardır (0140-0145); her biri DCP-002 preflight'ından geçti (exit 0, 2026-09-16).
> **No shadow stock.** Envanter yalnız MOD-0173'e (frozen INVENTORY contract, `POST /movements`); ikinci balance/ledger YASAK.

## 1. Identity and status
| Field | Value |
|---|---|
| ID | DCP-010 · slug `procurement-p2p` |
| Type | Delivery Capability Pack (CAP-001) |
| Status | **approved** (product owner Ali via CT, 2026-09-16) |
| Owner domain | procurement |
| Canonical source | Blueprint 8.1 (Procurement Suite / Procure-to-Pay (P2P)) |
| Service | `Diten.ProcurementService` · port **5062** |
| runtime_code_allowed | **true — ALL 0140-0145** (0140/0145/0141 done; 0142/0143/0144 authorized to build, slices 4-6) |

## 2. Business outcome
Denetlenebilir, uçtan uca satın alma: tedarikçi onboarding (KYC/sanctions) → sourcing/RFx → requisition/PO → mal kabul (GRN) → fatura 3-yönlü eşleştirme → sözleşme/clause yönetimi. GxP/pharma-uyumlu, çok-tüzel-kişilikli, audit/evidence'lı, envanter gerçeğini bozmadan (post via INVENTORY contract) çalışan P2P platformu.

## 3. Problem statement
vNext'te procurement YOK (greenfield). Blueprint P2P modülleri (0140-0148) registry'de değildi, procurement domain'i ve servisi yoktu. Envanteri besleyen GRN seam'i (0142→0173) contract olarak dondurulmuş ama üretici (procurement) tarafı yazılmamıştı. Bu pack o governance boşluğunu kapatır ve MVP-2 lane'ini contract-first açar.

## 4. Capability boundary
**Owns:** supplier master/onboarding (0140) · sourcing/RFx (0145) · requisition/PO (0141) · goods receipt/GRN (0142, envanteri 0173'e post eder) · invoice 3-way match outcome (0143) · procurement contract/clause (0144).
**Consumes (frozen contract only):** MOD-0290 PRODUCT-MASTER (item/SKU identity) · MOD-0173 INVENTORY-BUNDLE (`POST /movements` GOODS_RECEIPT_PO / SUPPLIER_RETURN) · LOCATION master · backbone (RBAC/Audit/Workflow/Reference/Evidence/Notification/EventBus/Gateway).
**Does NOT own:** stok balance/ledger (MOD-0173/SCE) · product identity (MOD-0290/MDM) · AP/payment execution (Finance/Treasury; MOD-0146 out) · supplier performance/portal (MOD-0147/0148 → MVP-6) · physical-location master decision (SCE/DEC-INV-03).

## 5. Member modules and follow-ups
MVP-2 core (Blueprint canonical names, all DCP-002 exit 0 2026-09-16):
- **MOD-0140 Supplier Onboarding (KYC/Sanctions/Docs)** (W-1) · **MOD-0141 Requisition & Purchase Orders** (W-1) · **MOD-0142 Receiving (GRN)** (W-2) · **MOD-0143 Invoice Capture & 3-Way Match** (W-2) · **MOD-0144 Contracting & Clause Library** (W-2) · **MOD-0145 Sourcing (RFQ/RFP)** (W-3).
Her üye kendi module pack + FU'larıyla açılır (bu pack basmaz). **Excluded members:** MOD-0146 Payment Run Support (Not SoR; Finance/Treasury; out of scope), MOD-0147/0148 (Supplier Perf/Portal → MVP-6).

## 6. Ownership map
SoR ownership = domain-config + rapor §3/§21.3. Özet: supplier→0140; requisition/PO→0141; goods receipt→0142; match outcome→0143; procurement contract→0144; RFx/bid/award→0145. Consumed SoR: product identity→0290(MDM), stock ledger→0173(SCE), location→shared master. Her OWNED contract tek-yazıcı; CONSUMED seam'lerde procurement salt-okur/post-eder.

## 7. Dependency graph
Rapor §13.1 + §21.2. Kritik: `G1 (0290 identity + 0173 movement/availability freeze)` → **Procurement lane**. Intra-lane: `0140 → 0145 → 0141 → 0142 (→0173 post) → 0143 (←GoodsReceived) → 0144`. Consumed frozen contract'lar (PRODUCT-MASTER, INVENTORY, LOCATION) hazır → procurement mock'a karşı beklemeden başlar.

## 8. Ordered delivery sequence
Dikey dilimler (contract-first, frozen mock'a karşı): **0140 Supplier → 0145 Sourcing → 0141 Req/PO → 0142 GRN (INVENTORY mock'una post) → 0143 Invoice 3-way → 0144 Contracting.** Her dilim: derle 0-error + test + tenant izolasyonu; push öncesi runtime KANIT. GRN→0173 seam'i frozen INVENTORY contract'ını hedefler (mock şimdi, gerçek 0173 sonra — progressive integration; GRN kodu değişmez).

## 9. Prerequisites
Backbone (RBAC 0018 / Audit 0021 / Workflow 0023 / Reference 0048 / Evidence 0029/0031 / Notification 0027 / Event Bus 0035 / Gateway 0032) mevcut. Frozen consumed contract'lar: PRODUCT-MASTER-BUNDLE, INVENTORY-BUNDLE, LOCATION (docs/analysis/contracts/, `x-status: FROZEN`). G1 (0290 identity + 0173 movement/availability freeze) consumer başlangıç kapısı.

## 10. Architecture decisions
- **AD-1 Ayrı servis/domain:** `Diten.ProcurementService` (port 5062), ayrı procurement domaini — envanterden bağımsız bounded context; iletişim yalnız contract.
- **AD-2 No shadow stock (rapor §21.1 rule 3):** mal kabul yalnız INVENTORY `POST /movements`; ikinci balance/ledger YASAK.
- **AD-3 No invented product identity:** ürün/SKU kimliği yalnız MOD-0290'dan consume edilir.
- **AD-4 Contract-first + additive-only (K16):** frozen contract donunca değişmez; yalnız additive minor.
- **AD-5 Tenant + Legal-Entity scoping (DEC-INV-18):** server-resolved, cross-LE fail-closed.
- **AD-6 Money = Decimal-string, float YASAK; para birimi LE base currency.**
- **AD-7 (ASSUMPTION-P2P-01) 3-way match tolerance policy-driven:** sabit sayısal varsayılan gömülmez; tolerans profil/exception-queue ile modellenir (EA/Finance-TBD).

## 11. Scope
Blueprint P2P W-1..W-3 (0140-0145). Pharma/GxP + regulated procurement (KYC/sanctions + audit/evidence). Payment run (0146) ve supplier perf/portal (0147/0148) domain'de değil.

## 12. Explicit exclusions
- **MOD-0146 Payment Run Support** — Not SoR; AP/payment = Finance/Treasury. Kapsam dışı.
- **MOD-0147/0148 Supplier Performance & Risk / Supplier Portal** — MVP-6 lane (DCP-009 §18 OD-4). Bu DCP sahiplenmez; SUPPLIER contract onların yüzeyini superset korur.
- **Stok balance/ledger redefinition** — MOD-0173 (frozen INVENTORY) dokunulmaz.
- **Product identity** — MOD-0290 (MDM) dokunulmaz.
- **Physical location master** — SCE/DEC-INV-03 kararı.

## 13. Governance drift risks
Registry↔code drift (K11); shadow-stock riski (GRN'in kendi balance'ını tutma cazibesi — AD-2 ile bloklu); consumed frozen contract'a additive-olmayan değişiklik cazibesi (K16); MVP-6'nın tükettiği SUPPLIER slice'ının producer genişletmede daraltılması riski (`x-front-loaded-for` kırılması — superset zorunlu); 3-way match tolerance'ın koda gömülme riski (ASSUMPTION-P2P-01 ile bloklu).

## 14. Review questions
- Ayrı `Diten.ProcurementService` (5062) doğru mu, yoksa mevcut bir servise mi katılmalı? (öneri: ayrı — bounded context.)
- 0144 Contracting bu MVP-2'de mi yoksa faz sonrası mı? (Blueprint W-2; roster'da "(faz)" notu — DCP'de üye, sıra sonuncu.)
- 3-way match tolerance sahipliği: tenant policy mi, Finance mi? (ASSUMPTION-P2P-01, EA/Finance-TBD.)
- SUPPLIER producer contract genişletmesi MVP-6 slice'ını superset koruyor mu? (freeze kapısında owner+consumer review.)

## 15. Gate criteria
- **G1 (consumer start):** 0290 identity + 0173 movement/availability freeze (SCE tarafı) — mevcut frozen contract + mock ile SATISFIED.
- **G2A (Procure-to-Receive, rapor §20.2):** `PO → GRN → 0173 inventory posting → invoice 3-way match` golden flow PASS; L3; idempotent; **GRN gerçekten 0173'e (mock/gerçek) post ediyor**; approval/audit. Her gate CT acceptance (agent PASS ≠ CT ACCEPTED, K13).

## 16. Acceptance criteria
DCP `approved` = product-owner (Ali) onayı + üye module pack'ler kendi kapılarından geçmeye hazır. Her modül DoD = ilgili gate (G1/G2A) + evidence E4 (state-changing) / E5 (cross-module GRN→INVENTORY integration). Contract'lar owner+consumer review sonrası `x-status: FROZEN`.

## 17. Downstream business-module impacts
Supply-Chain-Execution (0173 ← GRN goods-receipt post; DCP-009 §17 "Procurement 0142 GRN → 0173 post" tarafı) · Finance/Treasury (match outcome → AP/payment; MOD-0146 dışarıda) · MDM (0290 product identity consumer) · MVP-6 (0147/0148 ← SUPPLIER contract) · MRP (MOD-0189 → PO requirement önerisi; PO SoR burada).

## 18. Open decisions
- **OD-1 (RESOLVED, product owner 2026-09-16):** Ayrı procurement domaini + `Diten.ProcurementService` port 5062 + contract-first tam MVP-2, MVP-1'i beklemeden (frozen INVENTORY mock'una karşı). Branch `feature/procurement-mvp-2`. **DCP-010 approved (Ali, 2026-09-16); FAZ 1 = MOD-0140 scaffold.**
- **OD-2 (ASSUMPTION-P2P-01):** 3-way match tolerance sahibi/değeri EA/Finance-TBD; contract policy-driven modeller, sabit varsayılan gömmez.
- **OD-3:** SUPPLIER producer contract'ın MVP-6 slice'ını superset koruma doğrulaması freeze kapısında (owner+consumer review).
- **OD-4:** Contracting (0144) sıralamada sonuncu; MVP-2 içinde mi yoksa fast-follow mu — Ali onayında netleşir (şu an üye, son dilim).
- **OD-5 (DECISION_REQUIRED — port collision, ölçüldü 2026-09-16 FAZ 1):** `Diten.ProcurementService` AGENTS.md §3 authority'sine göre **5062**'ye scaffold edildi (Ali kararı). Ancak `Diten.PpmService` de **5062**'yi squat ediyor (appsettings + launchSettings + ocelot `/api/v1/ppm` route 9/29) — AGENTS.md §3'te listelenmemiş, §2'de "production service yok / yetkisiz" olarak işaretli önceden-var-olan drift (K11). İki servis aynı anda çalışamaz. Procurement sınırı dışı (PpmService protected). **Öneri (Option A):** Procurement 5062'de kalır (Ali + §3 authority); PpmService free port'a (ör. 5063) reassign edilir VEYA dormant teyit edilir — PpmService-owner/EA kararı. Ali onayı bekler; FAZ 2 build/test bundan etkilenmez, yalnız iki servisin aynı anda runtime'ı etkilenir.

## 19. Future follow-ups
Supplier performance/risk & portal (0147/0148 → MVP-6) · payment run integration (0146 → Finance/Treasury) · advanced sourcing (reverse auction) · contract clause AI-extraction · supplier collaboration/EDI adapter (dış entegrasyon, DEC-INV-19 deseni).

## 20. Audit and reconciliation notes
**FAZ 2 COMPLETE (all 6 members, 2026-09-17) @ evidence E2 (build+test+contract+static verifier); runtime E3/E4 environment-gated.**
CT-verified full regression: `Diten.ProcurementService` build 0-error; **145/145 xUnit green**; frontend + gateway build 0-error; all **7 frontend modules verifier 93/1** (golden parity, the 1 = shared `personalization-client.js`, not module-owned); 7 gateway route families (`/api/{suppliers,sourcing,requisitions,purchase-orders,grn,invoice-match,contracts}` → 5062); DCP-002 `--check-all` 0 HARD. Slices: 0140 (1a/1b) · 0145 (2a/2b) · 0141 (3a/3b) · 0142 GRN (4a/4b) · 0143 Invoice (5a/5b) · 0144 Contracting (6a/6b). Consumed frozen contracts (INVENTORY/PRODUCT-MASTER/LOCATION) + owned-frozen GRN-EVENT unedited (K16). No shadow stock (GRN posts via IInventoryPostingClient seam to MOD-0173 frozen shape; AssertNoBalanceField test). 3-way match tolerance policy-driven seam (ASSUMPTION-P2P-01, no baked numbers).
**Runtime smoke (2026-09-17, E3/E4 achieved for representative verticals):** ProcurementService run against live Mongo (isolated on 5099, Development env: dev JWT + DevBypass tenant/LE). E3: boot clean, anonymous `/api/suppliers` → 401 (auth wired), Swagger 200. E4 Supplier: CREATE→201, LIST→200, Mongo PERSIST with server-resolved TenantId/LegalEntityId + IsDeleted=false, duplicate taxId→409. E4 GRN (critical): POST→201 posting via IInventoryPostingClient (inventoryTransactionId returned), Mongo doc has NO balance field (no shadow stock), idempotent replay→1 doc. **Defect found+fixed by smoke:** `SupplierIndexConfiguration` partial unique index used `$ne null` (MongoDB partial-index-illegal → index silently not created; unit tests used fakes, K1) → fixed to `$type:string`, runtime-verified, 145/145 green (commit). Other 5 index configs unaffected. Not yet smoked: full browser E2E through Gateway/Web/Auth; per-module API smoke for Sourcing/Req-PO/Invoice/Contracting (share the now-proven infra; all index configs created clean at boot).
**Open (not blocking code, need product-owner/env):** OD-5 port 5062 vs PpmService (**now 3-way: a running `Diten.Dat…` process also holds 5062** — measured 2026-09-17; smoke used 5099); runtime E4 smoke (needs Mongo+service+gateway+web); real MOD-0173/0290 HTTP client wiring (progressive integration — mock now, code unchanged later); owned contracts (SOURCING/REQUISITION-PO/INVOICE-MATCH/CONTRACTING) still `x-status: REVIEW` → owner+consumer freeze + fold additive endpoints; MATCH has no invoice-list endpoint (Index uses exception queue) — additive contract revision candidate.

DCP-002 preflight: MOD-0140/0141/0142/0143/0144/0145 **exit 0 (proven against Blueprint, 2026-09-16)** — komut: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-01XX --name "<Blueprint canonical>"`. Registry rows added 2026-09-16 (identity-only, Procurement — Reserved block). Reconciliation `reconciled` statüsünde doldurulacak. Owned contract'lar `docs/analysis/contracts/` — SUPPLIER expanded (superset, front-loaded slice korunmuş) + GRN-EVENT (owned-frozen, değişmedi) + SOURCING/REQUISITION-PO/INVOICE-MATCH/CONTRACTING (yeni, freeze-ready owner+consumer review pending).
