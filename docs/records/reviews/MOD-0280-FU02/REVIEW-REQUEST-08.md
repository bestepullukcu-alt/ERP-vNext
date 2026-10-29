# MOD-0280-FU02 — bağımsız inceleme isteği 08 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `952d643d32348670632621fc151966dbfb824b8cb6ed74fbb706b608af6788aa` (revizyon 08, `status: ready-for-dev`)
- **Kaynak commit:** bu istekle aynı commit · dal `chore/ct/hcm-leave-ct-01` (sahibin sözüyle commit edilir; ZIP'teki dosyalar o commit'teki dosyalarla bayt bayt aynıdır, CT commit'ten sonra MANIFEST'i yeniden ölçer)
- **Risk:** HIGH (ilk onay) — onay satırı bu sürüme yazılacağı için bu sürümün kendi incelemesi gerekli (kart S-49: onay satırı künyesi `ready-for-dev` olan sürüme yazılır; künye değişikliği muaf değildir)
- **Önceki sürüm ve incelemeler:** inceleme 01 (`ea61e8c6`), 02 (`47df0fc4`), 03 (`436bc38b`), 04 (`5fb4d1bb`) UYGUN DEĞİL; 05 (`948f931b`) ve 06 (`878a24f0`) KOŞULLU; 07 (`0517be85`, dal ucu 4f5a9ee97) UYGUN, açık bulgu yok
- **Bu sürümün nedeni:** künye `status: draft` → `ready-for-dev` ve giriş durum uyarısı; Ready-for-dev Checklist işaretlendi ve dilim kapıları ayrı listelendi; §Teslim dilimleri kapı metinleri; Implementation Notes'a Revision 08 notu; HCM domain-config'e MOD-0280-FU02 satırı
- **Yetki:** sahibin sözü "rev08-hazir" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · CT · İzin ve devamsızlık · 2026-10-29
- **İnceleyen:** yeni bir Codex oturumu · en güçlü model, effort high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0280-FU02-review-08.zip` · sha256 `ba344da23754d1ee260f4e1609c606827b0b41c60ede3c4e9de7b211e090bc14` · 197033 bayt · 14 dosya + `MANIFEST.sha256`
  - Yeri: `C:\dev\ERP-vNext\.claude\worktrees\hr-leave-ct-01\.ct-teslim\MOD-0280-FU02-review-08\` (depoya girmez)
  - E-posta taraması: `scripts/pack_approval.py` içindeki `@x.invalid` test adresleri dışında aday yok
- **Bulgu dosyası:** `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-952d643d.md`
- **Prompt bloğunun SHA-256'sı:** `be861bbe6c321f35cf286e2e15fcb2472e67d802304f356e01ca25b3b33c5901` (ilk satır dışındaki kısım `90868c62…`; S-93 ön incelemesi aynı kısmı alır)

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 08 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ZIP MOD-0280-FU02-review-08.zip · sha256 ba344da23754d1ee260f4e1609c606827b0b41c60ede3c4e9de7b211e090bc14 · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 952d643d32348670632621fc151966dbfb824b8cb6ed74fbb706b608af6788aa — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Yeniden inceleme. Taban: inceleme 07, hedef 0517be855f30835cb773ba71c3911ded3105a99707fea451292793174d3b608f, sonuç UYGUN, açık bulgu yok (docs/records/reviews/MOD-0280-FU02/2026-10-29-0517be85.md). Taban metni ZIP'te: excerpts/MOD-0280-FU02-pack-rev07-reviewed-0517be85.md.
Ne değişti: künye status draft yerine ready-for-dev ve paketin girişindeki durum uyarısı; Ready-for-dev Checklist işaretlendi, bazı madde metinleri değişti ve dilim kapıları ayrı bir listeye alındı; Teslim dilimleri bölümündeki kapı metinleri değişti; Implementation Notes'a Revision 08 notu ve başlığa Revision 08 paragrafı eklendi; execution/domains/human-capital-management/domain-config.md'ye MOD-0280-FU02 satırı eklendi. Farklar: excerpts/pack-diff-0517be85-to-952d643d.diff ve excerpts/domain-config-diff-4f5a9ee97-to-rev08.diff.
Kapandı iddiasındaki önceki bulgular: yok. Her iddiayı kendin ölç.

Görev: bu sürüm ready-for-dev olarak onaylanabilir mi? Farkın beyan edilenden fazlasını değiştirip değiştirmediğini; her işaretli checklist maddesinin paketin kendi içeriğiyle karşılanıp karşılanmadığını; açık bırakılan maddelerin ready-for-dev koşulu mu yoksa dilim kapısı mı olduğunu (paketin Teslim dilimleri bölümü, Open Questions, ADR-005, .antigravity/rules/module-pack-standard.md, kartın S-90 kuralı, S-49: scripts/pack_approval.py DRAFT_WARNING ve docs/records/audits/2026-10/ct-setup-test-01/FINDINGS.md); domain-config satırının paket ve ADR-005 ile tutup tutmadığını ölç. Kod koşamazsın; ZIP'teki metne karşı statik ölç.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 952d643d32348670632621fc151966dbfb824b8cb6ed74fbb706b608af6788aa
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b, 878a24f0
F-76 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-77 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-78 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: bulgu numaraları F-76'dan başlar; seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
