# WORK PACKAGE — WP-SCMM-12 · Reusable components + Claims

> **Control Tower kaydı (SoR).** SCMM R1 content band, gate G2. Bağlı: SCMM-09 ✅ · SCMM-11 ✅ (eligibility/policy deseni) · SCMM-11-AUD ✅. Owner: content domain lead. Module: **CAND-CAP-0011** (Claim) + **MOD-0162** (KnowledgeContent component, reuse).
> **Boundary (SCMM-03 D02b/c):** Claim → **CAND-CAP-0011** (ContentComposition namespace, Eligibility'nin yanına); Component = **KnowledgeContent reuse** (MOD-0162, yeniden kurulmaz); shared evidence → **MOD-0031** (ref-only, resolution ertelendi).

## Metadata
```text
WP ID:            WP-SCMM-12
Prompt ID:        P-SCMM-12 · v1.0
Task Class:       New aggregate (Claim) + component-reuse wiring — state-changing
Golden-Flow Profile: B (backend); UI = SCMM-12-UI follow
Risk Class:       MEDIUM (yeni Claim aggregate + approval lifecycle; evidence C1-blocker ref-only)
Agent Lane:       AL-SCMM-CLAIM (DEV) · Target Agent: backend-architect
Branch:           feature/structured-content-messaging · Expected HEAD: 9bd9d17b
Persistence:      L3 (Claim versioned; EntityBase.Version)
```

## Ölçülmüş girdi
- Claim **greenfield** (yok).
- **KnowledgeContent D02c reuse uygun:** `ContentVersion` (iş versiyonu) + `LanguageCode` + context ref'ler (Subject/Topic/AudienceProfile/ConceptNode/Product/Brand…) mevcut → **component = KnowledgeContent** (reuse, yeniden kurulmaz).
- **MOD-0031 evidence = review/planned (spec-only)** → **C1-evidence blocker** (SCMM-04/SCMM-07): claim evidence-ref'leri **opak string sakla; MOD-0031 resolution ertele** (K12 — MOD-0031 contract UYDURMA).
- Desen: SCMM-11 eligibility (versioned aggregate + publish/freeze + disjoint/fail-closed) + MOD-0162 audit seam.

## Kapsam
1. **Claim aggregate (CAND-CAP-0011, docx §4):** governed wording (`ClaimText`), `Qualifiers`, **applicability** (context dims: product/market/audience refs — opsiyonel eligibility-policy ref), **evidence references** (`EvidenceRefs: List<string>` — OPAK; MOD-0031 resolution ertelendi), versioned (`ClaimCode`+`ClaimVersion`) + **approval lifecycle** (draft→approved; "claim approval ≠ assembly approval" — assembly onayı SCMM-15/17). CRUD + List/Get + repo + class-map + audit.
2. **Component reuse (D02c):** KnowledgeContent'i **reusable component record** olarak belgele/wire et — yeni component aggregate KURMA; SCMM-14 assembly bunları tüketir. (Bu slice'ta KnowledgeContent'e dokunmadan, claim↔component ilişki şekli tanımlanır; derin assembly SCMM-14.)
3. **Evidence blocker:** EvidenceRefs opak saklanır; validation/resolution MOD-0031 hazır olunca ayrı follow. Fail-closed: claim approve, evidence-ref'lerin GERÇEKTEN var olduğunu bu slice'ta doğrulayamaz (MOD-0031 yok) → bu bilinen sınır, kayıtlı.

## Frozen model uyumu (DEC-SCMM-03)
- **D02b:** claim → CAND-CAP-0011; evidence → MOD-0031 (ref-only).
- **D3a:** claim = governed **content/meaning** artifact; eligibility (permission) ve template (presentation) ayrı — claim onayı assembly'yi otomatik onaylamaz.
- **D8:** yalnız veri + approval lifecycle — otomatik assembly/execution YOK.
- Sektör-nötr: qualifiers/applicability değerleri config/ref.

## Acceptance
- E2: build temiz; unit — Claim versioned CRUD/publish/approval; approval-freeze (approved claim değişimi → yeni versiyon); duplicate-code 409; evidence-refs opak round-trip; applicability persist; audit event (entityType Claim, uygun SourceModule); class-map GUID trap.
- **Regresyon:** tam CrmService.Application.Tests — bilinen pre-existing PII flake HARİÇ yeni fail YOK.
- E4 (fleet): authz 200/403 + claim create/approve — authenticated → follow (HTTP uç + seed).
- Kapsam: yalnız Claim (+audit) + component-reuse wiring. KnowledgeContent aggregate DEĞİŞMEZ (yalnız ref/wire); eligibility/knowledge aggregate'leri değişmez; MOD-0031 contract uydurulmaz; başka modül yok.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-12 · Prompt P-SCMM-12 v1.0  (Reusable components + Claims — CAND-CAP-0011)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 9bd9d17b · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-12-components-claims.md (bu WP)
2. docs/decisions/DEC-SCMM-03-model-policy-freeze.md (D02b claim owner, D02c component reuse, D3a, D8)
3. DESEN: services/Diten.CrmService/.../Features/ContentComposition/Eligibility/* (versioned aggregate + publish + fail-closed + audit + port; yeni Claim buna komşu)
4. services/Diten.CrmService/.../Domain/Entities/KnowledgeContent.cs (component reuse — ContentVersion/LanguageCode/refs)

BOUNDARY: Claim → CAND-CAP-0011 (Features/ContentComposition/Claims, Eligibility'nin yanı). Component = KnowledgeContent
  REUSE (MOD-0162 — DOKUNMA, yalnız ref). Shared evidence → MOD-0031 (ref-only; MOD-0031 review/planned = C1-blocker).

NE:
 1) Claim aggregate (CAND-CAP-0011, versioned): ClaimText (governed wording) + Qualifiers + applicability (product/market/
    audience refs, opsiyonel eligibility-policy ref) + EvidenceRefs (List<string>, OPAK — MOD-0031 resolution ERTELE) +
    ClaimCode/ClaimVersion + approval lifecycle (draft→approved; approved değişimi → yeni versiyon). CRUD+List+Get+repo+
    class-map (GUID trap) + audit (entityType Claim, uygun SourceModule/reason).
 2) Component reuse (D02c): KnowledgeContent'i reusable component olarak wire/belgele — YENİ component aggregate KURMA;
    KnowledgeContent'e DOKUNMA. Derin assembly SCMM-14.
NEDEN: docx §4 Claim (governed wording + evidence refs); component = KnowledgeContent reuse; SCMM-14 assembly bunları tüketir.
NASIL: SCMM-11 eligibility aggregate/publish/audit desenini örnek al; L3 + EntityBase.Version. EvidenceRefs opak sakla
       (MOD-0031 contract UYDURMA); "claim approval ≠ assembly approval" (assembly onayı SCMM-15/17).
YAPMA: MOD-0031 evidence contract uydurma/entegre etme (ref-only); KnowledgeContent/eligibility/knowledge aggregate DEĞİŞTİRME;
       otomatik assembly/execution (D8); sektöre özgü değer gömme; ayrı microservice; global serializer; başka modül.
DOĞRULA (E2 + E4 fleet):
 - build temiz; unit: Claim versioned CRUD/approval · approval-freeze→yeni versiyon · duplicate-code 409 · evidence-refs
   opak round-trip · applicability persist · audit · class-map.
 - TAM CrmService.Application.Tests: bilinen ContactLocationPii flake HARİÇ yeni fail YOK.
 - E4 (fleet): authz 200/403 + claim create/approve — HTTP uç + seed follow'da; bu slice port/handler.
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: evidence contract'ı MOD-0031'siz zorlanıyorsa (ref-only kal, DUR) · claim/assembly onay sınırı belirsizse · kapsam Claim+component-reuse dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- **SCMM-12-UI** (frontend): claim authoring + component picker.
- **Evidence resolution follow** — MOD-0031 hazır olunca EvidenceRefs validation.
- **HTTP uç + RBAC seed** + E4 authenticated.
- **SCMM-13** (iki dil varyant) · **SCMM-14** (Content Studio ④⑤⑥ assembly — component+claim+template tüketir).
