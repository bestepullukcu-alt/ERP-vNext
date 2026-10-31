# PSS-UI-P1-05 — yapılandırılmış rapor (SOP §22)

Prompt PSS-UI-P1-05-P v1.1 (sha256 `ee4beffc29a5c2cf7b5586a65a36819e7e0269aa1d53201709c872833722a36a`, işe başlarken doğrulandı). Rol: `devops-agent`, MOD-0286 rev 15, dilim DL-3, AC-21.

**Agent Verdict:** PASS (işçi beyanı). Kapanış değildir: doğrulama başka ajanın, kabul Control Tower'ın tek kabul kaydınındır. CI işi, PR ve kabul bu işte yapılmadı.

**Branch / HEAD:** `feature/pss/pss-ui-p1-05` @ `e3eda1752e3fe74ee8aa4f6f1ab02c992bc7d04b` (taban `5d2774ae973f65797b0d67e75fee611ee83baeef`). Push yok, main birleştirilmedi.

**Worktree status:** `git status --porcelain --untracked-files=all`: kayıtlı koşu bittiğinde 91 yol, hepsi `docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01/` altında ve commit edilmemiş; bu rapor yazıldıktan sonra ek olarak `docs/records/audits/2026-10/pss-ui-p1-05-rapor-01/RAPOR.md`. Başka yol yok.

**Changed files:** (1 commit, `git log --oneline 5d2774ae9..e3eda1752`)

- `e3eda1752 feat(pss): MOD-0286 DL-3 — tek seviye sayfalar: okuyucu, eşleme ve FU-16 satırları (PSS-UI-P1-05)`
- `scripts/uic_changed_pages.py` +262 / -27
- `tests/ui-compliance-baseline.tsv` +134 / -0

Ek düzeltme commit'i gerekmedi (izin üçtü). Paket sha256 başta ve sonda `0c53533e3e2434fbd8abe615f814d037c3c390b06625ee822358871958aed102`.

**Golden/Contract flow:** N/A çalışma zamanı akışı yok; depo aracı ve veri dosyası. Komut sözleşmesi değişmedi: dört kip, çıkış 0 / 1 / 2. Yeni çıktı satırı yalnız `eşlenmemiş sayfa JS'i, sayfa kontrolü yok (FU-18) <yol>`; tek seviye sayfada `<page>` = `{Module}`.

**Sub-flows (dört kip, `e3eda1752`, yük ölçülerek):**

| Kip | Çıkış | Çıktı |
|---|---|---|
| kipsiz | 2 | argparse hatası |
| `--self-test` | 0 | `self-test: 93/93 passed` (eski 67 + yeni 26) |
| `--check-baseline` | 0 | aşağıdaki tablo; `toplam satır=2153 eşleşme=858` |
| `--base HEAD` | 0 | tek satır `süre: 0.2 s` |

`--check-baseline` CI başına satır / eşleşme: CI-1 81 / 0, CI-2 21 / 54, CI-3 1177 / 0, CI-4 78 / 79, CI-5 16 / 16, CI-6 129 / 322, CI-7 16 / 37, CI-8 1 / 1, CI-9 125 / 2, CI-10 109 / 347, CI-11 0 / 0, CI-12 22 / 0, CI-13 0 / 0, CI-14 376 / 0, CI-15 0 / 0, bilgi 2 / 0.

Yeni self-test durumları (26, hepsi PASS; her biri geçici depoda gerçek koşu):

- AC-21g01 a view file directly in the folder -> page {Module}, a new match is YENİ KIRMIZI, not a shared file
- AC-21g01 a non-.cshtml file directly in the folder selects the page and no check reads it
- AC-21g02 JS directly in the folder, a direct view at HEAD and at the base -> page {Module}, YENİ KIRMIZI
- AC-21g02 JS directly in the folder, the direct view only at the base -> the JS still selects page {Module}
- AC-21g03 JS directly in the folder, no direct view at HEAD or at the base -> its FU-18 line, no page
- AC-21g03 JS directly in the folder, the direct view only at the base, a subfolder page stays -> page, not FU-18
- AC-21g04 one folder, a single-level and a two-level page -> each reads only its own files
- AC-21g05 NOT_PAGE_AREAS (Shared, Archive): views and JS are the shared line, never a page, never FU-18, no draft row
- AC-21g06 pages/** -> its FU-18 line for each path, not the shared line, no page
- AC-21g07 js/{Module}/{Sub}/ with no view folder -> its FU-18 line, the single-level page is not selected
- AC-21g08 an area folder holding only subfolders: JS directly in it and in a subfolder with no view -> FU-18 lines
- AC-21g09 a folder-level _ViewStart.cshtml -> a file of page {Module} (its checks run) and the shared-file line
- AC-21g09 a folder-level _ViewImports.cshtml -> a file of page {Module} (its checks run) and the shared-file line
- AC-21g09 the root Views/_ViewStart.cshtml stays shared only
- AC-21g10 baseline rows with a page key {Module} are accepted and matched as TABAN FU-16
- AC-21g11 an empty key -> exit 2
- AC-21g11 a key with a space -> exit 2
- AC-21g11 a three-part key -> exit 2
- AC-21g11 a key in NOT_PAGE_AREAS -> exit 2
- AC-21g11 a CI-3 row with a single-level key -> exit 2
- AC-21g12 a single-level CI-2 row whose instance lies in a subfolder -> exit 2
- AC-21g12 a single-level CI-1 row whose instance lies in a subfolder -> exit 2
- AC-21g13 a single-level new surface in a folder holding only subfolder pages -> CI-11 runs; at a later base it does not
- AC-21g14 a single-level page removed while a subfolder page stays -> 'sayfa kaldırıldı', its rows closed, the subfolder page untouched
- FU-17: a single-level list page -> one 'ölçülmedi' CI-3 line, the verifier not run, never red, no draft row; CI-9 savedView red as YENİ KIRMIZI
- draft: a single-level row has FU-16, the unknown owner (no AREA_OWNER) and the decision's reason; two-level rows unchanged

**Failure paths (sabotaj koşuları, kayıtlı koşunun içinde, klonda; her biri kendi `run-record-v1.json`'u ile, hepsi verify 0):**

| Koşu | Çıkış | Adlı satır |
|---|---|---|
| ac21d1-sabotaj | 1 | `YENİ KIRMIZI Tasks CI-2 Views/Tasks/Create.cshtml 1 +1` |
| ac21d1-geri | 0 | |
| ac21d2-sabotaj | 1 | `YENİ KIRMIZI Recall CI-7 wwwroot/assets/js/Recall/cases.js 1 +1` |
| ac21d2-geri | 0 | |
| ac21e-Foo-1 / -2 | 1 / 0 | `YENİ KIRMIZI Foo CI-11 Views/Foo/_IndexL10n.cshtml:yok:GetAllStrings`, sonra 1 `PASS Foo CI-11` |
| ac21e-HCM-1 / -2 | 1 / 0 | `YENİ KIRMIZI HCM CI-11 Views/HCM/_IndexL10n.cshtml:yok:GetAllStrings`, sonra 1 `PASS HCM CI-11` |
| ac21f | 0 | `sayfa kaldırıldı Meetings`; 5 `kapandı - satır silinmeli Meetings …`; ` Meetings/` 0 |
| ac21f-geri | 1 | tek kırmızı `YENİ KIRMIZI Meetings CI-11 Views/Meetings/_IndexL10n.cshtml:yok:GetAllStrings` |
| reg-sabotaj | 1 | `YENİ KIRMIZI DevEnablement/GoldenReferenceSlim CI-5 wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js 1 +1` |
| reg-geri | 0 | |
| reg-bos | 0 | kontrol satırı 0 |

Okuyucu ret ölçümleri (kayıtlı koşu, AC-21a): BASE okuyucusunda iki FU-16 satırı çıkış 2; HEAD'de `Meetings · CI-2` ve `CI-1` alt klasör instance'ı, boş anahtar, `Foo Bar`, `A/B/C`, tek seviye CI-3 satırı her biri çıkış 2.

**Tests:**

Deneme koşusu (kayıtsız, depo dışı `/var/folders/1x/lwl5wtmx2rqdb8srq1t66nwh0000gn/T/tmp.cknjp4SVg4`): ham çıkış 0, `zorunlu geçti 10 / 10`, ilk denemede.

Kayıtlı koşu (`docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01`): `run_record.py verify` çıkış 0 (`KOŞU KAYDI biçimi geçerli · commit e3eda1752e3f · çıkış 0`); kayıttaki commit `e3eda1752e3fe74ee8aa4f6f1ab02c992bc7d04b`, tree `0fa347c4a72749bbb0a06575773cbb78b792ffcd`; ham çıkış 0; 2026-10-31T00:34:47Z – 00:36:01Z; 90 artefakt; `stderr.txt` 0 bayt; `stdout.txt` son satır:

```text
zorunlu geçti 10 / 10
```

Her `sonuç=` satırı (`stdout.txt`):

```text
CT-2 sonuç=geçti (zorunlu)
CT-3 sonuç=geçti (zorunlu)
AC-21a sonuç=geçti (zorunlu)
AC-21b sonuç=geçti (zorunlu)
AC-21c sonuç=geçti (zorunlu)
AC-21d sonuç=geçti (zorunlu)
AC-21e sonuç=geçti (zorunlu)
AC-21f sonuç=geçti (zorunlu)
AC-21g sonuç=geçti (zorunlu)
REG sonuç=geçti (zorunlu)
```

`ac21b/stdout.txt` FU-17 satırı birebir: `ölçülmedi Meetings CI-3 - doğrulayıcı alansız sayfayı desteklemiyor (FU-17)`. Aynı koşuda: 19 sayfa, CI-2 162, CI-1 121, CI-4 87, FU-18 106, paylaşılan 1 (`frontend/Diten.Web/Views/Platform/_ViewStart.cshtml`), YENİ KIRMIZI 0, çıkış 0, `süre: 0.6 s`.

Ek ölçüm (kayıtsız, istenmedi; seçicinin depo dışı kopyasında 17 mutant): hepsi self-test'i çıkış 1'e düşürdü; düşen kimlikler AC-21g02…g14, FU-17, draft. AC-21g01'i hedefleyen mutant koşulmadı.

Taban:

- FU-16 satır 134 (paket 134, CT ölçümü 134; bağımsız ölçüm `oracle satirlar` ile `diff` boş, taslakla da boş).
- CI başına satır / eşleşme: CI-1 81, CI-2 4 / 12, CI-4 2 / 2, CI-6 35 / 73, CI-7 8 / 24, CI-8 1 / 1, CI-9 1, CI-10 2 / 4 (beklenenle aynı).
- `awk -F'\t' '$6 == "FU-16"' tests/ui-compliance-baseline.tsv | grep -c 'taban-01'` = 0; `git diff --numstat` silinen 0; sahip denetimi `kararla aynı olmayan FU-16 satırı 0`.
- Sayfa başına satır karar kaydının tablosuyla aynı (fark yok); TimeEntry, Profile, Platform 0 satır.
- Taslakta FU-16 dışı satırların tabandan farkı: 2019 = 2019; yalnız taslakta 0, yalnız tabanda 0, aynı anahtar farklı satır **2** (`Pharmacovigilance/CaseIntakeTriage` CI-3 iki satır: tabanın `reason` hücresi `sahip kararı taban-pvg (2026-10-29)`, taslak kipi sabit `REASON` yazar). Düzeltilmedi.

**Persistence evidence:** N/A (veritabanı yok). Kalıcı çıktı: git commit ve kayıt dizini.

**Security/RBAC/Tenant evidence:** N/A (uç nokta, izin, kiracı verisi yok; paket satır 785, 798). Seçici ağ kullanmaz. Kayıt dizininde kişi adı / e-posta taraması 0 dosya.

**Audit/Evidence:** kanıt düzeyi E2. Kayıt `docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-05-kanit-01/run-record-v1.json`; commit edilmedi, Control Tower kendi commit'inde kaydeder.

**Observability:** N/A. `--base` çıktısının son satırı `süre: … s` korunur.

**Migration/Rollback:** `git revert e3eda1752` ikisini birlikte geri alır; okuyucu ve satırlar ayrı alınamaz (AC-21 (a) tek PR). Sıra: paket Order bölümü.

**Decisions:**

1. FU-16 `owner`: karar kaydı `pss-mod0286-tek-seviye-borc-01` madde 4 tablosundan; seçiciye gömülmedi. Taslak `UNKNOWN_OWNER` yazar.
2. Prompt madde metninde olmayan küçük tercih: okuyucu `Shared` ve `Archive` anahtarlı tek seviye satırı da reddeder (çıkış 2), çünkü paket bunları "asla sayfa" sayar. Bugün böyle satır yok. Kabul eden istemezse tek koşul kaldırılır.
3. `pages/**` (dosya türü fark etmeksizin) ve `js/{X}/{Sub}/**` FU-18 satırı alır, paketin `**` yazımı gereği. `js/{Module}/` altında doğrudan `.js` olmayan dosya genel paylaşılan satırına düşer; paket yalnız `<file>.js` yazar.

**Paket revizyonu girdileri:** davranış çelişkisi yok. Tek girdi: paket rev 15'in bu betiğe verdiği satır atıfları (`:286`, `:373-382`, `:801`, `:806`, `:842-849`, `:868-871`, `:884`, `:888`) Expected HEAD'deki kodu gösterir; bu commit'ten sonra kaydı.

**Blockers:** yok.

**Known gaps / ölçülmeyenler:**

- CI işi: workflow dosyası bu işin dışında; AC-17 ölçümü (kabul edilen commit'te CI koşusu) yok. Bütün koşular yerel.
- Bağımsız doğrulama: kanıt betiği ve ölçüm CT'nindir ama koşuyu ben yaptım; başka ajan henüz doğrulamadı.
- Tek seviye liste sayfasında CI-3 (`Meetings`): tasarım gereği ölçülmez, FU-17. Doğrulayıcı alansız sayfayı desteklemiyor.
- 106 FU-18 JS dosyası: yalnız görünür satır alır, hiçbir kontrol okumaz.
- `.NET` derlemesi, tam test süiti, servis, tarayıcı: prompt gereği koşulmadı.
- İki seviyeli sayfa regresyonu: kayıtlı koşuda bir sabotaj (REG), eski 67 self-test durumu ve taslağın 2019 satırının tabanla anahtar eşitliğiyle ölçüldü; her iki seviyeli sayfada ayrı `--base` koşusu yapılmadı.
- Sabotajlar yalnız klonda; gerçek PR akışında ölçülmedi.

Geçici klonlar (silinmedi, temizlik Control Tower'ın):

- `/var/folders/1x/lwl5wtmx2rqdb8srq1t66nwh0000gn/T/tmp.BNCTTJKBf1/klon` (deneme koşusu)
- `/var/folders/1x/lwl5wtmx2rqdb8srq1t66nwh0000gn/T/tmp.D8LO5Ikehd/klon` (kayıtlı koşu)
- `/var/folders/1x/lwl5wtmx2rqdb8srq1t66nwh0000gn/T/tmp.cknjp4SVg4` (deneme çıktı dizini)

Klon yolları çıkarımdır: betik yolu yazmıyor; bu worktree'yi gösteren ve koşu saatlerime (03:27, 03:34 yerel) uyan `.git/config` dosyalarından bulundu. Aynı TMP altında bu worktree'yi gösteren 15 klon daha var (02:03–02:32), benim oturumumdan önce; onlara dokunmadım.

Yük: ölçümlerde 1 dakikalık yük en çok 7.19; hiçbir koşu beklemedi.

**Out-of-scope changes:** none.
