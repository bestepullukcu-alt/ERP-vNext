# MOD-0286 — bağımsız inceleme isteği 10 (SOP §20.4 / CT-APR-02; inceleme 09 bulgularının kapanışı)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `51bbd6298446639b6fb1a29aeff08d414819391e830a69f820e72583e35ce8db` (revizyon 10)
- Kaynak commit: 8cd2f5d4c (dal `chore/ct/pss-ui-ct-01` = main `ded2a4de0` + 5 commit; yerel, push edilmedi)
- Önceki: rev 09 `9ef09732…` — inceleme 09 **UYGUN DEĞİL** (F-19, F-20, F-21 ENGEL; F-22 ORTA; kayıt `2026-10-26-9ef09732.md`, satır `reviews.tsv` @ 530fc95f8). Geçerli onaylı taban rev 06 `7f6868d9…`.
- Rev 10 = rev 09 + yalnız inceleme 09 bulgularının kapanışı (sahip kararı `kaydet-rev10`, 2026-10-26): DL-3 bölüm 1 (seçici: `--find-renames --diff-filter=ACDMRT`, silinen ve taşınan yollar, Ctrl+K kayıt dosyaları), bölüm 2 (taban anahtarı sayfa + kontrol + örnek; satır kontrolleri için `<path>:<h12>#<k>` parmak izi), bölüm 3 (tetikleyici, artefakt), sahiplik notu (kabul yolu), CI tablosu (CI-3, CI-12, CI-13 "Runs on"), Dependencies, Validation Rules, Failure Path, Repo Scope, AC-17…AC-20, Test Expectations, Ready-for-dev, Implementation Notes "Rev 10" satırı, başlık bandı. UIC 32 / RC 24 / D / FU / dilim tablosu / künye rev 09 ile aynı; AC-1…AC-16 aynı. 740 satır; `git diff --stat`: 122 ekleme / 53 silme.
- CT ölçümü (8cd2f5d4c): `slices()` = DL-1, DL-2, DL-3; kapının künye okuyucusu `(None, None)`; UIC ve RC rev 09 ile aynı; değişen AC'ler yalnız 17-20.
- Fark: `excerpts/MOD-0286-pack-diff-35c9fe94a-to-8cd2f5d4c.diff`; önceki sürüm `excerpts/MOD-0286-pack-previous-version-35c9fe94a.md`.
- Ölçülmeyen (paket beyanı): satır kontrolleri için taban satır sayısı (makine yükü nedeniyle geniş koşu yapılmadı); seçicinin gerçek PR davranışı (betik henüz yazılmadı, DL-3 iş paketinin işi).
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-26 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-10-8cd2f5d4c.zip` · sha256 `f1e3554fad29596267505c0b48c7466caad65d043474bbe462d16b29e9a508a8` · 1 743 141 bayt · 181 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-10/MOD-0286-review-10-8cd2f5d4c.zip`; depoya girmez.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-51bbd629.md`; numaralar F-23'ten. İnceleme 09 bulguları bu kayıtta kapanışlarıyla yeniden yazılır.
- Sonra: bulgu dosyası commit → `review-row` 10 → `check` kuru koşu (insan adımı dahil) → tek onay satırı en sonda, dalın kendi çizgisinde → push ve PR sahibin sözüyle, bestepullukcu-alt doğrudan merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 10 (SOP §20.4 / CT-APR-02; inceleme 09 bulgularının kapanışı). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-10-8cd2f5d4c.zip · sha256 f1e3554fad29596267505c0b48c7466caad65d043474bbe462d16b29e9a508a8 · kaynak commit 8cd2f5d4c (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 51bbd6298446639b6fb1a29aeff08d414819391e830a69f820e72583e35ce8db — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 10'dur: senin inceleme 09'un (rev 09 9ef09732…, UYGUN DEĞİL; docs/records/reviews/MOD-0286/2026-10-26-9ef09732.md ve CT karşılaştırması -ct-karsilastirma-09.md) dört bulgusunu kapatır; başka değişiklik iddiası yoktur. Fark: excerpts/MOD-0286-pack-diff-35c9fe94a-to-8cd2f5d4c.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-35c9fe94a.md.

Önce oku: inceleme 09 kaydı; pack diff; hedef paketin DL-3 bölümleri (Owned Objects altında ### DL-3, bölüm 1 seçici, bölüm 2 taban, bölüm 3 CI işi, sahiplik notu), CI tablosu (CI-3, CI-12, CI-13), Dependencies, Validation Rules, Failure Path to Verify, Repo Scope, Acceptance Criteria AC-17…AC-20, Test Expectations (DL-3 ölçüm yolları, Rev 10 ölçümleri, Ölçülmedi), Implementation Notes "Rev 10" satırı; .antigravity/scripts/verify_datatable_page.py (:48-51, :953-975); .antigravity/rules/platform-global-search-registry.md; frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json ve .tr.json; .antigravity/rules/docs-organization.md; tests/known-failures.tsv; karar kaydı docs/records/decisions/2026-10/pss-mod0286-daraltma-01.md.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH):
- F-19: seçici silinen yolları ve yeniden adlandırmanın iki yolunu gerçekten eşliyor mu; "sayfa kaldırıldı" ve "taşınan sayfa" kuralları borcu gizleyen bir yol açıyor mu (ör. sayfayı taşıyarak taban satırlarını kapatıp yeni sayfanın kırmızılarını onaysız geçirmek mümkün mü); AC-18 (c) silme sabotajı adlı yeni kırmızı veriyor mu?
- F-20: `<path>:<h12>#<k>` parmak izi aynı dosyaya eklenen ikinci ihlali her durumda yeni anahtar yapıyor mu (farklı satır, birebir kopya, aynı satıra ikinci ihlal); normalleştirme kuralı ihlali gizleyebilir mi; AC-18 (b) zorunlu mu ve tek başına (a) ile kapanamıyor mu; Validation Rules taban sütunlarıyla tutarlı mı?
- F-21: AC-17 iki adımı (ara teslim / nihai kabul) açık mı; nihai kabul CI işinin kabul edilen commit'e bağlı koşusuna ve artefaktına zorunlu bağlı mı; yerel kayıtla ya da başka commit'teki CI koşusuyla kapanabilecek bir yol kaldı mı; Dependencies, Repo Scope ve sahiplik notu bununla tutarlı mı; DL-3 iş paketi workflow dosyasına yazma yetkisi taşımıyor mu?
- F-22: platform-search.{en,tr}.json tetikleyicide ve seçim eşlemesinde mi; kayıt ekleme, silme ve değişikliği doğru Platform sayfasını seçiyor mu (ZIP'teki iki JSON ve kayıt kuralından ölç); AC-18 (d) adlı yeni kırmızı veriyor mu?
- Rev 10 başka bir şeyi değiştirdi mi (UIC, RC, D, FU, dilim tablosu, künye, AC-1…AC-16 aynı mı)?
- Kapanış: inceleme 09'un dört bulgusunu bu kayıtta an. Düzeltilen F-19, F-20, F-21 için bulgu satırı yazma; tek bir düz metin satırında "Kapananlar (inceleme 09): F-19 …, F-20 …, F-21 …" biçiminde gerekçesiyle an (satır "Kapananlar" kelimesiyle başlar). Düzeltilmeyen varsa onu F-23'ten başlayan yeni bir bulgu satırı olarak yaz. F-22 (ORTA) bir bulgu satırı olarak `kapandı` ya da `açık` ile yazılır. Yeni bulgu F-23'ten başlar.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 51bbd6298446639b6fb1a29aeff08d414819391e830a69f820e72583e35ce8db
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b, fca60c96
Kapananlar (inceleme 09): <F-19, F-20, F-21 için kısa gerekçe; düzeltilmeyen varsa burada anma, F-23'ten bulgu satırı yaz>
F-22 | ORTA | <kapanış gerekçesi> | <kapandı ya da açık> | <rol> | <YYYY-AA-GG>
F-23 | ENGEL | <yeni bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; kapanan ENGEL'leri yalnız "Kapananlar" satırında an — o satırda ve başka düz metin satırlarında seviye adlarını büyük harfle yazma ve satırı "F-" ile başlatma (ayrıştırıcı bunları bozuk bulgu satırı sayar); açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-10/MOD-0286-review-10-8cd2f5d4c.zip
sha256 f1e3554fad29596267505c0b48c7466caad65d043474bbe462d16b29e9a508a8
```
