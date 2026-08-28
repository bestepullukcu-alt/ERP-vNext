# Platform Modülleri — PV (Pharmacovigilance) Kullanım Hazırlık Değerlendirmesi

> PV readiness statüleri: READY_FOR_PV_USE · USABLE_WITH_MINOR_EXTENSION · FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED · CONTRACT_ONLY · BLOCKED · NOT_AVAILABLE
> Bağlam: ERP-vNext'te PV modülü **yoktur** (greenfield); bkz. `docs/analysis/pv-*`. Bu değerlendirme, PV'nin bu 9 platform modülünü **yeniden kullanabilirliğini** ölçer.

## Özet Tablo

| Module | PV kullanım alanı | Şu an kullanılabilir mi? | Eksik contract | PV blocker severity |
|---|---|---|---|---|
| MOD-0004 Metric & Semantic Registry | PV KPI'ları (overdue submission, case processing time, serious case count, reconciliation) | **NOT_AVAILABLE** | Metric definition + calc contract + certification; SEMANTIC-BUNDLE | P1 |
| MOD-0018 RBAC/ABAC | `pv.case.create/view/update/assess/approve`, `regulatory.submission.*`, tenant-scoped perms, batch decisions | **FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED** | PV permission catalog seed + ABAC/data-scope (FU15) | P1 |
| MOD-0019 Data Masking | patient/reporter masking, hassas medikal alanlar, row-level tenant/company izolasyonu | **NOT_AVAILABLE** | SEC-DATA-BUNDLE masking policy engine | **P0 (STOP-SHIP)** |
| MOD-0021 Audit Trail | her Safety Case mutation, before/after, decision reason, non-repudiation | **FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED** | Tamper-evidence hash zinciri + e-signature non-repudiation + PV entity type sözlüğü | P1 |
| MOD-0023 Workflow | case assessment, medical review, QPPV approval, submission workflow, SLA/escalation | **FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED** | PV workflow definition seed + adanmış golden-flow smoke | P1 |
| MOD-0028 Documentation | case attachments, source documents, regulatory outputs, controlled evidence | **FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED** | FU06 index fix + PV controlled-doc tipleri + checksum/version-compare doğrulama | P1 |
| MOD-0031 Evidence Linking | case↔document, assessment↔evidence, submission↔evidence, audit pack provenance | **CONTRACT_ONLY** | EvidenceLink SoR (hiç kod yok) | P1 |
| MOD-0040 Canonical ID & Correlation | legacy Mongo ObjectId → ExternalReference, case number, document ID, correlation ID | **FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED** | Canonical/external ID mapping model (migration için kritik) | P1 |
| MOD-0063 Data Warehouse/Lakehouse | PV KPI read models, trend/signal analytics, regulatory reporting analytics | **NOT_AVAILABLE** | LAKEHOUSE-BUNDLE (dataset/ingestion/lineage) | P2 |

## Modül Bazında PV Notları

### MOD-0004 — NOT_AVAILABLE
PV metrik ihtiyaçları (overdue submission, case processing time, serious case count, reconciliation) için certify edilmiş semantic definition altyapısı **hiç yok**. PV KPI'ları bu modülü beklemek yerine geçici olarak MOD-0063 veya read-model üzerinden ilerleyemez (o da yok).

### MOD-0018 — FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED
RBAC core + tenant scope + `HasPermissionAttribute` enforcement + login runtime hazır. PV için gerekenler: `pv.case.*`, `regulatory.submission.*` permission catalog + role seed; batch access decision (`AuthorizationProbeController`/`AccessExplainController` var → kullanılabilir); **ABAC/data-scope** (hangi kullanıcı hangi case'i görür) FU15 planned → medikal gizlilik için gerekli. Tek başına RBAC PV'ye açılış için yeterli değildir, MOD-0019 ile birlikte gerekir.

### MOD-0019 — NOT_AVAILABLE — **P0 STOP-SHIP (PV)**
Hasta ve raportör kişisel/medikal verisi PV'de en yüksek gizlilik sınıfıdır. Repo'da masking policy engine, field-level masking, row-level security **yok**; yalnız CRM log-redaction helper'ı var. **PV, MOD-0019 olmadan production'a alınamaz.** UI-only maskeleme riski test bile edilemez çünkü policy engine yok.

### MOD-0021 — FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED
Generic immutable audit trail (append-only, before/after, correlation, tenant, time) PV Safety Case mutation'ları için güçlü bir zemin. Eksikler: **kriptografik tamper-evidence** (regülatör non-repudiation için), **e-signature entegrasyonu** (MOD-0029 e-sign ayrı), audit-writer failure'da kritik işlemin engellenmesi (runtime doğrulanmadı). GxP/GVP için tamper-evidence P1.

### MOD-0023 — FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED
Approval/SLA/escalation runtime PV case assessment → medical review → QPPV approval → submission zinciri için doğrudan uygun. Backend endpoint seti tam. Eksik: PV'ye özel workflow definition seed'i, uçtan-uca approve/reject/request-info golden-flow smoke kanıtı.

### MOD-0028 — FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED
Controlled document + template + versioning + retention altyapısı PV case attachment/source document/regulatory output için uygun; ControlledDocuments authenticated smoke geçti. Eksik: FU06 Mongo index blocker'ının giderilmesi, PV controlled-doc tipleri, checksum/version-compare runtime doğrulaması.

### MOD-0031 — CONTRACT_ONLY
Pack detaylı ama kod yok. PV audit pack provenance (case↔document, assessment↔evidence, submission↔evidence) için EvidenceLink SoR şart. Bugün cross-module evidence normal string DocumentId ile taşınır → provenance/sızıntı guard yok.

### MOD-0040 — FOUNDATION_AVAILABLE_BUT_EXTENSION_REQUIRED
Correlation propagation (gateway + event envelope + audit) uçtan-uca izlenebilirliğin bir kısmını sağlar. **Kritik PV eksiği:** legacy Di10-PV Mongo `ObjectId` → canonical `ExternalReference` mapping modeli yok — bu, PV veri migration'ının (case number, document ID eşleme) temel bağımlılığıdır. Registry kimlik çelişkisi (CONF-01) de önce çözülmeli.

### MOD-0063 — NOT_AVAILABLE
PV analytics (trend, signal, regulatory reporting) için lakehouse/dataset katmanı yok. P2: operasyonel PV runtime'ı bloke etmez ama analitik/sinyal yeteneğini bloke eder. **Uyarı:** MOD-0063 yokken PV analytics'in operasyonel DB'yi doğrudan sorgulaması bir anti-pattern olur — önlem alınmalı.

## PV İçin İlk Tamamlanması Gereken Modüller (öncelik sırası)
1. **MOD-0019** (P0) — hasta/raportör maskeleme olmadan PV açılamaz.
2. **MOD-0021 hardening** (P1) — tamper-evidence + non-repudiation.
3. **MOD-0018 extension** (P1) — `pv.*` catalog + ABAC data-scope.
4. **MOD-0040 canonical ID** (P1) — migration için ObjectId→ExternalReference.
5. **MOD-0028 FU06 fix** (P1) + **MOD-0031** (P1) — controlled evidence + evidence linking.
6. **MOD-0023 seed+smoke** (P1) — PV approval flow.
7. **MOD-0004** (P1) + **MOD-0063** (P2) — KPI/analytics.
