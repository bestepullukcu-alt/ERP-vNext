---
id: MOD-0167-FU01
name: Segment-Sourced Visit Frequency Policy Authoring
parent: MOD-0167
parent_name: Segmentation / CDP
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız co-author sahipliği ve boundary yetkilendirmesidir. Segment engine, policy aggregate, endpoint ve UI açılmamıştır."
owner: module-pack-author
branch: feature/crm/mod-0167-fu01-segment-sourced-frequency-policy
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0167 (parent — Segment / TargetCustomer / UCLN SoR)
  - MOD-0165-FU01 (policy aggregate SoR + tek provider contract)
  - MOD-0155 (consumer — visit/route planning, due/overdue engine)
  - MOD-0151 (boundary consumer — territory coverage eşleşme anahtarı)
  - MOD-0048 (reference data — frequency vokabüleri)
  - MOD-0018 (RBAC — yalnız tüketim)
---

# MOD-0167-FU01 — Segment-Sourced Visit Frequency Policy Authoring

> **BOUNDARY / OWNERSHIP AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack, [MOD-0165-FU01](MOD-0165-FU01-visit-frequency-call-cycle-policy.md) ile **aynı zincirin** ikinci
> parçasıdır ve MOD-0167'nin frequency policy üzerindeki rolünü **co-author (üretici)** olarak sabitler.
> Kod yazma yetkisi vermez: segment engine, policy aggregate, endpoint, resolver ve UI **açılmamıştır**.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0167-FU01 --name "Segment-Sourced Visit Frequency Policy Authoring" --parent MOD-0167`
> → `OK  MOD-0167-FU01: proven against Blueprint/registry.` (exit 0). Parent `MOD-0167 | Segmentation / CDP`
> Blueprint canonical'dır. Registry satırı bu pack tarafından **eklenmez** (MOD-0165-FU01 §20 F4).
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Module Summary

MOD-0167, `Segment` / `TargetCustomer` / `SubjectList` / `UCLN` nesnelerinin SoR'udur
([crm-sor-boundary.md](../crm-sor-boundary.md)). Saha gerçeğinde segment kuralı **doğrudan bir ziyaret sıklığı
kuralı üretir**:

```text
A segment doktor → ayda 4 ziyaret
B segment doktor → ayda 2 ziyaret
C segment doktor → ayda 1 ziyaret
```

Bu pack, o kuralın **nereye yazılacağını** kesinleştirir: MOD-0167 kendi frequency store'unu açmaz;
`VisitFrequencyPolicy` aggregate'ine `Source=segmentation` ile yazar.

---

## 2. Ownership Decision

| Konu | Sahip |
|---|---|
| `Segment`, segment tanımı, kriter, üyelik çözümlemesi, `TargetCustomer` / `SubjectList` / `UCLN` | **MOD-0167** |
| Segment kaynaklı `VisitFrequencyPolicy` **yazımı** (`Source=segmentation`, `TargetType=segment`) | **MOD-0167** (co-author) |
| `VisitFrequencyPolicy` **aggregate'i / store'u / provider contract'ı** | **MOD-0165** (SoR — MOD-0165-FU01 D1) |
| Campaign/cycle kaynaklı policy | **MOD-0165** |
| Policy tüketimi, due/overdue, visit/route plan | **MOD-0155** |
| Territory eşleşme anahtarı | **MOD-0151** (yalnız boundary) |
| Availability ("ne zaman müsait?") | **MOD-0150** |

### D1 — Ayrı segment-frequency store açılmaz (karar)

MOD-0167 için ayrı bir `SegmentVisitFrequency` tablosu/aggregate'i **açılmaz**. Gerekçe: iki store iki priority
engine ve iki farklı "seçilen policy" cevabı doğurur; MOD-0155 ve MOD-0151 için tek deterministik provider kalmaz
(MOD-0165-FU01 §2/D1).

### D2 — Segment üyeliği policy içine kopyalanmaz (karar)

`TargetType=segment` policy'si **segmenti** hedefler, segmentin **üyelerini** değil. Üyelik çözümlemesi
(hangi contact/account hangi segmentte) **MOD-0167'nin runtime sorumluluğudur** ve resolution anında sorulur;
policy kaydına üye listesi **kopyalanmaz**. Aksi hâlde segment değiştiğinde frequency kuralı sessizce eskir.

---

## 3. Authorized Scope (bu pack neyi yetkilendirir)

1. MOD-0167'nin `VisitFrequencyPolicy` üzerinde **co-author** olduğunun kayda geçmesi.
2. Segment kaynaklı policy'lerin **zorunlu alan profili** (§4).
3. Segment membership seam'inin sözleşmesi (§5).
4. Priority zincirindeki yerinin sabitlenmesi (§6).
5. MOD-0165 / MOD-0155 / MOD-0151 sınırlarının yazılması (§7).

**Yetkilendirmediği:** aggregate, endpoint, segmentation engine, üyelik hesaplama, UI, migration, reference set
publish, RBAC grant, campaign engine ve visit/route planning.

---

## 4. Segment-Sourced Policy Profile

Alan sözleşmesinin tamamı MOD-0165-FU01 §4'tedir. Segment kaynaklı policy için **ek/daraltılmış** kurallar:

| Alan | Kural |
|---|---|
| `TargetType` | `segment` (zorunlu) |
| `TargetId` | `SegmentId` ile aynı değer |
| `SegmentId` | **Zorunlu** |
| `Source` | `segmentation` (zorunlu) |
| `BusinessUnit` | Zorunlu — aynı segment farklı BU'da farklı sıklık taşıyabilir |
| `TerritoryNodeId` | Optional daraltma; MOD-0151'den **okunur**, kopyalanmaz |
| `CampaignId` / `CycleId` / `CyclePeriodId` | Segment kaynaklı policy'de **normalde boş**; dolu ise policy campaign bağlamına girer ve öncelik bandı campaign tarafına kayar (MOD-0165-FU01 §9.1) |
| `Priority` | Varsayılan band **600**; açık değer verilebilir ve **görünür** olur |
| `Status` | `draft` / `active` / `inactive` / `archived` — hard delete yok |

---

## 5. Segment Membership Seam

Resolution sırasında `TargetType=segment` policy'sinin bir hedefe (contact / account / account-contact-link)
uygulanabilmesi için **üyelik sorusu** MOD-0167'ye sorulur:

```text
"Bu contact/account, effectiveAt anında şu segmentin üyesi mi?"
```

| Kural | Davranış |
|---|---|
| Üyelik sahibi | **MOD-0167** (segment engine) |
| Üyelik verisi yok / engine hazır değil | Segment policy resolution'a **girmez**; sonuç `unknown` kalır, **varsayılan üyelik veya varsayılan sıklık uydurulmaz** |
| Üyelik tarih duyarlıdır | `effectiveAt` ile sorulur; geçmiş planların açıklanabilirliği için üyelik geçmişi korunur |
| Üyelik policy kaydına yazılır mı? | **Hayır** (D2) |

Bu seam **sözleşme olarak** yetkilendirildi; implementasyonu MOD-0167 segmentation engine FU'suna aittir.

### 5.1 Segment → CampaignTarget resolution boundary (2026-08-02 eki)

Aynı membership seam'i, [MOD-0165-FU02 Campaign / Targeting Boundary](MOD-0165-FU02-campaign-targeting-boundary.md)
tarafından **campaign target snapshot** üretmek için de kullanılır:

| Kural | Karar |
|---|---|
| Sahiplik | Segment tanımı ve üyeliği **MOD-0167**'dedir; `CampaignTarget` kaydının SoR'u **MOD-0165**'tir |
| Üretim biçimi | MVP **static snapshot** — hedefler üretim anında sabitlenir (MOD-0165-FU02 §5) |
| Provenance | Snapshot hangi **segment sürümünden**, ne zaman ve kim tarafından üretildi → target satırında **görünür** |
| Kopyalama | Segment üyeliği target'a **liste olarak kopyalanmaz**; snapshot bir **türev**tir, ikinci bir segment master'ı değildir |
| Auto-refresh | **Yok** — snapshot yenilemek açık bir eylemdir ve yeni snapshot üretir; eskisi history kalır |
| Usage logging | Bir snapshot bir **segment kullanımıdır** (MOD-0167 Blueprint SoR: *segment usage logs*) → follow-up |
| Consent | Segment kullanımının consent filtresi **MOD-0164**'e aittir; bu pack consent engine açmaz |

**Bu pack yine hiçbir runtime açmaz**: segment engine, membership hesaplama, dynamic audience resolution ve
snapshot üretimi ayrı implementation FU'larına aittir.

---

## 6. Priority İçindeki Yeri

MOD-0165-FU01 §9 zinciri değişmez. Segment policy **genel kuraldır** ve daha spesifik hedefler tarafından
bastırılır:

```text
manager-override (100) > campaign-target (200) > account-contact-link (300) > contact (400)
> account (500) > segment (600) > territory-node (700) > business-rule/default (800)
```

Bastırılan segment policy **kaybolmaz**: resolution cevabında `CandidatePolicies[]` içinde eleme nedeniyle
birlikte döner (sessiz eleme yasağı).

---

## 7. Integration Boundaries

| Modül | Sınır |
|---|---|
| **MOD-0165** | Aggregate/store/provider contract sahibidir; MOD-0167 aynı contract'a **yazar**, paralel bir contract açmaz |
| **MOD-0155** | Policy'yi tüketir; due/overdue, visit target, visit/route plan **yalnız burada** üretilir |
| **MOD-0151** | Eşleşme anahtarını verir (`TargetType/TargetId` + `BusinessUnit` + `TerritoryNodeId` + current coverage/resource); policy üretmez/saklamaz; provider yokken `FrequencyStatus=unknown` korunur |
| **MOD-0150** | Availability sahibidir; frequency ile karıştırılmaz |
| **MOD-0164** | Consent/preference ayrı SoR'dur; frequency policy consent kararı vermez — iletişim izni MOD-0164'te kalır |

---

## 8. Explicit Exclusions

Runtime implementation · segmentation engine implementation · üyelik hesaplama · policy aggregate/endpoint/resolver ·
campaign engine · visit planning · route planning · route optimization · due/overdue engine · last visit history ·
visit execution · GPS/check-in/check-out · visit report · digital detailing · survey · Knowledge/Content
implementation · Brand/Product master implementation · Account/Contact mutation · ContactAvailability mutation ·
territory mutation · `ContactTerritoryAssignment` · patient data · workflow approval · ChangeRequest · MOD-0023
entegrasyonu · evidence pack · import/export yeni scope · hard delete · Mongo hand-edit · RBAC seed/grant ·
MOD-0048 publish · registry satırı yazımı · `TenantId` payload'da · doğrudan `5061` business API çağrısı.

---

## 9. Contract Flags

Ayrı bir MOD-0167 frequency flag'i **tanımlanmaz**; MOD-0165-FU01 §16 flag seti tek kaynaktır
(`supportsVisitFrequencyPolicy` · `supportsCallCyclePolicy` · `supportsFrequencyPolicyPriority` ·
`supportsFrequencyPolicyEffectiveWindow` · `supportsFrequencyPolicyProvider`).
`supportsVisitPlanning` / `supportsRoutePlanning` / `supportsRouteOptimization` **eklenmez**.

---

## 10. Acceptance Criteria for Pack Approval

- [x] MOD-0167 frequency policy **co-author**'ı olarak konumlandı; SoR MOD-0165'te kaldı (D1).
- [x] Segment üyeliğinin policy'ye kopyalanmayacağı kayda geçti (D2) ve membership seam sözleşmesi yazıldı (§5).
- [x] Segment kaynaklı policy alan profili (§4) ve priority içindeki yeri (§6) sabitlendi.
- [x] MOD-0165 / MOD-0155 / MOD-0151 / MOD-0150 / MOD-0164 sınırları yazıldı (§7).
- [x] Runtime/engine/visit/route scope'u açılmadı; `runtime_code_allowed: false`.
- [ ] Reviewer onayı → `status: approved`; implementation ayrı FU.

---

## 11. Follow-up Items

| # | Follow-up | Owner |
|---|---|---|
| F1 | **`MOD-0167-FU02 — Segment Definition & Membership Resolution`** (segment engine; frequency co-authoring'in runtime prereq'i) | commercial-suite |
| F2 | Registry satırı `MOD-0167-FU01` (MOD-0165-FU01 §20 F4 ile birlikte) | registry / governance owner |
| F3 | MOD-0048 frequency reference set publish (ortak — MOD-0165-FU01 §20 F3) | MOD-0048 operator |
| F4 | **Segment usage logging** — campaign target snapshot'ı bir segment kullanımıdır (Blueprint SoR: *segment usage logs*) (§5.1) | commercial-suite / MOD-0167 |
| F5 | **MOD-0164 consent filtresi** — segment kullanımının consent boundary'si (Blueprint dependency gate) — ✅ **KAPATILDI 2026-08-02** → [MOD-0164-FU01](MOD-0164-FU01-consent-preference-management-boundary.md); segment membership consent'i **kopyalamaz**, usage log'da filtrenin uygulanıp uygulanmadığı **görünür** olmalı | commercial-suite / EA |

---

## 12. Next Recommended Prompt

`Brand/Product Master Boundary Pack Authorization` (MOD-0165-FU01 §21 ile ortak zincir).
