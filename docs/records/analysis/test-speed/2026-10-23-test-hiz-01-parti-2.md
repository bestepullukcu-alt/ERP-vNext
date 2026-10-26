# TEST-HIZ-01 · Ortak Mongo yayılımı, parti 2 (2026-10-23)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, CT üzerinden (2026-10-23). Parti 1: PR #216, merge edildi. Bu partinin CI'ı merge edilmeden ölçülecek.

## 1. Parti 1'in CI sonucu (PR #216, service-tests 39956302248)

- service-tests çalışmasının **tamamı yeşil** (28 iş).
- platform-app "Test" adımı **9 dk 58 sn → 7 dk 53 sn**; 8.719 sonucun hepsi geçti.
- Pilot ve parti 1'in altı sınıfı CI'da (TRX) **255,5 sn → 47,1 sn**.

## 2. Bu partinin sınıfları — hepsi Düzey A

Her sınıfta replica set sınıf başına bir tane (`ReplicaSetClassFixture`). Her test, sınıfın sabit adlı veritabanını (DB-010)
silinmiş olarak alıyor. Host, World, Rig ya da Fixture test başına kalıyor. Test silinmedi, assert değişmedi.

| Sınıf | Test | Kurulum biçimi |
|---|---:|---|
| PlatformAdministratorSafetyMongoTests | 22 | `World.StartAsync` |
| TaskTypeEffectivenessGateMongoTests | 17 | test kendi mongod'unu açıp `ArrangeAsync`'e veriyordu |
| PlatformAdministratorLoadedVersionMongoTests | 15 | `World.StartAsync` |
| TenantModuleEntitlementTenantGuardMongoTests | 14 | `Rig`; veritabanı kurucuda açılıyor (eşzamanlı `FreshDatabase`) |
| ReviewMeetingGateReaderMongoTests | 11 | test mongod açıyordu; eşzamanlı `Arrange` (`FreshDatabase`) |
| InternalPlatformAdministratorStatusEndpointTests | 10 | `Host.StartAsync` |
| GlobalApplicabilityTransactionMongoTests | 11 | test mongod açıyordu; `Fixture.CreateAsync` |
| SubscriptionTransactionFailureMongoTests | 10 | `FailureFixture.StartAsync` |
| SubscriptionConcurrencySecurityMongoTests | 10 | `Fixture.StartAsync` |
| NotificationTemplateScopeHttpMongoTests | 7 | `Host.StartAsync` |

**Ön koşullar (taranarak doğrulandı):**
- Her testte en çok bir başlatma ve bir `CreateDatabase` var; sabit ad ikinci bir veritabanını silmez.
- Hiçbir test mongod sürecini durdurmuyor ya da portuna dokunmuyor.
- Başka dosyalarda bu sınıfların yalnız adları geçiyor (yorumlarda); kod bağımlılığı yok.

**Ortak dosyaya ek:** `ReplicaSetClassFixture.FreshDatabase` (eşzamanlı eşi).

## 3. Önce / sonra (yerel, her sınıf tek başına `--filter`, testlerin toplam süresi)

| Sınıf | Önce | Sonra | CI'da önce |
|---|---:|---:|---:|
| PlatformAdministratorSafety | 43,3 sn | 4,5 sn | 19,1 sn |
| TaskTypeEffectivenessGate | 32,8 sn | 2,9 sn | 14,5 sn |
| PlatformAdministratorLoadedVersion | 29,7 sn | 3,0 sn | 12,8 sn |
| TenantModuleEntitlementTenantGuard | 26,7 sn | 2,1 sn | 11,8 sn |
| ReviewMeetingGateReader | 21,7 sn | 1,9 sn | 9,8 sn |
| InternalPlatformAdministratorStatusEndpoint | 21,0 sn | 3,7 sn | 9,7 sn |
| GlobalApplicabilityTransaction | 22,4 sn | 2,7 sn | 9,7 sn |
| SubscriptionTransactionFailure | 20,5 sn | 3,0 sn | 9,3 sn |
| SubscriptionConcurrencySecurity | 23,7 sn | 3,0 sn | 8,8 sn |
| NotificationTemplateScope | 18,5 sn | 4,3 sn | 8,0 sn |
| **Toplam** | **261,0 sn** | **31,1 sn** | **113,5 sn** |

## 4. Sabotaj kanıtı

Sabotaj satırlarını bir keşif alt ajanı önerdi. Her birinin sınıfını gerçekten ayırdığı "önce" koşusunda ölçüldü
(hepsi ≥ 1 kırmızı). Her sabotaj tek satırdı ve sonra bayt bayt geri yüklendi.

| # | Satır (`services/Diten.Platform/src/…`) | Kırmızı önce → sonra | Test adları |
|---|---|---:|---|
| C1 | `UpdatePlatformAdministratorHandler.cs` · `IsSelfAsync` kontrolü kapatıldı | 4 → 4 | aynı |
| C2 | `TaskTypeEffectivenessGate.cs` · `if (true \|\| blocking.Count == 0)` | 5 → 5 | aynı |
| C3 | `ReactivatePlatformAdministratorHandler.cs` · istemci sürüm kontrolü kapatıldı | 1 → 1 | aynı |
| C4 | `TenantModuleEntitlementRepository.cs` · UpdateAsync kiracı kontrolü kapatıldı | 1 → 1 | aynı |
| C5 | `ReviewMeetingGateReader.cs` · iptal edilmiş toplantı koşulu kapatıldı | 2 → 2 | aynı |
| C6 | `InternalPlatformAdministratorsController.cs` · `IsActive` artık durumu okumuyor | 2 → 2 | aynı |
| C7 | `GlobalApplicabilityTransactionCoordinator.cs` · `EffectiveStateChanged` kısa devresi kapatıldı | 2 → 2 | aynı |
| C8 | `PlatformTransactionExecutor.cs` · commit yeniden denemesi kapatıldı | 4 → 4 | aynı |
| C9 | `SuspendTenantSubscriptionCommandHandler.cs` · `subscription == null` kontrolü kapatıldı | 3 → 3 | aynı |
| C10 | `ArchiveNotificationTemplateHandler.cs` · kiracı kapsamı kontrolü kaldırıldı | 1 → 1 | aynı |

## 5. Ek kontroller (sonra)

- `MongoTestDatabaseGuardTests` (DB-010): 6/6.
- **Platform hızlı kümesi:** zincirin sonunda ilk koşu **1 kırmızı / 6.898** verdi. O koşu TRX yazmadı; testin adı kaydedilmedi.
  Hemen ardından dört koşu 6.898/6.898 yeşil (üçü TRX'li).
  - Bu partinin değiştirdiği on sınıfın hepsi `Category=Mongo`; hızlı kümede koşmuyorlar. Yani kırmızı bu değişiklikten
    gelmiyor.
  - Hızlı kümede yük altında aralıklı kırmızıya dönen bir test var. Aday: 2 sn sınırıyla süre ölçen testler (ör.
    `BoundedDisplayNamesTests`). Ölçülmedi.
