# MOD-0029-FU30 — Gateway Route Integration

**Tarih:** 2026-07-22
**Kapsam:** Gateway route coverage audit + regression tests (additive / non-destructive)
**Commit/push:** YOK — tüm değişiklikler working tree'de
**Final verdict:** **PASS** (FU30 kapsamı tamamen yeşil; 1 pre-existing/kapsam-dışı test başarısızlığı raporlandı)

---

## 1. Initial Audit Summary

MOD-0029 governance backend (FU06–FU23) + permission hardening (FU29/FU29A) tamamlandı. FU30, bu endpoint'lerin
Gateway üzerinden Platform service'e forward edildiğini doğrular. **Sonuç: mevcut catch-all zaten hepsini
kapsıyor → ocelot.json'a route eklenmedi.**

## 2. Existing Gateway Route Model

- Config yükleme: `Program.cs` → `AddJsonFile("ocelot.json")` (tek dosya; environment-specific ocelot yok).
- Downstream servisler: auth(5056), platform(5057), dev-enablement(5058), mdm(5059), esbp/uploads(5004),
  + working-tree'de yeni: **hcm(5060), crm(5061)** (önceki uncommitted streamler).
- Document-management aile route'ları (ocelot.json):
  - `/api/v1/document-management` → Platform 5057 (GET/POST/PUT/PATCH/DELETE/OPTIONS)
  - `/api/v1/document-management/{everything}` → Platform 5057 (tüm method'lar) — **catch-all**

## 3. Document-Management Route Coverage Table

29 DocumentManagement controller'ının `[Route]` prefix'leri tarandı; **hepsi** `/api/v1/document-management/`
altında:

| Controller ailesi | Route prefix | Catch-all kapsıyor? |
|---|---|---|
| Master register, identifiers, lifecycle, approval, release-gates, training, periodic-review, suspension, retirement, temporary-instruction, repository-assessment, controlled-copy, external-documents, retention, legal-holds, disposition, gdocp, quality-events, deviations, capa, signatures, signature-policies/requests | `api/v1/document-management` | ✅ |
| Downtime | `api/v1/document-management/repository-downtime-events` | ✅ |
| Variant localization | `api/v1/document-management/template-variants` | ✅ |
| QMS baselines / access-profile / template-masters / template-variants / instantiations / controlled-documents (FU01–05) | `api/v1/document-management(/…)` | ✅ |

Ocelot'un `{everything}` placeholder'ı çok-segment (slash içeren) path'leri greedy eşleştirdiği için
`/api/v1/document-management/repository-downtime-events/{id}/temporary-issues/{issueId}/approve` gibi derin
path'ler dahil **tüm FU06–FU23 endpoint'leri** catch-all tarafından Platform'a forward edilir.

## 4. Missing Routes Identified

**YOK.** Base + catch-all route çifti tüm document-management yüzeyini kapsıyor. Task'ın "Expected route
coverage" listesindeki her grup (master-register, identifiers, lifecycle, approval-routes, release-gates,
training, periodic-reviews, suspensions/retirements/temporary-instructions, repository-assessments,
controlled-copies, external-documents, retention/legal-holds/disposition, variants-localization,
repository-downtime-events, gdocp-corrections, quality/deviations/capa, signatures) catch-all ile karşılanıyor.

## 5. Route Changes Made / No-Change Rationale

**ocelot.json değiştirilmedi (0 satır).** Task'ın "Not" maddesi: *"Eğer existing catch-all zaten hepsini
kapsıyorsa ayrı route eklemek gereksiz olabilir… ana çıktı: coverage audit + regression test + no route change
confirmation."* Ayrı grup route'ları eklemek gereksiz duplikasyon olur ve route ordering riski doğurur.

## 6. Route Ordering / Conflict Analysis

- Base (`/api/v1/document-management`) ve catch-all (`/api/v1/document-management/{everything}`) farklı upstream
  template'ler → Ocelot çakışması yok.
- Document-management ailesinde aynı (template + verb) çifti tekrarı yok (test ile doğrulandı).
- Catch-all Platform'ın kendi routing'ine (action-level `[Route]`) devreder; ordering Platform'da çözülür.

## 7. Tests Added

**Yeni:** `Mod0029Fu30DocumentManagementRouteCoverageTests.cs` (Diten.ApiGateway.Tests) — 34 test (28'i theory).
Shipped ocelot.json'u Ocelot `FileConfiguration` modeline deserialize eder (servis başlatılmaz):
- catch-all mevcut + tüm verb'ler; base route mevcut
- document-management route'ları Platform 5057'ye forward + path preserved (rewrite yok)
- 28 temsili governance path (her FU grubu) GET+POST için gateway'den erişilebilir + covering port 5057
- upstream template'lerde `:5057` yok (client'a direct port açılmıyor); downstream 5057
- document-management route'ları tenant header (X-Tenant-Id) enjekte etmiyor
- document-management ailesinde duplicate/conflict route yok

## 8. Build / Test Results

- `dotnet build Diten.ApiGateway.Tests` → **0 hata** (restore gerekti; `-o .tmp/...`).
- FU30 testleri (`~Mod0029Fu30`) → **34 başarılı, 0 başarısız**.
- Full gateway suite → 44 başarılı, **1 başarısız** → `EveryRoute_DownstreamPortIsInKnownServiceSet`.
  - **Pre-existing + kapsam dışı:** working-tree ocelot.json'da (önceki uncommitted iş) **hcm(5060)** ve
    **crm(5061)** servisleri var; testin `KnownDownstreamPorts` allowlist'i {5004,5056,5057,5058,5059} bunları
    içermiyor. Bu HCM/CRM stream'lerine ait; FU30 (document-management) ile ilgisi yok. Hard boundary
    ("CRM/HCM route'larına dokunma") gereği düzeltilmedi — allowlist güncellemesi ilgili stream'in işidir.
- Platform / AuthService build/test **çalıştırılmadı** — o servislere dokunulmadı.

## 9. Remaining Gaps

1. `EveryRoute_DownstreamPortIsInKnownServiceSet` testi hcm(5060)/crm(5061) portları için güncellenmeli
   (ilgili HCM/CRM gateway entegrasyon sahibi; FU30 dışı).
2. Bu task static config/route-coverage doğrulaması yapar; canlı HTTP smoke (gerçek 200/401/403) çalışan fleet
   ile ayrıca yapılabilir — routing katmanı bunu gerektirmiyor (catch-all + Platform authz zaten kanıtlı).

## 10. Guardrail Confirmations

- ✅ ocelot.json değiştirilmedi (0 route eklendi/silindi) · existing CRM/Auth/MDM/HCM route'larına dokunulmadı
- ✅ Frontend değişikliği yok · MOD-0028 mutation yok · AuthService seed değişikliği yok
- ✅ Permission key / runtime authorization değişikliği yok · Platform business behavior rewrite yok
- ✅ Yeni service port eklenmedi · direct 5057 frontend/client çağrısı eklenmedi · client TenantId/X-Tenant-Id yok
- ✅ raw bytes yok · hard delete yok · route duplicate/conflict yok · Commit/push yok

## Final Verdict: **PASS**

Mevcut catch-all tüm FU06–FU23 document-management endpoint'lerini Platform'a forward ediyor; route eksiği yok,
route değişikliği gerekmedi; 34 route-coverage regression testi eklendi ve yeşil. Tek suite başarısızlığı
pre-existing ve kapsam dışı (HCM/CRM port allowlist).
