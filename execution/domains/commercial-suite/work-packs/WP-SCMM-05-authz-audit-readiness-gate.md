# WORK PACKAGE — WP-SCMM-05 · Authz + Audit foundation readiness gate (R1, G1)

> **Control Tower kaydı (SoR).** SCMM iş planı R1 girişi, gate **G1**. Bağımlı: SCMM-04 ✅ (contract framework). Açar: SCMM-09+ (G2 model build) — bu gate geçmeden authz/audit tüketen build `READY` olamaz.
> **Ownership sınırı (§4.1, owner talimatı):** MOD-0018 (RBAC/ABAC) ve MOD-0021 (Audit) **başka ekibin (platform-shared-services) modülleridir.** Bu WP onları **yeniden yazmaz/hardenlemez** — SCMM tüketimi için **hazır mı diye DOĞRULAR** ve eksikse sahibi ekibe **remediation-scope talebi** çıkarır. Kod değişikliği gerekiyorsa o, owning-team'in ayrı WP'sidir.

## Metadata
```text
WP ID:            WP-SCMM-05
Prompt ID:        P-SCMM-05 · v1.0
Task Class:       Readiness verification + remediation hand-off (Profile C — no writes)
Risk Class:       HIGH sinyali (authz + audit — §9.1) ama bu WP read-only doğrulama
Capability:       SCMM · consumes MOD-0018 + MOD-0021
Agent Lane:       AL-SCMM-FOUNDATION (VER/INS)
Target Agent:     /read-only-audit (read-only-auditor)
Branch:           feature/structured-content-messaging · Expected HEAD: 886e6fd6
Evidence hedefi:  E1 (kod/permission presence) ŞİMDİ + E3 (runtime authz-denial + audit-durability smoke) fleet açılınca; cold ise "NOT MEASURED — fleet cold"
Verdict skalası:  READY-FOR-SCMM · HARDEN-BEFORE-USE (remediation-scope ile) · BLOCKED
```

## Neden (SCMM-02 + SCMM-04 girdisi)
PVG (kaynak-kanıt, §3): MOD-0018 62–75% HARDEN (ABAC/data-scope + admin UI eksik); MOD-0021 70–80% runtime-not-proven (tamper-evidence + authenticated smoke eksik). **Hiçbirinde runtime PASS yok.** SCMM-04 common-controls contract'ı bunları zorunlu kılıyor. Bu gate, SCMM build'i başlamadan foundation'ın **SCMM'in ihtiyacını** karşıladığını runtime'da kanıtlamalı (K1 — kod var ≠ davranış canlı).

## Doğrulanacak — SCMM'in authz/audit tüketim ihtiyaçları (SCMM-04 C-controls)
**MOD-0018 (RBAC/ABAC):**
- Her read/write **server-side permission** kontrolü (SCMM role matrisi §8: content-architect/author/reviewer/publisher/content-owner/admin-ops) — attribute/policy düzeyinde mevcut mu?
- **Tenant izolasyonu** + **ABAC/data-scope** (product/market scope) — SCMM audience/scope gereksinimi (docx §8) için var mı? (PVG "eksik" diyor — teyit et.)
- Denial path: yetkisiz → 403 (401 değil), tenant-cross → red.

**MOD-0021 (Audit):**
- SCMM'in audit ettiği aksiyonlar (create/edit/publish-config/submit/review/release/withdrawal/evidence-change/permission-sensitive) için event modeli: actor + tenant + UTC + object/version + correlation taşıyor mu?
- **Durability/tamper-evidence:** "required durable audit kaydı olmadan controlled release tamamlanamaz" kuralını destekliyor mu? (PVG "tamper-evidence eksik" — teyit et.)
- Failure davranışı: audit başarısız → pending-queue mu, mutation-block mu (SCMM-04 kararı) — mevcut mu?

**Common controls:** idempotency + expected-version token desteği; callback authn; browser'da servis/storage credential yok.

## Çıktı (acceptance)
1. **Readiness verdict** (MOD-0018 ve MOD-0021 için ayrı): READY-FOR-SCMM / HARDEN-BEFORE-USE / BLOCKED — SCMM tüketimi açısından, adlandırılmış revizyon (886e6fd6) + ortam.
2. **Remediation-scope talebi** (HARDEN/BLOCKED ise): owning-team'e (platform-shared-services) hangi eksik (ör. ABAC data-scope, tamper-evidence) — `path:line` kanıtı + PVG referansı. **Biz düzeltmeyiz; talebi kaydederiz.**
3. **Gate kararı:** hangi SCMM build WP'si (SCMM-09+) bu eksiklerle **BLOCKED**, hangisi geçebilir.
4. Runtime yoksa E3 kalemleri açıkça **NOT MEASURED — fleet cold** (K10); fleet açılınca operator/CT E3 turu.

---

## §37 CT bağımsız doğrulama (2026-09-07) → **GATE NOT PASSED (HARDEN/BLOCKED)**
```text
Branch/HEAD: feature/structured-content-messaging @ 886e6fd6
Agent Verdict: HARDEN/BLOCKED · Verification: PASS (kritik bulgular CT tarafından path:line teyit) · CT: ACCEPTED (rapor doğru) · Evidence: E1 (E3 fleet-cold)
```
CT'nin bağımsız teyit ettiği kritik bulgular:
- ✅ **Audit fail-soft** — `HttpCrmAuditPublisher.cs:83-92` catch → "event dropped (fail-soft)"; hata iş operasyonunu bloklamıyor → **"durable audit → release" zorlanamıyor → release yolu BLOCKED.**
- ✅ **Knowledge/Concept feature'ında 0 audit** (grep=0) → SCMM yüzeyi bugün denetimsiz.
- ✅ **Canonical `crm.knowledge.concept.*` seed edilmemiş**; DEV-ONLY `crm.territory.*` fallback (`ConceptPermissions.cs:19-21`, "prod tenant'a verilmemeli").
- ✅ **ABAC/data-scope yok** (düz string match) — PVG teyit.

### Gate sonucu: MOD-0018 🟠 HARDEN-BEFORE-USE · MOD-0021 🟠 HARDEN (release yolu 🔴 BLOCKED)

### Remediation register
**Owning-team (biz DOKUNMAYIZ — talep):**
- **R1 🔴 MOD-0021 ekibi** — audit fail-soft → controlled-release için durable-ack/mutation-block + tamper-evidence. (`HttpCrmAuditPublisher.cs:83-92`)
- **R2 🟠 MOD-0018 ekibi** — authz handler'da ABAC/data-scope (product/market) yok. (`PermissionAuthorizationHandler.cs:20-25`)
- **R3 🟠 MOD-0021 şema** — audit event'te object-version + ActorId eksik.

**SCMM-side (BİZİM işimiz — CAND-CAP-0011/MOD-0162, owning-team değil):**
- **S1** — canonical `crm.knowledge.concept.*` key'lerini RBAC kataloğuna seed et (dev-fallback'i kaldır). → authz'ı SCMM-09 için açar; **owning-team'e bağlı DEĞİL.**
- **S2** — Knowledge/Concept/Path/Content handler'larına audit publisher bağla (bugün sıfır) + SourceModule doğru etiket.
- **S3** — 6-rol/SoD grant şablonları (author/publisher SoD).

### Bloklanan SCMM build WP'leri
- **SCMM-17** (release+withdrawal) 🔴 — fail-soft audit "durable→release"i ihlal (R1'e bağlı).
- **SCMM-15** (review freeze) — submit/review audit yok (S2+R3).
- **SCMM-20** (pilot AT08 security+durability) — yukarıdakiler + E3 (fleet).
- **SCMM-05** kendi (bu gate) — S1/S2 + R2 kapanınca PASS.
- **SCMM-09** (concept extend) — authz için yalnız **S1** (bizim iş) yeterli; R1/R2'ye bağlı DEĞİL → S1 sonrası ilerleyebilir.

---

## §36.1 Agent Prompt (paste-ready)

```text
/read-only-audit
WP: WP-SCMM-05 · Prompt P-SCMM-05 v1.0  (VER/INS — readiness gate, YAZMA YOK)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 886e6fd6 · Worktree: ana checkout

Denetim modu: worktree-read-only (strict). Her bulgu path:line kanıtı. DÜZELTME/YAZMA YOK.
⚠ MOD-0018 ve MOD-0021 BAŞKA EKİBİN modülleri — onları HARDENLEME/DEĞİŞTİRME. Yalnız SCMM tüketimi için
  hazır mı DOĞRULA; eksikse remediation-scope TALEBİ çıkar (path:line + PVG referansı).

Önce oku:
1. docs/decisions/DEC-SCMM-04-integration-delivery-contracts.md (§1 C-controls, §3 security/role matrisi)
2. execution/domains/commercial-suite/work-packs/SCMM-content-studio-work-plan.md (§3 readiness, §8 role matrisi)

NE:  SCMM'in tükettiği authz+audit foundation'ının SCMM ihtiyacını karşılayıp karşılamadığını DOĞRULA:
     MOD-0018 (RBAC/ABAC):
       - her read/write server-side permission (SCMM 6 rolü: architect/author/reviewer/publisher/owner/admin-ops)?
       - tenant izolasyonu + ABAC/data-scope (product/market scope) var mı? (PVG: eksik — teyit)
       - denial: yetkisiz→403, tenant-cross→red.
     MOD-0021 (Audit):
       - SCMM aksiyonları (create/edit/publish-config/submit/review/release/withdrawal/evidence-change/permission-sensitive)
         için event: actor+tenant+UTC+object/version+correlation?
       - durability/tamper-evidence + "durable audit olmadan release tamamlanamaz"? (PVG: tamper eksik — teyit)
       - audit-failure davranışı (pending-queue vs mutation-block) mevcut mu?
     Common controls: idempotency+expected-version token; callback authn; browser'da credential yok.
NEDEN: SCMM build (SCMM-09+) başlamadan foundation'ın SCMM'i taşıdığı runtime'da kanıtlanmalı (K1); PVG estimate yeterli değil.
NASIL: E1 kod/permission-presence ŞİMDİ (path:line). E3 (runtime authz-denial 403 + audit-durability smoke) fleet
       AYAKTAYSA; DEĞİLSE "NOT MEASURED — fleet cold" yaz, uydurma (K10).
YAPMA: MOD-0018/0021 kodunu değiştirme/hardenleme (başka ekip). Yeni defect iddia etme; SCMM tüketimi açısından ölç.
       HR&TEP/PVG sheet'lerine dokunma. Estimate'i completion sayma.
DOĞRULA/ÇIKTI: MOD-0018 + MOD-0021 için ayrı VERDICT (READY-FOR-SCMM / HARDEN-BEFORE-USE / BLOCKED) +
       remediation-scope talebi (eksikler, path:line, owning-team) + hangi SCMM build WP'si BLOCKED.
       §22 formatı, RAPORU TÜRKÇE. Senin PASS'in kapanış değildir (K13); kod değişikliği owning-team'in ayrı WP'sidir.
```
