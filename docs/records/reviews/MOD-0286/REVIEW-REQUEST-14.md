# MOD-0286 — bağımsız inceleme isteği 14 (SOP §20.4 / CT-APR-02; CI-11 yeni yüzey + CI tetikleyicisi)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `87f46dcf035555c7c9c1b02a5b45579c66385a082fd7eeb36a2e49cd3e98dfe3` (revizyon 14)
- Kaynak commit: 1ac03c413 (dal `chore/ct/pss-ui-ct-01`; yerel, push edilmedi)
- Önceki: rev 13 `ce134e13…` — inceleme 13 **UYGUN** (kayıt `2026-10-27-ce134e13.md`), main'de onaylı (PR #263, merge 826150ef4, onaylayan bestepullukcu-alt).
- Rev 14 = rev 13 + yalnız şunlar (sahip kararı `rev14`, 2026-10-27):
  - CI-11 "Runs on" hücresi (satır 459) yalnız **yeni yüzeye** daraltıldı. Yeni yüzey, `Views/{Area}/{Module}/` altında tabanda hiç dosyası olmayan sayfadır. Var olan sayfaya yeniden eklenen ya da taşınan partial CI-11'i koşturmaz.
  - DL-3 bölüm 3 CI tetikleyicisine (satır 433) seçici, taban ve workflow dosyası eklendi.
  - Başlık bandına bir cümle, Implementation Notes'a "Rev 14" maddesi eklendi.
  - Neden: DL-3 işçisi (PSS-UI-P1-04) bir paket çelişkisinde durdu (`docs/records/audits/2026-10/pss-ui-p1-04-durdu-01/DURDU.md`). CI-11 "PR'ın eklediği her partial" diyordu; AC-18 (c) ise silinen `_IndexL10n.cshtml`'in geri alma koşusunda çıkış 0 istiyor (satır 679, 726). Geri alma dosyayı yeniden eklediği için CI-11 kırmızı veriyordu. Tetikleyici eki, PSS-UI-P1-04 prompt incelemesi 5'in 8. bulgusudur.
- **CT ön denetimi:** `excerpts/rev14-ct-precheck/precheck-output.txt`.
  - Seviye belirteci yalnız Rev 13 notunda (793) ve kaydırılmış üç dilim satırında var (820-822, her birinde tek belirteç).
  - `slices()` = DL-1, DL-2, DL-3.
  - UIC (32) ve RC (24) satırları rev 13 ile bayt bayt aynı.
  - CI-11 satırında 6 `|` var; sütun sayısı korundu.
  - `pack_approval.py check` "yeniden onay gerekli" diyor; bu beklenen sonuç.
- Fark: `excerpts/MOD-0286-pack-diff-abb16f58e-to-1ac03c413.diff`; önceki sürüm `excerpts/MOD-0286-pack-previous-version-abb16f58e.md`.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-27 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-14-1ac03c413.zip` · sha256 `bc62f5b35ddab67d34b08544bb8fbc5bdfd8f67978f4911c78e57dcdd0e0dc14` · 1 932 630 bayt · 212 dosya + `MANIFEST.sha256`
  - Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-14/MOD-0286-review-14-1ac03c413.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-87f46dcf.md`; yeni bulgular F-25'ten.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 14 (SOP §20.4 / CT-APR-02; CI-11 yeni yüzey + CI tetikleyicisi). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-14-1ac03c413.zip · sha256 bc62f5b35ddab67d34b08544bb8fbc5bdfd8f67978f4911c78e57dcdd0e0dc14 · kaynak commit 1ac03c413 (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 87f46dcf035555c7c9c1b02a5b45579c66385a082fd7eeb36a2e49cd3e98dfe3 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 14'tür: rev 13 (ce134e13…, senin inceleme 13'ün UYGUN; main'de onaylı) üzerine yalnız iki içerik değişikliği iddia edilir: CI-11 "Runs on" hücresinin yeni yüzeye daraltılması (satır 459) ve DL-3 bölüm 3 CI tetikleyicisine seçici, taban ve workflow dosyasının eklenmesi (satır 433); ayrıca başlık bandı cümlesi ve Implementation Notes "Rev 14" maddesi. Fark: excerpts/MOD-0286-pack-diff-abb16f58e-to-1ac03c413.diff; CT ön denetimi: excerpts/rev14-ct-precheck/precheck-output.txt.

Önce oku: inceleme 13 kaydı (docs/records/reviews/MOD-0286/2026-10-27-ce134e13.md); pack diff; hedef paketin CI tablosu (satır 447-463, özellikle CI-11), DL-3 bölüm 1 ve 3 (satır 318-373, 427-438), AC-17…AC-20 (satır 678-681) ve Test Expectations DL-3 maddeleri (satır 722-734), UIC-6 (satır 170), RC-5 (satır 224); işçinin durma kaydı docs/records/audits/2026-10/pss-ui-p1-04-durdu-01/DURDU.md; DL-3 prompt'u execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P1-04-v1.1.md, prompt incelemeleri docs/records/reviews/MOD-0286/prompts/PSS-UI-P1-04-v1.0-inceleme-01.md (8. bulgu) ve -v1.1-inceleme-01.md; CT kanıt betiği docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit-03.sh (AC-17a ve AC-18 (c) blokları).
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Çelişki gerçekten kapandı mı: yeni CI-11 tanımıyla AC-18 (c)'nin sabotaj koşusu (silinen _IndexL10n.cshtml, CI-3 kırmızı) ve geri alma koşusu (dosya yeniden eklenir, sayfanın tabanda dosyası var → CI-11 koşmaz → çıkış 0) paketle tutarlı mı? Kanıt betiğinin AC-17a bloğundaki yeni sayfa (DevEnablement/UicAra, tabanda dosyası yok) CI-11'i koşturur — bu, AC-17a'nın 0 ya da 1 çıkış kuralıyla ve UIC-6 "new surfaces only" ile tutarlı mı?
- "Yeni yüzey" tanımı yeterince kesin mi: sayfa klasörü yeniden adlandırması (eski sayfa kaldırıldı + yeni sayfa eklendi, satır 352-356), yalnız JS dosyası olan bir sayfa (satır 324), Archive ve Shared klasörleri — her birinde CI-11 koşup koşmadığı belirsiz kalıyor mu?
- Tetikleyici eki: seçici, taban ya da workflow değişince işin koşması gerekli ve yeterli mi; bu dosyalar değişince seçici hangi sayfaları seçer (paylaşılan dosya / sayfa kontrolü yok) — bölüm 1'in eşlemesiyle çelişiyor mu?
- Rev 14 başka bir şeyi değiştirdi mi (künye, UIC, RC, AC-1…AC-20, D, FU, dilim tablosu ve seviyeler, desen bloğu aynı mı)? Kaydırılan dilim satırları (820-822) tek seviye belirteci taşıyor mu?
- Bulgu varsa F-25'ten başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 87f46dcf035555c7c9c1b02a5b45579c66385a082fd7eeb36a2e49cd3e98dfe3
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b, fca60c96, d478079a, 7feec1de
F-25 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-14/MOD-0286-review-14-1ac03c413.zip
sha256 bc62f5b35ddab67d34b08544bb8fbc5bdfd8f67978f4911c78e57dcdd0e0dc14
```
