# MOD-0286 — bağımsız inceleme isteği 03 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `9d075026ca9c2d703125cec9612b511bc12fc59a1c9d47ed0559b380711f0050` (revizyon 03)
- Kaynak commit: 482543721 (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi; `origin/main` 83dab442f'yi içerir) · ilk onay → risk HIGH
- Önceki incelemeler (`execution/approvals/reviews.tsv`, iki satır, ZIP'te): 01 (hedef `33dc5e14…` @ 64529015e, Codex, **UYGUN DEĞİL** 3 ENGEL + 5 ORTA, bulgu özeti `fb15f188…`); 02 (hedef `d15b6c8a…` @ 20b3a2ca6, Codex, **UYGUN DEĞİL** 1 ENGEL + 3 ORTA, bulgu özeti `7242768b…`). İkisinde de CT bütün bulguları ölçümle kabul etti; 02'de iki etikete itiraz: F-10 "01 F-8 kapanmadı" ve F-11 "01 F-4 kapanmadı" abartılı (ana istekler rev 02'de karşılanmış, kalan açıklar yeni ve dar; `2026-10-22-d15b6c8a-ct-karsilastirma-02.md`). Beyan: Codex iki istekte de model/effort/oturum veremedi; sahip kararı (`beyan-yok`) ile satırlar Codex'in kendi cümleleriyle yazıldı.
- Bu sürümün nedeni: inceleme 02'nin F-9…F-12'si. Rev 03 eşlemesi (paket Implementation Notes): F-9 → UIC-24 istisnasız zorunlu (`frontend-js-standard.md:73`, `frontend-datatable-template.md:24`), UIC-32 istisnası kaynağın sözüyle (`:76`, ≤2 veri kolonu + yorum); F-10 → UIC-21 ve AC-8 dört koşul (`rc`, "could not read" = 0, MOD-0286 = 0, `git ls-files` = 1), `; true` yok, RC-11 ölçüldü (`rc=1`, `0`, `0`, `1`; CT yeniden koştu, aynı); F-11 → üç eşleme "kapsanmayan"a, bir eşleme düzeltildi, toplam 94 = 58 / 6 / 30, AC-15 ve FU-11 güncel; F-12 → koşu sözleşmesi `RC-1…RC-21` anahtarlı ("UIC kapsamı" sütunu), pakette `^| UIC-n` tekrarı 0, AC-4/AC-16 buna göre. Kimlikler korundu (UIC-1…32, AC-1…16, D-1…D-8c); yeni kimlik yok.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: sahip seçer; önceki ikisi Codex. Yazan oturum incelemez.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high; yanıtın başında beyan (verilemiyorsa "verilemiyor" yazılır; bu da kayda girer).
- ZIP: `MOD-0286-review-03-482543721.zip` · sha256 `78f3741340a3bd9eb9011ccc600a6bd5c8900db046ca97348583c97b6748d6a4` · 719 843 bayt · 107 dosya + `MANIFEST.sha256` (108)
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-03/MOD-0286-review-03-482543721.zip`; depoya girmez, sahip iletir.
  - 94 kaynak dosya `git show 482543721:<yol>` baytlarıyla: inceleme 02 ZIP'indeki küme + inceleme 02 kayıtları (istek, bulgu dosyası, CT karşılaştırması) + güncel `reviews.tsv` (2 satır).
  - `excerpts/` (13): yeniden ölçülmüş alıntılar (@482543721) + `MOD-0286-pack-diff-19f0b2f7a-to-482543721.diff` (rev 02 → rev 03) + `MOD-0286-pack-previous-version-19f0b2f7a.md`.
- Beyanlar (inceleyen bilsin):
  - Revizyon 03'ü `module-pack-author` yazdı (effort high); CT kimlikleri, tekrarları, istisna çapalarını ve RC-11'i yeniden ölçtü. Rev 03'te yazar bilinçli sapmalar bıraktı ve pakete yazdı: `/tmp` yerine `$OUT/navkeys.out` (koşu dizini, `run_record.py:141-151` artefakt); `frontend-js-standard.md:289` için yeni UIC açılmadı (Golden Slim `index.js`'te await satırı yok, fabrikanın içinde `diten-datatable.js:1031`), "kapsanmayan"a alındı; UIC-15'e `Related` koşulu eklendi (taşınmadı), fikstür USERS'ta 7 dilde ölçüldü.
  - Rev 03'te yeniden koşturulmayanlar (pakette yazılı): doğrulayıcı RC-3/12/14 (rev 02 ölçümü `79f87c1bf`); `run_record.py run` (henüz koşulmadı); UIC-32 için override'lı bir sayfa ölçülmedi.
  - CT'nin isteği (C-4): bu kez **ölçtüğün maddeleri ve sonucunu yaz** — bulgu yokluğu ölçüm kanıtı sayılmaz.
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-9d075026.md`
- Kayıt adımları: bulgu dosyası commit → `review-row --ref <commit> …` → `reviews.tsv` → UYGUN ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` → `check` → tek PR en sonda. Her adım sahibin açık onayıyla.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 03 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-03-482543721.zip · sha256 78f3741340a3bd9eb9011ccc600a6bd5c8900db046ca97348583c97b6748d6a4 · kaynak commit 482543721 (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 9d075026ca9c2d703125cec9612b511bc12fc59a1c9d47ed0559b380711f0050 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 03'tür. İnceleme 01 ve 02 (docs/records/reviews/MOD-0286/2026-10-22-33dc5e14.md, 2026-10-22-d15b6c8a.md) ve CT karşılaştırmaları ZIP'te; rev 02 → rev 03 farkı excerpts/MOD-0286-pack-diff-19f0b2f7a-to-482543721.diff. Önce F-9…F-12'nin rev 03'te kapanıp kapanmadığını ölç; sonra 01'in F-1…F-8'inin hâlâ kapalı olduğunu doğrula (02'de iki etikete CT itirazı var: karşılaştırma 02'yi oku ve kendi görüşünü yaz); sonra yeni bölümleri incele.

Önce oku (sırayla, hepsi ZIP'te): hedef paket (UIC-24, UIC-32, UIC-21, UIC-15; "Koşu sözleşmesi" RC-1…RC-21; "Kanıt toplama akışı"; "Kapsama tablosu"; AC-4, AC-5, AC-8, AC-15, AC-16; Implementation Notes rev 03 satırları); iki bulgu dosyası ve iki CT karşılaştırması; execution/approvals/reviews.tsv (2 satır); excerpts/* (pack diff, önceki sürüm, measurements, nav-keys, registry, blueprint satırları, doğrulayıcı çıktısı, keşif raporu, backlog alıntıları); scripts/check_pack_nav_keys.py (:131, :200-213); scripts/run_record.py (:79-85, :117-151); .antigravity/scripts/verify_datatable_page.py; .antigravity/rules/frontend-js-standard.md (:52-53, :73-76, :289, :981), frontend-datatable-template.md (:24, :107-129, :192, :238, :630-631), page-help-and-keyboard.md (:48-53, :105-118), module-pack-standard.md (§6, §7); frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/* + wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/* + Resources/Views/DevEnablement/GoldenReferenceSlim/* (kiracı fixture); frontend/Diten.Web/Views/Platform/Tenants/* + Resources/Views/Platform/Tenants/* + wwwroot/assets/json/platform-search.{en,tr}.json (platform fixture); Views/WorkCenterNext/_L10n.cshtml; wwwroot/assets/js/diten-datatable.js (:907-911, :994, :1031, :1046, :1052, :1083); execution/registries/module-id-registry.md; execution/approvals/POLICY-REVIEW-START.md.
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki dosyalara karşı statik olarak kendin ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek bir "Ölçülenler:" satırında (madde kimlikleri ve sonuç, | işareti kullanmadan) yaz.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- Kapanış denetimi 02: F-9 (UIC-24 istisnasız mı; UIC-32 istisnası `:76` ile birebir mi; koşu sözleşmesi ve kapsama tablosu buna uyuyor mu); F-10 (RC-11 dört koşulu araç hatasını gerçekten yakalar mı — `check_pack_nav_keys.py:207-211` "could not read" dışında başka bir hata metni var mı, `rc=2` hangi koşulda yakalanıyor; "paket tarandı" kanıtı olarak `git ls-files` yeterli mi); F-11 (kapsama tablosunun "kapsanan" hücrelerini en az 10 örnekle kendin doğrula: eşlenen UIC/RC ölçümü o cümleyi ölçüyor mu; 58/6/30 toplamı tutuyor mu; `localization-standard.md` satırının düzeltmesi doğru mu); F-12 (pakette `^| UIC-n` tekrarı 0 mı; RC kapsamı 22 otomatik kimliği eksiksiz taşıyor mu; AC-4 ve AC-16 tutarlı mı; DL-2 belgesi iki bölümlü tanımı Owned Objects'ta yazılı mı).
- Kapanış denetimi 01 (yeniden): F-1…F-8 rev 03'te hâlâ kapalı mı; özellikle F-6 kanıt akışı RC anahtarlarıyla tutarlı mı ve F-7 AC-13 adlı kırmızılar hâlâ geçerli mi.
- Yeni riskler: koşu sözleşmesinde beklenen değerlerin fixture'a bağlılığı (fixture değişirse ne olur, paket söylüyor mu); UIC-15'in `Related` koşulu `PageHelpCatalog.cs:17-24` ile ölçülebilir mi, fixture USERS değerleri (2/5) tutuyor mu; UIC-7'nin yeni ölçümleri (modül `Title/PageDescription`, SharedResource 12 anahtar) kaynak kuralla uyumlu mu; `$OUT` değişkeninin `run_record.py` koşu diziniyle ilişkisi doğru mu (`:127`, `:141-151`).
- Kimlik ve kapsam: frontmatter ↔ kayıt satırı 164 ↔ Blueprint; P1 kod/`.antigravity` yetkisi vermiyor mu; D-1…D-8c "karar bekliyor" mu; DL-3 WP'ye verilemez mi.
- Genel: belirsiz ifade; ölçülmemişken "ölçüldü" denen yer; `owner: TBD` / `target: TBD`; FU regresyon işaretleri gerekçeli mi.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 9d075026ca9c2d703125cec9612b511bc12fc59a1c9d47ed0559b380711f0050
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-13 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-14 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-15 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <madde kimlikleri ve sonuç, tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 13'ten başlar (01 ve 02'nin F-1…F-12'si kullanılmış); kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma; önceki bir bulgunun kapanmadığını düşünüyorsan yeni numarayla ve "0N F-n kapanmadı" diyerek yaz; CT'nin etiket itirazına katılıp katılmadığını tek cümleyle, düz metin olarak, Sonuç satırından önce yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-03/MOD-0286-review-03-482543721.zip
sha256 78f3741340a3bd9eb9011ccc600a6bd5c8900db046ca97348583c97b6748d6a4
```
