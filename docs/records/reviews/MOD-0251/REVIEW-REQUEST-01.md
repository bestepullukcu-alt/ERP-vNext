# MOD-0251 — bağımsız inceleme isteği 01 (SOP §20.4 / CT-APR-02; mevcut sürümün ilk incelemesi)

- Hedef: `execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md` @ sha256 `edb2c3215e731fa596c6629b8e75259c48ef5ed124e1de3f1bebc60cb506f5ea` (son değişiklik 9d8551e1e, 2026-09-07)
- Kaynak commit: ae6ed9d2e (dal `chore/ct/hcm-employee-ct-01`; origin/main 826150ef4 birleştirildi; yerel, push edilmedi)
- Önceki inceleme: yok — `execution/approvals/reviews.tsv` ve `approvals.tsv`'de MOD-0251 satırı 0 (2026-10-27).
- Neden şimdi: paket `approved` / `conditionally-approved-for-draft-reference-validation-slice`; sıradaki iş bu paketin "aktifleştirme ve izne hazır çalışan" dilimiyle revizyonudur (HCM-EMP-05). Revizyon yazılmadan önce mevcut sürümün bağımsız taban incelemesi isteniyor (HCM-EMP-03). CT'nin girdileri: keşif kaydı `docs/records/audits/2026-10/hcm-employee-discovery-01/RECORD.md` ve gerçeklik denetimi `docs/records/audits/2026-10/hcm-emp-02-inspection-01/INSPECTION-NOTE.md`.
- Bilinen eksik (CT beyanı): pakette `## Teslim dilimleri` tablosu yok (SOP §20.4 madde 6).
- İsteyen: depo sahibi (rol) · Hazırlayan: Çalışan kaydı Control Tower oturumu · Tarih: 2026-10-27 · İnceleyen: Codex (sahip iletir; sahip sözü `inceleme-codex`) · Model · Effort: en güçlü mevcut model · high; beyan verilemiyorsa "verilemiyor".
- ZIP: `MOD-0251-review-01-ae6ed9d2e.zip` · sha256 `1f3404dbcf717e646295f26d5cc71fc1c2fcb26f931348b656a00accc74b9363` · 378 849 bayt · 141 dosya + `MANIFEST.sha256`
  - Yeri (S-29): `C:\dev\ERP-vNext\.claude\worktrees\hcm-employee-ct-01\.ct-teslim\MOD-0251-review-01\MOD-0251-review-01-ae6ed9d2e.zip`; depoya girmez.
  - Gizlilik: geliştirme imza anahtarı taşıyan iki `appsettings*.json` çıkarıldı; Auth `DataSeeder.cs` (e-posta adresleri içerir) yerine satır numaralı MOD-0251 alıntısı kondu (`excerpts/withheld-files.txt`). Blueprint 8.8'den ilgili 18 satır `excerpts/blueprint-8.8-hcm-rows.txt`'de.
- Bulgu dosyası: `docs/records/reviews/MOD-0251/<YYYY-AA-GG>-edb2c321.md`; bulgular F-1'den.
- Sonra: ham sonuç commit'siz taslak olarak bu klasöre → CT karşılaştırması → bulgu dosyası commit (sahip sözüyle) → `review-row` → paket revizyonu `module-pack-author` ile.
- Kişi adı ve e-posta yazılmaz; roller kullanılır.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0251 Core HR / Employee Master module pack bağımsız incelemesi 01 (SOP §20.4 / CT-APR-02; mevcut sürümün ilk taban incelemesi). İnceleyensin; paketi yazan ya da denetleyen oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0251-review-01-ae6ed9d2e.zip · sha256 1f3404dbcf717e646295f26d5cc71fc1c2fcb26f931348b656a00accc74b9363 · kaynak commit ae6ed9d2e (CT beyanı; yerel dal) · önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md @ sha256 edb2c3215e731fa596c6629b8e75259c48ef5ed124e1de3f1bebc60cb506f5ea — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; ortamın vermiyorsa "verilemiyor" yaz, uydurma.
Gizlilik: iki appsettings dosyası ve Auth DataSeeder.cs ZIP'te yok ya da alıntıdır (excerpts/withheld-files.txt); bunların yokluğunu bulgu sayma.

Bu, paketin ilk bağımsız incelemesidir (önceki inceleme yok). Paket approved / conditionally-approved-for-draft-reference-validation-slice; yalnız P2 taslak / referans doğrulama dilimi onaylı. Sıradaki iş bu paketin aktifleştirme dilimiyle revizyonudur; bu inceleme onun tabanıdır.

Önce oku: hedef paket (tamamı); execution/domains/human-capital-management/domain-config.md; excerpts/blueprint-8.8-hcm-rows.txt (MOD-0251, 0280, 0288, 0298, 0299, 0314, 0360, 0362 satırları ve SoR_Map); CT keşif kaydı docs/records/audits/2026-10/hcm-employee-discovery-01/RECORD.md; CT gerçeklik denetimi docs/records/audits/2026-10/hcm-emp-02-inspection-01/INSPECTION-NOTE.md; execution/approvals/POLICY-REVIEW-START.md; docs/guides/operations/control-tower-sop.md §20.4; AGENTS.md Çekirdek. Kod: services/Diten.HcmService/**, frontend/Diten.Web/Controllers/HCM ve Views/HCM, gateway/Diten.ApiGateway/ocelot.json (HCM rotaları ~2788-2854), excerpts/auth-dataseeder-mod0251-lines.txt, Platform HcmEmployeeMasterManifestProvider.cs, HumanCapitalService EmployeeProfileProjection.cs.
Derleme ve test koşamazsın; ölç ve ÖLÇTÜKLERİNİ yanıtın sonunda tek "Ölçülenler:" satırında (| işareti kullanmadan) yaz.

İnceleme derinliği (risk HIGH — kiracı verisi, kişisel veri, yetki):
- Kimlik ve kapsam: paket Blueprint 8.8 MOD-0251 tanımıyla (iç çalışan kaydı; Employee, Employment Record, Job Assignment) uyumlu mu; MOD-0288 (kişi), MOD-0298 (profil çalışma alanı), MOD-0299 (atama), MOD-0314 (hassas erişim), MOD-0360 (kullanıcı ve kimlik bağı) sınırları doğru mu; MOD-0299 hakkındaki "deprecated SaaS Billing alias" notu bugünkü kimlik kaydıyla tutarlı mı?
- Onay durumu ve kanıt: approved + conditional künyesi kendi içinde tutarlı mı; P2 "browser-validated / PASS" iddialarının ZIP'te kayıtlı kanıtı var mı (paket docs/records/acceptance-reports/MOD-0251-scope-containment-rework-2026-06-21.md'yi gösteriyor; CT bu dosyanın hiçbir ref'te olmadığını ölçtü — doğrula)?
- Kod ↔ paket: CT denetiminin şu iddialarını koddan bağımsız doğrula ya da çürüt: çalışan liste/detay okuması (GET /api/v1/hcm/employees, /{id}) canlı ve onaylı dilim dışında; Gateway /api/v1/hcm/employees/{everything} POST/PATCH/DELETE geçiriyor; onaylı dilim dışı mod0251.* izin anahtarları tohumlanmış (paket "pending" diyor); audit yalnız log (DraftAuditService), GWT-A12 fail-closed karşılanmıyor; workflow-decisions uç noktasında [HasPermission] yok; smoke fixture yalnız ortam adıyla korunuyor. CT'nin kaçırdığı bir sapma var mı?
- Kabul kriterleri: GWT-A01…A13, B01…B07, D01…D07 ölçülebilir mi; her biri için test ya da kanıt yolu tanımlı mı; "P2 implemented" diye işaretlenenlerin testi koddaki 52 test yönteminde var mı?
- Dilim tablosu: `## Teslim dilimleri` yok — revizyon için hangi dilimler önerilir (yalnız öneri; paketi yazma)?
- Sıradaki dilim için ön koşullar: aktifleştirme (MOD-0023 iş akışı), çalışan–kullanıcı bağı (paket "Auth UserId PersonId yerine kullanılamaz" der; Blueprint MOD-0360 kimlik bağını sahiplenir), olaylar (employee.created.v1 / activated), çalışma yeri / FTE / kıdem tarihi alanları — paket bunlar için hangi kararları açık bırakıyor?
- Eskimiş ya da çelişen metin: spec yolu (docs/specs/… → docs/reference/architecture/…), "no-valid-HEAD" cümlesi (paket ve domain-config), test sayıları (5 / 13 / bugün 52), Ready-for-dev listesindeki işaretler.
- Bulgu varsa F-1'den başla.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0251 — bağımsız inceleme
hedef: execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md @ edb2c3215e731fa596c6629b8e75259c48ef5ed124e1de3f1bebc60cb506f5ea
değerlendirilen olumsuz incelemeler: yok
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya:satır> | açık | - | -
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Ölçülenler: <tek satır, | işareti yok>
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan (ENGEL için yalnız açık ya da kabul — onaylayan); UYGUN sonucunda ENGEL satırı olamaz; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; açık ORTA ve DÜŞÜK için sahip ve son tarih yazılır; Sonuç satırı gerekçesiz; düz metin satırlarında | işareti kullanma.
```

Ek dosya:

```text
C:\dev\ERP-vNext\.claude\worktrees\hcm-employee-ct-01\.ct-teslim\MOD-0251-review-01\MOD-0251-review-01-ae6ed9d2e.zip
sha256 1f3404dbcf717e646295f26d5cc71fc1c2fcb26f931348b656a00accc74b9363
```
