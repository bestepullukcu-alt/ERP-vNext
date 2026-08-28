---
id: MOD-0162-FU03
name: Concept Graph Runtime + UI
parent: MOD-0162
parent_name: Knowledge Base
implements_boundary: MOD-0162-FU01C
siblings: MOD-0162-FU01, MOD-0162-FU01A, MOD-0162-FU01B, MOD-0162-FU01C, MOD-0162-FU02
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: done
runtime_code_allowed: true
runtime_code_scope: "ACTIVE (kontrol-kulesi re-verify 2026-08-25: ExternalRef hedefi = MDM Global Product; brand/product kaldırıldı, picker `mdm.global-products.read` ister) — kullanıcı onayı ile `ready-for-dev` + `runtime_code_allowed: true` olduğunda kapsam: ConceptType + ConceptNode + ConceptRelationship + ConceptChainTemplate + KnowledgeContentConceptLink runtime (CRUD-minus-delete + archive + effective dating + cycle detection + non-conformance diagnostics + contract) Diten.CrmService içinde VE CRM Admin → Knowledge → Concepts UI frontend/Diten.Web içinde. KnowledgePath (FU01A), EngagementJourney (FU01B), graph traversal/resolution/recommendation engine, AI personalization, best-next-content, digital detailing, visit/route planning, Campaign/Consent mutation, MDM master mutation (Global Product dahil — yalnız read-only ExternalRef referansı serbest), Gateway config değişikliği, RBAC seed/grant, MOD-0048 publish, registry write ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/crm/mod-0162-fu03-concept-graph-runtime-ui
started: 2026-08-24
target: TBD (kullanıcı onayı sonrası)
form_field_count: 10   # ConceptNode primary surface — kullanıcı-form alanı sayımı §7.1'de türetilir (> 8 → Compact)
dependencies:
  - MOD-0162-FU01C (Subject Concept Graph boundary — approved; §3–§7/§13 sözleşmesi BURADA implement edilir)
  - MOD-0162-FU01C-ADDENDUM (content-model AC'leri — AC-MAP / AC-MODEL / AC-BOOK / AC-LINK / AC-FU02 / AC-SEQ / AC-UI)
  - MOD-0162-FU02 (KnowledgeContent + Subject/Topic/AudienceProfile runtime — SHIPPED; hard prerequisite, sözleşmesi KIRILMAZ)
  - MOD-0162-FU01A (KnowledgePath boundary — BU FU'DA IMPLEMENT EDİLMEZ; AC-SEQ-1 gereği sonraki FU)
  - MOD-0162-FU01B (EngagementJourney boundary — BU FU'DA IMPLEMENT EDİLMEZ)
  - MOD-0290 / MDM (Global Product SoT — yalnız ExternalRef ile read-only referans; master kopyalanmaz, mutate edilmez; picker `mdm.global-products.read` izni ister)
  - MOD-0165-FU04 (Campaign runtime — Campaign.ConceptChainTemplateId format-level referansı BURADA çözümlenebilir hâle gelir; Campaign DEĞİŞMEZ)
  - MOD-0048 (reference data — concept-status / concept-relationship-type / concept-chain-status; publish AYRI operatör işi, bkz. §16/D3)
  - MOD-0058 / MOD-0057 (boundary — enterprise graph & tagging orada kalır; burada motor açılmaz)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK, en sona bırakıldı)
  - DEV-0001 (Golden Reference Compact — ConceptNode primary surface)
  - DEV-0000 (Golden Reference Slim — type/relationship/template alt-form canvas, archive confirmation, toast)
---

# MOD-0162-FU03 — Concept Graph Runtime + UI

> **⛔ DRAFT — KOD YETKİSİ YOKTUR (`runtime_code_allowed: false`).**
> Bu pack, MOD-0162-FU01C **boundary**'sinin (approved, 2026-08-09) **implementation** karşılığıdır. AGENTS.md §10
> onay kapısı gereği `draft` pack yalnızca planlama dokümanıdır; `@orchestrator` bu pack ile kod yazamaz.
> Kullanıcı incelemesi → `status: ready-for-dev` + `runtime_code_allowed: true` sonrası implementasyon açılır.
>
> **Neden ayrı bir FU:** FU01C `runtime_code_allowed: false` taşır ve gövdesi kilitlidir; FU01C-ADDENDUM da
> (`status: draft`) açıkça *"runtime yetkisi ayrı bir implementation FU authorization'ı gerektirir"* der ve
> §10'da **"bu addendum'u temel alan bir ConceptGraph implementation FU pack taslağı"** ister. Bu dosya odur.
> Emsal: FU01 (boundary) → **FU02** (implementation) ile birebir aynı desen.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-24):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU03 --name "Concept Graph Runtime + UI" --parent MOD-0162`
> → `OK  MOD-0162-FU03: proven against Blueprint/registry.` (exit 0).
>
> Otorite sırası: **Blueprint Excel** > MOD-0162-FU01C (approved boundary) > FU01C-ADDENDUM > bu pack >
> [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

Legacy CRM2 detailing zincirinin (`UCLEType → UCLNList → UCLNConnection → UCLNDesign`) vNext runtime karşılığını,
**subject-scoped, konfigüre edilebilir** bir concept graph olarak açar.

```text
Subject (FU02, mevcut)
  └── ConceptType             "bu subject'te hangi kavram tipleri var?"        (legacy UCLEType)
        └── ConceptNode       "o tipin gerçek değeri"                          (legacy UCLNList)
              └── ConceptRelationship  "node'lar arası yönlü bağlantı"         (legacy UCLNConnection)
  └── ConceptChainTemplate    "beklenen TİP sırası — zincir şablonu"           (legacy UCLNDesign)
        ↘ KnowledgeContentConceptLink → KnowledgeContent (FU02, mevcut)
```

Cevapladığı tek soru: **"bu subject'te hangi kavramlar var, birbirine nasıl bağlanır, beklenen zincir nedir ve
hangi içerik hangi kavrama bağlıdır?"**

Cevaplamadığı sorular: *hangi içerik önce* (FU01A), *hangi ziyarette* (FU01B), *ne sıklıkla/kime* (MOD-0165/0167),
*kime/ne zaman gidilecek* (MOD-0155), *en iyi sonraki içerik hangisi* (F4 — Digital Detailing).

---

## 2. Boundary'den Sapma Kararları (kullanıcı prompt'u ↔ approved pack)

Kullanıcı görev tanımındaki taslak model ile approved FU01C boundary'si **beş noktada** ayrışır.
Boundary `approved` olduğu için **boundary kazanır**; sapmalar burada gerekçelendirilir.

| # | Kullanıcı taslağı | Approved boundary (kazanan) | Gerekçe |
|---|---|---|---|
| D1 | `ConceptNodeType`, `ConceptEdge` | **`ConceptType`**, **`ConceptRelationship`** | FU01C §3/§5 canonical adlandırma; ADDENDUM §2 legacy eşlemesi de bu adları kullanır. Yeni ad açmak üçüncü bir sözlük yaratır. |
| D2 | `SubjectId` **yok** | `SubjectId` **ZORUNLU** (Type/Node/Relationship/Template) | FU01C §3: *"ConceptType subject bazlıdır"*. Subject'siz model, "her tenant için tek global graph" demektir; pharma + Almanca + QMS aynı havuzda karışır. Cross-subject ilişki **400** (§5.1). |
| D3 | `ParentTypeId`, `ParentNodeId` | **YOK** — hiyerarşi `ConceptRelationship` + `ConceptChainTemplate` ile ifade edilir | İki rakip hiyerarşi mekanizması (parent FK + edge) tutarsızlık üretir; cycle detection iki yerde çalışmak zorunda kalır. Boundary tabloları parent alanı taşımaz. |
| D4 | `ConceptNode.GlobalProductId / TopicId / AudienceProfileId` (doğrudan FK) | **`ExternalRefType` + `ExternalRefId`** (tek çift) | FU01C §4: *"node hiçbir varlığın SoR'u değildir"*. Her master için ayrı kolon açmak (**`GlobalProductId` dahil**), master'ı graph'a kopyalamanın ilk adımıdır (AC-MAP-1 ihlali). `audience-profile` bir ConceptType olabilir ama FU01 AudienceProfile master'ının yerine geçmez. Ürün hedefi `ExternalRefType=global-product` + `ExternalRefId` ile MDM Global Product'a bağlanır. |
| D5 | `ConceptChainTemplate` **yok** | **VAR ve scope'ta** | Legacy `UCLNDesign` karşılığı (ADDENDUM §2). Ayrıca `Campaign.ConceptChainTemplateId` **bugün canlı kodda mevcut ve çözümlenemiyor** (`frontend/Diten.Web/Models/CRM/CampaignViewModels.cs:27`, `:92`) — bu FU o sarkan referansı kapatır. |

Ek olarak **üç validasyon** kullanıcı taslağından farklıdır:

| Kullanıcı taslağı | Boundary kararı |
|---|---|
| "cycle engellenmeli **veya explicit allowed flag**" | **Flag YOK.** Cycle detection zorunlu, `active` ilişkide döngü → **400** (§5.1) |
| "parent type'a aykırı edge **engellenmeli**" | **Engellenmez.** Template'e uymayan `(fromType→toType)` çifti **`IsTemplateConforming=false`** olarak işaretlenir ve response'ta **görünür** olur (§6.1). Sessiz kabul de sessiz ret de yasak |
| duplicate kuralı yok | Aynı `(From, To, RelationshipType)` için ikinci **active** kayıt → **409** |

---

## 3. Owned Objects

| Aggregate | Legacy | Sahiplik |
|---|---|---|
| `ConceptType` | `UCLEType` | Bu FU |
| `ConceptNode` | `UCLNList` | Bu FU |
| `ConceptRelationship` | `UCLNConnection` | Bu FU |
| `ConceptChainTemplate` | `UCLNDesign` | Bu FU |
| `KnowledgeContentConceptLink` | (yok — yeni) | Bu FU (§4.5 kararı) |

**Sahiplenilmeyen:** `Subject` / `Topic` / `AudienceProfile` / `KnowledgeContent` (FU02) · `Global Product` (MDM) ·
`Campaign` (MOD-0165) · `KnowledgePath` / `ContentPackage` (FU01A — **AC-BOOK-1**, UCLNBook buraya gömülmez) ·
enterprise entity graph (MOD-0058) · tag/taxonomy governance (MOD-0057).

---

## 4. Entity Modelleri

Ortak kurallar: `TenantId` **JWT claim'inden** (payload'da asla) · `CreatedAt/By` + `UpdatedAt/By` zorunlu ·
`EntityBase.Version` **teknik concurrency token**'dır, iş versiyonu değildir · **hard delete YOK** ·
archived kayıt update kabul etmez (**409**) · `EffectiveTo < EffectiveFrom` → **400**.

### 4.1 `ConceptType`

| Alan | Zorunlu | Not |
|---|---|---|
| `Id` (ConceptTypeId) | ✔ | |
| `TenantId` | ✔ | claim |
| `SubjectId` | ✔ | archived subject → yeni type **400** |
| `ConceptTypeCode` | ✔ | `(TenantId, SubjectId)` içinde unique (non-archived); **stabil**, rename edilmez |
| `ConceptTypeName` | ✔ | rename buradan yapılır |
| `Description` | ✖ | |
| `SortOrder` | ✔ | yönetim sırası — **zincir sırası DEĞİL** (o §4.4'tedir) |
| `Status` | ✔ | `draft` · `active` · `inactive` · `archived` |
| `ArchivedAt` | ✖ | soft lifecycle |

Örnek kodlar (tenant kurar; **hardcoded enum yasak**):
`indication` · `audience-profile` · `profile-need` · `need-benefit` · `atc-code` · `objection` · `key-message` ·
`language-level` · `skill` · `learning-need` · `process-area` · `sop` · `control-point`

### 4.2 `ConceptNode`

| Alan | Zorunlu | Not |
|---|---|---|
| `Id` (ConceptNodeId) | ✔ | **FU02 `KnowledgeContent.ConceptNodeId` bu id'yi işaret eder** |
| `TenantId` | ✔ | claim |
| `SubjectId` · `ConceptTypeId` | ✔ | node'un subject'i, tipinin subject'i ile aynı olmalı → aksi **400** |
| `ConceptNodeCode` | ✔ | `(SubjectId, ConceptTypeId)` içinde unique; stabil |
| `ConceptNodeName` | ✔ | |
| `Description` | ✖ | |
| `Status` | ✔ | `draft` · `active` · `inactive` · `archived` |
| `EffectiveFrom` · `EffectiveTo` | ✔ / ✖ | **iki DateTimeOffset birlikte index'lenmez/sort edilmez** (parallel-array tuzağı) |
| `ExternalRefType` · `ExternalRefId` | ✖ | **Set (karar 2026-08-25):** `global-product` · `document` · `audience-profile` · `reference-data-value` · `other` — **master SoR kalır, alan kopyalanmaz**. `brand`/`product` **kaldırıldı**; ürün hedefi tek kanonik master = MDM **Global Product** (`ExternalRefId` = Global Product `Id`) |
| `MetadataJson` | ✖ | kaçış kapısı; **iş kuralı buradan okunmaz** |
| `ArchivedAt` | ✖ | |

### 4.3 `ConceptRelationship`

| Alan | Zorunlu | Not |
|---|---|---|
| `Id` · `TenantId` · `SubjectId` | ✔ | |
| `FromConceptNodeId` · `ToConceptNodeId` | ✔ | aynı subject → aksi **400**; `From == To` → **400** |
| `RelationshipType` | ✔ | **Kanonik set (D3=A, boundary FU01C §5):** `leads-to` · `requires` · `addresses` · `evidences` · `belongs-to` · `custom` — **tek geçerli sözlük** |
| `RelationshipCode` · `RelationshipName` | ✔ | kod stabil; tie-break anahtarı |
| `Direction` | ✔ | `outbound` (varsayılan) \| `bidirectional` — **ters kenar otomatik türetilmez** |
| `Priority` | ✔ | **küçük değer önce**; eşitlikte `RelationshipCode` sırası |
| `IsTemplateConforming` | ✔ (türetilmiş) | §6.1 — reddetmez, **görünür** kılar |
| `Status` · `EffectiveFrom` · `EffectiveTo` · `ArchivedAt` | ✔/✖ | |

> **D3=A vokabüler eşlemesi (SoT çözüldü — boundary kazanır).** Erken authoring taslağındaki `concept-relationship-type`
> değerleri **kaldırıldı** ve kanonik sete eşlendi; bu sapan kodlar **hiçbir yerde yayınlanmaz**:
> `related-to → custom (yön beyanıyla)` · `depends-on → requires` · `targets → addresses` · `maps-to → belongs-to` ·
> `replaces → custom` (semantik farkı `MetadataJson` notu taşır; yeni kanonik kod açmak üçüncü sözlük yaratır).
> Doğrulama **in-domain fail-closed** (V19): kanonik set dışı değer **400**. MOD-0048 `concept-relationship-type`
> publish'i **ayrı operatör işidir** (F-RD) ve dev değerleriyle çelişmez.

### 4.4 `ConceptChainTemplate`

| Alan | Zorunlu | Not |
|---|---|---|
| `Id` · `TenantId` · `SubjectId` | ✔ | |
| `ChainCode` | ✔ | sürümler arası **stabil** |
| `ChainName` · `Description` | ✔ / ✖ | |
| `OrderedConceptTypes` | ✔ | sıralı `ConceptTypeId[]`; **min 2**; hepsi aynı subject → aksi **400**; **aynı tip iki kez geçemez** → **400** (v1; özyineleme = F7) |
| `Status` | ✔ | `draft` · `review` · `approved` · `published` · `inactive` · `archived` |
| `Version` | ✔ | iş versiyonu (≠ `EntityBase.Version`) |
| `EffectiveFrom` · `EffectiveTo` | ✔ / ✖ | aynı `ChainCode` için örtüşen pencerede iki `published` → **409** |

`published` sürümde `OrderedConceptTypes` **dondurulur**; değişiklik yeni sürüm ister.

### 4.5 `KnowledgeContentConceptLink` (AC-LINK kararı)

**Karar: many-to-many AÇILIR (AC-LINK-1).** Gerekçe — ADDENDUM §5'in kanıtı "1 içerik = 1 node" kuralını çürütür:
aynı brochure hem `need` hem `benefit` bağlamında kullanılır. AC-LINK-3 aksi hâlde "1:1 kuralı **kanıtla**
sabitlensin" der; böyle bir kanıt yok.

**Bağlanma modeli (çelişki giderildi):** Link **her zaman bir `ConceptNodeId`'ye** demirlenir (node zorunlu kalır —
graf'ın adreslenebilir birimi node'dur). İçerik bir **ilişki bağlamına** aitse, `ConceptRelationshipId` **ek olarak**
verilir; bu ilişki, link'in demirlendiği node'u (`From` veya `To`) içermelidir → aksi **400**. Yani "içerik bir
ilişkiye aittir" = *node + o node'un dahil olduğu ilişki*; **node'suz saf ilişki-linki yoktur**. Bu, önceki taslaktaki
"node'a değil ilişkiye" ifadesinin doğru okunuşudur.

| Alan | Zorunlu | Not |
|---|---|---|
| `Id` · `TenantId` | ✔ | |
| `KnowledgeContentId` | ✔ | archived content → yeni link **400** |
| `ConceptNodeId` | ✔ | archived node → yeni link **400** |
| `ConceptRelationshipId` | ✖ | içerik bir **ilişkiye** aitse |
| `LinkRole` | ✔ | `primary` · `supporting` · `evidence` · `objection-handling` (in-domain) |
| `SortOrder` | ✔ | deterministik sıra |
| `Status` · `ArchivedAt` | ✔ / ✖ | |

**AC-LINK-2:** FU02'nin `KnowledgeContent.ConceptNodeId` alanı **silinmez, taşınmaz**; "primary node kısayolu"
olarak kalır. Değişiklik tamamen **additive**'dir (AC-FU02-4).

---

## 5. FU02 Sözleşme Koruması (kırmızı çizgi)

- **AC-FU02-1** `KnowledgeContent` **atomik** kalır — pakete/zincire dönüştürülmez.
- **AC-FU02-2** `IKnowledgeContentLinkageReader.ResolvePublishedContentAsync(...)` **imzası ve davranışı değişmez**;
  Campaign tüketimi kırılmaz.
- **AC-FU02-3** DELETE/PATCH yok · archive lifecycle korunur · tenant claim server-resolved ·
  `ContentVersion` ≠ `EntityBase.Version`.
- **AC-FU02-4** Tüm değişiklikler additive: yeni collection'lar + `ConceptNodeId` doğrulamasının **sıkılaştırılması**.
  Mevcut FU02 alanı/endpoint'i **kaldırılmaz**.
- **Tek davranış değişikliği:** `KnowledgeContent.ConceptNodeId` bugün format-level'dır; bu FU sonrası **canlı,
  archived olmayan, aynı tenant'a ait** bir node'a çözümlenmelidir (aksi **400**).
  **Doğrulama tetiği — alan-değişimi bazlı (dirty-check):** `ConceptNodeId` doğrulaması YALNIZ gönderilen değer
  mevcut kayıttaki değerden **farklıysa** (veya create ise) çalışır. Bir PUT'ta `ConceptNodeId` **değişmemişse**
  (aynı değer ya da payload'da hiç yok), o alan için node-resolution **atlanır** ve **400 üretilmez** — içeriğin
  başka alanlarını (ör. başlık, status) düzenleyip Save eden kullanıcı, dokunmadığı **dangling/legacy** node değeri
  yüzünden bloke olmaz. Böylece hem yeni/değişen değerler fail-closed doğrulanır, hem eski veride **sessiz Edit→Save
  regresyonu** oluşmaz. Geriye dönük toplu 400 yoktur; temizlik ayrı bir migration işidir (F-MIG).

---

## 6. API Contract

Tüm route'lar mevcut Gateway wildcard'ı `/api/crm/knowledge/{everything}` altındadır →
**`ocelot.json` DEĞİŞMEZ** (F-GW zaten çözülmüş; `GET/POST/PUT/OPTIONS`, DELETE/PATCH yok).

```text
GET    /api/crm/knowledge/concept-graph/contract

GET    /api/crm/knowledge/concept-types            ?subjectId&status&search&includeArchived
POST   /api/crm/knowledge/concept-types
GET    /api/crm/knowledge/concept-types/{id}
PUT    /api/crm/knowledge/concept-types/{id}
POST   /api/crm/knowledge/concept-types/{id}/archive

GET    /api/crm/knowledge/concept-nodes            ?subjectId&conceptTypeId&status&externalRefType&effectiveAt&search&includeArchived
POST   /api/crm/knowledge/concept-nodes
GET    /api/crm/knowledge/concept-nodes/{id}
PUT    /api/crm/knowledge/concept-nodes/{id}
POST   /api/crm/knowledge/concept-nodes/{id}/archive

GET    /api/crm/knowledge/concept-relationships    ?subjectId&fromNodeId&toNodeId&relationshipType&conformance&status&includeArchived
POST   /api/crm/knowledge/concept-relationships
GET    /api/crm/knowledge/concept-relationships/{id}
PUT    /api/crm/knowledge/concept-relationships/{id}
POST   /api/crm/knowledge/concept-relationships/{id}/archive

GET    /api/crm/knowledge/concept-chain-templates  ?subjectId&status&effectiveAt&search&includeArchived
POST   /api/crm/knowledge/concept-chain-templates
GET    /api/crm/knowledge/concept-chain-templates/{id}
PUT    /api/crm/knowledge/concept-chain-templates/{id}
POST   /api/crm/knowledge/concept-chain-templates/{id}/archive

GET    /api/crm/knowledge/concept-graph            ?subjectId (zorunlu) &effectiveAt&includeArchived
GET    /api/crm/knowledge/concept-graph/by-node/{nodeId}
GET    /api/crm/knowledge/concept-graph/by-content/{contentId}

GET    /api/crm/knowledge/content-concept-links    ?contentId&conceptNodeId&linkRole&includeArchived
POST   /api/crm/knowledge/content-concept-links
POST   /api/crm/knowledge/content-concept-links/{id}/archive
```

**Yasaklar:** `DELETE` yok · `PATCH` yok · payload'da `TenantId` yok (gönderilirse **sessizce yok sayılır**, claim
kazanır) · service-to-service doğrudan iş çağrısı yok (yalnız Gateway) · `/concept-graph` **komşuluk okur,
traversal/resolution YAPMAZ** (motor = F4/MOD-0058).

### 6.1 `/concept-graph` semantiği (motor değildir)

Döndürür: verilen subject için **node listesi + kenar listesi + template listesi** (+ `by-node` için **1 hop**
komşuluk, `by-content` için içeriğin bağlı olduğu node'lar ve o node'ların 1-hop komşuluğu). Sıralama
deterministiktir (`Priority` → `RelationshipCode`). **Yapmaz:** çok-hop traversal, en-iyi-yol, skorlama, öneri,
otomatik içerik seçimi. Veri yoksa **boş döner** — varsayılan uydurulmaz (MOD-0151 R11 ruhu).

**Sabit derinlik sözleşmesi (AC-GRAPH-DEPTH):** `by-node` **tam 1 hop**; `by-content` **tam 2 kenar-katmanı**
(content → bağlı node'lar → o node'ların 1-hop komşuları) döndürür ve **daha derine inmez**. Bu, "içerikten 2-hop"
görünümünün bilinçli ve **sabit** olduğunu, açılabilir/parametrik bir traversal derinliği **olmadığını** belirtir;
`depth`/`maxHops` gibi bir sorgu parametresi **yoktur** ve eklenmesi motor kararıdır (F4/MOD-0058). Katman sayısı
sabit olduğundan çıktı, giriş büyüklüğünde lineerdir; recursive/transitif kapanış **hesaplanmaz**.

### 6.2 Contract flags

```json
{ "supportsSubjectConceptGraph": true, "supportsConfigurableConceptChain": true,
  "supportsConceptType": true, "supportsConceptNode": true,
  "supportsConceptRelationship": true, "supportsConceptChainTemplate": true,
  "supportsContentConceptLink": true, "supportsArchiveLifecycle": true,
  "supportsEffectiveDating": true, "supportsCycleDetection": true,
  "supportsTemplateConformanceDiagnostics": true, "supportsContractDrivenUi": true }
```

**ASLA eklenmez (false olarak bile):** `supportsRecommendationEngine` · `supportsAiPersonalization` ·
`supportsGraphTraversalEngine` · `supportsBestNextContent` · `supportsVisitPlanning` · `supportsRoutePlanning` ·
`supportsDigitalDetailing` · `supportsWorkflowApproval` · `supportsHardDelete`.
**FU02 contract'ı değişmez** — `SupportsConceptGraphReference` alanı ve 7 flag'i olduğu gibi kalır.

---

## 7. UI Scope

**Sayfa:** `/CRM/Knowledge/Concepts` — tenant shell, mevcut CRM Admin → Knowledge nav'ının altında ikinci `<li>`.
Controller **proxy-only** (`frontend/Diten.Web/Controllers/CRM/KnowledgeConceptsController.cs`), iş kuralı taşımaz.

| Sekme | Golden Ref | İçerik |
|---|---|---|
| 1 · Concept Types | Slim | subject seçici + type list/create/edit/archive; `SortOrder` |
| 2 · Concept Nodes | **Compact** (primary surface) | type'a göre filtreli list/create/edit/details/archive; `ExternalRef` çifti; effective window |
| 3 · Connections | Slim | from/to node picker (aynı subject'e kısıtlı) + type/direction/priority; **non-conforming rozeti** |
| 4 · Chain Templates | Slim | ordered type builder (min 2, tekrar yok) + version + status |
| 5 · Graph Preview | read-only | seçili subject için node + edge listesi, zincir görünümü; **salt okunur, motor yok** |

### 7.1 Golden-reference yüzey haritası + verifier beklentisi

Bu sayfa **hibrit** bir yüzeydir: **birincil** aggregate (ConceptNode) tam Compact set alır; yardımcı aggregate'ler
Slim alt-form canvas olarak açılır; Graph Preview salt-okunur olduğu için hiçbir golden-reference'a girmez. Verifier
her yüzeyi **kendi referansıyla** doğrular — tek bir global `--reference` yeterli değildir.

| Yüzey (aggregate) | Golden ref | Zorunlu dosya seti | Verifier komutu | DataTable |
|---|---|---|---|---|
| **ConceptNode** (Tab 2, primary) | **Compact** | 8 dosya (§11 tam liste) | `--reference compact` | var (v2, archive-only) |
| ConceptType (Tab 1) | **Slim** | Slim alt-form seti (`_CreateEditOffcanvas` + `_DetailsQuickView`) | `--reference slim` | var (v2, archive-only) |
| ConceptRelationship (Tab 3) | **Slim** | Slim alt-form seti | `--reference slim` | var (v2, archive-only) |
| ConceptChainTemplate (Tab 4) | **Slim** | Slim alt-form seti | `--reference slim` | var (v2, archive-only) |
| Graph Preview (Tab 5) | **N/A** (read-only) | golden set YOK; DataTable YOK; yalnız salt-okunur render | verifier'dan **muaf** (`N/A`) | yok |

**Slim ↔ Compact ayrımı gerekçesi:** ConceptNode primary surface'tir (FU02 `KnowledgeContent.ConceptNodeId` onu
işaret eder) ve kullanıcı-form alanı **> 8** olduğundan Compact zorunludur (bkz. alan türetmesi altta). Diğer üç
aggregate'in create/edit formları **≤ 8** kullanıcı alanı taşır (Type: code/name/description/sortOrder/status ≈ 5;
Relationship: from/to/type/direction/priority/code/name ≈ 7; Template: chainCode/name/description/orderedTypes/version/
status ≈ 6) → DEV-0000 Slim eşiği. Compact dosyalarını (`Create/Edit/Details.cshtml`) bu yüzeylere açmak Golden
Reference sapması sayılır; **Compact yalnız Node'da**.

**ConceptNode kullanıcı-form alanı türetmesi (form_field_count = 10):** §4.2 tablosunda **14 entity alanı** vardır;
bunların sistem/audit-yönetimli olanları form girişi değildir:

- **Form-dışı (4):** `Id` (üretilir), `TenantId` (JWT claim), `ArchivedAt` (lifecycle), `EffectiveTo` — ayrı bir
  giriş değil, `EffectiveFrom` ile **tek "effective window" kontrolü** içinde render edilir. (`CreatedAt/By`,
  `UpdatedAt/By` de audit; §4.2 tablosunda ayrı satır değiller.)
- **Kullanıcı-form alanı (10):** `SubjectId`, `ConceptTypeId`, `ConceptNodeCode`, `ConceptNodeName`, `Description`,
  `Status`, `EffectiveFrom` (window kontrolü), `ExternalRefType`, `ExternalRefId`, `MetadataJson`.

**10 > 8 → Compact doğrulanır** (FU02 emsalinin "18 kullanıcı alanı > 8 → Compact" türetme biçimiyle aynı yöntem).

**AC-UI karşılıkları:**
- **AC-UI-1** Knowledge UI'da `Subject` = *knowledge domain*; legacy "Subject Type" ile karıştırılmaz (etiket + yardım metni).
- **AC-UI-3** FU02 içerik formundaki **disabled `ConceptNodeId` alanı canlı selector'a dönüşür**
  (`frontend/Diten.Web/Views/CRM/Knowledge/_Form.cshtml:134-139` — bugün "runtime source yok" diye disabled).
  Subject → Type → Node zincirli seçici; **archived node listelenmez**. Bu, FU02'nin tek UI dokunuşudur ve gereksiz
  refactor sayılmaz — alan zaten oradadır.
- **AC-UI-4** `AudienceProfile` (FU01 master) ↔ "`audience-profile` adlı ConceptType" ayrımı UI'da açık not olarak görünür.
- **AC-UI-2** **Global Product picker:** node `ExternalRefType=global-product` seçildiğinde picker, MDM Global Products
  selector'ını tüketir → **`GET /api/global-products/selector`** (Gateway `:5000`; dönen alanlar `{ Id, CanonicalCode,
  GlobalProductName }`; seçilen `Id` → `ExternalRefId`). Çağrı tüketici kullanıcının **`mdm.global-products.read`**
  iznini gerektirir. **404** (endpoint yok) **veya 403** (izin yok) → **input disabled + gerekçe notu** (sessiz boş
  liste değil). Picker **read-only**; MDM master'ı okunur, yazılmaz/mutate edilmez. *(MOD-0290-FU02 Brand/Product
  endpoint referansı kaldırıldı.)*

**L10n:** 7 dil RESX pariteli (`ar/en/es/fr/ru/tr/zh`) + `SharedResource` menü anahtarı ×7.
**DataTable verifier:** archive-only modül → **6 bulk-delete kontrolü N/A** (FU02 emsali).

---

## 8. Validation Rules

| # | Kural | Sonuç |
|---|---|---|
| V01 | `TenantId` payload'da → yok sayılır, claim kazanır | 2xx (sessiz ignore) |
| V02 | `ConceptTypeCode` `(tenant, subject)` içinde duplicate (non-archived) | **409** |
| V03 | Archived/olmayan `SubjectId` ile type create | **400** |
| V04 | Archived `ConceptTypeId` ile node create | **400** |
| V05 | Node'un `SubjectId`'si tipinin subject'i ile uyuşmuyor | **400** |
| V06 | `ConceptNodeCode` `(subject, type)` içinde duplicate | **409** |
| V07 | `From == To` | **400** |
| V08 | Cross-subject ilişki | **400** |
| V09 | Archived node ile ilişki create | **400** |
| V10 | `active` ilişkilerde **cycle** oluşuyor | **400** (read-time hesap; cache yok) |
| V11 | Aynı `(From, To, RelationshipType)` ikinci **active** kayıt | **409** |
| V12 | Template `OrderedConceptTypes` < 2 · yabancı subject tipi · tekrarlı tip | **400** |
| V13 | Aynı `ChainCode` + örtüşen effective pencere + iki `published` | **409** |
| V14 | `EffectiveTo < EffectiveFrom` (dört nesnede de) | **400** |
| V15 | Archived kayıt update | **409** |
| V16 | Template'e uymayan `(fromType→toType)` | **kabul edilir**, `IsTemplateConforming=false` + diagnostics |
| V17 | `KnowledgeContent.ConceptNodeId` canlı olmayan/archived/başka tenant node | **400** (yalnız yeni create/update) |
| V18 | Archived content veya archived node ile link create | **400** |
| V19 | Bilinmeyen status / relationship-type / link-role değeri | **400** (fail-closed, in-domain) |
| V20 | Subject archive → bağlı Type/Node/Relationship/Template **archived kabul edilir**, silinmez | okunur kalır |
| V21 | Link'te `ConceptRelationshipId` verildi ama o ilişki, link'in demirlendiği `ConceptNodeId`'yi (`From`/`To`) **içermiyor** | **400** (§4.5; node'suz saf ilişki-linki yok) |
| V22 | `ConceptNodeId` PUT'ta **değişmedi** (aynı değer / payload'da yok) | node-resolution **atlanır**, 400 üretilmez (§5 dirty-check) |

---

## 9. Test Plan (min. kapsam)

Backend (`Diten.CrmService.Application.Tests`), hedef **≥ 33 test**:

1. Type/Node/Relationship/Template/Link × create · update · archive (mutlu yol) — 15
2. V02–V06 duplicate & archived-parent redleri — 5
3. V07 self-loop · V08 cross-subject · V10 **cycle** (2-hop ve 3-hop) · V11 duplicate-active — 5
4. V12/V13 template kuralları (min-2, tekrar, örtüşen published) — 3
5. Tenant isolation: başka tenant'ın node'u ilişkide/linkte **görünmez ve bağlanamaz** — 2
6. V01 `TenantId` payload injection yok sayılır — 1
7. V15 archived update 409 · V14 effective window 400 — 2
8. V17 `KnowledgeContent.ConceptNodeId` → canlı node bağlanır / archived node **reddedilir**; mevcut satır bozulmaz — 3
9. V16 non-conforming ilişki **kabul edilir ve flag'i response'ta görünür** — 1
10. Contract endpoint 12 flag `true`, 9 yasak flag **absent** (false bile değil) — 1
11. **Regression:** FU02'nin 23 testi ve `IKnowledgeContentLinkageReader` davranışı **değişmeden PASS** — mevcut suite
12. **RegisterClassMaps:** 5 yeni aggregate kayıtlı (aksi hâlde Guid FK binary yazılır, filtreler sessizce boş döner)
13. **V22 dirty-check:** legacy dangling `ConceptNodeId`'li content'in başka alanı düzenlenip PUT → **200** (node-resolution atlanır); aynı alan **değiştirilirse** archived/yabancı node → **400** — 2
14. **V21 link-ilişki tutarlılığı:** `ConceptRelationshipId` verilen link, o node'u içermeyen ilişkiye → **400**; içeren ilişkiye → **201** — 1
15. **AC-GRAPH-DEPTH:** `by-content` tam 2 kenar-katmanı döner, 3. katman komşusu response'ta **yok** (sabit derinlik) — 1

---

## 10. Smoke Plan (authenticated, Gateway)

Script: `scripts/smoke-mod0162-fu03-concept-graph-authenticated.ps1` (FU02 script'i şablon).
Tenant `97c59330…`, login **`X-Tenant-Id` header ile** (aksi hâlde platform …0001 token'ı gelir).

```text
 1 login → token
 2 GET  concept-graph/contract                       → 200, 12 flag true, 9 yasak flag absent
 3 POST concept-types (subject = FU02 subject'i)     → 201
 4 POST concept-types duplicate code                 → 409
 5 POST concept-nodes (type#1)                       → 201
 6 POST concept-nodes (type#2)                       → 201
 7 POST concept-relationships (n1→n2)                → 201
 8 POST concept-relationships (n1→n1)                → 400
 9 POST concept-relationships (n2→n1) cycle          → 400
10 POST concept-chain-templates (type1,type2)        → 201
11 GET  concept-graph?subjectId=…                    → 200 (2 node + 1 edge + 1 template)
12 GET  concept-graph/by-node/{n1}                   → 200
13 PUT  knowledge content { conceptNodeId = n1 }     → 200
14 POST content-concept-links (content, n2)          → 201
15 GET  concept-graph/by-content/{contentId}         → 200 (n1 + n2 görünür)
16 POST … { tenantId: "<yabancı>" }                  → tenant claim kazanır (yabancı tenant yazılmaz)
17 POST concept-nodes/{n2}/archive                   → 200
18 PUT  concept-nodes/{n2}                           → 409
19 PUT  knowledge content { conceptNodeId = n2 }     → 400 (archived node)
20 DELETE / PATCH herhangi bir route                 → 404
21 Campaign kaydı DEĞİŞMEDİ (before/after diff)      → identical
22 cleanup: archive-only (hard delete YOK)
```

---

## 11. Repo Scope

```text
services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/                            (+5 dosya)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Concept/**   (yeni)
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Contract/    (yeni ConceptGraphContract)
services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs          (RegisterClassMaps + index)
services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/                        (+4/5 controller)
services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/                        (+1/2 dosya)
frontend/Diten.Web/Controllers/CRM/KnowledgeConceptsController.cs                          (yeni, proxy-only)
frontend/Diten.Web/Models/CRM/KnowledgeConceptViewModels.cs                                (yeni)

# --- Views/CRM/KnowledgeConcepts/ — hibrit golden-reference seti (glob açıldı, §7.1) ---
# ConceptNode primary surface → DEV-0001 Compact 8 zorunlu dosya:
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/Index.cshtml                                (Layout="_LayoutTenantShell" açık)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/Create.cshtml                               (Compact-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/Edit.cshtml                                 (Compact-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/Details.cshtml                              (Compact-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_Form.cshtml                                (Compact-özel; Node)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_Filter.cshtml
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_DataTable.cshtml                           (data-dt-standard="v2" + skeleton)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_IndexL10n.cshtml
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.cs                   (marker class)
# ConceptType / ConceptRelationship / ConceptChainTemplate → DEV-0000 Slim alt-form seti (her aggregate için):
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TypeCreateEditOffcanvas.cshtml            (Slim-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TypeDetailsQuickView.cshtml               (Slim-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_RelationshipCreateEditOffcanvas.cshtml    (Slim-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_RelationshipDetailsQuickView.cshtml       (Slim-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateCreateEditOffcanvas.cshtml        (Slim-özel)
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateDetailsQuickView.cshtml           (Slim-özel)
# Graph Preview (Tab 5) → read-only, golden set YOK, DataTable YOK:
frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_GraphPreview.cshtml                        (salt-okunur render, verifier N/A)
frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/index.js                        (+ index.l10n.js)
# --- FU02'ye AC-UI-3 kasıtlı dokunuşu (§12 istisna) — hepsi additive; FU02 sözleşmesi (KnowledgeContent alanları + IKnowledgeContentLinkageReader imzası) DEĞİŞMEZ, yeni endpoint AÇILMAZ ---
frontend/Diten.Web/Views/CRM/Knowledge/_Form.cshtml                                        (AC-UI-3 — disabled ConceptNodeId → canlı selector)
frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs                                   (AC-UI-3 — ConceptType/ConceptNode option yüklemesi + EnsureSelectedAsync; yeni endpoint yok)
frontend/Diten.Web/Models/CRM/KnowledgeContentViewModels.cs                                 (AC-UI-3 — ConceptTypeOptions/ConceptNodeOptions eklendi; KnowledgeContent alan sözleşmesi genişlemez, kalıcı tek değer ConceptNodeId)
frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/form.js                                  (AC-UI-3 — setupConceptCascade; cascade yalnız gerçek kullanıcı değişiminde, V17 dirty-check korunur)
frontend/Diten.Web/Resources/**                                                            (7 dil RESX)
frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml                                  (tek <li>, dar istisna)
scripts/smoke-mod0162-fu03-concept-graph-authenticated.ps1                                 (yeni)
docs/audits/mod-0162-fu03-concept-graph-runtime-ui-*.md                                    (evidence)
```

## 12. Protected Paths

`gateway/Diten.ApiGateway/ocelot.json` (**değişmez** — wildcard yeterli) · `services/Diten.MdmService/**` ·
MOD-0165 Campaign runtime · MOD-0164 Consent/Preference · MOD-0155 · FU02'nin `KnowledgeContent` alanları ve
`IKnowledgeContentLinkageReader` imzası · RBAC seed / role template · MOD-0048 publish · Mongo hand-edit ·
`execution/registries/**` (yalnız closeout'ta, kullanıcı onayıyla) ·
**FU01A / FU01B pack dosyaları** (`MOD-0162-FU01A-*.md`, `MOD-0162-FU01B-*.md` — bu FU onların boundary'sini
implement ETMEZ; içerikleri okunur, değiştirilmez).

**Kasıtlı dokunulan istisnalar (protected DEĞİL — açıkça izinli, dar kapsam):**
- `frontend/Diten.Web/Views/CRM/Knowledge/_Form.cshtml` — **AC-UI-3 tek dokunuş**: bugün disabled olan
  `ConceptNodeId` alanı (satır 134-139) canlı Subject→Type→Node zincirli selector'a dönüşür. Alan zaten oradadır;
  yeni alan/endpoint eklenmez, FU02 form sözleşmesi genişletilmez.
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — CRM Admin → Knowledge nav'ına **tek `<li>`**
  (Concepts) eklenir; dar shell istisnası, başka menü öğesi/oturum davranışı değişmez.

---

## 13. Permission / Visibility

```text
crm.knowledge.concept.read
crm.knowledge.concept.manage
crm.knowledge.concept-template.manage
crm.knowledge.concept-link.manage
```

**TANIM ONLY — seed/grant YOK** (FU01C §14 + AC-SEQ-3, kullanıcı kararı: RBAC en sona).
Katalogda `crm.knowledge.*` henüz yok → FU02'nin belgelenmiş fallback'i kullanılır
(`crm.territory.read` / `crm.territory.model.manage`). Fallback **hiçbir guard'ı gevşetmez** (endpoint'ler yine
authenticated + policy-korumalı; fail-closed).

> **⚠️ Fallback = YALNIZ dev/smoke, geçici.** `crm.territory.*` yeniden kullanımı, territory yetkisi olan bir
> kullanıcının concept-graph yönetebilmesi demektir — bu **kabul edilebilir tek gerekçe local/dev doğrulamasıdır**.
> **Prod'a taşınamaz.** Kanonik `crm.knowledge.concept.*` anahtarları katalog + grant ile açılana kadar bu fallback
> **dev-only** işaretlidir; closeout, prod entitlement'ı **MOD-0162-FU03-RBAC** follow-up'ına devreder ve fallback'i
> kaldırır. Fallback ile prod tenant'a grant **YASAK**.

**Cross-service izin bağımlılığı (yeni — Global Product picker):** Node `ExternalRefType=global-product` picker'ı
MDM'in `GET /api/global-products/selector` endpoint'ini çağırır; bu endpoint **`mdm.global-products.read`** izni ister
(MDM tarafında `[HasPermission("mdm.global-products.read")]`, MOD-0290 sahipliğinde). Tüketici kullanıcıda bu izin
**yoksa 403** → picker **disabled + gerekçe** (AC-UI-2), FU03 hiçbir guard'ı gevşetmez. Bu izin **MDM-owned**'dır;
**bu pack seed/grant ETMEZ** — grant AYRI iş (MOD-0290 / F-RBAC kapsamı). FU03'ün kendi `crm.knowledge.concept.*`
anahtarları da §13 üstündeki gibi tanım-only kalır.

Follow-up: **MOD-0162-FU03-RBAC**.

---

## 14. Explicit Exclusions

graph traversal engine · concept resolution engine · recommendation engine · AI personalization · scoring ·
best-next-content · **KnowledgePath / ContentPackage (FU01A — AC-BOOK-1)** · EngagementJourney (FU01B) ·
visit planning · route planning · MicroTarget · activity/timeline · segmentation/ICP · commercial model routing ·
opportunity funnel · offer/contract · digital detailing · content usage tracking · workflow approval (MOD-0023) ·
file upload/render/preview · learning completion · patient data · **Campaign mutation** ·
**MDM master mutation (MDM Global Product dahil — yalnız read-only ExternalRef referansı serbest)** ·
**MDM write** · Account/Contact/territory mutation · cross-subject bridge (F6) · özyinelemeli/dallanan template (F7) ·
denormalize traversal cache · hard delete · `TenantId` payload · **RBAC seed/grant** · **MOD-0048 publish** ·
`ocelot.json` değişikliği · registry write · Mongo hand-edit.

---

## 15. Implementation Notes (repo'dan çıkarılan tuzaklar)

1. **RegisterClassMaps** — 5 yeni aggregate `Persistence/DependencyInjection.cs`'e eklenmezse Guid FK'lar binary
   yazılır, filtreler **sessizce boş döner** (MOD-0151 FU05 / AccountTerritoryAssignment dersi).
2. **Parallel-array tuzağı** — `EffectiveFrom` + `EffectiveTo` (ikisi de `DateTimeOffset`) **birlikte index'lenmez,
   birlikte sort edilmez**; gerekirse in-memory sort.
3. **Partial index `$ne` yasak** — `Filter.Ne(x, null)` içeren partial index Mongo'da servisi **başlangıçta
   crash-loop**'a sokar; `Filter.Type(...)` / `$lt` kullan.
4. **Standalone Mongo transaction** — çok dokümanlı atomik yazım varsa `SupportsTransactionsAsync` guard'ı +
   compensation; aksi hâlde dev ortamında 500.
5. **Cycle detection read-time** — denormalize traversal cache **v1'de yok** (o bir engine kararı → F4/MOD-0058).
6. **Fleet restart** — `.resx` değişiklikleri tam restart ister; build kilidinde `-t:CoreCompile` yöntemi.
7. **L10n bridge** — `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları `undefined`.

---

## 16. Kararlar (kullanıcı onayı ALINDI — 2026-08-25)

Aşağıdaki beş karar kullanıcı tarafından **onaylandı ve kapatıldı** (2026-08-25). Pack gövdesi bunlara göre revize
edildi; açık madde kalmadı.

| # | Konu | **KARAR (kapandı)** |
|---|---|---|
| **D1** | Bu FU'nun kimliği: `MOD-0162-FU03` mü, `MOD-0162-FU01C-IMPL` mi? | ✅ **FU03** — FU01→FU02 emsali; DCP-002 PASS |
| **D2** | `KnowledgeContentConceptLink` bu FU'da mı, FU01A'ya mı ertelensin? | ✅ **Bu FU'da** (§4.5 gerekçesi); additive, sonra ikinci migration gerekmez |
| **D3** | `concept-relationship-type` vokabüler kaynağı / kanonik set | ✅ **A — Boundary seti.** Kanonik = FU01C §5: `leads-to` / `requires` / `addresses` / `evidences` / `belongs-to` / `custom`. Authoring template'indeki sapan değerler (`related-to` / `depends-on` / `targets` / `maps-to` / `replaces`) **KALDIRILDI/eşlendi** (bkz. §4.3 notu). Doğrulama **in-domain** yürür (FU02 ile tutarlı; operatör publish'ine bağımlılık yok) → dev değerleriyle **çelişki YOK**. Publish yine ayrı operatör işidir (F-RD). **MOD-0164-FU02 sapması tekrar etmez.** |
| **D4** | `KnowledgeContent.ConceptNodeId` doğrulaması geriye dönük uygulansın mı? | ✅ **Hayır** — yalnız yeni create/update **ve yalnız alan değiştiğinde** (dirty-check, §5 V17); dokunulmamış legacy değer PUT'ta 400 üretmez |
| **D5** | FU02 `_Form.cshtml` concept selector dokunuşu kabul mü? | ✅ **Evet** — alan zaten var ve disabled; AC-UI-3 bunu ister (§12 kasıtlı istisna) |

---

## Dependencies (gövde — frontmatter'ın açıklamalı karşılığı)

| Bağımlılık | Yön | Sözleşme / etki |
|---|---|---|
| **MOD-0162-FU01C** (approved boundary) | implement eder | §3–§7/§13 sözleşmesi BURADA runtime'a döner; adlandırma/kurallar boundary'den gelir, sapma §2'de gerekçeli |
| **MOD-0162-FU01C-ADDENDUM** | tüketir | AC-MAP/AC-MODEL/AC-BOOK/AC-LINK/AC-FU02/AC-SEQ/AC-UI → konsolide AC bölümüne taşındı |
| **MOD-0162-FU02** (SHIPPED) | **hard prerequisite** | `KnowledgeContent` + `Subject/Topic/AudienceProfile` + `IKnowledgeContentLinkageReader` **kırılmaz** (§5); FU02 çalışır durumda olmadan bu FU açılmaz |
| **MOD-0162-FU01A** (KnowledgePath) | **implement ETMEZ** | AC-SEQ-1 gereği sonraki FU; UCLNBook oraya gider (F-PATH) |
| **MOD-0162-FU01B** (EngagementJourney) | **implement ETMEZ** | boundary okunur, dokunulmaz |
| **MOD-0290 / MDM** (Global Product SoT) | referans (read-only) | yalnız `ExternalRef` (`ExternalRefType=global-product`); master kopyalanmaz/mutate edilmez. Picker `GET /api/global-products/selector`'ı tüketir ve tüketici kullanıcının **`mdm.global-products.read`** iznini gerektirir → **404/403 ise input disabled + gerekçe** (AC-UI-2). **İzin grant'ı AYRI iş, bu pack'te YOK** (F-RBAC kapsamı) |
| **MOD-0165-FU04** (Campaign) | çözümlenebilir kılar | `Campaign.ConceptChainTemplateId` sarkan referansı bu FU template açtıktan sonra picker'a bağlanabilir (F-CAMP); **Campaign DEĞİŞMEZ** |
| **MOD-0048** (reference data) | ileride tüketir | `concept-status`/`concept-relationship-type`/`concept-chain-status` publish **ayrı operatör işi** (F-RD); dev'de in-domain yürür |
| **MOD-0058 / MOD-0057** | boundary | enterprise graph & tag/taxonomy governance orada; burada **motor açılmaz** |
| **MOD-0018** (RBAC) | yalnız tüketim | seed/grant YOK; dev fallback `crm.territory.*` (§13); F-RBAC en sonda |
| **DEV-0001 / DEV-0000** | golden reference | Node=Compact, Type/Relationship/Template=Slim, Graph Preview=N/A (§7.1) |

---

## Acceptance Criteria (konsolide — ADDENDUM AC'leri + bu FU kararları)

Tüm maddeler **test edilebilir** (backend testi §9 veya smoke §10 ile eşlenir).

**Model & boundary**
- **AC-MAP-1** Node hiçbir master'ın SoR'u değildir; global-product/document/audience-profile/reference-data-value **yalnız** `ExternalRefType`+`ExternalRefId` ile bağlanır — ayrı FK kolonu (**`GlobalProductId` dahil**) **yok** (§4.2). *Test:* şema/DTO'da `GlobalProductId/BrandId/ProductId/TopicId` alanı **bulunmaz**; `ExternalRefType` seti = `{global-product, document, audience-profile, reference-data-value, other}`.
- **AC-MODEL-1** `ConceptType/Node/Relationship/ChainTemplate` **ayrı aggregate**; parent-FK hiyerarşisi yok, hiyerarşi Relationship+ChainTemplate ile ifade edilir (D3/§2). *Test:* cross-subject ilişki **400** (V08), `From==To` **400** (V07).
- **AC-BOOK-1** `KnowledgePath/ContentPackage` (UCLNBook) bu FU'ya **gömülmez**; §14'te explicit exclusion. *Test:* repo'da Path/Package aggregate/endpoint **yok**.
- **AC-LINK-1** `KnowledgeContentConceptLink` many-to-many açılır; her link bir node'a demirlenir, opsiyonel olarak o node'u içeren bir ilişkiye bağlanır (§4.5). *Test:* node'suz link **400**; ilişki node'u içermiyorsa **400** (V21).
- **AC-GRAPH-DEPTH** `/concept-graph` sabit derinlik: `by-node`=1 hop, `by-content`=2 kenar-katmanı; `depth/maxHops` parametresi **yok**, transitif kapanış hesaplanmaz (§6.1). *Test:* by-content 2-katman döner, 3. katman **görünmez**.

**FU02 koruması**
- **AC-FU02-1..4** `KnowledgeContent` atomik kalır; `ResolvePublishedContentAsync` imza/davranışı değişmez; DELETE/PATCH yok; tüm değişiklik additive (§5). *Test:* §9-11 regression — FU02'nin 23 testi **değişmeden PASS**.
- **AC-FU02-V17** `ConceptNodeId` doğrulaması yalnız **create** veya **değişen** değerde çalışır; dokunulmamış legacy değer PUT'ta **400 üretmez** (§5, V22). *Test:* legacy dangling node'lu içerik başka alan düzenlenip Save → **200**.

**UI**
- **AC-UI-1** `Subject` = knowledge domain; legacy "Subject Type" ile karıştırılmaz (etiket + yardım metni). *Test:* view'da açıklayıcı not render edilir.
- **AC-UI-2** `ExternalRefType=global-product` seçilince picker `GET /api/global-products/selector`'ı (`:5000`, `{Id,CanonicalCode,GlobalProductName}`) tüketir; **`mdm.global-products.read`** izni ister. **404** (endpoint) veya **403** (izin) ise **input disabled + gerekçe notu** (sessiz boş liste değil). *Test:* 404 **ve** 403 senaryolarında disabled state + gerekçe; seçilen `Id` → `ExternalRefId`.
- **AC-UI-3** FU02 `_Form.cshtml`'deki disabled `ConceptNodeId` → canlı Subject→Type→Node selector; archived node listelenmez (§7). *Test:* selector render + archived node hariç.
- **AC-UI-4** `AudienceProfile` (FU01 master) ↔ `audience-profile` ConceptType ayrımı UI'da açık not. *Test:* not görünür.
- **AC-UI-GR** Golden-reference paritesi: Node=Compact 8 dosya, Type/Relationship/Template=Slim set, Graph Preview verifier'dan muaf (§7.1/§11). *Test:* `--reference compact` (Node) + `--reference slim` (diğer 3) PASS; 6 bulk-delete kontrolü archive-only → **N/A**.

**Sıralama & vokabüler**
- **AC-SEQ-1** ConceptGraph → Package sırası korunur (FU01A sonraki FU). **AC-SEQ-2** legacy migration crosswalk implementation'dan önce tanımlanır (F-MIG — **greenfield authoring için ön koşul DEĞİL**). **AC-SEQ-3** RBAC en sonda (F-RBAC).
- **AC-VOCAB (D3=A)** `RelationshipType` yalnız kanonik set (`leads-to/requires/addresses/evidences/belongs-to/custom`); sapan authoring değerleri eşlendi (§4.3 notu). *Test:* set dışı değer **400** (V19).

---

## 17. Ready-for-dev Checklist

- [x] Boundary (FU01C) `approved` ve okundu; sapmalar §2'de gerekçelendi
- [x] ADDENDUM AC'leri DoD'ye taşındı (AC-MAP / AC-MODEL / AC-BOOK / AC-LINK / AC-FU02 / AC-SEQ / AC-UI)
- [x] DCP-002 kimlik kapısı PASS
- [x] Gateway route ihtiyacı **yok** doğrulandı (wildcard mevcut)
- [x] Prerequisite FU02 **shipped/PASS** doğrulandı
- [x] Golden-reference yüzey haritası + dosya seti + verifier beklentisi netleşti (§7.1/§11 — Node=Compact 8 dosya,
      Type/Relationship/Template=Slim, Graph Preview=N/A)
- [x] `form_field_count` kullanıcı-alan türetmesiyle gerekçelendi (§7.1 — 10 > 8 → Compact)
- [x] §16 **D1–D5 kullanıcı kararı ALINDI** (2026-08-25): D1=FU03 · D2=bu FU · D3=A(boundary seti) · D4=hayır(+V17 dirty-check) · D5=evet
- [x] Konsolide Acceptance Criteria + gövde Dependencies bölümleri eklendi (20-bölüm hizası)
- [ ] **AC-SEQ-2 — legacy migration crosswalk** (Subject/SubjectList/UCLEType/UCLNList/UCLNConnection/UCLNDesign/
      UCLNBook/PromoSubject → external-ID) tanımlı **DEĞİL** → §18/F-MIG. **Bilinçli açık:** bu pack yalnız greenfield
      authoring açar; crosswalk **ConceptGraph implementation'ından önce** gerekir, **bu pack'in ready-for-dev'i için
      ön koşul DEĞİL** (AC-SEQ-2).
- [x] `status: ready-for-dev` + `runtime_code_allowed: true` — **RE-FLIP (2026-08-25)**: ExternalRef hedefi
      Brand/Product → MDM Global Product sözleşme değişikliği kontrol-kulesi tarafından doğrulandı (enum brand/product
      kaldırıldı + global-product eklendi, picker `/api/global-products/selector`, `mdm.global-products.read` bağımlılığı
      yazıldı, `GlobalProductId` dahil FK yok, boundary/motor/FU02/V17 korundu). Kalıntı canlı brand/product referansı yok.

---

## 18. Follow-up Items

| # | Follow-up | Neden |
|---|---|---|
| F-MIG | **Legacy ConceptGraph migration crosswalk** (AC-SEQ-2) | ConceptGraph implementation başlamadan tanımlanmalı; bu pack yalnız greenfield authoring açar |
| F-RD | **MOD-0048 concept set publish** (`concept-status` / `concept-relationship-type` / `concept-chain-status`) | D3 **çözüldü (A — boundary seti, §4.3)**; publish yine ayrı operatör işi, kanonik setle yayınlanır — dev in-domain değerleriyle çelişki yok |
| F-RBAC | **MOD-0162-FU03-RBAC** — `crm.knowledge.concept.*` katalog + grant | AC-SEQ-3 gereği en sonda |
| F-PATH | **MOD-0162-FU01A KnowledgePath / ContentPackage implementation FU** | AC-SEQ-1: ConceptGraph → Package sırası; UCLNBook oraya gider |
| F-CAMP | `Campaign.ConceptChainTemplateId` raw-GUID input → template picker | Bu FU template'i açtıktan sonra mümkün |
| F-L10N | **KnowledgeConcepts Slim tab anahtarlarının 5 dil çevirisi** (`ar` / `es` / `fr` / `ru` / `zh`) | Artım 2 / Dilim B'de 51 anahtar eklendi; `en`/`tr` gerçek, diğer 5 dil Dilim A konvansiyonuyla İngilizce placeholder taşıyor |
| F-VERIFY | `verify_datatable_page.py --reference slim` **hibrit yüzey desteği** | Script modül klasörü başına **tek** `_CreateEditOffcanvas.cshtml` varsayar; §7.1'in hibrit konsolu aggregate başına ayrı offcanvas ister (§11) → slim koşusunda 2 yapısal FAIL kalır (bkz. Dilim B audit) |
| F-UI-DETAILS | FU02 `Views/CRM/Knowledge/Details.cshtml` — `ConceptNodeId` ham GUID yerine çözümlenmiş `kod — ad` göstersin | AC-UI-3 yalnız **form** alanını adlandırır; Dilim C Details'e dokunmadı (kapsam genişletmesi olurdu). Artık FU03 runtime canlı olduğu için çözümleme mümkün |
| F1 / F4 / F6 / F7 / F8 | FU01C §19'dan devralınan açık maddeler | Kapsam dışı, kayıt için |
