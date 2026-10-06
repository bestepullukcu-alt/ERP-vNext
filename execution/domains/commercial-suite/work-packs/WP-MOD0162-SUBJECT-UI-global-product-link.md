# WORK PACKAGE — WP-MOD0162-SUBJECT-UI · Subject ↔ MDM Global Product bağı (pick veya custom)

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test tasarım geri bildirimi (2. tur). Subject (marka) **MDM Global Product'tan seçilebilmeli** (provenance/master bağı) **ya da custom** girilebilmeli. Module: **MOD-0162** (Subject). Branch: `feature/scmm-content-studio`. **Yalnız frontend** (Diten.Web); backend/API/RBAC'a **DOKUNMA** (hazır).
> **SIRA:** **WP-MOD0162-AUD-UI CT-ACCEPTED olduktan SONRA dispatch** — aynı dosyalara dokunuyor (`Taxonomy.cshtml` + `taxonomy.js`), çakışmayı önle. Expected HEAD = AUD §37 sonrası HEAD.

## KARAR — ne saklanır (KİLİTLİ)
> **Sakla = stabil MDM Global Product Id** (`ExternalRefType="global-product"`, `ExternalRefId=productId`). Bu sefer **id doğru** — MDM master kaydının id'si dayanıklı (ConceptNode da böyle saklıyor); versiyonlu-reference-data'daki "code vs version-id" ikilemi burada YOK (bu master data). Master alanı KOPYALANMAZ (ConceptNode "provenance only" deseni). **SubjectName kimlik/etiket olarak kalır** (pick'te prefill, düzenlenebilir); ExternalRef dayanıklı bağdır. Custom = ExternalRef yok, serbest SubjectName/Code.

## Ölçülmüş girdi (CT)
- **Backend HAZIR (DOKUNMA):** `Subject.ExternalReferences = List<KnowledgeExternalReference>` (Domain). CQRS: `SubjectCreate/UpdateCommand` `ExternalReferences` (`KnowledgeExternalReferenceInput`) taşıyor; handler `ValidateExternalReferences` + `KnowledgeMapper.ToEntities` (full-replace); API `SubjectCreate/UpdateRequest` `ExternalReferences` kabul; DTO döndürüyor. **Backend değişikliği YOK.**
- **Precedent (mirror):** ConceptNode node formu + Knowledge **content** formu **zaten** MDM Global Product picker kullanıyor: `KnowledgeController.LoadGlobalProductOptionsAsync` (~530, `/api/global-products/selector?pageSize=200`) + `EnsureGlobalProductSelectedAsync` (off-page kayıtlı değeri geri yükle) + `ResolveGlobalProductLabelAsync` (~id→ad). Content formu bu ucu `/CRM/Knowledge`'ten tüketiyor (~245 Json options). İzin `mdm.global-products.read`. Graceful degradation: MDM yoksa/izin yoksa disabled+reason.
- **taxonomy.js payload HAZIR:** `writePayload` common'ı `externalReferences: src.externalReferences || []` **zaten gönderiyor** (~727); load da `externalReferences: item.externalReferences || []` okuyor (~60). Şu an sadece **UI yok** (korunuyor ama düzenlenemiyor). `tax-only-subject` bloğu (Taxonomy.cshtml) + `openForm/load` (~694) subject alanlarını sürüyor.

## Kapsam (yalnız frontend)
1. **Subject formu (`tax-only-subject` bloğu) — Global Product bağı:** "Global Product'tan seç" seçeneği:
   - **Pick modu:** single select2, kaynak = KnowledgeController global-product options (mevcut content-form ucunu tüket/aynısını taxonomy için ekle). Seçilince: **SubjectName prefill** (düzenlenebilir bırak) + `src.externalReferences = [{ refType:"global-product", refId: productId }]`. Off-page kayıtlı ürün `EnsureGlobalProductSelected` deseniyle geri yüklenir (id→ad resolve).
   - **Custom modu:** ExternalRef yok; serbest SubjectName/Code (mevcut davranış).
   - Toggle (pick vs custom) net; custom seçilince externalReferences'tan global-product bağı temizlenir.
2. **Load/edit:** mevcut subject'in `externalReferences` içindeki global-product bağı yüklenip picker'da gösterilir (id→ad resolve); custom subject'te picker boş.
3. **Graceful degradation:** MDM erişilemez/izin yok → picker disabled+reason, **custom yine çalışır** (Subject oluşturma bloklanmaz — mevcut ConceptNode/content deseni).
4. **7-dil L10n:** "Global Product'tan seç / Custom / picker placeholder / MDM yok" etiketleri — gerçek çeviri, key-echo yok.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. Subject kimliğini (SubjectCode/SubjectName) global-product'a bağımlı kılma (additive ExternalRef; custom hep mümkün). Master alanı kopyalama (yalnız id+prefill). Versiyon/opaque değil — Global Product **Id** sakla (master, dayanıklı). Global-product yaz (yalnız oku, mdm.global-products.read). Topic/Profile bölümünü veya AUD dimension builder'ı (ayrı WP) bozma — yalnız Subject bölümü. Subject oluşturmayı MDM'e bağlı hard-block etme. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; Subject formunda Global Product picker (pick→SubjectName prefill + externalReferences global-product Id set; custom→ref yok); edit→mevcut bağ yüklenir (id→ad); MDM yoksa picker disabled + custom çalışır; kaydet→`externalReferences:[{refType:"global-product",refId}]` payload; 7-dil key-echo yok; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** ALMIBA subject'ini Global Product'tan seç → SubjectName gelir, ref saklanır; custom bir subject de girilebilir; MDM kapalıyken custom çalışır.
- Kapsam: yalnız Diten.Web (Taxonomy subject bölümü + taxonomy.js + gerekirse KnowledgeController global-product proxy taxonomy için + resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready) — AUD CT-ACCEPTED SONRASI

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-SUBJECT-UI · Prompt v1.0  (Subject ↔ MDM Global Product bağı — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <WP-MOD0162-AUD-UI §37 sonrası HEAD> · Worktree: ana checkout
ÖNCE: WP-MOD0162-AUD-UI merge+CT-ACCEPTED olmalı (aynı Taxonomy.cshtml/taxonomy.js — çakışmayı önle).

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-SUBJECT-UI-global-product-link.md (bu WP — "KARAR: sakla=Global Product Id")
2. MIRROR global-product picker: frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs (LoadGlobalProductOptionsAsync ~530, EnsureGlobalProductSelectedAsync, ResolveGlobalProductLabelAsync ~155; content formu tüketimi) + KnowledgeConceptsController.cs (api/global-product-options ~300, graceful-degradation disabled+reason)
3. frontend/Diten.Web/Views/CRM/Knowledge/Taxonomy.cshtml (tax-only-subject bloğu + taxonomyCanvas) + wwwroot/assets/js/CRM/Knowledge/taxonomy.js (writePayload ~719-739 externalReferences zaten common'da; load ~60; openForm ~694)
4. Backend sözleşme (DOKUNMA, sadece şekil): services/Diten.CrmService/.../Features/Knowledge/Subject/Commands/SubjectCommands.cs (ExternalReferences) + Domain/Entities/ConceptNode.cs (global-product ExternalRef deseni, provenance-only)

NE (yalnız frontend Diten.Web):
 0) KARAR (KİLİTLİ): pick→ SAKLA externalReferences=[{refType:"global-product", refId: GlobalProductId}] (master Id, dayanıklı; version/opaque değil) + SubjectName prefill (düzenlenebilir). Master alanı KOPYALAMA. custom→ ref yok, serbest.
 1) Subject formuna (tax-only-subject) Global Product picker: pick modu = single select2 (KnowledgeController global-product options; off-page kayıtlı değer EnsureGlobalProductSelected deseniyle geri yüklenir) + custom modu (serbest SubjectName/Code, ref yok). Toggle net; custom→ global-product bağını temizle.
 2) Edit/load: mevcut subject externalReferences'taki global-product bağını picker'a yükle (id→ad resolve, ResolveGlobalProductLabel deseni).
 3) Graceful degradation: MDM yok/izin yok → picker disabled+reason, custom yine çalışır (Subject oluşturma bloklanmaz; ConceptNode/content deseni).
 4) 7-dil resx (seç/custom/placeholder/MDM-yok) — gerçek çeviri, key-echo YOK.
NASIL: KnowledgeController global-product picker (content formu) + KnowledgeConcepts graceful-degradation desenini birebir izle. taxonomy.js writePayload externalReferences'ı zaten gönderiyor — sadece src.externalReferences'ı doldur.
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; Subject kimliğini global-product'a bağımlı kıl (additive; custom hep mümkün); master alanı kopyala; Global Product Id yerine version/opaque sakla; global-product yaz (yalnız oku); Topic/Profile/AUD-dimension bölümünü boz; Subject oluşturmayı MDM'e hard-block; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; Subject Global Product picker (pick→prefill+ref Id; custom→ref yok); edit→mevcut bağ yüklenir; MDM yoksa disabled+custom çalışır; kaydet→externalReferences global-product Id payload; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: global-product options ucu taxonomy formundan beslenemezse · externalReferences payload/DTO şekli beklenenden farklıysa · kapsam Subject dışına/frontend dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra ALMIBA retest'inde Subject'i Global Product'tan (ALMIBA) seçerek gir.
- SIRA hatırlatma: bu WP **AUD WP CT-ACCEPTED sonrası** dispatch edilir.
