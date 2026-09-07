---
id: UNASSIGNED
slug: product-regulatory-pharmacovigilance-traceability-readiness
name: Product, Regulatory and Pharmacovigilance Traceability Readiness
type: Delivery Capability Pack
standard: CAP-001
status: draft
owner_domain: master-data-management
accountable_owner: enterprise-architect
decision_owners: product-data-owner / regulatory-owner / pharmacovigilance-owner / quality-owner / privacy-security-owner
canonical_execution_branch: TBD-after-EA-reservation
authoring_branch_evidence: feature/mdm/dcp-006-product-regulatory-pv-readiness
created: 2026-09-06
canonical_blueprint: "docs/System Capability & Implementation Blueprint - master 8.1.xlsx"
management_input: "Project ongoing status report v 05 SEP 2026.xlsx — PVG; prior 04 AUG workbook retained as historical input"
last_reconciled: 2026-09-07
related_local_artifact: "C:/dev/ERP-vNext/execution/portfolio/delivery-capability-packs/DCP-005-material-product-master-data-coding-alignment.md"
related_artifact_identity: UNRESOLVED-COLLISION
related_artifact_evidence_state: local-unmerged
status_note: "Planning and review only. EA must reserve a collision-free DCP identity. No runtime, data, configuration, migration, commit, push or production authority."
---

# UNASSIGNED DCP — Product, Regulatory and Pharmacovigilance Traceability Readiness

> **Belge türü:** CAP-001 kapsamındaki Delivery Capability Pack. Runtime entity, Module Pack, üretim modülü veya
> MOD-0014 Capability Group değildir.
>
> **Kanıt ayrımı:** Bu belge üç farklı gerçeği birbirine karıştırmaz: Master 8.1 TO-BE kimlik/ownership kaynağı,
> güncel birleşmiş `main` code truth ve henüz birleşmemiş yerel Product/Item/SKU adayı. Yönetim çalışma kitabı iş
> sırası girdisidir; implementation kanıtı değildir.
>
> **Kodlama engeli:** Bu `draft`, hiçbir modülde code-start vermez. Her teslimat ayrıca geçerli bir Module Pack,
> doğru kimlik, owner onayı ve exact allow-list kapısından geçer.

## 1. Identity and status

| Alan | Değer |
|---|---|
| ID | `UNASSIGNED` — Enterprise Architect collision-free DCP reservation required |
| Ad | Product, Regulatory and Pharmacovigilance Traceability Readiness |
| Durum | `draft` |
| İlişkili yerel taslak | `DCP-005-material-product-master-data-coding-alignment.md`; current authoring worktree'de yok, root dirty/local-unmerged kanıttır ve `DCP-005` kimliği çakışmalıdır |
| Ana Blueprint | Master 8.1 `Blueprint_Data` |
| Merged code truth | `main` / `c965e5f6` — 2026-09-06 ölçümü |
| Local-unmerged code truth | `feature/mdm/mod-0290-product-identity-final-integration` / `ed5188dc` + dirty delta |
| Production yetkisi | Yok |

Repo içinde `DCP-006` birden fazla capability tarafından kullanıldığı için bu draft'a numara atanamaz. Yeni sıra
numarası varsayımla seçilmeyecek; mevcut DCP kimlik çakışması ayrı governance reconciliation ister.

Normal bir Module Pack yeterli değildir. Zincir MDM, R&D/Product Data, Regulatory/RIM, PV, Quality, Platform,
Security, Workflow, Evidence, Inventory ve dış sistem sınırlarını keser.

## 2. Business outcome

Amaç, bir güvenlik vakası veya sinyalden geriye doğru şu soruların kanıtlı cevaplanabilmesidir:

- Hangi madde, formülasyon, ürün, sunum, paket, ülke ve ruhsat etkilendi?
- Hangi Legal Entity hangi rolde hareket etti: MAH, üretici, tedarikçi veya yalnız veri erişimi olan grup şirketi?
- Ürün o pazarda yalnız ruhsatlı mı, fiilen tedarikte mi, askıda mı veya geri çekilmiş mi?
- Vaka oluştuğu tarihte geçerli ürün/ruhsat/paket gerçeği neydi?
- Sinyal hangi kanıtlardan doğdu, kim değerlendirdi, hangi karar ve regülatif aksiyon çıktı?
- İş WorkCenter'da kimdeydi; asıl lifecycle ve karar hangi kaynak modülde kaydedildi?

Hedef ilişki bağlamı aşağıdaki graph'tır; oklar onaylanmış `1:N` cardinality veya tek yönlü lifecycle anlamına
gelmez:

```text
Substance ──< Ingredient / Strength >── Pharmaceutical Product Definition
                                      └── Medicinal Product
Medicinal Product ──< Regulated Authorisation >── Authority / Jurisdiction / MAH
Medicinal Product ──< Packaged Product ──< Package Item
Packaged Product ──< contains >── Manufactured Item Definition

Diten GlobalProduct / Revision / GSKU / LSKU / FinishedGood
  ──< explicit versioned mappings >── regulated product graph

Registered Presentation (Diten business abstraction)
  = explicit projection/reference over Medicinal Product
    + Regulated Authorisation + authorised package/status

Market Supply Assignment
  = Registered Presentation + Market + Supplier Legal Entity
    + LSKU/FinishedGood + validity

PV Case and Signal
  = independent aggregates with typed N:M references and immutable snapshots
    into the product/regulatory graph
```

Global Product, Product Definition Revision, GSKU, LSKU ve Finished Good bu omurganın MDM/ticari tarafıdır.
Her kenar için ayrı owner, cardinality, effective-date ve external-SoR reconciliation kararı gerekir.

## 3. Problem statement

Mevcut sistem ortak Platform temelleri ve Product/Item/SKU create/read yüzeyleri bakımından ilerlemiştir. Ancak
ürün-regülasyon-PV izlenebilirliğinde şu kritik kopukluklar vardır:

1. `GlobalProduct` yanında ayrı bir `Product` aggregate'i kendisini MOD-0290 Product SoR'u ilan eder. İki modelin
   cardinality ve ownership ilişkisi açık değildir.
2. Brand/Product runtime'ı canonical Global Product → Revision → GSKU → LSKU/Finished Good zincirine bağlı değildir.
3. Substance, Ingredient ve Composition/Formulation için operational SoR yoktur.
4. Marketing Authorisation, Registered Presentation ve Market Supply Assignment operational modelleri yoktur.
5. PV intake ürün bilgisini `SuspectProductText` olarak tutar; canonical product/substance/MA/presentation/pack
   çözümlemesi yoktur.
6. Signal Management yalnız contract/in-memory seviyesindedir; production persistence, authorization, audit,
   evidence, workflow ve product-context bağlantısı yoktur.
7. Governance kimlikleri çakışmaktadır: ABB ve Brand için `MOD-0290-FU01`, Market Supply ve Brand için
   `MOD-0290-FU02` kullanılmıştır.

Bu boşluklar kapatılmadan yalnız bir Signal ekranı geliştirmek, çalışan görünen fakat hangi maddeyi, ürünü, ruhsatı,
şirketi ve pazarı etkilediği güvenilir olmayan kayıtlar üretir.

## 4. Capability boundary

### Kapsamda

- Product/Brand/Global Product SoR ve kimlik sınırının düzeltilmesi.
- Material bağlantısının sınırı; detaylı material içeriğinin hangi aşamaya erteleneceği.
- Substance, ingredient, strength ve versioned composition/formulation sahipliği.
- Pharmaceutical, manufactured, packaged ve medicinal product mapping sınırları.
- Marketing Authorisation, Registered Presentation ve Market Supply Assignment.
- PV Case, MedDRA, Signal, reporting/submission ve PV quality sırası.
- Tenant, Legal Entity, organisation/location, market ve yasal rol ayrımı.
- Workflow/WorkCenter, audit, evidence, identifier, idempotency ve temporal kurallar.
- Yapılmış, kısmi, eksik, ertelenebilir ve dış sistemde kalabilecek yeteneklerin açık sınıflandırması.

### Kapsam dışında

- Runtime kodu, collection, API, UI, gateway route, permission, seed veya migration yazmak.
- Production/Staging enablement veya canlı veri mutation.
- Ayrıntılı raw-material specification, procurement, stok değerleme, BOM veya manufacturing recipe tasarlamak.
- Otomatik signal detection algoritması, NLP/AI veya tam dış regulator connector geliştirmek.
- Yeni numeric `MOD-xxxx` kimliği icat etmek.

## 5. Member modules and follow-ups

### Master 8.1 içinde kimliği bulunan üyeler ve dependency references

Tek ID taşıyan satırlarda ad canonical Blueprint adıdır. Aralık veya birden çok ID içeren satırlar yalnız grouped
summary'dir, canonical module adı değildir. Registry/module pack bulunmayan Blueprint dependency'leri runtime üyesi
sayılmaz; önce registry ve ayrı Module Pack kapısından geçer.

| Kimlik | Canonical sahiplik | DCP içindeki rol |
|---|---|---|
| MOD-0290 | Product / Item / SKU Master | Global Product, revision, GSKU, LSKU, Finished Good ve product identifier omurgası |
| MOD-0048 | Reference Data Management | Canonical provider adayıdır. Merged main'de market ile GSKU UoM/pack-applicability tüketimi kanıtlıdır; dosage form, route ve presentation-unit ayrıca publish/assignment/read-back kanıtı ister |
| MOD-0021 | Audit Trail Service | Merkezi, immutable, acknowledged audit SoR |
| MOD-0023 | Workflow Designer | Domain-owned lifecycle için workflow instance ve karar kanıtı |
| MOD-0024 | Task & Checklist Engine | WorkCenter görev/projection tüketimi |
| MOD-0031 | Evidence Linking Service | Object–evidence graph |
| MOD-0040 | Canonical ID & Correlation Standard | Uçtan uca identity/correlation |
| MOD-0209 | Change Control | Regulated change ve impact kararları |
| MOD-0230 | Case Intake & Triage | PV intake SoR başlangıcı |
| MOD-0231 | Case Processing | Safety Case master ve assessment lifecycle |
| MOD-0232 | MedDRA Coding | Version-bound coded terms |
| MOD-0233 | Reporting & Submissions | Individual case submission package/acknowledgement |
| MOD-0234 | Signal Management | Signal hypothesis, evaluation, decision ve action lifecycle |
| MOD-0235 | PV Quality | PV QC, inspection readiness ve CAPA references |
| MOD-0236 | Dossier/Submissions Management | Regulatory dossier ve submission SoR |
| MOD-0237 | Variations & Renewals | Market-specific variation/renewal decisions |
| MOD-0238 | Labeling Lifecycle | Label/artwork change, approval ve published versions |
| MOD-0239 | Country Requirements Matrix | Effective market/regulatory requirements |
| MOD-0244 | SAE Linkage (Clinical ↔ PV) | Clinical SAE ↔ Safety Case bridge |
| MOD-0173..0177 | Inventory and traceability | Inventory, lot/batch, quarantine, expiry ve recall consumers |
| MOD-0193 / MOD-0195 / MOD-0204 | Manufacturing consumers | BOM/routing, batch/eBR ve stability consumption |
| MOD-0252 / 0253 / 0258 / 0259 | External SoR boundaries | ERP, PLM, PV ve RIM integration; dual-SoR yasağı |
| MOD-0272 | Regulatory Gateways (FDA/EMA/PMDA) [External Provider] | FDA/EMA/PMDA transport ve receipts |

### Blueprint exact eşleşmesi bulunmayan capability boşlukları

| Capability | Geçici karar | Kimlik kuralı |
|---|---|---|
| Material Master | MOD-0290 item-master follow-up adayı; Product Data/Supply Chain owner kararı gerekir | Mevcut MOD/FU uygunsa kullan; değilse DCP-002 `CAND-CAP` |
| Substance and Ingredient Master | Material ile Composition arasındaki SoR sınırı çözülmeli | Numeric MOD icat edilmez |
| Development Formulation / Specification | R&D/Product Development owner'lı deneysel, versioned gerçek | DCP-005'e göre candidate-required |
| Regulatory Product Composition | Submitted/authorised ingredient, strength ve basis-of-strength gerçeği; development formulation'ı sessizce değiştirmez | Candidate/parent kararı gerekir |
| Marketing Authorisation and Registered Presentation | RIM owner'lı ayrı regulatory gerçek | Candidate-required |
| Market Supply Assignment | MOD-0290-FU02 taslağı karantinadadır; DCP-005 owner belirsizliği ve tek SoR owner kararı çözülmeli | MOD-0290 sahipliği reddedilirse DCP-002 `CAND-CAP`; çözülmeden runtime yok |
| Aggregate Safety Reporting | MOD-0233 FU önerisi yalnız owner kararı sonrası değerlendirilebilir | MOD-0233 kapsamı otomatik genişletilmez |
| PV Governance/PSMF, RMP, risk minimisation, literature, partner exchange, PASS, RSI | Mevcut PV/RIM modüllerinin FU'ları veya candidate olabilir | Her biri SoR/owner kararı ve DCP-002 kapısından geçer |

### Kimlik çakışması kapısı

- Canonical ABB pack'i `MOD-0290-FU01` olarak korunmuştur.
- Canonical Market Supply pack'i registry'de `MOD-0290-FU02` olarak görünür.
- Brand için aynı FU01/FU02 kimliklerini kullanan pack/runtime governance geçersizdir; owner ve DCP-002
  reconciliation olmadan yeni Brand code-start yapılamaz.
- Blueprint'te standalone Global Brand modülü yoktur. Bu durum Brand ihtiyacını otomatik reddetmez; fakat yeni root
  MOD üretmeye de izin vermez.

## 6. Ownership map

| Gerçek / ilişki | Önerilen SoR owner | Açıklama |
|---|---|---|
| Global Product / Product Definition Revision | MOD-0290 | Global ürün ailesi ve controlled revision |
| GSKU / LSKU / Finished Good | MOD-0290 | Global configuration, market-local SKU ve sellable/produced item kimliği |
| Brand master | Product Data/MDM candidate owner | Corporate/commercial brand identity; exact owner ve GlobalProduct cardinality kararı bekler |
| Authorised trade/product name | Regulatory/RIM | Jurisdiction, authorisation ve presentation bağlamındaki ad; Brand master değildir |
| Material identity | Material Master owner | Stok hareketi veya supplier qualification değildir |
| Substance | Substance Master owner | Stable external/internal identifier ve versioned definition |
| Development Formulation / Specification | R&D/Product Development | Deneysel formula, specification ve revision lineage; detailed material contents MVP sonrası olabilir |
| Regulatory Product Composition | Regulatory Product Data | Submitted/authorised ingredient role, strength, basis-of-strength, form ve validity; development formulation ile versioned promotion/mapping |
| Pharmaceutical Product | Product Science owner | Composition/form/route temelinde PhPID-benzeri identity mapping |
| Manufactured Item Definition | Regulatory Product/Product Data | IDMP package-contained item tanımıdır; MES operation, batch/lot veya Finished Good değildir. Manufacturing yalnız versioned mapping consumer/provider olabilir |
| Packaged Product / Package Item | Packaging/Product Data | Nested container, quantity, presentation unit ve GTIN mappings |
| Medicinal Product | Regulatory Product owner | Jurisdiction-aware regulated product identity |
| Marketing Authorisation | Regulatory/RIM | Authority, country, number, type, status, MAH ve effective dates |
| Registered Presentation | Regulatory/RIM | ISO IDMP entity'si değil Diten business abstraction'ıdır; Medicinal Product + Regulated Authorisation + authorised package/status mapping'i; LSKU değildir |
| Market Supply Assignment | Tek accountable SoR owner henüz kararsız | Registered Presentation + Market + supplying Legal Entity + LSKU + Finished Good; diğer domain validator/consumer olur, iki ortak writer olmaz |
| Safety Case | MOD-0230/0231 | Intake, case master, assessment ve immutable exposure snapshot |
| ICSR submission package / acknowledgement | MOD-0233 | Versioned outbound report, transmission, acknowledgement, retry ve reconciliation |
| MedDRA assignment | MOD-0232 | Term/code/dictionary version/history |
| Signal | MOD-0234 | Scope, evidence, evaluation, decision, actions ve terminal snapshot |
| Product Quality Complaint | Quality/QMS | Kendi SoR/lifecycle'ı; PV typed complaint ref, affected product/lot snapshot, classification ve evidence tüketir |
| Patient/reporter protected record | PV + Privacy/Security | Ürün bağlamından ayrı, purpose-limited PII; Signal yalnız minimum gerekli non-PII evidence/ref tüketir |
| Work item | Kaynak modül lifecycle'ının projection'ı | WorkCenter karar veya lifecycle SoR'u değildir |
| Central audit | MOD-0021 | Local intent ancak central acknowledgement ile tamamlanır |

## 7. Dependency graph

```text
P0 Governance and duplicate-SoR repair
  ├─ FU01/FU02 identity reconciliation
  ├─ Product vs GlobalProduct ownership/cardinality
  └─ internal vs ERP/PLM/PV/RIM SoR decisions

Reference spine
  ├─ MOD-0048 markets/UoM/controlled terms
  ├─ Legal Entity + organisation/location
  └─ Substance identifiers and terminology

Product spine
  MOD-0290 GlobalProduct -> Revision -> GSKU -> LSKU / FinishedGood
      ├─ Brand/trade-name mappings
      ├─ Legal Entity scope
      └─ Development Formulation <-> Regulatory Product Composition
             <-> Pharmaceutical Product Definition (conceptual object; exact Diten aggregate TBD)
             <-> Manufactured Item Definition / Packaged Product

Regulatory spine
  Registered Presentation (Diten projection)
      <- {Medicinal Product, Regulated Authorisation, authorised Packaged Product/status}
      -> Market Supply Assignment
      -> MOD-0236 dossier / MOD-0239 requirements / MOD-0237 variations / MOD-0238 labeling

PV case/reporting spine
  MOD-0230 intake -> MOD-0231 case -> MOD-0232 MedDRA
      -> canonical Product Exposure Snapshot
      -> MOD-0233 reporting/submissions

Clinical safety spine
  Study / protocol / blinding + IMP mapping + effective RSI version
      -> MOD-0244 SAE linkage -> MOD-0231 case -> coding/reporting/signal evidence

Signal spine
  Reconciled cases + coded events + product exposure snapshots
      -> MOD-0234 signal lifecycle -> signal decision/action intent
      -> MOD-0233 / MOD-0272 / RIM submission and authority receipts

PV quality and privacy control planes apply across intake, case, reporting and signal; they are not final add-ons.

All regulated transitions
  -> Auth/RBAC + MOD-0021 audit + MOD-0031 evidence + MOD-0023 workflow
  -> WorkCenter projection/dispatch, while domain remains lifecycle owner
```

## 8. Ordered delivery sequence

### 2026-09-07 execution clarification — authoritative interpretation of the sequence below

The numbered rows are dependency groups, not a requirement to finish every upstream module before starting PV.
Deliver two parallel lanes after identity/SoR decisions: (A) product/composition/regulatory reference contracts and
(B) shared PV foundations plus the case/Signal vertical slices. Each individual module still closes its selected
scope with user acceptance before the same lane moves on. Full material, warehouse and RIM authoring are not global
PV prerequisites.

Choose the operating model before case/reporting development:

- Native case SoR: Diten owns case handling, reportability and reporting-clock responsibilities. Before real case
  acceptance, a qualified reporting/submission path, acknowledgement/reconciliation and QC must exist. A controlled
  external reporting service may fulfil that path; Signal completion is not a prerequisite for ICSR reporting.
- Signal consumer: a qualified external PV system owns case processing/reporting. Diten consumes versioned,
  reconciled case/evidence feeds and may deliver Signal before native MOD-0230/31/33. Ownership, clocks, lineage,
  duplicates, access and failure behaviour must be explicit; no second case master is created.
- Manual Signal assessment: governed evidence and source snapshots may support the first slice without automated
  statistical detection. Full MOD-0063 Lakehouse is not automatically a gate. Any analytics used must have approved
  data contracts, tenant ACLs, lineage, freshness, quality and replay evidence. Applicable Blueprint dependencies
  require an approved minimum consumer contract, not a copied local analytics platform.

The Excel's urgent Signal-first path is adopted for the consumer/manual operating models. Its delayed full-case
processing is not adopted as permission to leave native case reporting obligations uncovered.

Existing MOD-0234 Module Pack hard gates for MOD-0004/0063 are not silently waived by this draft. The selected
operating model and minimum consumer contract must be reconciled into its approved Module Pack before runtime
code-start. This update authorizes planning only.

### Explicit parallel foundation work

| Module / capability | Minimum PV deliverable | Release gate |
|---|---|---|
| MOD-0018 Authorization | PV permissions, role/scope matrix, purpose and server-side enforcement | UI/API/export cross-tenant and cross-scope negative tests |
| MOD-0019 Data Masking & Row/Field Security | Patient/reporter/narrative classification, server-side masking and export policy | Authorized full-data vs restricted-user masking proof |
| MOD-0021 Audit Trail | Canonical append/delivery, outage/recovery, integrity and PII minimisation | Regulated writes cannot be falsely reported as audit-complete |
| MOD-0023 Workflow + MOD-0024 task/WorkCenter consumption | Versioned PV workflow, source-owned lifecycle, projection/action dispatch | Maker/checker, stale version, retry, restart and read-back |
| MOD-0028 controlled documents + MOD-0031 Evidence Linking | Version-specific source evidence, provenance, supersession and coverage | Persistent evidence survives restart; no duplicate document SoR |
| MOD-0040 Canonical ID & Correlation | Canonical/external identity mapping, source-system uniqueness and correlation | No ambiguous external-to-canonical mapping; cross-service tracing |
| MOD-0004 Metric & Semantic Registry | Definitions/units/dimensions/version/owner for metrics actually used | Approved metric semantics before a calculated dashboard is accepted |
| MOD-0063 Data Warehouse / Lakehouse | Minimum governed feed/projection contract for analytics actually used | Lineage, freshness, quality, ACL and replay; full platform may be deferred |
| MOD-0022 e-signature + MOD-0030 records | Signature/record/retention/legal-hold contracts where applicable | Relevant approval/submission acceptance, not assumed from file existence |
| MOD-0235 PV Quality + MOD-0229/MOD-0208 dependencies | Basic QC, findings/CAPA boundaries and responsibility | Basic controls precede operational use; advanced inspection automation may follow |

Each existing foundation is audited and extended only for its missing PV contract. August status labels do not
authorize rebuilding an already implemented service. New root MOD identities are not reserved by this table.

| Sıra | Teslim | Neden bu sırada | Sonraya bırakılabilecek bölüm |
|---:|---|---|---|
| 0A | Governance identity repair | DCP, ABB/Brand FU01 ve Brand/Market-Supply FU02 çakışmaları çözülmeden yeni kod yanlış kimliğe bağlanır | Yok |
| 0B | Product/Brand SoR repair | Product–GlobalProduct ve Brand–authorised-name cardinality/ownership; no-second-writer/migration-alias kararı kapanır | Yok |
| 1 | Güncel main üzerinde MOD-0290 reconciliation | Local lifecycle, WorkCenter, Legal Entity scope ve audit delta'sı merge truth olmalı | Production deployment |
| 2 | Product context + material identity interface | GlobalProduct/GSKU/LSKU/FG + Legal Entity + Market ve gelecekteki Material resolver sınırı tüketilebilir olmalı | Material Master operational SoR, raw-material specification, procurement ve supplier qualification |
| 3 | Substance + development formulation + regulatory composition foundation | Deneysel formula ile submitted/authorised composition ayrılır; product/substance resolution sağlanır | Ayrıntılı material contents/specification ve manufacturing recipe |
| 4A | Package identity boundary | Manufactured/packaged product, nested container, GTIN/data-carrier sınırı kapanır | Full packaging authoring/automation |
| 4B | MOD-0238 Labeling foundation | Package identity ile versioned label/artwork/leaflet bağı ayrılır; language ve published-version ownership kapanır | Full content authoring paralel/sonra |
| 5 | Regulatory product + MA + Registered Presentation + MOD-0236/0239 foundations | Ülke, authority, MAH, package, dossier ve effective country-requirement gerçeğini sağlar | MA/RP aggregate foundation package activation kanıtıyla paralel ilerleyebilir; publication/activation bekler |
| 6 | Market Supply Assignment | Ruhsatlı sunum ile fiili LSKU/FG/Legal Entity supply bağını kurar | Otomatik supply integration |
| 7A | PV operational + privacy control plane | JWT/RBAC, protected PII, field/document security, Mongo, audit/evidence, retention/legal hold ve provider client'ları | Gelişmiş privacy automation |
| 7B | MOD-0235 PV Quality foundation/control plane | QC roles, records/e-signature, CAPA/evidence boundary ve inspection traceability case go-live öncesi kapanır | Gelişmiş inspection automation |
| 7C | MOD-0230 → 0231 → 0232 | Intake, processing, assessment ve MedDRA operational olur | Gelişmiş special-situation automation |
| 7D | MOD-0233 ICSR output | Case submission, acknowledgement, retry ve reconciliation case go-live öncesi kapanır | PSUR/PBRER/DSUR aggregate reporting ayrı FU/candidate kararıdır |
| 8 | MOD-0234 Signal Management | Domain lifecycle, typed scope, evidence, WorkCenter ve action intent | ML/NLP ve disproportionality otomasyonu |
| 9 | Signal-derived outputs | Aggregate reports, regulatory actions, RMP/PASS/label links | Tam external automation |
| 10 | Downstream RIM/Quality/SCM consumers | Variation, CAPA, lot, recall, stability ve manufacturing impact | Full WMS/inventory valuation |

PV platform/security/persistence foundation, sıralar 3–6 ile paralel geliştirilebilir. Kaynakta bulunan reported
product representation — verbatim text ve/veya structured identifier — immutable korunur. Verbatim text yoksa
yapay metin üretilmez; captured structured source evidence korunur. Canonical resolution reported representation'ın
yerine geçmez. Signal intake/detection/validation; provenance, immutable snapshot ve en
az bir `Substance`, `Product`, `ProductClass` veya açık `UnresolvedTarget` hypothesis scope'u ile başlayabilir.
Eksik MA/presentation/pack resolution `resolution-pending` olur; otomatik market-impact, authority submission ve
presentation-specific action closure fail-closed kalır. Bu eksiklik day-zero intake/triage veya Signal creation'ı
bloke etmez.

## 9. Prerequisites

- Master 8.1 identity lookup ve registry collision kontrolleri.
- Root dirty/local-unmerged `DCP-005-material-product-master-data-coding-alignment.md` içeriğinin, collided DCP-005
  kimliklerinden ayrılması; exact source branch/SHA/status ve owner kararlarının bu draft ile reconciliation'ı.
- Brand/Product ile GlobalProduct modelinin tek SoR kararı.
- Substance/Ingredient/Composition ve MA/Registered Presentation owner kabulü.
- Exact Legal Entity, organisation/location, market ve authority provider sözleşmeleri.
- Typed identifier registry ve effective-dated mapping politikası.
- Auth-issued service identity, tenant grants, credential rotation ve fail-closed provider access.
- Merkezi audit acknowledgement, evidence linking, retention/legal hold ve workflow contracts.
- Her üye için ayrı approved/ready-for-dev Module Pack ve code-start.

## 10. Architecture decisions

Etiketler: `[STANDARD/REGULATORY]` dış normative bağlamı, `[DITEN-POLICY]` repo için zorunlu mimari politikayı,
`[RECOMMENDED]` kanıta dayalı tasarım önerisini, `[OWNER-TBD]` ise açık owner kararını gösterir. Bir standart etiketi
storage tekniğinin standart tarafından emredildiği anlamına gelmez.

1. **Tek tabloya doldurma yok** `[DITEN-POLICY][RECOMMENDED]`: composition, package, MA, supply, PV case ve signal ayrı aggregate'lerdir.
2. **Mapping açık ve versioned** `[STANDARD/REGULATORY][RECOMMENDED]`: GlobalProduct/GSKU/LSKU/FG, IDMP-benzeri nesnelerle birebir varsayılmaz.
3. **Ruhsat ürün alanı değildir** `[STANDARD/REGULATORY][RECOMMENDED]`: jurisdiction, authority, MAH, number, status ve valid period taşıyan ayrı gerçektir.
4. **Registered Presentation LSKU değildir** `[RECOMMENDED][OWNER-TBD]`: aynı LSKU farklı regulatory bağlamlarda kullanılabilir; ruhsatlı sunum
   tedarikte olmayabilir.
5. **Market Supply ruhsat değildir** `[RECOMMENDED][OWNER-TBD]`: yasal yetki ile fiili tedarik durumu ayrı tutulur.
6. **Signal scope çokludur** `[STANDARD/REGULATORY][RECOMMENDED]`: signal N substance, composition, product, presentation, country, pack veya lot'a
   bağlanabilir; yalnız GlobalProductId taşımaz.
7. **Reported snapshot korunur** `[STANDARD/REGULATORY][DITEN-POLICY]`: PV vakasında source representation değişmez; canonical resolution ve current master ayrıca
   tutulur. Sonraki master değişikliği eski vakayı yeniden yazmaz.
8. **Typed identifiers** `[STANDARD/REGULATORY][OWNER-TBD]`: Substance ID, PhPID, MPID/PMS ID, package ID/PCID, MA number, GTIN/NDC ve local codes için
   system/issuer, value, jurisdiction, status, version/validity ve supersession semantiği zorunludur. Ayrı registry,
   component veya collection seçimi implementation/owner kararıdır; standart tek storage biçimi dayatmaz.
9. **Temporal semantik ayrı seçilir** `[RECOMMENDED][OWNER-TBD]`: MA, supply ve regulatory composition gibi dış-dünya geçerlilik gerçekleri
   `ValidFrom/ValidTo` kullanabilir. Case evidence ve Signal scope değişimi `OccurredAt/RecordedAt` ile append-only
   revision history taşır. Bitemporal model her aggregate'e otomatik dayatılmaz.
10. **Tenant yasal rol değildir** `[DITEN-POLICY]`: TenantId veri/güvenlik sınırıdır. MAH, manufacturer, sponsor ve supplier gerçek
    Legal Entity/organisation rolleridir.
11. **Visibility yasal rol değildir** `[STANDARD/REGULATORY][DITEN-POLICY]`: Bir şirketin ürünü görebilmesi onu MAH yapmaz. Grup çapı PV erişimi açık
    policy/scope ile sağlanır.
12. **Regulatory category rejime bağlıdır** `[STANDARD/REGULATORY][RECOMMENDED]`: Global Product yalnız non-authoritative intended family taşıyabilir.
    Authoritative Medicine/Supplement/Food/Cosmetic sınıflandırması jurisdiction + regulatory regime + competent
    authority/source + effective period ile tutulur; yalnız gerektiğinde presentation referansı taşır.
13. **WorkCenter pano görevi görür** `[DITEN-POLICY]`: görevi gösterir ve source action endpoint'ine dispatch eder. Kararı ve lifecycle
    state'ini kaynak modül kaydeder.
14. **Maker-checker risk-based** `[RECOMMENDED][OWNER-TBD]`: final medical/regulatory kararlar için distinct human/QC güçlü default'tur; her
    transition için evrensel mevzuat şartı gibi sunulmaz.
15. **No hard delete/no reuse** `[DITEN-POLICY]`: regulated identities supersede/retire/withdraw ile kapanır; history immutable kalır.
16. **Reported text canonical değildir** `[STANDARD/REGULATORY][DITEN-POLICY]`: Primary source ürün metni varsa zorunlu immutable evidence olarak korunur;
    master kayda yükseltilmez. Unresolved reference provenance ve SLA ile quarantine/reconciliation'a girer.
17. **Quality defect Signal değildir** `[STANDARD/REGULATORY][RECOMMENDED]`: Product Quality Complaint kendi QMS SoR/lifecycle'ında kalır. PV safety
    relevance assessment, handoff clock, typed complaint/case/signal ref, affected product/lot snapshot ve closure
    reconciliation tüketir; complaint closure PV Case/Signal'ı otomatik kapatmaz.
18. **Authority action sahipliği ayrıdır** `[RECOMMENDED][OWNER-TBD]`: Signal decision/action intent ve typed reference üretir. Submission,
    authority receipt ve acknowledgement MOD-0233/MOD-0272/RIM SoR'unda yaşar; Signal'a snapshot/ref döner.
19. **Development ve authorised composition ayrıdır** `[RECOMMENDED][OWNER-TBD]`: Promotion/version lineage açık olur; kopyalama veya silent
    overwrite yapılmaz.
20. **PV privacy tenant duvarından fazlasıdır** `[STANDARD/REGULATORY][DITEN-POLICY]`: patient/reporter PII ayrı protected record; purpose-limited access,
    masking/redaction/pseudonymisation, export audit, cross-border access, break-glass ve retention/legal-hold kararı
    gerekir.
21. **Jurisdictional Signal policy versioned olur** `[STANDARD/REGULATORY][RECOMMENDED]`: monitoring population/frequency,
    notification route, deadlines ve regional state overlay configuration/policy'dir; EU pilot listesi veya eski GVP
    Rev.1 timeframe kod içine gömülmez. Karar kanıtı event-time rule-set/version taşır.

## 11. Scope

- TO-BE regulatory/international benchmark.
- Master 8.1 exact module identity map.
- Merged main ve local-unmerged code-truth ayrımı.
- Capability/entity/source/consumer/dependency matrisi.
- Sıralı teslim, gate, acceptance ve erteleme kararları.
- Claude ve insan owner review'ü için açık sorular.

## 12. Explicit exclusions

- Bu draft üzerinden doğrudan kod yazmak.
- Module Pack yerine bu DCP'yi kullanmak.
- DCP-002 olmadan yeni MOD/FU kimliği üretmek.
- `Product.ProductType` değerini global regulatory truth kabul etmek.
- Placeholder/free-text Substance, MA, Registered Presentation veya Market Supply alanı eklemek.
- Global Product'a tek `LegalEntityId`, `MAHId` veya `ManufacturerId` gömmek.
- WorkCenter içinde domain lifecycle kopyalamak.
- Inventory Material ile regulatory Substance/Ingredient'i aynı nesne kabul etmek.
- Full ERP/PLM/PV/RIM external integration ve Production rollout.

## 13. Governance drift risks

- `MOD-0290-FU01` ve `MOD-0290-FU02` kimlikleri birden fazla capability için kullanılmıştır.
- `DCP-005` kimliği de birden fazla artifact tarafından kullanılır; local material/product alignment draft'ı merged
  main artifact'i veya approved authority gibi kullanılamaz.
- `Product` ve `GlobalProduct` iki ayrı SoR gibi davranabilir.
- Brand master ile authorised trade name karıştırılabilir.
- Module Pack ve implementation tracker yüzdeleri code truth'u temsil etmeyebilir.
- 04-Aug yönetim çalışma kitabındaki `MOD-0290 0%` ve PV `not started` ifadeleri güncel main tarafından
  superseded edilmiştir; yalnız iş sırası girdisi olarak kullanılmalıdır.
- Local Product/Item/SKU entegrasyon dalı latest main ile ayrışmış ve dirty durumdadır; merged sayılmaz.
- DCP-005 Market Supply'ı candidate/owner belirsizliği sayarken registry FU02 kimliği ayırmıştır.
- Blueprint'te gerçek MOD-0233/0235/0236–0239 bulunmasına rağmen registry/pack/runtime kapsamı eksiktir.
- External ERP/PLM/PV/RIM ile internal SoR'lar arasında dual-authority riski vardır.
- Backlog numaraları yeniden kullanılmış olabilir; kanıt başlık/path ile reconcile edilmelidir.

## 14. Review questions

1. `Product` ile `GlobalProduct` arasındaki gerçek cardinality nedir? Biri projection mı, ayrı business object mi?
2. Standalone Brand capability gerekli mi; yoksa Brand ve authorised trade name hangi mevcut owner'larda yaşar?
3. Material Master MOD-0290 follow-up mı, yoksa ayrı candidate capability mi olmalı?
4. Substance Master ile Ingredient/Composition aynı owner'da mı, ayrı SoR'larda mı yaşamalı?
5. Composition version'ın minimum identity key'i, strength modeli, freeze ve supersession kuralı nedir?
6. Finished Good ile Manufactured Item; LSKU ile Registered Presentation cardinality'leri nedir?
7. Marketing Authorisation ve Registered Presentation tek candidate altında mı, ayrı owner'larda mı olmalı?
8. Market Supply Assignment için draft FU02 ownership kararı DCP-005 ile nasıl reconcile edilecek?
9. Supplement/food/cosmetic gibi non-medicinal ürünler için hangi regulatory regime/registration gerçeği kullanılır?
10. PV case unresolved product resolution SLA'sı ve quarantine/reconciliation owner'ı kimdir?
11. Signal validation, final assessment, closure/reopen ve regulatory action approval rollerinde hangi SoD zorunludur?
12. MOD-0233 yalnız ICSR reporting mi; PSUR/PBRER/DSUR için FU mu, ayrı candidate mı gerekir?
13. RMP, PSMF, literature, partner safety exchange, PASS ve RSI hangi mevcut parent/FU sahipliklerine ayrılmalı?
14. Internal PV/RIM SoR ile Argus/ArisG/Veeva gibi external SoR seçimi object type, jurisdiction, Legal Entity,
    effective period ve migration state bazında nasıl yapılır; aynı object/period için tek authoritative writer nasıl korunur?
15. Hangi lifecycle aşamasında WorkCenter task zorunlu, hangilerinde domain içi synchronous decision yeterlidir?

## 15. Gate criteria

| Gate | Kapanış kanıtı | Bloke ettiği teslim |
|---|---|---|
| PRPV-G0 Identity/SoR | DCP/FU collision yok; Product/GlobalProduct ve internal/external SoR kararı | Tüm yeni geliştirme |
| PRPV-G1 Product context | Merged, tested Product/SKU lifecycle + Legal Entity/Market resolver | Regulatory ve PV product links |
| PRPV-G2 Substance/composition | Source/licence, terminology, ingredient role, strength/basis/UoM, development-vs-authorised version ve resolver contract | Automated product grouping/impact; unresolved-target Signal creation'ını bloke etmez |
| PRPV-G3 Packaging | Package identity/hierarchy/language ile printed artwork sınırı ve owner kararı | Registered Presentation activation/publication; MA/RP aggregate foundation'ını bloke etmez |
| PRPV-G4 Regulatory product | MA/RP owner, internal/external RIM SoR, identity, lifecycle, authority/MAH/jurisdiction contract | Market Supply, presentation-specific impact ve authority action closure |
| PRPV-G5 Market supply | Tek accountable SoR, exact relation, interval, validation ve tenant non-disclosure | Market impact automation |
| PRPV-G6 PV platform | In-memory/DenyAll yok; tenant-persistent store, Auth, protected PII, masking, audit/evidence, retention/legal hold, privacy ve workflow/provider clients | PV production use |
| PRPV-G7 Case/coding | Native: MOD-0230/31/32 operational; consumer: qualified external source feed. Assessment primitives, RSI/label version, SAE/partner/day-zero and source snapshot apply according to operating model | Native ICSR output/case processing or automated case-derived Signal analysis |
| PRPV-G8 ICSR/quality | Native case ownership requires qualified reporting/ack/reconciliation path and QC; external case SoR requires explicit contractual delegation and reconciliation. Basic Signal QC always applies | Native case operational use; not all Signal-only deliveries |
| PRPV-G9 Signal | Lifecycle, typed scope/snapshot, merge/split/reopen, evidence, actions, WorkCenter, replay/recovery tests | Signal go-live |
| PRPV-G10 External SoR | Per object/jurisdiction/period one writer, replay/lineage and migration-state proof | External ERP/PLM/PV/RIM activation |

## 16. Acceptance criteria

- [ ] Tüm real MOD kimlikleri Master 8.1 ile exact eşleşir.
- [ ] Hiçbir numeric MOD veya FU kimliği collision/varsayımla kullanılmaz.
- [ ] Brand için geçersiz FU01/FU02 identity ile runtime genişletilmez; Market Supply FU02 ownership kararı
      kapanana kadar quarantine edilir.
- [ ] Product ve GlobalProduct tek SoR kararına göre reconcile edilir.
- [ ] Brand master, trade name ve authorised product name ayrılır.
- [ ] Material identity interface, Substance, Ingredient ve Composition farklı anlamlarla ve typed boundary'lerle
      tanımlanır; operational Material Master repository/UI approved SCM/manufacturing consumer gate'ine ertelenebilir.
- [ ] GlobalProduct/GSKU/LSKU/FG ile regulatory product nesneleri explicit versioned mapping taşır.
- [ ] MA, Registered Presentation ve Market Supply birbirinden ayrıdır.
- [ ] Regulatory category jurisdiction + regulatory regime + competent authority/source + effective period ile
      tutulur; yalnız owner-approved ihtiyaçta medicinal-product/Registered-Presentation reference taşır.
- [ ] PV Case reported text + immutable exposure snapshot + resolved typed references taşır.
- [ ] Signal owner-approved typed scope references ve immutable resolution snapshots taşır; doğrudan FK/N:M ancak
      kanıtlanmış query/cardinality/lifecycle ihtiyacıyla eklenir.
- [ ] Signal unresolved/substance/product/class target ile başlayabilir; unresolved market/presentation action otomasyonu fail-closed kalır.
- [ ] Signal merge/split/duplicate/supersede, reopen/reassessment, regional overlay, monitoring rationale ve closure outcome history taşır.
- [ ] Native case ownership için ICSR reporting/acknowledgement path ve QC kabulden önce hazırdır; Signal consumer
      modelinde dış case/reporting owner ve qualified feed kanıtlanır. Native reporting Signal bitmesine bağlanmaz.
- [ ] MOD-0004/0063 tüketilen minimum sözleşmelerle değerlendirilir; full Lakehouse manuel Signal için varsayılan
      zorunluluk değildir. Statistical/automated outputs qualified dataset olmadan kabul edilmez.
- [ ] Case seriousness, expectedness/listedness, causality, outcome, reportability, clock-start/due-date ve duplicate/follow-up lineage kontratları vardır; expectedness event-time label/RSI versionına bağlanır.
- [ ] Clinical IMP, study/protocol/blinding, effective RSI ve SAE→Case→Signal lineage sınırı tanımlıdır; unblinding SoD korunur.
- [ ] Partner exchange/SDEA kaynak, day-zero, duplicate responsibility, forwarding SLA ve acknowledgement ownership'i case go-live öncesi kararlıdır; connector ertelenebilir.
- [ ] PV patient/reporter PII field/document-level access, masking, export audit, cross-border access, break-glass ve legal-hold çatışma testleriyle korunur.
- [ ] MedDRA ve diğer dictionaries exact version/source binding taşır.
- [ ] WorkCenter source lifecycle'ı kopyalamaz; generic projection/dispatch kullanır.
- [ ] Her regulated transition actor, version, idempotency, rationale, evidence ve audit linkage üretir.
- [ ] Cross-tenant erişim fail-closed; Legal Entity visibility ve yasal rol ayrıdır.
- [ ] External SoR seçimi duplicate internal master oluşturmaz.
- [ ] Historical PV product reconciliation ayrı auditli/reversible pack'tir; fuzzy match canonical değildir ve
      `unresolved/proposed/human-confirmed` durumları ile immutable reported text'i korur.
- [ ] Her architecture kararı `regulatory/standard requirement`, `Diten mandatory policy`, `recommended design`
      veya `owner decision/TBD` normative basis etiketi taşır.
- [ ] Ertelenen capability'lerin bugünden gereken interface boundary'leri dondurulur.
- [ ] Her üye kendi Module Pack ve code-start kapısından geçer.

## 17. Downstream business-module impacts

### Kanıtlı mevcut durum ve boşluk matrisi

| Capability | Merged main `c965e5f6` | Local-unmerged aday | Kalan iş | Ertelenebilir mi? |
|---|---|---|---|---|
| Global Product/GSKU/LSKU/Finished Good | Create/read foundation | Unmerged in-progress lifecycle/edit/withdraw/correction/retirement/recovery/WorkCenter/audit candidates | Latest main reconciliation; completion/test evidence yeniden kurulmalı | Hayır |
| ABB | Runtime mevcut; governance kimliği canonical FU01 | Unmerged in-progress WorkCenter/audit candidates | Identity cleanup; completion/test evidence yeniden kurulmalı | Material/composition öncesi kısmen |
| Brand/Product | Entity/API/UI mevcut ama canonical zincir kopuk | Esasen aynı | Duplicate-SoR remediation | PV minimumundan kısa süreli ertelenebilir |
| Product–Legal Entity scope | Merged main'de runtime yok | Effective-dated N:M implementation var | Reconcile, merge ve live policy acceptance | Hayır |
| Verified market/UoM/pack refs | Market ve GSKU UoM/pack-applicability tüketimi kanıtlı | Consumer/audit candidate delta | Form/route/presentation-unit publish/assignment/read-back ayrıca kanıtlanmalı | Hayır |
| Material identity interface | Yok | Yok | Owner, typed external reference ve resolver/mapping contract | Hayır |
| Material Master operational SoR/details | Yok | Yok | SCM/manufacturing consumer gate sonrası | Evet; Composition yalnız opaque/typed material ref taşıyabilir |
| Development Formulation / Regulatory Composition | Yok; forbidden placeholder guards var | Yok | Ayrı owner/version/lineage ve resolver contracts | Hayır; ayrıntılı material specification ertelenebilir |
| Package identity/hierarchy/GTIN | Operational runtime yok | Yok | Package/container identity ve FG/LSKU mappings | Tam authoring evet; identity hayır |
| Artwork/label/leaflet versions | MOD-0238 Blueprint dependency | Yok | Regulatory content SoR ve version links | Full authoring ilk PV foundation sonrasına kalabilir |
| MA/Registered Presentation | Yok | Yok | RIM candidate owner ve lifecycle | Hayır |
| Market Supply Assignment | Draft governance | Aynı | MA/RP sonrası runtime | Signal prototype için evet; market impact için hayır |
| MOD-0230 | API/Gateway/UI var; in-memory + deny-all adapters | Değişmiyor | Mongo/Auth/audit/evidence/workflow/product resolution | Hayır |
| MOD-0231/0232 | Contract/in-memory | Değişmiyor | Operational persistence/API/provider | Hayır |
| MOD-0234 | Contract + in-memory service | Değişmiyor | Full domain/persistence/API/UI/permissions/WorkCenter | Hayır |
| MOD-0233 ICSR output / MOD-0235 control plane | Runtime yok | Yok | Submission/ack/retry ve PV Quality/QC/CAPA boundaries | Native case use öncesi qualified reporting path; external case SoR varsa native reporting ertelenebilir. Basic QC ertelenmez |
| Aggregate reporting | Runtime yok | Yok | PSUR/PBRER/DSUR owner ve FU/candidate | Signal/exposure verisi olgunlaşana kadar evet |
| MOD-0236–0239 | Runtime yok | Yok | Dossier/variation/label/country requirements | MA foundation ile paralel/sonra |
| Inventory/BOM/batch/stability | Operational product link yok | Yok | Typed reference consumers | İlk Signal MVP için evet |

### Ertelenebilir fakat sınırı şimdi dondurulacak işler

- Ayrıntılı raw-material specification, supplier qualification ve procurement.
- Material Master'ın operational repository/UI'si; fakat typed identity/mapping interface'i ertelenmez.
- Full inventory valuation, WMS, BOM/routing, batch/eBR ve stability automation.
- Automated disproportionality, NLP/AI triage ve class-effect inference.
- EudraVigilance/FAERS/Argus/ArisG/Veeva tam otomatik connectors.
- PMS write/submission automation ve otomatik label/variation document generation.
- Gelişmiş exposure/RWE analytics ve multi-region rules automation.

### Ertelenemeyen minimumlar

- Substance/product/package/MA ayrımı.
- Typed identifier, source/version ve effective dating.
- Immutable PV case snapshot ve unresolved-reference workflow.
- Tenant wall, Legal Entity/yasal rol ayrımı, RBAC/field security.
- Evidence/audit/retention, idempotency ve recovery.
- Domain-owned lifecycle ile WorkCenter projection ayrımı.
- Signal lifecycle, scope ve regulatory action linkage.
- Native case use için qualified ICSR output/acknowledgement path; tüm ilgili kullanımlar için basic PV Quality,
  privacy ve day-zero ownership. Signal-only dış case SoR yolu §8'e tabidir.

## 18. Open decisions

- FU01/FU02 Brand governance identity remediation.
- Product vs GlobalProduct object and SoR decision.
- Material/Substance/Ingredient/Composition owner boundaries.
- Formula/Composition candidate identity.
- MA/Registered Presentation candidate identity and cardinality.
- Market Supply FU02 ownership reconciliation.
- Medicine/Supplement/food/cosmetic market classification model.
- Package/Finished Good/LSKU/Registered Presentation mapping cardinalities.
- Internal versus external PV/RIM SoR selection policy.
- Aggregate Safety Reporting, PSMF, RMP ve diğer PV follow-up identities.
- Signal decision SoD and risk-class policy.
- Retention/legal hold, regional rules and regulator acknowledgement contracts.

## 19. Future follow-ups

- DCP-002-compliant identity remediation pack for Brand collisions.
- Product/GlobalProduct SoR reconciliation Module Pack amendment.
- Material identity interface now; Material Master operational SoR/details later, after SCM/manufacturing consumer gate.
- Substance/Ingredient and Formula/Composition candidate reservation(s).
- Regulatory Product/MA/Registered Presentation candidate reservation(s).
- MOD-0290-FU02 Market Supply owner reconciliation.
- MOD-0230/31/32/34 operational hardening packs.
- MOD-0233/0235 and MOD-0236–0239 registry/module packs.
- PV Governance/PSMF, Aggregate Reporting, RMP, risk minimisation, literature surveillance, partner exchange, PASS,
  safety communication and RSI/special-situation follow-ups.
- Investigational product/IMP, study/protocol/blinding, RSI and MOD-0244 SAE linkage boundary pack.
- Product Quality Complaint ↔ PV safety-triage and closure-reconciliation contract.
- Historical PV product-reference reconciliation/backfill operational pack.
- External ERP/PLM/PV/RIM/regulatory-gateway integration packs after internal SoR decisions.

## 20. Audit and reconciliation notes

### 2026-09-07 management workbook reconciliation

Input: `C:/Users/AliT/Desktop/Project ongoing status report v 05 SEP 2026.xlsx`, `PVG!A8:K25`,
`PVG!B48:G56`, and `PVG!A40`. The September filename does not update the embedded 2026-08-03 static audit.
No August percentage is reused as September implementation proof.

Accepted: explicit parallel foundation remediation, masking, metric semantics, governed analytics contract,
canonical/external IDs, evidence linking, and native/external Safety Case ownership distinction.
Amended: Signal-first is conditional on operating model; ICSR and basic QC cannot be deferred for native case use.
Rejected as universal gate: full Lakehouse or every product/regulatory module before any Signal delivery.
Retained: composition/material interface now, operational Material details later; legal-entity/MA/presentation and
historical snapshot boundaries; WorkCenter source-owned lifecycle; exact identities and code-truth checks.

Completion evidence and the user-facing ordered module list are recorded in
`docs/audits/product-regulatory-pv-sequence-and-completion-review-20260907.md` in this authoring worktree.

Three independent read-only code reviews found: product candidate has recorded Global Product acceptance but
open GSKU/LSKU/ABB WorkCenter and scope activation acceptance; Finished Good lacks the requested lifecycle parity.
PV 0230 is in-memory/DenyAll despite API/UI; 0231/32/34 are contract/in-memory. Shared Auth/Audit/Workflow/Documents
have substantial reusable code, while PV integration is unproven. Masking/Evidence/Metric/Lakehouse generic runtimes
were not found. Document e-signature/retention slices exist under other identities and require ownership alignment.
No full-module 100% claim is established; no fresh test/runtime execution or remote fetch occurred in this review.

### 2026-09-06 authoring evidence

- DCP collision scan found that `DCP-006` is already reused by multiple capabilities and `DCP-007` also exists.
  This draft therefore remains `UNASSIGNED`; only Enterprise Architect may reserve a collision-free DCP identity.
- Master 8.1 exact rows were inspected for MOD-0290, MOD-0173..0177, MOD-0193/0195/0204/0209,
  MOD-0230..0239, MOD-0244, MOD-0252/0253/0258/0259 and MOD-0272.
- The 04-Aug management workbook `PVG Capability Block`, `Integrated R&D–RA Lifecycle Man`, `Code Reality Audit`
  and reconciliation sheets were read as planning input. Their historical completion percentages were not reused.
- Latest merged main `c965e5f6` and local-unmerged Product/Item/SKU candidate `ed5188dc` were inspected separately.
- PV code truth: MOD-0230 is a guarded local-dev/API/UI slice with in-memory repository and deny-all adapters;
  MOD-0231/0232/0234 are contract/in-memory slices; MOD-0233/0235/0236–0239 operational runtime is absent.
- Product code truth: merged foundations exist; lifecycle/WorkCenter/Legal Entity scope improvements remain local and
  unmerged. `Product` versus `GlobalProduct` is an unresolved duplicate-SoR risk.
- Official-source reference list was prepared and the architecture draft was independently red-teamed against it.
  Source retrieval/version/hash and decision-level claim mapping are not yet compliance evidence and remain an
  approval criterion.
- No runtime/config/data/Production change was authorized by this DCP.

### International reference baseline

| Konu | Resmi kaynak |
|---|---|
| ISO IDMP/SPOR separation | https://www.ema.europa.eu/en/human-regulatory-overview/research-development/data-medicines-iso-idmp-standards-overview/substance-product-organisation-referential-spor-master-data |
| Medicinal Product identification/lifecycle | https://www.iso.org/standard/70150.html |
| Pharmaceutical Product identification | https://www.iso.org/standard/70044.html |
| Substance identification | https://www.iso.org/standard/69697.html |
| Dose form, route, unit of presentation and packaging terms | https://www.iso.org/standard/81133.html |
| EMA PMS implementation details | https://www.ema.europa.eu/en/documents/regulatory-procedural-guideline/product-management-service-pms-implementation-international-organization-standardization-iso-standards-identification-medicinal-products-idmp-europe-chapter-8_en.pdf |
| EMA PMS public contract | https://api.pms.ema.europa.eu/public/v1/swagger |
| ICH E2B(R3) safety message/acknowledgement | https://admin.ich.org/node/348 |
| EMA GVP Module IX Signal Management | https://www.ema.europa.eu/en/documents/scientific-guideline/guideline-good-pharmacovigilance-practices-gvp-module-ix-signal-management-rev-1_en.pdf |
| EMA current Signal Management context | https://www.ema.europa.eu/en/human-regulatory-overview/post-authorisation/pharmacovigilance-post-authorisation/signal-management |
| FDA Medicinal Product Identification | https://www.fda.gov/industry/fda-data-standards-advisory-board/medicinal-product-identification |
| FDA postmarket evidence context | https://www.fda.gov/drugs/surveillance-post-drug-approval-activities/postmarketing-surveillance-programs |
| SAP material organisational views | https://help.sap.com/docs/SAP_ERP_SPV/a428aae377ba4a1199c3ecc8b7f5f33d/048bc95360267214e10000000a174cb4.html |
| Oracle item organisations | https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25c/faipr/item-organizations.html |

### External review instructions

Reviewer must not treat this draft, the management workbook or existing Module Packs as automatically correct.
Review must:

1. challenge every SoR, cardinality, owner and delivery-order decision;
2. identify missing product, regulatory, PV, quality, security, evidence and integration capabilities;
3. distinguish mandatory regulation/standard requirements from recommended product design;
4. show any disagreement with exact section/table reference and a concrete alternative;
5. never invent a numeric MOD ID;
6. preserve merged-main versus local-unmerged evidence separation;
7. propose deletions or consolidations where this plan over-models the domain;
8. perform a second red-team pass for dual SoR, lifecycle ownership, temporal history, tenant leakage, SoD,
   idempotency/recovery and external-system reconciliation risks.
