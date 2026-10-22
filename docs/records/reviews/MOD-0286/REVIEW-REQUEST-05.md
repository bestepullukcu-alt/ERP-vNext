# MOD-0286 — bağımsız inceleme isteği 05 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `d66d59cf0a82524af8c480fcdbe82fcd75ad7244853916e7111d19d9717a2a6f` (revizyon 05)
- Kaynak commit: 5e46ef8b6 (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi; `origin/main` 83dab442f'yi içerir) · ilk onay → risk HIGH
- Önceki incelemeler (`execution/approvals/reviews.tsv`, dört satır, ZIP'te): 01 `33dc5e14` UYGUN DEĞİL (`fb15f188`), 02 `d15b6c8a` UYGUN DEĞİL (`7242768b`), 03 `9d075026` KOŞULLU (`c710544a`), 04 `75bad532` **KOŞULLU**, 0 ENGEL, F-16/F-17 ORTA (`d77fc1d3`). CT dördünde de bütün bulguları kabul etti; itiraz yalnız 02'nin iki etiketine oldu ve inceleyen 03'te katıldı.
- Bu sürümün nedeni: inceleme 04'ün F-16/F-17'si. Rev 05: **RC-24** (yeni) dil başına eksik ve "İngilizceyle aynı" anahtar kümelerini resx'lerden kendisi çıkarır (`grep`/`awk`/`sort`/`comm`/`join`), baseline kümeleri `resx-baseline/{missing,equal}-<lang>.txt` ilk koşudan önce commit edilir, kabul = `comm -13 baseline current` boş (yeni anahtar yok); RC-22 yalnız Title Case = 0 + araç-hata kapısı, sayımlar kayıt. **RC-23** tüm sayısal `Step<n>` son eklerini çıkarıp `seq 1 k` ve `StepTitle` kümesiyle `diff` eder, 1 ≤ k ≤ 5; `Step[6-9]` deseni geri çekildi. Ölçüldü (CT yeniden koştu, aynı): RC-24 tr 0/10, fr 35/47, es 35/39, zh/ar/ru 35/35, eksik kümeler beş dilde birebir; RC-23 7 dilde `k=5 seq_diff=empty title_diff=empty`; sabotajlar (Step10 ekleme, Step2 silme, Step4Title silme, baseline'dan anahtar silme) kırmızı. Kapsama 94 = 57 / 6 / 31 (`localization-standard.md:21` kısmi kalır: RC-24 eşitlik ölçer, casing değil; FU-13). Kimlikler korundu (UIC-1…32, RC-1…24, AC-1…16, D-1…D-8c).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: sahip seçer; önceki dördü Codex. Yazan oturum incelemez.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high; beyan verilemiyorsa "verilemiyor" (sahip kararı `beyan-yok`).
- ZIP: `MOD-0286-review-05-5e46ef8b6.zip` · sha256 `dd3b27252047ed9336aea671909d763a7c97ffb7d44b65c77d6e12b3528ac3dc` · 1 026 322 bayt · 129 dosya + `MANIFEST.sha256` (130)
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-05/MOD-0286-review-05-5e46ef8b6.zip`; depoya girmez, sahip iletir.
  - 115 kaynak dosya `git show 5e46ef8b6:<yol>` baytlarıyla: inceleme 04 ZIP'indeki küme + inceleme 04 kayıtları; `excerpts/` (14): yeniden ölçülmüş alıntılar (@5e46ef8b6) + `MOD-0286-pack-diff-eece75428-to-5e46ef8b6.diff` (rev 04 → rev 05) + `MOD-0286-pack-previous-version-eece75428.md` + `l10n-evidence.txt`.
- Beyanlar (inceleyen bilsin):
  - Revizyon 05'i `module-pack-author` yazdı (effort high); CT RC-24 ve RC-23'ü yazarın betikleriyle yeniden koştu (aynı değerler), kimlikleri ve `Step[6-9]` kalıntısını (yalnız "geri çekildi" notlarında) ölçtü.
  - Henüz yazılmayanlar (pakette yazılı, DL-2 işinde): `uic-auto.sh`, `uic-sabotage.sh`, `uic-resx-baseline.sh` betikleri ve `resx-baseline/` dosyaları; `run_record.py run` koşulmadı. Çok satırlı `<value>` içeren `<data>` bugün yok; RC-24 bu durumu okumaz (sınır olarak yazılı).
  - Hedef: iki ORTA koşul kapandıysa UYGUN. Yeni bulgu varsa F-18'den başlar.
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-d66d59cf.md`
- Kayıt adımları: bulgu dosyası commit → `review-row --ref <commit> …` → `reviews.tsv` → UYGUN ise sahip kararları (D-1 belge yeri, D-2 kurallar yerinde, `owner`/`target`) → `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` → `check` → `yedek` push → tek PR en sonda. Her adım sahibin açık onayıyla.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 05 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-05-5e46ef8b6.zip · sha256 dd3b27252047ed9336aea671909d763a7c97ffb7d44b65c77d6e12b3528ac3dc · kaynak commit 5e46ef8b6 (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 d66d59cf0a82524af8c480fcdbe82fcd75ad7244853916e7111d19d9717a2a6f — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 05'tir. İnceleme 04 KOŞULLU verdi (docs/records/reviews/MOD-0286/2026-10-22-75bad532.md: F-16 RC-22 satır sayısı, F-17 RC-23 Step[6-9]); CT karşılaştırması 04 ve rev 04 → rev 05 farkı (excerpts/MOD-0286-pack-diff-eece75428-to-5e46ef8b6.diff) ZIP'te. Önce F-16 ve F-17'nin kapanıp kapanmadığını ölç; sonra 01–03'ün bulgularının hâlâ kapalı olduğunu doğrula; sonra kalan bölümleri.

Önce oku (sırayla, hepsi ZIP'te): hedef paket (UIC-7, UIC-15; RC-22, RC-23, RC-24; "Koşu sözleşmesi" girişi ve değişken satırı; "Kanıt toplama akışı" 1-5; "Kapsama tablosu"; AC-4, AC-5, AC-16; Test Expectations "Rev 05 ölçümleri"; Failure Path yeni vakalar; Implementation Notes rev 05 satırları); dört bulgu dosyası ve dört CT karşılaştırması; execution/approvals/reviews.tsv (4 satır); excerpts/l10n-evidence.txt ve diğer alıntılar; frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx; frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx; frontend/Diten.Web/Services/Help/PageHelpCatalog.cs; .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py (:33-49, :71-95, :169-191); .antigravity/rules/localization-standard.md (:21, :29-34), page-help-and-keyboard.md (:50-53); scripts/run_record.py (:117-139); diğer kural dosyaları ve kaynaklar önceki isteklerdeki gibi.
Derleme ve test koşamazsın; iddiaları ZIP'teki dosyalara karşı statik olarak kendin ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek bir "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- F-16 kapanışı: RC-24'ün anahtar ve değer çıkarımı (`<data name="` çapası, awk çift çıkarımı, tek satır / üç satır biçimleri) ZIP'teki resx dosyalarıyla doğru mu — en az iki dil için eksik ve eşit kümeleri kendin kur ve paketin sayılarıyla (tr 0/10, fr 35/47, es 35/39, zh/ar/ru 35/35) karşılaştır; izinli-eşit anahtarların (`:33-49`) baseline'da taşınması ve `is_allowed_same_as_en` (`:89-95`) ile farkın açıklaması tutuyor mu; `comm -13` kabulü yeni anahtarı gerçekten yakalar mı (sabotaj mantığı); baseline dosyalarının ilk koşudan önce commit edilmesi `run_record.py` sözleşmesiyle (`:117-120`, `:137-139`) uyumlu mu; çok satırlı `<value>` sınırı doğru ifade edilmiş mi.
- F-17 kapanışı: RC-23 son ek çıkarımı (`Step[0-9]*"` ve `Step[0-9]*Title"`) doğru mu; `seq 1 k | diff` ve `diff steps titles` kabulü boşluk, sınır dışı ve yetim başlığı yakalar mı (üç sabotaj sonucu makul mu); k=0 davranışı (BSD `seq 1 0`) doğru yazılmış mı; UIC-15 metni `page-help-and-keyboard.md:50-53` ile birebir mi; kapsama satırı (:50, :51) doğru RC'ye eşli mi.
- Kalan: 01 F-1…F-8, 02 F-9…F-12, 03 F-13…F-15 hâlâ kapalı mı; AC-5/AC-16'nın RC-24'ü kapsaması; kapsama 57/6/31 aritmetiği; kimlik ve kapsam (frontmatter ↔ kayıt 164 ↔ Blueprint; P1 kod/`.antigravity` yetkisi vermiyor; D-1…D-8c açık; DL-3 WP'ye verilemez); `owner: TBD` / `target: TBD`; FU-11…FU-13 regresyon işaretleri gerekçeli mi.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ d66d59cf0a82524af8c480fcdbe82fcd75ad7244853916e7111d19d9717a2a6f
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-18 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-19 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-20 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <madde kimlikleri ve sonuç, tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 18'den başlar (F-1…F-17 kullanılmış); kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma; önceki bir bulgunun kapanmadığını düşünüyorsan yeni numarayla ve "0N F-n kapanmadı" diyerek yaz, kalan dar bir açıksa yeni bulgu olarak yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-05/MOD-0286-review-05-5e46ef8b6.zip
sha256 dd3b27252047ed9336aea671909d763a7c97ffb7d44b65c77d6e12b3528ac3dc
```
