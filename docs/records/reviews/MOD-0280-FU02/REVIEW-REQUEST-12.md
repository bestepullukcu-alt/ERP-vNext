# MOD-0280-FU02 — bağımsız inceleme isteği 12 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `047664247337a7ade6495a4ae86415170d7626e04074e44cf9d96b0cae365b0b` (revizyon 10, istek 11 ile aynı sürüm)
- **Kaynak commit:** `40c3aaf91` · dal `chore/ct/hcm-leave-ct-01` (yerel)
- **Risk:** HIGH — onaylı revizyon 08'den sonra değişti; değişen metin yetki verme, AuthService'e ek ve denetim
- **Önceki sürüm ve incelemeler:** revizyon 08 onaylı (main); revizyon 09 inceleme 10 UYGUN, onaylanmadı; revizyon 10 inceleme 11 KOŞULLU, tek orta bulgu (`docs/records/reviews/MOD-0280-FU02/2026-10-31-04766424.md`)
- **Bu sürümün nedeni:** paket değişmedi. İnceleme 11'in bulgusu ZIP'te eksik girdiydi; kart §1.1/7 gereği aynı sürüm için bir tur daha. ZIP'e Platform'un doğrulama, yanıt ve taban denetleyici dosyaları eklendi.
- **Yetki:** sahibin seçimi "Yalnız açık izin" (2026-10-31, butonlu soru); kart §1.1/7
- **İsteyen / hazırlayan / tarih:** sahip · CT · İzin ve devamsızlık · 2026-10-31
- **İnceleyen:** yeni bir Codex oturumu · en yüksek model ve effort · yanıtın başına model, effort, oturum kimliği
- **ZIP:** `MOD-0280-FU02-review-40c3aaf91-since-e0af72cef.zip` · sha256 `52f1d223ea8fd8fe1999c9b9d9d04e03344e7f520717bc8770b3d27a5595044c` · 3455224 bayt · 151 dosya + `MANIFEST.sha256`
  - Yeri: `C:\dev\ct-teslim\MOD-0280-FU02-review-12\MOD-0280-FU02-review-40c3aaf91-since-e0af72cef.zip` (depoya girmez)
- **Bulgu dosyası:** `docs/records/reviews/MOD-0280-FU02/2026-10-31-04766424-r12.md`
- **Prompt bloğunun SHA-256'sı:** `5bdb8d4ed8d635c690b4cc9c1b21ef82c0688956da9cbc7145b8ad5b1781a6e3`

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 12 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 047664247337a7ade6495a4ae86415170d7626e04074e44cf9d96b0cae365b0b — tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Yeniden inceleme. Taban: revizyon 09, hedef 43c03832d2c83761d697f8117174204920758848eff1efb9ee549e8b0775bf33, inceleme 10 sonucu UYGUN (docs/records/reviews/MOD-0280-FU02/2026-10-30-43c03832.md); o sürüm onaylanmadı. Main'deki onaylı sürüm revizyon 08'dir (e9fc812297714a1d402b1e0db8ecc42336cd6b8b739ceadf1c055cf092fb6168).
Ne değişti (revizyon 10): Ownership and Boundaries'te izinler satırı; Owned Objects'te bir komut; Entity Fields'ta etiket, yıl ve azaltma tabanı cümlesi; Ports tablosunda ILeaveEmployeeGateway ve LeaveRequestWriter satırları; Repo Scope ve Protected Paths'te AuthService DL-0 yolları; Dependencies'e bir satır; Runtime Constraints 3; Layout & Shell Contract'ta sayfa anahtarları; Backend File Convention'da Adapters satırı; Validation Rules'ta Code, Category, etiket, tür kimliği, hak ve yazım sonucu satırları; Failure Path to Verify'da üç madde; Authorization Convention'da anahtar listesi, anahtarların kullanıcıya ulaşma maddesi ve onaylayan maddesi; Audited Events tablosunda iki satır ve iş olayı ile boru hattı kaydı maddesi; AC-1, AC-14, AC-17, AC-27, AC-28, AC-32, AC-38, AC-50, AC-56 metinleri ve yeni AC-58, AC-59, AC-60; Test Expectations'ta iki satır; Teslim dilimleri'nde DL-0 satırı, kapısı, kayıt kapısı ve DL-5 ile DL-7 maddesi; Ready-for-dev Checklist; Follow-up Items'ta iki madde; Recorded Deviations'ta R-7, R-8, R-9; başlıkta ve Implementation Notes'ta Revision 10 paragrafı. Fark: CHANGES.diff.
Kapandı iddiasındaki önceki bulgular: F-82 (inceleme 11, aynı sürüm, KOŞULLU; docs/records/reviews/MOD-0280-FU02/2026-10-31-04766424.md). Paket değişmedi; ZIP'e Platform'un ValidationBehavior.cs, ValidationReasonCode.cs, ExceptionBehavior.cs, Response.cs ve CustomBaseController.cs dosyaları eklendi. Her iddiayı kendin ölç.
Yeni izin kaydı: docs/records/decisions/2026-10/hcm-leave-xmc001-authservice-consent-01.md.
Kod kanıtı: ZIP'teki services/ dosyaları ve DIRECTORIES.txt commit 40c3aaf91'den alındı; paketin yol ve kod iddialarını bunlara karşı ölç.

Görev: bu sürüm ready-for-dev olarak onaylanabilir mi? Farkın beyan edilenden fazlasını değiştirip değiştirmediğini; izin anahtarlarının kullanıcıya ulaşma yolları hakkındaki iddiaların ZIP'teki AuthService ve Platform koduna uyup uymadığını; DL-0 yollarının ve üç anahtarın yeterli ve sınırlı olup olmadığını; yeni doğrulama satırlarının ve denetim maddesinin ZIP'teki koda ve paketin geri kalanına uyup uymadığını; değişen bölümlerin .antigravity/rules/module-pack-standard.md, .antigravity/rules/permission-key-standard.md, .antigravity/rules/cross-module-change-consent.md ve .antigravity/rules/audit-trail-standard.md ile tutup tutmadığını ölç. Kod koşamazsın; ZIP'teki metne karşı statik ölç. ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 047664247337a7ade6495a4ae86415170d7626e04074e44cf9d96b0cae365b0b
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b, 878a24f0, 775ce53c
F-83 | ENGEL | <bulgu; paket bölümü ve dayanak dosya> | açık | - | -
F-84 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-85 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Kapananlar (inceleme 11): <F-82 kapandı ya da açık kaldı, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: bulgu numaraları F-83'ten başlar; seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
ZIP'in yeri: C:\dev\ct-teslim\MOD-0280-FU02-review-12\MOD-0280-FU02-review-40c3aaf91-since-e0af72cef.zip
ZIP sha256: 52f1d223ea8fd8fe1999c9b9d9d04e03344e7f520717bc8770b3d27a5595044c
```
