# MOD-0280-FU02 — bağımsız paket incelemesi isteği 01 (SOP §20.4 / CT-APR-02; ilk onay)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md` @ sha256 `ea61e8c62407edc8647b241c46121de70b2379b177a54713ac48a25f8742df36` (revizyon 01, `status: draft`)
- Kaynak commit: 8fe7599b2356a121638d7e726685541588e326e1 (dal `chore/ct/hcm-leave-ct-01`; bu istek bir sonraki commit'te eklenir; GitHub'a sahibin `paket-inceleme` sözüyle gönderilir)
- Önceki: paket öncesi tasarım denetimi 01 **KOŞULLU** (`docs/records/reviews/MOD-0280-FU02/2026-10-27-design-01.md`; 7 bulgu kabul; F-01, F-06, F-07 kayıtta düzeltildi; F-02…F-05 kararları ADR-005 Ek karar 1 ve `docs/records/decisions/2026-10/hcm-leave-design-decisions-01.md` D-1…D-3). Bu paketin önceki paket incelemesi yok.
- Risk: HIGH (`pack_approval.py card`: ilk onay).
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-27 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-ea61e8c6.md`; bulgular F-01'den.
- Sonra: bulgu dosyası commit → `review-row` → onay satırı (onaylayan: `approvers.tsv`'de `high: evet`, paketi yazmamış) → push ve PR sahibin sözüyle ((a) zorunlu PR; iki HCM kulesinin kayıt satırları aynı PR'da, merge commit ile).
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management module pack bağımsız incelemesi 01 (SOP §20.4 / CT-APR-02; ilk onay, risk HIGH). İnceleyensin; paketi yazan oturum değilsin.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: GitHub dalı chore/ct/hcm-leave-ct-01. Önce `git fetch origin chore/ct/hcm-leave-ct-01` koş; dosyaları dalın ucundan oku (uç sha'sı aşağıdaki mesajda). Paket commit'i 8fe7599b2356a121638d7e726685541588e326e1; uçta bu commit'e yalnız bu istek dosyası eklenmiştir.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ sha256 ea61e8c62407edc8647b241c46121de70b2379b177a54713ac48a25f8742df36 — `sha256sum` tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Önce oku: hedef paket (tamamı); docs/records/reviews/MOD-0280-FU02/2026-10-27-design-01.md (önceki denetim ve CT değerlendirmesi); docs/records/decisions/2026-10/ADR-005-leave-absence-mod-0280-fu02-lives-in-platform.md (Ek karar 1 dahil); docs/records/decisions/2026-10/hcm-leave-design-decisions-01.md (D-1, D-2, D-3); docs/records/audits/2026-10/hcm-leave-discovery-01/RECORD.md; execution/registries/module-id-registry.md MOD-0280-FU02 satırı; execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md (emsal: §3, §8, §12–§15); .antigravity/rules/module-pack-standard.md (§2, §6, §7, §10, §10.1, §11, §12, §16); .antigravity/rules/permission-key-standard.md §1, §5; .antigravity/rules/audit-trail-standard.md; docs/guides/operations/control-tower-operating-card.md S-85 (dilim başına kanıt seviyesi); kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs ve ApproverResolver.cs. Çalışan sözleşmesi taslağı v0.3 başka bir dalda: `git fetch origin chore/ct/hcm-employee-ct-01` sonra `git show 641542099:docs/analysis/contracts/drafts/hcm-core-employee-link-v0.3-proposal.md` (dal GitHub'da yoksa "erişilemedi" yaz; bulguyu buna dayandırma).
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH — ilk onay, kiracı, yetki, kişisel veri, başka modülün servisi):
- Standart uyumu: frontmatter alanları; zorunlu 21 bölüm; AC biçimi (`- [ ] AC-<n> (zorunlu) — …`, tek Acceptance Criteria başlığı); Audited Events her yazma komutunu tam bir kez içeriyor mu; Teslim dilimleri tablosu (`pack_approval.py` slices kuralı, her dilimde tek kanıt seviyesi) ve seviyeler SOP §23 tablosuna göre savunulabilir mi?
- Tasarım kararlarının pakete doğru aktarılması: D-1 (erişim), D-2 (sağlık kategorisi kilitli, serbest metin/belge yok), D-3 (yedi bakiye kuralı), ADR-005 Ek karar 1 (kapalı hata, sözleşme testi, izleme). Eksik ya da çelişen madde var mı?
- Güvenlik: kiracılar arası 404; çalışan / yönetici / İK kapsamı; okuma ve yazma anahtarlarının ayrılığı; `health` kilidinin API, içe aktarma ve tohum yolunda kapalı olması; denetim kaydına kişisel veri taşınmaması.
- Eşzamanlılık ve tutarlılık: bakiye defteri koşullu yazımı; çakışan talep; karar tekilliği ve karar–iptal yarışı; yıl bölme; yıl sonu devrinin bir kez koşması.
- Bağımlılıklar: çalışan sözleşmesi kesinleşmeden DL-1…DL-3'ün sahte adaptörle geliştirilmesi güvenli mi; DL-4 kabul sınırı doğru yazılmış mı; açık sorular (XMC-001 onaylayanı, K-1, K-4, K-9, MCR) hazır geliştirme kararını engellemeli mi?
- Kapsam dışı bırakılanlar (devamsızlık, sağlık, puantaj bağlantısı, şirket bazlı İK kapsamı, yasal hak hesabı) açık ve tutarlı mı?
- Bulgu varsa F-01'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0280-FU02-leave-absence-management.md @ ea61e8c62407edc8647b241c46121de70b2379b177a54713ac48a25f8742df36
değerlendirilen olumsuz incelemeler: -
F-01 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
