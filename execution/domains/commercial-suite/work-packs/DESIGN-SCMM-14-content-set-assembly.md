# TASARIM BRIEF — SCMM-14 · ContentSet (Assembly) aggregate

> **Control Tower §14 tasarım brief'i (SoR).** Amaç: SCMM-14 aggregate şeklini WP paketlemeden ÖNCE netleştirmek. Kaynak: docx §7 records (Content set and draft) + SCMM planı + tüketilen aggregate'ler (SCMM-10/11/12/13). Module: **CAND-CAP-0011**, CrmService. Branch: `feature/scmm-content-studio`.

## 1. Ne (legacy ⑤ UCLN Book karşılığı — "resolved book" / Argument Set)
Seçili içerik **component**'lerini + **claim**'leri, bir **composition template** (ConceptChainTemplate) düzenine göre, opsiyonel bir **scope** içinde birleştiren **versiyonlu, mutable DRAFT**. Amaç: yetkili bir output'a giden yolu kurmak; kaynak sürümlere izlenebilir.
- **YENİ aggregate** (mevcut yok; ⑤ UCLN Book %0). `ContentSet` ≠ `KnowledgePath` (docx: ikisini eşitleme).
- **Sınır:** SCMM-14 = **yalnız mutable draft authoring** (kur/düzenle/arrange/kaydet). Immutable **Revision** (dependency-version snapshot + validation result + review) = **SCMM-15** (MOD-0023 bloklu). Render/output = SCMM-16. Release/withdrawal = SCMM-17. **SCMM-14 freeze/review/output/release YAPMAZ.**

## 2. Aggregate: `ContentSet` (draft) — alanlar (docx §7 "Content set and draft")
- Kimlik: `SetCode`, `SetName`, tenant-scoped, `Description?`.
- **TemplateRef:** `{ ConceptChainTemplateId + ChainVersion }` (③ arrangement iskeleti — SCMM-10; pinned).
- **ScopeRef (opsiyonel):** **reusable `ContentScope` aggregate**'e version-pinli referans `{ ContentScopeId + ScopeVersion }` (④ context/scope; D14-a). Inline DEĞİL.
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

## 4. KARARLAR — **KİLİTLENDİ (2026-09-14, owner onayı)**
| # | Karar | Kilitli değer |
|---|---|---|
| **D14-a** | Scope nerede durur | ✅ **Reusable `ContentScope` aggregate** (version-pinli ref). Legacy ④ + docx "scope references" + "define once, reference many" (owner önerisi). Inline DEĞİL. İsim `ContentScope` — **MOD-0167 StrategyTemplate ile karıştırma** (o ayrı, shipped). |
| **D14-b** | Arrangement modeli | ✅ **Arrangement item = {templateStepId, branchId?, position, componentRef\|claimRef}** — template paralel-branch/cardinality'sine saygılı (SCMM-10). Flat-liste değil, step-slot eşlemesi. |
| **D14-c** | Eligibility uygulama zamanı | ✅ **Draft-save'de değerlendir + sonucu kaydet (non-blocking validation)**; hard gate SCMM-15/17. |
| **D14-d** | Sürüm pinleme | ✅ **Seçim anında pin** (ContentVersion/ClaimVersion/ChainVersion/**ScopeVersion** snapshot); provenance. Live-latest değil. |
| **D14-e** | Paketleme | ✅ **SCMM-14 (ContentScope + ContentSet backend+API) → SCMM-14-UI (workspace)** ayrı (SCMM-12 deseni). UI hep aranabilir dropdown/select2 — **ham Id girişi YOK** (owner). |
| **LE** | Legal-entity boyutu | ✅ **EKLENMEZ** — market ekseni yeterli; eligibility'de LE yok (tutarlılık); gerekirse ileride scope+eligibility'ye birlikte additive. |

## 5. Tüketilen contract'lar (hazır)
- ConceptChainTemplate (SCMM-10, ChainVersion) · Claim (SCMM-12, ClaimVersion + ClaimsController) · KnowledgeContent variant (SCMM-13, ContentSetId/language) · Eligibility port (SCMM-11, in-process `IEligibilityEvaluationPort`).

## 6. Kaynaklar (SoR izlenebilirliği — bu brief neye dayanıyor)
1. **Birincil otorite:** `DiTEN SCMM IT Transformation Plan v1.1.docx` (Downloads) — **§7 Records "Content set and draft"** (ContentSet alanları birebir) + Revision/Render/Release (=SCMM-15/16/17 sınırı) + Claim "no-inherited-approval" + scope dims (product/audience/market/channel/language/period, **LE yok**) + §2 "no equivalent aggregate / resolved book" + §3 "domain eligibility application" + **§11 "Strategy Template→context/scope"** (D14-a reusable) + §13 dil-varyant.
2. **CT register:** `SCMM-content-studio-work-plan.md` — SCMM-14 satırı (bağ 10/11/12/13, clone-to-draft/provenance/no-inherited-approval) + §2 reuse-vs-build (⑤ UCLN Book %0 new-build "Argument Set") + UCLN ①-⑥.
3. **Frozen kararlar:** `docs/decisions/DEC-SCMM-03` (D02c component=KnowledgeContent reuse, sektör-nötr, additive).
4. **Legacy gerçek veri/mockup:** `Desktop/2025-31-10/ProjectSettings/UCLNBook.bson + UCLNDesign.bson` (Book=BookDesignId+SubjectListId+UclnLists; Design=UCLNTypeIds zinciri+Moderator+ForWhom) + `Downloads/ucln-workflow-standalone.html` (①-⑥ akış).
5. **Tüketilen aggregate kodu (feasibility/ref-pin):** ConceptChainTemplate (SCMM-10, ChainVersion) · Claim/ClaimDto (SCMM-12) · KnowledgeContent variant (SCMM-13) · Eligibility port (SCMM-11).
> "Ne" kısmı ≈%80 docx §7; legacy-uygunluk UCLN export+register; kısıtlar DEC-SCMM-03; ref/pin feasibility mevcut kod. docx'in kendisi = docx + ucln-html + status xlsx sentezi (plan §3).

## 7. Sonraki adım
Kararlar (D14-a..e) kilitlenince → **WP-SCMM-14** (backend+API) paketle (§36.1 backend-architect), sonra **WP-SCMM-14-UI** (frontend-ui-ux). Freeze/review (SCMM-15) MOD-0023 bloklu → ayrı, sonra.
