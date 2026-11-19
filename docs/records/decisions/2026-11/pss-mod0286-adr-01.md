---
record_id: PSS-MOD0286-ADR-01
status: Kabul edildi (kararların her biri paket ve sahip kararıyla onaylı; bu kayıt onları tek yerde toplar, yeni karar vermez)
prepared_at_local: 2026-11-16
prepared_by: documentation-writer (Claude Sonnet 5.5), PSS-UI kule talimatıyla
block: MOD-0286 UI Design System & Page Pattern Governance (platform-shared-services)
pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md (rev 15) ve MOD-0286-FU01-page-js-reference-mapping.md (rev 04)
queue: pss-ui
---

# MOD-0286 — mimari karar kaydı (ADR 01): arayüz uyumu PR'da, değişen sayfada, tabana karşı denetlenir

ADR düzeltilmez; yerine yenisi yazılır ve bu kayıt `Superseded` işaretlenir (`.antigravity/rules/docs-organization.md:108`,
§3.1 tablosu). Bu kayıt kod, ekran ya da uç nokta eklemez; MOD-0286'nın neden bir depo aracı olduğunu açıklar.

## Durum

Kabul edildi. Dayanaklar: sahip kararı "daralt" (`docs/records/decisions/2026-10/pss-mod0286-daraltma-01.md:15-27`), ilk taban kararı
(`pss-mod0286-taban-01.md:21-30`), tek seviye sayfa borcu kararı (`pss-mod0286-tek-seviye-borc-01.md`), FU01 satır sahipleri kararı
(`docs/records/decisions/2026-11/pss-mod0286-fu01-satir-sahipleri-01.md:20-30`). Uygulama durumu için sürüm notuna bak
(`docs/records/releases/2026-11-16-mod-0286-release-note.md`).

## Bağlam

- **Standart dağınıktı.** On bir kural dosyası 3187 satır; hangisinin kazanacağı yazılı değildi; üç liste uygulaması yan yana yaşıyordu
  (paket `Module Summary`, "Why now" ölçümleri, `MOD-0286-ui-design-system-page-pattern-governance.md:71-108`).
- **Yazılı kural kendiliğinden uygulanmaz.** İlk paket sürümü yalnız şartname (DL-1) ve kontrol listesi (DL-2) yazıyordu; sahip, bitiş
  çizgisinin "liste yazıldı" değil "otomatik maddeler CI'da koşuyor" olmasını istedi (`daraltma-01.md:15-17`). O tarihte CI'da hiçbir iş UI
  kontrolü koşmuyordu (`daraltma-01.md:43-44`).
- **Eski sayfalar zaten kırmızı.** Bugünkü kırmızılar tabanda 2153 satır (main) / 2175 satır (bu dal, FU-19 dahil) olarak ölçüldü
  (`tests/ui-compliance-baseline.tsv`, ölçüm: `awk` ile satır sayımı, 2026-11-16). Hepsini önce düzeltmek modülü süresiz bekletirdi.
- **Dilimler:** DL-1 şartname, DL-2 kontrol listesi, DL-3 CI bağlantısı
  (`MOD-0286-ui-design-system-page-pattern-governance.md:968-974`); ek paket MOD-0286-FU01 DL-1: sayfa JS dosyalarının görünümle eşlenmesi
  (`MOD-0286-FU01-page-js-reference-mapping.md:287-291`).

## Karar

1. **UI uyum kontrolü phase1'den ayrı bir CI işidir** (`.github/workflows/ui-compliance.yml`). Kendi iş akışı dosyası, salt okunur izin, sabitlenmiş
   eylemler, .NET yok, gizli bilgi yok; kırmızı phase1 onu durdurmaz, o da phase1'in sonucunu okumaz
   (paket DL-3 bölüm 3, `:463-475`). Bugün her pull request onu başlatır; böylece şube koruması onu zorunlu tutabilir
   (`.github/workflows/ui-compliance.yml:6-8`; commit `e2be73965`, 2026-11-02).
2. **Yalnız değişen sayfalar koşar; CI'da tüm-sayfa kipi yoktur.** Seçici `git diff` ile eklenen, değişen, silinen ve yeniden adlandırılan
   dosyaları sayfaya eşler (`:318-324`); `--draft-baseline` yalnız yerel ve ilk taban içindir, CI kipi hiçbir koşulda ona düşmez
   (`scripts/uic_changed_pages.py:9-11`). Paket ölçütü AC-20 (`:824`); iş akışı süreyi yazdırır (`ui-compliance.yml:33`).
3. **Eski borç sayı tabanlı satırlarla dondurulur; yeni kırmızı durdurur.** Taban `tests/ui-compliance-baseline.tsv`, başlık
   `page · check · instance · count · reason · fu · owner`; her satır bir FU kimliği, bir sahip ve karar adı taşır, boş hücre dosyayı bozuk
   sayar (paket `:404-462`). Satır kontrollerinde anahtar dosya, değer o dosyadaki eşleşme sayısıdır: sayı tabandan büyükse
   `YENİ KIRMIZI`, eşitse `TABAN`, küçükse `kapandı` raporlanır ama iş kırmızı olmaz (`:420-430`). Satır eklemek ya da sayıyı artırmak yeni
   bir sahip kararıdır (`taban-01.md:33`, paket AC-19 `:823`). Bilinen sınır yazılıdır: aynı dosyada bir ihlal kapanıp başkası
   eklenirse sayı değişmez ve `TABAN` yazılır (`:432-440`).
4. **Desenler tek kaynaktan gelir.** Satır kontrollerinin desenleri paketteki "DL-3 satır kontrol desenleri" kod bloğunun ham dizesidir
   (`:611-644`); tablo hücresi okunmaz (`:401-403`); `--self-test` seçicinin kendi kopyasını bloğa bayt bayt karşılaştırır. FU01'in üç
   deseni de paket bloğundadır (`MOD-0286-FU01-page-js-reference-mapping.md:71-74`) ve seçicide tek bir sözlüktedir
   (`scripts/uic_changed_pages.py:99`, `FU01_REF_PATTERNS`).
5. **Sayfa JS dosyaları görünümdeki `<script src>` başvurusuyla sayfaya bağlanır (FU01).** Yalnız o sayfaya ait bir `.cshtml` içindeki
   başvuru sayılır; ortak (`Shared`) görünümlerin başvurusu sayfa seçmez; başvurulan dosya sayfanın kontrol kapsamına girer
   (FU01 paketi `:69-85`). Birden çok sayfa aynı dosyayı yüklerse her sayfa için ayrı satır tutulur, havuzlanmaz (`:91`).
   Başvuru zinciri (kısmi görünüm adları) izlenmez (`:78`).
6. **Çıkış sözleşmesi 0 / 1 / 2; yalnız 0 yeşildir.** 0: taban dışında kırmızı yok; 1: en az bir `YENİ KIRMIZI`; 2: araç hatası (git, bozuk taban,
   okunamayan kayıt). Araç hatası "değişen sayfa yok" diye okunmaz (`scripts/uic_changed_pages.py:14-16`). İş akışının son adımı 0 dışındaki her
   değeri (127, 137, boş dahil) kırmızı sayar (`.github/workflows/ui-compliance.yml:45-56`). Son düzeltmeler main'dedir:
   `38e5bb13e` (yalnız 0 yeşil) ve `575c47792` (adım, mimari bekçisi "exit 0"ı yasakladığı için o yazım olmadan yeşil biter).

## Seçenekler

| Seçenek | Neden reddedildi | Kanıt |
|---|---|---|
| UI kontrolü `phase1-gates.yml`'in bir adımı olsun | phase1 .NET çözümlerini derler; kırmızı phase1 UI kontrolünü gizler; web projesine yalnız test projesi üzerinden ulaşır, hiçbir sayfa doğrulayıcısı koşmaz | paket `:463-470` |
| CI'da tüm sayfaları koş (tüm-sayfa kipi) | Tüm sayfaları koşan taslak kipinin ölçülen maliyeti 152 liste sayfası için yaklaşık 26 sn + yaklaşık 5 sn'dir, her PR bunu öderdi; ortak dosya değişince zaten "hepsi" demektir; AC-20 bunu yasaklar | paket `:453`, `:511`, `:824` |
| Önce bütün eski borcu düzelt, sonra kontrolü aç | 2153 satır, onlarca alan sahibi (XMC-001: başkasının modülüne sormadan dokunulmaz); kontrol aylarca kapalı kalırdı | `taban-01.md:35-48`; paket FU-14 `:962` |
| Eski sayfalar için tabanı yok say, her kırmızıda dur | Her PR eski borca takılırdı; sahip "eski sayfalar borcu büyümesin" dedi, "hepsi düzelsin" demedi | paket `:434-440`; `daraltma-01.md:15-17` |
| Satır parmak izi tabanı (rev 10) | Düzenlenmiş satırda yanlış `YENİ KIRMIZI` verdi; iki inceleme parmak izinin kaçırdığı durumlar buldu; dosya başına sayı hepsini yakalar | paket `:426-432` |
| Önce yönetim konsolu (P2: Design System Console, Page Pattern Registry) | İlk gerçek tüketici yok (MOD-0295 / MOD-0297 başlamadı); konsol "tema kararı sayfa sayfa sızar" sorununu başka biçimde tekrarlardı; ayrı pakete bırakıldı | `daraltma-01.md:18-19`; paket D-4 `:62`, FU-7 `:955` |
| FU01'de JS dosyalarını görünüm klasörüne taşı | Taşıma başka alanların kodunu değiştirir; seçici okumakla yetinir | FU01 paketi `:38` ("reference reading is chosen, moving JS is not") |
| FU01'de kısmi görünüm zincirini izle | Kısmi görünüm adı Razor ifadesidir; izlemek her seçimde bütün görünümleri ayrıştırmayı gerektirir; 8 dosya bu yüzden `.../Components` sayfasına düşer (bilinen açık) | FU01 paketi `:78`, FU-22 `:284` |
| Yalnız `Shared` görünümün yüklediği dosya için tüm sayfaları seç | Ortak dosya = tüm sayfalar = tüm-sayfa kipi (AC-20 ihlali); bu dosyalar kırmızıya gidemez, açık FU-23'tedir | FU01 paketi `:89`, `:285` |
| İş akışını yol süzgeçli tut | Süzgeçli bir zorunlu kontrol, ilgisiz PR'larda hiç rapor vermez; PR sonsuza dek bekler | `ui-compliance.yml:6-8` |

**Sektör karşılaştırması (genel bilgi; bu depoda ölçülmedi).** SAP Fiori tasarım kılavuzları ve Oracle Redwood tasarım sistemi, bildiğimiz
kadarıyla kuralı bir bileşen kitaplığı ve yayımlanmış tasarım yönergeleri üzerinden dağıtır; uyum, uygulamanın o kitaplığın denetimlerini kullanması
ve ürün ekiplerinin gözden geçirmesiyle sağlanır. Bizde de bileşenler ortaktır (`_ListShell`, DataTable fabrikası), ama on bir kural dosyasının
yanında kitaplık dışı sayfa JS'i çoktur; bu yüzden kural, bileşen kullanımına ek olarak PR'da makineyle ölçülür. Fark: eski borcu tabanla dondurmak ve
yalnız yeni kırmızıda durmak bizim tercihimizdir; bu iki ürünün kendi uygulama ayrıntıları burada doğrulanmadı.

## Sonuçlar

**İyi**
- Yeni ya da değişen bir sayfada ihlal ilk PR'da görünür; eski borç büyümez (satır sayısı tabanın üstüne çıkamaz).
- Her eski kırmızının adı, sahibi ve karar adı tabandadır; borç görünür ve sahibine atfedilmiştir.
- Araç yalnız Python standart kütüphanesi ve git'tir; .NET, servis ya da gizli bilgi gerektirmez; hiçbir sayfaya dokunmayan PR'da bir saniyeden kısa sürer
  (iş akışı yorumundaki ölçüm, `ui-compliance.yml:6-8`).
- Kural dosyalarına dokunulmadı (`.antigravity/**` korunur); çelişkiler kuralın kendisinde düzeltilmek üzere sahip işi olarak kaldı
  (`daraltma-01.md:20-22`).

**Kötü / kabul edilen bedel**
- Taban büyük (2175 satır) ve elle bakımlıdır; yanlış etiketli sahip ancak yeni bir sahip kararıyla düzelir (`tek-seviye-borc-01.md:30-31`, Karar 4).
- Aynı dosyada bir ihlalin kapanıp başkasının eklenmesi görünmez (paket `:432-440`).
- Kapsam dışı kalanlar: CI-3 tek seviyeli liste sayfasında (FU-17, doğrulayıcı alansız sayfayı desteklemiyor), ortak dosyalar (FU-23), görünüm
  zinciri (FU-22).
- Bir PR'da taban satırı eklemek sahip kararı ister; bu bilerek bir sürtünmedir.
- Gerçek bir sayfa değişikliğinin CI'da koşusu henüz kayda geçmedi: kabul koşusu hiçbir sayfaya dokunmayan bir PR'daydı
  (`docs/records/audits/2026-10/pss-ui-p1-04-kabul-02/KABUL.md`, "Bu PR hiçbir sayfaya dokunmuyor"); FU01 için AC-12 de PR'dan sonra kapanacak
  (`docs/records/audits/2026-11/mod-0286-fu01-01/dogrulama-01.md:11-12`, `:40`). Linux'ta harf duyarlılığı ölçülmedi.

## Bağlantılar

- Paketler: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md`,
  `.../MOD-0286-FU01-page-js-reference-mapping.md`.
- Belgeler: `docs/reference/modules/platform/ui-design-system-page-pattern-governance/spec.md`, `compliance-checklist.md`.
- Kabul kayıtları: `docs/records/audits/2026-10/pss-ui-p1-03-kabul-04/KABUL.md`, `pss-ui-p1-04-kabul-02/KABUL.md`, `pss-ui-p1-05-kabul-01/KABUL.md`.
- FU01 doğrulama: `docs/records/audits/2026-11/mod-0286-fu01-01/dogrulama-01.md`; işçi raporu `docs/records/audits/2026-11/pss-ui-fu01-01-rapor-01/RAPOR.md`.
- Sürüm notu: `docs/records/releases/2026-11-16-mod-0286-release-note.md`; kapanış listesi `docs/records/audits/2026-11/mod-0286-kapanis-paketi-01.md`.
- Mimari denetim raporu (başka bir ajan yazıyor, bu kayıt yazılırken yok): `docs/records/audits/2026-11/mod-0286-mimari-denetim-01.md`.
