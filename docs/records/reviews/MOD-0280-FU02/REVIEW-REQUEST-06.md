# MOD-0280-FU02 — bağımsız paket incelemesi isteği 06 (SOP §20.4 / CT-APR-02; revizyon 06, ilk onay)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `878a24f03267eaf9eaf08e21815b1cd6bdb9f42992c0aa0d8c3910649d010d93` (revizyon 06, `status: draft`)
- Kaynak commit: bu istekle aynı commit (dal `chore/ct/hcm-leave-ct-01`); GitHub'a sahibin sözüyle gönderilir. Uç sha'sı Codex'e mesajda verilir.
- Önceki: inceleme 01 (`ea61e8c6`), 02 (`47df0fc4`), 03 (`436bc38b`), 04 (`5fb4d1bb`) — dördü **UYGUN DEĞİL**; inceleme 05 (`948f931b`) **KOŞULLU**, tek bulgu F-21 (CT değerlendirmesi kayıtta: kabul).
- Revizyon 06: F-21'i karşılar; ayrıca sahibin gece talimatıyla taze bağlamlı alt ajan ara denetimlerinden geçti (resmî inceleme değil; bulgular F-22…F-66, paketin "Implementation Notes → Revision 06" bölümünde). Bu istek o ara denetimlerin yerine geçen resmî incelemedir.
- İstek 05 yanıtlandı (kayıt `2026-10-29-948f931b.md`); bu istek onun yerine geçer. Daha önce hazırlanan, gönderilmemiş sabah metinleri geçersizdir.
- Açık karar (sahip): settle 4. adım MOD-0023'ün mevcut `SupersedeWorkflowHalfStartCommand` komutunu kullanır; leave'in bu kullanımı MOD-0023 kulesinin onayına bağlıdır (kart S-90). Onay yoksa 4. adım çıkar, paket o dalı da tanımlar (AC-37, AC-39, AC-56).
- Son ara denetim (9. tur, taze bağlam): UYGUN; dört düşük not işlendi. Paket 57 kabul ölçütü, 7 teslim dilimi.
- Risk: HIGH (ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-29 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-878a24f0.md`; yeni bulgular F-67'den.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 06 (SOP §20.4 / CT-APR-02; revizyon 06, ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01 (uç sha'sı mesajda). Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları uçtan (FETCH_HEAD) oku.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 878a24f03267eaf9eaf08e21815b1cd6bdb9f42992c0aa0d8c3910649d010d93 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 06'dır. Senin incelemen 05 paket 948f931b'yi (dal ucu eedb4a456) okudu: KOŞULLU, F-21. Revizyon 06 F-21'i karşılar ve ara denetimlerden gelen düzeltmeleri içerir. Fark: `git diff eedb4a456 FETCH_HEAD -- execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`. Kayıt ve CT değerlendirmesi: docs/records/reviews/MOD-0280-FU02/2026-10-29-948f931b.md. Karar: docs/records/decisions/2026-10/hcm-leave-architecture-reassessment-01.md "Ek 2 — yol 1b".

Önce oku: inceleme 05 kaydı; pack diff; hedef paketin Runtime Constraints 9 tamamı (Settle 1–3, yeniden okuma durum tablosu, Re-check tablosu, yol × olay tablosu, eşzamanlılık, Cases that stay reserved), LeaveRequest alanları (StartFacts, WorkflowStartState, PendingIntent), Dependencies, Frontend File Contract, Validation Rules, Failure Path to Verify, Authorization tablosu, Audited Events, AC-17, AC-24, AC-37, AC-39…AC-57, Implementation Notes Revision 06. Kod (yalnız oku), services/Diten.Platform/src altında: Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs, WorkflowTaskTransitionSupport.cs, CloseAbandonedWorkflowStartHandler.cs, RunWorkflowEscalationsHandler.cs; Diten.Platform.Application/BackgroundJobs/PlatformRecurringJobRegistrar.cs; Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs; Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kapanış: F-21 karşılandı mı? "kapandı" ya da "açık" kararını gerekçesiyle ver.
- Yıl sonu devri: CarriedOutDays, yıl bitmeden devir yok, devredilmiş yıla ayırma yok, devir düzeltmesi — bir gün iki yılda kullanılabilir mi?
- Doğruluk: hiçbir yol günleri iki kez ya da kapanmamış bir örnek varken serbest bırakabilir mi; aynı talep için ikinci bir görev ya da ikinci bir gönderim açılabilir mi? Settle yalnız yeniden okunan durumla mı karar veriyor; görevsiz "true" ya da tek başına 409 başarı sayılıyor mu?
- Yeniden denetim (Re-check): her (talep durumu, yeniden okuma) çifti tanımlı ve güvenli mi; "Unconfirmed + kapalı → Withdrawn" kuralı doğru mu; Finalize ile ayrık mı?
- Eşzamanlılık: süpürme işi, okuma yolu ve çalışanın settle'ı aynı anda çalışınca tek sonuç çıkıyor mu?
- 4. adım: SupersedeWorkflowHalfStartCommand ile yarım başlatmayı kapatmak duraklamış ilk çağrıya, eşzamanlı settle'a ve yeniden denetime karşı güvenli mi; kule onay vermezse paket tutarlı mı?
- Takılı kalma: "Cases that stay reserved" listesi tam mı; listede olmayan kalıcı takılma var mı?
- Günlük: yol başına tek olay; zarf yalnız kimlik ve kapalı kod listesi mi?
- Paketin MOD-0023 kodu hakkındaki iddiaları (satır numaraları dahil) kodla tutuyor mu?
- AC-1…AC-38 numaraları korunmuş mu; yeni AC'ler ölçülebilir mi?
- Yeni bulgu varsa F-67'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 878a24f03267eaf9eaf08e21815b1cd6bdb9f42992c0aa0d8c3910649d010d93
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4, 436bc38b, 5fb4d1bb, 948f931b
F-67 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Kapananlar (inceleme 05): <F-21 — kapandıysa, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
