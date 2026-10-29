# PSS-UI-P1-04 — bağımsız doğrulama 01 (MOD-0286 DL-3)

- **Doğrulayan:** bağımsız doğrulayıcı alt ajan (Claude Code, Opus 5.5). İşi ve prompt'u bu ajan yazmadı. Salt okunur çalıştı: depoya yazmadı, stage etmedi, commit atmadı, dal değiştirmedi.
- **Tarih:** 2026-10-28
- **İşçi dalı:** `feature/pss/pss-ui-p1-04` @ `b008804637bf768b98e88effc534a09065cac6be`. Worktree `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-p1-04`; `git status --porcelain --untracked-files=all` başta ve sonda boştu.
- **Commit'ler:**
  - taban (Expected HEAD): `0a69c97120c2659781d6891a482b090491ec0953`
  - iş: `a37ae998891e454343d2df1e04f4f2663d8d019a`
  - kanıt: `880bf4aab19b7074aec1e7a78cd093c5d0443a85`
  - rapor: `b008804637bf768b98e88effc534a09065cac6be`
- **Yetki sha'ları (ölçüldü, `shasum -a 256`):**
  - paket MOD-0286 rev 14: `87f46dcf035555c7c9c1b02a5b45579c66385a082fd7eeb36a2e49cd3e98dfe3` (beklenenle aynı; `0a69c9712..b00880463` aralığında değişmedi)
  - prompt `PSS-UI-P1-04-v1.4.md` (`chore/ct/pss-ui-ct-01`, son dokunan commit `03f267b03`): `a1f826a8665f75a2ce045a53d2fed7d2da3c322c53ea314d9183ffe786fb76cd` (beklenenle aynı)
  - kanıt betiği `uic-dl3-kanit-04.sh`: `3d5ed8bcb0adcd71ae9e85e75ed05076bcb3bcca5adc2a93ee045e5532325723`; ekleyen commit `0a69c9712` (= BASE)
  - `scripts/run_record.py`: `37bd43ba…` (kayıttaki `tool.sha256` ile aynı)
- **Yöntem:**
  - Okunanlar: git geçmişi, prompt, paket satırları ve seçicinin tamamı (1335 satır).
  - Seçicinin kipleri HEAD'de koşuldu. `--self-test` için `TMPDIR` `mktemp -d` altına verildi; Python önbelleği kapalıydı (`PYTHONDONTWRITEBYTECODE=1`).
  - `--draft-baseline` bağımsız olarak yeniden üretildi ve commit'li tabanla bayt bayt karşılaştırıldı.
  - Satır kontrollerinin sayımı seçiciyi import etmeyen ayrı bir betikle yeniden yapıldı (`p104-dogrulama/indep_count.py`).
  - Depo dışında, geçici bir git deposunda küçük denemeler koşuldu (`p104-dogrulama/probe.sh`).
  - Kanıt betiği v04 HEAD'de bir kez, çıktı dizini `mktemp -d` altında olacak şekilde koşuldu; sonuç kayıtlı koşuyla karşılaştırıldı.
  - Yük, ağır koşulardan önce ölçüldü: 1 dakikalık yük 2.7–5.2 (12'nin altında).

## Kontroller

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| 1a Kapsam: commit sayısı | uygun | `git log --oneline 0a69c9712..b00880463` → tam 3 commit: `a37ae9988`, `880bf4aab`, `b00880463` |
| 1b Kapsam: her commit'in yolları | uygun | `git show --name-status`: `a37ae9988` → yalnız `A scripts/uic_changed_pages.py`, `A tests/ui-compliance-baseline.tsv`; `880bf4aab` → yalnız `A …/mod-0286-uic-ci-01/p1-04-kanit-01/*` (44 dosya); `b00880463` → yalnız `A …/pss-ui-p1-04-rapor-01/RAPOR.md` |
| 1c Kapsam: korunan yollar | uygun | `git diff --name-status 0a69c9712 b00880463` → 47 satır, hepsi `A`. `M`/`D`/`R` yok; `.github/**`, `.antigravity/**`, `frontend/**`, paket, kanıt betikleri ve öteki `docs/records/**` dosyalarına dokunulmadı. `git diff --stat 0a69c9712 b00880463 -- uic-dl3-kanit{,-02,-03,-04}.sh` boş |
| 1d `git diff --check` | uygun | `git diff --check 0a69c9712 b00880463` → çıkış 0, çıktı yok |
| 2a `run_record.py verify` | uygun | `python3 scripts/run_record.py verify …/p1-04-kanit-01/run-record-v1.json` → `KOŞU KAYDI biçimi geçerli · commit a37ae998891e · çıkış 1`, çıkış 0 |
| 2b Kayıt alanları | uygun | `commit` = `a37ae998891e454343d2df1e04f4f2663d8d019a`; `tree` = `8b8398c6d572…`, `git rev-parse 'a37ae9988^{tree}'` ile aynı; `argv` = `["sh", "…/uic-dl3-kanit-04.sh", "…/p1-04-kanit-01", "a37ae998…"]`; `exit` 1; başlangıç 14:51:17Z. İş commit'i 14:49:52Z'de atıldı, yani koşu commit'ten sonradır |
| 2c Artefakt hash'leri | uygun | 43 artefaktın sha256'sı ve bayt sayısı yeniden hesaplandı; commit'li dosyalarla uyuşmayan 0. Diskte artefakt listesinde olmayan tek dosya `run-record-v1.json`'un kendisi |
| 2d stdout özeti | uygun | `stdout.txt:1` `BASE=0a69c97120c2…` · `:140` `zorunlu geçti 11 / 13`. `sonuç=kaldı` yalnız `:120` AC-20 ve `:123` AC-17b. Öteki 11 kimlik `geçti`; `stderr.txt` 0 bayt |
| 3a CLI kipleri | uygun | Kipsiz → çıkış 2 (argparse `one of the arguments … is required`). `--base HEAD --check-baseline` → 2 (`not allowed with`). `--base=-x` → 2 (`HATA: base '-x' bir ref değil`). `--base nosuchref` → 2. `--base HEAD` → çıkış 0, tek satır `süre: 0.2 s`. Kod: `scripts/uic_changed_pages.py:1317-1325`, `:461-465` |
| 3b Çıktı dil bilgisi | uygun | Satır kalıpları prompt v1.4 satır 50-54 ile karşılaştırıldı (`:712`, `:714`, `:717`, `:722`, `:730-740`, `:745-753`, `:786`, `:795`). Kayıtlı çıktıdan örnekler: `YENİ KIRMIZI Meetings/Report CI-5 wwwroot/assets/js/Meetings/Report/index.js 2 +1` (`ac18b1-sabotaj.out:10`), `TABAN FU-14 … index.js 1` (`ac18b1-geri.out:10`), `ölçülmedi … CI-3 personalizationClient … - doğrulayıcı eksik dosyada durdu` (`ac18c-sabotaj.out:8`). CI çıktılarında `CI-6v`, `CI-6j` ya da `CI-9p` geçmiyor (grep `*.out` → 0) |
| 3c CI-11 örnek biçimi | uygun | `<path>:yok:GetAllStrings`: kod `:525`, taban okuyucu `:310-312`. Kayıtlı çıktıda `ac17a-ara.out:109`: `YENİ KIRMIZI DevEnablement/UicAra CI-11 Views/DevEnablement/UicAra/_IndexL10n.cshtml:yok:GetAllStrings`; `count` yok |
| 3d Eşleme kuralları ↔ paket 318-358 | uygun | Fark: `--name-status -z --find-renames --diff-filter=ACDMRT <base>...HEAD` (`:757-758`); harf `[ACDMRT]` dışı → 2 (`:223-224`); iki yol (`:225-228`). JS → sayfa yalnız `Views/{A}/{M}/` HEAD ya da base'te varsa (`:845-849`). Kayıt anahtarı JSON yolu + kanonik öğe, çoklu küme (`:409-414`, `:787-797`). Kaldırılan sayfa (`:801-803`). Paylaşılan dosya (`:844`, `:849`, `:857`). Depo dışı denemede (`probe.sh`) `T` değişikliği sayfayı seçti; var olan sayfaya taşınan partial CI-11 koşturmadı; yeni yüzeye `R100` ile gelen `_L10n.cshtml` → `YENİ KIRMIZI Ar/Yeni CI-11 Views/Ar/Yeni/_L10n.cshtml:yok:GetAllStrings`, çıkış 1 |
| 3e Kontroller ↔ paket 447-463 (CI-11 459) | uygun | CI-1 (`:490-497`); satır kontrolleri desen bloğundan okunuyor (`:234-270`, `:499-508`); CI-3 komutu ve referans çıkarımı (`:536-578`); CI-9 (`:512-518`); CI-11 yalnız `base`'te klasörü olmayan sayfada (`:806-807`, `:771-773`); CI-12 `navigation` + `url` eşitliği (`:580-591`); CI-13 (`:593-614`); CI-14 (`:616-655`); CI-15 (`:657-702`). Paket satır numaraları doğrulandı: 318, 433, 449, 459, 463, 503-513 |
| 3f Desen bloğu | uygun | `OWN_PATTERNS` (`:60-70`), paketin `:504-512` bloğuyla aynı. Bunu self-test durumu `pattern block: … same string` ölçüyor (PASS) |
| 4a `--self-test` | uygun | `TMPDIR=<mktemp> PYTHONDONTWRITEBYTECODE=1 python3 scripts/uic_changed_pages.py --self-test` → çıkış 0, `self-test: 67/67 passed`, `FAIL` satırı 0 |
| 4b `--check-baseline` | uygun | Çıkış 0. `taban tests/ui-compliance-baseline.tsv geçerli`; CI-2 17/42, CI-3 1182/0, CI-4 76/77, CI-5 16/16, CI-6 94/249, CI-7 8/13, CI-9 125/2, CI-10 107/343, CI-12 22/0, CI-14 376/0; CI-1, CI-8, CI-11, CI-13, CI-15 0; bilgi 2; `toplam satır=2025 eşleşme=742` |
| 5a Taban: satırı olmaması gereken kontroller | uygun | `cut -f2 tests/ui-compliance-baseline.tsv \| grep -c -x <id>` → CI-11 0, CI-1 0, CI-8 0, CI-13 0, CI-15 0 |
| 5b Taban biçimi | uygun | Başlık `page\tcheck\tinstance\tcount\treason\tfu\towner$` (`cat -vet`). fu: FU-14 1960, FU-4 63, FU-12 1, FU-15 1. reason: 2023 satır karar metni, iki `bilgi:` satırı karar metni + paket gerekçesi. Sahip tablosu `:88-101` ile karar kaydının tablosu aynı (36 alan). HEAD'deki 36 `Views` alanının hepsi tabloda. `belirsiz` 376 satırın hepsi `CI-14` (`Resources/SharedResource`, `Views/{Area}` değil) |
| 5c AC-19: taslak = commit'li taban | uygun | `--draft-baseline` → çıkış 0, `süre: 17.6 s`. `cmp draft.tsv tests/ui-compliance-baseline.tsv` → aynı; iki dosyanın sha256'sı da `a8a616d6…`. İki `bilgi:` satırı dahil bayt bayt aynı. Kanıt betiğinin süzgeci (`grep -v '\tbilgi: '`, `cut -f1-4`) iki tarafa aynı uygulanıyor (`uic-dl3-kanit-04.sh:89-90`) |
| 5d Bağımsız sayım | kısmi (açıklandı) | Seçiciden bağımsız sayım (`indep_count.py`, macOS diskinde) 321 dosya / 743 eşleşme verdi; taban 320 / 742. Tek fark `Meetings/Series CI-4 wwwroot/assets/js/Meetings/series/index.js 1`. Git `js/Meetings/series` (küçük s) izliyor, `Views/Meetings/Series` ise büyük S. Harfe duyarsız disk bunları eşliyor, git yolları eşlemiyor. Öteki 320 anahtar ve sayıları birebir aynı. Fark seçicinin hatası değil, paket kuralının harfe duyarlı okunmasıdır (bulgu E-1) |
| 6 Güvenlik | uygun | `grep subprocess\|shell=\|os.system\|urllib\|socket\|http\|requests` → yalnız liste argv'li `subprocess.run` (`:135`, `:548`, `:624`, `:1293`). `shell=True` yok; ağ modülü yok; token ya da gizli bilgi yok. `-` ile başlayan base reddediliyor (`:461`), öteki ref'ler `rev-parse --verify` ile çözülüyor. Dosya adı argv öğesi olarak geçiyor, komut metnine eklenmiyor. CI kipi depoya yazmıyor: koşulardan sonra `git status` boş; self-test `no write` PASS |
| 7 RAPOR iddiaları | kısmi | 15 iddia karşılaştırıldı: 13'ü uygun, 2'si yanlış (aşağıdaki tablo, bulgu E-2 ve E-3) |
| 8 Kanıt betiği v04 yeniden koşusu | uygun | HEAD `b00880463`'te: `sh …/uic-dl3-kanit-04.sh <mktemp-dizin> a37ae998…` → çıkış 1, `zorunlu geçti 11 / 13`, `sonuç=kaldı` yalnız AC-20 ve AC-17b. `sonuc.txt` kayıtlıyla bayt bayt aynı. 14 `.exit` dosyası aynı. 14 `.out` dosyası, `ac19-*`, `ac18c-ci3.txt`, `ct3*.txt` `süre:` satırları dışında aynı. Betiğin kendi self-test'i 67/67 |
| DoR-01 not 1 (kurcalama) | uygun | `git log --diff-filter=AD -- uic-dl3-kanit-03.sh uic-dl3-kanit-04.sh` → v03 için tek ekleme `5341e0dad`, v04 için tek ekleme `0a69c9712`; silme yok. `git diff 0a69c9712 b00880463 -- <betikler>` boş |
| DoR-01 not 2 (nihai koşuda AC-19) | uygun (bugün) | Bugün taslak ile taban bayt bayt aynı (5c). Main birleştirildikten sonra nihai koşuda yeniden ölçülmeli; bu doğrulamada main birleştirilmedi |
| DoR-01 not 3 (atıflar ve desen anahtarı) | uygun | `acceptance_record.py:525-529` artefakt kuralı, `:665-667` ham çıkış kuralı. CI çıktılarında `CI-6v`, `CI-6j`, `CI-9p` yok (3b) |
| DoR-01 not 4 (tetikleyici yolları) | uygun | Paket rev 14 satır 433: seçici, taban ve workflow dosyası tetikleyicide. `--base 0a69c9712` (yalnız seçici ve taban değişti) → çıkış 0, yalnız `süre: 0.2 s`; taban her koşuda okunuyor (`:469-471`). Workflow'un kendisi ölçülmedi |
| DoR-02 not 1 (CI-11/1/8/13/15 = 0) | uygun | 5a |
| DoR-02 not 2 (eski yorum) | uygun (davranış etkisiz) | `uic-dl3-kanit-04.sh:42` → `# CT-2 — paket rev 13 (bu işin onaylı paketi) değişmedi`; ölçülen sha rev 14 (`:25`) |
| DoR-02 not 3 (CI-11 biçimi pakette yok) | uygun (girdi doğrulandı) | Pakette `GetAllStrings` yalnız `:151`, `:170`, `:224`, `:459`, `:794`'te geçiyor; `:yok:GetAllStrings` biçimi yok. Yalnız genel `<path>:yok:<what>` (`:613`) var. Paket revizyonu girdisi |

### RAPOR iddiaları (kontrol 7)

| RAPOR iddiası | Sonuç | Kanıt |
|---|---|---|
| Seçici 1335 satır, sha256 `5ccbcd82…` | uygun | `wc -l` 1335; `shasum` `5ccbcd82257f…` |
| Taban başlık + 2025 satır, sha256 `a8a616d6…` | uygun | `wc -l` 2026; `shasum` `a8a616d6cc25…` |
| İşçinin commit sayısı 1 | uygun | `a37ae9988` tek iş commit'i |
| `--self-test` 67/67, `--base HEAD` `süre: 0.2 s` | uygun | 4a, 3a |
| CI başına taban tablosu (CI-4 76/77, öteki satırlar paketle aynı) | uygun | 4b; CI-14 dağılımı: ar/ru/zh 35+35, es 39+35, fr 47+35, tr 10 equal |
| CI-4 farkının nedeni `Meetings/series` harf farkı; depoda tek örnek | uygun | 5d; git ağacında harf farkıyla eşleşen tek JS klasörü `Meetings/series` ↔ `Meetings/Series` |
| `git log -S'createCrudTable(' 0eb90a4ce..HEAD` boş döndü | **uygun değil (yazıldığı biçimiyle)** | Bugün 10 commit dönüyor (`a37ae9988`, `b00880463`, `db61cf944` paket ve `docs/records/**` kayıtları). `-- frontend/Diten.Web` ile sınırlanınca boş. Vardığı sonuç (sayfa kodu değişmedi) doğru (E-3) |
| CI-13: 151 dilli taban, 21 Platform, 10 dilsiz | uygun | Git ağacı sayımı: 151 / 21 / 10 |
| `belirsiz` 376 satırın hepsi CI-14; sahip ve FU dağılımı | uygun | 5b; sahip sayıları (512, 376, 353, 279, 233, 78, 65, 39, 35, 35, 20) birebir |
| CI-3 instance'ı ilk ` — `'de kesiliyor; 14 satır `Server mode: export declared server-side (BL-452` | uygun | `awk` → 14; `verify_datatable_page.py:761` adın kendisinde ` — ` var (`:624`, `:640` de) |
| Kayıt alanları (commit, tree `8b8398c6…`, exit 1, zaman, stderr 0 bayt, stdout sha `95e23aca…`, kayıt sha `4b75c540…`) | uygun | 2b; `shasum` iki dosyada da aynı |
| `stdout.txt` satır numaraları (1, 4, 12, 16, 67, 78, 84, 91, 99, 105, 111, 117, 120, 123, 140) | uygun | `grep -n 'sonuç='` ve `cat -n`: hepsi aynı satırda |
| AC-18 13 koşu: çıkışlar ve adlı satırlar (ör. `ac18a-sabotaj.out:18`, `:8`; `ac18c-sabotaj.out:7`, `:8`; `ac18d/e-sabotaj.out:1`) | uygun | `grep -n` ile tek tek; geri alma ve hazırlık koşularında `YENİ KIRMIZI` 0; `ac18c-geri.out`'ta `CI-11` 0 |
| Ara koşu `ac17a-ara.out`: 133 satır; 124 PASS, 2 TABAN, **3** YENİ KIRMIZI, **2** ölçülmedi, 1 paylaşılan, 2 bilgi, 1 süre | **uygun değil** | Ölçülen: 133 satır; 124 PASS, 2 TABAN, **2** YENİ KIRMIZI (`:31`, `:109`), **1** ölçülmedi (`:32`), 0 kapandı, 1 paylaşılan, 2 bilgi, 1 süre (toplam 133). RAPOR'un sayıları toplamda 135 ediyor (E-2). Alıntılanan örnek satırlar doğru |
| Out-of-scope: none | uygun | 1b, 1c |

## Bulgular

### Engelleyici

Yok (0).

### Engelleyici olmayan

- **E-1 (kapsam boşluğu, paket revizyonu girdisi; işçi de bildirdi).** JS klasörü `wwwroot/assets/js/Meetings/series/` (küçük s), görünüm klasörü `Views/Meetings/Series/` (büyük S). Seçici git yollarını harfe duyarlı okuyor ve paketin "JS yolu yalnız `Views/{A}/{M}/` varsa sayfadır" kuralını uyguluyor (`:845-849`). Bu yüzden bu klasördeki bir JS değişikliği `paylaşılan dosya, sayfa kontrolü yok` olarak listeleniyor ve satır kontrollerinden geçmiyor. Depo dışı denemede `createCrudTable(` eklenen harf farklı JS klasörü çıkış 0 ve yalnız `paylaşılan dosya …` satırı verdi (`probe.sh`, bölüm B). Paketin CI-4 ölçümündeki 77/78 ile tabandaki 76/77 arasındaki fark tam olarak bu dosyadır. Kod paketle uyumlu; boşluk paket kuralındadır. Klasör adı düzeltmesi Meetings sahibinin işidir (XMC-001).
- **E-2 (rapor hatası).** RAPOR "Ara koşu (AC-17a)" bölümü 3 `YENİ KIRMIZI` ve 2 `ölçülmedi` diyor; artefakt 2 ve 1 gösteriyor (`ac17a-ara.out:31`, `:32`, `:109`). Kanıt artefaktı doğru, rapor metni yanlış. Kabul sonucunu değiştirmez.
- **E-3 (rapor hatası, yeniden üretilemeyen komut).** RAPOR'daki `git log -S'createCrudTable(' 0eb90a4ce..HEAD` yazıldığı biçimiyle boş dönmüyor (10 commit; hepsi `docs/**`, paket ya da seçici). `-- frontend/Diten.Web` ile boş. Vargı doğru, komut eksik yazılmış.
- **E-4 (kod, eskimiş taban satırı bildirilmiyor).** Bir sayfanın `_DataTable.cshtml`'i hem base'te hem HEAD'de yoksa, sonraki bir PR o sayfayı seçtiğinde CI-3 ve CI-9 (`savedView`) koşmuyor (`:509-518`). `emit` yalnız koşan kontrollerin satırlarına bakıyor (`:705-742`). Bu yüzden o sayfanın eski CI-3 ve CI-9 taban satırları `kapandı - satır silinmeli` olarak yazılmıyor; paket satır 639 "eskimiş satır yazılır" der. Bu durum hiçbir zaman yeni bir kırmızıyı gizlemez, yalnız silinecek satırı göstermez. Bugünkü tabanda böyle bir sayfa yok. Bu sonuç koddan çıkarıldı; senaryo koşturulmadı.
- **E-5 (bilgi).** argparse `allow_abbrev` varsayılan açık (`:1316`), yani `--draft` → `--draft-baseline`, `--check` → `--check-baseline` kısaltmaları kabul ediliyor. CI komut satırı `--base <ref>` olduğu için AC-20 ihlali değil. Yine de tüm sayfa kipine ulaşan yazım biçimlerini azaltmak için `allow_abbrev=False` önerilir.
- **E-6 (paket revizyonu girdileri; doğrulandı, işçi ve DoR bildirmişti).**
  - CI-11 instance biçimi `<path>:yok:GetAllStrings` pakette yazmıyor (DoR-02 not 3).
  - CI-3 instance'ı ` — ` içeren üç kontrol adında kesiliyor (14 satır). Kesilen değer kararlı ve tekil.
  - CI-14 ve CI-15 instance biçimleri (`title-case`, `Purpose-Audience`, …) pakette tanımsız; bugün bu biçimlerde satır yok.
  - CI-14'ün 376 satırı `belirsiz` sahipli; SharedResource için bir sahip kararı gerekiyor.
- **E-7 (bilgi).** Kanıt betiği v04 satır 42'deki yorum hâlâ "paket rev 13" diyor; davranışı etkilemiyor (DoR-02 not 2).
- **E-8 (bilgi).** RAPOR "Worktree status" bölümü kanıt dizinini ve RAPOR'u "commit edilmedi" diye yazıyor. İkisi sonra `880bf4aab` ve `b00880463` ile commit edildi (2026-10-28 13:21 +0300). Prompt'a göre bu commit'leri Control Tower atar. RAPOR yazıldığı an için doğru. Commit'i kimin attığı bu doğrulamada ölçülemedi (Ölçülmeyenler).

## Ölçülmeyenler

- **AC-17b ve AC-20 (CI koşusu, workflow'daki seçici çağrısı):** `.github/workflows/ui-compliance.yml` yok; CT-Setup ve sahip onayı bekleniyor.
- **Linux'ta koşu:** bütün koşular macOS'ta yapıldı. Seçici yolları git'ten okuduğu için harf duyarlılığı Linux'ta aynı olmalı; depo dışı deneme B bunu git düzeyinde gösterdi. Bu bir çıkarımdır, CI makinesinde ölçülmedi.
- **Commit yazarı:** `880bf4aab` ve `b00880463`'ü işçinin mi Control Tower'ın mı attığı ölçülemedi; üç commit de aynı git kimliğini taşıyor.
- **RAPOR.md'nin değişip değişmediği:** işçinin yazdığı RAPOR.md'nin commit'e kadar değişip değişmediği ölçülemedi; commit öncesi kopyası yok.
- **E-4 senaryosu:** koddan çıkarıldı, koşturulmadı.
- **CI-3 doğrulayıcısının varsayılan dışı profilleri:** `--api-profile` / `--data-mode` ile koşturulmadı; paket varsayılanları istiyor.
- **Yöntem notu:** kanıt betiği koşusunun stdout ve stderr dosyaları, `mktemp -d` dizininin kendisi yerine onun yanına yazıldı (`/var/folders/…/T/tmp.bdrJaCxHlt.stdout`, `.stderr`). İkisi de sistem geçici dizininde, depo dışında. Betiğin kendi geçici klonu `mktemp -d` altında kaldı (betik silmiyor).

## Sonuç

UYGUN (AC-17b ve AC-20 workflow bekliyor)
