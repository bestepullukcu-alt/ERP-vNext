# WORK PACKAGE — WP-SEG-G · Eligibility channel/purpose parametrelerini katalog-güdümlü seçim yap (backend+frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`580d35be` üstü). **Backend (CrmService katalog) + frontend (form.js).** Owner: consent.eligibility koşulundaki `channel` + `purpose` bare text input; bunlar Consent & Preferences'taki gibi **dropdown/chip seçimli** olmalı (değerler `ConsentChannel.All`/`ConsentPurpose.All` vocabulary'sinden).

## Ölçülmüş girdi (CT)
- Katalog parametre modeli: `SegmentAttributeDefinition.RequiredParameters/OptionalParameters` = **yalnız `IReadOnlyList<string>` (isim)** — parametre başına value-source YOK. Attribute'un tek `ValueSource`'u var (ana değer = eligibility enum), parametreler için değil.
- consent.eligibility: `RequiredParameters = ["channel","purpose"]` (`ParameterChannel`/`ParameterPurpose`). Ana ValueSource = `ConsentEligibilityValues` (allowed/blocked/unknown/not_applicable enum).
- Vocabulary kaynağı: `ConsentVocabulary.Current` → `ConsentChannel.All` (Channels) + `ConsentPurpose.All` (Purposes); `GET /api/crm/consents/contract` ile expose (ConsentPreferences UI aynı kaynak). **Aynı CrmService — cross-service değil.**
- Frontend `form.js`: `parameterFields` bare `<input>` render ediyor (value-source farkındalığı yok).

## Kapsam
**1) Backend (katalog, additive):**
- `SegmentAttributeDefinition`'a **additive `IReadOnlyDictionary<string, SegmentAttributeValueSource>? ParameterValueSources`** (parametre adı → value-source; default null/boş). Mevcut `RequiredParameters`/`OptionalParameters` isim listeleri **DEĞİŞMEZ** (geriye uyumlu).
- `SegmentAttributeCatalog`: consent.eligibility'ye `ParameterValueSources = { channel → enum(ConsentChannel.All), purpose → enum(ConsentPurpose.All) }` (mevcut `SegmentAttributeValueSource` enum kind — eligibility status ile aynı desen). Değerler `ConsentChannel.All`/`ConsentPurpose.All`'dan (hardcode YOK). Diğer parametreler (maxDepth/subjectId) value-source'suz (bare kalır; zaten Phase-1'de gizli).
- Katalog response DTO'suna `ParameterValueSources` yansıt.

**2) Frontend (`form.js`, `parameterFields` render):**
- Parametre value-source'u VARSA → chip/dropdown (mevcut enum/reference value-source render mantığını reuse — eligibility ana değer chip'i gibi); YOKSA → bare input (mevcut davranış).
- Seçilen parametre değeri **aynı payload alanına** yazılır (parameters map değişmez; yalnız giriş kontrolü chip/dropdown).

## KORU / YAPMA
- Attribute code/operatör/ana value-source/subject-type DEĞİŞMEZ. `RequiredParameters` isim listesi geriye uyumlu (additive dict). Payload parametre gönderimi (parameters map key/value) DEĞİŞMEZ (yalnız giriş kontrolü). buildNodes blok→tree payload byte-identical. Katalog-güdümlülük (hardcode channel/purpose YOK — ConsentChannel.All/ConsentPurpose.All). Canlı reach (SEG-C)/proxy/SEG-D-E-F DEĞİŞMEZ. Consent domain'e yazma YOK (yalnız vocabulary oku). Başka modül.

## Acceptance
- **E2:** CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail. Katalog consent.eligibility parametreleri channel/purpose için value-source (ConsentChannel.All/ConsentPurpose.All) taşır; frontend bunları chip/dropdown render eder; value-source'suz parametreler bare input kalır; payload parameters map byte-identical (giriş kontrolü değişti, değer şekli değil). buildNodes payload byte-identical.
- **E4:** Eligibility koşulunda channel + purpose Consent & Preferences ile aynı vocabulary'den seçilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-G · Eligibility channel/purpose parametreleri katalog-güdümlü seçim (MOD-0167-FU02, backend+frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (580d35be üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-G-parameter-value-source.md · Features/Segmentation/Catalog/SegmentAttributeDefinition.cs + SegmentAttributeCatalog.cs (ConsentEligibility, ConsentEligibilityValues, ParameterChannel/Purpose) + katalog response DTO (GetSegmentAttributeCatalogHandler + SegmentModels) · Features/ConsentPreference/Contract/ConsentPreferenceContract.cs (ConsentChannel.All / ConsentPurpose.All) · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (parameterFields render).

NE:
 Backend (katalog additive):
 - SegmentAttributeDefinition'a additive IReadOnlyDictionary<string, SegmentAttributeValueSource>? ParameterValueSources (default null); RequiredParameters/OptionalParameters isim listeleri DEĞİŞMEZ.
 - SegmentAttributeCatalog consent.eligibility'ye ParameterValueSources = {channel→enum(ConsentChannel.All), purpose→enum(ConsentPurpose.All)} (mevcut SegmentAttributeValueSource enum kind; ConsentChannel.All/ConsentPurpose.All'dan, hardcode YOK). maxDepth/subjectId value-source'suz.
 - Katalog response DTO'ya ParameterValueSources yansıt.
 Frontend (form.js parameterFields):
 - Parametre value-source VARSA chip/dropdown (mevcut enum/reference value-source render reuse); YOKSA bare input. Seçilen değer aynı parameters map alanına yazılır (payload şekli değişmez).
KORU/YAPMA: attribute code/operatör/ana value-source/subject-type değişmez; RequiredParameters isim listesi geriye uyumlu (additive dict); payload parameters map + buildNodes byte-identical (yalnız giriş kontrolü); katalog-güdümlülük (hardcode channel/purpose YOK); canlı reach/proxy/SEG-D-E-F değişmez; consent domain'e YAZMA (yalnız vocabulary oku); başka modül; yeni CSS gerekmiyorsa ekleme.
DOĞRULA (E2): TAM CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail; katalog consent.eligibility channel/purpose value-source (ConsentChannel.All/ConsentPurpose.All) taşır; frontend chip/dropdown; value-source'suz parametre bare kalır; payload parameters map + buildNodes byte-identical. Ayrı commit. §22 TÜRKÇE. K13.
Durma: parametre value-source katalog contract'a eklenemiyorsa; ConsentChannel/Purpose.All erişilemiyorsa; payload parameters şekli değişmek zorundaysa (DUR); frontend parametre render value-source reuse edilemiyorsa; kapsam Segmentation+form.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```text
Commit: eee823e7 · Agent: PASS (--no-build) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-segg-verify @eee823e7
```
- ✅ **Scope:** 4 backend katalog dosyası (SegmentAttributeDefinition/Catalog/GetSegmentAttributeCatalogHandler/SegmentModels) + form.js. Segmentation dışına taşma yok; consent domain'e YAZMA yok (yalnız vocabulary okundu).
- ✅ **Additive:** `ParameterValueSources` `IReadOnlyDictionary<…>? = null` — `RequiredParameters`/`OptionalParameters` isim listeleri değişmedi. Response DTO'ya additive `SegmentAttributeValueSourceDto` map.
- ✅ **Katalog-güdümlü (hardcode YOK):** consent.eligibility channel→`Enum(ConsentChannel.All.ToArray())`, purpose→`Enum(ConsentPurpose.All.ToArray())` (MOD-0164 sabitleri, ConsentPreferences ile aynı vocabulary). maxDepth/subjectId value-source'suz (bare kalır).
- ✅ **Payload byte-identical:** buildNodes'ta kod diff yok; seçilen değer aynı `condition.parameters[name]` slotuna yazılır (yalnız giriş kontrolü chip/dropdown). Frontend value-source'suz parametre → bare input (mevcut davranış), yeni CSS yok.
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/0**; CrmService.Application.Tests ilk run 1739/1 → **rerun 1740/0/5** (tek fail env/sıra flake — SEG-C'deki ContactWorkbookExport flake deseni; değişiklikler additive/payload-safe, kalıcı kırık değil). Baseline-diff temiz.
- ⏳ **E4:** Eligibility koşulunda channel + purpose dropdown (Consent & Preferences ile aynı vocabulary).
