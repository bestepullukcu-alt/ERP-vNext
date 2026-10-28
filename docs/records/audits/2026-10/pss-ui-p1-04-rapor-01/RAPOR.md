# PSS-UI-P1-04 · MOD-0286 DL-3 — yapılandırılmış rapor (SOP §22)

> İş paketi PSS-UI-P1-04 · Prompt PSS-UI-P1-04-P v1.4 · hedef ajan devops-agent · 2026-10-27.
> Bu rapor ajanın kendi beyanıdır (K13). Doğrulama başka bir ajanındır. Kabul, Control Tower'ın tek kabul kaydıyla ve CI
> koşusuyla verilir (AC-17 bölüm 2). Kayıtlı koşu ara teslimdir; paket satır 678'e göre DL-3'ü ya da P1'i kabul ettirmez.

## Agent Verdict

PASS (ara teslim): seçici ve ilk taban teslim edildi. Kayıtlı koşu `zorunlu geçti 11 / 13` ile bitti; açık kalan iki
madde tasarım gereği açık:

- **AC-17b**: CI koşusu yok.
- **AC-20**: workflow dosyası yok (`.github/workflows/ui-compliance.yml`; CT-Setup ve sahip onayı bekleniyor).

## Branch / HEAD

| Ne | Değer |
|---|---|
| Dal | `feature/pss/pss-ui-p1-04` |
| Başlangıç HEAD | `0a69c97120c2659781d6891a482b090491ec0953` (Expected HEAD; kanıt betiğinin BASE'i) |
| Son commit (iş commit'i) | `a37ae998891e454343d2df1e04f4f2663d8d019a`, mesaj `feat(pss): MOD-0286 DL-3 — UI uyum seçicisi ve ilk taban (PSS-UI-P1-04)` |
| Commit sayısı | 1 (izin en çok 3; deneme koşusu hata göstermedi, ek commit yok) |
| Dışarıya giden | push, PR, merge ve dal değişikliği yok; main birleştirilmedi |

## Worktree status

- **Başlangıç:** `git status --porcelain --untracked-files=all` boştu.
- **Paket sha256:** başta ve sonda `87f46dcf035555c7c9c1b02a5b45579c66385a082fd7eeb36a2e49cd3e98dfe3`.
- **Commit edilmeyen iki dizin (bilerek):**
  - `docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-01/`: kayıtlı koşu dizini, 44 dosya.
  - `docs/records/audits/2026-10/pss-ui-p1-04-rapor-01/RAPOR.md`: bu dosya.
- **Son `git status --porcelain --untracked-files=all`:** yanıtta verildi. Bu dosya yazılırken yalnız bu iki dizindeki
  yeni dosyaları gösteriyordu.

## Changed files

`git diff --name-only 0a69c9712 a37ae9988`:

| Dosya | Satır | sha256 @ a37ae9988 |
|---|---|---|
| `scripts/uic_changed_pages.py` (yeni) | 1335 | `5ccbcd82257f451ff2abfc2520532666d60fba969d529edac89ceef5d5a47ca0` |
| `tests/ui-compliance-baseline.tsv` (yeni) | başlık + 2025 satır | `a8a616d6cc25eb3c90c64dfd7d332eeb582ad3b8600630b1a3800fa6fb6e4b6c` |

- `git diff --check 0a69c9712 a37ae9988`: temiz (çıkış 0).
- `git log --oneline 0a69c9712..a37ae9988`: 1 commit.

## Golden/Contract flow

Komut sözleşmesi: `python3 scripts/uic_changed_pages.py`, birbirini dışlayan dört kip. Kip yoksa, birden çok kip varsa ya
da bilinmeyen bir seçenek verilirse çıkış 2 olur (argparse'ın zorunlu, birbirini dışlayan grubu).

| Kip | Ölçülen çıkış | Ölçülen çıktı |
|---|---|---|
| kipsiz | **2** | `error: one of the arguments --base --self-test --check-baseline --draft-baseline is required` |
| `--self-test` | **0** | `self-test: 67/67 passed`; yerelde ve kayıtlı koşuda aynı (artefakt `ac17a-selftest.txt`) |
| `--check-baseline` | **0** | Aşağıdaki "Taban" tablosundaki CI başına satır ve eşleşme toplamları (artefakt `ac19-check.txt`) |
| `--base HEAD` (boş fark) | **0** | Tek satır `süre: 0.2 s` |
| `--draft-baseline` | **0** | Başlıklı TSV; stderr'e `süre: 16.9 s` (Adım 2) ve `süre: 16.6 s` (kayıtlı koşu, `ac19-draft.err`) |

### Çıktı dil bilgisi (uygulanan biçim)

**Kontrol satırları:**

- `PASS <page> CI-<n> <instance>[ <count>]`
- `TABAN <FU-n> <page> CI-<n> <instance>[ <count>]`
- `YENİ KIRMIZI <page> CI-<n> <instance>[ <count> +<d>]`
- `ölçülmedi <page> CI-<n> <instance> - doğrulayıcı eksik dosyada durdu`

**Kapanış satırları:**

- `kapandı - satır silinmeli <page> CI-<n> <instance>`
- `kapandı - satırı güncelle <b> -> <n> <page> CI-<n> <instance>`

**Diğer satırlar:**

- `sayfa kaldırıldı <page>`
- `kaynak kümesi kaldırıldı <resx-base>`
- `kayıt sayfaya eşlenmedi <dosya> <JSON yolu> <kanonik öğe>`
- `paylaşılan dosya, sayfa kontrolü yok <depo köküne göre yol>`
- `bilgi: <check metni> <page> <fu> <owner>`
- Son satır: `süre: <s> s`

**Satırların sırası:** sayfa başına önce kontrol satırları gelir, ardından `bilgi:` satırları, en sonda o sayfanın
`kapandı` satırları.

### Satır kontrol desenleri

- Çalışma anında paket dosyasının "DL-3 satır kontrol desenleri" `text` bloğundan okunur. Her satır
  `<anahtar><TAB><ham desen>` biçimindedir; dokuz anahtar beklenir, `re.compile` bayraksız uygulanır.
- `re.finditer`, UTF-8 metnin tamamında koşar (`errors="replace"`).
- Seçicinin kendi kopyası (`OWN_PATTERNS`) yalnız `--self-test` karşılaştırmasında kullanılır.

## Sub-flows

### Eşleme (paket satır 318-358)

- `git diff --name-status -z --find-renames --diff-filter=ACDMRT <base>...HEAD` okunur.
- Silinen yol ile yeniden adlandırmanın iki yolu aynı eşlemeden geçer. `A C D M R T` dışında bir harf çıkış 2 verir.
- Sayfa `Views/{Area}/{Module}/**` altındadır (`Shared` ve `Archive` hariç). JS dosyası yalnız `Views/{Area}/{Module}/`
  HEAD'de ya da base'te bir dosya taşıyorsa sayfa sayılır.
- Diğer eşlemeler:
  - `Resources/Views/**.resx` → resx tabanı (CI-13).
  - `Resources/SharedResource.*.resx` → CI-14.
  - `PageHelpResource.*.resx` ya da `PageHelpCatalog.cs` → CI-15.
  - `platform-search.{en,tr}.json` → CI-12. Her dosya kendi base sürümüyle karşılaştırılır. Anahtar, JSON yolu ile
    kanonik öğenin (`json.dumps(sort_keys=True, ensure_ascii=False, separators=(',', ':'))`) çoklu kümesidir.
- Üç ağacın (`Views`, `wwwroot/assets/js`, `Resources`) içinde eşlenmeyen dosya `paylaşılan dosya` satırıyla yazılır.
- Sayfa klasörü HEAD'de boşsa `sayfa kaldırıldı` yazılır ve o sayfanın satırları `kapandı - satır silinmeli` olur.

### Kontroller (paket satır 447-463)

- **CI-1, CI-9 (savedView), CI-11, CI-12, CI-13, CI-14, CI-15:** yokluk denetimleri.
- **Satır kontrolleri:** CI-2, CI-4, CI-5, CI-6 (görünüm ve JS), CI-7, CI-8, CI-9p, CI-10. Kural `n > b` → YENİ
  KIRMIZI `+<n-b>`; `n = b > 0` → TABAN; `0 < n < b` → TABAN + `kapandı - satırı güncelle`; `n = 0 < b` → `kapandı -
  satır silinmeli`; `n = b = 0` → PASS.
- **CI-3:**
  - Komut `verify_datatable_page.py . --area A --module M [--reference slim|compact] --format gaps --ok-line`.
  - Referans `_CreateEditOffcanvas.cshtml` ise slim, `_Form.cshtml` ise compact. İkisi de HEAD'den ve base'ten okunur.
  - Çıktı, `list_screen_touch_hook.py:62`'deki gibi kapalı-güvenli sınıflandırılır.
  - Doğrulayıcı sekiz çekirdek dosyadan birinde durursa, sayfanın öteki CI-3 satırları `ölçülmedi` yazılır.
- **CI-11 (rev 14):** yalnız yeni yüzeyde çalışır, yani base'te `Views/{Area}/{Module}/` altında hiç dosya yoksa.
  Yeniden adlandırmayla (`R`, yeni yol) gelen partial eklenmiş sayılır.

### Ara koşu (AC-17a) — kayıtlı koşudan örnek satırlar

Artefakt `p1-04-kanit-01/ac17a-ara.out`: çıkış 1, 133 satır. Dağılım: 124 PASS, 2 TABAN, 3 YENİ KIRMIZI, 2 ölçülmedi,
0 kapandı, 1 paylaşılan dosya ve 2 `bilgi:` satırı; kalan 1 satır `süre`.

```text
1:paylaşılan dosya, sayfa kontrolü yok frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js
2:PASS CRM/VisitReport CI-1 Views/CRM/VisitReport/Index.cshtml
31:YENİ KIRMIZI DevEnablement/GoldenReferenceCompact CI-3 _IndexL10n.cshtml exists
32:ölçülmedi DevEnablement/GoldenReferenceCompact CI-3 personalizationClient sends tenant header only for tenant users - doğrulayıcı eksik dosyada durdu
69:bilgi: reference not ready (DEV-0001 status: in-progress) DevEnablement/GoldenReferenceCompact FU-15 developer-enablement
77:TABAN FU-4 DevEnablement/GoldenReferenceSlim CI-3 personalizationClient sends tenant header only for tenant users
105:bilgi: RC-6 AddNewGoldenReferenceSlim DevEnablement/GoldenReferenceSlim FU-12 developer-enablement
106:PASS DevEnablement/UicAra CI-2 Views/DevEnablement/UicAra/_IndexL10n.cshtml 0
109:YENİ KIRMIZI DevEnablement/UicAra CI-11 Views/DevEnablement/UicAra/_IndexL10n.cshtml:yok:GetAllStrings
110:PASS Meetings/Report CI-1 Views/Meetings/Report/Index.cshtml
131:PASS Platform/Tenants CI-12 platform-search.en.json
132:PASS Platform/Tenants CI-12 platform-search.tr.json
133:süre: 0.4 s
```

Ara commit'in yolları (`ac17a-commit.txt`):

- `M` `CRM/VisitReport/Index.cshtml`
- `M` `GoldenReferenceSlim/Index.cshtml`
- `R100` `GoldenReferenceCompact/_IndexL10n.cshtml` → `UicAra/_IndexL10n.cshtml`
- `D` `Meetings/Report/_Filter.cshtml`
- `M` `diten-datatable.js`
- `M` `platform-search.tr.json`

`Meetings/Report` bir liste sayfası değildir (`_DataTable.cshtml` yok). Silinen yol onu seçer, ama CI-3 koşmaz.

## Failure paths

- **Çıkış 2 (kapalı-güvenli; self-test'te her biri ölçüldü):**
  - Eksik base ref; `--` ile başlayan base.
  - Bilinmeyen durum harfi (`X`, `U`) ya da eksik yol (`R100` tek yol).
  - Git deposu olmayan dizin.
  - Bozuk taban: boş hücre, tekrar anahtar, bozuk instance, `count` 0 / `x` / `01` / `+1`, satır kontrolü dışında
    `count` ≠ `-`, `FU14`, `CI-16`, `CI-6v` check olarak yazılmış, başlık birebir değil, eksik hücre.
  - JSON olmayan kayıt dosyası; `suggestions` eksik; url'si metin olmayan öğe.
  - Sınıflandırılamayan doğrulayıcı çıktısı.
  - `frontend/Diten.Web` altında HEAD'den farklı çalışma ağacı.
- **Araç hatası çıktısı:** stderr'e `HATA: …`, stdout'a yalnız `süre` yazılır. Hiçbir yol "0 değişen sayfa" demez.

## Tests

### Taban (Adım 2)

`python3 scripts/uic_changed_pages.py --draft-baseline > tests/ui-compliance-baseline.tsv` çıkış 0 verdi. Satırlar
yalnız taslak çıktısıdır; elle satır eklenmedi, silinmedi, sayı değiştirilmedi. `--check-baseline` çıkış 0 verdi.
Kayıtlı koşudaki AC-19 da taban ile o anki taslağın (page, check, instance, count) kümelerini aynı buldu: 2023 satır,
`bilgi:` satırları hariç.

**Toplam:** başlık + 2025 satır (2023 kontrol satırı + 2 `bilgi:` satırı); satır kontrollerinde 742 eşleşme.

**CI başına satır / eşleşme ve paket ölçümüyle fark:**

| CI | Taban satır / eşleşme | Paket ölçümü | Fark |
|---|---|---|---|
| CI-1 | 0 / 0 | 0 (satır 449) | yok |
| CI-2 | 17 / 42 (14 sayfa) | 17 / 42, 14 sayfa @ 0eb90a4ce | yok |
| CI-3 | 1182 satır, 152 liste sayfası; FU-4 63 satır / 63 sayfa; Golden Slim 1, Golden Compact 1 (FU-4) | 152 sayfa, 1182 açık satırı, FU-4 63 sayfa (satır 451) | yok |
| CI-4 | **76 / 77 (70 sayfa)** | 77 / 78, 71 sayfa | **−1 dosya / −1 eşleşme / −1 sayfa** (aşağıda) |
| CI-5 | 16 / 16 (yalnız JS, 16 sayfa) | 16 / 16 | yok |
| CI-6 | 94 / 249: görünüm 59 / 192 (36 sayfa), JS 35 / 57 (28 sayfa) | görünüm 59 / 192, JS 35 / 57 | yok |
| CI-7 | 8 / 13 | 8 / 13 | yok |
| CI-8 | 0 / 0 | 0 / 0 | yok |
| CI-9 | 125 satır: `savedView` 123 sayfa; `api/personalization` 2 / 2 | 123 sayfa (satır 457); 2 / 2 | yok |
| CI-10 | 107 / 343 (78 sayfa) | 107 / 343 | yok |
| CI-11 | 0 | tabanda 0 satır | yok |
| CI-12 | 22 satır = 11 Platform sayfası × en ve tr | 19 Platform sayfasından 11'i (satır 460) | yok |
| CI-13 | 0 | 0 (satır 461) | yok (okunan taban: aşağıda) |
| CI-14 | 376: missing fr/es/zh/ar/ru 35'er; equal tr 10, fr 47, es 39, zh/ar/ru 35'er | aynı (satır 462) | yok |
| CI-15 | 0 (51 kod × 7 dil) | 0 (satır 463) | yok |
| bilgi | 2 (FU-12 Golden Slim, FU-15 Golden Compact) | 2 (satır 416-418) | yok |

**CI-4 farkının nedeni:**

- Eksik satır `wwwroot/assets/js/Meetings/series/index.js` (1 eşleşme). Görünüm klasörü `Views/Meetings/Series/`
  (büyük S); JS klasörü `Meetings/series` (küçük s).
- Seçici git yollarını okur; git yolları büyük-küçük harfe duyarlıdır, CI'daki Linux makinesi gibi. Paket kuralı "JS
  yolu yalnız `Views/{Area}/{Module}/` dosya taşıyorsa sayfadır" der; bu kurala göre dosya bir sayfa değildir, paylaşılan
  dosyadır.
- Paketin 77 / 78 ölçümü büyük olasılıkla macOS'un harfe duyarsız diskinde alındı. Bu bir çıkarımdır, ölçülmedi.
- Depoda harf farkıyla eşleşen tek klasör budur (taramanın çıktısı).
- `git log -S'createCrudTable(' 0eb90a4ce..HEAD` boş döndü: kod değişmedi, fark yalnız ölçüm yöntemindedir.

**CI-13 okunan taban:**

- Bugün `Resources/Views` altında dil dosyası taşıyan 151 taban var: 21 Platform tabanı (18'i en+tr, 3'ü 7 dil) ve 130
  öteki taban (7 dil). Ayrıca aynı tabanları paylaşan 10 dilsiz `X.resx` dosyası var.
- Paket "163 base names read" der. Fark sayım tabanından gelir; kırmızı sayısı ikisinde de 0.

**`belirsiz — sahip belirleyecek` sahipli satırlar:**

- 376 satırın hepsi `Resources/SharedResource` sayfasının CI-14 satırıdır (`<lang>:missing:<key>` ve
  `<lang>:equal:<key>`). Neden: karar kaydının alan tablosu `Views/{Area}` alanlarını eşler; `Resources/SharedResource`
  bir Views alanı değildir.
- Dağılım: ar 35 equal + 35 missing; es 39 + 35; fr 47 + 35; ru 35 + 35; tr 10 equal; zh 35 + 35.
- Tam liste: `awk -F'\t' '$7 ~ /^belirsiz/' tests/ui-compliance-baseline.tsv`.
- Başka hiçbir sayfa ya da kontrol `belirsiz` değildir.

**Sahip dağılımı:**

| Sahip | Satır |
|---|---|
| platform-shared-services | 512 |
| belirsiz | 376 |
| supply-chain-execution | 353 |
| commercial-suite | 279 |
| human-capital-management | 233 |
| master-data-management | 78 |
| developer-enablement | 65 |
| portfolio-delivery | 39 |
| management-governance | 35 |
| procurement | 35 |
| pharmacovigilance | 20 |

**FU dağılımı:** FU-14 1960, FU-4 63, FU-12 1, FU-15 1.

**reason:** bütün satırlarda `sahip kararı pss-mod0286-taban-01 (ilk taban, 2026-10-27)` yazar. İki `bilgi:` satırına
ayrıca paket gerekçesi eklendi.

### `--self-test` durum listesi (67 / 67 PASS)

**Çıkışlar ve süre:**

- PASS `--base HEAD` (empty diff) -> exit 0, the only line is the duration 'süre: … s'
- PASS exit 0: a changed page whose only reds are baseline rows -> TABAN with its FU, PASS lines, exit 0
- PASS exit 2: a missing base ref -> tool error, never '0 changed pages'
- PASS exit 2: a base that is an option ('--all') is refused — no all-pages value

**Sayım:**

- PASS count: a second match on the line of the baselined match -> +1
- PASS count: a byte copy of the violating line on a new line -> +1
- PASS count: a line inserted above the violation -> unchanged, TABAN
- PASS count: a re-indented violating line -> unchanged, TABAN
- PASS count: the violation moved inside the file -> unchanged, TABAN
- PASS count: the violating line edited without a new match -> unchanged, TABAN
- PASS count: one violation removed and one added in the same file (the written limit) -> TABAN
- PASS count: a match completed on its continuation line -> +1 (the preparatory commit itself stays TABAN)
- PASS count fell (2 -> 1) -> TABAN 1 and 'kapandı - satırı güncelle 2 -> 1', exit 0
- PASS count fell to 0 -> 'kapandı - satır silinmeli', exit 0

**Eşleme:**

- PASS mapping: a deleted path selects its page; the verifier stops -> YENİ KIRMIZI '_Filter.cshtml exists', the page's other CI-3 row 'ölçülmedi', never 'kapandı'
- PASS mapping: a rename maps both paths — the old page removed ('sayfa kaldırıldı', its rows 'kapandı - satır silinmeli'), the new page checked as added (YENİ KIRMIZI)
- PASS mapping: a shared file and a JS file with no Views page -> 'paylaşılan dosya, sayfa kontrolü yok', exit 0

**Arama kaydı:**

- PASS registry: an entry added (Platform/Other, tr) -> the page selected; en still red (TABAN), tr closed
- PASS registry: both entries of a page removed (tr) -> YENİ KIRMIZI Platform/Tenants CI-12 platform-search.tr.json:yok
- PASS registry: only the navigation item removed, the suggestions item kept -> page selected, YENİ KIRMIZI
- PASS registry: an item object edited (keywords) -> the page selected
- PASS registry: a group renamed -> the page selected
- PASS registry: an entry whose url maps to no page -> 'kayıt sayfaya eşlenmedi'
- PASS registry: a page selected only through the registry runs CI-12 only
- PASS exit 2: a registry file not JSON -> tool error
- PASS exit 2: a registry file without 'suggestions' -> tool error
- PASS exit 2: a registry file an item without a string url -> tool error

**CI-11, CI-3, CI-13/14/15:**

- PASS CI-11: a partial added to a new surface without GetAllStrings(true) -> YENİ KIRMIZI; with it -> PASS
- PASS CI-11: a partial re-added to a page that has files at the base (AC-18 (c) revert) -> no CI-11, exit 0
- PASS exit 2: verifier output the selector cannot classify -> tool error
- PASS CI-3: a new verifier line on a page with a baseline row -> YENİ KIRMIZI, the old row TABAN
- PASS CI-13 / CI-14 / CI-15: a language file deleted, a SharedResource key removed, a step title removed -> each a named YENİ KIRMIZI

**Taban okuyucu:**

- PASS baseline reader: the fixture baseline reads (6 rows)
- PASS baseline reader: an empty cell -> exit 2
- PASS baseline reader: a duplicate page + check + instance -> exit 2
- PASS baseline reader: a malformed instance (a line number) -> exit 2
- PASS baseline reader: a malformed instance (another page's file) -> exit 2
- PASS baseline reader: a line-check count 0 -> exit 2
- PASS baseline reader: a line-check count that is not an integer -> exit 2
- PASS baseline reader: a line-check count with a leading zero -> exit 2
- PASS baseline reader: a line-check count with a sign -> exit 2
- PASS baseline reader: another row's count other than '-' -> exit 2
- PASS baseline reader: a malformed FU id -> exit 2
- PASS baseline reader: a check outside CI-1…CI-15 -> exit 2
- PASS baseline reader: a pattern key written as a check (CI-6v) -> exit 2
- PASS baseline reader: a header that is not exact -> exit 2
- PASS baseline reader: a row with a missing cell -> exit 2
- PASS exit 2: CI mode on a malformed baseline (duplicate key) -> tool error

**Git çıktısı:**

- PASS exit 2: git diff output b'X\x00a\x00' (unknown letter / missing path) -> tool error
- PASS exit 2: git diff output b'U\x00a\x00' (unknown letter / missing path) -> tool error
- PASS exit 2: git diff output b'R100\x00a\x00' (unknown letter / missing path) -> tool error
- PASS git diff output 'R086 old new' and 'D path' are read with both rename paths
- PASS exit 2: not a git repository -> tool error

**Desen bloğu:**

- PASS pattern block: every key of the pack's block present, compiling with re.compile, and the selector's own pattern for each key the same string
- PASS pattern block: a missing key -> refused
- PASS pattern block: a pattern that does not compile -> refused
- PASS pattern block: a key written twice -> refused
- PASS pattern block: a different string for a key -> the self-test comparison fails

**Taslak, kipler ve yazmama:**

- PASS draft: the fixture's reds as rows (CI-5 count 1, CI-6 count 2, CI-3 'Old gap', CI-12 en + tr) plus the two bilgi rows; no CI-11 row
- PASS --check-baseline on a well-formed baseline -> exit 0 with per-CI rows and matches
- PASS exit 2: a working tree that is not HEAD under frontend/Diten.Web -> tool error
- PASS modes: no mode -> exit 2
- PASS modes: ['--base', 'HEAD', '--draft-baseline'] -> exit 2
- PASS modes: ['--self-test', '--check-baseline'] -> exit 2
- PASS modes: ['--base'] -> exit 2
- PASS modes: ['--all'] -> exit 2
- PASS no write: every throw-away repository is clean after the runs (no file, no __pycache__)

### Deneme koşusu (Adım 4, kayıtsız)

- **Komut:** depo dışında `mktemp -d` ile açılan boş dizine (`/var/folders/…/tmp.vmLsGr0Hb3`), depo kökünden
  `sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit-04.sh <dizin> a37ae998891e454343d2df1e04f4f2663d8d019a`.
- **Sonuç:** ham çıkış 1, `zorunlu geçti 11 / 13`. `sonuç=kaldı` yalnız AC-20 ve AC-17b'de çıktı; beklenen budur.
- **Düzeltme:** gerekmedi. Depoda dosya kalmadı.

### Kayıtlı koşu (Adım 5)

**Koşu:**

- **Komut:** `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-01 -- sh docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit-04.sh docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-01 a37ae998891e454343d2df1e04f4f2663d8d019a`
- **run_record çıkışı:** 0 (`KOŞU KAYDI … · commit a37ae998891e · komutun çıkışı 1 · 43 artefakt`).

**Kayıt (`run-record-v1.json`):**

| Alan | Değer |
|---|---|
| commit | `a37ae998891e454343d2df1e04f4f2663d8d019a` (= son commit) |
| tree | `8b8398c6d572c4d7adb641e35f73e10a4f03ed42` |
| exit | 1 |
| başlangıç – bitiş (UTC) | 2026-10-27T14:51:17Z – 14:51:56Z |
| stderr.txt | 0 bayt |
| sha256 | `stdout.txt` `95e23aca63d3c7958f0b54032373e06888c128fe051a1e18921d2ed0b84b709d`; `run-record-v1.json` `4b75c540ed0c2bda52c1f427dda8db2916dc592cf0d564c96bf5894d5093b382` |

**`run_record.py verify …/run-record-v1.json`:** çıkış 0 (`KOŞU KAYDI biçimi geçerli · commit a37ae998891e · çıkış 1`).

**`stdout.txt` satırları:**

- **1** — `BASE=0a69c97120c2659781d6891a482b090491ec0953 IS=a37ae998891e454343d2df1e04f4f2663d8d019a HEAD=a37ae998891e454343d2df1e04f4f2663d8d019a CI=yok`
- **4** — `CT-2 sonuç=geçti (zorunlu)`
- **12** — `CT-3 sonuç=geçti (zorunlu)`
- **16** — `AC-11 sonuç=geçti (zorunlu)`
- **67** — `AC-19 sonuç=geçti (zorunlu)`
- **78** — `AC-17a sonuç=geçti (zorunlu)`
- **84** — `AC-18a sonuç=geçti (zorunlu)`
- **91** — `AC-18b1 sonuç=geçti (zorunlu)`
- **99** — `AC-18b2 sonuç=geçti (zorunlu)`
- **105** — `AC-18c sonuç=geçti (zorunlu)`
- **111** — `AC-18d sonuç=geçti (zorunlu)`
- **117** — `AC-18e sonuç=geçti (zorunlu)`
- **120** — `AC-20 sonuç=kaldı (zorunlu)` (satır 119: `workflow dosyası yok (.github/workflows/ui-compliance.yml) — CI komut satırı ölçülemedi`)
- **123** — `AC-17b sonuç=kaldı (zorunlu)` (satır 122: `CI koşu kimliği verilmedi — workflow (CT-Setup + sahip onayı) ve PR koşusu bekleniyor`)
- **140** — `zorunlu geçti 11 / 13`

**AC-18'in on üç koşusu** (artefakt `ac18*.exit`, `ac18*.out`; satır numarası `.out` dosyasındadır):

| Koşu | Çıkış | Adlı satır (satır no) |
|---|---|---|
| ac18a-sabotaj | 1 | 18: `YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-5 wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js 1 +1` · 8: `YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-3 index.js does not use jQuery DataTable plugin (.(DataTable\|dataTable)(...))` · 38: `süre: 0.3 s` |
| ac18a-geri | 0 | 37: `süre: 0.2 s` (YENİ KIRMIZI yok) |
| ac18b1-sabotaj | 1 | 10: `YENİ KIRMIZI Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index.js 2 +1` · 25: `süre: 0.1 s` |
| ac18b1-geri | 0 | 10: `TABAN FU-14 Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index.js 1` · 25: `süre: 0.1 s` |
| ac18b2-hazirlik | 0 | 10: `TABAN FU-14 Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index.js 1` · 25: `süre: 0.2 s` |
| ac18b2-sabotaj | 1 | 10: `YENİ KIRMIZI Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index.js 2 +1` · 25: `süre: 0.2 s` |
| ac18b2-geri | 0 | 10: `TABAN FU-14 Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index.js 1` · 25: `süre: 0.1 s` |
| ac18c-sabotaj | 1 | 7: `YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-3 _IndexL10n.cshtml exists` · 8: `ölçülmedi DevEnablement/GoldenReferenceSlim CI-3 personalizationClient sends tenant header only for tenant users - doğrulayıcı eksik dosyada durdu` · 35: `süre: 0.2 s` · CI-3 satırlarında `kapandı` yok (`ac18c-ci3.txt`) |
| ac18c-geri | 0 | 37: `süre: 0.2 s` (CI-11 satırı yok) |
| ac18d-sabotaj | 1 | 1: `YENİ KIRMIZI Platform/Tenants CI-12 platform-search.tr.json:yok` · 3: `süre: 0.2 s` |
| ac18d-geri | 0 | 3: `süre: 0.2 s` |
| ac18e-sabotaj | 1 | 1: `YENİ KIRMIZI Platform/Tenants CI-12 platform-search.tr.json:yok` · 3: `süre: 0.2 s` |
| ac18e-geri | 0 | 3: `süre: 0.2 s` |

**AC-18a'daki ikinci kırmızı:** sabotaj satırı (`$('#t').DataTable({})`) doğrulayıcının jQuery eklentisi kontrolünü de
kırmızıya çevirir. Bu doğru bir yan sonuçtur; adlı CI-5 satırı ayrıca vardır.

**`süre` satırları:**

- ara kayıt (AC-17a): `süre: 0.4 s` (`ac17a-ara.out:133`)
- taslak: `süre: 16.6 s` (`ac19-draft.err`)
- sabotaj ve geri alma koşuları: 0.1 – 0.3 s

## Persistence evidence

N/A: kalıcılık yok. Seçici depoya yazmaz. Bu kanıtlandı: kayıtlı koşudan sonra `run_record.py` koşu dizini dışında
değişiklik görmedi; self-test'in "no write" durumu da geçti. Taban git'te izlenen bir dosyadır.

## Security/RBAC/Tenant evidence

N/A: uç nokta, izin ve kiracı verisi yok. Seçici yalnız Python standart kütüphanesi ve git kullanır. Ağ, servis, .NET
derlemesi ve gizli bilgi yoktur. Raporda ve kodda kişi adı ya da e-posta yoktur; yalnız self-test'in sahte kimliği
`uic-self-test@localhost` geçer.

## Audit/Evidence

- **Kanıt düzeyi E2:** self-test, kayıtlı sabotaj koşuları ve `run_record`.
- **Kayıtlı koşu dizini:** `docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-01/` (commit edilmedi;
  Control Tower kendi commit'inde kaydeder).
- **Nihai kabul:** AC-17 bölüm 2'ye bağlıdır (CI koşusu ve artefaktı); bu işin dışındadır.

## Observability

Her CI kipi çıktısı `süre: <s> s` ile biter; araç hataları stderr'e `HATA: <neden>` olarak yazılır. Ayrı bir log ya da
metrik yok.

## Migration/Rollback

Yok. Geri alma, iki dosyalı tek commit'in (`a37ae9988`) geri alınmasıdır.

## Decisions

Paketin açıkça yazmadığı ama komut sözleşmesine uyan uygulama seçimleri. Her biri paket revizyonu girdisi olarak da
aşağıda listelendi.

1. **PASS satırının instance'ı.** Yokluk denetimlerinde ihlal yeri `:yok:<ne>` eki olmadan yazılır: dosya yolu, dil,
   `platform-search.<dil>.json` ya da CI-3'te doğrulayıcının `GAPS-OK: …` satırı. `n = 0 < b` durumunda yalnız
   `kapandı - satır silinmeli` yazılır, PASS satırı yazılmaz (paket satır 392-396'daki eşleme).
2. **Kirli ağaçta durma.** CI ve taslak kipleri, `frontend/Diten.Web` altında çalışma ağacı HEAD'den farklıysa çıkış 2
   verir. Neden: seçici dosyaları HEAD'den okur, oysa doğrulayıcı ve resx denetçisi diskten okur; kirli ağaçta ölçüm
   HEAD'e bağlanamaz. CI'da ve `run_record` altında ağaç zaten temizdir.
3. **Ağaç dışındaki yollar.** Üç ağacın (`Views`, `wwwroot/assets/js`, `Resources`), iki kayıt dosyasının ve
   `PageHelpCatalog.cs`'in dışında değişen yollar için satır yazılmaz. Örnek: yalnız seçiciyi, tabanı ya da workflow'u
   değiştiren bir PR. Bu durumda da taban ve desen bloğu her koşuda okunur ve doğrulanır; bozuk taban çıkış 2 verir.
4. **CI-13 anahtar eşitliği.** `<base>.en.resx` anahtarları, var olan her en-dışı dil dosyasıyla karşılaştırılır.
   Paketin "another language" ifadesi zorunlu dille sınırlanmamıştır. Bugün iki okuma da 0 satır verir, çünkü zorunlu
   diller var olan dillerin alt kümesidir.
5. **CI-14.**
   - Resx denetçisi `--context tenant` ve `--context platform` ile iki kez koşar (RC-22).
   - RC-24 kümeleri Python ile `uic-auto.sh` RC-24 bloğunun okuma biçimiyle kurulur: `<data name="…"` anahtarları; değer
     aynı satırda yoksa bir sonraki satırdan okunur, awk'taki gibi.
6. **CI-15.**
   - Kodlar en dosyasındaki `Help.{CODE}.Purpose` anahtarlarından alınır.
   - Katalog satırı `["{CODE}"] = [new(` ile bulunur; `new("<KOD>"` kodları okunur.
   - Adım numarası, `Step<n>` anahtarının sonek rakamlarıdır. RC-23'teki `grep -o '[0-9]*'` kodun içindeki rakamları da
     alırdı; bugünkü 51 kodun hiçbiri rakam taşımıyor.

## Blockers

- **AC-17b ve AC-20** bu işle kapanamaz: workflow dosyası ve CI koşusu gerekir (CT-Setup ve sahip onayı).
- Bunun dışında engel yok. Hiçbir Durma koşulu oluşmadı:
  - HEAD, dal ve temizlik başta tuttu; paket sha'sı tuttu.
  - CI-1, CI-8, CI-11, CI-13, CI-15 için taslakta satır çıkmadı.
  - Hiçbir CI kimliği paket ölçümünün iki katı ya da yarısı değil; en büyük fark CI-4'te −1 dosya.
  - Kanıt betiğinde hata görülmedi.

## Known gaps

### Paket revizyonu girdileri

Paket değiştirilmedi; bunlar yalnız girdidir.

1. **CI-3 instance'ı ` — ` içeren kontrol adlarında kesik.**
   - Paket satır 380 ve komut sözleşmesi instance'ı "`N) ` ile ` — ` arası" olarak tanımlar. Seçici ilk ` — `'de keser.
   - Doğrulayıcının üç kontrol adı kendisi ` — ` taşır (`verify_datatable_page.py`):
     - `:761`: `Server mode: export declared server-side (BL-452 — CSV/Excel = every matching row, not the page)`
     - `:624`: `… for every row — visible columns, …` (List factory carries: Server export)
     - `:640`: `… exportUrl('csv') — every matching row, never the page`
   - Bugünkü tabanda 14 CI-3 satırının instance'ı bu yüzden `Server mode: export declared server-side (BL-452`.
   - Kesik değer kararlıdır ve tekildir: 1182 satır, 1182 anahtar, paketin ölçtüğüyle aynı. Ama "doğrulayıcının kontrol
     adı" ifadesiyle birebir aynı değildir.
   - Öneri: paket ya kesmeyi açıkça yazsın, ya da doğrulayıcının ad/ayrıntı ayracı değişsin (`.antigravity` işi).
2. **JS ile görünüm klasörü arasındaki harf farkı.**
   - `wwwroot/assets/js/Meetings/series/` ile `Views/Meetings/Series/` yalnız harf büyüklüğünde ayrışıyor.
   - Paket kuralı harfe duyarlı okunduğunda `Meetings/Series` sayfasının JS'i hiçbir değişiklikte sayfa kontrolüne
     girmez; `paylaşılan dosya` olarak listelenir. Paketin CI-4 ölçümü (77 / 78 / 71) bu dosyayı saymıştı.
   - Öneri: paket harf kuralını yazsın; klasör adı düzeltmesi Meetings sahibinindir (XMC-001).
3. **Paketin biçimini belirtmediği instance'lar.**
   - CI-14 Title Case için `<lang>:title-case:<key>` seçildi.
   - CI-15 koşul adları: `dosya-yok`, `Purpose-Audience`, `Related.<KOD>`, `related_total`, `range_ok`, `seq_diff`,
     `title_diff`. Paket yalnız `<lang>:<failed RC-21 / RC-23 condition>` der.
   - Bugün bu biçimlerde satır yok.
   - Diğer biçimlendirme seçimleri: `kayıt sayfaya eşlenmedi` satırının `<anahtar>` kısmı `<JSON yolu> <kanonik öğe>`;
     PASS satırının instance'ı Decisions madde 1'de.
4. **Resx tabanı sayısı.** Paket satır 461'deki "163 base names read" değeri yeniden üretilmedi. Bugün: dil dosyalı 151
   taban, aynı tabanları paylaşan 10 dilsiz dosya, ayrıca SharedResource ve PageHelpResource.
5. **CI-14 sahipliği.** CI-14 satırlarının sahibi karar kaydı tablosuna göre `belirsiz` (376 satır). SharedResource için
   bir sahip kararı gerekir.
6. **Liste olmayan sayfada dosya silme.** `Meetings/Report/_Filter.cshtml` gibi bir silme sayfayı seçer, ama hiçbir
   kontrol kırmızı vermez; CI-3 yalnız liste sayfasında koşar. Paket metniyle uyumlu, bilgi amaçlı.

### Ölçülmeyenler ve nedenleri

- **CI koşusu ve workflow'daki seçici çağrısı (AC-17b, AC-20):** workflow dosyası yok; CT-Setup ve sahip onayı bekleniyor.
- **Linux'ta (CI makinesi) koşu:** yalnız macOS'ta koşuldu. Seçici git yollarını okuduğu için harf duyarlılığı Linux'la
  aynı olmalı; bu bir çıkarımdır, ölçülmedi.
- **CI-3 doğrulayıcısının `--api-profile` / `--data-mode` ile koşusu:** paket varsayılanları ister (satır 451); başka
  profil denenmedi.
- **Bağımsız inceleme:** bu iş için yapılmadı; bu rapor ajanın beyanıdır.

## Out-of-scope changes

none. Yalnız `scripts/uic_changed_pages.py` ve `tests/ui-compliance-baseline.tsv` commit edildi. İzinli iki kayıt dizini
commit edilmedi. Paket, `.github/**`, `.antigravity/**`, `frontend/**`, kanıt betikleri ve öteki yollar değişmedi;
sayfa kırmızısı düzeltilmedi (XMC-001).
