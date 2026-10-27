# PSS-UI-P1-03 — rapor 01 (MOD-0286 DL-1 + DL-2 son ölçüm)

Hazırlayan: documentation-writer (hedef ajan). Bu rapor Control Tower'ın doğrulamasına ve tek kabul kaydına girdidir; kabul değildir (K13). Kanıt düzeyi E1 (durağan ölçüm) + run_record kaydı; canlı uygulama, tarayıcı, sunucu yok.

Kısaltma: `B` = `docs/records/audits/2026-10/mod-0286-uic-baseline-01`; `S02` = `B/auto-02/stdout.txt`.

## Agent Verdict

Tamamlandı, durma koşulu oluşmadı. Adım 1 taban koşusu Control Tower'ın kayıtlı kırmızı tabanıyla aynı küme (`zorunlu geçti 12 / 17`). Belge açıkları kapatıldı, tek commit `51a8ced4c7c7ce3c412c3dcb3ad17dd937a913fe`. Kayıtlı koşu `auto-02`: `zorunlu geçti 17 / 17`, ham çıkış 0, `run_record.py verify` çıkış 0. Bu bir PASS iddiası değildir; doğrulama başka bir ajanın, kabul Control Tower'ındır.

## Branch / HEAD

- Dal `feature/pss/pss-ui-p1-03`; başlangıç HEAD `8b9a45ba9b9b9e6d74cdf48467678d080708ac44` (Expected HEAD ile aynı); bitiş HEAD `51a8ced4c7c7ce3c412c3dcb3ad17dd937a913fe`.
- `git log --oneline 8b9a45ba9..HEAD`: tam bir commit (`51a8ced4c`).
- Paket sha256 `89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44` (başta ve CT-2 ile: tutuyor); paket değiştirilmedi.
- Prompt dosyası sha256 `e5d598591c99aedc174971c4e30599d984515daa66f6c375f4d9476b402d1930` (başta ölçüldü: tutuyor).

## Worktree status

Başlangıçta `git status --porcelain --untracked-files=all` boş. Commit sonrası boş. `auto-02` koşusundan sonra yalnız `B/auto-02/` (177 dosya: `run-record-v1.json` + 176 artefakt); bu rapor yazıldıktan sonra ek olarak `docs/records/audits/2026-10/pss-ui-p1-03-rapor-01/RAPOR.md`. İkisi de commit'li değildir. `git diff --check` temiz (çıkış 0).

## Changed files

Commit `51a8ced4c7c7ce3c412c3dcb3ad17dd937a913fe` (3 dosya, +79 / -23):

- `docs/reference/modules/platform/ui-design-system-page-pattern-governance/spec.md`
- `docs/reference/modules/platform/ui-design-system-page-pattern-governance/compliance-checklist.md`
- `docs/records/audits/2026-10/mod-0286-uic-baseline-01/README-p1-03.md` (yeni dosya)

Commit'siz bırakılanlar: `B/auto-02/`, `docs/records/audits/2026-10/pss-ui-p1-03-rapor-01/RAPOR.md`.

## Golden/Contract flow — Adım 1 taban koşusu (değişiklik yapmadan)

Komut: `sh B/uic-p1-03-02.sh <mktemp -d>` (çıktı dizini depo dışı). Koşudan sonra `git status --porcelain --untracked-files=all` boştu. Çıkış 1, `BASE=8b9a45ba9b9b9e6d74cdf48467678d080708ac44`.

- `sonuç=kaldı` kümesi: **AC-3, AC-4, AC-15, CT-1, CT-3**. Kalan 12 zorunlu kimlik (AC-1, AC-2, AC-12, AC-5, AC-7, AC-6, AC-8, AC-9, AC-10, AC-11, AC-16, CT-2) `geçti`; AC-13 (isteğe bağlı) `geçti`.
- `zorunlu geçti 12 / 17`.
- Kayıtlı tabanla (`B/p1-03-taban-02/stdout.txt`) `diff`: yalnız beklenen farklar. RC-3 / RC-12 / RC-14 bloklarında mutlak worktree yolu (`pss-ui-ct-01` → `pss-ui-p1-03`), `BASE=` satırı (boş → `8b9a45ba9…`), AC-2 satırlarından `c01b7f2a2` satırı (taban koşusunda BASE yoktu), CT-3 ayrıntısı (`BASE..HEAD commit = 0`, iki `EKSİK` satırı). `sonuç=` kümesi aynı.

Düzenlemeden sonra, commit'ten önce öz denetim (depo dışı yeni `mktemp -d`): çıkış 1, **`zorunlu geçti 16 / 17`**, tek `sonuç=kaldı` satırı **CT-3** (`BASE..HEAD commit = 0`, henüz commit yok). Ağaç yalnız üç belge değişikliğini gösteriyordu.

## Adım 2 maddeleri (a-i), satır kanıtıyla

Satır numaraları commit `51a8ced4c` ağacındadır (`spec.md` = SP, `compliance-checklist.md` = CL).

- **a. AC-15 kapsama tablosu — yapıldı.** `CL:81` `## Kapsama tablosu` (tek). Paket satır 284-286 düzyazısı, 288-300 tablo (başlık + ayraç + 11 satır), 302 toplam cümlesi (`CL:103` "Total 94 phrases: 57 covered, 6 meta, 31 not covered …") paketten aynen kopyalandı; satırlar `| UIC-` / `| RC-` ile başlamaz. Yeniden ölçüm `CL:105`: `grep -n -i "zorunlu" .antigravity/rules/<dosya>.md | wc -l` = frontend-standards 15, frontend-datatable-template 23, frontend-js-standard 31, premium-modal-standard 1, page-help-and-keyboard 8, views-organization 2, unauthorized-surface-standard 1, localization-standard 7, dynamic-localization-standard 2, frontend-details-template 2, frontend-form-template 2 (toplam 94). Her sayı paketteki N ile aynı; **fark yok, tablo değiştirilmedi, DUR gerekmedi**. 31 kapsanmayan ifade FU-11 girdisi olarak kaldı (`CL:107`). Önceki ölçüm: `grep -c -i kapsama CL` = 0; şimdi 3.
- **b. Künye ve UIC/RC bayt eşlemesi — yapıldı.** `SP:3` künye: "revizyon 12 · sha256 `89333f90` (PR #245, merge `c51568aa8`; approvals.tsv MOD-0286 rev 12 satırı, risk HIGH)"; `SP:5` durum: "paket rev 12 main'de onaylıdır, bu belge main'dedir; kabul PSS-UI-P1-03 ile bekliyor (kabul kaydı Control Tower'ındır …)"; `CL:3` aynı durum; `CL:4` "hücre metni değiştirilmedi" iddiası, doğru olan cümleyle ("bayt bayt aynıdır … `diff` boştur") değiştirildi. "Kabul edildi" yazılmadı. Doğrulama: `approvals.tsv:11` MOD-0286 `89333f90…` `approve` `HIGH`; `c51568aa8` "Merge pull request #245" (ikisi de bu ağaçta okundu). Fark veren dört satır paketten kopyalandı: **UIC-4, UIC-11, UIC-13, UIC-15** (`CL:18, :25, :27, :29`; UIC-15'te kaçışlı `\|`). Sonuç: AC-4 `UIC satırları paketle aynı`, `RC satırları paketle aynı`, 7 `|` olmayan UIC satırı 0 (`S02:342` `AC-4 sonuç=geçti`).
- **c. README için yeni dosya — yapıldı.** `B/README-p1-03.md` (yeni). Kayıtların bugünkü durumu tablosu (auto-01 koşusu `a0a4050b0`'a bağlı, kaydı `3fbf891bb`; sabotage-01 koşusu `3fbf891bb`'ye bağlı, kaydı `bb3464954`; manual.md `d3105a332`; auto-02 bu işin koşusu — bağlı commit'i commit sha'sını kendi içine yazamayacağı için `run-record-v1.json` `commit` alanına gönderir; bu alan `51a8ced4c7c7…`). `README.md`'nin eskiyen satırları: 3, 13, 14, 16-28, 30, 70. Commit'li `README.md`, `manual.md`, betikler, `resx-baseline/`, `auto-01/`, `sabotage-01/` düzenlenmedi. Bağlı commit'ler `run-record-v1.json`'dan okundu: auto-01 `a0a4050b0212…`, sabotage-01 `3fbf891bbdce…`.
- **d. §9 çatışma kaydı — yapıldı.** `SP:189-191` durum hücresi: "karar: kuralın kendisinde düzeltilir (sahip 2026-10-26, karar kaydı pss-mod0286-daraltma-01 madde 3; kural dosyası düzeltilene dek UIC istisnayı kayda geçirir) (D-8a|b|c)"; "çözüldü" yazılmadı. `SP:192` D-5 satırı "karar bekliyor (D-5, FU-2)" kaldı. `SP:185` `paket:512-520` → `paket:769-777`. Dört zorunlu satır ve yedi sütun: AC-3 `satır 1-4 '|' sayısı (7 sütun) = 8 (beklenen 8)` (kaçışsız `|` sayısı 8 = 7 sütun) ve satır 5; `S02:333` `AC-3 sonuç=geçti`. Yeni çatışma bulunmadı, satır eklenmedi/silinmedi.
- **e. AC-14 satır sayıları — gerekmedi (ölçüm paketle aynı).** `wc -l` (frontend/Diten.Web altında, commit ağacında): `_GlobalConfirmation.cshtml` 479, `_GlobalNotification.cshtml` 269, `_ListShell.cshtml` 58, `_BulkActionBar.cshtml` 30, `_TableSkeleton.cshtml` 51, `diten-datatable.js` 1414, `dt-defaults.js` 1603, `premium-modal.js` 126, `diten-dialog.js` 128, `diten-help.js` 138, `diten-shortcuts.js` 283, `diten-tour.js` 149, `diten-tour-catalog.js` 41, `diten-enter-primary.js` 207, `diten-datefield.js` 463, `diten-unauthorized.js` 52, `personalization-client.js` 193 — `SP:54-71` tablosuyla birebir aynı. Aynı ölçümde `_LayoutPlatformAdmin` 448, `_LayoutTenantShell` 703, `_Layout` 606, `Platform/_ViewStart` 3, `PPM/Shared/_ListShell` 41, `Wizard.cshtml` 438, `wizard.js` 557, `backbone-custom.css` 8622 de §3 / §1 / §4 değerleriyle aynı. Tablo değişmedi; `SP:5` durum satırına yeniden ölçüm notu eklendi. AC-14 isteğe bağlıdır ve betikte yoktur; kanıt bu komutlar ve çıktılardır.
- **f. Paket atıfları ve P2 — yapıldı.** `SP:9` `paket:147-178` → `paket:156-200`; `SP:185` → `paket:769-777`. Belgelerde başka `paket:` atfı yok (CT-1: atıflar tam olarak `paket:156-200` ve `paket:769-777`). `P2` geçen her satır `D-4` taşır: `SP:137`, `:160`, `:162`, `:205`, `:211`, `:213` düzeltildi (ör. `SP:211` "sahip (ayrı P2 paketi, D-4)"; `SP:213` "ayrı P2 paketinde yeniden sorulur (P2 bu paketten çıktı, D-4; D-7)"). `S02:450` `CT-1 sonuç=geçti`.
- **g. DL-1 kısa dizin bulgusu — yapıldı (bulgu: sıfır).** Gövdede tablo/başlık/künye dışında kalan satırlar tarandı (`.md`, `:<satır>` ya da `§` içermeyen satırlar): 20 satır, hepsi "Kontrol listesine bağ", envanter/ölçüm özeti, "Ölçülmedi", Nav anahtarları / P2 notu ve sınırlar satırları (`SP:38, 46, 73, 75, 88, 108, 118, 120, 135, 137, 139, 152, 162, 164, 181, 197, 218-220, 225`). Kural metnini kaynağı olmadan yeniden anlatan paragraf yok; kısaltma yapılmadı, kural anlamı değiştirilmedi. Sınır: tarama desen tabanlıdır, anlam taraması değildir.
- **h. Öteki eskimiş satırlar — yapıldı.** `SP:116` ("dört satır", "karar gelene dek" yerine): "… (4 şablon × 2 satır: halka `div` + ikon `i` = sekiz satır) → §9 satır 1 (D-8a: karar: kuralın kendisinde düzeltilir); kural dosyası düzeltilene dek DL-2 bu sekiz satırı … tolere edilen tek `style=` sayar" — sekiz satır `premium-modal-standard.md:27-28, 35-36, 43-44, 51-52` okunarak doğrulandı (her şablonda `div` + `i`, paket UIC-13 "eight prescribed lines" ile aynı). `SP:137, :160, :162` P2 → D-4 (f maddesiyle). `SP:190` (D-8b): rev 06 notu ve var olmayan `FU-17` atfı kaldırıldı; yerine "anti-pattern tablosu `:981-991` ikisini de adlandırmaz (paket D-8b)". `SP:192` (D-5): `.antigravity/ARCHITECTURE.md:234` ve `.antigravity/workflows/debug.md:59` eklendi (ikisi de okundu: `MOD-0013` geçiyor). `SP:221` (eski `SP:218-221` bloğu): "Bu taslak … `status: draft` … ayrı iş paketleridir" yerine bugünkü durum: paket rev 12 `ready-for-dev`, onaylı (PR #245 merge `c51568aa8`), DL-1 ve DL-2 tek iş paketi PSS-UI-P1-03 (karar kaydı `pss-mod0286-kabul-tek-01`), DL-3 ayrı iş paketi. §9 kural-kural satırlarının tarafları paket satır 773-776 ile karşılaştırıldı: satır 1-3 aynı, satır 4 iki taraf eksikti (eklendi); AC-3 betik kontrolü geçti.
- **i. Başka açıklar.** Belge ile kapananlar: yok (a-h dışında yeni açık çıkmadı). Belge ile kapanmayanlar (kapatılmadı, aşağıda "Known gaps" ve "paket revizyonu girdileri"): AGENTS.md etiket satırı kayması (pakette `:139`, ölçüm `:140`) ve RC-11 beklenen sütunu eskimesi.

## Sub-flows — AC tablosu (AC-1…AC-16)

Taban = Expected HEAD `8b9a45ba9` (bu işin Adım 1 koşusu, kayıtlı `p1-03-taban-02` ile aynı küme); son = `auto-02` (`run-record-v1.json` commit `51a8ced4c7c7…`). Kanıt = `S02:<satır>` (`AC-n sonuç=` satırı); ayrıntı satırları aynı bloğun üstündedir. Kimlik zorunluluğu betiğin `ZORUNLU` listesinden.

| AC | zorunlu / isteğe bağlı | taban | son | kanıt |
|---|---|---|---|---|
| AC-1 | zorunlu | geçti | geçti | `B/auto-02/stdout.txt:310` (9 başlık, atıfsız başlık 0, `.antigravity/rules/` satırı >= 9) |
| AC-2 | zorunlu | geçti | geçti | `S02:317` (a0a4050b0, 3fbf891bb, bb3464954, d3105a332 ve `BASE..HEAD` commit'i `51a8ced4c`: değişen 0) |
| AC-3 | zorunlu | kaldı | geçti | `S02:333` (§9 satır 1-5, 7 sütun; satır 1-3 `karar: kuralın kendisinde düzeltilir`; satır 4 `karar bekliyor`; taraflar paket 773-776) |
| AC-4 | zorunlu | kaldı | geçti | `S02:342` (UIC 32, RC 24, tekrar 0/0, iki tablo paketle aynı, 7 sütun) |
| AC-5 | zorunlu | geçti | geçti | `S02:380` (RC-1…RC-24 birer başlık, 24 `exit=`, bloklar paketin beklenen sütununa karşı) |
| AC-6 | zorunlu | geçti | geçti | `S02:396` (manual.md: 10 madde, sonuç hücreleri uygun / uygun değil / kısmi / ölçülmedi) |
| AC-7 | zorunlu | geçti | geçti | `S02:384` (RC-3 `[FAIL] personalizationClient sends tenant header only for tenant users` ve `exit=1`) |
| AC-8 | zorunlu | geçti | geçti | `S02:407` (`rc=0`, could not read 0, MOD-0286 0, git ls-files 1) |
| AC-9 | zorunlu | geçti | geçti | `S02:411` (`verify_module_id.py --check-id MOD-0286`, exit 0) |
| AC-10 | zorunlu | geçti | geçti | `S02:414` (`docs/` kökünde dosya 0) |
| AC-11 | zorunlu | geçti | geçti | `S02:418` (`Views/Platform/UiGovernance` ve `Features/UiGovernance` yok) |
| AC-12 | zorunlu | geçti | geçti | `S02:320` (`module-pack-standard.md` değişmedi, FU-1 satırı 1, bağlayıcı cümle belgelerde yok) |
| AC-13 | isteğe bağlı | geçti | geçti | `S02:422` (sabotage-01 verify çıkış 0, iki adlı `[FAIL]`) |
| AC-14 | isteğe bağlı | betikte yok | komutlarla ölçüldü | `wc -l` 17 dosya, yukarıda e maddesi; `inceleme bekler` değil, ölçüldü |
| AC-15 | zorunlu | kaldı | geçti | `S02:438` (`## Kapsama tablosu` tek, 11 satır paket 290-300 ile aynı, toplam cümlesi var, 11 `zorunlu` sayısı N ile aynı) |
| AC-16 | zorunlu | geçti | geçti | `S02:440` (22 otomatik UIC kimliği RC satırlarının "UIC kapsamı" sütununda) |

Betiğin ek kimlikleri: CT-1 (taban kaldı, son geçti, `S02:450`), CT-2 (geçti / geçti, `S02:453`), CT-3 (taban kaldı, son geçti, `S02:460`: `BASE..HEAD commit = 1`, değişen dosya yalnız `spec.md` ve `compliance-checklist.md` ile isteğe bağlı `README-p1-03.md`).

Kimlik özeti: 17 zorunlu kimlik (AC-1…AC-12, AC-15, AC-16, CT-1…CT-3) hepsi tam bir kez `geçti`: `S02:480` **`zorunlu geçti 17 / 17`**.

## Tests — auto-02 kayıtlı koşusu

Komut (temiz ağaçta, commit sonrası): `python3 scripts/run_record.py run --out B/auto-02 -- sh B/uic-p1-03-02.sh B/auto-02`.

- Koşu çıktısı: `KOŞU KAYDI B/auto-02/run-record-v1.json · commit 51a8ced4c7c7 · komutun çıkışı 0 · 176 artefakt`. "Hiçbir şey stage ya da commit edilmedi."
- Kayıttaki commit: `51a8ced4c7c7ce3c412c3dcb3ad17dd937a913fe` = Adım 3 commit'i. Ham (toplam) çıkış **0**.
- `python3 scripts/run_record.py verify B/auto-02/run-record-v1.json`: `KOŞU KAYDI biçimi geçerli · commit 51a8ced4c7c7 · çıkış 0`; verify çıkışı **0**.
- `S02:295` `uic-auto exit=0` (kayıt içindir, kabul değildir; son blok RC-24); `S02:296` `BASE=8b9a45ba9b9b9e6d74cdf48467678d080708ac44`; `S02:480` `zorunlu geçti 17 / 17`. `stderr.txt` boş (0 bayt).
- RC başına `exit=` (S02/`uic-auto.out`; auto-01 ile aynı dizilim): RC-1 0, RC-2 1, RC-3 1, RC-4 0, RC-5 0, RC-6 0, RC-7 1, RC-8 0, RC-9 0, RC-10 1, RC-11 0, RC-12 1, RC-13 0, RC-14 1, RC-15 0, RC-16 0, RC-17 0, RC-18 0, RC-19 1, RC-20 1, RC-21 0, RC-22 1, RC-23 0, RC-24 0. Beklenenle uyum (AC-5, `S02:380` geçti): `exit=1` olanlardan RC-2, RC-7, RC-10, RC-19, RC-20 `grep -c`/`grep -n` eşleşmesiz çıkışıdır ve beklenen stdout ile uyumludur; RC-3 / RC-12 / RC-14 FU-4 kırmızısıdır (bilinen kırmızı); RC-22 checker `rc=1`dir (bilinen kırmızı).

### Adım 5 — auto-01 `stdout.txt` ile auto-02 `uic-auto.out` farkı

`diff B/auto-01/stdout.txt B/auto-02/uic-auto.out`: fark RC satırlarıyla:

1. **RC-3** (satır 8-9, `[shell]` ve `[factory]` satırları), **RC-3** (satır 53, FU-4 ayrıntısı), **RC-12** (satır 180-181, 183) ve **RC-14** (satır 191-192, 194): yalnız mutlak worktree yolu (`…/worktrees/pss-ui-ct-01/…` → `…/worktrees/pss-ui-p1-03/…`). Sayılar ve `[PASS]` / `[FAIL]` aynı.
2. **RC-11** (satır 174): `rc=1` → **`rc=0`**. `navkeys.out` bugün tek satır: `pack nav keys: 12 key(s) off form, 12 known, 0 new` (auto-01'de MOD-0176:645 ve MOD-0177:915 satırları vardı). AC-8 (1) `rc=0`'ı kabul eder; paketin RC-11 beklenen sütunu `rc=1` der (paket revizyonu girdisi, aşağıda). Diğer üç değer (`0`, `0`, `1`) aynı.

Başka RC satırında fark yok; RC-6, RC-22, RC-24 vb. auto-01 ile aynıdır.

### Adım 6 — mevcut kayıtlar

- `run_record.py verify B/auto-01/run-record-v1.json`: `KOŞU KAYDI biçimi geçerli · commit a0a4050b0212 · çıkış 0` — çıkış 0.
- `run_record.py verify B/sabotage-01/run-record-v1.json`: `… commit 3fbf891bbdce · çıkış 0` — çıkış 0. Bağlı olduğu commit `3fbf891bbdceef89032097bd80898c1d391d21e4`. `B/sabotage-01/stdout.txt`: baseline `PASS=97 FAIL=1`, sabotaj `PASS=7 FAIL=2`, adlı kırmızılar `[FAIL] _IndexL10n.cshtml exists` ve `[FAIL] index.l10n.js exists`.
- sabotage-02 yapılmadı (auto-02 commit'siz, ağaç temiz değil; gerekliliği Control Tower'ın kararıdır, AC-13 isteğe bağlı).

## RC kanıtı — paket iki `paket:` çapası (DOĞRULA 4)

`sed -n '156,160p;769,777p'` paket satırları (rev 12): 156 "### DL-2 UI Compliance Checklist (draft items)" başlığı ve 158-160 sütun/otomatik-elle düzyazısı; DL-2 tablosu 163-200 aralığındadır, `paket:156-200` DL-2 bölümüne atıf yapar. 769 "Çatışma kaydı (Conflicts register, seeds the spec's §9)" satırı, 771-772 tablo başlığı, 773-776 dört kural-kural satırı (D-8a, D-8b, D-8c, D-5), 777 "rule vs spec" satırı: `paket:769-777` Çatışma kaydı'na atıf yapar.

## Failure paths

- Durma koşulu oluşmadı: HEAD / dal / kirli ağaç başta doğru; prompt ve paket sha256 tuttu; Adım 1 kümesi kayıtlı tabanla aynı; kapatılacak açık paket, commit'li kayıt, `.antigravity` / `services` / `frontend` / `gateway` / `execution` / `scripts` dosyası gerektirmedi (a'daki sayı ölçümü paketle aynı çıktı); run_record reddetmedi; ham çıkış 0; auto-02 RC farkları yalnız yol ve RC-11 (aşağıda).
- Öz denetim Adım 3'ten önce `16 / 17` verdi (tek `kaldı` CT-3, beklenen).

## Bilinen kırmızılar (ölçüldüğü gibi, gizlenmedi, düzeltilmedi)

- **FU-4 (RC-3, RC-12, RC-14 `exit=1`).** Tek `[FAIL] personalizationClient sends tenant header only for tenant users` (`wwwroot/assets/js/personalization-client.js`, Golden Slim). RC-3: 97 `[PASS]` / 1 `[FAIL]`. RC-12 ve RC-14: tek numaralı gap satırı `1) personalizationClient sends tenant header only for tenant users`.
- **RC-11.** Bugün `rc=0` (auto-01'de `rc=1`: MOD-0176:645, MOD-0177:915, FU-9). Bu iki satır `navkeys.out`ta artık yok (`12 key(s) off form, 12 known, 0 new`). Kırmızı kapanmış görünüyor ama bu işte nedeni araştırılmadı (başka modüllerin paketleri, XMC-001); kayıt ölçüldüğü gibi yazıldı. Paket beklenen sütunu `rc=1` der.
- **RC-22 resx denetçisi `rc=1`** (paylaşılan resx borcu): tenant `rc=1`, `should be Title Case` 0, `still equal to English` 6, `Missing` 5; platform `rc=1`, 0, 1, 0. Sayılar yalnız kayıt içindir; "yeni anahtar yok" kabulü RC-24'tedir: 6 dilde `new_missing=0 new_equal=0 closed_missing=0 closed_equal=0` (tr missing 0 / equal 10; fr 35 / 47; es 35 / 39; zh, ar, ru 35 / 35).
- **RC-6 `AddNewGoldenReferenceSlim` = 0** (FU-12): 7 dilin her birinde `ok`, `6`, **`0`**, `12`; `marker ok`; `en ok`, `tr ok`.
- **FU-10:** RC-17 `diten-datatable.js` `aria-label` sayısı `2` (`:600`, `:1303`), araç çubuğu filtre düğmesi `aria-label` taşımıyor; attr satırı `title: l.Filter, 'aria-controls': collapseId, 'aria-expanded': 'false'`.

## Persistence evidence / Security-RBAC-Tenant evidence / Observability / Migration-Rollback

N/A — P1 yalnız belge dilimidir (sayfa, uç nokta, izin, entity, Gateway yok; AC-2: `.antigravity services frontend gateway execution/registries` farkı boş; AC-11: P2 dizinleri yok).

## Audit/Evidence

- Kanıt: `B/auto-02/` (`run-record-v1.json`, `stdout.txt`, `stderr.txt`, `uic-auto.out`, `sonuc.txt` + artefaktlar); rapor dosyası kayda girmez (Control Tower auto-02'yi tek başına commit'ler, `acceptance_record.py:635-645`).
- Kapsam: elle maddeler (`manual.md`, commit'li, düzenlenmedi): `uygun` 1 (UIC-23), `uygun değil` 2 (UIC-8, UIC-18), `kısmi` 2 (UIC-15, UIC-16), `ölçülmedi` 5.

### Elle ölçülmeyen 5 madde ve kısmi / uygun değil maddeler

| UIC | durum | neden ölçülmedi / ne gerekir (`manual.md` planı) |
|---|---|---|
| UIC-9 | ölçülmedi | RTL (`ar`) çakışmasız render: çalışan uygulama ve tarayıcı gerekir; `?culture=ar` ile liste, filtre, hızlı görünüm, oluştur paneli için 4 ekran görüntüsü, görüntü başına çakışma sayısı 0 |
| UIC-10 | ölçülmedi | Koyu tema okunabilirlik: çalışan uygulama ve tarayıcı; açık + koyu temada 4 görünüm = 8 ekran görüntüsü, okunamayan metin sayısı |
| UIC-11 | ölçülmedi | Odak halkası: gerçek Tab turu; görünüm başına `n/N` (halkası görünen / toplam durak), liste, filtre açık, oluştur paneli açık |
| UIC-12 | ölçülmedi | Kontrast >= 4.5:1: DevTools ile belirteç başına ön plan / arka plan / oran tablosu, iki temada |
| UIC-28 | ölçülmedi | "Her kontrole basıldı": ekrandaki her kontrole basılıp kontrol / beklenen tepki / gözlenen tepki / sonuç listesi; N canlı kesinleşir |
| UIC-15, UIC-16 | kısmi | UIC-15: Enter → Kaydet ve "?" paneli davranışı canlı ister; UIC-16: izni olmayan kullanıcıyla oturum ve ekran görüntüsü ister (UAS-001 §4 deseni sayfada yok, durağan bulgu) |
| UIC-8, UIC-18 | uygun değil | Durağan bulgu: UIC-8 `J/index.js:30-32` metni; UIC-18 Golden Slim paketinde kayıt kodu sınıf satırı yok (Golden Reference bulguları, başka modül, XMC-001) |

Canlı ölçüm planı: `manual.md` "Ölçülmeyenler için canlı ölçüm planı" (sayfa `http://localhost:5001/GoldenReferenceSlim`). Bu işte sunucu, tarayıcı, oturum yok.

## Decisions

- Paket değiştirilmedi; paketle çelişen yerde paket kazandı (UIC/RC satırları paketten kopyalandı, `AGENTS.md:139` atfı pakette olduğu gibi tutuldu ve ölçüm ayrıca yazıldı).
- README için yeni dosya (`README-p1-03.md`), eski `README.md` düzeltilmedi (docs/records kuralı).
- `spec.md` §9 satır 1-3 "çözüldü" yazılmadı; kural dosyası düzeltilmedi.

## Blockers

Yok.

## Known gaps — paket revizyonu girdileri (paket değiştirilmedi)

1. **AGENTS.md etiket satırı:** paket D-5 (satır 63) ve Çatışma kaydı satır 4 (satır 776) `AGENTS.md:139` der; ölçüm (`grep -n MOD-0013 AGENTS.md`): etiket bugün **`AGENTS.md:140`** (satır 139 "DataTable" satırıdır). Betik (Korunan yol) `AGENTS.md:139` ister; spec bu yüzden `:139`'u paket atfı olarak tutar ve `:140` ölçümünü yanına yazar (`SP:192`).
2. **RC-11 beklenen sütunu eskidi:** paket `rc=1` (MOD-0176:645, MOD-0177:915) der; bugün `rc=0`, `navkeys.out` "12 key(s) off form, 12 known, 0 new". AC-8 `rc=0`'ı kabul eder. Paket RC-11 satırı, AC-8 "Measured …" cümlesi ve FU-9 durumu için revizyon girdisi.
3. Paketin Çatışma kaydı satır 4 ve D-5 tarafları 8 satır sayıyor (premium-modal 2, dynamic-localization 3, ARCHITECTURE 1, debug 1, AGENTS 1) ve FU-2 "five files / eight lines" der; sayı doğru, yalnız AGENTS satır numarası kaymış (madde 1).

### D-9 ölçümü (yalnız ölçüldü, değiştirilmedi)

`grep -n 'px-3\|backbone-custom.css' .antigravity/agents/*.md` (6 isabet):
- `.antigravity/agents/frontend-ui-ux.md:66` toolbar `row px-3 …` standardı
- `.antigravity/agents/frontend-ui-ux.md:67` "Shared CSS Standardı" (backbone-custom.css)
- `.antigravity/agents/frontend-ui-ux.md:78` modal / navbar kayması (`backbone-custom.css`)
- `.antigravity/agents/frontend-ui-ux.md:95` Section yönetimi (`backbone-custom.css`)
- `.antigravity/agents/orchestrator.md:25` SweetAlert Sidebar Stability
- `.antigravity/agents/orchestrator.md:49` Inline Filter (ZORUNLU)

Paketin D-9 satırıyla (66-67, 78, 95; orchestrator 25, 49) aynı. D-9 P1 kabulünden sonra sahibin işidir.

## Out-of-scope changes

Yok. İzinli yollar dışında hiçbir dosya değişmedi; commit yalnız üç belge dosyasını içerir.

## Ölçülmeyenler ve nedenleri

- Elle 5 madde (yukarıda) ve kısmi UIC-15 / UIC-16 davranışları: canlı uygulama / tarayıcı gerekir.
- `spec.md` içindeki canlı kod satır çapaları (ör. `diten-datatable.js:961`, `_GlobalConfirmation.cshtml:206`, `:1046`) bu işte yeniden ölçülmedi (yalnız RC'lerin okuduğu `1046` attr satırı ve `dt-save-filter-btn` sayısı betikçe ölçüldü); AC-14 yalnız satır sayısını ister.
- `spec.md`'nin başka sayıları 2026-10-27'de yeniden ölçüldü ve aynı çıktı: Archive dışı `Index.cshtml` 192, `DtDefaults` JS 177, `createList(` 37, `createCrudTable(` 81, `style="` içeren Archive dışı `.cshtml` 93, `_ListShell` tüketen görünüm 41, `_TableSkeleton` 33, `select2` JS 210, `GetAllStrings(true)` 5, `backbone-custom.css` `--diten-`/`--bs-` 1152, `--diten-` 8, koyu tema 19 + 13, RTL 13. Bunlar belgede değiştirilmedi (fark yoktu); belge başlığında yalnız §2 satır sayıları için yeniden ölçüm notu var.
- Belge anlamı (kural metninin doğru özetlendiği): yalnız desen / satır / sayı düzeyinde ölçüldü, anlam taraması bağımsız doğrulayıcıya aittir.
- Sabotaj yeniden koşusu (sabotage-02) yapılmadı (Adım 6).

## Sonda `git status --porcelain --untracked-files=all`

(Bu rapor yazıldığı anda; çıktı sohbet raporunda birebir aktarılır.)

```text
?? docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-02/... (177 dosya)
?? docs/records/audits/2026-10/pss-ui-p1-03-rapor-01/RAPOR.md
```
