# MOD-0286 — bağımsız inceleme isteği 02 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `d15b6c8ab92f0336a3726202db1101fb7a4dce162896001f8e0086eae246f770` (revizyon 02)
- Kaynak commit: 19f0b2f7a (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi) = eb809b1be (rev 02) + `origin/main` 83dab442f merge'i (çakışmasız) · ilk onay → risk HIGH (`pack_approval.py card`)
- Önceki inceleme: 01 (`33dc5e14…`, Codex, **UYGUN DEĞİL**, 3 ENGEL + 5 ORTA; bulgu dosyası `docs/records/reviews/MOD-0286/2026-10-22-33dc5e14.md` @ 64529015e, özeti `fb15f188…`). CT karşılaştırması: sekiz bulgu da ölçümle doğrulandı ve kabul edildi, itiraz yok; CT üç ek bulgu ekledi (C-1 platform fixture, C-2 elle maddelerde rol, C-3 Codex beyanı eksik). `reviews.tsv` satırı sahibin beyanıyla yazılacak; bu istek o satırdan bağımsızdır.
- Bu sürümün nedeni: inceleme 01'in F-1…F-8'i ve C-1/C-2. Rev 02 eşlemesi (paket Implementation Notes): F-1 → koşu sözleşmesi (`sh -c` argv, tam yollar, fixture, beklenen stdout/çıkış; UIC-1/2/7/13/14/17/20/21 yeniden yazıldı); F-2 → UIC-3/6/7 sınırları + UIC-26 alan sayımı; F-3 → UIC-24 zorunlu, UIC-31 dışa aktarma, UIC-32 colReorder; F-4 → UIC-27…30 + kapsama tablosu (94 "zorunlu" ifade: 61 kapsanan, 6 meta, 27 kapsanmayan → FU-11) + AC-15; F-5 → D-8a/b/c + çatışma kaydı tablosu (taraflar, katman, uygulanacak kaynak, karar veren, durum) + AC-3; F-6 → tek koşu `auto-01` + `sabotage-01` akışı + AC-5; F-7 → AC-13 adlı iki kırmızı (`_IndexL10n.cshtml exists`, `index.l10n.js exists`), fiilen ölçüldü (97/1 → 7/2); F-8 → AC-8 bu pakete daraltıldı + FU-9. Yeni: AC-15, AC-16; UIC-26…32; FU-9…11; D-8a..c. Kimlikler korundu (AC-1…14, UIC-1…25, D-1…7).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-22
- İnceleyen: sahip seçer (Codex ya da ayrı Claude oturumu); paketi `module-pack-author` yazdı, yazan oturum incelemez.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high (§17.1 r5); kullanılan model, effort ve oturum kimliği yanıtın başına yazılır (`reviews.tsv` beyanı; inceleme 01'de eksikti).
- ZIP: `MOD-0286-review-02-19f0b2f7a.zip` · sha256 `9474978c3a251975e283f87eca7f0c97a14db954b93d2e7b830e7be13c9e91b3` · 703 379 bayt · 104 dosya + `MANIFEST.sha256` (105)
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-02/MOD-0286-review-02-19f0b2f7a.zip`; depoya girmez, sahip iletir.
  - 91 kaynak dosya `git show 19f0b2f7a:<yol>` baytlarıyla: inceleme 01 ZIP'indeki küme + inceleme 01 kayıtları (istek, bulgu dosyası, CT karşılaştırması), `Views/WorkCenterNext/_L10n.cshtml` (A kalıbı fixture), `Views/Platform/Tenants/*` ve resx'leri (platform fixture), `platform-search.{en,tr}.json`.
  - `excerpts/` (13): inceleme 01'dekiler yeniden ölçüldü (@19f0b2f7a) + `MOD-0286-pack-diff-79f87c1bf-to-19f0b2f7a.diff` (rev 01 → rev 02) + `MOD-0286-pack-review01-version-79f87c1bf.md`.
- Beyanlar (inceleyen bilsin):
  - Revizyon 02'yi `module-pack-author` alt ajanı yazdı (model: oturumun modeli, effort high; ajan tanımından ölçüldü). CT yeni iddiaları yeniden ölçtü: `platform-search.{en,tr}.json:7, :117` Platform/Tenants kayıtları; `WorkCenterNext/_L10n.cshtml` `GetAllStrings(true)` = 1 (5 dosya); `diten-datatable.js:1046` filtre düğmesinde `title`/`aria-controls`/`aria-expanded` var, `aria-label` yok; resx checker tenant 11 `[X]` ("35 key(s)"), platform 1; doğrulayıcı baseline 97/1; sabotaj 7/2 iki adlı kırmızı; `frontend-js-standard.md:76` istisna; `DEV-0000…:16 form_field_count: 6`. Hepsi tutuyor.
  - Bilinen biçim: UIC kimlikleri iki tabloda satır başı olarak geçer (kontrol listesi 32 satır + koşu sözleşmesi 20 satır); AC-4'ün tekillik komutu DL-2 belgesine uygulanır, pakete değil. Paket içinde `^| UIC-n` taraması 20 tekrar gösterir; bu bir hata değil, tasarım. İnceleyen bunu sorun sayarsa söylesin.
  - Rev 01'den taşınan ve rev 02'de yeniden ölçülmeyen atıflar: `AGENTS.md:139`, UI-024 satırı, `product-backlog.md:1819/1972/2567/3661/3995`, 169/9/15 paket sayımları (CT rev 01 denetiminde ölçmüştü; main merge'i bu dosyaları değiştirmedi).
  - Sunucu modlu bir liste fixture'ı UIC-31 için adlandırılmadı (yalnız istemci modlu Golden Slim ölçüldü); AC-13 ve `auto-01` koşuları henüz `run_record.py` ile kaydedilmedi (kayıt DL-2 işinde).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-d15b6c8a.md`
- Kayıt adımları: bulgu dosyası commit → `python3 scripts/pack_approval.py review-row <paket> --ref <commit> --findings <dosya> --verdict … --reviewer codex|claude --model … --effort … --reviewer-session … --author K3 --author-session 05ce4e4b --requested-by GMG-ERP` → `reviews.tsv` satırı → UYGUN ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` → `check` → tek PR en sonda. Her adım sahibin açık onayıyla.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 02 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-02-19f0b2f7a.zip · sha256 9474978c3a251975e283f87eca7f0c97a14db954b93d2e7b830e7be13c9e91b3 · kaynak commit 19f0b2f7a (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 d15b6c8ab92f0336a3726202db1101fb7a4dce162896001f8e0086eae246f770 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı; inceleme 01'de bu üç değer verilmedi, bu kez zorunlu).

Bu revizyon 02'dir. İnceleme 01 (docs/records/reviews/MOD-0286/2026-10-22-33dc5e14.md, UYGUN DEĞİL, F-1…F-8) ZIP'te; CT karşılaştırması (2026-10-22-33dc5e14-ct-karsilastirma-01.md) sekiz bulguyu da kabul etti; rev 01 → rev 02 farkı excerpts/MOD-0286-pack-diff-79f87c1bf-to-19f0b2f7a.diff. Önce F-1…F-8'in her birinin rev 02'de gerçekten kapanıp kapanmadığını ölç; sonra yeni bölümleri incele.

Önce oku (sırayla, hepsi ZIP'te): hedef paket (özellikle "Koşu sözleşmesi", "Kanıt toplama akışı", "Kapsama tablosu", Acceptance Criteria AC-1…16, Implementation Notes çatışma kaydı, Open Decisions D-8a..c); inceleme 01 bulgu dosyası ve CT karşılaştırması; excerpts/* (pack diff, rev 01 sürümü, measurements, registry, blueprint satırları, doğrulayıcı çıktısı, nav anahtarları, keşif raporu, backlog alıntıları); scripts/run_record.py (:79-85, :117-139); .antigravity/scripts/verify_datatable_page.py (:535-536, :937-949, :956); scripts/check_pack_nav_keys.py (:131); .antigravity/rules/module-pack-standard.md (§6, §7, §10.1, §16); on bir UI kural dosyası; docs-organization.md; frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/* + wwwroot/assets/js/DevEnablement/GoldenReferenceSlim/* + Resources/Views/DevEnablement/GoldenReferenceSlim/* (kiracı fixture); frontend/Diten.Web/Views/Platform/Tenants/* + Resources/Views/Platform/Tenants/* + wwwroot/assets/json/platform-search.{en,tr}.json (platform fixture); Views/WorkCenterNext/_L10n.cshtml (A kalıbı fixture); wwwroot/assets/js/diten-datatable.js (:907-911, :994, :1046, :1052, :1083), dt-defaults.js, personalization-client.js; execution/registries/module-id-registry.md; execution/approvals/POLICY-REVIEW-START.md.
Derleme ve test koşamazsın; paketin iddialarını ZIP'teki dosyalara karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH — ilk onay):
- Kapanış denetimi: F-1 (koşu sözleşmesindeki her argv bloğu depo kökünde `sh -c` içinde gerçekten koşar mı: yollar ZIP'te var mı, `$G/$J/$R/$T` tanımlı mı, beklenen stdout değerleri fixture dosyalarıyla tutuyor mu — en az UIC-1, 2, 4, 7, 13, 17, 26, 27, 29'u kendin ölç); F-2 (UIC-3/6/7/26 sınırları doğru mu; UIC-6 için Golden Slim'de beklenen 0 ve WorkCenterNext'te 1 tutuyor mu); F-3 (UIC-24/31/32 zorunlu ve kuralların istisnaları doğru taşınmış mı: `frontend-js-standard.md:76`, `diten-datatable.js:907-911`); F-4 (kapsama tablosundaki sayılar `grep -n -i zorunlu | wc -l` ile tutuyor mu; "kapsanan" eşlemeleri gerçekten o UIC maddesiyle ölçülüyor mu; 27 kapsanmayan listesi doğru mu); F-5 (çatışma kaydı: taraflar/katman/uygulanacak/karar veren/durum; hiçbir kural-kural satırı "çözüldü" değil mi; AC-3 buna uyuyor mu); F-6 (tek koşu akışı `run_record.py` ile uyumlu mu: temiz ağaç, tek `--out`, stdout'tan madde başına kabul; "koşunun kendi çıkışı kayıt amaçlı" ifadesi kabul kuralını zayıflatıyor mu); F-7 (AC-13: scratch kökü depo biçiminde mi, adlı kırmızılar doğrulayıcı kaynağında var mı `:956`, mutlak yol sınırı yazılı mı); F-8 (AC-8 daraltması `grep -c MOD-0286; true` ile ne ölçüyor, "0" yanlış pozitif olabilir mi — ör. betik hiç koşmazsa da 0).
- Yeni riskler: koşu sözleşmesinde beklenen değerlerin fixture'a (Golden Slim / Platform/Tenants) bağlı olması — fixture değişince sözleşme bozulur mu, paket bunu söylüyor mu; UIC kimliklerinin iki tabloda tekrarı (bkz. beyan) belgeye taşındığında AC-4 ile çelişir mi; AC-5'in "22 otomatik madde" sayısı tabloyla tutuyor mu (20 satır, gruplu kimlikler); DL-1/DL-2 dilim tablosu yeni AC'leri (15, 16) kapsıyor mu; Teslim dilimleri her AC'yi tek dilime mi koyuyor.
- Kimlik ve kapsam (rev 01'deki gibi): frontmatter ↔ kayıt satırı 164 ↔ Blueprint; P1'in hiçbir cümlesi kod/`.antigravity` yetkisi vermiyor mu; D-1…D-8 "karar bekliyor" mu; DL-3 WP'ye verilemez mi.
- Genel: belirsiz ifade; ölçülmemişken "ölçüldü" denen yer; `owner: TBD` / `target: TBD`; FU regresyon işaretleri gerekçeli mi.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ d15b6c8ab92f0336a3726202db1101fb7a4dce162896001f8e0086eae246f770
değerlendirilen olumsuz incelemeler: fb15f188
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma; inceleme 01'in bulgularından kapanmamış saydığın varsa aynı numarayla değil, yeni F numarasıyla ve "01 F-n kapanmadı" diyerek yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-02/MOD-0286-review-02-19f0b2f7a.zip
sha256 9474978c3a251975e283f87eca7f0c97a14db954b93d2e7b830e7be13c9e91b3
```
