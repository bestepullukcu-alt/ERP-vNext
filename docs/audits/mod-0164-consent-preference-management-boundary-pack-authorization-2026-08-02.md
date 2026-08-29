# MOD-0164 — Consent & Preference Management Boundary Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0164-FU01 — Consent & Preference Management Boundary** (parent `MOD-0164`)
> Kapsam: Consent/Preference sahipliği, model sözleşmesi, evaluation boundary'si ve tüketim sınırları —
> **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / boundary authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 `MOD-0164` | `OK  MOD-0164: proven against Blueprint/registry.` (exit 0) |
| DCP-002 `MOD-0164-FU01` | `OK  MOD-0164-FU01: proven against Blueprint/registry.` (exit 0, `--parent MOD-0164`) |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı); `MOD-0164` registry'de zaten **reserved/planned** |

**Blueprint kanıtı (MOD-0164):** Capability Group **Marketing**, **Wave W-2**,
SoR = *consents, preferences, consent history, **consent evidence***,
`CONSENT-BUNDLE` (consent object model, **channel prefs**, **lawful basis tags**, audit export, **privacy hooks**),
dependency gate = SSO/MFA · **Policy & Control Library** · **Audit Trail**,
soft pages = **Consent Center (integration)** · Consent Audit Trail · **Evidence Pack**,
Build/Buy/Partner = **Buy/Partner**.

**Repo kanıtı:** `crm-sor-boundary.md` sat. 12/18/39 (*Consent Contact'a gömülmez → MOD-0164*),
`crm-build-lanes.md` sat. 12/35 (*`crm-consent-core` lane: **W-2, P0***).

---

## 2. Dependency Confirmation

| Ön koşul | Durum |
|---|---|
| MOD-0150 Contact Availability | **PASS** |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** |
| MOD-0165-FU01 / MOD-0167-FU01 Frequency Ownership | **PASS** |
| MOD-0162-FU01 / FU01A / FU01B / FU01C | **PASS** |
| MOD-0290-FU01 Brand/Product Master Boundary | **PASS** |
| MOD-0165-FU02 Campaign / Targeting Boundary | **PASS** (F8 bulgusu bu task ile kapanıyor) |
| MOD-0155 | **Başlamadı** |
| MOD-0021 Audit Trail | Blueprint dependency gate — mevcut platform yeteneği |
| MOD-0028 / MOD-0029 | Repoda **canlı** — `EvidenceRef` dayanağı |

---

## 3. Business Need Summary

Campaign, segmentation, frequency ve visit planning zincirinin tamamı şu soruya bağımlı:

> *"Bu kişi / contact / account-contact-link, bu kanal ve bu amaç için hedeflenebilir mi?"*

MOD-0165-FU02 raporunda kayda geçen bulgu: Blueprint hem MOD-0165 hem MOD-0167 için **Consent & Preference
Mgmt**'i dependency gate sayıyor, `CDP-BUNDLE` **consent filters** içeriyor ve `crm-build-lanes.md` consent'i
**W-2/P0** (campaign/segmentation'dan **daha erken**) işaretliyor — ama pack yoktu.

Boundary yazılmazsa iki tipik hata kaçınılmaz olur: consent'in **Contact üzerine düz alan** olarak eklenmesi ve
**`unknown` consent'in sessizce "uygun"** sayılması.

---

## 4. Ownership Decision

| Katman | Sorumluluk |
|---|---|
| **MOD-0164** | Consent record **SoR** · Preference record **SoR** · channel permission · purpose/legal-basis · opt-in/opt-out/restriction/withdrawal semantiği · **consent filter provider boundary** |
| **MOD-0167** | Segment membership tanımlar; consent filter'a **ihtiyaç duyar**, consent'i **sahiplenmez** |
| **MOD-0165** | Campaign target boundary; consent filter sonucunu **görünür kılar**, veriyi **kopyalamaz** |
| **MOD-0155** | Visit/route planlarken consent/preference uygunluğunu **tüketir**; consent engine **değildir** |
| **MOD-0150** | Contact master + availability; **consent SoR değildir**, düz consent alanı **yok** |

**MOD-0164 sahibi değildir:** campaign target üretmek · visit/route plan · frequency policy üretmek · segment
hesaplamak · content önermek · digital detailing.

**§1.1 Build/Buy kararı (Blueprint kaynaklı):** Blueprint MOD-0164'ü **Buy/Partner** ve ilk soft page'ini
**"Consent Center (integration)"** sayıyor. Karar **provider-agnostic**: harici Consent Center SoR olursa yerel
taraf yalnız **read-only projeksiyon + provenance + `EvidenceRef`** tutar; **ikinci, sapabilen master açılmaz**;
tüketici sözleşmesi her iki senaryoda **aynıdır**. Kesin Build/Buy kararı **EA'ya** bırakıldı (F1).

---

## 5. Consent Model

`ConsentId` · `TenantId` (**JWT claim**) · `SubjectType`/`SubjectId` · `ScopeType`/`ScopeId?` · `Channel` ·
`Purpose` · `LegalBasis` · `ConsentStatus` · `EffectiveFrom`/`EffectiveTo?` · `Source` · `EvidenceRef?` ·
`WithdrawalReason?` · `ExternalReferences[]` · audit dörtlüsü.

**SubjectType:** `contact` · `account-contact-link` (**en spesifik**) · `account` · `audience-profile` ·
`campaign-target`
**Channel:** `visit` · `email` · `sms` · `phone` · `whatsapp` · `portal` · `digital-detailing` · `training` · `other`
**Purpose:** `campaign` · `medical-visit` · `product-information` · `training` · `marketing` · `service` ·
`compliance` · `research` · `other`
**Status:** `granted` · `denied` · `withdrawn` · `restricted` · `unknown` · `expired`

**Temel kural:** consent daima **(özne × kanal × amaç × kapsam × zaman)** ile değerlendirilir; **"genel izin"
bayrağı yoktur** ve bir kanal/amaç izni başkasına **devredilemez**.
Hard delete yok · history korunur · **withdrawal eski kaydı silmez, yeni durumdur** ·
**`unknown` sessizce `granted` sayılmaz** · `expired` ve window dışı kayıt targeting'e **giremez** ·
silent overwrite **yasak**.

---

## 6. Preference Model

`PreferenceId` · `TenantId` · `SubjectType`/`SubjectId` · `Channel` · `PreferenceType`/`PreferenceValue` ·
`Priority` · effective window · `Source` · audit.

**PreferenceType:** `preferred-channel` · `do-not-contact` · `do-not-visit` · `preferred-visit-window` ·
`language-preference` · `content-preference` · `frequency-cap` · `topic-interest`.

**Preference consent'in yerine geçmez**; consent uygun olsa bile preference **kısıtlayıcı** olabilir.
Availability (MOD-0150) = ziyaret **zamanı**; preference = **kanal/tercih**. Preference yoksa **default
uydurulmaz**. `frequency-cap` bir **üst sınır sinyalidir**, frequency policy SoR'unu (MOD-0165-FU01) değiştirmez.

---

## 7. Campaign / Targeting Integration

Campaign: consent filter sonucunu target üzerinde **görünür gösterir** · snapshot üretirken provider sonucunu
**kullanır** · filtre uygulanmadıysa **raporda açıkça belirtir**.

Kurallar: consent verisi **kopyalanmaz**; target'ta yalnız **evaluation sonucu + provenance**
(`decision`, `reasonCodes`, `evaluatedAt`, `policyVersion`, `matchedConsentId`) tutulabilir;
**`unknown` sessizce uygun sayılmaz**; filtre uygulanmadıysa **`consent_filter_not_applied`** ile görünür;
sonuç hedefin **neden dahil/dışarıda** olduğunu açıklar.

**Reason code sözleşmesi:** `consent_granted` · `consent_denied` · `consent_unknown` · `consent_expired` ·
`preference_restricted` · `preference_channel_blocked` · `consent_filter_not_applied`.

**Ek karar (§4 provider sözleşmesi):** `EvaluateConsent(subjectType, subjectId, channel, purpose, scope?,
effectiveAt)` → `decision: allowed|blocked|unknown` + matched kayıt + preference kısıtları + reason codes +
`evaluatedAt`/`policyVersion`. Deterministik · **hiçbir kayıt yazmaz** · provider **engellemez, bildirir**.
**Çözüm önceliği (fail-closed):** en spesifik özne → en spesifik kapsam → **aynı spesifiklikte kısıtlayıcı kazanır
(denied/withdrawn/restricted > granted)** → en yeni `EffectiveFrom` → stabil `ConsentId`.
`do-not-contact`/`do-not-visit` **granted consent'i bile `blocked`** yapar ve bu görünür olur.

---

## 8. MOD-0167 Segmentation Boundary

Segment membership veya snapshot üretiminde consent filter **tüketilebilir**; **membership consent'i
kopyalamaz**; **segment usage log'da filtrenin uygulanıp uygulanmadığı görünür** olmalıdır (MOD-0167 SoR:
*segment usage logs*); `CDP-BUNDLE` consent filters dependency'si korunur.
Bu pack: segment engine · CDP runtime · membership hesaplama · dynamic resolution · **consent filter runtime**
**yapmaz**.

---

## 9. MOD-0155 Visit Planning Boundary

Tüketir: target consent eligibility · visit channel consent · digital detailing consent · training/contact
preference · `do-not-visit`/`do-not-contact` · unknown/denied reason code.

Kararlar: `denied`/`restricted` **otomatik uygun sayılmaz** · `unknown` **`unknown`/`not_ready`** olarak görünür
(**`allowed` değil**) · consent sonucu route optimizer içinde **sessizce düşürülmez**, reason code ile görünür
kalır · nihai engelleme kararı MOD-0155'te, MOD-0164 yalnız bildirir.
Bu pack: visit/route plan · due/overdue · schedule · execution · digital detailing **yapmaz**.

---

## 10. MOD-0150 Contact / Availability Boundary

```text
MOD-0150 : "hangi gün/saat müsait?"      MOD-0164 : "bu amaç/kanal için hedeflenebilir mi?"
```

**Availability consent yerine geçmez; consent availability yerine geçmez**; ikisi MOD-0155'te **birlikte**
değerlendirilir; Contact master'a **düz consent alanı eklenmez**; `preferred-visit-window` ile availability
çakışırsa availability **zaman uygunluğunu**, preference **tercihi** belirler ve ikisi de görünür kalır.

---

## 11. Knowledge / Content / Digital Detailing Boundary

MOD-0162 content/path/journey sağlar; MOD-0164 gösterim/iletişim **eligibility**'si sağlayabilir
(`Channel=digital-detailing`, `Purpose=product-information`).
Bu pack: content recommendation · digital detailing · usage tracking · evidence pack **yapmaz**.

---

## 12. Legal / Audit Boundary

Audit trail **zorunlu** (Blueprint dependency gate: Audit Trail; `CONSENT-BUNDLE`: audit export + privacy hooks) ·
`WithdrawalReason` korunur · `EvidenceRef` desteklenir ve dosya SoR'u **MOD-0028/0029**'dur (kopya yok) ·
`Source` görünür · **hard delete yasak** · **silent overwrite yasak** (güncelleme değil, **yeni durum kaydı**) ·
effective window + status transition history korunur · `LegalBasis` **kontrollü vocabulary**
(`consent` · `legitimate-interest` · `contract` · `legal-obligation` · `vital-interest` · `public-task`) ·
bu pack **hukuki yorum yapmaz**, alanı ve yönetişimini tanımlar.

---

## 13. MDM / Reference Data Boundary

MOD-0048 set'i olacaklar: `consent-channel` · `consent-purpose` · `consent-legal-basis` · `consent-status` ·
`preference-type` · `preference-value` · `consent-source`.
**MOD-0048 publish bu task'ta yapılmadı**; set yoksa runtime **fail-closed** olmalıdır (bu task runtime değil).

---

## 14. External Reference / Legacy Migration

`SourceSystem` · `ExternalId` · `ExternalCode` · `ExternalName` · `ImportedAt` · `IsPrimary` —
MOD-0290-FU01 / MOD-0165-FU02 ile **aynı sözleşme**.
**Silent merge yasak** · duplicate mapping **conflict raporu** · **withdrawal/opt-out history korunur** ·
migration implementation **yok** (F6) · harici Consent Center projeksiyonları da ExternalReferences ile kaynağa
bağlanır.

---

## 15. Permission Boundary

`crm.consent.read` · `crm.consent.manage` · `crm.preference.read` · `crm.preference.manage` ·
`crm.consent.evaluate`.
**SoD:** consent girenle değiştiren/onaylayan roller ayrılabilir. Tüketici modüller için **`evaluate` yeterlidir**
— ham consent okuma yetkisi gerekmez (en az yetki). **Seed/grant yapılmadı** → F5.

---

## 16. Explicit Exclusions

Runtime implementation · **consent CRUD** · **preference CRUD** · **consent evaluation engine** · campaign target
runtime · segment engine · frequency runtime · visit planning · route planning · digital detailing · content
recommendation · patient data · Account/Contact mutation · ContactAvailability mutation · territory mutation ·
workflow approval · MOD-0023 · file upload/render · import/export · harici Consent Center entegrasyonu ·
hard delete · Mongo hand-edit · RBAC seed/grant · registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 17. Contract Flags

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

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. **Eklenmedi:** `supportsCampaignEngine` ·
`supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDigitalDetailing` · `supportsRecommendationEngine` ·
`supportsWorkflowApproval`.

---

## 18. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend/Gateway changed? | **No** |
| **Consent CRUD implemented?** | **No** |
| **Preference CRUD implemented?** | **No** |
| **Consent evaluation engine opened?** | **No** |
| Campaign target runtime opened? | **No** |
| Segmentation engine opened? | **No** |
| Frequency runtime opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Digital detailing opened? | **No** |
| Account/Contact mutation opened? | **No** |
| ContactAvailability mutation opened? | **No** |
| Territory mutation opened? | **No** |
| Patient data opened? | **No** |
| Workflow/approval opened? | **No** |
| RBAC seed/grant changed? | **No** |
| Registry write? | **No** |
| MOD-0048 publish changed? | **No** |
| **Consent boundary added?** | **Yes** |
| **Campaign/Targeting dependency addressed?** | **Yes** (MOD-0165-FU02 F8 kapatıldı) |
| **Unknown consent not treated as granted?** | **Yes** (fail-closed, §5/§7) |
| Follow-ups opened? | **Yes** (9 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 19. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0164-FU01-consent-preference-management-boundary.md` | **Oluşturuldu** |
| `execution/domains/commercial-suite/module-packs/MOD-0165-FU02-campaign-targeting-boundary.md` | **Güncellendi** (yalnız doküman) — F8 kapatıldı, yeni pack'e bağlandı |
| `execution/domains/commercial-suite/module-packs/MOD-0167-FU01-segment-sourced-frequency-policy-authoring.md` | **Güncellendi** (yalnız doküman) — F5 kapatıldı, segment usage log görünürlüğü not edildi |
| `docs/audits/mod-0164-consent-preference-management-boundary-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 20. Final Verdict

### **PASS**

- Consent/Preference **SoR MOD-0164**'te sabitlendi; Contact üzerinde düz consent alanı **kalıcı olarak
  yasaklandı** (crm-sor-boundary ile tutarlı).
- Consent ve Preference conceptual modelleri; `SubjectType` / `Channel` / `Purpose` / `LegalBasis` /
  `ConsentStatus` / `PreferenceType` vokabülerleriyle yazıldı.
- **Consent = (özne × kanal × amaç × kapsam × zaman)** kuralı ve "genel izin bayrağı yoktur" kararı yazıldı.
- **`unknown` sessizce `granted` sayılmaz**; `expired` ve window dışı kayıt targeting'e giremez.
- **Consent filter provider** sözleşmesi (deterministik, yazmayan, görünür gerekçeli) ve **fail-closed çözüm
  önceliği** (aynı spesifiklikte kısıtlayıcı kazanır; `do-not-visit` granted'ı bile bloklar) tanımlandı.
- **MOD-0165-FU02 dependency gate'i kapandı**: target'ta yalnız evaluation sonucu/provenance, filtre
  uygulanmadıysa `consent_filter_not_applied` ile görünür.
- MOD-0167 segment usage, MOD-0155 tüketim, MOD-0150 availability ayrımı ve MOD-0162 detailing eligibility
  sınırları netleşti.
- Legal/audit/evidence boundary yazıldı (audit zorunlu · silent overwrite ve hard delete yasak ·
  `EvidenceRef` → MOD-0028/0029 · kontrollü `LegalBasis`).
- Blueprint **Buy/Partner** gerilimi **provider-agnostic SoR kuralıyla** çözüldü; ikinci sapabilen master
  yasaklandı ve kesin karar EA'ya bırakıldı.
- Runtime / engine / visit planning / detailing scope'u **açılmadı**; mevcut scope'lar bozulmadı.

FAIL kriterlerinin hiçbiri tetiklenmedi.

**Kayda geçen sıralama uyarısı (PASS'ı düşürmez):** Blueprint MOD-0164'ü **W-2**, `crm-build-lanes.md`
`crm-consent-core`'u **P0** sayıyor; campaign/segmentation (W-4) implementasyonları **consent olmadan canlıya
alınmamalıdır**. Önerilen implementation sırası: **consent → campaign/target → visit planning**.

Ayrıca **F8 (KVKK/GDPR right-to-erasure ↔ hard delete yasağı)** hukuki bir karar gerektirir ve consent
implementation'ından önce netleşmelidir.

---

## 21. Next Recommended Prompt

`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Implementation` (= **MOD-0165-FU03**)
