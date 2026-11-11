# MOD-0280-FU02 — bağımsız paket incelemesi isteği 02 (SOP §20.4 / CT-APR-02; revizyon 02, ilk onay)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `47df0fc4a5708519f29cb16c3cf22ccc48f9df17e6d1ebe4b115a5d97ee5f200` (revizyon 02, `status: draft`)
- Kaynak commit: 8dc72e350f1d1c0e2b1bf67021e5b156d8e84f0b (dal `chore/ct/hcm-leave-ct-01`; bu istek bir sonraki commit'te eklenir; GitHub'a sahibin `rev02-gonder` sözüyle gönderilir)
- Önceki: inceleme 01 **UYGUN DEĞİL** (`docs/records/reviews/MOD-0280-FU02/2026-10-29-ea61e8c6.md`; revizyon 01 sha256 `ea61e8c6…`, commit 8fe7599b2; 8 bulgu, sekizi de CT tarafından kabul). Revizyon 02 yalnız bu sekiz bulguyu karşılar.
- Arada: dal `origin/main` 4c08fc75a ile birleştirildi (a3a936575); paket dosyasına dokunmadı, yalnız kayıt dosyasında `pss-wf` satırı yanına geldi.
- Risk: HIGH (ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-29 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-47df0fc4.md`; yeni bulgular F-09'dan.
- Bilinen engel (Yönetici · Ofis aktarımı, CT ölçmedi): ana daldaki onay kontrolü şu an bütün paketlerde kırmızı (#241, approvals.tsv). İncelemeyi durdurmaz; onay satırı bu düzelmeden geçerli sayılmayabilir.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 02 (SOP §20.4 / CT-APR-02; revizyon 02, ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01 (uç sha'sı mesajda). Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları uçtan oku. Paket commit'i 8dc72e350f1d1c0e2b1bf67021e5b156d8e84f0b; uçta bu commit'e yalnız bu istek dosyası (docs/records/reviews/MOD-0280-FU02/REVIEW-REQUEST-02.md) eklenmiştir.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 47df0fc4a5708519f29cb16c3cf22ccc48f9df17e6d1ebe4b115a5d97ee5f200 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 02'dir: revizyon 01 (ea61e8c6…, senin incelemen 01 UYGUN DEĞİL) üzerine yalnız o incelemenin sekiz bulgusu karşılanır. Fark: `git diff 8fe7599b2 8dc72e350 -- execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md`. CT değerlendirmesi: docs/records/reviews/MOD-0280-FU02/2026-10-29-ea61e8c6.md "CT değerlendirmesi" bölümü. Paketin "Implementation Notes" altındaki "Revision 02" maddesi her bulgunun karşılandığı yeri gösterir.

Önce oku: inceleme 01 kaydı; pack diff; hedef paketin tamamı; docs/records/decisions/2026-10/hcm-leave-design-decisions-01.md (D-1, D-2, D-3); docs/records/decisions/2026-10/ADR-005-leave-absence-mod-0280-fu02-lives-in-platform.md (Ek karar 1); docs/guides/operations/control-tower-operating-card.md S-85 ve §7c (S-90); .antigravity/rules/module-pack-standard.md (§7, §10, §10.1, §12); .antigravity/rules/permission-key-standard.md §1, §5; .antigravity/rules/audit-trail-standard.md; kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/PlatformTransactionExecutor.cs ve onu kullanan services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/StartWorkflowInstanceHandler.cs; services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- Kapanış: F-01…F-08'in her biri pakette gerçekten karşılandı mı? Her biri için "kapandı" ya da "açık" kararını gerekçesiyle ver; kapanmayanı yeni bulgu olarak yaz.
- F-02 çözümü: `IPlatformTransactionExecutor` bu paketin önerdiği kapsamı (talep + iki yıla kadar bakiye satırı + kişi başı kilit) gerçekten tek işlemde taşıyabilir mi; MOD-0023 başlatma işlem dışında kaldığında yeniden deneme ve 5 denemeden sonra telafi güvenli mi; yıl sonu devrinin satır başı işaretlenmesi tekrar uygulamayı önlüyor mu?
- F-03 tablosu: her uç nokta için anahtar ve satır kapsamı eksiksiz ve kiracı sınırıyla tutarlı mı; yönetici okuması atama anlık görüntüsüne doğru bağlı mı?
- F-04 dilim ayrımı: DL-6 E4 ve DL-7 E5 SOP §23'e göre savunulabilir mi; dilim tablosunun bölüm adları paketteki başlıklarla eşleşiyor mu?
- F-05 taban kuralı ve örneği D-3/5 ile çelişiyor mu?
- F-06 kapıları: her açık karar doğru dilime ve doğru kapıya (dağıtım ya da kabul) bağlandı mı; sahte adaptörle geliştirme ve gerçek kabul ayrımı korunuyor mu?
- Revizyon 02 başka bir şeyi değiştirdi mi (AC-1…AC-22 numaraları aynı mı; yalnız AC-5 metni genişledi mi)? Yeni AC-23…AC-32 ölçülebilir mi?
- Yeni bulgu varsa F-09'dan başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ 47df0fc4a5708519f29cb16c3cf22ccc48f9df17e6d1ebe4b115a5d97ee5f200
değerlendirilen olumsuz incelemeler: ea61e8c6
F-09 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Kapananlar (inceleme 01): <F-01, F-02 … — kapandığını gördüklerin, düz metin>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (kapanan bulguları yalnız "Kapananlar" satırında düz metin olarak yaz); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
