# MOD-0280-FU02 — bağımsız inceleme isteği 09 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `e9fc812297714a1d402b1e0db8ecc42336cd6b8b739ceadf1c055cf092fb6168` (revizyon 08'in inceleme 08'den sonra düzeltilmiş hali, `status: ready-for-dev`)
- **Kaynak commit:** bu istekle aynı commit · dal `chore/ct/hcm-leave-ct-01` (sahibin seçimiyle commit edilir; ZIP'teki dosyalar o commit'teki dosyalarla bayt bayt aynıdır, CT commit'ten sonra yeniden ölçer)
- **Risk:** HIGH (ilk onay) — onay satırı bu sürüme yazılacağı için bu sürümün kendi incelemesi gerekli
- **Önceki sürüm ve incelemeler:** inceleme 01–04 UYGUN DEĞİL; 05 ve 06 KOŞULLU; 07 (`0517be85`) UYGUN; 08 (`775ce53c`, dal ucu 11be4fa76) KOŞULLU — F-76 orta, F-77 düşük (kayıt `2026-10-29-775ce53c.md`)
- **Bu sürümün nedeni:** F-76 — `CancelReason` ve devir düzeltmesi nedeni kapalı kod listesi olarak sayıldı, üyelik doğrulaması, geçersiz kod yanıtı ve başarısızlık yolu eklendi; F-77 — §Page Help sayfa başına amaç, yardım anahtarları, adımlar, Immediate ve Related kararlarını yazıyor, AC-19 ve yardım defteri testi bunları ölçüyor; ayrıca Repo Scope yardım ve gezinme dosyalarını, Frontend File Contract İK iptali ve neden seçimlerini, AC-11 ve AC-13 kapalı neden listelerini ölçüyor
- **Yetki:** sahibin seçimi "Şimdi düzelt (Önerilen)" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · CT · İzin ve devamsızlık · 2026-10-29
- **İnceleyen:** yeni bir Codex oturumu · en güçlü model, effort high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0280-FU02-review-09.zip` · sha256 `928c791e2880d5462614818be8fc485652b4c133ceb07986cc0ab77f3f2b0540` · 185645 bayt · 11 dosya + `MANIFEST.sha256`
  - Yeri: `C:\dev\ERP-vNext\.claude\worktrees\hr-leave-ct-01\.ct-teslim\MOD-0280-FU02-review-09\` (depoya girmez)
  - E-posta taraması: `scripts/pack_approval.py` içindeki `@x.invalid` test adresleri dışında aday yok
- **Bulgu dosyası:** `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-e9fc8122.md`
- **Prompt bloğunun SHA-256'sı:** `da440e46089be55cb069a873fd95cfe35ce15a528216997189bca00b61e26f53` (ilk satır dışındaki kısım `bb1b0088…`; S-93 ön incelemesi aynı kısmı alır)

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 09 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP MOD-0280-FU02-review-09.zip · sha256 928c791e2880d5462614818be8fc485652b4c133ceb07986cc0ab77f3f2b0540 · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 e9fc812297714a1d402b1e0db8ecc42336cd6b8b739ceadf1c055cf092fb6168 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Yeniden inceleme. Taban: inceleme 08, hedef 775ce53c3a22bc36072d54262871c15e3b681aef1c986b7aad72f807e24436ea, sonuç KOŞULLU (docs/records/reviews/MOD-0280-FU02/2026-10-29-775ce53c.md). Taban metni ZIP'te: excerpts/MOD-0280-FU02-pack-reviewed-08-775ce53c.md.
Ne değişti: Entity Fields'ta CancelReason satırı; Repo Scope'a üç ortak dosya yolu; Validation Rules'ta HR iptali ve devir satırları; Failure Path to Verify'a bir yol; Layout & Shell Contract altına Page Help bölümü; Frontend File Contract'ta iki ekran maddesi; AC-11, AC-13 ve AC-19'un metni; Test Expectations'ta ön yüz satırı; Ready-for-dev Checklist'te yardım ve lookup maddelerinin metni; Implementation Notes ve başlıktaki Revision 08 paragrafına birer cümle. Fark: excerpts/pack-diff-775ce53c-to-e9fc8122.diff.
Kapandı iddiasındaki önceki bulgular: F-76, F-77 (inceleme 08). Her iddiayı kendin ölç.

Görev: bu sürüm ready-for-dev olarak onaylanabilir mi? Önceki bulguların kapanıp kapanmadığını, farkın beyan edilenden fazlasını değiştirip değiştirmediğini ve değişen bölümlerin paketin geri kalanıyla, .antigravity/rules/module-pack-standard.md, .antigravity/rules/page-help-and-keyboard.md ve .antigravity/rules/platform-lookups-reference-data.md ile tutup tutmadığını ölç. Kod koşamazsın; ZIP'teki metne karşı statik ölç.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ e9fc812297714a1d402b1e0db8ecc42336cd6b8b739ceadf1c055cf092fb6168
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b, 878a24f0, 775ce53c
F-78 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-79 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-80 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Kapananlar (inceleme 08): <kapanan bulgular, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: bulgu numaraları F-78'den başlar; seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
