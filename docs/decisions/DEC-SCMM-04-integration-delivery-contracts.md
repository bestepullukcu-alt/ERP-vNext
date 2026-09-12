# DEC-SCMM-04 — Integration & delivery contract onayı (G0 son kalemi)

> **Format:** §14 approval package. **Durum:** ✅ `APPROVED` — **user/owner 2026-09-07.** SCMM-01 ✅ / SCMM-02 ✅ / SCMM-03 ✅ / SCMM-04 ✅ → **G0 KAPALI.** Build WP'leri (SCMM-09+) artık contract-binding + G1 foundation-proof kapılarına tabi. **Owner:** API lead + security + QA (docx §7/§8).
> **Kural (docx §7):** aşağıdaki contract'lar **önerilen sorumluluklardır, mevcut endpoint/event adları DEĞİL.** SCMM-04 bunları **onaylı producer/consumer contract'larına bağlamalı.** *"A missing contract is a blocker for the dependent work package, not permission to invent a substitute shared service."* (K12)

**Decision ID:** DEC-SCMM-04 (docx §7 integration + §8 security)

## 1. Contract-binding matrisi (6 sorumluluk × producer × durum)
Durum = SCMM-02 baseline (E1) + registry. **BLOCKER** olan contract, ona bağlı build WP'sini bloklar.

| # | Contract sorumluluğu (docx §7) | Producer (SoR) | Durum | Bağlı WP |
|---|---|---|---|---|
| C1 | **Resolve reference/version** — tenant-scoped identity/version/lifecycle; consumer eligibility + not-found | MOD-0290 (product) · MOD-0288 (position) · CAND-CAP-0011 (component) · **MOD-0031 (evidence)** · policy owner | 🟡 product **canonical** · **🔴 evidence MOD-0031 spec-only %0 = BLOCKER** · position MOD-0288 kimlik-belirsiz | SCMM-08/12 |
| C2 | **Evaluate proposed assembly** — context+pinned → pass/fail/unresolved + reasons + blocking level + policy version | CAND-CAP-0011 (policy) + MOD-0025 (rules) | 🟡 MOD-0025 registry'de yok → contract tanımlanmalı | SCMM-11 |
| C3 | **Submit review + consume decision** — revision↔workflow def/version; correlated decision; duplicate/stale-safe | MOD-0023 workflow | 🟡 partial, **runtime-unproven → G1** | SCMM-06/15 |
| C4 | **Generate output** — pinned manifest+format → durable job ID + artifact/hash veya controlled failure; stable retry | MOD-0026 scheduler + publishing adapter | 🟢 scheduler done; job contract bağlanmalı | SCMM-16 |
| C5 | **Activate/withdraw release** — domain owns authz/availability; consumer release identity/version/applicability/reason | CAND-CAP-0011 (release owner) | 🟢 yeni owner; contract yeni tanımlanır | SCMM-17/21 |
| C6 | **Source impact notification** — versioned changed/revoked/expired (source/tenant/event-ID/time); consumer dedup/retry/reconcile | CAND-CAP-0011 + MOD-0027 notif + MOD-0024 tasks | 🟡 notif approved; durable event/dedup contract bağlanmalı | SCMM-18 |

**Common request/event controls (zorunlu, tüm contract'lar):** tenant + actor/service identity + correlation ID + schema version + object/version ref + server UTC; mutating req → idempotency + expected-version token; callback authn; her read/write server-side authz; **browser'da servis/storage credential YOK.** → MOD-0018/0021 (harden, G1) + MOD-0032 gateway.

**Consistency/persistence:** per-save boundaries; submission manifest'i dondurur; external-ref failure → unresolved (partial success değil); durable bg = idempotent+retry+exception-queue+replay; **review/release/audit/evidence → L3 cold-start/DB-replay doğrulaması** (authoritative draft'lar da).

**Standards:** SKOS-compatible labels; DITA opsiyonel (zorunlu depolama formatı değil); XLIFF = R2; media-rights map. Kesin sürüm+conformance test ilgili integration spec'te.

## 2. Delivery-spec gereksinimleri (docx §10 governance matrix)
Her build WP'si üretmeden önce: API/UI/data/integration spec + security matrix + test plan. Delivery kanıtı: work-package checklist + review outcome + acceptance report + deployment checklist + release notes. *(Bu artifact'ların önceden onaylı olduğu iddia edilmez.)*

## 3. Security/role matrisi (docx §8 — onaylanacak)
Content architect / Author / Reviewer / Publisher / Content owner / Admin-ops — her biri allowed+restriction ile (ör. Author immutable submitted revision'ı değiştiremez; Publisher artifact substitute/self-approve edemez; admin access ≠ business approval). Central RBAC/ABAC + tenant + product/market scope; iki tenant + viewer/editor test; PII/confidential sınıflama + log/trace/audit redaction. Concurrency: stale write → controlled conflict (silent overwrite yok); duplicate create/submit/render/activate/withdraw → mevcut sonucu döndür; idempotency-key çakışması reddedilir. Audit: create/edit/publish-config/submit/review/release/withdrawal/evidence-change/permission-sensitive → actor+tenant+UTC+object/version+correlation; **required durable audit kaydı olmadan hiçbir controlled release tamamlanamaz.**

## 4. CT önerisi
**Onayla:** yukarıdaki 6 contract sorumluluğu + common controls + consistency + security matrisi **contract-binding framework olarak.** Şu **contract blocker'ları** kayda geç (G1'de çözülecek, build'i bloklar):
- 🔴 **C1-evidence (MOD-0031)** — spec-only %0; **SCMM-07/12 build'i bu bağlanmadan başlamaz.**
- 🟡 **C3 (MOD-0023 workflow)** runtime-unproven → G1 proof (SCMM-06).
- 🟡 **C2 (MOD-0025 rules)** registry'de yok → contract tanım/kimlik.
- 🟡 **C1-position (MOD-0288)** kimlik-belirsiz (deprecated?) → SCMM-08'de netleştir.
Contract adları/şemaları **bağlanmadan** ilgili build WP'si `READY` olamaz (K12/§18.0 contract-blocker).

**Selected/approved:** ✅ **APPROVED (user/owner, 2026-09-07)** — 6 contract sorumluluğu + common controls + consistency + §8 security matrisi contract-binding framework olarak onaylandı; blocker'lar (C1-evidence/MOD-0031, C3/MOD-0023, C2/MOD-0025, C1-position/MOD-0288) risk kaydına bağlandı, G1'de çözülecek.
**Affected boundaries:** CAND-CAP-0011, MOD-0290/0288/0031/0025/0023/0026/0027/0018/0021/0032.
**Required record changes (onay sonrası):** SCMM iş planı §5 contract inventory'yi kilitli işaretle; blocker'ları risk kaydına bağla; onaylanınca **G0 KAPANIR → R1 (SCMM-05→08 G1 foundation) açılır.**
**Effective:** onayda; G1 foundation gate'leri C1-C6 runtime proof'unu ister.

## 5. G0 kapanış kontrol listesi
- [x] SCMM-01 ownership (H)
- [x] SCMM-02 baseline (E1)
- [x] SCMM-03 model/policy freeze (11/11)
- [x] **SCMM-04 contract+delivery onayı (2026-09-07)** → **G0 ✅ KAPALI**
