# StrategyTemplate Hedefleme Modeli — Analiz & Hedef Model

> **CT analizi · 2026-09-17 · MOD-0167-FU04.** Owner sorusu: "Segmentimi seçip her segment için frekans girmem gerekmez mi? Tüm segmentlere tek frekans / aynı ürün mantıklı mı? Ayrıca holding'in her country / legal entity / business unit'i için ayrı StrategyTemplate gerekmez mi?" Bu rapor mevcut modeli kanıtla ölçer, 3 gerçek eksiği ortaya koyar ve bir hedef model + yol haritası önerir. **Kod değişmedi — analiz.**

---

## 0. Yönetici özeti

StrategyTemplate bir **"bağlayıcı" (binding) aggregate**'tir: KİM (segment) · NE SIKLIKTA (frekans) · NE (ürün/SKU%) · HANGİ HİKAYE (içerik) sorularını *birbirine bağlar*, hiçbirini kendisi üretmez. Owner'ın sezgileri iki doğru ilkeye dayanıyor; analiz üç gerçek boşluk buldu:

| # | Bulgu | Durum | Hedef |
|---|---|---|---|
| **1** | **Frekans zaten segment/hedef-başına** çözülüyor (MOD-0165), StrategyTemplate'teki alan **bağlayıcı değil** — **ama MOD-0165'in frontend'i YOK** | Mimari doğru, UI boşluğu | Frekans MOD-0165'te kalsın; **VisitFrequencyPolicy UI'ı** yapılsın; StrategyTemplate'te "işaretçi (non-binding)" olarak net göster |
| **2** | **Scope yok** — StrategyTemplate yalnız serbest-text `BusinessUnitId`; country/legal-entity/BU/territory hiyerarşisi yok (CyclePeriod & Campaign'de VAR) | Gerçek eksik | **Campaign-mirror scope** ekle (country→LE→BU/territory), legal-entity MDM validator + territory-BU catalog **reuse** |
| **3** | **Segment/ürün template-level** (tüm bağlı segmentlere ortak) | Downstream'e uygun | Template-level kalsın; segment-başına farklılaşma → **ayrı template** (matris değil) |

**Tek cümlelik hedef model:** `StrategyTemplate = Scope(country→LE→BU/territory) + Segment(kim, template-level) + Frekans(MOD-0165'e işaretçi, non-binding) + Ürün(ne, template-level SKU%) + İçerik(hikaye)`. Segment-başına strateji = ayrı template. Frekansın gerçek girişi = MOD-0165 UI.

---

## 1. Mevcut model (kanıt)

`StrategyTemplate.cs:3-21` — *"it binds, it does not produce"*. Dört bağlama (`:57-68`):
- `SegmentBindings : List` — KİM (FU02 read-only, PINNED SegmentId)
- `FrequencyIntent : tekil` — NE SIKLIKTA (MOD-0165, **asla yazılmaz**)
- `ProductLines : List` — NE (MDM GlobalProduct + Gsku, SKU%)
- `ContentBindings : List` — HANGİ HİKAYE (MOD-0162)
- `BusinessUnitId : string?` (`:46-47`) — **opak MOD-0048 kodu, yalnız uzunluk kontrolü, master read yok**

Homojen SubjectType zorunlu (`:29-32`, validator `StrategyTemplateBindingValidator.cs:86-92`): bir play ya account ya contact, karışık değil.

---

## 2. Eksen 1 — Frekans: doğru katman, eksik UI

**StrategyTemplate frekansı BAĞLAYICI DEĞİL.** Üç mod (`StrategyTemplate.cs:133-173`): `policy-reference` (MOD-0165 policy'ye işaretçi), `declared-intent` (**"MOD-0165 resolve provider bunu okumaz ve okumayacak"** `:139-140` — salt dokümantasyon), `none`.

**Gerçek frekans MOD-0165 `VisitFrequencyPolicy`'de ve hedef-başına:** `TargetType`+`TargetId` (`VisitFrequencyPolicy.cs:26-31`); desteklenen hedefler account-contact-link / contact / campaign-target / account / territory-node / **segment** / concept-node / audience-profile (`:114-128`); **özgüllük sırası** en-spesifik kazanır (account-contact-link=1 … segment=8 en geniş fallback, `:135-149`). Saha motoru cadence'i **doktor başına MOD-0165'ten** alır (`FrequencyExtendPlanner.cs:40-47`), template'ten değil.

> **Yani "her segment kendi frekansını almalı" ilkesi zaten uygulanıyor — hatta doktora kadar.** StrategyTemplate'e segment-başına frekans eklemek = MOD-0165 ile **çift kaynak / SoR ihlali** (kod bundan bilinçle kaçınmış: `StrategyTemplateReader.cs:86-88`).

**🔴 Gerçek boşluk:** MOD-0165 VisitFrequencyPolicy'nin **frontend'i YOK** (`Views/CRM/` altında VisitFrequencyPolicies klasörü yok; MOD-0165-FU03 "PARTIAL: CRUD+resolve" backend). Yani frekans hiçbir UI'dan girilemiyor → owner haklı olarak "frekansı nerede giriyorum?" diyor.

**Öneri:** (a) StrategyTemplate frekans alanı mockup'ta "MOD-0165 policy'ye işaretçi — bağlayıcı değil" olarak net; (b) **MOD-0165 VisitFrequencyPolicy frontend'i** (segment/hedef-başına frekans girme sayfası) ayrı iş olarak planlanmalı — frekansın gerçek sahibi orası.

---

## 3. Eksen 2 — Scope: gerçek eksik (holding senaryosu)

**StrategyTemplate scope taşımıyor** — yalnız opak serbest-text `BusinessUnitId` (`StrategyTemplateValidation.cs:78-90` sadece uzunluk; downstream'de yalnız liste filtresi `ListStrategyTemplatesHandler.cs:49-52`, davranışsal okuma yok). Country/legal-entity/territory yok.

**Oysa CyclePeriod (FU07) ve Campaign'de tam scope modeli VAR:**
- `CyclePeriod.cs:55-82` / `Campaign.cs:72-107`: `ScopeType` (tenant / country / legal-entity / business-unit) + `CountryScope` (ISO alpha-2) / `LegalEntityId` (MDM FK) / `BusinessUnitId` (MOD-0048 kod).
- Precedence tek yerde: `ByPrecedence = {BusinessUnit, LegalEntity, Country, Tenant}` (`CyclePeriodScopeTypes:240-241`).
- Tek-referans invariant (`HasConsistentScope`), pure normalize (`CyclePeriodScopeRules.Normalize:83-176`), write-path gate (`CyclePeriodScopeWriteValidator:47-106`: country=COUNTRY_CODES, BU=business-unit set, legal-entity=MDM fail-closed).

**Holding senaryosunun kod karşılığı (hazır bloklar):**
- **Legal entity:** MDM `GET /api/legal-entities/{id}/lookup-validation` (fail-closed, 503≠400); validator `ICyclePeriodLegalEntityValidator` **DI'da kayıtlı ve Campaign zaten reuse ediyor** (`CampaignScopeWriteValidator.cs:38-47`). Authoring selector `ICyclePeriodLegalEntityCatalog` (`GET /api/legal-entities/lookup`). Gateway route + MDM endpoint mevcut.
- **Country:** governed `COUNTRY_CODES` (CyclePeriod/Campaign) — ⚠ Territory `country` seti kullanıyor (divergence, memory: country-vocabulary-divergence; follow-up F-COUNTRY-SOT). İkisi de ISO alpha-2 olduğu için BU narrowing çalışır.
- **Business Unit ↔ Territory:** Bir BU'nun kendi country'si yoktur; BU kodu bir `TerritoryModel.BusinessScopes`'unda görünür, o model'in `CountryScope`'u vardır (`TerritoryModel.cs:17-24`). "Country → o country'nin aktif territory planlarındaki BU kodları" ilişkisi tam olarak `ITerritoryBusinessUnitCatalog.GetCandidatesAsync(country, window)` içinde kurulur (`TerritoryBusinessUnitCatalog.cs:59-101`) — narrowing, gate değil.
- **Scope selector:** `GetCyclePeriodScopeOptionsHandler` deseni (country feed + LE feed + territory-BU feed, üçü ayrı readiness flag) → StrategyTemplate'e kopyalanabilir; territory + legal-entity catalog seam'leri servis-genelinde reuse edilebilir.

**Owner'ın "country → legal entity → BU / territory model" akışı tam olarak bu bloklarla kurulur.** Campaign en yakın örnek (scope = **editable attribute**, referans değişince re-validate — CyclePeriod'un "scope=immutable identity" modeli StrategyTemplate'e uymaz çünkü BusinessUnitId zaten editable metadata).

---

## 4. Eksen 3 — Segment & ürün: template-level (downstream kanıtı)

- **Segment çoğulluğu = enumeration** ("aynı play'i birden çok homojen segmente uygula"), set-cebri yok (`StrategyTemplate.cs:102-105`); role'ler davranışsız etiket (primary/secondary/exclusion-note).
- **Ürün SKU%=100 line/SKU seviyesinde** (`StrategyTemplateAllocationRules.cs:35-92`), template-toplamı yok. Downstream (`VisitContentSequenceResolver.cs:185-238`) yalnız **"promote edilen ürün kümesi"ni** kullanıyor — yüzdeleri bile tüketmiyor. Bir segmentten play seçilirken TemplateCode'a göre **tek** play seçiliyor (`:168-172`).
- **Legacy:** SubjectList = brand+SKU% (ürün planı, audience değil); UCLN = per-target loyalty/promo-hafta (→ MicroTarget); ForWhom = content audience (→ MOD-0162). Frekans/loyalty legacy'de de hedef-başına.

**Sonuç:** Segment-başına ürün/frekans, template'i "tek-play" yerine segment-başına dallanan bir engine'e çevirir — bu FU'nun bilinçle kaçındığı şey (`StrategyTemplate.cs:8`, `:143-145` "no engine"). Farklı segment/ürün gerektiğinde doğru cevap **ayrı StrategyTemplate**.

---

## 5. Zincir hizası

`Segment (KİM) → StrategyTemplate (OYUN) → CyclePeriod (NE ZAMAN) → Campaign (uygula)`. Scope şu an her aggregate'te **bağımsız** tutuluyor (Campaign↔CyclePeriod binding'de bile scope kasıtlı otomatik eşleşmiyor — `Campaign.cs:123-125`, ama `CampaignCyclePeriodScopeMismatch` reason code applicability için var). StrategyTemplate scope eklenince zincir boyunca **scope tutarlılığı** (uyarı/applicability) ileride kurulabilir; zorunlu FK değil.

---

## 6. Sektör teyidi (Veeva / IQVIA pharma CRM)

- **Cycle/Call Plan** frekansı (call goal) **HCP/segment-başına**dır → ERP-vNext bunu doğru katmanda (MOD-0165, hedef-başına) yapıyor.
- Cycle plan'ler **country/affiliate (legal entity) + BU** bazında ayrılır → owner'ın scope talebi sektörle birebir uyumlu; mevcut StrategyTemplate bu ayrımı yapamıyor.
- Product detailing sırası play/segment düzeyinde → template-level ürün kümesi makul.

---

## 7. Hedef model

```
StrategyTemplate (bir "play")
├── SCOPE          ← YENİ: ScopeType(tenant|country|legal-entity|business-unit)
│                     + CountryScope / LegalEntityId(MDM) / BusinessUnitId(MOD-0048)
│                     + (opsiyonel) TerritoryModel narrowing; editable attribute
├── Segment(ler)   ← KİM (template-level, homojen SubjectType, enumeration)
├── Frekans        ← MOD-0165'e İŞARETÇİ (non-binding); gerçek cadence MOD-0165'te hedef-başına
├── Ürün + SKU%    ← NE (template-level; SKU%=100 line-level)
└── İçerik         ← HANGİ HİKAYE (MOD-0162 published)

Segment-başına farklı strateji  → AYRI StrategyTemplate
Segment/doktor-başına farklı frekans → MOD-0165 VisitFrequencyPolicy (kendi UI'ı)
```

---

## 8. Yol haritası (WP dizisi)

1. **ST-SCOPE** (backend+frontend, en büyük): StrategyTemplate'e Campaign-mirror scope. `StrategyTemplateScopeTypes` (mirror) + scope alanları + `Normalize/Apply` (CampaignScopeRules kopyası) + write-validator (country COUNTRY_CODES / BU business-unit / legal-entity `ICyclePeriodLegalEntityValidator` **reuse**, BU-check yalnız referans değişince) + `GetStrategyTemplateScopeOptions` selector (country/LE/territory-BU feed) + frontend cascade (Country→LE→BU/Territory). Additive; mevcut opak BusinessUnitId geriye-uyumlu.
2. **ST-FREQ-CLARIFY** (frontend): frekans alanını mockup'ta "MOD-0165 policy işaretçi — bağlayıcı değil" olarak net; declared-intent'i sadeleştir/etiketle.
3. **MOD-0165-FREQ-UI** (ayrı program): VisitFrequencyPolicy frontend — segment/hedef-başına frekans girme (frekansın gerçek UI'ı). StrategyTemplate'ten bağımsız planlanabilir.
4. **ST-MOCKUP + görsel port** (Segment akışının aynısı): hedef model netleşince Claude-Design mockup → birebir port + davranış koru + CT §37.

**Karar noktaları (owner):** country SoT (COUNTRY_CODES vs `country` — F-COUNTRY-SOT); scope davranışsal mı yoksa metadata mı (bugün Campaign metadata); MOD-0165 UI önceliği (StrategyTemplate scope'tan önce mi sonra mı).

---

## Ekler / referanslar
- Kanıt: `StrategyTemplate.cs`, `StrategyTemplateValidation.cs`, `StrategyTemplateBindingValidator.cs`, `VisitFrequencyPolicy.cs`, `FrequencyExtendPlanner.cs`, `VisitContentSequenceResolver.cs`, `CyclePeriod.cs`+`CyclePeriodScopeRules.cs`+`CyclePeriodScopeWriteValidator.cs`, `Campaign.cs`+`CampaignScopeRules.cs`+`CampaignScopeWriteValidator.cs`, `TerritoryModel.cs`+`TerritoryBusinessUnitCatalog.cs`, `MdmCyclePeriodLegalEntityValidator.cs`, MDM `LegalEntitiesController.cs`.
- Memory: legacy-crmv2-ucln-subjectlist-forwhom, mod0167-fu04, visit-frequency-policy-ownership, mod0165-fu07-cycle-period-scope, country-vocabulary-divergence, crm-field-sales-microtarget-dependency-chain.
