# Rol İzinleri etiket borç defteri

Backlog: BL-508 (3) · İş: WP-WEB-ROLE-PERMISSION-LABELS-01 · Ölçen test: `frontend/Diten.Web/tests/role-assignments-label-ledger.test.js`

Tavan: fiil 0 · satır 96

Tavan bu satırdadır, testte değil. Test satır sayısının bu sayılara **EŞİT** olduğunu ölçer; bir satır eklemek ya da
silmek bu başlığı da düzenletir, böylece her değişiklik PR farkında başlık satırıyla görünür.

Bu defter, Rol İzinleri ekranının (`/RoleAssignments`) **7 dilde söyleyemediği** fiil ve satır (kaynak) kodlarını
listeler. Söylenemeyen kod ekranda İngilizce "insanlaştırılmış" haliyle görünür (`access` → "Access"); bu bir hata
değil, bir **borçtur** — ama BL-508'in gösterdiği gibi, İngilizce kalan bir düğme yanlış yetki verdirebilir.

- **Katalog üretimden okunur:** Auth `DataSeeder.BuildCanonicalPermissions` satırları ile modül manifestlerinin izin
  alanları (sabitler bildirildikleri yerden çözülür). Fiil ve satır kodunu üretimdeki `PermLabel.split` hesaplar.
- **Fiil** söylenmiş sayılır: `_IndexL10n.cshtml` `ActionVerbs` köprüsünde anahtarı var ve `RoleAssignmentsIndex`
  resx'inde `ActionVerb_*` değeri 7 dilde dolu.
- **Satır** söylenmiş sayılır: `SharedResource` içinde `Perm.Entity.{KOD}` (bu ekranın adı) ya da `Nav.Page.{KOD}`
  (menünün adı) 7 dilde dolu. Satır adı `Nav.Page.*` olarak **eklenmez** — o önek kenar çubuğunun ve Ctrl+K'nin.
- **Grup başlıkları bu deftere girmez:** canlı grup, son senkronun yazdığı modüldür (manifest `ModuleCode` tohumun
  `moduleOverride`'ını ezer); statik okuyucu bunu bilemez. BL-507 / BL-342'nin on grubu testte adıyla ölçülür.

Bu liste **yalnız küçülür**. Adı 7 dilde yazılan kodun satırı çıkarılır (söylenen kod defterde kalırsa test kızarır);
yeni bir modül kendi fiil ve satır adlarıyla gelir (getirmezse test kızarır).

Neden `roadmap/backlog/`: belge henüz yazılmamış metinleri anlatıyor (`docs-organization.md` §1, soru 3) ve
`page-help-ledger.md`'nin yanındadır.

## Fiiller (0)

Kataloğun bütün fiilleri 7 dilde söyleniyor.

| tür | kod | örnek anahtar |
|---|---|---|

## Satır adları (96)

| tür | kod | örnek anahtar |
|---|---|---|
| satır | `access` | `platform.document-management.access.view` |
| satır | `association-memberships.member-company` | `tep.association-memberships.member-company.manage` |
| satır | `baseline-releases` | `platform.document-management.baseline-releases.list` |
| satır | `baselines` | `platform.document-management.baselines.instantiate` |
| satır | `blocked` | `quarantine.blocked.dispose` |
| satır | `bom` | `manufacturing.bom.read` |
| satır | `business-reference-data` | `platform.businessreferencedata.read` |
| satır | `business-reference-data.consumer` | `platform.businessreferencedata.consumer.read` |
| satır | `business-reference-data.fixture` | `platform.businessreferencedata.fixture.manage` |
| satır | `business-reference-data.import` | `platform.businessreferencedata.import.preview` |
| satır | `business-reference-data.usage` | `platform.businessreferencedata.usage.register` |
| satır | `business-reference-data.version` | `platform.businessreferencedata.version.create` |
| satır | `capa` | `platform.document-management.capa.view` |
| satır | `carriers.status` | `supplychain.carriers.status.change` |
| satır | `claim` | `crm.claim.read` |
| satır | `collection-definitions` | `platform.document-management.collection-definitions.list` |
| satır | `collection-instances` | `platform.document-management.collection-instances.view` |
| satır | `concept` | `crm.knowledge.concept.read` |
| satır | `concept-link` | `crm.knowledge.concept-link.manage` |
| satır | `concept-template` | `crm.knowledge.concept-template.manage` |
| satır | `contract` | `platform.document-management.contract.view` |
| satır | `controlled-documents.access` | `platform.document-management.controlled-documents.access.manage` |
| satır | `controlled-documents.version` | `platform.document-management.controlled-documents.version.create` |
| satır | `corporate-root` | `platform.document-management.corporate-root.initialize` |
| satır | `country-legal-profile` | `crm.country-legal-profile.read` |
| satır | `decisions` | `quarantine.decisions.create` |
| satır | `deviations` | `platform.document-management.deviations.view` |
| satır | `disposition` | `platform.document-management.disposition.manage` |
| satır | `document-repository.visit-evidence` | `platform.document-repository.visit-evidence.write` |
| satır | `downtime` | `platform.document-management.downtime.view` |
| satır | `employee-projections.source-link` | `hcm.employee-projections.source-link.manage` |
| satır | `escalations` | `platform.workflow.escalations.manage` |
| satır | `events` | `platform.audit.events.append` |
| satır | `external-documents` | `platform.document-management.external-documents.view` |
| satır | `external-documents.impact` | `platform.document-management.external-documents.impact.manage` |
| satır | `external-documents.monitoring` | `platform.document-management.external-documents.monitoring.record` |
| satır | `folder-documents` | `platform.document-management.folder-documents.upload` |
| satır | `folder-documents.access` | `platform.document-management.folder-documents.access.manage` |
| satır | `folder-shares` | `platform.document-management.folder-shares.create` |
| satır | `gdocp-correction-policies` | `platform.document-management.gdocp-correction-policies.manage` |
| satır | `gdocp-corrections` | `platform.document-management.gdocp-corrections.view` |
| satır | `genealogy` | `trace.genealogy.link` |
| satır | `holds` | `quarantine.holds.read` |
| satır | `identifiers` | `platform.document-management.identifiers.view` |
| satır | `instantiations` | `platform.document-management.instantiations.dry-run` |
| satır | `legal-hold` | `platform.document-management.legal-hold.view` |
| satır | `locations` | `inventory.locations.read` |
| satır | `master-register.approval` | `platform.document-management.master-register.approval.view` |
| satır | `master-register.approval.evidence` | `platform.document-management.master-register.approval.evidence.record` |
| satır | `master-register.controlled-copy` | `platform.document-management.master-register.controlled-copy.view` |
| satır | `master-register.effectiveness` | `platform.document-management.master-register.effectiveness.read` |
| satır | `master-register.lifecycle` | `platform.document-management.master-register.lifecycle.view` |
| satır | `master-register.periodic-review` | `platform.document-management.master-register.periodic-review.view` |
| satır | `master-register.periodic-review.escalation` | `platform.document-management.master-register.periodic-review.escalation.view` |
| satır | `master-register.registration` | `platform.document-management.master-register.registration.view` |
| satır | `master-register.release-gate` | `platform.document-management.master-register.release-gate.view` |
| satır | `master-register.release-gate.evidence` | `platform.document-management.master-register.release-gate.evidence.record` |
| satır | `master-register.retirement` | `platform.document-management.master-register.retirement.approve` |
| satır | `master-register.suspension` | `platform.document-management.master-register.suspension.view` |
| satır | `master-register.training` | `platform.document-management.master-register.training.view` |
| satır | `meanings` | `esign.meanings.read` |
| satır | `movements` | `inventory.movements.create` |
| satır | `my-samples` | `esign.my-samples.manage` |
| satır | `objection-response` | `crm.objection-response.manage` |
| satır | `offboarding` | `hcm.offboarding.manage` |
| satır | `offboarding.handoff` | `hcm.offboarding.handoff.manage` |
| satır | `path` | `crm.knowledge.path.read` |
| satır | `plans` | `demand.plans.read` |
| satır | `policy` | `esign.policy.read` |
| satır | `position-assignments.reference-link` | `hcm.position-assignments.reference-link.manage` |
| satır | `product-identity.lifecycle-operations` | `mdm.product-identity.lifecycle-operations.recover` |
| satır | `product-legal-entity-scope-rollout` | `mdm.product-legal-entity-scope-rollout.activate` |
| satır | `product-legal-entity-scopes` | `mdm.product-legal-entity-scopes.read` |
| satır | `quality-bridge` | `platform.document-management.quality-bridge.manage` |
| satır | `quality-events` | `platform.document-management.quality-events.view` |
| satır | `requests` | `esign.requests.read` |
| satır | `reservations` | `inventory.reservations.manage` |
| satır | `retention` | `platform.document-management.retention.view` |
| satır | `safety-text` | `crm.safety-text.read` |
| satır | `salary-benchmarking` | `tep.salary-benchmarking.manage` |
| satır | `samples` | `esign.samples.approve` |
| satır | `signature-policies` | `platform.document-management.signature-policies.manage` |
| satır | `signatures` | `platform.document-management.signatures.view` |
| satır | `stock` | `inventory.stock.read` |
| satır | `subject` | `crm.knowledge.subject.read` |
| satır | `template-variants.local-approval` | `platform.document-management.template-variants.local-approval.record` |
| satır | `template-variants.localization` | `platform.document-management.template-variants.localization.view` |
| satır | `template-variants.translation-review` | `platform.document-management.template-variants.translation-review.record` |
| satır | `templates.version` | `platform.document-management.templates.version.create` |
| satır | `transitions` | `platform.workflow.transitions.evaluate` |
| satır | `users.account-kind` | `auth.users.account-kind.manage` |
| satır | `visit-evidence-exception` | `crm.visit-evidence-exception.read` |
| satır | `visit-evidence-policy` | `crm.visit-evidence-policy.manage` |
| satır | `visit-plan` | `crm.visit-plan.generate` |
| satır | `visit-report` | `crm.visit-report.record` |
| satır | `working-calendar.auto-fetch` | `platform.working-calendar.auto-fetch.run` |
