# MOD-0251 — bağımsız inceleme isteği 05 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md` @ sha256 `619ec85b1f56a5d91cb5abafe92574e38d8116570a789d7f6d6cbc699ade7526` (revizyon 5)
- **Kaynak commit:** `a1a6526d4243ffc8f2e2626111c5dd74b3a2dd50` · dal `chore/ct/hcm-employee-ct-01` (yerel; push sahibin sözüyle)
- **Risk:** HIGH — paketin ilk onayı; kiracı verisi, kişisel veri, yetki
- **Önceki sürüm ve incelemeler:** revizyon 4 @ `7d04a408…` — inceleme 04 UYGUN (`2026-10-29-7d04a408.md`); önceki olumsuzlar 01 (260d1151), 02 (3ebb4e8d), 03 (a9ac2123)
- **Bu sürümün nedeni:** künyedeki ikinci onay alanı `approval_status` (koşullu P2 onayı) kaldırıldı; işçi kapısı (`scripts/prompt_gate.py`, CT-FIX-04) bu alan `approved` değilken dağıtmıyor. Bu alanı güncel sayan bir kabul satırı düzeltildi; Revizyon 5 notu eklendi
- **Yetki:** sahibin sözü "PR-bekletip-duzelt" ve "revizyon5-commit" (2026-10-29)
- **İsteyen / hazırlayan / tarih:** depo sahibi · CT · Çalışan kaydı · 2026-10-29
- **İnceleyen:** yeni bir Codex oturumu · en güçlü mevcut model, effort high · yanıtın başına model, effort, oturum kimliği
- **ZIP:** yok — kaynak GitHub dalındaki sabit commit (incelemeler 02–04 gibi)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0251/<YYYY-AA-GG>-619ec85b.md`
- **Prompt bloğunun SHA-256'sı:** `10c43907f14839b546bfd5d990b383f7d54858f68597615c283db7cff913fcc0`
- **Ön inceleme (S-93):** prompt'un ilk satırı "Hedef: ön inceleme N" yapılarak taze, ipucusuz, salt-okunur alt ajana verilir; sonuç kayda girmez. Koşulan: tur 1 (commit 20ef65a76) ve tur 2 (commit c8c1fc0a0), ikisi KOŞULLU, ENGEL yok — durma koşulu sağlandı; bulgular yalnız yazara gitti, bu sürümde düzeltildi.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0251 Core HR / Employee Master module pack bağımsız incelemesi 05 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, staging, commit, push, PR yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: depo bestepullukcu-alt/ERP-vNext, dal chore/ct/hcm-employee-ct-01, commit a1a6526d4243ffc8f2e2626111c5dd74b3a2dd50. Belgeleri ve kodu bu commit'ten oku (git show / git diff); bu commit'e erişemiyorsan dur ve bildir.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md @ sha256 619ec85b1f56a5d91cb5abafe92574e38d8116570a789d7f6d6cbc699ade7526 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.
Yeniden inceleme. Ne değişti: revizyon 4 (sha256 7d04a408051d79d4c0075522b7eda41dcc84860e1ab1f4770ee5f020c1633d9d, commit 111b1b4c6) ile bu sürüm arasındaki fark — git diff 111b1b4c6 a1a6526d4 -- <hedef belge>. Kapandı iddiasındaki önceki bulgular: yok (inceleme 04, docs/records/reviews/MOD-0251/2026-10-29-7d04a408.md, açık bulgu bırakmadı). Her iddiayı kendin ölç.
Görev: hedef belgeyi bu sürümüyle bağımsız incele (SOP §20.4); değişikliğin paketin kendisiyle, depo kurallarıyla ve araçlarla tutarlılığı dahil. Derleme ve test koşmadıysan öyle yaz; ölçtüklerini sonda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.
Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
MOD-0251 — bağımsız inceleme
Model: <model>. Effort: <effort>. Oturum kimliği: <kimlik ya da verilemiyor>.
hedef: execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md @ 619ec85b1f56a5d91cb5abafe92574e38d8116570a789d7f6d6cbc699ade7526
değerlendirilen olumsuz incelemeler: 260d1151, 3ebb4e8d, a9ac2123
F-27 | ENGEL|ORTA|DÜŞÜK | <bulgu; paket satır numarası ve dayanak dosya:satır> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: bulgu varsa F-27'den numarala; seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; `F-n@özet` yazma; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık); ENGEL satırında sahip ve tarih "-"; açık ORTA ve DÜŞÜK için sahip (rol) ve son tarih (YYYY-AA-GG) yazılır; UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
