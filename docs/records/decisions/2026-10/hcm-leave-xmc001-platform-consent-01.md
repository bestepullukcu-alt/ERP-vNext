# MOD-0280-FU02 · `Diten.Platform`'da kod yazma izni (XMC-001, paketin açık sorusu 1)

- Tarih: 2026-10-30 · Karar veren: sahip (proje sahibi), bu sohbette butonlu soruyla — seçim "Ben izin veriyorum" ·
  Kaydeden: CT · İzin ve devamsızlık
- Paket: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256
  `e9fc8122…` (ready-for-dev; onay satırı origin/main'de geçerli, `pack_approval.py check --ref origin/main` çıkış 0,
  2026-10-30)
- Neden sahip: kartın §8 tablosunda platform-shared-services için bugünkü sahip "sahip kararı bekliyor" yazar; bu yüzden
  izni proje sahibi verdi. Bu kayıt platform-shared-services'e kalıcı bir sahip atamaz.

## Kapsam (S-90: izin her ek için ayrıdır)

İzin yalnız paketin §Repo Scope listesindeki eklemeleri kapsar:

- `Diten.Platform` içinde izin modülünün kendi dosyaları — paketin revizyon 09 §Repo Scope'unda ölçülmüş yollarla
  (Platform'da ayrı bir `Persistence` projesi yok; depolar ve şema `Infrastructure/Persistence/` altında): `Domain/Entities/Leave/**`,
  `Domain/Enums/Leave/**`, `Domain/Repositories/ILeaveRepositories.cs`, `Application/Features/Leave/**`,
  `Infrastructure/Persistence/Repositories/LeaveRepositories.cs`, `Infrastructure/Persistence/Schema/PlatformSchemaManifest.Leave.cs`,
  `Infrastructure/Services/LeaveLinks.cs`, `API/Controllers/LeaveController.cs`, `tests/Diten.Platform.Application.Tests/Leave/**`.
- Platform'un ortak kayıt dosyalarına **yalnız satır ekleme** (var olan satır değişmez, taşınmaz, silinmez; eklenen satır
  yalnız izin türlerini anar; MOD-0280-FU01 emsali): `PlatformCollections.cs`, `SchemaProfile.cs`, `PlatformSchemaManifest.cs`,
  `Infrastructure/DependencyInjection.cs`, `Application/DependencyInjection.cs`, `PlatformRecurringJobRegistrar.cs`,
  `NotificationTemplateSeed.cs`. Sahip bunu 2026-10-30'da ikinci butonlu soruda "Paketi düzelt" seçimiyle onayladı (seçenek
  metni: "İzninizin bu eklemeleri de kapsadığını karar kaydına yazarım").
- Web tarafında izin modülünün kendi klasörleri ve ortak dosyalarda yalnız yeni anahtar ekleme (`PageHelpCatalog.cs`,
  `PageHelpResource.{7}.resx`, `SharedResource.{7}.resx`; kart S-38).
- Dilimler: DL-1, DL-2, DL-3, DL-4, DL-6, DL-7.

Kapsamaz:

- §Protected Paths'teki yollar: `Features/TimeEntry/**` (MOD-0280-FU01), `Features/Workflow/**`, `Features/Organization/**`,
  `Features/WorkingCalendar/**`, `Features/Tasks/**` (yalnız port üzerinden kullanılır), `ocelot.json` (integration-agent),
  HcmService ve MOD-0251 dosyaları.
- Yukarıdaki ekleme listesi dışında `Diten.Platform`'un ortak kodunda değişiklik; ortak denetim kategorisi (izin modülü
  zaman çizelgesi gibi var olan kategoriyi kullanır). Gerekirse iş paketi durur ve yeni izin istenir.

## Etkisi

- Paketin §Teslim dilimleri'ndeki XMC-001 kapısı bu kayıtla karşılanır. Kalan kapılar paketteki gibi bekler: DL-4 kabulü
  (çalışan sözleşmesi), DL-5 iki ekranı (Blueprint MCR), DL-7 (integration-agent görevi).
- Paketteki açık soru 1 bu kayıtla cevaplanır; paket revizyon 09 bu kayda bağlanır. İzin, onaylı paket sürümüyle birlikte
  geçerlidir: revizyon 09'un onay satırı origin/main'de geçerli olmadan kod dağıtılmaz.
- Not (2026-10-31): revizyon 09 onaylanmadı, yerine revizyon 10 geldi (sahip seçimi "Yalnız açık izin"). Bu izindeki yollar değişmedi;
  izin onaylanacak sürüm için geçerlidir ve o sürümün onay satırı origin/main'de geçerli olmadan kod dağıtılmaz.
