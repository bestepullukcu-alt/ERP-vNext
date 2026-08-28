# PV Migration Roadmap (L)

> Yaklaşım: **greenfield yeniden inşa + veri migrasyonu.** Legacy kod lift-and-shift edilmez. Her faz exit-gate ile.

## Phase 0 — Evidence & current-state confirmation
- **Amaç:** Bu analizin runtime ile teyidi + doküman düzeltmesi.
- **Kapsam:** Legacy fleet ayağa kaldır; Safety Report create/list smoke; gerçek Mongo hacmi/kalitesi; "Di10-PV/validated/FSAD" iddialarının doküman sahibiyle netleştirilmesi.
- **Bağımlılık:** —
- **Golden flow:** 1 gerçek case'in UI→Mongo yolu canlı doğrulanır.
- **Exit gate:** Bu rapordaki bulgular runtime ile çelişmiyor; doküman iddiaları düzeltilmiş.

## Phase 1 — Domain & contract foundation
- **Amaç:** PV/Regulatory bounded context'lerini registry'de açmak.
- **Kapsam:** DCP + module-id-registry rezervasyonu (Safety, Regulatory Affairs); aggregate sözleşmeleri; permission konvansiyonu (`pv.case.*`).
- **Bağımlılık:** Governance.
- **Exit gate:** Module ID'ler rezerve, boundary onaylı, RBAC anahtarları tanımlı.

## Phase 2 — Reference data / product / organization alignment
- **Amaç:** Case'in referans verilerini hazırlamak.
- **Değişecek servisler:** MOD-0048 (country/authority/pharma-form/ingredient), MOD-0290 (product/SKU), MOD-0288 (org/person/QPPV).
- **Migration işi:** Lookup + Organization + GlobalSku değer eşleme (dry-run).
- **Exit gate:** Reference/product/org SoR'ları PV'nin tüketebileceği kontratla hazır.

## Phase 3 — Core safety case intake
- **Amaç:** Case aggregate + intake API + persistence + audit + auth.
- **Kapsam:** SafetyCase CRUD; endpoint JWT+RBAC; her mutasyon MOD-0021; ekler MOD-0028.
- **Golden flow:** Yetkili kullanıcı case açar → kaydolur → audit'e düşer → doğru tenant'ta listelenir.
- **Failure path:** yetkisiz 401/403; eksik zorunlu alan 400; yanlış tenant görünmez.
- **Test:** unit + entegrasyon + authed smoke.
- **Exit gate:** Intake PASS + tenant izolasyon testi + audit kanıtı.

## Phase 4 — Case lifecycle / follow-up / versioning
- **Amaç:** Assessment (seriousness/expectedness/causality kriterli), structured follow-up, gerçek case versioning, duplicate detection.
- **Bağımlılık:** MOD-0023 (lifecycle), MOD-0024 (task).
- **Exit gate:** Versiyon geçmişi + follow-up koleksiyonu + dedupe testleri PASS.

## Phase 5 — Regulatory / reconciliation / reporting
- **Amaç:** Marketing Authorization lifecycle, Authority submission, LCPPV reconciliation süreci, PV KPI read-model.
- **Bağımlılık:** MOD-0027 (deadline notification), MOD-0026 (scheduler).
- **Exit gate:** MA lifecycle + submission + LCPPV + KPI PASS.

## Phase 6 — Data migration rehearsal
- **Amaç:** Mongo→ERP idempotent migrasyon provası.
- **Migration işi:** ExternalReference natural-key upsert; attachment içerik→MOD-0028; tenant host→kimlik eşleme; UTC normalize.
- **Exit gate:** Dry-run + referential integrity + case count + attachment reconciliation + duplicate raporu.

## Phase 7 — Validation / compliance hardening
- **Amaç:** GxP kanıt paketi.
- **Kapsam:** IQ/OQ/PQ, controlled release, traceability, e-signature, deviation/CAPA kayıtları.
- **Exit gate:** Onaylı validation package + e-sig + retention politikası.

## Phase 8 — Controlled cutover
- **Zorunlu kapılar (hepsi geçmeden hard cutover YOK):**
  1. Veri migration dry-run ✔
  2. Referential integrity ✔
  3. Tenant isolation ✔
  4. Case count reconciliation ✔
  5. Attachment reconciliation ✔
  6. Role/permission parity ✔
  7. Workflow parity ✔
  8. Audit trail doğrulaması ✔
  9. Regulatory report parity ✔
  10. Rollback rehearsal ✔
  11. Controlled validation evidence ✔
  12. Production smoke ✔
- **Exit gate:** 12/12 kapı yeşil + rollback provası başarılı.

## Phase 9 — Legacy retirement
- **Amaç:** Legacy PV servislerini read-only'ye alıp emekliye ayırmak.
- **Kapsam:** Legacy yazma kapatılır; arşiv; DitenPvLookup/host-based tenant/local-disk RETIRE.
- **Exit gate:** N gün paralel-doğrulama sonrası legacy kapatma onayı.

> **Genel not:** Phase 3'ten önce hiçbir gerçek vaka verisi ERP-vNext'e canlı yazılmamalı. Cutover, Phase 7 validation kanıtı olmadan yapılmamalı.
