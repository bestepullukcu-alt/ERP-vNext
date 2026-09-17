# WORK PACKAGE — WP-FREQ-DET-P · Resolve: kişi (contact) hedefinde aktif segment üyeliğini otomatik türet (backend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`399ceacc` üstü). **Kullanıcı kararı (A):** Çözümleme'de bir DOKTOR/kişi seçince, motor kişinin **aktif segment üyeliklerini** kendisi türetip o segment politikalarını da eşleştirsin. **Backend** (resolver + engine + DI + test). **Frontend DEĞİŞMEZ** — targetType=contact+targetId zaten gönderiliyor; türetme server-side.

> **FAZ 1 (bu WP):** contact → aktif **segment** üyeliği türetme (kullanıcının asıl ihtiyacı). **FAZ 2 (ayrı WP, ileride):** contact → territory-node / account / campaign türetme (link/assignment okuması gerektirir; Contact entity'de doğrudan alan yok). Bu WP yalnız FAZ 1.

## Mevcut pipeline (CT çıkardı)
- `ResolveVisitFrequencyPolicyHandler` → `IVisitFrequencyPolicyResolver.ResolveAsync(request)`.
- `VisitFrequencyPolicyResolver.ResolveAsync` (IVisitFrequencyPolicyResolver.cs:31): `targetIds = {TargetId} + context ids` → `_repository.ListActiveByTargetsAsync(tenantId, targetIds)` → `VisitFrequencyResolveEngine.Resolve(request, candidates, now)`.
- `Engine.Resolve` (VisitFrequencyResolveEngine.cs): `BuildAcceptedTargets` (primary + tekil context id'ler) + `Eliminate` (satır 178: `policy.SegmentId is {} sid && request.SegmentId != sid → SegmentContextMissing`). **Tekil `request.SegmentId`** — çok-segment tutamaz.
- **Üyelik altyapısı hazır:** `ISegmentMembershipReader.IsMemberAsync(segmentId, subjectType, subjectId, effectiveAt)` (crm.segment.resolve arkasında, PII-güvenli; taslak/etkisiz segment → Unknown = üye değil). `ISegmentRepository.ListAsync(tenantId)` tüm segmentler (filtre: `IsActive()` + `SubjectType`). Segment.SubjectType default `SegmentSubjectTypes.Contact`.

## NE (backend; FAZ 1 = contact→segment)
1. **Bağlam türetici** — `VisitFrequencyPolicyResolver.ResolveAsync` içinde, `FrequencyTargetType.Normalize(request.TargetType) == contact` iken:
   - `ISegmentRepository.ListAsync(tenantId)` → `IsActive()` **ve** `SubjectType == SegmentSubjectTypes.Contact` olanlar.
   - Her biri için `ISegmentMembershipReader.IsMemberAsync(seg.Id, SegmentSubjectTypes.Contact, request.TargetId, effectiveAt)` → verdict **member** olanların `seg.Id`'leri = `derivedSegmentIds`.
   - effectiveAt = request.EffectiveAt ?? now (engine ile aynı `now`).
   - **Perf/guard:** aktif segment sayısı üzerinde makul üst-sınır (ör. `MaxSegmentsToProbe = 200`); aşılırsa türetmeyi durdur (veya sınırla) + not. Üyelik "Unknown/not member" ise atla (taslak segment doğal olarak elenir).
2. **targetIds'e ekle:** `derivedSegmentIds` → `targetIds` (böylece `ListActiveByTargetsAsync` segment politikalarını da getirir). (`AddId` reuse; liste zaten çoklu.)
3. **Engine'i set'e genelleştir (geriye-uyumlu):** `Engine.Resolve`'a türetilmiş bağlam SET'i geçir (yeni `ResolveContext`/parametre: `IReadOnlySet<Guid> SegmentIds` = `{request.SegmentId (varsa)} ∪ derivedSegmentIds`).
   - `BuildAcceptedTargets`: tekil `request.SegmentId` yerine set'teki tüm segment id'lerini `(segment, id)` olarak ekle.
   - `Eliminate` SegmentContextMissing: `policy.SegmentId is {} sid && !contextSegmentIds.Contains(sid)` (tekil `request.SegmentId != sid` yerine).
   - **Geriye-uyum:** türetme yoksa `contextSegmentIds = { request.SegmentId }` (tek eleman) → mevcut davranış BİREBİR aynı. Diğer scope alanları (Campaign/Territory/Brand/Product) FAZ 1'de tekil KALIR (değişmez).
4. **DI wiring:** `VisitFrequencyPolicyResolver`'a `ISegmentRepository` + `ISegmentMembershipReader` inject (constructor + DI kaydı). Engine imzası değişirse **tüm çağıranları** güncelle (resolver + `GetVisitFrequencyPolicyAnalysisHandler` engine/resolver kullanımı + varsa MOD-0151 FU09B tüketicisi).
5. **Testler (Application.Tests):** (a) contact aktif segment üyesi + segment politikası → resolved (o politika seçilir); (b) contact üye değil → unknown; (c) taslak segment → üye değil (unknown, IsMemberAsync Unknown); (d) contact iki aktif segmentte + iki segment politikası → ikisi de aday, deterministik kazanan; (e) mevcut tekil-segment senaryo testleri YEŞİL kalır (geriye-uyum).

## KORU / YAPMA
- Frontend DEĞİŞMEZ (resolve.js/_Resolve.cshtml/Index dokunma). Engine determinizmi + tie-break sırası + FrequencyStatus sözleşmesi KORUNUR (unknown≠default; uydurma frekans yok). Segment üyeliği YALNIZ `ISegmentMembershipReader` üzerinden okunur (PII sınırı; kendi sorgunu yazma). Taslak/etkisiz segment üye SAYILMAZ (IsMemberAsync Unknown). Tekil-context çağıranlar (mevcut segment/campaign senaryoları, MOD-0151 tüketicisi) BİREBİR korunur (set default tek-eleman). FAZ 2 (territory/account/campaign türetme) BU WP'DE YOK. Tenant izolasyonu (her okuma tenantId). Engine'e yeni bağımlılık (repo/reader) ENJEKTE ETME — türetme resolver'da; engine saf kalır (yalnız set parametresi alır).
- **DUR koşulları:** (a) `ISegmentMembershipReader.IsMemberAsync` in-process çağrıda permission/claim zorluyorsa (VFP-resolve kullanıcısı crm.segment.resolve taşımıyorsa) → DUR+raporla (permission modeli kararı gerek). (b) Segment.SubjectType contact-target için "contact" değilse (ör. doctor/specialty subtype) ve üyelik hep mismatch dönüyorsa → DUR+raporla (subjectType eşleme kararı). (c) Engine imza değişikliği beklenenden çok çağıranı kırıyorsa → DUR+raporla.

## Acceptance
- **E2:** CrmService.Application.Tests yeşil (yeni testler dahil; PII order-flake hariç) + Diten.Web.Tests 137/0 (frontend değişmedi). git diff: resolver + engine + DI + yeni testler (+ engine çağıranları). Frontend diff YOK.
- **E4:** Çözümleme'de "Bir doktor" + Bülent Akgül (ALMIBA **aktif** iken) → segmentin kazanan politikası ("Ayda 2 ziyaret") + diğer segment kuralları aday olarak; ALMIBA taslakken → üye sayılmaz (bilgi). **Backend → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-FREQ-DET-P · Resolve contact hedefinde aktif segment üyeliği türetme (MOD-0165-FU03, backend, FAZ 1)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 399ceacc üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-P-resolve-contact-segment-derive.md · services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitFrequencyPolicy/Resolve/{IVisitFrequencyPolicyResolver.cs, VisitFrequencyResolveEngine.cs, VisitFrequencyResolveContracts.cs} · Queries/VisitFrequencyPolicyQueries.cs (ResolveVisitFrequencyPolicyQuery) · Features/Segmentation/Resolution/{ISegmentMembershipReader.cs, SegmentMembershipReader.cs} · Domain/Repositories/ISegmentRepository.cs · Domain/Entities/Segment.cs (SubjectType/IsActive) · SegmentSubjectTypes · GetVisitFrequencyPolicyAnalysisHandler.cs (engine çağıranı).

NE (backend; FAZ 1 = contact→segment; frontend DEĞİŞMEZ):
 1) VisitFrequencyPolicyResolver.ResolveAsync: targetType normalize contact ise → ISegmentRepository.ListAsync → IsActive() && SubjectType==SegmentSubjectTypes.Contact → her biri IsMemberAsync(seg.Id, SegmentSubjectTypes.Contact, request.TargetId, effectiveAt(=EffectiveAt??now)) member olanlar = derivedSegmentIds. Perf guard MaxSegmentsToProbe=200.
 2) derivedSegmentIds → targetIds (ListActiveByTargetsAsync segment politikalarını da getirsin).
 3) Engine.Resolve'a türetilmiş SegmentIds SET'i geçir (= {request.SegmentId varsa} ∪ derivedSegmentIds). BuildAcceptedTargets set'teki tüm (segment,id)'leri ekler; Eliminate SegmentContextMissing → policy.SegmentId is {} sid && !contextSegmentIds.Contains(sid). Türetme yoksa set=tek eleman (geriye-uyum BİREBİR). Diğer scope alanları tekil kalır.
 4) DI: resolver'a ISegmentRepository + ISegmentMembershipReader inject + DI kaydı. Engine imzası değişirse TÜM çağıranları güncelle (resolver + analysis handler + MOD-0151 tüketicisi varsa).
 5) Testler: contact-üye→resolved; üye-değil→unknown; taslak→üye değil; çok-segment→çoklu aday deterministik; mevcut tekil-segment testleri YEŞİL.
KORU/YAPMA: frontend DEĞİŞMEZ; engine determinizm/tie-break/FrequencyStatus korunur (uydurma frekans yok); segment üyeliği yalnız ISegmentMembershipReader (PII, kendi sorgun yok); taslak/etkisiz üye değil; tekil-context çağıranlar birebir korunur (set default tek); FAZ 2 (territory/account/campaign) YOK; tenant izolasyon; engine saf kalır (repo/reader enjekte etme — türetme resolver'da, engine yalnız set alır).
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/src/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo (yeni testler dahil yeşil; PII order-flake hariç) + frontend etkilenmedi. Ayrı commit ("feat(freq): WP-FREQ-DET-P — resolve contact hedefinde aktif segment üyeliği türetme (FAZ 1) (MOD-0165-FU03)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
DUR: (a) IsMemberAsync in-process permission zorluyorsa (VFP-resolve kullanıcısı crm.segment.resolve yoksa); (b) Segment.SubjectType contact-target'a uymuyorsa (hep mismatch); (c) engine imza değişikliği çok çağıranı kırıyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: a3aa8cf5 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detp-verify @a3aa8cf5
```
- ✅ **Kapsam (4 dosya):** DependencyInjection.cs (+3) + IVisitFrequencyPolicyResolver.cs (+76) + VisitFrequencyResolveEngine.cs (+52) + yeni test VisitFrequencyPolicyContactSegmentResolveTests.cs (+272). **Frontend diff YOK.**
- ✅ **Resolver:** targetType==contact iken `_segments.ListAsync` → `IsActive() && SubjectType==Contact` → `.Take(200)` → `IsMemberAsync(seg.Id, Contact, TargetId, EffectiveAt??now)` → `verdict.IsMember` olanlar `segmentContext`+`targetIds`'e. candidates yükleme (satır 89) türetmeden SONRA. `now` bir kez.
- ✅ **Engine SAF (repo/reader enjekte edilmedi):** opsiyonel `IReadOnlySet<Guid>? contextSegmentIds`; `ResolveSegmentContext` verilmezse `{request.SegmentId}` → **geriye-uyum birebir**; `BuildAcceptedTargets` set'teki tüm (segment,id); `Eliminate` `!segmentContext.Contains(sid)`. Diğer scope (Campaign/Territory/Brand/Product/Concept/Audience) tekil kaldı.
- ✅ **DI:** ISegmentRepository? + ISegmentMembershipReader? opsiyonel ctor param (auto-inject); mevcut `(tenant, repo)` çağıranlar birebir tekil-context korur. Engine tek çağıran (resolver); analysis handler + MOD-0151 arayüzü kullandığından etkilenmedi.
- ✅ **KORU=0:** determinizm/tie-break/FrequencyStatus korundu; üyelik yalnız ISegmentMembershipReader (PII); taslak/etkisiz segment probe edilmez/üye değil (test `Draft_Segment_Is_Never_Probed`); tenant izolasyon.
- ✅ **Build+test (CT izole, Release):** Application.Tests **1792/0/5** (PII order-flake tetiklenmedi); yeni testler **6/6**.
- ⏳ E4: "Bir doktor" + Bülent (ALMIBA **AKTİF** iken) → segment kazanan politikası. **Backend → FLEET RESTART. ALMIBA taslak kalırsa üye sayılmaz (tasarım).**

**DET-P KOMPLE (FAZ 1: contact→segment türetme). FAZ 2 (territory/account/campaign türetme) ileride.**
```
