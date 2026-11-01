# MOD-0286 — bağımsız inceleme isteği 06 (SOP §20.4 / CT-APR-02)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `7f6868d91a03d1390676c5c2711d5c7a17abbc5b0e4beccb3a09c9fd049ef09a` (revizyon 06)
- Kaynak commit: d5f6c9dc8 (dal `chore/ct/pss-ui-ct-01`, yerel; push edilmedi; `origin/main` 0123bda72 merge edilmiş, 19 ileri / 0 geri) · ilk onay → risk HIGH
- Önceki incelemeler (`execution/approvals/reviews.tsv`, main'in 7 satırı + bu paketin 5 satırı; ZIP'te): 01 `33dc5e14` UYGUN DEĞİL (`fb15f188`), 02 `d15b6c8a` UYGUN DEĞİL (`7242768b`), 03 `9d075026` KOŞULLU (`c710544a`), 04 `75bad532` KOŞULLU (`d77fc1d3`), 05 `d66d59cf` **UYGUN** (bulgu yok). CT beşinde de bütün bulguları kabul etti.
- Bu sürümün nedeni (kod ya da içerik değil, onay aracı): rev 05 UYGUN'du; onay öncesi `pack_approval.py check` iki ENGEL verdi. (1) Dilim tablosu: `scripts/pack_approval.py:821-869` `slices()` (Codex K1, `:866`) "kapsadığı bölümler" hücresindeki her parçanın paketin bir **başlık adı** olmasını ister; rev 05 hücrelerinde AC kimlikleri ve alt bölüm adları vardı → "dilim tablosu yok". (2) İlk onayda `owner`/`target` doldurması inceleme muafiyetine girmiyor (`review_exempt` yalnız onaylı bir tabana göre çalışır; ilk onayda taban yok) → rev 05 incelemesi `owner: K3` / `target: 2026-11-07` yazılınca bayatladı. **Rev 06 = rev 05 + `owner`/`target` dolu + dilim tablosunun üç satırı başlık adlarıyla + Implementation Notes'a tek "Rev 06" satırı.** Fark: `excerpts/MOD-0286-pack-diff-5e46ef8b6-to-d5f6c9dc8.diff` (owner/target 2 satır; tablo 3 satır; not 1 satır). Başka hiçbir bölüm değişmedi; UIC 32 / RC 24 / AC 16 / D-1…D-8c aynı. `slices()` → `['DL-1','DL-2','DL-3']` (ölçüldü).
- Ayrıca ZIP'te yeni: karar kaydı `docs/records/decisions/2026-10/pss-mod0286-open-decisions-01.md` (D-1 belge yeri, D-2 kurallar yerinde, owner K3, target 2026-11-07; D-3…D-8c açık) ve inceleme 05 kayıtları. 370044c7 sürümüne yanlışlıkla açılmış onay satırı push edilmeden yerel geçmişten çıkarıldı (sahip kararı `yeniden-kur`, 2026-10-23); `approvals.tsv` bu paket için satır içermez.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-23
- İnceleyen: sahip seçer; önceki beşi Codex.
- Model · Effort (istenen): inceleyenin en güçlü mevcut modeli · high; beyan verilemiyorsa "verilemiyor" (sahip kararı `beyan-yok`).
- ZIP: `MOD-0286-review-06-d5f6c9dc8.zip` · sha256 `ac06c6181049a34c5539257153c7065282f9103cad3bdc9ae624aa8c367dd5d1` · 1 034 139 bayt · 133 dosya + `MANIFEST.sha256` (134)
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-06/MOD-0286-review-06-d5f6c9dc8.zip`; depoya girmez, sahip iletir.
  - 119 kaynak dosya `git show d5f6c9dc8:<yol>` baytlarıyla (inceleme 05 ZIP'indeki küme + inceleme 05 kayıtları + karar kaydı; `scripts/pack_approval.py` main'in yeni sürümü); `excerpts/` (14): yeniden ölçülmüş alıntılar (@d5f6c9dc8) + pack diff + `MOD-0286-pack-previous-version-5e46ef8b6.md` + `l10n-evidence.txt`.
- Beyanlar (inceleyen bilsin):
  - Bu tur içerik incelemesi değil, **sürüm bağlama** incelemesidir: rev 05 UYGUN'un üstüne yalnız yukarıdaki altı satır değişti. Beklenen sonuç UYGUN; inceleyen farkın bundan ibaret olduğunu diff ile ölçer, isterse rev 05'in F-1…F-17 kapanışlarını örnekleme ile yeniden doğrular.
  - Bu incelemeden sonra pakete **hiç dokunulmaz**; onay satırı bu sha (`7f6868d9…`) için açılır (S-41: PR'da paket başına tek satır, onaylayan merge eder).
- Bulgu dosyası (inceleyenin çıktısı, olduğu gibi kaydedilir): `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-7f6868d9.md`
- Kayıt adımları: bulgu dosyası commit → `review-row --ref <commit> …` → UYGUN ise `pack_approval.py row <paket> --author GMG-ERP --approver bestepullukcu-alt --risk HIGH --reason …` (tek satır) → `check` → `sayfa-ac` → `yedek` → tek PR, bestepullukcu-alt merge eder. Her adım sahibin açık onayıyla.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 06 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-06-d5f6c9dc8.zip · sha256 ac06c6181049a34c5539257153c7065282f9103cad3bdc9ae624aa8c367dd5d1 · kaynak commit d5f6c9dc8 (CT beyanı; yerel dal, push edilmedi) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 7f6868d91a03d1390676c5c2711d5c7a17abbc5b0e4beccb3a09c9fd049ef09a — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 06'dır: inceleme 05 UYGUN verdiği rev 05'in üstüne yalnız altı satır değişti — frontmatter `owner: TBD → K3`, `target: TBD → 2026-11-07`; `## Teslim dilimleri` tablosunun üç satırı (hücreler yalnız başlık adı; neden: scripts/pack_approval.py:821-869 slices() ve :866 K1 kuralı); Implementation Notes'a tek "Rev 06" satırı. Fark: excerpts/MOD-0286-pack-diff-5e46ef8b6-to-d5f6c9dc8.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-5e46ef8b6.md. Beş inceleme ve beş CT karşılaştırması docs/records/reviews/MOD-0286/ altında; karar kaydı docs/records/decisions/2026-10/pss-mod0286-open-decisions-01.md.

Önce oku: hedef paket (frontmatter, `## Teslim dilimleri`, Implementation Notes son satırlar); pack diff ve önceki sürüm; docs/records/reviews/MOD-0286/2026-10-22-d66d59cf.md (inceleme 05) ve 2026-10-22-d66d59cf-ct-karsilastirma-05.md; karar kaydı; execution/approvals/reviews.tsv (13 satır) ve approvals.tsv (bu paket için satır yok); scripts/pack_approval.py (:821-869 slices, :590-640 review_exempt, §7a/7 S-41 için docs/guides/operations/control-tower-operating-card.md:316-319); .antigravity/rules/module-pack-standard.md §6 (22 bölüm) ve §16 "Teslim dilimleri".
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (sürüm bağlama; risk HIGH — ilk onay):
- Fark yalnız beyan edilen altı satır mı (diff'i kendin say; başka bir bölüm değişmiş mi)?
- Dilim tablosu: her hücre parçası paketin gerçek bir `##` başlığı mı (başlık listesini kendin çıkar); DL-1/DL-2 dağılımı AC ve Owned Objects metniyle hâlâ tutarlı mı (AC-1…AC-16'nın dilimlere eşlemesi Acceptance Criteria ve Owned Objects'ta duruyor); DL-3 hâlâ "teslim edilmez, WP'ye verilemez" mi?
- Frontmatter: `owner: K3` approvers.tsv'deki K3 ile tutarlı mı; `target` tarih biçimi; başka alan değişmemiş mi?
- Karar kaydı: D-1/D-2 cevapları pakete yazılmamış (paket "karar bekliyor" diyor, kayıt cevaplıyor) — bu düzen paket > kayıt yetki sırasıyla çelişiyor mu, yoksa paketin kararı sahibe bırakması yeterli mi? Tek cümle görüş.
- Onay yolu: S-41'e göre bu sürüme açılacak tek onay satırının önünde başka engel görüyor musun (ör. bu sürüme bağlı inceleme satırı bu commit'ten SONRA eklenecek; approvals.tsv'de bu paket için satır yok)?
- İsteğe bağlı örnekleme: rev 05 kapanışlarından en az üçünü (F-9 UIC-24 istisnasız, F-16 RC-24 anahtar kümeleri, F-17 RC-23 adım kümeleri) rev 06 metninde yeniden gör.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 7f6868d91a03d1390676c5c2711d5c7a17abbc5b0e4beccb3a09c9fd049ef09a
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-18 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-19 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 18'den başlar; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-06/MOD-0286-review-06-d5f6c9dc8.zip
sha256 ac06c6181049a34c5539257153c7065282f9103cad3bdc9ae624aa8c367dd5d1
```
