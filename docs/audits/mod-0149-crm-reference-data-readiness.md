# MOD-0149 — CRM Reference Data Readiness (MOD-0048)

**Date:** 2026-07-14 · **Type:** reference-data readiness (read-only + report; no code) · **Verdict:** PARTIAL

## Amaç

MOD-0149 Customer 360 / Account Hierarchy implementation'ı başlamadan önce create/update validation için gereken CRM
reference set'lerini MOD-0048 üzerinden readiness açısından değerlendirmek, set/value spesifikasyonunu ve operatör
authoring checklist'ini çıkarmak. **CRM içinde hardcoded lookup / local seed oluşturulmaz; SoR = MOD-0048.**

## MOD-0048 iki yüzey (kritik SoR ayrımı)

1. **Platform provisioning lookups** — `PlatformLookupItem : GlobalEntity` (`/api/lookups/*`, no TenantId). Country (20
   ülke hardcoded), currency, timezone, tenant-tier gibi **platform admin provisioning** vocabulary. Pack açıkça:
   *"countries remains Platform provisioning support, **not** Territory Reference ownership."*
2. **Business Reference Data engine** — sets/versions/values/mappings + draft→approve→publish governance + 12+ view UI
   (impl-status ~90%, `ready-for-dev`). Runtime `business_reference_data_*` collection'ları; domain id'ler
   `BusinessReferenceDataSetId/VersionId`. **CRM iş reference set'leri (account-type, workplace-type, …) buraya aittir.**

> Karar: CRM lookup'ları **Business Reference Data set** olarak author edilir (Platform provisioning lookup değil).
> Country/City/District ise **Territory Reference** business set'i olmalıdır; platform'un hardcoded country lookup'ı
> authoritative territory kaynağı değildir ve CRM'e canonical taşınmaz.

## MOD-0149'da required vs optional (pack §10)

- **MVP create için hard-required alanlar:** `AccountType` (→ account-type), `Status` (→ account-status). AccountName +
  AccountCode (auto-gen) lookup değildir.
- **Optional alan lookup'ları:** account-category, workplace-type, workplace-category, city, district, address-type, status-reason.
- **Sonuç:** MVP create validation'ı **yalnız account-type ve account-status** set'lerine sıkı bağımlıdır; diğerleri
  optional alan olduğu için authoring eksikse alan boş bırakılabilir (create bloklanmaz).

## Reference Set Readiness Matrix

| Set Code | Display Name | Required for MOD-0149? | Current Status | SoR | Action |
|---|---|---|---|---|---|
| `account-type` | Account Type | **Evet (required alan)** | Missing (author) | MOD-0048 Business Reference | **author** |
| `account-status` | Account Status | **Evet (required alan)** | Missing (author) | MOD-0048 Business Reference | **author** (veya MOD-0149 sabit enum — §Karar) |
| `account-category` | Account Category | Hayır (optional alan) | Missing (author) | MOD-0048 Business Reference | author |
| `workplace-type` | Workplace Type | Hayır (optional alan) | Missing (author) | MOD-0048 Business Reference | author |
| `workplace-category` | Workplace Category | Hayır (optional alan) | Missing (author) | MOD-0048 Business Reference | author |
| `address-type` | Address Type | Hayır (optional, önerilen) | Missing (author) | MOD-0048 Business Reference | author |
| `status-reason` | Account Status Reason | Hayır (optional, önerilen) | Missing (author) | MOD-0048 Business Reference | author |
| `country` | Country | Hayır (optional alan) | Found (platform provisioning, non-authoritative) | Territory Reference (MOD-0048) | **verify** — territory business set kararı |
| `city` | City | Hayır (optional alan) | Missing (author) | Territory Reference (MOD-0048) | author/defer |
| `district` | District | Hayır (optional alan) | Missing (author) | Territory Reference (MOD-0048) | author/defer |

## Proposed Values

Kurallar: `ValueCode` stable (magic number yok, kebab-case); `DisplayName` localization-ready (7 dil MOD-0048 l10n);
referenced value **hard delete edilmez → inactive/deprecated**; inactive value yeni kayıtta seçilemez, historical display korunur.

### account-type
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| organization | Organization | Genel kurumsal hesap | Evet | generic CRM |
| hospital | Hospital | Hastane | Evet | pharma workplace |
| pharmacy | Pharmacy | Eczane | Evet | pharma workplace |
| clinic | Clinic | Klinik | Evet | pharma workplace |
| distributor | Distributor | Distribütör | Evet | |
| wholesaler | Wholesaler | Toptancı | Evet | |
| corporate-group | Corporate Group | Holding / grup | Evet | hierarchy kökü |
| branch | Branch | Şube | Evet | child account |
| other | Other | Diğer | Evet | fallback |

### account-category
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| strategic | Strategic | Stratejik hesap | Evet | |
| key-account | Key Account | Anahtar hesap | Evet | |
| standard | Standard | Standart | Evet | default |
| prospect | Prospect | Aday | Evet | |
| inactive | Inactive | Pasif | Evet | |
| other | Other | Diğer | Evet | |

### workplace-type
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| hospital | Hospital | Hastane | Evet | |
| pharmacy | Pharmacy | Eczane | Evet | |
| clinic | Clinic | Klinik | Evet | |
| health-center | Health Center | Sağlık ocağı/merkez | Evet | |
| warehouse | Warehouse | Depo | Evet | |
| office | Office | Ofis | Evet | |
| other | Other | Diğer | Evet | |

### workplace-category
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| public | Public | Kamu | Evet | |
| private | Private | Özel | Evet | |
| university | University | Üniversite | Evet | |
| chain | Chain | Zincir | Evet | |
| independent | Independent | Bağımsız | Evet | |
| specialty | Specialty | Uzmanlık | Evet | |
| other | Other | Diğer | Evet | |

### address-type
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| primary | Primary | Birincil adres | Evet | default |
| billing | Billing | Fatura adresi | Evet | |
| shipping | Shipping | Sevk adresi | Evet | |
| visiting | Visiting | Ziyaret adresi | Evet | MOD-0155 tüketebilir |
| registered | Registered | Kayıtlı/resmi adres | Evet | |
| other | Other | Diğer | Evet | |

### account-status
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| active | Active | Aktif | Evet | default |
| inactive | Inactive | Pasif | Evet | |
| draft | Draft | Taslak | Evet | |
| suspended | Suspended | Askıda | Evet | |
| archived | Archived | Arşivlenmiş | Evet | |

### status-reason
| ValueCode | DisplayName | Description | Active? | Notes |
|---|---|---|---|---|
| duplicate | Duplicate | Mükerrer | Evet | |
| closed | Closed | Kapandı | Evet | |
| merged | Merged | Birleştirildi | Evet | |
| invalid-data | Invalid Data | Geçersiz veri | Evet | |
| out-of-scope | Out of Scope | Kapsam dışı | Evet | |
| customer-request | Customer Request | Müşteri talebi | Evet | |
| other | Other | Diğer | Evet | |

## Country / City / District Kararı

- **country:** Mevcut platform `/api/lookups/countries` (20 hardcoded, PlatformActor-gated) **provisioning** amaçlıdır ve
  authoritative territory reference **değildir**. CRM tenant-facing country tüketimi için **Territory Reference business
  set** (MOD-0048) tercih edilir. MOD-0149'da country **optional** olduğundan MVP'yi bloklamaz.
- **city / district:** Business territory reference olarak MOD-0048'de **yok**. Authoring gerekir; fakat MOD-0149'da
  optional oldukları için **MVP'de defer edilebilir** (alan boş bırakılır). CRM içinde **local city/district seed yapılmaz.**
- **CRM içinde local seed:** Hayır — hiçbir territory/country/city/district CRM'e canonical taşınmaz (SoR = MOD-0048/Reference).

## Validation Contract (MOD-0149 create/update)

| Durum | Beklenen davranış |
|---|---|
| **Invalid value** (set'te olmayan kod) | **400** + validator; kayıt reddedilir |
| **Inactive/deprecated value** yeni kayıtta | **400** — inactive value **yeni kayıt için seçilemez** |
| **Inactive value mevcut kayıtta** (historical) | Korunur — display edilir; edit'te değiştirilmeye zorlanmaz (historical display) |
| **Missing set** (henüz author edilmemiş) | Required set (account-type/account-status) yoksa → create **bloklanır** (readiness follow-up); optional set yoksa → alan boş geçilebilir |
| **Hardcoded fallback** | **Yasak** — CRM fallback/local list üretmez; MOD-0048 tek kaynak |

## Authoring Plan (operatör checklist — MOD-0048 governance UI, kod yok)

| Step | Actor | Action | Output |
|---|---|---|---|
| 1 | Reference Data operator (MOD-0048) | `account-type` set'ini oluştur + yukarıdaki value'ları gir | Published set |
| 2 | operator | `account-status` set'ini oluştur + value'lar (veya §Karar: MOD-0149 sabit enum) | Published set |
| 3 | operator | `account-category`, `workplace-type`, `workplace-category`, `address-type`, `status-reason` set'lerini oluştur | Published sets |
| 4 | operator / EA | Territory reference (country/city/district) business set kararı — author veya defer | Karar kaydı |
| 5 | operator | Her set'i draft→approve→**publish** akışından geçir; 7-dil DisplayName gir | Published + localized |
| 6 | MOD-0149 impl | Set code'ları `AccountModels`/validator'da referans al (bu task DEĞİL) | Validation binding |

**account-status Karar notu:** account-status küçük, sabit ve lifecycle-kritik olduğundan MOD-0149 içinde **sabit enum**
olarak da modellenebilir. Karar net değilse **"MOD-0048 set recommended"** — tutarlılık ve l10n için Business Reference
set tercih edilir; sabit enum seçilirse pack §10/§18 güncellenir.

## Blockers / Follow-ups

| Item | Blocks MOD-0149 ready-for-dev? | Blocks implementation? | Owner | Notes |
|---|---|---|---|---|
| `account-type` set authoring | Hayır (spec net) | **Evet** (required alan validation) | MOD-0048 operator | Step 1 |
| `account-status` set (veya enum kararı) | Hayır | **Evet** (required alan) | MOD-0048 operator / EA | Step 2 |
| Optional set'ler (category/workplace/address/status-reason) | Hayır | Hayır (alan optional) | MOD-0048 operator | Step 3 |
| Territory reference (country/city/district) | Hayır | Hayır (MVP optional) | MOD-0048 / EA | Step 4; MVP defer |
| CRM local seed / hardcoded fallback | — | — | — | **Yasak** (kural) |

## Changed Files

| File | Action | Status |
|---|---|---|
| docs/audits/mod-0149-crm-reference-data-readiness.md | Created (report) | ✅ |
| services/** · frontend/** · gateway/** · MOD-0048 · MOD-0149 · seed · migration | Untouched | ⛔ |

## Verdict: PARTIAL

Reference set listesi, value spesifikasyonu, validation contract ve operatör authoring checklist **net ve tam**
(readiness design PASS kalitesinde). Ancak **actual value authoring** MOD-0048 governance UI'da bir operatör aksiyonudur
ve **henüz yapılmadı** → MVP create validation (account-type/account-status) authoring tamamlanmadan tam çalışmaz.
Implementation'a geçmeden önce Step 1–2 (+ tercihen 3, 5) tamamlanmalı. MOD-0149 **pack content** bundan etkilenmez.
