# MOD-0280-FU02 — bağımsız inceleme isteği 11 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `047664247337a7ade6495a4ae86415170d7626e04074e44cf9d96b0cae365b0b` (revizyon 10)
- **Kaynak commit:** `69a3de1ff` · dal `chore/ct/hcm-leave-ct-01` (yerel)
- **Risk:** HIGH — onaylı revizyon 08'den sonra değişti; değişen metin yetki verme, AuthService'e ek ve denetim
- **Önceki sürüm ve incelemeler:** revizyon 08 onaylı (main); revizyon 09 inceleme 10 UYGUN (`docs/records/reviews/MOD-0280-FU02/2026-10-30-43c03832.md`), onaylanmadı
- **Bu sürümün nedeni:** revizyon 09'un "izin anahtarlarını rol şablonu vermez" cümlesi yanlış çıktı; üç İK okuma anahtarı açık izin listesine alınıyor (yeni dilim DL-0) ve ilk iş paketinin okumasının bulduğu boşluklar kapanıyor. Ön inceleme 1 (alt ajan, kayıt değil): UYGUN DEĞİL, bir engel; düzeltildi. Ön inceleme 2: KOŞULLU, engel yok; bulgular tek geçişte düzeltildi.
- **Yetki:** sahibin seçimi "Yalnız açık izin" (2026-10-31, butonlu soru); XMC-001 kaydı `docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md`
- **İsteyen / hazırlayan / tarih:** sahip · CT · İzin ve devamsızlık · 2026-10-31
- **İnceleyen:** yeni bir Codex oturumu · en yüksek model ve effort · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0280-FU02-review-69a3de1ff-since-e0af72cef.zip` · sha256 `85a29e87f2c7841cb3fe321c5d196aa77392be7e4267c6186b05ace9cf375459` · 3606009 bayt · 149 dosya + `MANIFEST.sha256`
  - Yeri: `C:\dev\ct-teslim\MOD-0280-FU02-review-11\MOD-0280-FU02-review-69a3de1ff-since-e0af72cef.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0280-FU02/2026-10-31-04766424.md`
- **Prompt bloğunun SHA-256'sı:** `93639018a413bc448bd88beaebe0373f56c178d9c86448e7bf61dacffc8b1392`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 11 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 047664247337a7ade6495a4ae86415170d7626e04074e44cf9d96b0cae365b0b — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Yeniden inceleme. Taban: revizyon 09, hedef 43c03832d2c83761d697f8117174204920758848eff1efb9ee549e8b0775bf33, inceleme 10 sonucu UYGUN (docs/records/reviews/MOD-0280-FU02/2026-10-30-43c03832.md); o sürüm onaylanmadı. Main'deki onaylı sürüm revizyon 08'dir (e9fc812297714a1d402b1e0db8ecc42336cd6b8b739ceadf1c055cf092fb6168).
Ne değişti (revizyon 10): Ownership and Boundaries'te izinler satırı; Owned Objects'te bir komut; Entity Fields'ta etiket, yıl ve azaltma tabanı cümlesi; Ports tablosunda ILeaveEmployeeGateway ve LeaveRequestWriter satırları; Repo Scope ve Protected Paths'te AuthService DL-0 yolları; Dependencies'e bir satır; Runtime Constraints 3; Layout & Shell Contract'ta sayfa anahtarları; Backend File Convention'da Adapters satırı; Validation Rules'ta Code, Category, etiket, tür kimliği, hak ve yazım sonucu satırları; Failure Path to Verify'da üç madde; Authorization Convention'da anahtar listesi, anahtarların kullanıcıya ulaşma maddesi ve onaylayan maddesi; Audited Events tablosunda iki satır ve iş olayı ile boru hattı kaydı maddesi; AC-1, AC-14, AC-17, AC-27, AC-28, AC-32, AC-38, AC-50, AC-56 metinleri ve yeni AC-58, AC-59, AC-60; Test Expectations'ta iki satır; Teslim dilimleri'nde DL-0 satırı, kapısı, kayıt kapısı ve DL-5 ile DL-7 maddesi; Ready-for-dev Checklist; Follow-up Items'ta iki madde; Recorded Deviations'ta R-7, R-8, R-9; başlıkta ve Implementation Notes'ta Revision 10 paragrafı. Fark: CHANGES.diff.
Kapandı iddiasındaki önceki bulgular: yok (inceleme 10 bulgusuz).
Yeni izin kaydı: docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md.
Kod kanıtı: ZIP'teki services/ dosyaları ve DIRECTORIES.txt commit 69a3de1ff'den alındı; paketin yol ve kod iddialarını bunlara karşı ölç.

Görev: bu sürüm ready-for-dev olarak onaylanabilir mi? Farkın beyan edilenden fazlasını değiştirip değiştirmediğini; izin anahtarlarının kullanıcıya ulaşma yolları hakkındaki iddiaların ZIP'teki AuthService ve Platform koduna uyup uymadığını; DL-0 yollarının ve üç anahtarın yeterli ve sınırlı olup olmadığını; yeni doğrulama satırlarının ve denetim maddesinin ZIP'teki koda ve paketin geri kalanına uyup uymadığını; değişen bölümlerin .antigravity/rules/module-pack-standard.md, .antigravity/rules/permission-key-standard.md, .antigravity/rules/cross-module-change-consent.md ve .antigravity/rules/audit-trail-standard.md ile tutup tutmadığını ölç. Kod koşamazsın; ZIP'teki metne karşı statik ölç. ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 047664247337a7ade6495a4ae86415170d7626e04074e44cf9d96b0cae365b0b
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b, 878a24f0, 775ce53c
F-82 | ENGEL | <bulgu; paket bölümü ve dayanak dosya> | açık | - | -
F-83 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-84 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Kapananlar: <yoksa "yok", düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: bulgu numaraları F-82'den başlar; seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
ZIP'in yeri: C:\dev\ct-teslim\MOD-0280-FU02-review-11\MOD-0280-FU02-review-69a3de1ff-since-e0af72cef.zip
ZIP sha256: 85a29e87f2c7841cb3fe321c5d196aa77392be7e4267c6186b05ace9cf375459
```
