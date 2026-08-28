# Campaign / Targeting Boundary — Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0165-FU02 — Campaign / Targeting Boundary** (parent `MOD-0165 Campaign Management`)
> Kapsam: Campaign + CampaignTarget sahipliği, hedefleme politikası ve MOD-0155 öncesi tüketim sözleşmesi —
> **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / boundary authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 `MOD-0165-FU02` | `OK  MOD-0165-FU02: proven against Blueprint/registry.` (exit 0, `--parent MOD-0165`) |
| FU numaralandırma | MOD-0165-FU01 §20/F6'da rezerve edilen "Visit Frequency Policy Implementation" **FU02 → FU03** olarak yeniden etiketlendi; FU02 bu boundary pack'ine ayrıldı. **Hiçbir registry satırı veya runtime literal etkilenmedi** (ikisi de yok) |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı) |

**Blueprint kanıtı (bu task'ın iki temel dayanağı):**

| Modül | SoR | Contract | Dependency gate |
|---|---|---|---|
| MOD-0165 | campaigns, campaign versions, campaign results (if hosted) | `CAMPAIGN-BUNDLE` (campaign schema, **audience references**, KPI bindings, audit/export) | **Consent & Preference Mgmt**; Metric & Semantic Registry |
| MOD-0167 | segments, segment versions, **segment usage logs** | `CDP-BUNDLE` (segment schema, lineage hooks, **consent filters**, data product linkage) | DWH/Lakehouse; Data Product Registry; **Consent & Preference Mgmt** |

---

## 2. Dependency Confirmation

| Ön koşul | Durum |
|---|---|
| MOD-0150 Contact Availability | **PASS** |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** |
| MOD-0165-FU01 / MOD-0167-FU01 Frequency Ownership | **PASS** |
| MOD-0162-FU01 Knowledge / Content & Subject Taxonomy | **PASS** |
| MOD-0162-FU01A KnowledgePath | **PASS** |
| MOD-0162-FU01B EngagementJourney | **PASS** |
| MOD-0162-FU01C Subject Concept Graph | **PASS** |
| MOD-0290-FU01 Brand/Product Master Boundary | **PASS** |
| MOD-0155 | **Başlamadı** |
| **MOD-0164 Consent & Preference Management** | **Pack yok** — Blueprint **dependency gate** → §12/F8 |

---

## 3. Business Need Summary

Zincirin son halkası: içerik (MOD-0162), sıklık (MOD-0165-FU01/MOD-0167-FU01), ürün (MOD-0290-FU01), müsaitlik
(MOD-0150) ve coverage (MOD-0151) hazır. Eksik olan **"bu kampanya kimin için, hangi amaçla, hangi dönemde ve
hangi bağlamla?"** sorusuydu.

Boundary yazılmazsa üç kopyalama riski gerçekleşir: Brand/Product'ın campaign içinde **duplicate master**'ı,
içerik sırasının campaign içine **hardcoded** yazılması, sıklığın campaign'e **düz alan** olarak gömülmesi.
`CAMPAIGN-BUNDLE`'ın **"audience references"** ifadesi de aynı yönü işaret ediyor: target bir **referanstır**.

---

## 4. Ownership Decision

| Katman | Sorumluluk |
|---|---|
| **MOD-0165** | Campaign aggregate **SoR** · objective · period · scope · **campaign target list boundary** · Brand/Product/Subject/Concept/Content/Journey **referansları** · `Source=campaign` frequency policy boundary'si |
| **MOD-0167** | Segment tanımı · membership source · segment-based targeting · **segment → campaign target resolution boundary** · `Source=segmentation` frequency authoring |
| **MOD-0155** | Campaign target + frequency + availability + coverage + content/journey **tüketir**; visit/route/execution **kararlarını verir** |
| **MOD-0151** | Coverage/readiness sağlar; **campaign target üretmez** |
| **MOD-0162** | Content · path · journey · concept **context**; campaign engine değil |
| **MOD-0290** | Brand/Product master **SoR**; campaign'de duplicate yok |
| **MOD-0164** | **Consent SoR** — targeting consent'i sahiplenmez, filtreler (§12) |

**Kapsam sınırı:** MOD-0165'in Blueprint SoR'undaki **campaign results / KPI ölçümü bu FU'da açılmadı** (F6).

---

## 5. Campaign Model

`CampaignId` · `TenantId` (**JWT claim**) · `CampaignCode` (stabil) · `CampaignName` · `Description?` ·
`CampaignType` · `Objective` · `BusinessUnit?` · `BrandId?` · `ProductId?` · `SubjectId?` · `TopicId?` ·
`ConceptChainTemplateId?` · `EngagementJourneyId?` · `DefaultKnowledgePathId?` · `DefaultContentId?` ·
`StartDate` · `EndDate?` · `Status` · `Source` · `ExternalReferences[]` · audit dörtlüsü.

Kurallar: kod stabil, rename ad ile · **hard delete yok** · **archived campaign yeni target/frequency/visit-plan
input'u olmaz** · **Campaign ne KnowledgeContent, ne Brand/Product master, ne EngagementJourney'dir** — hepsine
yalnız **referans** verir.

---

## 6. CampaignTarget Model

`CampaignTargetId` · `TenantId` · `CampaignId` · `TargetType` · `TargetId` · `TargetSource` · `SegmentId?` ·
`AccountId?` · `ContactId?` · `AccountContactLinkId?` · `TerritoryNodeId?` · `BusinessUnit?` · `Priority` ·
`Status` · `EffectiveFrom` · `EffectiveTo?` · **`ReasonCode`/`SelectionReason` (zorunlu)** · audit.

Kurallar: hard delete yok · target history korunur · aynı `(CampaignId, TargetType, TargetId)` ikinci **active**
kayıt → **409** · archived/inactive campaign'e yeni target eklenemez.

**§3.1 kararı:** `AccountId`/`ContactId`/`AccountContactLinkId` **çözüm anahtarlarıdır**, ikinci bir kitle master'ı
değil; ad/adres/telefon gibi master alanları target'a **kopyalanmaz**.

---

## 7. TargetType / TargetSource

**TargetType:** `account-contact-link` (**saha için en net hedef — tercih edilen**) · `account` · `contact` ·
`segment` · `territory-node` · `concept-node` · `audience-profile`.

**TargetSource:** `manual` · `segment` · `import` · `legacy-import` · `business-rule` · `manager-selection` ·
`campaign-rule` · `other`.

Kurallar: segment source'lu target'lar **MOD-0167'den** gelir · manual target campaign içinde author edilebilir ·
import/legacy-import **yalnız boundary** (implementation yok) · **`ReasonCode` zorunlu ve görünür** ·
**sessiz/rastgele target selection YASAK**.

**§4.3:** `contact` hedefi geçerlidir ama **lokasyon bağlamı eksiktir**; `AccountContactLink`'e çözüm
**MOD-0155'in** işidir ve çözülemeyen contact target **sessizce düşürülmez**, görünür eksik bağlam olarak
raporlanır.

---

## 8. Static vs Dynamic Target

| Model | Karar |
|---|---|
| **Static target snapshot** | ✅ **MVP** — hedef listesi sabitlenir; history/audit uyumlu; execution geçmişi değişmez |
| **Dynamic target rule** | **Future-ready metadata** — resolution engine bu FU'da **yok** |

Ek kararlar: snapshot **provenance** (hangi segment sürümü, ne zaman, kim) target satırında **görünür** ·
campaign dynamic modda ise bu **response'ta açıkça görünür** (tüketici tahmin etmez) · **target auto-refresh
yoktur** — yenileme açık bir eylemdir ve **yeni snapshot** üretir, eskisi history kalır.

---

## 9. Segment Integration

`SegmentId` referans olabilir · membership **snapshot üretmek için** kullanılır · `Source=segmentation` frequency
policy MOD-0167-FU01'de yetkilendirildi · **membership target'a kopyalanmaz**, hangi **segment sürümünden**
türetildiği kaydedilir.
**Bu FU yapmaz:** segment engine · CDP runtime · membership calculation · dynamic audience resolution ·
target auto-refresh.
MOD-0167 SoR'u *segment usage logs* içerdiğinden, bir snapshot **bir segment kullanımıdır** → F7.

---

## 10. Brand/Product Integration

Campaign `BrandId`/`ProductId` **referanslar**; duplicate **yok** (kopyalanan ad/kod görüntüleme türevidir,
master değişince **stale**) · archived/inactive master'a yeni linking **engellenir veya görünür uyarı** üretir ·
**Brand/Product'sız campaign oluşturulabilir** (örn. *German A1 Speaking Practice* → Subject=Almanca,
Brand/Product=null).

---

## 11. Knowledge / Content / Journey Integration

Referanslar: `KnowledgeContent` · `KnowledgePath` · `EngagementJourney` · `EngagementJourneyStage?` ·
`ConceptChainTemplate` · `ConceptNode`.
Kurallar: campaign **sahip değildir** · content/path/journey **kopyalanmaz** · **published+effective olmayan**
öğe active campaign recommendation'a **giremez** · campaign içinde **content sequence hardcoded yazılmaz**
(path/journey referansı kullanılır) · hangi journey/path önerildiği **görünür** · **recommendation engine yok**.

---

## 12. Frequency / Call-Cycle Integration

Campaign `VisitFrequencyPolicy` üretebilir/referanslayabilir (`Source=campaign`; `TargetType=campaign-target |
account-contact-link | segment | concept-node`). Frequency campaign içine **düz alan olarak gömülmez** ·
due/overdue **MOD-0155**'te · last visit history bu FU'da **yok** · policy yoksa MOD-0151 FU09A'daki **`unknown`**
davranışı korunur.

**§9.1 Consent bulgusu (governance):** Blueprint hem MOD-0165 hem MOD-0167 için **Consent & Preference Mgmt**'i
dependency gate sayar ve `CDP-BUNDLE` **consent filters** içerir. Karar: consent SoR **MOD-0164**'tedir; targeting
consent sahiplenmez ama **filtrelenebilir** olmalıdır ve filtre uygulanmadıysa bu **görünür** olmalıdır
(sessiz "hepsi uygun" varsayımı yasak). MOD-0164 pack'i yok → **F8**.

---

## 13. Subject Concept Graph Integration

Pharma (Almiba Q1 → indication → audience profile → need → benefit) ve learning (German A1 → level → skill →
topic → need → exercise) örnekleri **aynı modelle** karşılandı. Concept graph **runtime engine yok** · linkler
**bağlam** sağlar · **automatic best target/content selection yapılmaz** · concept chain yoksa **default concept
uydurulmaz**.

---

## 14. Territory / Readiness Integration

Campaign target'ın ziyaret edilebilirliği coverage gerektirir; **MOD-0151 campaign target üretmez**, yalnız
readiness/coverage sağlar; eşleşmenin ziyaret uygunluğu **MOD-0155 tüketiminde** değerlendirilir;
**campaign target territory'ye gömülmez** (`TerritoryNodeId` yalnız okunan daraltma anahtarı).

---

## 15. MOD-0155 Consumer Boundary

Tüketir: Campaign · CampaignTarget · bağlı `VisitFrequencyPolicy` · account/contact/link hedefi · availability ·
territory readiness · Brand/Product bağlamı · Subject/Concept bağlamı · path/journey · last visit history ·
due/overdue sonucu.
MOD-0155'te kalanlar: visit plan · route plan · daily/weekly schedule · execution · content usage · stage
progress · objection capture · **due/overdue engine** · route optimization. **Bu FU bunların hiçbirini yapmaz.**

---

## 16. Lifecycle / Status

**Campaign:** `draft` · `active` · `paused` · `completed` · `cancelled` · `archived`
**CampaignTarget:** `draft` · `active` · `inactive` · `completed` · `excluded` · `archived`

Yalnız **active + effective** target MOD-0155 input'u · **`paused` yeni visit candidate üretmez** ·
`completed`/`cancelled` history için okunur, yeni plan girdisi olmaz · `archived` yeni linking/target üretmez ·
**`excluded` için gerekçe zorunlu** (sessiz düşürme yasak) · hard delete yok · history korunur ·
campaign durum değişimi target'ları **otomatik değiştirmez** (sessiz cascade yasak) ama yeni target eklenemez ve
durum tüketiciye **görünür**.

---

## 17. External Reference / Legacy Migration

`SourceSystem` · `ExternalId` · `ExternalCode` · `ExternalName` · `ImportedAt` · `IsPrimary` — MOD-0290-FU01 §12
ile **aynı sözleşme**. Legacy kod canonical olmak zorunda değil; duplicate mapping **conflict olarak raporlanır**;
**silent merge yasak**; **campaign target import/export bu FU'da yapılmaz**; migration ayrı follow-up (F9).
Legacy kaynak: `legacy-value-preservation.md` sat. 27–28 (Campaign/PromoCampaign/CyclePeriod → MOD-0165;
TargetCustomer/UCLN/SubjectList → MOD-0167).

---

## 18. Permission Boundary

`crm.campaign.read` · `crm.campaign.manage` · `crm.campaign.target.read` · `crm.campaign.target.manage` ·
`crm.campaign.publish` (publish `manage`'den ayrı — SoD). **Seed/grant yapılmadı** → `-RBAC` follow-up (F5).

---

## 19. Explicit Exclusions

Runtime implementation · backend/frontend/Gateway değişikliği · **Campaign CRUD** · **campaign target runtime** ·
segmentation engine · frequency runtime · visit planning · route planning · **due/overdue engine** ·
last visit history · digital detailing · content recommendation engine · concept graph runtime ·
AI personalization · **target auto-refresh** · campaign import/export · **campaign results / KPI ölçümü** ·
Brand/Product master implementation · KnowledgeContent implementation · consent engine · workflow approval ·
MOD-0023 · file upload/render/preview · patient data · Account/Contact mutation · territory mutation ·
hard delete · Mongo hand-edit · RBAC seed/grant · registry write · MOD-0048 publish · `TenantId` payload'da.

---

## 20. Contract Flags

```json
{
  "supportsCampaignBoundary": true,
  "supportsCampaignTargets": true,
  "supportsCampaignBrandProductReferences": true,
  "supportsCampaignKnowledgeContentReferences": true,
  "supportsCampaignJourneyReferences": true,
  "supportsCampaignFrequencyPolicyReferences": true,
  "supportsStaticTargetSnapshot": true,
  "supportsFutureDynamicTargetRules": true
}
```

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. **Eklenmedi:** `supportsVisitPlanning` ·
`supportsRoutePlanning` · `supportsDueOverdueEngine` · `supportsDigitalDetailing` ·
`supportsRecommendationEngine` · `supportsAiPersonalization` · `supportsWorkflowApproval`.

---

## 21. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend/Gateway changed? | **No** |
| **Campaign CRUD implemented?** | **No** |
| **Campaign target runtime implemented?** | **No** |
| Segmentation engine opened? | **No** |
| Frequency runtime opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Due/overdue engine opened? | **No** |
| Digital detailing opened? | **No** |
| Recommendation engine opened? | **No** |
| Concept graph runtime opened? | **No** |
| AI personalization opened? | **No** |
| Target auto-refresh opened? | **No** |
| Campaign results / KPI opened? | **No** |
| Brand/Product implementation opened? | **No** |
| KnowledgeContent runtime opened? | **No** |
| Consent engine opened? | **No** |
| Workflow/approval opened? | **No** |
| Patient data opened? | **No** |
| Account/Contact mutation opened? | **No** |
| Territory mutation opened? | **No** |
| RBAC seed/grant changed? | **No** |
| Registry write? | **No** |
| MOD-0048 publish changed? | **No** |
| **Campaign/Targeting boundary added?** | **Yes** |
| **Brand/Product optional?** | **Yes** |
| **Non-pharma campaigns supported?** | **Yes** |
| **MOD-0155 consumer boundary defined?** | **Yes** |
| Follow-ups opened? | **Yes** (10 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 22. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0165-FU02-campaign-targeting-boundary.md` | **Oluşturuldu** |
| `execution/domains/commercial-suite/module-packs/MOD-0165-FU01-visit-frequency-call-cycle-policy.md` | **Güncellendi** (yalnız doküman) — F6 ve §21 madde 3: implementation FU'su **FU02 → FU03** olarak yeniden etiketlendi + FU02 cross-ref |
| `execution/domains/commercial-suite/module-packs/MOD-0167-FU01-segment-sourced-frequency-policy-authoring.md` | **Güncellendi** (yalnız doküman) — yeni §5.1 *Segment → CampaignTarget resolution boundary* + F4/F5 follow-up'ları |
| `docs/audits/campaign-targeting-boundary-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 23. Final Verdict

### **PASS**

- Campaign/Targeting boundary netleşti; **Campaign** ve **CampaignTarget** modelleri alan sözleşmeleriyle tanımlandı.
- `TargetType` (7) / `TargetSource` (8) politikaları, **`ReasonCode` zorunluluğu** ve **sessiz target selection
  yasağı** yazıldı; `account-contact-link` saha için tercih edilen hedef, `contact` hedefinin lokasyon eksikliği
  görünür kural hâline getirildi.
- **Static snapshot (MVP) vs dynamic rule (future)** ayrımı netleşti; snapshot provenance zorunlu, **auto-refresh
  yasak**, dynamic mod **response'ta görünür**.
- MOD-0167 segmentation, MOD-0290 Brand/Product, MOD-0162 content/path/journey/concept, MOD-0165-FU01 frequency,
  MOD-0151 readiness ve MOD-0155 consumer sınırlarının **hepsi** korundu — üç kopyalama riski (duplicate master,
  hardcoded content sequence, düz frequency alanı) açıkça yasaklandı.
- Lifecycle/status politikası yazıldı (`paused` → yeni candidate yok; `excluded` → gerekçe zorunlu; sessiz cascade
  yasak).
- Legacy `ExternalReferences[]` sözleşmesi MOD-0290-FU01 ile hizalandı (**silent merge yasağı** dahil).
- Runtime / campaign engine / visit planning / detailing scope'u **açılmadı**; mevcut scope'lar bozulmadı.

FAIL kriterlerinin hiçbiri tetiklenmedi.

**Kayda geçen governance bulgusu (PASS'ı düşürmez):** Blueprint, MOD-0165 ve MOD-0167 için **Consent & Preference
Management (MOD-0164)**'ü **dependency gate** sayıyor ve `CDP-BUNDLE` consent filters içeriyor; MOD-0164'ün pack'i
yok. Consent SoR sınırı §12'de yazıldı ve **F8** olarak açıldı — hedefleme implementasyonundan **önce**
kapatılması önerilir.

---

## 24. Next Recommended Prompt

`MOD-0165/MOD-0167-FU — Visit Frequency / Call-Cycle Policy Implementation` (= **MOD-0165-FU03**)

Alternatif/paralel öneri: `MOD-0164 Consent & Preference Management Boundary Pack Authorization` (Blueprint
dependency gate — F8).
