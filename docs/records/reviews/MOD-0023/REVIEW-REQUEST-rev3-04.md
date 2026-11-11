# MOD-0023 — bağımsız inceleme isteği, §17 revizyon 3, tur 3 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md`, sha256
  `f1c051366e7e19ab17ffa7809a686542f8fe761f707176936af08cc4021e887c`. **Kaynak commit:** `2cb76f974` (dal `chore/ct/pss-wf-ct-01`, push yok).
- **Önceki resmî tur:** `docs/records/reviews/MOD-0023/2026-10-30-5512cbca.md` — UYGUN DEĞİL (F-1 ENGEL, F-2 ORTA, F-3 ORTA);
  reviews.tsv satırı yerel. CT üçünü kodla doğruladı; sahip `duzelt` (2026-10-30).
- **Bu turun değişikliği:** `excerpts/MOD-0023-pack-diff-5512cbca-to-f1c05136.diff` — RequestInfo hedefi için çakışmasız ayrı alan
  (F-1), sıfır satırlı 503 sonrası deneme ilk yazma (F-2), AC-W5 zorlanmış yarış kanıtı (F-3); ara kontrol 09 (alt ajan, KOŞULLU,
  iki düşük bulgu düzeltildi; `ARA-KONTROL-09.md`). Düzeltmeden sonra yeni ara kontrol koşulmadı.
- **İnceleyen:** yeni bir Codex oturumu; en güçlü model, effort high.
- **ZIP:** `MOD-0023-review-rev3-c4-2cb76f974.zip`, sha256 `5e6090bf686d87aa33149a2af6cba8e13d6086ae6c4de2a5d9d912c9f7b3301a`,
  581 117 bayt; 135 dosya ve `MANIFEST.sha256`. Yeri: `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-wf-ct-01/.ct-teslim/MOD-0023-review-rev3-c4/`.
  Önceki ZIP'ler `GECERSIZ.txt` ile işaretli.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0023/<YYYY-AA-GG>-f1c05136.md`.

## İnceleme prompt'u

```text
HEDEF: Codex — MOD-0023 module pack §17 revizyon 3 bağımsız incelemesi, tur 3 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0023-review-rev3-c4-2cb76f974.zip · sha256 5e6090bf686d87aa33149a2af6cba8e13d6086ae6c4de2a5d9d912c9f7b3301a · kaynak commit 2cb76f974 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ sha256 f1c051366e7e19ab17ffa7809a686542f8fe761f707176936af08cc4021e887c — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Önceki tur (docs/records/reviews/MOD-0023/2026-10-30-5512cbca.md, ZIP'te) UYGUN DEĞİL dedi: F-1 RequestInfo hedefi birleşik Comment üzerinden karşılaştırılınca iki farklı istek aynı sayılıyor; F-2 sıfır satırlı 503 dalından sonraki deneme yanlışlıkla "tekrar" yazılmış; F-3 AC-W5 için mevcut testin zorlanmış yarış kanıtı yetersiz. Bu tur: §17.9'da üçü düzeltildi; ayrıca bir ara kontrolün iki düşük bulgusu (AC-W5 tek zorlama biçimi; hedef kuralının "unavailable" yeniden okuma yolunda da sınanması) işlendi. Fark: excerpts/MOD-0023-pack-diff-5512cbca-to-f1c05136.diff; onaylı revizyon 2'den fark: excerpts/MOD-0023-pack-diff-803fbaa5-to-f1c05136.diff. Paket açık bir sahip kararı içerir (O-S17-3); açık olması tek başına bulgu değildir.

Önce oku (hepsi ZIP'te): hedef paket §17 (17.1–17.9) ve Teslim dilimleri; iki fark dosyası; önceki iki resmî tur (2026-10-29-3e802d2f.md, 2026-10-30-5512cbca.md); docs/records/audits/2026-10/mod0023-s17-inspection-01/ (INSPECTION.md, ara kontroller); execution/approvals/approvals.tsv; treasury-intake-owner-decisions-02.md (D21, D22, D25, D32); mod0023-s17-owner-decisions-01.md; kart S-85 ve E1–E5; services/**, frontend/** kaynakları ve excerpts/.
Kod koşamazsın; iddiaları ZIP'teki kaynaklara karşı statik olarak kendin ölç (dosya:satır).

İnceleme derinliği:
- Önceki turun F-1, F-2, F-3'ü kapandı mı? Her biri için kapanış ya da neden açık kaldığı, dosya:satır ile.
- RequestInfo hedef alanı: iki çakışma sırasını üç yolda da (sıralı karşılaştırma, yinelenen anahtar eşlemesi, "unavailable" yeniden okuma) ayırıyor mu; boş ve null ayrımı ve eski satır davranışı tutarlı mı; Mongo'da eski belgelerde alanın olmaması okumayı bozar mı; izinli yollar ve kişi alanı listesi güncel mi?
- AC-W5 zorlama biçimi (anahtar okumasından sonra, ReserveTrustedStartAsync sarmalayıcısında tutma; sayaçlar) kodla uygulanabilir ve kesin mi?
- Bu fark başka yeri bozdu mu (O-S17-3 bağlı ölçütler, AC-W6/W7/W8/W11/W14, kapanış, Teslim bitiş listesi, tablo hücreleri, durum paragrafı)? Değişmemesi gereken metin (§1–§16, §17.1–17.8 gövdesi, frontmatter) değişmiş mi; O-S17-1 açık mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0023 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md @ f1c051366e7e19ab17ffa7809a686542f8fe761f707176936af08cc4021e887c
değerlendirilen olumsuz incelemeler: 470882d1, 3e802d2f, 5512cbca
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Kapananlar (inceleme 2026-10-30-5512cbca): <düz metin; satır başında F- kullanma>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa yalnız "Bulgu yok." yaz.
```
