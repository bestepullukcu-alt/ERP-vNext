# MOD-0230 — bağımsız inceleme isteği 04 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `23c498931fcafe43de1e52694f6c1c3fe92b1d225a109c935c5fa29b2a58cc5c`
- Kaynak commit: 778ccaf05 (dal `chore/ct/pvg-ct-01`, yerel; push edilmedi) · ilk onay → risk HIGH
- Önceki incelemeler: 01 (`f1b56737…`, UYGUN DEĞİL, bulgu özeti `4adc6faf…`), 02 (`b48500cf…`, UYGUN DEĞİL, `59d5a954…`),
  03 (`f5ca98e3…`, UYGUN, `ecfe911f…`). İnceleme 03 eski sürüme aittir; yeni sürümü kapsamaz.
- Bu sürümün nedeni: sahibin alım alanları kararı (`docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md`
  @ 440b04b84) ve ek karar (`-02.md` @ 26384dceb). Form alanı 16 → 32; yeni açıklar G-17…G-22, yeni kabul ölçütleri
  BG-21…BG-33.
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: **yeni** bir Codex oturumu öneriliyor. İnceleme 01, 02 ve 03 aynı Codex oturumunda (`01a15b88…`) yapıldı.
  SOP §20.4-2 bunu yasaklamaz; ama bu sürüm büyük bir alan değişikliği getiriyor ve taze bir göz daha değerlidir. Paketi
  Claude yazdığı için inceleyenin Codex olması önerilir.
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp `reviews.tsv`
  satırına yazılır.
- ZIP: `MOD-0230-review-04-778ccaf05.zip` · sha256 `e19b58fc1022e1d106794e362beb2f6879963c850ed4b19713d933c005c7d57a` ·
  668 361 bayt · 176 dosya + `MANIFEST.sha256`
  - Yeri (S-29): CT worktree'sinde git'in izlemediği `.ct-teslim/MOD-0230-review-04/`; depoya girmez, sahip iletir.
  - 167 kaynak dosya `git archive 778ccaf05` baytlarıyla aynı (ölçüldü); DCP-004 kopyasında üç kişisel e-posta maskelendi.
    Bu kez ek olarak: iki karar kaydı, MOD-0290 / MOD-0048 / MOD-0288 paketleri ve CRM kişi varlığı (`Contact.cs`).
  - `excerpts/`: kayıt satırları (MOD-0040, 0048, 0150, 0230, 0288, 0290), Blueprint 8.8 satırları, `AGENTS.md` §3, ocelot
    PV route'ları, DataTable doğrulayıcı çıktısı, onay kartı, paketin inceleme 03 sürümü ve iki sürüm arası diff.
- Beyanlar (inceleyen bilsin):
  - Revizyon 04'ü `module-pack-author` alt ajanı yazdı (istenen Opus · high; beyan edilen effort 15, üçüncü kez).
    Control Tower'ın mekanik denetimi geçti; içerik denetimi bu incelemenin işidir.
  - Dal geçmişi 2026-10-22'de sahip onayıyla yeniden kuruldu: `approvals.tsv` geçmiş boyunca yalnız eklemelidir; push
    edilmemiş bir onay satırı commit'i ve onu silen geri alma commit'i yerel daldan çıkarıldı. Bu geçmişte `f5ca98e3` için
    onay satırı yoktur. Yedek dal yereldedir.
  - Karar kaydı 01'deki AL-3 "ek onay yok" ifadesi Control Tower hatasıydı; ek kayıt 02 düzeltir (ülke listesi MOD-0048
    sahibinin onayını ister).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-23c49893.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict …
  --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` →
  `reviews.tsv` satırı → UYGUN ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` →
  `check` → tek PR en sonda, satırı bestepullukcu-alt merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack bağımsız incelemesi 04 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-04-778ccaf05.zip · sha256 e19b58fc1022e1d106794e362beb2f6879963c850ed4b19713d933c005c7d57a · kaynak commit 778ccaf05 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 23c498931fcafe43de1e52694f6c1c3fe92b1d225a109c935c5fa29b2a58cc5c — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; karar kayıtları docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md ve pvg-mod0230-intake-fields-02.md (bu sürümün tek yetkisi); excerpts/MOD-0230-pack-diff-f5ca98e3-to-23c49893.diff; önceki incelemeler docs/records/reviews/MOD-0230/ altındaki bulgu dosyaları ve Control Tower karşılaştırma notları; docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md; execution/domains/pharmacovigilance/work-packs/REQ-PVG-CRM-01-visit-report-adverse-event-msl.md; execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md (Global Ürün ve GSKU kimliği için ilgili bölümler); execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md (ülke listesi; satır 392 ve 555); execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md (kişi dizini); services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Contact.cs; execution/domains/pharmacovigilance/domain-config.md; DCP-004 (e-postalar maskeli); .antigravity/rules/module-pack-standard.md, multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md, audit-trail-standard.md; .claude/rules/agents-core.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; excerpts/ altındaki bütün dosyalar.
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- Karar uyumu: AL-1…AL-7 ve EK-1…EK-5 pakete eksiksiz ve kayıtlardan fazlası olmadan işlenmiş mi? Kayıtta olmayan bir karar pakette karar gibi yazılmış mı?
- Alanlar: 32 alanlık hesap doğru mu (tekrarlanan ürün satırı nasıl sayılmış); her yeni alanın zorunluluğu, ne zaman zorunlu olduğu (oluşturma / triyaj), hassasiyet sınıfı ve doğrulama kuralı tutarlı mı; SuspectProductText'in ürün listesine dönüşümü açık mı?
- Bağımlılıklar: MOD-0290, MOD-0150, MOD-0288 ve MOD-0048 okuma sözleşmeleri "sahip onayı bekliyor" olarak mı duruyor; PVG'nin bu modüllere yazmadığı (XMC-001) her yerde açık mı; MOD-0150'nin kayıt durumu ile kodun varlığı arasındaki fark doğru anlatılmış mı?
- Ülke listesi: MOD-0048 kısıtı doğru aktarılmış mı; liste gelene kadar "bilinmiyor" kuralı EventCountry ve ReporterCountry için tutarlı mı; liste geldikten sonraki kural (BG-26) çelişkisiz mi?
- Bildiren: üç yol, bağ + anlık kopya, düzenlenebilir kopya (EK-4) ve denetim kuralları (yalnız alan adı) tutarlı mı; bildiren ile şirket alıcısı ayrımı her yerde korunmuş mu; tek seferlik CRM araması yeni bir yazma komutu ya da arka plan işi gerektiriyor mu (AUD-001 §5 K4)?
- Denetim: Audited Events tablosu yeni alanlardan yalnız taksonomi kodlarını ve kimlikleri mi içeriyor; anlık kopya, as-reported metin, lot ve iletişim bilgisi hiçbir denetim yüküne girmiyor mu?
- Ölçütler: BG-21…BG-33 ölçülebilir mi (adı belli test, eşik); QPPV / başka sahip onayına bağlı olanlar "after …" ile işaretli mi; G-17…G-22 olguları koda karşı doğru mu?
- Dilimler: yeni alanların dilim sahipliği (DL-2 / DL-4 / DL-5 / DL-7) çakışmasız mı; `## Teslim dilimleri` DL-1…DL-9 tutarlı mı?
- Genel: hiçbir sözleşme kapalı / onaylı gösterilmiyor mu; operasyonel runtime kapısı kapalı mı; kimlik (Blueprint 8.8 satır 231, kayıt satırı 152) uyumlu mu; mevzuat ifadeleri "genel bilgi, QPPV doğrulamalı" olarak mı işaretli?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 23c498931fcafe43de1e52694f6c1c3fe92b1d225a109c935c5fa29b2a58cc5c
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya: `.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-04/MOD-0230-review-04-778ccaf05.zip` · sha256 `e19b58fc1022e1d106794e362beb2f6879963c850ed4b19713d933c005c7d57a`
