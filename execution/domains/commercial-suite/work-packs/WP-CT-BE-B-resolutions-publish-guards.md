# WORK PACKAGE — WP-CT-BE-B · ConformanceResolutions persist + publish guard'ları + min/max bounds + skip-ahead gevşetme (backend, CrmService)

> **CT (SoR).** MOD-0162 Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup'ının **"Yok say" (ignore) kalıcılığı** + **yayın engelleyen kuralları** + **adet sınırları** + **skip-ahead=conforming** (D2 gevşek kararı) backend'i. **CrmService (Domain+Application+Persistence+Api).** BE-A (`59612323`) üstüne. Frontend YOK.

## Kanıt
- **ConceptChainTemplate** (`.../Domain/Entities/ConceptChainTemplate.cs`): Branches/OrderedConceptTypes/ModeratorRoleType/ForWhomAudienceProfileIds var; yayında **branch+omurga+moderator+forwhom donar** (Update `BranchesEqual`/`ForWhomEqual` freeze guard'ları). **Resolution alanı YOK.**
- **Handler** (`ConceptChainTemplateHandlers.cs`): branch guard (satır 52/71) her dal **≥1 adım** (draft+publish); min/max validator (satır 96) `Max null|≥1|≥Min`; **tip-tekrar guard (satır 84 `typesInBranch.Add`) mevcut** (dalda tip tekrar edemez). Publish guard'ı (satır 285) yalnız **overlap** kontrol eder — **≥2 adım/dal + ≥1 kitle YOK**. Yeni-sürüm = aynı ChainCode ile yeni Create (yüksek ChainVersion).
- **Classify** (BE-A `ConceptChainConformance.cs`): şu an **adjacency** (bitişik ileri = conforming; atlamalı ileri + reverse = order). **Kullanıcı kararı: gevşek** → ileri (indexOf(first)<indexOf(second)) = conforming; yalnız reverse = order.
- **Mockup:** `res:{edgeId:'ignored'}` kalıcı; yeni sürüme **taşınır**; yayında read-only. min 0-9 (0=opsiyonel adım), max 1-9. Publish engelleyen: ≥2 tip[var] · **≥2 adım/dal** · **≥1 hedef kitle**.

## NE (CrmService; BE-A üstüne)
1. **Skip-ahead gevşetme (Classify):** `ConceptChainConformance.Classify`'da bitişiklik yerine **ileri-sıra**: `out` (biri omurgada değil) korunur; ikisi de omurgada ve `indexOf(first) < indexOf(second)` → **conforming** (atlamalı ileri dahil); `indexOf(first) > indexOf(second)` → **order** (reverse). Eşit olamaz (aynı tip tekrar etmez). BE-A adjacency testlerini **ileri-sıra**ya güncelle. ⚠ Bu, kayıtlı `IsTemplateConforming` semantiğini FU03'te değiştirir (daha çok conforming) — kabul edilen etki.
2. **ConformanceResolutions persist:** `ConceptChainTemplate`a additive `List<Guid> IgnoredNonConformingRelationshipIds` + Create/Update command'lara ekle + **class-map** (GUID subtype — [[crm-new-aggregate-classmap-guid]]). Yeni-sürüm Create'te aynen kopyalanır (frontend taşır). **Yayında donar** (published template'te değişmez — freeze guard'a ekle veya published→400).
3. **Immediate resolve endpoint:** `PUT api/crm/knowledge/concept-chain-templates/{id}/conformance-resolutions` body `{ignoredRelationshipIds[]}` — kayıtlı **draft** template'e anlık yazar (mockup "kaydedildi" hissi); **published→409** (donuk). RBAC = concept manage.
4. **Publish guard'ları (yalnız status=published):** her dal **≥2 adım** (draft ≥1 kalır); **ForWhom ≥1** (draft'ta opsiyonel kalır). Mesajlar net (hangi dal / kitle eksik).
5. **min/max bounds:** MinSelection **0–9**, MaxSelection **1–9** (+ mevcut Max≥Min). Aralık dışı → 400.
6. Tests: Classify ileri-sıra (atlamalı ileri conforming, reverse order) · resolution persist + yeni-sürüm kopyası + published-immutable · resolve endpoint draft-yazar/published-409 · publish ≥2 adım/dal + ≥1 kitle (draft serbest) · min/max bounds.

## KORU / YAPMA
- **BE-A'nın reversal + diagnostics + IsConforming tek-kaynak yapısı KORUNUR** (yalnız Classify'ın ileri-sıra kuralı değişir; reversal aynen kalır). Conformance **enforce edilmez** (D8) — resolution yalnız "ignore" kaydı, ilişki silinmez. Step modeli **tip+min/max DEĞİŞMEZ** (D1=B). Tip-tekrar guard (satır 84) korunur. Branch/omurga/moderator/forwhom freeze davranışı korunur (+resolutions da donar). Frontend YOK. TenantId JWT'den. Diğer servisler DOKUNMA.
- **DUR:** resolution'ı published'da değiştirme ihtiyacı çıkarsa (donuk olmalı); min/max 0-9 sınırı mevcut publish edilmiş bir template'i geçersiz kılıyorsa (0 kayıt → çıkmaz) → DUR+raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.CrmService/tests/... -c Release` → yeşil (baseline 1876 + yeni); CrmService.Api 0 hata. git diff: Domain (IgnoredNonConformingRelationshipIds) + Persistence (class-map) + Application (Classify ileri-sıra + Create/Update + resolve command/handler + publish guards + min/max) + Api (resolve endpoint) + BE-A testleri güncel. Reversal/diagnostics/step-model/tip-tekrar diff YOK (Classify kuralı + additive hariç).
- **E4 (smoke):** draft template → resolve endpoint ignore yazar; published → 409; publish ≥2 adım/dal + ≥1 kitle olmadan reddedilir; atlamalı-ileri ilişki artık conforming.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-CT-BE-B · ConformanceResolutions persist + publish guard'ları + min/max bounds + skip-ahead gevşetme (Diten.CrmService, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout · BE-A (59612323) üstüne

Amaç: Zincir Şablonu v2 backend'ini tamamla — "Yok say" (ignore) kalıcılığı, yayın engelleyen kuralları, adet sınırları, skip-ahead=conforming (D2 gevşek). Frontend YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-BE-B-resolutions-publish-guards.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ConceptChainTemplate.cs · .../Application/Features/Knowledge/Concept/ChainTemplate/ConceptChainTemplateHandlers.cs (branch guard 52/71, min/max 96, tip-tekrar 84, publish 285) · ConceptChainTemplateCommands.cs · .../Relationship/ConceptChainConformance.cs (BE-A Classify) · Persistence class-map · memory crm-new-aggregate-classmap-guid.

NE (CrmService; BE-A üstüne):
 1) Classify skip-ahead gevşet: out korunur; ikisi omurgada ve indexOf(first)<indexOf(second)→conforming (atlamalı ileri dahil); >→order (reverse). reversal AYNEN kalır. BE-A adjacency testlerini ileri-sıraya güncelle.
 2) ConceptChainTemplate + List<Guid> IgnoredNonConformingRelationshipIds (additive) + Create/Update + class-map (GUID subtype); yeni-sürüm Create'te kopyalanır; yayında donar.
 3) PUT api/crm/knowledge/concept-chain-templates/{id}/conformance-resolutions {ignoredRelationshipIds[]} → draft'a yazar, published→409; RBAC concept manage.
 4) Publish guard (status=published): her dal ≥2 adım (draft ≥1 kalır); ForWhom ≥1 (draft opsiyonel). Net mesaj.
 5) min/max bounds: Min 0-9, Max 1-9 (+Max≥Min); dışı→400.
KORU/YAPMA: BE-A reversal+diagnostics+IsConforming tek-kaynak KORU (yalnız Classify ileri-sıra); conformance enforce YOK (D8, resolution=ignore kaydı, ilişki silinmez); step modeli tip+min/max DEĞİŞMEZ; tip-tekrar guard(84) korunur; branch/omurga/moderator/forwhom+resolutions yayında donar; frontend YOK; TenantId JWT'den.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo → yeşil (baseline 1876+yeni); CrmService.Api 0 hata; git diff Domain+Persistence+Application+Api; reversal/diagnostics/step-model/tip-tekrar davranış diff yok. Testler: Classify ileri-sıra(atlamalı-ileri conforming, reverse order), resolution persist+yeni-sürüm-kopya+published-immutable, resolve endpoint draft/published-409, publish ≥2 adım/dal+≥1 kitle(draft serbest), min/max bounds. Ayrı commit ("feat(crm): WP-CT-BE-B — conformance resolutions persist + publish guards + min/max bounds + forward-order conformance (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: resolution published'da değişmeli çıkarsa; min/max sınırı mevcut published template'i geçersiz kılıyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-24) → **ACCEPTED (E2)**
```
Commit: ff18f3d4 (BE-A üstüne) · Agent: PASS (1888/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-beb @ff18f3d4 → 1888/0/5 (re-run; ilk koşuda 1 kırmızı = PII flake)
```
- ✅ **Kapsam (11 dosya, +541/−41):** yalnız CrmService (Domain field · Persistence class-map · Application[Classify+Create/Update+resolve+publish-guard+min/max] · Dtos/Mapper[field read] · Api). **reversal/diagnostics/tip-tekrar/branch-shape guard davranış diff YOK** (yalnız Classify kuralı + additive).
- ✅ **Classify forward-order (kod okundu):** `IndexOf(first) < IndexOf(second)` → conforming (skip-ahead dahil, "adım opsiyonel olabilir MinSelection 0"); reverse → order; out+missing korunur; **reversal (addresses/evidences swap) aynen** önce uygulanır. BE-A adjacency testleri ileri-sıraya güncellendi (R4 atlamalı-ileri artık conforming).
- ✅ **Resolutions:** `IgnoredNonConformingRelationshipIds` additive (string-Guid class-map); Create/Update taşır; **Update omit → mevcut korunur** (form silmez); cross-subject → 400; **published → 409 (donuk, freeze mesajına eklendi)**; yeni-sürüm Create'e taşınır.
- ✅ **Resolve endpoint** `PUT .../{id}/conformance-resolutions`: draft'a yazar, aynı liste idempotent, published/arşiv → 409, **ilişki değişmez (D8, testli)**.
- ✅ **Publish guard'ları (yalnız yayına geçiş):** her dal ≥2 adım (draft ≥1) + ForWhom ≥1 (draft opsiyonel); mevcut published template update'i kilitlenmez (testli). **min/max:** Min 0-9, Max 1-9 (+Max≥Min); create + branch-değişiminde.
- ✅ **Build+test:** Application.Tests **1888/0/5** (baseline 1876 + 12 yeni). Api 0 hata/0 uyarı.

### Notlar
- **⏳ RBAC tutarlılığı (küçük):** resolve endpoint `concept manage` (WP böyle dedi), Create/Update `template manage`. Tutarlılık için resolve'u da **template manage** yapmak daha doğru olabilir — kullanıcı kararı (küçük follow).
- **PII flake** (`ContactLocationPiiHardeningTests`, pre-existing, BE-B ile ilgisiz): rastgele GUID bazen telefon regex'ine benziyor; ayrı görev.

**WP-CT-BE-B KOMPLE (E2). → FAZ 1 BACKEND TAMAM (BE-A + BE-B).** Kalan: FAZ 2 frontend (FE-1..7, mockup'a dayanır) + E4 smoke + push.

