# WORK PACKAGE — WP-SEG-A2 · Segment Create/Edit iskelet mockup'a hizalama (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (SEG-A `4a3c80ac` üstü). **Yalnız frontend (Diten.Web)**. SEG-A blok editörü/payload/catalog/canlı-reach DOĞRU; bu WP **sayfa iskeletini owner mockup'ına** hizalar (SEG-A "mevcut section yapısını KORU" dediği için iskelet eski kaldı). **Yeni CSS yok** (numaralı badge + pill/radio mevcut class).

## Fark (mockup hedef ↔ mevcut impl) — 7 madde
1. **Numaralı iş-dili section başlıkları**: "1 What is this segment? / 2 Who belongs in it? / 3 Exceptions & validity" — uppercase IDENTITY/CRITERIA/CLASSIFICATION/LIFECYCLE yerine. **Standart kart chrome (section.card) KALIR**, yalnız başlık = numara-badge (badge bg-primary rounded-pill) + iş-dili metin + alt açıklama.
2. **Subject = pill toggle** ("Doctors/contacts | Institutions/accounts") **section 1'de** (mevcut: sağ ray CLASSIFICATION'da raw select2 → section 1'e taşı, pill toggle; create-sonrası immutable notu; SubjectType hidden input korunur).
3. **Membership = radio kart** ("By a rule — stays up to date | By a fixed list — you pick people") **section 1'de** — bu SegmentType'ı belirler (rule=dynamic, fixed-list=static). Mevcut: sağ rayda dropdown → section 1 radio kart.
4. **Exceptions & validity** (always-include / never-include + usable-from/until) → **section 3** (mevcut sağ ray LIFECYCLE kartından taşı).
5. **Sağ ray = SADECE reach paneli** (WHO THIS REACHES NOW + funnel + sample + activation checklist + "üyelik saklanmaz"). CLASSIFICATION + LIFECYCLE kartları rail'den kalkar (içerikleri section 1 ve 3'e taşındı).
6. **maxDepth/subjectId gizle**: koşul satırında opsiyonel-param bare input'ları render ETME (mockup göstermiyor). Attribute gerçekten gerektiriyorsa bile Phase-1'de gizli (concept.affinity maxDepth vb. niş; ileride advanced).
7. **Blok başlığı "every|any" toggle**: "Include people where [every|any] condition below is true" segmented toggle (mevcut "match [ANY of these]" dropdown yerine). GroupOperator eşleme aynı (every=all/any=or).

## KORUNACAK (SEG-A doğru, DOKUNMA)
Blok→tree payload (buildNodes, MatchMode=all + group/predicate, none-of=not-in); catalog-driven optgroup (Domain) + operatör/değer; value chip + source badge + free-text; canlı reach (SEG-C /preview debounced); read-back + "N match this alone"; show-stored-rule; empty-state şablonlar; static→manuel liste; same-origin proxy.

## YAPMA
Yeni CSS/vendor; backend/DTO/payload; catalog-güdümlülüğü boz; blok editörü mantığını/payload'ı değiştir; reach'i client-side uydur; başka modül. (İskelet yeniden düzeni + subject/membership/exceptions taşıma + maxDepth gizle + every/any toggle DIŞINDA bir şey değiştirme.)

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil. Numaralı section'lar; subject pill toggle + membership radio section 1'de; exceptions section 3'te; sağ ray sadece reach; maxDepth/subjectId görünmez; blok "every|any" toggle. Payload + catalog-driven + canlı reach + static→manuel KORUNDU. git diff yeni CSS yok. Create+Edit.
- **E4:** mockup'a görsel eşleşme (ALMIBA nefrolog canlı).

---

## §36.1 Agent Prompt (paste-ready)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-A2 · Segment Create/Edit iskelet mockup'a hizalama (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (SEG-A 4a3c80ac üstü)> · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SEG-A2-frontend-mockup-layout.md (bu WP — 7 fark)
2. frontend/Diten.Web/Views/CRM/Segments/_Form.cshtml (MEVCUT iskelet: IDENTITY/criteria/manual + sağ ray reach/CLASSIFICATION/LIFECYCLE) + wwwroot/assets/js/CRM/Segments/form.js (blok editörü — payload/catalog/reach KORU)
3. Numaralı section + radio-kart deseni referansı: frontend/Diten.Web/Views (Tasks/ChecklistTemplates veya mevcut numaralı adım kartı) — mevcut class

NE (yalnız _Form.cshtml + form.js + L10n; iskelet yeniden düzeni):
 1) Section başlıkları numaralı iş-dili: "1 What is this segment? / 2 Who belongs in it? / 3 Exceptions & validity" (numara badge bg-primary rounded-pill + metin). Standart section.card chrome KALIR.
 2) Subject → section 1'de pill toggle (Doctors/contacts | Institutions/accounts); create-sonrası immutable; SubjectType hidden input korunur. Sağ ray CLASSIFICATION'dan çıkar.
 3) Membership → section 1'de radio kart (By a rule=dynamic | By a fixed list=static); SegmentType'ı belirler. Sağ ray dropdown'dan çıkar.
 4) Exceptions & validity (always/never include + usable from/until) → section 3 (sağ ray LIFECYCLE'dan taşı).
 5) Sağ ray = SADECE reach paneli (WHO THIS REACHES NOW + funnel + sample + activation + "üyelik saklanmaz"). CLASSIFICATION+LIFECYCLE kartları rail'den KALDIR.
 6) maxDepth/subjectId: koşul satırında bare input render ETME (gizle).
 7) Blok başlığı "every|any" segmented toggle (dropdown yerine); GroupOperator eşleme aynı.
KORU (DOKUNMA): buildNodes blok→tree payload (MatchMode=all+group/predicate, none-of=not-in); catalog optgroup(Domain)/operatör/değer; chip+source badge+free-text; canlı reach (SEG-C /preview); read-back+"N match this alone"; show-stored-rule; empty-state; static→manuel; same-origin proxy.
NASIL: SADECE mevcut class (section.card/card-body p-4/badge/rounded-pill/btn-check pill toggle/form-check radio-card/form-*-sm). Yeni CSS YOK. Create+Edit paylaşılan _Form.
YAPMA: yeni CSS/vendor; backend/DTO/payload; catalog-güdümlülük; blok mantığı/payload; reach client-side; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil; numaralı section + subject pill + membership radio section1 + exceptions section3 + rail sadece reach + maxDepth/subjectId gizli + every/any toggle; payload+catalog+reach+static→manuel korundu; git diff yeni CSS yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: numaralı section/pill/radio mevcut class'la olmuyorsa (yeni CSS gerekiyorsa DUR+raporla); blok payload/catalog/reach korunamıyorsa; kapsam Segments dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 134afdfe · Agent: PASS (137/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-sega2-verify @134afdfe
```
- ✅ **Scope:** 10 dosya, hepsi frontend (_Form.cshtml + form.js + _IndexL10n + 7 resx). 0 yeni .css, 0 backend sızıntı.
- ✅ **7 fix:** numaralı badge (1/2/3 bg-primary rounded-pill) + iş-dili section başlıkları; subject pill (btn-check name=SubjectType + hidden asp-for, create-immutable) section 1'de; membership radio (name=SegmentType, kontrat-güdümlü dynamic/static/hybrid — hardcode değil, hybrid düşmesin) section 1'de; exceptions section 3'te; sağ ray SADECE reach (CLASSIFICATION+LIFECYCLE kaldırıldı, MatchMode hidden section 1'de); maxDepth/subjectId gizli (parameterFields required-only; opsiyoneller seeded '' → **payload byte-identical**); every/any segmented toggle (btn-check).
- ✅ **KORU (dokunulmadı):** buildNodes blok→tree payload (diff'te yok); catalog optgroup/operatör/değer; chip+source badge+free-text; canlı reach (SEG-C /preview); read-back+"N match this alone"; show-stored-rule; empty-state; static→manuel; same-origin proxy. currentSubjectType/SegmentType artık radio'dan okuyor.
- ✅ **L10n:** 7 resx 211 data, dup=0, tutarlı (18 yeni anahtar + BlockToggleEvery/Any köprüde).
- ✅ **Build+test (CT izole, Release):** build 0-err; **Diten.Web.Tests 137/137** (baseline-diff temiz).
- ⏳ **E4:** mockup'a görsel eşleşme (fleet + ALMIBA nefrolog canlı).

**SEG REDESIGN KOMPLE — SEG-C (preview) + SEG-B (Domain) + SEG-A (blok editör) + SEG-A2 (iskelet) hepsi CT-E2.**
