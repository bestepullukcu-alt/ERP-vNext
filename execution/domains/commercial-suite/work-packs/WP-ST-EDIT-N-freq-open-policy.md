# WORK PACKAGE — WP-ST-EDIT-N · Frekans: "Politikayı aç" butonu + info-strong primary renk (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`a2359ef1` üstü). **E4 rötuşu:** (1) "Politikaya işaretçi" + politika seçilince yanında **"Politikayı aç ↗"** butonu (mockup) — seçili politikanın detayına; (2) info-box'taki strong metin ("Frekans, Ziyaret Sıklığı Politikaları'nda yönetilir") → **"Politikaları aç" link rengi (primary)**. **Frontend** (_Form.cshtml + form.js + strategy-create.css + resx). Backend DEĞİŞMEZ.

## Kanıt
- `_Form.cshtml` policy block (~207-209): `<select id="frequencyPolicyId">` — yanında "Politikayı aç" YOK. Info strong satır 192 `.st-freq-info-strong`.
- `form.js` renderFrequency (~321-354): policyBlock/policySelect göster-gizle + FrequencyIntentJson `visitFrequencyPolicyId`. Per-policy detay linki yok.
- css: `.st-freq-info-strong { color: var(--st-text) }` (287); `.st-freq-info-link { color: var(--bs-primary) }` (289).

## NE (frontend; backend DEĞİŞMEZ)
1. **"Politikayı aç ↗" butonu:** policy block'ta select ile yan yana (flex row / input-group): `<a id="frequencyPolicyOpen" class="btn btn-outline-primary btn-sm" target="_blank" rel="noopener">@Localizer["FreqOpenPolicySingle"] ↗</a>` ("Politikayı aç", yeni L10n; info'daki "Politikaları aç"=liste, bu=tekil politika). form.js: policy select change'de `frequencyPolicyOpen.href = '/CRM/VisitFrequencyPolicies/Details/' + seçiliId`; politika seçili değilse buton **disabled/gizli** (href yok). renderFrequency içinde başlangıçta da senkronla. FrequencyIntentJson/select mantığı DEĞİŞMEZ.
2. **Info-strong primary:** `.st-freq-info-strong` color `var(--st-text)` → **`var(--bs-primary)`** (info-link ile aynı). Diğer info stilleri (text muted, link) DEĞİŞMEZ.

## KORU / YAPMA
- Backend DEĞİŞMEZ. FrequencyIntentJson (mode/visitFrequencyPolicyId/…) + mode buton (EDIT-M) + submit + declared/none blokları DEĞİŞMEZ (yalnız policy block'a aç-butonu + info-strong renk). KAPSAM/Segment/Ürün/İçerik/sağ panel DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. Buton yalnız policy-reference modunda + politika seçiliyken aktif. Tema/L10n köprüsü.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: _Form.cshtml + form.js + strategy-create.css + resx. Backend/Controller/liste/detay diff YOK.
- **E4:** politika seçilince "Politikayı aç ↗" (detaya, yeni sekme); seçili değilken pasif/gizli; info-strong primary renkte. **Razor+css+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-N · Frekans "Politikayı aç" butonu + info-strong primary (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: a2359ef1 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-N-freq-open-policy.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/_Form.cshtml (policy block ~207-209 + .st-freq-info ~190) · wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderFrequency ~321-354 policy select) · wwwroot/assets/css/strategy-create.css (.st-freq-info-strong ~287 / -link ~289). (referans) info'daki "Politikaları aç" link deseni + KAPSAM/VFP detay link.

NE (frontend; backend DEĞİŞMEZ):
 1) policy block: select ile yan yana <a id="frequencyPolicyOpen" class="btn btn-outline-primary btn-sm" target="_blank" rel="noopener">Politikayı aç ↗</a> (yeni L10n FreqOpenPolicySingle, 7 dil; info'daki FreqInfoLink=liste, bu=tekil). form.js: frequencyPolicyId change → frequencyPolicyOpen.href='/CRM/VisitFrequencyPolicies/Details/'+seçiliId; seçili değilse disabled/gizli; renderFrequency'de başlangıç senkron. FrequencyIntentJson/select mantığı DEĞİŞMEZ.
 2) .st-freq-info-strong color var(--st-text) → var(--bs-primary) (info-link ile aynı). Diğer info stilleri DEĞİŞMEZ.
KORU/YAPMA: backend DEĞİŞMEZ; FrequencyIntentJson + mode buton(EDIT-M) + submit + declared/none blok DEĞİŞMEZ; KAPSAM/Segment/Ürün/İçerik/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; buton yalnız policy-reference+politika seçili aktif; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff _Form+form.js+css+resx; backend/Controller/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-N — Frekans 'Politikayı aç' butonu + info-strong primary renk (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: aç-butonu FrequencyIntentJson/mode akışını bozuyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: f71f4f6a · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steitn-verify @f71f4f6a
```
- ✅ **Kapsam (10 dosya, +43/−2):** _Form.cshtml + form.js + strategy-create.css + 7 resx. **backend/Controller/ViewModel/liste/detay TEMİZ** ✓.
- ✅ `frequencyPolicyBlock`'a `#frequencyPolicyOpen` "Politikayı aç ↗" (btn-outline-primary btn-sm; başta disabled/aria-disabled/tabindex-1); `syncFrequencyPolicyOpen()` → seçili politikada href=/Details/{id} + aktif, seçili değilse pasif; renderFrequency + change'de senkron. `.st-freq-info-strong` color → `var(--bs-primary)`. Yeni L10n FreqOpenPolicySingle (7 dil). **FrequencyIntentJson/mode/readFrequency/submit DEĞİŞMEDİ.**
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: "Politikayı aç" + info-strong primary. **FLEET RESTART.**

**WP-ST-EDIT-N KOMPLE.**
```
