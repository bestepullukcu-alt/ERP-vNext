# MOD-0023 — bağımsız inceleme isteği 07 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md` @ sha256 `ec942baaac95cb850962d8fc914fcf4c2d0379ef36581ecf0c7d23c70500b70d` (§17 revizyon 3)
- **Kaynak commit:** `9b02691e1` · dal `chore/ct/pss-wf-ct-01` (yerel)
- **Risk:** HIGH (kart: `excerpts/ct-pack-approval-card.txt`) — paket onaylı revizyon 2'den (803fbaa5…) sonra değişti.
- **Önceki sürüm ve incelemeler:** revizyon 3: `2026-10-29-3e802d2f` UYGUN DEĞİL, `2026-10-30-5512cbca` UYGUN DEĞİL, `2026-10-30-d8a191fe` KOŞULLU (F-1 ORTA). Bu istek `REVIEW-REQUEST-06.md`'nin yerine geçer (o ZIP geçersiz; Codex'e verilmedi).
- **Bu sürümün nedeni:** `2026-10-30-d8a191fe` F-1 düzeltildi — §17.9 madde 18 (AC-W9); fark `excerpts/MOD-0023-pack-diff-d8a191fe-to-ec942baa.diff`. S-93 ön incelemesi yapıldı (ENGEL yok); bulguları Codex'e verilmez.
- **Yetki:** sahibin seçimi «Düzelt (Önerilen)» (2026-10-30; `mod0023-s17-owner-decisions-03.md` S17-5)
- **İsteyen / hazırlayan / tarih:** depo sahibi · CT · İş akışı · 2026-10-30
- **İnceleyen:** yeni bir Codex oturumu · en güçlü model, effort high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0023-review-07-9b02691e1.zip` · sha256 `41d776b5a923d5949284dcd14bafd6837c780acb8e96b65328465c5d42e8b1a2` · 588 661 bayt · 130 dosya + `MANIFEST.sha256`
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-07/` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-ec942baa.md`
- **Prompt bloğunun SHA-256'sı:** aşağıdaki ```text bloğunun içeriği (çitler hariç) — CT ölçümü sohbette yazılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0023 module pack bağımsız incelemesi 07 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP MOD-0023-review-07-9b02691e1.zip · sha256 41d776b5a923d5949284dcd14bafd6837c780acb8e96b65328465c5d42e8b1a2 · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 ec942baaac95cb850962d8fc914fcf4c2d0379ef36581ecf0c7d23c70500b70d — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Ne değişti — yalnız §17.9 ve Teslim dilimleri (fark: excerpts/MOD-0023-pack-diff-d8a191fe-to-ec942baa.diff; onaylı revizyon 2'den: excerpts/MOD-0023-pack-diff-803fbaa5-to-ec942baa.diff). Kapandı iddiasındaki önceki bulgular — docs/records/reviews/MOD-0023/2026-10-30-d8a191fe.md F-1. Her iddiayı kendin ölç.
Görev: hedef paketin §17'sini (17.1–17.9) ve Teslim dilimlerini ZIP'teki kaynaklara (services/**, frontend/**, karar kayıtları, approvals.tsv, kart S-85 ve E1–E5) karşı statik olarak incele: önceki bulgu kapandı mı; kabul ölçütleri kodla tutarlı ve ölçülebilir mi; izinli yollar yeterli mi; paket kendisiyle çelişiyor mu; değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter) değişmiş mi. Paket açık sahip kararları içerir (O-S17-1, O-S17-3); açık olmaları tek başına bulgu değildir. Kod koşamazsın; her bulgu dosya:satır taşır.
Çıktı: ilk satır `hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ ec942baaac95cb850962d8fc914fcf4c2d0379ef36581ecf0c7d23c70500b70d`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin> | açık | <K…> | <tarih>`; ardından "Kapananlar (inceleme 2026-10-30-d8a191fe): <düz metin>"; son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL. Kişi adı ve e-posta yazma; satır başında F- yalnız bulgu satırında.
```
