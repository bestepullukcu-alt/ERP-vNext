# MOD-0286 — UIC ölçüm kaydı dizini (`mod-0286-uic-baseline-01`)

**TASLAK / hazırlık.** Kaynak paket: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` revizyon 06 (sha256 `7f6868d9`); kontrol listesi: `docs/reference/modules/platform/ui-design-system-page-pattern-governance/compliance-checklist.md`. Çelişkide paket kazanır. Bu dizin, DL-2'nin otomatik maddelerinin (UIC-1..7, 13, 14, 17, 19..22, 24..27, 29..32; RC-1…RC-24) kanıt araçlarını ve resx baseline'ını tutar; ürün kodu içermez (`services/`, `frontend/`, `gateway/` dokunulmaz).

## Dosyalar

| Dosya | Ne | Argümanlar |
|---|---|---|
| `uic-auto.sh` | Koşu sözleşmesinin 24 bloğu sırayla; her blok `RC-n` … `exit=<son komutun durumu>` basar. `set -u`, `set -e` yok. Yalnız `$OUT` altına yazar (RC-11 `navkeys.out`; RC-22 `resx-tenant.out`, `resx-platform.out`; RC-23 `steps/titles/seqdiff/titlediff-<dil>.txt`; RC-24 `keys/kv-<dil>.txt`, `missing/equal/new-*/closed-*-<dil>.txt`). `$B` (baseline) yalnız okunur. | `$1` = koşu dizini (`OUT`) |
| `uic-resx-baseline.sh` | RC-24'ün küme çıkarımı (aynı `grep` / `awk` / `sort` / `comm` / `join` satırları, karşılaştırma yok): `missing-<dil>.txt`, `equal-<dil>.txt` (tr fr es zh ar ru, 12 dosya). Ara dosyalar (`keys-*`, `kv-*`) `mktemp -d` dizininde kurulur ve silinir; RC-24 onları kendi koşu dizininde yeniden üretir. | `$1` = baseline dizini (`OUT_BASE`) |
| `uic-sabotage.sh` | AC-13: depo biçiminde kopya kurar (`frontend/Diten.Web/{Views,Models,wwwroot/assets/js,wwwroot/assets/css}` + `DEV-0000-golden-reference-slim.md`), doğrulayıcıyı kopya kökünde koşar (`baseline.txt`), kopyada `_IndexL10n.cshtml` ve `index.l10n.js` dosyalarını siler, yeniden koşar (`sabotage.txt`); `[PASS]` / `[FAIL]` sayılarını ve iki adlı kırmızıyı basar. Scratch yolu mutlak, depo dışında ve henüz yok olmalı; aksi halde `exit 2` ile reddeder (kopya depoya ya da var olan bir dizine yazılmaz). | `$1` = scratch (mutlak, depo dışı, yok), `$2` = koşu dizini (`OUT`) |
| `resx-baseline/` | `uic-resx-baseline.sh` çıktısı: 12 dosya, HEAD `005a0cc58` resx dosyalarından. Koşular yeniden yazmaz; borcu bilerek kapatan bir revizyon yeni adla yeni baseline dizini commit'ler. | — |
| `auto-01/`, `sabotage-01/` | `run_record.py` koşu dizinleri (henüz yok; aşağıdaki sıra). | — |
| `manual.md` | Elle maddeler (UIC-8..12, 15, 16, 18, 23, 28), AC-6 biçimi (henüz yok). | — |

## Koşu sırası (paket "Kanıt toplama akışı")

`run_record.py` kirli ağacı (`:121-123`, izlenmeyen dosyalar dahil) ve koşu dizini dışında kalan değişikliği (`:137-139`) reddeder; `--out` var olmamalı ve HEAD'de izlenen dosya içermemelidir (`:117-120`). Bu yüzden:

1. **Önce commit:** üç betik + `resx-baseline/` (+ bu README ve `compliance-checklist.md`) commit edilir. Baseline, ilk `auto-01` koşusundan **önce** ve aynı commit'te durur; RC-24 ilk koşuda yapısı gereği `new_missing=0 new_equal=0` verir ve baseline commit'inden sonra eklenen ilk anahtarla ısırmaya başlar (F-16).
2. **Tek koşu, bütün otomatik maddeler** (depo kökünden):
   `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01 -- sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-auto.sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/auto-01`
   Koşunun kendi çıkışı son bloğun çıkışıdır ve yalnız kayıt içindir; kabul, `auto-01/stdout.txt` içindeki `RC-n` / `exit=` çiftlerinden paketin "beklenen" sütununa göre satır satır okunur. Baseline FU-4 ile tasarım gereği kırmızıdır.
3. `auto-01` commit edilir; `run-record-v1.json` içindeki commit, kabulün bağlandığı kod sürümüdür.
4. **Sabotaj koşusu** (3'ten sonra):
   `python3 scripts/run_record.py run --out docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-01 -- sh docs/records/audits/2026-10/mod-0286-uic-baseline-01/uic-sabotage.sh <mutlak scratch yolu, depo dışı, yok> docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-01`
   ve commit.
5. `python3 scripts/run_record.py verify <dizin>/run-record-v1.json` iki koşu için de 0; `manual.md` elle yazılır (AC-6).

## Kuru koşu özeti (2026-10-23, çalışma ağacı HEAD `005a0cc58`, ağaç kirli: bu dizinin dosyaları izlenmiyor — `run_record.py` koşulmadı, betikler doğrudan `sh` ile scratch'e koşuldu)

- `uic-resx-baseline.sh` → scratch ve `resx-baseline/` (bayt bayt aynı, `cmp`): `tr missing=0 equal=10`, `fr 35/47`, `es 35/39`, `zh 35/35`, `ar 35/35`, `ru 35/35`; `missing-fr` = `missing-ru` (`cmp`). Paketin rev 05 ölçümüyle aynı. Geçici dizin geride kalmadı.
- `uic-auto.sh` → stdout 293 satır, stderr boş, betik çıkışı 0 (son blok RC-24). Beklenen sütunla **fark yok**; bilinen kırmızılar paketteki gibi:

| RC | `exit=` | Gözlenen / beklenen |
|---|---|---|
| RC-1 | 0 | stdout boş ✓ |
| RC-2 | 1 | `1`, `0` ✓ (çıkış 1 = `grep -c` 0 bastı) |
| RC-3 | 1 | 97 `[PASS]` / 1 `[FAIL]` (FU-4 `personalization-client.js`) ✓ bilinen kırmızı |
| RC-4 | 0 | `0`, `1` ✓ |
| RC-5 | 0 | `0`, `1` ✓ |
| RC-6 | 0 | 7 × (`ok`, `6`, `0`, `12`), `marker ok`, `en ok`, `tr ok` ✓ (`0` = fixture'da `AddNewGoldenReferenceSlim` yok, FU-12, paket ölçümüyle aynı) |
| RC-7 | 1 | boş, `0` ✓ |
| RC-8 | 0 | boş, `2`, `1` ✓ |
| RC-9 | 0 | `en.json:2`, `tr.json:2` ✓ |
| RC-10 | 1 | boş ✓ |
| RC-11 | 0 | `rc=1`, `0`, `0`, `1` ✓; `navkeys.out` 3 satır (MOD-0176:645, MOD-0177:915, "14 key(s) off form, 12 known, 2 new") — bilinen `rc=1`. Not: BSD `wc -l` sayıyı önde boşlukla basar (`       1`) |
| RC-12 | 1 | gaps biçimi, tek numaralı satır (FU-4) ✓ (`[PASS]` satırları gaps biçiminde basılmaz; paketin "report form shows" notu rapor biçimine aittir) |
| RC-13 | 0 | `1`, `0`, `2` ✓ |
| RC-14 | 1 | tek numaralı gap satırı (FU-4) ✓ |
| RC-15 | 0 | `6`, `16:form_field_count: 6` ✓ |
| RC-16 | 0 | `10`, `5` ✓ |
| RC-17 | 0 | `1`, `1`, attr satırı (`title`, `'aria-controls'`, `'aria-expanded'`, ayrıca `'data-bs-toggle': 'tooltip'`; `aria-label` yok, FU-10), `2` ✓ |
| RC-18 | 0 | `_CreateEditOffcanvas.cshtml:88: … data-dt-primary="enter"` ✓ |
| RC-19 | 1 | `1`, `0` ✓ |
| RC-20 | 1 | `0` ✓ |
| RC-21 | 0 | 7 × `2` ✓ |
| RC-22 | 1 | tenant `rc=1`, `0`, `6`, `5`; platform `rc=1`, `0`, `1`, `0` ✓ bilinen kırmızı (paylaşılan resx borcu); `.out` dosyaları dolu |
| RC-23 | 0 | `1`, katalog satırı `:20`, `codes=USERROLES ROLES`, 7 × (`USERROLES=1`, `ROLES=1`, `related_total=2`, `k=5 range_ok=yes`, `seq_diff=empty`, `title_diff=empty`) ✓ |
| RC-24 | 0 | 6 dil, `new_missing=0 new_equal=0 closed_missing=0 closed_equal=0`; küme boyutları baseline ile aynı ✓ |

- `uic-sabotage.sh` → kopya `exit=0`; baseline `exit=1`, **97 `[PASS]` / 1 `[FAIL]`**; silme `exit=0`; sabotaj `exit=1`, **7 `[PASS]` / 2 `[FAIL]`**; adlı kırmızılar `[FAIL] _IndexL10n.cshtml exists` = 1, `[FAIL] index.l10n.js exists` = 1. AC-13 ile aynı. Reddetme yolları ölçüldü: var olan scratch, göreli yol, depo içi yol → `exit 2`, hiçbir şey yazılmadı.
- Betik blokları paketin hücreleriyle gidiş-dönüş karşılaştırıldı: 23/23 blok birebir (tırnak dışındaki `\|` → `|`, tırnak içindeki `\|` grep BRE seçeneği olarak korundu); RC-14 paketin düzyazı hücresinden ("same command as RC-12 without `--data-mode`") açık komuta yazıldı.

## Sınırlar

- AC-13: kayıttaki argv mutlak bir scratch yolu taşır; yeniden üretim, kopyayı yukarıdaki dosya listesinden yeniden kurmayı gerektirir. Betik var olan bir yolu reddettiği için her koşu yeni bir scratch adı ister.
- `uic-resx-baseline.sh`, paketin 1. adım metninin aksine `keys-*` / `kv-*` ara dosyalarını baseline dizinine yazmaz (12 dosya commit edilir); RC-24 bu ara dosyaları `$OUT` altında yeniden üretir ve baseline'dan yalnız `missing-*` / `equal-*` okur.
- RC-24: çok satırlı bir `<data>` değeri `awk` ile boş okunur (bugün yok); değerler XML kaçışlı haliyle karşılaştırılır; `LC_ALL=C` sıralamayı sabitler.
- Kuru koşular `run_record.py` ile değil doğrudan `sh` ile yapıldı; `run-record-v1.json` yok, kabul bağlaması yok. `manual.md` yazılmadı.
- `uic-auto.sh` ve `uic-sabotage.sh` depo kökünden koşulur (göreli yollar); doğrulayıcı kopya kökünü argümanından kurar (`verify_datatable_page.py:937-949`).
