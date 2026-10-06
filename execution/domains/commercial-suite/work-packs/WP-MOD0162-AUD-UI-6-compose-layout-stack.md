# WORK PACKAGE — WP-MOD0162-AUD-UI-6 · Compose-row: Axis+Values yan yana, Add altta, sol padding azalt

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test layout isteği (AUD-UI-5 üstüne). Compose-row: **Axis ile Values yan yana** (aynı satır) + **Add butonu onların altında** (kendi satırı) + **soldaki padding/gap azalt** (withdrawn grip-affordance yer yiyor). Module: **MOD-0162** (AudienceProfile Dimensions compose-row). Branch: `feature/scmm-content-studio` (HEAD `0ac3be1a`). **Yalnız frontend** (Diten.Web); backend DOKUNMA. **SIRA:** Subject WP'sinden önce.

## Ölçülmüş girdi (CT)
- **Mevcut (0ac3be1a) `renderCompose` (~683):** `.diten-checkitem` (yatay flex) → `checkitemAffordance()` (soldaki withdrawn grip/move, yer tutuyor) + `.diten-checkitem-text.d-flex.flex-wrap` (Axis 11rem sabit + Values `flex:1 1 12rem`) + Add butonu (sağda, kardeş). Dar canvas'ta Values alta sarıyor (yan yana durmuyor) + Add sağda + sol grip boşluğu fazla.
- **Korunacak (AUD-UI-3/4/5):** compose-then-add, ValueCode saklama, Name " / ", cascade, ProfileType-koşul, small select2 (`selectionCssClass:'form-select form-select-sm'`), eklenen display satırları `.diten-checkitem`.

## Kapsam (yalnız frontend, compose-row layout — SADECE düzen)
1. **Axis + Values yan yana (1 satır):** compose-row'da Axis select2 + Values select2 **aynı satırda** dursun (dar canvas'ta bile yan yana; Values alta sarmasın). Örn: iç `d-flex gap-2` satırı, Axis `flex:1 1 0` + Values `flex:2 1 0` (ikisi de `min-width:0`, small select2). Sabit 11rem yerine esnek → yan yana sığar.
2. **Add altta (kendi satırı):** Add butonu Axis/Values satırının **altına** taşınsın (sağda değil). compose-row dikey stack: satır1 = Axis+Values yan yana, satır2 = Add (sol-hizalı, `align-self-start`).
3. **Sol padding/gap azalt:** compose-row'daki **soldaki withdrawn grip-affordance** kaldırılsın veya sol boşluk daraltılsın (compose-row bir add-row; sürüklenebilir öğe değil — grip yer tutması gereksiz). Gap/padding sıkılaştır.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. AUD-UI-3/4/5 davranış+yapısını (compose-then-add, ValueCode, Name " / ", cascade, ProfileType-koşul, **small select2**, eklenen `.diten-checkitem` satırları) DEĞİŞTİRME — yalnız compose-row düzeni. Native select'e dönme (small select2 kalsın). Eklenen display satırlarının görünümünü değiştirme (yalnız compose-row). Yeni CSS icat (inline flex stilleri yeter). Subject/Topic/başka konsol. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; compose-row: Axis+Values **yan yana** (dar canvas'ta da), Add **altta** sol-hizalı, sol grip-boşluğu azaltıldı; small select2 korunur; AUD-UI-3/4/5 davranışı korunur (ekle/sil/Name " / "/ValueCode/cascade); eklenen satırlar değişmedi; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** compose-row Axis+Values yan yana, Add altında, düzen sıkı/temiz.
- Kapsam: yalnız Diten.Web (taxonomy.js renderCompose + gerekirse Taxonomy.cshtml). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI-6 · Prompt v1.0  (Compose-row Axis+Values yan yana + Add altta + sol padding azalt — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 0ac3be1a · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-6-compose-layout-stack.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js — renderCompose (~683 .diten-checkitem + checkitemAffordance + .diten-checkitem-text + Add) + initComposeSelect2 (small select2, KORUNUR) + checkitemAffordance

NE (yalnız frontend Diten.Web, compose-row layout — SADECE düzen):
 1) Axis + Values YAN YANA (aynı satır, dar canvas'ta bile): iç d-flex gap-2 satırı, Axis flex:1 1 0 + Values flex:2 1 0 (ikisi min-width:0, small select2 korunur). Sabit 11rem yerine esnek.
 2) Add butonu ALTTA: Axis/Values satırının altına, sol-hizalı (align-self-start). compose-row dikey stack (satır1=Axis+Values, satır2=Add). Add artık sağda değil.
 3) Sol padding/gap azalt: compose-row'daki withdrawn grip-affordance'ı kaldır veya sol boşluğu daralt (add-row, sürüklenebilir öğe değil); gap/padding sıkılaştır.
NASIL: renderCompose markup'ını yeniden düzenle (dikey stack + iç yan-yana satır). initComposeSelect2 small select2 + AUD-UI-3/4/5 mantığı DOKUNULMADAN yalnız düzen değişir. Eklenen display satırları (renderDimensions) DEĞİŞMEZ.
YAPMA: backend/API/RBAC/ocelot; AUD-UI-3/4/5 davranış/yapı/small-select2 değiştir; native select'e dön; eklenen satır görünümünü değiştir; yeni CSS icat; Subject/Topic/başka konsol; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; compose Axis+Values yan yana + Add altta sol-hizalı + sol grip boşluğu azaldı; small select2 korunur; AUD-UI-3/4/5 davranışı korunur.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: yan-yana + Add-altta düzen small select2'yi bozuyorsa · AUD-UI davranışı bozuluyorsa · kapsam compose-row dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra **WP-MOD0162-SUBJECT-UI** → ALMIBA retest.
