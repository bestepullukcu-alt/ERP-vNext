# TEST-HIZ-01 · 4. adım pilotu: sabotaj kanıtı (2026-10-23)

> **Tür:** tarihli ölçüm kaydı (`docs-organization` 2. soru). Düzeltilmez; yeni ölçüm yeni kayıt olur.
> **Kapsam:** `Diten.Platform.Application.Tests`, CI'ın en yavaş üç sınıfı. Hiçbir test silinmedi ya da değiştirilmedi.
> Her sabotaj tek bir üretim satırıydı. Her koşudan sonra dosya kopyasından bayt bayt geri yüklendi ve `git diff` ile
> boş olduğu doğrulandı.

## 1. Kısa sonuç

- **Silmek süreyi kısaltmıyor.** Bu sınıflarda her test 1,1–1,7 sn sürüyor ve bu süre testin içeriğinden bağımsız. Neden:
  her test kendi mongod replica set sürecini başlatıyor (`DisposableMongoReplicaSet.StartAsync()`). Platform'da bu
  biçimdeki 543 test (54 sınıf), aynı xUnit koleksiyonunda (`DisposableMongoReplicaSet`) **sırayla** koşuyor ve CI'da
  toplam **470 sn (7 dk 50 sn)** sürüyor. platform-app'in test adımı 9 dk 58 sn.
- **DB-010 bunu istemiyor.** `.antigravity/rules/mongo-indexing.md` "paylaşılan bir veritabanı + test başına yeni
  `TenantId`" diyor. Test başına ayrı süreç, kuralın istediğinden ağır bir desen.
- **Dört sabotajdan çıkan:** 1 gereksizlik adayı (kanıtlı, tek satır), 1 test boşluğu (yeşil mutant), 2 gerekli aile.

## 2. Ölçüm düzeni

| Ne | Değer |
|---|---|
| Commit | main `9618dacc6` |
| Her sabotajda koşan | Platform hızlı kümesi (`scripts/test-env/fast-local-tests.sh`, 6.898 test) + `Category=Mongo&FullyQualifiedName~.Tenants.` (138 test = aşağıdaki üç sınıf) |
| Taban (sabotajsız) | 6.898/6.898 ve 138/138 yeşil |
| Mongo | `DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod` (testlerin kendi süreci; paylaşılan geliştirme veritabanı değil) |
| Yerel süre | Mongo alt kümesi 5 dk 47 sn – 7 dk 51 sn (CI'da aynı üç sınıf 2 dk 58 sn) |

**Sınır:** Platform'un öteki 1.678 Mongo testi koşulmadı (yerelde tam takım yasak). Bu yüzden "başka test kırmızıya dönmedi"
sonucu yalnız koşulan 7.036 test için geçerli. Bir **gereksizlik** iddiası için tek bir başka kırmızı yeter; bu yön güvenli.
Bir **gereklilik** iddiası ise koşulmayan testlerde de kırmızı çıkmadığını varsayıyor.

## 3. Sabotajlar ve sonuçları

| # | Sabotaj (tek satır) | Kırmızıya dönen | Sonuç |
|---|---|---|---|
| S1 | `TenantCommercialSubscriptionsController.cs:14` · `[Authorize(Policy = "PlatformActor")]` silindi | **hiçbiri** (0 / 7.036) | **Yeşil mutant → test boşluğu** (§4) |
| S2 | Aynı dosya, Suspend eylemi · `[Authorize(Policy = "PlatformAdminOnly")]` silindi | 1 · `TenantSubscriptionEndpointsHttpMongoTests.A_partner_writes_no_subscription_even_for_its_allowed_tenant_and_reads_only_its_own` | Satırı tek bir test koruyor. Beklenen `A_platform_actor_who_is_not_an_active_administrator_is_refused(suspend)` **yeşil kaldı**: o aile başka bir satırı koruyor (`HasPermission` içindeki yönetici kaydı kontrolü) |
| S3 | `TransactionOwnedAuditCommands.cs:35` · `typeof(SuspendTenantSubscriptionCommand),` silindi | 4 · `TransactionOwnedAuditCommandGuardTests.The_pipelines_allow_list_is_exactly_the_marked_commands` (**hızlı, Mongo'suz**) + `No_subscription_endpoint_is_refused_by_the_audit_pipeline_gate(suspend)` + `A_lifecycle_endpoint_works_and_its_audit_record_reaches_audit_events_once(suspend)` + `Another_tenants_subscription_is_not_reachable_through_this_tenants_path(suspend)` | **Gereksizlik adayı** (§5) |
| S4 | `TenantSubscriptionRepository.cs:42` · `Filter.Eq(x => x.TenantId, tenantId)` silindi | 5 · yalnız `Another_tenants_subscription_is_not_reachable_through_this_tenants_path` (5 satır) | Kiracı izolasyonunu bu aile **tek başına** koruyor → **gerekli** |

## 4. Test boşluğu (S1)

- `HasPermissionAttribute.OnAuthorizationAsync`, platform aktörü olmayan bir kullanıcıyı token'daki izin anahtarıyla
  geçiriyor (`PermissionClaimEvaluator`).
- Controller'ın GET uçlarında (`GetCurrent`, `GetHistory`, `GetDetail`, `HasActive`, `GetEntitlements`) yalnız
  `HasPermission(...read)` var. Tenant kullanıcısını orada durduran tek şey sınıf düzeyindeki `PlatformActor` politikası.
- Var olan `A_tenant_user_is_refused_even_with_the_permission_key_in_the_token_and_for_their_own_tenant` yalnız 7 yazma
  ucunu deniyor. Yazma uçlarında ayrıca `PlatformAdminOnly` olduğu için, sınıf satırı silinse bile bu test yeşil kalıyor.
- **Çıkarım (kod okuması, ayrı bir deneme testiyle ölçülmedi):** sınıf satırı kaldırılırsa, izin anahtarı taşıyan bir
  tenant kullanıcısı GET uçlarından abonelik okuyabilir ve hiçbir test bunu yakalamaz. Üretim kodu bugün doğru; boşluk
  testte.
- **Öneri:** aynı testin teorisine GET satırları eklemek. Platform test kodu değişikliği olduğu için sahip onayı gerekir.

## 5. Gereksizlik adayı (S3)

| Test | Sabote edilen satır | Kırmızıya dönen diğer test |
|---|---|---|
| `TenantSubscriptionEndpointsHttpMongoTests.No_subscription_endpoint_is_refused_by_the_audit_pipeline_gate(route: "{0}/suspend")` | `TransactionOwnedAuditCommands.cs:35` | `TransactionOwnedAuditCommandGuardTests.The_pipelines_allow_list_is_exactly_the_marked_commands` (Mongo'suz, hızlı) ve `A_lifecycle_endpoint_works_and_its_audit_record_reaches_audit_events_once(suspend)` |

- Kanıt yalnız **suspend** satırı için. Ailenin öteki 6 satırı (assign, activate, cancel, renew, expire, reactivate) aynı
  desende, ama her birinin kendi sabotajı koşulmadı. Bu yüzden onlar aday bile değil.
- Kazanç küçük: satır başına ~1,2 sn. 7 satırın hepsi kalksa CI'da ~9 sn kazanılır; platform-app test adımının %2'sinden az.

## 6. Asıl hız kolu (karar bekliyor)

- 543 testin her birinde yeni bir mongod başlatmak yerine sınıf (ya da koleksiyon) başına tek bir replica set ve test
  başına yeni `TenantId` kullanılabilir. Bu, DB-010'un "doğru desen" dediği şey.
- Bunun için testlerin sabit kiracı kimlikleri (`TenantA`, `TenantB`) ve "hiçbir şey yazılmadı" denetimleri test başına
  benzersiz kimliklere geçmeli. Bu, test kodu değişikliği; sahip onayı gerekir.
- Beklenen etki tahmin, **ölçülmedi**: süreç başlatma maliyeti (~1 sn × 543) büyük ölçüde kalkar.
- Önce tek bir sınıfla pilot yapılırsa ölçülebilir: `TenantSubscriptionEndpointsHttpMongoTests`, 46 test, CI'da 59 sn.

## 7. Pilot sonucu (aynı gün, sahip onayıyla)

Yalnız `TenantSubscriptionEndpointsHttpMongoTests` değişti ve yalnız o sınıf koşuldu (`--filter`).

**Kurulumun süre dağılımı** (geçici süre ölçümü, commit edilmedi; ortak replica set adımında, test başına medyan):

| Aşama | Medyan |
|---|---:|
| Veritabanını silme | 93 ms |
| İndeksleri kurma | 296 ms |
| HTTP host (TestServer) kurma | 566 ms |
| İki kiracıyı ekleme | 17 ms |

**Yapılan:**
- Sınıf başına tek bir replica set ve tek bir host.
- Her test başında sabit adlı veritabanının (DB-010) bütün koleksiyonları boşaltılıyor; üretim indeksleri kalıyor.
- Hiçbir assert değişmedi.

| Ölçüm (yerel, yalnız bu sınıf) | Test | Test süresi | Duvar saati | mongod süreci |
|---|---:|---:|---:|---:|
| Önce (test başına replica set) | 46 | 1 dk 50 sn | 117 sn | 46 |
| Ara adım: sınıf başına replica set, test başına host | 46 | 1 dk 9 sn | 82 sn | 1 |
| **Sonra:** sınıf başına replica set + host | 46 | **4 sn** | 15 sn | 1 |

**Sabotajlar aynı sonucu verdi.** S1–S4 yalnız bu sınıfa karşı yeniden koşuldu. Sınıf içinde kırmızıya dönen testler §3'teki ile
birebir aynı: S1 → 0, S2 → 1, S3 → 3, S4 → 5. Yani hızlanma, testlerin koruduğu satırları değiştirmedi.

**Ek kontrol:** `MongoTestDatabaseGuardTests` (DB-010 guard'ı) 6/6 yeşil.
