# Auth — Admin baseline grant geri alma (Q358 / G7) çalıştırma rehberi

Kimin için: Auth servisini dağıtan operatör. Ne için: geçici olarak Admin rol şablonuna girmiş bir modülün, o dönemde
etkinleştirilen kiracıların Admin rollerine **System** kaynaklı olarak verdiği anahtarları denetimli ve tek seferlik
olarak geri almak.

| Temizlik | Ayar | Anahtarlar | Nerede gerekir |
|---|---|---|---|
| **Q358** (MOD-0183 Shipments) | `Q358Retraction:Mode` | `supplychain.shipments.{read,create,dispatch,cancel,pod.capture}` | **Üretim dahil**, `shipment-tracking-pod` Admin şablonundayken etkinleştirilmiş kiracıların olduğu her ortam |
| **G7** (MOD-0190 S&OP, MOD-0192 Capacity) | `G7Retraction:Mode` | `supplychain.sandop-plans.*` (4), `supplychain.capacity-plans.*` (4) | Yalnız `DemandFixtures:Enabled=true` olan **non-prod** host'lar. Üretimde S&OP/Capacity hiç compose edilmez (`DemandConsumerComposition.SeedFor`), temizlenecek bir şey yoktur |

Kaynak kod: `BaselineGrantRetraction` (karar), `Q358ShipmentGrantRetractionRunner` (Mongo), `Q358ShipmentGrantRetractionHostedService`
(başlatma). Kararlar: `docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md`,
`docs/records/decisions/2026-10/mvp6-0190-0192-g7-role-grants-owner-decision-01.md`. Lane kanıtı:
`docs/records/audits/2026-10/mvp6-q358-grant-cleanup-01/`, `docs/records/audits/2026-10/mvp6-g7-grant-removal-01/`.

## Ne yapar, ne yapmaz

Her kiracının **sistem `Admin` rolündeki**, tanımın anahtarlarına ait **System** kaynaklı her grant için:

| Kiracının o anahtarın modülüne entitlement'ı | Sonuç |
|---|---|
| var | grant **Module**'e çevrilir (`SourceModuleCode` = modül); erişim değişmez |
| yok | grant **soft-delete** edilir (`IsDeleted=true`, `UpdatedAt`, `UpdatedBy=system`) |
| okunamadı (Platform erişilemez / anahtar yok / yetkisiz) | **hiçbir şey yapılmaz**; kiracı "unknown" raporlanır |

- **Dokunmaz:** Viewer, SuperAdmin, özel roller; Module ya da Manual kaynaklı grant'lar; tanım dışındaki anahtarlar.
- **Denetim:** her değişiklikten **önce** `authAuditLogs`'a bir satır yazılır (`role_permission_source_corrected` ya da
  `role_permission_revoked`; `source` = `q358-shipment-grant-retraction` / `g7-sandop-capacity-grant-retraction`). Değişiklik
  olmazsa satırı geri çeken `role_permission_retraction_not_applied` yazılır.
- **Tekrar güvenlidir:** ikinci çalıştırma hiçbir şey değiştirmez (yalnız hâlâ System olan grant'lar aday).
- **Geri gelir:** soft-delete edilen bir grant, kiracı sonradan o modüle yetkilendirilince entitlement senkronu tarafından
  aynı belge üzerinden Module olarak yeniden etkinleştirilir.

## Ön koşullar

1. Auth, `PlatformService:BaseUrl` ve `PlatformService:InternalApiKey` ile Platform'a ulaşabiliyor olmalı. Ulaşamazsa
   bütün kiracılar "unknown" olur ve hiçbir şey değişmez (güvenli, ama iş yapılmamış olur).
2. Log seviyesi: özet ve plan satırları `Information`'dır. Üretim varsayılanı `Information` (`appsettings.json`); seviye
   düşürülmüşse geçici olarak açın:
   `Logging__LogLevel__Diten.AuthService.Infrastructure.Authorization.Q358ShipmentGrantRetractionHostedService=Information`
3. Auth veritabanının yedeği alınmış olmalı (her değişiklik geri alınabilir olsa da standart dağıtım kuralı).

## Adımlar

Ayar ortam değişkeni olarak verilir (`:` yerine `__`). Değer büyük/küçük harf duyarsızdır; tanınmayan her değer **Off**'tur.

### 1. DryRun — hiçbir şey yazmaz

```
Q358Retraction__Mode=DryRun        # G7 için: G7Retraction__Mode=DryRun
```

Auth'u bu ayarla başlatın. Başlangıçtan birkaç saniye sonra günlükte şunlar görünür:

```
"q358" retraction DryRun: tenant <id>, role <id>, key "supplychain.shipments.read" → ConvertToModule.
"q358" retraction DryRun: tenant <id>, role <id>, key "supplychain.shipments.read" → SoftDelete.
"q358" retraction DryRun finished: 5 Admin role(s), 9 grant(s) planned, 0 converted to Module, 0 soft-deleted, 0 not applied, 0 tenant(s) unknown, 0 failed.
```

Kontrol edin:
- **`tenant(s) unknown` 0 olmalı.** Değilse Platform bağlantısını/anahtarını düzeltin ve DryRun'ı tekrarlayın.
- **`failed` 0 olmalı.** Değilse `retraction failed for tenant … : <ExceptionType>` satırına bakın.
- Plan beklenene uyuyor mu: yetkili kiracılar `ConvertToModule`, yetkisizler `SoftDelete`.
- DryRun'da `converted` ve `soft-deleted` her zaman 0'dır; veritabanında değişiklik yoktur.

### 2. Apply

```
Q358Retraction__Mode=Apply
```

Auth'u yeniden başlatın. Beklenen özet:

```
"q358" retraction Apply finished: … N converted to Module, M soft-deleted, 0 not applied, 0 tenant(s) unknown, 0 failed.
```

`converted + soft-deleted`, DryRun'daki plan sayısına eşit olmalı. `not applied` > 0 ise, arada bir yönetici o grant'ı
değiştirmiştir; grant olduğu gibi bırakılır ve denetim satırı geri çekilir (beklenen, güvenli durum).

### 3. Doğrulama (isteğe bağlı, salt okunur)

```
// mongosh, Auth veritabanında
db.authAuditLogs.countDocuments({ Metadata: /q358-shipment-grant-retraction/ })      // Apply'daki converted + soft-deleted (+ not applied)
db.authAuditLogs.countDocuments({ Metadata: /q358-shipment-grant-retraction/, EventName: "role_permission_retraction_not_applied" })
```

Aynı ayarla bir kez daha başlatmak da doğrulamadır: özet `0 grant(s) planned` demelidir.

### 4. Ayarı kaldırın

`Q358Retraction__Mode` (ve G7 için `G7Retraction__Mode`) ortamdan silinir; Auth yeniden başlatılır. Ayar yoksa temizlik
hiçbir şey okumaz.

## Geri dönüş

- Bir kiracı yanlışlıkla soft-delete aldıysa: kiracıyı ilgili modüle yetkilendirmek (Platform entitlement) grant'ı Module
  olarak geri getirir. Elle veritabanı düzenlemesi gerekmez.
- Toplu geri dönüş gerekiyorsa: denetim satırlarındaki `grantId` alanları değişen belgeleri tek tek gösterir; geri alma
  bir sahip kararıdır ve ayrı bir iş olarak yapılır.

## Sık görülen durumlar

| Günlükte | Anlamı | Ne yapılır |
|---|---|---|
| `skips tenant … its entitlement could not be read` | Platform'dan yetkili cevap alınamadı | Bağlantı/anahtarı düzeltip tekrar çalıştırın |
| `could not run: <ExceptionType>` | Temizlik başlamadan durdu (ör. Mongo erişimi) | Nedeni giderip tekrar çalıştırın; hiçbir şey değişmemiştir |
| Hiç `retraction` satırı yok | Ayar yok/yanlış yazılmış ya da log seviyesi `Information`'ın altında | Ayarı ve log seviyesini kontrol edin |
| `0 Admin role(s)` ya da `0 grant(s) planned` | Temizlenecek bir şey yok (G7 için üretimde beklenen) | Ayarı kaldırın |
