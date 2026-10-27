# MOD-0286 — bağımsız inceleme isteği 13 (SOP §20.4 / CT-APR-02; dilim başına gerekli kanıt seviyesi)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `ce134e13b49751c4ba10bf0aefaaf563c5bf431b1697321e1f12212e87e00742` (revizyon 13)
- Kaynak commit: abb16f58e (dal `chore/ct/pss-ui-ct-01`; main `3a607547e` dal-ilk birleştirmeyle alındı, 649f41ca5; yerel, push edilmedi)
- Önceki: rev 12 `89333f90…` — inceleme 12 **UYGUN** (kayıt `2026-10-26-89333f90.md`), main'de onaylı (PR #245, merge c51568aa8, onaylayan bestepullukcu-alt).
- Rev 13 = rev 12 + yalnız şunlar (sahip kararı `rev13`, 2026-10-27): "Teslim dilimleri" tablosuna dördüncü sütun `gerekli kanıt seviyesi` (DL-1 `E1`, DL-2 `E1`, DL-3 `E2`); başlık bandında "revision 13" ve tek cümle; Implementation Notes'a "Rev 13" maddesi (gerekçe ve PSS-UI-P1-03 doğrulamasının iki ölçüm notu). Neden: `scripts/acceptance_record.py:587-631` ve SOP §23.1 (`control-tower-sop.md:1870`) gerekli kanıt seviyesini onaylı paketin tek seviye belirteci taşıyan bir satırında ister; rev 12'de böyle satır yoktu ve PSS-UI-P1-03 (DL-1 + DL-2; bağımsız doğrulama UYGUN) kabul kaydı yazılamadı.
- **CT ön denetimi (gönderilmeden önce):** `excerpts/rev13-ct-precheck/precheck-output.txt` — seviye belirteci yalnız üç dilim satırında (819-821, her birinde tek belirteç) ve Rev 13 notunda (793; bu satır kaynak gösterilmez); `slices()` = DL-1, DL-2, DL-3; UIC (32) ve RC (24) satırları rev 12 ile bayt bayt aynı; `pack_approval.py check` "paket onaydan sonra değişti — yeniden onay gerekli" (beklenen).
- Fark: `excerpts/MOD-0286-pack-diff-db61cf944-to-abb16f58e.diff`; önceki sürüm `excerpts/MOD-0286-pack-previous-version-db61cf944.md`. Öteki `excerpts/` dosyaları inceleme 12 teslimatındaki durağan alıntılardır (değişmedi).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-27 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-13-abb16f58e.zip` · sha256 `7c237d062137660c5fa1f06472ebfbf09c92c3ba5a793edf15f605668df6412b` · 1 887 658 bayt · 202 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-13/MOD-0286-review-13-abb16f58e.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-ce134e13.md`; yeni bulgular F-25'ten.
- Sonra: bulgu dosyası commit → `review-row` 13 → `check` kuru koşu → tek onay satırı en sonda, dalın kendi çizgisinde → push ve PR sahibin sözüyle ((a) zorunlu PR; PSS-UI-P1-03 işi de aynı PR'da), bestepullukcu-alt doğrudan merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 13 (SOP §20.4 / CT-APR-02; dilim başına gerekli kanıt seviyesi). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-13-abb16f58e.zip · sha256 7c237d062137660c5fa1f06472ebfbf09c92c3ba5a793edf15f605668df6412b · kaynak commit abb16f58e (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 ce134e13b49751c4ba10bf0aefaaf563c5bf431b1697321e1f12212e87e00742 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 13'tür: rev 12 (89333f90…, senin inceleme 12'n UYGUN; main'de onaylı) üzerine yalnız dilim başına gerekli kanıt seviyesi eklenir. İddia edilen değişiklikler: "Teslim dilimleri" tablosuna dördüncü sütun (DL-1 E1, DL-2 E1, DL-3 E2), başlık bandında "revision 13" cümlesi, Implementation Notes'ta "Rev 13" maddesi. Başka değişiklik iddiası yoktur. Fark: excerpts/MOD-0286-pack-diff-db61cf944-to-abb16f58e.diff; CT ön denetimi: excerpts/rev13-ct-precheck/precheck-output.txt.

Önce oku: inceleme 12 kaydı (docs/records/reviews/MOD-0286/2026-10-26-89333f90.md); pack diff; hedef paketin "## Teslim dilimleri", "## Implementation Notes" Rev 13 maddesi ve başlık bandı; scripts/acceptance_record.py satır 580-631 (gerekli seviyenin kaynağı: onaylı paket satırı, tek belirteç); docs/guides/operations/control-tower-sop.md §23 (E0…E5 tablosu, satır 1593-1603) ve satır 1870; emsal execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md "Teslim dilimleri" tablosu; PSS-UI-P1-03 doğrulama kaydı docs/records/audits/2026-10/pss-ui-p1-03-dogrulama-01/DOGRULAMA.md ve DoR kaydı pss-ui-p1-03-dor-01/DOR.md.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Seviyeler doğru mu: DL-1 ve DL-2 (belgeler; durağan ölçüm + run_record kaydı) için E1, DL-3 (seçici betik, --self-test, sabotaj koşuları, kabul edilen commit'te CI işi koşusu — AC-17 bölüm 2) için E2, SOP §23 tablosuna ve paketin kendi AC-17…AC-20 ve Test Expectations metnine göre savunulabilir mi; daha yüksek bir seviye gerekiyor mu (ör. DL-3 CI koşusu E3 mü)?
- Araç uyumu: her dilim satırında tam bir seviye belirteci var mı; acceptance_record.py'nin kaynak satırı kuralını (tek belirteç, gerekli seviyeyle aynı) bu satırlar karşılıyor mu; Rev 13 notu (satır 793) birden çok belirteç taşıdığı için kaynak gösterilemez — paket bunu karıştırmaya açık mı?
- Dilim tablosu: üçüncü sütun ("kapsadığı bölümler") rev 12 ile aynı mı; pack_approval slices() kuralına (hücre sayısı başlıkla eşit, bölüm adları başlık) uyuyor mu?
- Rev 13 başka bir şeyi değiştirdi mi (künye, UIC, RC, AC-1…AC-20, D, FU, DL-3 tasarımı, desen bloğu aynı mı)? Rev 13 notundaki iki ölçüm (AGENTS.md MOD-0013 etiketi bugün :140; RC-11 2026-10-27'de rc=0) doğru aktarılmış mı ve AC/RC metniyle çelişiyor mu?
- Bulgu varsa F-25'ten başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ ce134e13b49751c4ba10bf0aefaaf563c5bf431b1697321e1f12212e87e00742
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b, fca60c96, d478079a, 7feec1de
F-25 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-13/MOD-0286-review-13-abb16f58e.zip
sha256 7c237d062137660c5fa1f06472ebfbf09c92c3ba5a793edf15f605668df6412b
```
