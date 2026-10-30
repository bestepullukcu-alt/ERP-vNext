# MOD-0280-FU02 · AuthService açık izin listesine ekleme izni (XMC-001, kart S-90)

- Tarih: 2026-10-31 · Karar veren: sahip (proje sahibi), bu sohbette butonlu soruyla — seçim "Yalnız açık izin" ·
  Kaydeden: CT · İzin ve devamsızlık
- Seçenek metni: "Üç anahtar, AuthService'teki açık izin listesine girer; Zaman Girişi'nin kişi raporu anahtarlarıyla aynı
  yol. Hiçbir otomatik yol bunları vermez, kiracı rol yönetiminden elle verir. AuthService'e küçük bir ek gerekir ve
  birinci parçadan önce ayrı bir küçük iş olarak gider. Bu seçim o dosya için başka modül (XMC-001) iznidir; AuthService
  sahibinin incelemesi yine gerekir."
- Reddedilen seçenek: anahtarları `.read-all` diye adlandırmak. Modül yetkisi açılınca kiracı Admin'i bu anahtarları otomatik
  alabilirdi; aynı otomatik yol MOD-0024 ve CRM anahtarlarında sahip kararıyla kapatılmıştı.
- Neden sahip: `scripts/ct_route.py --kind delivery --module MOD-0018` hiçbir kuleye atanmış dilim bulmadı; kartın §8
  tablosunda platform-shared-services için sahip "sahip kararı bekliyor" yazar. Bu kayıt MOD-0018'e kalıcı bir sahip atamaz.

## Neden gerekti

Paket revizyon 09 "izin anahtarlarını rol şablonu vermez" diyordu; bu cümle yanlıştı. Ölçüm (2026-10-30, kod):

- AuthService `DefaultRolePermissionTemplate.SelectFor`, `ViewerRole` dalı: varsayılan Viewer rolü eylemi `read` olan her
  kiracı anahtarını alır.
- Platform `PlatformPermissionAutoRegistrationWorker`: her `[HasPermission]` anahtarını açılışta kataloğa iter; manifest beklemez.

Sonuç: `leave.admin-requests.read`, `leave.entitlements.read` ve `leave.sync-runs.read` İK dışındaki kullanıcılara kendiliğinden
giderdi. Kod yazılmadığı için hiçbir veri açığa çıkmadı.

## Kapsam (S-90: izin her ek için ayrıdır)

İzin yalnız şunları kapsar (paket dilimi DL-0):

- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — üç sabit ve
  `Keys` kümesine üç öğe; yalnız satır ekleme (`Keys`'in son öğesine virgül ekleme sayılır).
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AccountKindTests.cs` — kümeyi birebir sayan testin
  adı, sayısı ve üç yeni satırı.
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/LeaveExplicitGrantPermissionTests.cs` — yeni test.

Kapsamaz: rol şablonu (`DefaultRolePermissionTemplate.cs`), AuthService'in başka kodu, seed, başka anahtar, `leave.requests.create`
gibi çalışan anahtarlarının varsayılan verilmesi. Gerekirse iş durur ve yeni izin istenir.

## MOD-0018 paketi

Kart §7c/2 eki önce o modülün paketine öneri ister. Ölçüm: açık izin listesini sayan bir MOD-0018 ana paketi yok (yalnız
`MOD-0018-FU27` kendi anahtarlarını anar); liste koddadır ve MOD-0280-FU01 eki de pakete yazılmadan bu dosyaya girmiştir
(`79547b4e3`). Bu yüzden öneri MOD-0280-FU02 paketinin revizyon 10'undadır (§Repo Scope, §Authorization Convention,
§Teslim dilimleri DL-0, AC-58); o sürüm Codex'ten geçer ve onaylanır.

## Etkisi

- Yazar: CT · İzin ve devamsızlık (S-90/2). İnceleme: sahip, AuthService'in bugünkü sahibi olarak (S-90/3). Merge başkasınındır.
- Sıra: DL-0, anahtarları adı geçen kod main'e girmeden önce main'de olur.
- İzin, onaylı paket sürümüyle birlikte geçerlidir: revizyon 10'un onay satırı origin/main'de geçerli olmadan dağıtılmaz.
