# MOD-0280-FU02 — bağımsız paket incelemesi isteği 04 (SOP §20.4 / CT-APR-02; revizyon 04, ilk onay)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `5fb4d1bbe6ff56228c13d863de1d2413eb6cc5ff5fed74b2e8a4c383c1580853` (revizyon 04, `status: draft`)
- Kaynak commit: 3ce09edac81e1c1a03e4eda60d9ae9c03bf3ffe8 (dal `chore/ct/hcm-leave-ct-01`; bu istek bir sonraki commit'te eklenir; GitHub'a sahibin `kilit-gonder` sözüyle gönderilir)
- Önceki: inceleme 01 (`ea61e8c6`), 02 (`47df0fc4`), 03 (`436bc38b`, revizyon 03 commit ce8f2e941) — üçü de **UYGUN DEĞİL**. Revizyon 04 yalnız inceleme 03'ün üç bulgusunu (F-17…F-19) karşılar.
- Kart Y3: aynı kök üç turda kapanmadığı için mimari yeniden değerlendirme kaydı açıldı ve karar verildi: `docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md` (`status: decided`; talep başına başlatma kilidi + iki ayrı iptal yolu; MOD-0023 değişmez). Bu kök revizyon 04'te de kapanmazsa yol 2 (MOD-0023 korumalı başlatma) MOD-0023 kulesiyle açılır.
- Risk: HIGH (ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-29 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-5fb4d1bb.md`; yeni bulgular F-20'den.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 04 (SOP §20.4 / CT-APR-02; revizyon 04, ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01 (uç sha'sı mesajda). Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları uçtan oku. Paket commit'i 3ce09edac81e1c1a03e4eda60d9ae9c03bf3ffe8; uçta bu commit'e yalnız bu istek dosyası (docs/records/reviews/MOD-0280-FU02/REVIEW-REQUEST-04.md) eklenmiştir.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 5fb4d1bbe6ff56228c13d863de1d2413eb6cc5ff5fed74b2e8a4c383c1580853 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 04'tür: revizyon 03 (436bc38b…, senin incelemen 03 UYGUN DEĞİL) üzerine yalnız o incelemenin üç bulgusu (F-17…F-19) karşılanır; karar, mimari yeniden değerlendirme kaydındadır. Fark: `git diff ce8f2e941 3ce09edac -- execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`. CT değerlendirmesi: docs/records/reviews/MOD-0280-FU02/2026-10-29-436bc38b.md "CT değerlendirmesi". Paketin "Implementation Notes" altındaki "Revision 04" maddesi her bulgunun karşılandığı yeri gösterir.

Önce oku: inceleme 03 kaydı; docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md; pack diff; hedef paketin Runtime Constraints 6 ve 9, LeaveRequest alanları (StartLeaseId, StartLeaseUntilUtc), Validation Rules, Failure Path to Verify, Audited Events, AC-5, AC-11, AC-17, AC-34…AC-38; kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs (RetireSubmissionAsync, CancelAsync), services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs; .antigravity/rules/audit-trail-standard.md §3; docs/records/decisions/2026-10/hcm-leave-design-decisions-01.md D-3/3; queue execution/domains/human-capital-management/work-packs/hcm-leave/CT-QUEUE.tsv HCM-LEAVE-06 reassessment_record.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kapanış: F-17, F-18, F-19'un her biri pakette gerçekten karşılandı mı? Her biri için "kapandı" ya da "açık" kararını gerekçesiyle ver.
- Başlatma kilidi: kilit gönderim işleminde alınıp başlatma çağrısı dönünce bırakıldığında, başlatma sürerken iptal ya da yeniden gönderim gerçekten imkânsız mı; kilit süresi dolduktan sonra anahtarla sorgunun kesin olduğu iddiası StartWorkflowInstanceHandler'ın işlem sınırıyla tutarlı mı; yazılı kalan risk (10 dakikadan uzun asılı canlı çağrı) kabul edilebilir mi?
- Yeniden gönderim: önceki açık örnek iptal edilemezse yeniden gönderimin reddi FU01'in uyarıyla geçmesinden farkı doğru yazılmış mı?
- İki iptal yolu: onaylanmış iznin iptalinde MOD-0023'e dokunmamak ve günleri aynı işlemde iade etmek D-3/3 ile tutarlı mı; çalışan ve İK yetkileri doğru mu?
- Denetim: iş verisi ile AUD-001 §3 aktör zarfı ve teknik alanların ayrımı açık mı; AC-5, AC-17 ve AC-38 birlikte karşılanabiliyor mu?
- Revizyon 04 başka bir şeyi değiştirdi mi (AC-1…AC-35 numaraları korunmuş mu; AC-5 ve AC-34 metinleri, AC-35 daralması ve AC-36…AC-38 ölçülebilir mi)?
- Yeni bulgu varsa F-20'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 5fb4d1bbe6ff56228c13d863de1d2413eb6cc5ff5fed74b2e8a4c383c1580853
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b
F-20 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Kapananlar (inceleme 03): <F-17, F-18, F-19 — kapandığını gördüklerin, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
