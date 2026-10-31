# PSS-UI-P1-05 — bağımsız doğrulama 01

**Sonuç:** UYGUN · engelleyici 0 · diğer 3

Doğrulayıcı: işi ve işçi metnini yazmayan ayrı alt ajan (Opus 5.5, `claude-opus-5-5`). Depoda dosya değişmedi, commit yok. Denemeler depo dışında. Metin doğrulayıcının raporudur; CT yalnız depo dışı geçici yol adlarını kısalttı.

## 1. Kanıt koşusu

`sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-tek-seviye-kanit-02.sh <boş dizin> e3eda1752e3fe74ee8aa4f6f1ab02c992bc7d04b`: çıkış 0, `zorunlu geçti 10 / 10`, stderr 0 bayt, self-test `93/93`.

Kayıtlı koşuyla karşılaştırma: üst `stdout.txt`te 30 fark satırı, hepsi HEAD değeri ve klon commit kimlikleri. 14 alt koşunun `stdout.txt`i `süre:` satırı dışında bayt bayt aynı; 9 koşunun çıkış kodları aynı. İçeriği farklı 16 dosya var (`ac21a-draft.err`, `klon.txt`, 14 `.rr`); örnek açılan üçünde fark yalnız süre, commit kimliği ve tarih, öteki 13 `.rr` tek tek açılmadı.

Kayıt: `scripts/run_record.py verify` çıkış 0; `commit` = e3eda1752e3f…, `exit` 0; `tree` `0fa347c4a…` = `e3eda1752^{tree}`. `git log --diff-filter=AD`: betik yalnız 5d2774ae9'da, ölçüm dosyası yalnız 51b0370b6'da eklenmiş, silme yok.

## 2. Self-test (kod okuma ve mutant)

Kod: `scripts/uic_changed_pages.py:1350-1529`. Sabit doğru yok: AC-21g01…g14 durumlarının her biri geçici depoda gerçek `ci`, `execute("check")` ya da `execute("draft")` koşusu yapıyor; çıkış ve satır denetimi var (`:1358-1512`).

İstenen dallar: yalnız tabanda doğrudan görünüm `:1376-1382`, `:1389-1396`; `_ViewImports.cshtml` `:1447-1455` (zayıflığı D-1); FU-17 satırı birebir, tek kırmızı `:1513-1524`; tek seviye CI-3 reddi `:1482`. Diff'te `self_test` içinde yalnız ekleme var.

26 mutant koşuldu, 23'ü self-test'i çıkış 1'e düşürdü (işçinin koşmadığı AC-21g01 mutantı dahil). Yaşayan 3 mutant D-1, D-2, D-3'tür.

## 3. Paket satır 325-351 ve AC-21 ile kod karşılaştırması

| Paket isteği | Kod | Ölçüm |
|---|---|---|
| Doğrudan dosya → `{Module}`, her tür; `NOT_PAGE_AREAS` asla | `:884-885`, `:882` | `Meetings`, `Report`/`Series` satırı 0; self-test g01, g05 |
| `evaluate_page` yalnız doğrudan dosyaları okur | `:512`, `:192` | `Report`'taki sabotaj yalnız `YENİ KIRMIZI Meetings/Report CI-2 …`; `Meetings` seçilmedi; mutant düştü |
| JS yalnız doğrudan görünüm varsa sayfa (HEAD ya da taban) | `:898-900` | g02, g03; mutant düştü |
| FU-18 (1) (2) (2b) (3) kendi satırıyla | `:891-892`, `:897`, `:901-902` | `pages/`, `Meetings/series/`, `WorkCenterNext/fixtures/`, `Platform/PersonReferences/`, `CRM/RegulatoryTexts/`, `CRM/cycle-dates.js`, `SupplyChain/`, `Traceability/` → FU-18; `shared`, `custom`, üst düzey dosya, görünümü olmayan klasör → genel satır; `Views/Traceability` yalnız tabanda → FU-18 |
| Klasör düzeyi `_ViewStart` / `_ViewImports`: sayfa dosyası ve paylaşılan satır | `:885`, `:809-810` | `Tasks/_ViewImports`: paylaşılan satır + `PASS Tasks CI-2/5/6`, CI-1 yok; kök `Views/_ViewStart` yalnız paylaşılan |
| Page removed / new surface tek seviyede doğrudan, iki seviyede klasör | `:195-199`, `:841`, `:846` | `Meetings/Report` silindi → yalnız o sayfa; `Meetings/Zed` yeni yüzey CI-11 kırmızı; `Meetings/Index` silindi, sayfa yaşıyor; var olan sayfaya yeni partial CI-11 yok |
| CI-3 tek seviyede koşmaz | `:541-542`, `:739-740` | FU-17 satırı birebir, taslağa girmiyor (`:940`) |
| CI-12 tek seviyede yok | `:561` | Kod okuma |
| Okuyucu: `{Module}`, alt klasör instance reddi, CI-3 yalnız iki seviye | `:304-321`, `:314` | Mutant düştü; bozuk satırlar çıkış 2 |
| Taslak: tek seviyeler dahil, FU-16, sahip bilinmiyor | `:923-925`, `:395-396` | Mutant düştü |

CI kipi bütün sayfalara düşmüyor: sayfa kümesi yalnız `--name-status` farkından kuruluyor (`:791-840`); `self.draft()` tek çağrısı `:978`, yalnız `mode == "draft"`; `os.environ` yalnız git ortamına kopyalanıyor (`:137`). `--base ""`, `--base=--draft-baseline`, `--base HEAD --draft-baseline`, `--base HEAD --self-test`, `--base HEAD --check-baseline`, `--all` ve kipsiz hepsi çıkış 2; `UIC_ALL`, `ALL_PAGES`, `CI`, `GITHUB_ACTIONS` ortam değişkenleriyle `--base HEAD` çıkış 0 ve tek `süre:` satırı. `--draft` kısaltması yalnız taslak kipine gider. Workflow `--base "$BASE_SHA"` çağırıyor.

İki seviyeli sayfa değişmedi (işçinin yapmadığı ölçüm): klonda bir commit `Views` ve `wwwroot/assets/js` altındaki 2536 dosyaya boş satır ekledi; taban seçicisi + eski taban ile iş seçicisi + yeni taban karşılaştırıldı. İkisi de çıkış 0, `YENİ KIRMIZI` 0. Yalnız eskide 365 satır, hepsi `paylaşılan dosya` (162 + 87 + 106 + 11 − 1). Yalnız yenide 1324 satır: tek seviye PASS 1083, `TABAN FU-16` 134, FU-18 106, `ölçülmedi` 1. **Anahtarında `/` olan farklı satır 0.** FU-16 satırının üstüne çıkan sayı `YENİ KIRMIZI Meetings CI-7 … 2 +1` verdi.

## 4. İşçinin kendi kararları

Üçü de paketle çelişmiyor. `Shared`/`Archive` anahtarlı satırın reddi (`:304`): paket bu klasörlerin asla sayfa olmadığını söyler, ret kapalı-güvenlidir, bugün böyle satır 0; iki parçalı `Shared/X` anahtarı kod okumasına göre eskisi gibi kabul ediliyor (denenmedi). `js/{Module}/` altında `.js` olmayan doğrudan dosya → genel satır: paket yalnız `<file>.js` yazar, bugün böyle dosya 0, self-test'i yok (D-3). `pages/**` dosya türü fark etmeksizin FU-18: paket `**` yazar, bugün `.js` olmayan dosya 0.

## 5. Taban

Silinen 0, eklenen 134; eski satırlar aynı sırada, eklenenler dosya sonunda tek blok. Hepsi `FU-16`; `reason` tek değer `sahip kararı tek-seviye-borc-01 (tek seviye sayfalar, 2026-10-31)`; `taban-01` 0. `/` içermeyen anahtarlı FU-16 olmayan satır 0; `/` içeren FU-16 satırı 0. Yokluk 82, satır kontrolü 52 (48 dosya), 116 eşleşme. CI başına: CI-1 81, CI-2 4/12, CI-4 2/2, CI-6 35/73, CI-7 8/24, CI-8 1/1, CI-9 1, CI-10 2/4 (paketle aynı). Sahip tablosu karar kaydıyla satır satır aynı. Taslakla FU-16 satırlarının ilk altı hücresi aynı, yalnız `owner` farklı (beklenen). FU-16 dışında taslak ile taban arasında 2 Farmakovijilans satırı farkı (`taban-pvg`); iş değiştirmedi. Bağımsız ölçüm `origin/main` `cc63c620c`'de de 134, FU-16 ile fark 0. `--check-baseline` çıkış 0, `toplam satır=2153 eşleşme=858`. Depoda tabanı okuyan kod yalnız seçici ve workflow.

## 6. Kapsam

`git diff --name-only 5d2774ae9 acf9249e7`: seçici, taban, `pss-ui-p1-05-rapor-01/RAPOR.md`, `p1-05-kanit-01/**`. `execution/`, `.github/`, `.antigravity/` altında değişiklik yok; paket sha256 `0c53533e…`. İş commit'i yalnız ilk iki dosya (+396/−27). Kayıt ve raporda kişi adı ya da e-posta taraması 0 dosya.

## Bulgular

Engelleyici yok.

**D-1 — `_ViewImports` durumu üretim sabitini kullanıyor (`scripts/uic_changed_pages.py:1447`).** Döngü `for name in FOLDER_LEVEL_VIEWS`; `_ViewImports.cshtml` sabitten çıkarılınca durum da kayboluyor (`92/92`, çıkış 0), kanıt betiği de geçer. Davranış bugün doğru; paket AC-21 (g) yalnız `_ViewStart` ister, kabulü durdurmaz. Öneri: döngüde açık iki ad.

**D-2 — FU-18'in "yalnız tabanda" dalları self-test'te yok (`:897`, `:901`).** `either_has_dir(vtop)` → `head.has_dir(vtop)` mutantları iki satırda da `93/93`. Davranış gerçek koşuda doğru; AC-21 (g) bu durumu ayrıca istemez.

**D-3 — İşçi kararı 3'ün `.js` şartı test edilmiyor (`:898`).** `if True:` mutantı `93/93`. Davranış denemede doğru; bugün bu yolda dosya 0.

## Ölçülemeyenler

GitHub Actions'ta gerçek PR koşusu; Linux runner'daki büyük/küçük harf davranışı (seçici git ağacından okur; dosya sistemine bağlı yalnız CI-3 ve CI-14, tek seviyede CI-3 koşmuyor); birleşmiş PR ref'i ve taban ucu ile merge base'in ayrıştığı durum (bu iş değiştirmedi); doğrulayıcı aracı ve 106 FU-18 JS dosyasının içeriği (paket gereği ölçüm dışı); .NET derlemesi, servis, tarayıcı (iş kapsamı dışında). AC-21 (b) sayıları kanıt betiğinin koşusundan alındı. `js/Shared/d.js` denemesi APFS büyük/küçük harf duyarsızlığı yüzünden `js/shared/` altına düştü; `js/Shared/**` davranışı self-test g05 ve mutantın ölçümüdür.
