# MOD-0230 — bağımsız inceleme isteği 06 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `31f02845234f82325e9528006072cd95106945c7ba71cfc50abf777ef1ba8c65`
- Kaynak commit: a4fa3e107 (dal `chore/ct/pvg-ct-01`; paket 5aff9aade'de yazıldı, a4fa3e107 `main` birleştirmesidir ve
  paketi değiştirmez) · risk HIGH (onaylı önceki sürüm `main`'de değil; araç ilk onay sayar)
- Önceki incelemeler: 01 (`f1b56737…`, UYGUN DEĞİL, bulgu özeti `4adc6faf…`), 02 (`b48500cf…`, UYGUN DEĞİL, `59d5a954…`),
  03 (`f5ca98e3…`, UYGUN, `ecfe911f…`), 04 (`23c49893…`, UYGUN DEĞİL, `bfb5c82f…`), 05 (`94cbb2d5…`, KOŞULLU, 0 ENGEL +
  1 ORTA, `11a95e61…`). Önceki incelemeler eski sürümlere aittir; yeni sürümü kapsamaz.
- Önceki sürüm `94cbb2d5…` için yerel dalda bir onay satırı var (3a4bf4d05; yazar GMG-ERP, onaylayan bestepullukcu-alt);
  `main`'de değildir ve bu sürümü kapsamaz.
- Bu sürümün nedeni: inceleme 05 F-1 (bağ kaldırma ile tek seferlik CRM aramasının önceliği) ve sahibin ek kararı
  `docs/records/decisions/2026-10/pvg-mod0230-intake-fields-04.md` @ 97e76fbeb (KR-4 a/b). Form alanı sayısı değişmedi (25).
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: **yeni** bir Codex oturumu öneriliyor; oturum kimliğinin yanıtın başına yazılması istenir (inceleme 04 ve 05
  "/root" yazdı, kimlik doğrulanamadı). Paketi Claude yazdığı için inceleyenin Codex olması önerilir.
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp `reviews.tsv`
  satırına yazılır.
- ZIP: `MOD-0230-review-06-a4fa3e107.zip` · sha256 `2cb7e8c07823f6bb940a6324b6049c2ea12a52f6e2b3117df89341a22ed583c4` ·
  713 307 bayt · 184 dosya + `MANIFEST.sha256`
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-06/`; git'in
    izlemediği klasör, depoya girmez, sahip iletir.
  - 175 kaynak dosya `git archive a4fa3e107` baytlarıyla aynı (ölçüldü); DCP-004 kopyası paket 04/05'teki maskeli kopyadır
    (kaynak değişmedi, ölçüldü). E-posta taraması: yalnız test değeri `reporter@example.test`. Paket 05'e göre ek: karar
    kaydı 04, inceleme 05 bulgu dosyası ve karşılaştırma notu 05, REVIEW-REQUEST-05.
  - `excerpts/`: kayıt satırları, Blueprint 8.8 satırları, `AGENTS.md` §3, ocelot PV route'ları, DataTable doğrulayıcı çıktısı
    (kaynakları değişmedi; önceki pakettekiler), yeni onay kartı, paketin inceleme 05 sürümü ve iki sürüm arası diff
    (`MOD-0230-pack-diff-94cbb2d5-to-31f02845.diff`).
- Beyanlar (inceleyen bilsin):
  - Revizyon 06'yı `module-pack-author` alt ajanı yazdı; ajanın beyan ettiği effort "15", uygulamanın ölçtüğü oturum effort'u
    high (sahip kapıyı uygulama ölçümüne bağladı). Control Tower'ın mekanik denetimi geçti; içerik denetimi bu incelemenin işidir.
  - Ölçülen olgu (revizyon 05): "CRM'de bir kez aranır" kuralı hiçbir saklanan alanda tutulmuyordu. Revizyon 06 bunu yeni
    sistem alanı `ReporterCrmSearchState` (`Pending` / `Searched` / `ClosedByUserUnlink`) ile tutar.
  - Yazarın kayıtlara dayanmayan, kendi tasarımı ya da çıkarımı olarak beyan ettiği noktalar: alan adı ve üç değer;
    eşleşmesiz ya da belirsiz biten aramanın da `Searched` sayılması ("kayıt başına bir arama" okuması); durum değişikliğinin
    denetime yalnız alan adıyla girmesi (değer bir kod olduğu hâlde); aramayı tetikleyen "kayıt tamamlanınca" anının bu
    revizyondan önce de tanımsız olması ve bu revizyonda da tanımlanmaması.
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-31f02845.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict …
  --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` →
  `reviews.tsv` satırı → UYGUN ya da açık ENGEL'siz KOŞULLU ise `pack_approval.py row --risk HIGH --author GMG-ERP
  --approver bestepullukcu-alt` → `check` → `main` ile birleştirme ve kontroller (§7a) → tek PR en sonda, bestepullukcu-alt
  merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack bağımsız incelemesi 06 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-06-a4fa3e107.zip · sha256 2cb7e8c07823f6bb940a6324b6049c2ea12a52f6e2b3117df89341a22ed583c4 · kaynak commit a4fa3e107 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 31f02845234f82325e9528006072cd95106945c7ba71cfc50abf777ef1ba8c65 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini (UUID ya da araçtaki kimlik) yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-94cbb2d5-to-31f02845.diff; inceleme 05 bulguları docs/records/reviews/MOD-0230/2026-10-22-94cbb2d5.md ve Control Tower karşılaştırma notu 05 (2026-10-22-94cbb2d5-ct-karsilastirma-05.md); karar kayıtları docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md, -02.md, -03.md ve -04.md (bu sürümün tek yetkisi; -04 yeni); önceki incelemeler docs/records/reviews/MOD-0230/ altındaki diğer bulgu dosyaları ve karşılaştırma notları; docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md; execution/domains/pharmacovigilance/work-packs/REQ-PVG-CRM-01-visit-report-adverse-event-msl.md; execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md; execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md (satır 392 ve 555); execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md; services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Contact.cs; execution/domains/pharmacovigilance/domain-config.md; DCP-004 (e-postalar maskeli); .antigravity/rules/module-pack-standard.md, multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md, audit-trail-standard.md; .claude/rules/agents-core.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; excerpts/ altındaki bütün dosyalar.
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH):
- İnceleme 05'in kapanışı: F-1 pakette gerçekten kapandı mı? Bağ kaldırma işaretli kaydetmede CRM araması çalışmıyor mu (KR-4 a); bağı kullanıcı tarafından kaldırılmış kayıtta sonraki hiçbir kaydetmede çalışmıyor mu ve bağ yalnız seçim çiftiyle mi geri geliyor (KR-4 b)?
- Yeni durum alanı: ReporterCrmSearchState (Pending / Searched / ClosedByUserUnlink) Entity Fields, seçim sözleşmesi, Validation Rules, Failure Path, Audited Events, BG-29 / BG-30 / BG-31 / BG-33 ve DL-2 / DL-4 / DL-5'te aynı kuralı mı söylüyor; hiçbir durumun Pending'e dönmemesi, istekte gelirse 400, hassasiyet sınıfı ve yalnız alan adıyla denetim tutarlı mı; sistem alanı olarak alan sayımı dışında tutulması standarda (module-pack-standard satır 126–130) uygun mu?
- Kabul testleri: CRM kesin eşleşme döndürürken bağ kaldırmanın Manual / boş kimlik / korunan kopya ile bittiği, sonraki kaydetmede yeniden bağlanmadığı, kişi dizini bağı için de aynısının geçerli olduğu ve seçim çiftiyle yeniden bağlanmanın çalıştığı ölçülebilir mi; MOD-0150 sözleşmesine bağlı olanlar "after …" ile işaretli mi?
- Karar uyumu: AL-1…AL-7, EK-1…EK-5, KR-1…KR-3 ve KR-4 pakete eksiksiz ve kayıtlardan fazlası olmadan işlenmiş mi? Yazarın beyan ettiği tasarım ve çıkarımlar (istek kaydındaki "Beyanlar" listesi; özellikle eşleşmesiz aramanın Searched sayılması ve tanımsız "kayıt tamamlanınca" tetikleyicisi) karar gibi yazılmış mı, kayıtlarla çelişiyor mu? OD-PVG-1…12 değişmeden mi kaldı?
- Gerileme: revizyon 05'te kapanan inceleme 04 bulguları (F-1…F-5) bu revizyonda yeniden açıldı mı?
- Genel: hiçbir sözleşme kapalı / onaylı gösterilmiyor mu; MOD-0290 / MOD-0150 / MOD-0288 / MOD-0048 okuma sözleşmeleri "sahip onayı bekliyor" olarak mı duruyor ve PVG bunlara yazmıyor mu (XMC-001); operasyonel runtime kapısı kapalı mı; Teslim dilimleri DL-1…DL-9 çakışmasız mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 31f02845234f82325e9528006072cd95106945c7ba71cfc50abf777ef1ba8c65
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-06/MOD-0230-review-06-a4fa3e107.zip
sha256 2cb7e8c07823f6bb940a6324b6049c2ea12a52f6e2b3117df89341a22ed583c4
```
