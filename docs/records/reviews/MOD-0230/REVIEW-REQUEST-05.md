# MOD-0230 — bağımsız inceleme isteği 05 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `94cbb2d5c82aa5eae4093aa918c17359d559966e51d235b4fee0b9d1b17c7710`
- Kaynak commit: 67fe7446c (dal `chore/ct/pvg-ct-01`, yerel; push edilmedi) · ilk onay → risk HIGH
- Önceki incelemeler: 01 (`f1b56737…`, UYGUN DEĞİL, bulgu özeti `4adc6faf…`), 02 (`b48500cf…`, UYGUN DEĞİL, `59d5a954…`),
  03 (`f5ca98e3…`, UYGUN, `ecfe911f…`), 04 (`23c49893…`, UYGUN DEĞİL, 1 ENGEL + 4 ORTA, `bfb5c82f…`). İnceleme 03 ve 04
  eski sürümlere aittir; yeni sürümü kapsamaz.
- Bu sürümün nedeni: inceleme 04 bulguları F-1…F-5 (Control Tower karşılaştırma notu 04 hepsini kabul etti) ve sahibin ek
  kararı `docs/records/decisions/2026-10/pvg-mod0230-intake-fields-03.md` @ 8a88aef77 (KR-1…KR-3). Revizyon taslağında açılan
  OD-PVG-11 ve OD-PVG-12 bu kayıtla "Evet" olarak kapandı. Form alanı 32 → 25 (yalnız create/edit).
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: **yeni** bir Codex oturumu öneriliyor. İnceleme 04'ün oturumu kimlikle doğrulanamadı ("/root ajan kimliği");
  bu kez oturum kimliğinin yanıtın başına yazılması ayrıca isteniyor. Paketi Claude yazdığı için inceleyenin Codex olması
  önerilir.
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp `reviews.tsv`
  satırına yazılır.
- ZIP: `MOD-0230-review-05-67fe7446c.zip` · sha256 `b36ca58e639f45aed4efd32df7589f5cfc271c7825b3fecd26fb3a756f4b7e40` ·
  708 212 bayt · 180 dosya + `MANIFEST.sha256`
  - Yeri (S-29): CT worktree'sinde git'in izlemediği `.ct-teslim/MOD-0230-review-05/`; depoya girmez, sahip iletir.
  - 171 kaynak dosya `git archive 67fe7446c` baytlarıyla aynı (ölçüldü); DCP-004 kopyası paket 04'teki maskeli kopyadır
    (kaynak 778ccaf05'ten beri değişmedi, ölçüldü). E-posta taraması: yalnız test değeri `reporter@example.test`.
    Paket 04'e göre ek: karar kaydı 03, inceleme 04 bulgu dosyası ve karşılaştırma notu 04, REVIEW-REQUEST-04.
  - `excerpts/`: kayıt satırları, Blueprint 8.8 satırları, `AGENTS.md` §3, ocelot PV route'ları, DataTable doğrulayıcı çıktısı
    (kaynakları 778ccaf05'ten beri değişmedi; paket 04'teki kopyalar), yeni onay kartı, paketin inceleme 04 sürümü ve iki
    sürüm arası diff (`MOD-0230-pack-diff-23c49893-to-94cbb2d5.diff`).
- Beyanlar (inceleyen bilsin):
  - Revizyon 05'i `module-pack-author` alt ajanı yazdı. Ajanın kendi beyan ettiği effort "15"; uygulamanın ölçtüğü oturum
    effort'u high ve ajan tanımında `effort: high` var. Sahip effort kapısını uygulama ölçümüne bağladı (2026-10-22).
    Control Tower'ın mekanik denetimi geçti; içerik denetimi bu incelemenin işidir.
  - Yazar ajanın kayıtlara dayanmayan, kendi çıkarımı olarak beyan ettiği noktalar: istekte `ReporterSourceId` gelirse
    400; değişmeyen seçim çifti bağı ve kopyayı yeniden okumadan korur; `RemoveReporterLink` oluşturmada ya da bağsız
    kayıtta gelirse 400; yanlış dizinle gönderilen kimlik 404 (bulunamadı biçimi); `RemoveReporterLink` alan sayımına
    girmez (eylem, saklanan değer değil). Bunların karar gibi yazılıp yazılmadığı değerlendirilmeli.
  - KR-1 (b) önce yalnız bildiren ülkesi başlığı altındaydı; sahip commit öncesinde iki ülke alanına genişletti
    ("kapsasın"); kayıt 8a88aef77 bunu içerir.
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-94cbb2d5.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict …
  --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` →
  `reviews.tsv` satırı → UYGUN ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` →
  `check` → tek PR en sonda, satırı bestepullukcu-alt merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack bağımsız incelemesi 05 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-05-67fe7446c.zip · sha256 b36ca58e639f45aed4efd32df7589f5cfc271c7825b3fecd26fb3a756f4b7e40 · kaynak commit 67fe7446c (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 94cbb2d5c82aa5eae4093aa918c17359d559966e51d235b4fee0b9d1b17c7710 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini (UUID ya da araçtaki kimlik) yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-23c49893-to-94cbb2d5.diff; inceleme 04 bulguları docs/records/reviews/MOD-0230/2026-10-22-23c49893.md ve Control Tower karşılaştırma notu 04 (2026-10-22-23c49893-ct-karsilastirma-04.md); karar kayıtları docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md, -02.md ve -03.md (bu sürümün tek yetkisi; -03 yeni); önceki incelemeler docs/records/reviews/MOD-0230/ altındaki diğer bulgu dosyaları ve karşılaştırma notları; docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md; execution/domains/pharmacovigilance/work-packs/REQ-PVG-CRM-01-visit-report-adverse-event-msl.md; execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md; execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md (satır 392 ve 555); execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md; services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Contact.cs; execution/domains/pharmacovigilance/domain-config.md; DCP-004 (e-postalar maskeli); .antigravity/rules/module-pack-standard.md, multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md, audit-trail-standard.md; .claude/rules/agents-core.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; excerpts/ altındaki bütün dosyalar.
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- İnceleme 04'ün kapanışı: F-1…F-5'in her biri pakette gerçekten kapandı mı? Kapanış için yeni bir çelişki ya da eksik açıldı mı?
- Bildiren seçimi (F-1, KR-2): istekteki dizin türü + kimlik çifti, sunucunun yazdığı ReporterSource / ReporterSourceId ve RemoveReporterLink işareti; Entity Fields, Validation Rules, Failure Path, Audited Events, BG-24 ve BG-29…BG-33 ile DL-2 / DL-4 / DL-5 / DL-7 aynı sözleşmeyi mi söylüyor; API ve ekran ölçütleri aynı sözleşmeyi mi ölçüyor; kiracılar arası 404 ve "sessiz bağ kopması yok" kuralı ölçülebilir mi?
- Ülke (F-2, F-3, KR-1, KR-3): "unknown" ve null tanımlayıcı sayılmıyor mu; ülke kuralı tablosu iki alan ve üç liste durumu için tek anlamlı mı; kayıtlı değerin korunması iki ülke alanında da tutarlı mı; bağlı kaynaktan gelen ülkenin null kalması EK-3 ile uyumlu ve test edilebilir mi?
- Alan sayımı (F-4): 25 doğru mu; triyaj ve yönlendirme alanlarının ayrılması, bildiren seçim çiftinin tek sayılması ve RemoveReporterLink'in sayılmaması standarda (module-pack-standard satır 126–130) uygun mu?
- Denetim (F-5): BG-30 (DL-2) yalnız yerel yük üretimini, BG-33 (DL-5) gerçek denetim yazımını mı ölçüyor; yerel test ikamesi merkezi denetim kabulü gibi gösterilmiyor mu; CRM ve MOD-0288 kopyaları ikisi de kapsanıyor mu; denetim yüküne değer değil yalnız alan adı mı giriyor?
- Karar uyumu: AL-1…AL-7, EK-1…EK-5 ve KR-1…KR-3 pakete eksiksiz ve kayıtlardan fazlası olmadan işlenmiş mi? Yazarın beyan ettiği çıkarımlar (istek kaydındaki "Beyanlar" listesi) karar gibi yazılmış mı, gerekçeleri kayıtlarla çelişiyor mu? OD-PVG-1…10 değişmeden mi kaldı?
- Genel: hiçbir sözleşme kapalı / onaylı gösterilmiyor mu; MOD-0290 / MOD-0150 / MOD-0288 / MOD-0048 okuma sözleşmeleri "sahip onayı bekliyor" olarak mı duruyor ve PVG bunlara yazmıyor mu (XMC-001); operasyonel runtime kapısı kapalı mı; mevzuat ifadeleri "genel bilgi, QPPV doğrulamalı" olarak mı işaretli; Teslim dilimleri DL-1…DL-9 çakışmasız mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 94cbb2d5c82aa5eae4093aa918c17359d559966e51d235b4fee0b9d1b17c7710
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya: `.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-05/MOD-0230-review-05-67fe7446c.zip` · sha256 `b36ca58e639f45aed4efd32df7589f5cfc271c7825b3fecd26fb3a756f4b7e40`
