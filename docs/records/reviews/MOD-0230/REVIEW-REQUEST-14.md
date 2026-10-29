# MOD-0230 — bağımsız inceleme isteği 14 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`, sha256 `ee175d00f54199736d7741f81042bd8bcea52662391e92e2fa1e8964a10706b3`. Revizyon 13'ün onaydan önce düzeltilmiş hali.
- **Kaynak commit:** `7279d3558`. Dal `chore/ct/pvg-ct-01`, yerel, push edilmedi.
- **Risk:** Onay kartı HIGH hesaplıyor (kart sha256 `44ffdfeb…`). Bağımsız inceleme bu yüzden gerekli.
- **Önceki onaylı sürüm:** revizyon 12 (`a51f2349…`), onay satırı `main`'de (#267, HIGH).
- **İnceleme 13:** hedef `2d3f1177…`. Sonuç KOŞULLU; iki bulgu var: F-1 ve F-2, ikisi de ORTA, ikisi de AC-32'de. Bulgu dosyası ve CT karşılaştırma notu 13 ZIP'te. Revizyon 13 onaylanmadı ve dağıtılmadı; bu yüzden düzeltme revizyon 13 içinde yapıldı.
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`, `e7ccbcf9`.
- **Değişiklik (2d3f1177 → ee175d00):**
  - Revizyon 13 notuna bir düzeltme satırı eklendi.
  - AC-32 yeniden yazıldı:
    - (F-1) Sabit liste taraması yalnız rapor türü ve ürün rolü girdilerinin değer kaynağına bağlandı. SourceType alanı ile resx / `_IndexL10n` etiket eşlemeleri liste sayılmıyor. Sabotaj: rapor türü girdisine sabit bir `<option>` eklemek ya da JS'e bir kod listesi koymak.
    - (F-2) "Yalnız unknown" testi, ülke listesinin bulunmadığı duruma (Absent) bağlandı. Liste geldikten sonra liste kodları ve "unknown" birlikte sunulur (Present, AC-26); bu, ülke kaynağı kararından sonraki seçici ölçümüne kalır.
- **İddia (CT ölçümü, `excerpts/ct-parser-and-slice-check.txt`):**
  - 2d3f1177 → ee175d00 arasında yalnız AC-32 değişti ve AC-32 dışında yalnız iki not satırı eklendi; silinen satır yok.
  - `parse_pack` 34 zorunlu ölçüt okuyor. Dilim tablosu aynı.
  - Revizyon 12'ye göre değişen AC'ler 13'teki ile aynı: AC-2, AC-14, AC-15, AC-20, AC-28, AC-32, AC-34.
- **Bilinen konu:** WP-0230-DL7B prompt taslağı v1.0 eski ifadeyi taşıyor. Dağıtımdan önce v1.1 olarak bu sürüme göre yeniden yazılacak. Taslak dağıtılmadı.
- **İsteyen:** depo sahibi (rol).
- **Hazırlayan:** PVG Control Tower oturumu. Düzeltmeyi `module-pack-author` alt ajanı yazdı; CT denetledi.
- **Tarih:** 2026-10-29.
- **İnceleyen:** yeni bir Codex oturumu. İstenen: en güçlü model, effort high.
- **ZIP:** `MOD-0230-review-14-7279d3558.zip`, sha256 `d2086f2180b27c331814e4c11ac36d2b72fbebc0b78d2af23e02bb06e05400ae`, 473 425 bayt. İçinde 35 dosya ve `MANIFEST.sha256` var.
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-14/`.
  - Kaynak dosyalar `git archive 7279d3558` ile bayt bayt aynı.
  - E-posta taraması: araçlardaki `@x.invalid` test adresleri dışında aday yok.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-ee175d00.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 14 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-14-7279d3558.zip · sha256 d2086f2180b27c331814e4c11ac36d2b72fbebc0b78d2af23e02bb06e05400ae · kaynak commit 7279d3558 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 ee175d00f54199736d7741f81042bd8bcea52662391e92e2fa1e8964a10706b3 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu, revizyon 13'ün onaydan önce düzeltilmiş halidir. İnceleme 13 (hedef 2d3f1177, sonuç KOŞULLU) iki ORTA bulgu verdi, ikisi de AC-32'de:
F-1: sabit liste taraması altı kodu modülün bütün görünüm/JS kaynaklarında yasaklıyordu; oysa ayrı SourceType alanı bu kodların üçünü meşru kullanıyor.
F-2: "pvg event country offers only unknown" testi koşulsuzdu; ülke listesi geldikten sonraki onaylı davranışı (Country rule table Present, AC-26) reddediyordu.
Düzeltme yalnız AC-32'yi ve revizyon 13 notuna bir satırı değiştiriyor (excerpts/MOD-0230-pack-diff-2d3f1177-to-ee175d00.diff).
Revizyon 12 (a51f2349) onaylı (HIGH). Revizyon 13'ün diğer değişiklikleri (AC-2, AC-14, AC-15, AC-20, AC-28, AC-34, DL-4 ayrıntılarında WP-0230-DL4D) inceleme 13'te bulgu almadı ve bu düzeltmede değişmedi.

Önce oku (hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-2d3f1177-to-ee175d00.diff; excerpts/MOD-0230-pack-diff-a51f2349-to-ee175d00.diff; excerpts/MOD-0230-pack-rev13-reviewed-version-2d3f1177.md; excerpts/MOD-0230-pack-rev12-approved-version-a51f2349.md; inceleme 13 bulgusu (docs/records/reviews/MOD-0230/2026-10-29-2d3f1177.md), CT karşılaştırma notu 13 ve REVIEW-REQUEST-13.md; karar kayıtları (secenek-kaynagi-01, ceviri-dl7b-01, dalga2-kararlari-01, dalga5-plani-01, od-pvg-1-izin-01, od-pvg-2-onay-02); _Form.cshtml, _IndexL10n.cshtml, en resx, vitest dosyası, PharmacovigilanceCaseIntakeTriageController.cs; PvgIntakeOptionSets.cs, PvgIntakeDraftValidator.cs; DL7B prompt taslağı v1.0 (dağıtılmadı, v1.1'de bu sürüme uyarlanacak); kurallar (module-pack-standard, localization-standard, frontend-datatable-template, agents-core); excerpts/pack-approval-card.txt; excerpts/ct-parser-and-slice-check.txt; execution/approvals/*.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç.

İnceleme derinliği:
- F-1 kapandı mı? Yeni tarama tanımı ölçülebilir mi; doğru bir uygulama geçer, sabit bir rapor türü/ürün rolü listesi (görünümde `<option>` ya da JS'te kod listesi) kırmızı olur mu? SourceType ve etiket eşlemesi istisnası bir kaçak yol açıyor mu (ör. kod listesini etiket eşlemesi kılığında JS'e koymak)?
- F-2 kapandı mı? "Absent" durumuna bağlanan test bugünkü durumu doğru ölçüyor mu; "Present" davranışı AC-26 ve AC-32'nin seçici koşuluyla korunuyor mu; bir ölçüt daralıyor mu?
- Düzeltme, AC-32'nin revizyon 12'deki onaylı içeriğinden herhangi bir şeyi daraltıyor mu (triyaj ölçütlerinin AC-28'e taşınması inceleme 13'te değerlendirildi)?
- Diff gerçekten yalnız AC-32 ve not satırı mı? Başka bir AC, dilim, yol, kapsam ya da gerekli kanıt seviyesi değişmiş mi? Revizyon 13 notu düzeltmeyi doğru anlatıyor mu?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ ee175d00f54199736d7741f81042bd8bcea52662391e92e2fa1e8964a10706b3
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f, e7ccbcf9
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma. İnceleme 13'ün F-1 ve F-2'si kapandıysa onları yeniden yazma; kapanmadıysa yeni bir F satırı olarak yaz.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-14/MOD-0230-review-14-7279d3558.zip
sha256 d2086f2180b27c331814e4c11ac36d2b72fbebc0b78d2af23e02bb06e05400ae
```
