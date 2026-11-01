# MOD-0286 — bağımsız inceleme isteği 12 (SOP §20.4 / CT-APR-02; inceleme 11 bulgusunun kapanışı)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44` (revizyon 12)
- Kaynak commit: db61cf944 (dal `chore/ct/pss-ui-ct-01`; main `aafe15a7a` dal-ilk birleştirmeyle alındı, 73d4118dd; yerel, push edilmedi)
- Önceki: rev 11 `35e035a1…` — inceleme 11 **UYGUN DEĞİL** (F-24 ENGEL; F-22, F-23 kapandı; kayıt `2026-10-26-35e035a1.md`, satır @ 8870bf332). Geçerli onaylı taban rev 06 `7f6868d9…`.
- Rev 12 = rev 11 + yalnız F-24 kapanışı (sahip kararı `kaydet-rev12`): yeni `### DL-3 satır kontrol desenleri` — tek bağlayıcı kaynak bir ```text bloğu (`CI-n<TAB>ham Python re`), yanında okunur tablo; sayım kuralı ve `--self-test` deseni bloktan okur; CI tablosunun "Red when" sütunu yalnız açıklama. UIC / RC / AC-1…AC-20 / D / FU / dilim tablosu / künye rev 11 ile aynı. 820 satır; 57 ekleme / 13 silme.
- **CT ön denetimi (gönderilmeden önce):** bloktaki 9 desen (`CI-2`, `CI-4`, `CI-5`, `CI-6v`, `CI-6j`, `CI-7`, `CI-8`, `CI-9p`, `CI-10`) `re.compile` ile derleniyor; ölçüm betiği `excerpts/rev11/counts.py` ile bayt bayt aynı; 189 sayfada 321 dosya / 743 eşleşme (rev 11 ölçümüyle aynı); okunur tablonun 9 satırı ve CI tablosunun CI-5 hücresi kaçışlar çözülünce blokla aynı. Betik ve çıktı: `excerpts/rev12-ct-precheck/`.
- Fark: `excerpts/MOD-0286-pack-diff-bc06039e3-to-db61cf944.diff`; önceki sürüm `excerpts/MOD-0286-pack-previous-version-bc06039e3.md`.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-26 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-12-db61cf944.zip` · sha256 `aab87991f2e7e607802a89dbb0cf7470866ea4bde227158c9dfcf3ef93982d04` · 1 778 025 bayt · 193 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-12/MOD-0286-review-12-db61cf944.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-89333f90.md`; yeni bulgular F-25'ten.
- Sonra: bulgu dosyası commit → `review-row` 12 → `check` kuru koşu (insan adımı dahil, S-48 uyarısı) → tek onay satırı en sonda, dalın kendi çizgisinde → push ve PR sahibin sözüyle, bestepullukcu-alt doğrudan merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 12 (SOP §20.4 / CT-APR-02; inceleme 11 bulgusunun kapanışı). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-12-db61cf944.zip · sha256 aab87991f2e7e607802a89dbb0cf7470866ea4bde227158c9dfcf3ef93982d04 · kaynak commit db61cf944 (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 12'dir: senin inceleme 11'in (rev 11 35e035a1…, UYGUN DEĞİL; docs/records/reviews/MOD-0286/2026-10-26-35e035a1.md ve CT karşılaştırması -ct-karsilastirma-11.md) tek bulgusu F-24'ü kapatır; başka değişiklik iddiası yoktur. Fark: excerpts/MOD-0286-pack-diff-bc06039e3-to-db61cf944.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-bc06039e3.md. CT ön denetimi: excerpts/rev12-ct-precheck/ (betik ve çıktı); ölçüm betiği excerpts/rev11/counts.py.

Önce oku: inceleme 11 kaydı ve CT karşılaştırması 11; pack diff; hedef paketin "### DL-3 satır kontrol desenleri" bölümü (açıklama, okunur tablo, text kod bloğu), DL-3 bölüm 1 --self-test maddesi, bölüm 2 sayım kuralı, CI tablosu ve girişi, Implementation Notes "Rev 12" satırı; excerpts/rev12-ct-precheck/precheck_patterns.py ve precheck-output.txt; excerpts/rev11/counts.py.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz. Python re ile derleme ve metin üzerinde sayım yapabiliyorsan yap; yapamıyorsan bunu Ölçülenler'de yaz.

İnceleme derinliği (risk HIGH):
- F-24: kod bloğu tek bağlayıcı kaynak mı; her satır kontrolü (CI-2, CI-4, CI-5, CI-6 görünüm ve JS, CI-7, CI-8, CI-9 api/personalization, CI-10) için bir desen var mı; her desen geçerli bir Python düzenli ifadesi mi ve amaçlananı sayıyor mu (nokta, parantez ve dikey çizgi kaçışları); okunur tablo ve CI tablosu blokla çelişiyor mu; sayım kuralı ve --self-test deseni bloktan okuyor mu ve tablo hücresinden okumayı açıkça yasaklıyor mu?
- Ölçüm eşliği: bloktaki desenler counts.py ile aynı mı ve 321 dosya / 743 eşleşme sayısı bu desenlerden mi geliyor (CT ön denetimini oku; kendin derleyebiliyorsan derle)?
- Rev 12 başka bir şeyi değiştirdi mi (UIC, RC, AC-1…AC-20, D, FU, dilim tablosu, künye, rev 11'in sayım kuralı ve arama kaydı karşılaştırması aynı mı)?
- Kapanış: düzeltildiyse F-24 için bulgu satırı yazma; tek bir düz metin satırında "Kapananlar (inceleme 11): F-24 …" biçiminde gerekçesiyle an (satır "Kapananlar" kelimesiyle başlar). Düzeltilmediyse ya da yeni bulgu varsa F-25'ten bulgu satırı yaz.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 89333f90d886cdb11b499459dd12b4bc3da4a82efc8e5467be171fe2a2b35d44
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b, fca60c96, d478079a, 7feec1de
Kapananlar (inceleme 11): <F-24 için kısa gerekçe; düzeltilmediyse burada anma, F-25'ten bulgu satırı yaz>
F-25 | ENGEL | <yeni bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; kapanan ENGEL'leri yalnız "Kapananlar" satırında an — o satırda ve başka düz metin satırlarında seviye adlarını büyük harfle yazma ve satırı "F-" ile başlatma; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-12/MOD-0286-review-12-db61cf944.zip
sha256 aab87991f2e7e607802a89dbb0cf7470866ea4bde227158c9dfcf3ef93982d04
```
