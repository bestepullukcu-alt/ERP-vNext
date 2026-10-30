# MOD-0023 — bağımsız inceleme isteği 05 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md` @ sha256 `d8a191fe0572c7aa4055d87f60d6983c5cd25c442e844a8474070f1a938ad387` (§17 revizyon 3)
- **Kaynak commit:** `fbee8cc93` · dal `chore/ct/pss-wf-ct-01` (yerel)
- **Risk:** HIGH (kart: `excerpts/ct-pack-approval-card.txt`) — paket onaylı revizyon 2'den (803fbaa5…) sonra değişti; yeni onay için bağımsız inceleme gerekli.
- **Önceki sürüm ve incelemeler:** revizyon 2 onaylı (approvals.tsv:16; ana dalda onay geçersiz sayılıyor: satırı ekleyen ve birleştiren aynı kişi). Revizyon 3 incelemeleri: `2026-10-29-3e802d2f` UYGUN DEĞİL, `2026-10-30-5512cbca` UYGUN DEĞİL. Bu istek `REVIEW-REQUEST-rev3-04.md`'nin yerine geçer (o ZIP geçersiz).
- **Bu sürümün nedeni:** `2026-10-30-5512cbca`'nın üç bulgusu düzeltildi (fark: `excerpts/MOD-0023-pack-diff-5512cbca-to-d8a191fe.diff`). S-93 ön incelemesi yapıldı (ENGEL yok); bulguları Codex'e verilmez.
- **Yetki:** sahibin sözü "duzelt" (2026-10-30; kayıt `mod0023-s17-owner-decisions-02.md` S17-4)
- **İsteyen / hazırlayan / tarih:** depo sahibi · CT · İş akışı · 2026-10-30
- **İnceleyen:** yeni bir Codex oturumu · en güçlü model, effort high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0023-review-05-fbee8cc93.zip` · sha256 `16d517a715579c5f41b665622a83a3e8ebd04d86d96a88daf9a4e476e971fcd2` · 575 581 bayt · 127 dosya + `MANIFEST.sha256`
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-05/` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-d8a191fe.md`
- **Prompt bloğunun SHA-256'sı:** aşağıdaki ```text bloğunun içeriği (açılış ve kapanış çitleri hariç) — CT ölçümü sohbette yazılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0023 module pack bağımsız incelemesi 05 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP MOD-0023-review-05-fbee8cc93.zip · sha256 16d517a715579c5f41b665622a83a3e8ebd04d86d96a88daf9a4e476e971fcd2 · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 d8a191fe0572c7aa4055d87f60d6983c5cd25c442e844a8474070f1a938ad387 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Ne değişti — yalnız §17.9 ve Teslim dilimleri (fark: excerpts/MOD-0023-pack-diff-5512cbca-to-d8a191fe.diff; onaylı revizyon 2'den: excerpts/MOD-0023-pack-diff-803fbaa5-to-d8a191fe.diff). Kapandı iddiasındaki önceki bulgular — docs/records/reviews/MOD-0023/2026-10-30-5512cbca.md F-1, F-2, F-3. Her iddiayı kendin ölç.
Görev: hedef paketin §17'sini (17.1–17.9) ve Teslim dilimlerini ZIP'teki kaynaklara (services/**, frontend/**, karar kayıtları, approvals.tsv, kart S-85 ve E1–E5) karşı statik olarak incele: önceki bulgular kapandı mı; kabul ölçütleri kodla tutarlı ve ölçülebilir mi; izinli yollar yeterli mi; paket kendisiyle çelişiyor mu; değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter) değişmiş mi. Paket açık sahip kararları içerir (O-S17-1, O-S17-3); açık olmaları tek başına bulgu değildir. Kod koşamazsın; her bulgu dosya:satır taşır.
Çıktı: ilk satır `hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ d8a191fe0572c7aa4055d87f60d6983c5cd25c442e844a8474070f1a938ad387`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin> | açık | <K…> | <tarih>`; ardından "Kapananlar (inceleme 2026-10-30-5512cbca): <düz metin>"; son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL. Kişi adı ve e-posta yazma; satır başında F- yalnız bulgu satırında.
```
