# WORK PACKAGE — WP-CT-FE-10 · Canlı E4 bulgularından küçük düzeltmeler (vv1 · GUID · odak · yayında kilit · kodsuz tip · durum etiketi) (frontend)

> **CT (SoR).** MOD-0162 Chain Template v2. Branch `feature/crm-chain-template` (E4-LIVE §37 `e644e15f` üstü). **Kaynak:** `WP-CT-E4-LIVE-tutukon-almiba.md` bulguları #3, #4, #5, #6, #8 + #7'nin "ham durum kodu" kısmı. **Kullanıcı kararı (2026-09-27):** #1 (omurga türetme) ve #2 (ALMIBA `addresses` yönü) **olduğu gibi kalır — bu WP'de YOK.** Genel L10n/TR karakter düzeltmesi ayrı WP (`WP-CT-L10N-FULL`, bu WP'den SONRA).

## Bulgular → düzeltmeler
| # | Bulgu (canlı) | Kök neden (CT teyit) | Düzeltme |
|---|---|---|---|
| 3 | Liste "draft **vv1**", ipucu "**vv2 · vv1 yayında**" | `concept-slim.js:610` `versionLabel = v => \`v${v}\`` + resx `PublishedSiblingHint` "v{0} …" (635'te ham `chainVersion` basılıyor) | `versionLabel`: değer zaten `v`/`V` ile başlıyorsa olduğu gibi, değilse `v` ekle. İpucuna `{0}` olarak **`v`'si soyulmuş** sürüm geçir (resx "v{0}" kalır) → "v1 yayında". Liste arama/sıralama metni (708) aynı etiketi kullanır. |
| 4 | Sürümler sekmesi "Son değiştiren: c5769c62-…" (ham GUID) | `template-form.js:610-617` `t.updatedBy \|\| t.createdBy` doğrudan basılıyor | **Asla GUID gösterme.** Önce mevcut desen ara (CRM'de aktör id→ad çözen yardımcı/uç var mı). Yoksa: `KnowledgeConceptsController`'a salt-okuma passthrough `GET api/users/{id:guid}` → AuthService `/api/users/{id}` (VisitPlanningController.cs:199 deseni, `ReadPermission`), JS'te id başına tek istek + bellek önbelleği, ad = `firstName lastName` (yoksa email). **Herhangi bir hata/403/boş → satır gizlenir.** |
| 5 | "+ Tip ekle" ile adım ekledikten sonra odak `body`'ye düşüyor | `template-form.js:895-912` add-step → `renderBranches()` DOM'u yeniden çiziyor | `renderBranches()` sonrası aynı dalın `.js-branch-type-picker[data-b=bi]` öğesine `focus()` (compose açık kalıyor — `openCompose`). Tip seçici select2 ise select2 odağı. |
| 6 | Yayınlanmış şablonda Ad/Açıklama/Sürüm/tarih/durum alanları düzenlenebilir görünüyor | `setIdentityDisabled` (737) yalnız Moderatör + Kimin İçin'i kilitliyor | Kilit listesine `tplChainName`, `tplDescription`, `tplChainVersion`, `tplStatus`, `tplEffectiveFrom`, `tplEffectiveTo` ekle (select2 olanlar `change.select2`). `startNewVersion` (747) zaten `setIdentityDisabled(false)` çağırıyor → yeni sürümde açılır. Konu/Zincir Kodu mevcut kuralı (edit'te kilitli, yeni sürümde kod açılır) DEĞİŞMEZ. |
| 8 | Önizleme tipleri kodlu ("CT-010 — Bileşen / Etki"), editör kodsuz | `concept-slim.js:86` `labelType` = `typeMap` (kod — ad); 777'de `typeNameMap` (yalnız ad) zaten var | **Yalnız şablon önizlemesinde** (omurga `pv-tpl-sequence` + dal adımları) `typeNameMap[id] \|\| labelType(id)` kullan. Tip/ilişki/düğüm önizlemeleri ve tablolar DEĞİŞMEZ. |
| 7a | Durum kodları her dilde ham ("draft", "published", "archived") | liste `statusVersionCell`, önizleme `pv-tpl-status`, Sürümler sekmesi rozeti ham `status` basıyor | 3 yeni anahtar **7 dilde**: `ChainStatusDraft` (Taslak / Draft / Brouillon / Borrador / 草稿 / مسودة / Черновик), `ChainStatusPublished` (Yayında / Published / Publié / Publicado / 已发布 / منشور / Опубликован), `ChainStatusArchived` (Arşivlendi / Archived / Archivé / Archivado / 已归档 / مؤرشف / В архиве) + iki köprüye (`_IndexL10n`, `_TemplateFormL10n`). Görünen metin çevrilir; **değer/filtre/sıralama anahtarı ham kod kalır** (backend sözleşmesi). Bilinmeyen kod → ham gösterilir. `tplStatus` select'i contract'tan geliyorsa option **text**'ini çevir, `value` DOKUNMA. |

## KORU / YAPMA
- **Backend servisleri (CrmService/AuthService), veri modeli, submit payload, `spineFromBranches` (karar: kalsın), conformance/diagnostics, D2 yön kuralı DEĞİŞMEZ.** Controller'a yalnız #4 için tek salt-okuma passthrough eklenebilir (yazma yok).
- Diyagram/drag (FE-3/4/REFINE), sağ sekme mantığı (FE-5), yayın diyaloğu/guard'lar (FE-6), 2 sütun düzen (FE-8), önizleme uyumsuz sayısı (FE-9) davranışı aynı.
- Yeni resx anahtarı **yalnız** `ChainStatusDraft/Published/Archived` (7 dil). Diğer İngilizce kalan/TR karaktersiz metinler bu WP'de YOK (→ WP-CT-L10N-FULL).
- `pv-tpl-*`, `tpl*`, `js-*` id/class'ları KORU.
- **Tarayıcı doğrulaması:** oturum açık sekmelere mock/harness enjekte etme, ayrı sekme; canlı veride **yazma YOK** — mevcut kayıtlarla doğrula: TPL-TUTUKON-01 v1 published (`ff42ec4a-9ad0-47c0-bfaa-5edfda971147`), v2 draft (`221def9e-96ef-48a1-9191-f77955343848`), TPL-ALMIBA-01 v1 draft (`4f7d4ffb-deb3-4faf-bbf3-bee5c5c0b0f9`). #5 için ekleme yapılır ama **kaydedilmez** (sayfadan kaydetmeden çık).
- **DUR:** #4 için AuthService `/api/users/{id}` CRM kullanıcısına 403 dönüyorsa → gizleme yolu yeterli, raporla (izin/seed ekleme YOK). `tplStatus` değerleri contract'tan geliyor ve text çevirisi select2'yi bozuyorsa → yalnız rozet/liste çevirisini yap, raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → **229/0** (yeni test eklenirse sayı artar, 0 kırmızı). git diff: `concept-slim.js`, `template-form.js`, 7 resx (+3 anahtar), 2 köprü, (ops.) `KnowledgeConceptsController.cs` (+1 GET). Backend servis diff YOK.
- **E4 (fleet restart — resx):** liste "Taslak · v2 · v1 yayında" (vv yok); önizleme omurga/dallar kodsuz tip adı, durum "Yayında"; Sürümler sekmesi kullanıcı adı ya da satır yok (GUID asla); v1 published edit → Ad/Açıklama/Sürüm/Durum/tarihler kilitli, "Yeni sürüm oluştur" sonrası açık (KAYDETME); "+ Tip ekle" ile ekledikten sonra odak tip seçicide.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-10 · Canlı E4 bulgularından küçük düzeltmeler (vv1 · GUID · odak · yayında kilit · kodsuz tip · durum etiketi) (MOD-0162, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Kaynak: WP-CT-E4-LIVE bulguları #3 #4 #5 #6 #8 + #7a (ham durum kodu). Kullanıcı kararı: #1 omurga türetme ve #2 ALMIBA addresses yönü OLDUĞU GİBİ KALIR — dokunma. Genel L10n/TR karakter ayrı WP (sonra).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-10-live-e4-fixes.md (tablo: bulgu → kök neden → düzeltme) · execution/domains/commercial-suite/work-packs/WP-CT-E4-LIVE-tutukon-almiba.md (bulgular) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js (versionLabel 610, sibling hint 635, liste 708, labelType 86, typeNameMap 777, fillPreview şablon dalı) · template-form.js (renderVersions 600-620, setIdentityDisabled 737, startNewVersion 746, add-step 895-912) · Controllers/CRM/VisitPlanningController.cs:199 (users passthrough deseni) · _IndexL10n/_TemplateFormL10n + resx (7 dil).

NE:
 #3) versionLabel: zaten v/V ile başlıyorsa olduğu gibi, değilse v ekle; PublishedSiblingHint {0}'a v'si soyulmuş sürüm geçir → "v1 yayında" (vv yok).
 #4) Sürümler sekmesinde GUID ASLA gösterme: önce mevcut id→ad deseni ara; yoksa KnowledgeConceptsController'a salt-okuma GET api/users/{id:guid} → AuthService /api/users/{id} (VisitPlanning deseni, ReadPermission); JS'te id başına tek istek + önbellek; ad = firstName lastName (yoksa email); hata/403/boş → satır gizli.
 #5) add-step sonrası renderBranches() ardından aynı dalın .js-branch-type-picker[data-b=bi] öğesine focus (select2 ise select2 odağı).
 #6) setIdentityDisabled kilit listesine tplChainName, tplDescription, tplChainVersion, tplStatus, tplEffectiveFrom, tplEffectiveTo ekle; startNewVersion açar (mevcut). Konu/Zincir Kodu kuralı DEĞİŞMEZ.
 #8) Yalnız şablon önizlemesinde (pv-tpl-sequence + dal adımları) tip adı kodsuz: typeNameMap[id] || labelType(id). Diğer önizleme/tablolar DEĞİŞMEZ.
 #7a) 3 yeni anahtar 7 dilde: ChainStatusDraft (Taslak/Draft/Brouillon/Borrador/草稿/مسودة/Черновик), ChainStatusPublished (Yayında/Published/Publié/Publicado/已发布/منشور/Опубликован), ChainStatusArchived (Arşivlendi/Archived/Archivé/Archivado/已归档/مؤرشف/В архиве) + iki köprü; liste statusVersionCell, önizleme pv-tpl-status, Sürümler rozeti çevrilmiş metni gösterir; değer/filtre/sıralama ham kod kalır; bilinmeyen kod ham; tplStatus option text çevrilir, value DOKUNMA.
KORU/YAPMA: backend servisleri/veri modeli/submit/spineFromBranches/conformance/D2 DEĞİŞMEZ; controller'a yalnız #4 için tek salt-okuma GET; FE-3/4/5/6/8/9 davranışı aynı; yeni resx anahtarı yalnız ChainStatus* (diğer L10n YOK); tpl*/pv-tpl-*/js-* id KORU. Tarayıcı: oturum açık sekmelere mock/harness enjekte etme, ayrı sekme; canlı veride YAZMA YOK — mevcut kayıtlarla doğrula (TUTUKON v1 published ff42ec4a-9ad0-47c0-bfaa-5edfda971147, v2 draft 221def9e-96ef-48a1-9191-f77955343848, ALMIBA v1 draft 4f7d4ffb-deb3-4faf-bbf3-bee5c5c0b0f9); #5 testinde eklenen adımı KAYDETME.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 229/0 (yeni test eklenirse 0 kırmızı); git diff concept-slim.js + template-form.js + 7 resx (+3) + 2 köprü (+ ops. KnowledgeConceptsController.cs); backend servis diff yok. E4 (fleet restart sonrası, yazmasız): liste "vv" yok; önizleme kodsuz tip + çevrilmiş durum; Sürümler sekmesi ad ya da satır yok (GUID yok); v1 published edit'te Ad/Açıklama/Sürüm/Durum/tarih kilitli; "+ Tip ekle" sonrası odak seçicide. Ayrı commit ("fix(crm): WP-CT-FE-10 — chain template live E4 fixes (version label, actor name, focus, read-only fields, type names, status labels) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: AuthService /api/users/{id} CRM kullanıcısına 403 dönerse gizleme yeterli, raporla (izin/seed ekleme YOK); tplStatus text çevirisi select2'yi bozarsa yalnız rozet/liste çevir, raporla.
```

## §37 CT bağımsız doğrulama (2026-09-27) → **ACCEPTED (E2 + canlı yazmasız E4)**
```
Commit: bd35e7a3 · Agent: PASS (229/0, canlı 6/6 yazmasız) · CT: ACCEPTED · izole worktree /c/tmp/ct-fe10 @bd35e7a3 → Diten.Web.Tests 229/0 · node --check temiz
```
- ✅ **Kapsam (12 dosya, +98/−11):** concept-slim.js · template-form.js · 7 resx (+3 `ChainStatus*`, değerler WP tablosuyla birebir) · 2 köprü · KnowledgeConceptsController (+1 GET). Backend servis diff YOK.
- ✅ **#3:** `versionLabel` baştaki v/V'yi korur; ipucuna `versionBare` → "v1 yayında".
- ✅ **#4 (kod okundu, WP'den sıkı):** `GET api/users/{userId:guid}` — `RequireJson(ReadPermission)`; AuthService `/api/users/{id}` çağıranın token'ıyla (auth.users.read orada); dışarıya **yalnız `{ displayName }`** (Ad Soyad / e-posta), hata/403/boş → 404 → satır gizli. JS: id başına tek istek + Map önbellek; çözülene kadar/çözülemezse satır yok — **GUID hiçbir yolda basılmıyor**; çözülünce tek `renderVersions` (önbellekten, döngü yok).
- ✅ **#5:** add-step sonrası aynı dalın picker'ına odak (select2 ise `select2('focus')`).
- ✅ **#6:** kilit listesi +6 alan; `startNewVersion` → `setIdentityDisabled(false)`. Submit `val()` ile okuduğu için disabled alanlar payload'ı etkilemez.
- ✅ **#8:** yalnız şablon önizlemesinde `typeNameMap[id] || labelType(id)`.
- ✅ **#7a:** görünen metin çevrili; value/filtre/sıralama ham kod; bilinmeyen kod ham; `tplStatus` option text çevrili, value aynı.
- ✅ **Yazma yok (DB salt-okuma):** 3 şablon, dal adım sayıları [3,2] / [3,2] / [4,3] — E4-LIVE ile aynı.

**WP-CT-FE-10 KOMPLE.** Sırada: WP-CT-L10N-FULL (bu commit üstüne) → push/PR.
