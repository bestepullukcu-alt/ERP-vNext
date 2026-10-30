# MOD-0280-FU02 — paket öncesi bağımsız tasarım denetimi isteği 01

- Bu ne: module pack yazılmadan önce, izin ve devamsızlık parçasının kapsamı, kimliği, servis yeri ve kararları için bağımsız tasarım denetimi. Paket olmadığı için SOP §20.4 paket incelemesi **değildir**; `reviews.tsv` satırı üretmez. Paket yazılınca ayrıca §20.4 incelemesi gerekir.
- Kaynak commit: 01fb3b10dba1634f2a9a9397fead695b4de925c9 (dal `chore/ct/hcm-leave-ct-01`; origin/main 3a607547e + 9 yerel commit; push edilmedi)
- İsteyen: depo sahibi (rol) · Hazırlayan: CT · İzin ve devamsızlık oturumu · Tarih: 2026-10-27 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- Sahip sözü: `codex-istek` (2026-10-27) — yalnız istek taslağı hazırlama; `codex-zip` (2026-10-27) — ZIP teslimi.
- ZIP: `MOD-0280-FU02-design-01-01fb3b10d.zip` · sha256 `adbc3f688afc80b99a2063e823a3951dc10427836daaac55d583e29689b50eac` · 1 524 346 bayt · 21 dosya + `MANIFEST.sha256` (dosyalar 01fb3b10d commit'inden `git archive` ile alındı)
  - Yeri (S-29, S-36): `C:/dev/ERP-vNext/.claude/worktrees/hr-leave-ct-01/.ct-teslim/MOD-0280-FU02-design-01/MOD-0280-FU02-design-01-01fb3b10d.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0280-FU02/<YYYY-AA-GG>-design-01.md`; bulgular F-01'den.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0280-FU02 Leave & Absence Management için paket öncesi bağımsız tasarım denetimi 01. İnceleyensin; kararları ve taslakları yazan oturum değilsin. Paket henüz yok; bu SOP §20.4 paket incelemesi değildir.
Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Depoya erişimin yok: yalnız ekteki ZIP'i oku.
Kaynak: ZIP MOD-0280-FU02-design-01-01fb3b10d.zip · sha256 adbc3f688afc80b99a2063e823a3951dc10427836daaac55d583e29689b50eac · dosyalar dal chore/ct/hcm-leave-ct-01 @ 01fb3b10dba1634f2a9a9397fead695b4de925c9 commit'inden (CT beyanı; yerel dal, push edilmedi). Önce `shasum -a 256 -c MANIFEST.sha256` (ya da `sha256sum -c MANIFEST.sha256`) ile doğrula; tutmuyorsa dur ve bildir. Aşağıdaki yollar ZIP içindeki yollardır; ZIP'te olmayan bir dosyaya dayanan bulguyu "ZIP'te yok" diye işaretle.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Önce oku:
- docs/records/audits/2026-10/hcm-leave-discovery-01/RECORD.md (8 keşif bulgusu ve kararları) ve yanındaki iki rapor (MOD-0280-leave-absence.txt, MOD-0280-time-attendance-leave-management.txt; ikisini de verify_module_id.py --check-report ile doğrula)
- execution/registries/module-id-registry.md: MOD-0280, MOD-0280-FU01, MOD-0280-FU02 satırları
- docs/records/decisions/2026-10/ADR-005-leave-absence-mod-0280-fu02-lives-in-platform.md ve emsali docs/records/decisions/2026-09/ADR-004-time-entry-mod-0280-fu01-lives-in-platform.md (özellikle "Yeniden ele alma tetikleyicisi")
- docs/records/decisions/2026-10/hcm-leave-mcr-draft-01.md (Blueprint düzeltme talebi taslağı)
- execution/domains/human-capital-management/work-packs/hcm-leave/CT-QUEUE.tsv ve execution/delivery/ct-queue-registry.tsv hcm-leave satırı
- execution/domains/human-capital-management/domain-config.md; execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md (§2, "Out of scope")
- docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx: Blueprint_Data MOD-0280 ve MOD-0315 satırları; Module Pages 3144–3147 ve 3316–3318; SoR_Map 281 ve 326; Prioritized_Gap_Register 156 (GAP-155); CHANGE_LOG 53; Enterprise Object Catalog 46; Contract Bundle Dictionary 300; Dependencies_Normalized 521–524 ve 550
- kod (yalnız oku): services/Diten.Platform/src/Diten.Platform.Application/Features/TimeEntry/Services/TimesheetApprovalService.cs ve ApproverResolver.cs; services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/PositionAssignment.cs; services/Diten.HcmService/src/Diten.HcmService.Domain/Entities/Employee.cs; services/Diten.HcmService/src/Diten.HcmService.Application/Features/CoreHrEmployeeMaster/Handlers/SubmitEmployeeDraftHandler.cs; services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Application/Features/TimeAttendanceLeave/TimeAttendanceLeaveGuard.cs
- AGENTS.md Çekirdek 2, 6, 7, 9; .antigravity/rules/module-pack-standard.md §1, §2, §16

Sohbette verilen ve dosyada olmayan tasarım önerileri (CT beyanı; onay değil — bunları da denetle):
1. Tek kayıt türü: izin ve devamsızlık aynı kayıt; türü bakiyeden düşüp düşmediğini, belge ve onay gerekip gerekmediğini belirler (SAP SuccessFactors Time Off ve Oracle Absence Management benzetmesi — CT'nin genel bilgisi, depoda kaynak yok).
2. Dilim 1 sayfaları: İzin Türleri (İK) · İzin Hakları (İK, kişi başı yıllık hak elle girilir) · İzin Taleplerim (çalışan: talep, kalan gün, iptal). Onay ayrı sayfa değil: MOD-0023 akışıyla Görev Merkezi'nde, yönetici ApproverResolver deseniyle (pozisyon → bağlı olunan pozisyon → yönetici; yoksa havuz). Tatil ve iş günü MOD-0362'den okunur.
3. Dilim 2: Devamsızlık Kaydı (İK girer). Hastalık / rapor türleri MOD-0314 HR Governance & Sensitive Access Controls kuralı ve hukuk onayı gelene kadar açılmaz (sağlık verisi).
4. Sonraya: Devam Takvimi, Vardiya, MOD-0315 görünümleri, puantajda izin günü (MOD-0280-FU01 değişikliği, XMC-001), Görev Merkezi "izinde" uyarısı, toplantıda "izinli", vekâlet (kod yok), yasal hak hesabı (kaynak yok), MOD-0003 ve MOD-0037 için "ilk dilimde gerekmez" kararı.
5. Kişi anahtarı: Blueprint Enterprise Object Catalog satır 46 anahtarı "Canonical Employee ID". Bugün Employee kaydında UserId yok ve aktif çalışan oluşamıyor; kullanıcı ↔ kişi ↔ çalışan bağı sözleşmesini CT · Çalışan kaydı yazacak (öneri zinciri: MOD-0360 → MOD-0288 → MOD-0251). Paket bu sözleşme gelene kadar kişi anahtarını kesinleştirmez.
6. PR zamanı: kayıt satırı için şimdi PR yok; ilk işçi başlatılırken iki HCM kulesi (hcm-employee, hcm-leave) için tek PR, merge commit ile.

İnceleme derinliği (risk HIGH — ilk kez açılan modül, yetki, kiracı, hassas veri):
- Keşif kararları: 8 bulgunun her biri doğru sınıflanmış mı (parent / ayrı modül / eski model / kimlik çakışması); "Leave & Absence" çıkış 0'ın kanıt olarak kullanılmaması doğru mu; kaçırılan bir mükerrer iş ya da süren iş var mı?
- Kimlik: FU02'nin MOD-0280 altında alt modül olarak ayrılması Master-first kuralına (Blueprint 8.7) uygun mu, yoksa önce Master'da MCR mi gerekirdi? Araç alt modül adını Blueprint'le karşılaştırmıyor; bu boşluk bir risk mi?
- Servis yeri: ADR-005, ADR-004'ün "izin İK verisiyle aynı serviste durmalı" tetikleyicisini doğru mu değerlendiriyor; HcmService'in bugünkü eksikleri (outbox yok, UserId yok, submit 409) gerçekten ölçülmüş mü; Çekirdek 6 (başka domain'in servisi) sapması yeterince kayıtlı mı?
- MCR taslağı: Blueprint satır numaraları ve alıntılar doğru mu; istenen altı değişiklik tutarlı mı; eksik bir değişiklik var mı?
- Tasarım: tek kayıt türü ve dilim sınırları savunulabilir mi; ilk dilimde kişi anahtarı belirsizken paket yazmaya başlamak riskli mi; hassas veri (hastalık) ayrımı yeterli mi; kiracı izolasyonu ve RBAC açısından eksik var mı?
- Kuyruk ve kayıt satırı: sop7 durumları ve depends_on mantıklı mı; kayıt satırının main'de olmaması hangi kapıları kapatıyor?
- Bulgu varsa F-01'den başla.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0280-FU02 — paket öncesi bağımsız tasarım denetimi 01
hedef: chore/ct/hcm-leave-ct-01 @ 01fb3b10dba1634f2a9a9397fead695b4de925c9
F-01 | ENGEL | <bulgu; dosya ve satır numarası ya da Blueprint sayfa ve satırı> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
