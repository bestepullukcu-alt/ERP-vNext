# MOD-0230 — bağımsız inceleme isteği 01 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `f1b56737b6760ebd97d6347150e4a8a47b38d9dbe73d3e0419532ac292ee8e56`
- Kaynak commit: ee2fdcd76 (dal `chore/ct/pvg-ct-01`, yerel; push edilmedi) · ilk onay → risk HIGH
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-21
- İnceleyen: Codex, paketi yazan ve bu Control Tower'dan ayrı bir oturum; yerel depoya erişimi yok → iletim ZIP ile
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5: zor bağımsız denetim); kullanılan, oturumdan ölçülüp `reviews.tsv` satırına yazılır
- ZIP: `MOD-0230-review-01-ee2fdcd76.zip` · sha256 `a14908fe0bd5fdf59b4ae14a2b77fd3ae6ae8f9cf1722856ceb223ee56fafa52` · 149 dosya + `MANIFEST.sha256` (depo dışı, sahibin bilgisayarında)
  - Dosyalar `git archive ee2fdcd76` baytlarıyla; `excerpts/` üretilmiş alıntılar; DCP-004 kopyasında kişisel e-postalar maskelendi (hedef paket değişmedi)
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-f1b56737.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict … --reviewer codex --model … --effort … --reviewer-session … --author K… --author-session … --requested-by <hesap>` → satır `execution/approvals/reviews.tsv`'ye sahip tarafından → `pack_approval.py row --risk HIGH --author <A> --approver <B ≠ A, high=evet>` → `check` 0 → tek PR en sonda, satırı ekleyenden başkası merge eder
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

Hedef: Codex — MOD-0230 module pack bağımsız incelemesi (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-01-ee2fdcd76.zip · sha256 a14908fe0bd5fdf59b4ae14a2b77fd3ae6ae8f9cf1722856ceb223ee56fafa52 · kaynak commit ee2fdcd76 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 f1b56737b6760ebd97d6347150e4a8a47b38d9dbe73d3e0419532ac292ee8e56 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; execution/domains/pharmacovigilance/domain-config.md; execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md (e-postalar maskeli); .antigravity/rules/module-pack-standard.md (bölüm 22 ve "Onaydan önce bağımsız inceleme"); .claude/rules/agents-core.md (Çekirdek 2–5); .antigravity/rules/multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md; execution/approvals/POLICY-REVIEW-START.md (bulgu biçimi); excerpts/registry.txt (MOD-0040 takma adı, MOD-0230 satırı); excerpts/blueprint-8.8-rows.txt; excerpts/agents-port-table.txt; excerpts/ocelot-pv-routes.txt.
Bağlam (doğrula, güvenme): docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md — Control Tower'ın kod ve çalışma ölçümleri (E2 230/230, E3 çağrı tablosu). Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç, nota yalnız kendi okumanla uyuştuğu yerde dayan.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay): kimlik (ad, Blueprint 8.8 ve kayıt satırı 152 uyumu); kapsam (Repo Scope / Protected Paths ile koddaki gerçek yüzey); yetki (build gate açık, operasyonel runtime gate kapalı — kapı metinleri tutarlı mı, onay kapıları tam mı); bağımlılıklar (MOD-0018/0019/0021/0023/0031 sözleşmeleri; MOD-0040: Blueprint "Canonical ID & Correlation Standard" vs kayıtta MOD-0288 takma adı); kabul kriterlerinin ölçülebilirliği (kapalı işaretli ama kanıtsız madde, belirsiz ifade); `## Teslim dilimleri` DL-1…DL-7 — her dilim tek WP'ye verilebilir mi, bölümler doğru dilimde mi, eksik bölüm var mı.
Özellikle değerlendir: Gateway PV route'larında kimlik doğrulaması yok (ocelot alıntısı); API kiracı/aktör bağlamını HTTP başlığından alıyor; Web denetleyicisinde [HasPermission] yok ve pv.* izin kataloğu yok; üretim-benzeri + yetkili + adaptörsüz açılış 500 veriyor (not §4-3) — paket bunları kapsam/kapı metninde ele alıyor mu, çelişiyor mu, eksik mi bırakıyor?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ f1b56737b6760ebd97d6347150e4a8a47b38d9dbe73d3e0419532ac292ee8e56
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını bulgu satırı dışında büyük harfle yazma; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır.
