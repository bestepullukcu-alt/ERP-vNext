# TASARIM BRIEF — SCMM-14 · ContentSet (Assembly) aggregate

> **Control Tower §14 tasarım brief'i (SoR).** Amaç: SCMM-14 aggregate şeklini WP paketlemeden ÖNCE netleştirmek. Kaynak: docx §7 records (Content set and draft) + SCMM planı + tüketilen aggregate'ler (SCMM-10/11/12/13). Module: **CAND-CAP-0011**, CrmService. Branch: `feature/scmm-content-studio`.

## 1. Ne (legacy ⑤ UCLN Book karşılığı — "resolved book" / Argument Set)
Seçili içerik **component**'lerini + **claim**'leri, bir **composition template** (ConceptChainTemplate) düzenine göre, opsiyonel bir **scope** içinde birleştiren **versiyonlu, mutable DRAFT**. Amaç: yetkili bir output'a giden yolu kurmak; kaynak sürümlere izlenebilir.
- **YENİ aggregate** (mevcut yok; ⑤ UCLN Book %0). `ContentSet` ≠ `KnowledgePath` (docx: ikisini eşitleme).
- **Sınır:** SCMM-14 = **yalnız mutable draft authoring** (kur/düzenle/arrange/kaydet). Immutable **Revision** (dependency-version snapshot + validation result + review) = **SCMM-15** (MOD-0023 bloklu). Render/output = SCMM-16. Release/withdrawal = SCMM-17. **SCMM-14 freeze/review/output/release YAPMAZ.**

## 2. Aggregate: `ContentSet` (draft) — alanlar (docx §7 "Content set and draft")
- Kimlik: `SetCode`, `SetName`, tenant-scoped, `Description?`.
- **TemplateRef:** `{ ConceptChainTemplateId + ChainVersion }` (③ arrangement iskeleti — SCMM-10; pinned).
- **ScopeRef (opsiyonel):** inline `{ ProductRefs[], MarketRefs[], AudienceRefs[], Channel?, LanguageCode?, PeriodFrom/To? }` (④ Strategy Template context/scope).
- **SelectedComponents[]:** `{ KnowledgeContentId + ContentVersion + LanguageCode + Role? + Arrangement(templateStepId, branchId?, position) }` (⑥ — SCMM-13 variant; pinned object+version).
- **SelectedClaims[]:** `{ ClaimId + ClaimVersion + Arrangement(templateStepId, branchId?, position) }` (SCMM-12; pinned).
- `DraftSchemaVersion` (int) · optimistic concurrency token (`EntityBase.Version`) · `Status` (draft/inactive/archived — **approved/frozen YOK**, o Revision).
- **Provenance:** her ref **object+version** taşır; sürüm seçim anında pinlenir; silent retarget YOK (docx: "immutable revisions never silently retargeted").

## 3. Davranışlar
- **Create draft** (template'ten; ops. scope) · **clone-to-draft** (mevcut set → yeni draft, refs remap, yeni id, no-inherited-approval).
- Component/claim **ekle/çıkar/arrange** — template cardinality/branch (SCMM-10) + dil/variant (SCMM-13) doğrula.
- **Eligibility application** (SCMM-11): set scope + seçili component'ları eligibility policy'lere karşı değerlendir → per-item disjoint **Eligible/Blocked/Unresolved** (fail-closed) — **draft'ta validation olarak KAYDET, kaydetmeyi bloklama** (hard gate freeze/release'de).
- **No-inherited-approval** (docx): assembly kendi kaydı; onaylı claim/component set'i onaylamaz.
- **Variant awareness:** source `needs_assessment` olan component seçilirse **uyarı** (SCMM-13).
- Physical delete YOK (archive/retire).

## 4. KİLİTLENECEK KARARLAR (öneriyle)
| # | Karar | CT önerisi |
|---|---|---|
| **D14-a** | Scope: inline mi ayrı reusable Scope aggregate mı? | **Inline opsiyonel scope** (draft üstünde). Reusable "saved scope" gerekirse ayrı follow. docx "optional scopes" — inline yeterli. |
| **D14-b** | Arrangement modeli | **Arrangement item = {templateStepId, branchId?, position, componentRef\|claimRef}** — template'in paralel-branch/cardinality'sine saygılı (SCMM-10). Flat-liste değil, step-slot eşlemesi. |
| **D14-c** | Eligibility uygulama zamanı | **Draft-save'de değerlendir + sonucu kaydet (non-blocking validation)**; hard gate SCMM-15/17. |
| **D14-d** | Sürüm pinleme | **Seçim anında pin** (ContentVersion/ClaimVersion/ChainVersion snapshot); provenance. Live-latest değil. |
| **D14-e** | Paketleme | **SCMM-14 (backend+API) → SCMM-14-UI (workspace)** ayrı (SCMM-12 deseni). |

## 5. Tüketilen contract'lar (hazır)
- ConceptChainTemplate (SCMM-10, ChainVersion) · Claim (SCMM-12, ClaimVersion + ClaimsController) · KnowledgeContent variant (SCMM-13, ContentSetId/language) · Eligibility port (SCMM-11, in-process `IEligibilityEvaluationPort`).

## 6. Sonraki adım
Kararlar (D14-a..e) kilitlenince → **WP-SCMM-14** (backend+API) paketle (§36.1 backend-architect), sonra **WP-SCMM-14-UI** (frontend-ui-ux). Freeze/review (SCMM-15) MOD-0023 bloklu → ayrı, sonra.
