# WORK PACKAGE — WP-FREQ-DET-K · Çözümleme senaryosu kendi-kapsam boyutunu context gönderir (frontend bug-fix)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`d3deb254` üstü). **E4 bug:** Çözümleme "segment" senaryosunda tüm adaylar "devrede değil · Segment eksik" eleniyor, hiçbiri seçilmiyor. **Frontend only** (`resolve.js` runResolve query kurgusu). Backend DEĞİŞMEZ.

## Kök neden (CT doğruladı — DB + kaynak)
- Segment-hedefli politikalar `SegmentId` context alanı = `TargetId` taşıyor (editör segment hedefinde segment-context'i de dolduruyor; DB doğrulandı: 'Çözüm Test Üst' targetId=89ed2b5f · SegmentId=89ed2b5f, 4 test politikası + Test Standart aynı).
- `VisitFrequencyResolveEngine.Eliminate` (satır 178): `if (policy.SegmentId is {} sid && request.SegmentId != sid) return SegmentContextMissing;` — analog satır 173 CampaignId (CampaignContextMissing).
- Çözümleme `resolve.js:runResolve` (satır 586-594) query'de **yalnız** `targetType`+`targetId`+`includeDiagnostics`(+`effectiveAt`; territory senaryosunda `territoryNodeId`) gönderiyor. "segment" senaryosunda `segmentId` GÖNDERİLMİYOR → `request.SegmentId=null != policy.SegmentId=89ed2b5f` → hepsi elenir.
- **Kontrast:** Detay sayfası çakışanları ÇALIŞIYOR çünkü `GetVisitFrequencyPolicyAnalysisHandler` resolve isteğini politikanın KENDİ context alanlarından (SegmentId dahil) kuruyor. Çözümleme tab'ı manuel giriş olduğu için o context'i kurmuyor.
- Endpoint (`VisitFrequencyPoliciesController.Resolve`) bind ediyor: `segmentId`/`campaignId`/`territoryNodeId`/`conceptNodeId`/`audienceProfileId` (hepsi `Guid?`); Web proxy `Request.QueryString`'i aynen iletir.
- `BuildAcceptedTargets` (satır 151-155) bu context id'leri kendi targetType'ları olarak accepted-set'e ekler; Eliminate yalnızca CampaignId ve SegmentId için context-missing uygular (territory/concept/audience için Eliminate kısıtı YOK — onlar sadece targetMatched ile eşleşir).

## Fix (semantik: "X için çöz" → X'i context olarak taşı)
`resolve.js:runResolve` içinde, senaryonun `targetType`'ı kendi-kapsam boyutuysa `targetId`'yi eşleşen context param olarak DA gönder. Eşleme:
- `segment` → `segmentId`
- `campaign-target` → `campaignId`
- `territory-node` → `territoryNodeId`
- `concept-node` → `conceptNodeId`
- `audience-profile` → `audienceProfileId`

`q.set('targetId', targetId)` sonrasına (satır 588 civarı):
```js
const SCENARIO_SELF_CONTEXT = {
    'segment': 'segmentId',
    'campaign-target': 'campaignId',
    'territory-node': 'territoryNodeId',
    'concept-node': 'conceptNodeId',
    'audience-profile': 'audienceProfileId'
};
const selfCtx = SCENARIO_SELF_CONTEXT[norm(scenario.targetType)];
if (selfCtx) q.set(selfCtx, targetId);
```
(`norm` + `q` mevcut; `contact`/`account`/`account-contact-link` map dışı → değişiklik yok, mevcut davranış korunur. `contact-territory` senaryosu `territoryNodeId=terNode` context'ini zaten gönderiyor — targetType='contact' olduğu için map dışı, çakışma yok.)

## Neden bu doğru (semantik)
Bir segment hedefi için "frekans ne?" sorusu, segmentin kendisini context olarak taşımalı: hem segment'e-hedeflenen politika (targetMatched) hem segment-kapsamlı politika (SegmentId context) uygulanır. Farklı bir segment Y'ye (SegmentId=Y) kapsamlı politika `request.SegmentId=X != Y` ile doğru şekilde elenir. Bu, detay-analiz handler'ının davranışının aynısı (kanıt: detay çakışanları çalışıyor).

## KORU / YAPMA
- **Backend DEĞİŞMEZ** (endpoint + engine zaten context param'lerini bind/handle ediyor). Liste/editör/detay(Details.cshtml/details.js)/_Resolve.cshtml/Segment/resolve engine/CRUD DOKUNMA — **yalnız `resolve.js`** (runResolve query kurgusu, ~satır 586-594). targetId/context/IncludeDiagnostics sözleşmesi + senaryo→targetType eşlemesi (SCENARIOS) KORUNUR. Yeni context değeri uydurma YOK — id yalnız kullanıcının seçtiği `targetId`. L10n/css DEĞİŞMEZ.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff **yalnız resolve.js**. Backend/liste/editör/detay/_Resolve/css/resx diff YOK.
- **E4:** Segment senaryosu (ALMIBA segmenti + 4 test politikası: prio 100/500/500/900) → CEVAP "Ayda N ziyaret" + en dar (prio 100 Üst) seçilir; diğerleri aday/tarih-dışı status'leriyle "DENK GELEN DİĞER KURALLAR"; hiçbiri artık "Segment eksik" ile elenmiyor. Campaign-target/territory-node/concept-node/audience-profile senaryolarında da kendi kapsamına uygun politikalar eşleşir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-K · Çözümleme senaryosu kendi-kapsam boyutunu context gönderir (MOD-0165-FU03, frontend bug-fix)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: d3deb254 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-K-resolve-self-context.md · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/resolve.js (SCENARIOS ~289-298, runResolve ~580-606, query kurgusu ~586-594; norm/q mevcut) · (referans) services/.../Resolve/VisitFrequencyResolveEngine.cs Eliminate(satır 173 CampaignId / 178 SegmentId) + BuildAcceptedTargets(151-155) · VisitFrequencyPoliciesController.cs Resolve (segmentId/campaignId/territoryNodeId/conceptNodeId/audienceProfileId bind).

KÖK NEDEN: segment-hedefli politikalar SegmentId context=targetId taşıyor; Eliminate policy.SegmentId!=request.SegmentId → SegmentContextMissing. Çözümleme "segment" senaryosu query'de segmentId göndermiyor → hepsi eleniyor. Detay-analiz handler politikanın kendi context'ini gönderdiği için ORADA çalışıyor.

NE (frontend; backend DEĞİŞMEZ; YALNIZ resolve.js runResolve):
 runResolve içinde q.set('targetId', targetId) sonrasına: senaryonun targetType'ı kendi-kapsam boyutuysa targetId'yi eşleşen context param olarak DA gönder:
   segment→segmentId, campaign-target→campaignId, territory-node→territoryNodeId, concept-node→conceptNodeId, audience-profile→audienceProfileId.
 SCENARIO_SELF_CONTEXT haritası + selfCtx = map[norm(scenario.targetType)]; if(selfCtx) q.set(selfCtx, targetId). (contact/account/account-contact-link map dışı → değişmez; contact-territory territoryNodeId=terNode'u zaten gönderiyor, çakışma yok.)
KORU/YAPMA: backend DEĞİŞMEZ; liste/editör/detay(Details.cshtml/details.js)/_Resolve.cshtml/Segment/resolve engine/CRUD/css/resx DOKUNMA (yalnız resolve.js); targetId/context/IncludeDiagnostics + SCENARIOS eşlemesi KORU; uydurma context id YOK (yalnız kullanıcının targetId'si).
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff YALNIZ resolve.js; backend/liste/editör/detay/_Resolve/css/resx diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: fix runResolve dışına taşarsa; SCENARIOS/context sözleşmesini bozuyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: b6222293 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detkj-verify @0b55ef25
```
- ✅ **Kapsam:** yalnız `resolve.js` (+13). git diff: `runResolve` içinde `q.set('targetId',...)` sonrasına `SCENARIO_SELF_CONTEXT` (segment→segmentId, campaign-target→campaignId, territory-node→territoryNodeId, concept-node→conceptNodeId, audience-profile→audienceProfileId) + `selfCtx=map[norm(targetType)]; if(selfCtx) q.set(selfCtx,targetId)`. **KORU=0** (backend/liste/editör/detay/_Resolve.cshtml/Segment/resolve engine/CRUD/css/resx dokunulmadı; SCENARIOS/targetId/IncludeDiagnostics korundu; contact/account/account-contact-link map dışı → değişmedi; contact-territory zaten territoryNodeId gönderiyor, çakışma yok).
- ✅ **Uydurma yok:** gönderilen context id yalnız kullanıcının seçtiği `targetId`.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ E4: segment senaryosu (ALMIBA + 4 test politikası prio 100/500/500/900) → CEVAP "Ayda N ziyaret" + en dar (100) seçilir; hiçbiri artık "Segment eksik" ile elenmez. **wwwroot JS → Ctrl+F5 yeter (restart gerekmez).**

**DET-K KOMPLE (Çözümleme segment-context bug çözüldü).**
```
