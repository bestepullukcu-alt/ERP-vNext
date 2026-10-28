---
id: MOD-0057
name: Semantic Tagging & Taxonomy Management
domain: commercial-suite
service: Diten.CrmService
service_port: 5061
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: TagTaxonomies
    data_mode: client
    data_mode_max_rows: 200
  - module: TagTaxonomyTerms
    data_mode: server
  - module: TagAssignments
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: not-approved
owner: "Treasury Intake Control Tower (taslak) — commercial-suite domain sahibi atanmadı (yerleşim KARAR D61)"
branch: feature/crm/mod-0057-semantic-tagging-taxonomy
started: 2026-10-27
target: 2027-06-30
form_field_count: 6
slice: "TRM-18 — v1 bağımsız: taksonomi ağaçları + düz etiket kümeleri + başka modül varlıklarına opak başvuruyla etiket bağlama + tüketici okuma sözleşmesi (arama, bilgi grafiği, otomatik etiketleme, onay iş akışı YOK)"
---

# MOD-0057 — Semantic Tagging & Taxonomy Management

> **Revizyon (2026-10-28, sahip denetimi 5cfd67813 KOŞULLU):** (1) **P1 ağaç çiti:** kayıt bazındaki CAS iki kök terimin
> eşzamanlı olarak birbirinin altına taşınmasını önlemiyordu (iki işlem farklı belgeye yazar, ikisi de commit olur → döngü).
> Taksonomi düzeyinde ortak çit eklendi: `TagTaxonomy.HierarchyVersion` (Entity Fields); ağaç yapısını değiştiren terim
> komutları sürümü işlem içinde okur ve aynı işlemde koşullu artırır, çakışma 409 `CRM_TAG_CONCURRENCY_CONFLICT`, otomatik
> yeniden deneme yok (Runtime Constraints "Ağaç çiti", Validation Rules `ParentTermId`, Failure Path). **Yeni AC-42**
> (gerçek replica set'te karşılıklı taşıma yarışı, sabotajlı; DL-1). (2) **P2 kesişen AC'ler:** "kesişen AC yok" cümlesi
> yanlıştı; birden çok dilime uzanan AC'ler (AC-6, AC-11, AC-12, AC-13, AC-14, AC-15, AC-16, AC-18, AC-19, AC-24, AC-25,
> AC-27, AC-28) dilim bitişlerinde "(… kısmı)" ile yazıldı, son ilgili dilimde tam kapanır; dilim tablosu değişmedi.
> (3) **Yerleşim KARAR D61 ile onaylandı** (K1, commercial-suite sahibi atanana kadar); O-1 kapandı. Servis / port (O-2),
> MOD-0162-FU01 sınırı (O-4), kapsam ve diğer açık kararlar açık kalır; commercial-suite domain sahibi ataması ayrı açık
> nokta (O-13). Mevcut AC numaraları değişmedi. `status: draft`.
>
> **Durum: `draft` (planlama belgesi), `approval_status: not-approved`.** Bu paket kod, servis değişikliği, gateway
> rotası, defter satırı ya da kaynak projeden kod alma izni vermez. **Yerleşim onaylandı (KARAR D61,
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` "## D61"):** commercial-suite için kayıtlı domain
> sahibi olmadığından K1, sahip atanana kadar MOD-0057'nin `commercial-suite`'e yerleşimini yönetim olarak onayladı (D55/2
> önerisi). Bu yalnız yerleşim onayıdır: domain-config In-Scope satırı ve kimlik kaydı satırı paket onay PR'ında önerilir —
> bu taslak onları değiştirmez (XMC-001); servis / port (O-2), MOD-0162-FU01 sınırı (O-4), kapsam ve diğer açık kararlar
> açık kalır; commercial-suite domain sahibi ataması ayrı açık noktadır (O-13; atandığında D61 onayı ona bilgi olarak
> gider). D61 paket onayı, kod izni ya da sahiplik ataması değildir. **Kod alma izni yok:** kaynak (`R@e4c93c29`) yalnız okunur,
> başvuru malzemesidir; kod alma ayrı karardır (D55/4, D1 deseni; öneri O-9). Kod ancak paket `approved` /
> `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten sonra
> yazılır. Engeller: "Ready-for-dev Checklist" (işaretsiz maddeler paket düzeyi; "— DL-n için" maddeleri o dilimin
> önkoşulu, D51/1) ve "Açık kararlar". `target` tarihi yer tutucudur, sahip kararı değildir.
>
> **Bağlanan kararlar (yeniden karar verilmez):** **D55** (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md`)
> — (1) park kalkar, level-1 kaydı geçerli; (2) yerleşim önerisi `commercial-suite`, domain sahibi onayına bağlı; (3) v1
> bağımsız: taksonomi ve etiket yönetimi (sınıf ağaçları, etiketler, varlığa etiket bağlama), arama ve bilgi grafiği
> bağlantısı MOD-0053 / MOD-0056 Recovery'ye gelince sonraki sürüm; (4) kuleyi Treasury CT yürütür, taslağı CT yazar, kod
> alma ayrı karar. **D61** (`-05.md`) — D55/2 yerleşim önerisini K1, commercial-suite sahibi atanana kadar onayladı
> (yalnız yerleşim). **D19/2** (`-02.md`) tarihçedir — "park et" kararı D55/1 ile kalktı. **D51/1** (paket kapısı ile dilim
> kapısı ayrı) ve **D53** (Auth iki parça: paket metni paket kapısında, kayıt kodu DL-1 kabul kapısında) bu paketin
> checklist biçimine uygulanır.
>
> **Level-1 koşullu kabul:** `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0057-v1.0.md` — üç koşul,
> bkz. "Implementation Notes → Level-1 koşulları". Kuyruk: `treasury-intake` TRM-18
> (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:25`).
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-27, bu worktree (HEAD `40a1bcc91`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0057 --name "Semantic Tagging & Taxonomy Management"` →
> `OK  MOD-0057: proven against Blueprint/registry.` (çıkış 0). `--discover "Semantic Tagging"` → çıkış 3 (inceleme gerekli),
> rapor sha256 `8799a84fb2afab3242b9b7681f2208a4f3952fded031bbed5ed5ba3611386ca1`: Blueprint 1 aday (MOD-0057, "2) Data,
> Knowledge & Intelligence / Search & Knowledge Graph", veri nesnesi `taxonomies`), registry 0, paket 0, gateway 0, görev 1
> (TRM-18). Mükerrer yok; görev satırı bu paketin kendi kuyruk satırıdır.
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.0
> (`/Users/natig/Projects/treasury-transfer-handoff/source-batch6-v1.0/docs/roadmap/plans/treasury-source-transfer-preparation/mod0057-transfer-packet-v1.0/`):
> `manifest.json` sha256 `fd3e001e22125d77c0d28fa7ae24fafc1e97e8afb333662c0d20b9eeac98bd6a` (bu oturumda `shasum -a 256` ile
> ölçüldü, level-1 kaydıyla aynı); `MOD-0057-TRANSFER-PACKET-v1.0.md` `4aa60983…63e5`, `CONTRACT-STATE.json` `315b853c…d997`,
> `AUTHOR-REPORT.md` `91ff0591…2a95` — üçü de manifest değeriyle aynı. `SOURCE-BINDINGS.json` (`921f8215…0762`) açılmadı.
> Kaynak kodun kendisinden yalnız üç dosya salt-okunur `git -C … show/grep` ile okundu (`TaxonomyEntities.cs`,
> `TaxonomiesController.cs:585-605`, `Infrastructure/DependencyInjection.cs` `"*"` satırları); diğer `Dosya.cs:satır@e4c93c29`
> atıfları paketten aktarılmıştır. Recovery atıfları `yol:satır@40a1bcc91` (bu worktree, bu oturumda ölçüldü).
>
> **Revizyon 1 (2026-10-27):** ilk taslak. Bu revizyon paketi `ready-for-dev` yapmaz.

## Module Summary

Kiracının **genel amaçlı sınıflandırma ve etiketleme** hizmeti: kiracı kendi taksonomilerini (kodlu terimlerden oluşan
sınıf ağaçları) ve düz etiket kümelerini tanımlar, bunların terimlerini başka modüllerin kayıtlarına **opak başvuruyla**
(modül kimliği + kayıt türü + kayıt kimliği) bağlar; tüketici modüller bir kaydın etiketlerini sürümlü bir okuma
sözleşmesiyle okur. Blueprint (paket §A, ölçüm üreticinin, 8.1 satır 58; 8.8 aynı ad ve dalga — keşif raporu): Domain
`2) Data, Knowledge & Intelligence`, Capability Group `Search & Knowledge Graph`, Wave `W-4`, Dependency Gate
`Data Dictionary/Glossary; Enterprise Search` (MOD-0053, MOD-0056), Placement `Data Governance Service`, SoR "taxonomies,
tags, tagging rules, tag approvals", asgari sözleşme `TAXONOMY-BUNDLE` (kaynakta eseri yok — paket §B).

- **v1 kapsamı (D55/3):** taksonomi + terim ağacı, düz etiket kümesi, etiket bağlama ve kaldırma, tüketici okuma
  sözleşmesi, kiracı ekranları. Blueprint SoR'unun "tagging rules" ve "tag approvals" kısmı v1'de **yok** (Follow-up).
- **Hedef kullanıcı:** taksonomi yöneticisi (kiracıdaki sınıflandırma sorumlusu; taksonomi ve terim kurar), etiketleyici
  (kayıtlara etiket bağlar / kaldırır), okuyucu (etiketleri görür), tüketici modül (kullanıcı token'ıyla Gateway üzerinden
  etiket okur).
- **Çekirdek kural:** etiket = bir taksonominin terimi. Hiyerarşik taksonomide terimler ağaçtır (en çok 5 derinlik); düz
  taksonomide (`Kind = Flat`) terimler etikettir, ebeveynleri yoktur (O-11). Bağ yalnız `Active` taksonominin kullanımda
  olan terimine kurulur; hedef türü taksonominin izinli hedef listesinde olmalıdır.
- **Yok (v1):** arama indeksi, bilgi grafiği ve terim ilişkileri (Related / Broader / Equivalent), otomatik / kural tabanlı
  / ML etiketleme ve öneri, taksonomi sürümleri ve onay iş akışı (MOD-0023), olay yayını, kiracılar arası taksonomi, ters
  arama ("bu etiketi taşıyan bütün kayıtlar"), çok dilli terim etiketleri.

## Ownership and Boundaries

- **In-scope (v1):** `TagTaxonomy` (oluştur, düzenle, taslak silme, toplu taslak silme, etkinleştir, emekliye ayır);
  `TagTerm` (oluştur, düzenle / taşı, kullanılmamış terimi silme, toplu silme, kullanımdan kaldır + yerine geçen terim);
  `TagAssignment` (bağla, kaldır, toplu kaldır); hedef başına terim sınırı (`MaxTermsPerTarget`); tüketici okuma
  sözleşmesi `taxonomy-tagging-read.v1`; üç kiracı ekranı (`Views/CRM/TagTaxonomies/`, `Views/CRM/TagTaxonomyTerms/`,
  `Views/CRM/TagAssignments/`).
- **Out-of-scope (v1):** arama / bilgi grafiği (MOD-0053, MOD-0056 — D55/3), terim ilişkileri, etiketleme kuralları ve
  simülasyon, etiket önerileri, onay iş akışı ve taksonomi sürümleri, olaylar, tohum (seed) verisi ve varsayılan taksonomi
  kaydı içe aktarımı, başka modül ekranlarına gömülü etiket paneli (her tüketici kendi XMC-001 onayıyla — Follow-up), çok
  dilli terim etiketleri, kiracılar arası paylaşılan taksonomi.
- **MOD-0162-FU01 sınırı (O-4, ölçüldü @40a1bcc91):** MOD-0162-FU01 (`approved`) Knowledge içeriğinin **iş taksonomisinin**
  (Subject / Topic / AudienceProfile) sahibidir ve kendi §2 tablosunda MOD-0056 / MOD-0057'yi yalnız "indeksleme /
  etiketleme seam'i" olarak anar; "business Subject/Topic taksonomisi burada kalır" ve §4'te "MOD-0057 semantic tagging'in
  yerine geçmez, onunla çakışmaz" der (`execution/domains/commercial-suite/module-packs/MOD-0162-FU01-knowledge-content-subject-taxonomy.md:118,177`).
  Bu taksonomi kodda canlıdır: `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/{Subject,Topic,AudienceProfile}/`,
  izinler `crm.knowledge.*` (`Features/Knowledge/KnowledgePermissions.cs:11-14`). **Bu paket:** Subject / Topic /
  AudienceProfile'ı sahiplenmez, kopyalamaz, eşitlemez ve onlara yazmaz; `Features/Knowledge/**` korunan yoldur.
  MOD-0057, modüller arası **genel** sınıflandırma ve etiketlemenin sahibidir. Çakışan noktalar "Açık kararlar" O-4'te.
- **MOD-0053 Data Dictionary / Glossary sınırı (O-12):** İş terimleri, eşanlamlılar ve terim onayı MOD-0053'e aittir;
  MOD-0057 v1 etiketi iş terimine bağlamaz; MOD-0053 bağlantısı Follow-up. Blueprint 8.8 MOD-0057'yi MOD-0053'e **HARD**
  bağlı sayar (MOD-0053 de MOD-0052'ye HARD; `docs/records/audits/2026-10/treasury-intake-handover-review-01/MOD-0053-UZLASTIRMA-01.md:105-106`);
  D55/3 v1'i bağımsız tanımlar. Kavram ayrımı (aynı kayıt, `:152-153`): MOD-0053 = iş terimleri, tanımlar, eşanlamlılar,
  sahip (steward) onayı, metriğe bağlantı; MOD-0057 = taksonomiler, etiketler (v1'de kural ve onay yok); MOD-0162-FU01 =
  içerik için Subject / Topic. MOD-0057 terimindeki `Aliases` listesi yalnız arama / görüntü kolaylığıdır; iş sözlüğü
  anlamında eşanlamlı modeli **değildir**.
- **Sahip olunan sözleşme:** `taxonomy-tagging-read.v1` (DL-3; tüketiciye açılan okuma sözleşmesi). İlk tüketici adı
  açıktır (O-8); sözleşme tüketicisiz de yayımlanır, tüketici bağlantısı her tüketicinin kendi kararıdır.
- **Tüketilen yapılar:** `Diten.BuildingBlocks.CentralAudit` ortak iletici (AUD-001 K4, BL-501) ve Platform
  `POST /api/internal/audit/append`; MOD-0018 Auth (JWT, izin talepleri); CrmService Mongo işlem altyapısı (replica set).
  MOD-0048, MOD-0023, MOD-0262 v1'de tüketilmez.
- **Domain-config ile ilişki:** commercial-suite domain-config'te MOD-0057 satırı yok (`execution/domains/commercial-suite/domain-config.md:19-25`)
  ve bu taslak yazmaz. Yerleşim KARAR D61 ile onaylıdır; domain-config In-Scope satırı ve kimlik kaydı satırı paket onay
  PR'ında önerilir (D61). Yetki sırası gereği paket domain-config'in üstündedir; yine de domain-config satırı main'e
  girmeden `ready-for-dev` yok (Ready-for-dev Checklist).

## Owned Objects

### Varlıklar

| Varlık | Koleksiyon | Taban | Not |
|---|---|---|---|
| `TagTaxonomy` | `crm_tag_taxonomies` | `EntityBase` (CrmService) | taksonomi başlığı; `Kind` = `Hierarchical` / `Flat` |
| `TagTerm` | `crm_tag_terms` | `EntityBase` | terim (sınıf / kavram / etiket); hiyerarşide düğüm |
| `TagAssignment` | `crm_tag_assignments` | `EntityBase` | terim → hedef bağı (opak başvuru) |
| `TagTargetSlot` | `crm_tag_target_slots` | `EntityBase` | teknik sayaç: (taksonomi, hedef) başına etkin bağ sayısı; yalnız bağ komutlarının işleminde yazılır, kendi komutu yok |

`EntityBase` CrmService'in somut sınıfıdır (`services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/EntityBase.cs:7-16`:
`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `Version`). `CreatedBy` / `UpdatedBy` / `DeletedBy`
tabanda yok; varlıklara eklenir (Entity Fields).

### Komutlar (yazma)

`CreateTagTaxonomyCommand`, `UpdateTagTaxonomyCommand`, `DeleteTagTaxonomyCommand`, `BulkDeleteTagTaxonomyCommand`,
`ActivateTagTaxonomyCommand`, `RetireTagTaxonomyCommand`, `CreateTagTermCommand`, `UpdateTagTermCommand` (taşıma dahil),
`DeleteTagTermCommand`, `BulkDeleteTagTermCommand`, `DeprecateTagTermCommand`, `CreateTagAssignmentCommand`,
`DeleteTagAssignmentCommand`, `BulkDeleteTagAssignmentCommand` — 14 komut; her biri Audited Events'te tam bir kez.

### Sorgular (okuma)

`GetTagTaxonomyListQuery`, `GetTagTaxonomyByIdQuery`, `GetTagTermListQuery`, `GetTagTermByIdQuery`, `GetTagTermTreeQuery`,
`GetTagAssignmentListQuery`, `GetTagAssignmentByIdQuery`, `GetTagAssignmentsByTargetQuery`, `GetTagAssignmentsByTargetsQuery`.
Yazan sorgu yoktur.

### API uçları (CrmService, 5061; tarayıcı yalnız MVC vekili → Gateway 5000)

| Yöntem | Rota | Komut / sorgu | İzin |
|---|---|---|---|
| GET | `/api/crm/tag-taxonomies` | GetTagTaxonomyList | `crm.tag-taxonomies.read` |
| GET | `/api/crm/tag-taxonomies/{id}` | GetTagTaxonomyById | `crm.tag-taxonomies.read` |
| POST | `/api/crm/tag-taxonomies` | CreateTagTaxonomy | `crm.tag-taxonomies.create` |
| PUT | `/api/crm/tag-taxonomies/{id}` | UpdateTagTaxonomy | `crm.tag-taxonomies.update` |
| DELETE | `/api/crm/tag-taxonomies/{id}` | DeleteTagTaxonomy | `crm.tag-taxonomies.delete` |
| POST | `/api/crm/tag-taxonomies/bulk-delete` | BulkDeleteTagTaxonomy | `crm.tag-taxonomies.bulk-delete` |
| POST | `/api/crm/tag-taxonomies/{id}/activate` | ActivateTagTaxonomy | `crm.tag-taxonomies.activate` |
| POST | `/api/crm/tag-taxonomies/{id}/retire` | RetireTagTaxonomy | `crm.tag-taxonomies.retire` |
| GET | `/api/crm/tag-taxonomies/{id}/terms` | GetTagTermList | `crm.tag-taxonomies.read` |
| GET | `/api/crm/tag-taxonomies/{id}/terms/tree` | GetTagTermTree | `crm.tag-taxonomies.read` |
| GET | `/api/crm/tag-taxonomies/{id}/terms/{termId}` | GetTagTermById | `crm.tag-taxonomies.read` |
| POST | `/api/crm/tag-taxonomies/{id}/terms` | CreateTagTerm | `crm.tag-taxonomies.terms.create` |
| PUT | `/api/crm/tag-taxonomies/{id}/terms/{termId}` | UpdateTagTerm | `crm.tag-taxonomies.terms.update` |
| DELETE | `/api/crm/tag-taxonomies/{id}/terms/{termId}` | DeleteTagTerm | `crm.tag-taxonomies.terms.delete` |
| POST | `/api/crm/tag-taxonomies/{id}/terms/bulk-delete` | BulkDeleteTagTerm | `crm.tag-taxonomies.terms.bulk-delete` |
| POST | `/api/crm/tag-taxonomies/{id}/terms/{termId}/deprecate` | DeprecateTagTerm | `crm.tag-taxonomies.terms.deprecate` |
| GET | `/api/crm/tag-assignments` | GetTagAssignmentList (yönetim listesi, SEC-002) | `crm.tag-assignments.read` (+ `read-tenant-wide` ile kiracı geneli) |
| GET | `/api/crm/tag-assignments/{id}` | GetTagAssignmentById | `crm.tag-assignments.read` (listeyle aynı kapsam) |
| GET | `/api/crm/tag-assignments/by-target` | GetTagAssignmentsByTarget (okuma sözleşmesi) | `crm.tag-assignments.read` |
| POST | `/api/crm/tag-assignments/by-targets` | GetTagAssignmentsByTargets (en çok 100 hedef) | `crm.tag-assignments.read` |
| POST | `/api/crm/tag-assignments` | CreateTagAssignment | `crm.tag-assignments.create` |
| DELETE | `/api/crm/tag-assignments/{id}` | DeleteTagAssignment | `crm.tag-assignments.delete` |
| POST | `/api/crm/tag-assignments/bulk-delete` | BulkDeleteTagAssignment | `crm.tag-assignments.bulk-delete` |

### DTO'lar

`TagTaxonomyModels.cs`, `TagTermModels.cs`, `TagAssignmentModels.cs` — her özellik klasöründe tek dosya (Golden Reference
deseni). Hiçbir istek DTO'su `TenantId`, `ActorId`, `CreatedBy` taşımaz. Okuma sözleşmesi `taxonomy-tagging-read.v1`
alanları: `targetModule`, `targetType`, `targetId`, `assignments[]` → `assignmentId`, `taxonomyId`, `taxonomyName`,
`taxonomyKind`, `taxonomyStatus`, `termId`, `termCode`, `termLabel`, `termPath` (kodlardan), `termStatus`, `assignedAt`.

### Frontend rotaları

`/CRM/TagTaxonomies`, `/CRM/TagTaxonomyTerms?taxonomyId={id}`, `/CRM/TagAssignments` (Layout & Shell Contract).

### İzinler

Authorization Convention tablosu (öneri, O-5).

### Durumlar

| Nesne | Durumlar | Geçişler |
|---|---|---|
| `TagTaxonomy.Status` | `Draft`, `Active`, `Retired` | Draft → Active (en az bir kullanımdaki terim) · Active → Retired · Draft → silinmiş (soft) · Retired'dan dönüş yok (v1) |
| `TagTerm.Status` | `Active`, `Deprecated` | Active → Deprecated (isteğe bağlı yerine geçen terim) · dönüş yok (v1) · hiç bağlanmamış ve çocuksuz terim soft silinir |
| `TagAssignment` | etkin / kaldırılmış (`IsDeleted`) | etkin → kaldırılmış; aynı terim aynı hedefe yeniden bağlanırsa yeni kayıt |

## Entity Fields

### TagTaxonomy (`crm_tag_taxonomies`)

| Alan | Tür | Zorunlu | Kural |
|---|---|---|---|
| `Id` | Guid | evet | sunucu |
| `TenantId` | Guid | evet | yalnız `ITenantContext`'ten; DTO'da yok |
| `Name` | string | evet | trim, 1–200 |
| `NormalizedName` | string | evet | sunucu: trim + iç boşluk tekleme + `ToUpperInvariant` |
| `Description` | string? | hayır | ≤ 1000 |
| `Kind` | enum `Hierarchical` / `Flat` | evet | ilk etkinleştirmeden sonra değişmez |
| `AllowedTargetTypes` | list<{`TargetModule`, `TargetType`}> | evet | 1–20 öğe, tekrarsız; biçim Validation Rules |
| `MaxTermsPerTarget` | int? | hayır | boş = sınırsız; 1–50 |
| `Status` | enum `Draft` / `Active` / `Retired` | evet | sunucu; yalnız geçiş komutlarıyla |
| `HierarchyVersion` | long | evet | sunucu; 0'dan başlar; **ağaç çiti** — yalnız ağaç yapısını değiştiren terim komutlarının işleminde koşullu `$inc` (Runtime Constraints "Ağaç çiti"); `Version` CAS'ından ayrıdır; istek DTO'sunda yok |
| `ActivatedAt` / `ActivatedBy` | DateTimeOffset? / Guid? | — | sunucu |
| `RetiredAt` / `RetiredBy` | DateTimeOffset? / Guid? | — | sunucu |
| `CreatedBy` / `UpdatedBy` / `DeletedBy` | Guid / Guid? / Guid? | — | yalnız JWT `sub` |
| `IsDeleted` / `DeletedAt` / `Version` / `CreatedAt` / `UpdatedAt` | taban | — | soft delete + CAS |

### TagTerm (`crm_tag_terms`)

| Alan | Tür | Zorunlu | Kural |
|---|---|---|---|
| `TaxonomyId` | Guid | evet | aynı kiracının silinmemiş taksonomisi |
| `TermCode` | string | evet | `^[a-z0-9][a-z0-9-]{0,63}$`; oluşturulduktan sonra **değişmez**; silinen terimin kodu yeniden kullanılmaz |
| `Label` | string | evet | trim, 1–200 |
| `NormalizedLabel` | string | evet | sunucu |
| `Description` | string? | hayır | ≤ 1000 |
| `ParentTermId` | Guid? | hayır | yalnız `Hierarchical`; aynı taksonomi; döngü yok |
| `SiblingKey` | string | evet | sunucu: ebeveyn kimliği metni ya da `root` (kardeş etiket tekilliği için) |
| `Path` | string | evet | sunucu: kodlardan `/kod1/kod2/…` (etiket adından değil — yeniden adlandırma yolu bozmaz) |
| `Depth` | int | evet | sunucu; 1–5 |
| `SortOrder` | int | hayır | 0–9999, varsayılan 0 |
| `Aliases` | list<string> | hayır | ≤ 10 öğe, her biri 1–200, normalize hâlde tekrarsız |
| `Status` | enum `Active` / `Deprecated` | evet | sunucu |
| `ReplacementTermId` | Guid? | hayır | yalnız kullanımdan kaldırırken; aynı taksonominin `Active` terimi, kendisi değil |
| `DeprecatedAt` / `DeprecatedBy` | — | — | sunucu |
| `HasEverBeenAssigned` | bool | evet | sunucu; ilk bağ işleminde aynı işlemde `true`; silme kuralı bunu okur |
| `CreatedBy` / `UpdatedBy` / `DeletedBy` + taban alanlar | — | — | yukarıdaki gibi |

### TagAssignment (`crm_tag_assignments`)

| Alan | Tür | Zorunlu | Kural |
|---|---|---|---|
| `TaxonomyId` / `TermId` | Guid | evet | aynı kiracı; terim bu taksonominin |
| `TargetModule` | string | evet | `^MOD-\d{4}(-FU\d{2}[A-Z]?)?$` |
| `TargetType` | string | evet | `^[a-z][a-z0-9-]{0,63}$` |
| `TargetId` | string | evet | 1–128, ASCII yazdırılabilir, boşluksuz |
| `TargetKey` | string | evet | sunucu: `{TargetModule}|{TargetType}|{TargetId}` |
| `Note` | string? | hayır | ≤ 500 |
| `CreatedBy` / `DeletedBy` | Guid / Guid? | — | yalnız JWT `sub` |
| taban alanlar | — | — | kaldırma = soft delete |

### TagTargetSlot (`crm_tag_target_slots`)

`TaxonomyId`, `TargetKey`, `ActiveCount` (≥ 0) + taban alanlar. Yalnız `MaxTermsPerTarget` dolu taksonomilerde yazılır.

### Mongo indeksleri

| Ad | Koleksiyon | Anahtar | Seçenek |
|---|---|---|---|
| `ux_crm_tag_taxonomy_name` | taxonomies | (TenantId, NormalizedName) | unique, kısmi `IsDeleted = false` |
| `ix_crm_tag_taxonomy_status` | taxonomies | (TenantId, Status, IsDeleted) | — |
| `ux_crm_tag_term_code` | terms | (TenantId, TaxonomyId, TermCode) | unique, **kısmi değil** (kod yeniden kullanılmaz) |
| `ux_crm_tag_term_sibling_label` | terms | (TenantId, TaxonomyId, SiblingKey, NormalizedLabel) | unique, kısmi `IsDeleted = false` |
| `ix_crm_tag_term_path` | terms | (TenantId, TaxonomyId, Path) | — |
| `ux_crm_tag_assignment_active` | assignments | (TenantId, TermId, TargetKey) | unique, kısmi `IsDeleted = false` |
| `ix_crm_tag_assignment_target` | assignments | (TenantId, TargetKey, IsDeleted) | — |
| `ix_crm_tag_assignment_creator` | assignments | (TenantId, CreatedBy, CreatedAt) | — |
| `ux_crm_tag_target_slot` | target_slots | (TenantId, TaxonomyId, TargetKey) | unique |

Yeni sınıflar `RegisterClassMaps`'e kaydedilir (aksi hâlde Guid alanları ikili yazılır ve süzgeçler sessizce boş döner —
MOD-0162-FU01 §17 notu, `services/Diten.CrmService/src/Diten.CrmService.Persistence/DependencyInjection.cs`).

### Hata sözlüğü (öneri)

`CRM_TAG_FORBIDDEN_FIELD` 400 · `CRM_TAG_VALIDATION` 400 · `CRM_TAG_TERM_CYCLE` 400 · `CRM_TAG_TERM_DEPTH_EXCEEDED` 400 ·
`CRM_TAG_FLAT_NO_PARENT` 400 · `CRM_TAG_TERM_CODE_IMMUTABLE` 400 · `CRM_TAG_TARGET_TYPE_NOT_ALLOWED` 400 ·
`CRM_TAG_TAXONOMY_NAME_DUPLICATE` 409 · `CRM_TAG_TERM_CODE_DUPLICATE` 409 · `CRM_TAG_TERM_LABEL_DUPLICATE` 409 ·
`CRM_TAG_ASSIGNMENT_DUPLICATE` 409 · `CRM_TAG_TARGET_LIMIT_REACHED` 409 · `CRM_TAG_TAXONOMY_NOT_DRAFT` 409 ·
`CRM_TAG_TAXONOMY_STATE` 409 · `CRM_TAG_TERM_IN_USE` 409 · `CRM_TAG_TERM_NOT_ASSIGNABLE` 409 ·
`CRM_TAG_CONCURRENCY_CONFLICT` 409 · `CRM_TAG_AUDIT_UNAVAILABLE` 503. Bulunamayan ve başka kiracının kaydı 404 (ayrım yok).
Kaynaktaki küçük harfli hata literalleri (`taxonomy_cycle_detected` …, paket §E) kopyalanmaz.

## Repo Scope

- `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{TagTaxonomy,TagTerm,TagAssignment,TagTargetSlot}.cs` +
  enum dosyaları (`TagTaxonomyKind`, `TagTaxonomyStatus`, `TagTermStatus`).
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/{TagTaxonomies,TagTerms,TagAssignments}/**`
  (Backend File Convention).
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/**` — üç depo + birim iş (işlem) sınıfı, indeksler,
  `RegisterClassMaps` kaydı.
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/{TagTaxonomiesController,TagTermsController,TagAssignmentsController}.cs`
  ve CentralAudit kaydı (servis düzeyi; O-6).
- `services/Diten.CrmService/tests/**` — birim, entegrasyon (gerçek replica set), mimari.
- `tests/architecture/audit-ledger/Diten.CrmService.md` (`## İzler` yeni satır), `tests/architecture/audit-ledger/CT-DECISIONS.md`
  satırı ve `AuditTrailStandardTests` — **yalnız O-6 Control Tower kararından sonra**, DL-1 değişikliğinde.
- `frontend/Diten.Web/Controllers/CRM/{TagTaxonomiesController,TagTaxonomyTermsController,TagAssignmentsController}.cs`
  (`CrmGatewayProxy` deseni), `frontend/Diten.Web/Views/CRM/{TagTaxonomies,TagTaxonomyTerms,TagAssignments}/**`,
  `frontend/Diten.Web/wwwroot/assets/js/CRM/{TagTaxonomies,TagTaxonomyTerms,TagAssignments}/**`,
  `frontend/Diten.Web/Resources/Views/CRM/{TagTaxonomies,TagTaxonomyTerms,TagAssignments}/*.{en,tr,fr,es,zh,ar,ru}.resx`,
  `SharedResource.{7 dil}.resx` içinde yalnız `Nav.Page.TAG*` ve `Help.TAG*` anahtarları.
- Menü girdisi: DL-4 başında ölçülen CRM yolu (domain-config'in izin verdiği izinle korunan `<li>` ya da manifest) — "— DL-4 için" maddesi.
- `execution/registries/module-implementation-status.md` satırı (ilk kodla).
- Gateway rotaları: **integration-agent task'ı** (bu paket yazmaz).

## Protected Paths

- `.antigravity/**` (RCS-001 §3 satırı ya da PKS-001 §4 satırı gerekirse yalnız sahip onayıyla, ayrı değişiklik).
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/**` ve `Domain/Entities/{Subject,Topic,AudienceProfile,KnowledgeContent}.cs`
  — MOD-0162 SoR; dokunulmaz (O-4).
- Diğer CRM özellikleri (`Features/**` içinde Tag* dışı) ve `Diten.CrmService.md` defterinin `## Bilinen borç` listesi
  (yalnız küçülür; bu paket satır eklemez).
- Diğer domain servisleri: `services/Diten.Platform/**` (kaynak yerleşimi buydu; Recovery'de Platform'a dokunulmaz,
  `AuditCategory` değişmez), `services/Diten.AuthService/**`, `services/Diten.MdmService/**` ve diğerleri.
- `services/Diten.Building.Blocks/**` — CentralAudit yalnız tüketilir, değiştirilmez.
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN), `Controllers/Archive/**`, `Views/Archive/**`;
  `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` yalnız domain-config'in izin verdiği izinle korunan `<li>`.
- `execution/domains/commercial-suite/domain-config.md`, `execution/registries/module-id-registry.md`,
  `execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md` — paket onay PR'ına (domain-config ve
  kimlik kaydı satırı orada önerilir, D61) ve DCP kararına (O-10) kadar.
- Kaynak depo `/Users/natig/Projects/ERP-vNext` ve devir klasörü `/Users/natig/Projects/treasury-transfer-handoff/**` — salt-okunur.

## Dependencies

| Bağımlılık | Durum (ölçüldü @40a1bcc91) | Ne için | Engel mi |
|---|---|---|---|
| `Diten.CrmService` | var (`services/Diten.CrmService/src/` beş katman); port 5061 (AGENTS.md §3) | barındıran servis (O-2) | hayır |
| CentralAudit ortak iletici | var: `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit{,.Abstractions}/`; ilk kabul edilmiş kullanıcı SupplyChain (`tests/architecture/audit-ledger/CT-DECISIONS.md:22`) | yol `b` (O-6) | CrmService'te kaydı yok → O-6 CT kararı paket kapısı; kurulum DL-1 |
| CrmService denetim defteri | `tests/architecture/audit-ledger/Diten.CrmService.md:9-15`: beş iz, hepsi `aday`; kabul edilmiş iz yok | — | evet — yeni iz satırı CT kararı ister (O-6) |
| Mongo işlem (replica set) | CrmService'te işlemli birim iş sınıfları var (ör. `Persistence/Repositories/ConceptNodeWithRelationshipUnitOfWork.cs`) | bağ + sayaç + denetim olayı tek işlemde | hayır |
| MOD-0018 Auth | `crm.tag-*` anahtarları katalogda yok | izin kaydı | paket metni paket kapısı; kayıt kodu DL-1 kabul kapısı (D53) |
| İzin denetleyicisi | `services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Authorization/PermissionAuthorizationHandler.cs:20-31`: tam eşleşme, joker yok, `platform_admin` geçişi yok | `[HasPermission]` | hayır |
| MOD-0053 Data Dictionary / Glossary | Blueprint 8.8'de MOD-0057 → MOD-0053 **HARD** (MOD-0053 → MOD-0052 HARD; `MOD-0053-UZLASTIRMA-01.md:105-106`); Recovery'de paket, kayıt satırı ve kod yok; kaynakta da tüketilmiyor (paket §D) | iş terimi ↔ etiket bağı, eşanlamlı ve terim onayı | **hayır (v1)** — D55/3: v1 bağımsız; Blueprint bağımlılığı O-12 ile açık karar, bağlantı Follow-up |
| MOD-0056 Enterprise Search | Blueprint kapısı; Recovery'de yok, kaynakta da tüketilmiyor (paket §D) | arama / bilgi grafiği bağlantısı | **hayır** — D55/3: v1 bağımsız; bağlantı sonraki sürüm |
| MOD-0162-FU01 / FU02 | `approved`; Subject / Topic kodda canlı | yalnız sınır | hayır (O-4 karar) |
| Gateway rotaları | `/api/crm/tag-*` yok; catch-all `/api/crm/{everything}` yok | DL-3 | DL-3 için |
| RCS-001 ortak kod servisi (BL-565) | yok | yalnız O-3 (b) seçilirse | O-3'e bağlı |

## Runtime Constraints

- **Kiracı:** her sorgu ve yazma `TenantId` ile sınırlı (depo süzgeci); `TenantId` yalnız `ITenantContext`'ten; gövde, DTO
  ya da sorgu parametresinde kabul edilmez (gövdede `tenantId` → 400). Başka kiracının kaydı 404. Üretim kodunda sabit ya
  da varsayılan kiracı (`Guid.Empty` dahil) yok.
- **Aktör:** yalnız JWT `sub` (Guid). Yoksa ya da Guid değilse yazma 401, okuma 401; sabit değere (`"unknown"`, `Guid.Empty`)
  düşülmez. Gövdede `actorId` → 400. (Level-1 koşul 2.)
- **İzin:** tam anahtar eşleşmesi; `*` joker talebi hiçbir şey vermez; geçici "fallback" anahtar (ör. MOD-0162'nin
  `crm.territory.read` geri dönüşü, `Features/Knowledge/KnowledgePermissions.cs:16-18`) bu modülde **kullanılmaz** (D53/2).
- **Soft delete:** silme ve kaldırma yalnız `IsDeleted = true` + `DeletedAt` (UTC) + `DeletedBy`; filtrede `Id` + `TenantId`.
  Hard delete ucu yok. Silinmiş kayıt listelerde ve ayrıntıda görünmez (404).
- **Eşzamanlılık:** taksonomi ve terim güncellemeleri `Version` CAS (`MatchedCount == 0` → 409); istemci `version` gönderir.
  Ağaç yapısını değiştiren terim komutları buna ek olarak taksonomi düzeyindeki ağaç çitini alır (aşağıda "Ağaç çiti").
- **İşlem bütünlüğü:** her yazma komutu iş verisini, gerekiyorsa `TagTargetSlot` sayacını ve merkezi denetim olayını
  (`CentralAuditRecorder.EnlistAsync`) **tek Mongo işleminde** yazar; biri başarısızsa hiçbiri kalmaz. Taksonomi silme
  terimleri aynı işlemde soft siler.
- **Hedef başına sınır:** `MaxTermsPerTarget` dolu taksonomide bağ oluşturma `TagTargetSlot` belgesini koşullu artırır
  (`ActiveCount < Max` süzgeciyle `findOneAndUpdate` + `$inc`, yoksa ekle); eşleşme yoksa 409 `CRM_TAG_TARGET_LIMIT_REACHED`.
  Kaldırma aynı işlemde azaltır.
- **Hiyerarşi:** derinlik ≤ 5; taşıma `Path` ve `Depth` değerlerini alt ağaçta aynı işlemde yeniden yazar; alt ağaç
  boyutu taşıma başına ≤ 1000 terim (aşan taşıma 400 — tek işlemde sınırsız yazım yok).
- **Ağaç çiti (taksonomi düzeyinde ortak kontrol):** terim kaydının `Version` CAS'ı yalnız aynı terime yazan iki isteği
  ayırır; A'yı B'nin altına ve B'yi A'nın altına taşıyan iki işlem farklı belgelere yazar, ikisi de taşımadan önceki ağacı
  okur ve ikisi de commit olursa döngü oluşur. Bu yüzden ağaç yapısını değiştiren her terim komutu — terim oluşturma,
  `ParentTermId` değiştiren `UpdateTagTerm` (taşıma), terim silme ve toplu silme — işlemin **ilk okuması** olarak
  taksonominin `HierarchyVersion` değerini okur; ata zinciri, döngü, derinlik ve çocuk denetimleri bu okumadan sonra aynı
  işlemde yapılır; iş yazımıyla aynı işlemde taksonomi belgesine `updateOne({Id, TenantId, IsDeleted: false,
  HierarchyVersion: okunan}, {$inc: {HierarchyVersion: 1}})` uygulanır. `MatchedCount == 0` ya da Mongo yazma çakışması
  (`WriteConflict`, kod 112) → işlem geri alınır, 409 `CRM_TAG_CONCURRENCY_CONFLICT`, hiçbir terim, alt ağaç ya da denetim
  olayı kalmaz. Çitli komutlar sürücü tarafından **otomatik yeniden denenmez** (CrmService deseni açık
  `session.StartTransaction()` / `CommitTransactionAsync` — `Persistence/Repositories/ConceptNodeWithRelationshipUnitOfWork.cs:38-43`);
  istemci yeniden yükler. Çit yalnız aynı taksonomideki ağaç yazmalarını sıraya sokar; aynı taksonomi belgesine yazan
  taksonomi komutları (güncelleme, etkinleştirme, emekliye ayırma) ile eşzamanlı çakışmada da 409 döner. Terim etiketi,
  açıklaması, sırası ya da takma adı değişen (`ParentTermId` değişmeyen) güncelleme ve kullanımdan kaldırma çiti almaz.
- **Sınırlar:** liste sayfası ≥ 1, boyut 1–200 (kaynak validator sınırı, paket §K); toplu silme ve `by-targets` ≤ 100
  kimlik; taksonomi başına ≤ 5000 terim (öneri; aşılırsa 409). İstek boyutu ve hız sınırı servis genelinden gelir.
- **Hata sınıflandırması:** doğrulama 400 · durum / tekillik / eşzamanlılık 409 · izin 403 · kimlik 401 · bulunamadı /
  başka kiracı 404 · denetim iletisi yazılamadı 503 (iş verisi yazılmaz).
- **Veri kapsamı (SEC-002) — kapsam cümlesi:** (1) Taksonomi ve terimler kiracı içinde herkese açık **sözlük verisidir**
  (SEC-002 §3 "Ne zaman uygulanmaz", ikinci madde): `crm.tag-taxonomies.read` sahibi kiracının bütün taksonomilerini görür.
  (2) Bağlar örgütsel kapsam alanı taşımaz (tüzel kişilik, birim ya da pozisyon alanı yok); yönetim bağ listesi
  (`GET /api/crm/tag-assignments` ve `/{id}`) `crm.tag-assignments.read-tenant-wide` olmadan **yalnız kullanıcının kendi
  oluşturduğu** bağları döner, bu izinle kiracının tamamını; aktör çözülemezse liste boş. (3) Hedef okuması (`by-target`,
  `by-targets`) kiracı geneli liste değildir: çağıranın verdiği tek hedefin (ya da ≤ 100 hedefin) etiketlerini döner; hedef
  kaydın kendisini görme yetkisi hedefin sahibi modülde denetlenir. Ters arama yoktur. (O-7.)
- **Olay yok:** v1 entegrasyon olayı yayımlamaz, outbox satırı yazmaz (kaynaktaki okunmayan olay satırları taşınmaz).

## Layout & Shell Contract

- `shell: tenant` → `Views/CRM/TagTaxonomies/`, `Views/CRM/TagTaxonomyTerms/`, `Views/CRM/TagAssignments/` altındaki
  **tüm** `.cshtml` dosyalarında `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (canlı örnek
  `frontend/Diten.Web/Views/CRM/Segments/Index.cshtml:6`); `_ViewStart.cshtml` varsayılanı değiştirilmez. Kaynaktaki
  `Layout = "_LayoutBackbone"` (5 sayfa, paket §I) taşınmaz.
- Rotalar `/CRM/TagTaxonomies`, `/CRM/TagTaxonomyTerms`, `/CRM/TagAssignments` (`[Route("CRM/...")]` deseni —
  `frontend/Diten.Web/Controllers/CRM/SegmentsController.cs:23`). MOD-0162'nin `/CRM/Knowledge/Taxonomy` sayfası
  (`frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs:167-169`) ile ad ve rota ayrıdır; ekran başlıkları
  "Etiket taksonomileri" / "Etiket terimleri" / "Etiket bağları" olur, "Konu taksonomisi" ile karışmaz.
- Sayfa kodları ve nav anahtarları: `TAGTAXONOMIES` → `Nav.Page.TAGTAXONOMIES`, `TAGTAXONOMYTERMS` →
  `Nav.Page.TAGTAXONOMYTERMS`, `TAGASSIGNMENTS` → `Nav.Page.TAGASSIGNMENTS` (yalnız büyük harf ve rakam).
- Yetkisiz kullanıcı (UAS-001): sayfanın okuma izni yoksa kabuk içinde tek açıklama (`_AccessDenied` deseni); başlık, kart,
  boş tablo, düğme çizilmez; **yönlendirme yok** (kaynaktaki `Account/AccessDenied` yönlendirmesi, paket §G, taşınmaz).
  Oturumsuz istek 401 akışı.

## Backend File Convention

Golden Reference **Slim** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`:
`Commands/`, `Queries/`, `Handlers/{CommandHandlers,QueryHandlers}/`, `Validators/`, tek `…Models.cs` — bu oturumda dosya
listesi okundu). Üç özellik klasörü:

```text
Features/TagTaxonomies/
  Commands/   CreateTagTaxonomyCommand.cs, UpdateTagTaxonomyCommand.cs, DeleteTagTaxonomyCommand.cs,
              BulkDeleteTagTaxonomyCommand.cs, ActivateTagTaxonomyCommand.cs, RetireTagTaxonomyCommand.cs
  Queries/    GetTagTaxonomyListQuery.cs, GetTagTaxonomyByIdQuery.cs
  Handlers/CommandHandlers/   CreateTagTaxonomyHandler.cs … RetireTagTaxonomyHandler.cs
  Handlers/QueryHandlers/     GetTagTaxonomyListHandler.cs, GetTagTaxonomyByIdHandler.cs
  Validators/ CreateTagTaxonomyValidator.cs, UpdateTagTaxonomyValidator.cs
  TagTaxonomyModels.cs
Features/TagTerms/
  Commands/   CreateTagTermCommand.cs, UpdateTagTermCommand.cs, DeleteTagTermCommand.cs, BulkDeleteTagTermCommand.cs,
              DeprecateTagTermCommand.cs
  Queries/    GetTagTermListQuery.cs, GetTagTermByIdQuery.cs, GetTagTermTreeQuery.cs
  Handlers/CommandHandlers/ · Handlers/QueryHandlers/
  Validators/ CreateTagTermValidator.cs, UpdateTagTermValidator.cs, DeprecateTagTermValidator.cs
  Rules/      TagTermHierarchy.cs (döngü, derinlik, yol üretimi — saf işlev)
  TagTermModels.cs
Features/TagAssignments/
  Commands/   CreateTagAssignmentCommand.cs, DeleteTagAssignmentCommand.cs, BulkDeleteTagAssignmentCommand.cs
  Queries/    GetTagAssignmentListQuery.cs, GetTagAssignmentByIdQuery.cs, GetTagAssignmentsByTargetQuery.cs,
              GetTagAssignmentsByTargetsQuery.cs
  Handlers/CommandHandlers/ · Handlers/QueryHandlers/
  Validators/ CreateTagAssignmentValidator.cs, GetTagAssignmentsByTargetsValidator.cs
  Rules/      TagTargetReference.cs (hedef biçimi ve TargetKey — saf işlev)
  TagAssignmentModels.cs
```

- Command / Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` — **Command / Query suffix YOK**;
  Validator `{Verb}{Module}Validator`. Tek dosyada tek public tip.
- Controller `CustomBaseController`'dan türer, `Response<T>` döner, her aksiyonda `[HasPermission("…")]` (CRM deseni
  `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/KnowledgeConceptNodesController.cs:20-41`).
- Her yazma komutu `ICentralAuditDeclaration` taşır (O-6); handler iş yazımını ve `EnlistAsync` çağrısını aynı işlemde yapar.
- İş kuralı handler + `Rules/` içinde (`handler-design`); kaynaktaki toplu servisler (`TaxonomyMutableCommandService`,
  `TaxonomyGovernanceService`, `TaxonomyAssignmentService`) alınmaz. Kaynak önekleri (`Mod0057*`), koleksiyon adları
  (`mod0057_*`) ve hata literalleri kullanılmaz.

## Frontend File Contract

Golden Reference **Slim** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/` — dosya listesi bu oturumda
okundu), üç ekranın her biri için:

```text
Views/CRM/{TagTaxonomies|TagTaxonomyTerms|TagAssignments}/
├── Index.cshtml                 (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable → ④ Offcanvas)
├── _Filter.cshtml
├── _DataTable.cshtml            (data-dt-standard="v2"; data-dt-data-mode künyedeki screens kararıyla aynı)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml
├── _DetailsQuickView.cshtml
└── {Module}Index.cs             (TagTaxonomiesIndex · TagTaxonomyTermsIndex · TagAssignmentsIndex)
wwwroot/assets/js/CRM/{Module}/  index.js · index.l10n.js
Resources/Views/CRM/{Module}/    {Module}Index.{en,tr,fr,es,zh,ar,ru}.resx
```

- **Form alanları (slim, ≤ 8):** Taksonomi — Ad, Açıklama, Tür, İzinli hedef türleri, Hedef başına en çok terim (5;
  O-3 (b) seçilirse kod yok, değişmez). Terim — Kod (yalnız oluştururken), Etiket, Ebeveyn (yalnız hiyerarşik),
  Açıklama, Sıra, Takma adlar (6). Bağ — Taksonomi, Terim, Hedef modül, Hedef türü, Hedef kimliği, Not (6).
  `form_field_count: 6` en büyük formdur.
- **Sütunlar:** Taksonomiler — Ad, Tür, Durum, Terim sayısı, İzinli hedefler, Güncellenme, İşlemler. Terimler — Kod,
  Etiket (yol girintisiyle), Durum, Yerine geçen, Sıra, İşlemler. Bağlar — Taksonomi, Terim, Hedef modül, Hedef türü,
  Hedef kimliği, Oluşturan, Tarih, İşlemler.
- **Veri modu:** taksonomiler `client` (tasarımda sınırlı küme, `data_mode_max_rows: 200`; servis `total` döner, ekran
  `total > gelen` durumunu gösterir; JS'te sabit `pageSize=` yok); terimler ve bağlar `server`.
- Detay eylem düğmeleri (etkinleştir, emekliye ayır, kullanımdan kaldır) yalnız sunucunun döndürdüğü `allowedActions`
  listesindekilerdir; istemci durum ya da izin hesaplamaz. Onaylar Premium SweetAlert2 (MOD-0013).
- API profili: same-origin MVC vekili (`frontend/Diten.Web/Controllers/CRM/CrmGatewayProxy.cs:16-29` deseni) — tarayıcı
  5061'i ve token'ı görmez; vekil kiracı çözülemezse isteği oluşturmaz.
- Tür, durum ve hata etiketleri resx'ten; hardcoded metin ve `|| 'Text'` geri dönüşü yok. Kaynaktaki özdeş İngilizce
  7 resx (paket §I) ve hardcoded `Rules.cshtml` taşınmaz.

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| Gövde / sorguda `tenantId`, `actorId`, `createdBy` | — | bulunursa 400 `CRM_TAG_FORBIDDEN_FIELD` | — | — |
| TagTaxonomy.Name | evet | trim, 1–200 | `ux_crm_tag_taxonomy_name` | normalize ad var mı |
| TagTaxonomy.Description | hayır | ≤ 1000 | — | — |
| TagTaxonomy.Kind | evet | `Hierarchical` / `Flat`; `Active` ya da `Retired` iken değişiklik 409 `CRM_TAG_TAXONOMY_STATE` | — | — |
| TagTaxonomy.AllowedTargetTypes | evet | 1–20 öğe, tekrarsız; `TargetModule` `^MOD-\d{4}(-FU\d{2}[A-Z]?)?$`, `TargetType` `^[a-z][a-z0-9-]{0,63}$`; `Active` iken öğe **eklenebilir**, çıkarılamaz (409) | — | — |
| TagTaxonomy.MaxTermsPerTarget | hayır | boş ya da 1–50; `Active` iken yalnız artırılabilir (409) | — | — |
| TagTaxonomy.Version | güncellemede evet | ≥ 0 | CAS | — |
| Activate | — | yalnız `Draft`; en az bir `Active` terim (yoksa 409) | — | terim sayısı |
| Retire | — | yalnız `Active` | — | — |
| Delete / BulkDelete (taksonomi) | — | yalnız `Draft` (aksi 409 `CRM_TAG_TAXONOMY_NOT_DRAFT`); toplu ≤ 100 kimlik, her kimlik ayrı değerlendirilir ve sonuç kimlik başına döner | — | — |
| TagTerm.TermCode | oluştururken evet | `^[a-z0-9][a-z0-9-]{0,63}$`; güncellemede farklı değer 400 `CRM_TAG_TERM_CODE_IMMUTABLE` | `ux_crm_tag_term_code` (kısmi değil) | kod var mı (silinmiş dahil) |
| TagTerm.Label | evet | trim, 1–200; kardeşler arasında normalize tekil | `ux_crm_tag_term_sibling_label` | — |
| TagTerm.ParentTermId | hayır | `Flat` taksonomide dolu → 400 `CRM_TAG_FLAT_NO_PARENT`; aynı taksonominin silinmemiş terimi (aksi 400); kendisi ya da alt ağacı → 400 `CRM_TAG_TERM_CYCLE`; sonuç derinlik > 5 → 400 `CRM_TAG_TERM_DEPTH_EXCEEDED`; eşzamanlı ağaç yazmasıyla çakışma → 409 `CRM_TAG_CONCURRENCY_CONFLICT` (ağaç çiti) | `HierarchyVersion` koşullu `$inc` | `HierarchyVersion` okunduktan sonra ata zinciri aynı işlemde okunur |
| TagTerm.SortOrder | hayır | 0–9999 | — | — |
| TagTerm.Aliases | hayır | ≤ 10, her biri trim 1–200, normalize tekrarsız | — | — |
| Retired taksonomide terim yazımı | — | 409 `CRM_TAG_TAXONOMY_STATE` | — | — |
| Deprecate | — | yalnız `Active` terim; `ReplacementTermId` boş ya da aynı taksonominin başka `Active` terimi (aksi 400) | — | — |
| Delete / BulkDelete (terim) | — | `HasEverBeenAssigned = false` ve silinmemiş çocuk yok (aksi 409 `CRM_TAG_TERM_IN_USE`); toplu ≤ 100 | — | çocuk sayısı |
| TagAssignment.TaxonomyId / TermId | evet | taksonomi `Active`, terim `Active` ve o taksonominin (aksi 409 `CRM_TAG_TERM_NOT_ASSIGNABLE`) | — | — |
| TagAssignment.TargetModule / TargetType | evet | biçim yukarıdaki gibi; çift taksonominin `AllowedTargetTypes` listesinde (aksi 400 `CRM_TAG_TARGET_TYPE_NOT_ALLOWED`) | — | — |
| TagAssignment.TargetId | evet | 1–128, ASCII yazdırılabilir, boşluksuz; varlığı v1'de doğrulanmaz (opak; O-8) | — | — |
| TagAssignment tekilliği | — | aynı terim + aynı hedef etkin bağ → 409 `CRM_TAG_ASSIGNMENT_DUPLICATE` | `ux_crm_tag_assignment_active` | — |
| MaxTermsPerTarget | — | sınır doluysa 409 `CRM_TAG_TARGET_LIMIT_REACHED` | `ux_crm_tag_target_slot` + koşullu `$inc` | — |
| TagAssignment.Note | hayır | ≤ 500 | — | — |
| by-targets | evet | 1–100 hedef; her hedef biçim kuralına uyar (aksi 400) | — | — |
| Liste sayfalama | — | sayfa ≥ 1, boyut 1–200 | — | — |

## Failure Path to Verify

- **Mükerrer taksonomi adı** — Beklenen: 409 `CRM_TAG_TAXONOMY_NAME_DUPLICATE` + formda alan hatası + kayıt oluşmaz + denetim
  olayı yok; eşzamanlı iki istekte tek kayıt.
- **Mükerrer terim kodu (silinmiş terimin kodu dahil)** — Beklenen: 409 `CRM_TAG_TERM_CODE_DUPLICATE`; kayıt oluşmaz.
- **Mükerrer bağ** — Beklenen: 409 `CRM_TAG_ASSIGNMENT_DUPLICATE`; eşzamanlı 20 istekte tek bağ, tek denetim olayı.
- **Eksik ad / etiket / hedef** — Beklenen: 400 + doğrulayıcı mesajı (resx) + kaydetme engellenir.
- **Döngü ya da derinlik aşımı** — Beklenen: 400 `CRM_TAG_TERM_CYCLE` / `CRM_TAG_TERM_DEPTH_EXCEEDED`; ağaç değişmez.
- **Eşzamanlı karşılıklı taşıma (A → B altı ve B → A altı)** — Beklenen: biri 200, diğeri 409 `CRM_TAG_CONCURRENCY_CONFLICT`
  (ağaç çiti); ağaçta döngü yok, kaybeden isteğin hiçbir yazımı ve denetim olayı kalmaz; ekranda "veri değişti, yeniden
  yükleyin" (AC-42).
- **Hedef sınırı dolu** — Beklenen: 409 `CRM_TAG_TARGET_LIMIT_REACHED`; eşzamanlı isteklerde sınır aşılmaz.
- **Eşzamanlılık çakışması** — Beklenen: 409 `CRM_TAG_CONCURRENCY_CONFLICT` + ekranda "veri değişti, yeniden yükleyin";
  sessiz üzerine yazma yok.
- **Yetkisiz aktör** — Beklenen: 403; ekranda eylem düğmesi yok; okuma izni yoksa UAS-001 tek açıklama, yönlendirme yok.
- **Yalnız `*` izni taşıyan token** — Beklenen: her uçta 403 (joker taşınmaz).
- **`sub` talebi yok ya da Guid değil** — Beklenen: 401; hiçbir şey yazılmaz; `"unknown"` aktörlü kayıt oluşmaz.
- **Başka kiracının kimliği** — Beklenen: 404 (403 değil); başka kiracının verisi hiçbir yanıtta görünmez.
- **Silinmiş kayıt** — Beklenen: ayrıntı 404, listede yok; silinmiş taksonomiye terim / bağ eklenemez (404).
- **Denetim olayı yazılamadı (işlem içi hata enjeksiyonu)** — Beklenen: 503 `CRM_TAG_AUDIT_UNAVAILABLE`; iş verisi ve
  sayaç değişmez.
- **Kullanılmış terimi silme** — Beklenen: 409 `CRM_TAG_TERM_IN_USE`; ekranda "kullanımdan kaldırın" önerisi.
- **Emekliye ayrılmış taksonomiye bağ** — Beklenen: 409 `CRM_TAG_TERM_NOT_ASSIGNABLE`; mevcut bağlar okunur kalır.

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]`. Aktör türü `tenant_user`; arka plan aktörü yok.
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça; ad alanı **önerisi** `crm.*` (O-5; PKS-001 §4 `crm.*`'ı gelecek iş
  alanı olarak ayırır, domain-config `crm.*` ad alanını CrmService'e bağlar — `execution/domains/commercial-suite/domain-config.md:71-74`);
  son parça Tier 1 / Tier 2 sözlüğünden ya da bu pakette kayıtlı Tier 3 eylemi. `git grep "crm.tag-"` → 0 (ölçüldü):
  uydurulmuş "var olan" anahtar yok.
- Kaynaktaki `taxonomy.read|manage|assign|approve|publish` (iki parçalı — PKS-001'e aykırı) ve büyük harfli `TAXONOMY_*`
  takma adları (paket §E) **kopyalanmaz**; Recovery'de yayımlanmadıkları için takma ad gerekmez. Kaynakta `taxonomy.manage`'in
  onay ve yayın politikalarını da geçmesi (paket §G) taşınmaz — v1'de onay / yayın yok.
- **Joker yok (level-1 koşul 2):** kaynakta her taksonomi politikası `HasPermission(ctx.User, "*")` dalı taşıyordu
  (`services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs:110-219@e4c93c29`, bu oturumda
  `git grep` ile ölçüldü). Recovery CrmService denetleyicisi tam eşleşmedir, joker dalı yoktur ve bu modül eklemez;
  mimari test ve AC-14 korur.

| Anahtar (öneri, O-5) | Katman | Uçlar |
|---|---|---|
| `crm.tag-taxonomies.read` | T1 | taksonomi listesi / ayrıntı, terim listesi / ayrıntı / ağaç |
| `crm.tag-taxonomies.create` | T1 | CreateTagTaxonomy |
| `crm.tag-taxonomies.update` | T1 | UpdateTagTaxonomy |
| `crm.tag-taxonomies.delete` / `crm.tag-taxonomies.bulk-delete` | T1 / T2 | taslak taksonomi silme |
| `crm.tag-taxonomies.activate` | T2 | ActivateTagTaxonomy |
| `crm.tag-taxonomies.retire` | T3 (bu pakette kayıtlı; PKS-001 §5 örnek listesinde var) | RetireTagTaxonomy |
| `crm.tag-taxonomies.terms.create` / `.update` / `.delete` | T1 (alt kaynak `terms`) | terim yazmaları (taşıma `update` içinde) |
| `crm.tag-taxonomies.terms.bulk-delete` | T2 | toplu terim silme |
| `crm.tag-taxonomies.terms.deprecate` | T3 (PKS-001 §5 örnek listesinde var) | DeprecateTagTerm |
| `crm.tag-assignments.read` | T1 | hedef okuması (`by-target`, `by-targets`), kendi bağ listesi |
| `crm.tag-assignments.read-tenant-wide` | T3 (yalnız açık atama; SEC-002 §3.5 kalıbı) | kiracı geneli bağ listesi — **yalnız okuma** |
| `crm.tag-assignments.create` | T1 | CreateTagAssignment |
| `crm.tag-assignments.delete` / `crm.tag-assignments.bulk-delete` | T1 / T2 | bağ kaldırma |

- **Rol şablonları (öneri, O-5):** *Etiket okuyucu* (`crm.tag-taxonomies.read`, `crm.tag-assignments.read`); *Etiketleyici*
  (okuyucu + `crm.tag-assignments.create` / `.delete` / `.bulk-delete`); *Taksonomi yöneticisi* (bütün `crm.tag-taxonomies.*`
  + okuyucu). `read-tenant-wide` hiçbir şablonda varsayılan değildir. Auth'ta kayıt MOD-0018 sahibinin işidir (D9, D53).
- Taksonomi yazma izni bağ yazma izni vermez; bağ izni taksonomi değiştirmez (iki ayrı rol).

## Audited Events

**Yol `b` — `Diten.BuildingBlocks.CentralAudit` ortak ileticisi (AUD-001 §5 K4, BL-501).** Komut `ICentralAuditDeclaration`
taşır; servis `AddCentralAudit<CrmCentralAuditScope>(…)` ile kaydeder; MOD-0057 handler'ları olayı iş verisiyle **aynı
Mongo işleminde** `CentralAuditRecorder.EnlistAsync` ile yazar — iş verisi ve merkezi kayıt birlikte commit olur ya da
hiçbiri. `RequireEnlistment` servis düzeyinde `false` kalır (CrmService'in diğer komutları işlemsizdir ve borçtadır);
MOD-0057 komutlarının her birinin `EnlistAsync` çağırdığını mimari test ölçer (AC-11). **Servisin denetim altyapısı bugün
yok:** `tests/architecture/audit-ledger/Diten.CrmService.md:9-15` beş iz taşır, hepsi `aday` (kabul edilmemiş);
yeni iz satırı `crm-merkezi-iletim | b | işaret | ICentralAuditDeclaration` Control Tower kararı ister (O-6; D35 ve
SupplyChain E-2 deseni, `CT-DECISIONS.md:22`) — bu paket deftere satır yazıldığını iddia etmez. Defter dosyası,
`CT-DECISIONS.md` satırı ve `AuditTrailStandardTests` kayıtları DL-1 değişikliğinde birlikte yazılır; `## Bilinen borç`
listesine satır eklenmez.

`Category`: **`ReferenceData`** (Platform'da var, değer 5 — `services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:10`;
CentralAudit kabul listesinde — `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs:17`);
yeni Platform değeri istenmez (O-6). `SourceModule`: `MOD-0057`. `Operation` CentralAudit listesinden. **K2 sınıfı komut
yok:** etiketleme kimlik, yetki, kiracı durumu, GxP kaydı ya da KVKK özel nitelikli veri değiştirmez; yine de bütün komutlar
işlem içi yol sayesinde kapalı-başarısızdır (olay yazılamazsa iş verisi yazılmaz, 503).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateTagTaxonomyCommand | crm.tag-taxonomy.created (Create) | TagTaxonomy | b | AfterState: Name, Kind, AllowedTargetTypes, MaxTermsPerTarget, Status | — |
| UpdateTagTaxonomyCommand | crm.tag-taxonomy.updated (Update) | TagTaxonomy | b | değişen alan **adları**; Kind / AllowedTargetTypes / MaxTermsPerTarget için önce → sonra | — |
| DeleteTagTaxonomyCommand | crm.tag-taxonomy.deleted (Delete) | TagTaxonomy | b | BeforeState: Name, Status; birlikte soft silinen terim sayısı | — |
| BulkDeleteTagTaxonomyCommand | crm.tag-taxonomy.deleted (kimlik başına bir olay) | TagTaxonomy | b | aynı | — |
| ActivateTagTaxonomyCommand | crm.tag-taxonomy.activated (Activate) | TagTaxonomy | b | Status Draft → Active | — |
| RetireTagTaxonomyCommand | crm.tag-taxonomy.retired (LifecycleTransition) | TagTaxonomy | b | Status Active → Retired; etkin bağ sayısı | — |
| CreateTagTermCommand | crm.tag-term.created (Create) | TagTerm | b | AfterState: TaxonomyId, TermCode, Label, ParentTermId, Path | — |
| UpdateTagTermCommand | crm.tag-term.updated (Update) | TagTerm | b | değişen alan **adları**; taşımada ParentTermId ve Path önce → sonra, yeniden yazılan alt ağaç terim sayısı | — |
| DeleteTagTermCommand | crm.tag-term.deleted (Delete) | TagTerm | b | BeforeState: TermCode, Label | — |
| BulkDeleteTagTermCommand | crm.tag-term.deleted (kimlik başına bir olay) | TagTerm | b | aynı | — |
| DeprecateTagTermCommand | crm.tag-term.deprecated (Deactivate) | TagTerm | b | Status Active → Deprecated, ReplacementTermId | — |
| CreateTagAssignmentCommand | crm.tag-assignment.created (Assign) | TagAssignment | b | TaxonomyId, TermId, TargetModule, TargetType, TargetId; sayaç önce → sonra (sınırlı taksonomide) | — |
| DeleteTagAssignmentCommand | crm.tag-assignment.removed (Revoke) | TagAssignment | b | BeforeState: TermId, TargetKey | — |
| BulkDeleteTagAssignmentCommand | crm.tag-assignment.removed (kimlik başına bir olay) | TagAssignment | b | aynı | — |

- **Kayda yazılmayanlar:** açıklama metinleri, takma ad listesi, bağ notu (yalnız var / yok), istek gövdesi (CentralAudit
  gövdeyi kopyalamaz — `ICentralAuditDeclaration.cs:25-30`).
- **Kişisel veri uyarısı:** terim etiketleri kiracı verisidir; kiracının bir etiketle özel nitelikli kişisel veri (sağlık vb.)
  ifade etmesi bu modülün beklediği kullanım değildir. Sayfa yardımı bunu söyler; teknik engel v1'de yok (Follow-up).
- **Okumalar denetlenmez:** sözlük ve etiket okumaları AUD-001 §7'nin okuma sınıflarına girmez.
- **Bilinen boşluk:** reddedilen komut merkezi kayıt bırakmaz (MOD-0217 / MOD-0289 ile aynı sınıf); merkezi ret kaydı Follow-up.
- **Arka plan işi:** yok.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/crm/{everything}` catch-all yok (`grep` → 0); CRM
  rotaları kaynak başına açık yazılmış (ör. `/api/crm/knowledge` ve `/api/crm/knowledge/{everything}` → `localhost:5061`,
  `gateway/Diten.ApiGateway/ocelot.json:3719-3752`). Gerekli rotalar: `/api/crm/tag-taxonomies`,
  `/api/crm/tag-taxonomies/{everything}`, `/api/crm/tag-assignments`, `/api/crm/tag-assignments/{everything}` →
  `localhost:5061`; `UpstreamHttpMethod` GET, POST, PUT, DELETE, OPTIONS; açık rotalar catch-all'dan önce (`routes.md`).
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür (DL-3 önkoşulu).
- **Taşınmaz:** kaynaktaki beş rota ailesi `/api/v1/taxonomies`, `/tag-assignments`, `/objects/{everything}`,
  `/tag-suggestions`, `/taxonomy-rules` → 5057 (`ocelot.json:1390-1538@e4c93c29`, paket §A/§C) ve `servers: /api/v1` ile
  çift önekli OpenAPI sözleşmesi (paket §B).
- Frontend Gateway'e yalnız MVC vekili üzerinden gider; tarayıcı 5061'e gitmez.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) izlenir; bu paketteki her madde yalnız "tanımlı"dır. "Gerçek replica set" = işlemli, suite'in kendi Mongo URI'siyle açılan küme (Test Expectations).

- [ ] AC-1 (zorunlu) — `POST /api/crm/tag-taxonomies` geçerli istekle 201 döner; kayıt `Draft`; `TenantId` sunucuda `ITenantContext`'ten yazılır ve yanıt DTO'sunda yoktur; gövdede `tenantId`, `actorId` ya da `createdBy` varsa 400 `CRM_TAG_FORBIDDEN_FIELD` ve kayıt oluşmaz.
- [ ] AC-2 (zorunlu) — Aynı kiracıda aynı normalize adla ikinci taksonomi 409 `CRM_TAG_TAXONOMY_NAME_DUPLICATE`; gerçek replica set'te aynı adla 20 eşzamanlı oluşturma tek kayıt ve tek denetim olayı bırakır; `ux_crm_tag_taxonomy_name` kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-3 (zorunlu) — Aynı taksonomide aynı `TermCode` ile ikinci terim (silinmiş terimin kodu dahil) 409 `CRM_TAG_TERM_CODE_DUPLICATE`; gerçek replica set'te eşzamanlı iki oluşturma tek terim bırakır; `ux_crm_tag_term_code` kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-4 (zorunlu) — Başka kiracının taksonomisi ve terimi için okuma, güncelleme, silme, toplu silme, etkinleştirme, emekliye ayırma, terim ekleme, taşıma ve kullanımdan kaldırma 404 döner; depo `TenantId` süzgeci kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-5 (zorunlu) — Yalnız `Draft` taksonomi silinir: taksonomi ve terimleri aynı işlemde `IsDeleted = true`, `DeletedAt` (UTC) ve `DeletedBy` alır; silinen kayıt listede yoktur, ayrıntısı 404; `Active` ya da `Retired` taksonomide silme 409 `CRM_TAG_TAXONOMY_NOT_DRAFT`; hard delete ucu yoktur.
- [ ] AC-6 (zorunlu) — Terim silme yalnız hiç bağlanmamış ve silinmemiş çocuğu olmayan terimde yapılır; aksi 409 `CRM_TAG_TERM_IN_USE`; ilk bağ `HasEverBeenAssigned` değerini bağla aynı işlemde `true` yapar.
- [ ] AC-7 (zorunlu) — Eski `Version` ile taksonomi ya da terim güncellemesi 409 `CRM_TAG_CONCURRENCY_CONFLICT` döner ve kayıt değişmez; CAS koşulu kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-8 (zorunlu) — Terimi kendi alt ağacına taşımak 400 `CRM_TAG_TERM_CYCLE`; derinliği 5'i aşan ekleme ya da taşıma 400 `CRM_TAG_TERM_DEPTH_EXCEEDED`; başka taksonominin terimi ebeveyn olamaz (400); `Flat` taksonomide ebeveyn 400 `CRM_TAG_FLAT_NO_PARENT`; başarılı taşıma alt ağacın `Path` ve `Depth` değerlerini aynı işlemde yeniden yazar.
- [ ] AC-9 (zorunlu) — Kullanımdaki terimi olmayan taksonomi etkinleştirilemez (409); `Active` taksonomide `Kind` değiştirilemez ve izinli hedef türü çıkarılamaz (409); `Retired` taksonomi yeniden etkinleştirilemez ve terim yazmaları reddedilir (409 `CRM_TAG_TAXONOMY_STATE`).
- [ ] AC-10 (zorunlu) — Terim kodu oluşturulduktan sonra değişmez (farklı kod 400 `CRM_TAG_TERM_CODE_IMMUTABLE`); kullanımdan kaldırmada `ReplacementTermId` aynı taksonominin başka `Active` terimi olmalıdır, aksi 400.
- [ ] AC-11 (zorunlu) — Audited Events tablosundaki her yazma komutu iş verisiyle aynı Mongo işleminde tam bir merkezi denetim olayı yazar (toplu komutlarda kimlik başına bir); işlem içi hata enjeksiyonuyla olay yazılamazsa 503 `CRM_TAG_AUDIT_UNAVAILABLE` döner ve iş verisi ile sayaç değişmez; bir handler'dan `EnlistAsync` çağrısı kaldırıldığında mimari test ve entegrasyon testi kırmızıya döner (sabotaj).
- [ ] AC-12 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard` yeşil; `Diten.CrmService.md` defterinde `crm-merkezi-iletim` izi kabul edilmiş ve `## Bilinen borç` listesine satır eklenmemiş; denetim kaydı açıklama, takma ad ve not metni taşımaz.
- [ ] AC-13 (zorunlu) — Her MOD-0057 aksiyonu `[HasPermission]` taşır; ilgili izni olmayan kullanıcı her uçta 403 alır ve yanıtta veri yoktur; `[HasPermission]`'sız MOD-0057 aksiyonu mimari testi kırmızıya çevirir.
- [ ] AC-14 (zorunlu) — Yalnız `permission=*` talebi taşıyan token her MOD-0057 ucunda 403 alır (kaynaktaki joker taşınmaz — level-1 koşul 2); izin denetleyicisine `*` dalı eklendiğinde test kırmızıya döner (sabotaj).
- [ ] AC-15 (zorunlu) — Aktör yalnız JWT `sub`'dan (Guid) çözülür: `sub` yoksa ya da Guid değilse her yazma 401 döner ve hiçbir şey yazılmaz; `"unknown"` ya da başka sabit aktör değerine düşülmez (level-1 koşul 2); `CreatedBy`, `UpdatedBy`, `DeletedBy` ve denetim olayının aktörü `sub` ile aynıdır; aktör geri dönüşü eklendiğinde test kırmızıya döner (sabotaj).
- [ ] AC-16 (zorunlu) — MOD-0057 üretim kodunda sabit ya da varsayılan kiracı yoktur (`Guid.Empty` ve sabit kiracı taraması 0 sonuç); kiracı çözülemezse 400 döner ve hiçbir şey yazılmaz.
- [ ] AC-17 (zorunlu) — Taksonomi listesi `total` döner ve 200 satırı aşmaz; terim listesi sunucu tarafında sayfalıdır (sayfa ≥ 1, boyut 1–200); silinmiş kayıtlar dönmez; ağaç ucu yalnız seçilen taksonominin silinmemiş terimlerini yol sırasıyla döner.
- [ ] AC-18 (zorunlu) — Entegrasyon suite'i kendi Mongo URI değişkenini ister (varsayılan port ya da başka suite'in değişkenine düşmez) ve her test sınıfı benzersiz veritabanı açar; işlem ve yarış AC'leri gerçek replica set'te koşar, bellek içi sahte depo bunları kapatmaz.
- [ ] AC-19 (zorunlu) — DL-1 ve DL-2 testleri Auth'ta kayıtlı gerçek `crm.tag-*` anahtarlarıyla koşar (KARAR D53/2 deseni); başka modülün anahtarına geçici geri dönüş (ör. `crm.territory.read`) kullanılmaz.
- [ ] AC-20 (zorunlu) — `POST /api/crm/tag-assignments` geçerli istekle 201 döner; bağ yalnız `Active` taksonominin `Active` terimine kurulur; `Draft` ya da `Retired` taksonomi ya da kullanımdan kalkmış terim 409 `CRM_TAG_TERM_NOT_ASSIGNABLE`.
- [ ] AC-21 (zorunlu) — Hedef biçimi denetlenir (`TargetModule`, `TargetType`, `TargetId` Validation Rules'taki desenle); hedef türü taksonominin `AllowedTargetTypes` listesinde değilse 400 `CRM_TAG_TARGET_TYPE_NOT_ALLOWED`; yanıt hedef kaydın verisini taşımaz.
- [ ] AC-22 (zorunlu) — Aynı terim aynı hedefe ikinci kez bağlanamaz (409 `CRM_TAG_ASSIGNMENT_DUPLICATE`); gerçek replica set'te aynı bağ için 20 eşzamanlı istek tek bağ ve tek denetim olayı bırakır; `ux_crm_tag_assignment_active` kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-23 (zorunlu) — `MaxTermsPerTarget = 1` taksonomide aynı hedefe farklı terimlerle 20 eşzamanlı istek tam bir bağ bırakır, diğerleri 409 `CRM_TAG_TARGET_LIMIT_REACHED`; sayacın koşullu artışı kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-24 (zorunlu) — Bağ kaldırma soft delete'tir (`IsDeleted`, `DeletedAt` UTC, `DeletedBy`) ve sayaç aynı işlemde azalır; kaldırılan bağ hedef okumasında dönmez; aynı terim aynı hedefe yeniden bağlanabilir (yeni kayıt); hard delete ucu yoktur.
- [ ] AC-25 (zorunlu) — Başka kiracının bağı, terimi ya da taksonomisiyle bağ oluşturma, okuma ve kaldırma 404 döner; başka kiracının aynı `TargetId` değerindeki bağları hedef okumasında görünmez; depo süzgeci kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-26 (zorunlu) — Veri kapsamı (SEC-002 kanıt testi): aynı izinli iki kullanıcıdan biri yönetim bağ listesinde diğerinin oluşturduğu bağı görmez ve ayrıntısı 404; `crm.tag-assignments.read-tenant-wide` sahibi bütün bağları görür ama bu izin yazma vermez; aktör çözülemezse liste boştur; kapsam daraltması kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-27 (zorunlu) — Emekliye ayrılan taksonominin ve kullanımdan kalkan terimin mevcut bağları hedef okumasında `taxonomyStatus` / `termStatus` alanlarıyla okunur kalır; yeni bağ reddedilir.
- [ ] AC-28 (zorunlu) — `crm.tag-assignments.create` olmadan bağ oluşturma, `.delete` olmadan kaldırma, `.read` olmadan hedef okuması 403 döner; yalnız `crm.tag-taxonomies.*` izinleri bağ yazamaz, yalnız bağ izinleri taksonomi ya da terim yazamaz.
- [ ] AC-29 (zorunlu) — `GET /api/crm/tag-assignments/by-target` hedefin etkin bağlarını okuma sözleşmesi alanlarıyla döner; `POST /api/crm/tag-assignments/by-targets` en çok 100 hedef için hedef başına eşlem döner, 101 hedef 400.
- [ ] AC-30 (zorunlu) — Okuma sözleşmesi `taxonomy-tagging-read.v1` Owned Objects'teki alan listesine birebir uyar; sözleşme testi alan adlarını ve türlerini sabitler; alan kaldırmak ya da türünü değiştirmek testi kırmızıya çevirir (yeni sürüm gerekir).
- [ ] AC-31 (zorunlu) — Gateway (5000) üzerinden dört `/api/crm/tag-*` rotası CrmService'e (5061) ulaşır; kimliksiz istek 401, başka kiracının kaydı 404, OPTIONS ön uçuşu geçer.
- [ ] AC-32 (zorunlu) — Okuma sözleşmesinde ters arama yoktur: bir terimi taşıyan bütün hedefleri döndüren uç ya da sorgu parametresi tanımlanmaz (O-7 / O-8; MOD-0056 ile sonraki sürüm).
- [ ] AC-33 (zorunlu) — `Views/CRM/TagTaxonomies/`, `Views/CRM/TagTaxonomyTerms/` ve `Views/CRM/TagAssignments/` altındaki her `.cshtml` dosyasında `Layout = "_LayoutTenantShell"` açıkça yazılıdır.
- [ ] AC-34 (zorunlu) — Üç ekran `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module {TagTaxonomies|TagTaxonomyTerms|TagAssignments}` ve `quality-gate-datatable` doğrulamasından geçer; `data-dt-standard="v2"`; `data-dt-data-mode` künyedeki `screens` kararıyla aynıdır.
- [ ] AC-35 (zorunlu) — Her ekranda Golden Slim seti vardır (`_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`); ayrı Create / Edit / Details sayfası yoktur.
- [ ] AC-36 (zorunlu) — Üç ekranın `.resx` dosyaları 7 dilde (en, tr, fr, es, zh, ar, ru) aynı anahtar kümesiyle vardır; İngilizce dışı dosyalarda İngilizceyle aynı bırakılmış değer yoktur; görünüm ve JS'te hardcoded kullanıcı metni ve `|| 'Text'` geri dönüşü yoktur.
- [ ] AC-37 (zorunlu) — Okuma izni olmayan kullanıcı her üç ekranda başlık, kart, boş tablo ya da düğme görmez, tek açıklama görür (UAS-001); yönlendirme yoktur; oturumsuz istek 401 akışına girer.
- [ ] AC-38 (zorunlu) — Tarayıcı istekleri yalnız same-origin MVC vekiline gider; tarayıcı ağ kaydında 5061 ya da erişim token'ı görünmez; vekil kiracı çözülemezse isteği oluşturmaz.
- [ ] AC-39 (zorunlu) — `TAGTAXONOMIES`, `TAGTAXONOMYTERMS`, `TAGASSIGNMENTS` için `Help.{KOD}.Purpose`, `Help.{KOD}.Audience`, `Help.{KOD}.Step1…Step3` ve `Step{n}Title` 7 dilde vardır; `python3 scripts/check_pack_nav_keys.py` geçer.
- [ ] AC-40 (zorunlu) — Silme, toplu silme, emekliye ayırma, kullanımdan kaldırma ve bağ kaldırma Premium SweetAlert2 (MOD-0013) onayı ister; eylem düğmeleri yalnız sunucunun `allowedActions` listesindekilerdir.
- [ ] AC-41 (isteğe bağlı) — Terim ekranında etiket sütunu `Path` derinliğine göre girintili gösterilir; ayrı ağaç bileşeni yoktur.
- [ ] AC-42 (zorunlu) — **Ağaç çiti (taksonomi düzeyinde ortak kontrol):** gerçek replica set'te aynı `Hierarchical` taksonominin iki kök terimi A ve B için "A'yı B'nin altına taşı" ve "B'yi A'nın altına taşı" istekleri, test kancasıyla iki işlem de `HierarchyVersion` ve ata zincirini okuduktan sonra ve ikisi de commit etmeden önce buluşturularak eşzamanlı koşulur (en az 50 tur); her turda tam biri 200, diğeri 409 `CRM_TAG_CONCURRENCY_CONFLICT`; tur sonunda ağaçta döngü yoktur (her terimin ebeveyn zinciri en çok 5 adımda köke ulaşır, `Path` ve `Depth` ebeveyn zinciriyle tutarlıdır), `HierarchyVersion` tam bir artmıştır, kaybeden isteğin hiçbir terim yazımı ve denetim olayı kalmamıştır. Aynı çit, derinliği 4 olan bir terimin altına çocuk ekleme ile o terimin ebeveynini bir düzey derinleştiren taşımanın eşzamanlı yarışında derinliği 5'i aşan terim bırakmaz ve bir terimi silme ile altına çocuk ekleme yarışında ebeveyni silinmiş etkin terim bırakmaz. **Sabotaj:** terim kaydının `Version` CAS'ı yerinde bırakılıp yalnız çitin koşullu `$inc` adımı kaldırıldığında karşılıklı taşıma testi kırmızıya döner (iki istek de 200, döngü oluşur); bellek içi sahte depo bu AC'yi kapatmaz (AC-18).

## Test Expectations

- **Birim:** doğrulayıcılar (her alan); `TagTermHierarchy` (döngü, derinlik, yol üretimi, alt ağaç sınırı); `TagTargetReference`
  (desenler, `TargetKey`); durum geçişleri ve `allowedActions`; hata kodu → HTTP eşlemesi.
- **Entegrasyon (gerçek Mongo replica set, işlem):** kiracı izolasyonu (AC-4, AC-25), veri kapsamı (AC-26), soft delete
  (AC-5, AC-24), CAS (AC-7), ağaç çiti ve karşılıklı taşıma yarışı (AC-42), tekil indeks yarışları (AC-2, AC-3, AC-22),
  sayaç yarışı (AC-23), işlem bütünlüğü ve denetim
  olayı hata enjeksiyonu (AC-11), aktör ve joker (AC-14, AC-15).
- **Mongo yalıtımı (AC-18):** suite'in kendi URI değişkeni (ad DL-1'de belirlenir); test sınıfı başına benzersiz veritabanı;
  indeks testleri kendi veritabanında. Bellek içi sahte depo işlem / yarış AC'lerini kapatmaz.
- **Mimari:** `[HasPermission]` her MOD-0057 aksiyonunda (AC-13); her MOD-0057 yazma handler'ı `EnlistAsync` çağırır
  (AC-11); `AuditTrailStandard` (AC-12); `Guid.Empty` / sabit kiracı / `"unknown"` taraması (AC-15, AC-16); izin
  denetleyicisinde joker dalı yok (AC-14); `Features/Knowledge/**` dosyalarına MOD-0057 değişikliği yok (O-4 sınırı).
- **Sözleşme:** `taxonomy-tagging-read.v1` alan / tür anlık görüntüsü (AC-30).
- **Gateway / uçtan uca:** dört rota, 401 / 404 / OPTIONS (AC-31); dev ortamında bir oluşturma, bir güncelleme, bir silme
  komutunun merkezi denetim kaydı AUD-001 §8 sorgusuyla gösterilir.
- **Frontend:** tarayıcı smoke (üç ekran, oluştur / düzenle / sil / bağla / kaldır), UAS-001 canlı denemesi (AC-37),
  DataTable doğrulayıcı (AC-34), RESX 7 dil parite (AC-36), nav anahtar denetimi (AC-39), ağ kaydında port / token yokluğu (AC-38).
- **Build:** `Diten.CrmService` ve `Diten.Web` 0 hata; yerel kapıda `scripts/` Python testleri.
- **Bilinen kırmızılar:** koşu TRX ile alınır ve `scripts/known_failures.py check` ile okunur.

## Page Help (UXH-001)

- Sayfa kodları `TAGTAXONOMIES`, `TAGTAXONOMYTERMS`, `TAGASSIGNMENTS` (`Nav.Page.*` aynı adla).
- `Help.TAGTAXONOMIES.Purpose` (kiracının genel sınıflandırma ve etiket kümeleri; Knowledge "Konu taksonomisi"nin ayrı
  ekranda olduğu yazılır), `Audience` (taksonomi yöneticisi), `Step1…Step3` + `Step{n}Title` (Taksonomi oluştur · Terimleri
  ekle · Etkinleştir).
- `Help.TAGTAXONOMYTERMS.*` (terim ağacı, kod değişmezliği, kullanımdan kaldırma ve yerine geçen terim); `Help.TAGASSIGNMENTS.*`
  (bir kayda etiket bağlama, hedef modül / tür / kimlik, kaldırma; özel nitelikli kişisel veriyi etiketle ifade etmeme uyarısı).
- İlgili sayfalar: `PageHelpCatalog` + `Help.{KOD}.Related.{KOD}` üç sayfa arasında. Kiracı 7 dil. `Immediate` yok.
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Slim okundu (backend + frontend dosya listesi)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode + screens, entity_base, form_field_count, approval_status)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile (suffix yok, CommandHandlers / QueryHandlers ayrı, slim seti)
- [x] Validation her alan için yazılı; durum kuralları
- [x] Failure Path ≥ 4 senaryo (duplicate, missing, unauthorized, concurrency) + kiracılar arası + joker + aktör + 503
- [x] Authorization: izin listesi (öneri) + policy + aktör + rol şablonu önerisi
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (14 komut)
- [x] Veri kapsamı cümlesi (SEC-002 §4) ve kanıt testi (AC-26)
- [x] Sayfa yardımı anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir; kural koruyan AC'lerde sabotaj
- [x] Level-1 koşul 2 (joker, `"unknown"` aktör) pakete yazıldı (Implementation Notes, AC-14, AC-15)

**İki kapı (MOD-0217 / MOD-0289 deseni):** (a) **tasarım / onay kapısı = `ready-for-dev`** — aşağıdaki ENGELLER: kararlar,
onaylar ve yalnız var olan kodun okunarak ölçülmesi; (b) **dilim kabul kapısı** — "Dilim kabul kapısı" bölümünde.
**Tek kural (D51/1):** işaretsiz maddeler **paket düzeyidir** — hepsi kapanmadan `ready-for-dev` olmaz. **"— DL-n için"**
maddeleri paket düzeyinde engel değildir; yalnız o dilimin (ve sonrakilerin) önkoşuludur.

**ENGELLER — paket kapısı:**
- [x] O-1 yerleşim: `commercial-suite` — **KARAR D61** (2026-10-28; K1, commercial-suite domain sahibi atanana kadar; yalnız yerleşim onayı)
- [ ] commercial-suite domain-config In-Scope satırı ve kimlik kaydı satırı main'de (paket onay PR'ında önerilir — D61; bu taslak yazmaz)
- [ ] O-2 servis ve port, O-3 RCS-001, O-4 MOD-0162-FU01 sınırı, O-5 izin ad alanı ve rol şablonları, O-6 denetim yolu ve kategori, O-7 veri kapsamı, O-9 kod alma, O-10 DCP, O-11 etiket modeli, O-12 MOD-0053 Blueprint bağımlılığı sahip kararı
- [ ] O-6 Control Tower kararı: CrmService defterine `crm-merkezi-iletim | b | işaret | ICentralAuditDeclaration` iz satırı (borç ve istisna eklenmez); dosya yazımı DL-1'de
- [ ] Auth paket metni main'de — MOD-0018 paketinde `crm.tag-*` anahtarları, açık atama anahtarı (`read-tenant-wide`) ve rol şablonları (KARAR D53/1 deseni; MOD-0018 sahibi)
- [ ] O-4 kararından sonra MOD-0162 sahibine sınır bilgisi verildi (XMC-001; itiraz hakkı)
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi, açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı; paketi yazmamış onaylayıcı (ilk onay yüksek risk)

**Ayrı açık nokta (yerleşimi yeniden açmaz):** commercial-suite domain sahibi ataması (O-13; TBD). D61 yerleşimi sahip
atanana kadar K1 onayıyla kapattı; atamanın paket kapısını bekletip bekletmediği K1'in kararıdır — bu revizyon onu ENGELLER'e
eklemez, açık nokta olarak taşır.

**Dilim kapıları ("— DL-n için"; paketi `ready-for-dev`'den alıkoymaz, yalnız o dilimi bekletir — D51/1):**
- [ ] Auth kayıt kodu (`crm.tag-*` kaydı) main'de — MOD-0057 onayından sonra, DL-1 kabulünden önce; DL-1 testleri gerçek izinle (KARAR D53/2 deseni) — DL-1 için (kabul kapısı)
- [ ] Platform merkezi günlüğün `SourceModule = MOD-0057` ve `Category = ReferenceData` olaylarını kabul ettiği okunarak ölçüldü (CT-DECISIONS 2026-10-07 "merkezi eşleme" satırı dersi) — DL-1 için
- [ ] Gateway rotaları (integration-agent task'ı) main'de — DL-3 için
- [ ] O-8 ilk tüketici kararı (yoksa sözleşme tüketicisiz yayımlanır; karar gene yazılır) — DL-3 için
- [ ] CRM menü yolu ölçüldü (izinle korunan `<li>` ya da manifest) — DL-4 için

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Producer paketi MOD-0057 v1.0 (TRM-B6-01), `manifest.json` sha256 `fd3e001e…bd6a`; kaynak `R@e4c93c2908b8710008a24caef5c2a4133f1ff3c8`
  (dal `feature/hr/platform-backbone-hr-source-readiness`). Paket kanıt seviyesi E0 (üretici beyanı); level-1 kaydı paketi
  "kanıt tam, kapsamlanabilir" sayar, yazılımın çalıştığını değil.
- Bu oturumda doğrudan okunan kaynak: `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Mod0057/TaxonomyEntities.cs`
  (12 sınıf; `TenantScopedEntity`, `DeletedAt` yok), `services/Diten.Platform/src/Diten.Platform.API/Controllers/TaxonomiesController.cs:596-598`
  (`User.FindFirst("sub")?.Value ?? "unknown"`), `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs:110-219`
  (14 politikada `HasPermission(ctx.User, "*")`). Diğer bütün kaynak atıfları paket ve `CONTRACT-STATE.json`'dan aktarılmıştır.

### Level-1 koşulları (`MOD-0057-v1.0.md`) → bu paket

| # | Koşul | Bu paketteki karşılığı |
|---|---|---|
| 1 | §O taşıma izni: sahip özet kararı `treasury-b6-digest-MOD-0057-20261023` esas alınır | Taşıma izninin kaynağı bu özet karardır; paket §O'daki `DECISION.json` karar 5 / `SCOPE.md` yalnız paket yazımını yetkilendirir. Özet karar Recovery'de kayıtlı bir dosya değil (`git grep` yalnız level-1 kaydını buluyor) — içeriği bu oturumda okunmadı (kısmi). Kod alma izni ayrıca O-9'dadır; özet karar kod alma izni sayılmaz (D55/4). |
| 2 | `*` izin joker'i ve `"unknown"` aktör geri dönüşü §M / §T'ye yazılır | §M: aşağıda "Taşınmayanlar" ilk iki satır; §T: AC-14 ve AC-15 (sabotajlı), Runtime Constraints "Aktör" ve "İzin", Authorization Convention "Joker yok". |
| 3 | MOD-0053 ve MOD-0056 kapıları yok; park önerisi (TRM-13) | Park D55/1 ile kalktı; D55/3: v1 bağımsız, bağlantı sonraki sürüm. Dependencies tablosunda "engel değil" olarak yazılı; ters arama ve arama indeksi v1 dışında (AC-32). |

### §M karşılığı — kaynaktan alınanlar, uyarlananlar, taşınmayanlar

| Kaynak (paket §C / §E / §G / §H / §I) | Karar | Gerekçe |
|---|---|---|
| Politikalarda `HasPermission(ctx.User, "*")` (`DependencyInjection.cs:110-219@e4c93c29`) | **taşınmaz** | level-1 koşul 2; joker tek başına her yetkiyi verir; AC-14 |
| `actorId = body ActorId ?? sub ?? "unknown"` (`TaxonomiesController.cs:596-598@e4c93c29`) ve gövdeden `ActorId` (paket §G) | **taşınmaz** | level-1 koşul 2; aktör yalnız `sub`, yoksa 401; AC-15 |
| `taxonomy.manage`'in Assign / Approve / Publish politikalarını da geçmesi | **taşınmaz** | v1'de onay / yayın yok; bağ izni ayrı (AC-28) |
| `taxonomy.*` iki parçalı anahtarlar + `TAXONOMY_*` takma adları | **taşınmaz** | PKS-001 ≥ 3 parça; Recovery'de yayımlanmadı, takma ad gerekmez |
| TX-01…TX-04 taksonomi liste / oluştur / ayrıntı / terim arama | **uyarlanır** | taksonomi CRUD + liste süzgeci (`q`); arama MOD-0056 sonrası |
| TX-06…TX-09 terim oluştur / düzenle / taşı / kullanımdan kaldır; döngü, mükerrer kod / etiket, kullanımdan kalkmış terim korumaları (`TaxonomyMutableCommandService.cs:201-425@e4c93c29`, paket atıfı) | **kural olarak alınır, kod yazılır** | Recovery `Rules/TagTermHierarchy.cs` |
| `TermAlias` ayrı koleksiyonu | **uyarlanır** | terimde `Aliases` listesi (≤ 10) |
| TX-13 taksonomi arşivi | **uyarlanır** | `Retire` |
| TX-05 / TX-12 / TX-22 sürüm taslağı, sürüm emekliliği, yayın; TX-15…TX-21 değişiklik talebi ve onay | **taşınmaz (v1)** | sürüm ve onay iş akışı v1 dışı (D55/3); kaynak MOD-0023 nesnelerini doğrudan yazıyordu (paket §D) — Recovery'de yasak desen |
| TX-10 / TX-11 terim ilişkileri (Related / Broader / Narrower / Equivalent) | **taşınmaz (v1)** | bilgi grafiği MOD-0056 ile sonraki sürüm |
| TX-14 fixture teardown | **taşınmaz** | test altyapısı, ürün işlevi değil |
| TA-01 bağ doğrulama ucu | **taşınmaz** | doğrulama bağ oluşturmanın içinde |
| TA-02 / TA-03 bağ oluştur / kaldır | **uyarlanır** | `TagAssignment` + soft delete + sayaç |
| TA-04 `objects/{type}/{id}/tags` | **uyarlanır** | `by-target` (+ `by-targets`), modül kimliğiyle |
| TA-05 etiket önerileri, TR-01…TR-07 etiketleme kuralları ve simülasyon | **taşınmaz (v1)** | otomatik / kural tabanlı etiketleme v1 dışı |
| Tohum işçisi ve sabit kiracı değerli tohum ayarı (paket §C, §G) | **taşınmaz** | sabit kiracı yasak (çekirdek kural 2); varsayılan taksonomi kaydı içe aktarılmaz |
| `mod0057_integration_events` / `tag_assignment_events` satırları | **taşınmaz** | hiçbir kod okumuyordu (paket §F); v1 olay yok |
| `mod0057_audit_intents` (`pending_outbox`, okunmuyor) ve governance yolunda iş yazımından sonra denetim | **taşınmaz** | yerine CentralAudit işlem içi yol (Audited Events) |
| `Mod0057IdempotencyRecord` (önce iş, sonra kayıt; tekil indeks yok) | **taşınmaz** | mükerrerlik tekil indekslerle (AC-2, AC-3, AC-22) |
| İşlemsiz çoklu yazım (paket §H) | **taşınmaz** | her komut tek işlem (Runtime Constraints) |
| Version CAS | **kavram olarak alınır** | `EntityBase.Version` |
| Sayfa boyutu 1–200 | **alınır** | Validation Rules |
| UI: `_LayoutBackbone`, yetkisiz yönlendirme, özdeş İngilizce 7 resx, hardcoded `Rules.cshtml` ve menü etiketi | **taşınmaz** | Layout & Shell Contract, AC-33…AC-37 |
| `DeletedAt` yokluğu (kaynak `BaseEntity`) | **düzeltilir** | CrmService `EntityBase` `DeletedAt` taşır |

### §T karşılığı — test kanıtı

Level-1 koşul 2'nin kanıtı AC-14 (joker → 403, sabotajlı) ve AC-15 (aktör yalnız `sub`, `"unknown"` yok, sabotajlı) ile
DL-1 kabul kapısındadır. Kaynağın tarihsel "Conditionally Ready", "72 passed" ve işaretli AC kutuları (paket §N.3) hiçbir
şeyi boyutlandırmak için kullanılmaz.

### §U — tüketici onay slotları (paket §P, 6 slot) → bu paket

| Slot | Bu paketteki durum |
|---|---|
| SUPPLIER-ONBOARDING (MOD-0140; kaynakta Noop geçit) | v1'de bağlantı yok; O-8 — ilk tüketici adayı, kendi kararı ve XMC-001 ile |
| DOCUMENT-MANAGEMENT (yalnız ad düzeyi) | v1'de bağlantı yok; O-8 |
| WORKCENTER-PANEL (geliştirici senaryosu) | taşınmaz; gömülü panel Follow-up |
| AUTH (MOD-0018) | Ready-for-dev: paket metni (paket kapısı) + kayıt kodu (DL-1) |
| AUDIT (MOD-0021) | CentralAudit yol `b` (O-6); Platform kabul ölçümü DL-1 için |
| WORKFLOW (MOD-0023) | v1'de yok; kaynaktaki doğrudan MOD-0023 nesne yazımı taşınmaz |

Slotların kapanışı level 3'ten önce gerekir (level-1 kaydı); borçlu sahip (K1).

### MOD-0053 sınırı (Blueprint HARD bağımlılığı ↔ D55 bağımsız v1)

İş terimleri, eşanlamlılar ve terim onayı MOD-0053'e aittir; MOD-0057 v1 etiketi iş terimine bağlamaz; MOD-0053 bağlantısı
Follow-up. Kaynak: `docs/records/audits/2026-10/treasury-intake-handover-review-01/MOD-0053-UZLASTIRMA-01.md` (Blueprint 8.8
`Dependencies_Normalized`: MOD-0057 → MOD-0053 HARD, MOD-0053 → MOD-0052 HARD — satır 105-106; kavram ayrımı ve risk alanları —
satır 152-157; K5 önerisi — satır 187). Bu oturumda Blueprint dosyası doğrudan açılmadı (openpyxl yok); bağımlılık değeri o
kayıttan aktarılmıştır (kısmi). Örtüşme riskleri ve bu paketteki önlem:

| Risk | v1'de önlem |
|---|---|
| Eşanlamlı (MOD-0053 sözlük modeli) ile terim takma adı (`Aliases`) karışır | `Aliases` yalnız arama / görüntü kolaylığı; anlam, sahiplik ya da onay taşımaz; okuma sözleşmesinde "synonym" adıyla yayımlanmaz |
| İki onay akışı (MOD-0053 terim onayı ve Blueprint'teki MOD-0057 etiket onayı; ikisi de MOD-0023 görevine binebilir) | MOD-0057 v1'de onay akışı yok; etiket onayı Follow-up'ta MOD-0053 terim onayıyla birlikte tasarlanır, ikinci bir onay motoru yazılmaz |
| Terim–etiket bağı (etiketin bir iş terimine işaret etmesi) | v1'de `TagTerm`'de iş terimi başvurusu alanı yok; bağ MOD-0053 Recovery'ye gelince sürümlü sözleşmeyle, MOD-0053 sahibinin kararıyla eklenir |

### Tasarım notları

- **Opak başvuru:** MOD-0057 hedef kaydın varlığını doğrulamaz ve onun verisini okumaz; hedef silinirse bağ kalır
  (tüketici kendi silme akışında bağları kaldırabilir — Follow-up: hedef silme bildirimi sözleşmesi).
- **Yol kodlardan üretilir** (MOD-0162-FU01 §17 dersi): etiket yeniden adlandırma yolu bozmaz; kod değişmediği için yol da değişmez.
- **Derinlik 5** MOD-0162-FU01 §4 önerisiyle aynıdır (tutarlılık).
- **Okuma sözleşmesi sürümü:** `taxonomy-tagging-read.v1`; eklemeli alan sürümü değiştirmez, kaldırma / tür değişikliği `v2` ister.

## Follow-up Items

- Arama ve bilgi grafiği bağlantısı (MOD-0053 Data Dictionary / Glossary, MOD-0056 Enterprise Search) — Recovery'de olunca (D55/3).
- MOD-0053 bağlantısı: etiket ↔ iş terimi bağı, eşanlamlı modeliyle `Aliases` ayrımı, terim onayı ile etiket onayının tek
  akışta tasarlanması — MOD-0053 Recovery'ye gelince, MOD-0053 sahibiyle (O-12).
- Terim ilişkileri (Related / Broader / Narrower / Equivalent) ve ters arama ("bu etiketi taşıyan kayıtlar") — MOD-0056 ile.
- Etiketleme kuralları, öneriler ve otomatik etiketleme (Blueprint SoR "tagging rules").
- Etiket onayı ve taksonomi sürümleri / yayın (Blueprint SoR "tag approvals"; MOD-0023 sahibiyle, sözleşme üzerinden — doğrudan nesne yazımı değil).
- Çok dilli terim etiketleri (`dynamic-localization-standard`).
- Başka modül ekranlarına gömülü etiket paneli — her tüketici modül sahibinin XMC-001 onayıyla.
- İlk tüketici bağlantıları: MOD-0140 (kaynak Noop geçidi), Document Management, MOD-0162 `KnowledgeContent.Tags[]` serbest etiketlerinin MOD-0057 bağlarına geçişi (MOD-0162 sahibiyle; O-4).
- Hedef kayıt silindiğinde bağların kaldırılması için bildirim sözleşmesi.
- Özel nitelikli kişisel veri içeren etiketlere teknik engel (KVKK) — gerekirse.
- Merkezi ret kaydı (`Outcome = Denied`) — denetim sahibine.
- Olay yayını (`crm.tag-assignment.*`) kalıcı yolla (outbox) — tüketici adlandırılınca.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.
- Kimlik kaydında MOD-0057 satırı (paket onay PR'ında önerilir — D61) ve DCP-011 park satırının güncellenmesi (O-10
  kararından sonra); sahipleri yazar.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | Yerleşim (domain) | (a) `commercial-suite` (D55/2 önerisi; MOD-0162-FU01 MOD-0057'yi gelecek seam olarak anıyor) (b) `platform-shared-services` (kaynak yerleşimi; Blueprint Placement "Data Governance Service" Recovery'de yok) (c) yeni Data & Knowledge domain'i / servisi (EA kararı; port yok) | **(a)** — Recovery'de var olan bir domain ve servis; kaynak Platform yerleşimi ve "Data Governance Service" bugün karşılıksız. Blueprint'teki domain ("2) Data, Knowledge & Intelligence") ile fark Implementation Notes'ta açık kalır; yatay yeteneğe taşıma ileride kimlik değiştirmeden yapılabilir (MOD-0162-FU01 F1 deseni). **KAPANDI — KARAR D61 (2026-10-28): (a)** — commercial-suite sahibi atanana kadar K1 yönetim onayı; yalnız yerleşim (O-2, O-4 ve diğerleri açık; sahip ataması O-13) | K1 (D61) — sahip atanınca bilgi olarak ona gider |
| O-2 | Servis ve port | (a) `Diten.CrmService`, port 5061 (AGENTS.md §3; domain-config'in servisi; kod ve CRM Gateway rotaları var) (b) `Diten.Platform` 5057 (domain dışı — O-1 (b) gerektirir) (c) yeni servis — port EA kararı, burada önerilmez | **(a)** — yeni port ve servis yok; CRM'in işlem altyapısı ve vekil deseni hazır | sahip (K1) — açık; D61 yerleşimi kapattı, servis / portu kapatmadı; EA yalnız (c) seçilirse |
| O-3 | RCS-001 kayıt kodu | (a) Taksonomiye görünen kayıt kodu yok (ad + kimlik); `TermCode` kullanıcı tanımlı, değişmez sözlük anahtarı — RCS dışı, gerekçe: tüketicinin makine anahtarı, iş referansı değil (MOD-0048 referans kodları ve MOD-0162 Topic kodları gibi); bağın görünen kodu yok (b) Taksonomi OTOMATİK, tür öneki önerisi `TXY` (§3'te çakışma yok — ölçüldü; `TAX` vergiyle karışmasın diye seçilmedi), grup öneki (kiracı geneli kayıt, §5.3) — BL-565 ortak kod servisine bağımlı (c) terimler de RCS-001'e | **(a)** — taksonomi küçük bir yapılandırma kümesidir, kod değer katmaz ve BL-565 (servis yok) engelini doğurmaz; RCS-001 §5.6 varsayılanından ayrılış sahip onayı ister. (b) seçilirse §3 satırı ayrı `.antigravity` değişikliğidir, bu paket yazmaz | sahip (K1) |
| O-4 | MOD-0162-FU01 ile sınır | (a) MOD-0162 Subject / Topic / AudienceProfile'ın SoR'u kalır; MOD-0057 modüller arası genel taksonomi ve etiketleme; MOD-0057 o nesneleri kopyalamaz / eşitlemez; `KnowledgeContent.Tags[]` v1'de değişmez (geçiş Follow-up); Knowledge içeriği MOD-0057'ye hedef olabilir ama yalnız MOD-0162 sahibinin onayıyla (b) MOD-0057 bütün taksonomilerin SoR'u, Subject / Topic taşınır (c) MOD-0057 yalnız düz etiket, hiyerarşi modüllerde kalır | **(a)** — MOD-0162-FU01 `approved` ve kodda canlı; (b) onaylı bir paketi ve çalışan kodu bozar; (c) D55/3'ün "sınıf ağaçları" kapsamını düşürür. **Çakışmalar (açık):** (1) `KnowledgeContent.Tags[]` serbest etiketi ile MOD-0057 etiketleri iki ayrı "etiket" kavramı; (2) kiracı MOD-0057'de Subject / Topic'e benzeyen bir taksonomi kurabilir — v1'de teknik engel yok, sayfa yardımı yönlendirir; (3) MOD-0162-FU01 F9 seam sahibi "Data & Knowledge Plane" diyor, bu paket commercial-suite diyor (O-1, yerleşim D61 ile onaylı; sınır bu kararla kapanmaz) | sahip (K1) + MOD-0162 sahibi (XMC-001) |
| O-5 | İzin ad alanı ve rol şablonları | (a) `crm.tag-taxonomies.*`, `crm.tag-assignments.*` (Authorization Convention tablosu; PKS-001 §4 `crm.*` CrmService'e ait) (b) `commercial.*` (domain-config'te EA onayına tabi rezervasyon) (c) yeni `taxonomy.*` ad alanı (PKS-001 §4'e `.antigravity` satırı gerekir) | **(a)** + üç rol şablonu (okuyucu, etiketleyici, taksonomi yöneticisi); `read-tenant-wide` yalnız açık atama. Yatay servise taşınırsa takma ad kuralı (PKS-001 §3) uygulanır | sahip (K1) + MOD-0018 sahibi (kayıt) |
| O-6 | Denetim yolu ve kategori | (a) CentralAudit ortak iletici, yol `b`, işlem içi `EnlistAsync`; CrmService defterine yeni iz `crm-merkezi-iletim`, borç / istisna eklenmez; `Category = ReferenceData` (5) (b) aynı yol, `Category = MasterData` (14) (c) Platform'a yeni `Commercial` kategorisi (Platform sahibi, XMC-001) | **(a)** — ortak iletici var ve SupplyChain'de kabul edildi; taksonomi sözlük / referans verisidir; Platform değişikliği gerekmez. İz satırı Control Tower kararıdır (D35 deseni) | Control Tower + sahip (K1) |
| O-7 | Veri kapsamı (SEC-002) — bağlar | (a) Runtime Constraints'teki kapsam cümlesi: taksonomi / terim sözlük verisi (istisna cümlesi); yönetim bağ listesi yalnız kendi bağları, kiracı geneli `read-tenant-wide` ile; hedef okuması tek hedef anahtar okuması; ters arama yok (b) bağ okumasında hedef modülün kapsamı sorulur (her hedef modülle sözleşme gerekir) (c) bütün bağlar `crm.tag-assignments.read` ile kiracı geneli | **(a)** — örgütsel kapsam alanı olmayan veride en dar güvenli yol; (b) v1'de tüketici sözleşmesi olmadan yapılamaz; (c) SEC-002 §3'e aykırı | sahip (K1) |
| O-8 | Tüketici listesi ve okuma sözleşmesi | (a) v1'de adlandırılmış tüketici yok; `taxonomy-tagging-read.v1` yayımlanır; her tüketici (MOD-0140, Document Management, MOD-0162 Tags, gömülü panel) kendi kararı ve XMC-001 ile bağlanır; hedef varlığı doğrulanmaz (opak) (b) MOD-0140'ı ilk tüketici olarak v1'e al (kaynaktaki Noop geçidinin yerine) (c) hedef doğrulama sözleşmesi v1'de | **(a)** — D55/3 "v1 bağımsız"; tüketici sözleşmeleri başka modüllere dokunur | sahip (K1); tüketici modül sahipleri |
| O-9 | Kod alma izni | (a) kod alma yok: kaynak yalnız başvuru, kod CrmService düzeninde sıfırdan yazılır (MOD-0217 D38/3 deseni) (b) D1 deseni: kaynak derlenip testleri geçtikten sonra uyarlanarak alma | **(a)** — kaynak başka serviste (Platform), başka taban sınıfla, §L'de 12 eksikle; v1 kapsamı kaynağın küçük bir alt kümesi; uyarlama yeniden yazımdan pahalı | sahip (K1) — D55/4 |
| O-10 | DCP üyeliği | (a) MOD-0057 tek modül paketi, DCP üyesi değil; DCP-011'in `parked_modules` satırını DCP sahibi günceller (b) DCP-011'e aday üye (c) yeni DCP | **(a)** — tek modül, finans dilimlerine bağımlılığı yok; DCP-011 finance-treasury intake'tir | sahip (K1) + DCP-011 sahibi |
| O-11 | Etiket modeli | (a) etiket = düz taksonominin (`Kind = Flat`) terimi; tek terim / tek bağ modeli (b) ayrı `Tag` varlığı ve ayrı bağ türü | **(a)** — tek kod yolu, tek okuma sözleşmesi; düz ve hiyerarşik kümeler aynı kurallarla yönetilir | sahip (K1) |
| O-12 | Blueprint HARD bağımlılığı (MOD-0057 → MOD-0053) ile D55 bağımsız v1 | (a) v1 bağımsız: iş terimleri, eşanlamlılar ve terim onayı MOD-0053'ün; MOD-0057 v1 etiketi iş terimine bağlamaz; bağlantı Follow-up (b) MOD-0053 (ve onun HARD kapısı MOD-0052) Recovery'ye gelene kadar MOD-0057 bekler (c) MOD-0057 v1 kendi eşanlamlı / terim modelini kurar | **v1 bağımsız (D55); Blueprint bağımlılığı MOD-0053 geldiğinde bağlantıyla karşılanır.** (b) D55/1'i fiilen geri alır (MOD-0052 de yok); (c) MOD-0053'ün SoR'unu çoğaltır. Örtüşme riskleri (eşanlamlı ↔ takma ad, iki onay akışı, terim–etiket bağı) Implementation Notes → "MOD-0053 sınırı"nda | sahip (K1) + EA (Blueprint bağımlılık sapması) |
| O-13 | commercial-suite domain sahibi ataması | — (atama kararı; bu paket aday önermez) | — açık nokta, **kapı değil (KARAR D65/1)**: sahip atanana kadar paket onayını da K1 verir; yerleşimi yeniden açmaz (D61). Atanınca D61 ve paket onayı ona bilgi olarak gider | K1 (yönetim) |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi |
|---|---|---|---|
| DL-1 | taksonomi çekirdeği ve denetim izi ve paket iskeleti | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Backend File Convention, Validation Rules, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar, Teslim dilimleri, Dilim kabul kapısı | E4 |
| DL-2 | etiket bağlama API'si | Dependencies, Runtime Constraints, Failure Path to Verify, Authorization Convention, Audited Events | E4 |
| DL-3 | tüketici okuma sözleşmesi ve gateway | Gateway / API Routing Decision | E3 |
| DL-4 | kiracı ekranları | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001) | E4 |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events'in taksonomi ve terim satırları, Runtime Constraints'in kiracı / aktör / işlem kuralları ve
Authorization Convention'ın `crm.tag-taxonomies.*` anahtarları DL-1'de gelir; bölümler DL-2'de bağ satırlarıyla kapanır).
Ortak bölümler (Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Follow-up Items) her dilimde aşağıdaki AC
kimlikleriyle ölçülür. **Kesişen AC'ler:** bazı AC'ler birden çok dilimin koduna uzanır (bütün yazmalar, bütün uçlar, hedef
okuması). Birden çok dilimde "(… kısmı)" ile yazılan AC, o dilimde yalnız yazılan kısmıyla ölçülür ve onu sayan **son**
dilimde tam kapanır: AC-6, AC-11, AC-12, AC-15, AC-18 ve AC-19 DL-2'de; AC-13, AC-14, AC-24, AC-25, AC-27 ve AC-28 DL-3'te;
AC-16 DL-4'te. Kısmı geçen AC kabul kaydında "kapandı" sayılmaz; tek dilimde geçen diğer AC'ler o dilimde tam kapanır.

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: dört varlık ve enum'lar, `Features/TagTaxonomies/**`, `Features/TagTerms/**`, depolar, indeksler,
  `RegisterClassMaps`, iki controller, CentralAudit kaydı, defter + `CT-DECISIONS.md` satırı + `AuditTrailStandardTests`.
  **Neden önce:** sözlüğü, kiracı / aktör / izin / işlem kurallarını ve denetim yolunu dış bağımlılık olmadan kanıtlar.
  Önkoşul: paket düzeyi ENGELLER'in tümü ve "— DL-1 için" ölçüm maddesi. WP dalı `feature/crm/wp-0057-dl1` (D51/3 deseni).
  Bitiş: AC-1, 2, 3, 4, 5, 6 (terim silme kuralı kısmı: `HasEverBeenAssigned = false` ve silinmemiş çocuk denetimi; bayrak
  bu dilimde yalnız sunucu alanıdır), 7, 8, 9, 10, 11 (taksonomi ve terim komutları kısmı — 11 komut), 12 (taksonomi ve
  terim komutları kısmı), 13 (taksonomi ve terim uçları kısmı), 14 (taksonomi ve terim uçları kısmı), 15 (taksonomi ve
  terim yazmaları kısmı), 16 (DL-1 arka uç kodu kısmı), 17, 18 (suite kurulumu ve DL-1 yarışları kısmı: AC-2, AC-3, AC-7,
  AC-11, AC-42), 19 (`crm.tag-taxonomies.*` kısmı), 42.
- **DL-2** — Yollar: `Features/TagAssignments/**` (oluştur, kaldır, toplu kaldır, yönetim listesi, ayrıntı),
  `TagTargetSlot` deposu, `TagAssignmentsController` (bu aksiyonlar). **Neden ayrı:** bağlama yarışları ve SEC-002 kapsamı
  kendi kanıtını ister; DL-1'i bekletmesin. Önkoşul: DL-1. Bitiş: AC-6 (ilk bağın `HasEverBeenAssigned` değerini aynı
  işlemde `true` yapması kısmı — tam kapanış), 11 (bağ komutları kısmı: Create / Delete / BulkDelete TagAssignment — tam
  kapanış), 12 (bağ komutları kısmı — tam kapanış), 13 (bağ yazma, yönetim listesi ve ayrıntı uçları kısmı), 14 (aynı uçlar
  kısmı), 15 (bağ yazmaları kısmı — tam kapanış), 16 (DL-2 arka uç kodu kısmı), 18 (AC-22, AC-23 yarışları kısmı — tam
  kapanış), 19 (`crm.tag-assignments.*` kısmı — tam kapanış), 20, 21, 22, 23, 24 (soft delete, sayaç azalması ve yeniden
  bağlama kısmı; yönetim listesi ve ayrıntıyla ölçülür), 25 (bağ oluşturma, yönetim okuması ve kaldırma kısmı), 26, 27
  (yeni bağ reddi kısmı), 28 (bağ yazma izinleri ve taksonomi / bağ izin ayrımı kısmı).
- **DL-3** — Yollar: `GetTagAssignmentsByTarget` / `ByTargets` sorguları, okuma sözleşmesi testi; Gateway rotaları
  integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** tüketiciye açılan sözleşme ve Gateway başka
  sahiplere dokunur. Önkoşul: DL-2 ve "— DL-3 için" maddeleri. Bitiş: AC-13 (`by-target` / `by-targets` uçları kısmı —
  tam kapanış), 14 (aynı uçlar kısmı — tam kapanış), 16 (DL-3 arka uç kodu kısmı), 24 (kaldırılan bağın hedef okumasında
  dönmemesi kısmı — tam kapanış), 25 (başka kiracının aynı `TargetId` bağlarının hedef okumasında görünmemesi kısmı — tam
  kapanış), 27 (mevcut bağların hedef okumasında `taxonomyStatus` / `termStatus` ile okunması kısmı — tam kapanış), 28
  (`.read` olmadan hedef okuması 403 kısmı — tam kapanış), 29, 30, 31, 32.
- **DL-4** — Yollar: üç `Views/CRM/Tag*/**` klasörü, JS, resx (7 dil), MVC vekilleri, sayfa yardımı, nav anahtarları,
  menü girdisi. **Neden ayrı:** ekran API'ye ve Gateway'e bağlıdır. Önkoşul: DL-3 ve "— DL-4 için" maddesi. Bitiş:
  AC-16 (MVC vekilleri ve görünümlerin sabit / varsayılan kiracı taraması kısmı — tam kapanış), 33, 34, 35, 36, 37, 38,
  39, 40, 41.

## Dilim kabul kapısı

Kabul kapısı dilimin önkoşulundan ayrıdır: önkoşul dilim başlamadan kapanan karar / onay / okunarak ölçümdür, kabul kapısı
o dilimin yeni kodunun testlerinin geçmesidir (çalıştırmak geçmek değildir).

| Dilim | Geçmesi gerekenler |
|---|---|
| DL-1 | Bitiş listesindeki AC'ler (kesişen AC'lerin yalnız DL-1 kısmı); gerçek replica set AC'leri (AC-2, AC-3, AC-7, AC-11, AC-42 karşılıklı taşıma yarışı ve çit sabotajı) kendi Mongo URI'li suite'te (AC-18); `AuditTrailStandard` (AC-12); Auth kayıt kodu main'de ve testler gerçek izinle (AC-19); dev ortamında bir oluşturma / güncelleme / silme için merkezi denetim kaydı (AUD-001 §8); `Diten.CrmService` build 0 hata; yerel kapıda `scripts/` Python testleri |
| DL-2 | Bitiş listesindeki AC'ler — AC-6, AC-11, AC-12, AC-15, AC-18 ve AC-19'un tam kapanışı dahil (AC-11 hata enjeksiyonu ve `EnlistAsync` sabotajı bağ komutlarında da koşulur; AC-12 `AuditTrailStandard` bağ komutlarıyla yeşil); AC-22 ve AC-23 gerçek replica set'te; SEC-002 kanıt testi (AC-26); bağ komutlarının merkezi denetim kaydı (dev ortamı); build |
| DL-3 | Bitiş listesindeki AC'ler — AC-13, AC-14, AC-24, AC-25, AC-27 ve AC-28'in tam kapanışı dahil (AC-25 depo süzgeci sabotajı hedef okumasında da koşulur); sözleşme anlık görüntü testi (AC-30); Gateway uçtan uca 401 / 404 / OPTIONS (AC-31); build |
| DL-4 | Bitiş listesindeki AC'ler — AC-16'nın tam kapanışı dahil; tarayıcı smoke ve UAS-001 canlı denemesi; `verify_datatable_page.py` ve `quality-gate-datatable` (AC-34); RESX 7 dil parite; nav anahtar denetimi; `Diten.Web` build |
