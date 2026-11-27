# WP-0012-FU01-DL1 — Kiracı Sır Kasası DL-1: çekirdek (kayıt, zarf şifreleme, yaz / döndür / iptal, meta, işlem içi denetim)

Tarih: 2026-11-13 · Veren: CT (merkez, Beste adına) · Sahip kararı: "DL-1'i başlatalım" (Beste, 2026-11-13; Ali soruları kabul kapısıdır,
başlamayı bekletmez — `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:11-13`) · Paket:
`execution/domains/platform-shared-services/module-packs/MOD-0012-FU01-tenant-runtime-secret-vault.md` r3, LF sha256
`22f7e11edeb686bc529bfd79c37b80dabff1135ef4745814dcf8cccac590d12b` · Onay: `execution/approvals/approvals.tsv:95` (Ali / GMG-ERP, 2026-11-13, HIGH) ·
İnceleme: `docs/records/reviews/MOD-0012-FU01/2026-11-13-22f7e11e.md` (r3 UYGUN; F-1 CT kararıyla kapandı).

## 0. Başlamadan
- Dal: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`. CT etiketi `ct/line-ff-2026-11-13q`. Etiket dalının atası ise devam;
  değilse `git merge --ff-only ct/line-ff-2026-11-13q`; olmazsa `git merge --no-ff ct/line-ff-2026-11-13q`; çakışmada `git merge --abort` ve CT'ye yaz.
- Paketin LF sha256'sını ölç (`tr -d '\r' < <paket> | sha256sum`); `22f7e11e` ile başlamıyorsa dur, CT'ye yaz.
- **Eşzamanlılık:** Platform'da MOD-0040-FU02 DL-3 yeni bitti (hatta, `07ad98464`). Bu iş ile MOD-0049-FU01 DL-1'in Platform kısmı (X-4 / X-5 / X-6:
  `RecordCodeTypeCatalog`, `TrustedRecordCodeCredentialAuthenticator`, `RecordCodeTypeCatalogTests`) **aynı anda** Platform'a dokunur. Ortak dosyalar:
  `RecordCodeTypeCatalogTests.cs`, `tests/architecture/audit-ledger/Diten.Platform.md`, `AuditTrailStandardTests.cs` (AcceptedTrails), Platform test
  projesi. Ana checkout'ta derleme / test yok (dotnet watch ana checkout'u kilitler); derleme, test ve sabotaj ayrık bir worktree'de. Her commit'ten
  hemen önce hattı al (yukarıdaki ata / ff / no-ff kuralı); çakışmada **iki tarafın satırlarını da koru** (ör. defter belirteç listesi, AcceptedTrails
  sabit satırı), sonra tam takımı yeniden koş ve CT'ye yaz.
- Paketi oku: §2.3, §3, §4 (4.1–4.3), §5 ve §5.1 (X-2, X-3), §6, §8.1–8.5, §8.7, §8.9, §10, §12, §13, §14, §15, §16 DL-1 (AC-1…AC-15) ve AC-28,
  §17, §17.1, §21, §23, §24. Karar kaydı `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md:55-71`.

## 1. Kapsam (paket §5 / §23 DL-1 satırı; dışına çıkma)
- Domain: `Entities/TenantSecrets/` (`TenantSecret`, `TenantSecretCipherEnvelope`, `TenantSecretCommandRequest`), `Repositories/ITenantSecretRepository.cs`.
- Application `Features/TenantSecrets/`: üç yazma komutu (`Write`, `Rotate`, `Revoke`), `GetTenantSecretMetadataQuery`, işleyiciler, doğrulayıcılar,
  `Ports/` (`ITenantSecretCipher`, `IKeyEncryptionKeyProvider`), `TenantSecretAuditIntent` (statik niyet sınıfı), `TenantSecretErrorCodes`,
  `TenantSecretsModels`. **DL-2 / DL-3 dosyaları yazılmaz:** `ResolveTenantSecretQuery`, `RewrapTenantSecretsCommand`,
  `PurgeExpiredPreviousSecretsCommand`, `TenantSecretMaintenanceJob`, sözleşme YAML'ı, runbook.
- Infrastructure: `AesGcmTenantSecretCipher` (§10 iç yüzeyleri; KEK açık parametre), `EnvironmentKeyEncryptionKeyProvider`,
  `TenantSecretVaultOptions` + `TenantSecretVaultOptionsValidator` (`ValidateOnStart`), `TenantSecretRepository` + §4.3 index'leri, DI kayıtları.
- API: `InternalTenantSecretsController` (yalnız DL-1'in dört ucu: yaz, döndür, iptal, meta), `TrustedSecretVaultConsumerRequestExecutor`,
  `TrustedSecretVaultConsumerAuthorizationOptions` + doğrulayıcısı (kodda sabit tek giriş MOD-0179, §8.3), yeni servis şeması / audience sabiti;
  temsilî kullanıcı için **mevcut** `TrustedWorkflowDelegatedUser` şeması (yeni şema yok); `Program.cs`'te yalnız şema kaydı.
- X-3: `TransactionOwnedAuditCommands.Authorized`'a DL-1'in **üç** komutu + `TransactionOwnedAuditCommandGuardTests` beklentisi (DL-3 iki komutu kendi
  diliminde ekler; AC-10'daki "beş komut" DL-1'de üç olarak ölçülür).
- X-2: denetim defteri + AcceptedTrails (§2 F-1 kararı).
- Testler: `Diten.Platform.Application.Tests/TenantSecrets/**` (+ `TenantSecretCipherVectors.json`), `gateway/Diten.ApiGateway.Tests/Mod0012Fu01InternalSecretVaultRouteTests.cs`.
- **Yok:** Gateway rotası, ekran, yeni `[HasPermission]` anahtarı, Auth değişikliği (X-1 = DL-5), `appsettings*.json`'a tüketici girişi ya da KEK.

## 2. Kararlar (bağlayıcı)
- **Ali soruları (sahip: Beste, geliştirme beklemez):** Q-1 (KEK yeri / erişimi), Q-2 (yedek / kurtarma), Q-3 (Inventory servis istemcisi), Q-4
  (servisler arası TLS), Q-13 (FU onayı ana paket durumunu bekler mi) — **kabul kapısıdır** (§4). DL-1 paketteki önerilerle yazılır: KEK ortam
  değişkeni + `IKeyEncryptionKeyProvider` dikişi (Q-1), KEK yedeği Mongo yedeğinden ayrı (Q-2). Paket §24'teki "DL-1 başlamadan Q-1 / Q-2 / Q-13"
  koşulu bu sahip kararıyla kabul kapısına taşındı. Not: pakete göre Q-3 DL-5'in, Q-4 DL-2 canlı kullanımın kapısıdır; DL-1 kabulünde durumları kayda geçer.
- **Q-5 (CT, kapandı):** denetim yolu `platform-islem-ici`; X-3 kümesi ve X-2 belirteçleri bu dilimde.
- **İnceleme r3 F-1 (CT kararı, kararlar 01 :68-71) — X-2 tek adımdır:** `tests/architecture/audit-ledger/Diten.Platform.md` `platform-islem-ici`
  satırına `TenantSecretAuditIntent.EnqueueAsync/TenantSecretAuditIntent.EnqueueRefusalAsync` + ölçüm notu (MOD-0040-FU02 notu emsali) **ve**
  `AuditTrailStandardTests.AcceptedTrails` içindeki `platform-islem-ici` sabit satırı **aynı commit'te** güncellenir
  (`AcceptedTrails_ArePinned_ALedgerCellCannotGrantCredit` ikisini eşit ister). Bu commit DL-1 kodundan önce ya da onunla aynı commit'tir; ara
  commit'te tam takım kırmızı kalmaz. Paket metni bir sonraki içerik revizyonunda hizalanır.
- **Q-12 (CT):** domain-config "Vault" satırı (`execution/domains/platform-shared-services/domain-config.md:56`) CT adımıdır; geliştirici dokunmaz.
- **Q-6…Q-9, Q-14, Q-15 (sahip):** paketteki gibi. DL-1'de geçiş penceresi alanları varlıkta ve döndürme doğrulamasında vardır
  (`previousValidForHours`, `SECRET_GRACE_NOT_ALLOWED`); pencerenin okunması ve temizliği DL-2 / DL-3'tür.
- **Dilim sınırı (r3 F-1):** AC-3 (b) / (d), AC-7'nin resolve bacağı ve AC-9'un resolve vakası DL-2 kabulünde ölçülür; DL-1'de yazılmaz.
- **Dev ortamı (CT):** `appsettings*.json`'a tüketici girişi ve KEK yazılmaz. Tüketici listesi boşken Platform KEK'siz başlamaya devam eder (§8.2
  son cümle) — Platform dev'de ve dotnet watch'ta ayakta kalmalı; bunu bir başlangıç vakasıyla ölç. Testler KEK'i ve tüketici girişini
  `WebApplicationFactory` yapılandırmasıyla verir; test vektörü dosyasındaki ana KEK yalnız test değeridir ve öyle adlandırılır.

## 3. Kurallar
- Kiracı: kiracı servis jetonunun `tenant_id`'sinden; gövdede kiracı alanı yok. Başka kiracı ve başka tüketici modülün kaydı **404**
  `SECRET_NOT_FOUND`, gövde var olmayan kayıtla bayt-eşit (AC-9). Silme yok: iptal soft (`IsDeleted`, `DeletedAt`).
- Yetki: iç uçlarda `[HasPermission]` yok — paket §14 kararı (iç uç servis şemasıyla korunur, emsal `InternalTrustedWorkflowConsumerController`).
  Yetki = kodda sabit tüketici listesi + yazmada temsilî kullanıcının girişteki izni (`warehouse.third-party-logistics.channels.configure`).
  İnsan jetonu (platform_admin dahil) bu uçlara geçemez.
- Değer: düz değer Mongo'da, idempotency deposunda, denetimde, logda, istisna / doğrulama iletisinde, hiçbir HTTP cevabında yok (§2.3, §8.4).
  `SensitiveSecretValue.ToString()` → `[REDACTED]`, JSON'a yazımda istisna; komut kayıtları `PrintMembers`'ı geçersiz kılar.
- Denetim: işlem içi (`ITransactionalAuditOutboxWriter` her işleyicinin kurucusunda; niyet sınıfı servis olarak enjekte edilmez); retler
  `EnqueueRefusalAsync` ile kendi kısa işleminde; outbox yazılamazsa 503 `SECRET_AUDIT_UNAVAILABLE`, kayıt değişmez.
- Dil: v1'de ekran yok (Q-7) → resx yok; iç API hata iletisi teknik İngilizce, kullanıcıya gösterilmez; kod dil bağımsız.
- Korunan yollar: `.antigravity/**`, `ocelot.json` (yalnız integration-agent; AC-14 sabotajı yalnız geçici worktree'de), `services/Diten.Building.Blocks/**`,
  `services/Diten.AuthService/**` (X-1 DL-5'tir), diğer domain servisleri, `frontend/Diten.Web/**`, `appsettings*.json` mevcut anahtarları, karar kaydı.
  `Program.cs`'te başka modüllerin satırları değişmez.
- XMC-001: §5.1 dışındaki bir dosyaya dokunmak gerekirse dur ve sor. Yalnız açık yolla stage (`git add -A` / `git add .` yok); hook atlanmaz
  (sır tarayıcısı test vektörünü işaretlerse atlatma, CT'ye yaz); push / PR yok.

## 4. Kabul kapıları (başlamayı bekletmez)
- Ali: Q-1, Q-2, Q-3, Q-4, Q-13 cevapları DL-1 kabulünden önce kayda geçer (CT sorar; cevap paketteki öneriden farklıysa CT düzeltme dilimi açar).
- CT: Q-12 domain-config satırı; X-2 defter commit'inin ölçümü (tam takım yeşil).

## 5. Test ve kanıt
- Filtresiz tam koşu: `Diten.Platform.Application.Tests`, `Diten.Platform.BackgroundJobs.Tests`, `Diten.Platform.CompositionGuard.Tests`,
  `Diten.Platform.Eventing.Tests`, `tests/architecture/TenantArchitecture.ArchitectureTests` (denetim defteri dahil) ve `gateway/Diten.ApiGateway.Tests`.
  Mongo testleri gerçek replica set: `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`; 27017 kullanılmaz.
  Dilim başı ve sonu sayılarını raporla; dilim başında kırmızı olanları adıyla yaz (CT aynı gün main ile karşılaştırır).
- Build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` PASS.
- AC-1, AC-2, AC-3 (a) ve (c), AC-4, AC-5, AC-6, AC-7 (meta bacağı), AC-8, AC-9 (resolve hariç), AC-10 (üç komut), AC-11, AC-12, AC-13, AC-14, AC-15 —
  §17.1'deki test adlarıyla. Kripto testleri yalnız üretim `AesGcmTenantSecretCipher`'ı çağırır.
- Sabotajlar paketteki adlarla: S-1, S-2a, S-2b, S-3a (yalnız (a) bacağı), S-3b, S-4, S-5, S-6, S-7, S-8a, S-8b, S-9a, S-9b, S-10a, S-10b, S-10c, S-11a,
  S-11b, S-12a, S-12b, S-13, S-14 (geçici worktree), S-15; ayrıca S-28 (defterden bir DL-1 komutunu düşür → defter testi kırmızı). Her biri:
  uygula → hedef vaka kırmızı → bayt-eşit geri al → yeşil; `git status` temiz.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.

## 6. Rapor
Commit sha'ları (X-2 defter commit'i ayrıca); AC-1…AC-15 her biri için test adı ve sonuç (DL-2'ye kalan bacaklar adıyla "DL-2"); sabotaj tablosu;
filtresiz takım sayıları (önce / sonra) ve taban kırmızıları; KEK'siz dev başlangıcı ölçümü; MOD-0049-FU01 Platform kısmıyla çakışma olduysa nasıl
çözüldü; açık noktalar (Q-1…Q-4, Q-13 için paketteki önerinin kodda nasıl uygulandığı — CT Ali'ye bununla gider).
