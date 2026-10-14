# CONTROL TOWER — Daily Operating Card v1.1

> **Purpose:** günlük Control Tower turu için hızlı gate. Yeni authority yaratmaz. Ayrıntı için [CONTROL TOWER SOP](./control-tower-sop.md) (v2.5).

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
- [ ] **Ledger row (v2.5 R8):** WP'nin CT-QUEUE satırı dağıtımdan önce var; yoksa STOP (SOP §0.1 madde 1, §20.1).

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
- [ ] Bilinen kırmızılar: sonuç `python3 scripts/known_failures.py check <log|trx>` ile `tests/known-failures.tsv`'ye karşı okunur; **YENİ = regresyon, STOP**. Yerel tekrar tanıdan önce `should-rerun --suite <ad>`; ATLA yalnız yerel tekrar tanı içindir — CI, zorunlu kabul, güvenlik testleri ve sahibin açıkça istediği testler atlanmaz.

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

## 7. Kalıcı onaylar ve açık onay sınırları

> Sahip kararı 2026-10-15 (CT-OPS-01). "Kalıcı" yalnız önceden yetkilendirilmiş kapsam içindir. Bu tablo GIT-002, XMC-001,
> `AGENTS.md` ve SOP'yi değiştirmez; çelişkide onlar kazanır.

| İşlem | Onay | Kaynak |
|---|---|---|
| Salt-okunur inceleme ve ölçüm | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| REP-001'e uygun rapor üretme | Kalıcı — tekrar sorulmaz | [REP-001](../../../.antigravity/rules/status-reporting-and-evidence.md); sahip kararı |
| Ortak test kilidine uyarak, onaylı kapsamda test çalıştırma | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| Kendi geçici çalışma dosyalarını kullanma | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| Açıkça onaylanmış iş paketi için dal ve worktree oluşturma | Kalıcı — tekrar sorulmaz | [GIT-002](../../../.antigravity/rules/git-safety.md) §1; sahip kararı |
| Commit, push, PR ve merge | **Her seferinde açık onay** | [GIT-002](../../../.antigravity/rules/git-safety.md) §4, §5 |
| Yıkıcı Git işlemleri | **Her seferinde açık onay** | [GIT-002](../../../.antigravity/rules/git-safety.md) §6 |
| `.antigravity/**` değişiklikleri | **Her seferinde açık onay** | [AGENTS.md](../../../AGENTS.md) §4 |
| Başka modüle müdahale | **Her seferinde açık onay** | [XMC-001](../../../.antigravity/rules/cross-module-change-consent.md) |
| Mimari veya kapsam değişikliği | **Her seferinde açık onay** | SOP §4.1, §19.4 |
| Canlı ortam müdahalesi | **Her seferinde açık onay** | Sahip kararı 2026-10-15 |
| Ortak dosyada tek-yazar anlaşması | **Her seferinde**, etkin Control Tower ile | SOP §16.4, K15 |

## 8. Domain → Control Tower sahipliği (yalnız kanıttan)

> Kanıt = depo yolu@commit. Kanıt yoksa satır "Belirsiz — sahip ataması bekliyor" yazar; sahip kendiliğinden atanmaz.
> Ölçüm: 11 `execution/domains/*/domain-config.md` dosyasının hiçbiri Control Tower sahibi tanımlamıyor (2026-10-15, `6b8a62d5a`).

| Domain | Control Tower | Kanıt |
|---|---|---|
| supply-chain-execution | MVP6 Control Tower (CT-QUEUE) | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv@6b8a62d5a` (MOD-0147, 0148, 0183–0192) · `docs/guides/operations/mvp6-development-process-v1.0.md@6b8a62d5a` |
| pharmacovigilance | PVG Control Tower — planda; güncelliği doğrulanmadı | `docs/roadmap/plans/pvg-control-tower-development-plan-2026-08-13.md@6b8a62d5a` |
| platform-shared-services | Kapanış Control Tower'ı — **kısmi**: Auth, Platform, MOD-0023 onay motoru, MOD-0024 Görev Merkezi, MOD-0027 bildirimler; kapanış sonrası "modül başına CT" planı nedeniyle güncelliği doğrulanmalı | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (Auth 13, MOD-0024/Görev Merkezi/WorkCenter 12, MOD-0023 ve onay motoru 10, Platform ve bildirimler 6); paketler `execution/domains/platform-shared-services/module-packs/@6b8a62d5a` |
| master-data-management | Kapanış Control Tower'ı — **kısmi**: MDM devralma, MOD-0220; güncelliği doğrulanmalı | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (MDM devralma 10, MDM 4, MOD-0220 2) |
| procurement | Belirsiz — sahip ataması bekliyor | CT-QUEUE'da yalnız MOD-0140 satırı var; domain sahipliği sayılmadı |
| commercial-suite | Belirsiz — sahip ataması bekliyor | — |
| developer-enablement | Belirsiz — sahip ataması bekliyor | — |
| enterprise-strategy-business-performance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
| human-capital-management | Belirsiz — sahip ataması bekliyor | — |
| management-governance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
| portfolio-delivery | Belirsiz — sahip ataması bekliyor | — |
| *(domain dışı)* altyapı, CI, test altyapısı, ortak ön yüz | Kapanış Control Tower'ı; güncelliği doğrulanmalı | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (altyapı 12, ortak ön yüz 5, test altyapısı 2) |
