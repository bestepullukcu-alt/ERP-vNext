# MOD-0286 — bağımsız inceleme isteği 04 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `75bad5321a48e61298953066d0650812650375f9b9675f996dfc41c006d4fbc5` (revizyon 04 + 04b)
- Kaynak commit: eece75428 (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi; `origin/main` 83dab442f'yi içerir) · ilk onay → risk HIGH
- Önceki incelemeler (`execution/approvals/reviews.tsv`, üç satır, ZIP'te): 01 `33dc5e14` UYGUN DEĞİL (özet `fb15f188`), 02 `d15b6c8a` UYGUN DEĞİL (özet `7242768b`), 03 `9d075026` **KOŞULLU**, 0 ENGEL, F-13…F-15 ORTA (özet `c710544a`). CT üçünde de bütün bulguları kabul etti; 03'te tek itiraz etiketteydi (F-13/F-14 "02 F-11 kapanmadı"), inceleyen aynı yanıtta CT'nin görüşüne katıldı.
- Bu sürümün nedeni: inceleme 03'ün F-13/F-14'ü (paket) ve F-15'i (ZIP içeriği, CT). Rev 04: UIC-7 §2 zorunlu anahtarları (`localization-standard.md:31-34`) + Title Case ayrı ölçümü (RC-22, checker çıktısı dosyaya; `TITLE_CASE_KEYS = {"SaveView"}`, `NO_CASE_LANGS = {zh, ar}`); UIC-15 Related anahtarları kataloğun kendi kodlarıyla + Step 1..5 boşluk ve Step/StepTitle eşleşmesi (RC-23; USERS fixture 7 dilde ölçüldü, üç sabotaj kırmızı); kapsama 94 = 57 / 6 / 31; FU-12 (Golden Slim'de `AddNew{Module}` yok), FU-13 (checker Title Case yalnız `SaveView`). Rev 04b (yazar bulgusu, CT kabul): her RC bloğu tek tırnak içerdiğinden `sh -c '<script>'` argv olarak koşamazdı → önce commit edilen betik dosyaları `uic-auto.sh` / `uic-sabotage.sh`, `sh <dosya> <koşu-dizini>` ile; `run_record.py:117-120` yalnız `--out` yolunu tarar (betik kardeş dizinde). Kimlikler korundu (UIC-1…32, RC-1…23, AC-1…16, D-1…D-8c).
- F-15 için ZIP'e eklenenler: 7 `SharedResource.*.resx`, 7 `PageHelpResource.*.resx`, `Services/Help/PageHelpCatalog.cs`, inceleme 03 kayıtları; `excerpts/l10n-evidence.txt` (checker çıktıları tenant/platform, kataloğun USERS satırları, dil başına `Help.USERS.*` anahtar listeleri, `SharedResource` giriş sayıları, Golden Slim resx anahtar listeleri).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: sahip seçer; önceki üçü Codex. Yazan oturum incelemez.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high; beyan verilemiyorsa "verilemiyor" (sahip kararı `beyan-yok` ile kayda girer).
- ZIP: `MOD-0286-review-04-eece75428.zip` · sha256 `12cbcddd5a8bacded9dc3e0230d275a59ee43e0ce853b31e1243bbcc4dc1736e` · 1 006 441 bayt · 126 dosya + `MANIFEST.sha256` (127)
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-04/MOD-0286-review-04-eece75428.zip`; depoya girmez, sahip iletir.
  - 112 kaynak dosya `git show eece75428:<yol>` baytlarıyla; `excerpts/` (14): yeniden ölçülmüş alıntılar (@eece75428) + `MOD-0286-pack-diff-482543721-to-eece75428.diff` (rev 03 → rev 04) + `MOD-0286-pack-previous-version-482543721.md` + `l10n-evidence.txt`.
- Beyanlar (inceleyen bilsin):
  - Revizyon 04/04b'yi `module-pack-author` yazdı (effort high); CT RC-23'ü 7 dilde yeniden koştu (aynı), checker sabitlerini, Golden Slim `AddNew` boşluğunu ve `run_record.py` kapsamını kodda doğruladı.
  - Rev 04'te yeniden koşturulmayanlar (pakette yazılı): `run_record.py run` (betikler henüz commit edilmedi; DL-2 işinde), RC-6 marker/Tenants kontrolleri (rev 03 değerleri), RC-3/12/14 doğrulayıcı (rev 02 ölçümü).
  - Hedef: üç ORTA koşul kapandıysa UYGUN; kapanmayan varsa yeni numarayla ("03 F-n kapanmadı" demeden önce karşılaştırma 03'teki etiket notunu oku).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-75bad532.md`
- Kayıt adımları: bulgu dosyası commit → `review-row --ref <commit> …` → `reviews.tsv` → UYGUN ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` → `check` → tek PR en sonda. Her adım sahibin açık onayıyla. Onaydan önce sahip kararları: D-1 belge yeri, D-2 kurallar yerinde, `owner`/`target` doldurulması.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 04 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-04-eece75428.zip · sha256 12cbcddd5a8bacded9dc3e0230d275a59ee43e0ce853b31e1243bbcc4dc1736e · kaynak commit eece75428 (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 75bad5321a48e61298953066d0650812650375f9b9675f996dfc41c006d4fbc5 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 04'tür (04b dahil). İnceleme 03 KOŞULLU verdi (docs/records/reviews/MOD-0286/2026-10-22-9d075026.md: F-13 localization kapsama + Title Case, F-14 Related/Step eşleşmesi, F-15 ZIP'te kanıt eksikti); CT karşılaştırması 03 ve rev 03 → rev 04 farkı (excerpts/MOD-0286-pack-diff-482543721-to-eece75428.diff) ZIP'te. Önce F-13, F-14, F-15'in kapanıp kapanmadığını ölç; sonra 04b'yi (betik dosyası sözleşmesi) ve 01/02'nin bulgularının hâlâ kapalı olduğunu doğrula; sonra kalan bölümleri.

Önce oku (sırayla, hepsi ZIP'te): hedef paket (UIC-7, UIC-15; RC-6, RC-21, RC-22, RC-23; "Koşu sözleşmesi" girişi; "Kanıt toplama akışı" 1-5; "Kapsama tablosu"; AC-4, AC-5, AC-13, AC-15, AC-16; Test Expectations "Rev 04 ölçümleri"; Implementation Notes rev 04/04b satırları; FU-11…FU-13); üç bulgu dosyası ve üç CT karşılaştırması; execution/approvals/reviews.tsv (3 satır); excerpts/l10n-evidence.txt ve diğer alıntılar; frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx; frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx; frontend/Diten.Web/Services/Help/PageHelpCatalog.cs (:17-24); frontend/Diten.Web/Resources/Views/DevEnablement/GoldenReferenceSlim/*.resx ve Views/DevEnablement/GoldenReferenceSlim/_DataTable.cshtml (:17-21), _IndexL10n.cshtml, wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/index.js; .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py (:54-62, :193-201); .antigravity/rules/localization-standard.md (:21, :29-34), page-help-and-keyboard.md (:48-53); scripts/run_record.py (:117-139); diğer kural dosyaları ve kaynaklar önceki isteklerdeki gibi.
Derleme ve test koşamazsın; iddiaları ZIP'teki dosyalara karşı statik olarak kendin ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek bir "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- F-13 kapanışı: UIC-7'nin §2 anahtar listesi `localization-standard.md:31-34` ile birebir mi; RC-6 bu anahtarları Golden Slim resx'lerinde 7 dilde ölçüyor mu (beklenen 6 / 0 / 1 değerlerini l10n-evidence.txt ve resx dosyalarıyla doğrula); `AddNew{Module}` eksikliği FU-12 olarak doğru sınıflandırılmış mı (XMC-001); RC-22 Title Case sayımı checker `:193-201` ile uyumlu mu, "baseline kabul değil, yeni hata 0" kuralı tutarlı mı; kapsama satırı 6 kapsanan + `:21` kısmi doğru mu.
- F-14 kapanışı: RC-23 kataloğun USERS satırından kodları doğru çıkarıyor mu (`[new(` çapası, `:20`; `:29` StepIcons satırına takılmıyor mu); her kod için 7 dilde `Related.<KOD>` = 1; Step 1..5 boşluk ve Step/StepTitle eşleşmesi mantığı doğru mu; Step6+ = 0; üç sabotaj sonucu makul mu; UIC-15 metni `page-help-and-keyboard.md:50-53` ile birebir mi.
- F-15 kapanışı: ZIP'teki 14 resx + PageHelpCatalog.cs + l10n-evidence.txt paketin iddialarını (SharedResource 12 anahtar, USERS 2/5/5/2, iki ilişki) bağımsız doğrulamaya yetiyor mu; yetmiyorsa eksik olanı adıyla yaz.
- 04b: betik dosyası sözleşmesi `run_record.py` ile uyumlu mu (`:117-120` `--out` kapsamı, `:131` kabuksuz argv, `:137-139` koşu dışı değişiklik); `sh -c` hiçbir satırda hâlâ öngörülmüyor mu (5 geçiş yalnız "kullanılmaz" anlamında olmalı); AC-5/AC-13/AC-16 argv'leri doğru mu.
- Kalan: 01 F-1…F-8 ve 02 F-9…F-12 hâlâ kapalı mı (özellikle F-6 kanıt akışı ve F-12 RC anahtarı); kapsama 57/6/31 aritmetiği; kimlik ve kapsam (frontmatter ↔ kayıt 164 ↔ Blueprint; P1 kod/`.antigravity` yetkisi vermiyor; D-1…D-8c açık; DL-3 WP'ye verilemez); `owner: TBD` / `target: TBD` ready-for-dev öncesi açık mı; FU regresyon işaretleri gerekçeli mi.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 75bad5321a48e61298953066d0650812650375f9b9675f996dfc41c006d4fbc5
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-16 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-17 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-18 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <madde kimlikleri ve sonuç, tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 16'dan başlar (F-1…F-15 kullanılmış); kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma; önceki bir bulgunun kapanmadığını düşünüyorsan yeni numarayla ve "0N F-n kapanmadı" diyerek yaz, kalan dar bir açıksa "kapanmadı" yerine yeni bulgu olarak yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-04/MOD-0286-review-04-eece75428.zip
sha256 12cbcddd5a8bacded9dc3e0230d275a59ee43e0ce853b31e1243bbcc4dc1736e
```
