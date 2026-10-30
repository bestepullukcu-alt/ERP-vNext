# MOD-0280-FU02 — bağımsız inceleme isteği 10 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `43c03832d2c83761d697f8117174204920758848eff1efb9ee549e8b0775bf33` (revizyon 09)
- **Kaynak commit:** `e0af72cef` · dal `chore/ct/hcm-leave-ct-01` (yerel)
- **Risk:** HIGH — onaylı revizyon 08'den sonra değişti (`pack_approval.py check --ref HEAD`: "paket onaydan sonra değişti — yeniden onay gerekli"); değişen metin yetki verme, Platform'da yazma kapsamı ve XMC-001
- **Önceki sürüm ve incelemeler:** revizyon 08 onaylı (inceleme 09 UYGUN, `docs/records/reviews/MOD-0280-FU02/2026-10-29-e9fc8122.md`; onay satırı origin/main'de, PR bestepullukcu-alt/ERP-vNext#301)
- **Bu sürümün nedeni:** ilk iş paketinin kod ölçümü paketin dosya listesinde var olmayan bir proje ve eksik ortak kayıt dosyaları buldu; sahip seçimi "Paketi düzelt" (2026-10-30). Ön inceleme 1 (alt ajan, kayıt değil): KOŞULLU, engel yok; dört bulgu düzeltildi.
- **Yetki:** sahibin seçimleri "Paketi düzelt" ve "Commit et" (2026-10-30, butonlu soru)
- **İsteyen / hazırlayan / tarih:** sahip · CT · İzin ve devamsızlık · 2026-10-30
- **İnceleyen:** yeni bir Codex oturumu · en yüksek model ve effort · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0280-FU02-review-10.zip` · sha256 `90441ed828a06fad53aa8082f859aff027242f4ec3da754d453595c7061ccd5c` · 357367 bayt · 28 dosya + `MANIFEST.sha256`
  - Yeri: `C:\dev\ERP-vNext\.claude\worktrees\hr-leave-ct-01\.ct-teslim\MOD-0280-FU02-review-10\MOD-0280-FU02-review-10.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0280-FU02/2026-10-30-43c03832.md`
- **Durum:** kapandı (2026-10-30) — Codex sonucu UYGUN, bulgu yok; ham metin sahip ve Yönetici · Ofis yoluyla geldi
- **Prompt bloğunun SHA-256'sı:** `851ec2ad7251204f32cd0840c702debbc17fc8026269d7bab0e55aee9b6c4e58`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 10 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 43c03832d2c83761d697f8117174204920758848eff1efb9ee549e8b0775bf33 — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Yeniden inceleme. Taban: onaylı revizyon 08, hedef e9fc812297714a1d402b1e0db8ecc42336cd6b8b739ceadf1c055cf092fb6168, inceleme 09 sonucu UYGUN (docs/records/reviews/MOD-0280-FU02/2026-10-29-e9fc8122.md). Taban metni ZIP'te: excerpts/MOD-0280-FU02-pack-approved-08-e9fc8122.md.
Ne değişti (revizyon 09): Repo Scope'taki Diten.Platform yolları ve ortak kayıt dosyalarına yalnız satır ekleme maddeleri; Repo Scope'a Web test yolları; Backend File Convention'da depo dosyası; Ownership and Boundaries'te Task Center kartı satırı ve Placement deviation'da XMC-001 durumu; Audited Events'e denetim kategorisi maddesi; Teslim dilimleri, Ready-for-dev Checklist ve Open Questions 1'de XMC-001 kaydı; Follow-up Items'a iki madde; Authorization Convention'da rol verme maddeleri ve AC-28 metni; Ports tablosunda kart bağlantısı; AC-56 koşul notu; Teslim dilimleri ve Ready-for-dev Checklist'te DL-3 MOD-0023 kapısı; başlıkta ve Implementation Notes'ta Revision 09 paragrafı. Fark: excerpts/pack-diff-e9fc8122-to-r09.diff.
Kapandı iddiasındaki önceki bulgular: yok (inceleme 09 bulgusuz).
Kod kanıtı: ZIP'teki services/Diten.Platform/src dosyaları ve excerpts/file-list-diten-platform-and-web-tests@63acdf2ec.txt (dosya listesi) origin/main 63acdf2ec'den alındı; paketin yol ve satır iddialarını bunlara karşı ölç.

Görev: bu sürüm ready-for-dev olarak onaylanabilir mi? Farkın beyan edilenden fazlasını değiştirip değiştirmediğini, yeni yol ve dosya iddialarının ZIP'teki koda ve dosya listesine uyup uymadığını, ortak dosyalara "yalnız satır ekleme" kapsamının yeterli ve sınırlı olup olmadığını, değişen bölümlerin paketin geri kalanıyla, .antigravity/rules/module-pack-standard.md, .antigravity/rules/cross-module-change-consent.md ve .antigravity/rules/audit-trail-standard.md ile tutup tutmadığını ölç. Kod koşamazsın; ZIP'teki metne karşı statik ölç.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 43c03832d2c83761d697f8117174204920758848eff1efb9ee549e8b0775bf33
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b, 878a24f0, 775ce53c
F-78 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-79 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-80 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Kapananlar: <yoksa "yok", düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: bulgu numaraları F-78'den başlar; seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
ZIP'in yeri: C:\dev\ERP-vNext\.claude\worktrees\hr-leave-ct-01\.ct-teslim\MOD-0280-FU02-review-10\MOD-0280-FU02-review-10.zip
ZIP sha256: 90441ed828a06fad53aa8082f859aff027242f4ec3da754d453595c7061ccd5c
```
