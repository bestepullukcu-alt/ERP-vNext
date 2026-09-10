# WORK PACKAGE — WP-SCMM-11 · Context & eligibility evaluation (Policy + evaluator)

> **Control Tower kaydı (SoR).** SCMM R1 model band, gate G2. **İlk CAND-CAP-0011 (Marketing owner) kodu.** Bağlı: SCMM-03 ✅ (D3a/RM4 freeze) · SCMM-11-AUD ✅ (AudienceProfile çok-eksen) · MOD-0290 canonical. Owner: domain lead. Module: **CAND-CAP-0011** (Marketing owner).
> **Deployment boundary (docx §Recommended direction):** "a new capability boundary does not by itself require a separate microservice" → CAND-CAP-0011 kodu **mevcut deployment'ta (CrmService)** yaşar; ayrı capability namespace/klasörü (MOD-0162 knowledge'dan ayrık, ör. `Features/ContentComposition/Eligibility`).

## Metadata
```text
WP ID:            WP-SCMM-11
Prompt ID:        P-SCMM-11 · v1.0
Task Class:       New capability — policy aggregate + deterministic evaluator — state-changing
Golden-Flow Profile: B (backend); UI = SCMM-11-UI follow
Risk Class:       MEDIUM-HIGH (yeni capability + evaluation engine + versioned policy)
Agent Lane:       AL-SCMM-ELIGIBILITY (DEV) · Target Agent: backend-architect
Branch:           feature/structured-content-messaging · Expected HEAD: 60bc00c5
Persistence:      L3 (policy versioned; EntityBase.Version)
```

## Ölçülmüş girdi
- Eligibility/policy scaffold **yok** — greenfield. **İlk CAND-CAP-0011 kodu.**
- **Desen:** MOD-0029 effectiveness resolver (`ResolveDocumentEffectivenessHandler` + `IControlledDocumentEffectivenessPort`, disjoint `Effective/Blocked/Unresolved`, **fail-closed**) — eligibility bunu birebir izler (tutarlılık).
- Context girdileri hazır: **AudienceProfile çok-eksen** (AUD, Dimensions), **MOD-0290 product** (canonical).

## Kapsam (eligibility çekirdeği)
1. **Policy aggregate (RM4):** versioned eligibility policy — `Conditions` context boyutları üzerinde (audience axes [AUD Dimensions], product [MOD-0290 ref], market, channel, language, period). Alanlar: code, name, version, status, conditions[], effective window. CAND-CAP-0011 owned, CrmService'te.
2. **Eligibility evaluator (docx C2):** `ResolveEligibilityQuery(context, pinnedSelections)` + handler → **disjoint** per-item/overall `{ Eligible | Blocked(reason) | Unresolved, blockingLevel, policyVersion }`. **Kaydeder:** input context + result + reasons + policyVersion (RM4 eval log).
3. **Fail-closed:** eksik zorunlu context = **Unresolved** (permission DEĞİL); herhangi Blocked/Unresolved → RED; **altyapı hatası fırlatılır, Unresolved'a çevrilmez** (MOD-0029 deseni). Business exception = accountable decision+reason+validity+audit (docx §1) — bu WP'de exception KAYDI şekli, tam workflow değil.
4. **In-process port** (`IEligibilityEvaluationPort`) — MOD-0029 port deseni; HTTP uç = SCMM-11 follow.

## Frozen model uyumu (DEC-SCMM-03)
- **D3a:** eligibility = **PERMISSION** boyutu — meaning (concept) ve presentation (template) DEĞİL; ayrı policy dimension.
- **RM4:** versioned eligibility; eval input/result/reasons/policy-version loglar. (Evidence/rights/validity/review policy boyutları additive follow — bu slice eligibility çekirdeği.)
- **D8 açıklama:** eligibility evaluation **kasıtlı bir değerlendirme** (C2 gereği) — D8 "no-engine" concept foundation için; eligibility motoru izinli AMA deterministik + kayıtlı + fail-closed olmalı. **Segment membership HESAPLAMAZ** (o MOD-0167); içerik ilerletmez (execution). Yalnız context↔policy değerlendirir.
- Sektör-nötr: policy condition değerleri config/ref (AUD axis kodları, product ref) — hardcoded sektör değeri yok.

## Acceptance
- E2: build temiz; unit — policy versioned CRUD/publish; evaluator disjoint (Eligible/Blocked+reason/Unresolved); eksik-context→Unresolved (permission değil); repo-throws→propagate (fail-closed, MOD-0029 vacuity deseni); eval-log input/result/reasons/policyVersion; deterministik (aynı context+policy → aynı sonuç); port==query (tek resolver).
- **Regresyon:** tam CrmService.Application.Tests — **bilinen pre-existing PII flake hariç** yeni fail YOK.
- E4 (fleet WARM): canonical authz 200/403; policy create/publish + evaluate; audit. Authenticated → operatör tokenı.
- Kapsam: yalnız yeni CAND-CAP-0011 eligibility (Policy+evaluator+port). Segment membership/execution YOK; başka aggregate/modül YOK; MOD-0162 knowledge'a dokunma (yalnız ref oku).

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-11 · Prompt P-SCMM-11 v1.0  (Context & eligibility evaluation — CAND-CAP-0011 ilk kod, CrmService)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 60bc00c5 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-11-eligibility-evaluation.md (bu WP)
2. docs/decisions/DEC-SCMM-03-model-policy-freeze.md (D3a permission-dimension, RM4 policy, D8, sektör-nötr)
3. DESEN: services/Diten.Platform/.../DocumentManagementMasterRegister/Handlers/QueryHandlers/ResolveDocumentEffectivenessHandler.cs
   + Models/DocumentEffectivenessModels.cs + Services/IControlledDocumentEffectivenessPort.cs (disjoint result + fail-closed)
4. services/Diten.CrmService/.../Domain/Entities/AudienceProfile.cs (AUD Dimensions — context girdisi)

BOUNDARY: İlk CAND-CAP-0011 (Marketing owner) kodu. Ayrı capability namespace (ör. Features/ContentComposition/Eligibility);
  MOD-0162 knowledge'dan AYRIK. Ayrı microservice AÇMA — mevcut CrmService deployment'ında (docx §Recommended direction).

NE:
 1) Policy aggregate (RM4, versioned): eligibility policy — Conditions context boyutları üzerinde (audience axes = AUD
    Dimensions, product = MOD-0290 ref, market, channel, language, period). code/name/version/status/conditions[]/effective.
    Versioned + publish (MOD-0162 ChainTemplate freeze desenine benzer opsiyonel). L3 + EntityBase.Version.
 2) Eligibility evaluator (docx C2): ResolveEligibilityQuery(context, pinnedSelections) + handler → DISJOINT
    { Eligible | Blocked(reason) | Unresolved, blockingLevel, policyVersion }. input+result+reasons+policyVersion LOGLA (RM4).
 3) Fail-closed (MOD-0029 deseni): eksik zorunlu context = Unresolved (permission DEĞİL); altyapı hatası FIRLATILIR,
    Unresolved'a çevrilme; hepsi Eligible değilse RED. Sessiz varsayılan YOK.
 4) In-process port IEligibilityEvaluationPort (MOD-0029 port deseni). HTTP uç = ayrı follow.
NASIL: MOD-0029 effectiveness resolver'ı birebir örnek al (query+handler+port, disjoint, fail-closed). Canonical authz
       (yeni crm.* eligibility key veya uygun mevcut — S1 deseni; belirsizse DUR). new-aggregate class-map/GUID trap.
YAPMA: segment membership HESAPLAMA (MOD-0167); içerik ilerletme/execution; MOD-0162 knowledge aggregate'lerini DEĞİŞTİRME
       (yalnız ref oku); sektöre özgü değer GÖMME (config/ref); ayrı microservice; global serializer; başka modül.
DOĞRULA (E2 + E4 fleet):
 - build temiz; unit: policy versioned · evaluator disjoint (Eligible/Blocked+reason/Unresolved) · eksik-context→Unresolved ·
   repo-throws→propagate (fail-closed vacuity) · eval-log alanları · deterministik · port==query.
 - TAM CrmService.Application.Tests: bilinen pre-existing ContactLocationPii flake HARİÇ yeni fail YOK.
 - E4 (fleet WARM): authz 200/403 · policy create/publish + evaluate. Authenticated cold → §26 operator.
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: canonical authz key belirsizse · policy/eval sözleşmesi RM4 ile çelişirse · kapsam eligibility çekirdeği dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-10) → **ACCEPTED (E2)**
```text
Commit: 112f4d0b · Agent: PASS · Verification: PASS (CT scope+fail-closed+regresyon teyit) · CT: ACCEPTED · Evidence: E2 (E4 N/A — port-only slice)
```
- ✅ Scope: 19 dosya, hepsi `Features/ContentComposition/Eligibility` + EligibilityPolicy entity + repo + DI + test — **MOD-0162 knowledge aggregate'i DOKUNULMAMIŞ**; segment membership/execution YOK (yalnız yorum-referansı).
- ✅ **Fail-closed gerçek:** policy read try/catch'siz (propagate, L59-60); missing-required→Unresolved(context); not-published/effective→Unresolved(policy); try/catch yalnız log-writer'da (fail-soft logging — doğru).
- ✅ İlk CAND-CAP-0011 kodu; ayrı capability namespace; ayrı microservice yok (docx uyumlu). Disjoint sonuç (Eligible/Blocked/Unresolved) MOD-0029 desenini izliyor. D3a permission boyutu.
- ✅ CT kendi koşumu: eligibility 14/14; **tam suite 1645 passed / 5 skipped / 0 failed** (1631→1645) — regresyon yok.
- ⏳ **E4 = N/A bu slice** (port-only, HTTP uç yok). authz key'ler tanımlı (`crm.eligibility.*`) ama seed edilmedi → HTTP surface + RBAC seed + E4 authenticated = **SCMM-11 follow** (S1 deseni).

## Kalan (bu WP dışı)
- **SCMM-11 follow:** HTTP uç (`eligibility:evaluate` + policy CRUD controller) + `crm.eligibility.*` RBAC seed/grant (S1 deseni) → E4 authenticated.
- **SCMM-11-UI** (frontend): policy authoring + eligibility test/preview.
- **HTTP uç** (eligibility:evaluate) — SCMM-11 follow / SCMM-14 tüketimi.
- RM4 evidence/rights/validity/review policy boyutları (additive).
- **E4 authenticated** turu.
