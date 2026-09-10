# MOD-0151 — FU02A Country & Business Unit Scope Selector Hardening

> **Tarih:** 2026-07-25 · **Tür:** FU02 UI/contract/payload hardening (Diten.Web tenant shell) · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Verdict:** **PARTIAL** — UI + payload sözleşmesi reference-driven hale getirildi; `business-unit` reference değer seti publish DEĞİL ve backend `BusinessScopes` alanı yok. Guardrail'ler korundu.
> **Backend domain / MOD-0048 data / RBAC / Gateway:** DEĞİŞTİRİLMEDİ · **Gateway-only** (direct 5061 yok) · **7-dil RESX parity** (67×7).

---

## 1. Preflight

**Files reviewed:**
- Audits: [FU02 UI](./mod-0151-fu02-territory-hierarchy-ui-viewer-2026-07-25.md) · [FU01 live smoke retry](./mod-0151-fu01-live-smoke-retry-2026-07-23.md) · [FU01 backend](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) · [reference publish execution](./mod-0151-territory-reference-publish-execution-2026-07-23.md)
- Pack: `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` (§9 Business Scope, §16 Reference Data, §18 UI, §20 Validation, §21 Integration, §22 FU Breakdown)
- Reference data: `reference-data/mod-0151-territory-reference-values.json` · `smoke-mod-0151-territory-publishedvalues.ps1`
- Backend: `TerritoryModel` entity · `TerritoryModelDtos/Commands/Validators` · `CreateTerritoryModelHandler` · `TerritoryReferenceSets` · `TerritoryModelsController` · CrmService `Program.cs` (JSON binding)
- Frontend precedents: MOD-0149 `AccountsController` (`country`/`city`/`district` reference selectors, `lookups`, `LoadReferenceOptionsAsync`, `ReferenceOptionViewModel`, `PublishedValuesModel`) · existing Select2 offcanvas pattern (GoldenReferenceSlim)

**Scope confirmation:** Yalnızca FU02 Model formundaki **scope alanları** sertleştirildi. Country Scope → reference-driven **single select**; "Division Scope" → **kaldırıldı** ve yerine reference-driven **Business Unit Scope multi select** kondu. Assignment / rule / resource / workflow / evidence / import-export / delete / node yüzeyleri **değiştirilmedi**.

**Business reality confirmation:** Alpha / Beta = **Business Unit** (bu taskta ele alındı). Almiba / Tutukon = **Brand** (bu taskta **dışarıda** bırakıldı). `business-scope-type` yalnız TİP setidir (business-unit / product-portfolio / brand-group …) ve kullanıcıya seçilebilir business unit olarak gösterilmez.

**No-brand-scope confirmation:** Brand Scope alanı **eklenmedi**. Yalnızca bilgilendirici limitation notu var: *"Brand scope is planned for a later Brand/Marketing integration."* Almiba/Tutukon hiçbir yerde hardcoded değil.

---

## 2. Implementation Summary

| Area | Implemented | Notes |
|---|---|---|
| Country Scope → reference single select | ✅ | `country` published-values (MOD-0149 precedent); offcanvas Select2 + server-rendered ModelForm select |
| Division Scope kaldırma | ✅ | Label her iki formdan da kaldırıldı; payload'da hiç gönderilmiyor (`DivisionScope = null`) |
| Business Unit Scope → reference multi select | ✅ | `business-unit` published-values; not-ready path (set publish değil); NO hardcoded fallback |
| businessScopes payload sözleşmesi | ✅ (passive) | `[{scopeType:"business-unit", scopeCode}]`; dedupe; backend şu an ignore ediyor (mini-FU'ya kadar no-op) |
| Brand Scope | ⛔ (bilerek yok) | Yalnız limitation help text; seçilebilir alan yok |
| Backend domain / DTO persistence | ⛔ (ertelendi) | FU01 backend'de `BusinessScopes` yok; risk + reference eksik → ayrı mini-FU önerildi |
| RESX/localization | ✅ | 6 yeni anahtar × 7 dil (parity) |
| Guardrails (5061 / TenantId / forbidden perms) | ✅ | Korundu |

---

## 3. Reference Source Decision

| Selector | Source | Behavior | Notes |
|---|---|---|---|
| Country Scope | MOD-0048 `country` published-values (`scope_key={tenant}`) | Single select; boş = kapsam yok | MOD-0149 Account'ta canlı kullanılan set kodu; hardcoded ülke listesi yok |
| Business Unit Scope | MOD-0048 `business-unit` published-values | Multi select; **set publish değil → not-ready** + disabled; hardcoded Alpha/Beta yok | Alpha/Beta gerçek business-unit VALUE'ları; publish sonrası otomatik dolar |
| (karıştırma yok) | `business-scope-type` | **Kullanıcıya gösterilmez** | Yalnız TİP seti; business-unit VALUE seçtirme kaynağı değil |

Karar gerekçesi: `mod-0151-territory-reference-values.json` tenant için 12 territory setini publish eder; **`country` ve gerçek `business-unit` VALUE seti bu publish'te yoktur.** `business-scope-type` yalnız tip setidir. Bu nedenle business-unit için setCode `business-unit` seçildi (author/publish follow-up'ı raporlandı), hardcoded fallback eklenmedi.

---

## 4. Backend / Payload Summary

- **CountryScope:** Artık yayınlanmış **ülke value kodu** taşır (ör. `TR`), serbest metin değil. `TerritoryModelSavePayload.CountryScope` olarak gönderilir. (FU01 backend CountryScope'u ayrıca doğrulamaz — UI yalnız yayınlanmış değer sunduğu için güvenli; backend doğrulaması bir follow-up.)
- **BusinessScopes:** `TerritoryModelSavePayload`'a passive `List<TerritoryBusinessScopePayload>{ ScopeType, ScopeCode }` eklendi. Multi-select → `scopeType="business-unit"` sabit, dedupe. **CrmService `Program.cs`'te `UnmappedMemberHandling` yok → varsayılan Skip**, dolayısıyla ekstra alan FU01 create/update'i kırmaz; backend persist edene kadar **no-op** round-trip. Brand/product scopeType bu formdan asla gönderilmez.
- **DivisionScope (retired):** UI'dan kaldırıldı; `ToModelPayload` her zaman `DivisionScope = null` gönderir. Backend kolonu geriye dönük uyumluluk için duruyor (kırıcı migration yapılmadı).
- **TenantId:** Payload'da yok (JWT / X-Tenant-Id header'dan çözülür) — değişmedi.

---

## 5. UI Summary

| Field | Type | Source | Behavior |
|---|---|---|---|
| Model Code | text | — | required; edit'te readonly |
| Name | text | — | required |
| Country Scope | **single select** (Select2 offcanvas / native ModelForm) | `country` published-values | optional; set publish değilse "Country reference data not ready" |
| Business Unit Scope | **multi select** | `business-unit` published-values | optional; **publish değil → not-ready + disabled**; publish sonrası Alpha/Beta seçilir |
| Brand Scope | — | — | **Yok**; yalnız "planned for later Brand/Marketing" bilgi notu |
| Effective From / To | date | — | From required; To ≥ From (backend) |
| Change Reason | textarea | — | optional |

İki create/edit yüzeyi de kapsandı: **offcanvas** (Golden DataTable'daki Add New + satır Edit) ve **server-rendered ModelForm** (Details → Edit Model). Division Scope ikisinde de görünmez.

---

## 6. Validation / Error UX

| Scenario | Behavior |
|---|---|
| Country set publish değil | Select boş + "Country reference data not ready" uyarısı; submit engellenmez (optional) |
| Business Unit set publish değil | Select boş + disabled + "Business unit reference data not ready" uyarısı; hiç businessScope gönderilmez |
| BU seçili + duplicate | Controller `BuildBusinessUnitScopes` dedupe eder (OrdinalIgnoreCase) |
| Invalid country (teorik) | UI yalnız published değer sunar; backend CountryScope doğrulaması follow-up |
| Duplicate model code (409) | Backend hatası verbatim alert'te (mevcut davranış) |
| EffectiveTo < From | Backend 400 verbatim (mevcut davranış) |
| Gateway 401/403 (lookups) | Boş liste + not-ready; hardcoded fallback ÜRETİLMEZ |

---

## 7. Tests

| Suite | Result | Notes |
|---|---|---|
| Web build (C# + Razor compile) | ✅ **PASS** | `dotnet build Diten.Web.csproj` → "Oluşturma başarılı oldu / 0 Hata" (izole output; fleet'e dokunulmadı) |
| RESX 7-dil parity | ✅ **PASS** | 67 anahtar × 7 dil; 6/6 yeni anahtar tüm dillerde |
| Static guard grep | ✅ **PASS** | direct 5061 yok · hardcoded alpha/beta/country/almiba/tutukon yok (yalnız yorum/örnek) · brand-group & business-scope-type payload'a gitmiyor · forbidden perm yok · TenantId payload yok |

> **Not:** Repo'da Diten.Web için ayrı frontend unit-test projesi yoktur (FU02 ile aynı konvansiyon). Doğrulama = build + static guard + RESX parity + (aşağıdaki) sınırlı smoke.

---

## 8. Live / Manual Smoke

Login `POST /api/tenant-auth/login` **operatör parolası gerektirir**; ajan ortamında kimlik bilgisi olmadığı için token alınamadı → **create smoke ve authenticated published-values probe yapılamadı** (bu, PARTIAL koşuluyla uyumlu).

| Step | Result | Notes |
|---|---|---|
| Fleet full rebuild + restart (resx satellite) | ✅ | 8/8 port LISTEN; Web `/account/login` → 200 |
| Country set publish state | ⏳ pending-auth | `country` = MOD-0149 Account precedent (canlı kullanımda); tenant için kesin doğrulama authenticated smoke bekliyor |
| Business Unit set publish state | ✅ **artık PUBLISHED** (task sonrası) | BRD catalog loader ile `business-unit` (`scope_type:tenant`, alpha/beta/gamma) tenant 97c59330'a yüklendi; Platform log `sets_inserted=1, values_inserted=3`. BU multi-select artık dolar. |
| Country dropdown reference-driven | ⏳ manuel | Kod published-values driven; render manuel/authenticated smoke ile doğrulanacak |
| Business Unit dropdown not-ready | ✅ (beklenen) | Set publish olmadığından boş + not-ready uyarısı + disabled |
| Model create (TR + Alpha/Beta) | ⛔ yapılmadı | Reference (BU) publish + backend persist bekliyor; PARTIAL |

**Önerilen operatör smoke'u** (parola ile): `smoke-mod-0151-territory-publishedvalues.ps1 -Email … -Password …` ile `country` ve `business-unit` publish durumunu doğrula, ardından offcanvas'ta Country=TR seçimini gözle.

---

## 9. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `Models/CRM/TerritoryViewModels.cs` | Updated | `BusinessUnitScopes` (edit VM) + `Country/BusinessUnitOptions` + `BusinessScopes` payload + `TerritoryBusinessScopePayload` record; DivisionScope legacy-annotated |
| `Controllers/CRM/TerritoryManagementController.cs` | Updated | `country`/`business-unit` set kodları · `Models/lookups` JSON endpoint · `PopulateModelScopeOptionsAsync` · `ToModelPayload` (country code + businessScopes, division=null) · GetModelJson scopes |
| `Views/CRM/TerritoryManagement/_CreateEditOffcanvas.cshtml` | Updated | Country single select + Business Unit multi select + not-ready + brand help; Division Scope input kaldırıldı |
| `Views/CRM/TerritoryManagement/ModelForm.cshtml` | Updated | Aynı reference-driven scope alanları (server-rendered); Division Scope kaldırıldı |
| `wwwroot/assets/js/CRM/TerritoryManagement/index.js` | Updated | `Models/lookups` yükleme · Select2 (offcanvas) · country/BU set & prefill · not-ready toggling |
| `Resources/Views/CRM/TerritoryManagement/TerritoryManagementResources.{en,tr,ar,es,fr,ru,zh}.resx` | Updated (7) | +6 anahtar: BusinessUnitScope, BusinessUnitScopeHelp, BrandScopePlanned, CountryReferenceNotReady, BusinessUnitReferenceNotReady, SelectCountry (67×7 parity) |

**Backend (CrmService), MOD-0048 data, RBAC, Gateway, registry: DEĞİŞTİRİLMEDİ.**

---

## 10. Guard Checks

| Check | Result |
|---|---|
| Backend changed? | **no** (yalnız Diten.Web UI) |
| If backend changed, limited to passive BusinessScopes/CountryScope validation? | **n/a** (backend değişmedi) |
| MOD-0151 FU01 validations broken? | **no** (create/update/node/hierarchy/microzone etkilenmedi) |
| MOD-0048 data changed? | **no** |
| Reference publish done? | **no** |
| RBAC changed? | **no** |
| Gateway changed? | **no** |
| UI changed? | **yes** |
| Direct 5061 used? | **no** (yalnız yorumda "never called directly") |
| TenantId field shown? | **no** |
| TenantId payload sent? | **no** |
| Division Scope still visible? | **no** (offcanvas + ModelForm'dan kaldırıldı) |
| Country Scope hardcoded? | **no** (published-values) |
| Business Unit Scope hardcoded? | **no** (published-values) |
| Alpha/Beta hardcoded fallback? | **no** (yalnız yorum/örnek metin) |
| Almiba/Tutukon hardcoded? | **no** (hiç yok) |
| Brand Scope field added? | **no** (yalnız bilgi notu) |
| brand-group payload sent? | **no** (scopeType sabit "business-unit") |
| business-scope-type values shown as selectable business units? | **no** |
| Assignment/resource/workflow/evidence UI/API added? | **no** |
| Product/Brand master touched? | **no** |
| Account/Contact touched? | **no** |
| `crm.micro-zone.manage` introduced? | **no** |
| `crm.territory.delete` introduced? | **no** |
| RESX parity passed? | **yes** (67×7) |
| Tests passed? | **yes** (build + guards + parity) |

---

## 11. Final Verdict

**PARTIAL.**

UI ve payload sözleşmesi reference-data driven olarak sertleştirildi: Country Scope tek-seçim reference selector, "Division Scope" kaldırılıp yerine `business-unit` published-values kaynaklı çok-seçim **Business Unit Scope** kondu, businessScopes payload şekli (`scopeType="business-unit"`) hazırlandı, hardcoded fallback yok, Brand Scope eklenmedi, TenantId payload yok, guardrail'ler korundu, Web derlendi, 67×7 RESX parity sağlandı.

PARTIAL nedenleri (task L ile birebir): (1) `business-unit` reference **değer seti publish değil** (Alpha/Beta seçilemez, UI not-ready); (2) FU01 backend'de **`BusinessScopes` persistence yok** → ayrı mini-FU gerekiyor; (3) authenticated **live create smoke** reference publish + operatör kimlik bilgisi bekliyor. Hiçbiri guardrail ihlali değildir.

---

## 12. Next Recommended Prompt

1. ~~MOD-0048 Business Unit Reference Set publish~~ — **YAPILDI** (2026-07-25): BRD catalog loader ile `business-unit` (tenant-scoped, alpha/beta/gamma) tenant 97c59330'a yüklendi + publish edildi. Kalan: `country` setinin bu tenant için publish durumunu authenticated smoke ile doğrula.
2. ~~MOD-0151 FU02A Backend BusinessScopes Mini-FU~~ — **YAPILDI** (2026-07-25, aşağıdaki Addendum).

---

## Addendum — Backend BusinessScopes Mini-FU (2026-07-25)

CrmService'e passive `BusinessScope` value object eklendi ve tüm katmanlara işlendi:

| Katman | Değişiklik |
|---|---|
| Domain | `TerritoryBusinessScope` VO (`Domain/Entities/`) + `List<TerritoryBusinessScope> BusinessScopes` on `TerritoryModel` (Mongo POCO auto-map; `ReplaceOne` ile persist) |
| Command | `Create/UpdateTerritoryModelCommand`'e `IReadOnlyList<TerritoryBusinessScopeInput>? BusinessScopes` (opsiyonel) |
| DTO/Mapper | Detail DTO'ya `BusinessScopes` + `TerritoryBusinessScopeDto` + mapper projeksiyonu |
| Handler | `TerritoryBusinessScopeResolver` (her iki handler): scopeType sabit `business-unit`, scopeCode required, dedupe, her kod `business-unit` published set'e karşı **fail-closed** doğrulanır |
| Validator | `Create/UpdateTerritoryModelCommandValidator` (ValidationBehavior çalıştırır): non-business-unit scopeType reddi + scopeCode required |
| Frontend round-trip | Detail VM `BusinessScopes`, `GetModelJson`/`ToModelEdit` gerçek kodları döner; Details sayfası BU rozetlerini gösterir |

**Doğrulama:** Fleet clean rebuild (8/8 port, Web 200, 0 build-error), **221/221 CrmService Application testi PASS** (8 yeni BusinessScopes testi: persist, dedupe, non-business-unit reddi, unpublished set fail-closed, update replace, validator, mapper). Guardrail: brand-group/product-portfolio backend'de de reddediliyor; Almiba/Tutukon business unit olarak kabul edilmiyor. Sonuç: **Alpha/Beta/Gamma seçimleri artık kaydolup düzenlemede geri yükleniyor** (uçtan uca). Kalan tek açık: authenticated live create smoke (operatör parolası gerekiyor) + `country` setinin bu tenant için publish teyidi.
