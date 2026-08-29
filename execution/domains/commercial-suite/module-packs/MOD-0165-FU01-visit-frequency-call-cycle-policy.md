---
id: MOD-0165-FU01
name: Visit Frequency / Call-Cycle Policy Ownership
parent: MOD-0165
parent_name: Campaign Management
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız sahiplik/boundary/contract yetkilendirmesidir. Aggregate, endpoint, engine, UI ve migration ayrı bir implementation FU authorization'ı gerektirir."
owner: module-pack-author
branch: feature/crm/mod-0165-fu01-visit-frequency-call-cycle-policy
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0165 (parent — Campaign / CyclePeriod SoR)
  - MOD-0167 (co-author — Segment / TargetCustomer SoR)
  - MOD-0155 (consumer — visit/route planning, due/overdue engine)
  - MOD-0151 (boundary consumer — territory coverage + resource responsibility eşleşme anahtarı)
  - MOD-0150 (sibling boundary — contact availability; frequency sahibi DEĞİL)
  - MOD-0149 (read-only — Account master)
  - MOD-0048 (reference data — frequency/period/source/status setleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0165-FU01 — Visit Frequency / Call-Cycle Policy Ownership

> **BOUNDARY / OWNERSHIP AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey, *"bu account / contact / account-contact-link
> hangi sıklıkta ziyaret edilmeli?"* sorusunun **sahibi, veri sözleşmesi, öncelik kuralı ve tüketim
> boundary'sidir**. Aggregate, endpoint, resolver, UI, migration, reference set publish ve RBAC grant
> **açılmamıştır**; bunlar ayrı bir **implementation FU** authorization'ına tabidir.
>
> **Neden şimdi:** MOD-0151 FU09A (PASS, 2026-08-02) route readiness'i canlıya aldı ve `FrequencyStatus=unknown`,
> `SelectedFrequencyPolicyId=null`, `DueStatus=unknown` döndürüyor — bu **kasıtlı bir placeholder**'dır
> (MOD-0151 §22.6). MOD-0150 Contact Availability (PASS) *"ne zaman ziyaret edilebilir?"* sorusunu kapattı.
> Açık kalan tek soru *"ne sıklıkta ziyaret edilmeli?"*dir ve MOD-0155 Visit Planning başlamadan **önce**
> sahibi belirlenmelidir; aksi halde frequency, en kolay ama en yanlış yere — `Contact` / `Account` üzerine
> düz bir alana — gömülür (MOD-0151 R10).
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0165-FU01 --name "Visit Frequency / Call-Cycle Policy Ownership" --parent MOD-0165`
> → `OK  MOD-0165-FU01: proven against Blueprint/registry.` (exit 0). Parent `MOD-0165 | Campaign Management`
> Blueprint canonical'dır. **Registry satırı bu pack tarafından EKLENMEZ** (registry kaydı pack yetkisi dışıdır) —
> §20 F4 follow-up'ı olarak açıldı.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

MOD-0165-FU01, **`VisitFrequencyPolicy` / `CallCyclePolicy`** nesnesinin **sahipliğini ve sözleşmesini** kurar.

Frequency bir **alan** değil, bir **policy**dir: aynı hedef (doktor, eczane, hastane, account-contact-link)
farklı campaign, farklı segment, farklı business unit, farklı dönem ve — ileride — farklı brand/product altında
**farklı sıklıkta** ziyaret edilebilir. Bu çokluk, tek bir düz `VisitFrequency` alanına sığmaz.

Kanonik örnek (kayda geçirilen gerçek ihtiyaç):

```text
Dr. Ayşe
- Almiba Q1 Campaign      → ayda 2 ziyaret   (campaign-target, cycle-based)
- Bekant                  → ayda 1 ziyaret   (brand-scoped, future)
- A segment genel kuralı  → ayda 4 ziyaret   (segmentation)
- Ziyaret lokasyonu       → Medicana Beylikdüzü  (account-contact-link)
```

Bu dört satır **çakışma değildir** — aynı anda doğrudurlar ve bir **öncelik kuralıyla** tekilleştirilirler (§9).

---

## 2. Ownership Decision (kesin karar)

| Katman | Sorumluluk | Sahiplik türü |
|---|---|---|
| **MOD-0165** Campaign Management | `VisitFrequencyPolicy` **aggregate'inin SoR'u (custodian)**; campaign/cycle bazlı policy üretimi; tek **provider read contract**'ı | **Owner / SoR** |
| **MOD-0167** Segmentation / CDP | Segment/targeting bazlı policy **yazarı (co-author)**; `Segment` / `TargetCustomer` / üyelik çözümlemesinin SoR'u; policy'leri **aynı** aggregate ve contract üzerinden `Source=segmentation` ile yazar | **Co-author / producer** |
| **MOD-0155** Field Sales / Visit Planning | Policy'yi **tüketir**; last visit + due/overdue hesabı; visit target, visit plan ve route plan üretimi | **Consumer / engine** |
| **MOD-0151** Territory Management | Policy **üretmez, saklamaz, engine çalıştırmaz**; yalnız eşleşme anahtarını (`TargetType/TargetId` + `BusinessUnit` + `TerritoryNodeId` + current resource responsibility) verir; readiness cevabında seçilen policy metadata'sı **görünebilir** | **Boundary consumer** |
| **MOD-0150** Contact & Relationship | Frequency sahibi **değildir**; yalnız `AccountContactLink` bazlı availability ("ne zaman müsait?") | **Sibling boundary** |

### D1 — Tek aggregate, tek store, tek provider contract (karar)

`VisitFrequencyPolicy` **tek bir aggregate**tir ve **tek bir yerde** saklanır: MOD-0165.
MOD-0167 kendi ayrı frequency store'unu açmaz; segment kaynaklı policy'leri aynı aggregate'e `Source=segmentation`
ve `TargetType=segment` ile yazar.

**Değerlendirilen alternatif (reddedildi):** her üretici kendi store'unu tutsun, MOD-0155 birleştirsin.
**Reddetme gerekçesi:** iki store → iki priority engine → iki farklı "seçilen policy" cevabı; MOD-0155 ve MOD-0151
için tek deterministik provider endpoint'i kalmaz; çakışma diagnostiği imkânsızlaşır. Tek store + `Source` ayrımı
aynı esnekliği tek deterministik cevapla verir.

### D2 — Frequency asla düz alan değildir (karar)

Aşağıdaki alanlar **açılmaz** — bu pack bunu kalıcı bir yasak olarak kayda geçirir:

```text
Contact.VisitFrequency            ❌
Account.VisitFrequency            ❌
AccountContactLink.VisitFrequency ❌
TerritoryNode.VisitFrequency      ❌
Resource.VisitFrequency           ❌
```

Gerekçe: bunların hiçbiri campaign dönemi, segment kuralı, BU ve (gelecekte) brand/product boyutlarını taşıyamaz;
geri dönüşü pahalı bir veri modeli hatasıdır (MOD-0151 R10).

---

## 3. Owned Objects

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `VisitFrequencyPolicy` (alias `CallCyclePolicy`) | MOD-0165 | **Sözleşme yetkilendirildi**, implementasyon **açılmadı** |
| Policy resolution contract (selected + candidates + diagnostics) | MOD-0165 | Sözleşme yetkilendirildi |
| `Campaign` / `CyclePeriod` | MOD-0165 (parent pack) | Dokunulmadı |
| `Segment` / `TargetCustomer` / `SubjectList` / `UCLN` | MOD-0167 | Dokunulmadı |
| Last visit / visit history / due-overdue engine | **MOD-0155** | **Açıkça bu pack'te değil** |
| `ContactAvailability` / `VisitPreference` | **MOD-0150** | **Açıkça bu pack'te değil** |
| Territory coverage / resource responsibility | **MOD-0151** | **Açıkça bu pack'te değil** |

---

## 4. Authorized Policy Model (alan sözleşmesi)

`VisitFrequencyPolicy` minimum alan seti — **sözleşme olarak yetkilendirildi, implement edilmedi**:

| Alan | Tip | Zorunluluk | Not |
|---|---|---|---|
| `PolicyId` | Guid | Zorunlu | Aggregate kimliği |
| `TenantId` | Guid | Zorunlu | **JWT claim'inden**; request payload'ında **asla** bulunmaz |
| `PolicyCode` | string | Zorunlu | Tenant içinde stabil, makine-okunur kod |
| `PolicyName` | string | Zorunlu | İnsan-okunur ad |
| `TargetType` | enum | Zorunlu | §6 |
| `TargetId` | Guid/string | Zorunlu | `TargetType`'a göre çözümlenir |
| `BusinessUnit` | string | Zorunlu | MOD-0151 BU scope sözlüğüyle aynı vokabüler (`alpha`/`beta`/`gamma`/…) |
| `TerritoryNodeId` | Guid? | Optional | Daraltma anahtarı; **MOD-0151 sahipliğinde okunur**, kopyalanmaz |
| `CampaignId` | Guid? | Optional | `campaign-period` / `campaign-target` bağlamında zorunlu (§10) |
| `SegmentId` | Guid? | Optional | `TargetType=segment` için zorunlu |
| `BrandId` | Guid? | **Future optional** | §11 — master yok, alan rezerve |
| `ProductId` | Guid? | **Future optional** | §11 |
| `CycleId` | Guid? | Optional | `cycle-based` için `CycleId` veya `CyclePeriodId` zorunlu (§10) |
| `CyclePeriodId` | Guid? | Optional | Aynı kural |
| `FrequencyType` | enum | Zorunlu | §7 |
| `RequiredVisitCount` | int | Zorunlu | `> 0`; dönem başına gereken ziyaret sayısı |
| `PeriodType` | enum | Zorunlu | §7 |
| `EffectiveFrom` | date | Zorunlu | §10 |
| `EffectiveTo` | date? | Optional | `< EffectiveFrom` olamaz |
| `Priority` | int | Zorunlu | Sayısal; **küçük değer kazanır** (§9) |
| `Source` | enum | Zorunlu | §8 |
| `Status` | enum | Zorunlu | §8 |
| `Notes` | string? | Optional | |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | audit | Zorunlu | Standart audit seti |

**Kurallar:** `TenantId` payload'da taşınmaz · hard delete yoktur (§8) · policy hiçbir zaman `Contact` / `Account` /
`AccountContactLink` / `TerritoryNode` master'ını mutate etmez · policy, coverage veya availability verisini
**kopyalayarak saklamaz** (yalnız anahtarla referans verir).

---

## 5. Reference Data Boundary (MOD-0048)

`FrequencyType`, `PeriodType`, `Source` ve `Status` vokabülerleri implementation FU'sunda **MOD-0048 reference set**
olarak yayınlanmalıdır (MOD-0149/0150/0151 precedent'i): hardcoded enum fallback listesi kabul edilmez, set
yayınlanmadan create/update **fail-closed 400** döner. Bu pack **hiçbir set publish etmez** — önerilen set adları:
`visit-frequency-type` · `visit-frequency-period-type` · `visit-frequency-source` · `visit-frequency-status`.
Publish, MOD-0048 operator aksiyonudur (§20 F3).

---

## 6. TargetType Policy

| `TargetType` | Anlam | Zorunlu ek alan |
|---|---|---|
| `account` | Kurum/eczane/hastane belirli sıklıkta ziyaret edilmeli | — |
| `contact` | Doktor/eczacı **genel** ziyaret sıklığı (lokasyondan bağımsız) | — |
| `account-contact-link` | **En doğru saha hedefi**: "Dr. Ayşe + Medicana Beylikdüzü" — lokasyon bağlamlı sıklık | — |
| `segment` | A/B/C segment kuralı | `SegmentId` |
| `territory-node` | Territory içindeki hedeflere genel kural | `TerritoryNodeId` |
| `campaign-target` | Campaign hedef listesinden türeyen sıklık | `CampaignId` |

**Spesifiklik sırası (tie-breaker'da kullanılır, en spesifik → en genel):**

```text
account-contact-link  >  campaign-target  >  contact  >  account  >  segment  >  territory-node
```

`campaign-target`'ın `contact`/`account`'tan daha spesifik sayılmasının nedeni, campaign hedefinin **dönemli ve
listelenmiş** bir hedef olması; genel bir contact kuralından daha dar bir bağlam taşımasıdır.

---

## 7. FrequencyType / PeriodType Policy

**`FrequencyType` (minimum):**

| Değer | Anlam |
|---|---|
| `weekly` | Haftalık |
| `biweekly` | İki haftada bir |
| `monthly` | Aylık |
| `cycle-based` | Campaign/cycle dönemine göre |
| `custom` | Özel dönem veya özel kural (`PeriodType=custom` ile birlikte) |

**`PeriodType` (minimum):** `day` · `week` · `month` · `quarter` · `cycle` · `campaign-period` · `custom`

**Tutarlılık kuralları (validation contract):**

| Kural | Davranış |
|---|---|
| `RequiredVisitCount <= 0` | **400** |
| `FrequencyType=cycle-based` ve `CycleId`/`CyclePeriodId` yok | **400** |
| `PeriodType=campaign-period` ve `CampaignId` yok | **400** |
| `FrequencyType=weekly` ile `PeriodType=month` gibi çelişkili kombinasyon | **400** — `FrequencyType` × `PeriodType` matrisi implementation FU'sunda tablo olarak sabitlenir |
| `custom` | `PeriodType=custom` + `RequiredVisitCount` + açık `Notes` gerektirir; serbest metin **kural yerine geçmez** |

---

## 8. Source / Status Policy

**`Source` (minimum):** `campaign` · `segmentation` · `manual` · `legacy-import` · `business-rule` ·
`manager-override` · `other`

`Source` **provenance**tir ve §9'daki varsayılan öncelik bandını belirler; sonradan sessizce değiştirilemez
(değişiklik audit'e yazılır).

**`Status` (minimum):** `draft` · `active` · `inactive` · `archived`

| Kural | Davranış |
|---|---|
| Hard delete | **Yasak** — yalnız `inactive` / `archived` |
| `draft` | Resolution'a **girmez** |
| `active` | Yalnız effective window içinde resolution'a girer (§10) |
| `inactive` | Geçici olarak resolution dışı; history korunur |
| `archived` | Yeni visit planning'e **girdi olmaz**; history ve geçmiş plan açıklanabilirliği için okunabilir kalır |
| History | Her durum geçişi audit'lenir; geçmiş policy'ler silinmez |

---

## 9. Priority / Conflict Policy

Bir hedef için **birden fazla policy eşleşebilir** — bu normaldir, hata değildir.

```text
Dr. Ayşe:
- A segment policy        → ayda 4
- Almiba Campaign policy  → ayda 2
- Manager override        → haftada 1
```

### 9.1 Karar: `Priority` zorunlu sayısal alandır

`Priority` **zorunludur** ve **küçük değer kazanır**. Kayıt oluşturulurken açık bir değer verilmezse,
`(Source, TargetType)` ikilisinden **varsayılan öncelik bandı** atanır:

| # | Sınıf | Varsayılan band |
|---|---|---|
| 1 | `Source=manager-override` | 100 |
| 2 | `TargetType=campaign-target` (veya `Source=campaign`) | 200 |
| 3 | `TargetType=account-contact-link` | 300 |
| 4 | `TargetType=contact` | 400 |
| 5 | `TargetType=account` | 500 |
| 6 | `TargetType=segment` (veya `Source=segmentation`) | 600 |
| 7 | `TargetType=territory-node` | 700 |
| 8 | `Source=business-rule` / default | 800 |

Band içinde ince ayar (örn. 210, 250) serbesttir; band **varsayılandır, kilit değildir** — açık `Priority` her zaman
kazanır ve **görünür** olur.

### 9.2 Deterministik tie-breaker (aynı `Priority` durumunda)

```text
1. Most specific target wins   (§6 spesifiklik sırası)
2. Latest EffectiveFrom wins
3. Stable PolicyId ordinal     (final fallback — deterministik, rastgele değil)
```

### 9.3 Değişmez kurallar

- **Sessiz/rastgele seçim yasaktır.** Aynı girdi her zaman aynı policy'yi seçmelidir.
- **Seçilen policy response'ta görünür olmalıdır**: `SelectedFrequencyPolicyId` + `PolicyCode` + `Source` +
  `Priority` + seçim gerekçesi (`selection_reason`: `priority` / `specificity` / `effective_from` / `policy_id_order`).
- **Çakışan policy'ler diagnostics olarak raporlanabilmelidir**: `CandidatePolicies[]` (elenenler + eleme nedeni).
  Bu bir **hata listesi değil**, açıklanabilirlik yüzeyidir.
- Policy **yoksa** cevap `FrequencyStatus=unknown` + `SelectedFrequencyPolicyId=null`'dır; **varsayılan sıklık
  uydurulmaz** (MOD-0151 R11 ile aynı ruh: "veri yok" ≠ "uygun değil").

---

## 10. Effective Window / Cycle Policy

| Kural | Davranış |
|---|---|
| `EffectiveFrom` | **Zorunlu** |
| `EffectiveTo` | Optional (açık uçlu policy geçerlidir) |
| `EffectiveTo < EffectiveFrom` | **400** |
| Window dışı policy | Resolution'a **giremez** |
| Future policy | Yalnız ilgili `effectiveAt` ile sorgulandığında dahil olur — bugünün planına sızmaz |
| `cycle-based` | `CycleId` **veya** `CyclePeriodId` zorunlu |
| `campaign-period` | `CampaignId` zorunlu |
| Campaign/cycle penceresi | Policy penceresi campaign/cycle penceresini **aşamaz**; aşarsa **400** (campaign lifecycle SoR'u MOD-0165 parent'ıdır) |
| `effectiveAt` semantiği | MOD-0151 FU05A/FU09A ile **aynı**: tüm resolution çağrıları açık bir `effectiveAt` alır; sunucu "şu an"ı sessizce varsaymaz |

---

## 11. Brand / Product Boundary

- `BrandId` / `ProductId` alanları **future optional**'dır — sözleşmede rezerve edilmiştir, **implement edilmez**.
- Bu FU **Brand/Product master implementasyonu yapmaz** ve Brand/Product sahipliği talep etmez
  (MOD-0151 F6/F16 ile tutarlı; master adayı MDM/Product tarafındadır).
- Brand/Product **yokken** frequency policy `account` / `contact` / `account-contact-link` / `segment` /
  `campaign-target` / `territory-node` üzerinden **tam olarak çalışır**.
- Brand/Product geldiğinde policy bu boyutlarla **daraltılabilir** (ek eşleşme anahtarı), mevcut policy'ler
  geçersizleşmez.
- **Follow-up:** `Brand/Product Master Boundary Pack Authorization` (§20 F1).

---

## 12. Knowledge / Content Boundary

| Soru | Sahip |
|---|---|
| "Ne sıklıkla gidilecek?" | **Frequency policy** (bu pack — MOD-0165/MOD-0167) |
| "Ne anlatılacak / hangi içerik gösterilecek?" | **Knowledge / Content Management** (ayrı pack) |
| İkisini birlikte kullanan | **MOD-0155** Visit Planning |

- Content selection frequency policy içinde **hardcoded olmaz**; policy'ye içerik listesi gömülmez.
- Campaign policy ileride Content/Knowledge paketleriyle **ilişkilendirilebilir** (referans), ancak içerik SoR'u
  frequency policy'de değildir.
- **Follow-up:** `Knowledge / Content Management Pack Authorization` (§20 F2) — **✅ yetkilendirildi 2026-08-02:**
  [MOD-0162-FU01 — Knowledge Content & Subject Taxonomy Foundation](MOD-0162-FU01-knowledge-content-subject-taxonomy.md).
  İki yön de yasak kaldı: içerik listesi frequency policy'ye gömülmez, frequency kuralı `KnowledgeContent`'e gömülmez.

---

## 13. MOD-0155 Consumer Boundary

**MOD-0155 ileride şunları yapar (bu pack yapmaz):**

1. Selected frequency policy'yi provider contract'ından **okur**.
2. Last visit + visit status ile **due/overdue** hesaplar.
3. Visit target üretir.
4. Visit plan / daily-weekly plan üretir.
5. Route planning ve route optimization yapar.
6. Visit execution / visit report tutar.

**Provider read contract (öneri — route'lar `integration-agent` yetkisindedir, bu pack route açmaz):**

```text
GET /api/crm/visit-frequency-policies/resolve
    ?targetType=account-contact-link&targetId=…
    &businessUnit=gamma&territoryNodeId=…&campaignId=…
    &effectiveAt=2026-08-11T09:00:00Z
```

Dönüş sözleşmesi: `SelectedPolicy{ PolicyId, PolicyCode, FrequencyType, RequiredVisitCount, PeriodType,
EffectiveFrom, EffectiveTo, Priority, Source, SelectionReason }` + `CandidatePolicies[]{ PolicyId, Priority, Source,
ExclusionReason }` + `FrequencyStatus` (`resolved` / `unknown`).

**Provider hiçbir zaman şunu döndürmez:** due/overdue verdict'i · last visit tarihi · ziyaret sırası · mesafe/süre ·
günlük plan · optimizasyon skoru. Provider **sıklık kuralını** verir; **kararı** MOD-0155 verir.

---

## 14. MOD-0151 FU09A Integration Boundary

- MOD-0151 **policy provider değildir**: policy yazmaz, saklamaz, engine çalıştırmaz (MOD-0151 §22.6, R10).
- MOD-0151'in sağladığı tek şey **eşleşme anahtarıdır**: `TargetType/TargetId` + `BusinessUnit` + `TerritoryNodeId`
  + current coverage (FU05A guard'lı) + current resource responsibility.
- MOD-0151 route readiness response'unda ileride **selected policy metadata'sı görünebilir** (read-only, provenance
  amaçlı); ancak **policy seçimi ve due/overdue engine MOD-0155'e aittir**.
- Provider yokken bugünkü davranış **doğrudur ve korunur**: `FrequencyStatus=unknown`,
  `SelectedFrequencyPolicyId=null`, `DueStatus=unknown`, reason code `frequency_unknown`.
  **Varsayılan frequency uydurulmaz.**
- MOD-0151 tarafında bu pack nedeniyle **hiçbir kod/şema değişikliği gerekmez**; FU09A sözleşmesi zaten bu
  boundary'yi bekliyordu.

---

## 15. Explicit Exclusions

Runtime implementation · aggregate/migration/endpoint/resolver yazımı · UI · visit planning · route planning ·
route optimization · due/overdue engine · last visit history · visit execution · GPS / check-in / check-out ·
visit report · digital detailing · survey · Knowledge/Content implementation · Brand/Product master implementation ·
Campaign engine implementation · Segmentation engine implementation · Account/Contact mutation ·
ContactAvailability mutation · territory assignment mutation · `ContactTerritoryAssignment` · patient data ·
workflow approval · ChangeRequest · MOD-0023 entegrasyonu · evidence pack · yeni import/export scope · hard delete ·
Mongo hand-edit · RBAC seed/grant · MOD-0048 publish · registry satırı yazımı · `TenantId` payload'da · doğrudan
`5061` business API çağrısı.

---

## 16. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsVisitFrequencyPolicy": true,
  "supportsCallCyclePolicy": true,
  "supportsFrequencyPolicyPriority": true,
  "supportsFrequencyPolicyEffectiveWindow": true,
  "supportsFrequencyPolicyProvider": true
}
```

Bu flag'ler **pack authorization düzeyinde boundary/contract hazırlığıdır**; implementation FU'su açılıp
kabul edilene kadar canlı contract endpoint'inde `true` olarak yayınlanamaz.

**Kesinlikle eklenmez:** `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsRouteOptimization`.
MOD-0151 tarafında `supportsWorkflowActivation=false` ve mevcut FU09A flag seti **değişmez**.

---

## 17. Permission Decision (öneri)

Implementation FU'sunda önerilen anahtarlar (PKS-001 formatı): `crm.visit-frequency-policy.read` ·
`crm.visit-frequency-policy.manage`. MOD-0167 co-author'ı **aynı** `manage` anahtarını kullanır; ayrı bir
segmentation-frequency anahtarı **önerilmez**. Bu pack **hiçbir permission literal'i tanımlamaz, seed/grant
yapmaz**; katalog hizalaması ayrı bir `-RBAC` follow-up'ıdır (§20 F5).

---

## 18. Acceptance Criteria for Pack Approval

- [x] `VisitFrequencyPolicy` / `CallCyclePolicy` sahipliği tek bir SoR'a (MOD-0165) bağlandı; MOD-0167 co-author
      olarak konumlandı (D1).
- [x] Frequency'nin `Contact` / `Account` / `AccountContactLink` / `TerritoryNode` / `Resource` üzerine düz alan
      olarak gömülmesi **kalıcı olarak yasaklandı** (D2).
- [x] Alan sözleşmesi (§4), `TargetType` (§6), `FrequencyType`/`PeriodType` (§7), `Source`/`Status` (§8) kararları
      yazıldı.
- [x] Priority + deterministik tie-breaker + görünürlük/diagnostics kuralı yazıldı (§9).
- [x] Effective window / cycle kuralları yazıldı (§10).
- [x] Brand/Product (§11) ve Knowledge/Content (§12) boundary'leri kaybolmadan follow-up olarak açıldı.
- [x] MOD-0155 consumer boundary (§13) ve MOD-0151 FU09A integration boundary (§14) yazıldı; `unknown` davranışı
      korundu.
- [x] Runtime/visit/route/frequency-engine scope'u açılmadı; `runtime_code_allowed: false`.
- [ ] Reviewer onayı → `status: approved`; ardından **implementation FU** ayrı yetkilendirilir.
- [ ] MOD-0048 set publish (F3) ve registry satırı (F4) — pack yetkisi dışı, operator/governance aksiyonu.

---

## 19. Implementation Notes (implementation FU'suna devir)

- Golden Reference kararı **implementation FU zamanında** verilir: policy create/edit formu alan sayımı §4'e göre
  8'in üzerindedir → beklenen `golden_reference: compact`, `shell: tenant`. Bu pack'te `shell: none` /
  `golden_reference: none` olması, **hiçbir UI yetkilendirilmediği** içindir.
- Aggregate `Diten.CrmService` içinde açılır; **yeni servis yaratılmaz**.
- Yeni CRM aggregate'i eklenirken `RegisterClassMaps` kaydı zorunludur (aksi halde Guid FK'lar binary yazılır ve
  filtreler sessizce boş döner — MOD-0151 FU05 dersi).
- `DateTimeOffset` alanları için parallel-array index/sort tuzağı: iki `DateTimeOffset` alanı **birlikte
  index'lenmez/sort edilmez** (MOD-0151 CRM dersi); effective window sorguları buna göre tasarlanır.
- Standalone Mongo'da multi-doc atomic yazım `SupportsTransactionsAsync` guard'ı ister.

---

## 20. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **`Brand/Product Master Boundary Pack Authorization`** — ✅ **KAPATILDI 2026-08-02** → [MOD-0290-FU01](../../master-data-management/module-packs/MOD-0290-FU01-brand-product-master-boundary.md) (SoR = **MOD-0290**, MDM); policy alanları artık master'a bağlanabilir, davranış değişmedi | EA / MDM + commercial-suite | `BrandId`/`ProductId` future optional kaldı; master gelmeden policy daraltma yapılamaz (§11) |
| F2 | **`Knowledge / Content Management Pack Authorization`** — ✅ **KAPATILDI 2026-08-02** → [MOD-0162-FU01](MOD-0162-FU01-knowledge-content-subject-taxonomy.md) | commercial-suite / EA | "Ne anlatılacak?" sorusunun sahibi ayrı; content policy'ye gömülmemeli (§12) |
| F3 | **MOD-0048 frequency reference set authoring + publish** (`visit-frequency-type` / `-period-type` / `-source` / `-status`) | MOD-0048 operator | Hardcoded enum yasağı; implementation FU'sunun runtime prereq'i (§5) |
| F4 | **Registry satırı**: `MOD-0165-FU01` (+ `MOD-0167-FU01`) `module-id-registry.md`'ye follow-up olarak eklensin | registry / governance owner | Pack yetkisi dışı; DCP-002 preflight PASS ama satır yazımı ayrı |
| F5 | **`MOD-0165-FU01-RBAC — Visit Frequency Policy Permission Catalog Alignment`** | MOD-0018 / commercial-suite | §17 anahtarları katalog + grant gerektirir |
| F6 | **`MOD-0165-FU03 — Visit Frequency Policy Implementation`** (aggregate + CRUD + resolver + UI + tests) — *2026-08-02'de FU02→**FU03** olarak yeniden etiketlendi; FU02 [Campaign / Targeting Boundary](MOD-0165-FU02-campaign-targeting-boundary.md) pack'ine ayrıldı (registry satırı/runtime literal etkilenmedi)* | commercial-suite | Bu pack'in runtime devamı; ayrı authorization |
| F7 | **`MOD-0155-PREREQ — Last Visit / Due-Overdue Engine Ownership`** | MOD-0155 | Zincirin üçüncü ayağı; frequency + availability hazır, last-visit hâlâ sahipsiz |

---

## 21. Next Recommended Prompt

1. **`Brand/Product Master Boundary Pack Authorization`** — `BrandId`/`ProductId` sahipliği ve MDM sınırı.
2. **`Knowledge / Content Management Pack Authorization`** — "ne anlatılacak?" sahipliği.
3. **`MOD-0165-FU03 — Visit Frequency Policy Implementation`** — yalnız §4–§10 sözleşmesi; visit/route planning
   ve due/overdue engine **açılmaz**. *(FU02 = [Campaign / Targeting Boundary](MOD-0165-FU02-campaign-targeting-boundary.md))*
