# WORK PACKAGE — WP-ST-EDIT-O · Ziyaret Sıklığı Politikası dropdown → single select2 (aramalı) (frontend)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (WP-ST-EDIT-N §37 sonrası HEAD üstü — form.js SIRALI, EDIT-N aynı policy block). **E4 rötuşu:** Frekans policy-reference modundaki **"Ziyaret Sıklığı Politikası"** dropdown'u (`#frequencyPolicyId`) → **single select2, arama'lı** (çok sayıda politika arasında ara). **Frontend** (form.js + gerekirse css). Backend DEĞİŞMEZ.

## Kanıt
- `form.js` renderFrequency (~321): `#frequencyPolicyId` düz `<select>`; opsiyonlar `/visit-frequency-policies` load'dan. EDIT-L'de **rol için select2 helper** eklendi (`bindRoleSelect2`, `minimumResultsForSearch: Infinity` = aramasız + `change.stRoleBridge`→native). Policy select için **arama AÇIK** select2 gerekli.
- EDIT-N (bu WP'den önce): policy select yanına "Politikayı aç ↗" butonu + change'de href güncelleme. select2 jQuery-sentez change → native köprü ile hem FrequencyIntentJson sync hem "Politikayı aç" güncellenmeli.

## NE (frontend; backend DEĞİŞMEZ)
0. **Header rename (resx):** `FrequencySection` → **"Frekans niyeti — Ne sıklıkta"** (7 dil; diğer bölüm "{Ad} — {Soru}" deseni: Segmentler — Kim, Kapsam — Nerede). text-uppercase h6 büyük harfe çevirir.
1. **Policy select → select2 (aramalı):** EDIT-L select2 helper'ını **genelleştir** (ör. `bindSelect2(select, { search })`) veya policy için ayrı bind: `#frequencyPolicyId` select2 init — **arama AÇIK** (minimumResultsForSearch KALDIR/0; policy'ler için search'lü), `dropdownParent: $s.parent()`, `change`→native köprü (EDIT-L deseni). renderFrequency sonrası (opsiyonlar dolunca) init/rebind. jQuery/select2 yoksa düz select degrade.
2. **Değişim akışı:** select2 seçimi native change tetiklesin → mevcut form change dinleyicisi FrequencyIntentJson `visitFrequencyPolicyId`'yi güncellesin (byte-for-byte sözleşme) **VE** EDIT-N "Politikayı aç" butonunun href/aktifliği güncellensin. (EDIT-N change handler'ı native change'i dinliyorsa köprü yeterli; değilse köprüden sonra tetiklenmeli.)
3. **Rol select2 (EDIT-L) aramasız kalır** (3 opsiyon, Infinity). Yalnız policy select aramalı.

## KORU / YAPMA
- Backend DEĞİŞMEZ. FrequencyIntentJson (mode/visitFrequencyPolicyId/…) + mode buton (EDIT-M) + "Politikayı aç" (EDIT-N) + submit + declared/none blok DEĞİŞMEZ (yalnız policy select select2). Rol select2 (EDIT-L) davranışı korunur (aramasız). KAPSAM/Segment/Ürün/İçerik/sağ panel DOKUNMA. Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA. select2 salt sunum+arama (değer/GUID sözleşmesi aynı). Tema/L10n köprüsü.
- **DUR:** policy select2 FrequencyIntentJson sync'ini veya "Politikayı aç" güncellemesini bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: form.js (+ css gerekirse). Backend/_Form(mümkünse)/Controller/liste/detay diff YOK.
- **E4:** Ziyaret Sıklığı Politikası dropdown'u aramalı select2; seçince FrequencyIntentJson + "Politikayı aç" doğru; rol select2 aramasız kalır. **js → Ctrl+F5.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-ST-EDIT-N §37 sonrası)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-EDIT-O · Ziyaret Sıklığı Politikası dropdown → single select2 aramalı (MOD-0167-FU04, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: <WP-ST-EDIT-N §37 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-EDIT-O-policy-select2.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/form.js (renderFrequency #frequencyPolicyId ~321, EDIT-L select2 helper bindRoleSelect2/hasSelect2/change.stRoleBridge, EDIT-N frequencyPolicyOpen change handler) · (referans) VFP form.js select2 (aramalı).

NE (frontend; backend DEĞİŞMEZ):
 0) resx: FrequencySection → "Frekans niyeti — Ne sıklıkta" (7 dil; {Ad}—{Soru} deseni).
 1) EDIT-L select2 helper'ını genelleştir (bindSelect2(select,{search:true/false})) veya policy için ayrı: #frequencyPolicyId select2 init ARAMA AÇIK (minimumResultsForSearch kaldır/0), dropdownParent:$s.parent(), change→native köprü. renderFrequency sonrası (opsiyon dolunca) init/rebind. jQuery yoksa düz select degrade.
 2) select2 seçimi native change → FrequencyIntentJson visitFrequencyPolicyId sync (byte-for-byte) + EDIT-N "Politikayı aç" href/aktiflik güncelle.
 3) Rol select2 (EDIT-L) aramasız (Infinity) KALIR — yalnız policy aramalı.
KORU/YAPMA: backend DEĞİŞMEZ; FrequencyIntentJson + mode buton(EDIT-M) + Politikayı aç(EDIT-N) + submit + declared/none DEĞİŞMEZ; rol select2 aramasız korunur; KAPSAM/Segment/Ürün/İçerik/sağ panel DOKUNMA; Liste/Detay/details.js/CrmService/Controller/ViewModel DOKUNMA; select2 salt sunum+arama (değer aynı); tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff form.js(+css)+7 resx(FrequencySection); backend/Controller/liste/detay diff yok. Ayrı commit ("feat(strategy): WP-ST-EDIT-O — Frekans policy dropdown single select2 aramalı + header 'Frekans niyeti — Ne sıklıkta' (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: policy select2 FrequencyIntentJson sync/Politikayı aç güncellemesini bozuyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 78ab1bb5 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-steito-verify @78ab1bb5
```
- ✅ **Kapsam:** form.js + 7 resx. **backend/Controller/ViewModel/_Form/liste/detay TEMİZ** ✓.
- ✅ `bindSelect2(select,{search})` genelleştirildi (EDIT-L bindRoleSelect2 → wrapper search:false Infinity aramasız; policy search:true aramalı) + `unbindSelect2` (renderFrequency'de option yeniden yazımından önce teardown, sonra rebind); `#frequencyPolicyId` aramalı single select2; native change köprüsü FrequencyIntentJson.visitFrequencyPolicyId (byte-for-byte) + EDIT-N syncFrequencyPolicyOpen'ı besler. `FrequencySection` → "Frekans niyeti — Ne sıklıkta" (7 dil). Rol select2 aramasız korundu.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: header + policy select2 aramalı. **js/resx → Ctrl+F5 (resx → restart).**

**WP-ST-EDIT-O KOMPLE. Frekans bölümü mockup + insan-dostu + select2 tam.**
```
