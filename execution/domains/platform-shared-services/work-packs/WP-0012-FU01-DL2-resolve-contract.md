# WP-0012-FU01-DL2 — Kiracı Sır Kasası DL-2: servis kimliğiyle okuma (resolve) + TENANT-SECRET-VAULT 1.0.0 sözleşmesi

Tarih: 2026-11-14 · Veren: CT (merkez, Beste adına) · Paket:
`execution/domains/platform-shared-services/module-packs/MOD-0012-FU01-tenant-runtime-secret-vault.md` r3, LF sha256
`22f7e11edeb686bc529bfd79c37b80dabff1135ef4745814dcf8cccac590d12b` (CT ölçümü 2026-11-14, HEAD `8da76197f`; `execution/approvals/approvals.tsv:95`
satırındaki sha ile **eşit**) · Onay: `approvals.tsv:95` (Ali / GMG-ERP, 2026-11-13, HIGH; onay tüm paketi kapsar, dilim başına ayrı onay yok) ·
Önceki dilim: DL-1 **VERIFIED** (`docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md:123-137` ve `:165-172`; guard düzeltmesi
`15fcfe67c`) · DL-1 WP: `WP-0012-FU01-DL1-vault-core.md` · Sahip kararları: `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:92-105`.

**Kod izni:** var. Paket §24: "DL-2 — başlamadan önce: DL-1 kabulü" — kapandı (VERIFIED). Q-4 (servisler arası TLS) paket §7 / §24 ve sahip
kararına göre (`mvp5-wms-ct-owner-decisions-2026-11-13.md:101-102`) **canlı kullanım** kapısıdır, geliştirmeyi bekletmez. Paket revizyonu gerekmez.

## 0. Başlamadan
- Dal: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (yeni dal yok). CT etiketi `ct/line-ff-2026-11-14f` (CT oluşturur).
  Etiket dalının atası ise devam; değilse `git merge --ff-only ct/line-ff-2026-11-14f`; olmazsa `git merge --no-ff ct/line-ff-2026-11-14f`;
  çakışmada `git merge --abort` ve CT'ye yaz.
- Paketin LF sha256'sını ölç (`tr -d '\r' < <paket> | sha256sum`); `22f7e11e` ile başlamıyorsa dur, CT'ye yaz. Paketi değiştirme.
- **Eşzamanlılık:** Platform'a aynı günlerde başka işler de dokunuyor (MOD-0360-FU01 DL-1, MOD-0049-FU01 DL-2'nin Platform tarafı, varsa). Ana
  checkout'ta derleme / test yok (dotnet watch ana checkout'u kilitler); derleme, test ve sabotaj ayrık bir worktree'de. Her commit'ten hemen önce
  hattı al (yukarıdaki kural); çakışmada iki tarafın satırlarını da koru, tam takımı yeniden koş, CT'ye yaz.
- Paketi oku: §2.3 (madde 2, 3, 6), §3 (API uçları tablosu), §8.1, §8.2 (son madde — düz değer bellekte), §8.3, §8.4, §8.5 (son kullanma, geçiş
  penceresi sonu), §8.6 (bilinmeyen KekId), §8.7, §8.8, §10 (Ports, `TenantSecretsModels` notu), §12 (`use` satırı), §13, §14, §15, §16 AC-3,
  AC-7, AC-9, AC-15, AC-16…AC-19, AC-22 ve AC-28, §17, §17.1, §21 (Q-8 notu), §23 DL-2 satırı, §24.
- DL-1 kodunu oku (değiştirmeden önce): `InternalTenantSecretsController.cs`, `TrustedSecretVaultConsumerRequestExecutor.cs`
  (`ExecuteReadAsync`), `AesGcmTenantSecretCipher.cs` (`Open`), `GetTenantSecretMetadataHandler.cs` (kiracı + sahibi modül süzgeci, `Expired`
  hesabı), `ITenantSecretRepository.cs`, test host `TenantSecretHttpHost.cs`.

## 1. Kapsam (paket §23 DL-2 satırı, §16 DL-2 bölümü, §24 DL-2 satırı; dışına çıkma)

Paket §23 DL-2: "Servis kimliğiyle okuma (resolve) + TENANT-SECRET-VAULT 1.0.0 sözleşmesi + okuma yolu sızıntı taraması"; kapsadığı bölümler
§8.4, §8.8, §8.7, §14. §24: DL-2 kabulünde ayrıca **AC-3 (b) / (d), AC-7 resolve bacağı, AC-9 resolve vakası** ölçülür (r3 F-1; DL-1 WP §2
"Dilim sınırı" ve DL-1 kabul kaydı `:127` ile aynı).

### 1.1 Teslim
1. **Sözleşme — ilk adım (§8.8).** `docs/analysis/contracts/tenant-secret-vault.openapi.yaml`, `TENANT-SECRET-VAULT 1.0.0`, `x-status: DRAFT`:
   beş iç uç (§3 tablosu, DL-1'in dördü + resolve), istek / cevap şemaları (`Response<T>` zarfı), §8.7'deki tüm hata kodları (HTTP durumlarıyla),
   `Idempotency-Key` ve `X-Delegated-Authorization` başlıkları (yazma uçları), resolve 200'de `Cache-Control: no-store`, 401'in gövdesiz olduğu,
   tüketici önbellek üst sınırı **300 saniye** (açıklama + `x-consumer-cache-max-seconds: 300`), değer üst sınırı 1..16384 bayt (§8.9), Gateway
   rotası olmadığı (`x-gateway: none`). Sözleşme DL-1 uçlarını **kodun bugünkü davranışıyla** yazar (DL-1 sapmaları dahil, ör. 404 / 409 ret
   kayıtları cevabı değiştirmez). Dondurma ayrı CT adımıdır (§8.8: FU-0179-VAULT'tan önce); `docs/analysis/contracts/README.md` satırını CT yazar.
2. **Resolve (AC-16, AC-17).** `POST /api/internal/v1/tenant-secrets/{vaultRef}/resolve`:
   - `ResolveTenantSecretQuery` (`Queries/`), `ResolveTenantSecretHandler` (`Handlers/QueryHandlers/`), cevap DTO'su `TenantSecretsModels.cs`
     içinde (§10: tüm DTO'lar tek dosyada).
   - Kimlik: yalnız `TrustedSecretVaultConsumerService` şeması (denetleyicinin mevcut `[Authorize]`'ı); kapı DL-1'in
     `TrustedSecretVaultConsumerRequestExecutor.ExecuteReadAsync`'i (servis jetonu + kodda sabit liste; temsilî kullanıcı **yok**, §3 "yalnız
     servis jetonu"). Kiracı jetonun `tenant_id`'sinden (`TenantScope`, §8.1 `:130` emsali); gövdede kiracı alanı yok.
   - İstek gövdesi isteğe bağlı JSON nesnesi `{ "use": "Use" | "Verify" }` (§12; boş gövde = `Use`; bilinmeyen / yinelenen üye ya da başka değer
     → 400 `SECRET_VALIDATION_FAILED`, DL-1 `TryParse` deseni).
   - Sıra: kayıt yok · başka kiracı · başka tüketici modül → 404 `SECRET_NOT_FOUND` (var olmayan `vaultRef` ile bayt-eşit gövde) → `Revoked` → 409
     `SECRET_REVOKED` → `ExpiresAt` geçti (`TimeProvider`) → 409 `SECRET_EXPIRED` → `KekId` yapılandırmada yok → 503 `SECRET_KEY_UNAVAILABLE` →
     GCM etiketi tutmadı → 500 `SECRET_INTEGRITY_FAILED` (hata logunda kayıt kimliği + KekId, değer yok) → 200. Hiçbir hata cevabında değer yok.
   - Açma: `ITenantSecretCipher.Open` (DL-1'de yalnız birim testlerinde çağrılıyordu — DL-1 kabulündeki "`Open` DL-2'de" notu; bu dilimde üretim
     yolundan ve HTTP + Mongo ile ölçülür). Değer AAD'si kaydın **saklanan** alanlarından kurulur (`Id`, `TenantId`, `LegalEntityId`,
     `OwnerModule`, `ScopeRef`, `Name`, `SecretVersion`) — AC-3 (b) bunu ölçer.
   - 200 gövdesi (`Response<T>` içinde): `{ value, secretVersion, expiresAt }` (+ §1.1 madde 3'teki `previous`); `Cache-Control: no-store`
     zorunlu (AC-16). Değer yazılanla bayt-eşit (çok satırlı PEM dahil; trim / normalleştirme yok).
   - Cevap DTO'su değeri yalnız JSON yazımı için taşır; `ToString()` / `PrintMembers` değeri basmaz (`[REDACTED]`). `SensitiveSecretValue`
     baytları kullanım sonrası sıfırlanır (§8.2 son madde; .NET string'e dönen kopya için garanti yok — paket bunu kabul eder).
   - Resolve okuması **denetim kaydına yazılmaz** (sahip kararı Q-8, §21); 403 / 404 / 409 retleri de ret denetimi yazmaz (DL-1'in meta okuma yolu
     gibi: yalnız log). Değer içermeyen OTel sayacı `tenant_secret_resolve_total{owner_module, outcome}` (§21) — bkz. §2 "Sayaç".
3. **Doğrulama geçiş penceresinin okuma tarafı (§8.4, §8.5, §12 `use`).** `use = Verify` ve `PreviousValidUntil` > şimdi (`TimeProvider`) ise
   cevapta `previous: { value, secretVersion, validUntil }` (önceki zarf `PreviousSecretVersion` ve kendi `KekId`'siyle açılır); `use = Use` ya da
   pencere bitmişse `previous` **yazılmaz** (JSON'da üye yok). Gerekçe ve ölçüm: §2 "Geçiş penceresi".
4. **Okuma yolu sızıntı taraması (AC-18).**
5. **AC-15 güncellemesi.** `TenantSecretDtoShapeTests.NoDtoCarriesValueOrEnvelope` resolve cevap DTO'sunu (ve `previous` alt tipini) **adıyla tek
   istisna** olarak tanır (paket AC-15 "resolve cevabı dışında"); istisna dışındaki her DTO için yasak tipler / adlar aynen kalır; resolve DTO'sunun
   özellik kümesi tam eşitlikle sabitlenir.
6. **Sözleşme testi (AC-19).**

### 1.2 Repo Scope (paket §5 satırlarının DL-2 payı)

| Yol | DL-2 içeriği | Paket §5 satırı |
|---|---|---|
| `docs/analysis/contracts/tenant-secret-vault.openapi.yaml` | yeni — TENANT-SECRET-VAULT 1.0.0 (DRAFT) | DL-2 |
| `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantSecrets/Queries/ResolveTenantSecretQuery.cs` | yeni | §10 ağacı, DL-1…DL-3 |
| `…/Features/TenantSecrets/Handlers/QueryHandlers/ResolveTenantSecretHandler.cs` | yeni | §10 ağacı |
| `…/Features/TenantSecrets/TenantSecretsModels.cs` | resolve cevap DTO'su + `previous` alt tipi | §10 ağacı |
| `…/Features/TenantSecrets/TenantSecretErrorCodes.cs` | yalnız gerekirse ileti sabiti (kodların hepsi DL-1'de var) | §10 ağacı |
| `…/Features/TenantSecrets/` altında sayaç sınıfı (ör. `TenantSecretResolveMetrics.cs`) | yeni, `System.Diagnostics.Metrics` (BCL; yeni paket yok) — §10 ağacında adı yok, **ek dosya olarak raporla** (DL-1 "iki ek dosya" emsali) | §5 `Features/TenantSecrets/` satırı |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTenantSecretsController.cs` | `resolve` eylemi | DL-1, DL-2 |
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSecretVaultConsumerRequestExecutor.cs` | **değişiklik beklenmez** (`ExecuteReadAsync` yeniden kullanılır); gerekirse yalnız bu dosya, gerekçesiyle raporla | DL-1 |
| `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/ITenantSecretRepository.cs`, `…/Infrastructure/Persistence/TenantSecretRepository.cs`, `…/Infrastructure/Services/TenantSecrets/AesGcmTenantSecretCipher.cs` | **değişiklik beklenmez** (`FindAsync(tenantId, ownerModule, vaultRef)` ve `Open` var); gerekirse bu modülün dosyası, gerekçesiyle raporla | DL-1 |
| `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantSecrets/` | yeni `TenantSecretsResolveHttpMongoTests.cs`, `TenantSecretVaultContractTests.cs`; `TenantSecretAuditAndLeakTests.cs` (okuma yolu taraması, AC-15 güncellemesi); `TenantSecretHttpHost.cs` (resolve yardımcısı, cevap başlıklarına ve ham gövdeye erişim, span / sayaç dinleyicisi) | DL-1…DL-3 |

**Yok:** Gateway / `ocelot.json` (AC-14 testi resolve yollarını DL-1'de zaten içeriyor — `Mod0012Fu01InternalSecretVaultRouteTests.cs:34-35`),
`Program.cs` (şema DL-1'de kayıtlı; değişiklik gerekirse dur, sor), `appsettings*.json` (tüketici girişi / KEK / servis jetonu anahtarı yazılmaz),
yeni `[HasPermission]` anahtarı, ekran, resx (v1'de ekran yok, Q-7 — Platform 2 dil kuralı bu dilimde uygulanacak metin doğurmaz; iç API hata
iletisi teknik İngilizce, kullanıcıya gösterilmez), denetim defteri, `TransactionOwnedAuditCommands` (DL-2'de yazma komutu yok), DL-3 dosyaları
(`RewrapTenantSecretsCommand`, `PurgeExpiredPreviousSecretsCommand`, `TenantSecretMaintenanceJob`, runbook), Auth (X-1 = DL-5), Inventory
(X-5 = FU-0179-VAULT).

### 1.3 XMC-001
DL-2 için başka modül dosyası **gerekmez** (paket §5.1: X-1 DL-5, X-2 / X-3 DL-1 ve DL-3, X-5 FU-0179-VAULT). Aşağıdakilerden birine dokunmak
gerekirse **dur ve CT'ye yaz — sahip onayı gerekli**:

| Dosya | Neden gerekebilir | Sahip |
|---|---|---|
| `services/Diten.Platform/src/Diten.Platform.API/Middleware/GlobalExceptionHandler.cs` | yapılandırılmamış servis şemasının 400 dönmesi (§2 "401 gövdesi") | Platform ortak (CT) — sahip onayı gerekli |
| `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs` (kasa şeması satırları dışı) ve `UsePlatformAccessPipeline` | 401 gövdesi beklenenden farklı çıkarsa ortak boru hattı | MOD-0033-FU02 / Platform ortak — sahip onayı gerekli |
| `tests/architecture/audit-ledger/Diten.Platform.md`, `AuditTrailStandardTests.cs` | defter testi resolve'u sınıflandırmak isterse | CT (AUD-001 defteri) — sahip onayı gerekli |
| `docs/analysis/contracts/README.md` | sözleşme durum satırı | CT (dondurma adımı) |
| `services/Diten.Building.Blocks/**` | — (korunan yol, MOD-0012 ana paketi) | merkez — sahip onayı gerekli |
| `services/Diten.AuthService/**/ServiceIdentityTokenAudiencePolicy.cs` | — (X-1, DL-5, Q-3) | MOD-0033-FU02 (Ali) — sahip onayı gerekli |
| `services/Diten.InventoryService/**` | — (FU-0179-VAULT) | MOD-0179 (merkez) — sahip onayı gerekli |

## 2. Kararlar (bağlayıcı)
- **Q-1, Q-2, Q-4, Q-13 kapandı** (sahip Beste, `mvp5-wms-ct-owner-decisions-2026-11-13.md:97-103`). **Q-4:** canlıda servisler arası TLS zorunlu,
  geliştirme makinesinde değil, mTLS sonraki iş → DL-2 kodu TLS zorlaması yazmaz (paket AC'si yok); resolve'un **canlı** kullanımı TLS'li ortam
  kanıtına bağlıdır (§4). **Q-3 açık** (DL-5 kapısı; DL-2'yi bekletmez). **Q-8:** resolve denetlenmez, yalnız değer içermeyen sayaç.
- **DL-1 sapmaları (CT kabulü, kabul kaydı `:133-135`) DL-2'de de geçerli:** özel test host'u + kompozisyon sabitleme (`TenantSecretHttpHost`,
  gerçek `Program.cs` süreç başına bir kez kurulabildiği için), SourceService = `Diten.Platform`, 404 / 409 ret kayıtları. Resolve testleri aynı
  host'u kullanır; host'un üretim kayıtlarını (`AddTrustedServiceTokenValidation`, `AddTrustedSecretVaultConsumer`, `AddTenantSecretVault`,
  `UsePlatformAccessPipeline`, `GlobalExceptionHandler`) çağırdığı kompozisyon testi yeşil kalır.
- **401 gövdesi (DL-1 kabulü `:134`, `:172`).** Paket §8.7: 401 **gövdesiz**. DL-1 testi yalnız `Reason == null` ölçtü. DL-2'de:
  (a) **Test host'u** (şema yapılandırılmış: `TenantSecretHttpHost.cs:117-119` — `TrustedServiceTokenValidation:Issuer`, `:CurrentKeyId`,
  `:CurrentPublicKeyPem`, çalışma anında üretilen test anahtarı): beş ucun her biri için jetonsuz, yanlış audience'lı ve insan (`platform_admin`)
  jetonlu istek → 401, **ham gövde uzunluğu 0**, `WWW-Authenticate: Bearer` var. Gövde boş değilse ortak boru hattını **değiştirme**; ölçümü
  ve kaynağını CT'ye yaz (paket metni mi hizalanır, kodda mı düzeltilir — CT kararı, XMC olabilir).
  (b) **Dev ortamı (CT adımı, geliştirici değil):** bugün dev Platform'da `TrustedServiceTokenValidation` yok; jetonsuz istekte JwtBearer seçenekleri
  `CreateTokenValidationParameters`'ta `InvalidOperationException("Trusted service token issuer configuration is invalid.")` atar ve
  `GlobalExceptionHandler` bunu 400'e çevirir (`TrustedServiceTokenValidationExtensions.cs:228-230`, `GlobalExceptionHandler.cs:57-60`). Erişim yine
  kapalıdır (fail-closed), ama 401 ölçülemez. Ölçüm için Platform'un **çalıştığı checkout'ta** (dotnet watch ana checkout'tan koşar) şema
  yapılandırılır; repoya ve sohbete hiçbir anahtar yazılmaz:
  - **Yol A (tercih — gerçek zincir, DL-5 / FU-0179-VAULT için de gerekecek):** `scripts/dev/setup-service-trust.ps1 -TenantId <dev kiracı>
    -ApproverUserId <id>` (MOD-0033-FU02 §F.3; `docs/guides/operations/service-trust-chain-runbook.md:40-62`). Git'e girmeyen
    `appsettings.Development.local.json` dosyalarını Auth / Platform / MDM API klasörlerine yazar (Platform'a yalnız public key:
    `TrustedServiceTokenValidation`); hiçbir dosyanın üzerine yazmaz, kısmi durumda durur. **Yan etki:** MDM tarafında ürün kimliği iş akışı
    ayarlarını da açar — bu yüzden çalıştırma kararı sahibindir (Beste). Sonra runbook sırasıyla yeniden başlat: Auth → Platform → MDM → Gateway → Web.
  - **Yol B (yalnız 401 ölçümü, en dar):** yalnız Platform sürecine ortam değişkeni — `TrustedServiceTokenValidation__Issuer`,
    `TrustedServiceTokenValidation__CurrentKeyId`, `TrustedServiceTokenValidation__CurrentPublicKeyPem` — yerelde üretilip özel anahtarı hemen
    atılan tek kullanımlık bir RSA 2048 anahtarın public PEM'i; yalnız Platform yeniden başlatılır; ölçümden sonra değişkenler kaldırılıp Platform
    yeniden başlatılır. (Ortam değişkeni local json'dan ve `appsettings`'ten üstündür — runbook `:53-55`.)
  - Ölçüm: `curl -i -X POST http://localhost:5057/api/internal/v1/tenant-secrets/<rastgele-guid>/resolve` (jetonsuz) ve aynı istek
    `-H "Authorization: Bearer not-a-token"` ile → her ikisi 401, `Content-Length: 0`, `WWW-Authenticate: Bearer`. Aynısı bir yazma ucu
    (`POST …/tenant-secrets`) ve meta ucu için. Dev'de tüketici listesi boş olduğundan geçerli bir servis jetonu bile 403 alır — bu dilimde
    ölçülmez (ölçümü DL-5 sonrası). Kanıt (komut + başlıklar, gövde yok) CT kabul kaydına yazılır.
- **Geçiş penceresi (CT kararı).** §23 DL-2 satırı §8.4'ü kapsar ve §8.4 resolve'un `use = Verify` + pencere içinde `previous` döndürmesini yazar;
  §8.8 sözleşmenin dondurulduktan sonra yalnız ana sürümle kırılabileceğini söyler → `use` ve `previous` **1.0.0 sözleşmesinde ve DL-2 kodunda**
  bulunur. Çekirdek madde 10 ("sabotaj kanıtı olmayan guard, guard değildir") gereği bu okuma DL-2'de ölçülür: vaka `TenantSecretsResolveHttpMongoTests.
  VerificationGrace_ReadSide` (DL-1'in mevcut döndürme ucuyla `InboundSigningSecret` 1..72 saat pencereli döndür → `Verify` önceki değeri bayt-eşit
  `previous` olarak döner, `Use` dönmez; sahte saatle pencere bitince `Verify` de dönmez) ve **S-22a erken uygulanır**. AC-22'nin **kabulü
  DL-3'te kalır** (temizlik komutu, denetim satırı, `ApiKey` 422, 72 üstü 400 — S-22b); DL-3 bu vakayı yeniden koşar. Paket metni bir sonraki içerik
  revizyonunda hizalanır.
- **Sayaç (paket §21, Q-8; AC'si yok).** `tenant_secret_resolve_total` yalnız `owner_module` ve `outcome` (ör. `ok`, `not_found`, `revoked`,
  `expired`, `key_unavailable`, `integrity_failed`, `consumer_not_allowed`, `validation_failed`) etiketlerini taşır; `vaultRef`, kiracı, ad, değer
  yok. Platform'da metrik dışa aktarıcısı bugün yok (`ObservabilityServiceCollectionExtensions.cs` yalnız tracing) — dışa aktarıcı eklenmez; ölçüm
  `MeterListener` ile test içinde (§5, S-E1).
- **Span ölçümü.** Platform'da test dışa aktarıcısı emsali yok; yeni paket eklenmez. AC-18'in "OTel span öznitelikleri" ölçümü test sürecinde
  `System.Diagnostics.ActivityListener` (tüm kaynaklar, `ActivitySamplingResult.AllDataAndRecorded`) ile yapılır: resolve sırasında oluşan her
  `Activity`'nin etiketleri, olayları ve baggage'ı taranır. Nöbetçi rastgele olduğundan paralel testlerin span'ları sonucu bozmaz.

## 3. Kurallar
- Kiracı: servis jetonunun `tenant_id`'si; gövdede kiracı yok; başka kiracı ve başka tüketici modül **404**, gövde var olmayan kayıtla bayt-eşit.
- Yetki: `[HasPermission]` yok (paket §14, emsal `InternalTrustedWorkflowConsumerController`); resolve yalnız servis şeması + kodda sabit liste.
  İnsan jetonu (`platform_admin` dahil) ve başka audience 401. Temsilî kullanıcı başlığı resolve'da kullanılmaz.
- Değer: yalnız resolve 200 gövdesinde; logda (tüm seviyeler), span'da, denetim tablolarında, idempotency deposunda, hata cevabında, istisna
  iletisinde yok. Resolve gövdesi ve istek gövdesi loglanmaz.
- Gateway: rota yok; Web çağırmaz; tüketici Platform'u doğrudan çağırır (5057).
- Dil: ekran yok (Q-7) → resx yok; Platform modülü 2 dil kuralı bu dilimde kullanıcı metni doğurmaz.
- Korunan yollar: `.antigravity/**`, `ocelot.json` (yalnız integration-agent), `services/Diten.Building.Blocks/**`, `services/Diten.AuthService/**`,
  diğer domain servisleri, `frontend/Diten.Web/**`, `appsettings*.json`, karar kayıtları, paket dosyası. `Program.cs`'e dokunma.
- Git: yalnız açık yolla stage (`git add -A` / `git add .` yok); hook atlanmaz (sır tarayıcısı test anahtarını işaretlerse atlatma, CT'ye yaz);
  `main`'e commit yok; push / PR yok. Commit mesajı paket ve WP kimliğini taşır.

## 4. Kabul kapıları
- **DL-2 başlangıç kapısı:** DL-1 kabulü — **kapalı** (VERIFIED, 2026-11-14). DL-2'yi bekleten açık kapı yok.
- **DL-2 kabulü (CT):** §5 kanıtları + CT'nin kendi filtresiz koşusu (ayrık worktree) + §2 "401 gövdesi" (b) dev ölçümü (Yol A ya da B, sahip
  seçer) + sözleşme dosyası incelemesi.
- **Canlı kullanım kapısı (Q-4, sahip kararı):** resolve canlı ortamda yalnız servisler arası TLS ile; kanıt canlıya alma adımında (bu dilimin kodu
  değil).
- **DL-2'nin açtıkları (paket §24):** DL-3 (DL-1 + DL-2 kabulü); FU-0179-VAULT için gereken parçalardan biri — FU-0179-VAULT kapısı ayrıca
  DL-5 (X-1, Q-3), `TENANT-SECRET-VAULT 1.0.0`'ın **CT tarafından dondurulması**, Q-3 dev provisioning ve Q-4 ister.

## 5. Test ve kanıt
- **Filtresiz tam koşu** (dilim başı ve sonu, sayılarla; "atlanan test sıfır"):
  - `services/Diten.Platform/tests/Diten.Platform.Application.Tests` — `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`;
    27017 kullanılmaz. Taban: CT `15fcfe67c` koşusu **9509 / 9490 geçti / 19 kırmızı / 0 atlandı** (`mvp1-ct-acceptance-batch-2026-11-13.md:169-170`);
    19 kırmızı Mongo ortam kırmızısıdır ve **adları aynı** olmalı (yeni kırmızı 0, kaybolan adı raporla). Dilim başında farklı kırmızı varsa adıyla
    yaz (CT aynı gün main ile karşılaştırır).
  - Paket AC-28 gereği: `tests/architecture/TenantArchitecture.ArchitectureTests` (denetim defteri dahil; DL-1 tabanı 229 / 5) ve
    `gateway/Diten.ApiGateway.Tests` (DL-1: 384 / 0; Gateway dosyası değişmez, AC-14 resolve yollarını zaten ölçüyor).
  - `Diten.Platform.BackgroundJobs.Tests`, `Diten.Platform.CompositionGuard.Tests`, `Diten.Platform.Eventing.Tests` — yalnız o projelere ya da
    `Program.cs` / DI kayıtlarına dokunulursa (bu dilimde beklenmez; dokunduysan koş ve raporla).
  - `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.
- **Build:** `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` PASS.

### 5.1 AC → test (paket §17.1 adları)

| AC | Test | DL-2 bacağı |
|---|---|---|
| AC-3 (b) | `TenantSecretsResolveHttpMongoTests.ValueAadFieldTampered_FailsIntegrity` | yalnız değer AAD'sine giren bir alan (`Name`, `ScopeRef` ya da `SecretVersion`) Mongo'da değişir → resolve 500 `SECRET_INTEGRITY_FAILED`, değer yok, hata logunda kayıt kimliği |
| AC-3 (d) | `TenantSecretsResolveHttpMongoTests.TransplantedEnvelope_FailsIntegrity` | X'in `Cipher`'ı aynı kiracıda Y'ye kopyalanır → Y resolve 500, değer yok |
| AC-7 | `TenantSecretsResolveHttpMongoTests.Revoked_Resolve409` | iptal edilmiş kayıt → 409 `SECRET_REVOKED`, değer yok |
| AC-9 | `TenantSecretsResolveHttpMongoTests.CrossTenant_And_SeededForeignOwner_Resolve404_ByteEqual` (ya da DL-1 vakasına resolve eklenir — adını raporla) | B jetonuyla A'nın `vaultRef`'i ve tohumlanmış `MOD-9999-TEST` belgesi → 404, gövde var olmayan `vaultRef` ile bayt-eşit |
| AC-15 | `TenantSecretDtoShapeTests.NoDtoCarriesValueOrEnvelope` (güncellenir) | resolve DTO'su adıyla tek istisna, özellik kümesi tam eşit |
| AC-16 | `TenantSecretsResolveHttpMongoTests.Resolve_ReturnsExactBytes_NoStore` | 200, `{ value, secretVersion, expiresAt }`, `Cache-Control: no-store`; çok satırlı PEM, UTF-8 çok baytlı karakter ve 16384 baytlık değer bayt-eşit |
| AC-17 | `TenantSecretsResolveHttpMongoTests.UnauthorizedReaders_Refused` | insan jetonu (`platform_admin` dahil) / workflow audience / jetonsuz → 401 (ham gövde boş); liste dışı istemci ya da servis adı → 403 `SECRET_CONSUMER_NOT_ALLOWED`; başka kiracı / başka modül → 404; iptal → 409 `SECRET_REVOKED`; süresi geçmiş (`MutableClock`) → 409 `SECRET_EXPIRED`; hiçbirinde değer yok |
| AC-18 | `TenantSecretLeakScanTests.ReadPath_SentinelAbsentFromLogsSpansAudit` | 48 karakterlik rastgele nöbetçiyle yaz → resolve (200) + resolve hata yolları (404, 409, enjekte 500) → nöbetçi (UTF-8 ve base64) test log havuzunda, span etiket / olaylarında, `audit_outbox` / `audit_events`'te ve hata cevaplarında yok; resolve **yeni denetim satırı yazmaz** (Q-8: sayı değişmez) |
| AC-19 | `TenantSecretVaultContractTests` | YAML'daki beş uç (yol + metot), istek / cevap alan adları, §8.7 kodlarının tamamı (= `TenantSecretErrorCodes` sabitleri, HTTP durumlarıyla), başlıklar (`Idempotency-Key`, `X-Delegated-Authorization`, resolve 200 `Cache-Control`) üretim controller'ı (yansıma: rota şablonları, HTTP metotları) ve DTO'larıyla eşleşir; `x-consumer-cache-max-seconds: 300` var. Emsal `ESignature/ESignatureContractTests.cs` |
| 401 gövdesi | `TenantSecretsResolveHttpMongoTests.Unauthenticated_401_HasNoBody_OnEveryEndpoint` | beş uç × (jetonsuz, yanlış audience, insan jetonu) → 401, gövde uzunluğu 0, `WWW-Authenticate: Bearer` (§2) |
| Geçiş penceresi (okuma) | `TenantSecretsResolveHttpMongoTests.VerificationGrace_ReadSide` | §2 "Geçiş penceresi" — erken ölçüm; AC-22 kabulü DL-3 |
| Sayaç | `TenantSecretsResolveHttpMongoTests.ResolveCounter_CarriesOnlyOwnerModuleAndOutcome` | `MeterListener`: her sonuçta bir artış; etiket kümesi tam olarak `{owner_module, outcome}`; etiket değerlerinde nöbetçi / `vaultRef` yok |

Testler yalnız üretim koduna karşı ölçer (gerçek controller, kapı, işleyici, `AesGcmTenantSecretCipher`, depo, gerçek replica set); kural
kopyası yazılmaz. Mongo'ya doğrudan müdahale (alan kurcalama, transplant, yabancı sahip tohumu) yalnız test kurulumu içindir.

### 5.2 Sabotajlar
Her biri ayrık worktree'de: uygula → hedef vaka kırmızı → bayt-eşit geri al → yeşil; `git status` temiz.

| Sabotaj | Uygulama | Beklenen kırmızı |
|---|---|---|
| S-3a (b bacağı) | `ITenantSecretCipher` değer AAD'sini boş geçir (`ValueAad` boş bayt) | AC-3 (b) vakası (DL-1'de (a) kırmızısı zaten kayıtlı) |
| S-3d (türetilmiş — paket AC-3 (d) için tek başına S-3a / S-3b yetmez: sarma AAD'si `Id`'yi, değer AAD'si de `Id`'yi bağlar) | değer AAD'si **ve** sarma AAD'si birlikte boş | AC-3 (d) vakası; ayrıca yalnız S-3b ile (d)'nin hâlâ **yeşil** kaldığını kayda geç (yalıtım kanıtı) |
| S-7 | iptali `DeleteOne` ile yap | AC-7 resolve vakası (404 döner, 409 beklenir) |
| S-9a | depodaki `TenantId` süzgecini kaldır | AC-9 resolve kiracı vakası |
| S-9b | depodaki `OwnerModule` süzgecini kaldır | AC-9 resolve tohumlanmış belge vakası |
| S-15 | meta DTO'ya `Cipher` ekle **ve** ayrıca resolve dışındaki bir DTO'ya `Value` ekle (iki ayrı uygulama) | güncellenmiş AC-15 testi ikisinde de kırmızı |
| S-16 | `Cache-Control: no-store` başlığını kaldır | AC-16 |
| S-17a | resolve ucunun şemasını varsayılan kullanıcı JWT şemasına çevir (uygulanan biçimi raporla) | AC-17 insan jetonu vakası |
| S-17b | resolve'daki süre denetimini kaldır | AC-17 süresi geçmiş vakası |
| S-18 | resolve sırasında `Activity.Current`'a cevap gövdesini (ya da değeri) etiket olarak ekle | AC-18 span taraması |
| S-19 | bir cevap alanının adını değiştir (ör. `secretVersion`) | AC-19 |
| S-22a (erken) | okuma anındaki pencere sonu denetimini kaldır | `VerificationGrace_ReadSide` sahte saat vakası |
| S-E1 | sayaca `vaultRef` etiketi ekle | sayaç etiket kümesi vakası |
| S-E2 | `TrustedSecretVaultConsumerService` şemasına gövde yazan bir `OnChallenge` ekle (yalnız geçici worktree; üretimde bu dosyanın kasa satırları dışına dokunulmaz) | 401 gövdesi vakası |

S-28 bu dilimde uygulanmaz (yeni yazma komutu ve defter satırı yok); AC-28 tam koşuyla ölçülür. Sabotaj tarifi kodla uyuşmazsa eşdeğerini uygula ve
tabloya "eşdeğer" notuyla yaz (DL-1 emsali).

## 6. Rapor
Commit sha'ları (sözleşme commit'i ayrıca, varsa); paket LF sha ölçümü; AC-3 (b) / (d), AC-7 (resolve), AC-9 (resolve), AC-15, AC-16…AC-19, 401
gövdesi, geçiş penceresi okuma vakası ve sayaç için test adı + sonuç; sabotaj tablosu (S-3d yalıtım notu dahil); filtresiz takım sayıları (önce / sonra)
ve 19 taban kırmızısının ad karşılaştırması (yeni 0 / kaybolan); TenantArchitecture ve Gateway sayıları; sözleşme dosyasının LF sha256'sı ve
CT dondurmasına giden açık sorular (ör. `previous` şekli, 401'in sözleşmedeki yazımı); ek dosyalar ve §1.2'de "değişiklik beklenmez" denen bir
dosyaya dokunulduysa gerekçesi; DL-1 koduna dokunulduysa hangi davranışın değiştiği (beklenen: hiçbiri); test host'unda ölçülen 401 ham gövdesi
(boş değilse kaynağı — düzeltme yapılmadan); hattı alma sırasında çakışma olduysa nasıl çözüldü; açık noktalar.
