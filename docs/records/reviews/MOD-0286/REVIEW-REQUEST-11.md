# MOD-0286 — bağımsız inceleme isteği 11 (SOP §20.4 / CT-APR-02; inceleme 10 bulgularının kapanışı)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `35e035a1a2318731b1af34abc3ae2a3dbd25c7000f44c4e22b3557dfa401c4ae` (revizyon 11)
- Kaynak commit: bc06039e3 (dal `chore/ct/pss-ui-ct-01` = main `ded2a4de0` + 8 commit; yerel, push edilmedi)
- Önceki: rev 10 `51bbd629…` — inceleme 10 **UYGUN DEĞİL** (F-23 ENGEL, F-22 ORTA; kayıt `2026-10-26-51bbd629.md`, satır `reviews.tsv` @ 0eb90a4ce). Geçerli onaylı taban rev 06 `7f6868d9…`.
- Rev 11 = rev 10 + yalnız inceleme 10 bulgularının kapanışı (sahip kararı `kaydet-rev11`, 2026-10-26). CT'nin karşılaştırma 10'daki önerisi benimsendi: satır kontrollerinde taban anahtarı sayfa + kontrol + dosya, ayrı `count` sütunu = dosyanın tamamında düzenli ifadenin `re.finditer` eşleşme sayısı; `n > b` yeni kırmızı, `n = b` taban, `n < b` "kapandı - satırı güncelle"; sınır açıkça yazılı (aynı dosyada bir ihlal kapanıp biri eklenirse taban). Arama kayıtları JSON yolu + nesne topluluğu olarak karşılaştırılır; CI-12 navigation kaydını ister. AC-18'e (b1) aynı satırda ikinci eşleşme, (b2) çok satırlı devam, (e) yalnız navigation kaydının silinmesi eklendi. Değişen AC'ler yalnız 17, 18, 19; UIC 32 / RC 24 / D / FU / dilim tablosu / künye rev 10 ile aynı. 776 satır; `git diff --stat`: 96 ekleme / 60 silme.
- CT ölçümü (bc06039e3): `slices()` = DL-1, DL-2, DL-3; kapının künye okuyucusu `(None, None)`; CI tablosu 15 satır × 5 hücre (kaçışlı dikey çizgi ayırıcı sayılmadan).
- Fark: `excerpts/MOD-0286-pack-diff-8cd2f5d4c-to-bc06039e3.diff`; önceki sürüm `excerpts/MOD-0286-pack-previous-version-8cd2f5d4c.md`. Rev 11 ölçüm betikleri `excerpts/rev11/` (sayılar: 321 dosya / 743 eşleşme @ 0eb90a4ce).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-26 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-11-bc06039e3.zip` · sha256 `c0bfcbcb02f0bbd39324d9c9454bcf8a83757d1e3527eef5de2e31464fa819dd` · 1 767 382 bayt · 188 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-11/MOD-0286-review-11-bc06039e3.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-35e035a1.md`; yeni bulgular F-24'ten.
- Sonra: bulgu dosyası commit → `review-row` 11 → `check` kuru koşu (insan adımı dahil) → tek onay satırı en sonda, dalın kendi çizgisinde → push ve PR sahibin sözüyle, bestepullukcu-alt doğrudan merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 11 (SOP §20.4 / CT-APR-02; inceleme 10 bulgularının kapanışı). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-11-bc06039e3.zip · sha256 c0bfcbcb02f0bbd39324d9c9454bcf8a83757d1e3527eef5de2e31464fa819dd · kaynak commit bc06039e3 (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 35e035a1a2318731b1af34abc3ae2a3dbd25c7000f44c4e22b3557dfa401c4ae — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 11'dir: senin inceleme 10'un (rev 10 51bbd629…, UYGUN DEĞİL; docs/records/reviews/MOD-0286/2026-10-26-51bbd629.md ve CT karşılaştırması -ct-karsilastirma-10.md) iki bulgusunu kapatır; başka değişiklik iddiası yoktur. CT karşılaştırma 10'daki sayı tabanlı öneri benimsendi. Fark: excerpts/MOD-0286-pack-diff-8cd2f5d4c-to-bc06039e3.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-8cd2f5d4c.md; rev 11 ölçüm betikleri excerpts/rev11/.

Önce oku: inceleme 10 kaydı ve CT karşılaştırması 10; pack diff; hedef paketin DL-3 bölüm 1 (arama kaydı karşılaştırması) ve bölüm 2 (taban, count sütunu, kural ve sınır), CI tablosu (CI-5, CI-12), Validation Rules, Failure Path to Verify, Acceptance Criteria AC-17…AC-20, Test Expectations (Rev 11 ölçümleri, Ölçülmedi), Implementation Notes "Rev 11" satırı; frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json ve .tr.json; .antigravity/rules/platform-global-search-registry.md; excerpts/rev11/counts.py ve registry.py.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- F-23: sayı tabanlı kural (dosyanın tamamında eşleşme sayısı; n > b yeni kırmızı) aynı dosyaya eklenen her ikinci ihlali yakalıyor mu — ayrı satır, birebir kopya, aynı satırda ikinci eşleşme, çok satırlı devam; eşleşme sayısının tanımı (re.finditer, bayraksız, çakışmasız) her satır kontrolü için açık mı; yazılı sınır (aynı PR'da aynı dosyada bir ihlal kapanıp biri eklenirse taban) kabul edilebilir ve açıkça belgelenmiş mi; AC-18 (b1) ve (b2) adlı yeni kırmızı veriyor mu ve tek başına (a) ile kapanamıyor mu; Validation Rules ile taban sütunları tutarlı mı?
- F-22: arama kayıtları (JSON yolu + kanonik nesne) topluluğu olarak karşılaştırılıyor mu; yalnız navigation kaydını silen ya da değiştiren PR doğru sayfayı seçiyor ve CI-12 kırmızı veriyor mu; CI-12'nin "navigation kaydı gerekir" ölçütü kural dosyasıyla (§2, §5) tutarlı mı ve RC-9'dan ayrışması açıkça yazılmış mı; AC-18 (e) adlı yeni kırmızı veriyor mu?
- Rev 11 başka bir şeyi değiştirdi mi (UIC, RC, D, FU, dilim tablosu, künye, AC-1…AC-16, AC-20 aynı mı; inceleme 09'da kapanan silme ve CI kanıtı metinleri korunmuş mu)?
- Kapanış: inceleme 10'un iki bulgusunu bu kayıtta an. Düzeltildiyse F-23 için bulgu satırı yazma; tek bir düz metin satırında "Kapananlar (inceleme 10): F-23 …" biçiminde gerekçesiyle an (satır "Kapananlar" kelimesiyle başlar). F-22 (ORTA) bir bulgu satırı olarak `kapandı` ya da `açık` ile yazılır. Düzeltilmeyen ya da yeni bulgu F-24'ten başlar.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 35e035a1a2318731b1af34abc3ae2a3dbd25c7000f44c4e22b3557dfa401c4ae
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b, fca60c96, d478079a
Kapananlar (inceleme 10): <F-23 için kısa gerekçe; düzeltilmediyse burada anma, F-24'ten bulgu satırı yaz>
F-22 | ORTA | <kapanış gerekçesi> | <kapandı ya da açık> | <rol> | <YYYY-AA-GG>
F-24 | ENGEL | <yeni bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; kapanan ENGEL'leri yalnız "Kapananlar" satırında an — o satırda ve başka düz metin satırlarında seviye adlarını büyük harfle yazma ve satırı "F-" ile başlatma (ayrıştırıcı bunları bozuk bulgu satırı sayar); açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-11/MOD-0286-review-11-bc06039e3.zip
sha256 c0bfcbcb02f0bbd39324d9c9454bcf8a83757d1e3527eef5de2e31464fa819dd
```
