# CONTROL TOWER — Daily Operating Card v1.0

> **Purpose:** günlük Control Tower turu için hızlı gate. Yeni authority yaratmaz. Ayrıntı için [CONTROL TOWER SOP](./control-tower-sop.md) (v2.4).

## 1. START / DoR — dispatch etmeden önce

- [ ] **Identity + owner:** canonical module ID/name ve owner/SoR açık.
- [ ] **Pack authority:** gerekli Module Pack `approved/ready-for-dev`; cross-module ise DCP boundary açık.
- [ ] **Contract:** required API/DTO/event/permission/evidence contract mevcut; invention yok.
- [ ] **Dependencies:** `SATISFIED` veya kayıtlı `WAIVED`.
- [ ] **Scope:** allowed paths + protected paths açık.
- [ ] **Git isolation:** repo / branch / expected HEAD / worktree / dirty baseline ölçüldü.
- [ ] **Execution:** Profile A/B/C + Agent Lane ID/Type + risk class açık.
- [ ] **Target agent:** entry point §6'dan seçildi; o ajanın §17.4 zorunlu giriş alanları dolu; paste-ready prompt (§36.1) hazır.
- [ ] **Acceptance:** golden/contract/inspection flow + measurable AC + required evidence level açık.

**Bir kritik madde FAIL → implementation dispatch yok.**

## 2. PARALLEL / Agent Lane gate

- [ ] Shared contract/migration/permission/global route/shared registration için **single writer**.
- [ ] Parallel Agent Lane'ler ayrı worktree + disjoint scope kullanıyor.
- [ ] `Parallel-safe with` **ölçüldü**, varsayılmadı.
- [ ] Integration order açık.
- [ ] Güvenli paralellik varsa mümkünse **2+ executable WP** hazır.

## 3. DISPATCH PREFLIGHT

```text
WP / Prompt ID + version
Agent Lane ID / Type
Target agent / entry point
Repository / Branch / HEAD / Worktree
Depends on / Parallel-safe with
Allowed paths / Protected paths
```

Aşağıdakilerde STOP: wrong branch/HEAD, overlapping dirty files, draft pack, missing contract, ownership conflict, unplanned migration, **agent/scope mismatch**.

## 4. EVIDENCE LEVEL

| Level | Evidence | Meaning |
|---|---|---|
| E0 | Agent narrative | claim only |
| E1 | Static code/config inspection | readiness |
| E2 | Build/unit/contract tests | implementation confidence |
| E3 | Runtime API/UI smoke | runtime behavior |
| E4 | + persistence/RBAC/tenant/audit/failure paths | state-changing acceptance |
| E5 | + cross-module integrated runtime / restart-compatibility where required | capability/integration acceptance |

**Required level yoksa Agent PASS → CT ACCEPTED değildir.**

## 5. VERIFY

- [ ] Runtime freshness: process start > binary timestamp.
- [ ] Critical fix: mümkünse `fix absent → RED`, `fix present → GREEN`.
- [ ] Producer + consumer birlikte ölçüldü.
- [ ] State-changing ise UI/API + persisted state + audit/observability.
- [ ] RBAC + tenant + concurrency + idempotency applicable kontrolleri.
- [ ] Cross-module ise Integration Gate.

## 6. CLOSE / REPLAN

- [ ] Agent Verdict ayrı, Verification Verdict ayrı, CT Status ayrı.
- [ ] Backlog / Module Pack / Seam / DCP / Delivery Board applicable kayıtları güncellendi.
- [ ] Decision / waiver / intentionally-not-done kaydedildi.
- [ ] Dependency gates yeniden hesaplandı.
- [ ] Newly-unblocked work belirlendi.
- [ ] Güvenli paralellik varsa sonraki **2+ WP** üretildi; yoksa neden yazıldı.

```text
Commit ≠ Acceptance
Isolated PASS ≠ Integrated PASS
Agent PASS ≠ CT ACCEPTED
Chat ≠ System of Record
Replanning = Closure
```
