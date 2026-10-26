# MOD-0230 — bağımsız inceleme isteği 07 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `10fdbe5c6a5b8510eda1605c8ac25cb9fa6ee456efce46ddfa15252709995d66`
- Kaynak commit: 7aa6abc65 (dal `chore/ct/pvg-ct-01`, yerel; push edilmedi) · risk HIGH (onaylı önceki sürüm `main`'de
  değil; araç ilk onay sayar)
- Önceki incelemeler: 01 (`f1b56737…`, UYGUN DEĞİL, bulgu özeti `4adc6faf…`), 02 (`b48500cf…`, UYGUN DEĞİL, `59d5a954…`),
  03 (`f5ca98e3…`, UYGUN, `ecfe911f…`), 04 (`23c49893…`, UYGUN DEĞİL, `bfb5c82f…`), 05 (`94cbb2d5…`, KOŞULLU, `11a95e61…`),
  06 (`31f02845…`, KOŞULLU, 0 ENGEL + 1 ORTA, `47eb73e4…`). Önceki incelemeler eski sürümlere aittir; yeni sürümü kapsamaz.
- Önceki sürüm `94cbb2d5…` için yerel dalda bir onay satırı var (3a4bf4d05); `main`'de değildir ve bu sürümü kapsamaz.
- Bu sürümün nedeni: inceleme 06 F-1 (tek seferlik CRM aramasının tetikleyicisi ölçülebilir değil) ve sahibin ek kararı
  `docs/records/decisions/2026-10/pvg-mod0230-intake-fields-05.md` @ c4b396ac1 (KR-5; sahip A seçeneğini seçti ve
  netleştirdi). Form alanı sayısı değişmedi (25).
- İsteyen: depo sahibi (rol) · Hazırlayan: PVG Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: **yeni** bir Codex oturumu öneriliyor; oturum kimliğinin yanıtın başına yazılması istenir (inceleme 04, 05 ve
  06 "/root" yazdı, kimlik doğrulanamadı). Paketi Claude yazdığı için inceleyenin Codex olması önerilir.
- Model · Effort (istenen): Codex'in en güçlü mevcut modeli · high (§17.1 r5); kullanılan, oturumdan ölçülüp `reviews.tsv`
  satırına yazılır.
- ZIP: `MOD-0230-review-07-7aa6abc65.zip` · sha256 `6b2d971cc74702ef04c6acd0518c5dc3b3900c2d2ba34673f7c0fbfe2e4eb4ff` ·
  728 183 bayt · 188 dosya + `MANIFEST.sha256`
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-07/`; git'in
    izlemediği klasör, depoya girmez, sahip iletir.
  - 179 kaynak dosya `git archive 7aa6abc65` baytlarıyla aynı (ölçüldü); DCP-004 kopyası önceki paketlerdeki maskeli
    kopyadır (kaynak değişmedi, ölçüldü). E-posta taraması: yalnız test değeri `reporter@example.test`. Paket 06'ya göre ek:
    karar kaydı 05, inceleme 06 bulgu dosyası ve karşılaştırma notu 06, REVIEW-REQUEST-06.
  - `excerpts/`: kayıt satırları, Blueprint 8.8 satırları, `AGENTS.md` §3, ocelot PV route'ları, DataTable doğrulayıcı çıktısı
    (kaynakları değişmedi; önceki pakettekiler), yeni onay kartı, paketin inceleme 06 sürümü ve iki sürüm arası diff
    (`MOD-0230-pack-diff-31f02845-to-10fdbe5c.diff`).
- Beyanlar (inceleyen bilsin):
  - Revizyon 07'yi `module-pack-author` alt ajanı yazdı; ajanın beyan ettiği effort "15", uygulamanın ölçtüğü oturum effort'u
    high. Control Tower'ın mekanik denetimi geçti; içerik denetimi bu incelemenin işidir.
  - KR-5'in ilk taslağı "adın ilk kez dolduğu kaydetme" diyordu; yazar, MOD-0150 sözleşmesinden önce adı girilmiş kayıtların
    hiç aranmayabileceğini gösterdi; sahip kuralı commit'ten önce netleştirdi ("henüz aranmamış, ad dolu, arama yapılabilir;
    ilk kaydetme"). Commit edilen kayıt yalnız netleştirilmiş hâli içerir.
  - Kayıtlarda açıkça yazmayan, yazarın ya da Control Tower'ın okuması olan noktalar: "dolu" = kırpıldıktan sonra boş değil;
    "MOD-0150 sözleşmesi var" = okuma portu kayıtlı ve gerçek; CRM sözleşme varken o kaydetmede okunamazsa (kesinti) durum
    `Pending` kalır, kaydetme reddedilmez (EK-3), arama sonraki uygun kaydetmede yeniden denenir (KR-5'in "aramanın
    yapılabildiği" ifadesinin okuması, yeni karar olarak sunulmadı).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-10fdbe5c.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict …
  --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` →
  `reviews.tsv` satırı → UYGUN ya da açık ENGEL'siz KOŞULLU ise `pack_approval.py row --risk HIGH --author GMG-ERP
  --approver bestepullukcu-alt` → `check` → `main` ile birleştirme ve kontroller (§7a) → tek PR en sonda, bestepullukcu-alt
  merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack bağımsız incelemesi 07 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-07-7aa6abc65.zip · sha256 6b2d971cc74702ef04c6acd0518c5dc3b3900c2d2ba34673f7c0fbfe2e4eb4ff · kaynak commit 7aa6abc65 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 10fdbe5c6a5b8510eda1605c8ac25cb9fa6ee456efce46ddfa15252709995d66 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini (UUID ya da araçtaki kimlik) yanıtın başına yaz (reviews.tsv beyanı).

Önce oku (sırayla, hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-31f02845-to-10fdbe5c.diff; inceleme 06 bulguları docs/records/reviews/MOD-0230/2026-10-22-31f02845.md ve Control Tower karşılaştırma notu 06 (2026-10-22-31f02845-ct-karsilastirma-06.md); karar kayıtları docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md … -05.md (bu sürümün tek yetkisi; -05 yeni); önceki incelemeler docs/records/reviews/MOD-0230/ altındaki diğer bulgu dosyaları ve karşılaştırma notları; docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md; execution/domains/pharmacovigilance/work-packs/REQ-PVG-CRM-01-visit-report-adverse-event-msl.md; execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md; execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md (satır 392 ve 555); execution/domains/platform-shared-services/module-packs/MOD-0288-organization-person-position-directory.md; services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Contact.cs; execution/domains/pharmacovigilance/domain-config.md; DCP-004 (e-postalar maskeli); .antigravity/rules/module-pack-standard.md, multi-tenancy.md, security-jwt.md, permission-key-standard.md, localization-standard.md, audit-trail-standard.md; .claude/rules/agents-core.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; excerpts/ altındaki bütün dosyalar.
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki kaynak koda karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH):
- İnceleme 06'nın kapanışı: F-1 pakette gerçekten kapandı mı? "Kayıt tamamlanınca" ifadesi yerine gelen beş koşul (Pending, Manual, kaydetmeden sonra dolu ad, MOD-0150 sözleşmesi var, bağ kaldırma işareti yok) KR-5'i eksiksiz ve kayıttan fazlası olmadan mı yazıyor; "bir kez" yalnız durum alanıyla mı sağlanıyor?
- Sınır durumları: oluşturmada ad dolu, adsız (EK-3) kayıt, Pending iken ad değişikliği ya da silinip yeniden girme, Searched sonrası, bağlı bildiren, bağ kaldırma kaydetmesi ve MOD-0150 sözleşmesinden önce adı girilmiş kayıt tek anlamlı mı; KR-4 ile çelişen bir durum var mı?
- CRM kesintisi: sözleşme varken CRM okunamazsa Pending kalıp kaydetmenin reddedilmemesi ve sonraki uygun kaydetmede yeniden denenmesi KR-5 ve EK-3 ile uyumlu mu; Failure Path'teki mevcut "okunamayan kimlik reddedilir" kuralıyla çelişiyor mu; kesinti logu kişisel veri taşımıyor mu?
- Kabul testleri: BG-30'daki testler koşul öncesi sıfır arama çağrısını, koşulun sağlandığı kaydetmede tek aramayı, sonrasında aramamayı, sözleşme öncesi kaydı ve kesintiyi ölçülebilir biçimde (adı belli test, arama çağrısı sayısı) ölçüyor mu; MOD-0150 sözleşmesine bağlı olanlar "after …" ile işaretli mi?
- Karar uyumu: AL-1…AL-7, EK-1…EK-5, KR-1…KR-5 pakete eksiksiz ve kayıtlardan fazlası olmadan işlenmiş mi? İstek kaydındaki "Beyanlar" listesinde okuma olarak sunulan noktalar karar gibi yazılmış mı? OD-PVG-1…12 değişmeden mi kaldı?
- Gerileme: önceki incelemelerde kapanan bulgular (04 F-1…F-5, 05 F-1) bu revizyonda yeniden açıldı mı?
- Genel: hiçbir sözleşme kapalı / onaylı gösterilmiyor mu; MOD-0290 / MOD-0150 / MOD-0288 / MOD-0048 okuma sözleşmeleri "sahip onayı bekliyor" olarak mı duruyor ve PVG bunlara yazmıyor mu (XMC-001); operasyonel runtime kapısı kapalı mı; Teslim dilimleri DL-1…DL-9 çakışmasız mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 10fdbe5c6a5b8510eda1605c8ac25cb9fa6ee456efce46ddfa15252709995d66
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-07/MOD-0230-review-07-7aa6abc65.zip
sha256 6b2d971cc74702ef04c6acd0518c5dc3b3900c2d2ba34673f7c0fbfe2e4eb4ff
```
