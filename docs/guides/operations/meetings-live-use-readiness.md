# Toplantılar — canlı kullanım hazırlığı (MOD-0357 Management Review & Cadence)

> Kime: modülün sahibi ve canlı veriyi girecek kişiler (kiracı yöneticisi, İK, sunucu yöneticisi).
> Ne işe yarar: Toplantılar modülünün canlıda **kullanılabilmesi** için hangi verinin, hangi sırayla, kim tarafından
> girileceğini tek yerde toplar. Kart §6.1 (S-89) gereğidir.
> Hazırlayan: CONTROL TOWER (Toplantılar hattı), 2026-10-29. Kod okumaları bu dalın HEAD'inde yapıldı; canlı değerler CT'nin
> 2026-10-27 ölçümüdür (sahibin oturumu, canlı sitede salt okunur). Ölçülmeyen yerde "ölçülmedi" yazar.
>
> Bağlı belgeler (çelişkide onlar kazanır):
> - Eylül canlıya geçiş rehberi: [go-live-2026-09-meetings-task-engine.md](go-live-2026-09-meetings-task-engine.md)
>   (§3 arka plan işleri, §4 e-posta, §5 izinler, §6 organizasyon verisi, §7 canlıdan sonra kontrol). Bu rehber onu
>   tekrar etmez, ona gönderir.
> - Paket: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` §24.26
>   "Live-use readiness" ve §22 sahip kararları (D1–D5).
> - İş listesi: [meetings-mod0357-work-list-2026-10-27.md](../../roadmap/plans/meetings-mod0357-work-list-2026-10-27.md),
>   "Kısa liste" A1–A8.
> - Toplantı kataloğu taslağı ve raporu: `.ct-teslim/meetings-catalogue/catalogue-report.md` (depoya girmez; 22 ana tür,
>   51 alt tür, görev türü kataloğunda olmayan 3 aksiyon kodu, 11 açık soru). Katalog soruları **orada**dır, burada
>   tekrarlanmaz.

## 1. Bugün canlıda durum

Kaynak: CT ölçümü, 2026-10-27, canlı site, salt okunur.

| # | Konu | Ölçülen | Anlamı |
|---|---|---|---|
| 1 | Organizasyon birimleri | 39 birim, kök birimler var | Görev ve toplantı açmanın ön şartı karşılanıyor (BL-366) |
| 2 | Aktif kullanıcılar | 22 | — |
| 3 | Aktif pozisyonu olan kullanıcı | 17 var, **5 yok** | Bu 5 kişi toplantı düzenleyemez, katılımcı seçicide çıkmaz |
| 4 | Daveti tamamlanmamış kullanıcı | 8 kişi hâlâ "davet edildi" | Henüz giriş yapamazlar |
| 5 | Toplantı türü | **0** | Yeni toplantıda tür zorunlu → **bugün kimse toplantı açamaz** |
| 6 | Görev türü | 6 (İspanyolca adlı) | Onaylı 130 türlük katalog v3.1 yüklü değil |
| 7 | Arka plan işleri | Zaman girişi ayar ucu `reminderJobEnabled: false` diyor | Arka plan işleri **büyük olasılıkla kapalı** (çıkarım; depo varsayılanı `BackgroundJobs:Enabled = false`, `services/Diten.Platform/src/Diten.Platform.API/appsettings.json` satır 53–62; BL-536). Doğrudan ölçülmedi |
| 8 | E-posta (SMTP) ayarı | ölçülmedi | — |
| 9 | Kiracıya açık modüller (Toplantılar, Görevler, Görev Merkezi, İş Raporu) | ölçülmedi | Açık değilse rollere toplantı izni gitmez (Eylül rehberi §5, BL-410) |
| 10 | Rollerdeki `platform.meetings.*` izinleri | ölçülmedi | — |
| 11 | Tüzel kişilik kaydı, çalışma takvimi, tatiller, kiracı saat dilimi | ölçülmedi | — |

## 2. (a) Başka modüllerden gelen veri — önce bunlar girilir

Toplantılar bu verileri **okur** ama kendi ekranından girmez. Sıra tablodaki sıradır. "Ekran" sütununda sayfanın adı ve
adresi yazar; menüdeki tam yeri ölçülmedi.

| # | Ne | Modül | Ekran | Kim girer / sahibi | Toplantılar neden ister | Bugün tamam mı? |
|---|---|---|---|---|---|---|
| a1 | Tüzel kişilik (şirketler) | MOD-0220 Corporate Secretarial / Entity Management (tüzel kişilik) | Legal Entities (`/LegalEntities`) | Kiracı yöneticisi / sahip | Organizasyon birimi bir tüzel kişiliğe bağlı olmak zorunda (Eylül rehberi §6, BL-366) | Kök birimler var, demek ki en az bir tüzel kişilik var (çıkarım); liste ölçülmedi |
| a2 | Organizasyon kökü ve birimleri | MOD-0288 Organization, Person & Position Directory | Organization Units (`Views/Organization/OrganizationUnits`) | İK / organizasyon yöneticisi | Kök birim yoksa görev ve toplantı açılamaz | **Evet** — 39 birim, kökler var |
| a3 | Pozisyonlar (kodlarıyla) | MOD-0288 | Positions (`Views/Organization/Positions`) | İK / organizasyon yöneticisi | Düzenleyen ve katılımcı olabilmek; ileride türlerin varsayılan katılımcı pozisyonları (S19, S22, S24) | Kısmen — 17 kişinin pozisyonu var |
| a4 | Pozisyon atamaları (kim hangi pozisyonda) | MOD-0288 Organization, Person & Position Directory (pozisyon atamaları; MOD-0288-FU01) | Position Assignments (`Views/Organization/PositionAssignments`; ikinci bir ekran `HumanCapital/PositionAssignments` da var, hangisinin kalacağı kararı bekliyor — paket §24.26) | İK | Aktif pozisyonu olmayan kişi düzenleyen olamaz (`MEETING_ORGANIZER_INVALID`), seçicide çıkmaz, ona görev verilemez | **Hayır** — 5 aktif kullanıcının pozisyonu yok |
| a5 | Kullanıcılar (davetin tamamlanması) | MOD-0018 RBAC/ABAC Authorization | Users (`Views/Governance/Users`) | Kiracı yöneticisi; kullanıcı daveti kendisi kabul eder | Giriş yapamayan kişi daveti göremez, kabul / ret veremez | **Hayır** — 8 kişi "davet edildi" |
| a6 | Kiracıya modül açılması: Toplantılar, Görevler, Görev Merkezi, İş Raporu | MOD-0018 (modül yetkilendirmesi) | ölçülmedi (kiracı modül yetkilendirme ekranı) | Platform / kiracı yöneticisi | Açılmazsa Admin rolü toplantı ve görev anahtarlarını almaz; kimse ekranları kullanamaz (Eylül rehberi §5, BL-410) | ölçülmedi |
| a7 | Roller ve `platform.meetings.*` izinleri (aşağıdaki tablo) | MOD-0018 | Roles, Permissions, User Role Assignments (`Views/Governance/Roles`, `Permissions`, `UserRoleAssignments`) | Kiracı yöneticisi | Her ekran ve her eylem izin ister | ölçülmedi |
| a8 | Görev türü kataloğu (v3.1, 130 tür) ve katalogda eksik 3 kod (`COM-ACTION`, `FIN-ACTION`, `ITG-ACTION`) | MOD-0024 Task Engine | Task Types (`/Tasks/TaskTypes`; toplu yükleme iş listesi Dalga 2) | Görev türü kataloğu sahibi (kimin yükleyeceği açık: iş listesi §6 soru 3) | Toplantı türlerinin varsayılan aksiyon görev türü (S24-7); toplantıdan doğan görevler | **Hayır** — 6 İspanyolca adlı tür var, v3.1 yok |
| a9 | Çalışma takvimi, resmî tatiller, kiracı saat dilimi | MOD-0362 Working Calendar & Public Holidays | Working Calendars (`Views/WorkingCalendar`; platform tarafı `Platform/WorkingCalendars`) | Kiracı yöneticisi | Başlık yer tutucuları (`{Week}`, `{Month}`…, S21, S22); ileride "Daily" toplantının tatil günü atlanması | ölçülmedi |
| a10 | E-posta (SMTP) | MOD-0027 Notification Service (Email/SMS/WhatsApp) | Canlı ortam ayarı (`Smtp` bloğu → `TenantMessagingSettings` platform satırı) | Sahip / sunucu yöneticisi | Davet, değişiklik, iptal postası ve `.ics` takvim dosyası. Sunucu kimlik doğrulama (AUTH) sunmuyorsa posta sessizce düşer, toplantı yine oluşur (Eylül rehberi §4) | ölçülmedi |
| a11 | Arka plan işleri (Hangfire) | Platform (iş kimlikleri MOD-0027, MOD-0357, MOD-0024) | Canlı ortam ayarı `BackgroundJobs` | **Sunucu yöneticisi** | Kapalıysa: seri toplantılar hiç üretilmez, gönderilemeyen davet postası yeniden denenmez (ilk gönderim çalışır), "süresi yaklaşıyor" bildirimi gitmez (A1, BL-536) | **Büyük olasılıkla kapalı** (satır 7) |

### Arka plan işleri — açarken dikkat (a11)

- Açılacak dört iş ve ayar adı Eylül rehberi §3'tedir: `Diten.Platform.MOD-0027.EmailDispatchJob`,
  `Diten.Platform.MOD-0357.MeetingSeriesSweepJob`, `Diten.Platform.MOD-0024.TaskRecurrenceSweepJob`,
  `Diten.Platform.MOD-0024.TaskDueSoonSweepJob`. Ayar: `BackgroundJobs:Enabled = true` ve her biri için
  `BackgroundJobs:EnabledJobs:<iş kimliği> = true`. Yalnız bu dördü açılır; listedeki diğer işler bu modülün konusu değildir.
- ⚠ **Açıldığı an birikmiş iş hemen çalışır.** Kapalıyken biriken her şey ilk turda bir anda gider: yeniden denenecek
  postalar gönderilir, vadesi geçmiş seri toplantılar üretilir ve davetleri çıkar, tekrarlayan görevler oluşur,
  "süresi yaklaşıyor" bildirimleri toplu gider. Bugün canlıda toplantı 0 olduğu için seri tarafında birikme beklenmez
  (çıkarım); posta kuyruğu ve tekrarlayan görevler ölçülmedi.
- Bu yüzden önce **deneme (dry-run)**: açmadan önce bekleyen postaları ve etkin seri / tekrarlayan görev kayıtlarını salt
  okunur sayın; işleri mesai dışında ve birer birer açın; ilk turdan sonra gönderilenleri okuyun. Önce yedek alınır
  (Eylül rehberi §2).
- Dashboard canlıda kapalı kalır (Eylül rehberi §3).

### İzinler — hangi rol neyi ister (a7)

Kaynak: kod (`services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/MeetingModels.cs`,
`MeetingManifestProvider.cs`, `services/Diten.Platform/src/Diten.Platform.API/Controllers/Meeting*Controller.cs`).
Önerilen rol sütunu öneridir, karar değildir (bkz. §4 soruları 4–7).

| İzin anahtarı | Ne açar (kodda) | Önerilen rol |
|---|---|---|
| `platform.meetings.read` | Toplantılar listesi, toplantı detayı, Toplantı Raporu sayfası; yalnız düzenlediği ya da katıldığı toplantıları gösterir | Toplantıya katılacak herkes |
| `platform.meetings.create` | Yeni toplantı sayfası ve oluşturma | Toplantı düzenleyecek herkes |
| `platform.meetings.update` | Düzenleme sayfası; katılımcı, gündem, saat değişikliği, iptal | Toplantı düzenleyecek herkes |
| `platform.meetings.minutes-write` | Tutanak yazma | Toplantı düzenleyenler (ve tutanak tutan kişi) |
| `platform.meetings.minutes-publish` | Tutanağı yayımlama | Toplantı düzenleyenler |
| `platform.meetings.types-manage` | Toplantı Türleri sayfası (ekle, düzenle, sil) | 1–2 kişi: katalog sorumlusu |
| `platform.meetings.series-manage` | Toplantı Serileri sayfası (tekrarlayan toplantı) | Tekrarlayan toplantı yöneten yöneticiler |
| `platform.meetings.read-all` | Kiracıdaki **bütün** toplantıları görme (rapor dahil); `read`'den ayrı, kendiliğinden gelmez (§22 D3) | Yalnız kalite (QA) ve üst yönetim |
| `platform.meetings.delete`, `platform.meetings.bulk-delete` | Ekran eylemi olarak tanımlı; Toplantılar denetleyicisinde bunları isteyen bir silme ucu bulunmadı (kod okundu) | Şimdilik kimseye gerekmez |

Toplantıyla birlikte gereken başka anahtarlar (Eylül rehberi §5): `platform.work-aggregation.inbox.view` (Görev Merkezi;
davet kartı orada kabul / ret edilir), `platform.tasks.create` (toplantıdan görev açmak; paket §24.26). `platform.tasks.read-all`
yalnız açıkça verilir.

Not: `platform.meetings.invite`, `change`, `cancel`, `removed`, `organizer-added`, `organizer-updated`, `organizer-cancelled`,
`invite-undelivered` izin değil, **bildirim olay kodlarıdır** (e-posta şablonları); rollere verilmez.

## 3. (b) Toplantılar'ın kendi verisi — (a) bittikten sonra

| # | Ne | Ekran | Kim hazırlar | Durum |
|---|---|---|---|---|
| b1 | Toplantı türleri (ana tür + alt tür, varsayılan başlık, süre, tekrar, yer, gündem şablonu) | Toplantı Türleri (`/Meetings/MeetingTypes`); toplu yükleme içe aktarıcıyla (S24) | Katalog: yönetici; gündem: her toplantının iş sahibi; kalite işaretleri: QA süreç sahibi; pozisyon eşlemesi: İK | **Sahip kararı: bekle.** Elle 4–6 tür girilmez (A6 uygulanmaz); iki kademeli tür revizyonu (ana tür + alt tür) gelince katalog dosyası (CT hazırladı, `.ct-teslim/meetings-catalogue/`) S24 içe aktarıcısıyla yüklenir. Katalogdaki 11 soru cevaplanmadan yüklenmez. Bu süre içinde canlıda toplantı açılamaz (§1 satır 5) |
| b2 | Tekrarlayan seriler (isteğe bağlı) | Toplantı Serileri (`/Meetings/Series`); ileride oluşturma formunda "Tekrarla" (S23) | Düzenleyenler (`series-manage`) | Türler yüklendikten ve arka plan işleri açıldıktan sonra. İşler kapalıyken seri **hiç toplantı üretmez** |
| b3 | İlk deneme toplantısı | Toplantılar → Yeni toplantı (`/Meetings/Create`) | Sahip + ikinci bir kullanıcı; CT ölçer | Türler yüklendikten sonra. Eylül rehberi §7 madde 4, 5, 6, 9 izlenir: davet postası `.ics` ile gelir, Görev Merkezi'nde kabul, saat değişikliği postası, iptal postası, katılımcı çıkarma (iş listesi A5) |

## 4. Sahibe sorular

CT bunları **birer birer** sorar (kart §6.1 madde 2). Toplantı kataloğunun 11 sorusu burada yoktur; katalog raporundadır.

1. Organizasyondaki 39 birim ve kökleri bugünkü şirket yapınızla aynı mı? — Öneri: evet ise dokunmayın; eksik ya da fazla birim varsa İK önce onu düzeltsin.
2. Pozisyonu olmayan 5 aktif kullanıcı toplantıya katılacak mı, katılacaksa hangi pozisyonda? — Öneri: katılacak herkese İK bir pozisyon atasın; sistem ya da servis hesabıysa pozisyonsuz kalsın.
3. "Davet edildi" durumundaki 8 kişi toplantıları kullanmaya başlamadan önce girişlerini tamamlasın mı? — Öneri: evet; ilk toplantılarda olacak kişilere davet yeniden gönderilsin, diğerleri sonra.
4. Toplantı açma ve düzenleme (`create`, `update`, `minutes-write`, `minutes-publish`) kimde olsun? — Öneri: bütün çalışanların rolünde; toplantı zaten yalnız düzenleyene ve katılımcılara görünür.
5. Toplantı türlerini kim yönetsin (`types-manage`)? — Öneri: tek bir katalog sorumlusu ve bir yedeği; başka kimse.
6. Tekrarlayan toplantı serilerini kim yönetsin (`series-manage`)? — Öneri: düzenli toplantı yöneten bölüm yöneticileri.
7. Bütün toplantıları kim görsün (`read-all`)? — Öneri: yalnız kalite sorumlusu ve üst yönetim (§22 D3).
8. Toplantılar, Görevler, Görev Merkezi ve İş Raporu modülleri canlı kiracıya birlikte açılsın mı? — Öneri: evet, dördü birlikte; biri eksik kalırsa toplantıdan görev ve davet kartı çalışmaz.
9. Görev türü kataloğu v3.1'i (130 tür) kim yüklesin ve bugünkü 6 İspanyolca adlı tür ne olsun? — Öneri: kataloğu Görevler tarafı yüklesin, 6 eski tür kullanılmıyorsa silinmesin, pasif yapılsın.
10. Çalışma takviminde hangi ülkenin tatilleri ve hangi saat dilimi geçerli? — Öneri: merkez şirketin ülkesi ve saat dilimi; başka ülkedeki şirketler için ayrı takvim sonra.
11. Davet postaları hangi adresten gitsin ve bu adresin SMTP bilgisini kim girsin? — Öneri: şirket alan adında ayrı bir gönderici adres (ör. "noreply"), kimlik doğrulamalı; bilgiyi sunucu yöneticisi girsin.
12. Arka plan işlerini canlıda kim, ne zaman açsın? — Öneri: sunucu yöneticisi, yedekten sonra, mesai dışında, yalnız dört işi birer birer.
13. Canlı deneme toplantısını hangi iki kişi yapsın? — Öneri: siz ve gerçek e-posta adresi olan bir yönetici.
14. Pozisyon atamasında iki ekrandan (`Organization/PositionAssignments`, `HumanCapital/PositionAssignments`) hangisi kullanılsın? — Öneri: karar çıkana kadar `Organization/PositionAssignments`; ikisine aynı anda giriş yapılmasın.
15. Türlerde varsayılan katılımcı pozisyonlarını İK'da kim eşleyecek? — Öneri: pozisyonları giren İK kişisi; katalog yüklendikten sonra.
16. Türler yüklenene kadar canlıda kimsenin toplantı açamaması kabul mü? — Öneri: evet (bekleme kararınıza uygun); deneme toplantısı türler yüklenince yapılır.

## 5. Yardım ve tur

Kural UXH-001 (`.antigravity/rules/page-help-and-keyboard.md` §5–6). Kaynak: depo, bu dalın HEAD'i.

- **"?" yardım metni: yedi toplantı sayfasının hiçbirinde yok.** Yedisi de yardım borç defterinde:
  `docs/roadmap/backlog/page-help-ledger.md` satır 134–140 (MEETINGCREATE, MEETINGDETAIL, MEETINGEDIT, MEETINGREPORT,
  MEETINGS, MEETINGSERIES, MEETINGTYPES).
- **Tanıtım turu: yok.** `frontend/Diten.Web/wwwroot/assets/js/shared/diten-tour-catalog.js` başındaki açıklama Toplantılar'ı
  turu olacak beş akıştan biri sayıyor, ama katalogda Toplantılar için kayıt yok.
- **Planlı:** paket §24.8 S13 "Page help and Meetings tour" (iki tur: `MEETINGS` ve `MEETINGDETAIL`; yardım metni 7 dil);
  S20 ve S24'ün yeni sayfaları kendi yardımlarıyla gelir. İş listesi A7.
- Canlı ekranda görülerek ölçülmedi (depo okundu).

## 6. Giriş adımları

Veriyi **sahip ve ilgili kişiler girer** (ortak veri); CT her adımdan sonra salt okunur ölçer ve sonucu yazar. CT canlıya
yazmaz.

1. **CT** — §4 sorularını birer birer sorar, cevapları kaydeder.
2. **Sunucu yöneticisi** — Platform veritabanının ve arka plan iş deposunun yedeğini alır (Eylül rehberi §2).
3. **Sunucu yöneticisi** — SMTP ayarını girer ya da doğrular; sunucunun kimlik doğrulama sunduğunu teyit eder (Eylül rehberi §4).
4. **Sunucu yöneticisi** — Bekleyen postaları ve etkin tekrarlayan kayıtları sayar (deneme); sonra dört arka plan işini mesai dışında birer birer açar (§2 "Arka plan işleri"). **CT** ilk turdan sonra ölçer.
5. **Kiracı yöneticisi** — Toplantılar, Görevler, Görev Merkezi ve İş Raporu modüllerini canlı kiracıya açar (soru 8).
6. **Kiracı yöneticisi** — "Davet edildi" durumundaki kullanıcılara daveti yeniden gönderir; kullanıcılar girişlerini tamamlar (soru 3).
7. **İK** — Pozisyonu olmayan 5 kullanıcıya pozisyon atar (soru 2, 14). **CT** ölçer: aktif pozisyonu olan kullanıcı sayısı.
8. **Kiracı yöneticisi** — Rollere toplantı izinlerini ve `platform.work-aggregation.inbox.view`, `platform.tasks.create` anahtarlarını verir (§2 izin tablosu, soru 4–7). **CT** ölçer: rol başına anahtar listesi.
9. **Görev türü kataloğu sahibi** — v3.1'i ve eksik üç aksiyon kodunu yükler (soru 9; katalog sorusu 3). **CT** ölçer: tür sayısı ve üç kod.
10. **Kiracı yöneticisi** — Çalışma takvimini, tatilleri ve saat dilimini girer (soru 10).
11. **CT** — (a) bölümünün tamamını ölçer ve sahibe sade bir özet verir. Eksik varsa (b)'ye geçilmez.
12. **Yönetici ve katalog sahipleri** — Toplantı kataloğunun 11 sorusunu cevaplar (katalog raporu §8).
13. **Katalog sorumlusu** (`types-manage`) — İki kademeli tür revizyonu ve S24 içe aktarıcısı canlıya geldikten sonra CT'nin hazırladığı katalog dosyasını yükler. **CT** ölçer: ana tür ve alt tür sayısı, raporlanan satırlar.
14. **Sahip ve ikinci kullanıcı** — İlk deneme toplantısını açar ve Eylül rehberi §7 madde 4, 5, 6, 9'u yürütür. **CT** ölçer: postaların ve `.ics`'in gelişi, Görev Merkezi'ndeki davet kartı.
15. **Düzenleyenler** — İhtiyaç olan tekrarlayan serileri açar (`/Meetings/Series`). **CT** bir sonraki saatlik turdan sonra üretilen toplantıyı ölçer.
