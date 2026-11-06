# MOD-0290-FU06 — bağımsız inceleme isteği 02 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md` @ sha256 `c46636ccc8a612e2c3cbe7a59068f3b2968e830e357dd34e41a1e71cc84a1f9c` (revizyon 4)
- **Kaynak commit:** `b0390247e73addb16cc7c3332f56f2c18b8a170c` · dal `chore/ct/mdm-ct-02` (yerel)
- **Risk:** HIGH (ilk onay) — son bakış kendiliğinden koşar
- **Önceki sürüm ve incelemeler:** inceleme 01 (4 tur), `docs/records/reviews/MOD-0290-FU06/2026-11-02-c46636cc.md`; son bakışa verilmez
- **Yetki:** kart §1.1/3, HIGH kuralı
- **İsteyen / hazırlayan / tarih:** kule · CT · Ürün ana verisi · 2026-11-02
- **İnceleyen:** bağımsız son bakış: geçmişsiz alt ajan · Opus 5.5, high
- **ZIP:** `MOD-0290-FU06-review-b0390247e.zip` · sha256 `cc9bb2f52d05ebaa77c0e321b708e2667cd793f97628ff02ad993271268041dc`
  - Yeri: `/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/0d0a433f-17d4-42c7-bf47-7b50ebd3e6bb/scratchpad/review-fu06-son/MOD-0290-FU06-review-b0390247e.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0290-FU06/2026-11-02-c46636cc-son-bakis.md`
- **Prompt bloğunun SHA-256'sı:** `fef011b64daeba0e63798100b5018f011531f19b48d229557af8949b91f72cb4`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: bağımsız son bakış 1 — MOD-0290-FU06 module pack bağımsız incelemesi 02 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md @ sha256 c46636ccc8a612e2c3cbe7a59068f3b2968e830e357dd34e41a1e71cc84a1f9c — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Görev: paketi SOP §20.4'e göre incele: kendi içinde çelişki, kodla tutmayan iddia, ölçülemeyen kabul ölçütü, eksik ya da uygulanamaz test, kapsam ve yetki sınırı, canlıya alma sırası. Paketin iddialarını ZIP'teki kodla kendin ölç. ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz. Tek tur.
Çıktı: ilk satır `hedef: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md @ c46636ccc8a612e2c3cbe7a59068f3b2968e830e357dd34e41a1e71cc84a1f9c`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin> | açık | <K…> | <tarih>`;
son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL.
ZIP'in yeri: /private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/0d0a433f-17d4-42c7-bf47-7b50ebd3e6bb/scratchpad/review-fu06-son/MOD-0290-FU06-review-b0390247e.zip
ZIP sha256: cc9bb2f52d05ebaa77c0e321b708e2667cd793f97628ff02ad993271268041dc
```
