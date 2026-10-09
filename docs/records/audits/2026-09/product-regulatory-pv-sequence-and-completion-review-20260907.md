# Ürün, Composition, Ruhsat ve PV — sıra ve tamamlanma incelemesi

Tarih: 2026-09-07. Kapsam: plan güncellemesi ve salt-okunur code/evidence incelemesi.

## Kanıt ve %100 ölçütü

Bir modülün %100 olması, seçilen ve onaylanan kapsamın kod/test/entegrasyon/canlı kabul ve kullanıcı kontrolünün
tamamlanmasıdır. Commit/push/merge ayrı teslim durumudur; kullanıcı kabulünden önce push zorunlu değildir.
Dosya, controller veya test sınıfının bulunması testin geçtiğini kanıtlamaz. Dar MVP'nin kapanması tüm Blueprint
modülünün tamamlandığı anlamına gelmez. Bu inceleme yeni test veya canlı veri operasyonu çalıştırmadı; source ile
mevcut kayıtlı kanıtı karşılaştırdı. Bu nedenle ölçülmemiş tamamlanma yüzdeleri üretilmedi.

İncelenen kaynaklar:

- Plan/merged snapshot: `.worktrees/dcp-006-product-regulatory-pv-readiness`, HEAD `c965e5f6`.
- Yerel Product Identity adayı: `.worktrees/mod-0290-final-integration`, HEAD `ed5188dc`, 335 dirty entry, staged 0.
- Remote main bu tur yeniden fetch edilmedi; c965e5f6 bugünkü remote HEAD iddiası değildir.
- Yönetim Excel'i: `Project ongoing status report v 05 SEP 2026.xlsx`, PVG A8:K25, B48:G56, A40.
  İçindeki readiness yüzdeleri 2026-08-03 static audit'tir ve güncel yüzde olarak alınmadı.

## Önce mevcut ürün ekranlarını kapatma sırası

| Sıra | Modül/kapsam | Mevcut durum | %100 için kalan |
|---|---|---|---|
| 1 | MOD-0290 Global Product | Create/read/edit, submit/withdraw, correction, retirement request, recovery, WorkCenter; 4 Eylül kayıtlı canlı kabul | Son dirty delta üzerinde regresyon ve kullanıcının ekran kontrolü; Product/GlobalProduct ownership geniş kapsamda açık |
| 2 | MOD-0290 GSKU | Paired Revision lifecycle, edit/withdraw/correction/retirement, API/UI ve WorkCenter kodu | İki kullanıcıyla yeni akışların canlı kabulü, audit receipt, tenant izolasyonu |
| 3 | MOD-0290 LSKU | Create/read, approval/withdraw/retirement request, WorkCenter ve UI | Maker/approver canlı kabulü, audit receipt, menü/console/network kanıtı |
| 4 | MOD-0290 ABB Register | Maker-checker ve lifecycle, özel roller, remote WorkCenter provider | Yeni initial/correction/retirement WorkCenter akışlarının canlı kabulü; eski ABB smoke bunu kapatmaz |
| 5 | MOD-0290 Product Legal Entity Scope | Effective-dated policy, queries, enforcement, audit ve UI | H2 inventory/classification, Enforced activation, fresh-token scope matrisi, suspension/recovery ve kullanıcı kabulü |
| 6 | MOD-0290 Finished Good | Create/read, identity approve/reject; doğrudan retire yüzeyi | Withdrawal, correction ve retirement-request/WorkCenter parity kararı/uygulaması; actions/UI/permission ve canlı kabul |
| 7 | Brand/Product | Ayrı CRUD/archive ve manifest var | FU01/FU02 collision, Product–GlobalProduct ownership/cardinality, controlled vocabulary ve authenticated acceptance |

Bu satırların hiçbiri mevcut geniş hedef için %100 doğrulanmış sayılmadı. Global Product'ın kayıtlı bir kabulü olması
korunur; diğer modüllerdeki eksikler onun tamamlanmış geçmiş çalışmasını sıfırlamaz.

### Ürün kod ve kabul referansları

Yollar yerel aday worktree'sine göredir:

- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GskusController.cs:55`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LskusController.cs:100`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductAbbreviationWorkItemsController.cs:15`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductLegalEntityScopesController.cs:14`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.js:298`: direct retire.
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs:67`:
  source visibility ile live catalogue/menu acceptance ayrı; Finished Good hidden.
- `execution/domains/master-data-management/module-packs/MOD-0290-product-item-sku-master.md:4806`: GP recorded acceptance;
  `:4985`: GSKU blocked acceptance; `:5125`: LSKU code evidence, live acceptance open.
- `execution/domains/master-data-management/module-packs/MOD-0290-FU01-product-abbreviation-register-abb-foundation.md:698`:
  new ABB WorkCenter acceptance open.
- `execution/domains/master-data-management/module-packs/MOD-0290-FU03-product-legal-entity-scope-assignment.md:1321`:
  H2 operational acceptance open.

## Sonraki geliştirme sırası

## Mevcut PV ve ruhsat modülleri

| Modül | Code truth | %100 kapanışı |
|---|---|---|
| MOD-0230 Case Intake & Triage | API/Gateway/UI var; in-memory repository ve DenyAll permission/field-security/workflow/evidence DI | Hayır; persistence, gerçek güvenlik/servis bağlantıları ve uçtan uca kabul eksik |
| MOD-0231 Case Processing | Domain/application contracts, process-memory Dictionary ve testler | Hayır; operational API/Mongo/UI ve case lifecycle eksik |
| MOD-0232 MedDRA Coding | Contracts/in-memory servis | Hayır; lisanslı source/version/import, gerçek coding/persistence/API/UI eksik |
| MOD-0234 Signal Management | Contracts/in-memory servis | Hayır; persistence/API/UI/permissions/WorkCenter ve qualified feed yok |
| MOD-0233 Reporting & Submissions | Operational runtime bulunmadı | Hayır; submission/ack/retry/records akışı geliştirilecek |
| MOD-0235 PV Quality | Operational runtime bulunmadı | Hayır; temel QC/quality ve CAPA bağlantıları geliştirilecek |
| MOD-0236 Dossier/Submissions Management | Operational runtime bulunmadı | Hayır |
| MOD-0237 Variations & Renewals | Operational runtime bulunmadı | Hayır |
| MOD-0238 Labeling Lifecycle | Operational runtime bulunmadı | Hayır |
| MOD-0239 Country Requirements Matrix | Operational runtime bulunmadı | Hayır |

Kanıt: plan worktree `services/Diten.PvgService/src/Diten.PvgService.Api/PvgServiceApiHost.cs:30-41` in-memory ve
DenyAll registrations; `Program.cs:32` yalnız intake endpoint mapping. Case processing implementation memory
Dictionary kullanır; MedDRA/Signal service adları ve DI gerçek in-memory sınırını doğrular. Registry `:141-144`
0230/31/32/34 satırlarını içerir; ready-for-dev etiketi operational acceptance değildir. Testler bu tur koşulmadı.

## Mevcut ortak servisler — yeniden yazılmayacak, eksik consumer bağlantıları tamamlanacak

| Modül | Kodda bulunan | PV için kalan / %100 durumu |
|---|---|---|
| MOD-0018 Authorization | Gerçek permission policies, handler, tenant authorization, onboarding | PV roles/grants/scopes ve canlı allow/deny kanıtı; %100 doğrulanmadı |
| MOD-0021 Audit Trail | Append/query API, Mongo repository, outbox worker | PV intent delivery/central acknowledgement yok; %100 doğrulanmadı |
| MOD-0023 Workflow/WorkCenter | Templates/instances/tasks/transitions, repositories, generic projection/action bridge | PV DenyAll workflow adapter yerine gerçek bağlantı ve live acceptance; %100 doğrulanmadı |
| MOD-0028 Documentation | Geniş document API/persistence/UI/test kapsamı | PV source document/version/evidence/retention consumer contracts; %100 doğrulanmadı |
| MOD-0022 e-Signature | Document-specific implementation var; kod MOD-0029-FU23 altında | Canonical ownership ve PV tüketim kontratı açık; enterprise modül %100 değil |
| MOD-0030 Records | Document retention/legal-hold/disposition implementation; MOD-0029-FU15 altında | Canonical records ownership ve PV policy/reconciliation açık |
| MOD-0040 Canonical ID & Correlation | Middleware/context/event correlation primitives | Registry yanlış MOD-0288 alias; canonical/external identifier mapping yok |
| MOD-0019 Masking | Genel owner runtime bulunmadı; PV DenyAll | Field/purpose masking ve tüm output yollarının güvenliği geliştirilecek |
| MOD-0031 Evidence Linking | Generic runtime bulunmadı; yerel evidence nesneleri var | Cross-object version/provenance/coverage graph geliştirilecek |
| MOD-0004 Metric & Semantic Registry | Runtime bulunmadı; observability counters var | Governed semantic metric tanımı counters ile eşdeğer değildir |
| MOD-0063 Data Warehouse/Lakehouse | Runtime bulunmadı | Qualified analytics consumer contract; full warehouse şartı operating model'e göre |

Ortak servis kanıt yolları (plan worktree):

- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Authorization/PermissionAuthorizationHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformAuditAppendController.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/AuditEventRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/AuditOutboxWorker.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Services/WorkItemProjectionService.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementSignaturesController.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementElectronicSignature/Commands/ElectronicSignatureCommands.cs:14`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementRetentionController.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementRetention/Commands/RetentionCommands.cs:14`
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Observability/CorrelationIdMiddleware.cs`
- `execution/registries/module-id-registry.md:60,81,124,214`: planned/identity claims destekleyici governance; tek başına implementation kanıtı değildir.

## Birleştirilmiş uygulama listesi

| Grup | Modül / yetenek | İşin türü ve önceki bağımlılık |
|---|---|---|
| 0 | Identity/ownership reconciliation | DCP/FU çakışmaları ve Product/GlobalProduct kararları; yeni numeric kimlik icat edilmez |
| A1 | Substance ve Ingredient | Madde kimliği, kaynak/terminology, ingredient role, unit/strength sözleşmesi; exact parent/candidate kararı |
| A2 | Composition | Development formulation ile authorised composition ayrı; version/lineage ve ürün bağlantısı |
| A3 | Package/GTIN + MOD-0238 Labeling minimum | Paket kimliği ve published label/RSI version links; full artwork authoring ertelenebilir |
| A4 | Regulatory Product, MA/Ruhsat, Registered Presentation | Tek SoR owner, jurisdiction, authority, MAH ve version/status; foundation A3 ile paralel |
| A5 | Market Supply Assignment | Ruhsatlı presentation + market + supplying Legal Entity + LSKU/FG + geçerlilik |
| B1 | MOD-0018/0019/0021/0023/0028/0031/0040 | Mevcut auth/audit/workflow/docs genişletilir; masking/evidence/identity eksikleri code truth ile kapatılır |
| B2 | MOD-0004/0063 minimum contracts | Kullanılacak metrik/veri için tanım, provenance, ACL, freshness/quality/replay; full Lakehouse şart değildir |
| B3 | MOD-0235 PV Quality minimum | Temel QC, sorumluluk, evidence/records ve CAPA bağlantı sözleşmeleri |
| B4 | MOD-0230 Intake → MOD-0231 Case Processing → MOD-0232 MedDRA | Native case modeli seçilirse gerçek persistence, privacy, workflow ve source snapshot |
| B5 | MOD-0233 Reporting + MOD-0272 adapter / dış qualified reporting path | Native case işletiminden önce reporting clock, submission, acknowledgement ve reconciliation |
| B6 | MOD-0234 Signal Management | Manuel/evidence-driven slice veya qualified source feed; native case tamamlanmasına evrensel bağımlılık yok |
| C | Aggregate reports, RMP/PSMF, literature, PASS, safety communication, advanced RIM/SCM | İlgili iş sorumluluğu gerektirdiğinde; temel RSI/SDEA/QC/clock kuralları ilgili kullanım öncesi |

A ve B paralel planlanabilir. Bir lane'de yeni ekrana geçmeden seçilen modül kullanıcıyla kapatılır.
Material identity/mapping contract A2'de belirlenir; Material Master detayları, inventory, procurement ve üretim
recipe/BOM implementation'ı daha sonraya bırakılabilir. Eksik canonical reference serbest metinle sahteleştirilmez.

## Planın Excel ile birleştirilmesi

Excel'den alındı: açık foundation lanes, minimum metrics/data contract, masking, evidence, canonical IDs ve
urgent Signal operating-model ayrımı. Geniş Product/Composition/MA ilişkileri önceki plandan korundu.
Native case sahibi olduğumuzda reporting ve basic QC sonradan eklenemez. Dış case SoR varsa Signal önce teslim
edilebilir. Full Lakehouse ve tüm RIM modülleri her Signal kullanımının önüne zorunlu gate olarak konmadı.

Belge güncellemesi geliştirme/code-start, servis başlatma, commit veya push işlemi değildir.
