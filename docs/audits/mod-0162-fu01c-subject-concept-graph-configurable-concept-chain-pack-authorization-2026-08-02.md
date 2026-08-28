# MOD-0162-FU01C — Subject Concept Graph / Configurable Concept Chain Boundary Pack Authorization

> Tarih: 2026-08-02
> Kimlik: **MOD-0162-FU01C — Subject Concept Graph / Configurable Concept Chain Boundary** (parent `MOD-0162`)
> Kapsam: Subject bazlı konfigüre edilebilir kavram zinciri modeli + boundary — **kod yazma yok, runtime yok**
> Sonuç: **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Task türü | Documentation / boundary authorization (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| Çalışma alanı | `execution/domains/commercial-suite/module-packs/**` + `docs/audits/**` |
| DCP-002 `MOD-0162-FU01C` | `OK  MOD-0162-FU01C: proven against Blueprint/registry.` (exit 0, `--parent MOD-0162`) |
| Kimlik notu | MOD-0162-FU01 identity note'u geçerli (domain-nötr model; EA yatay capability göçü — FU01 §18/F1) |
| Registry satırı yazımı | **Yapılmadı** (pack yetkisi dışı) |

**Ek preflight bulgusu (governance):** Blueprint'te `MOD-0058 Knowledge Graph / Entity Linking` SoR'u
*KG entities/links, link audit trails* (soft pages: KG Explorer, Entity Resolution Workbench, Link Audits) ve
`MOD-0057 Semantic Tagging & Taxonomy Management` SoR'u *taxonomies, tags, tagging rules*. "Graph"/"taxonomy"
adları çakışma riski taşıdığı için pack §2.1'de kesin sınır tablosu yazıldı ve EA uzlaştırma follow-up'ı (F1)
açıldı.

---

## 2. Dependency Confirmation

| Ön koşul | Durum | Kanıt |
|---|---|---|
| MOD-0162-FU01 Knowledge / Content & Subject Taxonomy | **PASS** | [rapor](knowledge-content-subject-taxonomy-pack-authorization-2026-08-02.md) |
| MOD-0162-FU01A KnowledgePath / Content Sequence | **PASS** | [rapor](mod-0162-fu01a-knowledge-path-content-sequence-pack-authorization-2026-08-02.md) |
| MOD-0162-FU01B EngagementJourney / Multi-Visit Progression | **PASS** | [rapor](mod-0162-fu01b-engagement-journey-multi-visit-content-progression-pack-authorization-2026-08-02.md) |
| MOD-0165 / MOD-0167 Visit Frequency Ownership | **PASS** | [rapor](mod-0165-mod-0167-visit-frequency-call-cycle-policy-ownership-pack-authorization-2026-08-02.md) |
| MOD-0150 Contact Availability | **PASS** | |
| MOD-0151 FU09A Visit/Route Readiness | **PASS** | |
| MOD-0155 | **Başlamadı** | Consumer boundary sözleşme olarak yazıldı |
| Brand/Product Master | **Başlamadı** | Optional ConceptNode/metadata olarak kaldı |
| Workflow / approval | **En sona bırakıldı** | Template `review`/`approved` yalnız metadata |
| MOD-0058 / MOD-0057 | **Pack yok (reserved/planned)** | Sınır §2.1'de yazıldı |

---

## 3. Business Need Summary

Eski pharma sistemindeki **`Indication → Profile → Need → Benefit`** zinciri iş hafızasıdır ve kaybolmamalıdır.
Ancak bu zincir **bir örnektir**, sistemin sabit veri modeli değildir:

```text
Pharma          : Indication → AudienceProfile → ProfileNeed → NeedBenefit
Almanca         : LanguageLevel → Skill → Topic → LearningNeed → Exercise
QMS/SOP         : ProcessArea → SOP → Role → TrainingNeed → ControlPoint
Teknik eğitim   : Technology → Concept → Prerequisite → PracticeNeed → LabExercise
```

Zincir hardcoded olursa her yeni subject **yeni tablo/kolon** talebi doğurur; konfigüre edilebilir olursa
kullanıcı kendi subject'i için kendi zincirini kurar. Bu FU tam olarak bu genelleştirmeyi yetkilendirir.

---

## 4. Ownership Decision

| Soru | Sahip |
|---|---|
| Ne anlatılacak / hangi içerik / kime uygun? | MOD-0162-FU01 |
| Tek oturumda hangi sırayla? | MOD-0162-FU01A |
| Çoklu oturumda hangi aşama? | MOD-0162-FU01B |
| **Bu subject'te hangi kavramlar var, nasıl bağlanır, beklenen zincir ne?** | **Bu FU (MOD-0162-FU01C)** |
| **Enterprise entity graph / entity resolution / link audit** | **MOD-0058** |
| Data-asset tagging / tag governance | **MOD-0057** |
| Ne sıklıkla / kime hedeflenecek | MOD-0165 / MOD-0167 |
| Plan / execution / gösterim kaydı | MOD-0155 |
| Completion / score | MOD-0309 |
| Brand / Product master | Ayrı master (MDM/Product) |

**§2.1 MOD-0058 sınırı:** bu FU'nun node'ları **iş kavramlarıdır** (indication, need, benefit, skill, control
point), gerçek dünya varlıkları değil; kapsam **subject-scoped ve tenant-authored**; **motor yoktur**
(traversal/resolution/inference yok). Node **hiçbir varlığın SoR'u değildir** — master'a `ExternalRef` ile bağlanır.
Enterprise ölçekli traversal/resolution gerekirse **MOD-0058**'e gidilir.

---

## 5. Static Chain Rejection

Reddedilen beş model:

| Yanlış model | Neden |
|---|---|
| `IndicationProfileNeedBenefit` aggregate'i | Zinciri şemaya çakar; her yeni subject şema değişikliği ister |
| `DoctorProfileNeedBenefit` | Pharma-only; kurs/QMS/teknik eğitim dışarıda kalır |
| Need/Benefit'i Brand/Product içine gömmek | Brand/Product master'ı zorunlu hâle getirir; master yokken sistem kurulamaz |
| `KnowledgeContent` içine sabit `NeedBenefit` alanı | Aynı içerik farklı need/benefit bağlamlarında kullanılır |
| `VisitPlan` içine concept chain hardcoded | Kavram sahipliğini MOD-0155'e gömer; eğitimde yeniden kullanılamaz |

**Karar:** pharma zinciri, `ConceptChainTemplate` kayıtlarından **biridir** — çekirdek şema değildir.

---

## 6. ConceptType Model

`ConceptTypeId` · `TenantId` (JWT claim) · **`SubjectId`** · `ConceptTypeCode` (subject içinde unique, **stabil**) ·
`ConceptTypeName` · `Description?` · `SortOrder` · `Status` · audit dörtlüsü.

Örnekler: pharma (`indication`, `audience-profile`, `profile-need`, `need-benefit`, `atc-code`,
`therapeutic-area`, `specialty`) · Almanca (`language-level`, `skill`, `grammar-topic`, `vocabulary-set`,
`learning-need`, `exercise-type`) · QMS (`process-area`, `sop`, `role`, `training-need`, `control-point`).

Kurallar: tenant-scoped + subject bazlı · **hardcoded enum yok** · kod stabil, rename ad/alias ile ·
**archived tip yeni node'da kullanılamaz** · hard delete yok · history korunur.
`audience-profile` bir ConceptType olabilir ama **FU01'deki `AudienceProfile` master'ının yerine geçmez**
(bağlantı `ExternalRef` ile).

---

## 7. ConceptNode Model

`ConceptNodeId` · `TenantId` · `SubjectId` + `ConceptTypeId` (tutarlılık zorunlu → aksi **400**) ·
`ConceptNodeCode` (unique, stabil) · `ConceptNodeName` · `Description?` · `Status` · `EffectiveFrom` ·
`EffectiveTo?` · **`ExternalRefType`/`ExternalRefId`** (optional; `brand`/`product`/`document`/
`audience-profile`/`reference-data-value`/`other` — **master SoR olarak kalır**) · `MetadataJson?` · audit.

Kurallar: effective window · **archived node yeni content/path/journey bağlantısında kullanılamaz** (mevcut
linkler history) · hard delete yok · `MetadataJson` bir **kaçış kapısıdır, sözleşme değildir** — iş kuralı
oradan okunmaz, kalıcı ihtiyaç ConceptType/alan olarak modellenir.

---

## 8. ConceptRelationship Model

`RelationshipId` · `TenantId` · `SubjectId` · `FromConceptNodeId` · `ToConceptNodeId` · `RelationshipType`
(MOD-0048 set'i) · `RelationshipCode` · `RelationshipName` · **`Direction`** (`outbound` varsayılan /
`bidirectional` açık beyan; ters kenar **otomatik türetilmez**) · **`Priority`** (küçük değer önce) · `Status` ·
`EffectiveFrom` · `EffectiveTo?` · audit.

| Kural | Karar |
|---|---|
| Aynı subject | Zorunlu; **cross-subject varsayılan yasak** → 400 (açık bridge = future, F6) |
| **Cycle detection** | **Zorunlu** — active ilişkilerde döngü → **400** |
| Self-reference | `From == To` → **400** |
| Duplicate | Aynı `(From, To, RelationshipType)` ikinci active → **409** |
| Determinizm | `Priority`, eşitlikte stabil `RelationshipCode` sırası; **sessiz/rastgele zincir seçimi yasak**, seçim gerekçesi görünür |
| Hard delete | Yok; archived ilişki yeni resolution'da kullanılmaz, history korunur |

---

## 9. ConceptChainTemplate Model

`ChainTemplateId` · `TenantId` · `SubjectId` · `ChainCode` (stabil) · `ChainName` · `Description?` ·
**`OrderedConceptTypes`** (sıralı tip listesi, en az 2, hepsi aynı subject → aksi 400) · `Status` · `Version` ·
`EffectiveFrom` · `EffectiveTo?` · audit.

Kurallar: aynı subject'te **birden fazla** template olabilir · versioned · yalnız **published + effective**
tüketilebilir · **draft/review consumer'a aktif öneri gitmez** · template **node değil tip sırası** tanımlar ·
kullanıcı yeni subject için kendi zincirini kurar · **pharma template varsayılan olabilir ama core değildir** ·
v1'de aynı tip bir template'te **iki kez geçemez** → 400 (özyineleme F7) · published sürümde
`OrderedConceptTypes` **dondurulur** · aynı `ChainCode` örtüşen pencerede iki published → **409**.

**§6.1 Template ↔ ilişki uyumu:** template **beklentidir, zorlayıcı şema değildir**. `(fromType → toType)` çifti
hiçbir published template'te yoksa ilişki **reddedilmez**, **non-conforming olarak işaretlenir** ve diagnostics'te
**görünür** olur — sessiz kabul de sessiz ret de yasak.

---

## 10. Content / Path / Journey Linkage

İzin verilenler: `ConceptNode → KnowledgeContent | KnowledgePath | EngagementJourneyStage` ·
`ConceptRelationship → KnowledgeContent | KnowledgePath` ·
`ConceptChainTemplate → EngagementJourney | (optional) KnowledgePath`.

Kurallar: linkler **metadata/selection input**tur · **recommendation engine, AI personalization ve
best-next-content bu FU'da yok** · link varsa response/projection'da **görünür** · link yoksa **default content
uydurulmaz** · archived hedefe yeni link kurulamaz (mevcutlar history) · link **referanstır**, alan kopyalanmaz.

---

## 11. Pharma Compatibility

```text
Indication → Profile → Need → Benefit
   ↳ Indication → AudienceProfile → ProfileNeed → NeedBenefit   (ConceptType'lar olarak)
```

`Profile` yerine **generic `AudienceProfile`** · `DoctorProfileId` yalnız optional metadata veya ConceptNode ·
`ProfileNeed`/`NeedBenefit` **hardcoded field değil**, ConceptType/Node · Brand/Product **zorunlu değil** ·
`ATCCode`/`TherapeuticArea`/`Specialty` ConceptNode veya opsiyonel metadata · pharma dışı subject'ler **aynı
modelle** desteklenir.

---

## 12. Non-pharma Examples

```text
Almanca  : A1 → Speaking → Selamlaşma → Günlük konuşmaya başlama → Role-play
           → German A1 Greetings Path (KnowledgePath)
QMS      : Document Control → SOP-0001 → Document Controller → Controlled copy handling
           → Effective baseline check  →  SOP-0001 Training Article (KnowledgeContent)
Teknik   : .NET → CQRS → MediatR Basics → Pipeline Behavior Practice → Validation Behavior Lab
           → CQRS Pipeline Lesson Path (KnowledgePath)
```

QMS örneğinde `SOP-0001` node'u controlled document'a **`ExternalRef` ile** bağlanır — SOP'un SoR'u
**MOD-0029**'dur, node kopya tutmaz.

---

## 13. Runtime / Engine Boundary

**Yapılmaz:** concept resolution engine · graph traversal engine · recommendation engine · AI personalization ·
scoring · best-next-content · automatic journey/path selection · visit plan üretimi · digital detailing ·
runtime execution.

Consumer'lar graph'ı **okuyabilir**; **karar motoru ayrı authorization** gerektirir (F4; enterprise traversal
gerekirse MOD-0058).

---

## 14. MOD-0155 Consumer Boundary

Tüketebilir: visit objective bağlamını anlamak · profile need'e uygun content/path seçmek · campaign hedefini
journey stage ile eşleştirmek · ziyarette hangi need/benefit işlendiğini **evidence** olarak saklamak.

**Bu FU:** MOD-0155 implementation · visit/route plan · **content selection engine** · doctor response capture ·
objection handling execution **yapmaz**.

---

## 15. MOD-0165 / MOD-0167 Boundary

Campaign hedefi ileride concept zinciriyle ifade edilebilir (indication + audience profile + need + benefit +
frequency + journey). Ancak bu FU: campaign engine · segmentation engine · frequency runtime · due/overdue ·
**target assignment** yapmaz (FU01B §8 sınırıyla aynı).

---

## 16. Brand / Product Boundary

Brand/Product **optional ConceptNode veya metadata**; master bu FU'da yapılmaz ve core chain'in zorunlu parçası
değildir. Pharma subject'i için `brand`/`product` bir ConceptType **olabilir** (tercih, zorunluluk değil).
Master geldiğinde node `ExternalRefType=brand|product` ile bağlanır, **master SoR kalır**.
**Sistem Brand/Product olmadan da concept chain kurabilmelidir** — kabul kriteri olarak yazıldı.
**Follow-up:** `Brand/Product Master Boundary Pack Authorization`.

---

## 17. Governance / Versioning

Dört nesne de effective window taşır (`ChainTemplate` ayrıca `Version`) · **hard delete yok** · archived öğe yeni
linking/resolution'da kullanılamaz, mevcut linkler **history olarak korunur** · rename **kod değiştirmez** ·
`Priority` deterministik, sessiz zincir seçimi yasak · **cycle detection zorunlu** · cross-subject **future
bridge** · yalnız published+effective template tüketilir · draft/review consumer'a aktif öneri gitmez ·
vokabülerler MOD-0048 set'i (hardcoded enum yasak → fail-closed 400) · subject archive edilirse bağlı
Type/Node/Relationship/Template archived kabul edilir, **silinmez**.

---

## 18. Permission Boundary

`crm.knowledge.concept.read` · `crm.knowledge.concept.manage` · `crm.knowledge.concept-template.manage` ·
`crm.knowledge.concept-link.manage`. **Seed/grant yapılmadı**; katalog hazır değilse anahtar tanımlanır ama `All`
listesine eklenmez + geçici fallback + `-RBAC` follow-up (F5).

---

## 19. Explicit Exclusions

Runtime implementation · **graph traversal engine** · **recommendation engine** · **AI personalization** ·
best-next-content · visit planning · route planning · digital detailing · content usage tracking ·
journey progress engine · stage advancement engine · campaign engine · segmentation engine · frequency engine ·
due/overdue engine · target assignment · Brand/Product master implementation · workflow approval ·
MOD-0023 entegrasyonu · file upload/render/preview · learning completion · patient data ·
Account/Contact mutation · territory mutation · hard delete · Mongo hand-edit · RBAC seed/grant · registry write ·
MOD-0048 publish · `TenantId` payload'da.

---

## 20. Contract Flags

```json
{
  "supportsSubjectConceptGraph": true,
  "supportsConfigurableConceptChain": true,
  "supportsConceptType": true,
  "supportsConceptNode": true,
  "supportsConceptRelationship": true,
  "supportsConceptChainTemplate": true
}
```

Pack seviyesinde **öneri**; canlı contract'a yazılmadı. **Eklenmedi:** `supportsRecommendationEngine` ·
`supportsAiPersonalization` · `supportsVisitPlanning` · `supportsRoutePlanning` · `supportsDigitalDetailing` ·
`supportsWorkflowApproval`. FU01 / FU01A / FU01B flag setleri ve MOD-0151 canlı contract'ı **değişmedi**.

---

## 21. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0155 code changed? | **No** |
| MOD-0162 runtime changed? | **No** (yalnız FU01 pack dokümanında not + F11/F12 satırları) |
| **Graph engine opened?** | **No** |
| **Recommendation engine opened?** | **No** |
| **AI personalization opened?** | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Digital detailing opened? | **No** |
| Campaign/frequency engine opened? | **No** |
| Target assignment opened? | **No** |
| Brand/Product implementation opened? | **No** |
| Workflow/approval opened? | **No** |
| Patient data opened? | **No** |
| Account/Contact mutation opened? | **No** |
| Territory mutation opened? | **No** |
| RBAC seed/grant changed? | **No** |
| Registry write? | **No** |
| MOD-0048 publish changed? | **No** |
| **Static pharma chain avoided?** | **Yes** |
| **Configurable concept chain boundary added?** | **Yes** |
| **Pharma compatibility preserved?** | **Yes** |
| **Non-pharma subjects supported?** | **Yes** (Almanca · QMS · teknik eğitim) |
| Follow-ups opened? | **Yes** (8 adet) |

Pack frontmatter doğrulaması: `status: draft` · `runtime_code_allowed: false` · `shell: none` ·
`golden_reference: none` · `form_field_count: 0`.

---

## 22. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01C-subject-concept-graph-configurable-concept-chain.md` | **Oluşturuldu** |
| `execution/domains/commercial-suite/module-packs/MOD-0162-FU01-knowledge-content-subject-taxonomy.md` | **Güncellendi** (yalnız doküman) — §4'e "Subject/Topic ≠ concept graph, `Indication/Need/Benefit` Topic ağacına gömülmez" satırı; §18'e F11 + F12 (kapatıldı) |
| `docs/audits/mod-0162-fu01c-subject-concept-graph-configurable-concept-chain-pack-authorization-2026-08-02.md` | **Oluşturuldu** (bu rapor) |

Runtime kod, config, gateway, RBAC, reference data ve registry **değiştirilmedi**.

---

## 23. Final Verdict

### **PASS**

- Static `Indication → Profile → Need → Benefit` modeli **reddedildi**; zincir bir **ConceptChainTemplate** olarak
  korundu.
- Subject bazlı **configurable** Concept Chain yetkilendirildi: `ConceptType` / `ConceptNode` /
  `ConceptRelationship` / `ConceptChainTemplate` alan sözleşmeleriyle tanımlandı.
- Pharma uyumluluğu korundu (generic `AudienceProfile`, `ProfileNeed`/`NeedBenefit` concept olarak,
  ATC/TherapeuticArea/Specialty bağlanabilir) ve **pharma dışı üç subject** (Almanca, QMS, teknik eğitim) aynı
  modelle karşılandı.
- İlişki kuralları sağlamlaştırıldı: yön görünürlüğü, cross-subject yasağı + future bridge, **cycle detection**,
  duplicate 409, deterministik priority, **sessiz seçim yasağı**.
- Template ↔ ilişki uyumu **görünür non-conforming** olarak çözüldü — sessiz kabul de sessiz ret de yok.
- Content/Path/Journey bağlantısı **selection input** olarak mümkün oldu; recommendation/AI/best-next-content
  açılmadı.
- Brand/Product **optional ConceptNode/metadata**; sistem Brand/Product olmadan da zincir kurabiliyor.
- `ExternalRef` kuralıyla **node hiçbir varlığın SoR'u değil** — MOD-0058/MOD-0057/MOD-0029/MDM sınırları korundu.
- Runtime graph/recommendation/AI/visit planning scope'u **açılmadı**; mevcut scope'lar bozulmadı.

FAIL kriterlerinin hiçbiri tetiklenmedi. Kayda geçen governance açığı: **MOD-0058 "Knowledge Graph" ile ad/kapsam
yakınlığı** — sınır §2.1'de kesin yazıldı, kalıcı uzlaştırma EA'ya (F1) bırakıldı; kapsam ayrımı bugün net
olduğu için PASS'ı düşürmez.

---

## 24. Next Recommended Prompt

`Brand/Product Master Boundary Pack Authorization`
