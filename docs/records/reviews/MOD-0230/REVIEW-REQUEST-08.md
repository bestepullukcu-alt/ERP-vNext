# MOD-0230 — bağımsız inceleme isteği 08 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `9642497146800ed723dffa0641f0f695a6c59741ab2d5b154daf0dbf86206da2` (revizyon 08).
- **Kaynak commit:** `80bef24ed`; paket değişikliği `23a3b6232`. Dal `chore/ct/pvg-ct-01`, yerel, push edilmedi.
- **Risk:** HIGH. Onay kartının hesapladığı değer bu; "Entity Fields" altındaki atıf satırları değişti.
- **Önceki onaylı sürüm:** `10fdbe5c…` (revizyon 07). İnceleme 07 sonucu UYGUN (`docs/records/reviews/MOD-0230/2026-10-22-10fdbe5c.md`). Onay satırı `main`'de.
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`. Önceki incelemeler eski sürümlere aittir; bu sürümü kapsamaz.
- **Bu sürümün nedeni:**
  - Kabul aracı (`scripts/acceptance_record.py`) yalnız `AC-<n>` ölçütlerini tanıyor. Revizyon 07'nin `BG-<n>` maddeleri yüzünden MOD-0230'un main'e girmiş üç işi (WP-0230-DL2A, DL3A, DL8) kabul alamıyordu (`docs/records/audits/2026-10/pvg-wave1-integration-01/INTEGRATION.md`).
  - Sahip kararı paket yolu oldu: "1 paket ac", 2026-10-23; CT-Setup S-44.
  - Eşleme kaydı: `docs/records/decisions/2026-10/pvg-mod0230-ac-esleme-01.md` @ `80bef24ed`.
- **Revizyonun iddiası:** Yalnız biçim değişti. Ölçüt, karar ve kapsam aynı. Form alanı sayısı 25, değişmedi.
- **İsteyen:** depo sahibi (rol). **Hazırlayan:** PVG Control Tower oturumu. **Tarih:** 2026-10-23.
- **İnceleyen:** Yeni bir Codex oturumu öneriliyor. Paketi Claude yazdı, bu yüzden inceleyen başka bir sağlayıcı olmalı. Oturum kimliği yanıtın başına yazılmalı.
- **Model ve effort:** İstenen, Codex'in en güçlü modeli ve high (§17.1 r5). Gerçekte kullanılan değer oturumdan ölçülüp `reviews.tsv` satırına yazılır.
- **ZIP:** `MOD-0230-review-08-80bef24ed.zip`, sha256 `207ff01b9ef63c930e8dd6e467da4cc09db1bc94f24278563bb49acd0adf8591`, 370 392 bayt. İçinde 21 dosya ve `MANIFEST.sha256` var.
  - **Yeri (S-29):** `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-08/`. Git bu klasörü izlemez; ZIP depoya girmez, sahip iletir.
  - **Kaynak dosyalar:** 15 dosya, `git archive 80bef24ed` baytlarıyla aynı. E-posta taraması yalnız test değerleri buldu: `*@x.invalid` ve `reporter@example.test`.
  - **`excerpts/` içeriği:**
    - Revizyon 07'nin tam metni ve iki sürüm arasındaki diff (`MOD-0230-pack-diff-10fdbe5c-to-96424971.diff`).
    - Onay kartı.
    - CT'nin kelime çoklu kümesi denetimi.
    - Aracın `parse_pack()` çıktısı.
    - Aracın WP-0230-DL2A için kabul listesi taslağı.
- **Beyanlar (inceleyen bilsin):**
  - Revizyon 08'i `module-pack-author` alt ajanı yazdı. Ajanın beyan ettiği effort "15"; uygulamanın ölçtüğü oturum effort'u high.
  - Control Tower, ajanın çıktısında iki şeyi kendisi değiştirdi:
    1. Taşınan listelerin bölüm başlığını "Gate State and Acceptance History" yaptı. Sebep: taşınan listelerden biri açık operasyonel kapı maddeleri, yani yalnız "geçmiş" değil.
    2. Bu başlığı dilim tablosunda DL-1 hücresine ekledi ve revizyon notunu buna göre düzeltti.
  - Ajan, AC-31'de `+` ile başlayan bir devam satırını önceki satırın sonuna aldı. Kelimeler aynı, ama ayrıştırıcı o satırı liste işareti sayıyordu.
  - Kabul listesi taslağında her ölçütün yalnız ilk satırı görünüyor. Bu aracın davranışı; paket metni eksik değil.
  - `module-pack-standard.md:295` "Mevcut paketler dönüştürülmez" diyor. Bu dönüşüm sahip kararına dayanıyor; kural metni değişikliği ayrı bir iş (S-44).
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-96424971.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.
- **Kayıt adımları:**
  1. Bulgu dosyası commit edilir.
  2. `python3 scripts/pack_approval.py review-row <paket> --findings <dosya> --verdict … --reviewer codex --model … --effort … --reviewer-session … --author K3 --author-session … --requested-by GMG-ERP` ile `reviews.tsv` satırı eklenir.
  3. Sonuç UYGUN ya da açık ENGEL'siz KOŞULLU ise `pack_approval.py row --risk HIGH --author GMG-ERP --approver bestepullukcu-alt` çalıştırılır.
  4. `check` çalıştırılır.
  5. `main` ile birleştirilir ve kontroller yeniden koşulur (§7a).
  6. Tek PR en sonda açılır; bestepullukcu-alt birleştirir (§7a/7).
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 08 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-08-80bef24ed.zip · sha256 207ff01b9ef63c930e8dd6e467da4cc09db1bc94f24278563bb49acd0adf8591 · kaynak commit 80bef24ed (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 9642497146800ed723dffa0641f0f695a6c59741ab2d5b154daf0dbf86206da2 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (08) yalnız biçim değişikliği iddia ediyor: 33 kabul ölçütü `- [ ] BG-<n> (<kapsam>) - <metin>` biçiminden `- [ ] AC-<n> (zorunlu) — <kapsam>: <metin>` biçimine, aynı numarayla çevrildi; bölümdeki numarasız listeler değiştirilmeden yeni "Gate State and Acceptance History" bölümüne taşındı; başka her yerde BG-n → AC-n. Önceki sürüm (revizyon 07, 10fdbe5c) inceleme 07'de UYGUN aldı ve onaylı.

Önce oku (sırayla, hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-10fdbe5c-to-96424971.diff; excerpts/MOD-0230-pack-rev07-version-10fdbe5c.md; docs/records/decisions/2026-10/pvg-mod0230-ac-esleme-01.md (bu sürümün tek yetkisi); docs/records/audits/2026-10/pvg-wave1-integration-01/INTEGRATION.md ("Kabul engeli"); docs/records/audits/2026-10/ct-setup-test-01/FINDINGS.md (S-44 satırı); scripts/acceptance_record.py (parse_pack, AC_ITEM, check_list, draft_list); scripts/pack_approval.py (slices); .antigravity/rules/module-pack-standard.md (satır 283, 294-297, 520-535); inceleme 07 bulguları ve karşılaştırma notu 07; REVIEW-REQUEST-07.md; execution/approvals/POLICY-REVIEW-START.md ve POLICY-DISPATCH-REVIEW-START.md; execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv; excerpts/ altındaki bütün dosyalar.
Kod koşamazsın; iddiaları ZIP'teki metne ve araç koduna karşı statik olarak kendin ölç.

İnceleme derinliği (§20.4-2, risk HIGH):
- Eşleme: 33 ölçütün her biri aynı numarayla ve aynı metinle mi taşındı? Hiçbir ölçüt eklenmedi, silinmedi, birleşmedi, bölünmedi mi? Kapsam / koşul bilgisi ("DL-n; after …") kaybolmadan metnin başına mı alındı? "zorunlu" işareti her ölçüt için doğru mu (revizyon 07'de "after …" ile koşullanan ölçütler zorunlu sayılınca bir anlam değişikliği oluyor mu)?
- İçerik korunması: diff'te BG→AC, ayraç ( - → — / : ), "(zorunlu)", beş "criterion <n>" atfı, AC-31'deki satır birleştirme, revizyon 08 notu, yeni bölüm başlığı ve cümlesi, DL-1 hücresine eklenen başlık dışında bir değişiklik var mı? Künye bayt bayt aynı mı?
- Taşınan listeler: "Original planning pack", "Build / test gate state" ve "Operational runtime gate" listeleri değiştirilmeden mi taşındı; operasyonel runtime kapısının açık maddeleri yeni yerinde kapalı / tamamlanmış gibi okunuyor mu; yeni bölüm başlığı yanıltıcı mı?
- Araç uyumu: hedef paket parse_pack()'in kurallarına (tek Acceptance Criteria bölümü, her madde tek satırda AC_ITEM, bölümde başka liste işareti yok, devam satırlarında "AC-<rakam>" yok, en az bir zorunlu) uyuyor mu; pack_approval.slices() dilim tablosunu okuyor mu (her hücre bölümü paketin bir başlığı mı)?
- Kayıtlar: eşleme kaydı (BG-n ≡ AC-n) eski kayıtları ve gönderilmiş prompt'ları tek anlamlı kılıyor mu; kuyrukta yalnız PLANNED satırların değişmesi doğru mu?
- Kural: module-pack-standard.md:295 ("Mevcut paketler dönüştürülmez") ile bu dönüşüm çelişiyor mu; sahip kararı ve S-44 bunu yeterince kayda geçiriyor mu?
- Gerileme: inceleme 07'nin UYGUN sonucunun dayandığı hiçbir metin değişti mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 9642497146800ed723dffa0641f0f695a6c59741ab2d5b154daf0dbf86206da2
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-08/MOD-0230-review-08-80bef24ed.zip
sha256 207ff01b9ef63c930e8dd6e467da4cc09db1bc94f24278563bb49acd0adf8591
```
