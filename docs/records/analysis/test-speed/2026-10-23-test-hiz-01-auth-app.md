# TEST-HIZ-01 · auth-app: sıralı koleksiyonun bölünmesi (2026-10-23)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, bu oturumda (2026-10-23): Auth test koduna dokunmak için XMC-001 onayı; Adım 0 (ölçüm ve sabotaj
> tabanı) ve Adım 1 (koleksiyonu bölmek). Platform işi #219'da bırakıldı; iş aynı dalda birikiyor.

## 1. Neden

CI'da (service-tests 39965450499) auth-app'in "Test" adımı 11 dk 21 sn sürdü ve toplam CI süresini bu iş belirledi.

- `AccountKindAcceptance` koleksiyonu `DisableParallelization = true` ile tanımlıydı. 38 sınıf, 856 test sırayla koşuyordu:
  **481 sn, testlerin toplam süresinin %89'u**. Kalan 2.073 test paralel koşuyor ve 59 sn tutuyordu.
- **Gerekçe:** `AuthTestHost` süreç geneli ortam değişkenlerini değiştiriyor (C1/C3 §3).
- **Ama ölçülen kod bu değişikliği dar tutuyor.** Değişkenler yalnız host'un kurulduğu kısa anda değişiyor ve aynı
  kilidin (`StartLock`) içinde geri yükleniyor (`AccountKindAcceptance.cs`, `InitializeCoreAsync` içindeki `finally`).
  Host kurulduktan sonra testler ortam değişkenine bağlı değil.

## 2. Ön koşullar (kod okuyarak doğrulandı)

- Test projesinde `WebApplicationFactory<Program>` kuran yalnız iki yer var: `AuthTestHost` ve `CompositionRootGuardTests`.
  İkincisi kendi sıralı koleksiyonunda.
- Ortam değişkeni okuyan iki üretim sınıfı (`EntitlementReconciliationOperationalMode`, `ServiceClientOperationalBootstrap`)
  yalnız `Program.cs`'in başında, uygulama açılırken çağrılıyor; yani kilitli kurulum anının içinde. İstek sırasında
  ortam değişkeni okuyan bir yol bulunmadı.
- Ortam ya da konsol değiştiren öteki test grupları zaten kendi sıralı koleksiyonlarında: `CompositionRootGuard`,
  `Entitlement process environment`, `Console capture`, `AuthReconciliationMongoCollection`, `OperationalMongoCollection`.
  xUnit bu koleksiyonları paralel aşamadan sonra tek başına koşturuyor.
- BSON Guid kaydı: test derlemesi yüklenirken `AuthTestSerializers` (`[ModuleInitializer]`) Standard serileştiriciyi ilk test
  başlamadan kaydediyor. Host'ların `EnsureStandard` çağrısı uyumlu bir tekrar; kayıt değişmiyor.

## 3. Değişiklik (yalnız test kodu)

- 36 sınıftan `[Collection("AccountKindAcceptance")]` kaldırıldı. Her biri kendi xUnit koleksiyonunda, yani paralel koşuyor;
  host'ları kendi mongod'larıyla.
- **Sıralı kalan 2 sınıf** (değişkenleri ya da süreç geneli durumu kendileri değiştiriyor):
  `AccountKindAcceptanceGuardTests` ve `GuidSerializerRegistrationTests`.
- `AuthTestHost`'taki ve koleksiyon tanımındaki yorumlar yeni durumu anlatıyor: kilit artık asıl güvence.
- Test silinmedi, assert değişmedi, üretim kodu değişmedi. Toplam 38 dosya, +9 / −42 satır.

## 4. Önce / sonra (yerel, aynı 38 sınıf, en çok 4 paralel koleksiyon — CI'daki 4 çekirdek gibi)

| Koşu | Sonuç | Süre |
|---|---|---:|
| Önce (sıralı) | 856/856 | **13 dk 55 sn** |
| Sonra, 1. koşu | 856/856 | 7 dk 2 sn |
| Sonra, 2. koşu | 856/856 | 6 dk 18 sn |
| Sonra, 3. koşu | 856/856 | **6 dk 13 sn** |

- Testlerin toplam süresi paralel koşuda biraz arttı (646 sn → 660–681 sn). Bu, aynı anda koşan host'ların birbirini
  yavaşlatması; duvar saati ise yarıya indi.
- **Kalan en uzun yol** `ExportGrantBackfillScenarioTests` (yerelde 237 sn; tek sınıf, kendi içinde sıralı). Bir sonraki kol
  bu sınıfı bölmek (plan Adım 2, ayrı onay).
- **Önce, sınıf başına** (yerel, sıralı koşudan): ExportGrantBackfill 237,1 sn · AdminResetInvalidates 185,6 sn ·
  AccountKindAcceptanceGuard 77,7 sn · PlatformAccountAtTenantDoor 76,0 sn.

## 5. Sabotaj kanıtı (önce ve sonra aynı satır, aynı filtre)

Satırlar `services/Diten.AuthService/src/` altında. Her sabotaj tek satırdı ve sonra kopyasından geri yüklendi.

| # | Satır | Hedef sınıf | Kırmızı önce → sonra | Test adları |
|---|---|---|---:|---|
| A1 | `ExportGrantBackfill.cs` · okuma izni koşulu her zaman doğru | ExportGrantBackfillScenario | 15 → 15 | aynı |
| A2 | `AdminResetPasswordCommandHandler.cs` · kendi şifresini sıfırlama reddi kapatıldı | AdminResetInvalidatesEndpoint | 1 → 1 | aynı |
| A3 | `LoginCommandHandler.cs` · platform hesabının kiracı kapısında reddi kapatıldı | PlatformAccountAtTenantDoor | 16 → 16 | aynı |
| A4 | `ESignReauthHandlers.cs` · pencere biletinde kullanıcı adı kontrolü kapatıldı | ESignReauthEndpoint | 1 → 1 | aynı |
| A5 | `PlatformAuthController.cs` · "zorunlu değişiklik bekliyor mu" kontrolü kapatıldı | PlatformForcedPasswordChangeActorType | 1 → 1 | aynı |
| A6 | `ClientAddressResolver.cs` · başlangıç uyarısı kapatıldı | ClientAddressProductionDefaults | 1 → 1 | aynı |
| A7 | `PlatformAuthController.cs` · kullanıcı adı çakışması kontrolü kapatıldı | PlatformAdminSync | 1 → 1 | aynı |
| A8 | `UserListRules.cs` · sayfalama doğrulaması kapatıldı | UserListQuery | 3 → 3 | aynı |
| L1 | test altyapısı · `StartLock` kapasitesi sınırsız (kilit yok) | AccountKindAcceptanceGuard | 1 → 1 | aynı (`Two_concurrent_Start_calls_never_overlap_their_environment_mutation_window`) |

- A7 için ilk önerilen satır, girintisi farklı başka bir satırın içinde alt dize olarak da geçiyordu. Araç ikisini birden
  bozacaktı; bu yüzden aynı dosyada tek geçen bir satır seçildi.
- **Yöntemin sınırı:** A1–A8 tek sınıf filtresiyle koşuldu; o koşuda paralellik yok. Paralel koşunun sınıfları birbirine
  karıştırmadığının kanıtı, 38 sınıfın paralel olarak üç kez 856/856 yeşil koşması. L1 ise artık asıl güvence olan kilidin
  testinin hâlâ kırmızıya dönebildiğini gösteriyor.

## 6. Ölçülmeyenler

- auth-app'in tamamı (2.929 test) yerelde koşulmadı (PC kısıtı). 2.073 paralel testle birlikte koşu PR'ın CI'ında ölçülecek.
- CI'daki yeni süre (beklenen: auth-app "Test" adımı ~11 dk → ~6 dk) PR'da ölçülecek.
