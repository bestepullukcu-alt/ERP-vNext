# WORK PACKAGE — WP-SEG-DETAILS6 · Timeline kullanıcı adı (CreatedBy/ActivatedBy/UpdatedBy GUID → display name)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`1273543a` üstü). **Backend (CrmService) + frontend (Details.cshtml timeline).** Owner kararı (AskUserQuestion): "kullanıcı adı/email göster". Timeline'da CreatedBy/ActivatedBy/UpdatedBy ham GUID.

## Ölçülmüş girdi (CT)
- Details timeline (Details.cshtml): `@segment.CreatedBy` / `ActivatedBy` / `UpdatedBy` = ham user GUID (" · " + GUID).
- **Auth `InternalUsersController` HAZIR:** `GET internal/users/display-names?tenantId={guid}&ids=guid,guid` → **id + display name** (bulk, S2S internal; **email YOK — kasıtlı PII-minimal**). "id ve display name — tüm kontrat bu." → email değil display-name kullanacağız (mockup "S. Aydın" = display name; endpoint'i genişletme).
- Masking: Platform audit `MaskEmail/MaskDisplayName` audit'e özel — segment için gerekmez (display-name zaten PII-minimal).
- CrmService cross-service pattern mevcut (MDM validator'ları gateway üzerinden, fail-closed).

## Kapsam
**1) Backend (CrmService — user display-name reader, fail-closed):**
- CrmService'te Auth `internal/users/display-names` çağıran bir reader (gateway/S2S, tenant-scoped). Segment detail handler CreatedBy/ActivatedBy/UpdatedBy id'lerini (boş olmayan, benzersiz) toplar → tek bulk çağrı → id→displayName map.
- Segment detail DTO'ya **additive** `CreatedByName`/`ActivatedByName`/`UpdatedByName` (nullable) — mevcut *By alanları KALIR.
- **Fail-closed:** Auth erişilemez/timeout/çözülemeyen id → name null (frontend GUID YERİNE gizler/"—"; uydurma yok). PII/S2S auth engeliyse DUR+raporla.
**2) Frontend (Details.cshtml timeline):**
- "· @segment.CreatedBy" → "· display name" (CreatedByName varsa); yoksa **GUID gösterme** (yalnız tarih). ActivatedBy/UpdatedBy aynı.

## KORU / YAPMA
- Segment detail mevcut alanları/DTO şekli (additive *ByName). Resolution/preview/lifecycle/scope/tema/app-card DEĞİŞMEZ. Ekstra ağır çağrı YOK (tek bulk display-names, ≤3 id). Auth internal endpoint'i GENİŞLETME (email ekleme). segment-create.css/details.js DOKUNMA (yalnız timeline Razor + backend). Uydurma isim YOK. Başka modül.

## Acceptance
- **E2:** CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail. Segment detail DTO additive *ByName (mevcut *By korundu); reader tek bulk display-names, fail-closed (Auth yok → name null → timeline GUID göstermez); timeline display name gösterir, çözülemezse yalnız tarih. Ekstra per-id çağrı yok.
- **E4:** timeline "Created 2026-09-16 21:18 · S. Aydın" (GUID yok).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-DETAILS6 · Timeline kullanıcı adı (GUID→display name) (MOD-0167-FU02, backend+frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (1273543a üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS6-timeline-user.md · services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalUsersController.cs (GET internal/users/display-names?tenantId&ids — id+displayName, email YOK) · services/Diten.CrmService/src/Diten.CrmService.Infrastructure/StrategyTemplate/MdmStrategyTemplateReferenceValidator.cs (cross-service gateway/fail-closed DESEN) · Segment detail handler + SegmentModels (SegmentDetailDto CreatedBy/ActivatedBy/UpdatedBy) · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml (timeline satırları ~347-380).

NE:
 Backend (CrmService, fail-closed, tek bulk):
 - Auth internal/users/display-names çağıran reader (gateway/S2S, tenant-scoped) — MDM validator desenini izle. Segment detail handler CreatedBy/ActivatedBy/UpdatedBy (boş-olmayan benzersiz) id'leri topla → tek bulk çağrı → id→displayName.
 - SegmentDetailDto'ya additive nullable CreatedByName/ActivatedByName/UpdatedByName; mevcut *By alanları KALIR.
 - Fail-closed: Auth yok/timeout/çözülemeyen → name null. Ekstra per-id çağrı YOK (tek bulk).
 Frontend (Details.cshtml timeline):
 - "· @segment.CreatedBy" → CreatedByName varsa display name; yoksa GUID GÖSTERME (yalnız tarih). Activated/Updated aynı.
KORU/YAPMA: segment detail mevcut alan/şekil (additive *ByName); resolution/preview/lifecycle/scope/tema/app-card değişmez; Auth internal endpoint'i GENİŞLETME (email ekleme); ekstra ağır çağrı yok; segment-create.css/details.js DOKUNMA (yalnız timeline Razor + backend); uydurma isim yok; başka modül.
DOĞRULA (E2): TAM CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail; DTO additive *ByName (mevcut korundu); reader tek bulk + fail-closed (Auth yok→null→GUID gizli); timeline display name/tarih. Ayrı commit. §22 TÜRKÇE. K13.
Durma: CrmService→Auth internal S2S auth yapılamıyorsa (DUR+raporla, alternatif sun); display-names endpoint erişilemiyorsa; DTO additive tutulamıyorsa; kapsam Segmentation+Details timeline dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → CrmService.Application.Tests + Diten.Web.Tests baseline-diff; DTO additive *ByName (mevcut *By korundu); reader tek bulk display-names + fail-closed (Auth yok→null); timeline display name veya yalnız tarih (GUID gizli); email eklenmedi; ekstra per-id çağrı yok.
