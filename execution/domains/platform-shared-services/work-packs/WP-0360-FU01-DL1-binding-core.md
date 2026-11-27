# WP-0360-FU01-DL1 — Tedarikçi Kullanıcı Bağı DL-1: bağ kaydı (kur / kaldır / kod düzelt, uygunluk okuması, tedarikçi hesabı, K2 denetim izi)

Tarih: 2026-11-13 · Veren: CT (merkez, Beste adına) · Sahip kararı: "Natig'i beklemeden ilerleyelim, o bizim yaptığımıza göre yapsın"
(Beste, 2026-11-13 akşam — `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md`, son bölüm: SQ-7, SQ-8, SQ-11,
SQ-14 kapandı; **S0 kapalı**; MOD-0140 DL-3 DL-1'in başlama kapısı değil **kabul kapısı**) · Paket:
`execution/domains/platform-shared-services/module-packs/MOD-0360-FU01-supplier-user-binding.md` r2, LF sha256
`8086bf9aefa3f8e7b3db664893149d372def69153d1552cb8149b3d05aaa2d4a` · Onay: `execution/approvals/approvals.tsv:93` (Ali / GMG-ERP «onayla»,
2026-11-13, HIGH; CT-APR-01) · İnceleme: `docs/records/reviews/MOD-0360-FU01/2026-11-13-8086bf9a.md` (r2 UYGUN, CT-APR-02; tek düşük
bulgu F-1 DL-2'nin işi) · Servis: `Diten.AuthService` (5056; dışarıya yalnız Gateway 5000).

## 0. Başlamadan
- Dal: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`. CT etiketi `ct/line-ff-2026-11-13s`. Etiket dalının atasıysa devam et;
  değilse `git merge --ff-only ct/line-ff-2026-11-13s`; olmazsa `git merge --no-ff ct/line-ff-2026-11-13s`; çakışmada `git merge --abort` ve CT'ye yaz.
  Etiket yoksa dur, CT'ye yaz.
- Paketin LF sha256'sını ölç: `MSYS_NO_PATHCONV=1 git show HEAD:execution/domains/platform-shared-services/module-packs/MOD-0360-FU01-supplier-user-binding.md | sha256sum`;
  `8086bf9a` ile başlamıyorsa dur, CT'ye yaz.
- Sözleşme: `docs/analysis/contracts/supplier.openapi.yaml` sürüm **1.3.0**, `x-producer-status: FROZEN 2026-10-29`, LF sha256
  `7204e6d4d795218ce1560d6e6a41ee51b709c69830831606c59fc7730bfd7035` (2026-11-13 ölçümü, `cfd917df3`). Farklıysa dur, CT'ye yaz. Bu dosya
  **yalnız okunur** (paket §6).
- **Eşzamanlılık (2026-11-13 ölçümü):** Auth'ta hatta ve `origin/main`'de son değişiklik `353090ca5` (2026-11-03); bugün açık Auth işi
  MOD-0018 tarafında **Ali'de** (merkez dışı). Platform'da MOD-0012-FU01 DL-1 (sır kasası) ve MOD-0049-FU01 DL-1 (MDM iş ortağı, Platform
  kısmı) sürüyor. Ortak dosyalar: `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` (`AcceptedTrails`),
  `tests/architecture/audit-ledger/CT-DECISIONS.md`; Auth tarafında Ali'nin işiyle olası çakışma: `MongoDbIndexConfigurations.cs`, Auth
  `DependencyInjection.cs` (Persistence / Infrastructure), `User.cs`, `AccountKind.cs`, `AssignRoleCommandHandler.cs`,
  `tests/architecture/audit-ledger/Diten.AuthService.md`. Başlarken ve her commit'ten **hemen önce**
  `git log --oneline -5 -- services/Diten.AuthService` (hat ve `origin/main`) ölç ve hattı al (yukarıdaki ata / ff / no-ff kuralı); çakışmada
  **iki tarafın satırlarını da koru** (`AcceptedTrails` satırları, defter iz satırları, indeks kayıtları, DI kayıtları), tam takımı yeniden
  koş ve CT'ye yaz. Auth'ta beklemediğin yeni bir değişiklik görürsen (Ali) dur, CT'ye yaz.
- **Ana checkout'ta derleme / test yok** (fleet `dotnet watch` ana checkout'u kilitler). Derleme, test ve sabotaj ayrık bir worktree'de;
  commit tek çalışma dalına.
- Paketi oku: §0.1 (D1–D8), §0.3, §1, §2, §3, §4, §5 (DL-1), §6, §7, §8, §10, §12, §13 (DL-1 bölümü), §14, §16 DL-1 (AC-1…AC-10, AC-26,
  AC-27), §17, §19, §21, §22, §23. Karar dosyasının tamamını oku.

## 1. Kapsam (paket §5 DL-1 satırı; dışına çıkma)
- Domain: `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/SupplierActorBinding.cs`, `.../Domain/Enums/SupplierActorBindingStatus.cs`;
  değişir `.../Domain/Enums/AccountKind.cs` (**`Supplier = 3`**, SQ-1) ve `.../Domain/Entities/User.cs` (yalnız SQ-1'in gerektirdiği kadar).
- Application `Features/SupplierActorBindings/` — paket §10 yapısı birebir (3 komut, 4 sorgu, işleyiciler, 3 doğrulayıcı,
  `SupplierActorBindingsModels.cs`); `Common/Interfaces/ISupplierActorBindingRepository.cs`, `ISupplierEligibilityClient.cs`,
  `ISupplierAccessAuditWriter.cs`, `ISupplierAccessAuditReader.cs`.
- Persistence: `Repositories/SupplierActorBindingRepository.cs`, `SupplierAccessAuditWriter.cs`, `SupplierAccessAuditReader.cs`; değişir
  `Configurations/MongoDbIndexConfigurations.cs` (paket §4'ün üç indeksi, adlarıyla) ve `Persistence/DependencyInjection.cs`.
- Infrastructure: `Services/SupplierEligibilityClient.cs` (Gateway üzerinden, adres yapılandırmadan; port koda yazılmaz); değişir
  `Infrastructure/DependencyInjection.cs` (yalnız istemci kaydı — ikinci kimlik doğrulama şeması DL-2'dir).
- API: `Api/Controllers/SupplierActorBindingsController.cs` (`[Route("api/users/supplier-bindings")]`; `UsersController` rotaları
  `{id:guid}` kısıtlı, çakışma yok — yine de rota testinde ölç).
- XMC-001 (Ali'nin Auth'u; izin aşağıda): `Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs` (tedarikçi hesabına rol
  reddi, AC-26) ve `tests/.../Users/AccountKindCreationPathsGuardTests.cs` (kur komutu yeni oluşturma yoludur; bekçiye yalnız bu yol eklenir).
- İzin anahtarları `auth.supplier-bindings.manage` ve `auth.supplier-bindings.read` (SQ-6) — Auth izin kataloğu tohumunun yerini DL-1 başında
  ölç ve rapora yaz. Yer Auth dışındaysa (ör. Platform manifesti `AccessGovernanceManifestProvider`) ya da frontend etiket defteri
  (rol köprüsü, `perm-label.js` ailesi, vitest label-ledger) yeni anahtar isterse **dur ve CT'ye yaz** (XMC-001).
- Defter (§2 A-1): `tests/architecture/audit-ledger/Diten.AuthService.md`, `AuditTrailStandardTests.cs` (`AcceptedTrails` yeni üçlü),
  `tests/architecture/audit-ledger/CT-DECISIONS.md`.
- Testler: `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SupplierBindings/` (yeni). Auth test altyapısında
  (`Persistence/DisposableAuthMongoReplicaSet.cs` koleksiyon listesi vb.) değişiklik gerekirse yalnız ekleme ve raporda adıyla.
- **XMC-001 Auth izni (ölçüldü):** karar dosyası SQ-5 — "Bölüm 5'teki Auth dosya listesine XMC-001 izni (merkez)"; Platform / Auth işini merkez
  yürütür, Ali'ye bilgi verilir. İzin **yalnız paket §5 listesini** kapsar. Listede olmayan bir Auth dosyasına (ör. `CreateUserCommandHandler`,
  `SetAccountKindCommandValidator`, `UsersController`) dokunmak gerekirse dur, CT'ye yaz.
- **DL-1'de yok:** `Features/SupplierAuth/**`, `SupplierAuthController`, `SupplierBindingRevisionController`, servis token'ı şeması /
  doğrulayıcısı, `TokenService` / `JwtSettings` / `appsettings*.json`, `MfaChallenge.Door`, `RefreshToken` oturum alanları, seçim bileti,
  kiracı kapısı reddi (`LoginCommandHandler` vb.), `[Authorize]`-yalnız uç envanteri (SQ-12 → AC-15, DL-2), SQ-15 hesap pasifleştirme (DL-2),
  Gateway / `ocelot.json` (DL-3), ekran (DL-4).

## 2. Kararlar (bağlayıcı)
- **Sahip, 2026-11-13 (karar dosyası):** M2 (bağ, uygunluk cevabının kanonik `SUP-` kodunu saklar; kod değişirse merkez düzeltir);
  SQ-1 `AccountKind.Supplier = 3` (hesap `Human` kalmaz); SQ-6 ayrı `auth.supplier-bindings.read`, bağa görünen kayıt kodu yok (RCS-001
  "kod yok"), kod düzeltmesini kiracıda `.manage` yetkili hesap yürütür; SQ-13 (1) tedarikçi hesabı kiracı kullanıcı kotasına **sayılmaz**
  (`IUserQuotaClient` koltuğu ayrılmaz; ayrı sayım), (2) davet bağlantısı bugünkü Platform bildirim akışıyla (mevcut
  `ITenantUserInvitationEmailService`, 7 gün), (3) parola bugünkü anonim `/api/users/set-password` + kiracı Web sayfasıyla; F-5 üçlü uygunluk
  (`eligibleForTransactions`, `onboardingStatus = Approved`, `purchasingEnabled = true`; `null` uygun değil).
- **S0 kapalı** (karar dosyası son bölüm). D1–D8 paketteki biçimiyle tabandır.
- **MOD-0140 DL-3 = kabul kapısı:** uygunluk ucu bugün ne `origin/main`'de ne hatta (`services/Diten.ProcurementService/src`'de
  `eligibility` yok, 2026-11-13). DL-1, uygunluk okumasını **donmuş `supplier.openapi.yaml` 1.3.0'a karşı sözleşme ikiziyle** kodlar:
  - Okuyucu yalnız sözleşmeyi bilir: 200 gövdesi `SupplierEligibility` (`:362-372`, zorunlu alanların tamamı), hata gövdesi `Error`
    (`{ error: { code, message, correlationId }, contractVersion }`, `:374-377`). **Hata kodu `error.code`'dan okunur**; bugünkü Procurement
    kodundaki `Errors[]` biçimi okunmaz (MOD-0140 sözleşmeye uyar — Cihan'a iletildi).
  - Eşleme (paket §13; CT): 200 + üç koşul doğru → yaz. 200 + koşul tutmuyor → 422 `SUPPLIER_NOT_ELIGIBLE` (`details.reason` ilk tutmayan:
    `NOT_ACTIVE`, `ONBOARDING_NOT_APPROVED`, `PURCHASING_DISABLED`). 404 + `error.code` ∈ {`UNKNOWN_SUPPLIER`, `UNKNOWN_LEGAL_ENTITY`} → 422
    `SUPPLIER_BINDING_SUPPLIER_UNKNOWN`. 403 → 403 `SUPPLIER_BINDING_ELIGIBILITY_FORBIDDEN`. Diğer her şey → 503
    `SUPPLIER_BINDING_ELIGIBILITY_UNAVAILABLE`: 503 `DEPENDENCY_UNAVAILABLE`, 5xx, zaman aşımı, bağlantı hatası, `supplierId` `SUP-` ile
    başlamayan ya da zorunlu alanı eksik 200 gövdesi, `relationshipExists = false` olan 200 (sözleşme "bu uçta her zaman true" der),
    **`error.code`'u okunamayan 404** (rota yok / `Errors[]` biçimi), 401. **CT önerisi — paket bu dört satırda (404 kodsuz, 401,
    `relationshipExists = false`, 403 gövdesiz) sessiz;** kapalı-başarısız yönde yazıldı, raporda vakalarıyla göster.
  - **Sözleşme ikizi:** sahte Procurement HTTP sunucusu (gerçek `SupplierEligibilityClient` + gerçek `HttpClient` yolu); gövdeleri
    `supplier.openapi.yaml`'dan (1.3.0 örnekleri: `getSupplierEligibility` 200, `UnknownSupplierOrLegalEntity` iki örneği,
    `LegalEntityLookupForbidden`, `DependencyUnavailable`) **test anında dosyadan okur**, elle kopya tutmaz; ayrıca bir test sürümün `1.3.0`
    ve `FROZEN` olduğunu sabitler. Sabotaj: ikizin 404 gövdesini `Errors[]` biçimine çevir → AC-2 404 vakası 503 beklentisiyle kırmızı
    olmalı (yanlış eşleme yakalanır) — okuyucuyu `Errors[]`'a uydurmak değil, sözleşmeye bağlı kalmak ölçülür.
- **A-1 (CT defter kararı, bu talimatla):** yeni Auth izi `auth-tedarikci-erisim-gecmisi`, yol `c`, tür yazıcı, belirteç
  `ISupplierAccessAuditWriter.<önce-yaz yöntemi>` (adı kodla birlikte kesinleşir) — **kabul**, koşulu: kuralın §5.c beş koşulu DL-1'de tek
  tek testle kanıtlanır (önce-yaz `WriteOnceAsync` üzerine, iş yazımı + sonuç satırı aynı Mongo işlemi, okuma arayüzü + geçmiş ucu,
  meta-denetim `IPlatformAuditForwarder` — anahtar boşsa geçmiş ucu 503, güncelleme / silme yok). `Diten.AuthService.md` `## İzler` satırı +
  ölçüm notu, `AcceptedTrails` üçlüsü ve `CT-DECISIONS.md` satırı (tarih 2026-11-13, kapsam "Diten.AuthService", komutlar: üç DL-1 komutu,
  karar: "yol c kabul; §5.c beş koşul DL-1 testleri; Karar: Control Tower") **aynı commit'te**, DL-1 komutlarından önce ya da onlarla aynı
  commit'te; ara commit'te tam takım kırmızı kalmaz. `PinnedCounts` Auth (`Debt 18 / Exceptions 1 / K2Debt 11`) **değişmez**.
- **A-2 (CT, ara dönem riski):** tedarikçi hesabının kiracı kapısından reddi DL-2'dir. DL-1 ile DL-2 arasında kur komutuyla açılıp parolasını
  belirlemiş bir tedarikçi hesabı kiracı kapısından (sıfır rolle) girebilir. Hat push edilmez; yine de DL-1 raporunda ölç: tedarikçi hesabıyla
  `/api/auth/login` bugün ne döner. Dev ortamında gerçek tedarikçi hesabı açma.
- **A-3 (CT, ölç ve sor):** `AccountKind.Supplier` eklenince mevcut `CreateUserCommandHandler.ResolveInitialKind` (`Enum.TryParse`) ve
  `SetAccountKindCommand` yolları `Supplier` değerini kabul edebilir — tedarikçi hesabı kur komutu dışında açılmamalı. Bu iki yolu bir testle
  ölç; kabul ediyorlarsa **dur ve CT'ye yaz** (dosyalar §5 listesinde değil; CT izin verirse yalnız `Supplier`'ı reddeden doğrulayıcı satırı).

## 3. Kurallar
- **Kiracı:** `TenantId` yalnız JWT'den; DTO'da yok. Her repository okuması / yazması `TenantId` filtreli. Başka kiracının bağı tekil /
  geçmiş / kaldır / düzelt → **404** (var olmayan kayıtla aynı gövde); liste yalnız kendi kiracısı. Silme yok: kaldırma soft
  (`IsDeleted`, `DeletedAt`, `Status = Revoked`, `Revision + 1`).
- **Yetki:** her uçta `[Authorize]` + `[HasPermission]` — üç komut `auth.supplier-bindings.manage`, dört sorgu `auth.supplier-bindings.read`.
  Uçlar `actor_type = tenant_user` dışını ayrıca reddeder (emsal `AuthController.cs:112`, sabit `:177`); `supplier_user` / platform token'ı 403.
- **Denetim (K2, §21):** her komut iş yazımından önce deneme satırı; yazılamazsa 503 `SUPPLIER_BINDING_AUDIT_UNAVAILABLE`, iş verisi ve hesap
  yok; iş yazımı + sonuç satırı aynı Mongo işleminde; belirsiz commit'te ikinci mutasyon yok, denetim kimliğiyle okunur.
- **Sırlar ve kişisel veri:** parola, davet bağlantısı / set-password token'ı ve karması, bearer token (Admin'in Procurement'a ilettiği dahil)
  **hiçbir log satırına, denetim satırına, istisna / doğrulama iletisine ya da HTTP cevabına yazılmaz** (OTP ve token DL-2'de de aynı kural).
  E-posta, ad, soyad kiracı dışından gelen kişisel veridir: denetimde yalnız §21'in saydığı alanlar; logda e-posta maskeli ya da yok.
  `HttpClient` istek loglaması `Authorization` başlığını yazmamalı — bir testle ölç.
- **Dil:** v1 ekransız (DL-4 ayrı); API hata iletisi teknik İngilizce, kodlar dil bağımsız; resx yok.
- **Korunan yollar:** `.antigravity/**`; `gateway/Diten.ApiGateway/ocelot.json` (yalnız `integration-agent`, DL-3 — DL-1'de rota gerekmez,
  `/api/users/{everything}` kapsar); `services/Diten.Building.Blocks/**`; `services/Diten.ProcurementService/**`, `Diten.SupplyChainService/**`,
  `Diten.MdmService/**`, `Diten.Platform/**`; `docs/analysis/contracts/supplier.openapi.yaml`; `frontend/Diten.Web/**`; Auth `appsettings*.json`
  (`AccessTokenExpirationMinutes` / `RefreshTokenExpirationDays` dahil); karar kaydı ve paket.
- **XMC-001:** §1'deki listenin dışındaki her dosyada dur ve CT'ye sor.
- **Git:** yalnız açık yolla stage (`git add -A` / `git add .` yok); hook atlanmaz (`--no-verify` yok; sır tarayıcısı test değerini
  işaretlerse atlatma, CT'ye yaz); **push / PR yok**; yıkıcı git komutu yok.

## 4. Test ve kanıt
- **Ortam (Auth test altyapısında ölçüldü):** Auth testleri kendi mongod'unu açar; 27017 kullanılmaz. Üç değişkeni birlikte ver:
  `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"` (`OwnedMongo.Variable`; verilmezse Windows varsayılanı **7.0**
  yolu aranır, bu makinede yok → `[OwnedMongoFact]` testleri sessizce atlanır),
  `DITEN_REQUIRE_OWNED_MONGO=1` (`OwnedMongo.RequireVariable`; mongod yoksa atlama değil kırmızı) ve
  `DITEN_ITEST_MONGOD_BIN_DIR="C:/Program Files/MongoDB/Server/9.0/bin"` (`ServiceClientRegistry.Tests/Support/OwnedMongo.cs` ve
  `Application.Tests/Testing/AccountKindAcceptance.cs` bu değişkeni / PATH'i okur). Rapor **sıfır atlama** göstermeli; atlanan varsa adıyla yaz.
- **Filtresiz tam koşu** (proje dosyasıyla, çözümle değil):
  `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj`,
  `services/Diten.AuthService/tests/Diten.AuthService.ServiceClientRegistry.Tests/Diten.AuthService.ServiceClientRegistry.Tests.csproj`,
  `tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj` (denetim defteri dahil).
  **`Diten.AuthService.AccountKindAcceptanceHost.Tests` KESİNLİKLE koşulmaz** (çözüm düzeyinde `dotnet test` de koşma — onu da içerir).
  Dilim başı ve sonu sayılarını raporla; dilim başında kırmızı olanları adıyla yaz (CT aynı gün `origin/main` ile karşılaştırır).
- Build: `dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug` PASS.
- AC'ler (paket §16 / §17 test adlarıyla): AC-1, AC-2 (her dal ayrı; sözleşme ikiziyle, §2 eşleme tablosunun her satırı), AC-3 (gerçek
  replica set, eşzamanlı iki kur), AC-4 (HTTP, WebApplicationFactory), AC-5 (HTTP; `supplier_user` token'ı 403 dahil), AC-6 (SQ-13: kota
  koltuğu ayrılmaz, davet mevcut servisle gider; aynı e-posta başka kiracıda ayrı hesap), AC-7, AC-8, AC-27 (kur + düzelt; `Draft`, `InReview`,
  `Rejected`, `null`; `purchasingEnabled` `false` / `null`), AC-9 (arıza enjeksiyonu + `AuditTrailStandardTests`), AC-26; AC-10 isteğe bağlı
  (yazılırsa sabotajıyla). Testler üretim işleyicisini / ucunu / repository'yi çağırır; kopya kural yok.
- **Sabotajlar** (paket §16 / §17'deki her AC'nin adlı sabotajı + §2'deki sözleşme ikizi sabotajı + A-1 için "defterden üçlüyü düşür →
  `AcceptedTrails` testi kırmızı"): uygula → hedef vaka **kırmızı** → bayt-eşit geri al → **yeşil**; `git status` temiz. Kırmızıya dönmeyen
  sabotaj = bekçi değil; CT'ye yaz.
- Ek ölçümler: A-2 (tedarikçi hesabıyla kiracı girişi), A-3 (CreateUser / SetAccountKind `Supplier` kabulü), izin kataloğu tohumunun yeri,
  Admin rolünde `procurement.suppliers.eligibility.read` (ya da `.read`) ve `mdm.legal-entities.read` var mı (paket §7, rol yapılandırması).

## 5. Kabul kapıları (başlamayı bekletmez; DL-1 ACCEPTED için)
- **MOD-0140 DL-3 (Cihan):** uygunluk ucu `origin/main`'de **ve** Gateway 5000 üzerinden canlı kanıt: gerçek 200, gerçek 404 `UNKNOWN_SUPPLIER`
  (`error.code` sözleşme yerinde), kabul edilmeyen token reddi. Ardından DL-1 canlı kanıtı (paket §17, E4): Gateway üzerinden gerçek
  MOD-0140 ucuna karşı bir başarılı kur ve bir 422. MOD-0140 hâlâ `Errors[]` dönerse canlı 404 vakası 503 verir — bu DL-1 hatası değil,
  MOD-0140'ın sözleşme uyumsuzluğudur; CT Cihan'a gider, okuyucu değişmez.
- **CT:** A-1 defter commit'inin ölçümü (tam takım yeşil, `PinnedCounts` aynı); CT kendi worktree'sinde sabotaj örneklemesi; CT-APR kayıtları
  (CT-APR-01 `approvals.tsv:93` ve CT-APR-02 inceleme UYGUN mevcut — DL-1 kabulü CT kabul kaydıyla `docs/records/` altına yazılır).
- **Ali'ye bilgi (onay değil):** XMC-001 kapsamı SQ-5'le merkezde; AC-26 (`AssignRoleCommandHandler`) ve `AccountKindCreationPathsGuardTests`
  değişikliği ile A-3 sonucu Ali'ye bilgi olarak iletilir. A-3 yeni dosya izni gerektirirse CT verir ve Ali'ye bildirir.
- DL-2 başlama kapıları (bu dilimi bekletmez): SQ-14 (MOD-0148 manifesti, Natig), inceleme F-1 (TTL atfı + süresi dolmuş bilet vakası).

## 6. Rapor
Commit sha'ları (A-1 defter commit'i ayrıca); her hat alımı (ata / ff / no-ff) ve çakışma çözümü; paket ve sözleşme sha ölçümleri;
AC-1…AC-9, AC-26, AC-27 (ve yazıldıysa AC-10) için test adı ve sonuç; §2 eşleme tablosunun her satırı için vaka adı; sabotaj tablosu
(AC, üretimdeki değişiklik, kırmızı test, geri alma sonrası yeşil); filtresiz takım sayıları (önce / sonra, atlanan sayısı) ve taban
kırmızıları; izin kataloğu yeri; A-2 ve A-3 ölçümleri; Admin rolündeki Procurement / MDM izinleri; Auth'ta Ali'nin işiyle çakışma olduysa
nasıl çözüldü; açık noktalar (MOD-0140 DL-3 kanıtı bekleniyor, CT önerisi eşleme satırları).
