# MOD-0286 — bağımsız inceleme isteği 09 (SOP §20.4 / CT-APR-02; içerik incelemesi)

- Hedef: `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` @ sha256 `9ef097321c397a47d26cbc84d964548c3835d0201a540ae17f7d02d6ccd6764d` (revizyon 09)
- Kaynak commit: 35c9fe94a (dal `chore/ct/pss-ui-ct-01` = main `ded2a4de0` + rev 08 + rev 09; yerel, push edilmedi)
- Onaylı geçerli taban: rev 06 `7f6868d9…`. Rev 07 `ffeaae57…` incelendi (07 KOŞULLU, 07b UYGUN) ama onayı main'de geçersiz (satırlar main-ilk ebeveyn birleştirmesinde kaldı). Rev 08 `ebaf59e0…` = rev 07 + tek not satırı; **incelenmedi ve onaylanmayacak** (sahip kararı `daralt-rev09`).
- Neden: sahip kararı "daralt" (2026-10-26; karar kaydı `docs/records/decisions/2026-10/pss-mod0286-daraltma-01.md`, ZIP'te): P1 bitiş çizgisi CI'da (DL-3), P2 kapsam dışı, DL-1 kısa dizin, D-8a..c kuralın kendisinde, yeni D-9.
- Rev 09 değişiklikleri (paket yazarı, CT bağımsız ölçtü): künye `slice:` ve başlık bandı; Open Decisions (D-4 kapandı, D-7, D-8a..c durumu, yeni D-9); Module Summary; Ownership and Boundaries; Owned Objects (yeni `### DL-3 UI uyum CI bağlantısı`, CI-1…CI-15 kontrol tablosu); P2 taslak bölümlerinin kısaltılması (Entity Fields, Layout & Shell Contract, Backend File Convention, Frontend File Contract, Authorization Convention, Gateway / API Routing Decision, Page Help); Repo Scope; Protected Paths (`.github/workflows/**`, `.github/CODEOWNERS`); Dependencies; Runtime Constraints; Validation Rules (taban biçimi); Failure Path to Verify; Audited Events; AC-3 ve AC-11 güncellendi, AC-17…AC-20 yeni; Test Expectations (DL-3 ölçüm yolları ve rev 09 ölçümleri); Ready-for-dev; Implementation Notes (çatışma kaydı, "Rev 09" satırı); Follow-up (FU-7 yeniden, FU-14, FU-15 yeni); Teslim dilimleri (DL-1 "kısa dizin", DL-3 yeniden).
- CT ölçümü (35c9fe94a): 671 satır; `## ` 24 (22 zorunlu + Open Decisions + Page Help); UIC 32 ve RC 24 satır rev 08 ile bayt bayt aynı; AC 20, yalnız AC-3 ve AC-11 değişti; `slices()` = DL-1, DL-2, DL-3; kapının künye okuyucusu `(None, None)`; `pack_approval.py check`: "paket onaydan sonra değişti (333 satır) — yeniden onay gerekli".
- Fark: `excerpts/MOD-0286-pack-diff-6d8cc6ace-to-35c9fe94a.diff` (rev 08 → rev 09); önceki sürüm `excerpts/MOD-0286-pack-previous-version-6d8cc6ace.md`. Rev 06 → rev 07 farkı inceleme 07'nin konusuydu.
- DL-3 ölçümleri (paket yazarı, depo dışında, HEAD 6d8cc6ace): `excerpts/dl3/` (betikler ve ham çıktılar `verifier.tsv`, `verifier_full.tsv`, `navkeys.out`).
- Kurulum CT notları (bağlayıcı): DL-3 sahipliği "kod: PSS-UI CT; workflow dosyası: CT-Setup + sahip onayı"; taban satırında FU kimliği ve sahip; Golden Compact "hazır değil" satırı sahibi developer-enablement.
- İsteyen: depo sahibi (rol) · Hazırlayan: PSS-UI Control Tower oturumu · Tarih: 2026-10-26 · İnceleyen: Codex (sahip iletir) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0286-review-09-35c9fe94a.zip` · sha256 `e7b3fcf1c50e8f951770704b56aa3d355e9aaaf3b4a16621ca1f47de55079344` · 1 718 645 bayt · 176 dosya + `MANIFEST.sha256`
  - Yeri (S-29, S-36): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-09/MOD-0286-review-09-35c9fe94a.zip`; depoya girmez. İlk kurulum tam backlog dosyasını içermiyordu; `MOD-0286-review-09-INCOMPLETE-01` olarak kenara alındı.
- Bulgu dosyası: `docs/records/reviews/MOD-0286/<YYYY-AA-GG>-9ef09732.md`; numaralar F-19'dan.
- Sonra: bulgu dosyası commit → `review-row` 09 → `check` kuru koşu (insan adımı dahil) → tek onay satırı (`--author GMG-ERP --approver bestepullukcu-alt`) en sonda, dalın kendi çizgisinde → push ve PR sahibin sözüyle, bestepullukcu-alt doğrudan merge eder.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0286 module pack bağımsız incelemesi 09 (SOP §20.4 / CT-APR-02; içerik incelemesi). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0286-review-09-35c9fe94a.zip · sha256 e7b3fcf1c50e8f951770704b56aa3d355e9aaaf3b4a16621ca1f47de55079344 · kaynak commit 35c9fe94a (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ sha256 9ef097321c397a47d26cbc84d964548c3835d0201a540ae17f7d02d6ccd6764d — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.

Bu revizyon 09'dur: sahibin "daralt" kararıyla kapsam daraltması (karar kaydı docs/records/decisions/2026-10/pss-mod0286-daraltma-01.md). P1 bitiş çizgisi: DL-2'nin otomatik maddeleri yeni ve değişen sayfalar için CI'da koşar, eski sayfalar taban olarak kaydedilir ve CI'ı durdurmaz (yeni DL-3 "UI uyum CI bağlantısı", AC-17…AC-20). P2 kapsam dışı (ayrı paket), DL-1 kısa dizin, D-8a..c kuralın kendisinde düzeltilir, yeni D-9. Onaylı geçerli taban rev 06; rev 07 içerik incelemesini (inceleme 07/07b) geçti ama onayı geçersiz; rev 08 = rev 07 + tek not satırı, incelenmedi. Fark rev 08 → rev 09: excerpts/MOD-0286-pack-diff-6d8cc6ace-to-35c9fe94a.diff; önceki sürüm excerpts/MOD-0286-pack-previous-version-6d8cc6ace.md.

Önce oku: karar kaydı; pack diff; hedef paketin künye ve başlık bandı, Open Decisions (D-4, D-8a..c, D-9), Owned Objects (### DL-3), Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Validation Rules, Failure Path to Verify, Acceptance Criteria (AC-3, AC-11, AC-17…AC-20), Test Expectations, Follow-up Items, Teslim dilimleri; .github/CODEOWNERS ve .github/workflows/*.yml; tests/known-failures.tsv; .antigravity/rules/module-pack-standard.md (22 bölüm, künye, dilim tablosu); .antigravity/rules/docs-organization.md (taban dosyasının yeri); .antigravity/scripts/verify_datatable_page.py; scripts/run_record.py; scripts/pack_approval.py slices(); excerpts/dl3/ (DL-3 ölçüm betikleri ve ham çıktıları); docs/roadmap/backlog/product-backlog.md (tam dosya); .antigravity/agents/frontend-ui-ux.md ve orchestrator.md (D-9 atıfları).
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (içerik; risk HIGH):
- Daraltma tam ve tutarlı mı: paketin hiçbir yerinde P2 için bağlayıcı bir kapsam, izin, varlık, yönlendirme ya da kabul maddesi kaldı mı (bilinçli bırakılanlar: başlık bandındaki Blueprint özeti, D-5, D-6, D-7, AC-11 koruması, FU-7, Dependencies'teki yönlendirme cümlesi, rev 01–08 notları)? 22 zorunlu bölüm duruyor mu?
- DL-3 uygulanabilir ve ölçülebilir mi: CI-1…CI-15 kontrollerinin her biri sayfa başına genellenebilir mi (genellenemeyen RC'ler adıyla dışarıda mı); değişen sayfa seçiminin tanımı (hangi yollar, paylaşılan dosyalar) eksiksiz mi; ayrı CI işi bugünkü kırmızı phase1'den gerçekten bağımsız mı?
- AC-17…AC-20: her biri tek başına ölçülebilir mi; AC-18 sabotajı "adlı yeni kırmızı" kuralına uyuyor mu (ölçüt iki sabit değerle sağlanamaz mı); AC-19'un taban kuralları (FU kimliği, sahip, ekleme sahip kararı, silme yalnız kapanınca, tekrar ve boş hücre exit 2) çelişkisiz mi; AC-20 "tam süit yok" ölçütü doğrulanabilir mi?
- Taban dosyasının yeri (tests/ui-compliance-baseline.tsv) docs-organization ve tests/known-failures.tsv örneğiyle tutarlı mı; anahtar (sayfa + kontrol + ayrıntı) ikinci kırmızıyı gizlemiyor mu?
- Sahiplik ve korunan yollar: workflow dosyası CT-Setup + sahip onayı (CODEOWNERS satırlarıyla) doğru yazılmış mı; DL-3 iş paketi workflow dosyasına yazma yetkisi taşımıyor mu (onay gelene dek yerel run_record)?
- AC-3 ve AC-11 değişiklikleri D-8 kararıyla ve P2 çıkışıyla tutarlı mı; AC-1…AC-16'nın başka bir maddesi daraltmayla çelişiyor mu?
- D-9: atıfları (frontend-ui-ux.md :66-67, :78, :95; orchestrator.md :25, :49) ZIP'teki dosyalarda doğru mu; karar paketin yetkisi dışında mı tutulmuş (XMC-001, .antigravity onayı)?
- Ölçüm iddiaları: Test Expectations'taki rev 09 sayıları (ör. 152 liste sayfası, 1182 boşluk satırı, FU-4 63 sayfa, Platform 19 sayfanın 11'inde Ctrl+K kaydı yok) excerpts/dl3/ ham çıktılarıyla tutuyor mu (örnekleme yeterli)?
- Künye: status ready-for-dev; slice: satırının yeni metni DL-3 ile tutarlı mı; dilim tablosundaki her hücre parçası gerçek başlık mı?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0286 — bağımsız inceleme
hedef: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md @ 9ef097321c397a47d26cbc84d964548c3835d0201a540ae17f7d02d6ccd6764d
değerlendirilen olumsuz incelemeler: fb15f188, 7242768b
F-19 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-20 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; numaralar 19'dan başlar; kapanış yalnız açık / kapandı / kabul — onaylayan; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pss-ui-ct-01/.ct-teslim/MOD-0286-review-09/MOD-0286-review-09-35c9fe94a.zip
sha256 e7b3fcf1c50e8f951770704b56aa3d355e9aaaf3b4a16621ca1f47de55079344
```
