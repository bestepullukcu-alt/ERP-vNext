# MOD-0280-FU02 — bağımsız paket incelemesi isteği 07 (SOP §20.4 / CT-APR-02; revizyon 07, ilk onay)

> **Durum: yanıtlandı, kapandı.** Codex dal ucunu 4f5a9ee97 (paket 0517be85) olarak ölçtü: UYGUN, F-67 kapandı (kayıt
> `2026-10-29-0517be85.md`). Bu istek yeniden gönderilmez; yerine `REVIEW-REQUEST-08.md` geçer (S-49: onay satırı
> `ready-for-dev` sürüme yazılır).

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `0517be855f30835cb773ba71c3911ded3105a99707fea451292793174d3b608f` (revizyon 07, `status: draft`)
- Kaynak commit: bu istekle aynı commit (dal `chore/ct/hcm-leave-ct-01`); GitHub'a sahibin sözüyle gönderilir. Uç sha'sı Codex'e mesajda verilir.
- Önceki: inceleme 01 (`ea61e8c6`), 02 (`47df0fc4`), 03 (`436bc38b`), 04 (`5fb4d1bb`) — dördü **UYGUN DEĞİL**; inceleme 05 (`948f931b`) **KOŞULLU**, F-21; inceleme 06 (`878a24f0`) **KOŞULLU**, tek bulgu F-67, F-21 kapandı (CT değerlendirmesi kayıtta: kabul).
- Revizyon 07 yalnız F-67'yi karşılar: kapalı örneği yeniden okuyan `Unconfirmed` talep, saklı niyet `cancel` ise `Cancelled`, değilse `Withdrawn` olur (AC-47, AC-52, iki başarısızlık yolu; MOD-0023 kapanışı ile son Leave yazımı arasındaki kesinti AC-52 kanıtı). Taze bağlamlı alt ajan ara denetiminin notları (F-68…F-75, resmî inceleme değil) paketin "Implementation Notes → Revision 07" bölümünde.
- İstek 06 yanıtlandı (kayıt `2026-10-29-878a24f0.md`); bu istek onun yerine geçer.
- Dal ucunda ayrıca origin/main birleştirmesi var (#288, onay dosyası istisnası); paket dosyasına dokunmaz.
- Açık karar (sahip): settle 4. adım MOD-0023'ün `SupersedeWorkflowHalfStartCommand` komutunu kullanır; MOD-0023 kulesinin onayına bağlıdır (kart S-90). Onay yoksa 4. adım çıkar (AC-37, AC-39, AC-56).
- Risk: HIGH (ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-29 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-0517be85.md`; yeni bulgular F-76'dan.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 07 (SOP §20.4 / CT-APR-02; revizyon 07, ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01 (uç sha'sı mesajda). Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları uçtan (FETCH_HEAD) oku.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 0517be855f30835cb773ba71c3911ded3105a99707fea451292793174d3b608f — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 07'dir. Senin incelemen 06 paket 878a24f0'ı (dal ucu a4abfca03) okudu: KOŞULLU, F-67; F-21 kapandı. Revizyon 07 yalnız F-67'yi ve onun ara denetim notlarını karşılar. Fark: `git diff a4abfca03 FETCH_HEAD -- execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`. Kayıt ve CT değerlendirmesi: docs/records/reviews/MOD-0280-FU02/2026-10-29-878a24f0.md. Dal ucunda origin/main birleştirmesi de var; paket dosyasına dokunmaz.

Önce oku: inceleme 06 kaydı; pack diff; hedef paketin Runtime Constraints 9 yeniden denetim tablosu, yol × olay tablosu, Cases that stay reserved, Validation Rules (cancel satırı), Failure Path to Verify, Audited Events (RecheckLeaveSettlementCommand), AC-24, AC-45…AC-53, Implementation Notes Revision 07. Kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kapanış: F-67 karşılandı mı? "kapandı" ya da "açık" kararını gerekçesiyle ver.
- Tutarlılık: kapalı örneği yeniden okuyan bekleyen talep için her yer (yeniden denetim tablosu, yol × olay tablosu, finalize, günlük sonuç kodları, AC-24, AC-45…AC-53, başarısızlık yolları) aynı eşlemeyi mi söylüyor: niyet `cancel` → `Cancelled`, değilse `Withdrawn`, günler bir kez?
- AC-52 kanıtı: kesinti senaryosu ölçülebilir mi; sonraki iptalin yanıtı (409 `LEAVE_CANCEL_TOO_LATE`) paketin doğrulama kurallarıyla tutuyor mu; çift iade ikinci rezervasyon kurulumuyla görünür mü?
- Gerileme: revizyon 07, F-67 dışında bir kuralı değiştirdi ya da yeni çelişki yarattı mı? AC-1…AC-57 numaraları korunmuş mu?
- Yeni bulgu varsa F-76'dan başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 0517be855f30835cb773ba71c3911ded3105a99707fea451292793174d3b608f
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b, 878a24f0
F-76 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Kapananlar (inceleme 06): <F-67 — kapandıysa, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
