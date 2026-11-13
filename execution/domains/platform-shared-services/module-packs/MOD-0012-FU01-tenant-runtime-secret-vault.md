---
id: MOD-0012-FU01
name: Tenant Runtime Secret Vault
parent: MOD-0012
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: slim
data_mode: server
entity_base: BaseEntity
status: draft
approval_status: pending
revision: "r1 (2026-11-13) — ilk taslak. Girdi: sahip kararı (Beste, 2026-11-13, sohbet) 'tamam ekleyelim bunu platform tarafına' — MOD-0179 3PL kanal sırları için Platform'a ayrı FU, MOD-0179 DL-4'ün kapısı (karar kaydını CT yazar, §19.1). Kimlik ön kontrolü bu oturumda koşuldu (§0). Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) yok."
status_note: "draft r1 — kod izni yok. Paket onaylanmadan (künye approved / ready-for-dev + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali / GitHub GMG-ERP) hiçbir dilim başlamaz. Onaydan önce kapanacak: KEK yeri ve yedekleme (Q-1, Q-2 — Ali), denetim yolu ve AuditBehavior listesi (Q-5 — CT). Dilim kapıları: Auth servis kimliği ve X-1 izni (Q-3 — DL-5), servisler arası TLS (Q-4 — DL-2 canlı kullanım), admin ekranı kararı (Q-7 — DL-4)."
owner: platform-shared-services / merkez (Beste)
approver: Ali (GitHub GMG-ERP)
risk: "HIGH — (1) kiracı sırlarını kalıcı olarak saklayan ilk Platform bileşeni (şifreli; anahtar kaybı = tüm sırların kaybı); (2) değer servisler arası bir cevapta taşınır (resolve); (3) yeni servis kimliği audience'ı ve AuthService'te XMC (X-1); (4) güvenlik ve kiracı izolasyonu — yüksek riskli onay (CT-APR-01)."
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: TBD
form_field_count: 1
dependencies: [MOD-0012 (ISecretsProvider — KEK okuması; ana paket status review), MOD-0033-FU02 (servis kimliği jetonu — yeni audience + Inventory istemcisi, X-1, Q-3), MOD-0021 / AUD-001 (platform-islem-ici denetim yolu — Q-5), MOD-0018 (RBAC — admin dilimi), MOD-0179 (ilk tüketici; DL-4'ü bu paketin DL-1 + DL-2 + DL-5'ine bağlı), DEV-0000 (Golden Reference Slim — yalnız DL-4)]
---

# MOD-0012-FU01 — Tenant Runtime Secret Vault (Kiracı Sır Kasası)

> **draft (r1, 2026-11-13).** `status: draft`, `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız
> `approved` / `ready-for-dev` künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**)
> ile yazılır (AGENTS.md Çekirdek madde 7). Bu taslak kod, commit, push, PR ya da onay izni değildir.
>
> **Kaynak satırı (sahip kararı).** Beste, 2026-11-13, sohbet: *"tamam ekleyelim bunu platform tarafına"* — MOD-0179 3PL kanal sırları
> (API anahtarı, SFTP kimliği / anahtarı, EDI sertifikası) için Platform'a ayrı bir MOD-0012 FU'su açılır; bu FU **MOD-0179 DL-4'ün kapısıdır**
> (MOD-0179 X-14, Q-16). Karar kaydını CT yazacak: `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` dosyasına eklenecek
> (bu paket o dosyaya dokunmaz). Önceki bağlı karar aynı dosyada T-4: "kanal sırları Platform'un sır kasasında; ekranda yalnız ayarlı · son değişim".
>
> **Kısa özet.** MOD-0012 bugün yalnız **çalışma zamanı yapılandırma sırlarını okur** (ortam değişkeni / user-secrets; yazma yok). Bu FU,
> Platform'a **kiracı başına, çalışma anında yazılabilen, şifreli** bir sır kasası ekler: sır kiracı (`TenantId`) + şirket (isteğe bağlı
> `LegalEntityId`) + sahibi modül + kapsam referansı + ad ile tanımlanır; zarf şifrelemeyle (AES-256-GCM, kiracı başına türetilmiş KEK)
> saklanır; yazma / döndürme / iptal komutları yalnız sahibi modülün servis kimliği + işlemi yapan kullanıcı adına çalışır; değeri yalnız
> sahibi modülün servis kimliği okur. Değer hiçbir kullanıcı cevabında, logda ya da denetim kaydında yoktur.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — bu oturumda koşuldu (HEAD `a933a437d`, araç sha256 `ae87a56615f5…`):**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0012-FU01 --name "Tenant Runtime Secret Vault" --parent MOD-0012` | çıkış 0 — `OK  MOD-0012-FU01: proven against Blueprint/registry.` |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "tenant secret vault"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 2 · gateway 0 · görev 0`; rapor sha256 `65f29b3d295e7055fab1f71a34755014721c1334fa2e07c4f6ef6c7ef935527e`; 572 ref incelendi |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "sır kasası"` | çıkış 0 — eşleşme yok |

**FU numarası ölçümü.** `execution/registries/module-id-registry.md` içinde MOD-0012'nin yalnız ana satırı ve iki eski takma adı var
(`NEW-001`, `PSS-PLAN-RECON-1` → MOD-0012); `MOD-0012-FU` geçen satır yok. Depoda `MOD-0012-FU` / `0012-FU` geçen tek yer MOD-0179
paketidir (yalnız "FU gerekir" der). `execution/portfolio/**` ve `docs/records/decisions/**` içinde MOD-0012 için ayrılmış FU numarası yok
(DCP üye tabloları dahil). → İlk boş numara **FU01**.

**Keşif bulgularının kararı:**
- Blueprint `MOD-0012 'Secrets & Configuration Vault'` (veri nesnesi: Secrets) — üst modül; bu paket onun FU'sudur (yeni MOD açılmaz).
- Paket `execution/domains/platform-shared-services/module-packs/MOD-0012-secrets-configuration-vault.md` — ana paket (status `review`);
  "Out Of Scope: Full admin UI for secret management" ve "Plaintext secret values must not be persisted in MongoDB by this MVP" der.
  Bu FU o MVP'yi değiştirmez; düz değer bu FU'da da Mongo'ya yazılmaz (yalnız şifreli metin).
- Paket `MDM-011-secrets-and-configuration-vault.md` — yalnız 8 eski uzak dalda (`feature/dev-enablement` vb.), HEAD'de yok; MOD-0012'nin eski
  takma adı niteliğinde; mükerrer değil, yeniden canlandırılmaz.

**Kısmi inceleme notu:** keşif aracı bu makinede olmayan dalları, commit edilmemiş işleri ve kayıtlı olmayan görev kaynaklarını görmez
(araç çıktısındaki EKSİK satırları). Registry'ye `MOD-0012-FU01` satırının eklenmesi registry sürecinin (CT) işidir; bu paket registry'ye dokunmaz.

## 1. Module Summary

- **Amaç.** Kiracı iş modüllerinin dış sistem kimlik bilgilerini (API anahtarı, OAuth istemci sırrı, imza sırrı, SFTP parolası / özel anahtarı,
  AS2 / EDI sertifikası) kiracı + şirket kapsamında, çalışma anında yazılabilir ve şifreli saklamak; değeri yalnız sahibi modülün servis
  kimliğine vermek.
- **Hedef kullanıcı.** Doğrudan son kullanıcı yok. Kullanıcı, tüketici modülün kendi ekranında ("Ayarla / Değiştir") sırrı **bir kez** girer;
  tüketici servis değeri bu kasaya iletir (MOD-0179 §8.5). Platform yöneticisi yalnız isteğe bağlı DL-4 ekranında **meta** görür ve iptal eder.
- **Kapasite özeti (v1).** Yaz · döndür · iptal (soft) · meta oku · servis kimliğiyle değer oku · sürüm ve son kullanma · KEK döndürme ve
  yeniden sarma · isteğe bağlı doğrulama geçiş penceresi · isteğe bağlı platform-admin meta ekranı.
- **İlk tüketici.** MOD-0179 (3PL Integration) — yedi sabit ad: `ApiKey`, `OAuthClientSecret`, `InboundSigningSecret`, `SftpPassword`,
  `SftpPrivateKey`, `As2Certificate`, `As2PartnerCertificate` (MOD-0179 paketi satır 276); değer 1..16384 bayt (satır 746).

## 2. Ownership and Boundaries

### 2.1 Kapsam (v1)

- `TenantSecret` kaydı ve onun zarf şifrelemesi (§8.2).
- İç (servisler arası) API: yaz, döndür, iptal, meta oku, değer oku (resolve) — §15.
- Kodda sabit tüketici listesi (hangi modül, hangi servis kimliği, hangi adlar, hangi temsilî izin) — §8.3.
- KEK okuması MOD-0012'nin mevcut `ISecretsProvider`'ı üzerinden; başlangıçta doğrulama (§8.2).
- İşlem içi denetim (platform-islem-ici) — §21.
- KEK döndürme ve yeniden sarma işi; son kullanma; isteğe bağlı doğrulama geçiş penceresi (DL-3).
- Sözleşme `TENANT-SECRET-VAULT 1.0.0` (OpenAPI, yazılacak — §8.8).
- İsteğe bağlı: platform-admin meta listesi + iptal (DL-4, Q-7).
- XMC: AuthService audience politikasına tek çift ekleme (X-1, DL-5, sahibinin izniyle).

### 2.2 Kapsam dışı

- Dış kasa sağlayıcıları (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) — ana paket follow-up'ı olarak kalır; bu FU yalnız
  `IKeyEncryptionKeyProvider` dikiş yerini bırakır.
- Çalışma zamanı yapılandırma sırları (JWT, Mongo bağlantısı, iç API anahtarları, `Mfa:HashSecret`) — MOD-0012 ana paketi; bu FU onlara dokunmaz.
- Platform içi tüketiciler (ör. bildirim SMTP `CredentialSecretRef`) — v1'de yok, follow-up (§20).
- Tüketici modülün kendi ekranı, kendi portu ve kendi sır referansı alanı (MOD-0179 `IThirdPartySecretVault`, `Secrets[]`) — MOD-0179 DL-4.
- Değeri insana gösteren her yüzey (ekran, dışa aktarma, "göster" düğmesi) — hiçbir dilimde yok.
- Kiracı kapatıldığında sırların kripto-imhası — follow-up (§20).
- Sır yaklaşan son kullanma bildirimi (MOD-0027) — follow-up.

### 2.3 Sınır kuralları

1. **Düz değer kalıcı değildir.** Değer yalnız istek işlenirken bellekte bulunur; Mongo'da, idempotency deposunda, denetim kaydında, logda,
   OTel'de, istisna mesajında ve kullanıcıya dönen hiçbir HTTP cevabında yoktur.
2. **Değeri dönen tek cevap** iç `resolve` ucudur; yalnız servis kimliği jetonuyla çağrılır, Gateway'den erişilemez, gövdesi loglanmaz,
   `Cache-Control: no-store` taşır.
3. **Kiracı jetondan gelir.** Kiracı servis jetonunun `tenant_id` iddiasından çözülür; gövdede kiracı alanı yoktur. Başka kiracının ya da
   başka tüketici modülün kaydı **404** döner (var olmayan kayıtla bayt-eşit gövde).
4. **Sahibi modül istemciden değil listeden gelir.** `OwnerModule`, jetondaki istemci kimliğinin kodda sabit listedeki girişinden çözülür.
5. **Silme yok.** İptal soft'tur (`IsDeleted`, `DeletedAt`); kayıt kalır, şifreli metin ve sarılı DEK sıfırlanır (kripto-imha).
6. **İnsan jetonu değer okuyamaz.** `platform_admin` dahil hiçbir kullanıcı jetonu resolve ucuna geçemez (ayrı kimlik doğrulama şeması).

## 3. Owned Objects

**Varlıklar:** `TenantSecret` (koleksiyon önerisi `platform_tenant_secrets`), `TenantSecretCommandRequest` (idempotency; öneri
`platform_tenant_secret_command_requests`).

**Değer nesneleri:** `TenantSecretCipherEnvelope` (Alg, KekId, WrappedDek, DekNonce, DekTag, ValueNonce, Ciphertext, ValueTag),
`SensitiveSecretValue` (bayt taşıyıcı; `ToString()` → `[REDACTED]`; JSON'a yazılırken istisna atar — yanlışlıkla serileştirmeyi kırar).

**Komutlar (yazma, 5):** `WriteTenantSecretCommand`, `RotateTenantSecretCommand`, `RevokeTenantSecretCommand` (DL-1);
`RewrapTenantSecretsCommand`, `PurgeExpiredPreviousSecretsCommand` (DL-3; ikincisi yalnız AC-22 kabul edilirse).

**Sorgular:** `GetTenantSecretMetadataQuery` (DL-1), `ResolveTenantSecretQuery` (DL-2), `GetTenantSecretListQuery`, `GetTenantSecretByIdQuery` (DL-4).

**Portlar / servisler:** `ITenantSecretCipher` (zarf şifreleme), `IKeyEncryptionKeyProvider` (KEK; v1 uygulaması `ISecretsProvider` üzerinden
ortam değişkeni), `ITenantSecretRepository`, `TenantSecretAuditIntent` (işlem içi denetim yazıcısı), `TrustedSecretVaultConsumerRequestExecutor`,
`TrustedSecretVaultConsumerAuthorizationOptions` + doğrulayıcısı, `KekRewrapJob` (DL-3).

**API uçları (Platform 5057, iç — Gateway rotası yok):**

| Metot ve yol | Kimlik | Dilim |
|---|---|---|
| `POST /api/internal/v1/tenant-secrets` | servis jetonu + `X-Delegated-Authorization` | DL-1 |
| `POST /api/internal/v1/tenant-secrets/{vaultRef}/rotate` | servis jetonu + temsilî kullanıcı | DL-1 |
| `POST /api/internal/v1/tenant-secrets/{vaultRef}/revoke` | servis jetonu + temsilî kullanıcı | DL-1 |
| `GET /api/internal/v1/tenant-secrets/{vaultRef}/metadata` | servis jetonu | DL-1 |
| `POST /api/internal/v1/tenant-secrets/{vaultRef}/resolve` | yalnız servis jetonu | DL-2 |

**API uçları (admin, DL-4, Gateway üzerinden):** `GET /api/admin/tenant-secrets`, `GET /api/admin/tenant-secrets/{vaultRef}`,
`POST /api/admin/tenant-secrets/{vaultRef}/revoke`.

**İzinler:** `platform.tenant-secrets.read`, `platform.tenant-secrets.revoke` (yalnız DL-4; §14).

**Frontend rotası (DL-4):** `/Platform/TenantSecrets` (nav `Nav.Page.TENANTSECRETS`, sayfa kodu `TENANTSECRETS`).

## 4. Entity Fields

Platform'da kiracı kapsamlı kayıt `BaseEntity`'den türer (`services/Diten.Platform/src/Diten.Platform.Domain/Common/BaseEntity.cs:5`);
`TenantId`, `IsDeleted`, `DeletedAt`, `Version` varlıkta açıkça tanımlanır (`TenantMessagingSettings` emsali). İş sürümü `Version` adını
kullanmaz (entity-base-template): adı `SecretVersion`'dır; `Version` yalnız eşzamanlılık içindir.

### 4.1 TenantSecret

| Alan | Tip | Kural |
|---|---|---|
| `Id` | string (Guid) | sunucu üretir; dışarıya **`vaultRef`** olarak verilir (opak; tüketici yalnız bunu saklar) |
| `TenantId` | Guid | zorunlu; servis jetonunun `tenant_id`'sinden; DTO / istek gövdesinde yok |
| `LegalEntityId` | Guid? | tüketici listesindeki girişe göre zorunlu ya da yasak (MOD-0179: zorunlu); kasa şirketin kiracıya ait olduğunu doğrulamaz — tüketici sunucuda çözer (Q-14) |
| `OwnerModule` | string | listeden (ör. `MOD-0179`); gövdeden alınmaz |
| `ScopeRef` | string | 1..128, `[A-Za-z0-9._:-]`; tüketicinin opak kaynak kimliği (MOD-0179: kanal kimliği) |
| `Name` | string | tüketici girişinin izinli ad kümesinden, büyük-küçük harf duyarlı |
| `State` | enum | `Active` \| `Revoked` (`Expired` saklanmaz; okuma anında `ExpiresAt` ile hesaplanır) |
| `SecretVersion` | int | 1'den başlar; her döndürmede +1 |
| `SetAt` | DateTime (UTC) | son yazma / döndürme anı ("son değişim") |
| `SetBy` | string | temsilî kullanıcının kimliği |
| `SetByService` | string | servis jetonundaki `service_name` |
| `ExpiresAt` | DateTime? (UTC) | isteğe bağlı |
| `Cipher` | `TenantSecretCipherEnvelope?` | `Active`'te dolu; iptalde `null` |
| `PreviousCipher`, `PreviousSecretVersion`, `PreviousValidUntil` | zarf? / int? / DateTime? | yalnız AC-22 (geçiş penceresi) kabul edilirse; pencere bitince `null` |
| `RevokedAt`, `RevokedBy`, `RevokeReason` | DateTime? / string? / string? | iptalde dolu; gerekçe 10..500 |
| `IsDeleted`, `DeletedAt` | bool / DateTime? | iptalde `true` / dolu (soft delete) |
| `Version` | long | iyimser eşzamanlılık |
| `CreatedDate`, `ModifiedDate`, `CreatedBy`, `ModifiedBy` | `BaseEntity` | |

`TenantSecretCipherEnvelope`: `Alg` (`A256GCM-ENV-1`), `KekId` (string), `WrappedDek` (32 bayt), `DekNonce` (12), `DekTag` (16),
`ValueNonce` (12), `Ciphertext` (1..16384), `ValueTag` (16).

### 4.2 TenantSecretCommandRequest

| Alan | Tip | Kural |
|---|---|---|
| `TenantId`, `ClientId`, `IdempotencyKey` | Guid / Guid / string | birlikte tekil |
| `Operation` | enum | `Write` \| `Rotate` \| `Revoke` |
| `RequestFingerprint` | string | SHA-256(kanonik meta alanları + **HMAC-SHA256(K_idem, değer)**); `K_idem` KEK'ten HKDF ile türetilir; düz değer ya da düz değer özeti yok |
| `ResponseStatus`, `ResponseBody` | int / string | yalnız meta cevap (değer içermez) |
| `CreatedAt` | DateTime | TTL 24 saat |

### 4.3 Indexes

| Koleksiyon | İndeks | Gerekçe |
|---|---|---|
| `platform_tenant_secrets` | tekil kısmi `{TenantId, OwnerModule, LegalEntityId, ScopeRef, Name}` — `IsDeleted: false` | aynı anahtarda tek etkin sır |
| `platform_tenant_secrets` | `{TenantId, OwnerModule, State}` | meta listeleri |
| `platform_tenant_secrets` | `{Cipher.KekId}` (seyrek) | yeniden sarma işi eski KEK'li kayıtları bulur |
| `platform_tenant_secret_command_requests` | tekil `{TenantId, ClientId, IdempotencyKey}` + TTL `{CreatedAt}` 24 saat | idempotency |

## 5. Repo Scope

| Yol | İçerik | Dilim |
|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.Domain/Entities/TenantSecrets/` | `TenantSecret.cs`, `TenantSecretCipherEnvelope.cs`, `TenantSecretCommandRequest.cs` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/` | `ITenantSecretRepository.cs` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantSecrets/` | §10'daki CQRS ağacı, `TenantSecretAuditIntent.cs`, `TenantSecretErrorCodes.cs`, `Ports/` | DL-1…DL-4 |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/TenantSecrets/` | `AesGcmTenantSecretCipher.cs`, `EnvironmentKeyEncryptionKeyProvider.cs`, `TenantSecretVaultOptions.cs`, `KekRewrapJob.cs` (DL-3) | DL-1, DL-3 |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/` | `TenantSecretRepository.cs`, indeks kaydı | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` | kayıtlar + başlangıç doğrulaması | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/` | `InternalTenantSecretsController.cs` | DL-1, DL-2 |
| `services/Diten.Platform/src/Diten.Platform.API/Security/` | `TrustedSecretVaultConsumerRequestExecutor.cs`, `TrustedSecretVaultConsumerAuthorizationOptionsValidator.cs`; `TrustedServiceTokenValidationExtensions.cs`'e yeni şema + audience sabiti (`TrustedSecretVaultConsumerService`, `TRUSTED_SECRET_VAULT_CONSUMER`) | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Configuration/` | `TrustedSecretVaultConsumerAuthorizationOptions.cs` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Program.cs` | şema kaydı (aynı dosyada başka modüllerin satırlarına dokunulmaz) | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Admin/` | `TenantSecretsAdminController.cs` | DL-4 |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantSecrets/` | birim, HTTP + Mongo, sızıntı taraması testleri | DL-1…DL-4 |
| `docs/analysis/contracts/tenant-secret-vault.openapi.yaml` | sözleşme (yazılacak) | DL-2 |
| `docs/guides/operations/tenant-secret-vault-runbook.md` | KEK döndürme, yedekleme, kurtarma runbook'u (yazılacak) | DL-3 |
| `frontend/Diten.Web/**` | yalnız §11 listesi | DL-4 |
| `execution/domains/platform-shared-services/module-packs/MOD-0012-FU01-tenant-runtime-secret-vault.md` | bu paket | — |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar

Her satır, sahibinin açık izni olmadan dokunulmaz (XMC-001). İzin bu pakete ve sahibin modül paketine yazılır.

| # | Dosya / alan | Bugün (ölçüm) | Gereken değişiklik | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17` | Auth yalnız `Diten.MDM` servisine ve üç audience'a jeton verir (`IsAllowedPair`) | yalnız bir çift eklenir: (`Diten.Inventory`, `TRUSTED_SECRET_VAULT_CONSUMER`); mevcut çiftler değişmez; testi aynı commit | MOD-0033-FU02 (Ali'ye soru — Q-3) | DL-5 |
| X-2 | `tests/architecture/audit-ledger/Diten.Platform.md:16` | platform-islem-ici izinin belirteç listesi | `TenantSecretAuditIntent.EnqueueAsync/TenantSecretAuditIntent.EnqueueRefusalAsync` eklenir + ölçüm notu (MOD-0040-FU02 notu emsali, satır 30) | CT (AUD-001 defteri) | DL-1 |
| X-3 | `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs` | işlem içi komutlar sabit ad listesiyle kabul edilir (AUD-001 §4.4) | üç DL-1 komutu + DL-3 komutları listeye eklenir | CT kararı (Q-5) | DL-1, DL-3 |
| X-4 | `gateway/Diten.ApiGateway/ocelot.json` | `/api/admin/tenant-secrets` rotası yok | açık rota çifti (DL-4) — integration-agent işi; bu paket yazmaz | integration-agent | DL-4 |
| X-5 | Inventory tarafı (`IThirdPartySecretVault` bağdaştırıcısı, servis jetonu sağlayıcısı) | yok | MOD-0179 DL-4'ün kendi işi; bu paket yalnız sözleşmeyi verir | MOD-0179 (merkez) | — |

## 6. Protected Paths

- `.antigravity/**` (bu paket kural dosyası değiştirmez; `platform.*` ad alanı zaten `Diten.Platform`'undur — PKS-001 satır 98).
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent; X-4).
- `services/Diten.Building.Blocks/**` (MOD-0012 ana paketi; v1 değişiklik gerektirmez — §19.2 gözlemi follow-up'tır).
- `services/Diten.AuthService/**` (X-1 dışında).
- `services/Diten.InventoryService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`.
- MOD-0012 ana paketinin çalışma zamanı sırları ve `appsettings*.json` içindeki mevcut anahtarlar.
- `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (karar kaydını CT yazar).

## 7. Dependencies

| Bağımlılık | Tür | Bugün | Kullanım | Kapı | Sahip |
|---|---|---|---|---|---|
| MOD-0012 `ISecretsProvider` | HARD | var (yalnız okuma) | KEK okuması + başlangıç doğrulaması | — | merkez |
| MOD-0033-FU02 servis kimliği | HARD — DL-2 canlı kullanım ve DL-5 | Platform'da adlı RS256 doğrulayıcı var; Auth yalnız `Diten.MDM`'e jeton veriyor | yeni audience; Inventory istemcisi | X-1, Q-3 | Ali |
| AUD-001 platform-islem-ici | HARD — DL-1 | var (MOD-0040-FU02 emsali) | işlem içi denetim | Q-5, X-2, X-3 | CT |
| MongoDB işlemleri (replica set) | HARD | Platform'da kullanılıyor | sır + denetim + idempotency tek işlemde | — | — |
| MOD-0018 RBAC | yalnız DL-4 | var | `platform.tenant-secrets.*` | — | merkez |
| Servisler arası TLS | DL-2 canlıya çıkış | dev'de düz HTTP | resolve cevabı ağda | Q-4 | Ali |
| MOD-0179 | tüketici | draft r1 | ilk tüketici girişi (§8.3) | — | merkez |

**Denetim altyapısı bugün var mı?** Evet — `tests/architecture/audit-ledger/Diten.Platform.md` `## İzler` tablosunda kabul edilmiş
`platform-islem-ici` (yol a) izi var; bu paket yeni bir iz açmaz, o izin belirtecini genişletir (X-2).

## 8. Runtime Constraints

### 8.1 Servis ve kapsam

- Servis `Diten.Platform` (port 5057). İç uçlar yalnız servisler arasıdır; Web (5001) bu uçları çağırmaz.
- Her okuma ve yazma `TenantId` ile sınırlıdır; kiracı `TenantScope` jetondaki `tenant_id` ile açılır (trusted executor emsali,
  `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowConsumerRequestExecutor.cs:188`).
- Soft delete zorunlu; fiziksel silme yok.
- `GlobalEntity` kullanılmaz: kayıt kiracıya aittir.

### 8.2 Şifreleme ve anahtar yönetimi

- **Zarf şifreleme.** Her kayıt için rastgele 256 bit DEK; değer AES-256-GCM ile DEK altında şifrelenir (rastgele 96 bit nonce). DEK,
  kiracı KEK'i altında AES-256-GCM ile sarılır. .NET `System.Security.Cryptography.AesGcm` kullanılır; yeni paket bağımlılığı yok.
- **Kiracı KEK'i.** `KEK_tenant = HKDF-SHA256(IKM = ana KEK, salt = TenantId baytları, info = "diten:tenant-secret-vault:kek:1")`.
  A kiracısının türetilmiş anahtarıyla B'nin kaydı açılamaz.
- **AAD bağlama.** Değer şifrelemesinin AAD'si: `Id | TenantId | LegalEntityId | OwnerModule | ScopeRef | Name | SecretVersion` (kanonik,
  uzunluk önekli). DEK sarmanın AAD'si: `Id | TenantId | KekId`. Şifreli metin başka kayda kopyalanırsa açılmaz (`SECRET_INTEGRITY_FAILED`).
- **Ana KEK yeri (v1 önerisi, Q-1).** Platform ana makinesinde ortam değişkeni; MOD-0012 `ISecretsProvider` ile okunur (üretimde yalnız ortam
  değişkeni — `ConfigurationSecretsProvider.cs:51-55`):
  - `TenantSecretVault:Kek:CurrentId` → `TenantSecretVault__Kek__CurrentId` (1..32, `[a-z0-9-]`)
  - `TenantSecretVault:Kek:Current` → `TenantSecretVault__Kek__Current` (base64, tam 32 bayt)
  - `TenantSecretVault:Kek:Previous:{id}` → `TenantSecretVault__Kek__Previous__{id}` (döndürme sırasında)
  - Anahtar adlarında alt çizgi yoktur (bkz. §19.2 gözlem 4).
- **Başlangıç doğrulaması.** `RequiredSecretDefinition` ile: yok / boş / 32 bayttan farklı / bilinen yer tutucu / `Previous` içinde `Current`
  ile aynı değer → Platform başlamaz (`SecretValidationException`; mesajda anahtar adı var, değer yok). Kasa devre dışı bırakılamaz: tüketici
  listesi boş değilse KEK zorunludur.
- **Düz değer bellekte.** `SensitiveSecretValue` baytları kullanım sonrası `CryptographicOperations.ZeroMemory` ile sıfırlanır (en iyi çaba;
  .NET string'e dönüşen değer için garanti yok — resolve cevabının yazımı dahil).

### 8.3 Tüketici listesi ve servis kimliği

- **Kimlik doğrulama.** Yeni JWT şeması `TrustedSecretVaultConsumerService`, audience `TRUSTED_SECRET_VAULT_CONSUMER`; doğrulama mevcut
  adlı RS256 doğrulayıcının (`TrustedServiceTokenValidationExtensions.cs:16-31`) aynısıdır (zorunlu iddialar: `iss`, `aud`, `sub`,
  `actor_type`, `service_name`, `tenant_id`, `jti`, `iat`, `nbf`, `exp`).
- **Temsilî kullanıcı (yalnız yazma uçları).** `X-Delegated-Authorization` başlığında kullanıcının JWT'si; kullanıcı kiracısı = jeton
  kiracısı (aksi 403), kullanıcı girişteki temsilî izni taşır (`PermissionClaimEvaluator`, workflow emsali — aynı dosya satır 294).
- **Kodda sabit liste (closed set).** `TrustedSecretVaultConsumerAuthorizationOptionsValidator.AllowedConsumers` — yapılandırmadaki her giriş
  bu kümeye uymazsa Platform başlamaz (`ValidateOnStart`; `TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator` emsali). Giriş:
  `ConsumerModule`, `ServiceName`, `ClientId` (yapılandırmadan, Auth istemcisi), `AllowedNames`, `LegalEntity` (`Required` \| `Forbidden`),
  `DelegatedPermission`, `VerificationGraceNames` (AC-22). En çok 16 giriş; aynı modül ya da aynı istemci iki kez yok.
- **v1 kümesi (tek giriş):** `MOD-0179` · `Diten.Inventory` · adlar: yedi MOD-0179 adı · `LegalEntity = Required` · temsilî izin = MOD-0179'un
  kanal yapılandırma izni (anahtar adı MOD-0179 Q-1 ile kesinleşir; kesinleşmeden DL-1 onayı bu hücreyi doldurur) · geçiş penceresi adı:
  `InboundSigningSecret` (yalnız AC-22 kabul edilirse).
- **Listeye ekleme kuralı.** Yeni tüketici = bu paketin revizyonu + inceleme + onay satırı + koddaki küme + Auth istemcisi; yalnız yapılandırma
  yazmak hiçbir zaman yetmez.

### 8.4 Değer akışı ve sızıntı sınırları

- Yazma: kullanıcı → tüketicinin Web formu → tüketici servisi → `POST /api/internal/v1/tenant-secrets` (gövdede değer) → şifreleme → Mongo.
  Cevap: `{ vaultRef, secretVersion, setAt, expiresAt }`.
- Okuma: tüketici işçisi → `POST …/{vaultRef}/resolve` → `{ value, secretVersion, expiresAt }` (+ AC-22'de `previous`). Tüketici değeri
  kalıcı saklamaz; bellekte en çok 300 saniye önbelleğe alabilir (sözleşmede yazılır; MOD-0179 §8.5 ile uyumlu).
- Değerin bulunamayacağı yerler (AC-11, AC-17 taraması): HTTP cevapları (resolve hariç), uygulama logları (tüm seviyeler), OTel span
  öznitelikleri, `audit_outbox` / `audit_events`, idempotency deposu, Mongo'daki tüm koleksiyonlar (ham BSON bayt taraması), istisna mesajları,
  doğrulama hata mesajları (`{PropertyValue}` yok).
- Komut kayıtları (`record`) değer alanını `SensitiveSecretValue` olarak taşır ve `PrintMembers`'ı geçersiz kılar: `LoggingBehavior` bugün
  yalnız istek adını loglar (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/LoggingBehavior.cs:22-37`), ama
  istisna loglanır — istisna metni değer içermez.

### 8.5 Sırrın yaşam döngüsü

- **Yaz.** Anahtar (`TenantId, OwnerModule, LegalEntityId, ScopeRef, Name`) için etkin kayıt yoksa `SecretVersion = 1`. Etkin kayıt varsa
  409 `SECRET_ALREADY_EXISTS` (tüketici rotate çağırır). İptal edilmiş kaydın anahtarına yeniden yazmak yeni kayıt açar (yeni `vaultRef`).
- **Döndür.** `expectedSecretVersion` zorunlu; uyuşmazlık 409 `SECRET_VERSION_CONFLICT`. Yeni DEK + yeni şifreli metin; `SecretVersion + 1`;
  önceki şifreli metin ve sarılı DEK **aynı yazımda** sıfırlanır (kripto-imha) — AC-22 kabul edilirse ve ad listede işaretliyse önceki zarf
  `PreviousValidUntil`'e (0..72 saat) kadar tutulur.
- **İptal (soft).** Gerekçe 10..500; `State = Revoked`, `IsDeleted = true`, `DeletedAt`, `RevokedAt/By/Reason`; `Cipher` ve `PreviousCipher`
  `null`. Kayıt meta olarak kalır.
- **Son kullanma.** `ExpiresAt` geçtiyse resolve 409 `SECRET_EXPIRED` (değer yok); meta `state: Expired`. Saat `TimeProvider`'dan gelir.

### 8.6 KEK yaşam döngüsü ve kurtarma

- **Döndürme.** Yeni KEK `Current`, eski KEK `Previous:{id}` olarak verilir; Platform iki KEK ile açar. `KekRewrapJob` (barındırılan iş,
  `Diten.Platform.MOD-0012-FU01.KekRewrapJob`) eski `KekId`'li kayıtların **yalnız DEK'ini** yeni kiracı KEK'iyle yeniden sarar; şifreli metin,
  `SecretVersion` ve `SetAt` değişmez; 100'lük toplu işlerde, her kiracı için bir denetim satırı (§21). Kalan eski KEK'li kayıt sayısı
  sağlık uç noktası ve log ile raporlanır; sıfır olunca `Previous` kaldırılabilir.
- **Bilinmeyen KekId.** `Previous` erken kaldırılırsa o kayıtlar için resolve 503 `SECRET_KEY_UNAVAILABLE` (değer yok, hata logu, kayıt
  kimliği + KekId; değer yok).
- **Yedekleme ve kurtarma (Q-2).** Mongo yedeği KEK olmadan açılamaz (istenen davranış). KEK yedeği Mongo yedeğinden **ayrı** saklanır.
  KEK kaybında kurtarma: sırlar tüketici ekranından yeniden girilir (runbook adımı; kasa "kayıp" durumunu `SECRET_KEY_UNAVAILABLE` ile bildirir).
  Runbook (`docs/guides/operations/tenant-secret-vault-runbook.md`, yazılacak) döndürme, yedek ve geri yükleme tatbikatını yazar.

### 8.7 Hata kodları

| HTTP | Kod | Ne zaman |
|---|---|---|
| 400 | `SECRET_VALIDATION_FAILED` | alan kuralı (§12); mesajda alan adı var, değer yok |
| 401 | — (gövdesiz) | jeton yok / geçersiz / yanlış audience / insan jetonu |
| 403 | `SECRET_CONSUMER_NOT_ALLOWED`, `SECRET_DELEGATED_PERMISSION_MISSING`, `SECRET_DELEGATED_TENANT_MISMATCH` | liste dışı istemci; temsilî izin yok; temsilî kullanıcı başka kiracıda |
| 404 | `SECRET_NOT_FOUND` | kayıt yok · başka kiracı · başka tüketici modül (gövde bayt-eşit) |
| 409 | `SECRET_ALREADY_EXISTS`, `SECRET_VERSION_CONFLICT`, `SECRET_REVOKED`, `SECRET_EXPIRED`, `IDEMPOTENCY_KEY_REUSED` | §8.5, §4.2 |
| 422 | `SECRET_NAME_NOT_ALLOWED`, `SECRET_LEGAL_ENTITY_REQUIRED`, `SECRET_LEGAL_ENTITY_FORBIDDEN` | ad ya da şirket kuralı tüketici girişine uymaz |
| 500 | `SECRET_INTEGRITY_FAILED` | GCM etiketi tutmadı (kurcalama / transplant); hata logu + kayıt kimliği |
| 503 | `SECRET_KEY_UNAVAILABLE`, `SECRET_AUDIT_UNAVAILABLE` | KekId bilinmiyor; işlem içi denetim yazılamadı (komut başarısız, kayıt değişmez) |

MOD-0179 bu kodları kendi `TPL_VAULT_UNAVAILABLE` (503) ve doğrulama kodlarına eşler (MOD-0179 paketi satır 780).

### 8.8 Sözleşme (TENANT-SECRET-VAULT)

- `TENANT-SECRET-VAULT 1.0.0` — `docs/analysis/contracts/tenant-secret-vault.openapi.yaml` (yazılacak, DL-2 ilk adımı): beş iç uç, istek /
  cevap şemaları, hata kodları, `Idempotency-Key` ve `X-Delegated-Authorization` başlıkları, tüketici önbellek üst sınırı (300 s).
- Sözleşme MOD-0179 DL-4 başlamadan dondurulur (ayrı CT adımı); dondurulduktan sonra kırıcı değişiklik yeni ana sürümdür.

### 8.9 Lookup & Reference Data Decision

- DL-1…DL-3 lookup kullanmaz.
- DL-4 süzgeci: kiracı (mevcut platform-admin kiracı listesi `GET /api/admin/tenants`, same-origin proxy üzerinden), durum ve sahibi modül.
  Durum ve sahibi modül için yeni Platform lookup anahtarları önerilir: `tenant-secret-states`, `tenant-secret-owner-modules`
  (`/api/lookups/{key}`) — açık test kapısı AC-25'te. Sabit yedek liste yok. MDM / referans verisi kapsamı yok.

## 9. Layout & Shell Contract

- `shell: platform-admin` yalnız DL-4 için anlamlıdır; DL-1, DL-2, DL-3, DL-5 arka uçtur (ekran yok).
- DL-4: `frontend/Diten.Web/Views/Platform/TenantSecrets/*.cshtml` dosyalarının hepsinde `Layout = "_LayoutPlatformAdmin";` **açıkça** yazılır;
  `_ViewStart.cshtml` varsayılanı değiştirilmez; `_Layout.cshtml` kullanılmaz. Canlı emsal `Views/Platform/Tenants/`.
- Platform admin modülü → 2 dil (en, tr) — `.antigravity/rules/localization-standard.md:8-9`; domain-config satır 59.

## 10. Backend File Convention

Golden Reference birebir (DEV-0000 / DEV-0001 klasör ve ad kuralı):

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/TenantSecrets/
├── Commands/
│   ├── WriteTenantSecretCommand.cs            (sealed record)
│   ├── RotateTenantSecretCommand.cs
│   ├── RevokeTenantSecretCommand.cs
│   ├── RewrapTenantSecretsCommand.cs          (DL-3)
│   └── PurgeExpiredPreviousSecretsCommand.cs  (DL-3, yalnız AC-22)
├── Queries/
│   ├── GetTenantSecretMetadataQuery.cs
│   ├── ResolveTenantSecretQuery.cs            (DL-2)
│   ├── GetTenantSecretListQuery.cs            (DL-4)
│   └── GetTenantSecretByIdQuery.cs            (DL-4)
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── WriteTenantSecretHandler.cs
│   │   ├── RotateTenantSecretHandler.cs
│   │   ├── RevokeTenantSecretHandler.cs
│   │   ├── RewrapTenantSecretsHandler.cs
│   │   └── PurgeExpiredPreviousSecretsHandler.cs
│   └── QueryHandlers/
│       ├── GetTenantSecretMetadataHandler.cs
│       ├── ResolveTenantSecretHandler.cs
│       ├── GetTenantSecretListHandler.cs
│       └── GetTenantSecretByIdHandler.cs
├── Validators/
│   ├── WriteTenantSecretValidator.cs
│   ├── RotateTenantSecretValidator.cs
│   └── RevokeTenantSecretValidator.cs
├── Ports/
│   ├── ITenantSecretCipher.cs
│   └── IKeyEncryptionKeyProvider.cs
├── TenantSecretAuditIntent.cs
├── TenantSecretErrorCodes.cs
└── TenantSecretsModels.cs                     (tüm DTO'lar tek dosyada; hiçbir DTO değer ya da zarf alanı taşımaz — resolve cevabı hariç)
```

- Handler'da `Command` / `Query` soneki yok; validator'da `Command` soneki yok.
- Handler şifreleme yapmaz; `ITenantSecretCipher` portunu çağırır (handler-design: işleyici iş kuralını taşır, kripto Infrastructure'dadır).
- Cevaplar `Response<T>` zarfıyla; iç uçların hata gövdesi de aynı zarf.
- Yazma komutları `ITransactionOwnedAuditCommand` taşır ve `TenantSecretAuditIntent` üzerinden işlem içi denetim yazar (Q-5, X-3).

## 11. Frontend File Contract

Yalnız DL-4 (isteğe bağlı, Q-7). Golden Reference **Slim**, `data_mode: server` (kiracılar arası sınırsız küme).

```text
frontend/Diten.Web/Views/Platform/TenantSecrets/
├── Index.cshtml                    (Layout = "_LayoutPlatformAdmin" AÇIKÇA)
├── _Filter.cshtml
├── _DataTable.cshtml               (data-dt-standard="v2", data-dt-data-mode="server", iskelet)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml     (yalnız "İptal et" formu: gerekçe — oluşturma / düzenleme YOK; Q-7'de adlı sapma)
├── _DetailsQuickView.cshtml        (yalnız meta)
└── TenantSecretsIndex.cs
frontend/Diten.Web/wwwroot/assets/js/Platform/TenantSecrets/
├── index.js
└── index.l10n.js
frontend/Diten.Web/Controllers/Platform/TenantSecretsController.cs   (same-origin proxy → Gateway /api/admin/tenant-secrets; her yazma [ValidateAntiForgeryToken])
```

- Kolonlar: kiracı, şirket, sahibi modül, ad, durum rozeti, `SecretVersion`, son değişim, son kullanma. Değer, şifreli metin, `ScopeRef`
  dışında kaynak ayrıntısı yok; HTML'de değer alanı hiç yok.
- Form alanı sayımı: 1 (iptal gerekçesi) → Slim.
- Dokunma protokolü (BL-440): ekran dosyalarına dokunulduğunda `verify_datatable_page.py --area Platform --module TenantSecrets --format gaps`.

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB düzeyi | Ön kontrol |
|---|---|---|---|---|
| `scopeRef` | Evet (yaz) | trim yok, 1..128, `[A-Za-z0-9._:-]` | tekil anahtarın parçası | — |
| `name` | Evet (yaz) | tüketici girişinin `AllowedNames` kümesinde, harf duyarlı | tekil anahtarın parçası | 422 `SECRET_NAME_NOT_ALLOWED` |
| `legalEntityId` | girişe göre | uuid; `Required` girişte zorunlu, `Forbidden` girişte yasak | tekil anahtarın parçası | 422 |
| `value` | Evet (yaz, döndür) | UTF-8, 1..16384 bayt; trim edilmez, olduğu gibi saklanır; doğrulama mesajında değer yok | — | — |
| `expiresAt` | Hayır | UTC; `now + 1 dk` < değer ≤ `now + 5 yıl` | — | — |
| `expectedSecretVersion` | Evet (döndür, iptal) | int ≥ 1 | `Version` eşzamanlılık | 409 `SECRET_VERSION_CONFLICT` |
| `reason` | Evet (iptal) | trim, 10..500 | — | — |
| `previousValidForHours` | Hayır (döndür) | 0..72; yalnız `VerificationGraceNames` içindeki ad için, aksi 422 | — | — |
| `use` | Hayır (resolve) | `Use` (varsayılan) \| `Verify`; `previous` yalnız `Verify` + pencere içinde döner | — | — |
| `Idempotency-Key` başlığı | Evet (yaz, döndür, iptal) | 8..128, `[A-Za-z0-9-_]` | tekil `{TenantId, ClientId, Key}` | 409 `IDEMPOTENCY_KEY_REUSED` |
| `X-Delegated-Authorization` | Evet (yaz, döndür, iptal) | geçerli kullanıcı JWT'si, aynı kiracı, temsilî izin | — | 403 |

İlişkili kural: aynı anahtarda etkin kayıt varsa yaz 409; `Revoked` kayıtta döndür 409 `SECRET_REVOKED`.

## 13. Failure Path to Verify

- **Mükerrer yazma** — Expected: 409 `SECRET_ALREADY_EXISTS`; ikinci kayıt yok; denetimde ret satırı (`Outcome = Failed`).
- **Eksik değer / boş ad** — Expected: 400 `SECRET_VALIDATION_FAILED`; kayıt yok; mesajda değer yok.
- **Yetkisiz servis** (liste dışı istemci, yanlış audience, insan jetonu) — Expected: 401 / 403; sıfır yazım; ret denetimi (401 hariç — kimliği
  doğrulanmamış çağrı denetim satırı üretmez, log üretir).
- **Temsilî kullanıcı başka kiracıda / izni yok** — Expected: 403; sıfır yazım; ret denetimi.
- **Eşzamanlılık** — iki döndürme aynı `expectedSecretVersion` ile — Expected: biri 200, diğeri 409 `SECRET_VERSION_CONFLICT`; sessiz üzerine yazma yok.
- **Kiracılar arası** — B jetonuyla A'nın `vaultRef`'i (meta, resolve, rotate, revoke) — Expected: 404, gövde var olmayan kayıtla bayt-eşit.
- **Başka tüketici modül** aynı kiracıda — Expected: 404 (aynı gövde).
- **Şifreli metin transplantı** — Expected: 500 `SECRET_INTEGRITY_FAILED`; değer yok; hata logu.
- **Denetim yazılamıyor** (outbox hatası enjekte) — Expected: 503 `SECRET_AUDIT_UNAVAILABLE`; sır kaydı değişmez (işlem geri alınır).
- **KEK yok / zayıf** — Expected: Platform başlamaz; mesajda anahtar adı, değer yok.
- **Bilinmeyen KekId** — Expected: 503 `SECRET_KEY_UNAVAILABLE`; değer yok.
- **Süresi geçmiş / iptal edilmiş sır** — Expected: 409 `SECRET_EXPIRED` / `SECRET_REVOKED`; değer yok.
- **Aynı idempotency anahtarı, farklı değer** — Expected: 409 `IDEMPOTENCY_KEY_REUSED`; ilk kayıt değişmez.

## 14. Authorization Convention

```text
İç uçlar (DL-1, DL-2):
  [Authorize(AuthenticationSchemes = TrustedServiceTokenValidationExtensions.SecretVaultAuthenticationScheme)]
  — [HasPermission] yok: insan izni bu uçların kimliği değildir (emsal InternalTrustedWorkflowConsumerController,
    services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedWorkflowConsumerController.cs:14-16).
  Yetki = kodda sabit tüketici listesi (servis adı + istemci + ad) + yazmada temsilî kullanıcının, girişte adı yazılı izni.
  Actor type: service (sahibi modül) + delegated tenant_user (yalnız yazma).
Admin uçları (DL-4):
  [Authorize(Policy = "PlatformActor")]
  [HasPermission("platform.tenant-secrets.read")]     — liste, ayrıntı (yalnız meta)
  [HasPermission("platform.tenant-secrets.revoke")]   — iptal (Tier-3 eylem `revoke`, bu pakette kayıtlı — PKS-001 §5)
  Actor type: platform_admin.
```

- Hiçbir izin bir insana değer okutmaz; `platform_admin`'in tüm izinlere geçişi resolve şemasına ulaşmaz (ayrı şema).
- `platform.*` ad alanı `Diten.Platform`'undur (`.antigravity/rules/permission-key-standard.md:98`); yeni ad alanı satırı gerekmez.
- `tenant-secrets` kaynak adı, MOD-0012 ana paketinin çalışma zamanı sırlarından ayırmak için seçildi (`platform.secrets.*` değil).
- UAS-001: DL-4'te izni olmayana sayfa iskeleti çizilmez, yönlendirme yok.

## 15. Gateway / API Routing Decision

Karar: DL-1…DL-3 için Gateway değişikliği **gereksiz ve yasak**; DL-4 için **gerekli** (integration-agent).

- İç uçlar `/api/internal/v1/tenant-secrets/**` Gateway'den erişilemez olmalıdır. Bugün `ocelot.json`'da `/api/internal` ile başlayan ya da
  genel catch-all rota yok (ölçüm, §19.2). DL-1 bir rota testi ekler: gerçek `ocelot.json`'u okur ve hiçbir upstream şablonunun bu yolları
  eşlemediğini doğrular (emsal `gateway/Diten.ApiGateway.Tests/Mod0173Pm2InternalInventoryRouteTests.cs:8-15`).
- DL-4: `/api/admin/tenant-secrets` ve `/api/admin/tenant-secrets/{everything}` açık rota çifti (OPTIONS dahil) — integration-agent işi
  (X-4); bu paket `ocelot.json` yazmaz. Mevcut `/api/admin/tenants/{everything}` catch-all'u bu yolu eşlemez (farklı önek).
- Tüketici servis Platform'u doğrudan çağırır (5057; MDM → Platform iç çağrı emsali); Web asla çağırmaz.

## 16. Acceptance Criteria

**DL-1 — çekirdek: kayıt, şifreleme, yazma uçları, denetim**
- [ ] AC-1 (zorunlu) — KEK başlangıç doğrulaması: `TenantSecretVault:Kek:Current` yok, boş, 32 bayttan farklı ya da bilinen yer tutucu, ya da `Previous` içinde `Current` ile aynı değer → Platform başlamaz (`SecretValidationException`); mesajda anahtar adı var, değer yok. S-1: doğrulama kaydını kaldır → başlangıç testi kırmızı.
- [ ] AC-2 (zorunlu) — Şifresiz yazım yok: yazmadan sonra Mongo'daki `platform_tenant_secrets` belgesinin ham BSON baytlarında nöbetçi değer yok; aynı değer iki kayda yazılınca şifreli metinler ve nonce'lar farklı. S-2a: `ITenantSecretCipher`'ı kimlik fonksiyonuyla değiştir → bayt taraması kırmızı; S-2b: nonce'u sabitle → "farklı şifreli metin" vakası kırmızı.
- [ ] AC-3 (zorunlu) — AAD bağlama: bir kaydın `Cipher` zarfı aynı kiracıda başka kayda kopyalanınca resolve 500 `SECRET_INTEGRITY_FAILED`, değer yok. S-3: AAD'yi boş geçir → transplant vakası kırmızı.
- [ ] AC-4 (zorunlu) — Kiracı başına KEK: A kiracısının kaydı B kiracısının türetilmiş KEK'iyle açılamaz (zarf B'ye kopyalanır → `SECRET_INTEGRITY_FAILED`). S-4: HKDF tuzunu sabitle → vaka kırmızı.
- [ ] AC-5 (zorunlu) — Yaz: listedeki servis kimliği + aynı kiracıdan temsilî kullanıcı + girişteki izin → 201 `{ vaultRef, secretVersion: 1, setAt, expiresAt }`; cevapta değer ve zarf alanı yok; aynı anahtarda ikinci yazma 409 `SECRET_ALREADY_EXISTS`. S-5: tekil kısmi indeksi ve ön kontrolü kaldır → çift kayıt vakası kırmızı.
- [ ] AC-6 (zorunlu) — Döndür: `SecretVersion` +1, `SetAt` yenilenir, önceki şifreli metin ve sarılı DEK aynı yazımda sıfırlanır (ham BSON'da eski nöbetçinin şifreli metni yok); yanlış `expectedSecretVersion` 409. S-6: sıfırlama adımını kaldır → "eski zarf yok" vakası kırmızı.
- [ ] AC-7 (zorunlu) — İptal soft'tur: `State = Revoked`, `IsDeleted = true`, `DeletedAt` dolu, zarflar `null`, belge koleksiyonda durur; resolve 409 `SECRET_REVOKED`, değer yok. S-7: iptali `DeleteOne` ile yap → "kayıt duruyor" vakası kırmızı.
- [ ] AC-8 (zorunlu) — Yetkisiz yazma: liste dışı servis adı, liste dışı istemci, yanlış audience, insan jetonu, başka kiracıdaki temsilî kullanıcı, temsilî izni olmayan kullanıcı, girişte olmayan ad → 401 / 403 / 422 ve **sıfır yazım** (sır, idempotency ve iş kaydı yok); kimliği doğrulanmış retlerde ret denetim satırı. S-8a: liste denetimini kaldır → liste dışı istemci vakası kırmızı; S-8b: temsilî kiracı eşitliğini kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Kiracı sızıntısı yok: B kiracısı jetonuyla A'nın `vaultRef`'i için meta, döndür, iptal (ve DL-2'de resolve) → 404 `SECRET_NOT_FOUND`, gövde var olmayan `vaultRef` cevabıyla bayt-eşit; aynı kiracıda başka tüketici modülün kaydı da 404. S-9: depodaki `TenantId` süzgecini kaldır → kırmızı.
- [ ] AC-10 (zorunlu) — Denetim işlem içidir: yaz, döndür, iptal tam bir `audit_outbox` satırını iş verisiyle aynı işlemde yazar (platform-islem-ici), üretimdeki `AuditOutboxProcessor` onu `audit_events`'e taşır; aktör temsilî kullanıcı, `SourceService` servis adı, olay adları §21; outbox yazımı başarısızsa 503 `SECRET_AUDIT_UNAVAILABLE` ve sır kaydı değişmez. S-10a: `TenantSecretAuditIntent.EnqueueAsync` çağrısını kaldır → teslim testi ve defter testi kırmızı; S-10b: denetim yazımını işlem dışına al → "kayıt yoksa işlem yok" vakası kırmızı.
- [ ] AC-11 (zorunlu) — Değer sızıntısı yok (yazma yolu): rastgele 48 karakterlik nöbetçi değerle yaz + döndür + iptal + hata yolları (400, 409, enjekte 500) sonrası nöbetçi şu yerlerin hiçbirinde yok: HTTP cevapları, test log havuzu (tüm seviyeler), `audit_outbox`, `audit_events`, idempotency deposu, tüm Mongo koleksiyonlarının ham dökümü, istisna metinleri. S-11a: komut kaydında `PrintMembers` maskesini kaldır ve isteği logla → log taraması kırmızı; S-11b: doğrulama mesajına `{PropertyValue}` ekle → cevap taraması kırmızı.
- [ ] AC-12 (zorunlu) — Idempotency: aynı anahtar + aynı değer → aynı cevap, ikinci kayıt ve ikinci denetim satırı yok; aynı anahtar + farklı değer → 409 `IDEMPOTENCY_KEY_REUSED`; parmak izi düz değerin özetini taşımaz (yalnız KEK'ten türetilmiş anahtarla HMAC). S-12: HMAC'i parmak izinden çıkar → farklı değer vakası kırmızı.
- [ ] AC-13 (zorunlu) — Tüketici listesi kodda sabittir: yapılandırmada bilinmeyen modül, kümede olmayan ad, ikinci giriş, boş istemci kimliği ya da 16'dan fazla giriş → Platform başlamaz. S-13: doğrulayıcı kaydını kaldır → başlangıç testi kırmızı.
- [ ] AC-14 (zorunlu) — Gateway iç yolları eşlemez: gerçek `ocelot.json`'u okuyan test, `/api/internal/v1/tenant-secrets`, büyük harfli biçimi ve alt yolları için eşleşen rota bulmaz. S-14: geçici çalışma ağacında `ocelot.json`'a `/api/internal/{everything}` ekle → test kırmızı.
- [ ] AC-15 (zorunlu) — Meta sorgusu: `GET …/{vaultRef}/metadata` `{ vaultRef, name, state, secretVersion, setAt, setBy, expiresAt }` döner; hiçbir DTO (resolve cevabı dışında) `byte[]`, zarf ya da `value` alanı taşımaz (yansıma testi). S-15: meta DTO'ya `Cipher` ekle → yansıma testi kırmızı.

**DL-2 — servis kimliğiyle okuma (resolve) ve sözleşme**
- [ ] AC-16 (zorunlu) — Resolve: listedeki servis jetonu + aynı kiracı + kendi modülünün `vaultRef`'i → 200 `{ value, secretVersion, expiresAt }`, `Cache-Control: no-store`; değer yazılanla bayt-eşit (PEM dahil). S-16: `no-store` başlığını kaldır → kırmızı.
- [ ] AC-17 (zorunlu) — Yetkisiz okuma yok: insan jetonu (`platform_admin` dahil), başka audience, liste dışı servis → 401; başka kiracı ya da başka tüketici modül → 404; iptal → 409 `SECRET_REVOKED`; süresi geçmiş (sahte saat) → 409 `SECRET_EXPIRED`; hiçbirinde değer yok. S-17a: resolve ucunun şemasını varsayılan kullanıcı JWT şemasına çevir → insan jetonu vakası kırmızı; S-17b: süre denetimini kaldır → süresi geçmiş vakası kırmızı.
- [ ] AC-18 (zorunlu) — Okuma yolu sızıntısı yok: resolve sonrası nöbetçi değer test log havuzunda, OTel span özniteliklerinde (test dışa aktarıcısı), denetim tablolarında ve hata cevaplarında yok. S-18: span'a cevap gövdesini öznitelik olarak ekle → kırmızı.
- [ ] AC-19 (zorunlu) — Sözleşme testi: `tenant-secret-vault.openapi.yaml` içindeki beş uç, alan adları, hata kodları ve başlıklar üretim controller'ı ve DTO'larıyla eşleşir. S-19: bir cevap alanının adını değiştir → sözleşme testi kırmızı.

**DL-3 — anahtar yaşam döngüsü**
- [ ] AC-20 (zorunlu) — KEK döndürme: yeni `Current` + eski `Previous` ile her iki KEK'li kayıt okunur; `KekRewrapJob` sonrasında eski `KekId`'li kayıt kalmaz, şifreli metin ve `SecretVersion` değişmez, resolve aynı değeri döner; `Previous` erken kaldırılırsa o kayıtlar 503 `SECRET_KEY_UNAVAILABLE`. S-20: işte DEK'i yeniden sarmadan yalnız `KekId`'yi güncelle → resolve vakası kırmızı.
- [ ] AC-21 (zorunlu) — Son kullanma ve iptal edilmiş anahtara yeniden yazma: `ExpiresAt` sahte saatle geçince meta `Expired`, resolve 409; iptal edilen anahtara yazma yeni `vaultRef` açar, eski kayıt `Revoked` kalır. S-21: tekil indeksten `IsDeleted` koşulunu kaldır → yeniden yazma vakası kırmızı.
- [ ] AC-22 (isteğe bağlı) — Doğrulama geçiş penceresi (Q-6): `VerificationGraceNames` içindeki ad için döndürmede `previousValidForHours` (0..72) verilirse resolve `use = Verify` önceki değeri pencere içinde `previous` olarak döner; pencere bitince `PurgeExpiredPreviousSecretsCommand` önceki zarfı sıfırlar; listede olmayan ad için 422. S-22: pencere sonu denetimini kaldır → sahte saat vakası kırmızı.
- [ ] AC-23 (zorunlu) — Yedek ve geri yükleme: Mongo dökümü + aynı KEK ile geri yüklenen veritabanında resolve aynı değeri döner; farklı KEK ile 500 `SECRET_INTEGRITY_FAILED` (değer yok); runbook dosyası adımları ve tatbikat komutlarını yazar. S-23: geri yüklemede `KekId` eşlemesini yok say → beklenen ret vakası kırmızı.

**DL-4 — platform-admin meta ekranı (isteğe bağlı, Q-7)**
- [ ] AC-24 (isteğe bağlı) — `/Platform/TenantSecrets` listesi yalnız meta gösterir; tüm `Views/Platform/TenantSecrets/*.cshtml` dosyalarında `Layout = "_LayoutPlatformAdmin"` açıkça yazılı; `data-dt-standard="v2"` + `data-dt-data-mode="server"`; `verify_datatable_page.py --area Platform --module TenantSecrets` ve `quality-gate-datatable` PASS; çizilen HTML'de nöbetçi değer yok. S-24: QuickView'a zarf alanı bas → HTML taraması kırmızı.
- [ ] AC-25 (isteğe bağlı) — Yetki ve lookup: `platform.tenant-secrets.read` olmadan sayfa iskeleti yok (UAS-001), uç 403; iptal `platform.tenant-secrets.revoke` + gerekçe 10..500 ister ve §21 satırını yazar; süzgeç `/api/lookups/tenant-secret-states` ve `/api/lookups/tenant-secret-owner-modules` `LookupOptionDto` döner, sabit yedek liste yok. S-25: iptal ucundan `[HasPermission]` kaldır → 403 vakası kırmızı.
- [ ] AC-26 (isteğe bağlı) — Dil ve yardım: en + tr anahtar paritesi; `Help.TENANTSECRETS.*` metinleri (§25); nav anahtarı `Nav.Page.TENANTSECRETS`. S-26: bir tr anahtarını sil → parite testi kırmızı.

**DL-5 — Auth audience (XMC X-1, sahibinin izniyle)**
- [ ] AC-27 (zorunlu) — Auth yalnız (`Diten.Inventory`, `TRUSTED_SECRET_VAULT_CONSUMER`) çiftine yeni jeton verir; MDM'nin üç çifti aynen geçer; başka her çift (ör. `Diten.Inventory` + workflow audience) reddedilir. S-27: `IsAllowedPair`'i servis adından bağımsız yap → ret vakası kırmızı.

**Tüm dilimler**
- [ ] AC-28 (zorunlu) — Platform tam test takımı (filtresiz), Gateway testleri ve mimari testler (denetim defteri dahil) yeşil; dokunulan her servisin derlemesi PASS; kırmızı varsa aynı gün `main` taban çizgisiyle karşılaştırılır. S-28: §21'deki bir komutu defterden düşür → defter testi kırmızı.

## 17. Test Expectations

- **Üretim koduyla ölçüm.** HTTP + Mongo testleri gerçek Platform kompozisyonunu (`WebApplicationFactory`, gerçek `Program.cs`, gerçek Mongo
  replica set) kullanır; kural kopyası test edilmez. Kripto birim testleri gerçek `AesGcmTenantSecretCipher` üzerindedir.
- **Nöbetçi taraması.** Her test rastgele bir nöbetçi üretir; tarama ham baytlarda (UTF-8 ve base64 biçimleri) yapılır.
- **Kiracı izolasyonu** (AC-9, AC-17), **soft delete** (AC-7), **eşzamanlılık** (AC-6), **idempotency** (AC-12).
- **Build:** `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`; DL-4'te
  `frontend/Diten.Web/Diten.Web.csproj`; DL-5'te AuthService API; Gateway testleri (AC-14).
- **DL-4:** `verify_datatable_page.py`, `quality-gate-datatable`, RESX parite (en, tr), vitest izin etiketi defteri (yeni `revoke` fiili
  `perm-label.js` ailesinde zaten var — yine de etiket testi koşulur), tarayıcı smoke (liste → QuickView → iptal).
- **Sabotaj kanıtı.** Her S-n ayrı (ayrık) çalışma ağacında uygulanır, ilgili testin kırmızı olduğu kayda geçer, geri alınır (Çekirdek madde 10).

### 17.1 AC → test → sabotaj eşlemesi

| AC | Test (öneri adı) | Sabotaj |
|---|---|---|
| AC-1 | `TenantSecretVaultStartupTests.MissingOrWeakKek_StopsPlatform` | S-1 |
| AC-2 | `TenantSecretCipherMongoTests.StoredDocument_HasNoPlaintext_AndUniqueNonces` | S-2a, S-2b |
| AC-3 | `TenantSecretCipherTests.TransplantedEnvelope_FailsIntegrity` | S-3 |
| AC-4 | `TenantSecretCipherTests.OtherTenantKek_CannotOpen` | S-4 |
| AC-5 | `TenantSecretsInternalHttpMongoTests.Write_Returns201_WithoutValue_AndDuplicate409` | S-5 |
| AC-6 | `TenantSecretsInternalHttpMongoTests.Rotate_ShredsPreviousEnvelope` | S-6 |
| AC-7 | `TenantSecretsInternalHttpMongoTests.Revoke_IsSoft_AndResolveRefuses` | S-7 |
| AC-8 | `TenantSecretsInternalHttpMongoTests.UnauthorizedWriters_ZeroWrites` | S-8a, S-8b |
| AC-9 | `TenantSecretsInternalHttpMongoTests.CrossTenant_And_CrossConsumer_404_ByteEqual` | S-9 |
| AC-10 | `TenantSecretAuditDeliveryMongoTests.WriteRotateRevoke_OneRowInTransaction_FailClosed` | S-10a, S-10b |
| AC-11 | `TenantSecretLeakScanTests.WritePath_SentinelAbsentEverywhere` | S-11a, S-11b |
| AC-12 | `TenantSecretsInternalHttpMongoTests.Idempotency_SameKeyDifferentValue_409` | S-12 |
| AC-13 | `TrustedSecretVaultConsumerAuthorizationOptionsValidatorTests` | S-13 |
| AC-14 | `gateway/Diten.ApiGateway.Tests/Mod0012Fu01InternalSecretVaultRouteTests` | S-14 |
| AC-15 | `TenantSecretDtoShapeTests.NoDtoCarriesValueOrEnvelope` | S-15 |
| AC-16 | `TenantSecretsResolveHttpMongoTests.Resolve_ReturnsExactBytes_NoStore` | S-16 |
| AC-17 | `TenantSecretsResolveHttpMongoTests.UnauthorizedReaders_Refused` | S-17a, S-17b |
| AC-18 | `TenantSecretLeakScanTests.ReadPath_SentinelAbsentFromLogsSpansAudit` | S-18 |
| AC-19 | `TenantSecretVaultContractTests` | S-19 |
| AC-20 | `KekRewrapJobMongoTests.Rewrap_KeepsCiphertext_AndRetiresOldKek` | S-20 |
| AC-21 | `TenantSecretsLifecycleMongoTests.Expiry_And_RewriteAfterRevoke` | S-21 |
| AC-22 | `TenantSecretsLifecycleMongoTests.VerificationGrace_EndsAndPurges` | S-22 |
| AC-23 | `TenantSecretBackupRestoreTests.RestoreWithSameKek_Resolves` | S-23 |
| AC-24 | `TenantSecretsAdminViewTests` + `verify_datatable_page.py` | S-24 |
| AC-25 | `TenantSecretsAdminHttpTests.PermissionsAndLookups` | S-25 |
| AC-26 | RESX parite + yardım testi | S-26 |
| AC-27 | `ServiceIdentityTokenAudiencePolicyTests` (AuthService) | S-27 |
| AC-28 | tam takım koşusu (CT) | S-28 |

## 18. Ready-for-dev Checklist

- [ ] DCP-002 ön kontrolü bu sürümde yeniden koşuldu ve registry satırı (`MOD-0012-FU01`) registry sürecinde eklendi
- [ ] Golden Reference Slim okundu (yalnız DL-4)
- [ ] Frontmatter zorunlu alanları dolu (service, shell, golden_reference, data_mode, entity_base, form_field_count)
- [ ] Layout & Shell Contract'ta `_LayoutPlatformAdmin` açıkça yazılı
- [ ] Backend File Convention Golden Reference ile birebir (CommandHandlers / QueryHandlers ayrımı, soneksiz adlar)
- [ ] Frontend File Contract Slim dosya listesi tam; `_CreateEditOffcanvas` sapması (yalnız iptal formu) CT tarafından kabul / ret edildi (Q-7)
- [ ] Validation Rules her alan için yazılı
- [ ] Failure Path ≥ 4 senaryo (mükerrer, eksik, yetkisiz, eşzamanlılık + kiracılar arası, transplant, denetim)
- [ ] Authorization Convention: iç uç şeması, tüketici listesi, admin izinleri, actor type
- [ ] Gateway kararı: iç uç rotasız (test), admin rotası integration-agent işi
- [ ] Lookup & Reference Data Decision yazılı (§8.9)
- [ ] Audited Events her yazma komutunu tam bir kez içeriyor (§21); denetim yolu kararı (Q-5) verildi
- [ ] Q-1 (KEK yeri) ve Q-2 (yedekleme) Ali tarafından kapatıldı
- [ ] MOD-0179 temsilî izin anahtarı (MOD-0179 Q-1) kesinleşti ve §8.3 v1 kümesine yazıldı
- [ ] Sayfa yardım metni (DL-4): `Help.TENANTSECRETS.Purpose`, `.Audience`, `.Step1..3` + `Title` (en, tr); tur yok
- [ ] CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı (yüksek risk: paketi yazmamış, `high: evet` onaylayıcı)

## 19. Implementation Notes

### 19.1 Okunan kaynaklar

- `AGENTS.md` Çekirdek, §6, §6.1, §7, §10, DCP-002 bölümü.
- `execution/registries/module-id-registry.md` (MOD-0012 satırı ve takma adları; MOD-0033-FU02 satırı).
- `execution/domains/platform-shared-services/module-packs/MOD-0012-secrets-configuration-vault.md` (ana paket, 512 satır).
- `execution/domains/platform-shared-services/domain-config.md` (Vault satırı 56, Lokalizasyon satırı 59).
- `execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md` — sınır satırı 146, sınır kuralı 4 satır 160,
  `Secrets[]` satır 276, X-14 satır 415, §8.5 satırlar 536-542, değer doğrulaması satır 746, kasa erişilemez satır 780, DL-4 AC'leri
  satırlar 896-900, Q-16 satır 1102.
- `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md:12` (T-4).
- `docs/reference/modules/platform/secrets-configuration-vault/technical-reference.md:10-24`.
- Kurallar: `module-pack-standard.md` (§1-§16), `audit-trail-standard.md` (§3-§7), `permission-key-standard.md` (§1, §4, §5),
  `localization-standard.md`, `record-coding-standard.md` (kapsam).
- Kod: §19.2'deki dosyalar.
- **Sahip kararı (kaynak satırı):** Beste, 2026-11-13, sohbet — "tamam ekleyelim bunu platform tarafına" (MOD-0179 3PL kanal sırları için
  Platform'a ayrı FU; MOD-0179 DL-4'ün kapısı). Karar kaydı CT tarafından mod0179 karar dosyasına eklenecek; bu paket yazılırken eklenmemişti.

### 19.2 Ölçülen gerçekler

1. **`ISecretsProvider` yalnız okur** — `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/ISecretsProvider.cs:3-7`
   (iki `Get…` metodu). Tek uygulaması `ConfigurationSecretsProvider`: üretimde yalnız ortam değişkeni, diğer ortamlarda `IConfiguration`
   (`ConfigurationSecretsProvider.cs:51-64`). Kayıt `SecretServiceCollectionExtensions.cs:23`.
2. **Bugünkü kayıt yerleri (`AddSecretsProvider`)**: AuthService, Platform, DevEnablement, Inventory, Manufacturing, Procurement, Gateway, Diten.Web.
3. **Bugünkü tek çalışma anı tüketicisi** Platform bildirimleridir: `SecretReferenceResolver` bir **anahtar adını** `ISecretsProvider`'dan
   çözer (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/SecretReferenceResolver.cs:13-34`);
   kiracı ayarı yalnız referans tutar (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Notifications/TenantMessagingSettings.cs:18`);
   tohum değeri `Smtp:Password` (`NotificationMessagingSettingsSeed.cs:37`). Yani kiracı sırrı bugün ancak **ortam değişkeni adı** olarak
   referanslanabilir; çalışma anında yazılamaz.
4. **Gözlem (kapsam dışı, follow-up):** `ToEnvironmentVariableName` önce `:` → `_`, sonra `_` → `__` yapar (`ConfigurationSecretsProvider.cs:64`);
   adında alt çizgi olan anahtar üretimde yanlış ortam değişkenine eşlenir. Bu paketin anahtarlarında alt çizgi yoktur. Düzeltme MOD-0012
   ana paketinin işidir (XMC-001 — sorulacak).
5. **Platform servislerinde kalıcı şifreleme yok:** `services/**`, `gateway/**`, `frontend/**` üretim kodunda `AesGcm`, `IDataProtector`,
   `AddDataProtection` kullanımı yok (yalnız Web testlerinde `EphemeralDataProtectionProvider`).
6. **Servis kimliği:** Platform'da adlı RS256 jeton doğrulayıcı ve tüketici başına audience deseni var
   (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs:16-31`), temsilî kullanıcı başlığı
   `X-Delegated-Authorization` (aynı dosya satır 31). Auth ise yalnız `Diten.MDM`'e jeton verir
   (`services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17`).
   Inventory'de servis jetonu sağlayıcısı yok.
7. **Denetim:** platform-islem-ici izi kabul edilmiş ve MOD-0040-FU02 (A-10) aynı yolu kullanıyor (`tests/architecture/audit-ledger/Diten.Platform.md:16`,
   not satır 30; yazıcı `services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/RecordCodeAuditIntent.cs:16-22`).
8. **Gateway:** `ocelot.json`'da `/api/internal` ile başlayan ya da genel catch-all rota yok; 350 upstream şablonu.
9. **Domain kararı:** "Vault: appsettings + environment variables (thin abstraction). External vault (NEW-001) ertelenmiştir."
   (`execution/domains/platform-shared-services/domain-config.md:56`). Bu FU dış kasa getirmez; ama Mongo'da şifreli kiracı sırrı saklamak bu
   satırın genişlemesidir → domain-config satırının güncellenmesi CT işi (Q-12).

### 19.3 Çıkarımlar

- Kiracı sırrının ortam değişkeninde tutulması kiracı sayısıyla ölçeklenmez ve çalışma anında yazılamaz → kalıcı, şifreli depo gerekir.
- Değeri tüketici servise ağda vermek kaçınılmazdır (sır başka serviste kullanılıyor); bu yüzden resolve tek, servis-yalnız, Gateway dışı uçtur
  ve TLS kararı (Q-4) canlıya çıkış kapısıdır.
- Denetimin işlem içi (fail-closed) olması K2 sınıfı tartışmasını gereksiz kılar: komut sınıfı ne olursa olsun kayıt yoksa işlem yoktur.

### 19.4 Açık sorular (sahip / Ali / CT)

- **Q-1 (Ali)** Ana KEK nerede? Öneri: v1 Platform ana makinesinde ortam değişkeni (`ISecretsProvider`), `IKeyEncryptionKeyProvider` dikişiyle
  ileride KMS / HSM. Kim üretir, kim erişir?
- **Q-2 (Ali)** Yedekleme / kurtarma: KEK yedeği nerede ve kim erişir; KEK kaybında kabul edilen kurtarma "sırları yeniden gir" mi?
- **Q-3 (Ali)** Servis kimliği: Inventory için Auth servis istemcisi ve kiracı grant'leri (dev / prod provisioning), X-1 izni (MOD-0033-FU02).
- **Q-4 (Ali)** Servisler arası TLS: resolve cevabı değeri ağda taşır; dev düz HTTP. Canlıda TLS / mTLS zorunlu mu, DL-2 canlı kullanım kapısı mı?
- **Q-5 (CT)** Denetim yolu platform-islem-ici (MOD-0040-FU02 A-10 emsali) — kabul mü? `AuditBehavior` sabit listesine ad ekleme (X-3) ve
  defter belirteci (X-2).
- **Q-6 (Beste)** Doğrulama geçiş penceresi (`InboundSigningSecret` için 0..72 saat, AC-22) v1'de olsun mu? Öneri: evet — 3PL ortağının imza
  anahtarını aynı saniyede değiştirmesi gerekmez.
- **Q-7 (Beste / CT)** Platform-admin meta ekranı (DL-4) v1'de mi? Öneri: hayır, MOD-0179 DL-4'ü beklemesin; acil iptal ihtiyacında açılır.
  Açılırsa `_CreateEditOffcanvas`'ın yalnız iptal formu olması adlı Slim sapmasıdır — kabul mü?
- **Q-8 (Beste / CT)** Resolve okumaları denetim kaydına yazılmaz (AUD-001 §7'nin üç sınıfına girmez); yalnız değer içermeyen OTel sayacı. Kabul mü?
- **Q-9 (Beste)** MOD-0179 "son değişim"i kendi `SetAt`'ından gösterir; kasa meta sorgusu yine de kalır (tutarlılık denetimi için). Kabul mü?
- **Q-10 (CT)** Dal: künyede tek çalışma dalı yazıldı; kanonik ad `feature/pss/mod-0012-fu01-tenant-runtime-secret-vault`. Hangisi?
- **Q-11 (CT)** Registry satırı `MOD-0012-FU01 | Tenant Runtime Secret Vault | … | parent MOD-0012` — registry sürecinde eklenecek.
- **Q-12 (CT)** domain-config "Vault" satırının güncellenmesi (§19.2 madde 9).
- **Q-13 (Ali / CT)** Ana paket MOD-0012 `status: review`; FU onayı ana paketin kapanmasını beklemez — kabul mü?
- **Q-14 (Beste)** `LegalEntityId` kasada doğrulanmaz (tüketici sunucuda çözer ve jetonun kiracısına bağlıdır). Kabul mü, yoksa Platform
  organizasyon verisiyle kiracı-şirket eşleşmesi mi denetlensin?
- **Q-15 (Beste)** Platform içi tüketiciler (bildirim SMTP `CredentialSecretRef`) bu kasaya taşınsın mı? Öneri: follow-up.

## 20. Follow-up Items

- Dış KEK sağlayıcısı (KMS / HSM) bağdaştırıcısı `IKeyEncryptionKeyProvider` arkasında.
- Kiracı kapatma / silme akışında kiracının sırlarının kripto-imhası (MOD-0009 kiracı yaşam döngüsü ile).
- Yaklaşan son kullanma bildirimi (MOD-0027).
- Platform bildirimlerinin SMTP sırrının bu kasaya taşınması (Q-15).
- `ToEnvironmentVariableName` alt çizgi hatası (MOD-0012 ana paketi, §19.2 madde 4).
- İkinci tüketici geldiğinde liste revizyonu (§8.3 ekleme kuralı).

## 21. Audited Events

| Komut | Olay adı | Nesne (tür) | Yol | Önceki / sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `WriteTenantSecretCommand` | `platform.tenant-secret.written` | `TenantSecret` | a — platform-islem-ici (`TenantSecretAuditIntent.EnqueueAsync`, işlem içi) | After: `OwnerModule`, `LegalEntityId`, `ScopeRef`, `Name`, `SecretVersion`, `ExpiresAt`, `KekId` | — |
| `RotateTenantSecretCommand` | `platform.tenant-secret.rotated` | `TenantSecret` | a — platform-islem-ici | Before / After: `SecretVersion`, `SetAt`, `ExpiresAt`, `PreviousValidUntil` | — |
| `RevokeTenantSecretCommand` | `platform.tenant-secret.revoked` | `TenantSecret` | a — platform-islem-ici | Before / After: `State`; `RevokeReason`; aktör türü (temsilî kullanıcı ya da platform_admin) | — |
| `RewrapTenantSecretsCommand` | `platform.tenant-secret.rewrapped` | `TenantSecret` (kiracı başına bir satır) | a — platform-islem-ici (`ActorType = System`, `SourceService = Diten.Platform`) | kayıt sayısı, eski `KekId`, yeni `KekId` | — |
| `PurgeExpiredPreviousSecretsCommand` | `platform.tenant-secret.previous-purged` | `TenantSecret` | a — platform-islem-ici (`ActorType = System`) | `PreviousSecretVersion`, `PreviousValidUntil` | — (yalnız AC-22 kabul edilirse) |
| (yaz / döndür / iptal reddi) | `platform.tenant-secret.write-refused` | `TenantSecret` | a — `TenantSecretAuditIntent.EnqueueRefusalAsync` (kendi kısa işlemi, MOD-0040-FU02 ret emsali) | `Outcome = Denied` / `Failed`, ret kodu | — |

- **Okuma satırı yok:** `ResolveTenantSecretQuery` ve meta sorguları AUD-001 §7'nin üç okuma sınıfına girmez → denetlenmez (Q-8);
  değer içermeyen OTel sayacı (`tenant_secret_resolve_total{owner_module, outcome}`).
- **Kayda yazılmayan alanlar:** değer, `Ciphertext`, `WrappedDek`, nonce'lar, etiketler, idempotency HMAC'i, temsilî kullanıcı jetonu.
- **K2:** komutlar işlem içi yolla kapalı-başarısızdır (AC-10); sınıf tartışması bu yüzden kapıyı değiştirmez (Q-5 kararı yine gerekir: X-3).
- **Platform admin aktörü:** DL-4 iptalinde aktör `platform_admin`, `IsPlatformGlobal = false`, `TargetTenantId` = kaydın kiracısı.

## 22. Record Codes

RCS-001 kapsamı dışı: kullanıcıya görünen iş kodu yok. `vaultRef` teknik `Id`'dir (Guid) ve yalnız tüketici servis tarafından saklanır;
DL-4 ekranı kodu değil kiracı + ad + sahibi modülü gösterir.

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | Çekirdek: TenantSecret kaydı, zarf şifreleme ve KEK doğrulaması, tüketici listesi, yaz / döndür / iptal iç uçları, meta sorgusu, işlem içi denetim, Gateway rotasızlık testi | Owned Objects, Entity Fields, TenantSecret, TenantSecretCommandRequest, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Şifreleme ve anahtar yönetimi, Tüketici listesi ve servis kimliği, Değer akışı ve sızıntı sınırları, Sırrın yaşam döngüsü, Hata kodları, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | Servis kimliğiyle okuma (resolve) + TENANT-SECRET-VAULT 1.0.0 sözleşmesi + okuma yolu sızıntı taraması | Değer akışı ve sızıntı sınırları, Sözleşme (TENANT-SECRET-VAULT), Hata kodları, Authorization Convention | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | KEK döndürme ve yeniden sarma işi, son kullanma, isteğe bağlı doğrulama geçiş penceresi, yedek / geri yükleme runbook'u | KEK yaşam döngüsü ve kurtarma, Sırrın yaşam döngüsü, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-4 | (isteğe bağlı, Q-7) Platform-admin meta ekranı + iptal; en / tr; yardım | Layout & Shell Contract, Frontend File Contract, Lookup & Reference Data Decision, Authorization Convention, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |
| DL-5 | Auth audience çifti (`Diten.Inventory`, `TRUSTED_SECRET_VAULT_CONSUMER`) — XMC X-1, sahibinin izniyle (DL-1'den bağımsız) | XMC-001 — başka modülün dosyalarına dokunuşlar, Dependencies | E3 | plan kimliği yok | plan satırı yok |

Plan kaynağı: plan dosyası depoda değildir ve bu FU için çağıran plan satırı aktarmadı → `plan kimliği yok` / `plan satırı yok` (değer
uydurulmadı). Hizmet ettiği teslim: MOD-0179 DL-4 (MOD-0179 paketinde "MVP-5"); bu bilgi plan satırı değildir.

## 24. Dilim kapıları ve bağlı kararlar

| Dilim | Başlamadan önce | Bitince açtığı |
|---|---|---|
| DL-1 | paket onayı; Q-1, Q-2, Q-5; MOD-0179 temsilî izin anahtarı | DL-2, DL-3, DL-4 |
| DL-2 | DL-1 | MOD-0179 DL-4 (DL-5 ile birlikte) |
| DL-3 | DL-1 | — |
| DL-4 | DL-1; Q-7 = evet; X-4 (integration-agent) | — |
| DL-5 | X-1 izni (Q-3) | Inventory'nin jeton alabilmesi |

**MOD-0179 DL-4 kapısı** = bu paketin DL-1 + DL-2 + DL-5'i tamam + `TENANT-SECRET-VAULT 1.0.0` dondurulmuş + Q-3 provisioning (dev) + Q-4 kararı
(canlı için). DL-3 ve DL-4 MOD-0179 DL-4'ü beklemez ve bekletmez.

## 25. Page Help (UXH-001)

Yalnız DL-4 açılırsa. Sayfa kodu `TENANTSECRETS`, dil en + tr; tur yok (beş tur akışından biri değil).

| Anahtar | tr | en |
|---|---|---|
| `Help.TENANTSECRETS.Purpose` | Kiracıların modüllerin kullandığı dış sistem sırlarını meta olarak görür ve gerekirse iptal edersiniz. Sır değerleri hiçbir yerde gösterilmez. | View the external-system secrets that tenant modules use, as metadata only, and revoke one when needed. Secret values are never shown. |
| `Help.TENANTSECRETS.Audience` | Platform yöneticileri | Platform administrators |
| `Help.TENANTSECRETS.Step1Title` / `Step1` | Süz / Kiracı, durum ya da sahibi modülle süzün. | Filter / Filter by tenant, state or owner module. |
| `Help.TENANTSECRETS.Step2Title` / `Step2` | İncele / Satıra tıklayınca sürüm, son değişim ve son kullanma görünür. | Inspect / Click a row to see version, last change and expiry. |
| `Help.TENANTSECRETS.Step3Title` / `Step3` | İptal et / Sırrı gerekçe yazarak iptal edin; sahibi modül yeni sır girene kadar o bağlantı çalışmaz. | Revoke / Revoke the secret with a reason; the owner module's connection stops until a new secret is set. |
