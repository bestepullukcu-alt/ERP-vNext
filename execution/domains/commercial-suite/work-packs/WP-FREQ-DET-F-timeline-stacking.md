# WORK PACKAGE — WP-FREQ-DET-F · DURUM AKIŞI timeline title/detail alt-alta (css, mini)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`ecea7d04` üstü). Kullanıcı: DURUM AKIŞI'nda "2026-09-17 18:46 · Admin User" başlığın (Taslak oluşturuldu) **altında ayrı satırda** olmalı; şu an bitişik. Kök neden: `.vfp-dv-tl-title`/`.vfp-dv-tl-detail` span + `display:block` yok → inline bitişik. **Frontend only** (visit-frequency-details.css, 1-2 kural).

## Kapsam
- `visit-frequency-details.css`: **detay timeline'a scope'lu** (`#vfpDetTimeline`) — `.vfp-dv-tl-title` ve `.vfp-dv-tl-detail` `display:block` (tarih·aktör başlığın altına düşsün). Resolve panelinin paylaşılan `vfp-dv-tl` (varsa) etkilenmesin diye `#vfpDetTimeline` altına scope'la. Tema-duyarlı (renk token'ları mevcut).

## KORU / YAPMA
- Yalnız css (display kuralı). details.js/markup/backend/liste/editör/_Resolve/resolve.js/Segment DOKUNMA. Timeline dot/çizgi/renk mevcut korunur. Resolve tab timeline'ı bozma (#vfpDetTimeline scope).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff yalnız visit-frequency-details.css.
- **E4:** DURUM AKIŞI: başlık üstte, "tarih · aktör" bir alt satırda (muted); dot/çizgi aynı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-F · Timeline title/detail alt-alta (MOD-0165-FU03, css mini)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: ecea7d04 üstü · Worktree: ana checkout

Önce oku: frontend/Diten.Web/wwwroot/assets/css/visit-frequency-details.css (.vfp-dv-tl-title ~134 / .vfp-dv-tl-detail ~135 + #vfpDetTimeline blokları ~216) · frontend/.../details.js (renderTimeline markup: vfp-dv-tl-title + vfp-dv-tl-detail — DEĞİŞTİRME).

NE (yalnız css): #vfpDetTimeline'a scope'lu — .vfp-dv-tl-title ve .vfp-dv-tl-detail display:block (tarih·aktör başlığın ALTINA ayrı satır). Resolve panelinin paylaşılan vfp-dv-tl'sini etkileme (#vfpDetTimeline scope). Mevcut dot/dikey çizgi/renk/margin korunur.
KORU/YAPMA: yalnız visit-frequency-details.css; details.js/markup/backend/liste/editör/_Resolve/resolve.js/Segment DOKUNMA; Resolve tab timeline bozma.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız visit-frequency-details.css. Ayrı commit. §22 TÜRKÇE. K13.
Durma: markup değişikliği gerekiyorsa (css yetmiyorsa) → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: a2f68d2f · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detf-verify @a2f68d2f
```
- ✅ Kapsam: yalnız visit-frequency-details.css (+6). `#vfpDetTimeline .vfp-dv-tl-title, .vfp-dv-tl-detail { display: block; }` (scope'lu → Resolve trail etkilenmez). details.js/markup/backend/Segment dokunulmadı.
- ✅ Build+test (CT izole, Release, temiz): Diten.Web.Tests **137/0**.
- ⏳ E4: fleet restart + Ctrl+F5 → timeline "tarih · aktör" başlığın altında.

**DET-F KOMPLE. Detay sayfası serisi (DET-A→F) TAM.**
```
