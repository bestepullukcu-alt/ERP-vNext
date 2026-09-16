# WORK PACKAGE — WP-SEG-C · Segment draft-rule PREVIEW endpoint (backend, canlı reach)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio`. **Backend (CrmService)**. Owner: **biz**. SEG-A canlı reach rail'in (toplam sayı + koşul-koşul funnel + sample members) veri kaynağı. Mevcut resolution motorunu **draft-rule** ile sarar. **Paketli; dispatch owner'da.**

## Ölçülmüş girdi (CT)
- Mevcut resolution motoru: `Features/Segmentation/Resolution/SegmentMembershipResolver.cs` (+ `SegmentCriteriaEvaluator.cs`, `ISegmentMembershipReader`) — kayıtlı segment için kriter→sorgu→üye çözer. `ResolveSegmentMembershipHandler` bunu `segmentId` ile çağırıyor.
- **Eksik:** kaydedilmemiş (draft) kural için count/preview YOK. `/resolve` + `/membership/evaluate` **segmentId** (kayıtlı) ister.
- PII: member identity crm.segment.resolve iznine bağlı (controller notu).

## Kapsam (backend)
1. **Yeni endpoint** `POST /api/crm/segments/preview` (segmentId YOK) — body = **draft rule**: `{ subjectType, matchMode, criteria[] (SegmentCriteriaNode listesi, aynı şekil), effectiveAt? }`.
2. **Döndür:** `{ totalCount, conditionCounts:[{nodeId, count}] ("N match this alone" = o tek predicate'in count'u), sampleMembers:[{subjectId, displayName, specialty/label, workplace}] (limit ~50) }`.
3. **Reuse:** `SegmentMembershipResolver` (draft rule ile ResolveAsync/Count); toplam = draft kriterle count; conditionCounts = her predicate'i tek-koşullu kuralla ayrı count; sample = limitli resolve.
4. **Guard:** `[HasPermission("crm.segment.resolve")]` (member sayısı/kimliği PII); tenant-scoped; draft rule doğrulama (attribute-catalog + limitler, mevcut create-validation deseni).
5. **Performans:** count-only (üye materialize etme); conditionCounts MaxChildrenPerGroup ile sınırlı; sample limit. (Debounce frontend'de.)

## YAPMA
- `/resolve` `/membership/evaluate` `/create` DEĞİŞTİR. Persist (preview hiçbir şey yazmaz). Criteria model/limit DEĞİŞTİR. Yeni resolution motoru (mevcut reuse). Başka modül.

## Acceptance
- **E2:** build temiz; unit — draft rule preview totalCount = aynı kuralın kayıtlı segment /resolve count'u; conditionCounts her predicate için; sample limitli + PII-gated (crm.segment.resolve yoksa 403); boş kriter/geçersiz attribute→400; persist yok. Tam CrmService.Application.Tests baseline-diff sıfır-yeni-fail.

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-C · Segment draft-rule preview endpoint (MOD-0167-FU02, backend)
Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD> · Worktree: ana checkout
Önce oku: WP-SEG-C-backend-preview-endpoint.md · Features/Segmentation/Resolution/SegmentMembershipResolver.cs + SegmentCriteriaEvaluator.cs + ISegmentMembershipReader · Handlers/QueryHandlers/ResolveSegmentMembershipHandler.cs · Api/Controllers/CRM/SegmentsController.cs (resolve/create) · create-validation (attribute-catalog + SegmentContractLimits).
NE (backend): POST /api/crm/segments/preview (segmentId YOK), body draft rule {subjectType, matchMode, criteria[] (SegmentCriteriaNode), effectiveAt?} → {totalCount, conditionCounts[{nodeId,count}], sampleMembers[]}. SegmentMembershipResolver'ı draft rule ile reuse et (count-only + tek-koşul count + limitli sample). [HasPermission("crm.segment.resolve")], tenant-scoped, draft doğrulama (catalog+limit, create deseni). Persist YOK.
NASIL: mevcut resolver/evaluator birebir; yeni motor yazma. Query+Handler+Controller+Request/Response DTO.
YAPMA: /resolve /evaluate /create değiştir; persist; criteria model/limit değiştir; başka modül.
DOĞRULA (E2): build temiz; unit totalCount=kayıtlı /resolve count, conditionCounts, sample PII-gated (403 izinsiz), geçersiz→400, persist yok; TAM CrmService.Application.Tests baseline-diff sıfır-yeni-fail. Ayrı commit. §22 TÜRKÇE. K13.
Durma: resolver draft rule kabul etmiyorsa · PII gate uygulanamıyorsa · performans count-only yapılamıyorsa · kapsam Segmentation dışına taşarsa → DUR+raporla.
```
## §37 CT → (agent sonrası)
- İzole worktree: build + CrmService.Application.Tests baseline-diff; preview count = /resolve count; PII gate; persist yok.
