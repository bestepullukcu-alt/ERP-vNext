# MOD-0280-FU02 — bağımsız paket incelemesi isteği 03 (SOP §20.4 / CT-APR-02; revizyon 03, ilk onay)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `436bc38b5f924b62fe234c7f8c3c0f02afd94efe736754c122f9dcd0302e893f` (revizyon 03, `status: draft`)
- Kaynak commit: ce8f2e94188a675d494ed1c5af96c9520180902d (dal `chore/ct/hcm-leave-ct-01`; bu istek bir sonraki commit'te eklenir; GitHub'a sahibin `rev03-gonder` sözüyle gönderilir)
- Önceki: inceleme 01 **UYGUN DEĞİL** (`2026-10-29-ea61e8c6.md`), inceleme 02 **UYGUN DEĞİL** (`2026-10-29-47df0fc4.md`, revizyon 02 commit 8dc72e350). Revizyon 03 yalnız inceleme 02'nin sekiz bulgusunu (F-09…F-16) karşılar.
- Kök neden notu: F-02 → F-10, F-11, F-12, F-16 aynı kökten (yeni bir başlatma ve telafi protokolü). Revizyon 03 yeni protokol yazmaz; MOD-0280-FU01'in canlıdaki `TimesheetApprovalService` yöntemini birebir uygular. Üçüncü turda kapanmazsa kart Y3 uyarınca mimari yeniden değerlendirme kaydı açılır.
- Risk: HIGH (ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-29 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-436bc38b.md`; yeni bulgular F-17'den.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 03 (SOP §20.4 / CT-APR-02; revizyon 03, ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01 (uç sha'sı mesajda). Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları uçtan oku. Paket commit'i ce8f2e94188a675d494ed1c5af96c9520180902d; uçta bu commit'e yalnız bu istek dosyası (docs/records/reviews/MOD-0280-FU02/REVIEW-REQUEST-03.md) eklenmiştir.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 436bc38b5f924b62fe234c7f8c3c0f02afd94efe736754c122f9dcd0302e893f — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 03'tür: revizyon 02 (47df0fc4…, senin incelemen 02 UYGUN DEĞİL) üzerine yalnız o incelemenin sekiz bulgusu (F-09…F-16) karşılanır. Fark: `git diff 8dc72e350 ce8f2e941 -- execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`. CT değerlendirmesi ve kök neden notu: docs/records/reviews/MOD-0280-FU02/2026-10-29-47df0fc4.md "CT değerlendirmesi". Paketin "Implementation Notes" altındaki "Revision 03" maddesi her bulgunun karşılandığı yeri gösterir.

Önce oku: inceleme 02 kaydı; pack diff; hedef paketin tamamı (özellikle Runtime Constraints 9, Authorization Convention tablosu, Audited Events, Teslim dilimleri kapıları, AC-24, AC-26, AC-27, AC-29, AC-33…AC-35); kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs (StartAsync, CancelAsync, RetireSubmissionAsync, DecidableInstanceIdsAsync), services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs, services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/PlatformTransactionExecutor.cs; docs/records/decisions/2026-10/hcm-leave-design-decisions-01.md (D-1, D-2, D-3); .antigravity/rules/audit-trail-standard.md §3.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kapanış: F-09…F-16'nın her biri pakette gerçekten karşılandı mı? Her biri için "kapandı" ya da "açık" kararını gerekçesiyle ver; kapanmayanı yeni bulgu olarak yaz.
- FU01 yönteminin aktarımı: paketin Runtime Constraints 9 maddesi TimesheetApprovalService'in gerçek davranışıyla (başlatanın oturum kullanıcısı olması, gönderim sıra numaralı anahtar, yarım gönderimi emekliye ayırma, iptalden önce kararı okuma) birebir uyumlu mu; izin için eklenen fark (bakiye ayırma ve iade) bu yöntemi bozuyor mu?
- Gönderim anahtarı: belirsiz commit sonucunda aynı SubmissionKey ile yeniden deneme tek talep ve tek rezervasyonu gerçekten garanti ediyor mu?
- Başlatma–iptal yarışı ve geç gelen örnek: paketin tanımladığı davranış güvenli mi; geç gelen kararın yok sayılıp kayda yazılması kabul edilebilir mi?
- Okuma dalları ve yeni leave.approvals.read anahtarı PKS-001 ve D-1 ile tutarlı mı?
- Revizyon 03 başka bir şeyi değiştirdi mi (AC-1…AC-32 numaraları korunmuş mu; AC-24, AC-26, AC-27, AC-29 metinleri ve AC-33…AC-35 ölçülebilir mi)?
- Yeni bulgu varsa F-17'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 436bc38b5f924b62fe234c7f8c3c0f02afd94efe736754c122f9dcd0302e893f
değerlendirilen olumsuz incelemeler: ea61e8c6, 47df0fc4
F-17 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Kapananlar (inceleme 02): <F-09, F-10 … — kapandığını gördüklerin, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
