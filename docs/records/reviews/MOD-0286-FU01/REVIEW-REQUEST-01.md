# MOD-0286-FU01 — bağımsız inceleme isteği 01 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md` @ sha256 `28fa337d956cbb36fc96294e6b7cb7ee86d1419c684231005eefd0d0164677a3` (revizyon 01)
- **Kaynak commit:** `6dd1f904b891ddd4e705c5e39a85d99ae7250817` · dal `chore/ct/pss-ui-ct-01` (yerel)
- **Risk:** HIGH — tek dilimli paket, CI seçicisi ve taban; ilk onay
- **Önceki sürüm ve incelemeler:** ilk inceleme
- **Yetki:** sahibin sözü "Evet" (2026-10-31, kule sohbeti: paket ekini hazırla)
- **İsteyen / hazırlayan / tarih:** PSS-UI Control Tower · CT · UI Design System · 2026-10-31
- **İnceleyen:** yeni bir Codex oturumu · o oturumun en yüksek modeli ve effort'u · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0286-FU01-review-6dd1f904b.zip` · sha256 `20ee8ee99f8c339f94326f9ce1c6fbb92dfff31463217605528ff637bd35b5f6` · 5043487 bayt · 212 dosya + `MANIFEST.sha256`
  - Yeri: `/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/inceleme-fu01-01/MOD-0286-FU01-review-6dd1f904b.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286-FU01/2026-10-31-28fa337d.md`
- **Prompt bloğunun SHA-256'sı:** `9cb8d71b613ef7f390c178d7602aead814bbc137c3968e931b1c283e89a9d102`
- **Ön inceleme:** 1 tur, engelleyici 0, yazar tek geçişte düzeltti. Prompt şablondaki metin değil, CT'nin ayrıntılı listesiydi (kart §1.1/1'den sapma); bulgular `F-` önekliydi. Bulgular bu prompt'a eklenmedi.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286-FU01 module pack bağımsız incelemesi 01 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md @ sha256 28fa337d956cbb36fc96294e6b7cb7ee86d1419c684231005eefd0d0164677a3 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Görev: Paketi, bir işçinin yalnız bu metinle doğru iş teslim edip edemeyeceği ve kabul ölçütlerinin betikle ölçülüp ölçülemeyeceği açısından incele. Paketin içeriği, üst paketle (ZIP'te MOD-0286-ui-design-system-page-pattern-governance.md) ilişkisi, seçici kodu (scripts/uic_changed_pages.py) ve taban (tests/ui-compliance-baseline.tsv) karşısında iddiaları ile onay ve kabul araçlarının bu ayrı paketi kabul edip etmeyeceği kapsamdadır. Tüm görünümler ZIP'te değildir; görünümlerdeki script başvuruları docs/records/audits/2026-10/mod-0286-fu01-01/olcum/gorunum-script-basvurulari.tsv dosyasındadır, açılmayan dizinler DIRECTORIES.txt'dedir. ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz.
Çıktı: ilk satır `hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md @ 28fa337d956cbb36fc96294e6b7cb7ee86d1419c684231005eefd0d0164677a3`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, paket satırı ve dayanak dosya:satır> | açık | - | 2026-10-31`;
son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL.
ZIP'in yeri: /private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/inceleme-fu01-01/MOD-0286-FU01-review-6dd1f904b.zip
ZIP sha256: 20ee8ee99f8c339f94326f9ce1c6fbb92dfff31463217605528ff637bd35b5f6
```
