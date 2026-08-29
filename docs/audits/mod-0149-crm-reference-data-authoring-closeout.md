# MOD-0149 — CRM Reference Set/Value Authoring Closeout (MOD-0048 / PSS-012)

**Date:** 2026-07-14 · **Type:** reference-data authoring closeout (report + operator template; **no code/seed**) · **Verdict:** PASS

## Amaç

MOD-0149 required lookup'ları `account-type` ve `account-status` başta olmak üzere CRM reference set/value authoring
kapsamını **implementation-ready** hale getirmek: set/value spesifikasyonu, operatör checklist ve import taslağı. Actual
UI/import authoring bir operatör aksiyonudur; bu task kod/seed **üretmez**.

## Engine gerçeği (doğrulandı)

- CRM iş reference set'lerinin SoR'u **PSS-012 Business Reference Data Stewardship** (`Diten.Platform` host):
  `BusinessReferenceDataSet` (`SetCode`, `Name`, `ScopeType`, `Status`) → `BusinessReferenceDataVersion` (`VersionNumber`,
  `Status`, embedded) → `BusinessReferenceDataValue` (**`ValueCode`, `DisplayName`, `IsDeprecated`, `ParentValueCode`,
  `SortOrder`, `Attributes`**). Hepsi `TenantScopedEntity`; `(TenantId, SetCode)` unique.
- **Governance flow:** create-set → add values → **validate → submit → approve → publish**. Consumer lookup:
  `GET /sets/{setCode}/published-values | values | hierarchy`. Import: `POST /imports/preview` + `POST /imports/{previewId}/commit`
  (CSV/JSON, `Idempotency-Key` zorunlu, idempotent commit).
- **Deprecation:** value silinmez → `IsDeprecated=true`. Published-values yeni seçim için deprecated'ı hariç tutar;
  historical değerler display'de kalır. → MOD-0149 validation contract'ı ile **birebir uyumlu**.
- **Steward permission:** `Platform.BusinessReferenceData.*` (Platform-owned stewardship); ERP consumer yalnız
  published-values okur. CRM bu set'leri **tüketir**, sahiplenmez.
- **Durum:** engine + UI mevcut (MOD-0048 impl-status Backend+Frontend ~90%, sets/versions/values + governance +
  12+ view wizard); PSS-012 pack governance'ı `draft` olarak resmileştiriyor. Operatör authoring **bugün UI'dan yapılabilir**.

## Final Required Set Decision

| Set Code | Required for MOD-0149 MVP? | Decision | Blocks implementation? |
|---|---|---|---|
| `account-type` | **Evet** | Author (PSS-012 tenant set) | **Evet** (required alan) |
| `account-status` | **Evet** | Author (PSS-012 tenant set) — **karar: MOD-0048 set** | **Evet** (required alan) |
| `account-category` | Hayır | Author (önerilen) | Hayır (optional alan) |
| `workplace-type` | Hayır | Author (önerilen) | Hayır |
| `workplace-category` | Hayır | Author (önerilen) | Hayır |
| `address-type` | Hayır | Author (önerilen) | Hayır |
| `status-reason` | Hayır | Author (önerilen) | Hayır |
| `city` | Hayır | Territory set — **MVP defer** | Hayır |
| `district` | Hayır | Territory set — **MVP defer** | Hayır |

## Authoring Values

Tam value tabloları (ValueCode/DisplayName/Description/Active/Notes) makine-okunur biçimde:
[mod-0149-crm-reference-data-authoring-template.json](mod-0149-crm-reference-data-authoring-template.json). Özet (ValueCode'lar, hepsi Active / `IsDeprecated=false`):

- **account-type:** organization · hospital · pharmacy · clinic · distributor · wholesaler · corporate-group · branch · other
- **account-status:** draft · active · inactive · suspended · archived
- **account-category:** strategic · key-account · standard · prospect · inactive · other
- **workplace-type:** hospital · pharmacy · clinic · health-center · warehouse · office · other
- **workplace-category:** public · private · university · chain · independent · specialty · other
- **address-type:** primary · billing · shipping · visiting · registered · other
- **status-reason:** duplicate · closed · merged · invalid-data · out-of-scope · customer-request · other
- **city / district:** defer (boş) — territory reference

Tüm SetCode + ValueCode'lar lowercase kebab-case, stable, magic number yok. DisplayName template'te `en`; publish öncesi
7 dil (en, fr, es, zh, ar, ru, tr) localize edilir.

## Account Status Decision

- **Neden MOD-0048 set?** Business status kullanıcı seçimi + tutarlılık + 7-dil l10n + governance/audit gerektirir;
  hardcoded fallback yasaktır. Bu yüzden `account-status` **PSS-012 business reference set** olarak author edilir.
- **MOD-0149 internal enum ne için?** Yalnız **teknik lifecycle** (state-machine transition guard'ları, kod içi switch)
  gerekiyorsa internal enum kullanılabilir; ancak **kullanıcı seçimi ve business validation** için `account-status` set'i
  tüketilir. İki katman çakışırsa set kaynağı otoritedir; enum yalnız teknik geçiş kontrolüdür.
- **Business status validation nasıl?** Create/update'te seçilen status `published-values` içinde ve `IsDeprecated=false`
  olmalı; değilse **400**. (Detay: Validation Contract.)

## Country / City / District Decision

- **country:** Platform `/api/lookups/countries` (20 hardcoded, PlatformActor-gated) **provisioning** amaçlı ve
  **authoritative territory reference değil**. CRM tenant tüketimi için territory business set tercih; MOD-0149'da country
  **optional** → MVP bloklamaz. Karar: mevcut provisioning lookup **canonical CRM kaynağı yapılmaz**.
- **city / district:** PSS-012 territory business set olarak **author edilebilir**, fakat MOD-0149'da optional alan
  oldukları için **MVP'de defer** edilir (alan boş bırakılabilir). Bağımlılık: district → city (`ParentValueCode` hierarchy).
- **CRM local seed:** **Hayır** — hiçbir territory/country/city/district CRM içine canonical taşınmaz (SoR = MOD-0048/PSS-012).

## Operator Authoring Checklist

| Step | Actor | Action | Expected Output |
|---|---|---|---|
| 1 | Reference Data steward (`Platform.BusinessReferenceData.*`) | `account-type` set oluştur (tenant scope) → template'ten value'ları gir/import | Draft version + values |
| 2 | steward | `account-status` set oluştur → value'ları gir | Draft version + values |
| 3 | steward | `account-category`, `workplace-type`, `workplace-category`, `address-type`, `status-reason` set'lerini oluştur | Draft versions + values |
| 4 | steward | Her set'i **validate → submit → approve → publish** | Published versions |
| 5 | steward / l10n | DisplayName'leri 7 dile localize et (publish öncesi/sonrası) | Localized values |
| 6 | steward / EA | `city`/`district` territory set kararı (author veya defer) | Karar kaydı |
| 7 | (opsiyonel) steward | Import ile: `POST /imports/preview` (template JSON/CSV) → gözden geçir → `POST /imports/{previewId}/commit` (Idempotency-Key) | Committed values |
| 8 | MOD-0149 impl (bu task DEĞİL) | Set code'ları consumer `published-values` üzerinden validator'da tüket | Validation binding |

> Import kullanılacaksa taslak: [mod-0149-crm-reference-data-authoring-template.json](mod-0149-crm-reference-data-authoring-template.json).
> Bu **operatör aidi**dir; runtime seed/migration değildir. Alan eşlemesi PSS-012 `/imports/preview` sözleşmesine göre kesinleşir.

## Validation Contract

| Durum | Beklenen |
|---|---|
| **Invalid value** (set/published-values'ta yok) | **400** + validator; kayıt reddedilir |
| **Inactive/deprecated value** (`IsDeprecated=true`) yeni kayıtta | **400** — yeni create/update için seçilemez |
| **Missing required set** (account-type / account-status publish edilmemiş) | create **bloklanır** (implementation blocker) |
| **Missing optional set** (category/workplace/address/status-reason yok) | alan boş geçilebilir; create bloklanmaz |
| **Historical inactive value** mevcut kayıtta | display korunur; edit'te değiştirmeye zorlanmaz |
| **Hardcoded fallback / CRM local seed** | **Yasak** — MOD-0048/PSS-012 tek kaynak |

## Changed Files

| File | Action | Status |
|---|---|---|
| docs/audits/mod-0149-crm-reference-data-authoring-closeout.md | Created (report) | ✅ |
| docs/audits/mod-0149-crm-reference-data-authoring-template.json | Created (operator import template; JSON valid) | ✅ |
| services/** · frontend/** · gateway/** · .antigravity/** · AGENTS.md · execution/registries/** · execution/portfolio/** · MOD-0149 pack · MOD-0048 runtime · seed/migration | Untouched | ⛔ |

## Verdict: PASS

Required `account-type` ve `account-status` (+ 5 önerilen set) için set/value spesifikasyonu, PSS-012 engine eşlemesi,
validation contract, operatör checklist ve import taslağı **tam ve implementation-ready**. Actual UI/import publish bir
operatör aksiyonudur (engine + UI bugün mevcut). MOD-0149 **ready-for-dev** için reference-data ön koşulu — publish
adımının operatöre net biçimde devredilmesiyle — **kabul edilebilir kapanış** sağlar. Gerçek `publish` yapılmadan
**implementation create/update validation** çalışmaz (Step 1–4 zorunlu).

## Next Task Recommendation

- **Operatör:** Step 1–4'ü (en az `account-type` + `account-status` publish) MOD-0048/PSS-012 governance UI'da tamamlasın.
- **Sonra:** Son ready-for-dev ön koşulu **Diten.CrmService scaffold** prerequisite task'ına geçilir.
- İkisi de kapandığında kullanıcı **açık onayıyla** MOD-0149 status `ready-for-dev` yapılabilir; `@orchestrator`
  implementation'ı ondan önce başlatılmaz.
