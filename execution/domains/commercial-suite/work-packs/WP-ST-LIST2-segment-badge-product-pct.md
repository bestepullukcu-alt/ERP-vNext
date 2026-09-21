# WORK PACKAGE — WP-ST-LIST2 · Liste SEGMENT badge + ÜRÜN "N · %" (mockup rötuşu)

> **CT (SoR).** MOD-0167-FU04. Branch `feature/crm-scmm-studio` (`9aba3576` üstü). **E4 rötuşu:** liste SEGMENT kolonu düz "1 / contact" yerine mockup gibi **renkli badge** (kişi=mavi / kurum=turuncu) + sayı; ÜRÜN kolonu "1 / 0 SKU" yerine mockup gibi **"N · %"** (ürün sayısı · ürün-ağırlığı toplamı). **Frontend** (`index.js` + resx) **+ küçük additive backend** (list DTO + mapper: ürün-ağırlığı toplamı).

## Kanıt
- Mockup Liste SEGMENT: `[kişi] 1` / `[kurum] 2` (renkli badge + sayı). ÜRÜN: `3 · 100%` (ProductLineCount · Σ ağırlık%). Detay mockup ürün satırları 50%/30%/20% = `StrategyTemplateProductLine.LineWeightPercentage` (Σ=100).
- Mevcut list DTO: SegmentBindingCount + subjectType (contact/account) + ProductLineCount + SkuAllocationCount (yüzde-toplamı YOK). **ÜRÜN Σ% için mapper toplamı gerekir** (WP-ST-LIST'te bilinçle hardcode edilmemişti).
- `LineWeightPercentage` (decimal?, ProductLine) = ürün-satırı ağırlığı; template'in ürün satırları arasında Σ (Detay'daki 50/30/20). SKU-içi 100 ayrı (line-level ValidateLineTotal) — bu KOLON ürün-ağırlığı Σ'sını gösterir.

## NE
1. **Backend (additive, list DTO + mapper):** list row DTO'ya `ProductAllocationTotalPercentage` (decimal?) ekle = `Σ(ProductLines.LineWeightPercentage ?? 0)` (satır yoksa/hepsi null ise null). Mapper'da hesapla. Additive — mevcut alanlar/kontrat bozulmaz. (Detay/Create/Update DTO'ları DOKUNMA — yalnız list row.)
2. **Frontend index.js — SEGMENT renderer:** `[badge(subjectLabel, tone)] {SegmentBindingCount}` — subjectType contact→"kişi"/kurum→"kurum" (L10n); tone kişi→`primary` (mavi) / kurum→`warning` (turuncu) (mockup renkleri; bg-label-*). Sayı badge yanında.
3. **Frontend index.js — ÜRÜN renderer:** `{ProductLineCount} · {ProductAllocationTotalPercentage}%` — toplam pozitif sayı ise "N · {yuvarlanmış}%" (ör. "3 · 100%"); null/0 ise yalnız "N" (yanıltıcı "0%" YAZMA). "N SKU" alt-satırı kaldırılır (mockup ÜRÜN tek satır "N · %").
4. **resx (7 dil):** subject etiketleri (SubjectType_contact="kişi"/SubjectType_account="kurum" — yoksa ekle; mevcut varsa reuse). PascalCase alias köprüsü korunur.

## KORU / YAPMA
- Yalnız list DTO + mapper (backend) + index.js + resx (frontend). Detay/Create/Edit/form.js/details.js + Create/Update/Detail DTO'ları DOKUNMA. WP-ST-LIST diğer kolonları (OYUN/KAPSAM/İÇERİK/DURUM/GÜNCELLENDİ) + KAPSAM scope-options map + ülke filtresi + SaveView/actions/colReorder KORUNUR. Kontrat/flag bozma. Uydurma % YOK (gerçek Σ LineWeightPercentage; yoksa gösterme). Tenant izolasyon (mapper saf hesap). Tema/L10n köprüsü.

## Acceptance
- **E2:** CrmService.Application.Tests yeşil (mapper additive; mevcut 1830/0/5 korunur) + Diten.Web.Tests 201/0. git diff: StrategyTemplateModels(list row) + mapper + index.js + resx. Diğer diff yok.
- **E4:** SEGMENT renkli badge (kişi mavi/kurum turuncu) + sayı; ÜRÜN "N · %" (ağırlıklı template'lerde 100%, ağırlıksızda yalnız sayı). **Razor değişmiyorsa Ctrl+F5; mapper backend → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-LIST2 · Liste SEGMENT badge + ÜRÜN "N · %" (MOD-0167-FU04)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 9aba3576 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-LIST2-segment-badge-product-pct.md · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/index.js (SEGMENT/ÜRÜN renderer, WP-ST-LIST) · services/Diten.CrmService/src/Diten.CrmService.Application/Features/StrategyTemplate/StrategyTemplateModels.cs (list row DTO) + CycleCapacityMapper deseni değil → StrategyTemplate list mapper (ListStrategyTemplatesHandler/Mapper) · Domain/Entities/StrategyTemplate.cs (StrategyTemplateProductLine.LineWeightPercentage). Mockup: /c/tmp/mockup-strategy.html Liste.

NE:
 1) Backend additive: list row DTO'ya ProductAllocationTotalPercentage (decimal?) = Σ(ProductLines.LineWeightPercentage ?? 0) (satır yok/hepsi null → null); mapper'da hesapla. Yalnız list row (Detay/Create/Update DTO dokunma).
 2) index.js SEGMENT: badge(subjectLabel, tone) + SegmentBindingCount; contact→"kişi"/primary(mavi), account→"kurum"/warning(turuncu) (L10n).
 3) index.js ÜRÜN: "{ProductLineCount} · {total}%" (total>0 ise yuvarlanmış; null/0 ise yalnız sayı, "0%" YAZMA). "N SKU" alt-satırı kaldır.
 4) resx 7 dil: SubjectType_contact="kişi"/SubjectType_account="kurum" (yoksa ekle). PascalCase alias köprüsü.
KORU/YAPMA: yalnız list DTO+mapper+index.js+resx; Detay/Create/Edit/form.js/details.js + Create/Update/Detail DTO DOKUNMA; WP-ST-LIST diğer kolonlar/KAPSAM-map/ülke-filtresi/SaveView/actions KORU; uydurma % yok (gerçek Σ, yoksa gösterme); tenant izolasyon; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo (mevcut yeşil, PII flake hariç) + dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo (201/0). git diff: StrategyTemplateModels+mapper+index.js+resx. Ayrı commit ("feat(strategy): WP-ST-LIST2 — liste SEGMENT badge (kişi/kurum) + ÜRÜN N·% ağırlık toplamı (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: LineWeightPercentage toplamı beklenen semantik değilse (SKU-içi 100 ile karışıyorsa); değişiklik kapsam dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-21) → **ACCEPTED (E2)**
```
Commit: 999a1721 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stlist2-verify @999a1721
```
- ✅ **Kapsam:** StrategyTemplateModels(list row +ProductAllocationTotalPercentage) + StrategyTemplateMapper(ToListItem hesap) + index.js + _IndexL10n.cshtml + 7 resx. **Detay/Create/Edit + form.js/details.js + Detail/Create/Update DTO dokunulmadı** ✓.
- ✅ **ÜRÜN %:** `ProductLines.Any(LineWeightPercentage.HasValue) ? Σ(LineWeightPercentage ?? 0) : null` — ürün-satırı ağırlığı (SKU-içi 100 ile KARIŞMADI). Frontend: total>0→"N · %", null/0→yalnız sayı ("0%" yok). Eski "N SKU" alt-satırı kaldırıldı.
- ✅ **SEGMENT badge:** contact→"kişi"/primary(mavi), account→"kurum"/warning(turuncu) + SegmentBindingCount; bilinmeyen tip→yalnız sayı (uydurma badge yok). SubjectTypeContact/Account 7 resx + _IndexL10n köprü.
- ✅ **KORU=0:** WP-ST-LIST diğer kolonlar/KAPSAM scope-options map/ülke filtresi/SaveView/actions korundu; kontrat additive.
- ✅ **Build+test (CT izole, Release):** Application.Tests **1830/0/5** + Diten.Web.Tests **201/0**.
- ⏳ E4: SEGMENT badge + ÜRÜN N·%. **Mapper backend → FLEET RESTART.**

**WP-ST-LIST2 KOMPLE.**
```
