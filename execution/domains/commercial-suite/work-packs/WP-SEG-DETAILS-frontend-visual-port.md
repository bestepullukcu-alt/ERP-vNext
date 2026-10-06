# WORK PACKAGE — WP-SEG-DETAILS · Segment Details birebir görsel port (frontend, tema-duyarlı)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`2768ffa2` üstü). **Yalnız frontend (Diten.Web).** Owner Details mockup'ı verdi ("hemen hemen tam tasarım olsun") + *"daha önce karşılaştığımız sorunları göz önünde bulundurarak"*. Yani Create/Edit zincirinde (SEG-A3→A6) öğrenilen HER dersi **tek seferde** uygula: scoped CSS **tema-duyarlı** (`--bs-*`, sabit beyaz/koyu YOK) + **app-card kabuk** + **accent→`--bs-primary`** + davranışı koru.

## Referans (agent MUTLAKA okuyacak)
- **`C:\tmp\mockup-segment-details.html`** (58KB, x-dc template — 219 oklch, 174 inline-style, sc-if/sc-for) — görsel gerçeğin kaynağı.
- **`C:\tmp\mockup-segment-details-script.js`** (data/davranış modeli — bölümler, tokenlar, 3-durum mantığı, ALMIBA nefrolog örnek verisi).
- Mevcut: `Views/CRM/Segments/Details.cshtml` (256 satır, 7 read-only kart) + `wwwroot/assets/js/CRM/Segments/details.js` (151 satır: segment-type toggle + `/resolve` fetch + activate/newVersion). **Davranış kaynağı — KORU.**
- Görsel-referans olarak zaten yaptığımız `wwwroot/assets/css/segment-create.css` (tema-duyarlı scoped desen: `.segment-create-scope`, `--seg-card-bg=var(--bs-card-bg)`, Inter yalnız `[class^="seg-"]:not(.seg-json-pre)`, accent=`var(--bs-primary)`).

## Bölümler (mockup — birebir port)
Sol sütun: **Stats** (Members/On-the-list · Status · Version · Used-by — value+unit+note) · **Reads-as** cümlesi · **Criteria** (Frozen/Editable badge + intro + gruplar[AND ALSO joiner + operator + explain + predicate'ler{lead/human/attribute-operator-value badge}] + **Show stored JSON** toggle / static→noCriteria) · **Resolve** (idle/running/done + "Run resolve" + summary[included/dropped/from-manual] + resolvedAt + satırlar[avatar/name/subjectId/**secondary**/verdict/source/reasons] + footer + **Excluded**[name/secondary/reasonCode/reason]) · **Manual** (intro + counts[kept-in/kept-out] + satırlar[avatar/name/secondary/mode badge/reasonCode/reason/from] + footer). Sağ ray (sticky): **Timeline** (created/activated/updated/usable/supersedes) · **Classification** (segment-type badge/subject/match-mode/BU/owner + note) · **Used as audience by** (consumers) · **stored-note**.

## KURALLAR — SEG-A3→A6 dersleri (baştan doğru)
1. **YENİ `wwwroot/assets/css/segment-details.css`** — mockup inline stilleri `segd-*` class'larına; TÜMÜ `.segment-details-scope` altında; yalnız `Details.cshtml`'de link. (segment-create.css'i BOZMA/paylaşma — ayrı dosya.)
2. **Tema-duyarlı (light+dark)** — sabit `oklch(1 0 0)` beyaz / koyu metin YOK. Nötr: yüzey→`var(--bs-card-bg)`, kontrol/girinti→`var(--bs-body-bg)`/`var(--bs-tertiary-bg)`/`var(--bs-secondary-bg)`, metin→`var(--bs-body-color)`/`--bs-heading-color`/`--bs-secondary-color`, border→`var(--bs-border-color)`.
3. **Accent tema-duyarlı** — mor(285)→`var(--bs-primary)`/`rgba(var(--bs-primary-rgb),α)`; yeşil(152)→`var(--bs-success)`/`--bs-success-bg-subtle`+`-text-emphasis`; kırmızı(25/40)→`var(--bs-danger)`; amber(75/85)→`var(--bs-warning-*-subtle/emphasis)`. Verdict/mode/status/reason badge'leri bu bs subtle+emphasis çiftleriyle (dark okunur).
4. **app-card kabuk** — her section `<section class="card mb-4"><div class="card-body p-4">` + `h6.text-uppercase.text-heading.fw-semibold.mb-4.d-flex.align-items-center.gap-2` + `<i class="bx … dt-card-icon">`. **Font ayrımı:** `.segment-details-scope` root'ta global Inter YOK; Inter yalnız `[class^="segd-"]:not(.segd-mono)`; kart chrome app fontu (Public Sans); mono kod → `ui-monospace`.
5. **Scope izolasyon** — `.card`/global tema override ETME; `<link>` yalnız Details'te.

## KORUNACAK DAVRANIŞ (mockup'ın sahtelerini KULLANMA)
- **3-durum toggle** (`data-segment-type`): static→resolve gizli+manual görünür; dynamic→manual gizli+resolve görünür; hybrid→ikisi (details.js mevcut mantık).
- **Resolve = KAYITLI `/segments/{id}/resolve`** (mockup'ın `setTimeout 900ms`/sabit 1284 SAHTE — KULLANMA); gerçek members+excluded+verdict+reason+source. secondary label SEG-D/F'den (`subjectSecondaryLabel`).
- **Lifecycle eylemleri:** Activate (draft+CanActivate) · New Version (CanManage) · Edit — mevcut handler + yetki koşulları.
- **Criteria ağacı** kayıtlı kuraldan (group/predicate/combinator); Show stored JSON.
- **Timeline** mevcut Lifecycle verisinden türet (created/activated/updated/effective) — UYDURMA yok.
- **Consumers ("Used as audience by")**: backend'de segment-consumer reverse-lookup verisi **YOKSA** bu bölümü GİZLE (mockup görünümünü uydurma sayıyla DOLDURMA). Varsa göster.
- Same-origin proxy `/CRM/Segments/api`. Read-only (Details düzenlemez).

## YAPMA
- Mockup `sc-*`/`{{ }}`/`DCLogic` runtime'ını kopyalama (yalnız görünüm). Sahte resolve/sabit sayı. Sabit oklch beyaz/koyu (tema-duyarlı bs değişkeni). `.card`/global tema override. `.segment-details-scope` scope'unu kaldırma. Backend/DTO/controller. Uydurma consumer/veri. segment-create.css'i değiştirme. Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Yeni `segment-details.css` yalnız Details'te + `.segment-details-scope` (global sızıntı 0, `.card` override 0); light+dark okunur (sabit beyaz bg=0); accent→`--bs-primary`, badge'ler bs subtle/emphasis; app-card kabuk + font ayrımı; bölümler mockup ile eşleşir. **Davranış korundu:** 3-durum toggle + kayıtlı `/resolve` (sahte değil) + lifecycle eylemleri + criteria ağaç + secondary label + proxy. Consumer verisi yoksa gizli (uydurma yok). Create+Details tutarlı görünüm.
- **E4:** dynamic/static/hybrid segment Details canlı (ALMIBA); resolve gerçek sayı; dark + tema rengi.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-DETAILS · Segment Details birebir görsel port (tema-duyarlı) (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (2768ffa2 üstü)> · Worktree: ana checkout

Önce oku (SIRAYLA):
1. execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS-frontend-visual-port.md (bu WP — kurallar + bölümler + korunacak davranış)
2. C:\tmp\mockup-segment-details.html (görsel kaynak) + C:\tmp\mockup-segment-details-script.js (data/davranış modeli, 3-durum)
3. frontend/Diten.Web/Views/CRM/Segments/Details.cshtml + wwwroot/assets/js/CRM/Segments/details.js (DAVRANIŞ KAYNAĞI — toggle/resolve/lifecycle KORU)
4. wwwroot/assets/css/segment-create.css (tema-duyarlı scoped DESEN referansı — segd-* için aynı yaklaşım; bu dosyayı DEĞİŞTİRME)

AMAÇ: Owner Details mockup'ını "hemen hemen tam tasarım" istiyor, "önceki sorunları göz önünde bulundurarak" = SEG-A3→A6 dersleri tek seferde: scoped CSS tema-duyarlı (--bs-*), app-card kabuk, accent→--bs-primary, davranış koru.

NE (Details.cshtml + details.js + YENİ segment-details.css + L10n):
 - YENİ segment-details.css: mockup inline stilleri segd-* class'larına, TÜMÜ .segment-details-scope altında, yalnız Details.cshtml'de link. Font ayrımı: root global Inter YOK, Inter yalnız [class^="segd-"]:not(.segd-mono); kart chrome app fontu; mono→ui-monospace.
 - Tema-duyarlı: sabit beyaz/koyu oklch YOK → --bs-card-bg/body-bg/tertiary-bg/secondary-bg (yüzey), --bs-body-color/heading-color/secondary-color (metin), --bs-border-color (border). Accent 285→var(--bs-primary)/rgba(--bs-primary-rgb,α); success 152→--bs-success + --bs-success-bg-subtle/-text-emphasis; danger 25/40→--bs-danger(-subtle/-emphasis); warning 75/85→--bs-warning-*-subtle/emphasis. Badge'ler subtle+emphasis (dark okunur).
 - app-card kabuk: her section <section class="card mb-4"><div class="card-body p-4"> + h6.text-uppercase.text-heading.fw-semibold + bx dt-card-icon. İç içerik segd-* (Inter/tema).
 - Details.cshtml markup mockup bölümlerine: stats / reads-as / criteria(ağaç+JSON) / resolve(+excluded) / manual / sağ ray timeline / classification / used-by(consumers) / stored-note. details.js'in bağlandığı tüm id/data-attr KORU.
 - details.js: mockup markup'ını üretirken KORU: 3-durum toggle (data-segment-type: static→resolve gizli+manual, dynamic→manual gizli+resolve, hybrid→ikisi); resolve = KAYITLI /segments/{id}/resolve (mockup setTimeout/sabit 1284 SAHTE — kullanma); members+excluded+verdict+reason+source + secondary label (subjectSecondaryLabel SEG-D/F); lifecycle activate/newVersion/edit + yetki; criteria ağaç + Show stored JSON; timeline mevcut lifecycle verisinden (uydurma yok); consumers backend verisi yoksa GİZLE (uydurma sayı yok); same-origin proxy.
KORU/YAPMA: mockup sc-*/{{}}/DCLogic runtime kopyalama (yalnız görünüm); sahte resolve/sabit sayı KULLANMA; sabit oklch beyaz/koyu (tema bs değişkeni); .card/global tema override; scope kaldırma; backend/DTO/controller; uydurma consumer/veri; segment-create.css değiştirme; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); segment-details.css yalnız Details + .segment-details-scope (global sızıntı 0, .card override 0); light+dark okunur (sabit beyaz bg=0); accent→--bs-primary, badge bs subtle/emphasis; app-card+font ayrımı; 3-durum toggle + kayıtlı /resolve (sahte değil) + lifecycle + criteria ağaç + secondary label + proxy korundu; consumer yoksa gizli. Ayrı commit. §22 TÜRKÇE. K13.
Durma: mockup davranışı gerçek /resolve+toggle ile kurulamıyorsa; consumer verisi için backend gerekiyorsa (GİZLE, DUR değil); scope/font ayrımı yapılamıyorsa; kapsam Details+details.js+segment-details.css dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```text
Commit: 18f89e55 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-segdet-verify @18f89e55
```
- ✅ **Scope:** segment-details.css (YENİ) + Details.cshtml + details.js + _IndexL10n + 7 resx. Backend/segment-create.css DOKUNULMADI.
- ✅ **Scope izole + tema-duyarlı:** scope-dışı selektör=0, `.card` override=0, sabit beyaz background=0; `<link>` yalnız Details.cshtml'de. accent→`var(--bs-primary)` ×12, badge'ler `--bs-success`/`--bs-danger`/`--bs-warning` subtle+emphasis (light+dark okunur).
- ✅ **Davranış korundu (mockup sahtesi KULLANILMADI):** kayıtlı `/segments/{id}/resolve` fetch (satır 123); mockup `setTimeout 900`/sabit `1284` = **0**; 3-durum toggle (static→resolve gizli / dynamic→manual gizli / hybrid→ikisi); `subjectSecondaryLabel` (SEG-D/F) resolve+manual+excluded tablolarında; activate/newVersion lifecycle handler; criteria ağaç + Show stored JSON; same-origin proxy.
- ✅ **Uydurma yok:** consumer reverse-lookup backend'i olmadığından "Used as audience by" + "Used by" stat render EDİLMEDİ; timeline yalnız gerçek lifecycle verisinden (created/activated/updated); members stat kayıtlı /resolve'dan (static'te manuel kept-in).
- ✅ **app-card + font ayrımı:** section'lar `card mb-4 + card-body p-4 + h6.text-heading + bx dt-card-icon`; Inter yalnız `[class^="segd-"]:not(.segd-mono)`; mono→ui-monospace.
- ✅ **L10n:** 24 anahtar × 7 dil + JS köprü (FromManual/KeptIn/KeptOut/NoMembers).
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/0**.
- ⏳ **E4:** ALMIBA static/dynamic/hybrid Details canlı; resolve gerçek sayı; dark + tema rengi.

**Segment Details tema-duyarlı mockup port'u KOMPLE — Create/Edit ile görsel tutarlı.**
