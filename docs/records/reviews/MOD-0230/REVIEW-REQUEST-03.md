# MOD-0230 — bağımsız inceleme isteği 03 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `f5ca98e388f039a8460de736ec3a71e23fae889914d8b63d70766f0d40fde0f7`
- Kaynak commit: e1afd174b (dal `chore/ct/pvg-ct-01`, yerel; push edilmedi) · ilk onay → risk HIGH
- Önceki incelemeler (ikisi de UYGUN DEĞİL): 01 hedef `f1b56737…`, bulgu özeti `4adc6fafd68e…` (`reviews.tsv` satır 2); 02 hedef `b48500cf…`, bulgu özeti `59d5a954a8b8…` (`reviews.tsv` satır 3)
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: **yeni** bir Codex oturumu öneriliyor. İnceleme 01 ve 02 aynı Codex oturumunda (`01a15b88…`) yapıldı; SOP §20.4-2 bunu yasaklamaz, ama aynı oturum kendi önceki bulgularına bağlı kalabilir. Paketi Claude yazdığı için inceleyenin Codex olması önerilir.
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp `reviews.tsv` satırına yazılır.
- ZIP: `MOD-0230-review-03-e1afd174b.zip` · sha256 `2fc1dd2d8560d5199c7d5ad44c19af564a75ad4af4b7c792f1255f05f0c3f9af` · 428 040 bayt · 166 dosya + `MANIFEST.sha256`
  - Yeri (S-29): CT worktree'sinde git'in izlemediği `.ct-teslim/MOD-0230-review-03/`; depoya girmez, sahip iletir.
  - 157 kaynak dosya `git archive e1afd174b` baytlarıyla aynı (ölçüldü); DCP-004 kopyasında üç kişisel e-posta maskelendi. `excerpts/`: kayıt ve Blueprint 8.8 satırları, `AGENTS.md` §3, ocelot PV route'ları ve tüm `AuthenticationOptions` satırları, DataTable doğrulayıcı çıktısı, onay kartı, paketin inceleme 02 sürümü ve iki sürüm arası diff.
- Paket yazımına ilişkin beyan: revizyon 03'ü `module-pack-author` alt ajanı yazdı; istenen Opus · high, ajan effort'u 15 olarak beyan etti (ikinci kez). Control Tower'ın mekanik denetimi geçti (künye ve 41 başlık değişmedi, dilim tablosu temiz, yeni işaretli kutu yok, ad / e-posta yok); içerik denetimi bu incelemenin işidir.
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-f5ca98e3.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict … --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` → `reviews.tsv` satırı → UYGUN ise `pack_approval.py row --risk HIGH --author <A> --approver <B ≠ A, high=evet>` → `check` 0 → tek PR en sonda, satırı ekleyenden başkası merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack bağımsız incelemesi 03 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-03-e1afd174b.zip · sha256 2fc1dd2d8560d5199c7d5ad44c19af564a75ad4af4b7c792f1255f05f0c3f9af · kaynak commit e1afd174b (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 f5ca98e388f039a8460de736ec3a71e23fae889914d8b63d70766f0d40fde0f7 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; önceki iki inceleme docs/records/reviews/MOD-0230/2026-10-21-f1b56737.md ve 2026-10-21-b48500cf.md; Control Tower karşılaştırmaları 2026-10-21-f1b56737-ct-karsilastirma-01.md ve 2026-10-21-b48500cf-ct-karsilastirma-02.md; excerpts/MOD-0230-pack-diff-b48500cf-to-f5ca98e3.diff; docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md; execution/domains/pharmacovigilance/domain-config.md; execution/domains/pharmacovigilance/work-packs/REQ-PVG-CRM-01-visit-report-adverse-event-msl.md; execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md (e-postalar maskeli); .antigravity/rules/module-pack-standard.md; .claude/rules/agents-core.md; .antigravity/rules/multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md, audit-trail-standard.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; excerpts/ altındaki bütün dosyalar.
Bağlam (doğrula, güvenme): docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md. Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- Önceki inceleme 02'nin beş bulgusunun her biri bu sürümde kapandı mı? Kapanmayan her biri yeni bir F satırı olur.
- Yeni metin: okuma denetimi satırı ve BG-18 AUD-001 §7 ile uyumlu mu; denetim yazılamazsa davranışın MOD-0021 sözleşmesine bırakılması kurala aykırı mı?
- Daraltılmış BG-4 taraması yalnız MOD-0230 yüzeyini mi kapsıyor; MOD-0231 sınırı doğru mu?
- 409 sahiplik tablosu ve BG-12 / BG-19 / BG-20: her katman tek dilimde mi, dilim önkoşulları ve sırası uygulanabilir mi; BG-19'un OD-PVG-2/3 onaylarını beklememesi doğru mu?
- "Bugün / öneri, kodda yok" ayrımı: paket hâlâ kodda olmayan bir metodu ya da davranışı olgu gibi sunuyor mu?
- G-16 ve sayfalama ölçütleri ölçülebilir mi (101 kayıt, adı belli testler, toplam sayılar)?
- Genel: hiçbir sözleşme kapalı / onaylı gösterilmiyor mu; Open Decisions karar kaydıyla aynı mı; `## Teslim dilimleri` DL-1…DL-9 tutarlı mı; kimlik (Blueprint 8.8 satır 231, kayıt satırı 152) uyumlu mu?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ f5ca98e388f039a8460de736ec3a71e23fae889914d8b63d70766f0d40fde0f7
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954
önceki inceleme 59d5a954: 1 <kapandı ya da açık>; 2 …; 3 …; 4 …; 5 … (düz metin, | işareti ve büyük harfli seviye adı yok)
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır.
```

Ek dosya: `.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-03/MOD-0230-review-03-e1afd174b.zip` · sha256 `2fc1dd2d8560d5199c7d5ad44c19af564a75ad4af4b7c792f1255f05f0c3f9af`
