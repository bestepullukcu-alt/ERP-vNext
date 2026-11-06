---
format: rep-001-v2
module: MOD-0290-FU06
work_package: MDM-FU06-DL1
queue: mdm
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md@a63a966cff137791e2900fa7a7c4e9eb920b2493:558
decision: accepted
completion: WP Done
conditions: -
commit: d27fab84a2909c062a35749bb607b94e23ec7de1
evidence: docs/records/audits/2026-11/mod0290-fu06-dl1/e3-run-3/run-record-v1.json@80ee88d4fda0b12e60e3c48489c4f4f129d8e534
acceptance_list: docs/records/audits/2026-11/mdm-fu06-dl1-acceptance-01/MDM-FU06-DL1-ACCEPTANCE-LIST.md@83a6f9f14b699732672bc5c4f22cd108fb9e0d52
date: 2026-11-06
supersedes: -
verifier: Claude Opus 5.5 (claude-opus-5-5, high), bağımsız doğrulayıcı alt ajan; kule chore/ct/mdm-ct-02 oturumundan başlatıldı, kodu ve testleri yazmadı
implementer: işçi oturumu local_ad78694c (Ana veri › Ürün okuma izni)
module_pack: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md@a63a966cff137791e2900fa7a7c4e9eb920b2493
---

# Kabul kaydı — MDM-FU06-DL1

**Karar: kabul (doğrulayıcının kanıt kararı).** MOD-0290-FU06 DL-1: `mdm.product-master.read`, `product-item-sku-master`
manifestinde `GLOBAL_PRODUCTS / READ_PRODUCT_MASTER` arayüzsüz `View` eylemi oldu, modül sürümü 1.4.0. Dokuz ölçütün dokuzu
kanıtla karşılandı (liste: `MDM-FU06-DL1-ACCEPTANCE-LIST.md`). **`acceptance_record.py check` bu kaydı bugünkü haliyle
ACCEPTED'e geçirmez.** Engeller aşağıda "Araç sınırı" başlığında; hiçbiri ürün kusuru değil.

## Neden bu commit ve bu kanıt

- Araç `commit` alanını koşu kaydının commit'ine bağlar (`scripts/run_record.py` `verify_record`: «eski kanıt yeni kodu
  kabul ettiremez»). Kayıt tek koşu kaydı alır. Gerekli seviye E3 olduğu için E3 koşusu seçildi: `e3-run-3`,
  commit `d27fab84a`.
- `d3d998ff3` (işçi dalının kule dalına birleşmesi) seçilmedi: aracın kuralıyla hiçbir koşu kaydına eşit değil.
  `services/` ağacı `649bc813c` (E2), `a8daf1ea2`, `d3d998ff3` ve `d27fab84a` (E3) arasında aynı; `git diff` boş.
  `d3d998ff3..d27fab84a` yalnız kule dosyalarını değiştirir: E3 betiği, `CT-QUEUE.tsv`, kurulum ölçümü ve sayılmayan iki koşu.
- `d27fab84a..80ee88d4f` yalnız `e3-run-3/` altındaki beş dosyayı ekler. Aracın kanıt commit'i kuralına uyar.

## Dayanak

- **İki koşu kaydı da `run_record.py verify` ile geçerli:** `e2-run` (commit `649bc813c`, çıkış 0, 28/28) ve `e3-run-3`
  (commit `d27fab84a`, çıkış 0, `SONUÇ: E3 YEŞİL`).
- **AC-1…AC-4, AC-6:** T1, T2, T3, T4-b ve kimlik testi üretim koduyla ölçüyor. Anahtar testte yazıyla geçiyor. T3 ve T4-b
  Platform'un iki üretim işleyicisini üretim manifestiyle koşuyor. E2'de hepsi yeşil.
- **AC-5:** `SABOTAJ.md`'de on iki sabotajın her birinde çapa, beklenen ve görülen kırmızı aynı. Geri almadan sonra ağaç boş
  ve 28/28 yeşil. d'de T3 yeşil kaldı, paketteki beklenti de bu.
- **AC-7:** İşçinin `2c7301cd4..a8daf1ea2` diff'i dört §5 dosyası ve kanıt dizini. Korunan yolların hepsinde diff boş.
- **AC-8:** Bekçinin iki `[Fact]`'i E2'nin tek `dotnet test` sürecinde, T3 ile aynı filtrede. 0 başarısız, 0 atlanan.
- **AC-9:** `e3-run-3` Y1→Y6'yı paket sırasıyla geçti: taban 1.3.0'da 403; 1.4.0'dan sonra sayaç değişmeden hâlâ 403;
  `PATCH`'ten sonra Admin ve Viewer 200, anahtarsız kullanıcı 403; yeni kiracıda 200. Eşitlik hatası 0. Yük kapısı
  12'nin altına inince başladı. Üç artefaktta JWT, yetki başlığı ve sır kalıbı 0.
- `git grep -nF '"mdm.product-master.read"' d27fab84a -- 'services/*/src/*'` tam dört satır döner (paket §18).

## Sınırlar ve açık kayıtlar

- **Testler yeniden koşulmadı.** Doğrulayıcı `dotnet test`'i ve sabotajları yeniden koşmadı. Kanıt işçinin E2 koşu kaydı,
  `SABOTAJ.md` ve kodun okunmasıdır. Sabotaj TRX dosyaları depoda yok.
- E2 çıktısı test adlarını listelemiyor. Bekçinin 28 test içinde olduğu filtreden ve sayıdan (taban 25 + 3) çıkarım.
- `SABOTAJ.md` açık kaydı: kırmızıdan yeşile 1. adım yük 12.06'dayken başladı. Sabotaj satırlarını etkilemez.
- E3 betiğini işçi değil kule yazdı ve koştu. Betik `d27fab84a`'da değişti (sha256 `24b42b7e`). Sahip kararı
  (`docs/records/decisions/2026-11/mdm-fu06-dl1-e3-deferred-owner-decision-01.md`) bu yolu öngörür. Yeni sha için ayrı bir
  kırmızı taban kaydı bu incelemede aranmadı.
- `e3-run` (çıkış 4, yük) ve `e3-run-2` (çıkış 3) sayılmayan koşulardır. Kanıt değildir.
- §23 canlı doğrulaması bu dilimin kabulü değildir. Post-deploy §7 düzeltmeleri, paket künyesi ve §25 K-3/K-4/K-5 kule
  işidir. Bu kayıt Module Done demek değildir.

## Araç sınırı (check bugün engel verir)

1. **Paket AC biçimi eski.** Paketin §17 maddeleri `- [ ] AC-n <metin>` biçiminde, araç `(zorunlu|isteğe bağlı) —`
   bekler. `draft --part list` çıkış 2 verdi. `check` `module_pack` bağı için «paket: AC kimliği yok (eski biçim)» engeli
   verir. Liste elle, aracın sütunlarıyla yazıldı.
2. **Tek koşu kaydı.** AC-1…AC-8'in kanıtı E3 koşu kaydının artefaktı değil. Aracın kuralıyla bu satırlar
   `inceleme bekler — neden`. `decision: accepted` ile birlikte «otomatik kabul yok» engeli verir. Kabulü sahip ya da kule
   onaylar.
3. **`acceptance_list` commit'i boş.** Liste commit'lenmeden dolamaz. Önce liste commit'lenir, sha buraya yazılır, sonra
   kayıt commit'lenir.
