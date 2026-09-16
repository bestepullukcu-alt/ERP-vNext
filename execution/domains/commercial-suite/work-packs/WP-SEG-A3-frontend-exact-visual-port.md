# WORK PACKAGE — WP-SEG-A3 · Segment Create/Edit **birebir görsel port** (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (SEG-A2 `e2fdd087` üstü). **Yalnız frontend (Diten.Web).** Owner 3 kez yaklaşık tasarımı reddetti: *"tam buradaki tasarımı yapalım yine olmadı **tam tasarımı istiyorum**"* + mockup dosyasını verdi. Kök neden: SEG-A/A2 mockup'ı **bootstrap class'larıyla yaklaşık** kurdu; mockup **%100 inline-style + oklch** — yaklaşıklık ≠ piksel-birebir. **Bu WP için "yeni CSS yok" kuralı GERİ ALINIR:** mockup'ın inline stilleri **segment formuna özel scoped bir stylesheet'e** (`segment-create.css`) distile edilir. **Veri kablolaması (SEG-A/B/C) AYNEN korunur** — "görünüm mockup'tan, davranış SEG-A/B/C'den".

## Referans (agent MUTLAKA okuyacak)
- **`C:\tmp\mockup-segment.html`** (60588 byte) — owner'ın Claude Design export'undan çıkarılmış **temiz markup** (167 inline style, 268 oklch, Inter). **Görsel gerçeğin tek kaynağı bu dosya.** Token tablosunda olmayan her değeri (padding/renk/radius) buradan al.
- Mevcut impl (davranış kaynağı, KORU): `Views/CRM/Segments/_Form.cshtml` + `wwwroot/assets/js/CRM/Segments/form.js` (SEG-A blok editörü + SEG-C canlı reach + SEG-B optgroup + same-origin proxy).

## Yaklaşım
- Yeni dosya **`wwwroot/assets/css/segment-create.css`** — mockup inline stillerini class'lara distile et (prefix `seg-*`). **Yalnız _Form.cshtml'de link'le** (global tema/başka sayfa ETKİLENMEZ). Root'ta `.segment-create-scope { font-family: 'Inter', ... }` sarmalayıcı; tüm `seg-*` bu scope altında (sızıntı yok).
- `_Form.cshtml` markup'ı mockup'ın **section yapısına birebir** getir; `form.js` render fonksiyonları mockup'ın **blok/koşul/chip/reach markup'ını** üretsin — ama tüm `on*` handler / catalog / payload / reach çağrıları **mevcut mantıktan**.
- oklch renkler tarayıcı-destekli (2023+); Inter Google Fonts veya mevcut vendor'dan.

## Token tablosu (mockup'tan ölçülmüş — scoped CSS'e birebir)
| öğe | değer |
|---|---|
| **font** | `Inter` (gövde/label), `ui-monospace, SFMono-Regular, Menlo` (JSON `<pre>`) |
| **layout** | konteyner `max-width:1240px; margin:0 auto`; flex-wrap gap 20px; sol `flex:1 1 620px`; sağ ray `flex:0 1 340px; min-width:260px; position:sticky; top:18px` |
| **card (section)** | bg `oklch(1 0 0)`; border `1px solid oklch(0.93 0.006 285)`; radius 10px; padding `22px 24px` (sağ ray kartları 20px) |
| **section no-badge** | 22×22 daire; bg `oklch(0.95 0.02 285)`; renk `oklch(0.45 0.13 285)`; 12px/600 |
| **section başlık** | 15px/600 `oklch(0.28 0.02 285)`; helper 13px `oklch(0.55 0.015 285)`, `margin-left:32px` |
| **label** | 13px/500 `oklch(0.35 0.02 285)`; zorunlu-yıldız `oklch(0.58 0.2 25)` |
| **input/textarea/select** | padding `10px 12px`; border `1px solid oklch(0.87 0.008 285)`; radius 6px; 14px `oklch(0.3 0.02 285)`; **focus** border `oklch(0.55 0.19 285)` + `box-shadow:0 0 0 3px oklch(0.55 0.19 285/0.14)`; readonly bg `oklch(0.975 0.004 285)` |
| **primary accent** | `oklch(0.55 0.19 285)`; buton hover `oklch(0.49 0.19 285)`; koyu vurgu `oklch(0.45 0.13 285)` |
| **pill (subject) seçili** | bg accent, renk beyaz, radius 999px, `8px 16px` |
| **pill seçili değil** | bg beyaz, border `oklch(0.89 0.008 285)`, renk `oklch(0.42 0.015 285)`; hover border `oklch(0.72 0.1 285)` renk `oklch(0.45 0.13 285)` |
| **membership radio-card seçili** | bg `oklch(0.985 0.012 285)`; border `1.5px oklch(0.55 0.19 285)`; `box-shadow:0 0 0 3px oklch(0.55 0.19 285/0.1)`; ✓ 16px daire accent |
| **radio-card seçili değil** | bg beyaz; border `1.5px oklch(0.91 0.006 285)`; boş 16px daire border `1.5px oklch(0.82 0.01 285)` |
| **Reads as kutusu** | bg `oklch(0.975 0.012 285)`; border `oklch(0.91 0.02 285)`; radius 9px; etiket uppercase 11.5px/600 `oklch(0.52 0.08 285)`; metin 14.5px `oklch(0.3 0.04 285)` |
| **blok** | border `1px oklch(0.92 0.006 285)`; radius 10px; header bg `oklch(0.985 0.003 285)` + alt-border `oklch(0.95 0.005 285)`; gövde padding `12px 14px` |
| **every/any toggle** | track bg `oklch(0.94 0.006 285)` radius 7px pad 2px; aktif btn bg beyaz + `box-shadow:0 1px 2px oklch(0.3 0.02 285/0.1)` 600; pasif transparent 500 `oklch(0.5 0.015 285)` |
| **AND ALSO ayraç** | pill bg `oklch(0.955 0.02 285)` renk `oklch(0.45 0.13 285)` 12px/600; iki yanda 1px çizgi `oklch(0.9-0.94 …)` |
| **koşul satırı** | border `1px oklch(0.94 0.005 285)` radius 9px bg beyaz; lead 12px uppercase `oklch(0.62 0.02 285)` min-width 52px |
| **değer chip seçili** | bg accent renk beyaz + ✓; **seçili değil** bg beyaz border `oklch(0.89 0.008 285)`; free-text input `1px dashed oklch(0.85 0.01 285)` radius 999px, focus solid accent |
| **source badge** | bg `oklch(0.945 0.005 285)` renk `oklch(0.48 0.015 285)` 11.5px radius 999px |
| **reach chip (N match this alone)** | bg `oklch(0.96 0.018 285)` renk `oklch(0.45 0.13 285)` 12px radius 999px |
| **değer kabı (list/date/number/bool)** | bg `oklch(0.982 0.003 285)` border `oklch(0.95 0.005 285)` radius 8px pad `11px 12px` |
| **+ Add condition** | bg `oklch(0.96 0.018 285)` renk `oklch(0.45 0.13 285)` radius 6px; hover bg `oklch(0.92 0.035 285)` |
| **+ Add block** | dashed border `oklch(0.87 0.01 285)` radius 9px, tam-genişlik, sol-hizalı |
| **JSON pre** | ui-monospace 12px; bg `oklch(0.975 0.004 285)` border `oklch(0.93 0.006 285)` radius 8px |
| **reach big number** | 34px/700 `letter-spacing:-0.02em` `oklch(0.32 0.04 285)` |
| **funnel bar** | track `oklch(0.945 0.005 285)` h5px radius999; fill `oklch(0.6 0.15 285)` width=% |
| **sample avatar** | 30px daire bg `oklch(0.955 0.02 285)` renk `oklch(0.45 0.13 285)` initials |
| **checklist ok** | 16px daire bg `oklch(0.72 0.14 150)` beyaz ✓; pending boş daire border `1.5px oklch(0.85 0.01 285)` |
| **"How stored" kartı** | bg `oklch(0.985 0.008 285)` border `oklch(0.93 0.012 285)` |
| **header Save butonu** | bg accent beyaz `8px 20px` radius 6px 13.5px/600 |

## Section haritası (mockup ↔ bizim alanlar)
1. **"1 What is this segment?"** — Segment name + Segment code (readonly, "Generated from the name") grid; **"This segment groups"** = subject pill toggle (Doctors/contacts | Institutions/accounts) → `SubjectType` hidden (create-immutable); **"How is membership decided?"** = 2 radio-card (By a rule=dynamic | By a fixed list=static) → `SegmentType` (kontrat-güdümlü, hybrid düşmesin).
2. **"2 Who belongs in it?"** (isDynamic) — **Reads as** kutusu (canlı `sentence`); empty-state **recipes** (tek-tık kural); **groups**: her blok = header ("Include people where [every|any] condition below is true" + "Remove block") + koşullar (lead + attr `<optgroup>` SEG-B + op select + ✕ + değer kabı list-chips/date/number/bool + readback + reach chip SEG-C) + "+ Add condition"; bloklar arası **AND ALSO**; "+ Add another block"; footer "Match mode is set automatically" + JSON toggle (`<pre>`).
   **"2 Who is on the list?"** (isStatic) — textarea (CT-kodları) + "Why are they on it?" + reason code select.
3. **"3 Exceptions & validity"** — Always include / Never include / Usable from* / Usable until grid.
**Sağ ray (sticky):** "Who this reaches now" (big number + note + funnel + "Preview sample of 50") · "Sample members" (avatar+name+meta) · "How this is stored" · "Ready to activate?" checklist.

## KORUNACAK (SEG-A/B/C doğru — DAVRANIŞA DOKUNMA)
buildNodes blok→tree payload **byte-identical** (MatchMode=all + blok=group(every=all/any=any) + predicate; none-of=not-in, at-least=gte); catalog-driven optgroup (SEG-B Domain) + operatör + değer (hardcode YOK); value chip + **source badge** (valueSource.kind'den) + free-text; canlı reach (SEG-C `/preview` debounced, 422/stale guard) → totalCount + conditionCounts ("N match this alone") + sampleMembers (displayName); read-back "Reads as"; show-stored-rule (tree JSON); empty-state recipe → çalışan kural; static→manuel liste; subject create-immutable; same-origin proxy (`/CRM/Segments/api`). Create+Edit paylaşılan _Form; publish-freeze read-only.

## YAPMA
- Mockup'ın `sc-*`/`{{ }}`/`DCLogic` runtime'ını **kopyalama** (o Claude Design artefaktı) — yalnız **markup yapısı + stiller** portlanır, mantık mevcut form.js'ten. `segment-create.css`'i **global/başka sayfaya** uygulama (scope sarmalayıcı zorunlu). Backend/DTO/API/payload değiştir. Katalog-güdümlülüğü boz. Reach'i client-side uydur (mockup'taki `selectivity` JS sahte reach — KULLANMA; gerçek reach SEG-C `/preview`). Başka modül. **oklch/Inter'i sadece bu forma uygula, app shell'e (sidebar/topbar) dokunma.**

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil. Yeni `segment-create.css` yalnız _Form'da link'li + scope-sarmalayıcılı (global sızıntı yok). Görsel yapı mockup'la eşleşir: 3 numaralı section + subject pill + membership radio-card + Reads-as + blok(every/any + AND ALSO) + koşul(optgroup/op/chip/source/free-text/readback/reach) + JSON toggle + static textarea + exceptions grid + sağ ray(reach big-number/funnel/sample/how-stored/checklist). Token değerleri (accent oklch(0.55 0.19 285), card radius 10px, Inter) CSS'te birebir. **Payload byte-identical + catalog-driven + canlı reach (SEG-C) + static→manuel + same-origin proxy KORUNDU** (git diff form.js'te buildNodes/proxy/preview değişmedi). Create+Edit.
- **E4:** mockup'a piksel-yakın görsel eşleşme (fleet + ALMIBA nefrolog canlı reach).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-A3 · Segment Create/Edit birebir görsel port (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (SEG-A2 e2fdd087 üstü)> · Worktree: ana checkout

Önce oku (SIRAYLA):
1. execution/domains/commercial-suite/work-packs/WP-SEG-A3-frontend-exact-visual-port.md (bu WP — token tablosu + section haritası)
2. C:\tmp\mockup-segment.html (GÖRSEL GERÇEĞİN TEK KAYNAĞI — 167 inline style + oklch + Inter; token tablosunda OLMAYAN her değeri buradan al)
3. frontend/Diten.Web/Views/CRM/Segments/_Form.cshtml + wwwroot/assets/js/CRM/Segments/form.js (DAVRANIŞ KAYNAĞI — buildNodes/catalog/reach/proxy KORU)

AMAÇ: Owner 3 kez yaklaşık tasarımı reddetti ("tam tasarımı istiyorum") + mockup dosyasını verdi. Mockup'ı BİREBİR görsel olarak yeniden üret; davranışı (SEG-A/B/C) aynen koru. Bu WP için "yeni CSS yok" kuralı GERİ ALINDI.

NE (yalnız _Form.cshtml + form.js + YENİ wwwroot/assets/css/segment-create.css + L10n):
 - YENİ segment-create.css: mockup inline stillerini seg-* class'larına distile et (token tablosu + mockup dosyası). Root .segment-create-scope { font-family:'Inter',… } sarmalayıcı; TÜM seg-* bu scope altında. Yalnız _Form.cshtml'de link'le (global tema/başka sayfa ETKİLENMEZ).
 - _Form.cshtml markup'ı mockup section yapısına birebir: (1) What is this segment? [name+code grid, subject pill toggle, membership 2 radio-card]; (2) Who belongs in it? [Reads-as kutusu, empty-state recipes, bloklar(every/any toggle + AND ALSO ayraç + koşul satırları), +Add condition/+Add block, footer match-mode note + JSON toggle] / static ise Who is on the list? [textarea+reason]; (3) Exceptions & validity [always/never/from/until grid]; sağ ray sticky [Who this reaches now big-number+funnel+Preview50, Sample members, How this is stored, Ready to activate checklist].
 - form.js render fonksiyonları mockup'ın blok/koşul/chip/reach/optgroup markup'ını (seg-* class'larıyla) üretsin — ama on*/catalog/payload/reach ÇAĞRILARI MEVCUT mantıktan.
KORU (DAVRANIŞA DOKUNMA): buildNodes blok→tree payload byte-identical (MatchMode=all+group(every=all/any=any)+predicate; none-of=not-in, at-least=gte); catalog-driven optgroup(SEG-B Domain)/operatör/değer (hardcode YOK); chip+source badge(valueSource.kind)+free-text; canlı reach SEG-C /preview debounced(422/stale guard)=total+conditionCounts+sample; read-back "Reads as"; show-stored-rule tree JSON; empty-state recipe→çalışan kural; static→manuel; subject create-immutable; same-origin proxy /CRM/Segments/api; Create+Edit; publish-freeze read-only.
NASIL: markup YAPISI + stiller mockup'tan; MANTIK mevcut form.js'ten. oklch renkler + Inter yalnız .segment-create-scope altında. Mockup'ın sc-*/{{ }}/DCLogic runtime'ını KOPYALAMA (yalnız görünüm). Mockup selectivity JS sahte-reach'ini KULLANMA (gerçek reach SEG-C).
YAPMA: segment-create.css'i global/başka sayfaya uygula (scope zorunlu); app shell (sidebar/topbar) stilini değiştir; backend/DTO/payload; katalog-güdümlülüğü boz; reach client-side uydur; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil; segment-create.css yalnız _Form'da + scope-sarmalayıcılı (global sızıntı yok); 3 numaralı section + subject pill + membership radio-card + Reads-as + blok(every/any+AND ALSO) + koşul(optgroup/op/chip/source/free-text/readback/reach) + JSON toggle + static textarea + exceptions grid + sağ ray(big-number/funnel/sample/how-stored/checklist); token değerleri CSS'te birebir; payload byte-identical + catalog-driven + canlı reach(SEG-C) + static→manuel + same-origin proxy KORUNDU (form.js buildNodes/proxy/preview diff YOK). Ayrı commit. §22 TÜRKÇE. K13.
Durma: mockup section yapısı mevcut form.js mantığıyla kurulamıyorsa; payload/catalog/reach korunamıyorsa; scope sızıntısız yapılamıyorsa; kapsam Segments dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; scope-sızıntı kontrolü (segment-create.css yalnız _Form'da link + .segment-create-scope sarmalayıcı; global .css/tema değişmemiş); görsel-yapı eşleşme (section/pill/radio/Reads-as/blok/koşul/reach/exceptions markup + token değerleri); **davranış korundu** (form.js buildNodes/proxy/preview diff yok, catalog-driven, static→manuel).
