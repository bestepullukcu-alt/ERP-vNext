# MOD-0357 — bağımsız inceleme isteği 02 (SOP §20.4 / CT-APR-02; R1 düzeltme turu 1)

- Hedef: `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` @ sha256 `ab321ce2c34cc2d567a6eeb430688e6ce162256ab44f970d6414f78d4940f9c1`
- Kaynak commit: `e18a2294b` (dal `chore/ct/mg-ct-01`; yerel, push edilmedi)
- Önceki: `bd7b82ce…` @ `6a1e3a855` — inceleme 01 **KOŞULLU** (1 engel, 3 orta, 6 düşük), kayıt `docs/records/reviews/MOD-0357/2026-10-27-bd7b82ce.md` (`86a607184`), satır `execution/approvals/reviews.tsv` (`090a620ae`).
- Bu tur = önceki sürüm + yalnız inceleme 01 bulgularının düzeltmesi (sahip sözü `duzelt`, 2026-10-27): yeni ilk dilim **S28 — Meeting access security** (§24.31, DL-28); §24.0'da "Review 01 disposition" tablosu; açık sorular Q23 (ortak denetim aynası, XMC-001) ve Q24. Fark: `git diff 6a1e3a855 e18a2294b -- <paket>`.
- **CT ön denetimi:** `git diff --check` temiz; `\r` yok; `pack_approval.slices()` → DL-1, DL-13…DL-28; sahip cevapları bloğu yerinde; `ITaskReadAccessPolicy` toplantı kodunda zaten kullanılıyor (`RecordLinks/TaskRelatedRecordResolver.cs`, `Handlers/CommandHandlers/MeetingTaskBridgeCommandHandlers.cs`) — S28'in BL-541 yolu var olan bir servise dayanıyor.
- İsteyen: depo sahibi (GMG-ERP, K3) · Hazırlayan: CT · Toplantılar · Yazan: `module-pack-author` alt ajanı (Opus · high) · İnceleyen: inceleme 01'i yapan bağlamdan da ayrı, yeni bir Claude alt ajanı (Opus · high).
- Bulgu dosyası: `docs/records/reviews/MOD-0357/<YYYY-AA-GG>-ab321ce2.md`; yeni bulgular F-11'den; kapanan inceleme 01 bulguları düz metin satırında (S-59).

## İnceleme prompt'u

```text
HEDEF: MOD-0357 (Toplantılar) module pack R1 — bağımsız inceleme 02 (SOP §20.4 / CT-APR-02; düzeltme turu 1). İnceleyensin; paketi yazan değilsin, inceleme 01'i yapan da değilsin.
Yer: C:\dev\ERP-vNext\.claude\worktrees\mg-ct-01 (dal chore/ct/mg-ct-01). Yalnız oku: dosya düzenleme, kod yazma, commit, push yok. Tek çıktın aşağıdaki bulgu metnidir.
Önce doğrula: `git merge-base --is-ancestor e18a2294b HEAD` çıkış 0 ve paketin sha256'sı ab321ce2c34cc2d567a6eeb430688e6ce162256ab44f970d6414f78d4940f9c1. Tutmuyorsa dur ve bildir.

Önce oku: inceleme 01 bulguları (docs/records/reviews/MOD-0357/2026-10-27-bd7b82ce.md); paketteki §24.0 "Review 01 disposition" tablosu; fark `git diff 6a1e3a855 e18a2294b -- execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md`; yeni S28 (§24.31) tamamı; S15 (§24.10), S25 (§24.28), S26 (§24.29), S27 (§24.30), §24.7, §25, §26; ilgili kod (services/Diten.Platform/src/Diten.Platform.Application/Features/Meetings/**, özellikle Handlers/CommandHandlers/*.cs, RecordLinks/**, Handlers/QueryHandlers/MeetingReportQueryHandlers.cs; ITaskReadAccessPolicy'nin tanımı ve mevcut kullanımları); .antigravity/rules/audit-trail-standard.md, multi-tenancy.md, permission-key-standard.md, page-help-and-keyboard.md; docs/guides/operations/control-tower-sop.md §23 (`git show origin/main:` ile).

Denetle (risk HIGH):
1. İnceleme 01'in her bulgusu (F-1…F-10) gerçekten kapandı mı? Her biri için paket metnini ve gerekiyorsa kodu ölç. Kapanmayan ya da yarım kalanı yeni bulgu olarak yaz (F-11'den), hangi eski bulguya dayandığını söyle.
2. S28: on beş yazma işleyicisi gerçekten kodla eşleşiyor mu (eksik yazma yolu var mı: liste, toplu silme, seri, görev bağlama, tutanak); tek atlama (seri taraması) güvenli mi; 404 davranışı UAS-001 ve multi-tenancy ile uyumlu mu; BL-541 toplantı tarafı Features/Tasks/** değiştirmeden gerçekten kapanıyor mu; sabotaj ve test matrisi ölçülebilir mi?
3. Düzeltme yeni bir çelişki, döngülü bağımlılık, yeni K2 borcu ya da kapsam taşması getirdi mi? §26'da her satırda tek ve savunulabilir seviye var mı (DL-28 E5; DL-16/17/18 L3)?
4. Q23 ve Q24 doğru ve sahibin anlayacağı biçimde sorulmuş mu; Q23 cevaplanmadan S25'in başlayamayacağı açık mı?
Derleme ve test koşamazsın; ölçtüklerini tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz; en üste kullandığın modeli, effort'u ve "oturum: CT · Toplantılar oturumunun inceleme alt ajanı 02 (ayrı bağlam)" yaz):
# MOD-0357 — bağımsız inceleme
hedef: execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md @ ab321ce2c34cc2d567a6eeb430688e6ce162256ab44f970d6414f78d4940f9c1
Kapananlar (inceleme 01): <kapandığını ölçtüğün F numaraları, virgülle>
F-11 | ORTA | <bulgu; paket satır numarası ve dayanak dosya> | açık | CT | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır (Kapananlar satırı "Kapananlar" ile başlar); kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; yeni bulgu yoksa F satırı yazma; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```
