# ADR-001 — İzin modül atfı, ve ekrana özel modül adı

| | |
|---|---|
| **Durum** | Kabul edildi |
| **Tarih** | 2026-09-08 |
| **Karar veren** | Sahip (CONTROL TOWER önerisi üzerine) |
| **Bağlam** | `fix/rbac-permission-module-attribution`, MOD-0288-FU02/FU03, MOD-0024 |
| **Etkilenen** | AuthService izin kataloğu · Rol İzinleri ekranı · gelecek Meeting modülü |

---

## 1 · Karar bir: modül atfı ile yetki sınırı ayrı iki sorudur

`Permission.Module` iki soruyu birden cevaplıyordu. Rol İzinleri ekranını
**gruplandırıyordu**, ve `PermissionScope` ondan **sınıflandırılıyordu**. Yani
bir yeniden gruplama, aynı zamanda ve sessizce, "bu izni kim taşıyabilir"
sorusunun cevabını da değiştirebiliyordu.

Bunlar artık iki ayrı yerden gelir:

- **Module** — özelliğin sahibi modül. Açık bir atıf (tohumdaki `moduleOverride`,
  manifestteki `ModuleCode`) varsa o kazanır; yoksa anahtardan **türetilir**.
- **Scope** — kiracı/platform yetki sınırı. Türetmeden **önceki** atıftan
  sınıflandırılır.

Bu ikinci cümle kararın kendisidir: `Permission` kurucusundaki `legacyAttribution`
satırı silinip `Scope` türetilmiş `Module`'den hesaplanırsa, her `platform.*`
anahtarı sessizce kiracı kapsamına düşer. İki muhafız bunu ölçer:

    dotnet test services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/ \
      --filter "FullyQualifiedName~PermissionScopePreservationTests"

Sabotaj yapılıp geri alındı: `Scope`'u türetilmiş `Module`'den sınıflandırmak
`PermissionScopePreservationTests` **ve** `Mod0029Fu29PermissionSeedHardeningTests`
testlerini kırmızıya düşürür.

### Türetme kuralı

Anahtar `{adalanı}.{kaynak}.{aksiyon}` biçimindedir. Ad alanı bir **modül** ise
(`crm.accounts.read` → `crm`) olduğu gibi kalır. Ad alanı çok sayıda ürüne ev
sahipliği yapan bir **servis** ise, modül **ikinci segmenttir**. Servis ad
alanlarının tek kaynağı:

    services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/PermissionModuleAttribution.cs

`ppm` ve `pvg` bilerek bu listede **değildir**: bugün tek tutarlı bir modülün
gerçek kodudur, türetmek onları kaynak başına bir gruba parçalardı. Bir ad alanı
bu listeye ancak birden fazla modüle ev sahipliği yaptığı **gösterildiğinde**
eklenir.

Doğrulama (sayı değil komut — sayı kayar):

    mongosh "mongodb://localhost:27017/diten_auth_v3" --quiet --eval \
      'db.permissions.countDocuments({Module:{$in:["platform","auth","mdm"]}})'      # 0 olmalı

    mongosh "mongodb://localhost:27017/diten_auth_v3" --quiet --eval \
      'db.permissions.aggregate([{$group:{_id:"$Scope",n:{$sum:1}}}]).toArray()'     # migration öncesiyle aynı

---

## 2 · Karar iki: `tasks` ikiye AYRILMAZ; ekrana özel ad köprüsü kullanılır

### Sorun

`tasks` modülünün adı `"Görev Tanımları / Task Settings"`. `TaskManifestProvider`
on sayfa yayınlıyor: dördü çalışma yüzeyi (`/Tasks`, `/Tasks/Create`,
`/Tasks/{id}`, `/Tasks/{id}/Edit`) ve hepsi bilerek `IsNavigationVisible: false`;
altısı ayar ekranı ve menüde görünüyor.

Ad **menü için doğru** — menüye çıkan her şey gerçekten ayar ekranı.
Ad **Rol İzinleri için yanlış** — o ekran on altı iznin hepsini gösteriyor,
`create`/`update`/`delete` dahil, ve grup başlığına aynı dizeyi basıyor.
Kullanıcının sorusu haklıydı: *"görev yaratma yetkisi neden Görev Tanımları'nın
altında?"*

### Reddedilen: adı geri almak

Eski ad menüde iki kusur üretiyordu — görev listesi vaat edip konfigürasyon
veriyordu, ve yanındaki "Görev Merkezi" ile karışıyordu.
`TaskModuleDisplayNameRenameMigration` tam bu yüzden yazıldı. Adı geri almak
o iki kusuru geri getirir. ⚠ Bir sonraki tur bunu denemesin.

### Reddedilen: `tasks` → `tasks` + `task-settings`

Rol düzeyinde ayrım **zaten mümkün**; izinler çip çip veriliyor. Ayırmanın tek
kazancı modül **hakkı** düzeyinde ayrım ve tek-ad-tek-şey olurdu. Sahibin kararı:
bu kazanç, maliyetini karşılamıyor — bir module pack, iki manifest, bir migration,
ve MOD-0024 kimlik kararına dokunmak.

### Seçilen: ekrana özel ad köprüsü

    Nav.Module.TASKS   = "Görev Tanımları"   → sol menü      (DEĞİŞMEZ)
    Perm.Module.TASKS  = "Görevler"          → Rol İzinleri  (YENİ)

`ModuleLabel.buildMap` zaten katmanlı; en üste bir katman eklenir: *izin ekranına
özel ad varsa onu kullan, yoksa menü adına düş, o da yoksa humanize.* Modül kodu,
izin anahtarı ve hak modeli — hiçbiri değişmez.

**Bu bir desen kararıdır.** Aynı çift-anlamlılık başka bir modülde çıktığında
aynı köprü kullanılır; modül bölmek son çaredir.

---

## 3 · Mimari sonuç: izinler kaynak modülde durur, Görev Merkezi toplayıcıdır

Bu bir karar değil, kaydedilmesi gereken bir **olgudur** — çünkü bilinmediğinde
yanlış karar üretir.

`Features/WorkAggregation/Providers/IWorkItemProvider.cs`: her sağlayıcı
`RequiredActionPermissions` beyan eder ve API katmanı **yalnız beyan edileni**
çağıranın haklarına karşı değerlendirir. Yani izin her zaman **kaynak modülde**
durur. `work-aggregation` yalnız `inbox.view` taşır ve sıfır aksiyon beyan eder;
manifestinin kendi yorumu bunu "read/projection only" diye yazar.

Sonuç: **Görev Merkezi hiçbir zaman izin sahibi olmayacaktır.** Ona izin eklemeye
çalışan her tur yanlış yerdedir.

Bugün üç sağlayıcı var (`TaskWorkItemProvider`, `WorkflowApprovalWorkItemProvider`,
`HttpWorkItemProvider`). **Meeting dördüncüsü olacak** ve aynı şekli alacak:
çalışma yüzeyi menüde görünmez ve Görev Merkezi'ne akar, ayar ekranları menüde
görünür. Yani §2'deki çift-anlamlı ad sorunu Meeting'de **aynen tekrarlanır** ve
çözüm de aynıdır: köprü, bölme değil.

⚠ **Kodda yazılı tuzak.** Bir sağlayıcı bir izni kontrol edip beyan etmezse,
kullanıcı o izne sahip olsa bile aksiyon sessizce `PERMISSION_DENIED` görünür.
MOD-0024 eklenirken tam olarak bu oldu. Meeting sağlayıcısı yazılırken
`RequiredActionPermissions` ⇔ `actor.Has(...)` eşitliğini ölçen bir test **şarttır**.

---

## 4 · Bu kararın açık bıraktıkları

| ne | nerede izleniyor |
|---|---|
| `tasks` çalışma zamanı/ayar ayrımı — Meeting kapsamı netleşince yeniden değerlendir | BL-340 |
| Katalogdan modül silmek Auth izinlerini silmiyor (DELETE-sync) | BL-339 |
| `platform.tasks.update` sekiz uca birden kapı açıyor | BL-341 |
| `mod0251`, `person`, `lookups` — modül kodu olmayan gruplar | BL-342 |
