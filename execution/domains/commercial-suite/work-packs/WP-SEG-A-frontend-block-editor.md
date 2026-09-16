# WORK PACKAGE — WP-SEG-A · Segment Create/Edit blok-editörü + iş-dili (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio`. **Frontend (Diten.Web)**. Owner: **biz**. Owner mockup'ına göre Create/Edit'i yeniden yaz. **Bağımlı:** SEG-B (katalog Domain, optgroup) + SEG-C (preview endpoint, canlı reach) → önce onlar. **Paketli; dispatch owner'da.**

## Karar (owner)
- **Reach = CANLI** (SEG-C `/segments/preview`, debounced).
- **Optgroup = server-authored** (SEG-B katalog Domain).
- **Ana/dış section kartları + sağ ray kartları = STANDART Tasks/Create card** (`<section class="card"><div class="card-body p-4">` + `text-uppercase text-heading fw-semibold mb-4` başlık — _Form.cshtml'de ZATEN bu yapı var, KORU).
- **İç condition/blok editörü = mockup tasarımı** (blok rail, chip değerler, source badge, read-back). İç editörde mockup görünümü serbest; önce mevcut class (bg-label-*/badge/progress/border), gerekirse **minimal scoped CSS** (yalnız segments condition editörüne özel, dökümante et) — ana kartlarda yeni CSS YOK.

## Ölçülmüş girdi (CT)
- `Views/CRM/Segments/_Form.cshtml`: zaten section.card yapısı — Identity(19) · criteriaSection(48, #criteriaEditor 75) · manualMembershipSection(95) · sağ ray Classification(124)/Lifecycle(164). Bu **kartları KORU**, içeriği yeniden kur.
- `wwwroot/assets/js/CRM/Segments/form.js` (1033 satır): catalog-driven (`/attribute-catalog`), same-origin proxy (`endpoint=/CRM/Segments/api`, getJson credentials same-origin), value-source (reference-set/enum/entity-picker/free-text), segment-type-conditional (dynamic=criteria / static=manual). **Bu omurgayı KORU**, ağaç editörünü blok editörüne çevir.
- Backend payload UNCHANGED: Segment.MatchMode + Criteria[] (SegmentCriteriaNode flat+ParentNodeId, group/predicate, depth≤2).

## Kapsam (frontend) — mockup'ın 8 kararı
1. **Ağaç → blok:** 2 sabit katman — blok içi koşullar (**every/any** toggle), bloklar arası **AND ALSO** (her zaman AND). "Group/parent/move/nesting" kontrolleri YOK. **Blok→tree map:** Segment.MatchMode=All → her blok=Group node(GroupOperator every=all/any=any) → predicate'ler. Payload aynı.
2. **Match mode kullanıcıdan alınmaz** — bloklardan türetilir (MatchMode=All sabit; every/any blok grubunda).
3. **NOT toggle YOK** → operatöre girer: `is any of`=in · `is none of`=not-in · `is at least`=gte (+ mevcut diğerleri). (Negate model'de kalır ama UI not-in kullanır.)
4. **Attribute optgroup** (SEG-B Domain): `<optgroup>` Doctor profile / Consent / Workplace / Commercial / Activity / Institution. Etiketler katalog Domain'inden; attribute/operator/value **katalogdan** (hardcode YOK).
5. **Değer kontrolü tek görsel dil:** chip deseni + yanında **source badge** ("Reference set · MOD-0048", "Catalog list · MOD-0164", "Picker · MOD-0151"); serbest yazım korunur ("or type a value + Enter"); tarih/sayı/bool kendi kontrolü. (reference-set/enum/entity-picker/free-text hepsi mevcut value-source'tan.)
6. **Sürekli read-back:** üstte canlı "Reads as" cümlesi; her koşul altında özet + **"N match this alone"** (SEG-C conditionCounts).
7. **Sağ ray = güven (CANLI, SEG-C):** WHO THIS REACHES NOW toplam + koşul-koşul funnel (bar) + sample members + activation checklist + "üyelik saklanmaz" notu. Debounced `/preview` çağrısı.
8. **Show the stored rule:** tipli predicate tree JSON (escape hatch, varsayılan gizli). **Static** → criteria tamamen gizli (disable değil) + manuel liste editörü.
+ **Empty-state şablonlar** (ör. "Nephrologists we may contact") → tek tıkla çalışan kural üretir.

Ayrıca: subject toggle (Doctors/contacts ↔ Institutions/accounts) — SubjectType (sonradan değişemez uyarısı); membership radio (rule/fixed-list); Exceptions & validity (always/never include + usable from/until).

## YAPMA
- Ana section kartlarında yeni CSS. Mockup'ın **inline-style'ını kopyalama** (o Claude Design artefaktı) — proje class'larıyla kur. Backend/DTO/API/payload DEĞİŞTİR (blok→tree map ile aynı kalır). Katalog-güdümlülüğü bozma (hardcoded attribute/operator/değer YOK). same-origin proxy'yi bırakma. Reach'i client-side uydurma (CANLI, SEG-C). Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil. Blok editörü (every/any + AND ALSO), iş-dili operatörler, optgroup (SEG-B Domain), chip+source badge+free-text, read-back + "N match this alone" (SEG-C), canlı reach rail (SEG-C /preview), static→manuel, show-stored-rule, empty-state. **Ana kartlar standart Tasks/Create**; blok→tree payload doğru (MatchMode=All + block groups + predicates; none-of=not-in). Catalog-driven + same-origin proxy korunur. Create+Edit.
- **E4:** ALMIBA nefrolog örneği canlı oluşturulur; reach canlı sayı verir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (SEG-B + SEG-C sonrası)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-A · Segment Create/Edit blok-editörü + iş-dili (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <SEG-B + SEG-C merge sonrası HEAD> · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SEG-A-frontend-block-editor.md (bu WP — mockup 8 karar)
2. frontend/Diten.Web/Views/CRM/Segments/_Form.cshtml (mevcut section.card yapısı — KORU) + wwwroot/assets/js/CRM/Segments/form.js (catalog-driven + same-origin proxy + value-source + segment-type-conditional omurgası — KORU)
3. Backend: SEG-B katalog Domain (optgroup) + SEG-C POST /segments/preview (canlı reach) — proxy'yi SegmentsController'a ekle (same-origin /CRM/Segments/api/preview → gateway /api/crm/segments/preview)

NE (frontend, _Form.cshtml + form.js + L10n + SegmentsController preview proxy):
 - ANA/dış section kartları + sağ ray = STANDART Tasks/Create card (section.card + card-body p-4 + text-uppercase text-heading fw-semibold) — _Form.cshtml'de mevcut, KORU. İÇ condition/blok editörü = mockup tasarımı.
 - Kriter ağacını BLOK editörüne çevir: 2 katman (blok every/any + bloklar arası AND ALSO). Blok→tree map: MatchMode=All + her blok=Group(every=all/any=any) + predicate'ler (depth≤2). Payload UNCHANGED.
 - Operatörler iş-dili: is any of=in, is none of=not-in, is at least=gte (katalog operatörlerinden).
 - Attribute <optgroup> = katalog Domain (SEG-B); attribute/operator/value katalogdan (hardcode YOK).
 - Değer: chip + source badge (Reference set/Catalog list/Picker · MOD-*) + serbest yazım; tarih/sayı/bool doğal kontrol.
 - Read-back: canlı "Reads as" + koşul özeti + "N match this alone" (SEG-C conditionCounts).
 - Sağ ray CANLI (SEG-C /preview debounced): toplam reach + koşul funnel + sample members + activation checklist + "üyelik saklanmaz".
 - Show the stored rule (tree JSON, gizli); static→criteria gizle + manuel liste editörü; empty-state şablonlar.
NASIL: mevcut form.js omurgası (catalog/proxy/value-source) korunur; ağaç→blok yeniden yazımı. Ana kartlarda yeni CSS YOK; iç editörde önce mevcut class (bg-label-*/badge/progress/border), gerekirse minimal scoped CSS (segments'e özel, dökümante). Mockup inline-style'ı kopyalama.
YAPMA: ana kartta yeni CSS; backend/DTO/payload değiştir; katalog-güdümlülüğü boz; same-origin proxy bırak; reach client-side uydur; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil; blok editörü + iş-dili + optgroup + chip/source/free-text + read-back + canlı reach (SEG-C) + static→manuel + show-stored-rule + empty-state; ana kartlar standart; blok→tree payload doğru (MatchMode=All+groups+predicates, none-of=not-in); catalog-driven + same-origin proxy korundu. Ayrı commit. §22 TÜRKÇE. K13.
Durma: SEG-B Domain / SEG-C preview yoksa (önce onlar) · blok→tree map limiti aşıyorsa · katalog-güdümlülük korunamıyorsa · kapsam Segments dışına taşarsa → DUR+raporla.
```
## §37 CT → (agent sonrası)
- İzole worktree: Diten.Web.Tests baseline-diff; logic-read (blok→tree map MatchMode=All+groups+predicates none-of=not-in; catalog-driven optgroup; canlı reach SEG-C; static→manuel; show-stored-rule); ana kartlar standart Tasks/Create; iç editör mockup; same-origin proxy + payload UNCHANGED.

## Kalan (bu WP dışı)
- A2d ALMIBA manuel test (bu segment akışı dahil) → sync/PR.
