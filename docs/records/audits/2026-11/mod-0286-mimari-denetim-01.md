# MOD-0286 — UI Design System & Page Pattern Governance · kapanış öncesi mimari denetim 01

- **Tarih:** 2026-11-16
- **Denetlenen commit:** `a21c5f342204828d402de5e14d361c6d9d1c0ee6` (`git rev-parse HEAD`, dal `chore/ct/pss-ui-ct-01`)
- **Denetçi modeli:** Claude Opus 5.5 (`claude-opus-5-5`), bağımsız alt ajan; salt okunur (bu dosya dışında yazma yok)
- **Kapsam:** `scripts/uic_changed_pages.py`, `tests/ui-compliance-baseline.tsv`, `.github/workflows/ui-compliance.yml`,
  `docs/reference/modules/platform/ui-design-system-page-pattern-governance/` (`spec.md`, `compliance-checklist.md`),
  paketler `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md`
  ve `MOD-0286-FU01-page-js-reference-mapping.md`.
- **Okunan kurallar:** `AGENTS.md` Çekirdek, §4, §6.1; `.antigravity/rules/docs-organization.md` §1–§3.1; `.github/CODEOWNERS`;
  karar kaydı `docs/records/decisions/2026-10/pss-mod0286-taban-01.md`.

## Özet karar: KOŞULLU

Araç mimari olarak sağlam: yalnız standart kütüphane ve git kullanıyor, ürün kodunu import etmiyor, depoya yazmıyor;
araç hatalarının hepsi çıkış 2'ye gidiyor ve "0 değişen sayfa"ya düşen bir yol bulunmadı. İş akışı salt okunur, sır yok,
iki eylem de commit SHA'ya sabit; yerel koşuda yalnız 0 yeşil kaldı (1, 2, 127, 137 ve boş kod kırmızı). Öz test 163/163
geçti, sabotaj kanıtları kayıtlı. Kapanış için iki zayıflık var. (1) CI kendi kendini notluyor: seçici, taban ve desenleri
taşıyan paket CODEOWNERS dışında. Paket desen bloğunun kopyasıyla eşitliği yalnız `--self-test`'te denetleniyor, onu da
hiçbir CI koşmuyor. Zayıflatılmış bir CI-7 deseni çalışma anında kabul edildi (ölçüm D-2). (2) Taban satırlarının
376'sının (yüzde 17,3) sahibi hâlâ `belirsiz — sahip belirleyecek`. Kritik ya da engelleyici bulgu yok; orta önemde 3,
düşük önemde 6 bulgu var.

## Madde tablosu

| # | Madde | Sonuç | Kısa gerekçe |
|---|---|---|---|
| 1 | Katman ve sınır | UYGUN | stdlib + git (`uic_changed_pages.py:33-43`); ürün kodu importu yok; depoya yazma yok (öz test "no write" PASS, koşulardan sonra `git status` değişmedi) |
| 2 | CI güvenliği | UYGUN (not: B-6, B-7; dal koruması ÖLÇÜLMEDİ) | `permissions: contents: read` (`ui-compliance.yml:12-13`); sır yok; SHA sabit (`:20`, `:39`); `pull_request`, `_target` değil (`:9-10`); 0/1/2/127/137/boş yerelde koşuldu, yalnız 0 yeşil |
| 3 | Fail-closed | UYGUN (not: B-5) | araç hatası → `ToolError` → 2 (`:1177`); bozuk taban 2; boş, seçenek biçimli ya da çözülmeyen base 2; tüm-sayfa kipine CI yolu yok |
| 4 | Tek kaynak | BULGU (B-1, B-8) | iki desen kümesinin her biri iki yerde duruyor, eşitlik yalnız öz testte denetleniyor; yön asimetrik; `compliance-checklist.md` 56 satırlık korumasız üçüncü kopya |
| 5 | Taban yönetişimi | BULGU (B-3, B-4) | boş sahip ya da neden hücresi 0; `belirsiz` sahipli satır 376 (hepsi CI-14 SharedResource); okuyucu sahip ve nedenin içeriğini denetlemiyor |
| 6 | Performans ve bakım | UYGUN (not: B-9) | `--base HEAD` 0,33–0,37 s; 39 dosyalık fark 2,0 s; 2043 satır, `self_test` tek fonksiyonda 836 satır |
| 7 | Test | UYGUN (not: B-1) | `--self-test` 163/163, çıkış 0, 51,3 s; sabotaj kanıtı var (AC-18 a–e kırmızı/geri, FU01 oracle 4/4) |
| 8 | Kurallarla uyum | UYGUN | kiracı, RBAC ve l10n kuralları modülün kendisine uygulanmıyor (servis, sayfa, uç nokta ya da izin yok); belge yerleşimi §3.1'e uygun |
| 9 | Açık riskler ve takipler | 5 takip | aşağıda "Önerilen takipler" |

## Bulgular

Önem: kritik / engelleyici / orta / düşük. Kritik 0, engelleyici 0, orta 3, düşük 6.

### B-1 (orta) — Desen bloğu eşitlik bekçisi CI'da çalışmıyor; zayıflatılmış desen kabul ediliyor

- Seçici satır kontrollerinin desenlerini çalışma anında paketin `### DL-3 satır kontrol desenleri` kod bloğundan okuyor
  (`uic_changed_pages.py:272-300`, `:303-308`; paket satır 611, CI-7 satırı 640). Çalışma anı okuyucusu yalnız anahtar
  kümesini ve derlenmeyi denetliyor. Bloğun `OWN_PATTERNS` ile aynı olduğu yalnız `--self-test` içinde denetleniyor
  (`:1507`).
- `--self-test`'i hiçbir iş akışı koşmuyor: `grep -n "self-test" .github/workflows/*.yml` boş döndü,
  `grep -n "uic\|ui-compliance" scripts/run_phase1_gates.sh` de boş. Paket bölüm 1 ise farklı bir dizenin öz testi
  düşüreceğini söylüyor (paket satır 403).
- Ölçüm D-2: CI-7 deseni `ZZZ_hic_eslesmez` yapılmış paket metni çalışma anı okuyucusunda kabul edildi. `parse_pattern_block`
  ToolError vermedi; sonuç `OWN_PATTERNS` ile aynı değildi. CI'da bu değişiklik yeşil geçer ve CI-7 borcu görünmez olur.
- Etki: paketi değiştiren bir PR, kontrolü CI'da sessizce gevşetebilir. Bugün bunu yakalayan tek yer öz test, onu da insan
  koşuyor.

### B-2 (orta) — Seçici, taban ve paket CODEOWNERS dışında; CI kendi girdisini notluyor

- `.github/CODEOWNERS:4-11` iş akışlarını (`/.github/workflows/`) ve onay araçlarını koruyor. `scripts/uic_changed_pages.py`,
  `tests/ui-compliance-baseline.tsv` ve iki MOD-0286 paketi listede yok:
  `grep -n "uic_changed\|ui-compliance-baseline\|ui-compliance" .github/CODEOWNERS` boş döndü.
- `pull_request` iş akışı PR'ın kendi seçicisini ve tabanını koşuyor. Bir PR, kod sahibi incelemesi olmadan seçiciyi
  `return 0`'a çevirebilir, tabana satır ekleyebilir ya da desenleri gevşetebilir (B-1). İş akışı dosyası korunuyor,
  ama asıl karar mantığı korunmuyor.
- Paket onayının hash'i yalnız `execution/approvals/**` değişince denetleniyor (`approvals-append-only.yml:10-21`).
  Yalnız paketi değiştiren bir PR onay denetimini tetiklemiyor.

### B-3 (orta) — 376 taban satırının sahibi `belirsiz`

- `awk -F'\t' 'NR>1{print $7}' tests/ui-compliance-baseline.tsv | sort | uniq -c` sonucu: 376
  `belirsiz — sahip belirleyecek` (2175 satırın yüzde 17,3'ü). Hepsi `Resources/SharedResource · CI-14 · FU-14`
  (`tests/ui-compliance-baseline.tsv:1123-1498`).
- Kaynağı `owner_of`: CI-14 ve CI-15'te alan `None` oluyor, sonuç `UNKNOWN_OWNER` (`uic_changed_pages.py:436-447`, `:111`).
  Karar `docs/records/decisions/2026-10/pss-mod0286-taban-01.md:49` "tablo dışı alan → belirsiz, iş paketi raporunda listelenir" diyor. Paket ise
  taslağın "reviewed, owners filled" sonra commit edileceğini yazıyor (paket satır 459-460).
- 2026-10-27'den bu yana sahip belirlenmedi. SharedResource borcunu (fr/es/zh/ar/ru'da 35'er eksik anahtar, tr 10 / fr 47 /
  es 39 / zh, ar, ru 35'er aynı değer; paket satır 498) kapatacak bir sahip yok.

### B-4 (düşük) — Taban okuyucusu sahip ve neden içeriğini denetlemiyor

- `parse_baseline` (`uic_changed_pages.py:388-421`) yalnız hücrenin boş olmamasını istiyor (`:402-403`). Ölçüm D-1'de
  `owner = kimse`, `owner = belirsiz…` ve `reason = x` olan satırların üçü de kabul edildi.
- AC-19 "satır eklemek sahip kararı ister, karar nedene yazılır" diyor. Bunun bir karar adına, bir karar dosyasına ya da
  `AREA_OWNER` değerlerine bağlandığını ölçen bir denetim yok. Bugün yönetişim yalnız PR incelemesine dayanıyor
  (B-2 ile birlikte okunmalı).

### B-5 (düşük) — Beklenmeyen istisna çıkış 1 veriyor ve "yeni kırmızı" diye etiketleniyor

- `execute` yalnız `ToolError` yakalıyor (`uic_changed_pages.py:1156-1178`, `:1177`). Başka bir istisna (ör. `prefetch`
  içindeki `int(header[2])`, `:233`) Python'un varsayılanıyla çıkış 1 verir. İş akışı 1'i "kırmızı (yeni kırmızı)" diye
  yazıyor (`ui-compliance.yml:53`).
- Fail-closed bozulmuyor, iş yine kırmızı. Ama araç hatası, geliştiriciye kendi değişikliğinin borcu gibi gösteriliyor.
  Bu yolu tetikleyen gerçek bir girdi bulunmadı; bulgu yapısal.

### B-6 (düşük) — Onaylı paketin CI metni çalışan iş akışını anlatmıyor

- Paket DL-3 bölüm 3 (satır 467-473) yol süzgeçli bir tetikleyici ve "yalnız 1 ya da 2'de kırmızı" diyor. İş akışı her
  PR'da çalışıyor (`ui-compliance.yml:6-10`, commit `e2be73965`) ve sıfır dışındaki her çıkışta kırmızı veriyor
  (`:45-56`, commit `38e5bb13e`).
- İki sapma da daha sıkı yönde, güvenliği azaltmıyor. Ama onaylı paket metni (sha `0c53533e…` = `approvals.tsv:35`)
  bugünkü CI'ı anlatmıyor. Commit mesajı "sahip kararı 2026-11-02" diyor; bu karar için bir karar kaydı bulunamadı:
  `grep -rln "2026-11-02" docs/records` sorgusunun ui-compliance süzgeçli sonucunda karar dosyası çıkmadı.

### B-7 (düşük) — Checkout kimlik bilgisini saklıyor

- `actions/checkout` `persist-credentials` belirtilmeden kullanılıyor (`ui-compliance.yml:20-22`, varsayılan `true`).
  Salt okunur `GITHUB_TOKEN`, PR'ın kendi kodu (seçici) koşarken `.git/config` içinde duruyor.
- Token yalnız `contents: read` olduğu için etki düşük. `persist-credentials: false` olağan bir sıkılaştırma.

### B-8 (düşük) — Tek kaynak asimetrik; korumasız üçüncü kopya

- CI-2…CI-10 desenlerinin çalışma anı kaynağı paket; betikteki `OWN_PATTERNS` yalnız test kopyası
  (`uic_changed_pages.py:19-21`, `:64-74`). FU01 desenlerinde yön ters: çalışma anı kaynağı betikteki `FU01_REF_PATTERNS`
  (`:97-101`), paketteki blok (FU01 paket satır 71-75) test kopyası (öz test FU01-67, `:1975-1986`).
- `compliance-checklist.md`, paketin 56 UIC/RC satırının bayt kopyası. Bugün aynılar (ölçüm D-5: `diff` çıkışı 0, 0 satır).
  Ama `grep -rln "compliance-checklist" scripts tests .github frontend/Diten.Web.Tests` yalnız seçicinin yorumunu buldu.
  Eşitliği koruyan bir bekçi yok, belge başlığı da hâlâ "paket rev 12" diyor (`compliance-checklist.md:3`); paket
  rev 15'te.

### B-9 (düşük) — Bakım yükü

- `scripts/uic_changed_pages.py` 2043 satır, 77 fonksiyon. `self_test` tek fonksiyonda 836 satır (`:1182-2017`), `ci`
  100 satır (`:956-1055`), `evaluate_page` 63 satır. Öz test dışındaki kod yaklaşık 1207 satır.
- Belgeleme ve öz test kapsamı güçlü. Ama tek dosya ve tek dev test fonksiyonu, desen ya da tablo değişikliklerinde
  inceleme maliyetini yükseltiyor.

## Madde ayrıntıları

**1 · Katman ve sınır — UYGUN.** İmportlar `argparse, collections, json, os, re, subprocess, sys, tempfile, time,
xml.etree.ElementTree, pathlib` (`:33-43`); `importlib`, `sys.path` ya da `__import__` yok. Dış süreçler: git (`:160`),
`.antigravity/scripts/verify_datatable_page.py` (`:743-746`) ve resx denetçisi (`:822`). İkisi de depo aracı, `-B` ile
alt süreç olarak koşuyor, import edilmiyor. `services/`, `gateway/` ya da `.csproj` başvurusu yok. Öz test dışında
`write_text`, `open(`, `mkdir` ya da `unlink` yok (ölçüm D-6). Öz test yalnız `tempfile.TemporaryDirectory` altında yazıyor
(`:1191`). Bu denetimdeki bütün koşulardan sonra `scripts/__pycache__` ve `.antigravity/scripts/__pycache__` oluşmadı.

**2 · CI güvenliği — UYGUN.** Yerel koşu (ölçüm D-3): iş akışının iki adım gövdesi `sed -n 29,35p` / `51,56p` ile dosyadan
kesildi ve `bash --noprofile --norc -eo pipefail` ile koşuldu. Seçici çıkışları ve sonuçlar: 0 → adım 2 = 0; 1 → 1;
2 → 1; 127 → 1; 137 → 1; boş kod → 1. Adım 1 her durumda 0 bitiyor; iş yalnız adım 2'de kırmızı oluyor ve kod boş
kalırsa yine kırmızı. Dal koruması ve "zorunlu kontrol" ayarı ÖLÇÜLMEDİ (ağ yok).

**3 · Fail-closed — UYGUN.** Ölçüm D-1, üretim fonksiyonları bellekte çağrıldı: boş owner hücresi, tekrarlı satır ve CR
karakteri `ToolError` (çıkış 2) verdi. `execute(ci, base=…)`: `''`, `'   '`, `'--draft-baseline'`, `'-x'`, sıfır SHA ve
olmayan ref için çıkış 2. Kod: çalışma ağacı HEAD'le aynı değilse 2 (`:531`); base seçenek biçimindeyse 2 (`:537`);
kipler argparse'ta birbirini dışlıyor (`:2025`); taslak kip yalnız `--draft-baseline` ile seçiliyor. Ortam değişkeni
yalnız git ortamında okunuyor (`:158`). Öz test kanıtları: "exit 2: a missing base ref -> tool error, never '0 changed
pages'" ve "a base that is an option ('--all') is refused". "0 değişen sayfa" yalnız `git diff` başarıyla boş dönünce
oluşuyor. İstisna: B-5.

**4 · Tek kaynak — BULGU.** Bkz. B-1 ve B-8.

**5 · Taban yönetişimi — BULGU.** `--check-baseline` çıkış 0, "taban geçerli" (ölçüm D-4). Borç, fu sütununa göre
(satır / eşleşme): FU-14 1953 / 742; FU-16 134 / 116; FU-19 22 / 92; FU-4 64 / 0; FU-12 1 / 0; FU-15 1 / 0. Toplam
2175 / 950 (seçicinin "toplam satır=2175 eşleşme=950" çıktısıyla aynı). Kontrole göre: CI-3 1177, CI-14 376, CI-6 140
(399 eşleşme), CI-9 125, CI-10 110 (350), CI-1 81, CI-4 80 (81), CI-12 22, CI-2 21 (54), CI-5 20, CI-7 20 (43), CI-8 1,
bilgi 2. Sahiplere göre: platform-shared-services 531, belirsiz 376, supply-chain-execution 368, commercial-suite 279,
human-capital-management 233, master-data-management 78, enterprise-strategy-business-performance 76, portfolio-delivery
67, developer-enablement 66, management-governance 53, procurement 35, pharmacovigilance 13. Boş sahip ya da neden
hücresi 0. Nedenler: 2015 satır `pss-mod0286-taban-01`, 134 `tek-seviye-borc-01`, 22 `fu01-satir-sahipleri`, 2 `taban-pvg`,
2 `bilgi:` satırı. FU-16 ve FU-19 satırlarının hepsinde sahip dolu.

**6 · Performans — UYGUN.** `--base HEAD`: 3 koşuda 0,37 / 0,35 / 0,33 s, araç kendi süresini 0,3 s veriyor, çıkış 0.
39 frontend dosyası değiştiren gerçek bir fark (`--base 49b0ed74a~1`): 2,0 s; HEAD'de 146, tabanda 15 görünüm okundu;
çıkış 0, YENİ KIRMIZI 0. `--check-baseline` 0,83 s. Karmaşıklık: B-9.

**7 · Test — UYGUN.** `PYTHONDONTWRITEBYTECODE=1 python3 scripts/uic_changed_pages.py --self-test`: "self-test: 163/163
passed", PASS 163, FAIL 0, çıkış 0, 51,29 s. Sabotaj kanıtları:
`docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-04/ac18{a,b1,b2,c,d,e}-sabotaj.exit` = 1, eşleri `-geri.exit`
= 0 (6 çift); `docs/records/audits/2026-10/mod-0286-uic-baseline-01/sabotage-01/`;
`docs/records/audits/2026-11/mod-0286-fu01-01/oracle-sabotaj-01.txt:15` "kontrol=eşit, a=kırmızı, b=kırmızı, c=kırmızı,
d=kırmızı". FU01 paketi SB-14 `FU01_REF_PATTERNS` sabotajını öz testle tanımlıyor (FU01 paket satır 224-225). Eksik: öz
test CI'da koşmuyor (B-1).

**8 · Kurallarla uyum — UYGUN.** Kiracı (`multi-tenancy`), RBAC (`security-jwt`, `permission-key-standard`) ve UAS-001 bu
modülün kendisine uygulanmıyor. Paket "no service, page, entity, endpoint or permission" diyor (paket satır 30-31);
`entity_base: GlobalEntity` hiçbir varlığa yetki vermiyor (satır 34-36). Araç kiracı verisine dokunmuyor. Seçicinin
Türkçe çıktısı geliştirici aracı çıktısı; ürünün kullanıcıya görünen metni değil, bu yüzden `localization-standard`
uygulanmıyor. Modül l10n kuralını tersinden uyguluyor: CI-13 zorunlu dil kümesi `Resources/Views/Platform/` için 2,
diğerleri için 7 (`:796`); bu, Çekirdek madde 4 ile aynı. Belge yeri: spec ve kontrol listesi
`docs/reference/modules/platform/<modül>/` altında (§3.1 "Modül belgesi"); ölçüm kayıtları `docs/records/audits/2026-10/`
ve `2026-11/` altında (§3.1 "Mimari denetim raporu", §2 `audits/<yyyy-mm>/`); bu rapor da öyle. `docs/` kökünde yalnız
`README.md` var. Onay: paket sha256 `0c53533e3e2434fb…` `approvals.tsv:35`'teki son satırla, FU01 `d970554beef0b5e1…`
`:40` ile aynı.

## Önerilen takipler (en çok 5)

1. **(orta)** `ui-compliance.yml`'ye `--self-test` adımı eklensin; böylece paket desen bloğu ile `FU01_REF_PATTERNS`
   eşitlik bekçileri her PR'da koşar (B-1). Korunan yol: `.github/workflows/` CODEOWNERS ve sahip onayı ister.
2. **(orta)** `scripts/uic_changed_pages.py`, `tests/ui-compliance-baseline.tsv` ve iki MOD-0286 paketi `.github/CODEOWNERS`'a
   eklensin (B-2). Korunan yol: sahip kararı gerekir.
3. **(orta)** 376 `Resources/SharedResource · CI-14` satırının sahibi bir sahip kararıyla belirlensin; karar verilene kadar
   kapanış belgesinde açık TBD olarak listelensin (B-3). Sahip başka bir domain olursa: başka modül, sahibine sorulur.
4. **(düşük)** `execute` beklenmeyen istisnayı da çıkış 2 ve "araç hatası" olarak bildirsin; bunu kanıtlayan bir öz test
   vakası eklensin (B-5).
5. **(düşük)** Paketin bir sonraki revizyonunda DL-3 bölüm 3 bugünkü tetikleyiciye ve "yalnız 0 yeşil" kuralına
   getirilsin; `compliance-checklist.md` kopyası ya bir eşitlik bekçisi alsın ya da pakete atıfa indirilsin (B-6, B-8).

## Ölçülmeyenler

- **Dal koruması ve zorunlu kontrol ayarı** (madde 2): ağ yok, `gh api` koşulmadı. ÖLÇÜLMEDİ: ağ.
- **GitHub'daki gerçek iş akışı koşusu**: ağ yok. Kayıtlı kanıt olarak
  `docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-04/ac17b-*` dosyalarının varlığı görüldü, içerikleri
  yeniden doğrulanmadı.
- **`--draft-baseline` (tüm-sayfa kipi)**: kapsam dışı ve ağır; koşulmadı. CI'dan erişilemediği kodla ve öz testle
  gösterildi.
- **Yük kuralından sapma (kayıt amaçlı):** `--base HEAD` koşusu, `uptime` ile aynı komutta başlatıldı; o anda 1 dakikalık
  yük 12,75'ti (eşik 12). Koşu hafifti (3 × 0,35 s) ve sonucu etkilemedi, ama kural "önce ölç, sonra koş" diyordu.
  Sonraki ağır koşulardan önce yük ayrıca ölçüldü: öz test 11,63'te, 39 dosyalık fark 7,93'te.
- **Denetim sırasında çalışma ağacında başka oturumların dosyaları** belirdi (izlenmeyen, bu denetimin değil, açılmadı):
  `docs/guides/ui-design-system-page-pattern-governance/`, `docs/records/decisions/2026-11/pss-mod0286-adr-01.md`,
  `docs/records/releases/2026-11-16-mod-0286-release-note.md`. Denetim başında `git status` temizdi.

## Koşulan komutlar ve ham çıkış özetleri

Hepsi çalışma ağacı kökünden; Python koşuları `PYTHONDONTWRITEBYTECODE=1` ile. Geçici dosyalar oturum karalama
dizininde, depo dışında.

| # | Komut | Yük (1 dk) | Ham çıkış özeti |
|---|---|---|---|
| — | `uptime; git rev-parse HEAD` | 6,75 | `a21c5f342204828d402de5e14d361c6d9d1c0ee6` |
| D-4 | `/usr/bin/time -p python3 scripts/uic_changed_pages.py --check-baseline` | 7,71 | "taban … geçerli", CI-1…CI-15 + bilgi, "toplam satır=2175 eşleşme=950", real 0,83 s, çıkış 0 |
| — | `python3 scripts/uic_changed_pages.py --base HEAD` × 3 | 12,75 (aynı komutta ölçüldü, bkz. Ölçülmeyenler) | "süre: 0.3 s", real 0,37 / 0,35 / 0,33 s, çıkış 0 |
| D-3 | `bash …/exit-harness.sh` (iş akışı adımları `sed -n 29,35p` / `51,56p` ile kesildi, `bash --noprofile --norc -eo pipefail`) | — | `selector=0 step2=0`; `1 → 1`; `2 → 1`; `127 → 1`; `137 → 1`; `empty code step2=1` |
| — | `uptime` | 11,63 | eşik altında, öz test koşuldu |
| — | `/usr/bin/time -p python3 scripts/uic_changed_pages.py --self-test` | 11,63 | "self-test: 163/163 passed", PASS 163, FAIL 0, real 51,29 s, çıkış 0 |
| D-1 | `python3 …/failclosed_probe.py <kök>` (üretim `parse_baseline` / `execute`, bellekte) | — | gerçek taban kabul (2175); boş owner, tekrar ve CR → ToolError; `owner=belirsiz`, `owner=kimse`, `reason=x` → kabul; 6 hatalı base → çıkış 2 |
| D-2 | `python3 …/pattern_probe.py <kök>` (üretim `parse_pattern_block`, bellekte) | — | "gerçek blok == OWN_PATTERNS: True"; zayıflatılmış CI-7 → "KABUL etti … OWN_PATTERNS ile aynı mı: False" |
| — | `git merge-base --is-ancestor 49b0ed74a HEAD`; `git diff --name-only 49b0ed74a~1...HEAD -- frontend/Diten.Web \| wc -l` | 7,93 | ata = 0; 39 dosya |
| — | `/usr/bin/time -p python3 scripts/uic_changed_pages.py --base 49b0ed74a~1` | 7,93 | "okunan görünüm metni: HEAD 146, taban 15", "süre: 2.0 s", real 2,10 s, çıkış 0, YENİ KIRMIZI 0 |
| D-5 | `grep -E '^\| (UIC\|RC)-[0-9]+ '` ile kontrol listesi ve paket → `diff` | — | 56 / 56 satır, `diff` çıkışı 0, fark 0 satır |
| D-6 | `awk 'NR<1182 \|\| NR>2017' scripts/uic_changed_pages.py \| grep -n "write_text\|write_bytes\|open(\|mkdir\|unlink\|rmtree\|os.remove\|shutil"` | — | boş (öz test dışında yazma yok) |
| — | `python3 -c "import ast …"` (fonksiyon uzunlukları) | — | 77 fonksiyon; `self_test` 836, `ci` 100, `evaluate_page` 63, `instance_problem` 49 satır |
| — | `awk -F'\t' 'NR>1{print $7}' tests/ui-compliance-baseline.tsv \| sort \| uniq -c` ve fu / neden toplamları | — | Madde 5'teki sayılar; `belirsiz` 376, hepsi `FU-14 CI-14` |
| — | `grep -n "uic_changed\|ui-compliance-baseline\|ui-compliance" .github/CODEOWNERS` | — | boş |
| — | `grep -n "self-test" .github/workflows/*.yml`; `grep -n "uic\|ui-compliance" scripts/run_phase1_gates.sh` | — | ikisi de boş |
| — | `grep -n -i "secret\|token\|pull_request_target\|write" .github/workflows/ui-compliance.yml` | — | yalnız satır 5'teki yorum ("no secret") |
| — | `shasum -a 256` iki paket | — | `0c53533e3e2434fb…` (= `approvals.tsv:35`), `d970554beef0b5e1…` (= `:40`) |
| — | `git log -1 --format=%B e2be73965` | — | "run on every pull request so branch protection can require it … self-test 93/93" |
| — | `git status --short` (son) | — | yalnız başka oturumların 3 izlenmeyen yolu; bu denetimin koşularından değişiklik yok |
