# MOD-0286-FU01 — bağımsız inceleme isteği 02 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md` @ sha256 `065a45b49eab02fccb49681ddf83a1433e825daf4bbaeca9773e5e829fc6c921` (revizyon 02)
- **Kaynak commit:** `5fc6b9d85e84885bd93828118e2fd21bbe3570d6` · dal `chore/ct/pss-ui-ct-01` (yerel)
- **Risk:** HIGH — tek dilimli paket, CI seçicisi ve taban; ilk onay
- **Önceki sürüm ve incelemeler:** revizyon 01 `28fa337d…`, inceleme 01 (Claude alt ajan, Yönetici oturumu başlattı; 1 ENGEL, 4 ORTA, 7 DÜŞÜK; ham metin sha `f66efdf7…`, henüz kayda çevrilmedi: sahibin sözünü bekliyor)
- **Bu sürümün nedeni:** inceleme 01'in ENGEL'i ve ORTA bulguları; ZIP eksikleri
- **Yetki:** sahibin sözü "Evet" (2026-10-31, kule sohbeti: paket ekini hazırla); inceleme yolu: Yönetici duyurusu, sahibin sözü Yönetici sohbetinde (2026-10-31), bu sohbette değil
- **İsteyen / hazırlayan / tarih:** PSS-UI Control Tower · CT · UI Design System · 2026-10-31
- **İnceleyen:** yazar oturumdan bağımsız yeni bir oturum · Opus 5.5 · high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0286-FU01-review-5fc6b9d85-since-e16d4ff7d.zip` · sha256 `87463b0731ca9b82bc2e51a6b928990d799123ed617eb0151c7abce0a9468deb` · 5808929 bayt · 237 dosya + `MANIFEST.sha256`
  - Yeri: `/private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/inceleme-fu01-02/MOD-0286-FU01-review-5fc6b9d85-since-e16d4ff7d.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0286-FU01/2026-10-31-065a45b4.md`
- **Prompt bloğunun SHA-256'sı:** `e8659e5a476d1ced88461db0c66b89e3d296b6ac35f62af00e9e51d19a07e385`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Bağımsız inceleyen — MOD-0286-FU01 module pack bağımsız incelemesi 02 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md @ sha256 065a45b49eab02fccb49681ddf83a1433e825daf4bbaeca9773e5e829fc6c921 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın ikinci satırına yaz; veremiyorsan "verilemiyor" yaz.
Ne değişti: boş farkta tarama satırlarının basılma kuralı ve yeni öz test vakası; SCRIPT ve VARIABLE desenleri ile aralarındaki öncelik, dört yeni öz test vakası; satır sahibinin ölçümü (AC-8); sabotaj sayılarının iş tabanından türetilmesi; AC-10'un ölçümü; "HEAD'de var olan" şartı; lookup cümlesi. ZIP'e araçlar, onay dosyaları, üç kabul kaydı, görünüm ve JS listeleri, bağımsız ölçüm betiği ve çıktıları eklendi; CHANGES.diff önceki sürümden farkı gösterir (1. sürüm taslağından itibaren).
Kapandı iddiasındaki önceki bulgular: F-1, F-2, F-3, F-4, F-5, F-6, F-7, F-8, F-9, F-10. Her iddiayı kendin ölç. Açık bilinenler: paket öncesi kimlik satırı kayıt defterine henüz eklenmedi (paketin O-2'si).
Görev: Paketi, bir işçinin yalnız bu metinle doğru iş teslim edip edemeyeceği ve kabul ölçütlerinin betikle ölçülüp ölçülemeyeceği açısından incele. Paketin içeriği, üst paketle (ZIP'te MOD-0286-ui-design-system-page-pattern-governance.md) ilişkisi, seçici kodu (scripts/uic_changed_pages.py) ve taban (tests/ui-compliance-baseline.tsv) karşısında iddiaları ile onay ve kabul araçlarının bu ayrı paketi kabul edip etmeyeceği kapsamdadır. Görünüm dosyalarının kendisi ZIP'te değildir; görünümlerdeki script başvuruları ve görünüm ile JS listeleri docs/records/audits/2026-10/mod-0286-fu01-01/olcum/ altındadır. ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz.
Çıktı: ilk satır `hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md @ 065a45b49eab02fccb49681ddf83a1433e825daf4bbaeca9773e5e829fc6c921`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin, paket satırı ve dayanak dosya:satır> | açık | <sahip> | <son tarih>`: ENGEL'de sahip `-` ve tarih 2026-10-31; ORTA ve DÜŞÜK'te sahip K3, son tarih 2026-11-07. Yeni bulgular F-13'ten başlar. Satırda bulgu dışında `|` kullanma; bulgu dışı satırda ENGEL, ORTA, DÜŞÜK sözcüklerini büyük harfle yazma.
Son satırda sonuç: açık ENGEL varsa UYGUN DEĞİL; ENGEL yok, ORTA varsa KOŞULLU; ikisi de yoksa UYGUN.
ZIP'in yeri: /private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext/05ce4e4b-9875-47e7-8842-ee9372026895/scratchpad/inceleme-fu01-02/MOD-0286-FU01-review-5fc6b9d85-since-e16d4ff7d.zip
ZIP sha256: 87463b0731ca9b82bc2e51a6b928990d799123ed617eb0151c7abce0a9468deb
```
