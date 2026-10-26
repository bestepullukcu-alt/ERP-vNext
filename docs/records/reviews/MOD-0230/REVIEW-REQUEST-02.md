# MOD-0230 — bağımsız inceleme isteği 02 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `b48500cf10e5708ae97eac7f5b93916f88442e3cf41cee14273c2178025030db`
- Kaynak commit: 73cce019a (dal `chore/ct/pvg-ct-01`, yerel; push edilmedi) · ilk onay → risk HIGH
- Önceki inceleme: 01, hedef sha256 `f1b56737…`, sonuç UYGUN DEĞİL, 7 ENGEL; bulgu dosyası `docs/records/reviews/MOD-0230/2026-10-21-f1b56737.md` (findings_sha256 `4adc6fafd68e685a…`, `reviews.tsv` satır 2)
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-21
- İnceleyen: Codex, paketi yazan ve bu Control Tower'dan ayrı bir oturum; yerel depoya erişimi yok → iletim ZIP ile
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp `reviews.tsv` satırına yazılır
- ZIP: `MOD-0230-review-02-73cce019a.zip` · sha256 `e0524675fe2e25c3bcb6f01a8b16492bdaa50ffbdad701d991b825fa6d9541f4` · 425 774 bayt · 162 dosya + `MANIFEST.sha256` (depo dışı; Control Tower oturumunun geçici klasöründe, sahip iletir)
  - Dosyalar `git archive 73cce019a` baytlarıyla (manifestteki 154 kaynak dosyanın 153'ü git ile bayt bayt aynı, ölçüldü); DCP-004 kopyasında üç kişisel e-posta maskelendi (baş harfler inceleme 01'deki gibi kaldı); `excerpts/` üretilmiş alıntılardır: kayıt satırları, Blueprint 8.8 satırları, `AGENTS.md` §3, ocelot PV route'ları ve tüm `AuthenticationOptions` satırları, DataTable doğrulayıcı çıktısı, `pack_approval.py` card/check/dilim çıktısı, paketin inceleme 01 sürümü ve iki sürüm arası diff
- Paket yazımına ilişkin beyanlar (inceleyen bilsin):
  - Yeniden yazım (8b66d283b) ve karar işleme (73cce019a) `module-pack-author` alt ajanıyla yapıldı; istenen Opus · high. İkinci koşu effort'u 15 (low) olarak beyan etti — istenenle uyuşmuyor; Control Tower denetimi mekanik kontrolleri geçti, içerik denetimi bu inceleme ister.
  - Sahip 2026-10-21'de OD-PVG-1…10'un onuna da "Evet" dedi (`docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md@4fe62fdc3`); 4'ü tam karar, 6'sı başka sahiplerin onayını bekliyor. Bu kayıt onay satırı değildir.
  - OD-PVG-3: paket satırın tamamını "onaya kadar uygulanmaz" diye yazıyor; karar kaydı yalnız hata zarfının beklediğini söylüyor (paket kayıttan sıkı; Control Tower talimatı).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-b48500cf.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict … --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` → satır `execution/approvals/reviews.tsv`'ye → UYGUN ise `pack_approval.py row --risk HIGH --author <A> --approver <B ≠ A, high=evet>` → `check` 0 → tek PR en sonda, satırı ekleyenden başkası merge eder
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## Control Tower'ın inceleme 01'e itirazları (inceleyene iletilir; inceleyen kabul ya da ret eder)

Kaynak: `docs/records/reviews/MOD-0230/2026-10-21-f1b56737-ct-karsilastirma-01.md` §3 (ZIP'te).

1. Bulgu 7, seviye: kural (POLICY-REVIEW-START "Dilim") bir dilim tablosu ve 3. sütundaki adların başlıkla birebir eşleşmesini ister; "her bölüm eşlensin, her dilimde yol · önkoşul · kapsam dışı · bitiş" kuralın ötesidir. Yeni sürüm bunu yine de yapıyor; değerlendir ama kural gereği olarak ENGEL sayma.
2. Bulgu 1, Gateway vurgusu: route başına `AuthenticationOptions` yokluğu depo normudur (dosyada 2 route'ta var; hat genelinde kimlik doğrulama ve kiracı çözümü). Açık servis tarafındadır (JWT yok, kiracı/aktör başlıktan) ve Gateway'in `X-Tenant-Id` yazıp servisin `X-Diten-Tenant-Context` okumasındadır.
3. Atıf kaymaları: RBAC kanıt satırı 241 (239 değil), AuditEvent 243 (241 değil) — eski sürümde.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

Hedef: Codex — MOD-0230 module pack bağımsız incelemesi 02 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-02-73cce019a.zip · sha256 e0524675fe2e25c3bcb6f01a8b16492bdaa50ffbdad701d991b825fa6d9541f4 · kaynak commit 73cce019a (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 b48500cf10e5708ae97eac7f5b93916f88442e3cf41cee14273c2178025030db — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; önceki inceleme docs/records/reviews/MOD-0230/2026-10-21-f1b56737.md ve Control Tower karşılaştırması 2026-10-21-f1b56737-ct-karsilastirma-01.md (özellikle §2, §3, §4); excerpts/MOD-0230-pack-diff-f1b56737-to-b48500cf.diff; docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md; execution/domains/pharmacovigilance/domain-config.md; execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md (e-postalar maskeli); .antigravity/rules/module-pack-standard.md; .claude/rules/agents-core.md; .antigravity/rules/multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md, audit-trail-standard.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; excerpts/ altındaki bütün dosyalar.
Bağlam (doğrula, güvenme): docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md (iki yanlışı paketin "Build-Gate Gaps" bölümünde düzeltildi). Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- Önceki inceleme: 01'in yedi ENGEL'inin her biri bu sürümde kapandı mı? Kapanmayan her biri yeni bir F satırı olur.
- Gerçeklik: paket hiçbir sözleşmeyi kapalı/onaylı göstermiyor mu; "Build-Gate Gaps" G-1…G-15 olguları koda karşı doğru mu?
- Kapsam ve yetki: Repo Scope / Protected Paths koddaki gerçek yüzeyle uyumlu mu; build gate açık, operasyonel runtime gate kapalı — tek ve çelişkisiz mi?
- Kararlar: Open Decisions satırları karar kaydıyla aynı mı; başka sahip onayı bekleyen satırlar uygulanabilir gibi yazılmış mı?
- Kabul ölçütleri BG-1…BG-17 ve Test Expectations ölçülebilir mi (komut, eşik, adı belli test)?
- `## Teslim dilimleri` DL-1…DL-9: her dilim tek WP'ye verilebilir mi, bölüm çakışması ya da eksik bölüm var mı, dilim ayrıntıları (yol · önkoşul · kapsam dışı · bitiş) tutarlı mı?
- Kimlik ve bağımlılıklar: Blueprint 8.8 satır 231 ve kayıt satırı 152 uyumu; MOD-0040 takma adı çatışması doğru anlatılmış mı?
- Control Tower'ın üç itirazını kabul ya da ret et; gerekçeyi bulgu satırında ya da bir düz metin satırında yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ b48500cf10e5708ae97eac7f5b93916f88442e3cf41cee14273c2178025030db
değerlendirilen olumsuz incelemeler: 4adc6faf
önceki inceleme 4adc6faf: 1 <kapandı ya da açık>; 2 …; 3 …; 4 …; 5 …; 6 …; 7 … (düz metin, | işareti ve büyük harfli seviye adı yok)
itirazlar: 1 <kabul ya da ret, kısa gerekçe>; 2 …; 3 … (düz metin, | işareti yok)
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır.
