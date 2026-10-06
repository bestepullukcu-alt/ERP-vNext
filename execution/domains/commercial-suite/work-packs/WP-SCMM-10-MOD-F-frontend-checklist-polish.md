# WORK PACKAGE — WP-SCMM-10-MOD-F · ChainTemplate satır/branch header rötuşu (frontend)

> **CT (SoR).** MOD-0162 SCMM-10. Branch `feature/scmm-content-studio` (WP-E `46796154` üstü). **Yalnız frontend, YENİ CSS YOK** — mevcut class + TaskCenter (Tasks/ChecklistTemplates) stili. Owner 3 rötuş. WP-E'nin devamı. **Paketli; dispatch owner'da (CRM paralel).**

## Owner'ın 3 rötuşu
1. **Row butonları TaskCenter checklist stiline** — step satırındaki kontrol butonları (move ↑↓, Details toggle, remove ×) Tasks/ChecklistTemplates deseni: remove = `btn btn-icon btn-text-danger` + `<i class="icon-base bx bx-trash icon-sm">`; move/details = `btn btn-icon btn-text-secondary` (veya btn-text-body) + `icon-base bx-* icon-sm`. Mevcut `btn-sm btn-label-secondary/btn-label-danger` yerine TaskCenter text-buton görünümü.
2. **Branch-name input kart genişliğinde** — branch header'daki isim input'undan `style="max-width:18rem"` KALDIR; input `flex-grow-1` ile kart genişliğini kullansın (number badge sol + step-count/delete sağ arasında esner).
3. **"N steps" badge sil butonunun YANINA** — step-count badge'i isimden sonra (sol) yerine header'ın SAĞINA, delete (trash) butonunun hemen soluna al. Header düzeni: `[number badge] [name input flex-grow] [N steps badge] [delete]` (`d-flex align-items-center gap-2`).

## Ölçülmüş girdi (CT)
- Renderer: `wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js` (WP-E dikey satır; branch header + stepRow render). Backend/submit DOKUNMA (refs-free + template-level Moderator/ForWhom).
- TaskCenter buton referansı: `Views/Tasks/ChecklistTemplates/_Form.cshtml` — remove `btn btn-icon btn-text-danger`+`bx-trash icon-sm`, add `btn btn-sm btn-label-primary`+`bx-plus`.
- Mevcut branch-name input: `style="max-width:18rem"` (kaldırılacak). Step-count badge şu an isimden sonra (sola yakın).

## YAPMA
- **Yeni CSS/inline-style/vendor YOK** (yalnız mevcut class + var olan style değerleri). Backend/DTO/API/submit. Step'e Moderator/Audience. Details=Min/Max modelini bozma. Reference set. Faz-2. Başka modül. WP-E'nin dikey-satır/SortableJS/paging/refs-free yapısını bozma — yalnız buton stili + header düzeni.

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` baseline-diff yeşil. Row butonları TaskCenter stili; branch-name input kart-genişliği (max-width:18rem yok, flex-grow); "N steps" badge delete'in yanında (sağ). **git diff yeni CSS/style-değeri yok**; WP-E yapısı (dikey satır/drag/Details=Min-Max/refs-free/template-level) korunur. Create+Edit aynı.
- **E4:** A2d — header'da isim geniş, N steps sağda delete yanında, butonlar TaskCenter görünümü.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SCMM-10-MOD-F · Prompt v1.0  (ChainTemplate satır/branch header rötuşu — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (WP-E 46796154 üstü)> · Worktree: ana checkout
(CRM paralel: yalnız KnowledgeConcepts template-form.js'e dokunur; çakışırsa sıralı git.)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-F-frontend-checklist-polish.md (bu WP — 3 rötuş)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (WP-E dikey satır render: branch header + stepRow)
3. TaskCenter buton referansı: Views/Tasks/ChecklistTemplates/_Form.cshtml (remove btn btn-icon btn-text-danger + bx-trash icon-sm)

NE (yalnız template-form.js, gerekiyorsa _TemplateFormL10n yok):
 1) Step satırı kontrol butonları TaskCenter stiline: remove=btn btn-icon btn-text-danger + icon-base bx bx-trash icon-sm; move ↑↓ + Details toggle = btn btn-icon btn-text-secondary + icon-base bx-* icon-sm. (btn-label-* → btn-text-*).
 2) Branch-name input: style="max-width:18rem" KALDIR; input flex-grow-1 (kart genişliği). Header d-flex align-items-center gap-2.
 3) "N steps" badge'i header SAĞINA, delete butonunun soluna al: [number badge][name flex-grow][N steps badge][delete].
NASIL: SADECE mevcut class (btn-icon/btn-text-danger/btn-text-secondary/icon-base/bx-*/icon-sm/flex-grow-1/d-flex/align-items-center/gap-2/badge). WP-E yapısını (dikey satır/SortableJS/Details=Min-Max/branch paging/refs-free) KORU — yalnız buton stili + header düzeni değişir.
YAPMA: yeni CSS/inline-style-değeri/vendor; backend/DTO/API/submit; step'e Moderator/Audience; Details modelini bozma; başka modül; WP-E dikey-satır/refs-free/template-level yapısını bozma.
DOĞRULA (E2): frontend/Diten.Web.Tests baseline-diff yeşil; 3 rötuş uygulandı; git diff yeni CSS/style-değeri yok + 0 yeni vendor; WP-E yapısı korundu (payload refs-free + template-level Moderator/ForWhom değişmedi). Ayrı commit. §22 TÜRKÇE. K13 — CT bağımsız doğrular.

Durma: TaskCenter buton/flex mevcut class'la olmuyor + yeni CSS gerekiyorsa DUR+raporla; backend sözleşmesi farklıysa; kapsam KnowledgeConcepts dışına taşarsa; CRM paralel dosya çakışması → DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2, hafif) · satır-buton kısmı WP-G ile superseded**
```text
Commit: 6691791f · Agent: PASS (owner dispatch, 137/137) · CT: kapsam=template-form.js tek dosya (doğrulandı); branch-header rötuşları (name flex-grow + N-steps sağda) taşınır; satır-buton stili WP-G'de .diten-checkitem'e devrolur → tam build-doğrulama WP-G'ye katlandı.
```
- ✅ Scope: yalnız `template-form.js` (git show --stat). Backend/başka modül yok.
- ↪ Satırlar WP-G'de paylaşılan `.diten-checkitem` bileşenine geçecek (owner "aynı checklistteki gibi olsun" — boyut/hover/drag) → WP-F row-buton tweakleri orada yenilenecek; branch-header (name flex-grow, N-steps sağ) korunur.

## Kalan (bu WP dışı)
- A2d manuel test → A3→A8 → sync/PR. · Faz-2 position lookup.
