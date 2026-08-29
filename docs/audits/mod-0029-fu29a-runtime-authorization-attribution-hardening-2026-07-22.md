# MOD-0029-FU29A — Runtime Authorization Attribution Hardening

**Tarih:** 2026-07-22
**Kapsam:** Platform runtime endpoint authorization attribution (additive / non-destructive)
**Commit/push:** YOK — tüm değişiklikler working tree'de
**Final verdict:** **PASS**

---

## 1. Initial Audit Summary

FU29 ile 69 adanmış MOD-0029 governance permission key AuthService seed'e eklendi, ancak Platform runtime
controller'ları hâlâ genel `controlled-documents.view/create` anahtarlarını reuse ediyordu. FU29A bu gap'i
kapatır: 16 governance controller'ın her action'ının `[HasPermission]` attribute'u, FU29'da seed edilen
adanmış key'e bağlandı.

**Kritik mekanizma bulgusu:** `HasPermissionAttribute` içinde `platform_admin`/`partner_admin` aktörleri
claim kontrolünü bypass eder (aktif admin doğrulaması sonrası doğrudan izin). Dolayısıyla attribute key
değişimi platform-admin smoke akışlarını **kırmaz**; yalnızca `tenant_user` aktörleri claim ile gated olur —
bu, değişimi güvenli kılan ana faktördür. Ek olarak `HasPermissionReflector` bu key'leri startup'ta
AuthService catalog'una otomatik kaydeder, yani adanmış key'ler artık hem enforce edilir hem self-register olur.

## 2. FU29 Seeded Key Confirmation

Wire edilen tüm adanmış key'ler FU29'da seed edilmiş 69 key kümesinde mevcut (kod sabitleri = seed SSOT).
**Eksik seed key tespit edilmedi → AuthService seed'e dokunulmadı.**

## 3. Endpoint Attribution — Before / After

Genel desen (her controller): reads `controlled-documents.view` → adanmış `*.view`; writes
`controlled-documents.create` → adanmış `*.manage` veya action-spesifik key. Migrasyon sonrası 16 governance
controller'da genel `controlled-documents.view/create` referansı **0** (grep ile doğrulandı).

| Controller | Permission sınıfı | View→ | Write→ (default / spesifikler) |
|---|---|---|---|
| Retention | DocumentRetentionPermissions | RetentionView / LegalHoldView | RetentionManage · LegalHoldManage · **LegalHoldRelease** · DispositionManage · DispositionApprove |
| GDocPCorrection | GDocPCorrectionPermissions | View | PolicyManage · Record · **Review** (review+reject) |
| QualityEvent | QualityEventPermissions | QualityEventsView / DeviationsView / CapaView | QualityEventsManage · DeviationsManage · CapaManage · BridgeManage |
| Signatures | ElectronicSignaturePermissions | SignaturesView | SignaturePoliciesManage · SignaturesRequest · **Sign/Verify/Invalidate** |
| Downtime | DowntimePermissions | View | Manage · TemporaryIssue · Reconcile |
| ExternalDocuments | ExternalDocumentPermissions | View | Manage · MonitoringRecord · ImpactManage |
| VariantLocalization | VariantLocalizationPermissions | View | Manage · TranslationReviewRecord · LocalApprovalRecord |
| RepositoryAssessment | DocumentRepositoryAssessmentPermissions | View | Manage · Approve |
| ControlledCopy | DocumentControlledCopyPermissions | View | Manage · Reconcile |
| ReleaseGates | DocumentReleaseGatePermissions | View | Evaluate · RecordEvidence |
| Training | DocumentTrainingPermissions | View | Manage · Verify (complete/effectiveness) |
| PeriodicReview | DocumentPeriodicReviewPermissions | View / EscalationView | Manage · ApproveExtension |
| Suspension | DocumentSuspensionPermissions | View | Manage · Approve · RetirementApprove |
| MasterRegister | DocumentMasterRegisterPermissions | View | Manage · Link |
| Identifiers | DocumentIdentifierPermissions | View | Allocate · Reserve · Cancel |
| Lifecycle | DocumentLifecyclePermissions | View | Manage |
| Approval | DocumentApprovalPermissions | View | RecordEvidence (evidence/reject) · Manage (resolve) |

### Granularity notu (task mapping vs gerçek seed)
Task'ın önerdiği bazı ince key'ler (ör. `quality-events.close` vs `.cancel`, `capa.effectiveness` vs
`.close`, `disposition.execute-marker`) FU29 seed'inde ayrı ayrı **yok** — sadece coarse `*.manage`
karşılıkları var. Hard boundary ("FU29 key isimlerini değiştirme", "attributes missing seeded key yok")
gereği bu action'lar mevcut en yakın seed edilmiş key'e (`*.manage`) bağlandı. İnce granularity gerekirse
ayrı bir seed-genişletme FU'su gerekir (Remaining Gaps).

## 4. Permission Constant / Catalog Changes

**YOK.** Yeni permission constant eklenmedi, mevcut sabitler kullanıldı. AuthService seed değişmedi.

## 5. Runtime Authorization Changes

16 controller × yalnızca `[HasPermission]` attribute constant referansı değişti. Route/payload/response,
business validation, MediatR dispatch, handler davranışı — hiçbiri değişmedi (attribute-only). Build 0 hata.

## 6. Critical Endpoint Hardening (öne çıkanlar)

| Endpoint | Eski | Yeni |
|---|---|---|
| `legal-holds/{id}/release` | controlled-documents.create | **legal-hold.release** |
| `disposition-requests/{id}/execute-marker` | controlled-documents.create | disposition.manage |
| `retention-policies/{id}/activate\|retire` | controlled-documents.create | retention.manage |
| `gdocp-corrections/{id}/review\|reject` | controlled-documents.create | gdocp-corrections.review |
| `quality-events/{id}/close\|cancel` | controlled-documents.create | quality-events.manage |
| `deviations/{id}/close\|cancel` | controlled-documents.create | deviations.manage |
| `capa-actions/{id}/effectiveness\|close` | controlled-documents.create | capa.manage |
| `signatures/sign` · `/verify` · `/invalidate` | controlled-documents.create | signatures.sign/verify/invalidate |
| `…/temporary-issues/{id}/approve` · `/reconcile` | controlled-documents.create | downtime.temporary-issue · downtime.reconcile |
| `external-documents/{id}/impact-assessments/{id}/complete` | controlled-documents.create | external-documents.impact.manage |

## 7. Tests Added

**Yeni:** `Mod0029Fu29aEndpointAttributionTests.cs` (Platform.Application.Tests) — 30 test.
Route-template tabanlı reflection ile (C# method adına bağımlı değil) her kritik write endpoint'in adanmış
FU29 key'ini enforce ettiğini doğrular; değerler gerçek Platform permission sabitlerine referanslıdır
(compile-safe, drift-proof). Kapsam: task test listesi 1–26 + Master register update generic değil (27) +
**hiçbir governance endpoint generic controlled-documents key kullanmıyor (28)** + tüm key'ler
document-management-scoped (29) + her controller ≥1 hardened key.

## 8. Build / Test Results

- `dotnet build Diten.Platform.API` → **0 hata** (fleet bin kilidi nedeniyle `-o .tmp/...`).
- `dotnet test Diten.Platform.Application.Tests` (full) → **1826 başarılı, 0 başarısız** (30 yeni dahil).
- AuthService build/test **çalıştırılmadı** — AuthService dosyalarına dokunulmadı (gerekmiyor).

## 9. Remaining Gaps

1. **Granularity**: close/cancel/effectiveness/execute-marker gibi işlemler coarse `*.manage` altında
   birleşik. Regülasyonda ayrı yetki gerekiyorsa ince key'ler için ayrı seed FU'su.
2. **Read scope**: disposition GET'leri `retention.view`e bağlandı (adanmış `disposition.view` seed'de yok).
3. **Tenant grant**: governance key'leri tenant governance kullanıcılarına ulaştırmak için grant stratejisi
   (module-grant / manual) hâlâ ayrı iş — FU29A tenant grant migration yapmaz.

## 10. Guardrail Confirmations

- ✅ AuthService seed yeniden yazılmadı (dokunulmadı) · FU29 key isimleri değişmedi · mevcut key kaldırılmadı
- ✅ Business validation / controller route / payload / response değişmedi (attribute-only)
- ✅ Gateway ocelot yok · Frontend yok · MOD-0028 mutation yok · aggregate/model behavior yok
- ✅ raw bytes yok · hard delete yok · direct 5057 yok · client TenantId/X-Tenant-Id yok
- ✅ Yeni e-sign/provider/CAPA/workflow behavior yok · permission key spelling drift yok
- ✅ Attributes → hepsi seed edilmiş key (test 28/29 ile doğrulandı) · Commit/push yok

## Final Verdict: **PASS**

16 governance controller'ın tüm action'ları adanmış FU29 permission key'lerine bağlandı; generic
controlled-documents reuse tamamen kaldırıldı; 30 metadata testi + full suite (1826) yeşil; runtime business
behavior değişmedi; AuthService seed'e dokunulmadı.
