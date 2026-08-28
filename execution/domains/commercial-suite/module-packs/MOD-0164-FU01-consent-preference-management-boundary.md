---
id: MOD-0164-FU01
name: Consent & Preference Management Boundary
parent: MOD-0164
parent_name: Consent & Preference Management
domain: commercial-suite
service: Diten.CrmService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE — bu pack yalnız Consent/Preference sahipliği, model sözleşmesi ve tüketim boundary'sidir. Aggregate, CRUD, consent evaluation engine, endpoint, provider entegrasyonu, UI ve migration ayrı bir implementation FU authorization'ı gerektirir."
owner: module-pack-author
branch: feature/crm/mod-0164-fu01-consent-preference-boundary
started: 2026-08-02
target: TBD (implementation FU ayrı yetkilendirilir)
form_field_count: 0
dependencies:
  - MOD-0164 (parent — Blueprint SoR: consents, preferences, consent history, consent evidence)
  - MOD-0021 (Blueprint dependency gate — Audit Trail)
  - MOD-0028 / MOD-0029 (evidence/doküman SoR — EvidenceRef)
  - MOD-0165-FU02 (consumer — campaign target consent filter)
  - MOD-0167-FU01 (consumer — segment usage consent filter)
  - MOD-0155 (consumer — visit/route eligibility)
  - MOD-0150 (Contact master + availability — consent SoR DEĞİL)
  - MOD-0162-FU01..FU01C (digital detailing / content gösterim eligibility bağlamı)
  - MOD-0048 (reference data — channel / purpose / legal-basis / status vokabülerleri)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok)
---

# MOD-0164-FU01 — Consent & Preference Management Boundary

> **BOUNDARY AUTHORIZATION (2026-08-02) — `runtime_code_allowed: false`.**
> Bu pack **kod yazma yetkisi vermez**. Yetkilendirdiği tek şey şu sorunun sahipliği ve sözleşmesidir:
> *"Bu kişi / contact / account-contact-link, belirli bir kanal ve amaç için hedeflenebilir mi?"*
> Consent evaluation engine, consent/preference CRUD, provider entegrasyonu, campaign target runtime,
> segment engine, visit/route planning ve digital detailing **açılmamıştır**.
>
> **Neden şimdi:** [MOD-0165-FU02](MOD-0165-FU02-campaign-targeting-boundary.md) §9.1'de kaydedilen governance
> bulgusu: Blueprint hem MOD-0165 hem MOD-0167 için **Consent & Preference Mgmt**'i **dependency gate** sayar ve
> `CDP-BUNDLE` **consent filters** içerir; MOD-0164'ün pack'i yoktu. Ayrıca
> [crm-build-lanes.md](../crm-build-lanes.md) `crm-consent-core` lane'ini **W-2 / P0** olarak işaretler — yani
> consent, campaign/segmentation (W-4) işlerinden **daha erken** olmalıydı. Bu pack o boşluğu boundary
> seviyesinde kapatır.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-02):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0164 --name "Consent & Preference Management"` → `OK` (exit 0)
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0164-FU01 --name "Consent & Preference Management Boundary" --parent MOD-0164` → `OK` (exit 0)
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` >
> `.antigravity/rules/`.

---

## 1. Ownership Decision

**Blueprint kanıtı (MOD-0164):**

| Alan | Değer |
|---|---|
| Module Name | **Consent & Preference Management** · Capability Group: Marketing · **Wave W-2** |
| **SoR** | *consents, preferences, consent history, **consent evidence*** |
| Integration contract | `CONSENT-BUNDLE` (consent object model, **channel prefs**, **lawful basis tags**, audit export, **privacy hooks**) |
| Dependency gate | SSO/MFA · **Policy & Control Library** · **Audit Trail** |
| Soft pages | **Consent Center (integration)** · Consent Audit Trail · **Evidence Pack** |
| Build / Buy / Partner | **Buy/Partner** |
| Placement | Domain App (CRM/MarTech Core) |

| Katman | Sorumluluk |
|---|---|
| **MOD-0164** | Consent record **SoR** · Preference record **SoR** · channel permission boundary · purpose/legal-basis boundary · opt-in / opt-out / restriction / withdrawal semantiği · **consent filter provider boundary** |
| **MOD-0167** Segmentation | Segment membership tanımlar; segment kullanımında consent filter'a **ihtiyaç duyar**; consent verisini **sahiplenmez** |
| **MOD-0165** Campaign | Campaign target boundary'sini sağlar; target üretirken consent filter sonucunu **görünür kılar**; consent verisini campaign içine **kopyalamaz** |
| **MOD-0155** Visit Planning | Visit/route planlarken consent/preference uygunluğunu **tüketir**; consent engine **değildir** |
| **MOD-0150** Contact | Contact master + availability sağlar; **consent SoR değildir**; Contact üzerinde **düz consent alanı yoktur** ([crm-sor-boundary.md](../crm-sor-boundary.md) sat. 12/18/39) |

**MOD-0164 şunların sahibi değildir:** campaign target üretmek · visit plan · route plan · frequency policy
üretmek · segment hesaplamak · content önermek · digital detailing.

### 1.1 Build/Buy — provider-agnostic SoR kararı (Blueprint kaynaklı)

Blueprint MOD-0164'ü **Buy/Partner** olarak işaretler ve ilk soft page'i **"Consent Center (integration)"**tir.
Bu, repo gerçeğiyle (in-house `Diten.CrmService`) gerilim yaratabilir — MOD-0151'deki belgelenmiş sapmayla aynı
durum. Karar:

| Senaryo | Kural |
|---|---|
| **In-house** | Consent kayıtları MOD-0164 aggregate'lerinde tutulur; SoR MOD-0164'tür |
| **Harici Consent Center (Buy/Partner)** | SoR **harici sistemdir**; MOD-0164 tarafında tutulan şey **read-only projeksiyon + provenance + `EvidenceRef`**'tir |
| Her iki senaryoda | **İkinci, sapabilen bir master açılmaz**; projeksiyon üzerinde yerel düzenleme yapılmaz; her karar bir **kaynak kayıt + sürüm/zaman damgasına** izlenebilir olmalıdır |
| Tüketici sözleşmesi | **Değişmez** (§7) — campaign/segment/visit tarafı provider'ın kim olduğunu bilmek zorunda değildir |

Build/Buy kesin kararı **EA'ya aittir** → F1.

---

## 2. Consent Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `ConsentId` | Zorunlu | |
| `TenantId` | Zorunlu | **JWT claim'inden**; payload'da **asla** |
| `SubjectType` · `SubjectId` | Zorunlu | §2.1 |
| `ScopeType` · `ScopeId` | Zorunlu / optional | Kapsam daraltması (ör. `campaign` + `CampaignId`, `brand` + `BrandId`, `global`) |
| `Channel` | Zorunlu | §2.2 |
| `Purpose` | Zorunlu | §2.2 |
| `LegalBasis` | Zorunlu | **Kontrollü vocabulary** (§12) |
| `ConsentStatus` | Zorunlu | §2.3 |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | `EffectiveTo < EffectiveFrom` → **400** |
| `Source` | Zorunlu | `web-form` · `paper` · `verbal-in-visit` · `import` · `legacy-import` · `external-consent-center` · `other` |
| `EvidenceRef` | Optional | **MOD-0028/0029 doküman referansı** (§11) — dosya burada saklanmaz |
| `WithdrawalReason` | Optional | `withdrawn` durumunda **beklenir** |
| `ExternalReferences[]` | Optional | §13 |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

### 2.1 `SubjectType`

```text
contact · account-contact-link · account · audience-profile · campaign-target
```

`account-contact-link` **en spesifik** özne tipidir (aynı doktor farklı kurumda farklı izne sahip olabilir);
`account` en genel. Spesifiklik sırası §5'teki çözümde kullanılır.

### 2.2 `Channel` / `Purpose`

```text
Channel : visit · email · sms · phone · whatsapp · portal · digital-detailing · training · other
Purpose : campaign · medical-visit · product-information · training · marketing · service ·
          compliance · research · other
```

**Kural:** consent her zaman **(özne × kanal × amaç × kapsam × zaman)** dörtlüsüyle değerlendirilir; "genel izin"
diye tek bir bayrak **yoktur**. Bir kanal/amaç için izin, başka bir kanal/amaç için **devredilemez**.

### 2.3 `ConsentStatus`

```text
granted · denied · withdrawn · restricted · unknown · expired
```

| Kural | Karar |
|---|---|
| Hard delete | **Yok** |
| History | **Korunur** — durum geçişleri izlenebilir |
| Withdrawal | Eski kaydı **silmez**; **yeni durum** olarak görünür (`WithdrawalReason` ile) |
| `unknown` | **Sessizce `granted` sayılmaz** (fail-closed varsayım) |
| `expired` | Active targeting için **geçerli sayılmaz** |
| Effective window dışı | **Kullanılmaz** |
| `restricted` | Kısmi izin — kısıtın kendisi preference/scope ile ifade edilir, "granted" gibi davranmaz |
| Silent overwrite | **Yasak** (§12) |

---

## 3. Preference Model

| Alan | Zorunluluk | Not |
|---|---|---|
| `PreferenceId` · `TenantId` | Zorunlu | `TenantId` JWT claim'inden |
| `SubjectType` · `SubjectId` | Zorunlu | Consent ile aynı özne tipleri |
| `Channel` | Zorunlu | Kanal-bağımsız tercihler için `other`/`any` değeri kullanılır |
| `PreferenceType` · `PreferenceValue` | Zorunlu | §3.1 |
| `Priority` | Zorunlu | Küçük değer önce (zincirdeki diğer paketlerle aynı yön) |
| `EffectiveFrom` · `EffectiveTo` | Zorunlu / optional | |
| `Source` | Zorunlu | Consent ile aynı vokabüler |
| `CreatedAt` / `CreatedBy` / `UpdatedAt` / `UpdatedBy` | Zorunlu | |

### 3.1 `PreferenceType` (örnekler)

```text
preferred-channel · do-not-contact · do-not-visit · preferred-visit-window ·
language-preference · content-preference · frequency-cap · topic-interest
```

| Kural | Karar |
|---|---|
| Consent ile ilişki | **Preference, consent'in yerine geçmez** |
| Kısıtlayıcılık | Consent uygun olsa bile preference **kısıtlayıcı** olabilir (`do-not-visit` → engelleyici) |
| MOD-0150 ile ayrım | **Availability** = "ne zaman müsait" (ziyaret zamanı); **preference** = "hangi kanal/tercih" (iletişim tercihi) — §9 |
| Veri yokluğu | **Default preference uydurulmaz** |
| `frequency-cap` | Bir **üst sınır sinyalidir**; frequency policy'nin yerine geçmez (MOD-0165-FU01 SoR'u değişmez) |

---

## 4. Consent Filter Provider Boundary (evaluation sözleşmesi)

MOD-0164 tüketicilere **karar desteği** verir; **zorlayıcı nokta tüketicidedir**.

Önerilen değerlendirme sözleşmesi (route'lar `integration-agent` yetkisindedir, bu pack route açmaz):

```text
EvaluateConsent(subjectType, subjectId, channel, purpose, scope?, effectiveAt)
→ {
    decision: allowed | blocked | unknown,
    consentStatus, matchedConsentId?, matchedScope?,
    preferenceRestrictions[]: { preferenceType, value, effect },
    reasonCodes[]: [...],
    evaluatedAt, policyVersion
  }
```

| Kural | Karar |
|---|---|
| Determinizm | Aynı girdi → **aynı karar**; sessiz/rastgele seçim **yasak** |
| `unknown` | Ayrı bir karardır; **`allowed` değildir** ve tüketici tarafından `allowed` gibi kullanılamaz |
| Görünürlük | Hangi consent kaydının eşleştiği (`matchedConsentId`, `matchedScope`) ve **neden** (`reasonCodes`) cevapta yer alır |
| Yazma | Değerlendirme **hiçbir kayıt yazmaz** (MOD-0151 FU09A readiness deseniyle aynı); kullanım logu gerekiyorsa ayrı ve açık bir yetkilendirmedir |
| Zorlayıcılık | Provider **engellemez**, **bildirir**; engelleme kararı campaign/visit tarafındadır |

### 4.1 Çözüm önceliği (karar)

Birden fazla consent/preference kaydı eşleştiğinde:

```text
1. En spesifik özne kazanır      (account-contact-link > contact > account)
2. En spesifik kapsam kazanır    (campaign/brand scope > global)
3. AYNI spesifiklikte KISITLAYICI kazanır:
      denied / withdrawn / restricted  >  granted        (fail-closed)
4. Sonra en yeni EffectiveFrom
5. Son çare: stabil ConsentId sırası
```

Ek kural: **blocking preference (`do-not-contact` / `do-not-visit`) consent `granted` olsa bile `blocked`
üretir** ve bu durum `reasonCodes` ile görünür olur.

### 4.2 Reason code sözleşmesi

Stabil, lowercase-snake, lokalize mesajdan bağımsız (MOD-0151 FU09A deseniyle aynı):

```text
consent_granted · consent_denied · consent_unknown · consent_expired ·
preference_restricted · preference_channel_blocked · consent_filter_not_applied
```

`consent_filter_not_applied` **kritik bir koddur**: filtre hiç uygulanmadıysa bu **sessizce "uygun" anlamına
gelmez**, açıkça raporlanır (§7).

---

## 5. Campaign / Targeting Integration Boundary (MOD-0165-FU02)

Campaign yapabilir: `CampaignTarget` üzerinde **consent filter sonucunu görünür göstermek** · target snapshot
üretirken consent provider sonucunu **kullanmak** · filtre uygulanmadıysa **raporda açıkça belirtmek**.

| Kural | Karar |
|---|---|
| Kopyalama | Campaign consent verisini **kopyalamaz** |
| Target üzerinde tutulabilecek | Yalnız **evaluation sonucu + provenance** (`decision`, `reasonCodes`, `evaluatedAt`, `policyVersion`, `matchedConsentId`) |
| `unknown` | Target **sessizce uygun sayılmaz** |
| Filtre uygulanmadıysa | `consent_filter_not_applied` ile **görünür** olur |
| Açıklanabilirlik | Consent sonucu, hedefin **neden dahil/dışarıda** olduğunu açıklar (MOD-0165-FU02 §3 `ReasonCode` alanıyla birleşir) |
| Tazelik | Snapshot'taki consent sonucu bir **anlık görüntüdür**; tüketim anında yeniden değerlendirme gerekip gerekmediği MOD-0155 politikasıdır (§8) |

---

## 6. MOD-0167 Segmentation / CDP Boundary

- Segment membership üretirken veya segmentten campaign target snapshot çıkarırken MOD-0164 consent filter'ı
  **tüketilebilir**.
- **Segment membership consent verisini kopyalamaz.**
- **Segment usage log'da** consent filter'ın uygulanıp uygulanmadığı **görünür** olmalıdır
  (MOD-0167 Blueprint SoR: *segment usage logs*).
- `CDP-BUNDLE`'ın **consent filters** dependency'si korunur.

**Bu pack yapmaz:** segment engine · CDP runtime · membership hesaplama · dynamic resolution ·
**consent filter runtime implementation**.

---

## 7. MOD-0155 Visit Planning Boundary

MOD-0155 ileride tüketir: target consent eligibility · visit channel consent · digital detailing consent ·
training/contact preference · `do-not-visit` / `do-not-contact` · consent unknown/denied reason code.

| Boundary kararı | Kural |
|---|---|
| `denied` / `restricted` | Visit candidate olarak **otomatik uygun sayılmaz** |
| `unknown` | Politikaya göre **`unknown` / `not_ready`** olarak görünür — `allowed` sayılmaz |
| Görünürlük | Consent sonucu route optimizer içinde **sessizce düşürülmez**; **reason code ile görünür** kalır (MOD-0151 R9/R11 ile aynı ilke) |
| Sorumluluk | Nihai engelleme/uygulama kararı **MOD-0155'te**; MOD-0164 yalnız bildirir |

**Bu pack yapmaz:** visit plan · route plan · due/overdue · daily schedule · visit execution · digital detailing.

---

## 8. MOD-0150 Contact / Availability Boundary

```text
MOD-0150 : "Bu contact/account-link hangi gün/saat müsait?"
MOD-0164 : "Bu contact/account-link bu amaç/kanal için hedeflenebilir mi?"
```

| Kural | Karar |
|---|---|
| Yer değiştirme | **Availability consent yerine geçmez; consent availability yerine geçmez** |
| Değerlendirme | İkisi **MOD-0155'te birlikte** değerlendirilir |
| Contact master | **Düz consent alanı eklenmez** (crm-sor-boundary sat. 12/18/39; MOD-0150 D7 read-only seam precedent'i) |
| Çakışma | `preferred-visit-window` (preference) ile availability çakışırsa: **availability zaman uygunluğunu**, preference **tercih/kanalı** belirler; ikisi de görünür kalır |

---

## 9. Knowledge / Content / Digital Detailing Boundary

MOD-0162 content/path/journey sağlar; MOD-0164 **gösterim/iletişim eligibility'si** sağlayabilir:

```text
Channel = digital-detailing · Purpose = product-information · ConsentStatus = granted
```

**Bu pack yapmaz:** content recommendation · digital detailing · content usage tracking · evidence pack üretimi.

---

## 10. Legal / Audit Boundary

Consent **yüksek riskli governance verisidir**. Blueprint dependency gate'i **Audit Trail**'i zorunlu kılar
(MOD-0021) ve `CONSENT-BUNDLE` **audit export + privacy hooks** içerir.

| Kural | Karar |
|---|---|
| Audit trail | **Zorunlu** — her oluşturma/değişiklik/geri çekme kaydı |
| `WithdrawalReason` | **Korunur** |
| `EvidenceRef` | **Desteklenir**; dosya/doküman SoR'u **MOD-0028/0029**'dur (kopya tutulmaz) |
| Source görünürlüğü | **Zorunlu** — iznin nereden geldiği izlenebilir |
| Hard delete | **Yasak** |
| **Silent overwrite** | **Yasak** — güncelleme değil, **yeni durum kaydı** |
| History | Effective window + status transition geçmişi **korunur** |
| `LegalBasis` | **Kontrollü vocabulary** (§12); bu pack **hukuki yorum yapmaz**, alanı ve yönetişimini tanımlar |
| Kişisel veri | Consent kayıtları KVKK/GDPR kapsamındadır; erişim **en az yetki** ilkesine tabidir (§14) |

---

## 11. MDM / Reference Data Boundary

Şu vokabüleler **MOD-0048 reference set / governed vocabulary** olmalıdır:
`consent-channel` · `consent-purpose` · `consent-legal-basis` · `consent-status` · `preference-type` ·
`preference-value` · `consent-source`.

`LegalBasis` önerilen değerler (kontrollü): `consent` · `legitimate-interest` · `contract` ·
`legal-obligation` · `vital-interest` · `public-task`.

**MOD-0048 publish bu task'ta yapılmaz.** Reference set yoksa runtime implementation **fail-closed** olmalıdır
(MOD-0149/0150/0151/0162 precedent'i) — ancak bu task runtime değildir (F4).

---

## 12. External Reference / Legacy Migration Boundary

`ExternalReferences[]`: `SourceSystem` · `ExternalId` · `ExternalCode` · `ExternalName` · `ImportedAt` ·
`IsPrimary` (MOD-0290-FU01 §12 / MOD-0165-FU02 §14 ile **aynı sözleşme**).

| Kural | Karar |
|---|---|
| Silent merge | **Yasak** — legacy consent kaydı sessizce birleştirilmez |
| Duplicate mapping | **Conflict olarak raporlanır** |
| Withdrawal / opt-out history | **Korunur** (migration sırasında da) |
| Migration implementation | **Bu task'ta yok** (F6) |
| Harici Consent Center | Projeksiyon kayıtları da `ExternalReferences` ile kaynağa bağlanır (§1.1) |

---

## 13. Permission Boundary

Canonical öneriler: `crm.consent.read` · `crm.consent.manage` · `crm.preference.read` ·
`crm.preference.manage` · `crm.consent.evaluate`.

| Kural | Karar |
|---|---|
| SoD | Consent kaydı **giren** ile **onaylayan/değiştiren** roller ayrılabilir olmalıdır |
| `evaluate` | Tüketici modüller (campaign/segment/visit) için **yeterli** olan anahtar budur — ham consent okuma yetkisi gerekmez |
| Hassasiyet | Consent kişisel veri içerir → **en az yetki**; hassas permission kataloğu ayrı follow-up (F5) |
| Seed/grant | **Bu pack'te yapılmaz** |

---

## 14. Explicit Exclusions

Runtime implementation · **consent CRUD** · **preference CRUD** · **consent evaluation engine** ·
campaign target runtime · segment engine · frequency runtime · visit planning · route planning ·
digital detailing · content recommendation · patient data · Account/Contact mutation ·
ContactAvailability mutation · territory mutation · workflow approval · MOD-0023 entegrasyonu ·
file upload/render · import/export implementation · harici Consent Center entegrasyonu · hard delete ·
Mongo hand-edit · RBAC seed/grant · registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 15. Contract Flags (öneri — implementation anlamına gelmez)

```json
{
  "supportsConsentManagement": true,
  "supportsPreferenceManagement": true,
  "supportsConsentEvaluation": true,
  "supportsConsentPurposeChannelScope": true,
  "supportsConsentEvidenceReference": true,
  "supportsConsentFilterProvider": true
}
```

**Eklenmez:** `supportsCampaignEngine` · `supportsVisitPlanning` · `supportsRoutePlanning` ·
`supportsDigitalDetailing` · `supportsRecommendationEngine` · `supportsWorkflowApproval`.
MOD-0151 / MOD-0162 / MOD-0165 / MOD-0167 flag setleri **değişmez**.

---

## 16. Acceptance Criteria for Pack Approval

- [x] Consent/Preference **SoR MOD-0164**'te sabitlendi; Contact üzerinde düz consent alanı **kalıcı olarak
      yasaklandı**.
- [x] Consent ve Preference alan sözleşmeleri, `SubjectType`/`Channel`/`Purpose`/`LegalBasis`/`Status`
      vokabülerleriyle yazıldı.
- [x] **`unknown` sessizce `granted` sayılmaz**; `expired` ve window dışı kayıt targeting'e giremez.
- [x] **Consent filter provider** sözleşmesi (deterministik, yazmayan, görünür gerekçeli) ve **fail-closed
      çözüm önceliği** (aynı spesifiklikte kısıtlayıcı kazanır) yazıldı.
- [x] MOD-0165-FU02 dependency gate'i **kapatıldı**: target üzerinde yalnız evaluation sonucu/provenance,
      filtre uygulanmadıysa `consent_filter_not_applied` ile görünür.
- [x] MOD-0167 segment usage, MOD-0155 tüketim, MOD-0150 availability ayrımı ve MOD-0162 detailing
      eligibility sınırları yazıldı.
- [x] Legal/audit/evidence boundary (audit zorunlu, silent overwrite yasak, `EvidenceRef` → MOD-0028/0029) yazıldı.
- [x] Build/Buy gerilimi **provider-agnostic SoR kuralıyla** çözüldü (§1.1) ve EA kararına bırakıldı.
- [x] Runtime / engine / visit planning / detailing scope'u açılmadı; `runtime_code_allowed: false`.
- [ ] Reviewer onayı → `status: approved`; ardından implementation FU ayrı yetkilendirilir.

---

## 17. Implementation Notes (implementation FU'suna devir)

- **Sıralama uyarısı:** Blueprint MOD-0164'ü **W-2**, `crm-build-lanes.md` `crm-consent-core`'u **P0** sayıyor;
  campaign/segmentation (W-4) işleri consent olmadan **canlıya alınmamalıdır**. Implementation sırası:
  consent → campaign/target → visit planning.
- `ConsentRecord` ve `PreferenceRecord` **ayrı aggregate**'ler olmalıdır (farklı lifecycle, farklı hassasiyet).
- Evaluation **read-only** olmalıdır (MOD-0151 FU09A readiness deseni): yazma üyesi olmayan bir seam.
- Yeni aggregate'ler `RegisterClassMaps`'e eklenmelidir (Guid FK'lar aksi hâlde binary yazılır, filtreler sessizce
  boş döner — MOD-0151 FU05 dersi).
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez**.
- Audit entegrasyonu (MOD-0021) **ilk teslimin parçası** olmalıdır; consent'te audit sonradan eklenen bir özellik
  değildir.

---

## 18. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F1 | **EA Build/Buy kararı** — in-house MOD-0164 mi, harici Consent Center entegrasyonu mu (Blueprint: Buy/Partner) | EA | §1.1; servis/entegrasyon mimarisini belirler |
| F2 | **`MOD-0164-FU02 — Consent & Preference Implementation`** (aggregate + CRUD + evaluation seam + audit + UI + tests) | commercial-suite | Bu pack'in runtime devamı |
| F3 | **Consent evaluation provider entegrasyonu** — MOD-0165-FU02 target snapshot ve MOD-0167 segment usage tarafına bağlanması | commercial-suite | Dependency gate'in runtime karşılığı |
| F4 | **MOD-0048 consent reference set publish** (`consent-channel` · `-purpose` · `-legal-basis` · `-status` · `preference-type` · `preference-value` · `consent-source`) | MOD-0048 operator | Hardcoded enum yasağı |
| F5 | **`MOD-0164-FU01-RBAC — Consent Permission Catalog Alignment`** (+ hassas veri erişim politikası, SoD) | MOD-0018 / commercial-suite | §13 |
| F6 | **Legacy consent/opt-out migration mapping planı** | commercial-suite | §12 |
| F7 | **MOD-0021 audit + MOD-0031 evidence linkage sözleşmesi** (Consent Audit Trail / Evidence Pack soft page'leri) | commercial-suite + Platform | Blueprint dependency gate ve soft page'ler |
| F8 | **KVKK/GDPR veri saklama & silme talebi (right-to-erasure) politikası** — hard delete yasağıyla nasıl uzlaşacağı | EA / compliance | Consent kayıtları kişisel veridir; saklama/anonimleştirme kararı hukuki |
| F9 | **`Policy & Control Library` bağlantısı** (Blueprint dependency gate) | Platform / EA | Legal basis ve kontrol kütüphanesi hizalaması |

---

## 19. Next Recommended Prompt

1. **`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Implementation`** (= `MOD-0165-FU03`)
2. **`MOD-0164-FU02 — Consent & Preference Implementation`** — yalnız §2/§3/§4/§10 sözleşmesi; campaign/segment/
   visit runtime **açılmaz**.
