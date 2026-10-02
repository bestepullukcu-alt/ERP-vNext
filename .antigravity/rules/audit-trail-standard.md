---
description: "AUD-001 — Denetim kaydı (audit trail) standardı: veri değiştiren her komut denetlenir; denetlenmemek gerekçeli istisnadır; borç yalnız küçülür"
---

# Denetim Kaydı Standardı — AUD-001

> Bu kural 2026-10-02'de yazıldı çünkü canlıda bir kullanıcı hesabı silindi ve **kimin
> sildiği hiçbir yerde yazmıyordu** (BL-456; o komut düzeltildi). Sahip diğer ekiplerle
> konuştu: aynı eksik modüllerin çoğunda vardı.
>
> O gün ölçüldü (`docs/records/audits/2026-10/system-audit-trail-inventory-2026-10-02.md`):
>
>     1030 yazma komutu (13 servis)
>       317  denetleniyor          (280 Platform içi · 14 merkezi günlüğe iletim · 23 eşdeğer iz)
>         5  gerekçeli istisna
>       708  hiçbir kabul edilmiş ize gitmiyor
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

## 2. Tanımlar

**Yazma komutu.** `services/<servis>/src` altında, MediatR `IRequest` / `IRequest<T>`
uygulayan (doğrudan ya da aynı serviste tanımlı, `IRequest`'ten türeyen bir arayüz
üzerinden), soyut olmayan ve **sorgu olmayan** her tür. Sorgu, üretim kodunun kendi
tanımıdır (Platform `AuditBehavior.IsQueryRequest`): tür adı `Query` ile biter **ya da**
ad alanı `.Queries` içerir. Geri kalan her istek yazma komutudur — adı `Command` ile
bitmese de.

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
| `b` MDM iletimi | **Devam eder.** İletim hatası yutulur (uyarı logu). Handler istisna atarsa hiç iletim yapılmaz — başarısız komut kaydedilmez. | `AuditForwardingBehavior.cs` |
| `b` Auth kullanıcı olayları | **Devam eder.** Yerel yazım + iletim; ikisi de hatayı yutar. | `UserAuditRecorder.cs` |
| Veri dışa aktarımı | **Durur (fail-closed).** Kayıt kuyruğa girmediyse dosya verilmez. | `DataExportAuditWriter.cs` |

Yani bugün dışa aktarım dışında **her yol "en iyi çaba"dır**: kayıt düşmezse iş yine
tamamlanır ve eksik kayıt yalnız uygulama günlüğünde bir uyarı satırıdır.

**4.3 · Kuralın kabul ettiği:**

| olay sınıfı | kabul edilen | durum |
|---|---|---|
| Kişisel verinin toplu dışa aktarımı | Kayıt yoksa işlem yok (fail-closed) | **uygulanıyor** |
| Kimlik, rol/izin, kiracı durumu, GxP kaydı (belge, imza, sapma, eğitim, kalite olayı), KVKK özel nitelikli veri | ⚠ **AÇIK KARAR K2 (§10)** — "en iyi çaba" yetmiyor olabilir; karar sahibin. Karar verilene kadar bugünkü davranış geçerlidir ve bu sınıftaki her yeni komutun paketi yazma hatası davranışını **açıkça** yazar. | karar bekliyor |
| Diğer iş kayıtları ve yapılandırma | En iyi çaba, **ama sessiz değil**: düşmeyen kayıt uyarı olarak loglanır ve sayılabilir olur | uygulanıyor |

## 5. Üç geçerli yol

| yol | ne | ne zaman | komutta ne görünür |
|---|---|---|---|
| **a** | Platform içi: komut `IAuditableCommand` + `IAuditMetadataProvider` taşır; `AuditBehavior` yazar | Komut `Diten.Platform` içindeyse — **başka seçenek aranmaz** | komut türünün taban listesinde iki arayüz |
| **b** | Başka servisten merkezi günlüğe iletim (MDM deseni: işaret + pipeline davranışı + `POST /api/internal/audit/append`; ya da Auth deseni: handler'ın çağırdığı iletici) | Komut başka bir servisteyse ve o servisin ileticisi varsa. İletici yoksa önce o kurulur (§10 K4) | işaret ya da handler'da yazıcı |
| **c** | **Eşdeğer iz:** modülün kendi değişmez, kullanıcıya gösterilen geçmişi | Yalnız aşağıdaki **beş koşulun hepsi** sağlanıyorsa | handler'da izin yazıcısı |

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
    git diff --stat <taban>..HEAD -- tests/architecture/audit-ledger/

İkinci komutun çıktısı ya boştur ya da yalnız **silinen** borç satırları gösterir.

**(3) Canlı kanıt — salt-okunur sorgu.** Test yeşili kaydın yazıldığını kanıtlamaz
(§9). Modülün bir oluşturma, bir güncelleme ve bir silme komutu canlıda çalıştırılır ve
üç kayıt gösterilir:

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
| §1 Borç yalnız küçülür | `…KnownDebt_OnlyShrinks_AnEntryThatIsAuditedOrGone_MustBeRemoved` + `code-quality-agent` (`git diff` ile eklenen satır) | Listeye satır **eklenmesini** test göremez (dosyanın geçmişini okumaz) — yalnız inceleme |
| §1 Denetlenen komuttan işaret kaldırılamaz | aynı ilk test (komut "yeni denetimsiz" olur) | — |
| §2 MediatR dışı `*Command` yalnız adla ölçülen iki serviste olabilir | `…CommandsThatBypassMediatR_ExistOnlyInTheServicesMeasuredByName` | Komut türü hiç olmayan yazma yolu (controller → servis → depo, arka plan işi, olay tüketicisi) |
| §3 Kayıt on alanı taşır | — | **Hiçbir test.** Yalnız §8-(3) canlı kanıt ve `testing-agent` kapısı |
| §3 Sır / gereksiz kişisel veri yazılmaz | Platform'da `ISensitiveFieldRedactor` (çalışma zamanı) | Yol `b` / `c` için **hiçbir şey**; `security-agent` kapısı |
| §4.1 Kayıt değiştirilemez / silinemez | — | **Hiçbir test.** `data-agent` kapısı; depo arayüzü elle okunur |
| §4.2 Yazma hatası davranışı | Servislerin kendi davranış testleri (varsa) | Bu standardın testi ölçmez |
| §5 İşaret gerçekten bir pipeline davranışına bağlı | `…DeclaredTrails_ExistInProductionCode_AndMarkersAreWiredIntoThePipeline` | Davranışın içi kapatılmışsa (`if (false && …)`) yeşil kalır |
| §5 Yazıcıyı anan handler onu gerçekten çağırıyor | — | **Hiçbir şey.** Handler yazıcıyı enjekte edip çağırmasa test yeşildir |
| §5.c Eşdeğer izin beş koşulu | — | **Hiçbir test.** Koşullar defterde ölçüm notu olarak yazılıdır; yolun `aday` → `c` olması bir insan kararıdır |
| §5 `## Dolaylı` bildirimi doğru | `…IndirectDeclarations_AreProvenByTheHandlerAndTheTypeItGoesThrough` (iki ucu kanıtlar) | Aradaki türün **o komut için** yazıcıyı çağırdığını görmez; geniş bir tür (depo, genel servis) yazılırsa sahte kredi verir — inceleme |
| §6 İstisnanın sınıfı kuralda var, gerekçesi dolu | `…Exceptions_NameARealUnauditedCommand_AClassFromTheRule_AndAReason` | Gerekçenin **doğru** olduğunu — inceleme |
| §7 Okuma denetimi | — | **Hiçbir test.** Paket tablosu + `security-agent` |
| §8 Canlı kanıt | — | Yalnız orchestrator teslim kutusu ve release checklist |
| Pakette `Audited Events` bölümü var | — | Paketleri okuyan bir doğrulayıcı yok; `module-pack-author` + `orchestrator` kapısı |

## 10. Açık kararlar

Bu standart aşağıdakilere **karar vermez**; karar Control Tower ve sahibindir.

| # | karar | neden açık |
|---|---|---|
| K1 | Platform "işlem içi" denetim yolu (`ITransactionOwnedAuditCommand`, 25 komut: abonelik, yetkilendirme, modül kataloğu, manifest kaydı) kabul edilecek mi? | Kod okumasıyla: `AuditBehavior` bu komutları atlar; handler'ın yazdığı `audit_outbox` satırının yükü eşleyicinin zorunlu alanlarını taşımıyor → `DeadLetter`. Ayrıca 25 komutun 8'i `AuditBehavior`'ın izin listesinde (17 ad) yok. **Dev veritabanında ölçüldü 2026-10-02:** bu yoldan gelen 7 satırın 7'si `DeadLetter` (hepsi `RegisterModuleManifestCommand`); bu yoldan teslim edilmiş tek satır yok. Teslim edilen yetkilendirme / abonelik satırları işaret eklenmeden (2026-08-31) önce, `AuditBehavior` yolundan yazılmış. Kanıt gelene kadar 25 komutun hepsi `aday` ve borçta. |
| K2 | Kimlik / yetki / GxP / KVKK-özel olaylarında kayıt yazılamazsa komut dursun mu (§4.3)? | Bugün her yol en iyi çaba. 21 CFR Part 11 / Annex 11 için eksik kayıt kabul edilemez olabilir; durdurmak ise işlem bütünlüğü (outbox) ister. |
| ~~K3~~ | **KARAR VERİLDİ 2026-10-02 (CT):** üç eşdeğer iz (görev etkinlik akışı, iş akışı geçiş günlüğü, MDM kısaltma geçmişi) onaylandı; "kiracı tarafında okunabilir" tanımı §5.c-3'e yazıldı. | — |
| K4 | Denetim altyapısı olmayan servisler için ortak iletici | Seçenekler envanter belgesinin sonunda. |
| K5 | Komut türü olmayan yazma yolları nasıl ölçülecek (controller'dan doğrudan yazan uçlar, arka plan işleri, olay tüketicileri) | Tek bir kaynak-metni kuralıyla ölçülemiyor. PvgService ve ManagementGovernance'ın MediatR dışı komutları için **karar verildi 2026-10-02**: ad kuralı (§2). |
| K6 | Kiracı yöneticisi merkezi günlüğü okuyabilecek mi? | Bugün tek okuma ucu `PlatformAdminOnly`. Kiracı kendi verisinde kimin ne yaptığını göremiyor. |
| K7 | Auth `authAuditLogs` (rol/izin, parola, giriş olayları) için okuma ucu ya da merkezi günlüğe iletim | Kayıt yazılıyor, kimse okuyamıyor → `aday`. |

## 11. Gerekçe — başkaları nasıl yapıyor

| | denetim kaydını kim açar | geliştirici unutabilir mi |
|---|---|---|
| **SAP** | Değişiklik belgeleri (`CDHDR` / `CDPOS`) iş nesnesi için çerçeve tarafından yazılır; alan düzeyinde önce/sonra | Hayır — nesne tanımının parçası |
| **Oracle Fusion** | Denetim politikası iş nesnesi başına **bildirimle** açılır; kod yazılmaz | Hayır — yapılandırma |
| **Veeva Vault** | Denetim izi her nesnede platformun işidir; geliştirici seçmez (21 CFR Part 11 / EU Annex 11) | Hayır — kapatılamaz |
| **Diten (2026-10-02 öncesi)** | Geliştirici komuta işaret koyarsa | **Evet** — ve 1017 komutun 695'inde unutulmuş |

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
- [ ] `--filter AuditTrailStandard` yeşil; borç listesine satır eklenmedi
- [ ] Bir oluşturma, bir güncelleme, bir silme canlıda çalıştırıldı; üç kayıt §8 sorgusuyla gösterildi
- [ ] Kimlik / yetki / GxP / KVKK-özel komut varsa yazma hatası davranışı pakette yazılı
