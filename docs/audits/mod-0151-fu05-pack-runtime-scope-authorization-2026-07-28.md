# MOD-0151 FU05 — Pack Runtime Scope Authorization

> **Tarih:** 2026-07-28  
> **Verdict:** **PASS** — FU05 runtime scope additive yetkilendirildi; runtime koduna dokunulmadı.

## 1. Preflight

İncelenen kaynaklar:

- `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
- `docs/audits/mod-0151-fu03-assignment-rules-preview-implementation-2026-07-28.md`
- `docs/audits/mod-0151-fu04-resource-assignments-implementation-2026-07-28.md`
- `docs/audits/mod-0151-fu02b-live-smoke-closeout-retry-2-after-status-publish-2026-07-28.md`
- Önceki FU05 orchestrator preflight authorization-gate sonucu

FU02B **72/72 PASS**, FU03 **49/49 canlı smoke PASS**, FU04 **52/52 canlı smoke PASS** olarak doğrulandı.

## 2. Authorization Blocker Summary

Pack `ready-for-dev` olmasına rağmen `runtime_code_scope` yalnız FU01–FU04'ü kapsıyor ve giriş notu account
assignment apply/history'yi kapalı tutuyordu. Orchestrator bu nedenle doğru biçimde fail-closed durdu.

## 3. Pack Frontmatter Changes

`runtime_code_scope` mevcut FU01–FU04 değerleri korunarak additive biçimde
`FU05-account-assignment-apply-history` ile genişletildi. `runtime_code_allowed: true` ve pack statüsü korunmuştur.

## 4. FU05 Authorized Scope

- `AccountTerritoryAssignment` aggregate/repository/index'leri
- Preview selected-row apply
- Effective-dated model/account history ve current query
- Conflict detection; controlled 409; reason zorunlu override
- Önceki kaydı end-date/status ile kapatıp yeni kayıt açma
- Account master'dan ayrı CoverageSummary query/read model/projection
- Apply paneli, history listesi ve conflict/override warning UI'ı
- Contract: apply/history/coverage summary true; resource true; workflow activation false
- Testler, Gateway-only smoke ve implementation evidence report

## 5. FU05 Explicit Exclusions

Account/Contact master update veya entity'lere territory alanı ekleme; resource assignment mutation; workflow
approval ve submit/approve/reject; MOD-0023 integration; evidence pack; import/export; visit/route planning/readiness;
Brand Scope; Product/Brand master; hard delete; Mongo hand-edit; RBAC seed/grant; MOD-0048 publish;
`crm.territory.delete`; `crm.micro-zone.manage`; payload `TenantId`; direct 5061 business API çağrısı.

## 6. Apply / History Policy Decisions

| Karar | FU05 politikası |
|---|---|
| Model status | Yalnız active; draft/inactive/archived/expired/soft-deleted reddedilir |
| Batch atomicity | All-or-nothing |
| Default conflict | Controlled 409; hiçbir yazma yapılmaz |
| Override | Reason zorunlu; eski kayıt silinmeden end-date/status alır, yeni kayıt açılır |
| History | Ended/expired kayıtlar görünür; future kayıt başlangıçtan önce current değildir |
| CoverageSummary | Account master'a yazılmaz; ayrı read model/query/projection |

## 7. RBAC Notes

Canonical hedefler `crm.territory.assignment.read/manage` anahtarlarıdır. Katalogda yoklarsa FU05 seed/grant
değiştirmez; **MOD-0151 FU05A — Assignment RBAC Permission Catalog Alignment** ayrı follow-up olur. Bu arada yalnız
FU05 yüzeyi için mevcut `crm.territory.model.read/manage` geçici fallback'i açıkça yetkilendirilmiştir.

## 8. Reference Data Readiness Notes

`territory-assignment-status`, `territory-assignment-source` ve `territory-conflict-policy` FU05 için required'dır.
FU02B canlı closeout target tenant'ta publish readiness'i doğrulamıştır. Runtime yine fail-closed kalır; eksik
set/value kontrollü 400 üretir. Eksik publish kodu değil, ilgili canlı smoke adımını bloklar.

## 9. FU06/FU07/FU08/FU09 Boundaries

- FU06: submit/approve/reject, workflow trace, MOD-0023 ve approval-governed activation.
- FU07: Evidence Pack ve audit/evidence export.
- FU08: import/export hardening.
- FU09: MOD-0155 visit/route readiness API'leri.

FU05 assignment apply workflow approval değildir ve bu future scope'ların hiçbirini açmaz.

## 10. Guard Checks

| Check | Result |
|---|---|
| Runtime code changed? | No |
| Backend/frontend changed? | No |
| Gateway changed? | No |
| RBAC seed changed? | No |
| MOD-0048 publish changed? | No |
| Mongo changed? | No |
| FU05 scope added to runtime_code_scope? | Yes |
| FU01–FU04 scope preserved? | Yes |
| Account master mutation allowed? | No |
| Contact mutation allowed? | No |
| Workflow approval opened? | No |
| Evidence/import/export opened? | No |
| Brand Scope opened? | No |
| Hard delete allowed? | No |
| FU06 boundary preserved? | Yes |

## 11. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | Updated | FU05 additive runtime authorization ve policy/boundary kararları |
| `docs/audits/mod-0151-fu05-pack-runtime-scope-authorization-2026-07-28.md` | Created | Governance authorization evidence |

## 12. Final Verdict

### PASS

FU05 apply/history/CoverageSummary kapsamı açıkça yetkilendirildi. Account/Contact/resource mutation yasakları,
hard-delete yasağı ve FU06–FU09 sınırları korunmuştur. Hiçbir runtime, gateway, RBAC, reference-data veya Mongo
değişikliği yapılmamıştır.

## 13. Next Recommended Prompt

`@orchestrator MOD-0151 FU05 — Account Assignment Apply + History`
