# MOD-0230 — bağımsız inceleme isteği 13 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`, sha256 `2d3f1177e763bc403a679ad945dcff39f47b05820d85e1d8c6cef19407597e16` (revizyon 13).
- **Kaynak commit:** `7e574ecf1`. Dal `chore/ct/pvg-ct-01`, yerel, push edilmedi.
- **Risk:** Onay kartı HIGH hesaplıyor (kart sha256 `7cc4417b…`). Bağımsız inceleme bu yüzden gerekli.
- **Önceki onaylı sürüm:** revizyon 12 (`a51f2349…`). İnceleme 12'nin sonucu KOŞULLU: tek bulgu F-1, önem ORTA. Onay satırı `main`'de (#267, HIGH).
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`, `e7ccbcf9`.
- **Bu sürümün nedeni:** WP-0230-DL7B (alan girdileri) prompt taslağı v1.0 yazılırken iki çelişki ölçüldü:
  - Rapor türü ve ürün rolü seçeneklerinin ekrana gelecek bir kaynağı yok. AC-32 ve DALGA2-01 K1 ekran kopyasını yasaklıyor.
  - Triyajın dört ölçütü AC-32'de duruyor. Oysa API'de bu ölçütler yok ve paket onları QPPV onayına bağlıyor.

  Sahip kararı SECENEK-01 ("secenek-servis"): seçenekleri servis verir (S-1, S-2) ve yeni iş WP-0230-DL4D açılır (S-3). Revizyon 13 bunu yazar; önceki incelemelerden kalan açıklamaları da ekler.
- **Değişiklikler (S-4):**
  - (a) Yeni AC-34: `GET /api/v1/pv-case-intake-triage/options`. Uç, mevcut Gateway kayıt route'undan geçer; `ocelot.json` ve AC-16 değişmez. DL-4 ayrıntılarına WP-0230-DL4D eklenir.
  - (b) AC-32: seçenek kaynağı yeni uç olur. Triyaj ölçütleri AC-28'in işine taşınır (S-5).
  - (c) AC-14: kategori ifadesi (DL7A prompt incelemesi v1.2 F-4).
  - (d) AC-20: C# vekil test katmanı (W7).
  - (e) AC-15: altı dilde İngilizceyle aynı değer olmaz (CEVIRI-01 C-3).
  - (f) AC-2: seçenekler için eklenen JSON eylemi 11. eylemdir ve aynı kurala uyar.
- **Yetki:**
  - Sahibin sözleri: "secenek-servis" ve "ceviri-dl7b" (2026-10-28).
  - Karar kayıtları ZIP'te: SECENEK-01, CEVIRI-01, DALGA2-01, DALGA5-01, ODPVG1-01, ODPVG2-02.
- **İddia (CT ölçümü, `excerpts/ct-parser-and-slice-check.txt`):**
  - `parse_pack` revizyon 12'de 33, revizyon 13'te 34 zorunlu ölçüt okuyor. Silinen AC yok.
  - Metni değişen AC satırları: AC-2, AC-14, AC-15, AC-20, AC-28, AC-32 ve yeni AC-34.
  - Dilim tablosu satırları ve `gerekli kanıt` sütunu aynı.
  - Eski revizyon notlarında değişen satır yok.
- **Bilinen tasarım seçimleri (yazarın emin olmadığı noktalar; inceleyen değerlendirsin):**
  - WP-0230-DL4D dilim tablosuna satır olarak değil, DL-4 ayrıntılarına yazıldı. Nedeni: `pack_approval.slices()` yalnız `| DL-N |` satırı kabul ediyor. Gerekli kanıt DL-4'ün E4'ü.
  - `options` ayrılmış bir yol parçasıdır; üzerinde PUT, `/triage` ve `/route` 404 döner.
  - Servis ucu izin anahtarına bakmıyor; yalnız JWT ve kiracı bağlamı denetleniyor. Kodlar kiracı verisi değil.
  - AC-28 etiketi hâlâ "DL-2"; ama API (DL-4) ve ekran (DL-7) testlerini de taşıyor.
  - Yanıt üyelerinin adları: `reportTypes`, `productRoles`, `unknownCountry`.
- **İsteyen:** depo sahibi (rol).
- **Hazırlayan:** PVG Control Tower oturumu. Revizyon 13'ü `module-pack-author` alt ajanı yazdı; AC-2 eklemesi CT'nin. Effort high.
- **Tarih:** 2026-10-28.
- **İnceleyen:** yeni bir Codex oturumu. İstenen: en güçlü model, effort high. Yanıtın başına model, effort ve oturum kimliği yazılır.
- **ZIP:** `MOD-0230-review-13-7e574ecf1.zip`, sha256 `10c28aced8c642e705156b478b6f8e8900fcfcfb79834296e30645c179f27d4e`, 429 964 bayt. İçinde 46 dosya ve `MANIFEST.sha256` var.
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-13/`.
  - Kaynak dosyalar `git archive 7e574ecf1` ile bayt bayt aynı.
  - E-posta taraması yalnız araçlardaki `@x.invalid` test adreslerini buldu.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-2d3f1177.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 13 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-13-7e574ecf1.zip · sha256 10c28aced8c642e705156b478b6f8e8900fcfcfb79834296e30645c179f27d4e · kaynak commit 7e574ecf1 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 2d3f1177e763bc403a679ad945dcff39f47b05820d85e1d8c6cef19407597e16 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (13), sahip kararı SECENEK-01 ("secenek-servis") ve CEVIRI-01 ("ceviri-dl7b") üzerine şunları değiştiriyor:
(a) yeni AC-34: PvgService'te salt okunur `GET /api/v1/pv-case-intake-triage/options`; tek kaynak `PvgIntakeOptionSets`; JWT + kiracı bağlamı; kiracı verisi yok; mevcut Gateway kayıt route'u `/api/pv-case-intake-triage/{intakeDraftId}` (GET, PUT) üzerinden; `ocelot.json` ve AC-16 değişmez (gerekçe: kayıt kimliği `pvg-draft-<guid>`); iş WP-0230-DL4D, DL-4 ayrıntılarında;
(b) AC-32: ekran seçenekleri yalnız bu uçtan (aynı kökenli MVC vekili); görünüm ve JS'te kod listesi yok; triyajın dört ölçütü AC-32'den çıkıp AC-28'in işine bağlandı (paket bu ölçütleri QPPV AL-5 onayına bağlıyor; API'de yoklar);
(c) AC-14: "13 items" yerine OD-PVG-10 kategori ifadesi;
(d) AC-20: vekil eşlemesinin C# test katmanı (W7);
(e) AC-15: aynı anahtar kümesine ek olarak altı dilde İngilizceyle aynı değer yok (adıyla listelenen istisnalar hariç);
(f) AC-2: seçenekler için eklenen JSON eylemi 11. eylemdir, `pvg.case-intake-triage.read` ile aynı kurala uyar.
Revizyon 12 (a51f2349) onaylı (HIGH); inceleme 12 KOŞULLU, F-1 ORTA.

Önce oku (hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-a51f2349-to-2d3f1177.diff; excerpts/MOD-0230-pack-rev12-version-a51f2349.md; karar kayıtları (secenek-kaynagi-01, ceviri-dl7b-01, dalga2-kararlari-01, dalga5-plani-01, od-pvg-1-izin-01, od-pvg-2-onay-02); servis kaynakları: PvgIntakeOptionSets.cs, PvgCaseIntakeTriageEndpoints.cs, PvgIntakeDraftValidator.cs, InMemoryPvgIntakeDraftRepository.cs, PvgCaseIntakeAuthenticationTests.cs; gateway/Diten.ApiGateway/ocelot.json; ekran kaynakları: PharmacovigilanceCaseIntakeTriageController.cs, PermissionClaims.cs, _Form.cshtml, _IndexL10n.cshtml, en/tr/fr/es resx, vitest dosyası, PvgCaseIntakeTriageWebTests.cs; DL7B prompt taslağı v1.0; DL7A prompt incelemesi v1.2; wave-5 DOR ve VERIFY-DL7A; kurallar (module-pack-standard, permission-key-standard, security-jwt, frontend-datatable-template, localization-standard, agents-core); inceleme 12 bulgusu, karşılaştırma notu 12 ve REVIEW-REQUEST-12.md; excerpts/pack-approval-card.txt; excerpts/ct-parser-and-slice-check.txt; execution/approvals/*.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç.

İnceleme derinliği:
- AC-34: uç tanımı ölçülebilir mi; test adları ve sabotajlar gerçekten kırmızı üretir mi; tek kaynak iddiası doğru mu; kiracı bağlamı ve 401 davranışı DL4B (AC-5) ile tutarlı mı; Gateway kayıt route'unu bu uç için kullanmak güvenli mi (kimlik biçimi, yöntemler, `options` ayrılmış parça, AC-16 korunuyor mu); servis ucunun izin anahtarına bakmaması kabul edilebilir mi?
- WP-0230-DL4D'nin dilim tablosu yerine DL-4 ayrıntılarına yazılması ve gerekli kanıtın E4 kalması tutarlı mı?
- AC-32: seçenek kaynağı ve "kod listesi yok" taraması, ekran adının koddan resx ile çözülmesiyle çelişiyor mu (`_IndexL10n` köprüsü)? Triyaj ölçütlerinin AC-28'e taşınması paketin QPPV koşuluyla tutarlı mı ve bir ölçütü daraltıyor mu?
- AC-14/AC-15/AC-20/AC-2 değişiklikleri önceki onaylı metni daraltıyor mu; AC-15 istisna kuralı ölçülebilir mi?
- Revizyon 13 notu değişiklikleri doğru ve eksiksiz anlatıyor mu? Değişmesi gerekmeyen hiçbir AC, dilim, yol, kapsam ya da gerekli kanıt seviyesi değişmiş mi? Kapanmamış bir konu kapanmış gösterilmiş mi?
- Gerileme: inceleme 12'nin dayandığı metinlerden biri yanlışlıkla değişti mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 2d3f1177e763bc403a679ad945dcff39f47b05820d85e1d8c6cef19407597e16
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f, e7ccbcf9
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-13/MOD-0230-review-13-7e574ecf1.zip
sha256 10c28aced8c642e705156b478b6f8e8900fcfcfb79834296e30645c179f27d4e
```
