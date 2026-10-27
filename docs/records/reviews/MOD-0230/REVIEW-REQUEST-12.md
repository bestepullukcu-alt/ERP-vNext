# MOD-0230 — bağımsız inceleme isteği 12 (SOP §20.4 / CT-APR-02)

- **Hedef:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`, sha256 `a51f2349a83ee99c0a47c0528ca429a83ba48bc2844f22a488b13a9fdada7966` (revizyon 12).
- **Kaynak commit:** `65c6380e9`. Dal `chore/ct/pvg-ct-01`, yerel, push edilmedi.
- **Risk:** Onay kartı HIGH hesaplıyor. Bağımsız inceleme bu yüzden gerekli.
- **Önceki onaylı sürüm:** revizyon 11 (`918dd660…`). İnceleme 11'in sonucu KOŞULLU: tek bulgu F-1, önem DÜŞÜK. Onay satırı main'de (#261, HIGH).
- **Değerlendirilen olumsuz incelemeler:** `4adc6faf`, `59d5a954`, `bfb5c82f`, `e7ccbcf9`.
- **Bu sürümün nedeni:** WP-0230-DL7A (ekran) prompt'u yazılırken iki çelişki ölçüldü. Ayrıca iki düzeltme bekliyordu.
  1. AC-2, MVC denetleyicisinde `[HasPermission]` istiyor. Diten.Web'de böyle bir öznitelik yok; ekranlar `RequirePage` / `RequireJson` + `PermissionClaims.HasPermission` kalıbını kullanıyor (ör. CRM `AccountsController`).
  2. AC-20(a), tarayıcı isteğinde `pageNumber`/`pageSize` istiyor. Oysa ortak `createList` bileşeni `start`/`length` gönderiyor ve sayfa içi istek eşlemesini yasaklıyor (`frontend-datatable-template.md`).
  3. Kiracı claim adı. Sahip kararı ODPVG2-02 ("claim-duzelt"): token'daki ad `tenant_id`; AuthService bu adı yazıyor.
  4. İnceleme 11'in F-1 bulgusu: revizyon 11 notu AC-10'un CI bağını eksik anlatıyordu. Düzeltme revizyon 12 notunda.
- **Yetki:**
  - Sahibin sözü "paket-12" (2026-10-27).
  - Karar kayıtları ZIP'te: ODPVG2-02, RTA-01, ODPVG6-01, ODPVG1-01, DALGA5-01.
- **İddia (CT ölçümü, `excerpts/ct-parser-and-slice-check.txt`):**
  - `parse_pack` 33 zorunlu ölçüt okuyor.
  - Metni değişen AC satırları AC-2, AC-5 ve AC-20. AC-5'te yalnız ön koşuldaki claim adı değişti.
  - Dilim tablosu ve `gerekli kanıt` sütunu aynı.
  - Revizyon 11 notu bayt bayt aynı kaldı (geçmiş not).
- **İsteyen:** depo sahibi (rol). **Hazırlayan:** PVG Control Tower oturumu. Revizyon 12'yi `module-pack-author` alt ajanı yazdı; effort high. **Tarih:** 2026-10-27.
- **İnceleyen:** yeni bir Codex oturumu. İstenen: en güçlü model, effort high. Yanıtın başına model, effort ve oturum kimliği yazılır.
- **ZIP:** `MOD-0230-review-12-65c6380e9.zip`, sha256 `6541f6734a382f88eb7a45fdd38fc9d75d4132612cef1f29ff1df9fb3218efed`, 391 749 bayt. İçinde 35 dosya ve `MANIFEST.sha256` var.
  - Yeri (S-29): `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-12/`.
  - Kaynak dosyalar `git archive 65c6380e9` ile bayt bayt aynı.
  - E-posta taramasında bulgu çıkmadı.
- **Bulgu dosyası:** `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-a51f2349.md`. İnceleyenin çıktısı olduğu gibi kaydedilir.

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
HEDEF: Codex — MOD-0230 module pack bağımsız incelemesi 12 (SOP §20.4 / CT-APR-02). İnceleyensin; paketi yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki bulgu metnidir.

Kaynak: ZIP MOD-0230-review-12-65c6380e9.zip · sha256 6541f6734a382f88eb7a45fdd38fc9d75d4132612cef1f29ff1df9fb3218efed · kaynak commit 65c6380e9 (sahip beyanı) · MANIFEST.sha256 ZIP içinde; önce `shasum -a 256 -c MANIFEST.sha256` ile doğrula.
Hedef belge: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 a51f2349a83ee99c0a47c0528ca429a83ba48bc2844f22a488b13a9fdada7966 — tutmuyorsa dur ve bildir.
Model · Effort (istenen): <model> · high — kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz (reviews.tsv beyanı).

Bu revizyon (12) dört şeyi değiştiriyor: (1) AC-2: MVC denetleyicisinin 10 eylemi `pvg.case-intake-triage.*` anahtarını Diten.Web ev kalıbıyla denetler — sayfa eylemleri denetleyiciye özel `RequirePage(<key>)` (çıplak 403, `/Home/Status/403`'e yeniden yürütülür), JSON eylemleri `RequireJson(<key>)` (403 + mesaj gövdesi), ikisi de `PermissionClaims.HasPermission` ile; `[HasPermission]` Diten.Web'de yok. (2) AC-20(a)(b): liste ortak `DitenDataTable.createList({ dataMode: 'server' })` bileşenidir; tarayıcı `start`/`length` gönderir, MVC vekili `List` eylemi bunları API'nin `pageNumber = start/length + 1`, `pageSize = length` (≤ 100) değerlerine eşler; test adları aynı. (3) Kiracı claim adı `tenant_id` (sahip kararı ODPVG2-02; AuthService `tenant_id` yazıyor). (4) İnceleme 11 F-1: revizyon 12 notu, AC-10'un onaylı bağ biçimini (ApprovalReference + CI varlık denetimi, RTA-01) ve CI kanıtının da gerektiğini söylüyor; revizyon 11 notu değişmedi. Revizyon 11 (918dd660) onaylı (HIGH); inceleme 11 KOŞULLU, F-1 DÜŞÜK.

Önce oku (hepsi ZIP'te): hedef paket; excerpts/MOD-0230-pack-diff-918dd660-to-a51f2349.diff; excerpts/MOD-0230-pack-rev11-version-918dd660.md; karar kayıtları (od-pvg-2-onay-01, od-pvg-2-onay-02, od-pvg-6-bayrak-01, runtime-onay-kaydi-01, od-pvg-1-izin-01, dalga5-plani-01); ev kalıbı kaynakları: frontend/Diten.Web/Controllers/CRM/AccountsController.cs (RequirePage/RequireJson), frontend/Diten.Web/Security/PermissionClaims.cs, frontend/Diten.Web/Program.cs (UseStatusCodePagesWithReExecute), frontend/Diten.Web/wwwroot/assets/js/diten-datatable.js (createList, toServerQuery), .antigravity/rules/frontend-datatable-template.md, frontend/Diten.Web/Controllers/GlobalProductsController.cs (BuildListQuery), CRM LegalProfiles index.js; bugünkü MOD-0230 denetleyicisi, index.js ve vitest dosyası; AuthService TokenService.cs (tenant_id); permission-key-standard.md, security-jwt.md, module-pack-standard.md; inceleme 11 bulgusu, karşılaştırma notu 11 ve REVIEW-REQUEST-11.md; excerpts/pack-approval-card.txt; excerpts/ct-parser-and-slice-check.txt; execution/approvals/*.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç.

İnceleme derinliği:
- AC-2'nin yeni metni ev kalıbını doğru anlatıyor mu (yardımcı adları, 403 davranışı, 10 eylemin listesi bugünkü denetleyiciyle aynı mı), ölçülebilir mi ve AC-3 (UAS-001: yetkisiz kullanıcıya iskelet yok) ile çelişiyor mu?
- AC-20(a)(b) createList kalıbıyla tutarlı mı; vekil eşlemesi (start/length → pageNumber/pageSize, ≤100) doğru tanımlı mı; dört test adı aynı mı ve vitest'te ölçülebilir mi (vekil eşlemesi C# tarafında — vitest'ten nasıl ölçülür)?
- `tenant_id` değişikliği yalnız claim adında mı (X-Tenant-Id başlığı ve C# özellik adları dokunulmamış mı)? AC-5'te yalnız ön koşul mu değişti?
- Revizyon 12 notu dört değişikliği doğru ve eksiksiz anlatıyor mu; F-1 düzeltmesi doğru mu; revizyon 11 notu bayt bayt aynı mı?
- Değişmesi gerekmeyen hiçbir AC, dilim, yol, kapsam, gerekli kanıt seviyesi değişmiş mi? Kapanmamış bir konu kapanmış gösterilmiş mi?
- Gerileme: inceleme 11'in dayandığı metinlerden biri yanlışlıkla değişti mi?

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz):
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ a51f2349a83ee99c0a47c0528ca429a83ba48bc2844f22a488b13a9fdada7966
değerlendirilen olumsuz incelemeler: 4adc6faf, 59d5a954, bfb5c82f, e7ccbcf9
F-1 | ENGEL | <bulgu; paket satır numarası ve dayanak dosya> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <rol> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL> (tek değer, | işareti yok)
Kurallar: seviye adını (engel, orta, düşük) bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; "düzeltildi@commit" kapanış değildir; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; Sonuç satırı gerekçesiz, gerekçe bulgu satırlarındadır; düz metin açıklama satırlarında | işareti kullanma.
```

Ek dosya:

```text
/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-ct-01/.ct-teslim/MOD-0230-review-12/MOD-0230-review-12-65c6380e9.zip
sha256 6541f6734a382f88eb7a45fdd38fc9d75d4132612cef1f29ff1df9fb3218efed
```
