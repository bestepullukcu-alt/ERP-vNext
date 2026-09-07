# MOD-0151 — FU02B Pack Lifecycle Scope Update

> **Tarih:** 2026-07-25 · **Değişiklik türü:** Governance / module pack only · **Verdict:** **PASS**

## 1. Preflight

İncelenen kaynaklar:

- `AGENTS.md`
- `execution/domains/commercial-suite/domain-config.md`
- `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
- `.antigravity/workflows/add-module.md`
- FU01 implementation ve live-smoke raporları
- FU02 hierarchy UI viewer raporu
- FU02A Country & Business Unit Scope selector hardening raporu ve backend addendum'u
- Kullanıcı tarafından sağlanan FU02B preflight/pack-update talebi

Bu çalışma yalnız module pack governance kapsamındadır. Runtime, backend, UI, gateway, reference data, RBAC ve Mongo
değişikliği yapılmadı.

## 2. Existing Blocker Confirmation

Pack `ready-for-dev` ve `runtime_code_allowed: true` olsa da önceki `runtime_code_scope` yalnız
`FU01-territory-model-node-backend-only` idi. Activation FU06 workflow kapsamına yerleştirilmiş, FU02B için açık
runtime yetkisi verilmemişti. Bu nedenle orchestrator'ın FU02B kodu yazmaması governance açısından doğruydu.

FU01 backend kanıtı PASS (214/214 test), FU01 live-smoke PASS (23/23), FU02 UI PASS'tir. FU02A raporu ilk aşamada
PARTIAL olsa da addendum `BusinessScopes` persistence ve 221/221 CrmService test PASS sonucunu belgelemiştir; canlı
authenticated smoke açık takip maddesi olarak kalır.

## 3. Pack Changes Summary

| Section | Change | Notes |
|---|---|---|
| Frontmatter | Runtime scope FU01 + FU02 + FU02A + FU02B olarak güncellendi | Status `ready-for-dev`, runtime flag `true` kaldı |
| Scope banner | Adlandırılmış FU yetkileri ve kapalı kapsamlar açıklandı | Genel runtime yetkisi verilmedi |
| FU Breakdown | FU02A ve FU02B satırları eklendi | FU02B dependency: FU02A |
| FU02B design | Lifecycle, expiry, delete/archive, active uniqueness, contract, permissions, audit eklendi | Runtime implementasyonu yapılmadı |
| FU06 | Workflow approval sahipliği netleştirildi | FU06 kaldırılmadı |
| Follow-ups / risks | FU02A, FU02B, FU06 ve future hardening maddeleri eklendi | Brand/Product, scheduler, MOD-0155, FU07 future |

## 4. FU02B Scope Added

Included: manual model activate/deactivate/archive; modelle bağlı node lifecycle guard'ları; computed expiry read
state; draft-only soft-delete; single-active-model guard; lifecycle UI action visibility; audit event'leri; testler
ve evidence report.

Explicitly excluded: workflow approval, MOD-0023, submit/approve/reject, transition gate, approval trace, evidence
pack, assignment rule/preview/apply, resource assignment, import/export, Brand Scope, Product/Brand master,
background scheduler, hard delete, active record delete ve RBAC seed/grant değişikliği.

## 5. FU06 Boundary Clarification

FU02B manual lifecycle güvenliğini sağlar. FU06 ise MOD-0023 integration, submit/approve/reject, workflow transition
gate, approval trace, evidence-backed activation, approval-based immutable lifecycle, before/after diff ve Change
Approval Trace sahipliğini korur. FU02B, FU06'yı kaldırmaz veya workflow activation flag'ini açmaz.

## 6. Runtime Code Scope Decision

Önceki scope:

`FU01-territory-model-node-backend-only`

Yeni scope:

`FU01-territory-model-node-backend-only; FU02-territory-hierarchy-ui-viewer; FU02A-country-business-unit-scope-selector-hardening; FU02B-lifecycle-computed-expiry-draft-soft-delete`

Pack `status: ready-for-dev` ve `runtime_code_allowed: true` olarak kalmıştır. FU02B runtime implementation artık
allowed'dır; yalnız pack içindeki explicit scope ve guard'larla sınırlıdır.

## 7. Lifecycle Design Summary

| Entity | Lifecycle | Notes |
|---|---|---|
| TerritoryModel | `draft → active → inactive → active`; `inactive/computed-expired → archived`; `draft → soft-deleted` | Active doğrudan archive edilemez; archived read-only |
| TerritoryNode | `draft → active`; `active → inactive`; `inactive/computed-expired → archived`; `draft → soft-deleted` | Active/inactive/archive geçişleri model lifecycle ile yönetilir |

Model ve node create status'u `draft`tır. Model active değilken node tek başına active olamaz. Active kayıt hard
delete edilemez.

## 8. Expiry Decision

V1 computed expiry kullanır. `EffectiveTo` geçtiğinde stored DB status otomatik mutate edilmez; read model
`isExpired=true` ve/veya `computedStatus=expired` üretir ve UI expired badge gösterir. Tarihi geçmiş draft kayıt
draft kalır ve warning gösterir. Background scheduler future hardening'dir.

## 9. Delete / Archive Decision

Yalnız draft model/node soft-delete edilebilir; default listelerden gizlenir, audit/history korunur.
Active/inactive/expired/archived kayıt silinemez. Inactive veya computed-expired model archive edilebilir. Archived
kayıt read-only'dir. Hard delete hiçbir durumda yetkilendirilmemiştir.

## 10. Single Active Model Rule

Aynı tenant + normalized CountryScope + normalized unordered BusinessUnitScope seti + overlapping effective date
window için en fazla bir active model olabilir; ihlal 409'dur. FU02A `BusinessScopes` persistence/normalization
sözleşmesi zorunlu dependency'dir. Bu dependency eksikse FU02B verdict'i PARTIAL olmalıdır.

## 11. Dependencies and Follow-ups

| Item | Status | Notes |
|---|---|---|
| FU02A BusinessScopes dependency | Required | Backend addendum mevcut; implementation preflight'ta tekrar doğrulanmalı |
| FU02B lifecycle hardening | Ready for dev | Bu pack update ile runtime scope açıldı |
| FU06 workflow approval + activation | Future | MOD-0023, approval trace ve immutable approved lifecycle |
| Background expiry scheduler | Future | FU02B yalnız computed expiry |
| Brand Scope / Product-Brand master | Future | Brand/Marketing/MDM capability sonrası |
| MOD-0155 visit/route readiness | Future / FU09 | FU02B dışında |
| Evidence Pack | Future / FU07 | FU02B audit event üretir, evidence pack üretmez |

## 12. Guard Checks

| Check | Result |
|---|---|
| Runtime code changed? | No |
| Backend changed? | No |
| UI changed? | No |
| Gateway changed? | No |
| MOD-0048 changed? | No |
| RBAC changed? | No |
| Mongo changed? | No |
| Workflow approval implemented? | No |
| FU06 removed? | No |
| FU06 boundary clarified? | Yes |
| FU02B added? | Yes |
| `runtime_code_scope` updated? | Yes |
| Brand Scope added? | No |
| Assignment/resource/evidence/import/export opened? | No |
| `crm.territory.delete` introduced? | No |
| `crm.micro-zone.manage` introduced? | No |
| Hard delete authorized? | No |

## 13. Final Verdict

**PASS.** FU02B pack'e eklendi, FU06 workflow boundary netleştirildi, runtime scope FU02B için açıldı; lifecycle
design, computed expiry, draft soft-delete ve single-active-model rule pack'e işlendi. Runtime kod değişmedi.

## 14. Next Recommended Prompt

**MOD-0151 FU02B — Territory Lifecycle Activation, Computed Expiry and Draft Soft Delete Implementation**
