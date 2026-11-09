# MOD-0290-FU06 — bağımsız inceleme isteği 01 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md` @ sha256 `978cf073f9d0b5d3151ec40f5e0a50a7858625abf33559a5ffc09f20857266f9` (revizyon 1)
- **Kaynak commit:** `3043a88027cefd7bcac6402c1e4bffbb6de51c5b` · dal `chore/ct/mdm-ct-02` (yerel)
- **Risk:** HIGH (ilk onay; önceki onaylı sürüm yok)
- **Önceki sürüm ve incelemeler:** ilk inceleme
- **Yetki:** sahibin seçimi "Küçük ayrı paket (Önerilen)" (2026-11-02)
- **İsteyen / hazırlayan / tarih:** kule · CT · Ürün ana verisi · 2026-11-02
- **İnceleyen:** diyalog inceleyeni: alt ajan · Opus 5.5, high · yanıtın başına model, effort, ajan kimliği
- **ZIP:** `MOD-0290-FU06-review-3043a8802.zip` · sha256 `7fe09cc6b63dc397edf2ce150dd45743d966db96dbdd231c42a9ab446f8e96cb` · 3398206 bayt · 86 + `MANIFEST.sha256`
  - Yeri: `/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/0d0a433f-17d4-42c7-bf47-7b50ebd3e6bb/scratchpad/review-fu06/MOD-0290-FU06-review-3043a8802.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0290-FU06/2026-11-02-978cf073.md`
- **Prompt bloğunun SHA-256'sı:** `bcc40b4d1ff0798f7bbc808e46cfa584304a1511c4ca8b375cae0eaf9bd3eaf8`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: inceleme 1 — MOD-0290-FU06 module pack bağımsız incelemesi 01 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md @ sha256 978cf073f9d0b5d3151ec40f5e0a50a7858625abf33559a5ffc09f20857266f9 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Görev: paketi SOP §20.4'e göre incele: kendi içinde çelişki, kodla tutmayan iddia, ölçülemeyen kabul ölçütü, eksik ya da uygulanamaz test, kapsam ve yetki sınırı, canlıya alma sırası. Paketin iddialarını ZIP'teki kodla kendin ölç. ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz.
Ek: sık bulgu türleri — docs/guides/operations/pack-review-common-findings.md; en azından bunlara bak, yalnız bunlara bakma.
Bulgularını yazdıktan sonra kuleyle konuşabilirsin; kule düzeltince "düzeltildi, yeniden oku" der, sen yeni ZIP'in sha256'sını doğrulayıp imzalayacağın sürümü yeniden okursun. Son sonucun o sürüm içindir.
Çıktı: ilk satır `hedef: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md @ 978cf073f9d0b5d3151ec40f5e0a50a7858625abf33559a5ffc09f20857266f9`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin> | açık | <K…> | <tarih>`;
son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL.
ZIP'in yeri: /private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/0d0a433f-17d4-42c7-bf47-7b50ebd3e6bb/scratchpad/review-fu06/MOD-0290-FU06-review-3043a8802.zip
ZIP sha256: 7fe09cc6b63dc397edf2ce150dd45743d966db96dbdd231c42a9ab446f8e96cb
```
