---
id: MOD-0012-FU01
name: Tenant Runtime Secret Vault
parent: MOD-0012
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: ready-for-dev
approval_status: pending
revision: "r3 (2026-11-13, dar tur) — bağımsız inceleme r2 (docs/records/reviews/MOD-0012-FU01/2026-11-13-9da835eb.md, KOŞULLU, F-1…F-4 düşük) işlendi; yalnız bu maddelerin bölümleri değişti (kapanış tablosu §19.6): F-1 resolve ucuyla ölçülen bacaklar (AC-3 (b) / (d), AC-7 resolve) DL-2 kabulüne, DL-3 DL-2 kabulünü de bekler; F-2 ITenantSecretCipher iç yüzeyi KEK'i açık parametre alır (UnwrapDek(tenantKek, wrapped, wrapContext)), S-4 (b) buna bağlı; F-3 barındırılan işin kurucusunda yalnız ISender, S-20b yansıma + LoggingBehavior izi ile ölçülür, S-10a kırmızısı teslim ve defter testlerinden, TenantSecretAuditIntent statik niyet sınıfı ve işleyici ITransactionalAuditOutboxWriter tutar (S-10c); F-4 §8.1 TenantScope atfı ölçülen satırlara. Künye ready-for-dev / pending değişmedi. r2 (2026-11-13) — bağımsız inceleme r1 (docs/records/reviews/MOD-0012-FU01/2026-11-13-754b2c86.md, KOŞULLU, F-1…F-3 orta, F-4…F-8 düşük) ve sahip kararları (kararlar 01 :55-59 — Q-6…Q-9, Q-14, Q-15) işlendi; F-1…F-8 kapandı (kapanış tablosu §19.5). Başlıca değişiklikler: bu FU MOD-0179 v1.0'ın kapısı değil, FU-0179-VAULT'un kapısıdır (F-1); admin ekranı, DL-4, AC-24…AC-26 (kimlikleri korunarak), platform.tenant-secrets.* izinleri ve X-4 v1 dışı — künye shell none / golden_reference none / form_field_count 0 (Q-7); geçiş penceresi zorunlu (Q-6, AC-22); S-2b, S-3, S-4 yalıtılmış vakalarla yeniden yazıldı (F-3); tüketici girişi MOD-0179 r3'e daraltıldı (F-4); kasaya özgü KEK doğrulayıcısı (F-5); işlem içi liste yolu ve temsilî kullanıcı şeması (F-6); kripto belirtimi ve test vektörleri (F-7); hata kodları (F-8). Künye status ready-for-dev (çağıran CT talimatı, S-49); approval_status pending. r1 (2026-11-13) — ilk taslak (commit 243841c70, LF sha256 754b2c86…)."
status_note: "ready-for-dev r2 — approval_status pending; kod izni yok. Paket onayı için: r2 fark incelemesi (CT-APR-02) + onay satırı (CT-APR-01, onaylayan Ali / GitHub GMG-ERP; yazan ≠ onaylayan; yüksek risk). Onaydan önce AÇIK: Ali — Q-1 (KEK yeri / erişimi), Q-2 (yedek / kurtarma), Q-13 (FU onayı ana paketi bekler mi); CT — Q-5 (işlem içi denetim listesi ve defter), Q-12 (domain-config Vault satırı). Dilim kapıları (paket onayını bekletmez): Q-3 Inventory servis istemcisi + X-1 izni (DL-5), Q-4 servisler arası TLS (DL-2 canlı kullanım)."
owner: platform-shared-services / merkez (Beste)
approver: Ali (GitHub GMG-ERP)
risk: "HIGH — (1) kiracı sırlarını kalıcı olarak saklayan ilk Platform bileşeni (şifreli; anahtar kaybı = tüm sırların kaybı); (2) değer servisler arası bir cevapta taşınır (resolve); (3) yeni servis kimliği audience'ı ve AuthService'te XMC (X-1); (4) güvenlik ve kiracı izolasyonu — yüksek riskli onay (CT-APR-01)."
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: TBD
form_field_count: 0
dependencies: [MOD-0012 (ISecretsProvider — KEK okuması; ana paket status review), MOD-0033-FU02 (servis kimliği jetonu — yeni audience + Inventory istemcisi, X-1, Q-3), MOD-0021 / AUD-001 (platform-islem-ici denetim yolu — Q-5), MOD-0179 (ilk tüketici; bu FU MOD-0179 v1.0'ın kapısı değil, FU-0179-VAULT'un kapısıdır — F-10)]
---

# MOD-0012-FU01 — Tenant Runtime Secret Vault (Kiracı Sır Kasası)

> **r3 (2026-11-13, dar tur).** Bağımsız inceleme r2 (`docs/records/reviews/MOD-0012-FU01/2026-11-13-9da835eb.md`, KOŞULLU, dört düşük)
> işlendi; F-1…F-4 kapandı (§19.6). Registry `:70` notu ve Q-10 karar kaydı CT işidir; bu paket onlara dokunmaz.
>
> **r2 (2026-11-13).** Bağımsız inceleme r1 (`docs/records/reviews/MOD-0012-FU01/2026-11-13-754b2c86.md`, KOŞULLU) ve sahip kararları
> (kararlar 01 :55-59) işlendi; bulgular F-1…F-8 kapandı (§19.5). Künye `status: ready-for-dev` (çağıran CT talimatı, S-49),
> `approval_status: pending`. **Paket onaylanmadan geliştirme başlamaz:** kod yalnız bağımsız inceleme kaydı (CT-APR-02) + onay satırı
> (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**) ile yazılır (AGENTS.md Çekirdek madde 7). Bu belge kod, commit, push, PR ya da onay izni değildir.
>
> **Kaynak satırı (sahip kararı).** Beste, 2026-11-13, sohbet: *"tamam ekleyelim bunu platform tarafına"* — MOD-0179 3PL kanal sırları için
> Platform'a ayrı bir MOD-0012 FU'su açılır. Aynı gün verilen F-10 kararıyla (kararlar 01 :45-47) MOD-0179 v1.0 kasayı beklemez: kanal kaydı
> yalnız sır referansı tutar, değeri işletim yerleştirir. Bu yüzden bu FU **FU-0179-VAULT'un kapısıdır (MOD-0179 v1.0 kapısı değil)** —
> MOD-0179 r3 X-14 satır 409, Q-16 satır 1032-1033, FU-0179-VAULT satır 1079; registry notu "not a gate" (`execution/registries/module-id-registry.md:70`).
> Kaynak dosya `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (bu paket o dosyaya dokunmaz).
>
> **Kısa özet.** MOD-0012 bugün yalnız **çalışma zamanı yapılandırma sırlarını okur** (ortam değişkeni / user-secrets; yazma yok). Bu FU,
> Platform'a **kiracı başına, çalışma anında yazılabilen, şifreli** bir sır kasası ekler: sır kiracı (`TenantId`) + şirket (isteğe bağlı
> `LegalEntityId`) + sahibi modül + kapsam referansı + ad ile tanımlanır; zarf şifrelemeyle (AES-256-GCM, kiracı başına türetilmiş KEK)
> saklanır; yazma / döndürme / iptal komutları yalnız sahibi modülün servis kimliği + işlemi yapan kullanıcı adına çalışır; değeri yalnız
> sahibi modülün servis kimliği okur. Değer hiçbir kullanıcı cevabında, logda ya da denetim kaydında yoktur. v1'de ekran yoktur (Q-7).

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed):**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0012-FU01 --name "Tenant Runtime Secret Vault" --parent MOD-0012` | r1 (HEAD `a933a437d`) ve r2 (HEAD `4c85c7e51`): çıkış 0 — `OK  MOD-0012-FU01: proven against Blueprint/registry.` |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "tenant secret vault"` | r1'de koşuldu: çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 1 · sor 0 · registry 1 · paket 2 · gateway 0 · görev 0`; rapor sha256 `65f29b3d295e7055fab1f71a34755014721c1334fa2e07c4f6ef6c7ef935527e`; 572 ref incelendi. r2'de yeniden koşulmadı |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "sır kasası"` | r1'de: çıkış 0 — eşleşme yok |

**FU numarası.** r1 ölçümü: registry'de MOD-0012 FU satırı yoktu, DCP üye tablolarında ayrılmış numara yoktu → FU01. Registry satırı artık
var (`execution/registries/module-id-registry.md:70`, Q-11 kapandı).

**Keşif bulgularının kararı:**
- Blueprint `MOD-0012 'Secrets & Configuration Vault'` (veri nesnesi: Secrets) — üst modül; bu paket onun FU'sudur (yeni MOD açılmaz).
- Paket `execution/domains/platform-shared-services/module-packs/MOD-0012-secrets-configuration-vault.md` — ana paket (status `review`);
  "Plaintext secret values must not be persisted in MongoDB by this MVP" der. Bu FU o kuralı korur: düz değer Mongo'ya yazılmaz.
- Paket `MDM-011-secrets-and-configuration-vault.md` — yalnız 8 eski uzak dalda, HEAD'de yok; MOD-0012'nin eski takma adı niteliğinde; mükerrer değil.

**Kısmi inceleme notu:** keşif aracı bu makinede olmayan dalları, commit edilmemiş işleri ve kayıtlı olmayan görev kaynaklarını görmez.

## 1. Module Summary

- **Amaç.** Kiracı iş modüllerinin dış sistem kimlik bilgilerini (API anahtarı, OAuth istemci sırrı, imza sırrı; ileride SFTP parolası /
  özel anahtarı) kiracı + şirket kapsamında, çalışma anında yazılabilir ve şifreli saklamak; değeri yalnız sahibi modülün servis kimliğine vermek.
- **Hedef kullanıcı.** Doğrudan son kullanıcı yok. Kullanıcı, tüketici modülün kendi ekranında sırrı **bir kez** girer; tüketici servis değeri bu
  kasaya iletir (MOD-0179'da bu akış FU-0179-VAULT'tur — r3 §8.5 satırlar 512-525). v1'de Platform tarafında ekran yoktur (Q-7).
- **Kapasite özeti (v1).** Yaz · döndür · iptal (soft) · meta oku · servis kimliğiyle değer oku · sürüm ve son kullanma · doğrulama geçiş
  penceresi (en çok 72 saat) · KEK döndürme ve yeniden sarma.
- **İlk tüketici.** MOD-0179 (3PL Integration), v1.0 API kanalı adları: `ApiKey`, `OAuthClientSecret`, `InboundSigningSecret`
  (MOD-0179 r3 `SecretRefs[]` satır 287). SFTP adları (`SftpPassword`, `SftpPrivateKey`) MOD-0179 v1.1 ile tüketici listesinin revizyonuyla
  eklenir (§8.3 ekleme kuralı); AS2 MOD-0179 kapsamından çıktı.

## 2. Ownership and Boundaries

### 2.1 Kapsam (v1)

- `TenantSecret` kaydı ve onun zarf şifrelemesi (§8.2).
- İç (servisler arası) API: yaz, döndür, iptal, meta oku, değer oku (resolve) — §15.
- Kodda sabit tüketici listesi (hangi modül, hangi servis kimliği, hangi adlar, hangi temsilî izin) — §8.3.
- KEK okuması MOD-0012'nin mevcut `ISecretsProvider`'ı üzerinden; doğrulama kasaya özgü seçenek doğrulayıcısıyla (§8.2).
- İşlem içi denetim (platform-islem-ici) — §21.
- Doğrulama geçiş penceresi (zorunlu, Q-6), son kullanma, KEK döndürme ve yeniden sarma işi (DL-3).
- Sözleşme `TENANT-SECRET-VAULT 1.0.0` (OpenAPI, yazılacak — §8.8).
- XMC: AuthService audience politikasına tek çift ekleme (X-1, DL-5, sahibinin izniyle).

### 2.2 Kapsam dışı

- **Platform-admin meta ekranı ve iptal ekranı** (sahip kararı Q-7 = hayır, 2026-11-13): r1'deki DL-4, AC-24…AC-26, `platform.tenant-secrets.*`
  izinleri, Gateway admin rotası (X-4), lookup anahtarları ve yardım metinleri v1'de yoktur; takip işi ADMIN-UI (§20).
- Dış kasa sağlayıcıları (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) — ana paket follow-up'ı; bu FU yalnız
  `IKeyEncryptionKeyProvider` dikiş yerini bırakır.
- Çalışma zamanı yapılandırma sırları (JWT, Mongo bağlantısı, iç API anahtarları, `Mfa:HashSecret`) — MOD-0012 ana paketi.
- Platform bildirimlerinin SMTP sırrının bu kasaya taşınması — ayrı FU (sahip kararı Q-15).
- Tüketici modülün kendi ekranı, kendi portu ve kendi sır referansı alanı (MOD-0179 `SecretRefs[]`, FU-0179-VAULT).
- Değeri insana gösteren her yüzey (ekran, dışa aktarma, "göster" düğmesi).
- Kiracı kapatıldığında sırların kaldırılması; yaklaşan son kullanma bildirimi (MOD-0027) — follow-up (§20).

### 2.3 Sınır kuralları

1. **Düz değer kalıcı değildir.** Değer yalnız istek işlenirken bellekte bulunur; Mongo'da, idempotency deposunda, denetim kaydında, logda,
   OTel'de, istisna mesajında ve kullanıcıya dönen hiçbir HTTP cevabında yoktur.
2. **Değeri dönen tek cevap** iç `resolve` ucudur; yalnız servis kimliği jetonuyla çağrılır, Gateway'den erişilemez, gövdesi loglanmaz,
   `Cache-Control: no-store` taşır.
3. **Kiracı jetondan gelir.** Kiracı servis jetonunun `tenant_id` iddiasından çözülür; gövdede kiracı alanı yoktur. Başka kiracının ya da
   başka tüketici modülün kaydı **404** döner (var olmayan kayıtla bayt-eşit gövde).
4. **Sahibi modül istemciden değil listeden gelir.** `OwnerModule`, jetondaki istemci kimliğinin kodda sabit listedeki girişinden çözülür.
5. **Silme yok.** İptal soft'tur (`IsDeleted`, `DeletedAt`); kayıt kalır, zarf alanları canlı belgeden kaldırılır (§8.5 — bu, yedekteki kopyayı silmez).
6. **İnsan jetonu değer okuyamaz.** `platform_admin` dahil hiçbir kullanıcı jetonu resolve ucuna geçemez (ayrı kimlik doğrulama şeması).
7. **Şirket doğrulaması çağıranındır** (sahip kararı Q-14): kasa `LegalEntityId`'nin kiracıya ait olduğunu ayrıca doğrulamaz; tüketici servis
   şirketi sunucuda çözer ve jetonun kiracısına bağlar.

## 3. Owned Objects

**Varlıklar:** `TenantSecret` (koleksiyon önerisi `platform_tenant_secrets`), `TenantSecretCommandRequest` (idempotency; öneri
`platform_tenant_secret_command_requests`).

**Değer nesneleri:** `TenantSecretCipherEnvelope` (Alg, KekId, WrappedDek, DekNonce, DekTag, ValueNonce, Ciphertext, ValueTag),
`SensitiveSecretValue` (bayt taşıyıcı; `ToString()` → `[REDACTED]`; JSON'a yazılırken istisna atar — yanlışlıkla serileştirmeyi kırar).

**Komutlar (yazma, 5):** `WriteTenantSecretCommand`, `RotateTenantSecretCommand`, `RevokeTenantSecretCommand` (DL-1);
`RewrapTenantSecretsCommand`, `PurgeExpiredPreviousSecretsCommand` (DL-3).

**Sorgular:** `GetTenantSecretMetadataQuery` (DL-1; sahip kararı Q-9 — meta sorgusu kalır), `ResolveTenantSecretQuery` (DL-2).

**Portlar / servisler:** `ITenantSecretCipher` (zarf şifreleme), `IKeyEncryptionKeyProvider` (KEK; v1 uygulaması `ISecretsProvider` üzerinden
ortam değişkeni), `TenantSecretVaultOptions` + `TenantSecretVaultOptionsValidator` (F-5), `ITenantSecretRepository`, `TenantSecretAuditIntent`
(işlem içi denetim **niyet sınıfı**: statik, DI'ya kayıtlı bir servis değildir — `RecordCodeAuditIntent` emsali; her yazma işleyicisi
`ITransactionalAuditOutboxWriter`'ı kendi kurucusunda tutar ve niyet metoduna parametre olarak verir, §10), `TrustedSecretVaultConsumerRequestExecutor`, `TrustedSecretVaultConsumerAuthorizationOptions` + doğrulayıcısı,
`TenantSecretMaintenanceJob` (DL-3 barındırılan iş; kurucusunda yalnız `ISender`, `TimeProvider`, seçenekler ve logger — §8.6).

**API uçları (Platform 5057, iç — Gateway rotası yok):**

| Metot ve yol | Kimlik | Dilim |
|---|---|---|
| `POST /api/internal/v1/tenant-secrets` | servis jetonu + `X-Delegated-Authorization` | DL-1 |
| `POST /api/internal/v1/tenant-secrets/{vaultRef}/rotate` | servis jetonu + temsilî kullanıcı | DL-1 |
| `POST /api/internal/v1/tenant-secrets/{vaultRef}/revoke` | servis jetonu + temsilî kullanıcı | DL-1 |
| `GET /api/internal/v1/tenant-secrets/{vaultRef}/metadata` | servis jetonu | DL-1 |
| `POST /api/internal/v1/tenant-secrets/{vaultRef}/resolve` | yalnız servis jetonu | DL-2 |

**İzinler:** yeni `[HasPermission]` anahtarı yok (iç uçlar servis şemasıyla korunur, §14). Yazmada temsilî kullanıcıdan tüketicinin kendi izni
aranır (v1: `warehouse.third-party-logistics.channels.configure`).

**Frontend rotası:** yok (Q-7).

## 4. Entity Fields

Platform'da kiracı kapsamlı kayıt `BaseEntity`'den türer (`services/Diten.Platform/src/Diten.Platform.Domain/Common/BaseEntity.cs:5`);
`TenantId`, `IsDeleted`, `DeletedAt`, `Version` varlıkta açıkça tanımlanır (`TenantMessagingSettings` emsali). İş sürümü `Version` adını
kullanmaz (entity-base-template): adı `SecretVersion`'dır; `Version` yalnız eşzamanlılık içindir.

### 4.1 TenantSecret

| Alan | Tip | Kural |
|---|---|---|
| `Id` | string (Guid, `D` biçimi, küçük harf) | sunucu üretir; dışarıya **`vaultRef`** olarak verilir (opak; tüketici yalnız bunu saklar) |
| `TenantId` | Guid | zorunlu; servis jetonunun `tenant_id`'sinden; DTO / istek gövdesinde yok |
| `LegalEntityId` | Guid? | tüketici listesindeki girişe göre zorunlu ya da yasak (MOD-0179: zorunlu); kasada ayrıca doğrulanmaz (Q-14 kararı) |
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
| `PreviousCipher`, `PreviousSecretVersion`, `PreviousValidUntil` | zarf? / int? / DateTime? | yalnız geçiş penceresi verilen döndürmede (§8.5); pencere bitince `null` |
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
| `FingerprintKekId` | string | parmak izini üreten `K_idem`'in türetildiği KEK (§8.2) |
| `RequestFingerprint` | string | SHA-256(kanonik meta alanları + **HMAC-SHA256(K_idem, değer)**); düz değer ya da düz değer özeti yok |
| `ResponseStatus`, `ResponseBody` | int / string | yalnız meta cevap (değer içermez) |
| `CreatedAt` | DateTime | TTL 24 saat |

### 4.3 Indexes

| Koleksiyon | İndeks | Gerekçe |
|---|---|---|
| `platform_tenant_secrets` | tekil kısmi `{TenantId, OwnerModule, LegalEntityId, ScopeRef, Name}` — `IsDeleted: false` | aynı anahtarda tek etkin sır |
| `platform_tenant_secrets` | `{TenantId, OwnerModule, State}` | meta okumaları |
| `platform_tenant_secrets` | `{Cipher.KekId}` (seyrek) | yeniden sarma işi eski KEK'li kayıtları bulur |
| `platform_tenant_secrets` | `{PreviousValidUntil}` (seyrek) | geçiş penceresi temizliği |
| `platform_tenant_secret_command_requests` | tekil `{TenantId, ClientId, IdempotencyKey}` + TTL `{CreatedAt}` 24 saat | idempotency |

## 5. Repo Scope

| Yol | İçerik | Dilim |
|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.Domain/Entities/TenantSecrets/` | `TenantSecret.cs`, `TenantSecretCipherEnvelope.cs`, `TenantSecretCommandRequest.cs` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/` | `ITenantSecretRepository.cs` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantSecrets/` | §10'daki CQRS ağacı, `TenantSecretAuditIntent.cs`, `TenantSecretErrorCodes.cs`, `Ports/` | DL-1…DL-3 |
| `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditCommands.cs` | `Authorized` kümesine bu paketin beş komutu (X-3, CT kararı Q-5) | DL-1, DL-3 |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/TenantSecrets/` | `AesGcmTenantSecretCipher.cs`, `EnvironmentKeyEncryptionKeyProvider.cs`, `TenantSecretVaultOptions.cs`, `TenantSecretVaultOptionsValidator.cs`, `TenantSecretMaintenanceJob.cs` (DL-3) | DL-1, DL-3 |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/` | `TenantSecretRepository.cs`, indeks kaydı | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` | kayıtlar + `ValidateOnStart` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/` | `InternalTenantSecretsController.cs` | DL-1, DL-2 |
| `services/Diten.Platform/src/Diten.Platform.API/Security/` | `TrustedSecretVaultConsumerRequestExecutor.cs`, `TrustedSecretVaultConsumerAuthorizationOptionsValidator.cs`; `TrustedServiceTokenValidationExtensions.cs`'e yeni servis şeması + audience sabiti (`TrustedSecretVaultConsumerService`, `TRUSTED_SECRET_VAULT_CONSUMER`). Temsilî kullanıcı için **yeni şema yok**: mevcut `TrustedWorkflowDelegatedUser` şeması (aynı dosya satır 20 ve satırlar 153-175 — `X-Delegated-Authorization` başlığından insan JWT'si) yeniden kullanılır | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Configuration/` | `TrustedSecretVaultConsumerAuthorizationOptions.cs` | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.API/Program.cs` | şema kaydı (aynı dosyada başka modüllerin satırlarına dokunulmaz) | DL-1 |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantSecrets/` | birim, test vektörü, HTTP + Mongo, sızıntı taraması testleri; `TenantSecretCipherVectors.json` | DL-1…DL-3 |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TransactionOwnedAuditCommandGuardTests.cs` | yalnız bu paketin komutlarının eklenmesinin gerektirdiği beklenti (liste = işaretli komutlar) | DL-1, DL-3 |
| `gateway/Diten.ApiGateway.Tests/` | `Mod0012Fu01InternalSecretVaultRouteTests.cs` (yalnız test; `ocelot.json` değişmez) | DL-1 |
| `docs/analysis/contracts/tenant-secret-vault.openapi.yaml` | sözleşme (yazılacak) | DL-2 |
| `docs/guides/operations/tenant-secret-vault-runbook.md` | KEK döndürme, yedekleme, kurtarma runbook'u (yazılacak) | DL-3 |
| `execution/domains/platform-shared-services/module-packs/MOD-0012-FU01-tenant-runtime-secret-vault.md` | bu paket | — |

### 5.1 XMC-001 — başka modülün dosyalarına dokunuşlar

Her satır, sahibinin açık izni olmadan dokunulmaz (XMC-001). İzin bu pakete ve sahibin modül paketine yazılır.

| # | Dosya / alan | Bugün (ölçüm) | Gereken değişiklik | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | `services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17` | Auth yalnız `Diten.MDM` servisine ve üç audience'a jeton verir (`IsAllowedPair`) | yalnız bir çift eklenir: (`Diten.Inventory`, `TRUSTED_SECRET_VAULT_CONSUMER`); mevcut çiftler değişmez; testi aynı commit | MOD-0033-FU02 (Ali — Q-3) | DL-5 |
| X-2 | `tests/architecture/audit-ledger/Diten.Platform.md:16` | platform-islem-ici izinin belirteç listesi | `TenantSecretAuditIntent.EnqueueAsync/TenantSecretAuditIntent.EnqueueRefusalAsync` eklenir + ölçüm notu (MOD-0040-FU02 notu emsali, satır 30) | CT (AUD-001 defteri — Q-5) | DL-1 |
| X-3 | `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditCommands.cs:22-57` (`Authorized`) + `TransactionOwnedAuditCommandGuardTests` | işlem içi komutlar bu sabit kümeyle kabul edilir; test kümeyi işaretli komutlara eşit tutar (AUD-001 §4.4) | DL-1'in üç komutu ve DL-3'ün iki komutu kümeye eklenir | CT kararı (Q-5) | DL-1, DL-3 |
| X-4 | (r1: `ocelot.json` admin rotası) | — | **r2: v1 dışı** (Q-7 — admin ekranı yok); takip işi ADMIN-UI | — | — |
| X-5 | Inventory tarafı (değer kabul eden kanal sırrı yazımı, `IThirdPartySecretResolver`'ın kasa bağdaştırıcısı, servis jetonu sağlayıcısı) | MOD-0179 r3: yalnız referans + işletim yerleştirir | FU-0179-VAULT'un kendi işi; bu paket yalnız sözleşmeyi verir | MOD-0179 (merkez) | — |

## 6. Protected Paths

- `.antigravity/**` (bu paket kural dosyası değiştirmez).
- `gateway/Diten.ApiGateway/**/ocelot.json` (bu paket hiçbir rota eklemez; AC-14 sabotajı yalnız geçici çalışma ağacında).
- `services/Diten.Building.Blocks/**` (MOD-0012 ana paketi; v1 değişiklik gerektirmez — KEK doğrulaması bu yüzden Platform'dadır, F-5).
- `services/Diten.AuthService/**` (X-1 dışında).
- `services/Diten.InventoryService/**`, `services/Diten.MdmService/**` ve diğer domain servisleri.
- `frontend/Diten.Web/**` (v1'de ekran yok).
- MOD-0012 ana paketinin çalışma zamanı sırları ve `appsettings*.json` içindeki mevcut anahtarlar.
- `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (karar kaydını CT yazar).

## 7. Dependencies

| Bağımlılık | Tür | Bugün | Kullanım | Kapı | Sahip |
|---|---|---|---|---|---|
| MOD-0012 `ISecretsProvider` | HARD | var (yalnız okuma) | KEK okuması | — | merkez |
| MOD-0033-FU02 servis kimliği | HARD — DL-5 ve DL-2 canlı kullanım | Platform'da adlı RS256 doğrulayıcı var; Auth yalnız `Diten.MDM`'e jeton veriyor | yeni audience; Inventory istemcisi | X-1, Q-3 | Ali |
| AUD-001 platform-islem-ici | HARD — DL-1 | var (MOD-0040-FU02 emsali) | işlem içi denetim | Q-5, X-2, X-3 | CT |
| MongoDB işlemleri (replica set) | HARD | Platform'da kullanılıyor | sır + denetim + idempotency tek işlemde | — | — |
| Servisler arası TLS | DL-2 canlıya çıkış | dev'de düz HTTP | resolve cevabı ağda | Q-4 | Ali |
| MOD-0179 | tüketici | r3 ready-for-dev / pending | ilk tüketici girişi (§8.3); bu FU onun v1.0 kapısı değildir | — | merkez |

**Denetim altyapısı bugün var mı?** Evet — `tests/architecture/audit-ledger/Diten.Platform.md` `## İzler` tablosunda kabul edilmiş
`platform-islem-ici` (yol a) izi var; bu paket yeni bir iz açmaz, o izin belirtecini genişletir (X-2).

## 8. Runtime Constraints

### 8.1 Servis ve kapsam

- Servis `Diten.Platform` (port 5057). İç uçlar yalnız servisler arasıdır; Web (5001) bu uçları çağırmaz.
- Her okuma ve yazma `TenantId` ile sınırlıdır; kiracı `TenantScope` jetondaki `tenant_id` ile açılır (trusted executor emsali,
  `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowConsumerRequestExecutor.cs:130` — yalnız servis jetonlu yol (resolve,
  meta); `:208` — temsilî kullanıcılı yol (yaz, döndür, iptal)).
- Soft delete zorunlu; fiziksel silme yok.
- `GlobalEntity` kullanılmaz: kayıt kiracıya aittir.

### 8.2 Şifreleme ve anahtar yönetimi

- **Zarf şifreleme.** Her kayıt (ve her döndürme) için rastgele 256 bit DEK; değer AES-256-GCM ile DEK altında şifrelenir (rastgele 96 bit
  nonce, 128 bit etiket). DEK, kiracı KEK'i altında AES-256-GCM ile sarılır (ayrı rastgele 96 bit nonce). .NET 8
  `System.Security.Cryptography.AesGcm` ve `HKDF`; yeni paket bağımlılığı yok.
- **Kiracı KEK'i.** `KEK_tenant = HKDF-SHA256(IKM = ana KEK (32 bayt), salt = TenantId, info = "diten:tenant-secret-vault:kek:1", L = 32)`.
- **İdempotency anahtarı.** `K_idem = HKDF-SHA256(IKM = ana KEK, salt = TenantId, info = "diten:tenant-secret-vault:idem:1", L = 32)` —
  kiracıya bağlıdır; hangi KEK'ten türetildiği `FingerprintKekId`'de saklanır.
- **Kanonik baytlar (F-7 (b); test vektörüyle sabitlenir, AC-3a).**
  - `TenantId` tuzu: 16 bayt, RFC 4122 ağ sırası (`Guid.TryWriteBytes(span, bigEndian: true, out _)`), `Guid.ToByteArray()`'in karışık sırası değil.
  - AAD alanları: her alan `4 bayt büyük-uçlu uzunluk + UTF-8 baytlar`; Guid'ler `D` biçiminde küçük harf; `SecretVersion` ondalık dizge;
    `LegalEntityId = null` uzunluk 0 olarak kodlanır (36 karakterlik bir Guid'le çakışamaz). Öne `"A256GCM-ENV-1"` sürüm etiketi eklenir.
  - Değer AAD'si: `Alg | Id | TenantId | LegalEntityId | OwnerModule | ScopeRef | Name | SecretVersion`.
  - Sarma AAD'si: `Alg | Id | TenantId | KekId`.
  - Test vektörü dosyası `TenantSecretCipherVectors.json`: sabit ana KEK, kiracı, DEK, nonce'lar, alanlar → beklenen türetilmiş KEK, AAD
    baytları, sarılı DEK, şifreli metin, etiketler (bilinen cevap testi).
- **Ana KEK yeri (v1 önerisi, Q-1 — Ali).** Platform ana makinesinde ortam değişkeni; MOD-0012 `ISecretsProvider` ile okunur (üretimde yalnız
  ortam değişkeni — `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/ConfigurationSecretsProvider.cs:51-55`):
  - `TenantSecretVault:Kek:CurrentId` → `TenantSecretVault__Kek__CurrentId` (1..32, `[a-z0-9-]`)
  - `TenantSecretVault:Kek:Current` → `TenantSecretVault__Kek__Current` (base64, çözüldüğünde tam 32 bayt)
  - `TenantSecretVault:Kek:Previous:{id}` → `TenantSecretVault__Kek__Previous__{id}` (döndürme sırasında)
  - Anahtar adlarında alt çizgi yoktur (bkz. §19.2 madde 4).
- **Başlangıç doğrulaması (F-5).** MOD-0012'nin `SecretRequirementValidator`'ı uzunluğu karakterle sayar ve "önceki ≠ şimdiki" denetimini
  yalnız JWT koleksiyonu için yapar (`services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/SecretRequirementValidator.cs:88-101`);
  Building.Blocks korunan yol olduğundan doğrulama Platform'da, kasaya özgü `TenantSecretVaultOptionsValidator` (`IValidateOptions`,
  `ValidateOnStart`) ile yapılır: `CurrentId` biçimi; `Current` geçerli base64 ve çözülmüş uzunluğu tam 32 bayt; bilinen yer tutucu değil
  (`change-me` vb., MOD-0012 listesi) ve 32 sıfır bayt değil; her `Previous` aynı kurallara uyar; `Previous` kimlikleri tekil ve `CurrentId`'den
  farklı; hiçbir `Previous` çözülmüş baytları `Current`'a eşit değil. Uymazsa Platform başlamaz; hata metni anahtar adını ve kuralı söyler,
  değeri söylemez. Tüketici listesi boş değilse KEK zorunludur.
- **Düz değer bellekte.** `SensitiveSecretValue` baytları kullanım sonrası `CryptographicOperations.ZeroMemory` ile sıfırlanır (en iyi çaba;
  .NET string'e dönüşen değer için garanti yok — resolve cevabının yazımı dahil).

### 8.3 Tüketici listesi ve servis kimliği

- **Servis kimliği.** Yeni JWT şeması `TrustedSecretVaultConsumerService`, audience `TRUSTED_SECRET_VAULT_CONSUMER`; doğrulama mevcut adlı
  RS256 doğrulayıcının aynısıdır (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs:16-45`;
  zorunlu iddialar `iss`, `aud`, `sub`, `actor_type`, `service_name`, `tenant_id`, `jti`, `iat`, `nbf`, `exp`).
- **Temsilî kullanıcı (yalnız yazma uçları).** Mevcut `TrustedWorkflowDelegatedUser` şeması yeniden kullanılır (yeni şema yok): kullanıcının JWT'si
  `X-Delegated-Authorization` başlığında; executor `AuthenticateAsync(WorkflowDelegatedUserAuthenticationScheme)` çağırır. Kullanıcı kiracısı =
  jeton kiracısı (aksi 403 `SECRET_DELEGATED_TENANT_MISMATCH`); kullanıcı girişteki temsilî izni taşır (`PermissionClaimEvaluator`, workflow
  emsali — `TrustedWorkflowConsumerRequestExecutor.cs:294`).
- **Kodda sabit liste (closed set).** `TrustedSecretVaultConsumerAuthorizationOptionsValidator.AllowedConsumers` — yapılandırmadaki her giriş bu
  kümeye uymazsa Platform başlamaz (`ValidateOnStart`; `TrustedDocumentRepositoryConsumerAuthorizationOptionsValidator` emsali). Giriş:
  `ConsumerModule`, `ServiceName`, `ClientId` (yapılandırmadan, Auth istemcisi), `AllowedNames`, `LegalEntity` (`Required` \| `Forbidden`),
  `DelegatedPermission`, `VerificationGraceNames`. En çok 16 giriş; aynı modül ya da aynı istemci iki kez yok.
- **v1 kümesi (tek giriş, F-4):**

  | Alan | Değer |
  |---|---|
  | `ConsumerModule` | `MOD-0179` |
  | `ServiceName` | `Diten.Inventory` |
  | `AllowedNames` | `ApiKey`, `OAuthClientSecret`, `InboundSigningSecret` (MOD-0179 v1.0 yalnız API kanalı — r3 satır 287) |
  | `LegalEntity` | `Required` |
  | `DelegatedPermission` | `warehouse.third-party-logistics.channels.configure` (MOD-0179 r3 izin satırı 758) |
  | `VerificationGraceNames` | `InboundSigningSecret` (Q-6) |

- **Listeye ekleme kuralı.** Yeni tüketici ya da yeni ad (ör. MOD-0179 v1.1'in `SftpPassword`, `SftpPrivateKey` adları) = bu paketin revizyonu +
  inceleme + onay satırı + koddaki küme (+ yeni tüketicide Auth istemcisi); yalnız yapılandırma yazmak hiçbir zaman yetmez.

### 8.4 Değer akışı ve sızıntı sınırları

- Yazma: kullanıcı → tüketicinin Web formu → tüketici servisi → `POST /api/internal/v1/tenant-secrets` (gövdede değer) → şifreleme → Mongo.
  Cevap: `{ vaultRef, secretVersion, setAt, expiresAt }`.
- Okuma: tüketici işçisi → `POST …/{vaultRef}/resolve` → `{ value, secretVersion, expiresAt }` (+ `use = Verify` ve pencere içindeyse
  `previous`). Tüketici değeri kalıcı saklamaz; bellekte en çok 300 saniye önbelleğe alabilir (sözleşmede yazılır).
- Değerin bulunamayacağı yerler (AC-11, AC-18 taraması): HTTP cevapları (resolve hariç), uygulama logları (tüm seviyeler), OTel span
  öznitelikleri, `audit_outbox` / `audit_events`, idempotency deposu, Mongo'daki tüm koleksiyonlar (ham BSON bayt taraması), istisna mesajları,
  doğrulama hata mesajları (`{PropertyValue}` yok).
- Komut kayıtları (`record`) değer alanını `SensitiveSecretValue` olarak taşır ve `PrintMembers`'ı geçersiz kılar: `LoggingBehavior` bugün yalnız
  istek adını loglar (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/LoggingBehavior.cs:22-37`), ama istisna loglanır —
  istisna metni değer içermez.

### 8.5 Sırrın yaşam döngüsü

- **Yaz.** Anahtar (`TenantId, OwnerModule, LegalEntityId, ScopeRef, Name`) için etkin kayıt yoksa `SecretVersion = 1`. Etkin kayıt varsa
  409 `SECRET_ALREADY_EXISTS` (tüketici rotate çağırır). İptal edilmiş kaydın anahtarına yeniden yazmak yeni kayıt açar (yeni `vaultRef`).
- **Döndür.** `expectedSecretVersion` zorunlu; uyuşmazlık 409 `SECRET_VERSION_CONFLICT`. Yeni DEK + yeni şifreli metin; `SecretVersion + 1`.
  Önceki zarf **aynı yazımda canlı belgeden kaldırılır** — ad `VerificationGraceNames` içindeyse ve `previousValidForHours` > 0 verildiyse önceki
  zarf `PreviousValidUntil`'e (en çok 72 saat, sahip kararı Q-6) kadar `PreviousCipher`'da tutulur.
- **İptal (soft).** Gerekçe 10..500; `State = Revoked`, `IsDeleted = true`, `DeletedAt`, `RevokedAt/By/Reason`; `Cipher` ve `PreviousCipher`
  canlı belgeden kaldırılır. Kayıt meta olarak kalır.
- **Kaldırma ne demektir (F-7 (a)).** Canlı belgeden kaldırılan zarf, Mongo yedeklerinde ve oplog penceresinde durur ve **o KEK canlıyken**
  açılabilir. Değerin tümden okunamaz olması ancak o KEK emekliye ayrıldığında (§8.6 yeniden sarma + `Previous` kaldırma) ve o KEK'i taşıyan
  yedekler saklama süresini doldurduğunda tamamlanır. Bu paket "kripto-imha" demez; runbook bu notu taşır.
- **Son kullanma.** `ExpiresAt` geçtiyse resolve 409 `SECRET_EXPIRED` (değer yok); meta `state: Expired`. Saat `TimeProvider`'dan gelir.
- **Geçiş penceresi sonu.** `PreviousValidUntil` geçtiyse resolve `previous` döndürmez (okuma anında denetim); `TenantSecretMaintenanceJob`
  `PurgeExpiredPreviousSecretsCommand` ile önceki zarfı canlı belgeden kaldırır.

### 8.6 KEK yaşam döngüsü ve kurtarma

- **Döndürme.** Yeni KEK `Current`, eski KEK `Previous:{id}` olarak verilir; Platform iki KEK ile açar. `TenantSecretMaintenanceJob`
  (barındırılan iş, `Diten.Platform.MOD-0012-FU01.TenantSecretMaintenanceJob`) `RewrapTenantSecretsCommand`'ı **`ISender.Send` ile** gönderir
  (F-6: komut pipeline'dan, `ITransactionOwnedAuditCommand` denetiminden geçer; aktör `System`). İşin kurucusunda yalnız `ISender`,
  `TimeProvider`, `IOptions<TenantSecretVaultOptions>` ve `ILogger` bulunur; hiçbir `IRequestHandler<,>` uygulaması, işleyici türü ya da
  `ITenantSecretRepository` bulunmaz (r3, F-3 — AC-20 S-20b bunu ölçer). Komut eski `KekId`'li kayıtların **yalnız DEK'ini**
  yeni kiracı KEK'iyle yeniden sarar; şifreli metin, `SecretVersion` ve `SetAt` değişmez; 100'lük toplu işlerde, kiracı başına bir denetim satırı (§21).
  Kalan eski KEK'li kayıt sayısı log ve sağlık bilgisinde raporlanır.
- **`Previous` ne zaman kaldırılır.** Kalan eski KEK'li kayıt sıfır **ve** son yeniden sarmadan sonra en az 24 saat geçti (idempotency TTL'i — eski
  `K_idem` ile üretilmiş parmak izleri bu süre içinde yeniden hesaplanabilsin, §8.7 `IDEMPOTENCY_KEY_REUSED` yanlış üretilmesin).
- **Bilinmeyen KekId.** `Previous` erken kaldırılırsa o kayıtlar için resolve 503 `SECRET_KEY_UNAVAILABLE` (değer yok; hata logunda kayıt kimliği +
  KekId). Idempotency tekrarında `FingerprintKekId` bilinmiyorsa da 503 `SECRET_KEY_UNAVAILABLE` (yanlış 409 değil).
- **Yedekleme ve kurtarma (Q-2 — Ali).** Mongo yedeği KEK olmadan açılamaz (istenen davranış). KEK yedeği Mongo yedeğinden **ayrı** saklanır.
  KEK kaybında kurtarma: sırlar tüketici ekranından yeniden girilir. Runbook (`docs/guides/operations/tenant-secret-vault-runbook.md`, yazılacak)
  döndürme, `Previous` kaldırma koşulu, yedek / geri yükleme tatbikatı ve §8.5'teki "kaldırma" notunu yazar.

### 8.7 Hata kodları

| HTTP | Kod | Ne zaman |
|---|---|---|
| 400 | `SECRET_VALIDATION_FAILED` | alan kuralı (§12); mesajda alan adı var, değer yok |
| 401 | — (gövdesiz) | jeton yok / imza ya da süre geçersiz / yanlış audience / insan jetonu (kimlik doğrulanamadı) |
| 403 | `SECRET_CONSUMER_NOT_ALLOWED` | geçerli servis jetonu, ama servis adı ya da istemci tüketici listesinde yok |
| 403 | `SECRET_DELEGATED_PERMISSION_MISSING`, `SECRET_DELEGATED_TENANT_MISMATCH` | temsilî kullanıcıda girişteki izin yok; temsilî kullanıcı başka kiracıda |
| 404 | `SECRET_NOT_FOUND` | kayıt yok · başka kiracı · başka tüketici modül (gövde bayt-eşit) |
| 409 | `SECRET_ALREADY_EXISTS`, `SECRET_VERSION_CONFLICT`, `SECRET_REVOKED`, `SECRET_EXPIRED`, `IDEMPOTENCY_KEY_REUSED` | §8.5, §4.2 |
| 422 | `SECRET_NAME_NOT_ALLOWED`, `SECRET_LEGAL_ENTITY_REQUIRED`, `SECRET_LEGAL_ENTITY_FORBIDDEN`, `SECRET_GRACE_NOT_ALLOWED` | ad, şirket ya da geçiş penceresi kuralı tüketici girişine uymaz |
| 500 | `SECRET_INTEGRITY_FAILED` | `KekId` biliniyor ama GCM etiketi tutmadı (kurcalama, transplant, aynı kimlikte farklı KEK); hata logu + kayıt kimliği |
| 503 | `SECRET_KEY_UNAVAILABLE` | `KekId` yapılandırmada yok |
| 503 | `SECRET_AUDIT_UNAVAILABLE` | işlem içi denetim yazılamadı (komut başarısız, kayıt değişmez) |

FU-0179-VAULT bu kodları MOD-0179'un kendi kodlarına eşler (MOD-0179 tarafının işi).

### 8.8 Sözleşme (TENANT-SECRET-VAULT)

- `TENANT-SECRET-VAULT 1.0.0` — `docs/analysis/contracts/tenant-secret-vault.openapi.yaml` (yazılacak, DL-2 ilk adımı): beş iç uç, istek /
  cevap şemaları, hata kodları, `Idempotency-Key` ve `X-Delegated-Authorization` başlıkları, tüketici önbellek üst sınırı (300 s).
- Sözleşme FU-0179-VAULT başlamadan dondurulur (ayrı CT adımı); dondurulduktan sonra kırıcı değişiklik yeni ana sürümdür.

### 8.9 Değer üst sınırı

- 1..16384 bayt (UTF-8). Gerekçe bu paketin kendisidir: v1 adları (API anahtarı, OAuth sırrı, imza sırrı) birkaç yüz baytı geçmez; ileride
  eklenecek PEM biçimli özel anahtar ve sertifika zincirleri (4096 bit RSA özel anahtarı PEM ≈ 3,3 KB, birkaç sertifikalık zincir) için pay
  bırakılır; Mongo belge sınırının çok altında kalır. Üst sınırın değişmesi sözleşme sürümüdür.

## 9. Layout & Shell Contract

- `shell: none` — v1 yalnız arka uçtur; Razor sayfası, layout ve view klasörü yoktur (sahip kararı Q-7). `golden_reference: none`,
  `form_field_count: 0`, `data_mode` yok (DataTable yok).
- Platform ekranı ileride açılırsa (takip işi ADMIN-UI) o paket `shell: platform-admin`, `_LayoutPlatformAdmin`, 2 dil (en, tr) ile yazılır.

## 10. Backend File Convention

Golden Reference klasör ve ad kuralı (DEV-0000 / DEV-0001):

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/TenantSecrets/
├── Commands/
│   ├── WriteTenantSecretCommand.cs            (sealed record)
│   ├── RotateTenantSecretCommand.cs
│   ├── RevokeTenantSecretCommand.cs
│   ├── RewrapTenantSecretsCommand.cs          (DL-3)
│   └── PurgeExpiredPreviousSecretsCommand.cs  (DL-3)
├── Queries/
│   ├── GetTenantSecretMetadataQuery.cs
│   └── ResolveTenantSecretQuery.cs            (DL-2)
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── WriteTenantSecretHandler.cs
│   │   ├── RotateTenantSecretHandler.cs
│   │   ├── RevokeTenantSecretHandler.cs
│   │   ├── RewrapTenantSecretsHandler.cs
│   │   └── PurgeExpiredPreviousSecretsHandler.cs
│   └── QueryHandlers/
│       ├── GetTenantSecretMetadataHandler.cs
│       └── ResolveTenantSecretHandler.cs
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
- Handler şifreleme yapmaz; `ITenantSecretCipher` portunu çağırır (kripto Infrastructure'dadır).
- `AesGcmTenantSecretCipher` test edilebilir iç yüzeyler sunar (`internal`, test projesine `InternalsVisibleTo`); KEK hiçbir iç yüzeyde
  bağlamdan türetilmez, **açık parametredir** (r3, F-2):
  - `byte[] DeriveTenantKek(ReadOnlySpan<byte> masterKek, Guid tenantId)`
  - `WrappedDek WrapDek(ReadOnlySpan<byte> tenantKek, ReadOnlySpan<byte> dek, WrapContext wrapContext)`
  - `byte[] UnwrapDek(ReadOnlySpan<byte> tenantKek, WrappedDek wrapped, WrapContext wrapContext)` — etiket tutmazsa `CryptographicException`
  - `ValueCipher EncryptValue(ReadOnlySpan<byte> dek, ReadOnlySpan<byte> plaintext, ValueContext valueContext)`
  - `byte[] DecryptValue(ReadOnlySpan<byte> dek, ValueCipher cipher, ValueContext valueContext)`
  Port metotları (`Seal` / `Open`) bunları birleştirir: KEK'i `IKeyEncryptionKeyProvider` + `DeriveTenantKek` ile kaydın kiracısından bulur.
  Testler yalnızca bu üretim sınıfını çağırır (§17).
- Cevaplar `Response<T>` zarfıyla; iç uçların hata gövdesi de aynı zarf.
- Beş yazma komutu `ITransactionOwnedAuditCommand` taşır, `TransactionOwnedAuditCommands.Authorized` kümesindedir (X-3) ve
  `TenantSecretAuditIntent` üzerinden işlem içi denetim yazar (Q-5). `TenantSecretAuditIntent` statik niyet sınıfıdır (`RecordCodeAuditIntent`
  emsali: `EnqueueAsync(ITransactionalAuditOutboxWriter writer, IPlatformTransactionSession session, …)`); her işleyici
  `ITransactionalAuditOutboxWriter`'ı **kendi kurucusunda** tutar — `TransactionOwnedAuditCommandGuardTests` işaretli komutun işleyicisinde bu
  kapı türünü arar (niyet sınıfını servis olarak enjekte etmek guard testini kırmızı yapar).
- DL-3 komutları barındırılan işten `ISender.Send` ile gönderilir (pipeline atlanmaz); işin kurucusunda işleyici türü yok (§8.6).

## 11. Frontend File Contract

v1'de frontend dosyası yoktur (sahip kararı Q-7). `frontend/Diten.Web/**` korunan yol listesindedir (§6). Tüketicinin "Ayarla / Değiştir"
formu tüketici modülün dosyasıdır (FU-0179-VAULT).

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB düzeyi | Ön kontrol |
|---|---|---|---|---|
| `scopeRef` | Evet (yaz) | trim yok, 1..128, `[A-Za-z0-9._:-]` | tekil anahtarın parçası | — |
| `name` | Evet (yaz) | tüketici girişinin `AllowedNames` kümesinde, harf duyarlı | tekil anahtarın parçası | 422 `SECRET_NAME_NOT_ALLOWED` |
| `legalEntityId` | girişe göre | uuid; `Required` girişte zorunlu, `Forbidden` girişte yasak | tekil anahtarın parçası | 422 |
| `value` | Evet (yaz, döndür) | UTF-8, 1..16384 bayt (§8.9); trim edilmez, olduğu gibi saklanır; doğrulama mesajında değer yok | — | — |
| `expiresAt` | Hayır | UTC; `now + 1 dk` < değer ≤ `now + 5 yıl` | — | — |
| `expectedSecretVersion` | Evet (döndür, iptal) | int ≥ 1 | `Version` eşzamanlılık | 409 `SECRET_VERSION_CONFLICT` |
| `reason` | Evet (iptal) | trim, 10..500 | — | — |
| `previousValidForHours` | Hayır (döndür) | 0..72; > 0 yalnız `VerificationGraceNames` içindeki ad için, aksi 422 `SECRET_GRACE_NOT_ALLOWED` | — | — |
| `use` | Hayır (resolve) | `Use` (varsayılan) \| `Verify`; `previous` yalnız `Verify` + pencere içinde döner | — | — |
| `Idempotency-Key` başlığı | Evet (yaz, döndür, iptal) | 8..128, `[A-Za-z0-9-_]` | tekil `{TenantId, ClientId, Key}` | 409 `IDEMPOTENCY_KEY_REUSED` |
| `X-Delegated-Authorization` | Evet (yaz, döndür, iptal) | geçerli kullanıcı JWT'si, aynı kiracı, girişteki temsilî izin | — | 403 |

İlişkili kural: aynı anahtarda etkin kayıt varsa yaz 409; `Revoked` kayıtta döndür 409 `SECRET_REVOKED`.

## 13. Failure Path to Verify

- **Mükerrer yazma** — Expected: 409 `SECRET_ALREADY_EXISTS`; ikinci kayıt yok; denetimde ret satırı (`Outcome = Failed`).
- **Eksik değer / boş ad** — Expected: 400 `SECRET_VALIDATION_FAILED`; kayıt yok; mesajda değer yok.
- **Kimliği doğrulanamayan çağrı** (jeton yok, yanlış audience, insan jetonu) — Expected: 401 gövdesiz; sıfır yazım; denetim satırı yok, log var.
- **Liste dışı servis** (geçerli servis jetonu) — Expected: 403 `SECRET_CONSUMER_NOT_ALLOWED`; sıfır yazım; ret denetimi.
- **Temsilî kullanıcı başka kiracıda / izni yok** — Expected: 403; sıfır yazım; ret denetimi.
- **Eşzamanlılık** — iki döndürme aynı `expectedSecretVersion` ile — Expected: biri 200, diğeri 409 `SECRET_VERSION_CONFLICT`; sessiz üzerine yazma yok.
- **Kiracılar arası** — B jetonuyla A'nın `vaultRef`'i (meta, resolve, rotate, revoke) — Expected: 404, gövde var olmayan kayıtla bayt-eşit.
- **Başka tüketici modül** aynı kiracıda (yabancı `OwnerModule`'lü belge testte doğrudan Mongo'ya tohumlanır) — Expected: 404 (aynı gövde).
- **Şifreli metin transplantı / meta kurcalaması** — Expected: 500 `SECRET_INTEGRITY_FAILED`; değer yok; hata logu.
- **Denetim yazılamıyor** (outbox hatası enjekte) — Expected: 503 `SECRET_AUDIT_UNAVAILABLE`; sır kaydı değişmez (işlem geri alınır).
- **KEK yok / zayıf / yanlış uzunluk** — Expected: Platform başlamaz; mesajda anahtar adı ve kural, değer yok.
- **Bilinmeyen KekId** — Expected: 503 `SECRET_KEY_UNAVAILABLE`; değer yok.
- **Süresi geçmiş / iptal edilmiş sır** — Expected: 409 `SECRET_EXPIRED` / `SECRET_REVOKED`; değer yok.
- **Aynı idempotency anahtarı, farklı değer** — Expected: 409 `IDEMPOTENCY_KEY_REUSED`; ilk kayıt değişmez. KEK döndürmesinden sonra aynı değerle
  tekrar — Expected: ilk cevap (yanlış 409 yok).

## 14. Authorization Convention

```text
İç uçlar (DL-1, DL-2):
  [Authorize(AuthenticationSchemes = TrustedServiceTokenValidationExtensions.SecretVaultAuthenticationScheme)]
  — [HasPermission] yok: insan izni bu uçların kimliği değildir (emsal InternalTrustedWorkflowConsumerController,
    services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedWorkflowConsumerController.cs:14-16).
  Yetki = kodda sabit tüketici listesi (servis adı + istemci + ad) + yazmada temsilî kullanıcının, girişte adı yazılı izni
  (v1: warehouse.third-party-logistics.channels.configure; temsilî kimlik doğrulama mevcut TrustedWorkflowDelegatedUser şemasıyla).
  Actor type: service (sahibi modül) + delegated tenant_user (yalnız yazma); DL-3 işleri System.
```

- v1'de yeni izin anahtarı yoktur (Q-7: admin ekranı ve `platform.tenant-secrets.*` v1 dışı).
- Hiçbir izin bir insana değer okutmaz; `platform_admin`'in tüm izinlere geçişi resolve şemasına ulaşmaz (ayrı şema).

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz ve yasak**.

- İç uçlar `/api/internal/v1/tenant-secrets/**` Gateway'den erişilemez olmalıdır. Bugün `ocelot.json`'da `/api/internal` ile başlayan ya da
  genel catch-all rota yok (ölçüm, §19.2). DL-1 bir rota testi ekler: gerçek `ocelot.json`'u okur ve hiçbir upstream şablonunun bu yolları
  eşlemediğini doğrular (emsal `gateway/Diten.ApiGateway.Tests/Mod0173Pm2InternalInventoryRouteTests.cs:8-15`).
- Admin rotası yok (Q-7; r1 X-4 kalktı).
- Tüketici servis Platform'u doğrudan çağırır (5057; MDM → Platform iç çağrı emsali); Web asla çağırmaz.

## 16. Acceptance Criteria

**DL-1 — çekirdek: kayıt, şifreleme, yazma uçları, denetim**
- [ ] AC-1 (zorunlu) — KEK başlangıç doğrulaması `TenantSecretVaultOptionsValidator` ile (§8.2): `Current` yok, boş, geçersiz base64, çözülmüş uzunluğu 32 bayttan farklı, bilinen yer tutucu ya da 32 sıfır bayt; `CurrentId` biçim dışı; `Previous` kimliği yinelenmiş ya da `CurrentId`'ye eşit; bir `Previous` çözülmüş baytları `Current`'a eşit → Platform başlamaz; hata metninde anahtar adı ve kural var, değer yok. S-1: `TenantSecretVaultOptionsValidator` kaydını (`ValidateOnStart`) kaldır → başlangıç testinin her vakası kırmızı.
- [ ] AC-2 (zorunlu) — Şifresiz yazım yok ve nonce tekrar etmez: yazmadan sonra `platform_tenant_secrets` belgesinin ham BSON baytlarında nöbetçi değer (UTF-8 ve base64) yok; aynı değer iki kayda yazılınca `ValueNonce`'lar ve `DekNonce`'lar **açıkça** farklı; birim vakada aynı DEK ve aynı düz metinle `EncryptValue` iki kez çağrılınca nonce'lar ve şifreli metinler farklı. S-2a: `ITenantSecretCipher.EncryptValue`'yu düz metni döndüren bir uygulamayla değiştir → bayt taraması kırmızı; S-2b: nonce üreticisini sabit 12 bayta bağla → nonce eşitsizliği vakaları (Mongo ve birim) kırmızı.
- [ ] AC-3 (zorunlu) — AAD bağlama, her AAD ayrı ölçülür. (a) Bilinen cevap testi: `TenantSecretCipherVectors.json`'daki vektörler için AAD baytları, türetilmiş KEK, sarılı DEK, şifreli metin ve etiketler bayt-eşit. (b) Değer AAD'si: aynı kayıtta yalnız değer AAD'sine giren bir meta alanı (`Name`, `ScopeRef` ya da `SecretVersion`) Mongo'da doğrudan değiştirilince resolve 500 `SECRET_INTEGRITY_FAILED` (sarma AAD'si bu alanları içermez, DEK açılır — vaka yalnız değer AAD'sini ölçer). (c) Sarma AAD'si: birim vakada aynı kiracı ve aynı KEK altında X kaydının `WrappedDek`'i Y kaydının sarma bağlamıyla (`Id` farklı) `UnwrapDek`'e verilince açılmaz. (d) Bütünleşik vaka: X'in `Cipher` zarfı aynı kiracıda Y'ye kopyalanınca resolve 500, değer yok. **Dilim:** (a) ve (c) birim düzeyindedir, DL-1 kabulünde; (b) ve (d) resolve ucuyla ölçülür, **DL-2 kabulünde** (r3, F-1). S-3a: değer AAD'sini boş geçir → (a) kırmızı (DL-1), (b) kırmızı (DL-2); S-3b: sarma AAD'sini boş geçir → (a) ve (c) kırmızı.
- [ ] AC-4 (zorunlu) — Kiracı başına KEK, AAD'den bağımsız ölçülür: (a) `DeriveTenantKek(master, A)` ≠ `DeriveTenantKek(master, B)` ve vektör dosyasındaki beklenen türetilmiş KEK'e bayt-eşit; (b) birim vakada `kekA = DeriveTenantKek(master, A)`, `kekB = DeriveTenantKek(master, B)`; `wrappedA = WrapDek(kekA, dek, ctxA)`; `UnwrapDek(kekB, wrappedA, ctxA)` — aynı sarma bağlamı (aynı AAD), yalnız KEK farklı — `CryptographicException` atar (§10 imzaları, r3 F-2). S-4: HKDF tuzunu sabit değere bağla → (a) eşitsizlik ve vektör vakaları kırmızı; (b) `kekA == kekB` olur, `UnwrapDek(kekB, wrappedA, ctxA)` DEK'i açar, vaka kırmızı.
- [ ] AC-5 (zorunlu) — Yaz: listedeki servis kimliği + aynı kiracıdan temsilî kullanıcı + girişteki izin → 201 `{ vaultRef, secretVersion: 1, setAt, expiresAt }`; cevapta değer ve zarf alanı yok; aynı anahtarda ikinci yazma 409 `SECRET_ALREADY_EXISTS`. S-5: tekil kısmi indeksi ve ön kontrolü kaldır → çift kayıt vakası kırmızı.
- [ ] AC-6 (zorunlu) — Döndür (geçiş penceresi verilmeden): `SecretVersion` +1, `SetAt` yenilenir, önceki zarf aynı yazımda canlı belgeden kaldırılır (ham BSON'da eski şifreli metin yok); yanlış `expectedSecretVersion` 409. S-6: kaldırma adımını atla → "eski zarf canlı belgede yok" vakası kırmızı.
- [ ] AC-7 (zorunlu) — İptal soft'tur: `State = Revoked`, `IsDeleted = true`, `DeletedAt` dolu, zarflar `null`, belge koleksiyonda durur; meta sorgusu `state: Revoked` döner (DL-1 kabulü); resolve 409 `SECRET_REVOKED`, değer yok (**DL-2 kabulü**, r3 F-1). S-7: iptali `DeleteOne` ile yap → "kayıt duruyor" vakası kırmızı.
- [ ] AC-8 (zorunlu) — Yetkisiz yazma ve **sıfır yazım** (sır, idempotency kaydı yok): kimliği doğrulanamayan çağrı (jeton yok, yanlış audience, insan jetonu) → 401; geçerli servis jetonu ama liste dışı servis adı ya da istemci → 403 `SECRET_CONSUMER_NOT_ALLOWED`; başka kiracıdaki temsilî kullanıcı → 403 `SECRET_DELEGATED_TENANT_MISMATCH`; izni olmayan temsilî kullanıcı → 403 `SECRET_DELEGATED_PERMISSION_MISSING`; girişte olmayan ad → 422 `SECRET_NAME_NOT_ALLOWED`; 403 ve 422'de ret denetim satırı. S-8a: liste denetimini kaldır → liste dışı istemci vakası kırmızı; S-8b: temsilî kiracı eşitliğini kaldır → kırmızı.
- [ ] AC-9 (zorunlu) — Kiracı sızıntısı yok: B kiracısı jetonuyla A'nın `vaultRef`'i için meta, döndür, iptal (ve DL-2'de resolve) → 404 `SECRET_NOT_FOUND`, gövde var olmayan `vaultRef` cevabıyla bayt-eşit. Aynı kiracıda başka tüketici modül: v1 listesinde tek giriş olduğundan test, yabancı `OwnerModule`'lü (ör. `MOD-9999-TEST`) bir belgeyi doğrudan Mongo'ya tohumlar; MOD-0179 jetonuyla bu belgeye her uç 404 (aynı gövde). S-9a: depodaki `TenantId` süzgecini kaldır → kiracı vakası kırmızı; S-9b: `OwnerModule` süzgecini kaldır → tohumlanmış belge vakası kırmızı.
- [ ] AC-10 (zorunlu) — Denetim işlem içidir: yaz, döndür, iptal tam bir `audit_outbox` satırını iş verisiyle aynı işlemde yazar (platform-islem-ici), üretimdeki `AuditOutboxProcessor` onu `audit_events`'e taşır; aktör temsilî kullanıcı, `SourceService` servis adı, olay adları §21; outbox yazımı başarısızsa 503 `SECRET_AUDIT_UNAVAILABLE` ve sır kaydı değişmez; `TransactionOwnedAuditCommandGuardTests` beş komutu kümede ve işleyicilerinin kurucusunda `ITransactionalAuditOutboxWriter` görür; denetim defteri mimari testi (`tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`) her işleyicinin `TenantSecretAuditIntent.EnqueueAsync` çağrısını (platform-islem-ici belirteci, X-2) bulur. S-10a: `TenantSecretAuditIntent.EnqueueAsync` çağrısını kaldır (yazıcı kurucuda kalır) → teslim testi ve defter testi kırmızı (guard testi bu sabotajı görmez — yalnız kurucu türüne bakar); S-10b: denetim yazımını işlem dışına al → "kayıt yoksa işlem yok" vakası kırmızı; S-10c: bir işleyicinin kurucusundan `ITransactionalAuditOutboxWriter`'ı çıkarıp yazıcıyı başka yoldan al → guard testi kırmızı.
- [ ] AC-11 (zorunlu) — Değer sızıntısı yok (yazma yolu): rastgele 48 karakterlik nöbetçi değerle yaz + döndür + iptal + hata yolları (400, 409, enjekte 500) sonrası nöbetçi şu yerlerin hiçbirinde yok: HTTP cevapları, test log havuzu (tüm seviyeler), `audit_outbox`, `audit_events`, idempotency deposu, tüm Mongo koleksiyonlarının ham dökümü, istisna metinleri. S-11a: komut kaydında `PrintMembers` maskesini kaldır ve isteği logla → log taraması kırmızı; S-11b: doğrulama mesajına `{PropertyValue}` ekle → cevap taraması kırmızı.
- [ ] AC-12 (zorunlu) — Idempotency: aynı anahtar + aynı değer → aynı cevap, ikinci kayıt ve ikinci denetim satırı yok; aynı anahtar + farklı değer → 409 `IDEMPOTENCY_KEY_REUSED`; parmak izi düz değerin özetini taşımaz (yalnız kiracıya bağlı `K_idem` ile HMAC); KEK döndürüldükten sonra (eski KEK `Previous`'ta) aynı anahtar + aynı değer → ilk cevap, 409 yok; `FingerprintKekId` yapılandırmada yoksa 503 `SECRET_KEY_UNAVAILABLE`. S-12a: HMAC'i parmak izinden çıkar → farklı değer vakası kırmızı; S-12b: tekrar karşılaştırmasında `FingerprintKekId` yerine her zaman `Current`'ı kullan → döndürme sonrası vaka kırmızı (yanlış 409).
- [ ] AC-13 (zorunlu) — Tüketici listesi kodda sabittir: yapılandırmada bilinmeyen modül, kümede olmayan ad (ör. `SftpPassword` v1'de), ikinci giriş, boş istemci kimliği ya da 16'dan fazla giriş → Platform başlamaz. S-13: doğrulayıcı kaydını kaldır → başlangıç testi kırmızı.
- [ ] AC-14 (zorunlu) — Gateway iç yolları eşlemez: gerçek `ocelot.json`'u okuyan test, `/api/internal/v1/tenant-secrets`, büyük harfli biçimi ve alt yolları için eşleşen rota bulmaz. S-14: geçici çalışma ağacında `ocelot.json`'a `/api/internal/{everything}` ekle → test kırmızı (geri alınır).
- [ ] AC-15 (zorunlu) — Meta sorgusu: `GET …/{vaultRef}/metadata` `{ vaultRef, name, state, secretVersion, setAt, setBy, expiresAt }` döner; hiçbir DTO (resolve cevabı dışında) `byte[]`, zarf ya da `value` alanı taşımaz (yansıma testi). S-15: meta DTO'ya `Cipher` ekle → yansıma testi kırmızı.

**DL-2 — servis kimliğiyle okuma (resolve) ve sözleşme**
- [ ] AC-16 (zorunlu) — Resolve: listedeki servis jetonu + aynı kiracı + kendi modülünün `vaultRef`'i → 200 `{ value, secretVersion, expiresAt }`, `Cache-Control: no-store`; değer yazılanla bayt-eşit (çok satırlı PEM dahil). S-16: `no-store` başlığını kaldır → kırmızı.
- [ ] AC-17 (zorunlu) — Yetkisiz okuma yok: insan jetonu (`platform_admin` dahil) ya da başka audience → 401; geçerli servis jetonu ama liste dışı servis → 403 `SECRET_CONSUMER_NOT_ALLOWED`; başka kiracı ya da başka tüketici modül → 404; iptal → 409 `SECRET_REVOKED`; süresi geçmiş (sahte saat) → 409 `SECRET_EXPIRED`; hiçbirinde değer yok. S-17a: resolve ucunun şemasını varsayılan kullanıcı JWT şemasına çevir → insan jetonu vakası kırmızı; S-17b: süre denetimini kaldır → süresi geçmiş vakası kırmızı.
- [ ] AC-18 (zorunlu) — Okuma yolu sızıntısı yok: resolve sonrası nöbetçi değer test log havuzunda, OTel span özniteliklerinde (test dışa aktarıcısı), denetim tablolarında ve hata cevaplarında yok. S-18: span'a cevap gövdesini öznitelik olarak ekle → kırmızı.
- [ ] AC-19 (zorunlu) — Sözleşme testi: `tenant-secret-vault.openapi.yaml` içindeki beş uç, alan adları, hata kodları ve başlıklar üretim controller'ı ve DTO'larıyla eşleşir. S-19: bir cevap alanının adını değiştir → sözleşme testi kırmızı.

**DL-3 — anahtar yaşam döngüsü ve geçiş penceresi**
- [ ] AC-20 (zorunlu) — KEK döndürme: yeni `Current` + eski `Previous` ile her iki KEK'li kayıt okunur; `TenantSecretMaintenanceJob` `RewrapTenantSecretsCommand`'ı `ISender.Send` ile gönderir; sonrasında eski `KekId`'li kayıt kalmaz, şifreli metin ve `SecretVersion` değişmez, resolve aynı değeri döner; `Previous` erken kaldırılırsa o kayıtlar 503 `SECRET_KEY_UNAVAILABLE`. S-20a: komutta DEK'i yeniden sarmadan yalnız `KekId`'yi güncelle → resolve vakası kırmızı; Pipeline'dan geçiş iki yolla ölçülür: (i) yansıma vakası — işin kurucu parametreleri arasında `IRequestHandler<,>` uygulayan hiçbir tür ve `ITenantSecretRepository` yok, `ISender` var; (ii) gözlenebilir etki — iş koştuktan sonra test log havuzunda `LoggingBehavior`'ın `Starting RewrapTenantSecretsCommand` ve `Completed RewrapTenantSecretsCommand` satırları var (`services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/LoggingBehavior.cs:22-37`; işleyici doğrudan çağrılırsa bu satırlar oluşmaz). S-20b: işin kurucusuna `RewrapTenantSecretsHandler`'ı ekleyip `Handle`'ı doğrudan çağır → (i) yansıma vakası ve (ii) log satırı vakası kırmızı. Ölçüm DL-2 kabulünden sonradır (resolve, r3 F-1).
- [ ] AC-21 (zorunlu) — Son kullanma ve iptal edilmiş anahtara yeniden yazma: `ExpiresAt` sahte saatle geçince meta `Expired`, resolve 409; iptal edilen anahtara yazma yeni `vaultRef` açar, eski kayıt `Revoked` kalır. S-21: tekil indeksten `IsDeleted` koşulunu kaldır → yeniden yazma vakası kırmızı.
- [ ] AC-22 (zorunlu) — Doğrulama geçiş penceresi (sahip kararı Q-6): `InboundSigningSecret` döndürmesinde `previousValidForHours` (1..72) verilirse resolve `use = Verify` önceki değeri pencere içinde `previous` olarak döner, `use = Use` döndürmez; pencere bittiğinde (sahte saat) `previous` dönmez ve `PurgeExpiredPreviousSecretsCommand` önceki zarfı canlı belgeden kaldırır (denetim satırı §21); `ApiKey` için `previousValidForHours > 0` → 422 `SECRET_GRACE_NOT_ALLOWED`; 72'den büyük → 400. S-22a: okuma anındaki pencere sonu denetimini kaldır → sahte saat vakası kırmızı; S-22b: `VerificationGraceNames` denetimini kaldır → `ApiKey` vakası kırmızı.
- [ ] AC-23 (zorunlu) — Yedek ve geri yükleme: Mongo dökümü + aynı KEK (aynı `KekId`, aynı bayt) ile geri yüklenen veritabanında resolve aynı değeri döner; aynı `KekId` altında farklı KEK baytlarıyla 500 `SECRET_INTEGRITY_FAILED`; dökümdeki `KekId` yapılandırmada yoksa 503 `SECRET_KEY_UNAVAILABLE`; hiçbirinde değer yok; runbook dosyası adımları, `Previous` kaldırma koşulunu, "kaldırma" notunu (§8.5) ve tatbikat komutlarını yazar. S-23: `UnwrapDek` GCM hatasını yutup boş DEK'le devam etsin → "farklı KEK" vakası kırmızı.

**DL-4 — v1 dışı (sahip kararı Q-7, 2026-11-13)**
- [ ] AC-24 (isteğe bağlı) — v1 dışı: platform-admin meta listesi bu pakette teslim edilmez ve kabul ölçümüne girmez; takip işi ADMIN-UI açılırsa ölçütü o pakette yazılır. Sabotaj yok (teslim yok).
- [ ] AC-25 (isteğe bağlı) — v1 dışı: `platform.tenant-secrets.*` izinleri ve admin lookup anahtarları bu pakette yoktur; takip işi ADMIN-UI. Sabotaj yok.
- [ ] AC-26 (isteğe bağlı) — v1 dışı: admin ekranı dil ve yardım metinleri bu pakette yoktur; takip işi ADMIN-UI. Sabotaj yok.

**DL-5 — Auth audience (XMC X-1, sahibinin izniyle)**
- [ ] AC-27 (zorunlu) — Auth yalnız (`Diten.Inventory`, `TRUSTED_SECRET_VAULT_CONSUMER`) çiftine yeni jeton verir; MDM'nin üç çifti aynen geçer; başka her çift (ör. `Diten.Inventory` + workflow audience, `Diten.MDM` + kasa audience) reddedilir. S-27: `IsAllowedPair`'i servis adından bağımsız yap → ret vakası kırmızı.

**Tüm dilimler**
- [ ] AC-28 (zorunlu) — Platform tam test takımı (filtresiz), Gateway testleri ve mimari testler (denetim defteri dahil) yeşil; dokunulan her servisin derlemesi PASS; kırmızı varsa aynı gün `main` taban çizgisiyle karşılaştırılır. S-28: §21'deki bir komutu defterden düşür → defter testi kırmızı.

## 17. Test Expectations

- **Üretim koduyla ölçüm.** HTTP + Mongo testleri gerçek Platform kompozisyonunu (`WebApplicationFactory`, gerçek `Program.cs`, gerçek Mongo
  replica set) kullanır; kural kopyası test edilmez. Kripto birim testleri ve bilinen cevap testi gerçek `AesGcmTenantSecretCipher`'ın iç
  yüzeylerini (§10) çağırır; testte ayrı bir kripto uygulaması yazılmaz.
- **Yalıtılmış vakalar (F-3).** Her güvenlik özelliği, başka bir özellik onu maskelemeyecek biçimde ölçülür: kiracı KEK ayrımı aynı AAD ile
  (AC-4 (b)), değer AAD'si yalnız değer AAD'sine giren alanla (AC-3 (b)), sarma AAD'si birim düzeyinde (AC-3 (c)), nonce tekrarı açık eşitsizlik
  iddiasıyla (AC-2).
- **Nöbetçi taraması.** Her test rastgele bir nöbetçi üretir; tarama ham baytlarda (UTF-8 ve base64 biçimleri) yapılır.
- **Kiracı izolasyonu** (AC-9, AC-17), **soft delete** (AC-7), **eşzamanlılık** (AC-6), **idempotency** (AC-12).
- **Build:** `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`; DL-5'te AuthService API;
  Gateway test projesi (AC-14).
- **Ekran yok:** `verify_datatable_page.py`, RESX parite ve tarayıcı smoke v1'de uygulanmaz (Q-7).
- **Sabotaj kanıtı.** Her S-n ayrık bir çalışma ağacında uygulanır, ilgili testin kırmızı olduğu kayda geçer, geri alınır (Çekirdek madde 10).

### 17.1 AC → test → sabotaj eşlemesi

| AC | Test (öneri adı) | Sabotaj |
|---|---|---|
| AC-1 | `TenantSecretVaultOptionsValidatorTests` + `TenantSecretVaultStartupTests.InvalidKek_StopsPlatform` | S-1 |
| AC-2 | `TenantSecretCipherMongoTests.StoredDocument_HasNoPlaintext_AndDistinctNonces`, `TenantSecretCipherTests.SameDekSamePlaintext_DistinctNonceAndCiphertext` | S-2a, S-2b |
| AC-3 | DL-1: `TenantSecretCipherVectorTests` (a), `TenantSecretCipherTests.WrapContextOfOtherRecord_CannotUnwrap` (c); DL-2: `TenantSecretsResolveHttpMongoTests.ValueAadFieldTampered_FailsIntegrity` (b), `TenantSecretsResolveHttpMongoTests.TransplantedEnvelope_FailsIntegrity` (d) | S-3a, S-3b |
| AC-4 | `TenantSecretCipherTests.DerivedKeks_DifferPerTenant_AndMatchVectors`, `TenantSecretCipherTests.UnwrapDek_OtherTenantKek_SameWrapContext_Throws` | S-4 |
| AC-5 | `TenantSecretsInternalHttpMongoTests.Write_Returns201_WithoutValue_AndDuplicate409` | S-5 |
| AC-6 | `TenantSecretsInternalHttpMongoTests.Rotate_RemovesPreviousEnvelopeFromLiveDocument` | S-6 |
| AC-7 | DL-1: `TenantSecretsInternalHttpMongoTests.Revoke_IsSoft_MetadataRevoked`; DL-2: `TenantSecretsResolveHttpMongoTests.Revoked_Resolve409` | S-7 |
| AC-8 | `TenantSecretsInternalHttpMongoTests.UnauthorizedWriters_ZeroWrites` | S-8a, S-8b |
| AC-9 | `TenantSecretsInternalHttpMongoTests.CrossTenant_And_SeededForeignOwner_404_ByteEqual` | S-9a, S-9b |
| AC-10 | `TenantSecretAuditDeliveryMongoTests.WriteRotateRevoke_OneRowInTransaction_FailClosed` + `AuditTrailStandardTests` (defter) + `TransactionOwnedAuditCommandGuardTests` | S-10a, S-10b, S-10c |
| AC-11 | `TenantSecretLeakScanTests.WritePath_SentinelAbsentEverywhere` | S-11a, S-11b |
| AC-12 | `TenantSecretsInternalHttpMongoTests.Idempotency_SameKeyDifferentValue_409_AndAfterKekRotation` | S-12a, S-12b |
| AC-13 | `TrustedSecretVaultConsumerAuthorizationOptionsValidatorTests` | S-13 |
| AC-14 | `gateway/Diten.ApiGateway.Tests/Mod0012Fu01InternalSecretVaultRouteTests` | S-14 |
| AC-15 | `TenantSecretDtoShapeTests.NoDtoCarriesValueOrEnvelope` | S-15 |
| AC-16 | `TenantSecretsResolveHttpMongoTests.Resolve_ReturnsExactBytes_NoStore` | S-16 |
| AC-17 | `TenantSecretsResolveHttpMongoTests.UnauthorizedReaders_Refused` | S-17a, S-17b |
| AC-18 | `TenantSecretLeakScanTests.ReadPath_SentinelAbsentFromLogsSpansAudit` | S-18 |
| AC-19 | `TenantSecretVaultContractTests` | S-19 |
| AC-20 | `TenantSecretMaintenanceJobMongoTests.Rewrap_KeepsCiphertext_AndRetiresOldKek`, `TenantSecretMaintenanceJobTests.Constructor_HoldsOnlySender`, `TenantSecretMaintenanceJobMongoTests.Rewrap_GoesThroughPipeline_LoggingBehaviorLines` | S-20a, S-20b |
| AC-21 | `TenantSecretsLifecycleMongoTests.Expiry_And_RewriteAfterRevoke` | S-21 |
| AC-22 | `TenantSecretsLifecycleMongoTests.VerificationGrace_EndsAndPurges_OnlyForListedNames` | S-22a, S-22b |
| AC-23 | `TenantSecretBackupRestoreTests.SameKek_Resolves_DifferentKek500_UnknownKekId503` | S-23 |
| AC-24…AC-26 | — (v1 dışı, Q-7) | — |
| AC-27 | `ServiceIdentityTokenAudiencePolicyTests` (AuthService) | S-27 |
| AC-28 | tam takım koşusu (CT) | S-28 |

## 18. Ready-for-dev Checklist

- [x] DCP-002 ön kontrolü r2 HEAD'inde yeniden koşuldu (OK); registry satırı var (`:70`)
- [x] Frontmatter zorunlu alanları dolu (service, shell `none`, golden_reference `none`, entity_base, form_field_count 0) — Q-7 gerekçesi §9
- [x] Layout & Shell Contract: ekran yok (§9)
- [x] Backend File Convention Golden Reference ile birebir (CommandHandlers / QueryHandlers ayrımı, soneksiz adlar)
- [x] Frontend File Contract: v1'de dosya yok (§11)
- [x] Validation Rules her alan için yazılı
- [x] Failure Path ≥ 4 senaryo (mükerrer, eksik, yetkisiz, eşzamanlılık + kiracılar arası, transplant, denetim, idempotency)
- [x] Authorization Convention: iç uç şeması, temsilî kullanıcı şeması, tüketici listesi, actor type
- [x] Gateway kararı: değişiklik yok, iç yollar rotasız (AC-14)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (§21)
- [x] v1 tüketici girişi MOD-0179 r3'e göre dolu (§8.3 — ad kümesi ve temsilî izin)
- [ ] Q-1 (KEK yeri / erişimi), Q-2 (yedek / kurtarma), Q-13 — Ali
- [ ] Q-5 (işlem içi denetim kümesi X-3 ve defter X-2) — CT
- [ ] Q-12 (domain-config Vault satırı) — CT
- [ ] CT-APR-02 r2 fark incelemesi + CT-APR-01 onay satırı (yüksek risk: paketi yazmamış, `high: evet` onaylayıcı)

## 19. Implementation Notes

### 19.1 Okunan kaynaklar

- `AGENTS.md` Çekirdek, §6, §6.1, §7, §10, DCP-002 bölümü.
- `execution/registries/module-id-registry.md` (MOD-0012 satırı ve takma adları; MOD-0012-FU01 satırı `:70`; MOD-0033-FU02 satırı).
- `execution/domains/platform-shared-services/module-packs/MOD-0012-secrets-configuration-vault.md` (ana paket).
- `execution/domains/platform-shared-services/domain-config.md` (Vault satırı 56, Lokalizasyon satırı 59).
- `execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md` **r3** — `SecretRefs[]` satır 287, X-14 satır 409, §8.5
  satırlar 512-525, izin satırı 758, Q-16 satırlar 1032-1033, FU-0179-VAULT satır 1079, DL-5 satır 1140.
- `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` — T-4 satır 12, F-10 satırlar 45-47, MOD-0012-FU01 kararları satırlar 55-59.
- `docs/records/reviews/MOD-0012-FU01/2026-11-13-754b2c86.md` (r1 incelemesi).
- `docs/reference/modules/platform/secrets-configuration-vault/technical-reference.md:10-24`.
- Kurallar: `module-pack-standard.md` (§1-§16), `audit-trail-standard.md` (§3-§7), `permission-key-standard.md` (§1, §4, §5),
  `localization-standard.md`, `record-coding-standard.md` (kapsam).
- Kod: §19.2'deki dosyalar.
- **Sahip kararları:** Beste, 2026-11-13, sohbet — "tamam ekleyelim bunu platform tarafına" (FU açılışı); F-10 (aynı gün, MOD-0179 v1.0 kasayı
  beklemez); Q-6…Q-9, Q-14, Q-15 "öneri gibi olsun" (kararlar 01 satırlar 55-59). Q-10 cevabı çağıran CT'nin aktarımıyla (§19.4).

### 19.2 Ölçülen gerçekler

1. **`ISecretsProvider` yalnız okur** — `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/ISecretsProvider.cs:3-7`
   (iki `Get…` metodu). Tek uygulaması `ConfigurationSecretsProvider`: üretimde yalnız ortam değişkeni, diğer ortamlarda `IConfiguration`
   (`ConfigurationSecretsProvider.cs:51-64`). Kayıt `SecretServiceCollectionExtensions.cs:23`.
2. **Bugünkü kayıt yerleri (`AddSecretsProvider`)**: AuthService, Platform, DevEnablement, Inventory, Manufacturing, Procurement, Gateway, Diten.Web.
3. **Bugünkü tek çalışma anı tüketicisi** Platform bildirimleridir: `SecretReferenceResolver` bir **anahtar adını** `ISecretsProvider`'dan
   çözer (`services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Notifications/SecretReferenceResolver.cs:13-34`);
   kiracı ayarı yalnız referans tutar (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Notifications/TenantMessagingSettings.cs:18`).
   MOD-0179 r3 v1.0 aynı deseni kullanır (F-10).
4. **Gözlem (kapsam dışı, follow-up):** `ToEnvironmentVariableName` önce `:` → `_`, sonra `_` → `__` yapar (`ConfigurationSecretsProvider.cs:64`);
   adında alt çizgi olan anahtar üretimde yanlış ortam değişkenine eşlenir. Bu paketin anahtarlarında alt çizgi yoktur.
5. **Platform servislerinde kalıcı şifreleme yok:** üretim kodunda `AesGcm`, `IDataProtector`, `AddDataProtection` kullanımı yok.
6. **Servis kimliği:** Platform'da adlı RS256 jeton doğrulayıcı, tüketici başına audience deseni ve temsilî kullanıcı şeması var
   (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs:16-45`, temsilî şema satırlar 153-175).
   Auth ise yalnız `Diten.MDM`'e jeton verir
   (`services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17`).
7. **Denetim:** platform-islem-ici izi kabul edilmiş, MOD-0040-FU02 (A-10) aynı yolu kullanıyor (`tests/architecture/audit-ledger/Diten.Platform.md:16`,
   not satır 30); işlem içi komut kümesi
   `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TransactionOwnedAuditCommands.cs:22-57`.
8. **KEK doğrulaması için MOD-0012 doğrulayıcısı yetmez:** uzunluk karakterle sayılır, önceki ≠ şimdiki yalnız JWT koleksiyonunda
   (`SecretRequirementValidator.cs:88-101`) → §8.2 kasaya özgü doğrulayıcı.
9. **Gateway:** `ocelot.json`'da `/api/internal` ile başlayan ya da genel catch-all rota yok; 350 upstream şablonu.
10. **Domain kararı:** "Vault: appsettings + environment variables (thin abstraction). External vault (NEW-001) ertelenmiştir."
    (`execution/domains/platform-shared-services/domain-config.md:56`). Bu FU dış kasa getirmez; Mongo'da şifreli kiracı sırrı saklamak bu
    satırın genişlemesidir → güncellemesi CT işi (Q-12).

### 19.3 Çıkarımlar

- Kiracı sırrının ortam değişkeninde tutulması kiracı sayısıyla ölçeklenmez ve çalışma anında yazılamaz → kalıcı, şifreli depo gerekir; MOD-0179
  v1.0 bu açığı işletim yerleştirmesiyle geçici olarak kapatır (F-10).
- Değeri tüketici servise ağda vermek kaçınılmazdır; bu yüzden resolve tek, servis-yalnız, Gateway dışı uçtur ve TLS kararı (Q-4) canlıya çıkış
  kapısıdır.
- Denetimin işlem içi (fail-closed) olması K2 sınıfı tartışmasını gereksiz kılar: komut sınıfı ne olursa olsun kayıt yoksa işlem yoktur.

### 19.4 Sorular ve kararlar

**Kararlar (Beste, 2026-11-13, kararlar 01 satırlar 55-59 — "öneri gibi olsun"):**
- **Q-6 — karar:** 3PL gelen imza sırrı değişiminde eski ve yeni sır en çok 72 saat birlikte geçerli → AC-22 zorunlu, `PurgeExpiredPreviousSecretsCommand` zorunlu.
- **Q-7 — karar:** yönetim (meta) ekranı v1'de yok → DL-4 ve AC-24…AC-26 v1 dışı, izin ve Gateway admin rotası yok; takip işi ADMIN-UI.
- **Q-8 — karar:** resolve okumaları denetim kaydına yazılmaz; yalnız değer içermeyen sayaç (§21).
- **Q-9 — karar:** meta sorgusu kalır (`GetTenantSecretMetadataQuery`).
- **Q-14 — karar:** `LegalEntityId` kasada ayrıca doğrulanmaz; çağıran servis doğrular (§2.3 madde 7).
- **Q-15 — karar:** Platform bildirimlerinin SMTP sırrının taşınması ayrı FU (§20).
- **Q-11 — kapandı:** registry satırı eklendi (`execution/registries/module-id-registry.md:70`).
- **Q-10 — cevap (çağıran CT'nin aktarımı, 2026-11-13):** dal tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (sahip kuralı);
  künyede yazılı. Karar kaydına işlenmesi CT'nindir.

**Açık — Ali:**
- **Q-1** Ana KEK nerede, kim üretir, kim erişir? Öneri: v1 Platform ana makinesinde ortam değişkeni, `IKeyEncryptionKeyProvider` dikişiyle ileride KMS / HSM.
- **Q-2** Yedekleme / kurtarma: KEK yedeği nerede ve kim erişir; KEK kaybında "sırları yeniden gir" kabul mü?
- **Q-3** Inventory için Auth servis istemcisi ve kiracı grant'leri (dev / prod provisioning) ve X-1 izni (MOD-0033-FU02) — DL-5 kapısı.
- **Q-4** Servisler arası TLS / mTLS: canlıda zorunlu mu — DL-2 canlı kullanım kapısı.
- **Q-13** FU onayı, ana paket MOD-0012'nin `review` durumunu bekler mi? Öneri: beklemez.

**Açık — CT:**
- **Q-5** Denetim yolu platform-islem-ici (MOD-0040-FU02 A-10 emsali): `TransactionOwnedAuditCommands.Authorized` kümesine beş komut (X-3) ve defter
  belirteci (X-2). AUD-001 §4.4'e göre kümeye ad eklemek CT kararıdır.
- **Q-12** domain-config "Vault" satırının güncellenmesi (§19.2 madde 10).

### 19.5 r1 inceleme bulguları — kapanış tablosu

| Bulgu | Düzeltme (r2) | Bölüm |
|---|---|---|
| F-1 | "MOD-0179 DL-4 kapısı" ifadeleri kaldırıldı; FU, FU-0179-VAULT'un kapısıdır (MOD-0179 v1.0 kapısı değil); MOD-0179 r3 ve registry `:70` atıfları | künye, giriş notu, §1, §7, §8.8, §24 |
| F-2 | Q-6 (AC-22 zorunlu), Q-7 (DL-4 / AC-24…AC-26 / ekran / izinler / X-4 v1 dışı; shell none, golden none, form 0), Q-8, Q-9, Q-14, Q-15 karar; Q-11 kapandı; açıklar adıyla | künye, §2.2, §3, §9, §11, §14, §15, §16, §19.4, §23 |
| F-3 | S-4: aynı AAD ile kiracı KEK ayrımı + türetme vektörü; S-3a / S-3b: değer ve sarma AAD'si için ayrı yalıtılmış vaka; S-2b: nonce eşitsizliği açık iddia + birim vaka | §16 AC-2…AC-4, §17, §17.1 |
| F-4 | v1 ad kümesi üç API adı; temsilî izin `warehouse.third-party-logistics.channels.configure`; MOD-0179 atıfları r3 satırları; değer sınırının kendi gerekçesi | §1, §8.3, §8.9, §19.1 |
| F-5 | Kasaya özgü `TenantSecretVaultOptionsValidator` (base64, 32 bayt, yer tutucu, sıfır, Previous ≠ Current); S-1 onu hedefler | §8.2, AC-1 |
| F-6 | X-3 yolu `TransactionOwnedAuditCommands.cs:22-57` + guard testi; DL-3 komutları `IMediator.Send`; temsilî kullanıcı mevcut `TrustedWorkflowDelegatedUser` şeması | §5, §5.1, §8.3, §8.6, §10, AC-20 |
| F-7 | (a) "kripto-imha" yerine canlı belgeden kaldırma + yedek / oplog notu; (b) tuz bayt sırası, AAD kodlaması, null kodlaması, test vektörü; (c) `K_idem` info, kiracı bağı, `FingerprintKekId`, `Previous` kaldırma koşulu | §2.3, §4.2, §8.2, §8.5, §8.6, AC-3, AC-12 |
| F-8 | 401 (kimlik doğrulanamadı) / 403 (liste dışı geçerli servis) ayrımı tutarlı; AC-23 aynı `KekId` farklı bayt 500, bilinmeyen `KekId` 503; AC-9 yabancı `OwnerModule` belgesi Mongo'ya tohumlanır | §8.7, §13, AC-8, AC-9, AC-17, AC-23 |

### 19.6 r2 inceleme bulguları — kapanış tablosu

| Bulgu | Düzeltme (r3) | Bölüm |
|---|---|---|
| F-1 | AC-3 (b) / (d) ve AC-7'nin resolve bacağı DL-2 kabulünde ölçülür; AC-20 ölçümü DL-2'den sonra; §24'te DL-3 satırı DL-2 kabulünü bekler | AC-3, AC-7, AC-20, §17.1, §24 |
| F-2 | `AesGcmTenantSecretCipher` iç yüzeyi KEK'i açık parametre alır (`UnwrapDek(tenantKek, wrapped, wrapContext)` vb.); AC-4 (b) ve S-4 (b) bu imzaya bağlı | §10, AC-4, §17.1 |
| F-3 | İşin kurucusunda yalnız `ISender` (işleyici türü ve depo yok); S-20b yansıma + `LoggingBehavior` satırlarıyla ölçülür; S-10a kırmızısı teslim ve defter testlerinden, guard testi için ayrı S-10c; `TenantSecretAuditIntent` statik niyet sınıfı, işleyici `ITransactionalAuditOutboxWriter`'ı kurucusunda tutar | §3, §8.6, §10, AC-10, AC-20, §17.1 |
| F-4 | §8.1 TenantScope atfı ölçülen satırlara: `:130` (servis jetonlu yol), `:208` (temsilî kullanıcılı yol). Registry `:70` notu ve Q-10 karar kaydı CT işidir — bu paket dokunmaz | §8.1 |

## 20. Follow-up Items

- **ADMIN-UI** — platform-admin meta ekranı ve iptal (Q-7 = v1'de yok). Açılırsa MOD kimliği DCP-002 `--parent MOD-0012` ile alınır (uydurulmaz).
- **SMTP-VAULT** — Platform bildirimlerinin SMTP sırrının bu kasaya taşınması, ayrı FU (Q-15).
- Dış KEK sağlayıcısı (KMS / HSM) bağdaştırıcısı `IKeyEncryptionKeyProvider` arkasında.
- Kiracı kapatma / silme akışında kiracının sırlarının kaldırılması (MOD-0009 kiracı yaşam döngüsü ile).
- Yaklaşan son kullanma bildirimi (MOD-0027).
- `ToEnvironmentVariableName` alt çizgi hatası (MOD-0012 ana paketi, §19.2 madde 4; XMC-001 — sorulacak).
- MOD-0179 v1.1 SFTP adları için tüketici listesi revizyonu (§8.3 ekleme kuralı).

## 21. Audited Events

| Komut | Olay adı | Nesne (tür) | Yol | Önceki / sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `WriteTenantSecretCommand` | `platform.tenant-secret.written` | `TenantSecret` | a — platform-islem-ici (`TenantSecretAuditIntent.EnqueueAsync`, işlem içi) | After: `OwnerModule`, `LegalEntityId`, `ScopeRef`, `Name`, `SecretVersion`, `ExpiresAt`, `KekId` | — |
| `RotateTenantSecretCommand` | `platform.tenant-secret.rotated` | `TenantSecret` | a — platform-islem-ici | Before / After: `SecretVersion`, `SetAt`, `ExpiresAt`, `PreviousValidUntil` | — |
| `RevokeTenantSecretCommand` | `platform.tenant-secret.revoked` | `TenantSecret` | a — platform-islem-ici | Before / After: `State`; `RevokeReason` | — |
| `RewrapTenantSecretsCommand` | `platform.tenant-secret.rewrapped` | `TenantSecret` (kiracı başına bir satır) | a — platform-islem-ici (`ActorType = System`, `SourceService = Diten.Platform`) | kayıt sayısı, eski `KekId`, yeni `KekId` | — |
| `PurgeExpiredPreviousSecretsCommand` | `platform.tenant-secret.previous-purged` | `TenantSecret` | a — platform-islem-ici (`ActorType = System`) | `PreviousSecretVersion`, `PreviousValidUntil` | — |
| (yaz / döndür / iptal reddi) | `platform.tenant-secret.write-refused` | `TenantSecret` | a — `TenantSecretAuditIntent.EnqueueRefusalAsync` (kendi kısa işlemi, MOD-0040-FU02 ret emsali) | `Outcome = Denied` / `Failed`, ret kodu | — |

- **Okuma satırı yok (sahip kararı Q-8):** `ResolveTenantSecretQuery` ve meta sorgusu AUD-001 §7'nin üç okuma sınıfına girmez → denetlenmez;
  değer içermeyen OTel sayacı `tenant_secret_resolve_total{owner_module, outcome}`.
- **Kayda yazılmayan alanlar:** değer, `Ciphertext`, `WrappedDek`, nonce'lar, etiketler, idempotency HMAC'i, temsilî kullanıcı jetonu.
- **K2:** komutlar işlem içi yolla kapalı-başarısızdır (AC-10); kümeye ad ekleme yine CT kararıdır (Q-5, X-3).

## 22. Record Codes

RCS-001 kapsamı dışı: kullanıcıya görünen iş kodu yok. `vaultRef` teknik `Id`'dir (Guid) ve yalnız tüketici servis tarafından saklanır.

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | Çekirdek: TenantSecret kaydı, zarf şifreleme ve KEK doğrulaması, tüketici listesi, yaz / döndür / iptal iç uçları, meta sorgusu, işlem içi denetim, Gateway rotasızlık testi | Owned Objects, Entity Fields, TenantSecret, TenantSecretCommandRequest, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Şifreleme ve anahtar yönetimi, Tüketici listesi ve servis kimliği, Değer akışı ve sızıntı sınırları, Sırrın yaşam döngüsü, Hata kodları, Değer üst sınırı, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | Servis kimliğiyle okuma (resolve) + TENANT-SECRET-VAULT 1.0.0 sözleşmesi + okuma yolu sızıntı taraması | Değer akışı ve sızıntı sınırları, Sözleşme (TENANT-SECRET-VAULT), Hata kodları, Authorization Convention | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | KEK döndürme ve yeniden sarma işi, son kullanma, doğrulama geçiş penceresi (zorunlu), yedek / geri yükleme runbook'u | KEK yaşam döngüsü ve kurtarma, Sırrın yaşam döngüsü, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-5 | Auth audience çifti (`Diten.Inventory`, `TRUSTED_SECRET_VAULT_CONSUMER`) — XMC X-1, sahibinin izniyle (DL-1'den bağımsız) | XMC-001 — başka modülün dosyalarına dokunuşlar, Dependencies | E3 | plan kimliği yok | plan satırı yok |

Plan kaynağı: plan dosyası depoda değildir ve bu FU için çağıran plan satırı aktarmadı → `plan kimliği yok` / `plan satırı yok` (değer
uydurulmadı). Hizmet ettiği teslim: MOD-0179'un FU-0179-VAULT adımı (MOD-0179 v1.0 değil); bu bilgi plan satırı değildir.

**DL-4 yok (r2):** r1'in DL-4'ü (platform-admin meta ekranı) sahip kararı Q-7 ile v1 dışına çıktı ve tablodan kaldırıldı; numara yeniden
kullanılmaz, DL-5 numarasını korur. AC-24…AC-26 kimlikleri §16'da "v1 dışı" olarak durur; takip işi ADMIN-UI (§20).

## 24. Dilim kapıları ve bağlı kararlar

| Dilim | Başlamadan önce | Bitince açtığı |
|---|---|---|
| DL-1 | paket onayı (Q-1, Q-2, Q-13 Ali; Q-5, Q-12 CT kapanmış) | DL-2, DL-3 |
| DL-2 | DL-1 kabulü | FU-0179-VAULT (DL-5 ile birlikte); DL-3; AC-3 (b) / (d), AC-7 resolve bacağı ve AC-9 resolve vakası bu dilimin kabulünde ölçülür |
| DL-3 | DL-1 kabulü + **DL-2 kabulü** (AC-20…AC-23 resolve ucuyla ölçülür — r3, F-1) | — |
| DL-5 | X-1 izni ve provisioning (Q-3) | Inventory'nin kasa jetonu alabilmesi |

**FU-0179-VAULT'un kapısı (MOD-0179 v1.0 kapısı değil)** = bu paketin DL-1 + DL-2 + DL-5'i tamam + `TENANT-SECRET-VAULT 1.0.0` dondurulmuş +
Q-3 provisioning (dev) + Q-4 kararı (canlı için). MOD-0179 v1.0 bu paketi beklemez (F-10).

## 25. Page Help (UXH-001)

Sayfa yok (sahip kararı Q-7) → UXH-001 yardım metni ve tur bu pakette uygulanmaz; takip işi ADMIN-UI açılırsa o pakette yazılır.
