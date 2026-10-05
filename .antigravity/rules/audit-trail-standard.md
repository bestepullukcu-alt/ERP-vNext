---
description: "AUD-001 — Denetim kaydı (audit trail) standardı: veri değiştiren her komut denetlenir; denetlenmemek gerekçeli istisnadır; borç yalnız küçülür"
---

# Denetim Kaydı Standardı — AUD-001

> Bu kural 2026-10-02'de yazıldı çünkü canlıda bir kullanıcı hesabı silindi ve **kimin
> sildiği hiçbir yerde yazmıyordu** (BL-456; o komut düzeltildi). Sahip diğer ekiplerle
> konuştu: aynı eksik modüllerin çoğunda vardı.
>
> O gün ölçüldü (2026-10-02). Aşağıdaki sayılar **o günün** ölçümüdür ve bu dosyada güncellenmez;
> bugünkü sayıyı kural değil ölçüm söyler:
>
>     1030 yazma komutu (13 servis) · 317 denetleniyor · 5 gerekçeli istisna · 708 kabul edilmiş hiçbir ize gitmiyor
>
>     dotnet test tests/architecture/TenantArchitecture.ArchitectureTests \
>       --filter "FullyQualifiedName~Inventory_PrintsTheTable" --logger "console;verbosity=detailed"
>
> Ayrıntı ve bulgular: `docs/records/audits/2026-10/`.
>
> Sebep ihmal değil, kural yokluğu: `.antigravity/rules/` altında denetim kaydına ayrılmış
> dosya yoktu, `AGENTS.md` haritasında satırı yoktu, paket şablonu "denetlenen olaylar"
> istemiyordu, 20 ajandan hiçbiri sormuyordu ve hiçbir test ölçmüyordu. Mekanizma vardı
> ama **gönüllüydü**: işareti koyan denetleniyor, koymayan sessizce denetlenmiyordu.

---

## 1. Kural

**Kiracı ya da platform verisini değiştiren her komut denetlenir.** Denetlenmemek bir
istisnadır; istisna §6'daki sınıflardan biriyle ve yazılı gerekçesiyle bildirilir.
Sınıf dışında "denetlenmiyor" olamaz.

Bir komut şu dört durumdan **tam birindedir** — beşincisi yoktur:

| durum | nerede görünür |
|---|---|
| Denetleniyor — yol `a`, `b` ya da `c` (§5) | üretim kodunda (işaret ya da yazıcı) |
| Bildirilmiş istisna (§6) | `tests/architecture/audit-ledger/<servis>.md` → `## İstisnalar` |
| Bilinen borç — 2026-10-02'de denetimsiz bulunan komut | aynı dosya → `## Bilinen borç` |
| ~~Yeni ve denetimsiz~~ | **olamaz**: mimari testi kırmızıdır |

**Borç listesi yalnız küçülür.** Listeye satır eklemek yasaktır. Listedeki bir komut
denetlenir ya da silinirse satırı çıkarılır; çıkarılmazsa test kırmızıdır.

Bu cümle üç yerde **makineyle** tutulur — biri tek başına yetmez:

1. Defterdeki her sayı (servis başına borç · istisna · K2 borcu · yazan sorgu) ve kabul edilmiş her iz
   `AuditTrailStandardTests` içinde **sabittir** (tam eşitlik). Deftere satır eklemek testi kırar; sayıyı
   yükseltmek test dosyasını değiştirmeyi gerektirir.
2. CI (`scripts/run_phase1_gates.sh`, "audit ledger growth"): taban dala göre deftere `- Ad` satırı **ekleyen**
   değişiklik kırmızıdır — sayı da yükseltilmiş olsa.
3. İkisini birden aşmanın **kurallı** yolu Control Tower kararıdır: karar
   `tests/architecture/audit-ledger/CT-DECISIONS.md` dosyasına ad + gerekçeyle yazılır ve sabit sayı aynı
   değişiklikte yükseltilir.
4. Bu iki koruma her şeyi görmez; göremediği yollar §9'un "yakalamayan" sütununda tek tek yazılıdır (bir borç
   satırının adını başka bir komutla değiştirmek sayıyı korur ve yalnız PR'da CI'a takılır; dar sorgu algılayıcısı;
   adı serviste tek olan bir sınıf üyesine dayanan yol `c` notu). O yolların bekçisi inceleme ve ajan kapılarıdır.

## 2. Tanımlar

**Yazma komutu.** `services/<servis>/src` altında, MediatR `IRequest` / `IRequest<T>`
uygulayan (doğrudan ya da aynı serviste / `Diten.Building.Blocks` içinde tanımlı,
`IRequest`'ten türeyen bir arayüz üzerinden), soyut olmayan ve **sorgu olmayan** her tür.

**Sorgu.** Üretim kodunun kendi tanımı (Platform `AuditBehavior.IsQueryRequest`): tür adı
`Query` ile biter **ya da** ad alanı `.Queries` içerir. Geri kalan her istek yazma
komutudur — adı `Command` ile bitmese de. İki düzeltmeyle:

- **Ad, ad alanını yener.** Adı `Command` ile biten bir tür `.Queries` ad alanında dursa
  da ölçümde komuttur. Üretim kuralı ise onu sorgu sayar ve **kaydını yazmaz** — işaret
  taşısa bile. Bu yüzden böyle bir tür ayrı bir testle kırmızıdır: komut `.Queries`
  ad alanından çıkarılır. Üretim kuralına dokunulmaz.
- **Sorgu yazmaz.** İşleyicisi depoya yazan sorgu her komut listesinden gizlenir ve
  hiçbir kaydı olmaz. Ölçülenler defterde `## Yazan sorgular` altındadır; yeni bir
  yazan sorgu kabul edilmez — yazma bir komuta taşınır.

**Ad kuralı (CT kararı 2026-10-02).** Komutlarını MediatR ile göndermeyen iki serviste —
`Diten.PvgService` ve `Diten.ManagementGovernanceService`, **yalnız bu ikisinde** — adı
`Command` ile biten soyut olmayan her tür de yazma komutudur. Aynı adı taşıyan bir MediatR
komutu varsa o tür onun yüküdür ve ikinci kez sayılmaz. Üçüncü bir serviste MediatR isteği
olmayan bir `*Command` türü belirirse test kırmızıdır: ya MediatR isteği yapılır ya da
servis bu listeye Control Tower kararıyla eklenir.

Bu tanım tek sayımdır: envanter belgesi de mimari testi de onu kullanır
(`AuditTrailStandard/AuditTrailMeasurement.cs`).

**MediatR'a uğramayan yazma yolu.** Controller → servis → depo, minimal-API ucu, arka
plan işi, olay tüketicisi. Kural bunlar için de geçerlidir; **ölçüm bunları görmez**
(§9). Yeni kodda yazma yolu MediatR komutu olarak yazılır.

**Merkezi günlük.** Platform `audit_events` koleksiyonu (`IAuditService` → `audit_outbox`
→ `AuditOutboxWorker` → `audit_events`).

## 3. Kayıt neyi taşır

Her denetim kaydı şu alanların **hepsini** taşır:

| # | alan | merkezi günlükteki karşılığı |
|---|---|---|
| 1 | Kim — kullanıcı kimliği | `ActorId` + `ActorType` |
| 2 | Kim — o andaki adı (kullanıcı sonradan silinse / adı değişse de okunur) | `ActorDisplayNameMasked`, `ActorEmailMasked` |
| 3 | Servis çağrısıysa servis kimliği | `ActorType = System` + `SourceService` |
| 4 | Hangi kiracı | `TenantId` / `TargetTenantId` (platform geneli ise `IsPlatformGlobal`) |
| 5 | Hangi nesne | `EntityType` + `EntityId` |
| 6 | Hangi eylem | `Operation` + `RequestType` |
| 7 | Ne zaman (UTC) | `OccurredAtUtc` |
| 8 | Sonuç — başarılı / reddedildi | `Outcome` (`Succeeded` / `Failed` / `Denied`) |
| 9 | Korelasyon kimliği | `CorrelationId` |
| 10 | Değişiklikte: önce/sonra **ya da** değişen alan adları | `BeforeState` / `AfterState` / `Metadata` |

**Kayda YAZILMAZ:** parola, parola özeti (hash), token, API anahtarı, MFA sırrı,
kurtarma kodu; ve olayı anlamak için gerekmeyen kişisel veri (kimlik numarası, sağlık
verisi, tam adres, banka bilgisi). Bir alan değiştiyse **adı** yazılır, değeri değil.
Platform'da bunu `ISensitiveFieldRedactor` uygular; başka yolda sorumluluk komutu
yazandadır.

Reddedilen komut da kaydedilir (`Outcome = Failed` / `Denied`): yetkisiz bir silme
denemesi, başarılı bir silme kadar denetim bilgisidir.

## 4. Değişmezlik ve yazma hatası

**4.1 · Kayıt değiştirilemez ve silinemez.** Denetim kaydını ya da eşdeğer izi tutan
deponun arayüzünde `Update`, `Delete`, `Replace` yoktur — yalnız ekleme ve okuma.
İki adı konmuş istisna vardır ve ikisi de **kendisi denetlenir**:

- KVKK gereği aktör kişisel verisinin maskelenmesi (`RedactAuditActorCommand`): olayı
  silmez, üç alanı `[REDACTED]` yapar ve kimin, ne zaman, neden yaptığını yazar.
- Saklama süresi dolan kaydın imhası: yalnız adı konmuş bir saklama işiyle.
  ⚠ Ölçüldü 2026-10-02: saklama politikaları saklanıyor ve düzenlenebiliyor, ama onları
  `audit_events` üzerinde uygulayan bir iş, TTL ya da silme **yok**.

**4.2 · Kayıt yazılamazsa ne olur — bugünkü davranış (ölçüldü):**

| yol | yazma hatasında iş komutu | kaynak |
|---|---|---|
| `a` Platform `AuditBehavior` | **Devam eder.** `AppendAsync` hatayı yakalar, `EnqueueFailed` döner, uyarı loglanır. Komutu durduran tek dal `CriticalCategories` listesidir ve o liste **boştur**; `RejectedCritical` üreten hiçbir kod yoktur. | `AuditBehaviorOptions.cs`, `AuditService.cs` |
| `b` MDM iletimi | **Devam eder.** İletim hatası yutulur (uyarı logu). Handler istisna atarsa hiç iletim yapılmaz — başarısız komut kaydedilmez. **Adres ya da S2S anahtarı boşsa iletim hiç denenmez ve bu `LogDebug` düzeyinde yazılır** — varsayılan günlük düzeyinde görünmez. | `AuditForwardingBehavior.cs`, MDM `PlatformAuditForwarder.cs` |
| `b` Auth kullanıcı olayları | **Devam eder.** Yerel yazım + iletim; ikisi de hatayı yutar. **S2S anahtarı boşsa ya da kiracı boşsa iletim sessizce atlanır**; temel `appsettings.json` anahtarı boş taşır. | `UserAuditRecorder.cs`, Auth `PlatformAuditForwarder.cs` |
| Veri dışa aktarımı | **Durur (fail-closed).** Kayıt kuyruğa girmediyse dosya verilmez. | `DataExportAuditWriter.cs` |

Yani bugün dışa aktarım dışında **her yol "en iyi çaba"dır**: kayıt düşmezse iş yine
tamamlanır ve eksik kayıt yalnız uygulama günlüğünde bir uyarı satırıdır.

**4.3 · Kuralın kabul ettiği:**

| olay sınıfı | kabul edilen | durum |
|---|---|---|
| Kişisel verinin toplu dışa aktarımı | Kayıt yoksa işlem yok (fail-closed) | **uygulanıyor** |
| Kimlik, rol/izin, kiracı durumu, GxP kaydı (belge, imza, sapma, eğitim, kalite olayı), KVKK özel nitelikli veri | **Kayıt yoksa işlem yok (fail-closed) — sahip kararı 2026-10-02 (K2).** Kayıt ile iş verisi BİRLİKTE yazılır (aynı işlem ya da işlem-içi outbox); kayıt yazılamıyorsa komut başarısız olur ve kullanıcı "şu an yapılamadı" cevabını alır. "En iyi çaba" bu sınıfta kabul edilmez. **Bu sınıftaki her YENİ komut böyle doğar**; pakette yazma hatası davranışı açıkça yazılır ve testi vardır. Mevcut komutların bu davranışa geçirilmesi borçtur: defterde `## K2 borcu` bölümünde izlenir (sayısı testte sabit) ve ayrı iş paketleriyle kapatılır (bugün bu sınıfın hiçbir yolu fail-closed değil). **Bugün hiçbir yol bunu teslim etmiyor — uyulabilir yol §4.4.** | karar verildi · teslim eden yol YOK (§4.4) |
| Diğer iş kayıtları ve yapılandırma | En iyi çaba, **ama sessiz değil**: düşmeyen kayıt uyarı olarak loglanır ve sayılabilir olur | ⚠ **AÇIK — yol `b` bunu sağlamıyor.** Anahtar / adres boşken iletim atlanıyor ve MDM'de bu `LogDebug`: kayıt düşmüyor, uyarı da yok. Yol `a` sağlıyor (uyarı logu). Düzeltme üretim kodu ister; bu standart yalnız adını koyar. |

**4.4 · K2'nin uyulabilir yolu — bugün ne yapılır**

Karar net: kimlik / yetki / kiracı durumu / GxP / KVKK-özel sınıfında kayıt yazılamazsa işlem durur. Ölçüm de net:
**bugün hiçbir yol bunu sağlamıyor.**

| yol | neden sağlamıyor |
|---|---|
| `a` `AuditBehavior` | Kayıt handler'dan **sonra** kuyruğa yazılır; hata komutu durdurmaz. Durduran dal `CriticalCategories`'tir ve **boş kalmalıdır**: işlem bütünlüğü yokken doldurmak, iş verisi yazılmış bir komutu "başarısız" gösterir (`AuditBehaviorOptions.cs` uyarısı). |
| işlem içi (`ITransactionOwnedAuditCommand`) | Tasarım olarak doğru yol — kayıt iş verisiyle aynı işlemde. Ama `aday`: dev'de bu yoldan teslim edilmiş tek kayıt yok (K1), ve `AuditBehavior` yalnız 17 adlık sabit bir listeyi kabul ediyor. |
| `b` iletim | En iyi çaba; anahtar boşken hiç denenmiyor. |

Bu yüzden K2 sınıfı bir komut için kural şudur:

- **Auth içinde:** kayıt handler'ın içinde, iş yazmasından **önce** yazılır; yazılamazsa komut başarısız olur
  (önce-yaz kalıbı). Devralma dalındaki `IAuthAuditService.WriteOnceAsync` bu kalıbın örneğidir; **bu dalda henüz yok**
  — geldiğinde K2 sınıfı Auth komutlarının yolu odur. O güne kadar yeni K2 sınıfı Auth komutu = **engelli, Control
  Tower'a yaz.**
- **Platform içinde:** işlem içi yol (`ITransactionOwnedAuditCommand`). `AuditBehavior`'ın sabit listesine ad
  eklemek Control Tower kararıdır. **K1 kapanana kadar yeni K2 sınıfı Platform komutu = engelli, Control Tower'a
  yaz.** Yol `a` ile yazıp "sonra düzeltiriz" demek yeni K2 borcu üretir ve defter yeni K2 borcu kabul etmez.
- **Diğer servislerde:** ortak iletici paketi (K4) gelene kadar yeni yazma komutu zaten engellidir (§5).

**K2 borcu.** Bugün denetlenen ama kapalı-başarısız olmayan K2 sınıfı komutlar servisin defterinde
`## K2 borcu` altında durur, sayısı testte sabittir ve liste yalnız küçülür. "En iyi çaba yazan paket geçer" diye
bir kapı yoktur: K2 sınıfı komut ya kapalı-başarısızdır, ya K2 borcundadır (eski), ya da engellidir (yeni).

## 5. Üç geçerli yol

| yol | ne | ne zaman | komutta ne görünür |
|---|---|---|---|
| **a** | Platform içi: komut `IAuditableCommand` + `IAuditMetadataProvider` taşır; `AuditBehavior` yazar | Komut `Diten.Platform` içindeyse — **başka seçenek aranmaz** (K2 sınıfı ise §4.4) | komut türünün taban listesinde iki arayüz |
| **b** | Başka servisten merkezi günlüğe iletim: işaret + pipeline davranışı + `POST /api/internal/audit/append` | Yalnız iletici **gerçekten merkezi günlüğe yazıyorsa**. Bugün: MDM (tüzel kişi komutları) ve Auth (kullanıcı olayları). Diğer servisler için aşağıdaki K4 kararı | işaret, ya da handler'ın çağırdığı yazma üyesi |
| **c** | **Eşdeğer iz:** modülün kendi değişmez, kullanıcıya gösterilen geçmişi | Yalnız aşağıdaki **beş koşulun hepsi** sağlanıyorsa ve Control Tower izi kabul etmişse | handler'ın çağırdığı yazma üyesi (`Tip.Metot`) |

**Yazıcı izinde kanıt bir ÇAĞRIDIR.** Defterdeki belirteç `Tip.Metot` biçimindedir ve handler o yazma üyesini
çağırıyor olmalıdır. Tipi anmak kanıt değildir: bir enum'u ya da okuma metotları da olan bir depoyu, hiçbir şey
yazmayan handler da anar.

**K4 — KARAR (Control Tower, 2026-10-02): ortak iletici.** Denetim izi olmayan on serviste (CRM, HumanCapital, HCM,
EnterpriseStrategy, PPM, Procurement, TalentEcosystem, DevEnablement, ManagementGovernance, PVG) hedef tek bir ortak
iletici ve işaret arayüzüdür; `Diten.Building.Blocks` içinde durur ve ayrı bir iş paketiyle gelir.

- Servis başına MDM kalıbını **kopyalamak YASAKTIR** (yedi ayrı davranış, yedi ayrı kusur).
- O paket gelene kadar bu on serviste **yeni yazma komutu = engelli, Control Tower'a yaz.** Geçici kopya yok,
  borç satırı yok, "şimdilik kendi günlüğümüze yazalım" yok.
- Mimari testi işareti ve pipeline davranışını `Diten.Building.Blocks` içinde tanımlıysa da tanır; davranışın
  **kaydı** ise servisin kendisinde aranır — paylaşılan bir davranışı pipeline'ına eklemeyen servis hiçbir şey yazmaz.

**Neden CRM `aday`, Auth ve MDM `b`?** Üçü de "yayıncı / iletici çağırıyor". Fark kaydın nereye düştüğüdür:
Auth ve MDM ileticileri yapılandırıldığında merkezi günlüğe yazar (`POST /api/internal/audit/append`, iki istemci).
CRM yayıncılarının kayıtlı uygulaması varsayılan olarak uygulama günlüğüdür; HTTP modu hiçbir ayar dosyasında açık
değildir ve açılsa gönderdiği kategori / işlem değerleri Platform'da tanımlı değildir. İlki iz, ikincisi değil.
⚠ Auth ve MDM'nin `b` notu **yapılandırmaya bağlıdır**: anahtar boşsa onlar da hiçbir şey yazmaz (§4.2) — bu
açıktır ve K4 paketinin kapatması gereken şeydir.


**Yol `c` kabul koşulları** — beşi de ölçülür, biri eksikse iz `aday`dır ve ona giden
komut borçta kalır:

1. Kayıt kim (kullanıcı kimliği), ne (eylem + nesne) ve ne zaman (UTC) taşır; kiracı kimliği taşır.
2. Deponun arayüzünde güncelleme / silme yoktur (§4.1).
3. **Kiracı tarafında okunabilir:** o kaydı görmeye yetkili kiracı kullanıcısı, izi **kaydın kendi ekranında**
   okuyabilir (görevin etkinlik akışı görevin ekranında, iş akışı geçmişi örneğin ekranında). Okuma bir izne
   bağlıdır. "Yalnız kiracı yöneticisi" şartı yoktur — kaydı gören izini de görür (CT kararı 2026-10-02).
   Okuma ucu olmayan bir günlük bu koşulu sağlamaz.
4. Kayıt gerçek bir depoya yazılır — uygulama günlüğü (logger), bellek içi kuyruk, no-op ya da teslim edilmeyen bir outbox **iz değildir**.
5. İzin adı ve belirteci servisin defterinde (`## İzler`) kayıtlıdır.

Eşdeğer iz, merkezi günlüğün **yerine** geçer, onu taklit etmez: görev etkinlik akışı
"bu göreve ne oldu" sorusunu merkezi günlükten daha iyi cevaplar. Ama platform
yöneticisinin "bu kiracıda dün kim ne yaptı" sorusunu cevaplamaz — yol `c` kullanan
modül bunu paketinde yazar.

## 6. İstisna sınıfları

İstisna, defterde `komut | sınıf | gerekçe` satırıdır. Sınıf aşağıdakilerden biridir;
gerekçe **o komuta özgü** bir cümledir ("gerek yok" gerekçe değildir).

| sınıf | ne | gerekçe cümlesi şunu söyler | örnek |
|---|---|---|---|
| İ1 | Kişisel tercih / görünüm | "Yalnız komutu çalıştıran kullanıcının kendi deneyimini değiştirir; başka kimsenin verisi, yetkisi ya da gördüğü şey değişmez." | kayıtlı liste görünümü, kendi bildirimini okundu işaretleme |
| İ2 | Teknik sinyal / zamanlanmış tetikleyici | "Bir iş kaydını kendisi değiştirmez; tetiklediği her değişiklik kendi komutuyla denetlenir." | zamanlayıcının çağırdığı "vadesi gelenleri üret" tetikleyicisi — **ürettiği kayıtlar denetleniyorsa** |
| İ3 | Denetim altyapısının kendi komutu | "Denetim kaydını yazan ya da taşıyan komuttur; denetlenmesi sonsuz döngüdür." | outbox işleme, denetim ekleme |
| İ4 | Türetilmiş verinin yeniden üretimi | "Kaynak kayıt değişmez; kaynaktan yeniden hesaplanabilen bir projeksiyon / sayaç / önbellek yenilenir." | projeksiyon yenileme |
| İ5 | Kalıcı değişiklik yapmayan komut | "Hiçbir kalıcı kaydı değiştirmez (doğrulama, önizleme, kuru çalıştırma)." | içe aktarım önizlemesi |

İstisna **olamaz**: kimlik, rol/izin, kiracı durumu, abonelik/yetkilendirme, GxP kaydı
ve kiracının **ortak** ayarı (bir kullanıcının değil herkesin gördüğünü değiştiren ayar
"kişisel tercih" değildir — İ1 buna uygulanamaz).

## 7. Okuma denetimi — kim neye baktı

Varsayılan: okuma denetlenmez. Şu üç durumda okuma **denetlenir**:

| ne zaman | neden | bugün (ölçüldü 2026-10-02) |
|---|---|---|
| Denetim günlüğünün kendisi okunur, dışa aktarılır ya da saklama ayarı görüntülenir | Günlüğe bakan da iz bırakır (meta-denetim) | **var** — `IAuditMetaAuditWriter`, `IsMetaAudit = true`: liste, tekil, dışa aktarım, saklama |
| Kişisel veri toplu dışa aktarılır (rapor / liste dışa aktarımı) | KVKK: verinin kiracı dışına çıktığı an | **kısmen** — `IDataExportAuditWriter`: iş raporu ve toplantı raporu. Diğer dışa aktarımlar ölçülmedi |
| KVKK özel nitelikli veri, İK-gizli kayıt (ücret, disiplin, sağlık) ya da hasta-güvenliği verisi (farmakovijilans vakası) **tekil olarak** açılır | Erişimin kendisi korunan bilgidir | **yok** — bu sınıfta okuma denetimi yapan modül yok |

Bu üç sınıftan birine giren ekranı yazan paket, okuma denetimini `Audited Events`
tablosuna **okuma satırı** olarak yazar. Okuma denetimi her zaman merkezi günlüğe gider
(yol `a` / `b`); eşdeğer iz okuma için kullanılamaz.

## 8. Teslim kapısı

Modül turunun kapanış tablosundaki **"denetim"** satırı üç şeyle doldurulur — üçü de
yoksa satır boştur ve tur kapanmaz:

**(1) Tablo** — paketin `Audited Events` bölümünden, komut başına yol ya da istisna.

**(2) Mimari testi**

    dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard
    git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '

`<taban>` yazılmadan `git diff` commit'ten sonra boş çıkar ve hiçbir şey kanıtlamaz. İkinci komutun çıktısı **boş**
olmalıdır: `+- ` ile başlayan her satır deftere eklenmiş bir borç / K2 borcu / yazan sorgu satırıdır. (`--stat`
kullanılmaz: satırın borç mu iz mi olduğunu göstermez.)

**(3) Çalıştırılmış kanıt — salt-okunur sorgu, ortamın adıyla.** Test yeşili kaydın yazıldığını kanıtlamaz
(§9). Modülün bir **oluşturma**, bir **güncelleme** ve bir **silme** komutu **dev ortamında** çalıştırılır ve üç kayıt
gösterilir; canlıya çıkışta aynı üç kayıt **canlı ortamda** bir kez daha gösterilir (`release-checklist`). Rapor
"canlıda" demez, ortamın adını yazar: `dev` ya da `canlı`.

- Yol `a` / `b` — merkezi günlük (alan adları C# özellik adlarıdır):

      db.audit_events.find(
        { TenantId: <kiracı>, EntityType: "<NesneTürü>", EntityId: <kimlik> },
        { RequestType: 1, Operation: 1, Outcome: 1, ActorId: 1, ActorDisplayNameMasked: 1, OccurredAtUtc: 1, CorrelationId: 1 }
      ).sort({ OccurredAtUtc: -1 }).limit(5)

  ya da platform yöneticisi oturumuyla `GET /api/platform/audit/events?entityType=…`.
  Kayıt yoksa önce kuyruğa bak — orada kalmış ya da `DeadLetter` olmuş olabilir:

      db.audit_outbox.find({ CorrelationId: <korelasyon> }, { Status: 1, LastError: 1, Attempts: 1 })

- Yol `c` — izin kendi okuma ucu (defterde yazılıdır), aynı üç eylem için.

Gösterilen her kayıtta §3'ün on alanı tek tek işaretlenir; eksik alan teslimi durdurmaz
ama rapora **"bilinen boşluk"** olarak yazılır.

## 9. Kim yakalıyor — dürüst tablo

Mimari testi bir **metin** ölçümüdür: üretim kaynağını okur, komut çalıştırmaz. İşaretin
ya da yazıcının **bağlı** olduğunu görür; kaydın **yazıldığını** görmez.

| zorunlu madde | yakalayan | yakalamayan |
|---|---|---|
| §1 Yeni komut denetimsiz gelemez | `AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt` | MediatR'a uğramayan yazma yolu — yalnız ajan kapıları (`security-agent`, `add-endpoint-cqrs`) |
| §1 Borç yalnız küçülür | `…DebtAndExceptionCounts_ArePinned_AddingDebtNeedsAControlTowerDecision` (sayılar testte sabit) + `…KnownDebt_OnlyShrinks_…` + CI "audit ledger growth" adımı (taban dala göre eklenen `- ` satırı) | CI adımı yalnız taban dal bilindiğinde çalışır (PR); push'ta ve taban getirilemediğinde **ATLANDI** der. Aynı değişiklikte hem satır ekleyip hem sabiti yükselten ve hem de `CT-DECISIONS.md`'ye yazan birini yalnız inceleme durdurur |
| §1 Denetlenen komuttan işaret kaldırılamaz | aynı ilk test (komut "yeni denetimsiz" olur) | Handler aynı zamanda kabul edilmiş bir yazıcı izin üyesini çağırıyorsa (ör. `TaskItem.Declare`) komut yol `c` ile denetlenmiş sayılmaya devam eder |
| §2 MediatR dışı `*Command` yalnız adla ölçülen iki serviste olabilir | `…CommandsThatBypassMediatR_ExistOnlyInTheServicesMeasuredByName` | Komut türü hiç olmayan yazma yolu (controller → servis → depo, arka plan işi, olay tüketicisi) |
| §2 Sorgu kuralı yazmayı gizlemez | `…ACommandFiledUnderQueries_IsRefused_…` (`.Queries` ad alanında `*Command`) · `…QueriesThatWrite_AreListed_AndNewOnesAreRefused` (sorgu işleyicisinde depo yazma çağrısı) | İkincisi dardır: `…Repository / …Store / …Collection` adlı bir alana yapılan yazma çağrısını görür; bir servis üzerinden ya da başka adlı bir alan üzerinden yazan sorguyu **görmez** |
| §2 Tarayıcının göremediği istek | `…EveryRequestHandler_HandlesARequestThisMeasureCanSee` — taban sınıftan miras `IRequest`, başka projedeki istek arayüzü, `using` takma adı, küçük harfle başlayan ad: handler'ın isteği çözülemez ve kırmızıdır | Handler'ı da olmayan (hiç gönderilmeyen) bir istek |
| §5 İşaretli komut adı yüzünden dışlanmıyor | `…MarkedCommands_AreNotSilentlyExcludedByName` — parçalar üretimden okunur (`AutoExcludedRequestNameFragments`) | Yalnız Platform |
| §4.4 K2 sınıfı komut kapalı-başarısız | `…K2Debt_NamesAuditedCommands_AndOnlyShrinks` + sabit sayı: eski K2 borcu büyüyemez | Yeni bir komutun K2 **sınıfında olduğunu** hiçbir test bilemez — sınıfı paket söyler (`Audited Events`), `security-agent` ve orchestrator sorar |
| Defter satırı sessizce atlanmaz | `…LedgerFiles_AreReadableLineByLine_NothingIsSilentlySkipped` (`* X`, `-X`, girintili madde, `###`, bilinmeyen `##`) | — |
| §3 Kayıt on alanı taşır | — | **Hiçbir test.** Yalnız §8-(3) canlı kanıt ve `testing-agent` kapısı |
| §3 Sır / gereksiz kişisel veri yazılmaz | Platform'da `ISensitiveFieldRedactor` (çalışma zamanı) | Yol `b` / `c` için **hiçbir şey**; `security-agent` kapısı |
| §4.1 Kayıt değiştirilemez / silinemez | — | **Hiçbir test.** `data-agent` kapısı; depo arayüzü elle okunur |
| §4.2 Yazma hatası davranışı | Servislerin kendi davranış testleri (varsa) | Bu standardın testi ölçmez |
| §5 İşaret gerçekten bir pipeline davranışına bağlı | `…DeclaredTrails_ExistInProductionCode_AndMarkersAreWiredIntoThePipeline` | Davranışın içi kapatılmışsa (`if (false && …)`) yeşil kalır |
| §5 Yazıcı izinde handler yazma üyesini çağırıyor | ölçümün kendisi: belirteç `Tip.Metot`, handler'da çağrı aranır; `…DeclaredTrails_…` belirtecin bu biçimde ve üretimde var olduğunu doğrular | Çağrı ölü bir dalda ise (`if (false)`) yeşil kalır. Bir sınıfın (arayüz değil) üyesi `var` üzerinden çağrılıyorsa yalnız metot adına bakılır; bu zayıf biçim yalnız o adı serviste **tek bir tür** bildiriyorsa geçerlidir (`…WeakWriterForm_IsRefused_WhenSeveralTypesDeclareThatMethodName`) — 2026-10-02'de `.CancelAsync(` çağıran sekiz handler'a iş akışı günlüğü notu verdiği ölçüldü. Kalan boşluk: ad serviste tek ama çağrılan nesne başka (çerçeve türü, başka paket) ise sahte kredi |
| §5.c Eşdeğer izin beş koşulu | Koşulların kendisi: — . Kabulün kendisi: `…AcceptedTrails_ArePinned_ALedgerCellCannotGrantCredit` — defterde `aday` → `c` yapmak not vermez | Beş koşulu ölçen **hiçbir test yok**; kabul bir insan kararıdır, test yalnız o kararın bu dosyaya yazılmasını zorlar |
| §5 `## Dolaylı` bildirimi doğru | `…IndirectDeclarations_AreProvenByTheHandlerAndTheTypeItGoesThrough` (iki ucu kanıtlar); satır sayısı servis başına sabit (`PinnedCounts.Indirect`) — tek satır ekleyerek not alınamaz | Aradaki türün **o komut için** yazıcıyı çağırdığını görmez; geniş bir tür (depo, genel servis) yazılırsa sahte kredi verir — inceleme |
| §6 İstisnanın sınıfı kuralda var, gerekçesi bir cümle, kimlik/rol/izin komutu istisna değil | `…Exceptions_NameARealUnauditedCommand_AClassFromTheRule_AndAReason` (en az beş farklı kelime; adında `User` / `Role` / `Permission` geçen komut istisna olamaz); istisna sayısı da sabit | Gerekçenin **doğru** olduğunu; adı bu üç kelimeyi içermeyen bir kimlik komutunu — inceleme |
| §7 Okuma denetimi | — | **Hiçbir test.** Paket tablosu + `security-agent` |
| §8 Çalıştırılmış kanıt | — | Yalnız orchestrator teslim kutusu ve release checklist |
| Pakette `Audited Events` bölümü var | — | Paketleri okuyan bir doğrulayıcı yok; `module-pack-author` + `orchestrator` kapısı |

## 10. Açık kararlar

Bu standart aşağıdakilere **karar vermez**; karar Control Tower ve sahibindir.

| # | karar | neden açık |
|---|---|---|
| K1 | Platform "işlem içi" denetim yolu (`ITransactionOwnedAuditCommand`, 25 komut: abonelik, yetkilendirme, modül kataloğu, manifest kaydı) kabul edilecek mi? | Kod okumasıyla: `AuditBehavior` bu komutları atlar; handler'ın yazdığı `audit_outbox` satırının yükü eşleyicinin zorunlu alanlarını taşımıyor → `DeadLetter`. Ayrıca 25 komutun 8'i `AuditBehavior`'ın izin listesinde (17 ad) yok. **Dev veritabanında ölçüldü 2026-10-02:** bu yoldan gelen 7 satırın 7'si `DeadLetter` (hepsi `RegisterModuleManifestCommand`); bu yoldan teslim edilmiş tek satır yok. Teslim edilen yetkilendirme / abonelik satırları işaret eklenmeden (2026-08-31) önce, `AuditBehavior` yolundan yazılmış. Kanıt gelene kadar 25 komutun hepsi `aday` ve borçta. |
| ~~K2~~ | **KARAR VERİLDİ 2026-10-02 (sahip): EVET, dursun.** Kimlik / yetki / kiracı durumu / GxP / KVKK-özel olaylarında kayıt yazılamazsa komut başarısız olur (§4.3). Gerekçe: canlıda silinen bir hesabı kimin sildiği bulunamadı; 21 CFR Part 11 / Annex 11 eksik kaydı kabul etmez. Karşılaştırma: SAP değişiklik belgeleri iş verisiyle aynı işlem biriminde yazılır; Veeva'da denetim izi kaydın parçasıdır. Uygulama işlem bütünlüğü (aynı işlem ya da outbox) ister — yeni komutlarda zorunlu, mevcutlar borç. | — |
| ~~K3~~ | **KARAR VERİLDİ 2026-10-02 (CT):** üç eşdeğer iz (görev etkinlik akışı, iş akışı geçiş günlüğü, MDM kısaltma geçmişi) onaylandı; "kiracı tarafında okunabilir" tanımı §5.c-3'e yazıldı. | — |
| ~~K4~~ | **KARAR VERİLDİ 2026-10-02 (CT): seçenek A — ortak iletici + işaret `Diten.Building.Blocks` içinde**, ayrı iş paketi. O gelene kadar izi olmayan on serviste yeni yazma komutu engellidir; MDM kalıbını kopyalamak yasaktır (§5). | — |
| K5 | Komut türü olmayan yazma yolları nasıl ölçülecek (controller'dan doğrudan yazan uçlar, arka plan işleri, olay tüketicileri) | Tek bir kaynak-metni kuralıyla ölçülemiyor. PvgService ve ManagementGovernance'ın MediatR dışı komutları için **karar verildi 2026-10-02**: ad kuralı (§2). |
| K6 | Kiracı yöneticisi merkezi günlüğü okuyabilecek mi? | Bugün tek okuma ucu `PlatformAdminOnly`. Kiracı kendi verisinde kimin ne yaptığını göremiyor. |
| K7 | Auth `authAuditLogs` (rol/izin, parola, giriş olayları) için okuma ucu ya da merkezi günlüğe iletim | Kayıt yazılıyor, kimse okuyamıyor → `aday`. |
| K8 | Yol `b` anahtar / adres boşken sessizce atlıyor (MDM'de `LogDebug`) — §4.3 "sessiz değil" şartını karşılamıyor | Üretim kodu değişikliği ister (günlük düzeyi ya da başlangıçta doğrulama); K4 paketinin kapsamına alınması önerilir. |
| K9 | Platform'da üç sorgu işleyicisi yazıyor (`## Yazan sorgular`) | Yazmanın komuta taşınması üretim kodu değişikliğidir; sahibi PSS. |
| K10 | K2 sınıfının Platform'daki sınırı | `## K2 borcu` özellik klasörüne göre çıkarıldı (kimlik · kiracı durumu · belge yönetimi). Organizasyon / pozisyon atamaları, abonelik özellikleri, kotalar sınıfa girer mi? |

## 11. Gerekçe — başkaları nasıl yapıyor

| | denetim kaydını kim açar | geliştirici unutabilir mi |
|---|---|---|
| **SAP** | Değişiklik belgeleri (`CDHDR` / `CDPOS`) iş nesnesi için çerçeve tarafından yazılır; alan düzeyinde önce/sonra | Hayır — nesne tanımının parçası |
| **Oracle Fusion** | Denetim politikası iş nesnesi başına **bildirimle** açılır; kod yazılmaz | Hayır — yapılandırma |
| **Veeva Vault** | Denetim izi her nesnede platformun işidir; geliştirici seçmez (21 CFR Part 11 / EU Annex 11) | Hayır — kapatılamaz |
| **Diten (2026-10-02 öncesi)** | Geliştirici komuta işaret koyarsa | **Evet** — o günkü ölçümde yazma komutlarının üçte ikisinden fazlasında unutulmuş |

Üçünün ortak noktası: denetim **varsayılandır**, denetlenmemek açık bir seçimdir. Bu
standart aynı yönü bizim mimarimizde kurar: işaret hâlâ geliştiricinin elindedir, ama
koymamak artık sessiz değildir — test kırmızıdır.

Neden zorunlu: ilaç sektöründe GxP kaydı ALCOA+ ilkelerini karşılamak zorundadır
(**A**ttributable — kime ait olduğu belli; **C**ontemporaneous — olduğu anda yazılmış;
**O**riginal, **E**nduring — değiştirilmemiş ve kalıcı). "Kim, neyi, ne zaman" kaydı
olmayan bir değişiklik bu ilkelerin ilkini karşılamaz. KVKK ise kişisel veriye
erişimin ve değişikliğin izlenebilir olmasını ister.

## 12. Defter

Biçim ve bölümlerin anlamı: [`tests/architecture/audit-ledger/README.md`](../../tests/architecture/audit-ledger/README.md).

## ✅ Kontrol Listesi

- [ ] Paketin `Audited Events` tablosu her yazma komutunu tam bir kez içeriyor
- [ ] Her komut: yol `a` / `b` / `c` **ya da** `İ1…İ5` + o komuta özgü gerekçe
- [ ] Servisin denetim altyapısı var (yoksa: engel, kullanıcıya soruldu)
- [ ] Kayıtta sır / gereksiz kişisel veri yok
- [ ] `--filter AuditTrailStandard` yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş
- [ ] Bir oluşturma, bir güncelleme, bir silme **dev ortamında** çalıştırıldı; üç kayıt §8 sorgusuyla gösterildi
- [ ] Komut K2 sınıfındaysa (kimlik / yetki / kiracı durumu / GxP / KVKK-özel): kapalı-başarısız yolla yazıldı ve testi var (§4.4) — o yol yoksa komut yazılmadı, Control Tower'a yazıldı
- [ ] Komut izi olmayan on servisten birindeyse: yazılmadı, Control Tower'a yazıldı (§5 K4)
