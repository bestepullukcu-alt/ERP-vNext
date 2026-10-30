# MOD-0023 — bağımsız inceleme isteği 06 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md` @ sha256 `373e4af7a9a68921ba76d37d6183168acfdc28344f5b66fbfded1d579b645692` (§17 revizyon 3)
- **Kaynak commit:** `d6e7f3d60` · dal `chore/ct/pss-wf-ct-01` (yerel)
- **Risk:** HIGH (kart: `excerpts/ct-pack-approval-card.txt`) — paket onaylı revizyon 2'den (803fbaa5…) sonra değişti.
- **Önceki sürüm ve incelemeler:** revizyon 3 incelemeleri `2026-10-29-3e802d2f` UYGUN DEĞİL, `2026-10-30-5512cbca` UYGUN DEĞİL, `2026-10-30-d8a191fe` KOŞULLU (F-1 ORTA).
- **Bu sürümün nedeni:** `2026-10-30-d8a191fe` F-1 düzeltildi — §17.9 madde 18 (AC-W9); fark `excerpts/MOD-0023-pack-diff-d8a191fe-to-373e4af7.diff`. S-93 ön incelemesi yapıldı; bulguları Codex'e verilmez.
- **Yetki:** sahibin seçimi «Düzelt (Önerilen)» (2026-10-30; kayıt `mod0023-s17-owner-decisions-03.md` S17-5)
- **İsteyen / hazırlayan / tarih:** depo sahibi · CT · İş akışı · 2026-10-30
- **İnceleyen:** yeni bir Codex oturumu · en güçlü model, effort high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0023-review-06-d6e7f3d60.zip` · sha256 `3b4f0c11d22eb80fc5cc5450aa48bc911f3e64beb5fbf3fb8192e9c398aacab8` · 586 555 bayt · 130 dosya + `MANIFEST.sha256`
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-06/` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-373e4af7.md`
- **Prompt bloğunun SHA-256'sı:** aşağıdaki ```text bloğunun içeriği (çitler hariç) — CT ölçümü sohbette yazılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0023 module pack bağımsız incelemesi 06 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP MOD-0023-review-06-d6e7f3d60.zip · sha256 3b4f0c11d22eb80fc5cc5450aa48bc911f3e64beb5fbf3fb8192e9c398aacab8 · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 373e4af7a9a68921ba76d37d6183168acfdc28344f5b66fbfded1d579b645692 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Ne değişti — yalnız §17.9 ve Teslim dilimleri (fark: excerpts/MOD-0023-pack-diff-d8a191fe-to-373e4af7.diff; onaylı revizyon 2'den: excerpts/MOD-0023-pack-diff-803fbaa5-to-373e4af7.diff). Kapandı iddiasındaki önceki bulgular — docs/records/reviews/MOD-0023/2026-10-30-d8a191fe.md F-1. Her iddiayı kendin ölç.
Görev: hedef paketin §17'sini (17.1–17.9) ve Teslim dilimlerini ZIP'teki kaynaklara (services/**, frontend/**, karar kayıtları, approvals.tsv, kart S-85 ve E1–E5) karşı statik olarak incele: önceki bulgu kapandı mı; kabul ölçütleri kodla tutarlı ve ölçülebilir mi; izinli yollar yeterli mi; paket kendisiyle çelişiyor mu; değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter) değişmiş mi. Paket açık sahip kararları içerir (O-S17-1, O-S17-3); açık olmaları tek başına bulgu değildir. Kod koşamazsın; her bulgu dosya:satır taşır.
Çıktı: ilk satır `hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ 373e4af7a9a68921ba76d37d6183168acfdc28344f5b66fbfded1d579b645692`; her bulgu tek satırda `F-n | ENGEL|ORTA|DÜŞÜK | <metin> | açık | <K…> | <tarih>`; ardından "Kapananlar (inceleme 2026-10-30-d8a191fe): <düz metin>"; son satırda sonuç UYGUN / KOŞULLU / UYGUN DEĞİL. Kişi adı ve e-posta yazma; satır başında F- yalnız bulgu satırında.
```
