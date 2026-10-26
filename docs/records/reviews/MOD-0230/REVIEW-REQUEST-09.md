# MOD-0230 — bağımsız inceleme isteği 09 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `7dd6a8dad2362a54df97c4ecdd682ec40d11cf7204a5db5c38f6659b9ed048d2` (revizyon 09).
- **Kaynak commit:** `fba750a48`, dal `chore/ct/pvg-ct-01`, yerel; push edilmedi.
- **Risk:** HIGH (onay kartının hesapladığı değer).
- **Önceki sürümler:**
  - Revizyon 07 (`10fdbe5c…`): inceleme 07 UYGUN, onay satırı `main`'de.
  - Revizyon 08 (`96424971…`): inceleme 08 UYGUN DEĞİL, 1 ENGEL, bulgu özeti `e7ccbcf9…`.
    - Bulgu dosyası: `docs/records/reviews/MOD-0230/2026-10-23-96424971.md`.
    - CT karşılaştırma notu 08: `docs/records/reviews/MOD-0230/2026-10-23-96424971-ct-karsilastirma-08.md`.
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`, `e7ccbcf9`.
- **Bu sürümün nedeni:** İnceleme 08 F-1 şunu buldu: `parse_pack()` bir ölçütün yalnız ilk fiziksel satırını alıyor, bu yüzden 33 ölçütün 31'i mekanik kabulde kesiliyordu. Sahip kararı "ok 1" (2026-10-23): revizyon 09'da her ölçüt tek fiziksel satırda olacak.
- **İddia:**
  - Revizyon 08'e göre yalnız ölçütlerin içindeki satır sonları kalktı: 31 madde, 251 devam satırı.
  - Eklenen tek metin revizyon 09 notu. Hiçbir kelime değişmedi.
- **İsteyen:** depo sahibi (rol). **Hazırlayan:** PVG Control Tower oturumu. **Tarih:** 2026-10-23.
- **İnceleyen:** yeni bir Codex oturumu. Oturum kimliği yanıtın başına yazılır. Model ve effort istenen değerler: Codex'in en güçlü modeli, high. Kullanılan değerler oturumdan ölçülüp `reviews.tsv` satırına yazılır.
- **ZIP:** `MOD-0230-review-09-fba750a48.zip`, sha256 `3a5bb5ed94cacd0be6c0626bc386fdb05d8482f5a7e5d7783068a29c44ac3c3f`, 357 915 bayt, 24 dosya ve `MANIFEST.sha256`.
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-09/`. Git'in izlemediği bir klasör; ZIP depoya girmez, sahip iletir.
  - 19 kaynak dosya `git archive fba750a48` baytlarıyla aynı.
  - E-posta taramasında yalnız test değerleri çıktı: `*@x.invalid` ve `reporter@example.test`.
  - `excerpts/` içinde:
    - revizyon 08'in tam metni;
    - iki sürüm arasındaki diff (`MOD-0230-pack-diff-96424971-to-7dd6a8da.diff`);
    - onay kartı;
    - CT'nin belirteç dizisi ve ayrıştırıcı denetimi;
    - aracın WP-0230-DL2A için kabul listesi taslağı (artık tam ölçüt metinleriyle).
- **Beyanlar:**
  - Revizyon 09'u `module-pack-author` alt ajanı yazdı. Beyan effort'u 15; uygulamanın ölçtüğü oturum effort'u high.
  - CT'nin kendi denetimi şunları buldu:
    - Belirteç dizisi revizyon 08 ile aynı; fark yalnız eklenen not.
    - `parse_pack`: 33 zorunlu ölçüt, sorun yok.
    - AC-12 metni 715 karakter, üç zorunlu test adı içinde. AC-30 metni 5 587 karakter.
    - `slices`: DL-1…DL-9.
  - AC-32'de iki kod aralığı (backtick) eskiden satır sonundan bölünüyordu; birleştirmeden sonra tek boşlukla doğru eşleşiyor.
  - Ölçüt satırları artık çok uzun; AC-30 tek satırda 5 587 karakter. Bu, aracın biçiminin sonucu.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-7dd6a8da.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.
- **Kayıt adımları:** İstek 08'deki gibi. Sırasıyla bulgu dosyası, `review-row`, `row`, `check`, `main` birleştirmesi ve kontroller (§7a), en sonda tek PR. PR'ı bestepullukcu-alt birleştirir.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 09 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-09-fba750a48.zip · sha256 3a5bb5ed94cacd0be6c0626bc386fdb05d8482f5a7e5d7783068a29c44ac3c3f · kaynak commit fba750a48 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 7dd6a8dad2362a54df97c4ecdd682ec40d11cf7204a5db5c38f6659b9ed048d2 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (09) yalnız biçim değişikliği iddia ediyor: inceleme 08'in F-1 bulgusu üzerine her kabul ölçütü tek fiziksel satıra alındı; yalnız ölçütlerin içindeki satır sonları kalktı ve bir revizyon notu eklendi. Revizyon 08 (96424971), BG-n ölçütlerini aynı numarayla AC-n'ye çeviren biçim revizyonuydu; inceleme 08 onu yalnız F-1 ile UYGUN DEĞİL buldu. Revizyon 07 (10fdbe5c) inceleme 07'de UYGUN aldı ve onaylı.

Önce oku (sırayla, hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-96424971-to-7dd6a8da.diff; excerpts/MOD-0230-pack-rev08-version-96424971.md; docs/records/reviews/MOD-0230/2026-10-23-96424971.md (inceleme 08) ve -ct-karsilastirma-08.md; REVIEW-REQUEST-08.md ve REVIEW-REQUEST-09 bilgileri bu prompt'ta; docs/records/decisions/2026-10/pvg-mod0230-ac-esleme-01.md; docs/records/audits/2026-10/pvg-wave1-integration-01/INTEGRATION.md; docs/records/audits/2026-10/ct-setup-test-01/FINDINGS.md (S-44); scripts/acceptance_record.py (parse_pack, AC_ITEM, check_list, draft_list); scripts/pack_approval.py (slices); .antigravity/rules/module-pack-standard.md (satır 283, 294-297, 520-535); inceleme 07 bulguları ve karşılaştırma notu 07; execution/approvals/reviews.tsv; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv; excerpts/ altındaki bütün dosyalar.
Kod koşamazsın; iddiaları ZIP'teki metne ve araç koduna karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH):
- F-1 kapanışı: 33 ölçütün her biri tek fiziksel satırda mı ve parse_pack'in aldığı metin artık ölçütün bütün kapsamını, koşullarını ve test adlarını içeriyor mu (örnek: AC-5, AC-12, AC-30, AC-33)? Kabul listesi taslağı tam metni mi taşıyor?
- İçerik korunması: diff'te ölçüt içi satır sonlarının kalkması ve revizyon 09 notu dışında değişiklik var mı? Birleştirme sırasında kod aralıkları (backtick), parantezler ya da tireli sözcükler bozuldu mu (özellikle AC-32)?
- İnceleme 08'in kalan sorularını yeniden değerlendir: BG-n → AC-n eşlemesi (33 ölçüt aynı numara ve metin), "zorunlu" işaretinin "after …" koşullu ölçütlerde anlamı, "Gate State and Acceptance History" bölümüne taşınan listeler, eşleme kaydının eski kayıtları ve prompt'ları tek anlamlı kılması, module-pack-standard.md:295 ile ilişki.
- Araç uyumu: tek Acceptance Criteria bölümü, her madde AC_ITEM, bölümde başka liste işareti ya da girintili satır yok, devam satırı yok, en az bir zorunlu; pack_approval.slices() DL-1…DL-9.
- Gerileme: inceleme 07'nin UYGUN sonucunun dayandığı hiçbir metin değişti mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 7dd6a8dad2362a54df97c4ecdd682ec40d11cf7204a5db5c38f6659b9ed048d2
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f, e7ccbcf9
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-09/MOD-0230-review-09-fba750a48.zip
sha256 3a5bb5ed94cacd0be6c0626bc386fdb05d8482f5a7e5d7783068a29c44ac3c3f
```
