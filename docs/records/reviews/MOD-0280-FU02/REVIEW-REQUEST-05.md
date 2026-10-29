# MOD-0280-FU02 — bağımsız paket incelemesi isteği 05 (SOP §20.4 / CT-APR-02; revizyon 05, ilk onay)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `948f931b880590435e346e5dda899b039ce4ae2e00d6bdca5e0f875660912cb5` (revizyon 05, `status: draft`)
- Kaynak commit: c120baa4ecc757e09f19f51bc77ee6e95cc6502a (dal `chore/ct/hcm-leave-ct-01`; bu istek bir sonraki commit'te eklenir; GitHub'a sahibin `yeniden-oynat` sözüyle gönderilir)
- Önceki: inceleme 01 (`ea61e8c6`), 02 (`47df0fc4`), 03 (`436bc38b`), 04 (`5fb4d1bb`, revizyon 04 commit 3ce09edac) — dördü de **UYGUN DEĞİL**. Revizyon 05 yalnız inceleme 04'ün tek bulgusunu (F-20) karşılar.
- Karar kaydı: `docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md` "Ek 2 — yol 1b" (sahip sözü `yeniden-oynat`). MOD-0023 tasarım isteği 01 engel olmaktan çıktı, iyileştirme olarak açık.
- Arada (revizyon 04 ile 05 arası, paket dışı): inceleme 04 bulgu dosyası ve MOD-0023 tasarım isteği 01 commit'i (72a00e83b).
- Risk: HIGH (ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-29 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-948f931b.md`; yeni bulgular F-21'den.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 05 (SOP §20.4 / CT-APR-02; revizyon 05, ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01 (uç sha'sı mesajda). Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları uçtan oku. Paket commit'i c120baa4ecc757e09f19f51bc77ee6e95cc6502a; uçta bu commit'e yalnız bu istek dosyası (docs/records/reviews/MOD-0280-FU02/REVIEW-REQUEST-05.md) eklenmiştir.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 948f931b880590435e346e5dda899b039ce4ae2e00d6bdca5e0f875660912cb5 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 05'tir: revizyon 04 (5fb4d1bb…, senin incelemen 04 UYGUN DEĞİL) üzerine yalnız F-20 karşılanır. Fark: `git diff 3ce09edac c120baa4e -- execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`. Karar: docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md "Ek 2 — yol 1b". CT değerlendirmesi: docs/records/reviews/MOD-0280-FU02/2026-10-29-5fb4d1bb.md.

Önce oku: inceleme 04 kaydı; yeniden değerlendirme kaydı (Ek 2 dahil); pack diff; hedef paketin Runtime Constraints 9, LeaveRequest alanları (StartFacts, WorkflowStartState, StartLeaseId), Validation Rules, Failure Path to Verify, AC-34, AC-37, AC-39, AC-40; kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs (özellikle :81 anahtar araması, :372–411 ExistingStartResponseAsync ve CompleteStartAsync, :432–438 SameStarter ve parmak izi, :620–659 ClaimWithStartRowsAsync), services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowTaskTransitionSupport.cs (:640–675 görev iptali ve örnek durumu), services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/CloseAbandonedWorkflowStartHandler.cs, services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kapanış: F-20 karşılandı mı? "kapandı" ya da "açık" kararını gerekçesiyle ver.
- Yeniden oynatma kesinliği: saklanan StartFacts ile aynı başlatanın yeniden oynatması, MOD-0023 kodunda gerçekten (a) yarım kalmış başlatmayı tek görevle tamamlıyor, (b) kapalı örneği yeniden açmıyor, (c) görev iptaliyle örneği kapatıyor ve (d) geç kalan görev yazımını Active olmayan örnekte engelliyor mu? Ayırmadan önce ve ayırmadan sonra duraklayan eski başlatma için paketin iddiası doğru mu?
- Parmak izi: StartFacts'in saklanıp birebir kullanılması MOD-0023'ün olgu denetimini karşılıyor mu; onay tanımı değiştiğinde paketin "uzlaştırma gerekiyor, hiçbir şey iade edilmez" sınırı güvenli mi?
- Kilidin artık doğruluk iddiası taşımaması tutarlı yazılmış mı (AC-40)?
- Revizyon 05 başka bir şeyi değiştirdi mi (AC-1…AC-38 numaraları korunmuş mu; AC-34 ve AC-37 metinleri, AC-39 ve AC-40 ölçülebilir mi)?
- Yeni bulgu varsa F-21'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 948f931b880590435e346e5dda899b039ce4ae2e00d6bdca5e0f875660912cb5
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb
F-21 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Kapananlar (inceleme 04): <F-20 — kapandıysa, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
