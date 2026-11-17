# MOD-0018-FU31 — DL-1 tasarım kaydı 01: F-1 tasarımı ve F-3

- **Kaynak:** CT · Auth (Yetki kulesi).
- **Tür:** CT tasarım kaydı. Sahip kararı değildir.
- **Dayanak:**
  - Sahip kaydı 05 (`mod-0018-fu31-decisions-05.md`): F-1 tasarımı ve F-3, DL-1 başlamadan kapanır.
  - Sahip kaydı 07: paket onaylı (`07db3711…`) ve donmuş.
- **Paket:** Değişmez.
  - Bu kayıt paketin "Yerel kesinlik" tablosunu (paket satır 452-471) yalnız F-1 ve F-3'ün istediği kadar tamamlar.
  - Paketten ayrıldığı tek yer, denetim kimliğine satırın ömür damgasının eklenmesidir (K-A).
  - Çelişki olursa F-1 ve F-3 için bu kayıt geçerlidir, başka her konuda paket.
- **Ölçüm tabanı:** kule dalı `e44b25c51` (origin/main `5198db918` alınmış).
- **Ön inceleme:** İki kör alt ajan turu, her turda yeni ve ipucusuz bir ajan.
  - Tur 1: UYGUN DEĞİL (1 ENGEL, 1 ORTA, 3 DÜŞÜK).
  - Tur 2: KOŞULLU (3 ORTA, 2 DÜŞÜK).
  - İki turun bulguları bu sürümde kapatıldı (aşağıda "Ön incelemenin bulguları").
- **Asıl inceleme:** Tasarım DL-1 promptuyla birlikte bağımsız incelemeye girer (HIGH prompt, `prompt_gate.py --review`).

## Güvenlik özelliği

Tasarım şu beş özelliği korur. Testler bunlara göre yazılır.

1. Silinen her izin satırının yerel `authAuditLogs`'ta geçerli bir "revoked" kaydı vardır. Geçerli kayıt, hiçbir geri çekme kaydının adını taşımadığı kayıttır.
2. Canlı bir satırın o ömrüne ait son geçerli kaydı "revoked" değildir.
3. Aynı olgu için iki kayıt yazılmaz: kimlikler deterministiktir ve yazım bir kezdir.
4. Geri çekme olayı Platform'a iletilmez ve yeni Platform sözlüğü istemez.
5. Aynı satırı başka bir koşunun sildiği yarışı kaybeden koşu "uygulanmadı" yazmaz.

## Kapsam

"FU31 satırı" şu demektir: hedef rollerdeki `GrantSource = Module` ve `SourceModuleCode = putaway-picking-packing` olan izin satırı. Hedef roller: Admin, Viewer, WarehouseSupervisor, ShippingPlanner, WarehouseOperator. DL-2'de bunun yerine Viewer rollerindeki `warehouse.outbound-shipments.read` satırı geçer (K-17 tanımı).

| Silen yol | Kod (bugün) | Silme türü | Dilim |
|---|---|---|---|
| Daraltma / plan farkı | `GrantModuleWithKeysAsync` → depo profili | Sert (`TryRemoveByIdAsync`, `Id + TenantId`) | DL-1 |
| Hak düşüşü | `RevokeModuleAsync` (`EntitlementSyncConsumer.cs:161`, `:169`) | Sert | DL-1 |
| Bayat modül temizliği | `SyncTenantModulesWithKeysAsync` → `RevokeStaleModulesAsync` | Sert | DL-1 |
| Viewer geri alma koşucusu | Q358 deseni | Yumuşak (koşullu güncelleme) | DL-2 |

Kurallar dört yolun hepsinde geçerlidir.

## F-1 tasarımı

### K-A — Satırın ömrü ve kimlikler

**Ömür.** Bir satırın ömrü, `L = (satır.Id, satır.AssignedAt)` çiftidir. `AssignedAt` UTC'dir ve milisaniyeye kesilir.

**Bu tanım neden gerekli:**
- `AssignedAt` yalnız iki yerde atanır:
  - satır kurulurken (`RolePermission.cs:21`),
  - yumuşak silinmiş satır yeniden canlanırken (`RolePermissionRepository.cs:54`, `:61-79`, `:73`). Canlanmada belge `Id`'si aynı kalır.
- Yalnız `Id`'ye dayalı kimlik bu durumda yeni ömrü eski ömürle karıştırır. Ön incelemenin F-2 bulgusu bunu gösterdi:
  - eski ömrün gerçek silme kaydı yanlışlıkla geri çekilir;
  - yeni ömrün "granted" kaydı hiç yazılmaz.
- Sert silmede yeniden ekleme yeni bir `Id` alır; ömür kendiliğinden ayrışır.

**Kimlikler** (H = paketin deterministik özeti, `BaselineGrantRetraction.cs:88` deseni):

| Olay | Kimlik |
|---|---|
| granted | H("granted", L) |
| revoked, nesil k | H("revoked", L, k), k = 0, 1, 2, … |
| geri çekme | H("not_applied", revokedId) |

**Paketten tek ayrılık.** Paket kimliği yalnız satır kimliğinden türetir (paket satır 456-457: H("granted", izin satırı kimliği), H("revoked", izin satırı kimliği)). Bu kayıt ömür damgasını ve nesli ekler.
- Paketin kuralı korunur: kimlik o anda var olan kalıcı bir satırdan türetilir (paket satır 451-452).
- Nesil k DL-1'de de gerekir (E-3). Ömür damgası yalnız DL-2'deki canlanma için gerekir; DL-1'de milisaniye kesimiyle zararsızdır.
- AC-28'in kimlik iddiaları kabul kaydında bu kimliklere yeniden bağlanır.
- Bu ayrılık F-1'in "nasıl ele alınacağı tanımlanır" şartının parçasıdır. Asıl inceleme bunu ayrıca ölçer.

### K-B — Silme sırası (tek yardımcı)

Her silme tek yardımcıdan geçer. Bu kayıtta adı `RevokeWithAuditAsync(satır)`; kesin ad worker'ındır.

1. **Eksik "granted" kaydı yazılır.** `RecordOnceAsync(H("granted", L), role_permission_granted, recordedLate = true)`. Kayıt varsa `false` döner, kopya yazılmaz. Bu adım ekleme sonrası çökmede eksik kalan kaydı silmeden önce yerine getirir.
2. **"revoked" kaydı yazılır.** Nesil k şöyle seçilir: revoked_k ya yoktur ya da vardır ama geri çekilmemiştir. Bulunan en küçük k kullanılır. Varsa yarım kalmış önceki yazım budur; `false` döner, kopya yazılmaz. Bu çağrının satırı kendisinin yazıp yazmadığı (`ownRow`) saklanır.
3. **Satır silinir.** DL-1'de `TryRemoveByIdAsync`; DL-2'de Q358 koşullu güncellemesi.
4. **Silme olduysa hızlı kontrol yapılır.** revoked_k bu arada geri çekildiyse revoked_{k+1} yazılır (`recordedLate = true`). Bu adım hızlandırıcıdır; kalıcı onarım K-D'dir.
5. **Silme olmadıysa** F-3 kuralı uygulanır.

**Yerel yazım hatası** (1. ya da 2. adım fırlarsa): silme yapılmaz. Bu modül için hata biriktirilir, olay yeniden denenir (paket satır 456).

### K-C — Sarkan kayıt: canlı satırın geri çekilmemiş "revoked" kaydı

**Durum:** 2. adımdan sonra çökme olur. Sonraki koşunun planı satırı artık silmez. Örnek: hak geri geldi.

**Kural:** Paketin tamamlama geçişi (paket satır 457) her koşunun sonunda hedef rollerdeki canlı FU31 satırlarını zaten tarar. Bu geçiş her canlı satır için:
- satırın **bu ömrüne** ait en son nesil revoked_k kaydına bakar;
- kayıt geri çekilmemişse geri çekme kaydı yazar (`recordedLate = true`, yazım yolu F-3).

**Sınırlar:**
- Tarama, planın sildiği ya da silmeyi denediği satırlar silindikten sonra çalışır. O satırlar taramaya girmez; silme hatası modülün hata birikimiyle yeniden denenir.
- Kimlik ömre bağlı olduğu için canlanmış bir satırın eski ömrüne ait kayıt eşleşmez ve geri çekilmez.
- DL-2 koşucusu aynı taramayı yalnız kendi hedefi için yapar.

### K-D — Kalıcı onarım: geri çekilmiş ama silinmiş satır

**Sorun:** Ön incelemenin F-1 bulgusu (ENGEL). Şu sırada hızlı kontroller kaybolabilir:
1. B revoked_0'ı yazar.
2. A satırı canlı görür ve geri çekme kaydını yazar.
3. B satırı siler.
4. B, 4. adımdan önce çöker.

Sert silinen satır artık hiçbir planda ya da taramada görünmez. Bu yüzden onarım satırdan değil, denetim günlüğünden yürür.

**Kural:** Her koşunun sonunda (DL-1 eşitlemesi kiracı başına, DL-2 koşucusu kiracı başına), **K-C'den sonra** çalışır. Sıra her koşuda sabittir: önce K-C, sonra K-D.
- Kiracının FU31 geri çekme kayıtları okunur: `EventName = role_permission_retraction_not_applied`, `TenantId = t`.
  - Sorgu var olan dizinleri kullanır: `(EventName, OccurredAt)` ve `(TenantId, OccurredAt)` (`MongoDbIndexConfigurations.cs:86-89`).
  - Gövde (`Metadata`) JSON metindir; bu yüzden kaynak, rol ve ömür süzmesi getirildikten sonra bellekte yapılır.
  - Bu küme yalnız geri çekmeleri içerdiği için küçüktür. Q358'in aynı adlı satırları gövdedeki kaynakla ayıklanır.
- Her geri çekme kaydının adını taşıdığı revoked_k(L) için:
  - revoked_{k+1}(L) zaten varsa geçilir;
  - L ömrü bittiyse revoked_{k+1}(L) yazılır (`recordedLate = true`).
- **"L ömrü bitti"** şu demektir:
  - DL-1: `Id` ile satır yoktur, ya da vardır ama `AssignedAt`'ı farklıdır.
  - DL-2: satır `IsDeleted = true`'dur, ya da `AssignedAt`'ı farklıdır.
- **Katalog istisnası (F-3 ile aynı):** İzin katalogdan kalkmışsa satırı FU31 değil katalog silmesi kaldırmıştır. O zaman revoked_{k+1} yazılmaz.
  - "Kalkmış" demek: `Permission` kimliğiyle okunamıyor ya da `IsDeleted = true`.
  - Katalog silmesi önce bütün izin satırlarını sert siler, sonra izni yumuşak siler (`InternalPermissionsController.cs:211-212`, `PermissionRepository.cs:64-70`).
  - Kendi kaydı `permission_catalog_removed`'dir (`InternalPermissionsController.cs:215`), `role_permission_revoked` değil.

**Sonuç:**
- Bu onarım hızlı kontrollerin sırasına ve satırın hâlâ var olmasına bağlı değildir. Kayıp bir hızlı kontrol en geç bir sonraki koşuda onarılır.
- Sınır, paketin ekleme için yazdığıyla aynıdır (paket satır 467-468): çökmeden sonra hiç koşu olmazsa onarım gecikir. DL-2 koşucusu ve sonraki hak olayı onarımı tamamlar.

### K-E — AC-31'e eklenen çökme sıraları

Test kancası Q358 deseninde (`hook`). Testler gerçek Mongo ile koşar.

**Kanca kuralı:** Çökmeyi taklit eden kanca bütün koşuyu durdurur. Eşitleme servisinin modül başına hata yakalaması (`EntitlementPermissionSyncService.cs:498-510`) kancayı yutmamalı; yutarsa aynı koşunun tamamlama geçişi eksik kaydı hemen yazar ve vaka hiç oluşmaz. Her vakada, sonraki olaydan önce ön koşul ayrıca iddia edilir (ör. E-1'de "granted kaydı yok"). AC-31'in çökme kancaları da aynı kurala uyar.

| # | Sıra | Beklenen | Sabotaj (kırmızı vermeli) |
|---|---|---|---|
| E-1 (kayıt 05) | Ekleme yapılır; kanca "granted" kaydından önce bütün koşuyu durdurur. "granted kaydı yok" iddia edilir. Sonraki olay hak düşüşüdür. | Sırayla H("granted", L) (`recordedLate`) ve H("revoked", L, 0) yazılır; satır silinir | K-B 1. adımı kaldırılır |
| E-2 | revoked_0 yazılır, kanca silmeden önce bütün koşuyu durdurur. Sonraki olayda hak geri gelir. | Satır canlı; H("not_applied", revoked_0) yazılır | K-C taraması kaldırılır |
| E-3 | E-2'den sonra hak yeniden düşer | revoked_1 K-B 2. adımında yazılır: `recordedLate = false`. Satır silinir | Nesil kuralı kaldırılır (2. adım hep k = 0). revoked_1 yalnız 4. adımdan ya da K-D'den, `recordedLate = true` ile gelir; iddia kırmızıya döner |
| E-4 (ön inceleme F-1) | B revoked_0'ı yazar, A geri çeker, B siler, kanca B'yi 4. adımdan önce fırlatır. Sonra bir koşu daha olur. | revoked_1 yazılır | K-D onarımı kaldırılır |
| E-5 (ön inceleme F-2, DL-2) | Koşucu yumuşak siler. Yönetici aynı anahtarı atar; satır aynı `Id` ile canlanır. Koşucu yeniden koşar. | Eski ömrün revoked kaydı geçerli kalır, geri çekme yazılmaz. Canlanan satır `Manual` kaynaklıdır, FU31 satırı değildir; ona FU31 kaydı yazılmaz | Kimlikten `AssignedAt` çıkarılır |

## F-3 kapanışı

**Seçim:** koşucu deseniyle yalnız yerel yazılan `role_permission_retraction_not_applied`. Bu ad bugün Q358 koşucusunda vardır: `Q358ShipmentGrantRetractionRunner.cs:38`.

**Ad.** Paketteki `role_permission_revoked:not_applied` yazımının (yalnız paket satır 456) yerine geçer. Paket satır 430 ve 600 Q358 koşucusunun kendi `…_not_applied` yazımını anlatır. Yeni ad yaratılmaz.

**Yazım yolu.**
- Yalnız yerel `authAuditLogs`'a, `IAuthAuditService.WriteOnceAsync` ile (`IAuthAuditService.cs:18`), kimlik H("not_applied", revokedId).
- Kayıt `IRoleAuditRecorder` / `RecordOnceAsync` üzerinden geçmez.
- `RoleAuditEvents.Operations` sözlüğüne (`RoleAuditEvents.cs:37-44`) eklenmez. Bu yüzden Platform'a iletilmez; K-19 Platform izninin kapsamı değişmez; yeniden teyit ve Platform etiketi gerekmez.

**Gövde.** Q358'in `NotAppliedRow` alanları (`Q358ShipmentGrantRetractionRunner.cs:214` ve devamı; `withdraws` alanı orada zaten var, `:220`), artı `recordedLate` ve ömür damgası `assignedAt`.

**Ne zaman yazılır.** Silme olmadıysa yazılır; kural dilime göre ayrışır:

- **Yönetici FU31 satırını silemez.** Elle geri alma, `Manual` olmayan her satırı 409 ile reddeder (`RevokePermissionCommandHandler.cs:67-68`). Bu yüzden "satır yoksa" durumunun FU31 dışındaki tek üretim yolu katalog silmesidir (K-D katalog istisnası).
- **DL-1 (sert silme):** `TryRemoveByIdAsync` yalnız `Id + TenantId` ile süzer (paket satır 200-201). Bu yüzden `DeletedCount == 0` yalnız satır zaten yoksa olur.
  - Geri çekme kaydı yalnız şu durumda yazılır: bu çağrı revoked kaydını kendisi yazmıştır (`ownRow == true`) **ve** izin katalogdan kalkmıştır (K-D tanımı).
  - Başka bir FU31 koşusu sildiyse kayıt doğrudur ve geri çekme yazılmaz.
- **DL-2 (yumuşak silme):** `ModifiedCount == 0` olunca satır yeniden okunur.
  - Satır aynı ömürde hâlâ canlıysa ve `ownRow == true` ise geri çekme yazılır (Q358 deseni).
  - Satır silinmişse ya da ömrü değişmişse, DL-1'deki katalog kuralı uygulanır.
  - Bu "canlı ama değişmemiş" dalına bugün üretimde hangi yolun düştüğü DL-2 başında ölçülür. Hiçbir yol düşmüyorsa dal ve T-3 kaldırılır: sahte depo üretim kodunu değil kopyayı ölçer.
- Q358 koşucusunun kendisi değişmez; bu iş FU31 dışıdır.

**Kabul edilen sınır.** Katalog silmesi önce izin satırlarını siler, sonra izni yumuşak siler. Bu iki adımın arasındaki dar pencerede K-D, izni hâlâ katalogda görüp FU31 adına fazladan bir revoked_{k+1} yazabilir. Bu, 3. özelliğin bilinçli bir istisnasıdır: kayıtsız değişiklik kalmaz, yalnız fazla kayıt kalır. Depo anahtarları manifestle bildirildiği için katalogdan kalkmaları ayrıca beklenmez.

**Testler:**

| # | Dilim | Vaka | Beklenen | Sabotaj (kırmızı vermeli) |
|---|---|---|---|---|
| T-1 | DL-1 | Yarışı kaybeden koşu. Sıra kancayla zorlanır: A revoked_0'ı yazar (`ownRow = true`) ve durur; B revoked_0'ı dener (`false`) ve siler; A sürer, silme `0` döner | Geri çekme yok | "`ownRow` ise ve silme `0` döndüyse geri çekme yaz" |
| T-2 | DL-1 | A revoked_0'ı yazar ve durur; izin gerçek katalog ucuyla silinir (`InternalPermissionsController` üzerinden, depo çağrısıyla değil); A sürer, silme `0` döner. Ayrıca K-D, katalogdan kalkmış iznin geri çekilmiş kaydı için revoked_{k+1} yazmaz | İlk vakada geri çekme var; ikinci vakada revoked_{k+1} yok | Katalog kontrolü kaldırılır |
| T-3 | DL-2 | Satır aynı ömürde canlı, `ModifiedCount == 0` (yalnız DL-2 başında üretim yolu ölçülürse) | Geri çekme var | Yeniden okuma kaldırılır |
| T-4 | Her iki dilim | Olay iletilmez: forwarder sahtesine sıfır çağrı | Sıfır çağrı | Geri çekme yazımı `RecordOnceAsync` üzerinden yönlendirilir ve olay `Operations`'a eklenir |

**Sabitleme testleri** (her ikisinin de sabotajı aynı testi kırmızı yapar):
- `RoleAuditEvents.Operations` anahtar kümesi tam olarak bugünkü 5 olay ile `role_provisioning_skipped`'tir.
- Geri çekme yazımı `IAuthAuditService.WriteOnceAsync` ile yapılır.

## Ön incelemenin bulguları ve kapanışı

| Bulgu | Önem | Kapanış |
|---|---|---|
| F-1 — yarışta hızlı kontrol kaybolursa geri çekilmiş kayıtla silinmiş satır kalır | ENGEL | K-D denetim günlüğünden onarım; E-4 |
| F-2 — DL-2 canlanmasında aynı `Id`; eski ömrün gerçek silmesi geri çekilir, yeni ömrün granted kaydı yazılmaz | ORTA | K-A ömür damgası; E-5 |
| F-3 — "satır yoksa başka FU31 koşusu sildi" önermesi yanlış | DÜŞÜK | Tur 2 düzeltti: dış yol katalog silmesidir (aşağıda) |
| F-4 — sözlük sabotajı kırmızı veremezdi | DÜŞÜK | T-4 yeni sabotaj + iki sabitleme testi |
| F-5 — DL-1'de "satır canlı ve silinemedi" üretim kodunda oluşamaz | DÜŞÜK | Canlı satır dalı DL-2'ye taşındı (T-3); T-1'in sırası kancayla belirlendi |
| Atıf | — | `NotAppliedRow` `:214`; `withdraws` zaten var (`:220`) |

**Tur 2:**

| Bulgu | Önem | Kapanış |
|---|---|---|
| F-1 — yönetici istisnası yanlış atıfa dayanıyordu: yönetici `Module` satırını silemez (409). Asıl dış silme katalog silmesi; o `role_permission_revoked` yazmaz | ORTA | Yönetici istisnası ve eski T-2 kaldırıldı; katalog istisnası (K-D, F-3); yeni T-2 gerçek katalog ucuyla; kabul edilen sınır yeniden yazıldı. T-3'ün üretim yolu DL-2 başında ölçülür |
| F-2 — E-3 sabotajı 4. adım ve K-D tarafından örtülüyordu | ORTA | E-3 revoked_1 için `recordedLate = false` iddia eder |
| F-3 — E-1 ön koşulu modül başına hata yakalamasıyla hiç oluşmayabilirdi | ORTA | Kanca kuralı: kanca bütün koşuyu durdurur; ön koşul ayrıca iddia edilir |
| F-4 — K-C ile K-D'nin sırası tanımsızdı | DÜŞÜK | K-D her koşuda K-C'den sonra |
| F-5 — paket satır atıfları kaymıştı | DÜŞÜK | 200-201 ve 451-452 düzeltildi; satır 600 Q358 koşucusunun yazımıdır, paketteki `role_permission_revoked:not_applied` yazımı yalnız 456'dadır |
| K-D sorgusu | — | Dizinler ve bellek içi süzme yazıldı |

## DL-1 kabulüne kalanlar (bu kayıt kapatmaz)

- **F-1 testi:** E-1…E-4. E-5 ve T-3 DL-2'nin kabulüne kalır.
- **F-2:** AC-31 (1)–(2) sabotajları ölçülen davranışın bulunduğu yerde, fırlatan sahte forwarder ile (`RoleAuditRecorderTests.cs:56-68`).
- **F-4 önerisi:**
  - İlk koşudaki geriye dönük kayıtlar yazılır.
  - İletilen gövde `recordedLate = true` ile kayıt zamanını taşır. Asıl değişiklik zamanı bilinmediği için `occurredAt = null` olur.
  - AC-28 (b) ve Validation Rules kabul kaydında bu istisnayla hizalanır.
- **F-5:** metin düzeltmesi kabul kaydında yapılır.

Bu kayıt commit'ten sonra değiştirilmez.
