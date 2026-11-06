---
format: rep-001-v2
module: MOD-0290-FU06
work_package: MDM-FU06-DL1
queue: mdm
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md@16c4d07a769ecf1fbdd2e82e8395d4fc3b37591f:560
decision: accepted
completion: WP Done
conditions: -
commit: edaab86104e2cecf7616889f649e5c5bdad0c07b
evidence: docs/records/audits/2026-11/mod0290-fu06-dl1/accept-run-1/run-record-v1.json@e4fcaf8d4c0e1ef9993926f79377ca2306d8ef06
acceptance_list: docs/records/audits/2026-11/mdm-fu06-dl1-acceptance-02/MDM-FU06-DL1-ACCEPTANCE-LIST.md@2fa8fbacdc727f3f6da7cbc479820e6229a7aa11
date: 2026-11-06
supersedes: docs/records/audits/2026-11/mdm-fu06-dl1-acceptance-01/MDM-FU06-DL1-ACCEPTANCE.md
verifier: Claude Opus 5.5 (claude-opus-5-5, high), bağımsız doğrulayıcı alt ajan; kule chore/ct/mdm-ct-02 oturumundan başlatıldı, kodu, testleri ve kanıt betiğini yazmadı
implementer: işçi oturumu local_ad78694c (Ana veri › Ürün okuma izni)
module_pack: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md@16c4d07a769ecf1fbdd2e82e8395d4fc3b37591f
---

# Kabul kaydı — MDM-FU06-DL1 (sürüm 2)

**Karar: kabul (doğrulayıcının kanıt kararı).** MOD-0290-FU06 DL-1'de `mdm.product-master.read` anahtarı
`product-item-sku-master` manifestinde `GLOBAL_PRODUCTS / READ_PRODUCT_MASTER` arayüzsüz `View` eylemi oldu. Modül
sürümü 1.4.0. Dokuz zorunlu ölçütün dokuzu tek bir `run_record` koşusunda (`accept-run-1`) geçti. Liste:
`MDM-FU06-DL1-ACCEPTANCE-LIST.md`.

## Neden yeni kayıt

Kayıt 01 eski biçimli pakete (revizyon 5, `a63a966c`) ve iki ayrı koşuya bağlıydı. Bu yüzden AC-1…AC-8 «inceleme bekler»
kaldı. Paket revizyon 6 (`16c4d07a7`, sha256 `745e0c49…`) §17'yi aracın biçimine getirdi; anlam aynı. Kule aynı kod
ağacında testleri, on iki sabotajı, son testleri ve E3'ü tek koşuda aldı. Kayıt 01 değişmedi; bu kayıt onun yerini alır.

## Dayanak

- `python3 scripts/run_record.py verify …/accept-run-1/run-record-v1.json` → geçerli, commit `edaab86104e2`, çıkış 0.
  `edaab8610..e4fcaf8d4` yalnız `accept-run-1/` altında dosya ekler.
- `services/`, `frontend/` ve `gateway/` ağaçları `d27fab84a` (kayıt 01'in kodu) ile `edaab8610` arasında aynı.
  `d27fab84a..edaab8610` yalnız kule dosyalarını değiştirdi: paket rev 6, inceleme, `reviews.tsv`, kabul betiği,
  kayıt 01 ve `e3-run-3`.
- Taban ve son testler 28/28, 0 başarısız. T1, T2, T3, T4-b, GP ve ID adıyla `Passed`. Bekçinin iki testi T3 ile aynı
  `dotnet test` sürecinde geçti.
- Sabotajlar 12/12 `eşit`. Beklenmeyen ya da eksik kırmızı yok, derleme hatası yok. Geri alma bayt bayt eşit.
  d'de T3 yeşil kaldı, paketteki beklenti bu.
- `diff-scope.json`: kod yolları yalnız §5'in dört dosyası, yasak alanlarda değişiklik yok. İşçinin kendi diff'i
  (`2c7301cd4..a8daf1ea2`) dört §5 dosyası ve kanıt dizini.
- E3: 77 `KONTROL` satırının hepsi geçti, Y1→Y6 paket sırasıyla, `SONUÇ: E3 YEŞİL`. Yük başlangıçta 8.34; sabotaj ve
  test adımlarında 8.26–11.79, eşik 12. Çıktılarda JWT ve yetki başlığı 0.

## Sınırlar ve açık kayıtlar

- **TRX dosyaları commit'te yok.** Koşu kaydının 40 artefaktından 14'ü TRX: `tests.trx`, `tests-final.trx` ve
  `sabotage-*.trx`. Bunlar `.gitignore:32` (`*.trx`) yüzünden kanıt commit'ine girmedi. Diskteki 14 dosyanın baytı ve
  sha256'sı koşu kaydıyla eşit (doğrulayıcı ölçtü). Liste TRX'e değil, commit'lenmiş JSON ve günlük özetlerine atıf
  yapar. `check` bu eksik artefaktlar için engel verir.
- **Kanıt betiğini kule yazdı ve koştu** (`mdm_fu06_dl1_accept.py`, sha256 `31a5a79e`). Doğrulayıcı betiğin doğruluğunu
  kod düzeyinde incelemedi. Özetleri paket §17 ve §18 ile karşılaştırdı; testleri ve sabotajları yeniden koşmadı.
- AC-5 sabotaj kaydını `SABOTAJ.md` satırı olarak değil, koşunun `sabotage-summary.json` özeti olarak taşır. Özet aynı
  tabloyu kaynak gösterir. `git status` ölçümü kanıt dizini dışını sayar.
- AC-7'de koşu `docs/` ve `execution/` yollarını kod dışı sayar. Tabandan beri değişen kule kayıtları işçinin değildir.
- Paket revizyon 6'nın `approvals.tsv`'de onay satırı yok; yalnız inceleme satırı var (KOŞULLU, engel yok). Onay
  Beste'den istenecek.
- §23 canlı doğrulaması bu dilimin kabulü değildir. Post-deploy §7 düzeltmeleri ve §25 K-3/K-4/K-5 kule işidir. Bu kayıt
  Module Done demek değildir.

## Araç durumu

`acceptance_list` commit'i bilerek boş bırakıldı; kule doldurur. Liste commit'lenmeden `check` koşmaz. Commit'lenmemiş
liste için `check_list` salt okunur denendi: 9 satır, 0 engel.
