---
id: MOD-0262-FU01
name: Document Binary Store
parent: MOD-0262
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: ready-for-dev
approval_status: pending
revision: "r5 (2026-11-02) — bağımsız inceleme 01 tur 2 (r4 d3e37c37, KOŞULLU: 1 ORTA, 7 DÜŞÜK; docs/records/reviews/MOD-0262-FU01/2026-11-02-d3e37c37.md) F-1…F-8 ve CT kararı (2026-11-02, hepsi kabul) tek geçişte işlendi: F-1 AC-32 boyut sınırı uç metadata'sıyla ölçülür (IRequestSizeLimitMetadata, TestServer RequestSizeLimit uygulamaz), F-2 ret yazıcısı aktör alanlarını açık doldurur, F-3 alreadyCompensated = yürütücü ön okuması + komut alanı + yarış kuralı, F-4 ret başına tek yazar tablosu, F-5 akış içi 413 komut içinde çevrilir + iptal dalı, F-6 AC-24 sabotajı, F-7 K-E yol başına 50 MiB sabit sınır (CT önerisi; sahip onayı r5 onay satırıyla), F-8 §19 ForDataExport maddesi. AC-35 eklendi; kapanış tablosu §19.1. r4 (d3e37c37…) — inceleme 01 tur 1 (r3 cc9acb1b, UYGUN DEĞİL: 1 ENGEL, 3 ORTA, 9 DÜŞÜK; docs/records/reviews/MOD-0262-FU01/2026-11-02-cc9acb1b.md) F-1…F-13 işlendi, AC-32…AC-34 eklendi. r3 (cc9acb1b…) — K-B sahip kararı A (Beste): service → Service eşlemesi; K-A / K-C / K-D CT kararları. r2 (9420fd5d…) — DL-2 servis kimliği desteği (MOD-0217 KARAR D56/2). r1 = sha256 cc49014c4f8e4502daa9d6bfdfda80a72520f32a02f1cfe942e3087045f151f3 (CT onayı 2026-09-07; CT-APR-01 öncesi, approvals.tsv satırı yok). r5 yeni bağımsız inceleme (CT-APR-02) ve approvals.tsv onay satırı ister."
status_note: "r1: CT promoted draft → ready-for-dev 2026-09-07 after DCP-008 was approved and OD-1 / OD-3 / OD-4 / OD-A were resolved; DL-1 kodu main'de (kapanış KISMİ, §19). r5 (2026-11-02): K-A, K-B, K-C, K-D kapalı; inceleme 01 tur 1 ve tur 2 bulguları işlendi; K-E (güvenilen yolda 50 MiB sabit sınır) CT önerisidir, sahip onayı r5 approvals.tsv onay satırına bağlıdır (§18). §18'de geriye yalnız r5'in bağımsız incelemesi ve approvals.tsv onay satırı kaldı. approval_status pending — bu sürümün onayı yok, approved_by / approved_on yalnız r1'i gösterir. Onay satırı gelene kadar hiçbir dilim (DL-1 düzeltmesi dahil) bu paketle dağıtılmaz."
approved_by: control-tower
approved_on: 2026-09-07
owner: platform-shared-services
branch: feature/structured-content-messaging
started: 2026-09-07
target: TBD
form_field_count: 0
capability_pack: DCP-008
runtime_implementation: in-progress
---

# MOD-0262-FU01 — Document Binary Store

> **Status guard (r5, 2026-11-02).** `status: ready-for-dev`: K-A…K-D kapalı, K-E CT önerisi ve sahip onayı r5 onay satırına bağlı (§18); geriye yalnız r5'in bağımsız incelemesi (CT-APR-02) ve `execution/approvals/approvals.tsv` onay satırı kaldı. `approval_status: pending` olduğu için CT SOP §36.1 görev sınıfı kuralına göre `status` tek başına yetki **değildir**; onay satırı gelmeden hiçbir dilim dağıtılmaz. DL-1 kodu r1 onayıyla yazıldı ve main'de (§19); DL-2 (servis kimliği) başlamadı.
>
> **Scope guard.** This authorization covers **`MOD-0262-FU01` only**. FU02…FU07 remain planned and unreserved. DL-2 bu FU'nun içinde yeni dilimdir, yeni MOD/FU kimliği açmaz (§19 "Plan hizalaması").
>
> **Identity.** Canonical child of `MOD-0262` (Internal Document Repository Service). DCP-002 preflight: `verify_module_id.py --check-id MOD-0262-FU01 --name "Document Binary Store" --parent MOD-0262` → **exit 0** (yeniden koşuldu 2026-11-02). No new `MOD-xxxx` is minted.

---

## 1. Module Summary

FU01 makes MOD-0262 **real as a bounded store**: it takes the existing MOD-0029-FU01 content-storage seam, moves it out of a consumer's feature folder into a shared contract layer, replaces its in-process-shaped contract with a stream-based one, and puts **authorization, tenant isolation and audit at the repository boundary** — where a service boundary will later be drawn.

It is deliberately the **narrowest useful slice**. Per DCP-008 **AD-1**, FU01 depends on **neither** MOD-0030 (Records Management, W-3), **nor** MOD-0031 (Evidence Linking, W-4), **nor** the undefined `DOC-REPO-BUNDLE` contract. That is what allows a W-1 / `R1 - PPM MVP` / RC=Y module with two HARD dependants (MOD-0028, MOD-0313) to ship without waiting on a W-4 module and an open EA item.

**DL-2 (r2).** Başka bir servisin kendi **servis kimliğiyle** (kullanıcı token'ı olmadan) belge deposunu çağırabilmesi: akışlı yükleme, meta okuma, içerik okuma ve telafi. İlk ve v1'de tek tüketici MOD-0217 CLM (Diten.MdmService) — KARAR D56/2: kullanıcılara `platform.document-repository.*` verilmez, belge indirme yalnız MOD-0217 uçlarıyla. Kimlik doğrulama yeni bir şema uydurmaz; MOD-0033-FU02'nin Auth imzalı servis token'ı desenine yeni bir audience eklenir (§8.1).

**Target users:** none directly — FU01 delivers **no page**. Its consumers are MOD-0028 / MOD-0029 (today), MOD-0217 (DL-2, servis kimliğiyle), MOD-0162 and MOD-0313 (later FUs).

---

## 2. Ownership and Boundaries

**In scope**
- The `IContentStorageGateway` / `ContentRef` abstraction, relocated to a shared contract layer and owned by MOD-0262.
- Provider implementation(s) behind that abstraction (R1: local filesystem, carried over).
- Deterministic, **tenant-isolated** object keys.
- Checksum (SHA-256), media-type / extension / size validation.
- **Authorization enforced by the store itself** (DCP-008 AD-5).
- Audit emission to MOD-0021 for store / read / delete-compensation.
- Stream-based upload and download (DCP-008 AD-4).
- **DL-2:** güvenilen tüketici yüzeyi (`api/internal/v1/document-repository/trusted-consumer/*`), servis token'ı doğrulama şeması, kapalı tüketici listesi, tüketiciye bağlı nesne görünürlüğü ve bu yolun denetim kaydı (retler dahil).

**Out of scope (each owned elsewhere)**
- Document metadata, versioning, controlled-document lifecycle → MOD-0028 / MOD-0029.
- Evidence packages → MOD-0262-FU04.
- Physical destruction / disposition execution → MOD-0262-FU05.
- Repository Admin, Repository Health, Document Binary Store **pages** → MOD-0262-FU02 / FU03.
- CRM pointer resolution → MOD-0262-FU06.
- Extraction into a standalone `Platform Document Service` process → MOD-0262-FU07 (DCP-008 AD-3).
- `DOC-REPO-BUNDLE` payload definition → ⛔ blocked on **EA-BP-003**; FU01 must not define it.
- `EnterpriseStrategyService/UploadsController` → CT decision **D-5**, separate security lane.
- **DL-2 dışı:** Auth'ta yeni audience ve MDM servis istemcisi tanımı (MOD-0033 / MOD-0018 — Auth, sahibi Ali; §7 XMC-001 bağımlılığı); MdmService'teki token sağlayıcı ve belge deposu istemcisi (MOD-0217 DL-2 — Treasury CT); sözleşme kapsamı (SEC-002), `mdm.contracts.documents.*` izinleri ve indirme denetimi (MOD-0217 AC-27, AC-62).

**Owned by this FU:** document binary objects and their pointers, and (DL-2) the trusted-consumer access surface to them. Nothing else.

---

## 3. Owned Objects

> r1 adları ready-for-dev kapısı için öneriydi; DL-1 kodu main'de olduğundan aşağıdaki DL-1 satırları **koddan ölçülmüş** adlardır (2026-11-02, HEAD 925ea4e34 = origin/main 743f42d1c bu yollarda aynı). DL-2 satırları **yazılacak** adlardır; bugün kodda yoktur (`grep` 0).

| Kind | Item | Note |
|---|---|---|
| Contract (relocated) | `IContentStorageGateway` | Ölçüldü: `services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/IContentStorageGateway.cs`. Signature changes per AD-4/AD-5. |
| Contract (relocated) | `ContentStoreRequest` / `ContentStoreResult` / `ContentStreamResult` / `ContentStorageScope` | Same move. `ContentStoreRequest.Content: byte[]` is **removed** (stream). `ContentStorageScope` bugün `Documents`, `Templates`, `TaskAttachments`, `ContentMessagingArtifacts`, `ESignatureImages`, `CrmVisitEvidence` (son dördü başka WP'lerin eklemeli değerleri). |
| Domain | `ContentRef` | Existing embedded pointer — **preserved unchanged** (`ContentId`, `StorageProvider`, `ObjectKey`, `FileName`, `MediaType`, `ByteSize`, `Checksum`, `CreatedAt`, `CreatedBy`, `VersionId`). |
| Domain | `RepositoryObject` | The store's own record of a stored object; the SoR row behind a `ContentRef`. Enables FU03 orphan reconciliation. DL-2 iki eklemeli alan getirir (§4). |
| Provider | `LocalFileSystemContentStorageGateway` | Carried over; re-homed behind the relocated contract. |
| Application | `DocumentRepositoryService` (`StoreForCallerAsync` / `GetForCallerAsync` / `OpenReadForCallerAsync` / `CompensateForCallerAsync` + süreç içi `StoreAsync` / `GetAsync` / `OpenReadAsync` / `CompensateAsync`), `DocumentRepositoryAccessPolicy` | Replaces the "caller authorises" assumption. |
| Command (DL-1) | `StoreRepositoryObjectCommand`, `CompensateRepositoryObjectCommand` | Ölçüldü: ikisi de `IAuditableCommand` + `IAuditMetadataProvider`. |
| Query (DL-1) | `GetRepositoryObjectByIdQuery`, `OpenRepositoryObjectContentQuery` | Ölçüldü: denetim işareti yok (§19 ölçüm notu). |
| API (DL-1) | `POST api/v1/document-repository/objects` (multipart upload → `RepositoryObjectModel`) | AD-4: no `byte[]` in a JSON body. Ölçüldü: bugün `[FromForm] IFormFile` bağlaması (§19 ölçüm notu, §20). |
| API (DL-1) | `GET api/v1/document-repository/objects/{contentId}/content` (stream) | Authorised by the store. |
| API (DL-1) | `GET api/v1/document-repository/objects/{contentId}` (pointer metadata; never `ObjectKey`) | `ObjectKey` is internal and never returned. |
| API (DL-1) | `POST api/v1/document-repository/objects/{contentId}/compensate` | Ölçüldü; r1 tablosunda yoktu. Telafi yalnız — purge değil (AD-6). |
| Permission | `platform.document-repository.read` | See §14. |
| Permission | `platform.document-repository.manage` | |
| Permission (başka WP) | `platform.document-repository.visit-evidence.write` / `.read` / `.read-all` | WP-VW-W3-PLT'nin kapsam bağlı anahtarları (kodda, `DocumentRepositoryModels.cs` → `DocumentRepositoryPermissions`); FU01 kapsamında değil, listeye ölçüm olarak yazıldı. |
| API (DL-2) | `POST api/internal/v1/document-repository/trusted-consumer/objects` | Akışlı multipart (`MultipartReader`), `IFormFile` / `[FromForm]` yok → `RepositoryObjectModel`. |
| API (DL-2) | `GET api/internal/v1/document-repository/trusted-consumer/objects/{contentId}` | Meta; `ObjectKey` yok. |
| API (DL-2) | `GET api/internal/v1/document-repository/trusted-consumer/objects/{contentId}/content` | Akış. |
| API (DL-2) | `POST api/internal/v1/document-repository/trusted-consumer/objects/{contentId}/compensate` | Telafi yalnız (AD-6). |
| Controller (DL-2) | `InternalTrustedDocumentRepositoryConsumerController` | `[AllowAnonymous]` (r4, F-1): kapı yürütücünün içindedir; `LoginOnlyEndpointGuardTests.AnonymousInternalControllers`'a listenin koşuluyla girer. Emsal `InternalTrustedSourceAuditIntentController`. |
| Security (DL-2) | `TrustedServiceTokenValidationExtensions.DocumentRepositoryAuthenticationScheme` = `"TrustedDocumentRepositoryConsumerService"`, `DocumentRepositoryRequiredAudience` = `"TRUSTED_DOCUMENT_REPOSITORY_CONSUMER"` | Var olan uzantıya eklemeli **beşinci JWT şeması**, **dördüncü servis audience'ı** (§8.1). Denetleyiciye öznitelikle bağlanmaz; yürütücü `AuthenticateAsync` ile çağırır. |
| Security (DL-2) | `TrustedDocumentRepositoryConsumerRequestExecutor` (+ `ITrustedDocumentRepositoryConsumerRequestExecutor`), `TrustedDocumentRepositoryConsumerIdentity` | Başlık ön kontrolü, açık kimlik doğrulama, kapalı liste, kiracı bağlama, boyut sayacı, ret denetimi; emsal `TrustedSourceAuditIntentRequestExecutor` + `TrustedWorkflowConsumerRequestExecutor`. |
| Security (DL-2) | `TrustedDocumentRepositoryRefusalAudit` | Ret kaydı yazıcısı: kimliği doğrulanmış ret → token kiracısı; kimliksiz ret → `IsPlatformGlobal`, sabit pencereli sınırlayıcı (dakikada 60) + `Meter` sayacı (§8.1). r5 (F-2): `ActorType` sabit yazılır, `ActorId` / `ActorEmail` / `ActorDisplayName` hiçbir zaman null bırakılmaz (`AuditService`'in `ICurrentUserContext` dolgusu devreye girmez); hangi sonucu kimin yazdığı §8.1 "Ret başına tek yazar" tablosundadır (F-4). |
| Security (DL-2) | `TrustedDocumentRepositoryUploadLimits` (+ `TrustedUploadLimitedReadStream`, `TrustedUploadLimitExceededException`) | `MaxFileBytes = 52_428_800`, `MaxRequestBytes = MaxFileBytes + 1_048_576`, form alanları 16 KiB (§8.1). Sayan akış `ITrustedUploadLimitSignal`'ı uygular (r5, F-5). |
| Contract (DL-2) | `ITrustedUploadLimitSignal` (`bool LimitExceeded`) | **Yeni** (r5, F-5): Application katmanının sayan akışın bayrağını okuduğu tek yer; `StoreTrustedRepositoryObjectHandler` akış içi aşımı komutun içinde 413'e çevirir (Application ASP.NET'e referans vermez — ölçüldü: `Diten.Platform.Application.csproj`). |
| Configuration (DL-2) | `TrustedDocumentRepositoryConsumerAuthorizationOptions` (bölüm `TrustedDocumentRepositoryConsumerAuthorization`) + `TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator` | Kapalı tüketici listesi; başlatmada doğrulama (`ValidateOnStart`), modül başına tek girdi; emsal `TrustedWorkflowStartAuthorizationOptions` + `TrustedWorkflowStartAuthorizationOptionsValidator`. |
| Repository (DL-2) | `IRepositoryObjectRepository.GetByContentIdIncludingCompensatedAsync` | Yalnız güvenilen telafinin tekrarı için (§8.1 "Telafi tekrarı"). |
| Command (DL-2) | `StoreTrustedRepositoryObjectCommand`, `CompensateTrustedRepositoryObjectCommand` | `IAuditableCommand` + `IAuditMetadataProvider` (yol `a`, §21). r5 (F-3): telafi komutu `AlreadyCompensated` (init) alanını taşır; değeri yürütücünün ön okumasıdır, `GetAuditMetadata()` `alreadyCompensated`'ı bu alandan yazar (§8.1 "Telafi tekrarı"). |
| Query (DL-2) | `GetTrustedRepositoryObjectByIdQuery`, `OpenTrustedRepositoryObjectContentQuery` | İçerik okuma denetlenir (§21). |
| Frontend route | **none** | `shell: none`, `golden_reference: none`. |

---

## 4. Entity Fields

**`ContentRef` (embedded pointer) — unchanged, listed for completeness**

| Field | Type | Required | Rule |
|---|---|---|---|
| `ContentId` | Guid | yes | Store-assigned. |
| `StorageProvider` | string | yes | Provider discriminator (`local-filesystem` in R1). |
| `ObjectKey` | string | yes | **Internal only** — never serialised to any client. |
| `FileName` | string | yes | Sanitised. |
| `MediaType` | string | yes | Must pass the allow-list. |
| `ByteSize` | long | yes | Must be > 0 and ≤ configured maximum. |
| `Checksum` | string | yes | SHA-256, computed by the store, never accepted from the caller. |
| `CreatedAt` / `CreatedBy` | DateTimeOffset / string | yes | |
| `VersionId` | Guid | yes | Owning item's version. |

**`RepositoryObject` (`TenantScopedEntity`)**

| Field | Type | Required | Rule |
|---|---|---|---|
| `TenantId` | Guid | yes | From `TenantScopedEntity`; **mandatory** isolation dimension. |
| `ContentId` | Guid | yes | Unique per tenant. |
| `StorageProvider` / `ObjectKey` | string | yes | Resolution pair. |
| `Scope` | string (enum adı) | yes | Extends today's `Documents` / `Templates`; extension is additive and backward-compatible. |
| `OwningItemId` / `OwningVersionId` | Guid | yes | Back-reference to the consumer's record. |
| `CompanyId` | Guid? | no | Ölçüldü (kodda var, r1 tablosunda yoktu). DL-2 yolunda zorunlu ve boş olmayan Guid (§12). |
| `Checksum` / `ByteSize` / `MediaType` / `FileName` | — | yes | Mirrors `ContentRef`. |
| `TrustedConsumerClientId` | Guid? | DL-2 | **Yeni, eklemeli.** Güvenilen tüketici yolunun yazdığı satırda servis token'ının `sub`'ı; kullanıcı ve süreç içi yolunun satırlarında `null`. **Yalnız iz ve denetim için** — görünürlük kararında kullanılmaz (r4, F-2). Ad emsali `WorkflowInstance.TrustedConsumerClientId`. |
| `TrustedConsumerModule` | string? | DL-2 | **Yeni, eklemeli.** Kapalı listedeki tüketici modülü (v1'de yalnız `MOD-0217`); kullanıcı yolunda `null`. **Görünürlük bu alana bağlıdır** (güvenilen yol yalnız kendi modülünün satırını görür; kullanıcı yolu dolu satırı görmez — K-C). |

`CreatedBy`: güvenilen tüketici yolunda `service:Diten.MDM:{clientId}` biçiminde yazılır (kullanıcı yolunun `ICurrentUserContext.ActorName` değeriyle hiçbir zaman eşleşmez; `DocumentRepositoryAccessPolicy.IsCreator` karşılaştırması bu satırlara kullanıcıya yetki açamaz).

**Mongo index needs**
- `{ TenantId: 1, ContentId: 1 }` — unique.
- `{ TenantId: 1, StorageProvider: 1, ObjectKey: 1 }` — unique; backs FU03 orphan detection.
- `{ TenantId: 1, OwningItemId: 1, OwningVersionId: 1 }`.
- DL-2 yeni indeks getirmez: güvenilen yol satırı `{TenantId, ContentId}` ile bulur, `TrustedConsumerModule` / `Scope` eşleşmesini bellekte denetler. İki yeni alan eksik satırda `null` okunur (eski satırlar kullanıcı yolunun satırıdır).
- ⚠️ Partial-index filters must not use `$ne` / `$not` (unsupported → startup crash); use `$type`. See `mongo-indexing.md`.
- ⚠️ Never index or sort **two** `DateTimeOffset` fields together (BSON-array "parallel arrays" failure).

---

## 5. Repo Scope

**DL-1 (kodu main'de; ölçüldü 2026-11-02 — aşağıdaki yolların hepsi origin/main 743f42d1c'de ve HEAD 925ea4e34'te var, iki ref arasında fark yok):**

```text
services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/IContentStorageGateway.cs   ← relocated (OD-A)
services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/ContentStorageOptions.cs
services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/DocumentRepositoryReasonCodes.cs
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/**
services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ContentRef.cs   ← unchanged
services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentRepository/RepositoryObject.cs
services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs
services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.DocumentRepository.cs
services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/**
gateway/Diten.ApiGateway/ocelot.json                                                    ← /api/v1/document-repository rotası (var)
```

**Call sites that must keep compiling unchanged in behaviour** (AD-2 — the seam is preserved, not rewritten): `DocumentManagementControlledDocumentsController`, `DocumentManagementTemplatesController`, `DocumentManagementTemplateMastersController`, `DocumentVersioningService`, `ControlledDocumentRegistrationService`, `TemplateService`, `TemplateMasterService`, `TemplateVariantService`. **Eight** call sites (measured at implementation; the pack originally said six). All binary I/O funnels through `DocumentVersioningService`, whose single `StoreAsync` caller is what makes AD-4 surgical.

**DL-2 (yazılacak; bugün hiçbiri kodda yok):**

```text
services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedDocumentRepositoryConsumerController.cs   ← NEW
services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs        ← eklemeli şema + audience sabiti
services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedDocumentRepositoryConsumerRequestExecutor.cs   ← NEW
services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerRequestExecutor.cs    ← NEW
services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerIdentity.cs           ← NEW
services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedDocumentRepositoryConsumerAuthorizationOptions.cs   ← NEW
services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator.cs   ← NEW (ValidateOnStart)
services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryRefusalAudit.cs        ← NEW (ret kaydı + sınırlayıcı + Meter sayacı)
services/Diten.Platform/src/Diten.Platform.API/Security/TrustedDocumentRepositoryUploadLimits.cs        ← NEW (boyut sabitleri + sayan akış TrustedUploadLimitedReadStream : ITrustedUploadLimitSignal + TrustedUploadLimitExceededException)
services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/ITrustedUploadLimitSignal.cs   ← NEW (r5, F-5: sayan akışın bayrağı; handler akış içi aşımı 413'e çevirir)
services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IRepositoryObjectRepository.cs             ← GetByContentIdIncludingCompensatedAsync (eklemeli)
services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/RepositoryObjectRepository.cs   ← aynı yöntem (kiracı süzgeci korunur, yalnız IsDeleted koşulu yok)
services/Diten.Platform/src/Diten.Platform.API/Program.cs                        ← yalnız DI / options kaydı
services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json      ← yalnız kapalı liste girdisi (ClientId; sır yok) — Auth istemci kimliği verildikten sonra
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Commands/StoreTrustedRepositoryObjectCommand.cs        ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Commands/CompensateTrustedRepositoryObjectCommand.cs   ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Queries/GetTrustedRepositoryObjectByIdQuery.cs         ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Queries/OpenTrustedRepositoryObjectContentQuery.cs     ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Handlers/CommandHandlers/StoreTrustedRepositoryObjectHandler.cs        ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Handlers/CommandHandlers/CompensateTrustedRepositoryObjectHandler.cs   ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Handlers/QueryHandlers/GetTrustedRepositoryObjectByIdHandler.cs         ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Handlers/QueryHandlers/OpenTrustedRepositoryObjectContentHandler.cs     ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Validators/StoreTrustedRepositoryObjectValidator.cs    ← NEW
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Services/DocumentRepositoryService.cs     ← güvenilen yöntemler + kullanıcı yolunda tüketici satırı dışlaması + CompensateRowAsync sağlayıcı silmesini MarkCompensatedAsync dönüşüne bağlar (r5, F-3; iki yol)
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Services/DocumentRepositoryAccessPolicy.cs   ← tüketici satırı dışlaması
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/DocumentRepositoryModels.cs   ← güvenilen girdi kayıtları
services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/DocumentRepositoryReasonCodes.cs   ← yeni ret kodları (§13)
services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentRepository/RepositoryObject.cs   ← iki eklemeli alan
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/AuditActorTypeResolver.cs   ← K-B sahip kararı A (Beste, 2026-11-02; XMC-001 izni bu kararla): FromActorTypeClaim'e "service" => AuditActorType.Service; ForDataExport servis aktörünü reddetmeye devam eder; sınıf yorumu güncellenir (§19 K-B)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumer*Tests.cs   ← NEW
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedServiceTokenValidationTests.cs   ← audience ayrımı vakaları
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/LoginOnlyEndpointGuardTests.cs     ← AnonymousInternalControllers'a tek ad + kapı / kanıt notu (TrustedServiceSchemeControllers'a girdi YOK)
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/**                                          ← yalnız resolver eşlemesi ve yan etki testleri (AC-31); AuditBehaviorActorTypePipelineTests'teki "service" InlineData'sı Unknown teorisinden çıkıp yeni Service testine taşınır
```

DL-2, `gateway/**`, `services/Diten.AuthService/**` ve `services/Diten.MdmService/**` altına **dokunmaz** (§6, AC-28).

---

## 6. Protected Paths

- `.antigravity/**`
- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx` — **EA/SoT-owned, never edited**
- `services/Diten.CrmService/**` — FU06's lane
- `services/Diten.EnterpriseStrategyService/**` — CT decision **D-5**, separate lane
- `services/Diten.AuthService/**` — DL-2 dahil hiçbir dilim dokunmaz; audience ve istemci tanımı Auth'un işidir (sahibi Ali, §7 XMC-001)
- `services/Diten.MdmService/**` — MOD-0217'nin istemcisi ve token sağlayıcısı MOD-0217 DL-2'nindir (Treasury CT)
- `services/Diten.DevEnablementService/**`, `services/Diten.PvgService/**`
- `gateway/Diten.ApiGateway/ocelot.json` — yalnız `integration-agent`; DL-2 rota gerektirmez (§15)
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**` — MOD-0021'in; DL-2 yalnız `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/AuditActorTypeResolver.cs`'e tek eşleme ekler, `ForDataExport` korumasını yazar ve sınıf yorumunu günceller (K-B sahip kararı A, Beste, 2026-11-02 — XMC-001 izni bu kararla).
- Değişmeyen denetim dosyaları (tam yollar; r4, F-13): `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs`, `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditService.cs`, `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/CanonicalTransactionalAuditOutboxWriter.cs`, `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/Services/DataExportAuditWriter.cs`. r5: DL-2'nin tek yazar kuralının (§8.1) dayandığı davranış dosyaları da değişmez — `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ValidationBehavior.cs`, `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ExceptionBehavior.cs`.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs` — DL-2 değiştirmez (boyut aşımı yürütücünün sayan akışıyla, §8.1).
- MOD-0029 retention/disposition: `…/Features/DocumentManagementRetention/**` — read as input only; FU01 changes **no** disposition behaviour

---

## 7. Dependencies

| Dependency | Type | State |
|---|---|---|
| MOD-0018 RBAC / ABAC Authorization | **HARD** | ✅ available |
| MOD-0021 Audit Trail Service | **HARD** | ✅ ~98% |
| MOD-0029-FU01 existing seam + local provider | reuse basis (AD-2) | ✅ in repo |
| `Diten.Platform` service + Mongo + gateway | infrastructure | ✅ |
| MOD-0030 Records Management | **NOT a dependency of FU01** (AD-1) | ❌ absent — deliberately excluded |
| MOD-0031 Evidence Linking | **NOT a dependency of FU01** (AD-1) | ⚠️ planned — deliberately excluded |
| `DOC-REPO-BUNDLE` contract | **NOT a dependency of FU01** (AD-1) | ⛔ EA-BP-003 open — deliberately excluded |
| **DL-2 — Auth: audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER`** (MOD-0033-FU02 servis token'ı verme; `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs` → `IsAllowedAudience`; emsal MOD-0033-FU02 pack "Section D" — `Diten.MDM/TRUSTED_REFERENCE_DATA_CONSUMER`) | **XMC-001** — Auth kodu; bu paketin dilimi değildir | ⛔ yok (ölçüldü: politika üç audience tanıyor). Sahip: **Ali (Auth, MOD-0018 / MOD-0033)**. Tarih: **TBD**. DL-2 kodunu bekletmez (testler test anahtarıyla imzalı token kullanır); DL-2 **kabul kapısını** bekletir. |
| **DL-2 — Auth: MDM servis istemcisi + dar izin** (tek `AllowedAudience` = `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` olan `Diten.MDM` istemci kimliği; dev kiracı tahsisleri; sır dağıtımı) | **XMC-001** — Auth işletim / yapılandırma | ⛔ yok. Sahip: **Ali (Auth)**. Tarih: **TBD**. Platform kapalı listesine yazılacak `ClientId` bu adımdan gelir. |
| **DL-2 — MOD-0021: servis aktörünün denetim kaydı** (`AuditActorTypeResolver.FromActorTypeClaim` bugün `service` değerini `Unknown`'a eşler; `AuditService` `Unknown` aktörlü kaydı reddeder; `AuditBehavior` bunu yalnız uyarı loguyla geçer — kayıt düşmez) | **XMC-001** — MOD-0021 dosyası | ✅ **K-B sahip kararı A (Beste, 2026-11-02)**: `"service" => AuditActorType.Service` eşlemesi DL-2'de yazılır; XMC-001 izni bu kararla verildi. Yan etki envanteri §19, ölçüt AC-31. |
| **AUD-001 §3 kural metni hizalaması** (`.antigravity/rules/audit-trail-standard.md` §3 tablosu satır 3: "Servis çağrısıysa servis kimliği → `ActorType = System` + `SourceService`") | `.antigravity/**` korunan yol — kullanıcı onayı | ⛔ metin kodla farklı (kod `Service` kullanıyor, §19 K-B ölçümü). Sahip: **Ali** (`.antigravity` değişikliği). Tarih: **TBD**. DL-2'yi bekletmez (yetki sırası: paket kuralın üstünde); fark kapanana kadar §19'da yazılı kalır. |
| **DL-2 — Tüketici: MOD-0217 DL-2** (MdmService token sağlayıcısı, belge deposu istemcisi, `MultipartReader` akışı, ret → 503) | ters bağımlılık (tüketici) | Treasury CT; MOD-0217 Ready-for-dev Checklist "— DL-2 için" maddeleri bu paketin DL-2'sini bekler. |

Reverse: **MOD-0028** and **MOD-0313** are Blueprint-recorded **HARD** dependants of MOD-0262. **MOD-0217** (DL-2) KARAR D56/2 ile bu paketin DL-2'sine bağlıdır.

---

## 8. Runtime Constraints

- Service: `Diten.Platform`, port **5057**; gateway **5000**.
- `entity_base: TenantScopedEntity` — repository objects are **tenant-owned**, not a cross-tenant catalog, so `GlobalEntity` is wrong here. (`TenantScopedEntity` extends `BaseEntity` and is what `TenantRepository<T>` requires; the frontmatter had followed the standard's table, which lagged the code.)
- Tenant isolation is **mandatory and structural**: `TenantId` participates in the object key, not only in the query filter.
- Soft delete on `RepositoryObject` metadata. **Physical deletion is not implemented in FU01** — see AD-6 and FU05.
- `TryDeleteAsync` remains **best-effort orphan compensation only**. ⛔ It is not a purge path and must never be reused as one.
- Binary bytes are never stored in Mongo; GridFS remains banned.
- Storage root stays outside `wwwroot` and is never served by a static/public URL.
- Standalone-Mongo guard: multi-document atomic writes must check `SupportsTransactionsAsync` before `StartTransaction`, with a compensation path.
- `Diten.Platform` uses a **global** `GuidSerializer(GuidRepresentation.Standard)` (`DependencyInjection.cs`) — there is **no** `RegisterClassMaps` in this service. The binary-Guid class-map trap is CrmService-specific; a round-trip test still proves Guid FKs persist queryably, not silently empty.

### 8.1 Trusted consumer identity

**Seçilen desen ve gerekçe (ölçüldü 2026-11-02).** Platform'da servis kimliği için üç emsal var:

| emsal | nerede | neden seçilmedi / seçildi |
|---|---|---|
| İç API anahtarı (`X-Internal-Api-Key`, `AuthService:InternalApiKey` ile sabit zamanlı karşılaştırma) | `LoginOnlyEndpointGuardTests` → `AnonymousInternalControllers` notları (ör. `InternalESignActionsController`, `InternalTenantEmailIdentityController`) | **Seçilmedi.** Tek ortak sır; çağıran servisi ve kiracıyı imzalı olarak taşımaz; anonim iç denetleyici listesine girer. D74/1 bu deseni MOD-0018 aday sorgusu için kullanıcı token'ıyla birlikte seçti — orada kimlik kullanıcıdır; burada D56/2 kullanıcı token'ını yasaklar. |
| Güvenilen kimlik bilgisi üçlüsü (tanımlayıcı + sır + audience; FU21) | `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs` → `Authenticate`, `AllowsPair` | **Seçilmedi.** Platform yapılandırmasında duran paylaşılan sır; sabit modül kodu `product-item-sku-master` ve sabit izin çifti listesine bağlı; kiracı imzalı token'dan gelmez. |
| Auth imzalı servis token'ı (MOD-0033-FU02: RS256, tam 10 talep, 300 sn ömür, `actor_type=service`, `service_name=Diten.MDM`, `tenant_id`, audience başına ayrı şema) | `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs` → `AddTrustedServiceTokenValidation`, `HasExactServiceClaims`; tüketen `InternalTrustedWorkflowConsumerController` + `TrustedWorkflowConsumerRequestExecutor`, `VerifiedReferenceDataServiceTenantContext` | **Seçildi.** Kiracı Auth'un kiracı tahsisiyle imzalı gelir (başlık / gövde değil); audience başına ayrı şema yanlış amaçlı token'ı reddeder (MOD-0033-FU02 Section D: "Audit and Workflow identities cannot substitute for this identity"); MDM tarafında aynı desenin sağlayıcıları var (`AuthProductIdentityWorkflowServiceIdentityProvider`, `AuthWorkflowVerifiedMarketServiceIdentityProvider`; Auth ucu `/api/internal/v1/auth/service-tokens/issue`). Yeni şema türü uydurulmaz: var olan uzantıya **dördüncü servis audience'ı** (beşinci JWT şeması) eklenir; denetleyiciye öznitelikle bağlanmaz, yürütücü çağırır (r4, F-1 / F-6). |

**Kimlik doğrulama mekanizması (r4, inceleme 01 F-1).**
- Ölçüm: `services/Diten.Platform/src/Diten.Platform.API/Security/PlatformAccessPipeline.cs` → `UsePlatformAccessPipeline` sırası `UseAuthentication` → `UseTenantResolution` → `UseAuthorization` → `UseRateLimiter`. r2/r3'teki sınıf düzeyi `[Authorize(AuthenticationSchemes = …)]` `UseAuthorization`'da yürütücüden **önce** koşar; şema reddi çerçevenin gövdesiz 401 / 403'üne döner, şema yapılandırma hatası 500 olur, ret denetimi ve 2 sn bütçe hiç çalışmaz. Bu kapı r4'te **kaldırıldı**.
- **Seçilen yol: `[AllowAnonymous]` + yürütücü içinde açık `HttpContext.AuthenticateAsync(DocumentRepositoryAuthenticationScheme)`.** Emsal: `InternalTrustedSourceAuditIntentController` (`[AllowAnonymous]`, `api/internal/v1/audit/source-intents`) + `TrustedSourceAuditIntentRequestExecutor` (başlık ön kontrolü, sonra kimlik) + `TrustedSourceAuditIntentServiceIdentity.ResolveAsync` (`AuthenticateAsync`; `InvalidOperationException` → kullanılamaz). MOD-0040-FU02'nin iç ucu da aynı desendir (`[AllowAnonymous]`, `LoginOnlyEndpointGuardTests.AnonymousInternalControllers`'a tek ad). Yeni mekanizma yok: şema olayları (`JwtBearerEvents.OnChallenge` / `OnAuthenticationFailed`) ve `IAuthorizationMiddlewareResultHandler` kodda bu amaçla kullanılmıyor; ikisi de yeni ve genel bir mekanizma olurdu.
- Sonuç: her ret yürütücüde gövdeli `Response<T>` ve ret koduyla döner (401 / 403 / 404 / 400 / 413 / 503 / 504), ret denetimi aynı yerde yazılır, 2 sn bütçe gerçekten uygulanır. Denetleyici `AnonymousInternalControllers`'a o listenin koşuluyla girer: her ret dalı kaldırılınca adlı bir gerçek host testi kırmızıya döner (AC-16).
- `UseAuthentication`'ın varsayılan kullanıcı JWT şeması servis token'ını doğrulamaz (audience `diten-erp` istenir; `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` `AddJwtBearer`), `HttpContext.User` anonim kalır. `/api/internal` `UseTenantResolution`'da atlanır (ölçüldü: `InternalAuditController` ve `InternalModuleRegistrationController` notları) — kiracı yalnız yürütücüde token'dan kurulur.
- Şema `TrustedDocumentRepositoryConsumerService`, `AddTrustedServiceTokenValidation` içindeki dört JWT şemasının (`TrustedServiceToken`, `TrustedWorkflowConsumerService`, `TrustedReferenceDataConsumerService`, `TrustedWorkflowDelegatedUser`) yanına **beşinci şema**; servis audience'ı olarak **dördüncü** (`TRUSTED_AUDIT_SOURCE_INGEST`, `TRUSTED_WORKFLOW_CONSUMER`, `TRUSTED_REFERENCE_DATA_CONSUMER`'dan sonra). `CreateTokenValidationParameters(options, clock, DocumentRepositoryRequiredAudience)` + `OnTokenValidated`'da `HasExactServiceClaims(token, issuer, DocumentRepositoryRequiredAudience)`; parametreler ve talep sözleşmesi diğer servis şemalarıyla aynı, yalnız audience farklı.
- Kimlik (`TrustedDocumentRepositoryConsumerIdentity`): `ClientId` = `sub`, `TenantId` = `tenant_id`, `TokenId` = `jti`, `ServiceName` = `Diten.MDM`, `Audience`. Doğrulama başarılı ama talep eksik / çift / biçimsiz → 403. Doğrulama başarısız → 401. `AuthenticateAsync` `InvalidOperationException` atar (şema / anahtar yapılandırılmamış) → 503 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAVAILABLE`.
- Başarılı doğrulamadan sonra yürütücü `HttpContext.User`'ı servis principal'ı yapar ve gönderimden sonra eskisine döndürür (emsal `TrustedWorkflowConsumerRequestExecutor`), böylece `AuditBehavior`'ın `ITenantAuthorizationContext`'i `actor_type = service` görür. ⚠ `JwtTenantAuthorizationContext` değerlerini kurucuda okur; istek kapsamında `HttpContext.User` değişmeden önce çözülmemelidir — AC-25 gerçek host'ta ölçer.
- Token izin talebi taşımaz (`RoleClaimType = "service_identity_role_forbidden"`); servis bir kullanıcı izniyle geçmez (§14).

**Başlık ön kontrolü (gövde okunmadan, kimlik doğrulamadan önce; emsal `TrustedSourceAuditIntentRequestExecutor`).**
- `Content-Length` > `TrustedDocumentRepositoryUploadLimits.MaxRequestBytes` → 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE`.
- Birden çok `Authorization`, `X-Tenant-Id` var ya da `X-Delegated-Authorization` var → 403 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_FORBIDDEN` (kiracı yalnız token'dan; D56/2: kullanıcı token'ı iletilmez).
- `Authorization` yok ya da tam `Bearer <token>` biçiminde değil → 401 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAUTHENTICATED`.
- Bu üç ret **kimliksiz rettir** (aşağıda "Ret denetimi").
- `X-Correlation-Id` kabul edilir ve denetim kaydının `CorrelationId`'sine yazılır (`routes.md`); yoksa sunucu üretir.

**Kapalı tüketici listesi (r4, inceleme 01 F-2 — CT kararı 2026-11-02).**
- Yapılandırma `TrustedDocumentRepositoryConsumerAuthorization:Entries[]`: `{ ClientId, ServiceName, Audience, ConsumerModule, Scopes[] }`.
- **Başlatmada doğrulama** (`IValidateOptions<TrustedDocumentRepositoryConsumerAuthorizationOptions>` + `ValidateOnStart`; emsal `Program.cs`'teki `TrustedWorkflowStartAuthorizationOptionsValidator` kaydı): her girdi kodda sabit kümeyle birebir — `ServiceName == "Diten.MDM"`, `Audience == DocumentRepositoryRequiredAudience`, `ConsumerModule ∈ {"MOD-0217"}`, `Scopes` boş değil ve kodda o modüle izinli scope'ların alt kümesi (`MOD-0217` → `{"Documents"}`), `ClientId` boş olmayan Guid. Kural dışı girdi, aynı `ConsumerModule` için ikinci girdi, aynı `ClientId` ikinci kez ya da 8'den fazla girdi → **uygulama açılmaz** (başlatma hatası). Yalnız yapılandırma yazarak tüketici eklenemez.
- Boş liste geçerlidir ve güvenilen yüzey kapalıdır: geçerli token'lı her çağrı 503 `…_UNAVAILABLE` (MOD-0217 Failure Path: "yapılandırılmamış → 503").
- Token geçerli ama `ClientId` listede yok → 403 `…_FORBIDDEN`.
- **İstemci yenileme:** modül girdisinin `ClientId`'si yeni istemcinin `sub`'ı ile değiştirilir; eski istemcinin token'ı 403 alır, eski istemciyle yazılmış nesneler yeni istemciden okunur ve telafi edilir (görünürlük modüle bağlı).
- **Listeye ekleme kuralı:** yeni tüketici (başka modül ya da başka scope) = bu paketin revizyonu (yeni dilim ya da AC) + bağımsız inceleme + approvals.tsv onay satırı + kodda sabit kümeye ekleme + Auth'ta o servis için audience / istemci (XMC-001, Ali) + yapılandırma girdisi. İkinci bir MDM modülü kendi girdisini ve ayrı istemci kimliğini ister (modül başına tek girdi; MOD-0033-FU02: "identity AllowedAudience remains singular").

**Kiracı ve şirket kapsamı.**
- Kiracı: **yalnız** token'ın `tenant_id` talebi; `TenantScope.Begin(_tenantContext, identity.TenantId)` içinde komut / sorgu çalışır. Formda `tenantId` alanı → 400 (bilinmeyen alan).
- Başka kiracının `ContentId`'si → 404 `NOT_FOUND_NON_LEAKAGE` (kiracı süzgeci + nesne anahtarındaki kiracı; 403 değil).
- Şirket: yüklemede multipart form alanı `companyId` (zorunlu, boş olmayan Guid) satıra yazılır. Platform şirketin sözleşme kapsamını **denetlemez**: SEC-002 kapsamı ve `mdm.contracts.documents.*` MOD-0217'de, Platform'u çağırmadan önce denetlenir (MOD-0217 AC-27). Okuma ve telafi şirket parametresi almaz.

**Tüketiciye bağlı görünürlük (r4, F-2 — CT kararı).**
- Güvenilen yükleme satıra `TrustedConsumerModule = girdinin ConsumerModule'ü` ve `TrustedConsumerClientId = sub` yazar. `TrustedConsumerClientId` yalnız iz ve denetim içindir; **görünürlük kararında kullanılmaz**.
- Güvenilen meta / içerik / telafi yalnız şu satırı görür: aynı kiracı **ve** `TrustedConsumerModule == girdinin ConsumerModule'ü` **ve** `Scope ∈ girdinin Scopes`'u. Diğer her satır (aynı kiracıdaki MOD-0029 `Documents` satırları dahil) → 404.
- Kullanıcı yolu (`api/v1/document-repository/*`) `TrustedConsumerModule` dolu satırı **görmez** (K-C): meta / içerik / telafi 404 — `platform.document-repository.read` / `.manage` taşıyan platform yöneticisi token'ı dahil (D56/2: indirme yalnız MOD-0217 uçlarıyla). Kullanıcı yolunun kendi satırlarındaki davranışı değişmez.

**Boyut (r4, inceleme 01 F-4; r5, tur 2 F-1 / F-5 / F-7).**
- **Tek etkin sınır:** dosya bölümü en çok `TrustedDocumentRepositoryUploadLimits.MaxFileBytes = 52_428_800` bayt (50 MiB; MOD-0217 Runtime Constraints "sınır önerisi 50 MB … MOD-0262'nin yapılandırılmış üst sınırından küçük olanı"). Koddadır, yapılandırılmaz. Bu, **K-E** kararıdır (§18, §19; CT önerisi, sahip onayı r5 onay satırıyla): yalnız güvenilen tüketici yolunun **daha dar** sınırıdır. r1'in kilitli kararı ("Maximum object size → config-driven, default greater than 50 MB") genel sınırı yönetir — `ContentStorageOptions.MaxFileSizeBytes`, yapılandırılır, varsayılan 268 435 456 — ve DL-2'de değişmez; K-E onu yeniden açmaz.
- İstek gövdesi sınırı uçta açık: `[RequestSizeLimit(TrustedDocumentRepositoryUploadLimits.MaxRequestBytes)]`, `MaxRequestBytes = MaxFileBytes + 1_048_576 = 53_477_376` (form alanları ve sınırlar). Kestrel varsayılanı (30 000 000 bayt) bu uçta geçerli değildir.
- **Uç sınırının ölçüm biçimi (r5, F-1 — CT kararı 2026-11-02).** .NET 8'de `RequestSizeLimitAttribute`, `Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata.MaxRequestBodySize`'ı uygular (ölçüldü: `microsoft.aspnetcore.app.ref` 8.0.31 → `ref/net8.0/Microsoft.AspNetCore.Mvc.Core.xml`, açık arayüz üyesi `RequestSizeLimitAttribute…IRequestSizeLimitMetadata#MaxRequestBodySize`); MVC eylem ve denetleyici öznitelikleri uç metadata'sına girer. Kanıt: host'un `EndpointDataSource`'undan okunan güvenilen yükleme ucunun (`RouteEndpoint`, şablon `api/internal/v1/document-repository/trusted-consumer/objects`, `POST`) `Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize == TrustedDocumentRepositoryUploadLimits.MaxRequestBytes` (AC-32). `GetMetadata` son girdiyi döndürdüğü için sonradan eklenen `[DisableRequestSizeLimit]` (`MaxRequestBodySize = null`) de yakalanır.
- ⚠ **TestServer `RequestSizeLimit`'i uygulamaz.** Host testleri TestServer'dır: `Microsoft.AspNetCore.Mvc.Testing` 8.0.8 → restore edilen `Microsoft.AspNetCore.TestHost` 8.0.8 (`project.assets.json`; 8.0.11 de aynı). TestHost `IHttpMaxRequestBodySizeFeature`'ı uygulamaz (ölçüldü 2026-11-02: iki sürümün derlemesinde bu tür adı yok; tanınan özellikler `IHttpBodyControlFeature`, `IHttpRequestBodyDetectionFeature`, `IHttpRequestLifetimeFeature`, `IHttpResetFeature`, `IHttpResponseBodyFeature`, `IHttpResponseFeature`, `IHttpResponseTrailersFeature`, `IHttpUpgradeFeature`, `IHttpWebSocketFeature`). Bu yüzden host testinde `[RequestSizeLimit]` etkisizdir ve Kestrel'in 30 000 000 baytlık varsayılanı yoktur: "büyük yükleme Kestrel sınırına takılmaz" vakası her durumda yeşil kalacağı için **kanıt sayılmaz ve yazılmaz**. Gerçek Kestrel'le (`UseKestrel`, port 0) koşan test **isteğe bağlıdır**; yazılırsa 31 MB'lık yükleme 201 döner ve `RequestSizeLimit` kaldırılınca Kestrel'in gövdesiz 413'üyle kırmızıya döner.
- `ContentStorageOptions.MaxFileSizeBytes` (`Documents`'ın genel kuralı; varsayılan 268 435 456) `MaxFileBytes`'tan küçükse başlatma hatası (yukarıdaki doğrulayıcı) — sağlayıcının kendi boyut 400'ü bu yolda hiç tetiklenmez.
- **Aşım tek kodla döner: 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE`** (MOD-0217 bunu `MDM_CONTRACT_DOCUMENT_TOO_LARGE` 413'e eşler):
  - (a) `Content-Length > MaxRequestBytes` → yürütücünün başlık ön kontrolü, gövde okunmadan (kimliksiz ret; yazarı ret yazıcısı, aşağıdaki tablo).
  - (b) **Akış içi aşım — çeviri komutun içindedir (r5, F-5).** Yürütücü dosya bölümünü `TrustedUploadLimitedReadStream`'e sarar ve komuta bu akışı verir. Dosya baytları `MaxFileBytes`'ı aşınca akış `LimitExceeded = true` yapar ve `IOException` türevi `TrustedUploadLimitExceededException` atar. Sağlayıcı (`LocalFileSystemContentStorageGateway.StoreAsync`, `IOException` dalı, satır 118–123) yarım dosyayı `TryRemovePartial` ile siler ve 503 `STORAGE_UNAVAILABLE` döner. `StoreTrustedRepositoryObjectHandler` servis sonucu başarısızsa ve `command.Content is ITrustedUploadLimitSignal { LimitExceeded: true }` ise 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE` döndürür. Böylece `AuditBehavior` 503'ü değil 413'ü görür ve tek `Failed` kaydına `ResponseStatusCode = 413` yazar; istemci de 413 alır — kayıt ile yanıt aynıdır. Satır yazılmaz. Yürütücü komut yanıtını **çevirmez** (r4'teki "yürütücü 503'ü 413'e çevirir" kaldırıldı: kayıt 503 kalıyordu).
  - (c) Kestrel'in gövde sınırı istisnası (`Microsoft.AspNetCore.Http.BadHttpRequestException`, `IOException` türevi, `StatusCode = 413`) sayan akışın iç okumasında yakalanır: akış `LimitExceeded = true` yapar ve istisnayı yeniden atar → (b) ile aynı dal, aynı kayıt. Form alanları ≤ 16 KiB olduğu için iyi biçimli istekte sayan akış Kestrel'den önce tetiklenir; (c) savunma dalıdır, TestServer'da oluşturulamaz ve sayan akışın birim testiyle ölçülür (AC-35).

**Akış ve bütçe.**
- Yükleme `MultipartReader` ile okunur; denetleyicide `IFormFile`, `IFormCollection`, `[FromForm]` ve `Request.ReadFormAsync` yoktur; çerçevenin geçici dosyası oluşmaz. Form alanları (`owningItemId`, `owningVersionId`, `companyId`, `scope`) dosya bölümünden **önce** gelir, toplamı en çok 16 KiB (aşarsa 400); dosya bölümü adı `file`, tek. Kurallar §12.
- 2 sn bütçe yalnız başlık ön kontrolü + kimlik doğrulama + kapalı liste aşamasıdır; aşılırsa 504 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TIMEOUT` ve depolama çağrılmaz (emsal `ExecuteWithBudgetAsync`). Form alanlarının okunması ve içerik kopyası bu bütçeye **dahil değildir** (alanlar 16 KiB, içerik `MaxFileBytes` ile sınırlı; istek zaman aşımı Kestrel'indir). Emsal yürütücünün 32 KiB gövde sınırı dosya için kullanılmaz.

**İptal ve kesik gövde (r5, inceleme 01 tur 2 F-5).**
- **Belirteç:** yürütücü `ISender.Send`'e `HttpContext.RequestAborted`'ı geçirir (dört çağrı için aynı belirteç). Handler bu belirteci servise ve sağlayıcıya iletir; başka belirteç üretilmez.
- Ölçüm (pipeline sırası `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs` satır 45–49: `ValidationBehavior` → `LoggingBehavior` → `ExceptionBehavior` → `AuditBehavior` → `PerformanceBehavior`; ilk kayıtlı en dıştadır):
  - Sağlayıcı `OperationCanceledException`'da, belirteç tetiklenmişse yarım dosyayı siler ve istisnayı yeniden atar (`LocalFileSystemContentStorageGateway`, satır 113–117).
  - `AuditBehavior` bu istisnayı kaydetmeden geçirir (`AuditBehavior.cs` satır 59–62).
  - `ExceptionBehavior` her istisnayı yakalar, hata loglar ve `Response<T>`'yi yansımayla `Fail(string, int)` imzasıyla arar. Gerçek imza dört parametrelidir (`Response.cs`, BL-040 notu), bulunamaz ve istisna yeniden atılır.
  - Sonuç: istisna yürütücüye ulaşır ve r4'ün "503 ve tek `Failed` kaydı" sonucu oluşmaz.
- **Dal 1 — iptal belirteci tetiklendi** (istemci bağlantıyı kopardı; `HttpContext.RequestAborted.IsCancellationRequested`).
  - Tek kaydı yürütücü yazar: `catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)` → `TrustedDocumentRepositoryRefusalAudit`, kimliği doğrulanmış kayıt, token kiracısında.
  - Kayıt: `Outcome = Failed`, `Operation` = komutun işlemi (yükleme `Create`, telafi `Delete`), metadata `ReasonCode = DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CLIENT_ABORTED` (yeni kod, §13).
  - Yazım `CancellationToken.None` ile yapılır: `AuditService.AppendAsync` belirteci `IAuditOutboxWriter.TryEnqueueAsync`'e iletir ve `OperationCanceledException`'ı yeniden atar (ölçüldü); iptal edilmiş istek belirteciyle kaydın düşmemesi yazıcının davranışına bırakılmaz.
  - Yanıt gövdesi yazılmaz (istemci yok), satır yok. İstisna yürütücüde biter, çerçeveye atılmaz.
  - Sorgularda (meta / içerik) istisna yine yürütücüde biter ama iptal kaydı yazılmaz: başarılı meta okuması zaten kaydedilmez, içerik okumanın kaydı akış açılınca yazılmıştır (§21).
- **Dal 2 — belirteç tetiklenmeden gövde okuması `IOException` ile bitti** (kesik gövde, bağlantı sıfırlandı).
  - Sağlayıcının `IOException` dalı yarım dosyayı siler ve 503 `STORAGE_UNAVAILABLE` döner; satır yok.
  - Tek kaydı `AuditBehavior` yazar: `Failed`, metadata `ResponseStatusCode = 503`. Yanıt 503 `STORAGE_UNAVAILABLE`.
  - Sayan akışın bayrağı kalkık değilse handler 503'ü çevirmez.
- `RequestAborted` dışından gelen bir `OperationCanceledException` yakalanmaz (beklenmeyen; çerçeveye gider).

**Ret denetimi ve kayıt kiracısı (r4, inceleme 01 F-3 — CT kararı (c); r5, tur 2 F-2 / F-4 / F-5).**
- Ölçüm: `/api/internal` kiracı çözümünü atlar; `AuditService.ResolveTenantId` kiracı bağlamı çözülmemişse kaydı reddeder ("Audit tenant context is not resolved."), `IsPlatformGlobal = true` ise kaydı platform denetim kiracısına (`AuditTenantIds.PlatformSystemTenantId`, `…2121`) yazar.
- **Ret başına tek yazar (r5, F-4).** Her sonucun tek yazarı, `Outcome`'u ve kayıttaki ret kodu aşağıdaki tablodadır. Her satır tam bir kayıttır; kimliksiz satırlarda dakikada 60 sınırı vardır (aşağıda), kimliği doğrulanmış satırlar sınırlanmaz.

| Sonuç | Nerede oluşur | Tek yazar | `Outcome` / `Operation` | Kiracı ve aktör | Kayıttaki ret kodu |
|---|---|---|---|---|---|
| Başlık ön kontrolü: 401 / 403 / 413 | Yürütücüde, kimlik doğrulamadan önce | ret yazıcısı, kimliksiz | 401 ve 403 `Denied`, 413 `Failed` / `PermissionDenied` | `PlatformSystemTenantId`, `System`, açık aktör alanları | metadata `ReasonCode`: `…_UNAUTHENTICATED`, `…_FORBIDDEN` ya da `…_TOO_LARGE` |
| `AuthenticateAsync` reddi: 401 (kullanıcı JWT'si dahil) | Yürütücüde | ret yazıcısı, kimliksiz | `Denied` / `PermissionDenied` | aynı | `…_UNAUTHENTICATED` |
| Yapılandırma 503, bütçe 504 | Yürütücüde | **yok** — uyarı logu | — | — | — |
| Talep / kapalı liste / scope: 403 | Yürütücüde, komuttan önce (scope dosya bölümünden önce, form alanları okununca) | ret yazıcısı, kimliği doğrulanmış | `Denied` / `PermissionDenied` | token kiracısı (`TenantScope.Begin`), `Service`, `ActorId = ClientId` | `…_FORBIDDEN` |
| Multipart biçimi, 16 KiB, sınır değerleri: 400 | Yürütücünün ayrıştırıcısında, komuttan önce | ret yazıcısı, kimliği doğrulanmış | `Failed` / `PermissionDenied` | aynı | `VALIDATION_FAILED` |
| Doğrulayıcı (`StoreTrustedRepositoryObjectValidator`): 400 | `ValidationBehavior` `ValidationException` atar; `AuditBehavior`'dan önce koştuğu için `AuditBehavior` hiç çalışmaz | ret yazıcısı, kimliği doğrulanmış: yürütücü `catch (FluentValidation.ValidationException)` ile yakalar, gövdeli `Response` 400 `VALIDATION_FAILED` döner (`GlobalExceptionHandler`'ın `ValidationProblemDetails`'i değil) ve kaydı yazar | `Failed` / `PermissionDenied` | aynı | `VALIDATION_FAILED` |
| Meta ya da içerik 404 (tüketici bağı / kiracı / fiziksel nesne yok) | Sorgu handler'ında, satır okunduktan sonra. Sorgular `AuditBehavior`'da kaydedilmez (`IsQueryRequest`) | ret yazıcısı, kimliği doğrulanmış: yürütücü sorgu yanıtında 404 `NOT_FOUND_NON_LEAKAGE` görünce yazar | `Denied` / `PermissionDenied` | aynı | `NOT_FOUND_NON_LEAKAGE` |
| Telafi 404 | Yürütücünün ön okumasında (F-3, "Telafi tekrarı"); komut gönderilmez | ret yazıcısı, kimliği doğrulanmış | `Denied` / `PermissionDenied` | aynı | `NOT_FOUND_NON_LEAKAGE` |
| Sağlayıcı 400 (uzantı / medya türü / boş içerik) | Komutun içinde | `AuditBehavior` | `Failed` / `Create` | token kiracısı, `Service` (`HttpContext.User` servis principal'ı) | `ResponseStatusCode = 400` |
| Akış içi 413 | Komutun içinde; handler çevirir (yukarıda "Boyut" (b)) | `AuditBehavior` | `Failed` / `Create` | aynı | `ResponseStatusCode = 413` |
| Sağlayıcı 503 (G/Ç, kesik gövde) | Komutun içinde | `AuditBehavior` | `Failed` / `Create` | aynı | `ResponseStatusCode = 503` |
| İstemci iptali (`RequestAborted`) | Komutun içinde `OperationCanceledException`; `AuditBehavior` kaydetmez | ret yazıcısı, kimliği doğrulanmış: yürütücünün iptal dalı, `CancellationToken.None` ile | `Failed` / `Create` ya da `Delete` | token kiracısı, `Service` | `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CLIENT_ABORTED` |

- **Yürütücünün ret yazıcısını çağırdığı yerler.**
  - Komut ya da sorgu gönderilmeden önceki her ret.
  - `ValidationException` ve iptal `OperationCanceledException` yakalamaları.
  - Sorgu yanıtının 404'ü.
- **Çift kayıt yok.** Yürütücü komut yanıtına bakıp ret yazıcısını **hiç çağırmaz**: komut içindeki her sonucu yalnız `AuditBehavior` yazar. Savunma: telafi handler'ı yine de 404 dönerse (ön okumadan sonra oluşamaz — satır silinmez, modül ve scope değişmez) tek kaydı `AuditBehavior` yazar (`Failed`, `ResponseStatusCode = 404`); ret yazıcısı çağrılmaz.
- **`AuditBehavior` satırlarında ret kodu yoktur.** Ölçüm: `BuildLogicalFailureMetadata` yalnız `StatusCode` ve `Errors`'u okur (`AuditBehavior.cs` satır 293–311); `Response<T>.ReasonCode` kayda girmez. `AuditBehavior` korunan yoldur (§6) ve değişmez. Bu yolda durum kodu ret kodunu tek anlamlı belirler: 400 → `VALIDATION_FAILED`, 413 → `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE`, 503 → `STORAGE_UNAVAILABLE`. AC'ler bu satırlarda `ResponseStatusCode`'u ölçer.
- **Aktör alanları (r5, F-2).**
  - Ölçüm, iki adım:
    - `AuditService.BuildPayload` istekte null kalan `ActorId`, `ActorEmail` ve `ActorDisplayName`'i `??` ile `ICurrentUserContext`'ten doldurur.
    - `CurrentUserContext` bu değerleri okuma anında `HttpContext.User`'dan alır. Varsayılan şema kullanıcı JWT'sini `UseAuthentication`'da doğrular; bu durumda `HttpContext.User` kullanıcıdır.
  - **Mekanizma:** ret yazıcısı bu üç alanı **hiçbir zaman null bırakmaz**, böylece `??` dolgusu devreye girmez. `ActorType`'ı da sabit yazar: `AuditActorTypeResolver.ForCommand(principal)` çağrılmaz, çünkü kullanıcı JWT'sinde `TenantUser` döner.
  - **Kimliksiz:** `ActorType = AuditActorType.System`, `ActorId = Guid.Empty`, `ActorEmail = string.Empty`, `ActorDisplayName = "system"`. Kayıtta:
    - `ActorId` null olur (`AuditOutboxPayloadMapper.ReadOptionalGuid` `Guid.Empty`'yi null okur).
    - `ActorEmailMasked` null olur (`AuditOutboxPayload.MaskEmail("")`).
    - `ActorDisplayNameMasked = "s***m"` olur. Bu, `CurrentUserContext.ActorName`'in kimliksiz değeri `"system"`'in maskesiyle aynıdır.
  - **Kimliği doğrulanmış:** `ActorType = AuditActorType.Service`, `ActorId = identity.ClientId`, `ActorEmail = string.Empty`, `ActorDisplayName = string.Empty` (kayıtta ikisi de null).
  - Kullanıcı JWT'li kimliksiz ret (AC-24'ün vakası) kayda kullanıcının `sub`'ını, e-postasını ve adını yazmaz (AC-26).
- **Kimliksiz retler** (başlık ön kontrolünün 401 / 403 / 413'ü ve doğrulama 401'i): `IsPlatformGlobal = true`, `ActorType = System` (doğrulanmış principal yok; sabit yazılır, yukarıda), metadata yalnız uç + ret kodu; doğrulanmamış kiracı / özne / token yazılmaz. **Sınırlanır** (CT kararı (c), MOD-0040-FU02 K-10 / K-13 deseni): yazımdan önce yürütücü içi sabit pencereli sınırlayıcıdan izin istenir (`System.Threading.RateLimiting.FixedWindowRateLimiter`, emsal `PeopleSearchRateLimit`; tek bölüm `unauthenticated`, dakikada 60 kayıt); izin yoksa kayıt **yazılmaz**, uyarı logu + `Meter` sayacı `document_repository_trusted_refusals_unrecorded_total{reason="rate_limited"}` (emsal `WorkflowStartRecoveryMetrics`'in `Meter`'ı). Cevap her iki durumda aynıdır. Bu, AUD-001 §3 "Reddedilen komut da kaydedilir" kuralından **adlı sapmadır** — sahip Beste (CT kararı 2026-11-02).
- 503 (yapılandırma) ve 504 (bütçe) servis hatasıdır, ret değildir: denetim kaydı yok, uyarı logu.
- Denetim yazımı başarısızsa ret aynı kodla kalır.

**Telafi tekrarı (r4, inceleme 01 F-11).**
- Ölçüm: `RepositoryObjectRepository.MarkCompensatedAsync` `IsDeleted = true` yazar; `ExecutionFilter` `IsDeleted == false` ister — bugün ikinci telafi satırı görmez → 404.
- DL-2'de güvenilen telafi **tekrar güvenlidir**: aynı kiracı ve aynı `TrustedConsumerModule` için zaten telafi edilmiş satır 204 döner, sağlayıcı silmesi tekrar çağrılmaz, denetime metadata `alreadyCompensated = true` ile tek kayıt yazılır. Depoya telafi edilmiş satırı da okuyan tek okuma yöntemi eklenir (`IRepositoryObjectRepository.GetByContentIdIncludingCompensatedAsync`; yalnız güvenilen telafi kullanır). Başka modülün / kiracının satırı 404 kalır. MOD-0217'nin "telafi tam bir kez" kuralı tüketici tarafının çağrı sayısıdır; yanıt kaybında tekrar Platform'da zararsızdır (S-5, Treasury CT teyidi).
- **`alreadyCompensated`'ın mekanizması (r5, inceleme 01 tur 2 F-3).**
  - Ölçüm: `AuditBehavior.BuildAuditPlan` metadata'yı handler'dan **önce**, komutun parametresiz `GetAuditMetadata()`'sından alır (`AuditBehavior.cs` satır 126–143). Başarılı yanıta metadata eklenmez; `BuildLogicalFailureMetadata` yalnız `Failed` için koşar. Handler'ın vardığı sonuç kayda bu yolla giremez.
  - **Seçilen yol: yürütücüde ön okuma + komut alanı.**
    - Yürütücü `TenantScope.Begin(_tenantContext, identity.TenantId)` içinde, komutu göndermeden önce satırı `GetByContentIdIncludingCompensatedAsync(contentId)` ile okur ve görünürlüğü uygular: aynı kiracı, `TrustedConsumerModule == girdinin ConsumerModule'ü`, `Scope ∈ girdinin Scopes`'u.
    - Satır yoksa ya da görünmüyorsa: 404 `NOT_FOUND_NON_LEAKAGE`; yazar ret yazıcısıdır ("Ret başına tek yazar" tablosu); komut gönderilmez.
    - Satır görünüyorsa yürütücü `CompensateTrustedRepositoryObjectCommand`'ı `AlreadyCompensated = row.IsDeleted` ile gönderir. `GetAuditMetadata()` `alreadyCompensated`'ı bu alandan yazar.
    - Anlamı: **"bu çağrının ön okumasında satır zaten telafi edilmişti."**
  - **Handler ve yarış kuralı.**
    - Handler satırı aynı yöntemle yeniden okur ve görünürlüğü yeniden uygular. Kararı kendi okumasına göre verir: satır telafi edilmişse sağlayıcıya gitmez ve 204 döner.
    - Sağlayıcı silmesi (`TryDeleteAsync`) yalnız atomik `MarkCompensatedAsync`'i kazanan çağrıda yapılır (`ModifiedCount > 0` → `true`).
    - Ölçüm: `RepositoryObjectRepository.MarkCompensatedAsync` bu `bool`'u döndürür; `CompensateRowAsync` bugün dönüşü yok sayar ve silmeyi her durumda çağırır.
    - DL-2'de `CompensateRowAsync` silmeyi bu dönüşe bağlar; kural iki yolda da (kullanıcı ve güvenilen) geçerlidir.
  - **Zamanlama durumları** (her biri ayrı test, AC-33):
    1. Ön okumada satır telafi edilmiş (sıralı ikinci çağrı): handler sağlayıcıya gitmez, yanıt 204. Kayıt 1, `alreadyCompensated = true`.
    2. Ön okumada satır telafi edilmemiş ve handler `MarkCompensatedAsync`'i kazanır: `TryDeleteAsync` 1 kez çağrılır, yanıt 204. Kayıt 1, `alreadyCompensated = false`.
    3. Örtüşme: iki çağrı da ön okumada satırı telafi edilmemiş görür.
       - `MarkCompensatedAsync` birinde `true`, ötekinde `false` döner. Ön okumadan sonra handler'ın okumasında satırı telafi edilmiş bulan çağrı da bu durumdadır.
       - Kaybeden çağrı silme yapmaz ve 204 döner. Uyarı logu yazar: `compensation race lost`, `ContentId` ve `CorrelationId` ile.
       - İki kayıt yazılır, ikisi de `alreadyCompensated = false`. `TryDeleteAsync` toplamda 1 kez çağrılır.
       - **Kabul edilen sınır:** metadata handler'dan önce sabitlenir, bu yüzden kayıt ön okumanın anlamını taşır. Yarışı kaybeden çağrıyı yalnız uyarı logu ayırır.
       - Zorlama: deterministiktir, gerçek eşzamanlılık beklenmez. Sahte depo ön okumada telafi edilmemiş satır verir, handler'ın `MarkCompensatedAsync` çağrısında `false` döner.
  - **Kullanıcı yolu (DL-1).**
    - İkinci telafi bugünkü gibi 404 döner (`GetByContentIdAsync` `IsDeleted == false` ister); yanıt ve kayıt değişmez.
    - **Değişir:** örtüşen iki kullanıcı telafisinde ikinci sağlayıcı silmesi artık çağrılmaz (`MarkCompensatedAsync` dönüşü). İki yanıt bugünkü gibi 204 kalır.

**Sınır değerleri (r4, inceleme 01 F-12).**
- `scope` yalnız enum adı, büyük-küçük harf duyarlı sıralı karşılaştırma (`Documents`); sayısal değer (`0`), `documents`, baş / son boşluk → 400 (`Enum.TryParse` sayısal kabul ettiği için kullanılmaz).
- Guid alanları yalnız `D` biçimi (`Guid.TryParseExact(…, "D")`), `Guid.Empty` → 400.
- Boş kapalı liste → 503 (yukarıda).

**Ağ yolu.** `api/internal/*` Gateway'den yönlendirilmez (ölçüldü: `ocelot.json`'da `api/internal` 0 eşleşme; `LoginOnlyEndpointGuardTests` anonim iç denetleyiciyi `api/internal` altında ister). MdmService ucu Platform'un iç adresinden doğrudan çağırır (emsal: MDM `PlatformProductIdentityWorkflowClient` + `PlatformBaseUrl`). MOD-0217 Runtime Constraints "Gateway üzerinden" diyor → açık soru S-1 (Treasury CT, §19).

---

## 9. Layout & Shell Contract

`shell: none` — backend-only. No Razor view, no layout, no `wwwroot` asset, no RESX. The Blueprint soft pages (Repository Admin, Document Binary Store, Repository Health, Evidence Package Storage) belong to **FU02 / FU03 / FU04**, whose packs set their own `golden_reference` and `form_field_count`.

⚠️ Blueprint's `Module Pages` sheet still lists MOD-0262's **old** external-provider pages and contradicts `Blueprint_Data`'s four soft pages (**EA-BP-001**). FU01 is unaffected because it delivers no page; FU02/FU03/FU04 must not be authored until that contradiction is resolved.

---

## 10. Backend File Convention

`golden_reference: none`, so the Slim/Compact **frontend** contract does not apply. The backend layout still follows `module-pack-standard.md` §4 (ölçülmüş DL-1 adları + DL-2 adları):

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/
├── Commands/
│   ├── StoreRepositoryObjectCommand.cs
│   ├── CompensateRepositoryObjectCommand.cs          (compensation only — NOT purge)
│   ├── StoreTrustedRepositoryObjectCommand.cs        (DL-2)
│   └── CompensateTrustedRepositoryObjectCommand.cs   (DL-2; compensation only)
├── Queries/
│   ├── GetRepositoryObjectByIdQuery.cs
│   ├── OpenRepositoryObjectContentQuery.cs
│   ├── GetTrustedRepositoryObjectByIdQuery.cs        (DL-2)
│   └── OpenTrustedRepositoryObjectContentQuery.cs    (DL-2)
├── Handlers/
│   ├── CommandHandlers/                              ← separate folder (mandatory)
│   └── QueryHandlers/                                ← separate folder (mandatory)
├── Services/
│   ├── DocumentRepositoryService.cs
│   └── DocumentRepositoryAccessPolicy.cs
├── Validators/
│   ├── StoreRepositoryObjectValidator.cs
│   └── StoreTrustedRepositoryObjectValidator.cs      (DL-2)
└── DocumentRepositoryModels.cs                       ← all DTOs in ONE file
```

r1 tablosundaki `DeleteRepositoryObjectCommand` kodda `CompensateRepositoryObjectCommand` adıyla yazıldı (ölçüldü; davranış aynı: telafi, purge değil). Handler/validator names carry **no** `Command` / `Query` / `Request` suffix. One public type per file. DL-2'nin API katmanı dosyaları (`Controllers/Internal/`, `Security/`, `Configuration/`) §5'te.

---

## 11. Frontend File Contract

**Not applicable** — `shell: none`, `golden_reference: none`, `form_field_count: 0`. No `verify_datatable_page.py` run and no `quality-gate-datatable` step is required for FU01 (they become mandatory for FU02/FU03).

---

## 12. Validation Rules

| Rule | Behaviour |
|---|---|
| File name | Sanitised before use; path separators and traversal sequences rejected. |
| Extension | Must be in the configured allow-list → `400` + `ValidationFailed`. |
| Media type | Must be in the configured allow-list → `400`. |
| Empty content | Rejected → `400`. |
| Size | `> MaxFileSizeBytes` → `400`. |
| Checksum | Computed by the store; a caller-supplied checksum is **ignored**, never trusted. |
| `TenantId` | Required and taken from the **authenticated context**, never from the request body. DL-2: yalnız servis token'ının `tenant_id`'si; `X-Tenant-Id` başlığı → 403; gövde / form `tenantId` alanı → 400. |
| `ObjectKey` | Never accepted as input and never returned in any response. |
| Scope | Must be a known `ContentStorageScope` value. DL-2: yalnız enum adı, büyük-küçük harf duyarlı sıralı karşılaştırma (`Documents`); sayısal (`0`), `documents`, boşluklu → 400; bilinen ama girdinin `Scopes` listesinde olmayan değer (v1'de `Documents` dışı) → 403. |
| DL-2 multipart biçimi | `Content-Type: multipart/form-data` ve sınır (boundary) zorunlu → değilse 400. Tam bir dosya bölümü `file`; iki dosya → 400. Form alanları `owningItemId`, `owningVersionId`, `companyId`, `scope` dosyadan önce, her biri tam bir kez, toplamı ≤ 16 KiB; eksik / tekrar / dosyadan sonra gelen / bilinmeyen alan ya da 16 KiB aşımı → 400 ve depolama çağrılmaz. |
| DL-2 Guid alanları (`owningItemId`, `owningVersionId`, `companyId`) | Yalnız `D` biçimi (`Guid.TryParseExact(…, "D")`), `Guid.Empty` değil → değilse 400. |
| DL-2 boyut | Dosya ≤ `MaxFileBytes = 52_428_800` bayt; istek gövdesi ≤ `MaxRequestBytes = 53_477_376` bayt; aşım → 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE` (§8.1 "Boyut"). |

---

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Unauthenticated upload | `401` — enforced by the store, not by the caller (AD-5). |
| Authenticated but missing `…repository.manage` | `403`. |
| Tenant A requests tenant B's `ContentId` | `404` (not `403` — existence is not disclosed). |
| Tenant A supplies tenant B's raw `ObjectKey` | `404`; the key is not an addressable input. |
| Disallowed extension / media type / empty / oversize | `400` + reason code. |
| Metadata write fails after the bytes land | Compensation calls `TryDeleteAsync`; on `false`, an orphan-cleanup follow-up is recorded (never silently swallowed). |
| Physical object missing on read | `404`; the orphan is reportable (consumed by FU03). |
| Standalone Mongo (no replica set) | Transaction guard engages; no `-532462766` crash. |
| Attempt to reach a purge/destroy path | **No such path exists in FU01.** Asserted by test. |
| DL-2: `Authorization` yok / Bearer biçimsiz | Yürütücü (başlık ön kontrolü): gövdeli 401 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAUTHENTICATED`; komut çalışmaz; kimliksiz ret kaydı (sınırlı, §8.1). |
| DL-2: token imzası / ömrü / talep sözleşmesi geçersiz, ya da başka audience (`TRUSTED_WORKFLOW_CONSUMER`, `TRUSTED_AUDIT_SOURCE_INGEST`, `TRUSTED_REFERENCE_DATA_CONSUMER`) ya da kullanıcı JWT'si | Yürütücüde `AuthenticateAsync` başarısız: gövdeli 401 `…_UNAUTHENTICATED`; kimliksiz ret kaydı (sınırlı). Kullanıcı JWT'sinde de kayıt `ActorType = System`'dir; kullanıcının `sub`'ı, e-postası ve adı kayda yazılmaz (aktör alanları açık doldurulur, §8.1; r5 F-2). |
| DL-2: doğrulayıcı reddi (`StoreTrustedRepositoryObjectValidator`) | Yürütücü `ValidationException`'ı yakalar: gövdeli 400 `VALIDATION_FAILED` (ProblemDetails değil); depolama çağrılmaz; ret yazıcısı tek `Failed` kaydı yazar (`AuditBehavior` çalışmaz — `ValidationBehavior` ondan önce koşar; r5 F-4). |
| DL-2: çift `Authorization`, `X-Tenant-Id` var, `X-Delegated-Authorization` var | Gövdeli 403 `…_FORBIDDEN`; gövde okunmaz; kimliksiz ret kaydı (sınırlı). |
| DL-2: `Content-Length` > `MaxRequestBytes` | Gövdeli 413 `…_TOO_LARGE`; gövde okunmaz; kimliksiz ret kaydı (sınırlı). |
| DL-2: kimliksiz retler dakikada 60'ı aştı | Cevap aynı; kayıt yazılmaz, uyarı logu + `document_repository_trusted_refusals_unrecorded_total{reason="rate_limited"}` (AUD-001 §3'ten adlı sapma, sahip Beste). |
| DL-2: doğrulanmış token'da talep eksik / çift / biçimsiz | 403 `…_FORBIDDEN`; token kiracısında `Denied` kaydı. |
| DL-2: token geçerli, `ClientId` kapalı listede yok | 403 `…_FORBIDDEN`; token kiracısında `Denied` kaydı. |
| DL-2: scope girdinin listesinde değil (ör. `Templates`, `CrmVisitEvidence`) | 403 `…_FORBIDDEN`; depolama çağrılmaz; `Denied` kaydı. |
| DL-2: kapalı liste kural dışı (kod kümesi dışı modül / servis / audience / scope, aynı modül iki kez, aynı `ClientId` iki kez, 8'den fazla girdi) ya da `ContentStorageOptions.MaxFileSizeBytes < MaxFileBytes` | Uygulama açılmaz (`ValidateOnStart`). |
| DL-2: kapalı liste boş, ya da kimlik doğrulama şeması / anahtarı yapılandırılmamış (`InvalidOperationException`) | Gövdeli 503 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAVAILABLE` (MOD-0217 bunu 503 `MDM_CONTRACT_DOCUMENT_STORE_UNAVAILABLE`'a eşler); denetim kaydı yok, uyarı logu. |
| DL-2: başlık + kimlik doğrulama + kapalı liste aşaması 2 sn'yi aştı | Gövdeli 504 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TIMEOUT`; depolama çağrılmaz; denetim kaydı yok, uyarı logu (AC-27). |
| DL-2: dosya akışta `MaxFileBytes`'ı aştı | 413 `…_TOO_LARGE` (handler komutun içinde çevirir, §8.1 "Boyut" (b)); yarım dosya silinir, satır yok; tek `AuditBehavior` `Failed` kaydı, `ResponseStatusCode = 413` (r5 F-5). |
| DL-2: istemci bağlantıyı kopardı (`RequestAborted` tetiklendi) | Yanıt yok; yarım dosya silinir, satır yok; `AuditBehavior` kaydetmez; yürütücünün iptal dalı tek `Failed` kaydı yazar, `ReasonCode = DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CLIENT_ABORTED` (yeni kod), `CancellationToken.None` ile (r5 F-5). |
| DL-2: gövde belirteç tetiklenmeden kesildi (`IOException`) | 503 `STORAGE_UNAVAILABLE`; yarım dosya silinir, satır yok; tek `AuditBehavior` `Failed` kaydı, `ResponseStatusCode = 503`. |
| DL-2: başka modülün (`TrustedConsumerModule` farklı ya da boş) / kullanıcı yolunun / başka kiracının nesnesi | 404 `NOT_FOUND_NON_LEAKAGE`; token kiracısında tek `Denied` kaydı. Yazarı ret yazıcısıdır: meta / içerikte yürütücü sorgu yanıtının 404'ünü görünce, telafide yürütücünün ön okumasında (komut gönderilmez); `AuditBehavior` kayıt yazmaz (r5 F-4). |
| DL-2: istemci yenilendi (modül girdisinde yeni `ClientId`) | Yeni istemci eski istemcinin yazdığı nesneyi okur / telafi eder (görünürlük modüle bağlı); eski istemcinin token'ı 403. |
| DL-2: aynı nesne için ikinci güvenilen telafi | 204; sağlayıcı silmesi tekrar çağrılmaz; metadata `alreadyCompensated = true` ile tek kayıt (değer yürütücünün ön okumasından komut alanına; §8.1 "Telafi tekrarı", r5 F-3). |
| DL-2: aynı nesne için örtüşen iki güvenilen telafi | İkisi de 204; `MarkCompensatedAsync`'i kaybeden silmez (`TryDeleteAsync` toplam 1); iki kayıt, ikisi de `alreadyCompensated = false`; kaybeden uyarı logu `compensation race lost` yazar (kabul edilen sınır, §8.1). |
| DL-2: kullanıcı yolu tüketiciye bağlı nesneyi ister | 404 (platform yöneticisi token'ı dahil). |
| DL-2: yükleme yanıtı tüketiciye ulaşmadı (zaman aşımı / bağlantı kopması) | Nesne ve satırı kalır; tüketici işaretçi yazmaz. DL-2 idempotency anahtarı eklemez; bu nesne `TrustedConsumerModule` ile tüketiciye bağlı, işaretçisiz nesnedir — tespiti MOD-0262-FU03 mutabakatının işidir (açık soru S-2). |
| DL-2: denetim kaydı yazılamadı | Başarılı yazma geri alınmaz (K2 sınıfı değil: sözleşme belgesi; §21); uyarı logu (yol `a` davranışı). Ret her durumda ret kalır. |

---

## 14. Authorization Convention

Per **PKS-001** (`module.resource.action`, lowercase-dotted, ≥3 segments, hyphen-in-segment, closed action dictionary). `Diten.Platform` owns the `platform.*` namespace.

| Key | Action tier | Purpose |
|---|---|---|
| `platform.document-repository.read` | Tier 1 | Read pointer metadata / stream content. |
| `platform.document-repository.manage` | Tier 3 (`manage`, already in use) | Store objects, run compensation delete. |

**Reserved for later FUs — declared here so the namespace is not fragmented:** `platform.document-repository.health.read` (FU03), `platform.document-repository.evidence-package.read` / `.manage` (FU04), `platform.document-repository.purge.execute` (FU05).

- ⚠️ **AD-7:** `purge.execute` is a **separate** key; `manage` never implies it (SoD — destroying records is not administration).
- ⚠️ **AD-5:** the seam's current XML contract (*"Caller is responsible for all permission checks BEFORE invoking this"*) is **revoked**. Every entry point authorises independently; a remote caller's claim is never trusted.
- **OD-3 (kapandı, CT 2026-09-07 — §18):** `platform.document-repository.*`, LOCKED. (r1'de bu satır "open" diyordu ve §18 ile çelişiyordu; r2'de mekanik olarak düzeltildi, karar değişmedi.)
- Actor type: Platform actor. `Platform.*` PascalCase keys are **not** used (superseded by PKS-001).
- No permission **seed** is authorised by this pack; seeding is an implementation-time task with its own record.

**DL-2 — güvenilen tüketici yetkisi (KARAR D56/2).**
- Yeni izin anahtarı **yok**. Servis kimliği kullanıcı izniyle geçmez: kapı, yürütücü içindeki başlık ön kontrolü + açık `AuthenticateAsync("TrustedDocumentRepositoryConsumerService")` + kapalı tüketici listesi + scope listesi + modüle bağlı görünürlüktür (§8.1). Denetleyici sınıf düzeyinde `[AllowAnonymous]` taşır (r4, F-1); `[HasPermission]`, `[LoginOnly]` ve şema adlı `[Authorize]` yoktur.
- Bu, `LoginOnlyEndpointGuardTests.AnonymousInternalControllers`'ın gözden geçirilmiş istisna mekanizmasıdır ("Anonymous controllers. Each must sit under /api/internal … and check its caller INSIDE"; liste koşulu: "A controller enters this list only on those terms" — her ret dalı kaldırılınca adlı bir test kırmızı). Denetleyici bu listeye kapı ve kanıt notuyla tek ad olarak eklenir; `TrustedServiceSchemeControllers`'a eklenmez (`Every_authentication_scheme_gate_is_a_reviewed_internal_trusted_service_controller` şema adlı kapı bulamaz).
- Kullanıcılara `platform.document-repository.read` / `.manage` verilmez (D56/2; rol şablonları Auth'ta — Platform bunu zorlamaz, ama tüketiciye bağlı nesneyi kullanıcı yolundan gizleyerek indirmenin yalnız MOD-0217'den geçmesini sağlar). Ölçüldü: genel `read` / `.manage` kiracı rota düzeltmelerinde yok, PlatformAdmin kapsamında kalır (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/TenantRouteScopeCorrections.cs` → visit-evidence satırlarının üstündeki not).
- **Aktör:** çağrı **servis olarak** kaydedilir, kullanıcı adına değil (D56/2: kullanıcı token'ı iletilmez). Kullanıcı düzeyindeki iz MOD-0217'nindir (niyet kaydı, AC-62 indirme denetimi); Platform kaydı `CorrelationId` ile bağlanır. Platform doğrulanmamış bir "kullanıcı kimliği" başlığını kabul etmez ve kaydetmez.
- Aktör kaydı (K-B, §19): `ActorType = Service`, `ActorId = ClientId` (servis token'ının `sub`'ı; `AuditService` bunu `ICurrentUserContext.UserId`'den doldurur), `SourceService = "Diten.Platform"` (kaydı yazan servis; `AuditBehavior` sabiti), çağıran servis metadata'da `callerService = Diten.MDM`.

---

## 15. Gateway / API Routing Decision

- A new Ocelot route is required for `…/document-repository/*` → `Diten.Platform` (5057). Integration-agent task at implementation time. (DL-1: rota var — `/api/v1/document-repository` ve `/{everything}`.)
- ⚠️ Upload/download routes must be verified for **multipart and streaming** pass-through; the existing document path base64s through a proxy, which AD-4 retires.
- ⚠️ This repository's gateway performs **no** authentication (`AuthenticationOptions` count = 0 across 157 routes); auth is downstream. FU01 must therefore not assume any gateway-level protection.
- ⚠️ Proxy `204`/bodiless-status handling: the known `ForwardAsync` defect turns an upstream `204` into `500` by writing `"{}"`. Any new proxy action must use the `IsBodilessStatus` guard.
- **DL-2 kararı: Gateway değişikliği gereksiz.** Güvenilen tüketici ucu `api/internal/v1/document-repository/trusted-consumer/*` altındadır; `api/internal` Gateway'den yönlendirilmez (ölçüldü, §8.1) ve yönlendirilmemelidir — tarayıcı ya da kiracı token'ı bu uca ulaşmaz. `ocelot.json`'a dokunulmaz; integration-agent görevi yok. MdmService Platform'un iç adresini (yapılandırma, emsal `PlatformBaseUrl`) kullanır; adres yoksa MDM tarafı açılmaz (MOD-0217'nin kuralı). MOD-0217 metniyle hizalama açık soru S-1.

---

## 16. Acceptance Criteria

Her madde tek satırdır; dilim eşlemesi Teslim dilimleri bölümündedir. r1'in numarasız on dört maddesi sırasıyla numaralandı; metinleri değişmedi.

- [ ] AC-1 (zorunlu) — `IContentStorageGateway` and its request/result records no longer live under `Features/DocumentManagementControlledDocuments/`; they resolve from a shared contract location owned by MOD-0262.
- [ ] AC-2 (zorunlu) — All **eight** existing consumers (3 controllers + `DocumentVersioningService` + `ControlledDocumentRegistrationService` + `TemplateService` + `TemplateMasterService` + `TemplateVariantService`) compile and behave identically against the relocated seam (AD-2).
- [ ] AC-3 (zorunlu) — `ContentStoreRequest` no longer exposes `byte[] Content`; upload is multipart/stream and returns a `ContentRef` (AD-4). A test asserts **no** binary payload is carried in a JSON body.
- [ ] AC-4 (zorunlu) — Every repository entry point authorises independently; a test proves an unauthenticated and an under-privileged call are rejected **by the store** even when the caller claims prior authorisation (AD-5).
- [ ] AC-5 (zorunlu) — Two tenants storing an identically named file receive distinct object keys; cross-tenant read by `ContentId` **and** by raw `ObjectKey` both return `404`.
- [ ] AC-6 (zorunlu) — `ObjectKey` does not appear in any API response payload (asserted by serialisation test).
- [ ] AC-7 (zorunlu) — SHA-256 checksum is computed by the store; a caller-supplied checksum is ignored.
- [ ] AC-8 (zorunlu) — Store / read / compensation-delete each emit a MOD-0021 audit event.
- [ ] AC-9 (zorunlu) — `RepositoryObject` round-trips under `Diten.Platform`'s global `GuidSerializer(Standard)` (no `RegisterClassMaps` in this service); a test proves Guid FKs are queryable (not silently empty).
- [ ] AC-10 (zorunlu) — Mongo indexes created; no partial-index filter uses `$ne` / `$not`; the service starts cleanly on a standalone Mongo.
- [ ] AC-11 (zorunlu) — **No purge, destroy, scheduler or cascade path exists in FU01**; a test asserts `TryDeleteAsync` is reachable only from the compensation path (AD-6).
- [ ] AC-12 (zorunlu) — No `DOC-REPO-BUNDLE` payload is defined anywhere in this FU (EA-BP-003 guard).
- [ ] AC-13 (zorunlu) — `verify_module_id.py --check-id MOD-0262-FU01 --parent MOD-0262` exit 0 and `--check-all` exit 0 with 0 HARD violations.
- [ ] AC-14 (zorunlu) — No file under `frontend/` is touched; `services/Diten.CrmService/` and `services/Diten.EnterpriseStrategyService/` untouched.
- [ ] AC-15 (zorunlu) — DL-2 şeması: `TrustedServiceTokenValidationExtensions` içinde `TrustedDocumentRepositoryConsumerService` şeması yalnız `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` audience'lı, `HasExactServiceClaims` sözleşmesini sağlayan RS256 token'ı kabul eder; `TrustedServiceTokenValidationTests`'te dört yönlü audience ayrımı (bu şema workflow / audit / reference-data token'ını reddeder, diğer üç şema bu token'ı reddeder), HS256, `none`, süresi dolmuş, fazladan talepli ve `service_name` farklı token reddi ayrı vakalardır; sabotaj: şemanın `ValidAudience`'ı `WorkflowRequiredAudience` yapılınca test kırmızı.
- [ ] AC-16 (zorunlu) — DL-2 yerleşimi ve kapı (r4, F-1): `InternalTrustedDocumentRepositoryConsumerController` rotası `api/internal/v1/document-repository/trusted-consumer`, sınıf düzeyinde `[AllowAnonymous]`, şema adlı `[Authorize]`, `[HasPermission]` ve `[LoginOnly]` yok; `LoginOnlyEndpointGuardTests.AnonymousInternalControllers`'ta kapı ve kanıt notuyla tek ad, `TrustedServiceSchemeControllers`'ta yok; liste koşulu gerçek HTTP host'ta (`WebApplicationFactory`, gerçek `UsePlatformAccessPipeline`) ölçülür: yürütücünün her ret dalı (başlık ön kontrolü, `AuthenticateAsync` reddi, talep reddi, kapalı liste reddi, scope reddi, modül bağı) ayrı ayrı kaldırılınca adlı bir host testi kırmızı; sabotaj: denetleyiciye `[Authorize(AuthenticationSchemes = "TrustedDocumentRepositoryConsumerService")]` geri konunca AC-17'nin gövde ve kod beklentileri kırmızı, rota `api/v1` altına taşınınca guard testi kırmızı.
- [ ] AC-17 (zorunlu) — DL-2 başlık ve kimlik retleri gerçek HTTP host'ta, her biri gövdeli `Response` ve ret koduyla, ayrı vakalarda: `Authorization` yok → 401 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAUTHENTICATED`; geçersiz imzalı / süresi dolmuş / başka audience'lı servis token'ı → 401 aynı kod; çift `Authorization` → 403 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_FORBIDDEN`; `X-Tenant-Id` var → 403; `X-Delegated-Authorization` var → 403; `Content-Length` 53 477 377 → 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE`; şema anahtarı yapılandırılmamış host → 503 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAVAILABLE` (500 değil); hiçbirinde komut / sorgu çalışmaz ve depolama sağlayıcısı çağrılmaz (sahte sağlayıcı çağrı sayısı 0); sabotaj: `X-Tenant-Id` denetimi kaldırılınca ilgili vaka, `InvalidOperationException` yakalaması kaldırılınca 503 vakası kırmızı.
- [ ] AC-18 (zorunlu) — DL-2 kiracı: A kiracısının token'ıyla yüklenen nesne için B kiracısının token'ıyla meta, içerik ve telafi üçü de 404 döner ve A'nın nesnesi ile satırı değişmez; formda `tenantId` alanı 400 döner; sabotaj: kiracı form alanından okunacak biçimde değiştirilince test kırmızı.
- [ ] AC-19 (zorunlu) — DL-2 kapalı liste (r4, F-2): listede olmayan `ClientId` → 403 ve token kiracısında tek `Denied` kaydı; girdinin scope listesinde olmayan `Templates` ve `CrmVisitEvidence` ayrı vakalarda 403 ve depolama çağrısı 0; boş liste → geçerli token'la 503; başlatma doğrulaması ayrı fixture'larla host'u açtırmaz: `ConsumerModule = MOD-9999`, `ServiceName = Diten.Crm`, `Audience = TRUSTED_WORKFLOW_CONSUMER`, `Scopes = [Templates]`, aynı `MOD-0217` için iki girdi, aynı `ClientId` iki kez, 9 girdi, `ContentStorageOptions.MaxFileSizeBytes = 52_428_799`; sabotaj: koddaki sabit küme denetimi kaldırılınca `MOD-9999` vakası, modül tekilliği denetimi kaldırılınca iki-girdi vakası, audience denetimi kaldırılınca audience vakası kırmızı.
- [ ] AC-20 (zorunlu) — DL-2 akış: mimari test güvenilen denetleyicide `IFormFile` / `IFormCollection` parametresi, `[FromForm]` özniteliği ve `ReadFormAsync` çağrısı olmadığını, `MultipartReader` kullanıldığını doğrular; host testinde `IFormFeature.ReadFormAsync` çağrılınca istisna atan bir özellikle yükleme başarılı olur; sabotaj: `IFormFile` parametresi eklenince mimari test, form okunursa host testi kırmızı.
- [ ] AC-21 (zorunlu) — DL-2 multipart biçimi: dosyadan sonra gelen alan, eksik `companyId`, `Guid.Empty` `owningItemId`, tekrarlanan `scope`, bilinmeyen alan, iki dosya bölümü ve `boundary` parametresi olmayan içerik türü ayrı fixture'larla 400 döner ve her birinde depolama çağrısı 0, `RepositoryObject` koleksiyonunun satır sayısı değişmez.
- [ ] AC-22 (zorunlu) — DL-2 modüle bağlı görünürlük (r4, F-2): güvenilen yükleme satıra `TrustedConsumerModule = MOD-0217`, `TrustedConsumerClientId = sub`, `CreatedBy = service:Diten.MDM:{clientId}` yazar (gerçek Mongo testiyle okunur); aynı kiracıda kullanıcı yoluyla (`Documents`) yazılmış satır ve `TrustedConsumerModule` farklı satır güvenilen meta / içerik / telafide 404; istemci yenileme: A istemcisiyle yüklenen nesne, yapılandırmada `MOD-0217` girdisinin `ClientId`'si B yapılınca B token'ıyla meta / içerik / telafide başarılı, A token'ı 403; sabotaj: görünürlük `TrustedConsumerClientId == sub`'a bağlanınca yenileme vakası, `TrustedConsumerModule` eşleşmesi kaldırılınca farklı-modül vakası kırmızı.
- [ ] AC-23 (zorunlu) — DL-2 kullanıcı yolu dışlaması (K-C): `TrustedConsumerModule` dolu satır `api/v1/document-repository` meta, içerik ve telafide `platform.document-repository.read` + `.manage` taşıyan platform yöneticisi token'ıyla 404 döner; kullanıcı yolunun kendi satırları için DL-1 testleri (`DocumentBinaryStoreTests`, `CrmVisitEvidenceScopeTests`, `ContentMessagingArtifactsScopeTests`) yeşil kalır; sabotaj: dışlama kaldırılınca test kırmızı.
- [ ] AC-24 (zorunlu) — DL-2 kullanıcı izni gerekmez: izin talebi taşımayan geçerli servis token'ıyla yükleme, meta, içerik ve telafi başarılıdır; `tenant_user` JWT'si (`read` + `manage` izinli) güvenilen uca gönderilince — varsayılan şema onu `UseAuthentication`'da doğrulayıp `HttpContext.User` yapsa da — 401 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAUTHENTICATED` döner ve depolama çağrısı 0; sabotaj (r5, F-6): yürütücü `HttpContext.User`'ı (varsayılan şemanın principal'ını) kabul edecek biçimde değiştirilince (ör. `HttpContext.User.Identity.IsAuthenticated` ise `AuthenticateAsync` atlanır) kullanıcı JWT'si vakası kırmızı (r4'ün "denetleyiciye varsayılan JWT şeması eklenince" sabotajı gerçek değildi: sınıf düzeyi `[AllowAnonymous]` varsayılan şemalı `[Authorize]`'ın challenge'ını engeller, test yeşil kalırdı).
- [ ] AC-25 (zorunlu) — DL-2 başarı denetimi: güvenilen yükleme, telafi ve içerik okuma her biri gerçek HTTP host'ta tam bir merkezi denetim kaydı üretir (`ActorType = Service` — K-B sahip kararı A, `ActorId = ClientId`, `SourceService = Diten.Platform`, kiracı = token kiracısı, `EntityType = RepositoryObject`, `EntityId = ContentId`, `Outcome = Succeeded`, `CorrelationId = X-Correlation-Id`, metadata `callerService`, `consumerModule`, `audience`, `scope`, `operation`); kayıtta token, dosya içeriği ve `ObjectKey` yoktur; başarılı meta okuma kayıt üretmez (sayı 0 ile ölçülür); sabotaj: `AuditActorTypeResolver`'daki `"service"` eşlemesi kaldırılınca güvenilen yükleme vakasında denetim kaydı yazılmaz (`AuditService` `Unknown` aktörü reddeder, kayıt sayısı 0) ve test kırmızı.
- [ ] AC-26 (zorunlu) — DL-2 ret denetimi (r4 F-3; r5 F-2 / F-4) gerçek HTTP host'ta, §8.1 "Ret başına tek yazar" tablosunun yürütücü satırları ayrı testlerle ve her vakada toplam denetim kaydı sayısı ölçülerek: kimliği doğrulanmış retler — talep reddi 403, kapalı liste 403, scope 403, multipart 400, doğrulayıcı 400 (yürütücü `ValidationException`'ı yakalar; gövdeli `Response` 400 `VALIDATION_FAILED`, ProblemDetails değil; `AuditBehavior` kaydı 0), meta 404 ve içerik 404 (sorgu yanıtından), telafi 404 (ön okumadan; komut gönderilmez, `AuditBehavior` kaydı 0) — her biri token kiracısında `ActorType = Service`, `ActorId = ClientId` ile toplam **1** kayıt üretir (tablodaki `Outcome`; `ReasonCode` metadata'da; `TenantScope.Begin` kaldırılınca `AuditService` "Audit tenant context is not resolved." ile reddeder ve test kırmızı); kimliksiz retler (başlık ön kontrolü 401 / 403 / 413 ve `AuthenticateAsync` 401) `IsPlatformGlobal = true` ile `PlatformSystemTenantId` altında `ActorType = System` toplam 1 kayıt üretir (yalnız uç ve ret kodu; doğrulanmamış kiracı / özne / token yok); **kullanıcı JWT'li kimliksiz ret** (`sub`, `email` ve `name` talepli `tenant_user` JWT'si) kaydında `ActorType = System`, `ActorId` null, `ActorEmailMasked` null, `ActorDisplayNameMasked = "s***m"` ve metadata'da kullanıcının `sub`'ı ya da e-postası yoktur; aynı dakikada 61. kimliksiz ret kaydı yazmaz, aynı cevabı verir, uyarı logu ve `document_repository_trusted_refusals_unrecorded_total{reason="rate_limited"}` +1 olur; 503 ve 504 kayıt yazmaz; denetim yazımı başarısızken ret aynı durum koduyla kalır; komut içi sonuçlar AC-35'tedir; savunma vakası: telafi handler'ı 404 döndüren sahte servisle toplam kayıt 1'dir (`AuditBehavior` `Failed`, `ResponseStatusCode = 404`); sabotaj: sınırlayıcı kaldırılınca 61. vaka; ret yazıcısı aktör alanlarını null bırakınca kullanıcı JWT'li vaka (kayıtta kullanıcının `sub`'ı); `ActorType` `ForCommand(principal)`'dan alınınca aynı vaka (`TenantUser`); `ValidationException` yakalaması kaldırılınca doğrulayıcı vakası (ProblemDetails, kayıt 0); yürütücü komut yanıtının 404'ünde de ret yazıcısını çağırınca savunma vakası (kayıt 2) kırmızı.
- [ ] AC-27 (zorunlu) — DL-2 bütçe (r4, F-1 / F-12) gerçek HTTP host'ta: kimlik doğrulama aşaması 2 sn'yi aşınca (yavaş doğrulama sahtesi) gövdeli 504 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TIMEOUT` döner, depolama çağrılmaz, denetim kaydı yazılmaz; form alanlarının ve içerik kopyasının okunması bütçeye bağlı değildir — 3 sn boyunca parça parça gelen 5 MiB'lik yükleme ve 3 sn süren içerik akışı başarılı; form alanları bölümü 16 KiB'ı aşınca 400; sabotaj: bütçe kopyayı da kapsayacak biçimde değiştirilince 3 sn vakası kırmızı.
- [ ] AC-28 (zorunlu) — DL-2 kapsamı (r4, F-7): taban DL-2 dalının ayrıldığı main commit'idir (`git merge-base origin/main <dl-2 ucu>`); `git diff --name-only <taban>..<dl-2 ucu>` yalnız §5 DL-2 listesindeki yollardadır; `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `gateway/**` ve `frontend/**` altında değişen dosya 0'dır; MOD-0040-FU02 ile ortak dosyalarda (`LoginOnlyEndpointGuardTests.cs` `AnonymousInternalControllers`, `TrustedServiceTokenValidationExtensions.cs`, `AuditActorTypeResolver.cs`) ikinci giren dal main'i alır ve birleşimi korur — öbür paketin satırı silinmez, değiştirilmez, liste girdileri birleşimdir; birleştirmeden sonra iki paketin AC'leri (bu paket AC-16 / AC-31, MOD-0040-FU02'nin guard ve resolver'a dayanan AC'leri) yeşil.
- [ ] AC-29 (zorunlu) — DL-2 canlı kayıt (dilim kabul kapısı; Auth bağımlılığı gelince): dev ortamında Auth'un verdiği `Diten.MDM / TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` token'ıyla yükleme → meta → içerik → telafi zinciri, başka kiracı token'ıyla 404, kullanıcı yolundan 404 ve her adımın `audit_events` satırı kayıt dosyasında komut çıktısıyla gösterilir (MOD-0217 Ready-for-dev Checklist "MOD-0262-FU01 canlı doğrulama kaydı" maddesinin kanıtı).
- [ ] AC-30 (zorunlu) — DL-2 telafi sınırı: güvenilen telafi yalnız `CompensateRowAsync` yolunu çalıştırır (r5, F-3: sağlayıcı silmesi `MarkCompensatedAsync` dönüşüne bağlı), saklama / yasal tutma denetimi ve fiziksel imha yolu eklemez; AD-6 negatif yetenek testi güvenilen denetleyiciyi de kapsar (purge / destroy / delete adlı eylem 0).
- [ ] AC-31 (zorunlu) — DL-2 servis aktörü eşlemesi ve yan etkileri (K-B): `AuditActorTypeResolver.FromActorTypeClaim("service")` `AuditActorType.Service` döner, eşleme resolver'ın var olan tek switch'ine eklenir ve yeni bir eşleme dosyası doğmaz (`AuditActorTypeResolverSourceGuardTests` değişmeden yeşil — yalnız destekleyici kanıt: kişi türü eşlemesini arar, `Service` eşlemesini ölçmez; r4, F-5); `AuditBehaviorActorTypePipelineTests`'te `actor_type = service` principal'ıyla denetimli komut `Service` aktörlü tek kayıt yazar (`"service"` Unknown teorisinden çıkarılır, `null` / `""` / `"system"` / `"admin"` Unknown kalır); `DataExportAuditWriterTests`'in `"service"` ret vakası değişmeden yeşil — bu, DL-2'de **yazılacak** `ForDataExport` korumasının (`Service` → `Unknown`) ölçüsüdür, bugünkü kod onu eşleme olmadığı için geçer (r4, F-5); `CanonicalTransactionalAuditOutboxWriter` `service` principal'ında `Service` aktörlü ve `ActorId = sub` kayıt yazar (yeni test); sabotaj: eşleme kaldırılınca bu üç pozitif test ve AC-25'in yükleme vakası kırmızı, `ForDataExport` korumasından vazgeçilince ret vakası kırmızı.
- [ ] AC-32 (zorunlu) — DL-2 boyut (r4 F-4; r5 F-1) gerçek HTTP host'ta (TestServer): 52 428 800 baytlık dosya 201 Created (güvenilen yükleme 201 döner — MOD-0217 sahte bağımlılığı 201 bekler; DL-1 kullanıcı yolu bugün 200 döner, değişmez) ve satır yazılır; 52 428 801 baytlık dosya 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE`, satır yok ve depolama kökünde yarım dosya yok; `Content-Length` 53 477 377 gövde okunmadan 413; uç sınırı **uç metadata'sıyla** ölçülür: host'un `EndpointDataSource`'undan güvenilen yükleme ucu (`POST`, şablon `api/internal/v1/document-repository/trusted-consumer/objects`) bulunur ve `Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize == TrustedDocumentRepositoryUploadLimits.MaxRequestBytes` (53 477 376); TestServer `IHttpMaxRequestBodySizeFeature`'ı uygulamadığı için (§8.1 "Boyut") Kestrel sınırına dayanan host vakası yazılmaz, gerçek Kestrel testi isteğe bağlıdır; sabotaj: sayan akış kaldırılınca 52 428 801 vakası (sağlayıcının 256 MB sınırı nedeniyle 201) kırmızı; `[RequestSizeLimit]` kaldırılınca, değeri 30 000 000 yapılınca ya da uca `[DisableRequestSizeLimit]` eklenince metadata vakası kırmızı.
- [ ] AC-33 (zorunlu) — DL-2 telafi tekrarı (r4 F-11; r5 F-3), §8.1 "Telafi tekrarı"nın üç zamanlama durumu ayrı testlerle: (1) sıralı ikinci güvenilen telafi 204, sağlayıcının `TryDeleteAsync`'i iki çağrıda toplam 1 kez, ikinci kayıt metadata `alreadyCompensated = true`; (2) ilk telafinin kaydı `alreadyCompensated = false`, `TryDeleteAsync` 1; (3) örtüşme — sahte depo ön okumada telafi edilmemiş satır verir, handler'ın `MarkCompensatedAsync`'i `false` döner: yanıt 204, bu çağrıda `TryDeleteAsync` 0, kayıt `alreadyCompensated = false`, uyarı logu `compensation race lost`; kullanıcı yolunda (3)'ün eşi: `MarkCompensatedAsync` `false` → `TryDeleteAsync` 0, yanıt 204; başka modülün ya da başka kiracının telafi edilmiş satırı 404 (ön okuma, komut gönderilmez); kullanıcı yolunun sıralı ikinci telafisi 404 kalır; sabotaj: `GetByContentIdIncludingCompensatedAsync` yerine `GetByContentIdAsync` kullanılınca (1) 404 olur; `AlreadyCompensated` ön okuma yerine sabit `false` yazılınca (1)'in metadata'sı; `MarkCompensatedAsync` dönüşü yok sayılınca (3)'te `TryDeleteAsync` 1 olur ve test kırmızı.
- [ ] AC-34 (zorunlu) — DL-2 sınır değerleri (r4, F-12): `scope` için `Documents` geçer, `documents`, `0`, `" Documents"` ayrı vakalarda 400; Guid alanları için `N` biçimi (`32` hane), `Guid.Empty` ve boş değer ayrı vakalarda 400; her vakada depolama çağrısı 0; sabotaj: `Enum.TryParse` kullanılınca `0` vakası kırmızı.
- [ ] AC-35 (zorunlu) — DL-2 komut içi sonuçların kaydı (r5, F-4 / F-5) gerçek HTTP host'ta, her vaka ayrı testle ve toplam denetim kaydı sayısı ölçülerek: (a) akış içi aşım (52 428 801 bayt) → istemci 413 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TOO_LARGE`, tek `AuditBehavior` `Failed` kaydı `ResponseStatusCode = 413` (503 değil), ret yazıcısı kaydı 0, toplam 1; (b) sağlayıcı 400 (izin listesinde olmayan uzantı) → 400, tek `AuditBehavior` kaydı `ResponseStatusCode = 400`, toplam 1; (c) belirteç tetiklenmeden kesik gövde — test ara yazılımı gövdeyi ilk 1 MiB'dan sonra `IOException` atan akışla değiştirir → 503 `STORAGE_UNAVAILABLE`, tek `AuditBehavior` kaydı `ResponseStatusCode = 503`, satır ve yarım dosya yok, toplam 1; (d) istemci iptali — test ara yazılımı gövdeyi 1 MiB'dan sonra `HttpContext.Abort()` çağırıp `OperationCanceledException(HttpContext.RequestAborted)` atan akışla değiştirir (TestHost `IHttpRequestLifetimeFeature`'ı uygular, ölçüldü) → `AuditBehavior` kaydı 0, yürütücünün iptal dalının tek `Failed` kaydı `ReasonCode = DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CLIENT_ABORTED`, `Operation = Create`, token kiracısında `ActorType = Service`, `ActorId = ClientId`, satır ve yarım dosya yok, toplam 1; (e) sayan akışın birim testi: iç akış `BadHttpRequestException("…", 413)` atınca `LimitExceeded = true` olur ve istisna `IOException` olarak yeniden atılır, iç akış 413 dışı `IOException` atınca bayrak `false` kalır; sabotaj: handler'daki 413 çevirisi kaldırılınca (a)'nın kaydı `ResponseStatusCode = 503` olur; yürütücünün iptal dalı kaldırılınca (d)'nin toplamı 0 olur; sayan akışın 413 yakalaması kaldırılınca (e) kırmızı.

---

## 17. Test Expectations

| Layer | Expectation |
|---|---|
| Unit | Validation matrix (§12), object-key determinism + tenant separation, checksum computation, compensation-on-metadata-failure. DL-2: kapalı liste yapılandırma doğrulaması (geçerli / kod kümesi dışı / tekrar / 8'den fazla girdi), `DocumentRepositoryAccessPolicy` tüketici satırı dışlaması, multipart alan sırası ayrıştırıcısı, sayan akış (`LimitExceeded`, Kestrel 413 yakalaması; AC-35 (e)), telafi handler'ının `MarkCompensatedAsync` dönüşüne bağlı silmesi (AC-33 (3)). |
| Integration (Mongo) | Must use `MongoIntegrationHarness`. Index creation, class-map round-trip, standalone-Mongo transaction guard. DL-2: iki yeni alanın gerçek Mongo'da yazılıp okunması (AC-22), eski satırda `null` okunması. |
| Authorization | Unauthenticated `401`; under-privileged `403`; cross-tenant `404` by `ContentId` and by raw `ObjectKey`. DL-2: AC-15…AC-19, AC-22…AC-24, AC-26, AC-27, AC-32…AC-35 — **gerçek HTTP host** (`WebApplicationFactory`, üretimdeki `UsePlatformAccessPipeline` sırası korunur; emsal `Audit/AuditBehaviorActorTypeHttpTests.cs`, `Authorization/AuthorizationProbeControllerIntegrationTests.cs`), test anahtarıyla imzalı RS256 token. r4 (F-1): kapı yürütücüde olduğu için 401 / 403 / 413 / 503 / 504 gövde ve kodları yalnız host testiyle kanıt sayılır; `DefaultHttpContext` birim testi (`TrustedWorkflowConsumerSecurityTests` deseni) yalnız destekleyicidir. r5 (F-1): bu "gerçek HTTP host" TestServer'dır (`Microsoft.AspNetCore.TestHost` 8.0.8); `[RequestSizeLimit]` ve Kestrel'in gövde sınırı orada uygulanmaz — uç sınırı uç metadata'sıyla ölçülür (AC-32), gerçek Kestrel testi isteğe bağlıdır. Kesik gövde ve iptal, gövdeyi değiştiren test ara yazılımıyla zorlanır (AC-35). |
| Contract | Serialisation test: `ObjectKey` absent from responses; no `byte[]` in any request/response DTO. DL-2 yanıtları aynı `RepositoryObjectModel`'dir. |
| Audit | DL-2: AC-25, AC-26, AC-31, AC-33, AC-35 — gerçek `AuditBehavior` + `AuditService` ile kayıt sayımı (sahte değil); her ret ve komut içi sonuç için §8.1 "Ret başına tek yazar" tablosunun toplam kayıt sayısı (çift kayıt 0); ret yazıcısının açık aktör alanları (kullanıcı JWT'li kimliksiz ret); kayıt kiracısı (token kiracısı / `PlatformSystemTenantId`); kimliksiz ret sınırlayıcısı ve `Meter` sayacı; `"service"` eşlemesi sabotajı; `AuditBehaviorActorTypePipelineTests`, `DataExportAuditWriterTests` ve transactional yazıcı için servis principal'ı vakaları. `AuditActorTypeResolverSourceGuardTests` (destekleyici) ve `AuditTrailStandardTests` yeşil. |
| Negative-capability | Assert the absence of a purge/destroy code path (DL-2 denetleyicisi dahil, AC-30). |
| Architecture | DL-2: AC-16 (`LoginOnlyEndpointGuardTests.AnonymousInternalControllers` + host'ta ret dalı sabotajları), AC-20 (`IFormFile` / `[FromForm]` / `ReadFormAsync` yok), AC-28 (yol kapsamı, merge-base tabanı). |
| Startup | DL-2: AC-19 başlatma doğrulaması — her kural dışı yapılandırma için host'un açılmadığını gösteren ayrı fixture. |
| Frontend smoke | **N/A** (`shell: none`). |
| Verifier | `verify_module_id.py` (both modes). `verify_datatable_page.py` **N/A** for FU01. |
| RESX | **N/A** — no UI surface. |
| Build | Full solution build green; the **eight** existing consumers unchanged in behaviour. Platform tam suite (CT kuralı: servis dokunulunca süzgeçsiz). |
| Live (DL-2 kabul kapısı) | AC-29 — Auth bağımlılığı (§7) gelmeden koşulamaz. |

---

## 18. Ready-for-dev Checklist

**✅ CLOSED — CT, 2026-09-07 (r1).** Every decision gate below is resolved. The remaining unticked items are **kickoff-infrastructure confirmations**, not decisions: they are verified during `/add-module` **Phase 0** (orchestrator's infra check) and **do not block `ready-for-dev`**.

**Decision gates — all closed**

- [x] **DCP-008 is `approved` / `ready-for-execution`** (CAP-001 §7 — hard gate). — **approved 2026-09-07.**
- [x] **OD-A** — target location for the relocated seam. → **A shared contract location inside `Diten.Platform.Application`, NOT `Diten.Common`.** Cross-service contract extraction is deferred to FU07. The exact namespace/folder name is settled at implementation time against the Golden Reference and the existing MOD-0029 code. (Ölçüldü: `Application/Contracts/DocumentRepository/`.)
- [x] **OD-3** — permission namespace. → **`platform.document-repository.*`, LOCKED.** PKS-001 §4 allows exactly one owning module per namespace, so MOD-0029's `platform.document-management.*` is **not** extended. The MOD-0018 owner confirms key registration at implementation time; the namespace choice is not reopened.
- [x] **DCP-008 OD-1** — seam relocation. → **Stays inside FU01** (AD-8). Splitting it would leave FU01 unable to start.
- [x] **DCP-008 OD-4** — R1 scope. → **FU01 alone is the `R1 - PPM MVP` commitment.** FU02/FU03 are R1 **stretch**, gated on EA-BP-001.
- [x] **Maximum object size** → **config-driven, default greater than 50 MB.** The legacy 50 MB `RequestSizeLimit` was a buffered-upload symptom; **AD-4's streaming contract removes that memory constraint.** The exact per-environment value is set at implementation time. (r5: bu karar genel sınırı — `ContentStorageOptions.MaxFileSizeBytes` — yönetir ve değişmez; DL-2'nin güvenilen yoldaki daha dar sabit sınırı ayrı karardır, aşağıda K-E.)

**Kickoff-infrastructure confirmations — verified in `/add-module` Phase 0 (non-blocking)**

- [x] Storage-root configuration and allow-lists confirmed for each environment. → *Phase 0, 2026-09-08: `ContentStorageOptions` SectionName preserved; appsettings unchanged.*
- [x] Gateway route + multipart/stream pass-through confirmed with the integration agent. → *Phase 0, 2026-09-08: ocelot route added (surgical, 40-line addition; `IsBodilessStatus` guard).*
- [x] Audit event taxonomy for repository operations agreed with the MOD-0021 owner. → *Phase 0, 2026-09-08: `AuditCategory.DocumentManagement`. Live emission still pending smoke (see §16 / §19 ⚠️ partial closure).*
- [x] Confirmation that FU01 introduces **no** permission seed, menu entry, or migration without its own record. → *Phase 0, 2026-09-08: no seed; keys auto-register via `PlatformPermissionAutoRegistrationWorker` at startup.*

**r3 paket kararları — kapandı (2026-11-02)**

- [x] **K-A** — DL-2 bu paketin dilimidir, ayrı takip paketi değildir. → CT kararı (MVP-1 CT), 2026-11-02.
- [x] **K-B** — servis aktörünün denetim yolu: `AuditActorTypeResolver`'a `"service" => AuditActorType.Service`; aktör türü `Service`. → sahip kararı A (Beste), 2026-11-02; XMC-001 (MOD-0021) izni bu kararla. Ölçüm, gerekçe ve yan etkiler §19; ölçüt AC-25, AC-31.
- [x] **K-C** — tüketiciye bağlı nesne kullanıcı yolunda 404 (platform yöneticisi dahil). → CT kararı (MVP-1 CT), 2026-11-02; ölçüt AC-23.
- [x] **K-D** — DL-1'in içerik okuma denetim açığı (AC-8'in okuma kısmı) ve kullanıcı yolu `IFormFile` tamponlaması ayrı FU'dur, bu paketin DL-2'si değildir. → CT kararı (MVP-1 CT), 2026-11-02; sahip Beste, tarih TBD (§20).

**r5 paket kararı — CT önerisi, sahip onayı r5 onay satırına bağlı (inceleme 01 tur 2 F-7)**

- [ ] **K-E** — güvenilen tüketici yolunda dosya sınırı **52 428 800 bayt (50 MiB), koddaki sabit** (`TrustedDocumentRepositoryUploadLimits.MaxFileBytes`; yapılandırılmaz), istek gövdesi sınırı `MaxRequestBytes = 53 477 376`. Gerekçe: r1'in kilitli kararı ("Maximum object size → config-driven, default greater than 50 MB") **genel** sınırı yönetir (`ContentStorageOptions.MaxFileSizeBytes`, yapılandırılır, varsayılan 268 435 456) ve değişmez; K-E yalnız bu yolun **daha dar** sınırıdır ve MOD-0217 Runtime Constraints'in "sınır önerisi 50 MB … MOD-0262'nin yapılandırılmış üst sınırından küçük olanı" kuralını uygular; genel sınır 50 MiB'ın altına çekilirse uygulama açılmaz (§8.1). Öneren: MVP-1 CT, 2026-11-02. **Sahip onayı:** r5'in `execution/approvals/approvals.tsv` onay satırı («onayla») bu kararı da onaylar; satır gelince bu kutu kapanır. Onay gelmezse r6 sınırı yapılandırılabilir yapar ya da r1 kararını açıkça revize eder.

**r5 paket kapısı (bu sürümün dağıtımından önce — geriye kalan yalnız bunlar)**

- [ ] r5 için bağımsız inceleme (inceleme 01'in tur 3'ü; r4 tur 2 KOŞULLU — `docs/records/reviews/MOD-0262-FU01/2026-11-02-d3e37c37.md`, F-1…F-8 r5'te işlendi, §19.1; r3 tur 1 UYGUN DEĞİL — `docs/records/reviews/MOD-0262-FU01/2026-11-02-cc9acb1b.md`, F-1…F-13 r4'te işlendi, §19) (CT-APR-02, yazandan ayrı oturum) — `execution/approvals/reviews.tsv` satırı ve bulgu dosyası; `UYGUN DEĞİL` ya da açık ENGEL yok.
- [ ] r5 için `execution/approvals/approvals.tsv` onay satırı (yüksek risk: güvenlik / kimlik doğrulama / kiracı / denetim / ürün kodu yolu — `high: evet` olan ve paketi yazmamış onaylayıcı); K-E'nin sahip onayını da taşır.

**Dilim kapıları ("— DL-2 için"; paketi değil yalnız DL-2'nin kabulünü bekletir)**

- [ ] Auth audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` main'de (Ali — Auth, TBD; XMC-001) — DL-2 kabul kapısı (AC-29) için; DL-2 kodunun başlamasını bekletmez.
- [ ] MDM servis istemcisi + dev kiracı tahsisi + Platform kapalı listesi `ClientId` (Ali — Auth, TBD) — DL-2 kabul kapısı (AC-29) için.

S-1…S-4 (§19) MOD-0217 tarafının ya da Auth'un sorularıdır; DL-2'nin Platform kodunu bekletmez (S-1: güvenilen ucun `api/internal` altında olması Platform kuralıdır — `LoginOnlyEndpointGuardTests`; hizalanacak metin MOD-0217'dedir).

---

## 19. Implementation Notes

- **Decisions are locked.** CONTROL TOWER closed OD-1, OD-3, OD-4 and OD-A on **2026-09-07** and promoted this pack to `ready-for-dev` (see §18). Implementation must not reopen them: the seam relocation stays inside FU01, the permission namespace is `platform.document-repository.*`, FU01 alone carries the R1 commitment, the relocated seam lands inside `Diten.Platform.Application` (not `Diten.Common`), and the maximum object size is config-driven with a default above 50 MB. (r5, inceleme 01 tur 2 F-7: bu son madde genel sınırdır ve değişmez; DL-2'nin güvenilen yoldaki 50 MiB sabit sınırı onu yeniden açmayan, daha dar ayrı karardır — K-E, §18; CT önerisi, sahip onayı r5 onay satırıyla.)
- **Why this FU exists in this shape.** Blueprint puts MOD-0262 at W-1 / R1 / RC=Y, yet its own `Dependency Gate` names MOD-0030 (W-3) and MOD-0031 (W-4). CT decision **D-3** refused to move the module's wave and instead sliced the FUs. FU01 is the slice that survives that inversion.
- **The seam is preserved deliberately.** `IContentStorageGateway` / `ContentRef` already abstract **eight** consumers; DCP-008 **AD-2** keeps them. This FU fills in behind the seam and re-homes it — it is not a rewrite.
- **In-process by decision, HTTP-shaped by design.** DCP-008 **AD-3**: R1 stays inside `Diten.Platform`; the contract is nevertheless designed for a boundary so FU07's extraction does not force a redesign.
- **The two hard risks this FU closes** (measured in `docs/analysis/mod-0262-internal-document-repository-prd-and-impact-analysis.md`): `ContentStoreRequest.Content` as `byte[]`, and the seam's written instruction that callers perform authorisation.
- **Retention boundary is untouched.** `DocumentDispositionService` states in code that it never deletes and that destruction is "a deliberate future task". FU01 does not become that task — FU05 does, and only once MOD-0030 has an owner.
- **Eski plan atfı (r4, inceleme 01 F-9).** r1–r3 burada `docs/platform/master-plan.md`'nin MOD-0262'yi atlanabilir dış sağlayıcı diye anlattığını yazıyordu; dosya HEAD'de **yok** (ölçüldü 2026-11-02, a3fe45562). Atıf kaldırıldı; bu paket ve `execution/registries/module-id-registry.md` yetkilidir.
- **Pack↔code reconciliation (2026-09-08, from FU01 `/add-module` Phase 0).** Three record drifts corrected against the implemented code: (a) **eight** consumers, not six — `TemplateMasterService` and `TemplateVariantService` also route through `DocumentVersioningService`; (b) `entity_base` is **`TenantScopedEntity`**, not `BaseEntity` (`TenantRepository<T>` requires it; the standard's table lagged the code); (c) **no `RegisterClassMaps`** in `Diten.Platform` — it uses a global `GuidSerializer(Standard)`, and the class-map trap is CrmService-specific (correctly not invented).
- **⚠️ KAPANIŞ (KISMİ) — 2026-09-08 (DK#10).** Code complete; static verification green — 13/14 §16 boxes (audit-event box is code-wired, **not** live-verified), full solution build green, the eight consumers behaviourally unchanged (ControlledDocument 49/49, Template 222/222), `verify_module_id` `--check-id`/`--check-all` exit 0 / 0 HARD. **Runtime smoke NOT run** (fleet down, 5057/5000 closed) → per DK#10 this is **not** ✅. Full suite 3758 pass / 155 pre-existing-or-environmental FAIL (77 macOS-mongod-path, 49 BSON-Timestamp, ~26 MOD-0029 domain + BL-279, 3 MOD-0029 attribution — none FU01's; the 3 attribution warrant a pre-FU01 baseline confirmation). Live-verification list owned by CONTROL TOWER: multipart `POST …/document-repository/objects`→201+ContentRef · streamed `GET …/{contentId}/content` · cross-tenant 404 (ContentId **and** raw ObjectKey) · 401/403 authz · audit events reaching MOD-0021 · four indexes present in the live DB · MOD-0029 upload/download unbroken. Keys `platform.document-repository.read|manage` auto-register at Platform startup (`PlatformPermissionAutoRegistrationWorker`) and land on the full-catalog SuperAdmin role → smoke uses a **platform-admin** token (no manual grant, no raw-Mongo rolePermissions write).
- **DL-1 kapanış durumu — r4 ölçümü (inceleme 01 F-10, 2026-11-02, HEAD a3fe45562).** Yukarıdaki 2026-09-08 notu tarihçedir. (a) "13/14 §16 boxes" sayımının kanıt kaydı yok: AC-1…AC-14 işaretsizdir ve bu paket için `docs/records/` altında kabul ya da smoke kaydı yoktur (ölçüldü: `docs/records` içinde MOD-0262 geçen dosyalar yalnız karar, inceleme ve tarihçe kayıtları). (b) AC-3: `byte[]` kaldırıldı ve JSON gövdesinde ikili yok, ama kullanıcı yolu yüklemesi `[FromForm] IFormFile` ile tamponlanır (r2 ölçüm b) — AC-3'ün "multipart/stream" kısmı kullanıcı yolunda tam karşılanmıyor; K-D ile ayrı FU. (c) AC-8'in okuma kısmı kodda yok (K-D). (d) Çalışma zamanı smoke'u 2026-09-08'den bu yana koşulmadı; smoke tarihi yok. (e) Canlı doğrulama listesindeki "→201+ContentRef" beklentisi kodla tutmuyor: DL-1 `StoreForCallerAsync` `Response.Success` ile **200** döner (`Response.Success` varsayılanı 200); liste smoke'ta 200 diye okunur. DL-1 kapanışı **KISMİ** kalır; AC-1…AC-14'ün işaretlenmesi DL-1 kabul kaydının işidir.
- **Branch reconciliation (2026-09-08).** Frontmatter `branch:` was the proposed `feature/pss/mod-0262-…`; CT declined a worktree and kept work on `feature/structured-content-messaging` (the active multi-workstream branch). Field updated to match. Commit/push deferred by CT until all workstreams are done. DL-2 dalı dağıtım anında kararlaştırılır; `branch:` alanı r1 tarihçesidir.
- **r2 revizyonu (2026-11-02) — DL-2 servis kimliği.** İstek: MOD-0217 CLM (origin/main `execution/domains/master-data-management/module-packs/MOD-0217-contract-lifecycle-management-clm.md` — revizyon notu "D56 + DL-2 hazırlık bulguları", Runtime Constraints "Çağrı kimliği", Failure Path "Belge deposu servis kimliği (KARAR D56/2)", Authorization Convention "Belge deposu çağrıları servis kimliğiyle", AC-24, AC-27, Ready-for-dev Checklist "Dilim kapıları" — "MOD-0262-FU01'de servis kimliği desteği" ve "MOD-0262-FU01 canlı doğrulama kaydı" maddeleri) ve karar metni `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` "D56 — DL-2 hazırlığında çıkan dört karar" madde 2. Tüketici sahibi Treasury CT; Platform sahibi Beste. r1 hiçbir kararı değişmedi; yalnız §14'teki OD-3 satırının "open" ifadesi §18 ile çelişiyordu, mekanik olarak düzeltildi. r1'in numarasız AC'leri AC-1…AC-14 olarak numaralandı (metin aynı).
- **r2 ölçüm notları (2026-11-02, HEAD 925ea4e34; origin/main 743f42d1c — DL-1 yollarında iki ref aynı).** (a) DL-1 kodu main'de: Repo Scope'taki her DL-1 yolu iki ref'te var. (b) `DocumentRepositoryController.Store` `[FromForm] IFormFile` bağlıyor: ASP.NET form bağlaması gövdeyi eylemden önce okur (büyük dosyada geçici dosyaya), denetleyici yorumundaki "streams straight from the request" iddiası tam değildir — DL-1 kullanıcı yolu DL-2'de değişmez, takip §20. (c) `OpenRepositoryObjectContentQuery` ve `GetRepositoryObjectByIdQuery` denetim işareti taşımıyor: AC-8'in "read" kısmı kodda yok (DL-1 açığı, K-D). (d) `AuditActorTypeResolver.FromActorTypeClaim` yalnız `tenant_user` / `platform_admin` / `partner_admin` tanır; servis token'ı (`actor_type=service`) `Unknown` olur, `AuditService` `Unknown` aktörü reddeder, `AuditBehavior` yalnız uyarı loglar — DL-2 bunu çözmeden güvenilen yazmalar sessizce denetimsiz kalır (K-B). (e) `AuditActorType.Service` enum'da var ve kullanılıyor (`ESignature/Handlers/CatalogHandlers.cs`, `AuditOutboxRepository.cs`); AUD-001 §3 tablosu servis çağrısı için `ActorType = System` + `SourceService` yazıyor — çelişki K-B'de (r3'te kapandı: `Service`, aşağıda). (f) Ret denetimi emsali: `PartnerAdminScopeAttribute.RecordAsync` (`AuditOperation.PermissionDenied`, `AuditOutcome.Denied`, `IsPlatformGlobal`). (g) DL-2 adları kodda yok (`grep` 0); `TrustedConsumerClientId` adı `WorkflowInstance.TrustedConsumerClientId` emsalinden.
- **Plan hizalaması.** Modülün plan satırı bu revizyona verilmedi (plan depoda değildir) → Teslim dilimleri tablosunda `plan satırı yok`. DL-2 yeni bir plan MVP'si ya da yetenek kimliği getirdiği ölçülemedi; D56/2 metni desteği açıkça "MOD-0262-FU01'de" istediği için aynı pakette yeni dilim olarak yazıldı; K-A (CT, 2026-11-02) bunu onayladı — takip paketi açılmaz.
- **r3 revizyonu (2026-11-02) — kararlar işlendi.** r2 (sha256 9420fd5d…, HEAD e7f6734b3) üzerine: K-B sahip kararı A (Beste), K-A / K-C / K-D CT kararları (MVP-1 CT). AC-25 aktör türüne bağlandı, AC-31 eklendi; §5, §6, §7, §14, §17, §18, §20, §21 buna göre güncellendi. AC numaraları değişmedi, madde silinmedi.
- **r4 revizyonu (2026-11-02) — bağımsız inceleme 01 tur 1 (r3, UYGUN DEĞİL: 1 ENGEL, 3 ORTA, 9 DÜŞÜK; `docs/records/reviews/MOD-0262-FU01/2026-11-02-cc9acb1b.md`) bulguları ve CT kararı işlendi.** F-1 (ENGEL): sınıf düzeyi şema kapısı kaldırıldı → `[AllowAnonymous]` + yürütücüde açık `AuthenticateAsync` (emsal `InternalTrustedSourceAuditIntentController`; MOD-0040-FU02 aynı desen), `AnonymousInternalControllers`; §3, §5, §8.1, §13, §14, AC-16 / AC-17 / AC-26 / AC-27, gerçek host testi. F-2: görünürlük `TrustedConsumerModule`'e bağlı, `TrustedConsumerClientId` yalnız iz; modül başına tek girdi, başlatmada doğrulama (audience dahil); istemci yenileme; AC-19 / AC-22. F-3: başlık ön kontrolü retleri AC-26'da; kayıt kiracısı ölçüldü (token kiracısı / `PlatformSystemTenantId`); kimliksiz ret kaydı sınırlı (CT kararı (c), MOD-0040-FU02 K-10 / K-13 deseni, adlı sapma — sahip Beste). F-4: tek sınır 52 428 800 bayt, uçta açık `RequestSizeLimit`, aşım 413; AC-32. F-5: `ForDataExport` koruması yazılacak kod, guard testi destekleyici (AC-31). F-6: beşinci JWT şeması / dördüncü servis audience'ı. F-7: AC-28 tabanı ve MOD-0040-FU02 ortak dosya kuralı. F-8: §21 okuma ve ret satırlarının yolu, aktör türü kaynağı, yarıda kesilen akışın sonucu. F-9: eski plan atfı kaldırıldı. F-10: DL-1 kapanış durumu ölçüldü (yukarıda). F-11: telafi tekrarı 204, AC-33. F-12: boş liste 503, form alanları bütçe dışı ve 16 KiB, scope / Guid biçimi; AC-34. F-13: §6 tam yollar.
- **r5 revizyonu (2026-11-02) — bağımsız inceleme 01 tur 2 (r4, KOŞULLU: 1 ORTA, 7 DÜŞÜK; `docs/records/reviews/MOD-0262-FU01/2026-11-02-d3e37c37.md`) bulguları ve CT kararı (hepsi kabul) tek geçişte işlendi.** Kapanış tablosu §19.1. Yeni karar yalnız K-E (CT önerisi, sahip onayı r5 onay satırıyla); kapsam genişlemedi: yeni yollar `ITrustedUploadLimitSignal.cs` (Application sözleşmesi) ve §6'ya eklenen iki değişmeyen davranış dosyası. AC-35 eklendi; AC numaraları değişmedi, madde silinmedi. Ölçümler bu çalışma ağacında HEAD `e3cbbca4d`'de yapıldı.
- **Kararlar (kapandı, 2026-11-02; K-E açık — sahip onayı bekler).**
  - **K-A (CT, MVP-1 CT)** — DL-2 bu paketin dilimidir; ayrı takip paketi açılmaz (module-pack-standard Bölüm 16 "Plan hizalaması": yeni plan MVP'si ya da yetenek kimliği getirdiği ölçülmedi, D56/2 desteği "MOD-0262-FU01'de" istiyor).
  - **K-B (sahip kararı A, Beste)** — `AuditActorTypeResolver.FromActorTypeClaim`'e `"service" => AuditActorType.Service` eşlemesi DL-2'de yazılır; MOD-0021 dosyası için XMC-001 izni bu kararla verildi. Aktör türü **`Service`**. Ölçüm ve gerekçe aşağıda.
  - **K-C (CT, MVP-1 CT)** — tüketiciye bağlı nesne (r4: `TrustedConsumerModule` dolu) kullanıcı yolunda 404, platform yöneticisi dahil (AC-23).
  - **K-D (CT, MVP-1 CT)** — DL-1'in içerik okuma denetim açığı (AC-8'in "read" kısmı) ve kullanıcı yolu yüklemesinin `IFormFile` tamponlaması **ayrı FU**'dur; sahip Beste, tarih TBD (§20). FU kimliği burada açılmaz: DCP-002 ön kontrolüyle (`verify_module_id.py --check-id MOD-0262-FUnn --parent MOD-0262`) açılır; FU02…FU07 başka içeriğe ayrılmıştır. AC-8 metni değişmez; okuma kısmı o FU kapanana kadar karşılanmamış kalır ve DL-1 kapanışı KISMİ kalır.
  - **K-E (CT önerisi, MVP-1 CT, 2026-11-02; sahip onayı r5 onay satırıyla — inceleme 01 tur 2 F-7)** — güvenilen tüketici yolunda dosya sınırı 52 428 800 bayt, koddaki sabit; r1'in genel, yapılandırılan sınırı değişmez (gerekçe §18). "TBD sahip" değildir: onay r5'in `approvals.tsv` satırına bağlıdır.
- **K-B ölçümü — `Service` mi, `System` + `SourceService` mı (2026-11-02, HEAD e7f6734b3).**
  - Enum: `services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs` → `AuditActorType` `Unknown = 0 … System = 4, Service = 5`.
  - Ret: `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/AuditService.cs` doğrulaması `ActorType == Unknown` → "Audit actor type is required." (kayıt yok).
  - `System`'in kodda anlamı **principal yok**tur: `AuditActorTypeResolver.ForCommand` kimliği doğrulanmamış çağrıyı (Hangfire işi, olay tüketicisi, anonim iç çağrı) `System` yapar. Servis token'ını `System`'e eşlemek "principal'sız makine" ile "kimliği doğrulanmış servis"i aynı değere katar.
  - Servis aktörü bugün kodda üç yerde `Service`'tir: `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/AuditOutboxRepository.cs` (güvenilen kaynak denetim niyeti — MDM'den yol `b` — kişi adı yoksa `ActorType = Service`); `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/PlatformEntitlementAuditSink.cs` → `MapActorType` (`"service" => AuditActorType.Service`); `services/Diten.Platform/src/Diten.Platform.Application/Features/ESignature/Handlers/CatalogHandlers.cs` (katalog kaydı servis aktörüyle).
  - `SourceService`: `AuditBehavior` sabiti `Diten.Platform`'dur (kaydı yazan servis); çağıran servis DL-2'de metadata `callerService`'e yazılır. MOD-0040-FU02 K-9 `SourceService`'i yalnız işlem içi yazıcıda (`CanonicalTransactionalAuditOutboxWriter`) çağıran servise genişletiyor — bu paket o yazıcıyı kullanmaz, çakışma yok.
  - MOD-0040-FU02 (`execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md`, HEAD) servis aktörü varsaymaz: ayırmada aktör isteği yapan kullanıcıdır (`ActorType = TenantUser`, K-8r), `actor_type ≠ tenant_user` olan token yürütücüde 403'tür (AC-44) ve `AuditActorTypeResolver.cs`'i kendi Repo Scope'unda "değişmez" sayar (korunan yol listesi). `"service"` eşlemesi onun hiçbir yolunu değiştirmez (ret yürütücüde, eşlemeden önce); iki paket aynı dalda yürürse resolver'ı yalnız bu paketin DL-2'si değiştirir.
  - **Sonuç:** kod `Service`'te birleşiyor; DL-2 `ActorType = Service` + `ActorId = ClientId` + `SourceService = Diten.Platform` + metadata `callerService` yazar. AUD-001 §3 satır 3'ün metni ("`ActorType = System` + `SourceService`") kodla farklı kalır → `.antigravity/rules/audit-trail-standard.md` hizalaması Ali'nin işidir (§7, tarih TBD). Yetki sırasına göre paket kazanır; DL-2'yi bekletmez.
- **K-B yan etki envanteri (2026-11-02, HEAD e7f6734b3).** `FromActorTypeClaim`'i çağıran her yer ve servis principal'ının oraya ulaşıp ulaşmadığı:
  - `AuditBehavior` (`ForCommand`) — `IAuditableCommand`'lar. Servis token'ı yalnız güvenilen şemalardan principal olur; varsayılan JWT şeması `JwtSettings` audience `diten-erp` ister, servis token'ının audience'ı `TRUSTED_*`'dır (`appsettings.Development.json` `JwtSettings`, `TrustedServiceTokenValidationExtensions`). `HttpContext.User`'ı servis principal'ı yapan tek yer `InternalTrustedWorkflowConsumerController` (sınıf düzeyi şema) + `TrustedWorkflowConsumerRequestExecutor`'dır: `GetTerminalDecisionEvidence`, `GetStartResult`, `GetCancellationPreflight` servis principal'ıyla **sorgu** gönderir (`GetTrustedWorkflowTerminalDecisionEvidenceQuery`, `GetTrustedWorkflowStartResultQuery`, `GetTrustedWorkflowCancellationPreflightQuery` — denetim işareti yok); `Start` ve `Cancel` gönderimden önce principal'ı temsilci kullanıcıya çevirir (`tenant_user`). Güvenilen kaynak denetim, legal-entity kapsamı ve reference-data yürütücüleri `HttpContext.User`'ı değiştirmez (anonim → `System`, değişmez). **Bugün değişen kayıt yok;** yeni kayıt yalnız DL-2'nin güvenilen komutlarından gelir.
  - `CanonicalTransactionalAuditOutboxWriter.ResolveActor` (K2 işlem içi yol) — bugün servis principal'ını `Unknown` diye reddeder; eşlemeden sonra `Service` aktörlü ve `ActorId = sub` kayıt yazar. Bugün bu yazıcıya servis principal'ıyla ulaşan komut yok (yukarıdaki ölçüm). Davranış AC-31 ile sabitlenir.
  - `DataExportAuditWriter` (`ForDataExport`) — dışa aktarım "her zaman bir kişidir".
    - **DL-2'de yazılacak kod** (r5, inceleme 01 tur 2 F-8): `ForDataExport`, `FromActorTypeClaim` `Service` döndürünce sonucu `Unknown`'a çevirir; böylece servis aktörü dışa aktarımda açıkça reddedilir.
    - Bugünkü kodda bu çeviri **yoktur**. Ölçüm: `AuditActorTypeResolver.ForDataExport` kimliği doğrulanmış principal için doğrudan `FromActorTypeClaim(principal.ActorType)` döndürür. Servis principal'ı bugün yalnız `"service"` eşlemesi olmadığı için `Unknown` olur.
    - `DataExportAuditWriterTests`'in `"service"` vakası (`result.ActorType == Unknown` ve `AuditService`'e istek yok) bu yüzden bugün geçer. Eşleme eklendikten sonra vakayı yeşil tutan, bu yeni koddur (AC-31). "Bugünkü davranış" ifadesi r4'te kalmıştı; kaldırıldı.
  - `SignatureHandlers` (`ForCommand`) ve `PlatformAuditAppendController` (`ForCommand`) — varsayılan `[Authorize]` (kullanıcı JWT'si) arkasında; servis token'ı ulaşmaz, değişmez.
  - Test değişikliği: `AuditBehaviorActorTypePipelineTests.An_authenticated_principal_without_a_recognised_actor_type_is_Unknown_and_the_command_is_not_blocked` bugün `"service"`'i Unknown olarak sabitliyor → `"service"` bu teoriden çıkar, `Service` beklentili yeni teste taşınır (AC-31). `AuditActorTypeResolver` sınıf yorumu ("AuthService mints exactly three actor types") servis token'ını (MOD-0033-FU02, `actor_type = service`) da anacak şekilde güncellenir.
- **Açık sorular (sahip + tarih).** Hiçbiri DL-2'nin Platform kodunu bekletmez (§18).
  - **S-1 (Treasury CT; tarih TBD):** MOD-0217 Runtime Constraints MdmService → MOD-0262 çağrısını "Gateway üzerinden" yazıyor; güvenilen uç `api/internal` altında ve Gateway'den yönlendirilmiyor. MOD-0217 metni iç adrese (`PlatformBaseUrl` deseni) çekilsin mi?
  - **S-2 (Treasury CT + Platform — Beste; tarih TBD):** Yükleme yanıtı kaybolursa nesne işaretçisiz kalır. DL-2'ye `Idempotency-Key` eklensin mi, yoksa MOD-0262-FU03 mutabakatına mı bırakılsın? Öneri: FU03.
  - **S-3 (Ali — Auth; tarih TBD):** Audience adı `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` ve MDM için ayrı istemci kimliği uygun mu; Auth kodu ve istemci tanımı hangi tarihte?
  - **S-4 (Treasury CT; tarih TBD):** v1 scope listesi yalnız `Documents` (D41/10'un geçerli kısmı). MOD-0217 başka scope isterse §8.1 ekleme kuralı işler.
  - **S-5 (Treasury CT; tarih TBD):** DL-2'de güvenilen telafinin tekrarı 204 ve zararsızdır (§8.1 "Telafi tekrarı", AC-33). MOD-0217'nin "telafi tam bir kez" kuralı tüketici tarafının çağrı sayısı olarak okunur — teyit edilsin; MOD-0217 yanıt kaybında yeniden denerse 404 beklememelidir.

### 19.1 Bağımsız inceleme 01 tur 2 — kapanış tablosu (r4 → r5)

Bulgu dosyası `docs/records/reviews/MOD-0262-FU01/2026-11-02-d3e37c37.md` (dosya özeti `14f68ca4…`, `git show e3cbbca4d:<yol> | sha256sum`; hedef r4 pack sha256 `d3e37c37…0634282`). Sonuç KOŞULLU: 1 ORTA, 7 DÜŞÜK. CT kabulü 2026-11-02 (hepsi kabul). F-7 karar satırı K-E'dir (CT önerisi, sahip onayı r5 onay satırıyla).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@d3e37c37 (ORTA) | Uç sınırı uç metadata'sıyla ölçülür: `EndpointDataSource` → güvenilen yükleme ucu → `IRequestSizeLimitMetadata.MaxRequestBodySize == MaxRequestBytes`; sabotajlar (öznitelik kaldırma, 30 000 000, `[DisableRequestSizeLimit]`) buna bağlı; TestServer'ın (`TestHost` 8.0.8 / 8.0.11) `IHttpMaxRequestBodySizeFeature`'ı uygulamadığı ve `RequestSizeLimit`'in orada etkisiz olduğu açıkça yazıldı; 31 MB host vakası kaldırıldı; gerçek Kestrel testi isteğe bağlı | §8.1 "Boyut", §17; AC-32 |
| F-2@d3e37c37 | Ret yazıcısı `ActorType`'ı sabit yazar (`ForCommand(principal)` çağrılmaz) ve `ActorId` / `ActorEmail` / `ActorDisplayName`'i hiçbir zaman null bırakmaz; böylece `AuditService.BuildPayload`'ın `??` ile `ICurrentUserContext` dolgusu devreye girmez (kimliksiz: `Guid.Empty` / `""` / `"system"` → kayıtta null / null / `s***m`); kullanıcı JWT'li kimliksiz ret vakası | §3, §8.1 "Aktör alanları", §13; AC-26 |
| F-3@d3e37c37 | `alreadyCompensated` = yürütücünün ön okuması (`GetByContentIdIncludingCompensatedAsync` + görünürlük) → komut alanı `AlreadyCompensated` → `GetAuditMetadata()`; yarış kuralı: silme yalnız `MarkCompensatedAsync`'i kazanan çağrıda; üç zamanlama durumu ve kayıt sayıları; kayıt ön okumanın anlamını taşır (kabul edilen sınır); DL-1 kullanıcı yolunda örtüşen ikinci silme kalkar ("değişir") | §3, §5, §8.1 "Telafi tekrarı", §13; AC-30, AC-33 |
| F-4@d3e37c37 | "Ret başına tek yazar" tablosu: her sonuç için yazar, `Outcome` / `Operation`, kiracı / aktör ve kayıttaki ret kodu; meta / içerik 404 sorgu yanıtından, telafi 404 ön okumadan (ret yazıcısı), doğrulayıcı 400 yürütücünün `ValidationException` yakalamasıyla (ret yazıcısı; `AuditBehavior` çalışmaz); yürütücü komut yanıtına bakıp ret yazıcısını çağırmaz — çift kayıt yok | §6, §8.1, §13, §17; AC-26, AC-35 |
| F-5@d3e37c37 | Akış içi 413 komutun içinde çevrilir (`ITrustedUploadLimitSignal`, handler) → `AuditBehavior` kaydı `ResponseStatusCode = 413`; belirteç `HttpContext.RequestAborted`; iptal dalı: `AuditBehavior` ve `ExceptionBehavior` geçirir, tek kaydı yürütücü yazar (`…_CLIENT_ABORTED`, `CancellationToken.None`); kesik gövde 503 tek `AuditBehavior` kaydı; `AuditBehavior` satırlarında `ReasonCode` olmadığı ölçüldü (durum kodu ret kodunu belirler) | §3, §5, §8.1 "Boyut", "İptal ve kesik gövde", §13, §17; AC-35 |
| F-6@d3e37c37 | AC-24 sabotajı: yürütücü `HttpContext.User`'ı kabul eder → kullanıcı JWT'si vakası kırmızı; r4 sabotajının neden gerçek olmadığı yazıldı | AC-24 |
| F-7@d3e37c37 | K-E karar satırı: güvenilen yolda 50 MiB sabit sınır, r1'in genel yapılandırılan sınırı değişmez; öneren MVP-1 CT; sahip onayı r5 onay satırına bağlı | §8.1 "Boyut", §18, §19 |
| F-8@d3e37c37 | K-B yan etki envanterindeki `ForDataExport` maddesi DL-2'de yazılacak kod olarak düzeltildi; bugünkü kodun ölçümü eklendi | §19 |

---

## 20. Follow-up Items

| Item | Target |
|---|---|
| Repository Admin + Document Binary Store page | MOD-0262-FU02 (after EA-BP-001) |
| Repository Health / two-way orphan reconciliation | MOD-0262-FU03 (DL-2'nin işaretçisiz tüketici nesneleri dahil — S-2) |
| Evidence Package Storage | MOD-0262-FU04 — ⛔ blocked on EA-BP-003 + MOD-0031 |
| Physical disposition execution (AD-6 rules carried verbatim) | MOD-0262-FU05 — blocked on MOD-0030 identity |
| MOD-0162 consumer cutover (four untyped pointers) | MOD-0262-FU06 — ⛔ blocked on EA-BP-003 |
| Extraction to standalone `Platform Document Service` | MOD-0262-FU07 |
| Additional storage providers (S3 / Azure Blob) | post-R1, behind the unchanged seam |
| Antivirus scan + encryption-at-rest policy | post-R1 |
| ⚠️ `EnterpriseStrategyService/UploadsController` authentication gap | **CT decision D-5 — separate security lane, not a MOD-0262 follow-up** |
| Kullanıcı yolu yüklemesinin `IFormFile` yerine `MultipartReader` akışına geçmesi (§19 r2 ölçüm notu b) | ayrı FU (K-D, CT 2026-11-02) — sahip Beste, tarih TBD; kimlik DCP-002 ön kontrolüyle açılır |
| Kullanıcı yolu içerik okuma denetimi (AC-8 okuma kısmı) | aynı ayrı FU (K-D) — sahip Beste, tarih TBD |
| AUD-001 §3 satır 3 metninin kodla (`Service`) hizalanması | `.antigravity` değişikliği — Ali, tarih TBD (§7) |
| İkinci güvenilen tüketici (başka modül / scope) | bu paketin revizyonu — §8.1 ekleme kuralı |

---

## 21. Audited Events

Servisin denetim altyapısı var: `tests/architecture/audit-ledger/Diten.Platform.md` → `## İzler` → `platform-merkezi` (yol `a`, işaret `IAuditableCommand+IAuditMetadataProvider+!IAuditExcludedRequest+!ITransactionOwnedAuditCommand`). Olay adı `AuditBehavior`'ın yazdığı `RequestType`'tır (komut türünün adı). K2 sınıfı değil: bu yollardan geçen nesne sözleşme belgesi ve genel ikilidir; GxP kontrollü belge MOD-0029'un süreç içi yolundan (`DocumentVersioningService`) geçer, bu komutlardan değil.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `StoreRepositoryObjectCommand` (DL-1) | `StoreRepositoryObjectCommand` | `RepositoryObject` | a | AfterState: `ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum` (içerik ve `ObjectKey` yok) | — |
| `CompensateRepositoryObjectCommand` (DL-1) | `CompensateRepositoryObjectCommand` | `RepositoryObject` | a | BeforeState: `ContentId`, `Scope`; telafi işareti | — |
| `StoreTrustedRepositoryObjectCommand` (DL-2) | `StoreTrustedRepositoryObjectCommand` | `RepositoryObject` | a (`ActorType = Service` — K-B sahip kararı A; eşleme olmadan kayıt düşmez, AC-25 sabotajı) | AfterState: yukarıdaki alanlar + `TrustedConsumerModule`; metadata `callerService`, `consumerModule`, `audience`, `serviceClientId` | — |
| `CompensateTrustedRepositoryObjectCommand` (DL-2) | `CompensateTrustedRepositoryObjectCommand` | `RepositoryObject` | a (`ActorType = Service`, K-B) | BeforeState: `ContentId`, `Scope`; metadata aynı + `alreadyCompensated` (komut alanından, yürütücünün ön okuması — r5 F-3; §8.1 "Telafi tekrarı") | — |
| Okuma satırı: `OpenTrustedRepositoryObjectContentQuery` (DL-2) | `document-repository.trusted-consumer.content-read` | `RepositoryObject` | merkezi günlük — `IAuditService.AppendAsync` açık çağrısı (komut değil; `AuditBehavior` işareti yok, denetim defterinin komut sayımına girmez). `ActorType = AuditActorTypeResolver.ForCommand(principal)` → `Service`, `ActorId = ClientId`; `Operation = Export`; kayıt akış açılınca, ilk bayttan önce yazılır (`Outcome = Succeeded`, metadata `delivery = started`); akış yarıda kesilirse kayıt değişmez (eklemeli günlük), kesinti uyarı loguna düşer | metadata `ByteSize`, `scope`, `consumerModule`; içerik yok | — (AUD-001 §7 sınıfı değil; makine kimliğinin içerik çıkarması Platform tarafındaki tek iz olduğu için yazılır — D56/2) |
| Okuma satırı: `OpenRepositoryObjectContentQuery` (DL-1) | — | — | — | — | DL-1 açığı: AC-8 okuma ister, kod yazmıyor (§19 r2 ölçüm c); K-D (CT 2026-11-02) ile ayrı FU'ya taşındı — sahip Beste, tarih TBD (§20); İ sınıfı değildir, istisna yazılmaz |
| Ret: güvenilen yüzey (DL-2; 401 / 403 / 404 / 400 / 413 — AC-26; 503 yapılandırma ve 504 bütçe servis hatasıdır, kayıt yok). Hangi sonucu bu satırın, hangisini `AuditBehavior`'ın yazdığı §8.1 "Ret başına tek yazar" tablosundadır (r5 F-4): komut içi sonuçlar (sağlayıcı 400, akış içi 413, 503) bu satıra girmez | `document-repository.trusted-consumer.denied` | `RepositoryObject` ya da uç | merkezi günlük — `TrustedDocumentRepositoryRefusalAudit` → `IAuditService.AppendAsync` (emsal `PartnerAdminScopeAttribute.RecordAsync`; komut değil). `Operation = PermissionDenied`; `Outcome = Denied` (yetki / kimlik / bağ retleri) ya da `Failed` (400, 413). Aktör alanları açık (r5 F-2; `ICurrentUserContext` dolgusu devreye girmez). Kimliği doğrulanmış: `TenantScope.Begin(token kiracısı)`, `ActorType = Service`, `ActorId = ClientId`, e-posta ve ad boş. Kimliksiz: `IsPlatformGlobal = true` (`PlatformSystemTenantId`), `ActorType = System`, `ActorId = Guid.Empty` (kayıtta null), e-posta boş, ad `"system"`; dakikada 60 sınırı — aşan kayıt yazılmaz (adlı sapma, sahip Beste) | metadata `ReasonCode`, uç; kimliği doğrulanmışsa `serviceClientId` | — (AUD-001 §3 "Reddedilen komut da kaydedilir"; kimliksiz sınırı adlı sapma) |
| İptal: güvenilen yükleme ya da telafi sırasında istemci bağlantıyı kopardı (DL-2; AC-35 (d)) | `document-repository.trusted-consumer.client-aborted` | `RepositoryObject` | merkezi günlük — yürütücünün iptal dalı → `TrustedDocumentRepositoryRefusalAudit` → `IAuditService.AppendAsync`, `CancellationToken.None` ile (`AuditBehavior` `OperationCanceledException`'ı kaydetmeden geçirir — r5 F-5). `Operation` = komutun işlemi (`Create` / `Delete`), `Outcome = Failed`; kimliği doğrulanmış aktör alanları (yukarıdaki satır) | metadata `ReasonCode = DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_CLIENT_ABORTED`, uç, `serviceClientId` | — (AUD-001 §3: komut sonuç vermeden bitti; kaydı komutun yolu yazamadığı için açık çağrı) |

Kayda yazılmayan alanlar: token (servis ya da kullanıcı), istemci sırrı, `ObjectKey`, dosya içeriği. Dosya adı yalnız `RepositoryObject` satırındadır, denetim metadata'sına yazılmaz.

---

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek ikili depo — taşınan dikiş, akış sözleşmesi, depo içi yetki, kiracı yalıtımı, denetim (kodu main'de; kapanış KISMİ) | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events | birim + gerçek Mongo entegrasyon + tam build + dev ortamı smoke (§19 CT canlı doğrulama listesi) | plan satırı yok | plan satırı yok |
| DL-2 | güvenilen tüketici servis kimliği — MOD-0217 için akışlı yükleme, meta, içerik, telafi; kapalı tüketici listesi, retler dahil denetim (DL-1'e dayanır) | Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Trusted consumer identity, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events | birim + sabotajlı güvenlik testleri + gerçek HTTP host + gerçek Mongo + tam Platform suite; kabul kapısı: Auth'un verdiği token'la dev ortamı canlı kaydı (AC-29) | plan satırı yok | plan satırı yok |
