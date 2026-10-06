# WORK PACKAGE — WP-SEG-DETAILS5 · Üye tablosu rötuşları (draft preview + resolve) (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`a18c4182` üstü). **Yalnız frontend (`details.js` + `Details.cshtml` + gerekirse resx).** Owner 5 madde (draft preview tablosu).

## Maddeler
1. **Tarih formatı** — "9/17/2026, 10:10:06 AM" gibi (mockup). Şu an locale'e bağlı. → `new Date().toLocaleString('en-US')` (hem draft preview hem active resolve "resolved at").
2. **Verdict / Source / Reasons kolonları** — DRAFT preview'da BOŞ (`/preview` verdict taşımaz). → **draft tablosunda bu 3 kolonu KALDIR** (thead + satır). **Active resolve tablosunda DOLU → KALIR** (gerçek `/resolve` verdict/source/reason).
3. **Display name altındaki GUID** — draft preview'da subjectId GUID ("006400df-…") satırı. → **KALDIR** (isim yeterli). (Active'de de subjectId alt-satırı kaldırılabilir — isim + specialty/workplace yeterli.)
4. **Specialty · Workplace tek kolon → İKİ kolon** — `subjectSecondaryLabel` (SEG-F) `"Nephrology · Marmara Univ. Hospital"` birleşik. → frontend `split(' · ')` ile **Specialty | Workplace** ayrı kolon (her iki tablo). Ayraç yoksa: tek parça Specialty kolonuna, Workplace boş ("—").
5. **Footer + "Open the full result"** — "Showing the first N of M members. Nothing here is stored — the result is recomputed on every call." + **"Open the full result"** linki. Draft ve active ikisinde. **"Open the full result"** = mevcut endpoint'i (draft `/preview` / active `/resolve`) **yüksek limit** (cap'e kadar, ör. limit=SegmentContractLimits sample cap) ile yeniden çağırıp tam tabloyu göster (yeni sayfa/backend YOK; mevcut fetch limit artışı). Cap aşılırsa mevcut cap-uyarısı.

## KORU / YAPMA
- `/resolve` + `/preview` fetch/parse mantığı, secondaryLabel (SEG-F), 3-durum toggle, lifecycle, scope izolasyon, tema, app-card DEĞİŞMEZ. Active resolve verdict/source/reasons kolonları KALIR (yalnız draft'ta kaldırılır). Backend/DTO DOKUNMA (limit mevcut request alanı). segment-create.css DOKUNMA. Uydurma veri YOK. Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Draft preview tablosu: verdict/source/reasons kolon YOK, GUID satırı YOK, Specialty|Workplace ayrı kolon, tarih en-US, footer + "Open the full result". Active resolve: verdict/source/reasons KALIR, Specialty|Workplace ayrı, tarih en-US, footer + "Open the full result". "Open the full result" yüksek-limit ile tam tablo. Davranış korundu. git diff yalnız details.js/Details.cshtml(/resx).
- **E4:** draft preview → temiz tablo (ayrı kolonlar, GUID yok, en-US tarih, full-result linki).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-DETAILS5 · Üye tablosu rötuşları (draft preview + resolve) (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (a18c4182 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS5-preview-table.md · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/details.js (runResolve + runDraftPreview tablo render, resolveMembersBody, resolvedAt, footer) · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml (resolve tablo thead: segd-col-person/secondary/verdict/source/reasons).

NE (details.js + Details.cshtml + gerekirse resx):
 1) Tarih: new Date().toLocaleString('en-US') (draft + active "resolved/at").
 2) DRAFT preview tablosu: verdict/source/reasons kolonlarını KALDIR (thead + satır). ACTIVE resolve tablosu: bu 3 kolon DOLU → KALIR.
 3) Display name altındaki subjectId GUID satırını KALDIR (isim yeterli; active'de de kaldırılabilir).
 4) subjectSecondaryLabel'ı split(' · ') → Specialty | Workplace ayrı kolon (her iki tablo). Ayraç yoksa tek parça Specialty'ye, Workplace "—".
 5) Footer "Showing the first N of M members. Nothing here is stored — recomputed on every call." + "Open the full result" linki (draft+active). Open-full = mevcut endpoint'i yüksek limit (sample cap) ile yeniden çağır → tam tablo; cap aşılırsa mevcut uyarı. Yeni sayfa/backend YOK.
KORU/YAPMA: /resolve+/preview fetch/parse, secondaryLabel, 3-durum toggle, lifecycle, scope/tema/app-card DEĞİŞMEZ; active verdict/source/reasons KALIR (yalnız draft'ta kaldır); backend/DTO DOKUNMA (limit mevcut alan); segment-create.css DOKUNMA; uydurma veri yok; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); draft tablo verdict/source/reasons yok + GUID yok + Specialty|Workplace ayrı + en-US tarih + footer/full-result; active tablo verdict/source/reasons var + Specialty|Workplace ayrı + footer/full-result; davranış korundu; git diff yalnız details.js/Details.cshtml(/resx). Ayrı commit. §22 TÜRKÇE. K13.
Durma: specialty/workplace split güvenli değilse; open-full mevcut endpoint limitiyle yapılamıyorsa; backend gerekiyorsa DUR; kapsam Details dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; draft tablo (verdict/source/reasons yok, GUID yok, Specialty|Workplace ayrı, en-US, footer+full-result); active tablo (verdict/source/reasons var, ayrı kolon); open-full mevcut-endpoint yüksek-limit; davranış korundu; backend dokunulmadı.

## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: d021ee5d · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-det5-verify @d021ee5d
```
- ✅ Scope: details.js + Details.cshtml + _IndexL10n + 7 resx. Backend/segment-create.css sızıntı=0.
- ✅ 5 madde: en-US tarih ×2; draft'ta verdict/source/reasons `@if(active)` koşullu (active'de kalır); subjectId GUID satırı resolve/preview'da yok (showSubId=false; manuel'de provenance korundu); subjectSecondaryLabel split(' · ')→Specialty|Workplace; footer ShowingFirst+OpenFullResult (full=limit 1000) + SampleCapNote.
- ✅ Korundu: /resolve+/preview fetch/parse, secondaryLabel, 3-durum toggle, lifecycle, scope/tema/app-card, excluded+manuel tablo.
- ✅ Build+test (CT izole, Release, GERÇEK): Diten.Web.Tests 137/0.
