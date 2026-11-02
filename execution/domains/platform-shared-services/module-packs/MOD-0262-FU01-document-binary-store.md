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
revision: "r2 (2026-11-02) — DL-2 servis kimliği (güvenilen tüketici) desteği eklendi; istek MOD-0217 KARAR D56/2 (docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md, D56 madde 2). r1 = origin/main ve HEAD sha256 cc49014c4f8e4502daa9d6bfdfda80a72520f32a02f1cfe942e3087045f151f3 (CT onayı 2026-09-07; CT-APR-01 öncesi onay, approvals.tsv satırı yok). r2 yeni bağımsız inceleme (CT-APR-02) ve approvals.tsv onay satırı ister."
status_note: "r1: CT promoted draft → ready-for-dev 2026-09-07 after DCP-008 was approved and OD-1 / OD-3 / OD-4 / OD-A were resolved; DL-1 kodu main'de (kapanış KISMİ, §19). r2 (2026-11-02): DL-2 eklendi; approval_status pending — bu sürümün onayı yok, approved_by / approved_on yalnız r1'i gösterir. Onay satırı gelene kadar hiçbir dilim (DL-1 düzeltmesi dahil) bu paketle dağıtılmaz."
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

> **Status guard (r2, 2026-11-02).** `status: ready-for-dev` r1 onayından (CT, 2026-09-07) kalır; `approval_status: pending` olduğu için CT SOP §36.1 görev sınıfı kuralına göre `status` tek başına yetki **değildir**. Bu sürüm (r2) yeni bağımsız incelemeden (CT-APR-02) ve `execution/approvals/approvals.tsv` onay satırından geçmeden hiçbir dilim dağıtılmaz. DL-1 kodu r1 onayıyla yazıldı ve main'de (§19); DL-2 (servis kimliği) başlamadı.
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
| Controller (DL-2) | `InternalTrustedDocumentRepositoryConsumerController` | Sınıf düzeyinde `[Authorize(AuthenticationSchemes = TrustedServiceTokenValidationExtensions.DocumentRepositoryAuthenticationScheme)]`; emsal `InternalTrustedWorkflowConsumerController`. |
| Security (DL-2) | `TrustedServiceTokenValidationExtensions.DocumentRepositoryAuthenticationScheme` = `"TrustedDocumentRepositoryConsumerService"`, `DocumentRepositoryRequiredAudience` = `"TRUSTED_DOCUMENT_REPOSITORY_CONSUMER"` | Var olan uzantıya eklemeli dördüncü JWT şeması (§8.1). |
| Security (DL-2) | `TrustedDocumentRepositoryConsumerRequestExecutor` (+ `ITrustedDocumentRepositoryConsumerRequestExecutor`), `TrustedDocumentRepositoryConsumerIdentity` | Başlık ön kontrolü, şema doğrulaması, kapalı liste, kiracı bağlama, ret denetimi; emsal `TrustedWorkflowConsumerRequestExecutor`. |
| Configuration (DL-2) | `TrustedDocumentRepositoryConsumerAuthorizationOptions` (bölüm `TrustedDocumentRepositoryConsumerAuthorization`) | Kapalı tüketici listesi; emsal `TrustedWorkflowStartAuthorizationOptions`. |
| Command (DL-2) | `StoreTrustedRepositoryObjectCommand`, `CompensateTrustedRepositoryObjectCommand` | `IAuditableCommand` + `IAuditMetadataProvider` (yol `a`, §21). |
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
| `TrustedConsumerClientId` | Guid? | DL-2 | **Yeni, eklemeli.** Güvenilen tüketici yolunun yazdığı satırda servis token'ının `sub`'ı; kullanıcı ve süreç içi yolunun satırlarında `null`. Ad emsali `WorkflowInstance.TrustedConsumerClientId`. |
| `TrustedConsumerModule` | string? | DL-2 | **Yeni, eklemeli.** Kapalı listedeki tüketici modülü (v1'de yalnız `MOD-0217`); kullanıcı yolunda `null`. |

`CreatedBy`: güvenilen tüketici yolunda `service:Diten.MDM:{clientId}` biçiminde yazılır (kullanıcı yolunun `ICurrentUserContext.ActorName` değeriyle hiçbir zaman eşleşmez; `DocumentRepositoryAccessPolicy.IsCreator` karşılaştırması bu satırlara kullanıcıya yetki açamaz).

**Mongo index needs**
- `{ TenantId: 1, ContentId: 1 }` — unique.
- `{ TenantId: 1, StorageProvider: 1, ObjectKey: 1 }` — unique; backs FU03 orphan detection.
- `{ TenantId: 1, OwningItemId: 1, OwningVersionId: 1 }`.
- DL-2 yeni indeks getirmez: güvenilen yol satırı `{TenantId, ContentId}` ile bulur, `TrustedConsumerClientId` / `TrustedConsumerModule` / `Scope` eşleşmesini bellekte denetler. İki yeni alan eksik satırda `null` okunur (eski satırlar kullanıcı yolunun satırıdır).
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
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Services/DocumentRepositoryService.cs     ← güvenilen yöntemler + kullanıcı yolunda tüketici satırı dışlaması
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Services/DocumentRepositoryAccessPolicy.cs   ← tüketici satırı dışlaması
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/DocumentRepositoryModels.cs   ← güvenilen girdi kayıtları
services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/DocumentRepositoryReasonCodes.cs   ← yeni ret kodları (§13)
services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentRepository/RepositoryObject.cs   ← iki eklemeli alan
services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/AuditActorTypeResolver.cs   ← YALNIZ K-B kararı (a) ve XMC-001 (MOD-0021) onayıyla: "service" eşlemesi
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/TrustedDocumentRepositoryConsumer*Tests.cs   ← NEW
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedServiceTokenValidationTests.cs   ← audience ayrımı vakaları
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/LoginOnlyEndpointGuardTests.cs     ← TrustedServiceSchemeControllers girdisi
services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/**                                          ← yalnız resolver eşlemesi testi (K-B (a) ile)
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
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**` — MOD-0021'in; DL-2 yalnız K-B (a) kararı ve XMC-001 onayıyla `AuditActorTypeResolver.cs`'e tek eşleme ekler
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
| **DL-2 — MOD-0021: servis aktörünün denetim kaydı** (`AuditActorTypeResolver.FromActorTypeClaim` bugün `service` değerini `Unknown`'a eşler; `AuditService` `Unknown` aktörlü kaydı reddeder; `AuditBehavior` bunu yalnız uyarı loguyla geçer — kayıt düşmez) | **XMC-001** — MOD-0021 dosyası; CT kararı K-B | ⛔ karar yok. Sahip: MOD-0021 sahibi (platform-shared-services) + CT. Tarih: **TBD**. K-B kararı olmadan DL-2 başlamaz: aksi halde güvenilen yazmalar sessizce denetimsiz kalır. |
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
| Auth imzalı servis token'ı (MOD-0033-FU02: RS256, tam 10 talep, 300 sn ömür, `actor_type=service`, `service_name=Diten.MDM`, `tenant_id`, audience başına ayrı şema) | `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs` → `AddTrustedServiceTokenValidation`, `HasExactServiceClaims`; tüketen `InternalTrustedWorkflowConsumerController` + `TrustedWorkflowConsumerRequestExecutor`, `VerifiedReferenceDataServiceTenantContext` | **Seçildi.** Kiracı Auth'un kiracı tahsisiyle imzalı gelir (başlık / gövde değil); audience başına ayrı şema yanlış amaçlı token'ı reddeder (MOD-0033-FU02 Section D: "Audit and Workflow identities cannot substitute for this identity"); MDM tarafında aynı desenin sağlayıcıları var (`AuthProductIdentityWorkflowServiceIdentityProvider`, `AuthWorkflowVerifiedMarketServiceIdentityProvider`; Auth ucu `/api/internal/v1/auth/service-tokens/issue`). Yeni şema uydurulmaz: var olan uzantıya **dördüncü audience** eklenir. |

**Kimlik doğrulama.**
- Şema `TrustedDocumentRepositoryConsumerService`, `AddTrustedServiceTokenValidation` içinde `CreateTokenValidationParameters(options, clock, DocumentRepositoryRequiredAudience)` ve `HasExactServiceClaims(token, issuer, DocumentRepositoryRequiredAudience)` ile — doğrulama parametreleri, anahtar dönüşü ve talep sözleşmesi diğer üç şemayla birebir aynı; yalnız audience farklı.
- Kimlik (`TrustedDocumentRepositoryConsumerIdentity`): `ClientId` = `sub`, `TenantId` = `tenant_id`, `TokenId` = `jti`, `ServiceName` = `Diten.MDM`, `Audience`. Biri eksik / çift / biçimsiz → 403.
- Token izin talebi taşımaz (`RoleClaimType = "service_identity_role_forbidden"`); servis bir kullanıcı izniyle geçmez (§14).

**Başlık ön kontrolü (gövde okunmadan, emsal `TrustedWorkflowConsumerRequestExecutor.Preflight`).**
- Tam bir `Authorization: Bearer <token>`; yok → 401; birden çok → 403.
- `X-Tenant-Id` başlığı varsa → 403 (kiracı yalnız token'dan).
- `X-Delegated-Authorization` başlığı varsa → 403 (D56/2: kullanıcı token'ı iletilmez; bu yol temsilci kullanıcı kabul etmez).
- `X-Correlation-Id` kabul edilir ve denetim kaydının `CorrelationId`'sine yazılır (`routes.md`); yoksa sunucu üretir.
- Kimlik doğrulama şeması yapılandırılmamış / geçersiz (`InvalidOperationException`) → 503 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAVAILABLE`.

**Kapalı tüketici listesi.**
- Yapılandırma `TrustedDocumentRepositoryConsumerAuthorization:Entries[]`: `{ ClientId, ServiceName, Audience, ConsumerModule, Scopes[] }`; en çok 8 girdi; tekrarlayan `ClientId` geçersiz.
- Kodda sabit izinli tüketici kümesi: `{ ("Diten.MDM", "MOD-0217", Scopes ⊆ {"Documents"}) }`. Yapılandırmadaki bir girdi bu kümenin dışındaysa (başka modül, başka servis, başka scope) **yapılandırmanın tamamı geçersizdir** → her güvenilen çağrı 503 (kapalı-başarısız; emsal `TrustedLegalEntityScopeCredentialAuthenticator.IsValidConfiguration` + `FirstConsumerPermissions`). Yalnız yapılandırma yazarak tüketici eklenemez.
- Token geçerli ama `ClientId` listede yok → 403 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_FORBIDDEN`.
- **Listeye ekleme kuralı:** yeni tüketici (başka modül ya da başka scope) = bu paketin revizyonu (yeni dilim ya da AC) + bağımsız inceleme + approvals.tsv onay satırı + kodda sabit kümeye ekleme + Auth'ta o servis için audience / istemci (XMC-001, Ali) + yapılandırma girdisi. Aynı MDM istemcisini ikinci bir MDM modülü paylaşamaz: modül ayrımı `ClientId` ile yapılır, ikinci modül ayrı istemci kimliği ister (MOD-0033-FU02: "identity AllowedAudience remains singular").

**Kiracı ve şirket kapsamı.**
- Kiracı: **yalnız** token'ın `tenant_id` talebi; `TenantScope.Begin(_tenantContext, identity.TenantId)` içinde komut / sorgu çalışır. Gövdede ya da formda `tenantId` alanı → 400 (bilinmeyen alan).
- Başka kiracının `ContentId`'si → 404 `NOT_FOUND_NON_LEAKAGE` (kiracı süzgeci + nesne anahtarındaki kiracı; 403 değil).
- Şirket: yüklemede multipart form alanı `companyId` (zorunlu, boş olmayan Guid) satıra yazılır. Platform şirketin sözleşme kapsamını **denetlemez**: SEC-002 kapsamı ve `mdm.contracts.documents.*` MOD-0217'de, Platform'u çağırmadan önce denetlenir (MOD-0217 AC-27). Okuma ve telafi şirket parametresi almaz.

**Tüketiciye bağlı görünürlük.**
- Güvenilen yükleme satıra `TrustedConsumerClientId = sub` ve `TrustedConsumerModule = <listedeki modül>` yazar.
- Güvenilen meta / içerik / telafi yalnız şu satırı görür: aynı kiracı **ve** `TrustedConsumerClientId == sub` **ve** `TrustedConsumerModule == girdinin modülü` **ve** `Scope ∈ girdinin Scopes`'u. Diğer her satır (aynı kiracıdaki MOD-0029 `Documents` satırları dahil) → 404.
- Kullanıcı yolu (`api/v1/document-repository/*`) `TrustedConsumerClientId` dolu satırı **görmez**: meta / içerik / telafi 404 — `platform.document-repository.read` / `.manage` taşıyan platform yöneticisi token'ı dahil (D56/2: indirme yalnız MOD-0217 uçlarıyla). Kullanıcı yolunun kendi satırlarındaki davranışı değişmez.

**Akış ve bütçe.**
- Yükleme `MultipartReader` ile okunur; denetleyicide `IFormFile`, `IFormCollection`, `[FromForm]` ve `Request.ReadFormAsync` yoktur; çerçevenin geçici dosyası oluşmaz. Form alanları (`owningItemId`, `owningVersionId`, `companyId`, `scope`) dosya bölümünden **önce** gelir; dosya bölümü adı `file`, tek. Kurallar §12.
- Boyut sınırı `ContentStorageOptions.MaxFileSizeBytes` kopyalama sırasında uygulanır (DL-1 sağlayıcısı); uç `RequestSizeLimit` ile bu değeri aşamaz.
- Başlık + kimlik doğrulama + kapalı liste aşaması 2 sn bütçeyle sınırlıdır, aşılırsa 504 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TIMEOUT` ve depolama çağrılmaz (emsal `ExecuteWithBudgetAsync`); **içerik kopyası ve akış bu bütçeye bağlı değildir** (emsal yürütücünün 32 KiB gövde ve 2 sn toplam bütçesi dosya için kullanılmaz).

**Ağ yolu.** `api/internal/*` Gateway'den yönlendirilmez (ölçüldü: `ocelot.json`'da `api/internal` 0 eşleşme; `LoginOnlyEndpointGuardTests` güvenilen şema denetleyicisini `api/internal` altında ister). MdmService ucu Platform'un iç adresinden doğrudan çağırır (emsal: MDM `PlatformProductIdentityWorkflowClient` + `PlatformBaseUrl`). MOD-0217 Runtime Constraints "Gateway üzerinden" diyor → açık soru S-1 (Treasury CT, §19).

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
| Scope | Must be a known `ContentStorageScope` value. DL-2: ayrıca girdinin `Scopes` listesinde (v1 yalnız `Documents`) → değilse 403. |
| DL-2 multipart biçimi | `Content-Type: multipart/form-data` ve sınır (boundary) zorunlu → değilse 400. Tam bir dosya bölümü `file`; iki dosya → 400. Form alanları `owningItemId`, `owningVersionId`, `companyId` (her biri boş olmayan Guid), `scope` (enum adı) dosyadan önce, her biri tam bir kez; eksik / tekrar / dosyadan sonra gelen / bilinmeyen alan → 400 ve depolama çağrılmaz. |
| DL-2 `companyId` | Zorunlu, `Guid.Empty` değil → değilse 400. |

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
| DL-2: `Authorization` yok / Bearer biçimsiz | 401 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAUTHENTICATED`; komut çalışmaz. |
| DL-2: token imzası / ömrü / talep sözleşmesi geçersiz, ya da başka audience (`TRUSTED_WORKFLOW_CONSUMER`, `TRUSTED_AUDIT_SOURCE_INGEST`, `TRUSTED_REFERENCE_DATA_CONSUMER`) ya da kullanıcı JWT'si | 401 (şema reddi). |
| DL-2: çift `Authorization`, `X-Tenant-Id` var, `X-Delegated-Authorization` var | 403 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_FORBIDDEN`; gövde okunmaz. |
| DL-2: token geçerli, `ClientId` kapalı listede yok | 403; denetim kaydı `Denied`. |
| DL-2: scope girdinin listesinde değil (ör. `Templates`, `CrmVisitEvidence`) | 403; depolama çağrılmaz. |
| DL-2: kapalı liste yapılandırması geçersiz ya da kimlik doğrulama şeması yapılandırılmamış | 503 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_UNAVAILABLE` (MOD-0217 bunu 503 `MDM_CONTRACT_DOCUMENT_STORE_UNAVAILABLE`'a eşler). |
| DL-2: başlık + kimlik doğrulama + kapalı liste aşaması 2 sn'yi aştı | 504 `DOCUMENT_REPOSITORY_TRUSTED_CONSUMER_TIMEOUT`; depolama çağrılmaz (AC-27). |
| DL-2: başka tüketicinin / kullanıcı yolunun / başka kiracının nesnesi | 404 `NOT_FOUND_NON_LEAKAGE`. |
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
- Yeni izin anahtarı **yok**. Servis kimliği kullanıcı izniyle geçmez: kapı, sınıf düzeyindeki `[Authorize(AuthenticationSchemes = "TrustedDocumentRepositoryConsumerService")]` + kapalı tüketici listesi + scope listesi + tüketiciye bağlı görünürlüktür (§8.1). Denetleyicide `[HasPermission]`, `[AllowAnonymous]`, `[LoginOnly]` yoktur.
- Bu, `LoginOnlyEndpointGuardTests.TrustedServiceSchemeControllers`'ın gözden geçirilmiş istisna mekanizmasıdır ("no user permission can be the gate, because the caller is another service"); denetleyici bu sözlüğe şemasıyla eklenir ve `api/internal` altında durur (`Every_authentication_scheme_gate_is_a_reviewed_internal_trusted_service_controller`).
- Kullanıcılara `platform.document-repository.read` / `.manage` verilmez (D56/2; rol şablonları Auth'ta — Platform bunu zorlamaz, ama tüketiciye bağlı nesneyi kullanıcı yolundan gizleyerek indirmenin yalnız MOD-0217'den geçmesini sağlar). Ölçüldü: genel `read` / `.manage` kiracı rota düzeltmelerinde yok, PlatformAdmin kapsamında kalır (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/TenantRouteScopeCorrections.cs` → visit-evidence satırlarının üstündeki not).
- **Aktör:** çağrı **servis olarak** kaydedilir, kullanıcı adına değil (D56/2: kullanıcı token'ı iletilmez). Kullanıcı düzeyindeki iz MOD-0217'nindir (niyet kaydı, AC-62 indirme denetimi); Platform kaydı `CorrelationId` ile bağlanır. Platform doğrulanmamış bir "kullanıcı kimliği" başlığını kabul etmez ve kaydetmez.
- Aktör türünün değeri (`Service` mı `System` + `SourceService` mı) K-B kararıdır (§19).

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
- [ ] AC-16 (zorunlu) — DL-2 yerleşimi: `InternalTrustedDocumentRepositoryConsumerController` rotası `api/internal/v1/document-repository/trusted-consumer`, sınıf düzeyinde tek `[Authorize(AuthenticationSchemes = "TrustedDocumentRepositoryConsumerService")]`, eylemlerinde şema / `[HasPermission]` / `[AllowAnonymous]` / `[LoginOnly]` yok ve `LoginOnlyEndpointGuardTests.TrustedServiceSchemeControllers`'ta şemasıyla kayıtlı; sabotaj: sözlük girdisi silinince ya da rota `api/v1` altına taşınınca guard testi kırmızı.
- [ ] AC-17 (zorunlu) — DL-2 başlık kuralları gerçek HTTP host testinde dört ayrı vakayla: `Authorization` yok → 401, çift `Authorization` → 403, `X-Tenant-Id` var → 403, `X-Delegated-Authorization` var → 403; dördünde de komut / sorgu çalışmaz ve depolama sağlayıcısı çağrılmaz (sahte sağlayıcı çağrı sayısı 0); sabotaj: `X-Tenant-Id` denetimi kaldırılınca ilgili vaka kırmızı.
- [ ] AC-18 (zorunlu) — DL-2 kiracı: A kiracısının token'ıyla yüklenen nesne için B kiracısının token'ıyla meta, içerik ve telafi üçü de 404 döner ve A'nın nesnesi ile satırı değişmez; formda `tenantId` alanı 400 döner; sabotaj: kiracı form alanından okunacak biçimde değiştirilince test kırmızı.
- [ ] AC-19 (zorunlu) — DL-2 kapalı liste: listede olmayan `ClientId` → 403 ve tek `Denied` denetim kaydı; girdinin scope listesinde olmayan `Templates` ve `CrmVisitEvidence` ayrı vakalarda 403 ve depolama çağrısı 0; yapılandırmada kod kümesi dışında bir girdi (`ConsumerModule = MOD-9999`) varken her güvenilen çağrı 503; sabotaj: koddaki sabit küme denetimi kaldırılınca `MOD-9999` vakası kırmızı.
- [ ] AC-20 (zorunlu) — DL-2 akış: mimari test güvenilen denetleyicide `IFormFile` / `IFormCollection` parametresi, `[FromForm]` özniteliği ve `ReadFormAsync` çağrısı olmadığını, `MultipartReader` kullanıldığını doğrular; host testinde `IFormFeature.ReadFormAsync` çağrılınca istisna atan bir özellikle yükleme başarılı olur; sabotaj: `IFormFile` parametresi eklenince mimari test, form okunursa host testi kırmızı.
- [ ] AC-21 (zorunlu) — DL-2 multipart biçimi: dosyadan sonra gelen alan, eksik `companyId`, `Guid.Empty` `owningItemId`, tekrarlanan `scope`, bilinmeyen alan, iki dosya bölümü ve `boundary` parametresi olmayan içerik türü ayrı fixture'larla 400 döner ve her birinde depolama çağrısı 0, `RepositoryObject` koleksiyonunun satır sayısı değişmez.
- [ ] AC-22 (zorunlu) — DL-2 tüketici bağı: güvenilen yükleme satıra `TrustedConsumerClientId = sub`, `TrustedConsumerModule = MOD-0217`, `CreatedBy = service:Diten.MDM:{clientId}` yazar (gerçek Mongo testiyle okunur); aynı kiracıda kullanıcı yoluyla (`Documents`) yazılmış satır ve başka `ClientId`'li satır güvenilen meta / içerik / telafide 404; sabotaj: `TrustedConsumerClientId` eşleşmesi kaldırılınca test kırmızı.
- [ ] AC-23 (zorunlu) — DL-2 kullanıcı yolu dışlaması: `TrustedConsumerClientId` dolu satır `api/v1/document-repository` meta, içerik ve telafide `platform.document-repository.read` + `.manage` taşıyan platform yöneticisi token'ıyla 404 döner; kullanıcı yolunun kendi satırları için DL-1 testleri (`DocumentBinaryStoreTests`, `CrmVisitEvidenceScopeTests`, `ContentMessagingArtifactsScopeTests`) yeşil kalır; sabotaj: dışlama kaldırılınca test kırmızı.
- [ ] AC-24 (zorunlu) — DL-2 kullanıcı izni gerekmez: izin talebi taşımayan geçerli servis token'ıyla yükleme, meta, içerik ve telafi başarılıdır; `tenant_user` JWT'si (`read` + `manage` izinli) güvenilen uca gönderilince 401 döner; sabotaj: denetleyiciye varsayılan JWT şeması eklenince test kırmızı.
- [ ] AC-25 (zorunlu) — DL-2 başarı denetimi: güvenilen yükleme, telafi ve içerik okuma her biri tam bir merkezi denetim kaydı üretir (K-B'nin seçtiği aktör türü, `ActorId = ClientId`, kiracı = token kiracısı, `EntityType = RepositoryObject`, `EntityId = ContentId`, `Outcome = Succeeded`, `CorrelationId = X-Correlation-Id`, metadata `callerService`, `consumerModule`, `audience`, `scope`, `operation`); kayıtta token, dosya içeriği ve `ObjectKey` yoktur; başarılı meta okuma kayıt üretmez (sayı 0 ile ölçülür); sabotaj: aktör türü eşlemesi kaldırılınca yükleme vakasında kayıt sayısı 0 olur ve test kırmızı.
- [ ] AC-26 (zorunlu) — DL-2 ret denetimi: kimliği doğrulanmış servis için kapalı liste reddi, scope reddi, tüketici bağı / kiracı 404'ü ve multipart 400'ü her biri token kiracısıyla tam bir `Denied` ya da `Failed` kaydı üretir (`ReasonCode` metadata'da); kimliği doğrulanmamış 401 tek bir platform geneli `Denied` kaydı üretir (yalnız uç ve ret kodu; doğrulanmamış kiracı / özne / token yazılmaz); denetim yazımı başarısızken ret aynı durum koduyla kalır; her ret sınıfı ayrı testtir.
- [ ] AC-27 (zorunlu) — DL-2 bütçe: içerik kopyası 2 sn kimlik doğrulama bütçesine bağlı değildir — 3 sn boyunca parça parça gelen 5 MiB'lik yükleme ve 3 sn süren içerik akışı başarılıdır; kimlik doğrulama aşaması 2 sn'yi aşınca 504 döner ve depolama çağrılmaz.
- [ ] AC-28 (zorunlu) — DL-2 kapsamı: DL-2 değişiklik kümesi (`git diff --name-only <taban>..<dl-2 ucu>`) yalnız §5 DL-2 listesindeki yollardadır; `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `gateway/**` ve `frontend/**` altında değişen dosya 0'dır.
- [ ] AC-29 (zorunlu) — DL-2 canlı kayıt (dilim kabul kapısı; Auth bağımlılığı gelince): dev ortamında Auth'un verdiği `Diten.MDM / TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` token'ıyla yükleme → meta → içerik → telafi zinciri, başka kiracı token'ıyla 404, kullanıcı yolundan 404 ve her adımın `audit_events` satırı kayıt dosyasında komut çıktısıyla gösterilir (MOD-0217 Ready-for-dev Checklist "MOD-0262-FU01 canlı doğrulama kaydı" maddesinin kanıtı).
- [ ] AC-30 (zorunlu) — DL-2 telafi sınırı: güvenilen telafi yalnız `CompensateRowAsync` yolunu çalıştırır, saklama / yasal tutma denetimi ve fiziksel imha yolu eklemez; AD-6 negatif yetenek testi güvenilen denetleyiciyi de kapsar (purge / destroy / delete adlı eylem 0).

---

## 17. Test Expectations

| Layer | Expectation |
|---|---|
| Unit | Validation matrix (§12), object-key determinism + tenant separation, checksum computation, compensation-on-metadata-failure. DL-2: kapalı liste yapılandırma doğrulaması (geçerli / kod kümesi dışı / tekrar / 8'den fazla girdi), `DocumentRepositoryAccessPolicy` tüketici satırı dışlaması, multipart alan sırası ayrıştırıcısı. |
| Integration (Mongo) | Must use `MongoIntegrationHarness`. Index creation, class-map round-trip, standalone-Mongo transaction guard. DL-2: iki yeni alanın gerçek Mongo'da yazılıp okunması (AC-22), eski satırda `null` okunması. |
| Authorization | Unauthenticated `401`; under-privileged `403`; cross-tenant `404` by `ContentId` and by raw `ObjectKey`. DL-2: AC-15…AC-19, AC-22…AC-24 — gerçek HTTP host (`WebApplicationFactory`; emsal `Audit/AuditBehaviorActorTypeHttpTests.cs`, `Authorization/AuthorizationProbeControllerIntegrationTests.cs`), test anahtarıyla imzalı RS256 token. Yürütücü birim düzeyinde ayrıca `TrustedWorkflowConsumerSecurityTests` deseniyle (kayıt tutan kimlik doğrulama servisi) sınanır; bu birim testi host testinin yerini tutmaz. |
| Contract | Serialisation test: `ObjectKey` absent from responses; no `byte[]` in any request/response DTO. DL-2 yanıtları aynı `RepositoryObjectModel`'dir. |
| Audit | DL-2: AC-25, AC-26 — gerçek `AuditBehavior` + `IAuditService` ile kayıt sayımı (sahte değil); aktör türü eşlemesi sabotajı. `AuditActorTypeResolverSourceGuardTests` ve `AuditTrailStandardTests` yeşil. |
| Negative-capability | Assert the absence of a purge/destroy code path (DL-2 denetleyicisi dahil, AC-30). |
| Architecture | DL-2: AC-16 (`LoginOnlyEndpointGuardTests`), AC-20 (`IFormFile` / `[FromForm]` / `ReadFormAsync` yok), AC-28 (yol kapsamı). |
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
- [x] **Maximum object size** → **config-driven, default greater than 50 MB.** The legacy 50 MB `RequestSizeLimit` was a buffered-upload symptom; **AD-4's streaming contract removes that memory constraint.** The exact per-environment value is set at implementation time.

**Kickoff-infrastructure confirmations — verified in `/add-module` Phase 0 (non-blocking)**

- [x] Storage-root configuration and allow-lists confirmed for each environment. → *Phase 0, 2026-09-08: `ContentStorageOptions` SectionName preserved; appsettings unchanged.*
- [x] Gateway route + multipart/stream pass-through confirmed with the integration agent. → *Phase 0, 2026-09-08: ocelot route added (surgical, 40-line addition; `IsBodilessStatus` guard).*
- [x] Audit event taxonomy for repository operations agreed with the MOD-0021 owner. → *Phase 0, 2026-09-08: `AuditCategory.DocumentManagement`. Live emission still pending smoke (see §16 / §19 ⚠️ partial closure).*
- [x] Confirmation that FU01 introduces **no** permission seed, menu entry, or migration without its own record. → *Phase 0, 2026-09-08: no seed; keys auto-register via `PlatformPermissionAutoRegistrationWorker` at startup.*

**r2 paket kapısı (bu sürümün dağıtımından önce)**

- [ ] r2 için bağımsız inceleme (CT-APR-02, yazandan ayrı oturum) — `execution/approvals/reviews.tsv` satırı ve bulgu dosyası; `UYGUN DEĞİL` ya da açık ENGEL yok.
- [ ] r2 için `execution/approvals/approvals.tsv` onay satırı (yüksek risk: güvenlik / kimlik doğrulama / kiracı / ürün kodu yolu — `high: evet` olan ve paketi yazmamış onaylayıcı).
- [ ] K-A (DL-2'nin bu paketin dilimi olması), K-B (servis aktörünün denetim yolu), K-C (kullanıcı yolu dışlaması) CT kararları (§19).

**Dilim kapıları ("— DL-2 için"; paketi değil yalnız DL-2'yi bekletir)**

- [ ] K-B kararı ve MOD-0021 XMC-001 onayı — DL-2 için (başlamadan önce).
- [ ] Auth audience `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` main'de (Ali — Auth, TBD; XMC-001) — DL-2 kabul kapısı için.
- [ ] MDM servis istemcisi + dev kiracı tahsisi + Platform kapalı listesi `ClientId` (Ali — Auth, TBD) — DL-2 kabul kapısı için.
- [ ] S-1 (ağ yolu) Treasury CT yanıtı — DL-2 için.

---

## 19. Implementation Notes

- **Decisions are locked.** CONTROL TOWER closed OD-1, OD-3, OD-4 and OD-A on **2026-09-07** and promoted this pack to `ready-for-dev` (see §18). Implementation must not reopen them: the seam relocation stays inside FU01, the permission namespace is `platform.document-repository.*`, FU01 alone carries the R1 commitment, the relocated seam lands inside `Diten.Platform.Application` (not `Diten.Common`), and the maximum object size is config-driven with a default above 50 MB.
- **Why this FU exists in this shape.** Blueprint puts MOD-0262 at W-1 / R1 / RC=Y, yet its own `Dependency Gate` names MOD-0030 (W-3) and MOD-0031 (W-4). CT decision **D-3** refused to move the module's wave and instead sliced the FUs. FU01 is the slice that survives that inversion.
- **The seam is preserved deliberately.** `IContentStorageGateway` / `ContentRef` already abstract **eight** consumers; DCP-008 **AD-2** keeps them. This FU fills in behind the seam and re-homes it — it is not a rewrite.
- **In-process by decision, HTTP-shaped by design.** DCP-008 **AD-3**: R1 stays inside `Diten.Platform`; the contract is nevertheless designed for a boundary so FU07's extraction does not force a redesign.
- **The two hard risks this FU closes** (measured in `docs/analysis/mod-0262-internal-document-repository-prd-and-impact-analysis.md`): `ContentStoreRequest.Content` as `byte[]`, and the seam's written instruction that callers perform authorisation.
- **Retention boundary is untouched.** `DocumentDispositionService` states in code that it never deletes and that destruction is "a deliberate future task". FU01 does not become that task — FU05 does, and only once MOD-0030 has an owner.
- **`docs/platform/master-plan.md` still describes MOD-0262 as an external provider that may be skipped** (3 locations, out of `execution/` scope and therefore not corrected in the canonicalization pass). Treat this pack and the registry as authoritative; the legacy plan is stale.
- **Pack↔code reconciliation (2026-09-08, from FU01 `/add-module` Phase 0).** Three record drifts corrected against the implemented code: (a) **eight** consumers, not six — `TemplateMasterService` and `TemplateVariantService` also route through `DocumentVersioningService`; (b) `entity_base` is **`TenantScopedEntity`**, not `BaseEntity` (`TenantRepository<T>` requires it; the standard's table lagged the code); (c) **no `RegisterClassMaps`** in `Diten.Platform` — it uses a global `GuidSerializer(Standard)`, and the class-map trap is CrmService-specific (correctly not invented).
- **⚠️ KAPANIŞ (KISMİ) — 2026-09-08 (DK#10).** Code complete; static verification green — 13/14 §16 boxes (audit-event box is code-wired, **not** live-verified), full solution build green, the eight consumers behaviourally unchanged (ControlledDocument 49/49, Template 222/222), `verify_module_id` `--check-id`/`--check-all` exit 0 / 0 HARD. **Runtime smoke NOT run** (fleet down, 5057/5000 closed) → per DK#10 this is **not** ✅. Full suite 3758 pass / 155 pre-existing-or-environmental FAIL (77 macOS-mongod-path, 49 BSON-Timestamp, ~26 MOD-0029 domain + BL-279, 3 MOD-0029 attribution — none FU01's; the 3 attribution warrant a pre-FU01 baseline confirmation). Live-verification list owned by CONTROL TOWER: multipart `POST …/document-repository/objects`→201+ContentRef · streamed `GET …/{contentId}/content` · cross-tenant 404 (ContentId **and** raw ObjectKey) · 401/403 authz · audit events reaching MOD-0021 · four indexes present in the live DB · MOD-0029 upload/download unbroken. Keys `platform.document-repository.read|manage` auto-register at Platform startup (`PlatformPermissionAutoRegistrationWorker`) and land on the full-catalog SuperAdmin role → smoke uses a **platform-admin** token (no manual grant, no raw-Mongo rolePermissions write).
- **Branch reconciliation (2026-09-08).** Frontmatter `branch:` was the proposed `feature/pss/mod-0262-…`; CT declined a worktree and kept work on `feature/structured-content-messaging` (the active multi-workstream branch). Field updated to match. Commit/push deferred by CT until all workstreams are done. DL-2 dalı dağıtım anında kararlaştırılır; `branch:` alanı r1 tarihçesidir.
- **r2 revizyonu (2026-11-02) — DL-2 servis kimliği.** İstek: MOD-0217 CLM (origin/main `execution/domains/master-data-management/module-packs/MOD-0217-contract-lifecycle-management-clm.md` — revizyon notu "D56 + DL-2 hazırlık bulguları", Runtime Constraints "Çağrı kimliği", Failure Path "Belge deposu servis kimliği (KARAR D56/2)", Authorization Convention "Belge deposu çağrıları servis kimliğiyle", AC-24, AC-27, Ready-for-dev Checklist "Dilim kapıları" — "MOD-0262-FU01'de servis kimliği desteği" ve "MOD-0262-FU01 canlı doğrulama kaydı" maddeleri) ve karar metni `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` "D56 — DL-2 hazırlığında çıkan dört karar" madde 2. Tüketici sahibi Treasury CT; Platform sahibi Beste. r1 hiçbir kararı değişmedi; yalnız §14'teki OD-3 satırının "open" ifadesi §18 ile çelişiyordu, mekanik olarak düzeltildi. r1'in numarasız AC'leri AC-1…AC-14 olarak numaralandı (metin aynı).
- **r2 ölçüm notları (2026-11-02, HEAD 925ea4e34; origin/main 743f42d1c — DL-1 yollarında iki ref aynı).** (a) DL-1 kodu main'de: Repo Scope'taki her DL-1 yolu iki ref'te var. (b) `DocumentRepositoryController.Store` `[FromForm] IFormFile` bağlıyor: ASP.NET form bağlaması gövdeyi eylemden önce okur (büyük dosyada geçici dosyaya), denetleyici yorumundaki "streams straight from the request" iddiası tam değildir — DL-1 kullanıcı yolu DL-2'de değişmez, takip §20. (c) `OpenRepositoryObjectContentQuery` ve `GetRepositoryObjectByIdQuery` denetim işareti taşımıyor: AC-8'in "read" kısmı kodda yok (DL-1 açığı, K-D). (d) `AuditActorTypeResolver.FromActorTypeClaim` yalnız `tenant_user` / `platform_admin` / `partner_admin` tanır; servis token'ı (`actor_type=service`) `Unknown` olur, `AuditService` `Unknown` aktörü reddeder, `AuditBehavior` yalnız uyarı loglar — DL-2 bunu çözmeden güvenilen yazmalar sessizce denetimsiz kalır (K-B). (e) `AuditActorType.Service` enum'da var ve kullanılıyor (`ESignature/Handlers/CatalogHandlers.cs`, `AuditOutboxRepository.cs`); AUD-001 §3 tablosu servis çağrısı için `ActorType = System` + `SourceService` yazıyor — çelişki K-B'de. (f) Ret denetimi emsali: `PartnerAdminScopeAttribute.RecordAsync` (`AuditOperation.PermissionDenied`, `AuditOutcome.Denied`, `IsPlatformGlobal`). (g) DL-2 adları kodda yok (`grep` 0); `TrustedConsumerClientId` adı `WorkflowInstance.TrustedConsumerClientId` emsalinden.
- **Plan hizalaması.** Modülün plan satırı bu revizyona verilmedi (plan depoda değildir) → Teslim dilimleri tablosunda `plan satırı yok`. DL-2 yeni bir plan MVP'si ya da yetenek kimliği getirdiği ölçülemedi; D56/2 metni desteği açıkça "MOD-0262-FU01'de" istediği için aynı pakette yeni dilim olarak yazıldı — takip paketi (`MOD-0262-FUxx`) gerekip gerekmediği K-A.
- **CT kararı gereken noktalar (r2).**
  - **K-A** — DL-2 bu paketin dilimi mi, yoksa ayrı takip paketi mi (module-pack-standard Bölüm 16 "Plan hizalaması")? Öneri: bu paketin DL-2'si (D56/2 metni; FU02…FU07 başka içeriğe ayrılmış).
  - **K-B** — Servis aktörünün denetim yolu: (a) `AuditActorTypeResolver.FromActorTypeClaim`'e `"service" => AuditActorType.Service` eşlemesi (MOD-0021 dosyası, XMC-001; yan etki: servis principal'ıyla çalışan diğer `IAuditableCommand`'lar da kayıt üretmeye başlar — DL-2 başında ölçülüp raporlanır) ya da (b) güvenilen komutlarda açık `IAuditService.AppendAsync` (yeni iz → `tests/architecture/audit-ledger/Diten.Platform.md` ve `AuditTrailStandardTests.AcceptedTrails` CT kararı). Ayrıca aktör türü değeri: `Service` (enum + emsal) mı, AUD-001 §3'teki `System` + `SourceService` mı. Öneri: (a) + `Service`, AUD-001 §3 satırının CT tarafından hizalanması.
  - **K-C** — Kullanıcı yolunda tüketiciye bağlı nesnenin 404'ü (platform yöneticisi dahil). Öneri: evet — D56/2 "indirme yalnız MOD-0217 uçlarıyla".
  - **K-D** — AC-8'in okuma kısmının DL-1 açığı (kullanıcı yolunda içerik okuma denetlenmiyor) DL-2'de mi kapanır, ayrı düzeltme mi? Öneri: ayrı düzeltme (DL-2 kapsamını büyütmez).
- **Açık sorular.**
  - **S-1 (Treasury CT):** MOD-0217 Runtime Constraints MdmService → MOD-0262 çağrısını "Gateway üzerinden" yazıyor; güvenilen uç `api/internal` altında ve Gateway'den yönlendirilmiyor. MOD-0217 metni iç adrese (`PlatformBaseUrl` deseni) çekilsin mi?
  - **S-2 (Treasury CT + Platform):** Yükleme yanıtı kaybolursa nesne işaretçisiz kalır. DL-2'ye `Idempotency-Key` eklensin mi, yoksa MOD-0262-FU03 mutabakatına mı bırakılsın? Öneri: FU03.
  - **S-3 (Ali — Auth):** Audience adı `TRUSTED_DOCUMENT_REPOSITORY_CONSUMER` ve MDM için ayrı istemci kimliği uygun mu; tarih?
  - **S-4 (Treasury CT):** v1 scope listesi yalnız `Documents` (D41/10'un geçerli kısmı). MOD-0217 başka scope isterse §8.1 ekleme kuralı işler.

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
| Kullanıcı yolu yüklemesinin `IFormFile` yerine `MultipartReader` akışına geçmesi (§19 ölçüm notu b) | ayrı düzeltme; CT sırası |
| Kullanıcı yolu içerik okuma denetimi (AC-8 okuma kısmı, K-D) | ayrı düzeltme; CT sırası |
| İkinci güvenilen tüketici (başka modül / scope) | bu paketin revizyonu — §8.1 ekleme kuralı |

---

## 21. Audited Events

Servisin denetim altyapısı var: `tests/architecture/audit-ledger/Diten.Platform.md` → `## İzler` → `platform-merkezi` (yol `a`, işaret `IAuditableCommand+IAuditMetadataProvider+!IAuditExcludedRequest+!ITransactionOwnedAuditCommand`). Olay adı `AuditBehavior`'ın yazdığı `RequestType`'tır (komut türünün adı). K2 sınıfı değil: bu yollardan geçen nesne sözleşme belgesi ve genel ikilidir; GxP kontrollü belge MOD-0029'un süreç içi yolundan (`DocumentVersioningService`) geçer, bu komutlardan değil.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `StoreRepositoryObjectCommand` (DL-1) | `StoreRepositoryObjectCommand` | `RepositoryObject` | a | AfterState: `ContentId`, `Scope`, `OwningItemId`, `OwningVersionId`, `ByteSize`, `Checksum` (içerik ve `ObjectKey` yok) | — |
| `CompensateRepositoryObjectCommand` (DL-1) | `CompensateRepositoryObjectCommand` | `RepositoryObject` | a | BeforeState: `ContentId`, `Scope`; telafi işareti | — |
| `StoreTrustedRepositoryObjectCommand` (DL-2) | `StoreTrustedRepositoryObjectCommand` | `RepositoryObject` | a (K-B (a) ile; (b) seçilirse yeni iz) | AfterState: yukarıdaki alanlar + `TrustedConsumerModule`; metadata `callerService`, `consumerModule`, `audience`, `serviceClientId` | — |
| `CompensateTrustedRepositoryObjectCommand` (DL-2) | `CompensateTrustedRepositoryObjectCommand` | `RepositoryObject` | a (K-B ile) | BeforeState: `ContentId`, `Scope`; metadata aynı | — |
| Okuma satırı: `OpenTrustedRepositoryObjectContentQuery` (DL-2) | `document-repository.trusted-consumer.content-read` | `RepositoryObject` | a (merkezi günlük, `IAuditService`; `Operation = Export`) | metadata `ByteSize`, `scope`, `consumerModule`; içerik yok | — (AUD-001 §7 sınıfı değil; makine kimliğinin içerik çıkarması Platform tarafındaki tek iz olduğu için yazılır — D56/2) |
| Okuma satırı: `OpenRepositoryObjectContentQuery` (DL-1) | — | — | — | — | DL-1 açığı: AC-8 okuma ister, kod yazmıyor (§19 ölçüm c, K-D) |
| Ret: güvenilen yüzey (DL-2; 401 / 403 / 404 / 400 — AC-26; 503 yapılandırma reddi ve 504 bütçe aşımı uyarı loguyla, denetim kaydı yok) | `document-repository.trusted-consumer.denied` | `RepositoryObject` ya da uç | a (`IAuditService.AppendAsync`, emsal `PartnerAdminScopeAttribute.RecordAsync`; `Operation = PermissionDenied`, `Outcome = Denied` / `Failed`) | metadata `ReasonCode`, uç; kimliği doğrulanmışsa token kiracısı ve `serviceClientId`, değilse `IsPlatformGlobal` ve doğrulanmamış değer yok | — (AUD-001 §3 "Reddedilen komut da kaydedilir") |

Kayda yazılmayan alanlar: token (servis ya da kullanıcı), istemci sırrı, `ObjectKey`, dosya içeriği. Dosya adı yalnız `RepositoryObject` satırındadır, denetim metadata'sına yazılmaz.

---

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek ikili depo — taşınan dikiş, akış sözleşmesi, depo içi yetki, kiracı yalıtımı, denetim (kodu main'de; kapanış KISMİ) | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events | birim + gerçek Mongo entegrasyon + tam build + dev ortamı smoke (§19 CT canlı doğrulama listesi) | plan satırı yok | plan satırı yok |
| DL-2 | güvenilen tüketici servis kimliği — MOD-0217 için akışlı yükleme, meta, içerik, telafi; kapalı tüketici listesi, retler dahil denetim (DL-1'e dayanır) | Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Trusted consumer identity, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events | birim + sabotajlı güvenlik testleri + gerçek HTTP host + gerçek Mongo + tam Platform suite; kabul kapısı: Auth'un verdiği token'la dev ortamı canlı kaydı (AC-29) | plan satırı yok | plan satırı yok |
