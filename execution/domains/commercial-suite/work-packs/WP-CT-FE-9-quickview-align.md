# WORK PACKAGE — WP-CT-FE-9 · Zincir Şablonu hızlı önizlemesini editörle hizala (DAL-X · ×min–max çip · uyumsuz sayısı) (frontend)

> **CT (SoR).** MOD-0162 Chain Template v2. Branch `feature/crm-chain-template` (FE-8 `f6a9243c` / §37 `c98dd7f0` üstü). **Kullanıcı isteği (2026-09-27):** listedeki göz ikonuyla açılan **hızlı önizleme** (offcanvas) editörle aynı dili konuşsun. Önizleme v2 veriyi zaten doğru okuyor; fark yalnız **sunum**: dal başlığı DAL-X değil, min–max biçimi editör çipinden farklı, uyumsuz ilişki sayısı hiç yok. **Frontend; yeni resx anahtarı YOK; backend DEĞİŞMEZ.**

## Kanıt
- **Önizleme şablonu:** `Views/CRM/KnowledgeConcepts/_TemplateDetailsQuickView.cshtml` — bölümler: Kimlik · `OrderedConceptTypes` (`pv-tpl-sequence` + `pv-tpl-frozen`, satır 78-81) · `Branches` (`pv-tpl-branches`, 84-87) · Açıklama · Referans.
- **Önizleme dolumu:** `concept-slim.js` `fillPreview` son `else` dalı (~1146-1188). Dallar (~1171-1183): başlık = `b.branchName || b.branchCode`; adım = `tip (min–max)` (`minSelection ?? 1`, `maxSelection == null ? '∞'`). `fillPreview` **senkron** (liste satırından).
- **Editör referansı** (`template-form.js`):
  - `chipLabel` (173-178): `×N` (min==max) / `×min–max` (∞ sınırsız).
  - `branchLetter` (223): `bi<26 ? A..Z : bi+1`; lane başlığı (332-334): `[BranchLabelPrefix harf]` badge + ad + `StepCountLabel` "{0} adım".
  - Uyumsuz: `POST ${base}/concept-chain-templates/conformance-diagnostics` body `{subjectId, orderedConceptTypeIds}` (452-455) → `items[]` (`result !== 'conforming'` = uyumsuz); çözülmemiş = uyumsuz − `row.ignoredNonConformingRelationshipIds` (437-438, 845).
- **Proxy:** `KnowledgeConceptsController.cs:281` diagnostics = **ReadPermission** (önizlemeyi gören okuyabilir). `concept-slim.js` `base`/`envelope`/`jsonHeaders` hazır (23/46/28).
- **L10n:** gereken anahtarlar resx'te 7 dilde **zaten var** (`BranchLabelPrefix`, `StepCountLabel`, `UnresolvedCount` "{0} çözülmemiş", `NonConformingEmpty`, `TabNonConforming`, `Spine`, `Loading`, `ErrorState`). `_IndexL10n.cshtml` köprüsünde `Spine/Loading/ErrorState` var; **`BranchLabelPrefix/StepCountLabel/UnresolvedCount/NonConformingEmpty/TabNonConforming` eksik → köprüye eklenecek** (yalnız dizi; resx değişmez).

## NE
1. **Dal başlığı (DAL-X):** `pv-tpl-branches` her dal kartı başlığı = editörle aynı: `<badge bg-label-primary text-uppercase>{BranchLabelPrefix} {harf}</badge>` + ad (`branchName || branchCode`, boşsa yalnız badge) + sağda muted `StepCountLabel` "{N} adım". Harf kuralı editörle aynı (`<26` harf, sonrası sayı) — `concept-slim.js` içinde küçük yerel yardımcı (template-form.js'i import ETME; aynı kural, kopya kabul).
2. **Min–max çipi:** adım satırı `tip (1–∞)` → editör çipi `×N` / `×min–max` (∞ sınırsız), `badge bg-label-secondary` küçük. `minSelection ?? 1` / `maxSelection ?? null` normalizasyonu editörle aynı.
3. **Omurga başlığı:** `OrderedConceptTypes` bölüm başlığı → `Spine` (v2 terimi, listede de "Omurga"). `pv-tpl-sequence` / `pv-tpl-frozen` id'leri KORU.
4. **Uyumsuz ilişki bölümü (yeni, Dallar'ın altında):** `<section>` başlık `TabNonConforming`, gövde `pv-tpl-nonconforming`.
   - Önizleme açılınca **async** diagnostics çağrısı: `{subjectId: row.subjectId, orderedConceptTypeIds: row.orderedConceptTypes}`.
   - Sonuç: çözülmemiş > 0 → `badge bg-label-warning` "`UnresolvedCount`" ; 0 → `badge bg-label-success` "`NonConformingEmpty`". (İsteğe bağlı küçük muted: toplam ilişki sayısı — yeni anahtar gerektirirse YAPMA.)
   - Yüklenirken `Loading`; hata → muted `ErrorState` (önizleme bozulmaz).
   - **Yarış koruması:** kullanıcı başka satıra geçerse eski yanıt yazılmaz (istek sırası/rowId karşılaştırması).
   - subjectId yok veya omurga < 2 tip → bölüm "—" (çağrı YAPMA).
   - **Salt-okunur:** Yok say / Geri al YOK (düzenleme editörde). Resolutions PUT çağrısı YOK.
5. **Köprü:** `_IndexL10n.cshtml` dizisine `BranchLabelPrefix`, `StepCountLabel`, `UnresolvedCount`, `NonConformingEmpty`, `TabNonConforming` ekle (resx'te 7 dilde mevcut — doğrula).

## KORU / YAPMA
- **Backend, controller, resx (7 dil), `template-form.js`, `_TemplateForm.cshtml`, `_TemplateFormL10n.cshtml` DEĞİŞMEZ.** Yeni resx anahtarı YOK (gerekirse DUR).
- Önizlemenin diğer bölümleri (Kimlik, Açıklama, Referans), diğer `data-preview-kind` önizlemeleri (types/relationships/nodes), liste/tablo DOKUNMA. Mevcut `pv-tpl-*` id'leri KORU.
- Diagnostics yalnız **okuma** (POST ama yan etkisiz — BE-A read). Resolutions/publish/save çağrısı YOK.
- Önizleme senkron dolumu bozulmaz: diagnostics hatası/yavaşlığı diğer alanları geciktirmez.
- **Tarayıcı doğrulaması:** oturum açık sekmeye mock/harness enjekte etme; ayrı sekme aç; kullanıcı onayı olmadan kalıcı veri yazma (kaydet/yayınla/yok say) YOK. Tenant'ta şablon yoksa canlı önizleme kontrolünü raporla, veri oluşturma.
- **DUR:** gereken anahtarlardan biri resx'te 7 dilde yoksa → yeni anahtar ekleme, raporla; diagnostics proxy önizleme kullanıcısında 403 dönüyorsa → bölümü gizle + raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → **229/0**. git diff: yalnız `_TemplateDetailsQuickView.cshtml`, `concept-slim.js`, `_IndexL10n.cshtml`. resx/backend/template-form.js diff YOK.
- **E4 (tarayıcı; resx değişmediği için fleet restart gerekmez):** listede bir şablonun göz ikonu → dallar "DAL A · ad · N adım", adımlar `×1` / `×1–3`, bölüm "Omurga", "Uyumsuz" bölümü çözülmemiş sayısını (veya "yok") gösterir; editördeki sayıyla aynı. Başka satıra hızlı geçişte yanlış sayı görünmez.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-9 · Zincir Şablonu hızlı önizlemesini editörle hizala (DAL-X · ×min–max çip · uyumsuz sayısı) (MOD-0162, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Kullanıcı isteği: listedeki göz ikonuyla açılan hızlı önizleme editörle aynı dili konuşsun. v2 veri zaten doğru okunuyor; fark yalnız sunum. Yeni resx anahtarı YOK; backend DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-9-quickview-align.md · frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplateDetailsQuickView.cshtml (78-87) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js (fillPreview şablon dalı ~1146-1188; base 23, envelope 46, jsonHeaders 28) · template-form.js REFERANS (chipLabel 173-178, branchLetter 223, lane başlığı 332-334, diagnostics çağrısı 452-455, unresolved 437-438) · _IndexL10n.cshtml · KnowledgeConceptsIndex.*.resx (anahtar varlığını doğrula).

NE:
 1) Dal başlığı DAL-X: badge "{BranchLabelPrefix} {harf}" (<26 harf, sonrası sayı; concept-slim.js içinde yerel yardımcı, template-form.js import ETME) + ad (branchName||branchCode) + sağda muted StepCountLabel "{N} adım".
 2) Adım satırı "tip (1–∞)" → editör çipi ×N / ×min–max (∞ sınırsız), badge bg-label-secondary; minSelection??1 / maxSelection??null.
 3) Bölüm başlığı OrderedConceptTypes → Spine; pv-tpl-sequence/pv-tpl-frozen id KORU.
 4) Yeni bölüm (Dallar altında): başlık TabNonConforming, gövde pv-tpl-nonconforming. Önizleme açılınca async POST ${base}/concept-chain-templates/conformance-diagnostics {subjectId,orderedConceptTypeIds:row.orderedConceptTypes}; çözülmemiş = result!=='conforming' − row.ignoredNonConformingRelationshipIds; >0 → warning badge UnresolvedCount, 0 → success badge NonConformingEmpty; yüklenirken Loading, hata → muted ErrorState; başka satıra geçişte eski yanıt yazılmaz (istek sırası/rowId); subjectId yok veya omurga <2 → "—" ve çağrı yok. Salt-okunur: Yok say/Geri al/resolutions PUT YOK.
 5) _IndexL10n.cshtml dizisine BranchLabelPrefix, StepCountLabel, UnresolvedCount, NonConformingEmpty, TabNonConforming ekle (resx'te 7 dilde var — doğrula).
KORU/YAPMA: backend/controller/resx/template-form.js/_TemplateForm.cshtml/_TemplateFormL10n.cshtml DEĞİŞMEZ; yeni resx anahtarı YOK; önizlemenin diğer bölümleri ve diğer data-preview-kind önizlemeleri, liste DOKUNMA; mevcut pv-tpl-* id KORU; diagnostics yalnız okuma; senkron dolum diagnostics'i beklemez. Tarayıcı doğrulaması: oturum açık sekmeye mock/harness enjekte etme, ayrı sekme aç; onay olmadan kalıcı veri yazma (kaydet/yayınla/yok say) YOK; tenant'ta şablon yoksa canlı kontrolü raporla, veri oluşturma.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 229/0; git diff yalnız _TemplateDetailsQuickView.cshtml + concept-slim.js + _IndexL10n.cshtml; resx/backend/template-form.js diff yok. Ayrı commit ("feat(crm): WP-CT-FE-9 — chain template quick view aligned with editor (branch letters, chips, non-conforming count) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: gereken anahtar resx'te 7 dilde yoksa yeni anahtar ekleme, raporla; diagnostics proxy önizleme kullanıcısında 403 dönerse bölümü gizle + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-27) → **ACCEPTED (E2)**
```
Commit: 2b8a62a2 · Agent: PASS (229/0, ayrı sekmede harness — gerçek concept-slim.js + gerçek markup) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe9 @2b8a62a2 → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (3 dosya, +66/−8):** `_TemplateDetailsQuickView.cshtml` · `concept-slim.js` · `_IndexL10n.cshtml` (+5 anahtar). resx/backend/template-form.js diff YOK. `node --check` temiz.
- ✅ **DAL-X + çip (kod okundu):** yerel `pvBranchLetter` (<26 harf / sayı) ve `pvChipLabel` (`×N` / `×min–max`, ∞) editör kuralıyla aynı; import yok. Başlık badge + ad (`branchName||branchCode`) + `StepCountLabel`. Bölüm başlığı `Spine`; `pv-tpl-sequence/frozen` id korundu.
- ✅ **Uyumsuz sayısı:** yalnız okuma POST `conformance-diagnostics` `{subjectId, orderedConceptTypeIds: row.orderedConceptTypes}`; çözülmemiş = `result !== 'conforming'` − `ignoredNonConformingRelationshipIds` (editör `unresolved()` ile aynı). Senkron dolum beklemiyor (`void`).
- ✅ **Yarış koruması:** `pvDiagSeq` + `previewRef.id` karşılaştırması (previewRef aynı senkron turda set ediliyor → await sonrası geçerli). 403 → bölüm gizli (envelope `status` taşıyor), diğer hata → muted `ErrorState`; subject yok / omurga <2 → "—", istek yok.
- ✅ **Süreç kuralı uygulandı:** harness ayrı sekmede; kullanıcının oturumlu sekmesine dokunulmadı; veri yazılmadı.
- ➕ **Agent'ın ek düzeltmesi (kabul):** numaralı liste flex'te sayaç öğe gibi davranıyordu → `justify-content-between` kaldırıldı, ada `me-auto`.
- ⚠ **Canlı doğrulanmadı:** tenant'ta şablon yok → kullanıcı canlı E4'ünde (TUTUKON şablonu kaydedilince) kapanacak.

**WP-CT-FE-9 KOMPLE (E2).** Chain Template v2 tüm WP'ler CT-E2. Kalan: canlı E4 + push + PR.
