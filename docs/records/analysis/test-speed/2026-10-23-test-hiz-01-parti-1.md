# TEST-HIZ-01 · Ortak Mongo yayılımı, parti 1 (2026-10-23)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Karar:** sahip, CT üzerinden (2026-10-23): pilot desenini öteki Platform Mongo test sınıflarına 5–10 sınıflık
> partilerle yay. Test silme ya da assert değiştirme yok; her sınıfta önce/sonra yalnız o sınıf (`--filter`).

## 1. Sınıflar ve desen

| Sınıf | Test | Desen |
|---|---:|---|
| `PartnerAdminScopeHttpMongoTests` | 47 | **B** — sınıf başına replica set + host; `ResetAsync` koleksiyonları boşaltır, kota mock'unun kayıtlı çağrılarını siler, iki kiracıyı yeniden ekler |
| `TenantModulesScreenHttpMongoTests` | 45 | **B** — aynı; ayrıca kota mock'unun çağrılarını ve yakalanan sunucu hatalarını (`Failures`) temizler |
| `DecisionMakerSearchHttpMongoTests` | 28 | **A** — sınıf başına replica set; host test başına (sahte isim kaynağı, saat ve hız sınırı test başına durum taşıyor) |
| `MeetingAttendeeSearchHttpMongoTests` | 16 | **A** — aynı nedenle (sahte Auth çağrı sayacı, hız sınırı) |
| `TransactionOwnedAuditDeliveryMongoTests` | 26 | **A** — HTTP host yok; yalnız replica set paylaşılıyor |

**Ortak parçalar:**
- **Yeni dosya:** `Persistence/ReplicaSetClassFixture.cs`. Sınıf başına tek replica set sağlıyor; `FreshDatabaseAsync`
  sınıfın sabit adlı veritabanını (DB-010) her test başında silip veriyor.
- **Zorunlu yan değişiklik:** `PlatformExceptionPipelineOrderTests`, PartnerAdminScope'un host'unu kendisi kuruyordu
  (`Host.StartAsync()`). Artık kendi replica set'ini açıp `Host.CreateAsync` kullanıyor. Assert aynı.
- **Erişim değişikliği:** Düzey B'de fixture host'u tuttuğu için `Host` türleri ve TenantModulesScreen'in `Row` kaydı public
  oldu.

## 2. Önce / sonra (yerel, her sınıf tek başına)

| Sınıf | Önce: testlerin toplam süresi | Sonra | CI'da önce (39921122915) |
|---|---:|---:|---:|
| PartnerAdminScope | 226,2 sn | 2,5 sn | 67,9 sn |
| TenantModulesScreen | 170,0 sn | 2,3 sn | 50,8 sn |
| DecisionMakerSearch | 109,0 sn | 23,2 sn | 33,2 sn |
| MeetingAttendeeSearch | 61,8 sn | 19,9 sn | 24,2 sn |
| TransactionOwnedAuditDelivery | 64,9 sn | 5,5 sn | 20,8 sn |
| **Toplam** | **631,9 sn** | **53,4 sn** | **196,9 sn** |

- Yerel "önce" süreleri CI'dan yaklaşık 3 kat yavaş, çünkü bilgisayarda başka işler de koşuyor.
- CI'daki yeni süre bu PR'ın CI çalışmasında ölçülecek.
- Düzey A sınıfları daha az hızlandı: host her testte yeniden kuruluyor (~0,6 sn) ve veritabanı her testte yeniden
  oluşuyor.

## 3. Sabotaj kanıtı (önce ve sonra aynı satır, aynı filtre)

| # | Sabotaj (tek satır, sonra bayt bayt geri yüklendi) | Hedef | Kırmızı önce → sonra | Test adları |
|---|---|---|---:|---|
| B1 | `HasPermissionAttribute.cs:73` · `if (!scope.IsUnrestricted && !admitsPartners)` → `if (false && …)` | PartnerAdminScope | 0 → 0 | — (**yeşil mutant**, §4) |
| B1′ | `PartnerAdminScopeAttribute.cs:87` · `if (scope.IsUnrestricted \|\| …)` → `if (true \|\| …)` | PartnerAdminScope | 25 → 25 | **aynı** |
| B2 | `TransactionOwnedAuditCommands.cs:28` · `typeof(DisableTenantModuleEntitlementCommand),` silindi | TenantModulesScreen | 27 → 27 | **aynı** |
| B3 | `CanonicalTransactionalAuditOutboxWriter.cs` · `if (userId == Guid.Empty)` → `if (false && …)` | TransactionOwnedAuditDelivery | 0 → 0 | — (**yeşil mutant**, §4) |
| B3′ | aynı dosya · `if (string.IsNullOrWhiteSpace(intent.SystemActor))` → `if (false && …)` | TransactionOwnedAuditDelivery | 2 → 2 | **aynı** |
| B4 | `TasksController.cs:1245` · `[EnableRateLimiting(PeopleSearchRateLimit.PolicyName)]` silindi | DecisionMaker + MeetingAttendee | 2 → 2 | **aynı** (her sınıftan bir test) |

"Aynı" demek, kırmızıya dönen test adları (teori satırları dahil) önce ve sonra birebir aynı demek. Desen değişikliği
hiçbir korumayı düşürmedi ve hiçbir yeni kırmızı yaratmadı.

**Ek kontroller (sonra):**
- `PlatformExceptionPipelineOrderTests` 5/5 yeşil.
- Platform hızlı kümesi (`fast-local-tests.sh`) 6.898/6.898 yeşil.
- `MongoTestDatabaseGuardTests` (DB-010) 6/6 yeşil.

## 4. Yeşil mutantlar (yalnız kayıt; bu partide araştırılmadı)

- **B1:** `HasPermission`'daki "partner, partnere açık olmayan uçta reddedilir" satırı kapatılınca PartnerAdminScope'un
  47 testinin hiçbiri kırmızıya dönmedi. Olası açıklama: aynı kuralı `PartnerAdminScopeAttribute` ya da politika katmanı
  da uyguluyor (iki katman; bkz. S1 düzeltme kaydı). Ölçülmedi.
- **B3:** "imzalı ama kullanıcı kimliği yok" reddi kapatılınca TransactionOwnedAuditDelivery'de kırmızı yok. Bu sınıfın
  "kimse adlandırılamıyor" testleri imzasız yolu (B3′) deniyor. İmzalı ama kimliksiz yolu bu sınıf denemiyor.
  Platform'un başka bir testinin bunu yakalayıp yakalamadığı ölçülmedi.
