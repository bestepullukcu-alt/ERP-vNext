# MOD-0286 — sürüm notu: arayüz uyumu artık PR'da ölçülür; eski borç tabanda, yeni kırmızı durdurur

- Modül: MOD-0286 UI Design System & Page Pattern Governance (platform-shared-services) · servis yok, ekran yok, uç nokta yok
  (paket "Gateway / API Routing Decision": "Gateway değişikliği gereksiz; endpoint yok", `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md:798-800`)
- Tarih: 2026-11-16 · durum: **taslak** (sürüm numarası verilmedi; dağıtım izni değildir)
- Çıktısı: bir depo aracı (`scripts/uic_changed_pages.py`), bir taban dosyası (`tests/ui-compliance-baseline.tsv`), bir CI işi
  (`.github/workflows/ui-compliance.yml`), bir şartname ve bir kontrol listesi
  (`docs/reference/modules/platform/ui-design-system-page-pattern-governance/spec.md`, `compliance-checklist.md`).
- Dilimler: DL-1 şartname, DL-2 kontrol listesi, DL-3 CI bağlantısı (üst paket `:968-974`); ek paket MOD-0286-FU01 DL-1 sayfa JS eşlemesi
  (`MOD-0286-FU01-page-js-reference-mapping.md:287-291`).
- Ölçüm notu: bu notun yazımında araç, self-test ya da CI koşulmadı. Sayılar kabul kayıtlarından, doğrulama kaydından ve dosyalardan okundu;
  dal konumu `git branch -r --contains <commit>` ile, taban sayıları `awk` ile ölçüldü (2026-11-16).
- **P1'in bütün kabulü bu notla ilan edilmez.** Kabul kayıtları dilim bazındadır (aşağıda); P1 bütünü için ayrı bildirim kule işidir
  (`docs/records/audits/2026-10/pss-ui-p1-04-kabul-02/KABUL.md`, "Kapsam dışı"). Bu belgeyi yazarken böyle bir kayıt aranmadı: ölçülmedi.

## Dilimler ve nerede oldukları

| Dilim | İş | Commit | Kabul kaydı | Konum (ölçüm) |
|---|---|---|---|---|
| DL-1 şartname + DL-2 kontrol listesi | PSS-UI-P1-03 | `e5f62bded` (koşu), kayıt `3f6e383cd` | `docs/records/audits/2026-10/pss-ui-p1-03-kabul-04/KABUL.md` (kabul, E1, 2026-10-31) | `origin/main`'de |
| DL-3 seçici, ilk taban, CI işi | PSS-UI-P1-04 | seçici ve taban `a37ae9988`; iş akışı `6aed3c788` | `pss-ui-p1-04-kabul-02/KABUL.md` (kabul, E2; CI koşusu 41172080715, PR #325, head `d49ddca56`) | `origin/main`'de (PR #325 birleşimi `9b531144f`) |
| DL-3 yeniden açma: tek seviye sayfalar | PSS-UI-P1-05 | `e3eda1752` (134 FU-16 satırı) | `pss-ui-p1-05-kabul-01/KABUL.md` (kabul, E2) | `origin/main`'de |
| Seçici: sayfa başına API profili (CI-3) | kayıt yok | `dad448bb1` (2026-11-09) | **bulunamadı** (`grep dad448bb1 docs execution` boş) | `origin/main`'de |
| İş akışı düzeltmeleri | CT-Setup | `38e5bb13e`, `575c47792` (yalnız 0 yeşil), `e2be73965` (her PR'da koşar) | — | `origin/main`'de |
| **MOD-0286-FU01 DL-1: sayfa JS başvuru eşlemesi** | PSS-UI-FU01-01 | iş `f4950c377`; kanıt ve doğrulama `a21c5f342` | `docs/records/audits/2026-11/mod-0286-fu01-01/dogrulama-01.md` (**KOŞULLU**, kritik 0, engelleyici 0) — kabul kaydı yok; kuyruk satırı **READY** (`execution/domains/platform-shared-services/work-packs/CT-QUEUE.tsv:7`) | **yalnız bu CT dalında** (`chore/ct/pss-ui-ct-01`); `git branch -r --contains f4950c377` ve `a21c5f342` boş: hiçbir uzak dalda yok, PR yok |

Dal farkı: bu dalın `ui-compliance.yml` kopyası main'dekinden eskidir (main'de ayrıca birleştirme kuyruğu `merge_group` ve eski koşuyu iptal eden eşzamanlılık grubu var;
commit'ler `871ea0fb7`, `0dfcd4037`, `a70df54f0`; `git diff origin/main HEAD` iş akışında 17 satır fark verdi). Bu not iş akışı davranışını iki kopyada da aynı olan kısımlardan anlatır.

## Ne değişti — geliştirici açısından

### PR açınca ne koşar

Her pull request `ui-compliance` adlı kontrolü başlatır (`.github/workflows/ui-compliance.yml:9-10`, `:6-8`). Kontrol `python3 scripts/uic_changed_pages.py --base <PR tabanı>` çalıştırır
(`:30`). Araç PR'ın dokunduğu dosyalardan sayfaları bulur: `Views/{Alan}/{Modül}/**`, `wwwroot/assets/js/{Alan}/{Modül}/**`, tek seviye
`Views/{Modül}/*.cshtml`, ilgili `.resx` dosyaları, `PageHelpCatalog.cs`, Ctrl+K kayıt dosyaları. Yalnız o sayfaların kontrolleri koşar (CI-1…CI-15);
CI'da tüm sayfaları koşan bir kip yoktur (`scripts/uic_changed_pages.py:9-11`; iş akışı çıktısı: "yalnız değişen sayfalar; CI'da tüm-sayfa kipi yok", `:33`).
Hiçbir sayfaya dokunmayan PR'da çıktı tek `süre:` satırıdır, çıkış 0.

FU01 ile (yalnız bu dalda): `wwwroot/assets/js/pages/**` gibi sayfa klasörü dışındaki 106 JS dosyası, bir görünümün `<script src>` satırıyla yüklendiği
sayfaya bağlanır; artık "kontrol yok" demez, sayfa kapsamına girer (FU01 paketi `:69-85`; doğrulama `dogrulama-01.md:20`, 163/163 self-test).

### Ne zaman yeşil, ne zaman kırmızı

| Aracın çıkışı | Anlamı | Kontrol |
|---|---|---|
| 0 | taban dışında kırmızı yok | yeşil |
| 1 | en az bir `YENİ KIRMIZI` satırı | kırmızı |
| 2 | araç hatası: git hatası, bozuk taban, okunamayan kayıt | kırmızı |
| başka (127, 137, boş) | beklenmeyen çıkış | kırmızı |

Yalnız 0 yeşildir (`ui-compliance.yml:45-56`; araç belgesi `scripts/uic_changed_pages.py:14-16`). Çıktıda her denetim bir satırdır: `PASS`, `TABAN <fu>`
(eski borç, durdurmaz), `YENİ KIRMIZI <sayfa> <CI-n> <dosya> <sayı> +<fazla>` (durdurur), `ölçülmedi` (bir kontrol koşmadı, sebebi yazılı).
Kırmızı olunca çıktıdaki `YENİ KIRMIZI` satırını oku: sayfa, kontrol, dosya ve tabanın üstündeki fazlalık yazılıdır. Çıktı ayrıca iş çıktısı olarak
saklanır ve SHA-256'sı günlüğe yazılır (`:31-32`, `:37-43`).

### Taban satırı ne demek

`tests/ui-compliance-baseline.tsv` bugünkü kırmızıların kaydıdır; bu kayıt eski sayfaları durdurmaz ama büyümesine izin vermez. Satır kontrollerinde (örnek: `createCrudTable(`)
satır, o dosyadaki ihlal sayısını tutar; PR sayıyı tabanın üstüne çıkarırsa kırmızı olur. Bir ihlal kapanırsa araç `kapandı - satır silinmeli` ya da
`kapandı - satırı güncelle` yazar; bu iş kırmızı olmaz, ama satır silinir ya da düşürülür (paket `:420-430`). Satır eklemek ya da sayıyı artırmak yeni bir sahip kararı ister
(`docs/records/decisions/2026-10/pss-mod0286-taban-01.md:33`).

### Eski borç kimde

Taban (bu dal) 2175 satırdır; main'de 2153 satır (aradaki 22 satır FU-19, yalnız bu dalda). Ölçüm: `awk` ile `fu` ve `owner` sütunları, 2026-11-16.

| `fu` | Satır | Anlamı |
|---|---|---|
| FU-14 | 1953 | iki seviyeli eski sayfaların UI borcu (sahibi alanın domain'i) |
| FU-16 | 134 | tek seviye sayfaların borcu (karar `tek-seviye-borc-01`) |
| FU-4 | 64 | `personalizationClient` tenant başlığı kırmızısı (developer-enablement) |
| FU-19 | 22 | FU01'in getirdiği sayfa JS borcu (yalnız bu dalda; karar `fu01-satir-sahipleri`) |
| FU-12, FU-15 | 1 + 1 | Golden Slim / Golden Compact `bilgi:` satırları, kontrol edilmez, yalnız yazılır |

Sahip (domain) başına satır: platform-shared-services 531, **belirsiz 376**, supply-chain-execution 368, commercial-suite 279, human-capital-management 233,
master-data-management 78, enterprise-strategy-business-performance 76, portfolio-delivery 67, developer-enablement 66, management-governance 53, procurement 35,
pharmacovigilance 13. "Belirsiz" 376 satırın hepsi CI-14'tür (ortak çeviri dosyasındaki eksik ya da İngilizceyle aynı anahtarlar); sahibi henüz belirlenmedi
(`pss-ui-p1-04-kabul-01/KABUL.md:82`, E-6). Başkasının sayfasını düzeltmeden önce sahibine sor (XMC-001); bu iş hiçbir eski kırmızıyı düzeltmedi.

## Tüketici etkisi

- **Geliştiriciler (PR yazarları):** `ui-compliance` kontrolü her PR'da görünür; sayfa değiştirmeyen PR'da etkisi yok. Sayfaya dokunan PR, eski borcu büyütmeden geçer,
  yeni ihlalde durur. Bir sayfayı klasör olarak yeniden adlandırmak eski satırları taşımaz: yeni sayfanın kırmızıları `YENİ KIRMIZI` olur ve satır için sahip kararı gerekir (paket `:381-383`).
- **Sayfa sahipleri (domain'ler):** tabandaki satırlar sizin sayfalarınızın borcudur; sayfaya sonraki dokunuşta ya da kendi işinizle kapatılır.
- **Son kullanıcılar:** etki yok; ekran, servis, veri ya da izin eklenmedi.
- **Başka kuralları okuyanlar:** şartname ve kontrol listesi kuralları dizinler, değiştirmez; kural dosyalarına (`.antigravity/rules/**`) dokunulmadı.
  Kural çelişkileri (D-8a, D-8b, D-8c) kuralın kendisinde düzeltilmek üzere sahip işidir (`daraltma-01.md:20-22`).

## Bilinen açıklar ve ertelenenler

Paket takip satırları (üst paket `:951-967`; FU01 `:277-286`). Durum sütunu paket satırlarından okundu; her açığın bugünkü durumu tek tek ölçülmedi:

| Kimlik | Açık | Durum |
|---|---|---|
| FU-1 | Paket standardına "UI uyum" kabul maddesi (`.antigravity` onayı) | açık, sahip işi |
| FU-4 | Golden Slim `personalization-client.js` kırmızısı | açık; tabanda 64 satır |
| FU-10 | DataTable fabrikasında filtre düğmesinde `aria-label` yok | açık, BL-440 sahibi |
| FU-11 | 31 "zorunlu" ifadenin kontrol listesinde karşılığı yok | açık, kontrol listesi revizyonu |
| FU-12, FU-15 | Golden Slim anahtar adı; Golden Compact kabul edilmedi | açık, developer-enablement |
| FU-14, FU-16, FU-19 | Eski sayfa borçları (yukarıdaki tablo) | açık, domain sahipleri |
| FU-17 | CI-3 tek seviyeli liste sayfasında (`Views/Meetings/_DataTable.cshtml`) koşmaz; `ölçülmedi` satırı | açık, `.antigravity` onayı |
| FU-18 | 106 sayfa JS dosyası kontrolsüz | **FU01 ile kapanıyor** (yalnız dalda); kalan: FU-23 |
| FU-20, FU-21 | FU01: başvurulmayan 2 dosya; `StrategyPeriodDetail.cshtml:132`'nin adlandırdığı eksik dosya (404 çıkarım, canlı ölçülmedi) | açık, enterprise-strategy-business-performance |
| FU-22 | FU01: kısmi görünüm zinciri izlenmez; 8 dosya `.../Components` sayfasına düşer | açık |
| FU-23 | FU01: yalnız `Shared` görünümün yüklediği dosyalar kırmızıya gidemez (borç bugün yok) | açık |

Doğrulama-01'in "Diğer bulgular"ı (`dogrulama-01.md:30-36`):
1. İşçi raporunda kanıt betiği sha'sı yoktu; doğrulama kaydında verildi.
2. PR sonrası `--ci-kaydi` adımı raporda açık değildi; AC-12 bu adımla kapanır (aşağıda).
3. İşçi yük 12 üstündeyken geliştirme self-test'leri koşmuş; resmî koşular kural içinde.
4. Düşük: `scan()` (`scripts/uic_changed_pages.py:607`) `Views/<tek seviye>/<Alt>/x.cshtml` görünümünü `<tek seviye>/<Alt>` sayfasına bağlıyor; bugünkü depoda oracle ile uyumlu, bu biçimde **yeni** görünüm ayrıca ölçülmedi.

**FU01 AC-12 hâlâ açık.** Kabul listesinin 13 zorunlu ölçütünden 12'si geçti, AC-12 (gerçek CI koşusu) kaldı: PR yok. Kule, PR'dan sonra `ui-compliance` koşusunu
`--ci-kaydi` ile yeni bir dizinde kaydedecek; kayıt olmadan FU01 kabulü yoktur (`dogrulama-01.md:11-12`, `:40`; `RAPOR.md:3`).

Ölçülmeyenler: gerçek CI koşusu (FU01 için), CI runner süresi, Linux'ta harf duyarlılığı (`pss-ui-p1-04-kabul-02/KABUL.md`, "Açık kalanlar"), P1'in bütün kabul bildirimi,
`dad448bb1` için ayrı kabul kaydı. P1-04'ün kabul koşusu hiçbir sayfaya dokunmayan bir PR'daydı; gerçek bir sayfa değişikliğinin CI'daki kaydı yoktur.

## Geri alma ve gerileme

- Araç ve taban eklemelidir; geri alma = commit revert (`docs/records/audits/2026-11/pss-ui-fu01-01-rapor-01/RAPOR.md:19`, "geri alma = commit revert"). İş akışı geri alınırsa PR'da UI denetimi kalmaz.
- Kontrolü zorunlu tutan şube koruması bu notta ölçülmedi.
- Bir PR taban satırı eklerse ya da sayısını artırırsa borcu büyütmüş olur; sahip kararı kuralı bunu denetler (AC-19, paket `:823`).

## Kapanış paketi

Kart kuralı S-118 ve `.antigravity/rules/docs-organization.md` §3.1 (`:97-117`) altı belgeyi ister. Karşılıkları:

| Belge | Yer ya da neden yok |
|---|---|
| API anlatısı | **YOK.** Uç nokta yok; paketin "Gateway / API Routing Decision" satırı: Gateway değişikliği gereksiz, endpoint yok (`MOD-0286-ui-design-system-page-pattern-governance.md:798-800`; FU01 `:181-183`). |
| Servis README | **YOK.** Servis yok. Aracın kullanımı betik başlığında (`scripts/uic_changed_pages.py:1-30`) ve bu notta. |
| Kullanıcı kılavuzu | **YERİNE "ne görülür" notu:** `docs/guides/ui-design-system-page-pattern-governance/index.html` (ekran yok, PR kontrolünün anlamı). |
| ADR | `docs/records/decisions/2026-11/pss-mod0286-adr-01.md` |
| Mimari denetim raporu | `docs/records/audits/2026-11/mod-0286-mimari-denetim-01.md` (başka bir ajan yazıyor; bu notun yazımında dosya yoktu, yalnız yolu anıldı) |
| Sürüm notu | bu dosya |

Kapanış listesi: `docs/records/audits/2026-11/mod-0286-kapanis-paketi-01.md`.
