# WORK PACKAGE — WP-DM-1 · Register ingest + tenant-agnostik auto-seed (358 doküman)

> **Control Tower kaydı (SoR).** DM planı DM-1 adımı. Module: **MOD-0029** (Document Master Register). Owner: doc-mgmt lead.
> **Branch:** `feature/dm-document-master-register` (origin/main tabanlı, **MOD-0262 #96 merge dahil** — HEAD 8a8190ac). KARAR-1 (DM bu branch ailesinde).
> **İzolasyon:** MOD-0262 artık main'de (çakışma yok). DM çekirdeği (MasterRegister) izole. Auto-seed DI wiring'i `Infrastructure/DependencyInjection.cs`'e dokunur — MOD-0262 bloğu artık main'de olduğu için **çakışma yok**.

## Metadata
```text
WP ID:            WP-DM-1
Prompt ID:        P-DM-1 · v1.0
Task Class:       Ingest feature (command/handler) + config-driven startup seeder — state-changing (L2 writes)
Golden-Flow Profile: B (backend); UI yok (auto-seed + ingest komutu)
Risk Class:       MEDIUM (idempotent upsert + tenant-agnostik seed + DM-0 status eşlemesi kritik)
Agent Lane:       AL-DM-INGEST (DEV) · Target Agent: backend-architect
Branch:           feature/dm-document-master-register · Expected HEAD: 8a8190ac (dispatch 2026-09-10)
Persistence:      L2 (register entry upsert; auto-seed idempotent)
```

## Ölçülmüş girdi (CT, 2026-09-10)
- **CSV:** `docs/reference/integrations/gmg-qms/GMG_ERP_Document_Reference_List_2026-08-24.csv` — 358 veri satırı, 17 kolon (`document_uid, document_code, title, gqms_domain, gqms_type, erp_document_type, version, status, criticality, owner, effective_date, review_cycle, folder_id, folder_path, is_mandatory_group_sop, linkable_in_erp, link_blocked_reason`).
- **Parser reuse:** `Features/Tasks/Services/DocumentReferenceListParser.cs` — `static Parse(...) → ParseResult(Entries: IReadOnlyList<DocumentReferenceEntry>, Errors, MissingColumns, UnreadColumns, LinkableCount, ContentHash)`. Tenant + ListVersionId **required parametre** (sonradan patch edilmez). `HashContent` (SHA-256) idempotency imzası. **Naive CSV split YASAK** (bu parser quoted-field-aware; DM-0'da awk-split 21 sahte statü vermişti).
- **Hedef entity:** `Domain/Entities/DocumentManagement/DocumentMasterRegisterEntry.cs : TenantScopedEntity` — `PermanentUid`, `DocumentCode`, `DocumentTitle`(required), `LifecycleStatus`(=`ControlledDocumentLifecycleStatus`, default Draft), `RegisterStatus`(=`DocumentRegisterStatus`, default Draft), `CollectionInstanceId`(Guid), `IsSystemAllocated`(bool), `LinkScopeCompatibilityStatus`, `StatusReason`, `Criticality`, `DocumentType`, `DocumentClass`, `FolderId`.
- **Repo:** `IDocumentManagementMasterRegisterRepositories` — `CreateAsync`, `GetByPermanentUidAsync(uid)`, `GetByDocumentCodeAsync`, `GetByPermanentUidsAsync` (batch), `UpdateAsync`, `GetAllForTenantAsync`, `ListAsync(filter)`. → **idempotent upsert = GetByPermanentUidAsync → varsa Update, yoksa Create** (guard: `(TenantId, PermanentUid)`).
- **Collection:** `document_management_master_register` `PlatformCollections`'ta **ZATEN TANIMLI** → **şema/manifest DEĞİŞMEZ** (PlatformCollections.cs / PlatformSchemaManifest.cs'e DOKUNMA).
- **Lifecycle enum** (`Enums/DocumentManagement/MasterRegisterEnums.cs`): Draft=0, InReview=1, ApprovedPendingEffective=2, Effective=3, UnderRevision=4, Suspended=5, Superseded=6, Retired=7, ObsoleteCopy=8.
- **Config seed deseni (mirror):** `Infrastructure/Settings/AuditRetentionSeedOptions.cs` + `Infrastructure/Persistence/Configurations/AuditRetentionPolicySeed.cs` + `Infrastructure/DependencyInjection.cs` (`services.Configure<AuditRetentionSeedOptions>(...SectionName)` ~satır 129). Dev-gate deseni: `BootstrapSeedPolicy` / `SeedMarkerStore` (idempotency marker).

## DM-0 onaylı status → LifecycleStatus eşlemesi (SABİT, auto-seed ön koşulu)
| CSV status | Adet | LifecycleStatus | linkable | blocked reason |
|---|---|---|---|---|
| Draft | 320 | `Draft` | yes | — |
| Draft — final draft for approval | 1 | `InReview` | yes | — |
| Void | 7 | `Retired` | no | terminal, non-citable |
| Planned | 23 | `Draft` + **blocked** | no | "does not exist yet" |
| NOT REGISTERED | 6 | `Draft` + **blocked** | no | "mandatory-but-unregistered" |
| Executed (source on file) | 1 | **blocked/özel** | (QA) | record, controlled-doc değil |
- 36 bağlanamayan satır **DÜŞÜRÜLMEZ** — blocked + `StatusReason`/`LinkScopeCompatibilityStatus` ile "görünür-uyumsuz". Gizleme yok.
- 🔴 **0 doküman Effective** (Faz 1 gerçeği) → seed sonrası effectiveness/citation her şey için Blocked/Unresolved = **doğru**.

## Kapsam
### DM-1a — Register ingest (command/handler) [MOD-0262'den bağımsız, DI'ya dokunmaz]
1. `IngestDocumentMasterRegisterCommand` + handler: CSV içeriği (veya path) → `DocumentReferenceListParser.Parse` **reuse** → her `DocumentReferenceEntry` → `DocumentMasterRegisterEntry` map (DM-0 eşlemesi uygulanır).
2. Map kuralları: `IsSystemAllocated=false`; `CollectionInstanceId=Guid.Empty` (klasörsüz projeksiyon, §8); `LifecycleStatus` = DM-0 eşlemesi; blocked satırlar `StatusReason` + uygun `LinkScopeCompatibilityStatus`; `DocumentTitle` boşsa raporla (uydurma).
3. **Idempotent upsert** by `(TenantId, PermanentUid)`: `GetByPermanentUidAsync` → varsa `UpdateAsync`, yoksa `CreateAsync`. Re-import = güncelleme, duplicate YOK. `ContentHash` ile "aynı dosya" tanınır.
4. Sonuç raporu: created/updated/blocked sayıları + parse Errors.

### DM-1b — Tenant-agnostik config-driven auto-seed [KARAR-2] [MOD-0262 main'de → DI çakışması yok]
5. `DocumentRegisterSeedOptions` (`Infrastructure/Settings/`, `AuditRetentionSeedOptions` mirror): `SectionName="DocumentRegisterSeed"`, `TenantId` (string/Guid?, nullable), opsiyonel `CsvPath`/`Enabled`.
6. `DocumentRegisterSeed` (`Infrastructure/Persistence/Configurations/`): startup'ta `DocumentRegisterSeed:TenantId` doluysa **ingest handler'ı** (DM-1a) o tenant için idempotent koşar; **config yoksa/boşsa/prod → ATLA** (leakage yok). Dev-gate (`BootstrapSeedPolicy`) + `SeedMarkerStore` idempotency.
7. `DependencyInjection.cs`'e **tek satır** `services.Configure<DocumentRegisterSeedOptions>(configuration.GetSection(DocumentRegisterSeedOptions.SectionName))` + seeder kaydı (AuditRetention deseni). ⚠️ **HARDCODE tenant YOK** (PositionAssignmentSeed'in 97c5 hardcode'unu İZLEME). Local=97c5 config'ten; arkadaş=kendi; prod=boş→skip.

## Frozen model uyumu
- **KARAR-2:** tenant-agnostik config-driven (hardcode yasak). **KARAR-3:** yalnız metadata katalog — **gerçek dosya YOK** (o DM-4/MOD-0262). **DM-0:** status eşlemesi SABİT. `CollectionInstanceId=Guid.Empty`, `IsSystemAllocated=false`.

## Acceptance
- **E2:** build temiz; unit — parser reuse ile 358 satır map; DM-0 eşlemesi doğru (Draft/InReview/Retired + blocked+reason); `by=uid` idempotent upsert (re-import duplicate YOK, update eder); `CollectionInstanceId=Guid.Empty`; blocked 36 satır düşmez (görünür); auto-seed: config VAR→idempotent seed, config YOK/prod→ATLA (leakage testi); GUID subtype-4/tenant tuzağı yok.
- **Regresyon:** tam Platform.Application.Tests — **yeni fail YOK** (bilinen env/Mongo tabanı + dokunulmayan 14 release-gate hariç; baseline-diff ile ölç).
- **E4 (fleet, opsiyonel):** dev fleet'te auto-seed 97c5'e 358 satır yazar; `GetByPermanentUidsAsync` doğru satır; `effectiveness:batch` seed sonrası Blocked/Unresolved (0 Effective) döner (DM-3 reuse).
- Kapsam: yalnız ingest + auto-seed. Şema/collection DEĞİŞMEZ; MOD-0262 (binary) DEĞİŞMEZ; gerçek dosya YOK; başka modül YOK.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-DM-1 · Prompt P-DM-1 v1.0  (Register ingest + tenant-agnostik auto-seed — MOD-0029)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/dm-document-master-register · Expected HEAD: 8a8190ac · Worktree: ana checkout

Önce oku:
1. execution/domains/platform-shared-services/work-packs/WP-DM-1-register-ingest-autoseed.md (bu WP)
2. services/Diten.Platform/.../Features/Tasks/Services/DocumentReferenceListParser.cs (Parse → ParseResult; REUSE — CSV parse'ı YENİDEN YAZMA)
3. services/Diten.Platform/.../Domain/Entities/DocumentManagement/DocumentMasterRegisterEntry.cs + Enums/DocumentManagement/MasterRegisterEnums.cs (ControlledDocumentLifecycleStatus)
4. services/Diten.Platform/.../Domain/Repositories/IDocumentManagementMasterRegisterRepositories.cs (CreateAsync/GetByPermanentUidAsync/UpdateAsync/GetByPermanentUidsAsync)
5. DESEN (config seed): services/Diten.Platform/.../Infrastructure/Settings/AuditRetentionSeedOptions.cs + Infrastructure/Persistence/Configurations/AuditRetentionPolicySeed.cs + Infrastructure/DependencyInjection.cs (Configure<...SeedOptions> ~satır 129) + BootstrapSeedPolicy/SeedMarkerStore

NE:
 DM-1a) IngestDocumentMasterRegisterCommand + handler: CSV → DocumentReferenceListParser.Parse REUSE → her DocumentReferenceEntry'yi
   DocumentMasterRegisterEntry'ye map et. Map: IsSystemAllocated=false; CollectionInstanceId=Guid.Empty; LifecycleStatus = DM-0
   eşlemesi (Draft→Draft, "final draft for approval"→InReview, Void→Retired, Planned→Draft+blocked "does not exist yet",
   NOT REGISTERED→Draft+blocked "mandatory-but-unregistered", Executed→blocked/özel); blocked satır DÜŞÜRÜLMEZ (StatusReason +
   LinkScopeCompatibilityStatus). Idempotent upsert (TenantId,PermanentUid): GetByPermanentUidAsync→varsa Update, yoksa Create.
   Sonuç: created/updated/blocked + parse Errors raporu.
 DM-1b) Tenant-AGNOSTİK auto-seed: DocumentRegisterSeedOptions (Settings, AuditRetentionSeedOptions mirror, SectionName=
   "DocumentRegisterSeed", TenantId nullable + ops. CsvPath/Enabled) + DocumentRegisterSeed (Configurations): startup'ta
   TenantId doluysa DM-1a ingest'i o tenant için idempotent koşar; config YOK/boş/prod → ATLA. Dev-gate (BootstrapSeedPolicy)
   + SeedMarkerStore idempotency. DependencyInjection.cs'e Configure<DocumentRegisterSeedOptions> + seeder kaydı (tek yer).
NEDEN: push sonrası arkadaş TaskCenter repoint'ini GERÇEK veriyle yapabilsin (effectiveness/citation seed sonrası Blocked/
   Unresolved dönsün, boş değil). Metadata kataloğu — gerçek dosya değil.
NASIL: DocumentReferenceListParser REUSE (naive CSV split YASAK — quoted-aware). Config seed = AuditRetention deseni birebir.
   Collection document_management_master_register ZATEN VAR (şema DEĞİŞTİRME). new-aggregate class-map/GUID subtype-4 + tenant tuzağı.
YAPMA: tenant HARDCODE etme (KARAR-2 — PositionAssignmentSeed 97c5 hardcode'unu İZLEME); PlatformCollections/PlatformSchemaManifest
   DEĞİŞTİRME; gerçek dosya/binary (MOD-0262) DOKUNMA; blocked satırları düşürme/gizleme; global serializer; başka modül.
DOĞRULA (E2):
 - build temiz; unit: 358 satır map + DM-0 eşlemesi doğru · idempotent upsert (re-import duplicate YOK) · CollectionInstanceId=
   Guid.Empty · blocked 36 satır görünür/düşmez · auto-seed config VAR→seed / YOK/prod→ATLA (leakage testi).
 - TAM Platform.Application.Tests: yeni fail YOK (env/Mongo tabanı + dokunulmayan 14 release-gate HARİÇ; baseline-diff ile).
Ayrı commit(ler): DM-1a ingest ayrı, DM-1b auto-seed ayrı. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: DocumentReferenceEntry→DocumentMasterRegisterEntry map'inde alan yoksa (raporla, uydurma) · status DM-0
  dışında değer çıkarsa (naive-split şüphesi — DUR) · auto-seed hedef tenant belirsizse (config-key, hardcode YASAK) ·
  kapsam ingest+auto-seed dışına (şema/MOD-0262/gerçek dosya) taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-10) → **ACCEPTED (E2)**
```text
Commits: DM-1a 98738949 · DM-1b 51c16343 · Agent: PASS · Verification: PASS · CT: ACCEPTED · Evidence: E2 (E4 fleet opsiyonel/pending)
```
- ✅ **Scope:** DM-1a 5 dosya (command/handler/models/mapping/test — hepsi yeni, DI'ya dokunmaz) + DM-1b 5 dosya (options/seed/gate/test + DI **tek hunk**). **Yasak dosya YOK** (PlatformCollections/SchemaManifest/MOD-0262/DocumentRepository dokunulmadı). DI değişikliği yalnız register-seed (Configure + self-gated EnsureSeededAsync).
- ✅ **Mantık (CT okudu):** Mapping DM-0 birebir + kapalı-küme fail-closed (beklenmedik statü→throw); handler idempotent upsert (GetByPermanentUidAsync→Create/Update) + **all-or-nothing 422** (tüm statüler yazımdan önce valide); gate leakage-guard (dev+Enabled+gerçek-GUID-tenant+CSV-var, aksi skip) + **KARAR-2 hardcode YOK** (97c5 yalnız "izlenmedi" yorumunda).
- ✅ **CT kendi koşumu (izole worktree):** DM-1 kendi testleri **17/17**; gerçek 358-satır GMG CSV testi (Ingests_the_real_358_row_gmg_csv_end_to_end) **358 created / 36 blocked (7+23+6) / hepsi CollectionInstanceId=Guid.Empty / 0 Effective** doğruladı.
- ✅ **Gold-standard baseline-diff (isim-set, sayı değil):** baseline 8a8190ac **167 fail** vs with-mine 51c16343 **169 fail**. İsim-diff: 3 YENİ = hepsi `BusinessReferenceDataMongoResidueSweeperTests` (Mongo residue env-flake — **izole 5/5 geçer**, kanıtlandı), 1 GONE (Organization Mongo). **DocumentRegister alanı 0 fail → 0 gerçek regresyon.** (Agent'ın "167/167" sayı iddiası env-varyansıyla hafif kaymıştı; CT isim-diff+izolasyon ile "0 gerçek yeni fail" sonucunu bağımsız doğruladı.)
- ⚠ **Bayrak (agent şeffaf):** "Executed (source on file)" (1 satır) DM-0'da underspecified → muhafazakâr Draft (non-citable) + açıklayıcı StatusReason, 36-blocked'a dahil değil, reversible. Kabul edilebilir; QA firm'lerse re-ingest üzerine yazar.

## Kalan (bu WP dışı)
- DM-2b (Search/Resolve gerçek impl, seed sonrası canlı) · DM-3 (effectiveness:batch doğrula, reuse) · DM-4 (gerçek dosyalar, MOD-0262 ile koordineli).
- E4 authenticated fleet turu (auto-seed canlı + effectiveness:batch).
