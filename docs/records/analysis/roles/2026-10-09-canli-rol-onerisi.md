# Canlı rol önerisi — kendi üretim · fason üretim · satış/dağıtım

| | |
|---|---|
| **Ölçüm** | 2026-10-09, salt okunur. Kod okundu; veritabanı ve canlı ortam **okunmadı**. |
| **Canlı** | `origin/main` @ `9ea273066` (2026-10-08) |
| **Yolda olan** | `chore/ct-round-2` @ `5897e898f` (yerel, 2026-10-09) · `feature/mdm/product-five-takeover` @ `07bcdbd8b` (yalnız yerel, origin'de yok) |
| **Okuyucu** | Ürün sahibi |

İşaretler: **[PA]** = "platform kapsamlı": kiracı yöneticisi bu izni hiçbir role veremez. **⚠** = Rol İzinleri ekranında adı kısmen
İngilizce görünür. **(elle)** = hiçbir zaman otomatik verilmez, yalnız bir kişinin bilerek atamasıyla gelir.

---

## 1. Kısa özet

- Canlıda **19 kiracı modülü** var (artı 2 yalnız-platform, 2 geliştirici örnek modülü). Kodda **568 izin anahtarı** tanımlı:
  **407**'si kiracı rollerine verilebilir, **161**'i [PA] olduğu için verilemez.
- Verilemeyen 161'in **123**'ü Doküman Yönetimi (SOP, onay, sapma, CAPA, e-imza), **13**'ü İş Akışı onayı. Yani **Kalite Güvence (QA)
  rolü bugün kiracı ekranından kurulamaz**; bu izinler yalnız sistem rolü `Admin`'e modül açılınca gelir.
- `Admin` bir modül açıldığında o modülün **bütün** izinlerini (onaylar dahil) alır ve bunlar **elle kaldırılamaz**. Bu yüzden
  `Admin` rolü yalnız 1–2 BT kişisine verilmeli, bu kişiler GxP onaycısı olmamalı.
- Üretim (parti kaydı), Kalite Kontrol (laboratuvar), stok/depo, satış siparişi/dağıtım ve fason sözleşmesi için **modül henüz yok**.
  Bu roller bugün yalnız Görev, Zaman Çizelgesi, Toplantı ve (izin sorunu çözülünce) Doküman Yönetimi ile çalışabilir.
- **Öneri:** canlıda 13 rol kurun (§4). Önce çalışan / ekip lideri / bölüm yöneticisi / İK / sistem yöneticisi / ana veri / satış /
  denetçi rollerini açın (bugün kurulabilir); QA, QC, üretim ve fason rollerini "kısmi" açın, eksikleri §7'deki boşluklarla kapatın.

---

## 2. İzinler bugün nasıl çalışıyor (rol kurmadan önce bilinmesi gereken 5 kural)

| # | Kural | Kanıt |
|---|---|---|
| 1 | Kiracı yöneticisi yalnız kiracı kapsamlı izni bir role verebilir; [PA] izin ekranda listelenmez bile. | `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs:57-61`, `.../Features/Permissions/Handlers/GetTenantAssignablePermissionsQueryHandler.cs` |
| 2 | Bir modül kiracıya açılınca `Admin` modülün **tüm** izinlerini, `Viewer` yalnız eylemi tam olarak `read` olan izinleri alır (PPM hariç). | `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs:367-375` |
| 3 | Modül veya sistem tarafından verilmiş izin elle kaldırılamaz (409); yalnız elle verilen kaldırılır. | `.../Roles/Handlers/CommandHandlers/RevokePermissionCommandHandler.cs:60-61` |
| 4 | Sekiz "geniş okuma / hassas" izin hiçbir zaman otomatik verilmez (elle). | `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs:72-77` |
| 5 | Ekrandaki izin adı = "Eylem · Ekran" (ör. `platform.tasks.assign` → "Ata · Görevler"). Çeviri yoksa İngilizce kelime gösterilir. | `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (ActionVerbs), `frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js:133-136` |

Roller düzdür: rolün içinde rol, ya da "yalnız şu şirket / şu müşteri" kısıtı yoktur (`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/Role.cs:7-20`).

---

## 3. Canlı modüller

Anahtar listesinin kaynağı: izin tohumu `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs:400-853` ve her modülün
manifesti (`services/Diten.Platform/src/Diten.Platform.Application/Features/<Modül>/SelfRegistration/*ManifestProvider.cs`,
`services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/*`). Ekran adları: `frontend/Diten.Web/Resources/SharedResource.tr.resx`
(Nav.Module / Nav.Page) ve `.../Governance/RoleAssignments/RoleAssignmentsIndex.tr.resx` (ActionVerb_*).

| # | Modül (menüdeki ad) | Ne işe yarar | Durum | İzinler — ekran adı · anahtar | Adet |
|---|---|---|---|---|---|
| 1 | Erişim Yönetişimi | Kullanıcı açar, rol tanımlar, role izin ve kişiye rol verir. | Canlı | Kullanıcılar: Görüntüle/Oluştur/Güncelle/Sil/Rol Ata/Dışa Aktar `auth.users.read/create/update/delete/assign-role/export` · Lookup⚠ `auth.users.lookup` · Lookup Validation⚠ `auth.users.lookup-validation` · Hesap türü⚠ `auth.users.account-kind.manage` (elle) · Roller: Görüntüle/Oluştur/Güncelle/Sil/İzin Ata `auth.roles.read/create/update/delete/assign-permission` (DataSeeder.cs:402-423) | 14 |
| 2 | Kiracı Ayarları | Parola/oturum güvenliği ve menü düzeni. | Canlı | Güvenlik Ayarları Görüntüle/Yönet `platform.tenant-security.read/manage` · Menü Ayarları Yönet `platform.tenant-navigation.manage` (DataSeeder.cs:632-639) | 3 |
| 3 | Organizasyon | Birimler, pozisyonlar, kim hangi koltukta; onaycı buradan bulunur. | Canlı | Organizasyon: Görüntüle/Oluştur/Güncelle/Arşivle/Sil `platform.organization-units.*` · Raporlama Hattı Güncelle `...reporting-line.update` · Özel Alanlar Görüntüle/Yönet/Write Value⚠ `...custom-fields.read/manage/write-value` · Pozisyonlar `platform.positions.read/create/update/archive/delete` · Pozisyon Atamaları `platform.position-assignments.read/create/update/delete` (DataSeeder.cs:596-624) | 18 |
| 4 | Tüzel Kişilik | Grup şirketleri (üretim sahası, satış şirketi). | Canlı | Tüzel Kişilikler: Görüntüle/Oluştur/Güncelle/Sil/Toplu Sil/Dışa Aktar `mdm.legal-entities.read/create/update/delete/bulk-delete/export` (DataSeeder.cs:425-430) | 6 |
| 5 | Görev Merkezi | Herkesin "işim nerede" ekranı: görevler ve onaylar tek listede. | Canlı, herkese açık | Gelen Kutusu Görüntüle `platform.work-aggregation.inbox.view` — her kiracı kullanıcısı zaten açar (BL-410) | 1 |
| 6 | Görev Tanımları (izin ekranında "Görevler") | Görev açma/atama/tamamlama; görev türü, şablon, yineleme ayarları. | Canlı | Görevler: Görüntüle/Oluştur/Güncelle/Ata/Üstlen/Tamamla/İptal Et/Sil/Toplu Sil `platform.tasks.read/create/update/assign/claim/complete/cancel/delete/bulk-delete` · Read All⚠ `platform.tasks.read-all` (elle) · Yönet: Alan Tanımları/Görev Türleri/Yineleme Kuralları/Checklist Templates⚠/Templates⚠ `platform.tasks.field-definitions/task-types/recurrence-rules/checklist-templates/templates.manage` · Doküman Listesi Görüntüle `platform.tasks.document-list.read` (`.../Tasks/TaskModels.cs:11-30`) | 16 |
| 7 | İş Raporu | Kişinin kendi kapsamındaki (birim/pozisyon) görevlerin raporu. | Canlı | Görüntüle · İş Raporu `platform.tasks.work-report.read` · Kiracı geneli `...work-report.read-tenant-wide` (elle) | 2 |
| 8 | Toplantılar | Toplantı, tutanak, takip görevi. | Canlı | Toplantılar: Görüntüle/Oluştur/Güncelle/Sil/Toplu Sil `platform.meetings.read/create/update/delete/bulk-delete` · Minutes Write⚠/Minutes Publish⚠ `...minutes-write/minutes-publish` · Types Manage⚠/Series Manage⚠ `...types-manage/series-manage` · Read All⚠ `platform.meetings.read-all` | 10 |
| 9 | Zaman Çizelgesi | Haftalık süre girişi, onay, kategori, ayar. | Canlı | Timesheets⚠ Görüntüle/Güncelle `time-entry.timesheets.read/update` · Approvals⚠ `time-entry.approvals.read` · Reopen Weeks⚠ `time-entry.weeks.reopen` · Categories⚠ `time-entry.categories.manage` · Settings⚠ `time-entry.settings.manage` | 6 |
| 10 | İş Akışı | Onay rotaları ve onay adımları (onayla/reddet/devret). | Canlı, **tamamı [PA]** | `platform.workflow.definitions.view/manage/publish` · `instances.start/view` · Onayla/Reddet/Devret/İptal · Görevler `tasks.approve/reject/delegate/cancel` · Request Info⚠ `tasks.request-info` · `transitions.evaluate` · `escalations.manage/run` (DataSeeder.cs:841-853) | 13 |
| 11 | Çalışma Takvimi | Tatiller ve şirket istisnaları. | Canlı | Takvim `platform.working-calendar.read/manage/activate` [PA] · İstisna Görüntüle/Yönet `platform.working-calendar.override.read/manage` | 5 |
| 12 | Doküman Yönetimi | SOP/kontrollü doküman, şablon, ana kayıt defteri, onay, eğitim, periyodik gözden geçirme, sapma/CAPA, e-imza, saklama. | Canlı, **123/124 [PA]** | Kontrollü Dokümanlar `platform.document-management.controlled-documents.view/create/version.create/version.view/share/access.manage` · Ana Kayıt `master-register.view/manage/link/audit.view/lifecycle.*/approval.view/manage/evidence.record/release-gate.view/evaluate/evidence.record/training.view/manage/verify/periodic-review.view/manage/approve-extension/suspension.view/manage/approve/retirement.approve/controlled-copy.*` · Kalite `deviations.view/manage`, `capa.view/manage`, `quality-events.view/manage` · E-imza `signatures.view/request/sign/verify/invalidate`, `signature-policies.manage` · Saklama `retention.*`, `legal-hold.*`, `disposition.manage/approve` · Erişim `access.view/manage/preview/audit.view` · şablon, KYS temel çizgisi, harici doküman anahtarları (DataSeeder.cs:646-836). Tek kiracı izni: `master-register.import`. | 124 |
| 13 | Ürün / Kalem / SKU Ana Verisi | Küresel ürün, bitmiş ürün, GSKU, yerel SKU, ürün kısaltmaları, marka listesi. | Canlı | Görüntüle/Oluştur: Küresel Ürünler `mdm.global-products.read/create`, Bitmiş Ürünler `mdm.finished-goods.read/create`, GSKU'lar `mdm.gskus.read/create`, Yerel SKU'lar `mdm.lskus.read/create` · Ürün Kısaltmaları: Görüntüle/Talep Et/İptal Et/Onayla/Reddet `mdm.product-abbreviations.read/request/cancel/approve/reject`, Correct⚠/Retire⚠/Audit⚠ `...correct/retire/audit` · Markalar Görüntüle `mdm.brands.read` · tohumda ayrıca Markalar/Ürünler Oluştur/Güncelle/Arşivle `mdm.brands.create/update/archive`, `mdm.products.read/create/update/archive` (DataSeeder.cs:437-449) | 24 |
| 14 | Müşteri İlişkileri Yönetimi (CRM) | İlaç saha gücü: müşteri hesabı (hastane, eczane, hekim), kişi, bölge, segment, kampanya, ziyaret planı/raporu, tanıtım iddiası, güvenlilik metni, rıza. | Canlı | Müşteri Hesabı `crm.account.read/create/update/delete/import/export`, Hiyerarşi/Öznitelik Yönet, Genel Bakış · Kişi `crm.contact.read/create/update/delete/import/export` · Hesap Kişisi/İlişkisi `crm.account-contact.*`, `crm.account-relationship.*` · Bölge `crm.territory.read`, Model/Düğüm `crm.territory.model/node.read/manage` · Planlı Ziyaret `crm.planned-visit.read/manage`, Confirm⚠, Read All⚠ (elle) · Visit Plan⚠ `crm.visit-plan.read/generate/apply/read-all`(elle) · Visit Report⚠ `crm.visit-report.read/record/amend` · Segment/Kampanya/Strateji Şablonu/Döngü `...read/manage/activate` · Claim⚠ `crm.claim.read/manage/approve` · Safety Text⚠ `crm.safety-text.read/manage/submit` · Rıza/Tercih `crm.consent.*`, `crm.preference.manage` · Bilgi Bankası `crm.knowledge.*` (DataSeeder.cs:454-550, CrmManifestProvider.cs) | 89 |
| 15 | Çalışan Ana Verisi | Çalışan kaydı: taslak → gönder → onay. | Canlı | Ara/Görüntüle `mod0251.employee.search/view` · Create Draft⚠ `...create_draft` · Gönder `...submit` · Onayla `...approve` · Edit Legal⚠/Edit Employment⚠/Change Status⚠/Attach Evidence⚠ · Dışa Aktar · View Sensitive⚠ · View Status History⚠ · Veri Kalitesi `mod0251.data_quality.view/resolve` (DataSeeder.cs:552-565) | 14 |
| 16 | İnsan Sermayesi | 23 İK ekranı: aday, teklif, oryantasyon, ücret-yan hak, performans, eğitim, izin, işten ayrılış, hassas erişim… | Canlı | Her alan için Görüntüle/Yönet/Değerlendir `hcm.<alan>.read/manage/evaluate` (ör. `hcm.compensation-benefits.read`) + `hcm.offboarding.review/handoff.manage/archive`, `hcm.sensitive-access.review` (HumanCapitalManifestProvider.cs) | 73 |
| 17 | Yetenek Ekosistemi | Sektör geneli yetenek ağı (aday pasaportu, referans, ücret kıyaslama). | Canlı | 30 alan × `tep.<alan>.read/manage/evaluate` + `verify/review/archive` (TalentEcosystemManifestProvider.cs) | 93 |
| 18 | Proje ve Portföy Yönetimi | Portföy, program, proje, yatırım vakası, fayda taahhüdü. | Canlı (hiç otomatik izin yok) | `ppm.<nesne>.read/create/update/change-lifecycle` · Assign Owner⚠ `ppm.portfolios.assign-owner` (elle) | 25 |
| 19 | Tedarikçiler | Tedarikçi listesi — yalnız görüntüleme. | Canlı (ilk faz) | Görüntüle · Tedarikçiler `procurement.suppliers.read` (SupplierOnboardingManifestProvider.cs) | 1 |
| – | Referans Verileri · Çalışma Takvimi İçe Aktarma | Platform ekibinin ekranları. | Yalnız platform | `platform.BusinessReferenceData.*` [PA] · `platform.working-calendar.auto-fetch.*` [PA] | 19 |
| – | Golden Slim · Golden Compact | Geliştirici örnek modülleri. | Rol önerisine alınmaz | `goldenslim.records.*`, `goldencompact.records.*` | 9 |

### Yolda olan (henüz canlıda değil)

| Ne geliyor | Dal | Yeni izin / rol | Kanıt |
|---|---|---|---|
| Ürün kimliği yaşam döngüsü (gönder, güncelle, geri çek, düzeltme/emeklilik talebi, emekli et) | product-five-takeover | `mdm.global-products/gskus/lskus/finished-goods.submit/update/withdraw/request-correction/request-retirement/retire`; roller **ProductDataSteward, ProductIdentityApprover, ProductIdentityRetirementSteward** | `.../Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs:48-166` |
| İş akışında Başlat/Onayla/Reddet kiracıya açılıyor | product-five-takeover | `platform.workflow.instances.start`, `tasks.approve`, `tasks.reject` → kiracı kapsamı | `.../Domain/Authorization/SeedOwnedTenantScopeKeys.cs:58-65` |
| Marka/Ürün ayrı modül + dışa aktarma | product-five-takeover | modül `brand-product-master`, `mdm.brands.export` | `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/BrandProductMasterManifestProvider.cs` |
| Ürün–tüzel kişi kapsamı | product-five-takeover | `mdm.product-legal-entity-scopes.read/configure/replace/end`, `...rollout.activate/rollback` | DefaultRolePermissionTemplate.cs (dal farkı) |
| Görev talebi gönderme | product-five-takeover | `platform.tasks.requests.create` | TaskManifestProvider.cs (dal farkı) |
| Rolleri dışa aktarma | ct-round-2 | `auth.roles.export` | AccessGovernanceManifestProvider.cs (dal farkı) |
| Satın alma (talep, sipariş, mal kabul, fatura eşleştirme, sözleşme, kaynak bulma) | **hiçbir dalda katalogda değil** | Kodda var (`procurement.purchase-orders.approve` vb.), izin kataloğunda yok | `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/PurchaseOrdersController.cs:76` |

---

## 4. Bugünkü roller

| Rol | Nerede | Bugün aldığı | Kanıt |
|---|---|---|---|
| SuperAdmin | Yalnız platform kiracısı | Tüm katalog ((elle) olanlar hariç) | `DefaultRolePermissionTemplate.cs:108-109`, DataSeeder.cs:1012 |
| Admin (Yönetici) | Her kiracı, sistem rolü | Erişim Yönetişimi, Tüzel Kişilik, **CRM hesap + kişi** tamamı; `platform.tenant-security.*`, `tenant-navigation.manage`, `auth.users.lookup`; + açılan her modülün tüm izinleri | `DefaultRolePermissionTemplate.cs:36,54-67,117-120`, `RoleProvisioningService.cs:13` |
| Viewer (İzleyici) | Her kiracı, sistem rolü | Kiracı kapsamlı tüm `read` izinleri (ürün okumaları ve ayarlar hariç) + açılan modülün `read`'leri | `DefaultRolePermissionTemplate.cs:73-92,127-132`, `RoleProvisioningService.cs:14` |
| ProductAbbreviation Requester / Steward / Approver / Auditor | Ürün modülü açılınca otomatik | Talep: read/request/cancel · Sorumlu: +correct/retire · Onaycı: read/approve/reject · Denetçi: read/audit | `ProductAbbreviationEntitlementGrantProfile.cs:39-61` |
| GQD, QADocumentation | Yalnız test kiracısı 97c5 | **Hiç izin yok**; onay rotasında rol adı olarak kullanılıyor | DataSeeder.cs:54-58, 2073-2133 |
| DocumentMasterRegisterLinker | Yalnız test kiracısı 97c5 | 6 Doküman Yönetimi izni | DataSeeder.cs:53,59-73 |
| Senaryo rolleri: Çalışan · Ekip Lideri · Bölüm Yöneticisi · İK Yöneticisi | Dev test senaryosu (ct-round-2) | Görev, zaman çizelgesi, toplantı izinleri | `docs/records/tests/diten-pharma-scenario.md:60-63` |

---

## 5. Önerilen canlı roller

Sütun "İş kolu": **K** = kendi üretim · **F** = fason üretim · **S** = satış/dağıtım. "Bugün kurulabilir mi" son satırda.

### R1 · Sistem Yöneticisi
| | |
|---|---|
| Amaç | Hesap açar, rol atar, güvenlik ve menü ayarlarını yönetir. İş kaydına dokunmaz. |
| Unvan / iş kolu | BT Uygulama Yöneticisi, ERP Yöneticisi · K F S |
| İzinler | Kullanıcılar: Görüntüle/Oluştur/Güncelle/Sil/Rol Ata/Dışa Aktar `auth.users.*` · Roller: Görüntüle/Oluştur/Güncelle/İzin Ata `auth.roles.*` · `auth.users.lookup` · Güvenlik/Menü Ayarları `platform.tenant-security.read/manage`, `platform.tenant-navigation.manage` · Organizasyon ve Tüzel Kişilik okuma `platform.organization-units.read`, `mdm.legal-entities.read` |
| Olmamalı | Hiçbir onay izni (`*.approve`, `platform.workflow.tasks.approve`, `signatures.sign`) — hesabı açan, işi onaylayan olmamalı. `auth.users.account-kind.manage` yalnız gerekince (elle). CRM/ürün verisi yazma. |
| Not | Hazır `Admin` rolü bu amaç için **fazla geniştir** (CRM verisi + açılan modüllerin tüm onayları, kaldırılamaz). Özel bir "Sistem Yöneticisi" rolü kurun; `Admin`'i en fazla 1–2 kişiye "acil durum" olarak bırakın. Bugün kurulabilir. |

### R2 · Bölüm Yöneticisi
| | |
|---|---|
| Amaç | Bölümünün işini görür, atar, rapor alır; haftaları onaylar. |
| Unvan / iş kolu | Üretim Müdürü, Satış Müdürü, Lojistik Müdürü · K F S |
| İzinler | Görevler: Görüntüle/Oluştur/Güncelle/Ata/Üstlen/Tamamla/İptal Et `platform.tasks.read/create/update/assign/claim/complete/cancel` · İş Raporu `platform.tasks.work-report.read` (kendi kapsamı) · Zaman Çizelgesi `time-entry.timesheets.read/update`, `time-entry.approvals.read` · Toplantılar `platform.meetings.read/create/update/minutes-write/minutes-publish` · Organizasyon/Pozisyon/Pozisyon Atamaları Görüntüle |
| Olmamalı | `platform.tasks.read-all` ve `work-report.read-tenant-wide` — bütün şirketin (İK, QA dahil) görevlerini açar; bölüm yöneticisi için `work-report.read` zaten kendi kapsamını verir. `time-entry.weeks.reopen` İK'nın işi. `delete/bulk-delete`. |
| Not | Senaryodan farkı: senaryo bu role `read-all` + `weeks.reopen` veriyor (scenario:62); öneri bunları denetçiye ve İK'ya bırakmak. Bugün kurulabilir. |

### R3 · Ekip Lideri
| | |
|---|---|
| Amaç | Ekibine görev atar, haftasını onaylar, ekip toplantısı yapar. |
| Unvan / iş kolu | Vardiya Amiri, Hat Şefi, Bölge Müdürü (saha) · K F S |
| İzinler | Görevler `platform.tasks.read/create/update/assign/claim/complete/cancel` · `platform.tasks.work-report.read` · `time-entry.timesheets.read/update`, `time-entry.approvals.read` · `platform.meetings.read/create/update/minutes-write` |
| Olmamalı | Ayar izinleri (`platform.tasks.task-types.manage` vb.), geniş okuma izinleri. Kendi haftasını onaylayamaz — sistem zaten engelliyor (`TimeEntryModels.cs:62`). Bugün kurulabilir. |

### R4 · Çalışan
| | |
|---|---|
| Amaç | Kendi görevini yapar, süresini girer, toplantısını görür. |
| Unvan / iş kolu | Operatör, teknisyen, uzman, mümessil · K F S |
| İzinler | `platform.tasks.read/create/update/claim/complete` · `time-entry.timesheets.read/update` · `platform.meetings.read` · SOP okuma `platform.document-management.controlled-documents.view` [PA] |
| Olmamalı | Atama, iptal, silme; başkasının haftasını görme. `Viewer` rolünü sıradan çalışana vermeyin (§7 G4). Bugün kurulabilir (SOP okuma hariç). |

### R5 · İK (maker: İK Uzmanı · checker: İK Müdürü)
| | |
|---|---|
| Amaç | Çalışan kaydı, pozisyon ataması, zaman çizelgesi ayarları; İK süreçleri. |
| Unvan / iş kolu | İK Uzmanı, İK Müdürü · K F S |
| İzinler — Uzman | Çalışan Ana Verisi `mod0251.employee.search/view/create_draft/submit/edit_legal/edit_employment/attach_evidence/view_status_history`, `mod0251.data_quality.view` · Pozisyon Atamaları `platform.position-assignments.read/create/update` · İnsan Sermayesi `hcm.employee-onboarding/learning-training/time-attendance-leave/offboarding.read/manage` |
| İzinler — Müdür | Uzman + `mod0251.employee.approve`, `change_status`, `export`, `mod0251.data_quality.resolve` · `hcm.<alan>.evaluate` · `hcm.compensation-benefits.read/manage` · `hcm.sensitive-access.read/review` · `time-entry.categories.manage`, `time-entry.settings.manage`, `time-entry.weeks.reopen` · Pozisyonlar `platform.positions.create/update` |
| Olmamalı | Aynı kişide `create_draft`+`approve` (taslağı açan onaylamaz). `auth.users.create` (hesap BT'den). `hcm.sensitive-access.manage` yalnız İK direktörüne. |
| Not | `mod0251.employee.approve` kodda hiçbir ekranda kullanılmıyor (§7 G7); onay adımı bugün çalışmaz. Kısmen kurulabilir. |

### R6 · Kalite Güvence (QA) — bağımsız onaycı (GxP)
| | |
|---|---|
| Amaç | SOP/doküman onayı, serbest bırakma kapısı, sapma/CAPA kapatma, e-imza, eğitim doğrulama; ürün kısaltması ve tanıtım iddiası onayı. |
| Unvan / iş kolu | QA Müdürü, Sorumlu Müdür / QP, QA Uzmanı · K F (fason müşteri adına serbest bırakma) |
| İzinler | Doküman Yönetimi [PA]: `controlled-documents.view`, `master-register.view/lifecycle.view/approval.view/approval.evidence.record/release-gate.view/release-gate.evaluate/training.verify/periodic-review.approve-extension/suspension.approve/retirement.approve`, `deviations.view`, `capa.view/manage`, `signatures.view/sign/verify` · İş Akışı [PA]: `platform.workflow.tasks.approve/reject/request-info` · `platform.workflow.instances.view` · `mdm.product-abbreviations.read/approve/reject` · `crm.claim.read/approve` · `platform.meetings.read-all` |
| Olmamalı | Onayladığı dokümanı yazma (`controlled-documents.create`, `version.create`); onay rotasını yazma (`platform.workflow.definitions.manage/publish`); imza kuralını yazma (`signature-policies.manage`); erişim matrisini yönetme (`access.manage`); üretim kaydı girme. |
| Not | **Bugün kurulamaz:** Doküman Yönetimi ve İş Akışı izinleri [PA] (§7 G1–G2). Yalnız `mdm.product-abbreviations.*` ve `crm.claim.approve` verilebilir. Onay izinleri takeover dalıyla kiracıya açılıyor. |

### R7 · Kalite Kontrol (QC)
| | |
|---|---|
| Amaç | Numune, analiz, spesifikasyon, OOS. |
| Unvan / iş kolu | QC Analisti, QC Şefi · K F |
| İzinler | **Modül henüz yok: Laboratuvar Bilgi Yönetimi (LIMS — numune, test, spesifikasyon, analiz sertifikası, OOS).** Bugün: R4 Çalışan + `deviations.view/manage` [PA] (sapma açma), `controlled-documents.view` [PA] (spesifikasyon okuma) |
| Olmamalı | Serbest bırakma (`release-gate.evaluate`), sapma/CAPA kapatma onayı, `signatures.sign` ile QA adına imza. Kısmen kurulabilir. |

### R8 · Üretim
| | |
|---|---|
| Amaç | Üretim emri, parti kaydı, hat temizliği, ekipman. |
| Unvan / iş kolu | Üretim Şefi, Operatör · K F |
| İzinler | **Modül henüz yok: Üretim Emri / Elektronik Parti Kaydı (EBR), Reçete/BOM, Ekipman-Temizlik kaydı.** Bugün: R3/R4 + `controlled-documents.view` [PA], `deviations.manage` [PA], `platform.working-calendar.override.read` |
| Olmamalı | QA onayı ve serbest bırakma; kendi partisinin kalite kararı. Kısmen kurulabilir. |

### R9 · Fason Müşteri Temsilcisi
| | |
|---|---|
| Amaç | Fason müşteri firmanın tek muhatabı: kişiler, toplantılar, iş takibi, doküman paylaşımı. |
| Unvan / iş kolu | CMO Proje Yöneticisi, Müşteri Temsilcisi · F |
| İzinler | Müşteri Hesabı `crm.account.read/update`, Genel Bakış `crm.account.overview.read` · Kişi `crm.contact.read/create/update` · Hesap Kişisi `crm.account-contact.read/manage` · Toplantılar `platform.meetings.read/create/update/minutes-write/minutes-publish` · Görevler (R3 seti) · Paylaşım `controlled-documents.view/share` [PA] · Ürün okuma `mdm.global-products.read`, `mdm.finished-goods.read` |
| Olmamalı | `crm.account.delete/import/export` (müşteriler arası gizlilik), QA onayı, ürün ana verisi yazma. |
| Not | **Modül henüz yok: Fason sözleşme + Kalite Anlaşması, müşteri siparişi, teknik transfer.** Rolde "yalnız şu müşteri" kısıtı kurulamaz (§7 G14) — temsilci tüm CRM hesaplarını görür. Kısmen kurulabilir. |

### R10 · Satış / Dağıtım (Mümessil · Satış Yöneticisi)
| | |
|---|---|
| Amaç | Saha ziyareti, ziyaret raporu, müşteri ve kampanya takibi. |
| Unvan / iş kolu | Tıbbi Mümessil, Bölge Müdürü, Satış Müdürü · S (üçüncü taraf ürünler dahil) |
| İzinler — Mümessil | `crm.account.read`, `crm.contact.read/create/update` · Planlı Ziyaret `crm.planned-visit.read/manage/confirm` · `crm.visit-plan.read/generate/apply` · `crm.visit-report.read/record/amend` · `crm.territory.read`, `crm.segment.read`, `crm.campaign.read`, `crm.knowledge.read`, `crm.claim.read`, `crm.consent.read` · `mdm.global-products.read`, `mdm.lskus.read` |
| İzinler — Yönetici | Mümessil + `crm.planned-visit.read-all`, `crm.visit-plan.read-all` (elle) · `crm.territory.model.manage/node.manage` · `crm.campaign.manage`, `crm.segment.manage` · `time-entry.approvals.read` |
| Olmamalı | `crm.claim.approve` (kendi tanıtım iddiasını onaylamak), `crm.safety-text.manage` (medikal içerik), ürün ana verisi yazma. |
| Not | **Modül henüz yok: Satış siparişi, fiyat listesi, faturalama, stok/depo, sevkiyat (İyi Dağıtım Uygulamaları), İTS/serileştirme.** CRM kısmı bugün kurulabilir. |

### R11 · Satın Alma
| | |
|---|---|
| Amaç | Tedarikçi, talep, sipariş, mal kabul. |
| Unvan / iş kolu | Satın Alma Uzmanı · K F S |
| İzinler | Bugün tek izin: Görüntüle · Tedarikçiler `procurement.suppliers.read`. Talep/sipariş/mal kabul/fatura anahtarları kodda var, katalogda yok (§7 G8). **Tedarikçi kalifikasyonu (GMP onaylı tedarikçi) modülü henüz yok.** |
| Olmamalı | (Açıldığında) `procurement.suppliers.onboard` + `procurement.purchase-orders.approve`; `procurement.grn.create` + `procurement.invoice-match.match`. Çok kısmi. |

### R12 · Ürün Ana Veri Sorumlusu
| | |
|---|---|
| Amaç | Küresel ürün, bitmiş ürün, GSKU/LSKU ve kısaltma kayıtlarını açar ve bakımını yapar. |
| Unvan / iş kolu | Ürün Ana Veri Sorumlusu, Ruhsat/Regülasyon Uzmanı · K F S |
| İzinler | `mdm.global-products.read/create`, `mdm.finished-goods.read/create`, `mdm.gskus.read/create`, `mdm.lskus.read/create` · Kısaltma Sorumlusu profili `mdm.product-abbreviations.read/request/correct/cancel/retire` · Markalar/Ürünler `mdm.brands.read/create/update/archive`, `mdm.products.read/create/update/archive` · `mdm.legal-entities.read` |
| Olmamalı | `mdm.product-abbreviations.approve/reject` — sistem kendi talebini onaylamayı zaten reddediyor (`ProductAbbreviationWorkflow.cs:203`). Gelecekte ürün onayı (ProductIdentityApprover) ve emekli etme (RetirementSteward) ayrı kişide. Bugün kurulabilir. |

### R13 · İç Denetim / Müfettiş (salt okunur)
| | |
|---|---|
| Amaç | Kimin neye yetkisi var, ne yapılmış — okur, değiştirmez. |
| Unvan / iş kolu | İç Denetçi, Uyum Sorumlusu, dış müfettişe geçici hesap · K F S |
| İzinler | `auth.users.read`, `auth.roles.read` · Organizasyon/Pozisyon okuma · `mdm.*.read`, `crm.*.read` · `platform.tasks.read` + `platform.tasks.read-all` (elle) · `platform.tasks.work-report.read-tenant-wide` (elle) · `platform.meetings.read-all` · `crm.planned-visit.read-all`, `crm.visit-plan.read-all` (elle) · Kısaltma Denetçisi `mdm.product-abbreviations.read/audit` · `platform.document-management.access.audit.view`, `master-register.audit.view` [PA] |
| Olmamalı | Her türlü `create/update/delete/approve/manage`; `export` yalnız gerekirse ve süreli. |
| Not | Kiracıda **denetim izi (audit trail) ekranı yok** (§7 G10). Kısmen kurulabilir. |

---

## 6. Görevler ayrılığı kuralları (aynı kişide birlikte olmamalı)

| # | Birlikte olmamalı | Neden | Sistem bugün engelliyor mu? |
|---|---|---|---|
| S1 | `auth.users.assign-role` + `auth.roles.assign-permission` | Kişi kendine istediği yetkiyi verebilir | Hayır; `Admin` ikisini de alır (`DefaultRolePermissionTemplate.cs:36,117-120`). Denetim günlüğü takibi + ikinci kişi şart. |
| S2 | `mdm.product-abbreviations.request/correct` + `.approve/.reject` | Yapan–onaylayan (maker-checker) | **Evet** (`ProductAbbreviationWorkflow.cs:203`); roller de ayrı (`ProductAbbreviationEntitlementGrantProfile.cs:39-61`) |
| S3 | `mod0251.employee.create_draft/submit` + `mod0251.employee.approve` | Çalışan kaydını açan onaylamaz | Ölçülemedi: `approve` hiçbir ekranda kullanılmıyor (§7 G7) |
| S4 | `crm.claim.manage` + `crm.claim.approve` | Tanıtım iddiasını yazan onaylamaz (medikal/düzenleyici onay) | Kısmen: onay iş akışından geçer, gönderen kendi akışını onaylayamaz (`WorkflowTaskTransitionSupport.cs:121`) |
| S5 | `platform.workflow.definitions.manage/publish` + `platform.workflow.tasks.approve` | Onay rotasını yazan, rotayı kendine çevirip onaylayabilir | Hayır |
| S6 | `controlled-documents.create/version.create` + `approval.evidence.record` / `signatures.sign` / `release-gate.evaluate` | GxP: yazar ≠ onaycı ≠ serbest bırakan | Kısmen: tek kişinin bütün zorunlu adımları tamamlaması reddedilir (`DocumentSegregationRuleEvaluator.cs:60`) |
| S7 | `suspension.manage`+`suspension.approve` · `disposition.manage`+`disposition.approve` · `repository-assessment.manage`+`approve` · `periodic-review.manage`+`approve-extension` · `gdocp-corrections.record`+`review` | Talep eden onaylamaz | Anahtarlar ayrı; kişi düzeyinde engel ölçülmedi |
| S8 | `signature-policies.manage` / `access.manage` + `signatures.sign` / `access.audit.view` | Kuralı yazan, kuralın denetçisi olmamalı | Hayır |
| S9 | `deviations.manage` (sapmayı açan) + CAPA kapanış onayı | Sorunu açan kendi kapanışını onaylamaz | Ayrı CAPA onay anahtarı yok (`capa.view/manage` yalnız) — boşluk |
| S10 | `time-entry.weeks.reopen` kendi haftası için | Kendi kaydını yeniden açmak | **Evet** (`ReopenTimesheetWeekHandler.cs:58`); kendi haftasına onay da **engelli** (`TimeEntryModels.cs:62`) |
| S11 | `procurement.suppliers.onboard` + `procurement.purchase-orders.approve` · `procurement.grn.create` + `procurement.invoice-match.match` | Tedarikçiyi açan sipariş onaylamaz; malı kabul eden faturayı eşleştirmez | Anahtarlar kataloğa girdiğinde rol tasarımında baştan ayrılmalı |
| S12 | `Admin` rolü + GxP onaycı pozisyonu | `Admin` açılan modülün tüm onaylarını alır, kaldırılamaz | Hayır (`EntitlementPermissionSyncService.cs:367-375`, `RevokePermissionCommandHandler.cs:60-61`) |
| S13 | `mdm.global-products.create/submit` + ürün onayı (gelecek ProductIdentityApprover) | Ürünü açan onaylamaz | Takeover dalında rol ayrımı tasarlanmış (`ProductIdentityLifecycleEntitlementGrantProfile.cs:109-166`) |

---

## 7. Karşılaştırma (kısa)

| Konu | SAP S/4HANA | Oracle Fusion | Bizde |
|---|---|---|---|
| Hazır rol şablonu | İş rolü şablonları: `SAP_BR_ADMINISTRATOR`, `SAP_BR_EMPLOYEE`, `SAP_BR_PURCHASER`, `SAP_BR_INTERNAL_SALES_REP`, `SAP_BR_PRODUCTION_PLANNER`, `SAP_BR_QUALITY_ENGINEER`, `SAP_BR_QUALITY_TECHNICIAN`, `SAP_BR_INVENTORY_MANAGER`, `SAP_BR_WAREHOUSE_CLERK`; `SAP_BR_PRODN_SUPERVISOR_PROC`, `SAP_BR_INTERNAL_AUDITOR` (doğrulanmadı) | İş (job) rolleri: Buyer, Procurement Manager, Human Resource Specialist, Production Supervisor, Order Manager, IT Security Manager; Product Data Steward, Quality Inspector (doğrulanmadı) | Yalnız `Admin`, `Viewer` ve iki modül için hazır profil (Ürün Kısaltması 4 rol; yolda Ürün Kimliği 3 rol). Diğer roller elle kurulur. |
| İzin birimi | PFCG yetki nesnesi + faaliyet (01 oluştur, 02 değiştir, 03 görüntüle) | Fonksiyon ayrıcalığı → görev (duty) rolü → iş rolü | `alan.nesne.eylem` anahtarı — SAP faaliyetine benzer; **görev (duty) katmanı yok** |
| Herkese verilen temel rol | `SAP_BR_EMPLOYEE` | Soyut roller: Employee, Line Manager (kişinin atamasına göre otomatik) | Görev Merkezi herkese açık; ama **pozisyona göre otomatik rol verme yok** — rol elle atanır |
| Organizasyon kısıtı | Türetilmiş rol: şirket kodu, üretim yeri gibi alanlarla sınırlama | Veri rolü / güvenlik profili: iş birimi, envanter organizasyonu | **Yok** — rol tüm kiracıda geçerli; yalnız birkaç "kendi kaydı / kendi kapsamı" kuralı var (ziyaret, görev, İş Raporu) |
| Görevler ayrılığı denetimi | SAP GRC Access Control: risk kuralları, telafi edici kontroller | Advanced Access Controls (Risk Management) | Genel bir SoD motoru **yok**; kodda tek tek maker-checker (S2, S4, S6, S10) |
| Hassas izin | "Kritik yetki" listesi | Ayrıcalıklı roller ayrı provizyon | (elle) listesi aynı fikir (`ExplicitGrantOnlyPermissions.cs`) |
| QA serbest bırakma | QM kullanım kararı (usage decision), parti durumu | Kalite modülü + lot durumu | Parti/numune modülü yok; yalnız doküman serbest bırakma kapısı var (ve [PA]) |

Aynı yaptığımız: eylem bazlı izin; hassas izinlerin otomatik verilmemesi; platform/kiracı yetki sınırı; modül başına hazır rol profili.
Farklı olan: rol hiyerarşisi, organizasyon/müşteri kısıtı, görev rolü katmanı, otomatik rol ataması ve merkezi SoD denetimi bizde yok.

---

## 8. Bulunan boşluklar

| # | Boşluk | Kanıt |
|---|---|---|
| G1 | Doküman Yönetimi'nin 124 izninden 123'ü [PA]: kiracı yöneticisi QA/QC/üretim rolüne SOP okuma bile veremez. | `DataSeeder.cs:646-836` (ad alanı `platform`, kapsam belirtilmemiş) + `Domain/Entities/Permission.cs:44,63` + `AssignPermissionCommandHandler.cs:57-61` |
| G2 | İş Akışı Başlat/Onayla/Reddet [PA]; Görev Merkezi'nde onay bu izinleri istiyor → bağımsız onaycı rolü kurulamaz (takeover dalı düzeltiyor). | `DataSeeder.cs:841-853`, `Features/WorkAggregation/Providers/WorkflowApprovalWorkItemProvider.cs:72-78` |
| G3 | `Viewer` yalnız eylemi `read` olanı alır; `view`/`search` kullanan modüller (Doküman Yönetimi, İş Akışı, Çalışan Ana Verisi) salt-okur rolde boş kalır. | `EntitlementPermissionSyncService.cs:371-373`, `DefaultRolePermissionTemplate.cs:127-132` |
| G4 | `Viewer` açılan modülün **tüm** `read`'lerini alır: ücret-yan hak, hassas erişim, ücret kıyaslama dahil (`hcm.compensation-benefits.read`, `hcm.sensitive-access.read`, `tep.salary-benchmarking.read`). | Aynı satırlar; anahtarlar `HumanCapitalManifestProvider.cs`, `TalentEcosystemManifestProvider.cs` |
| G5 | `Admin`'in modülden gelen izinleri (onaylar dahil) kaldırılamaz → görevler ayrılığı kurulamaz. | `RevokePermissionCommandHandler.cs:60-61` |
| G6 | `Admin` temel setinde CRM hesap ve kişi verisi var: BT yöneticisi müşteri verisini yazar/siler. | `DefaultRolePermissionTemplate.cs:36` |
| G7 | `mod0251.employee.approve`, `view_sensitive`, `view_status_history`, `data_quality.view/resolve` katalogda var, hiçbir ekranda kullanılmıyor; Çalışan Ana Verisi manifesti 14 anahtarın yalnız 2'sini bildiriyor (açılınca Admin yalnız 2 alır). | `DataSeeder.cs:554-565`, `services/Diten.HcmService/src/Diten.HcmService.Api/Controllers/Hcm/EmployeeDraftsController.cs:17-18`, `HcmEmployeeMasterManifestProvider.cs:21-22` |
| G8 | Satın alma anahtarları (talep, sipariş onayı, mal kabul, fatura eşleştirme, sözleşme, kaynak bulma, tedarikçi oluştur/onboard) kodda zorunlu, katalogda yok → kimseye verilemez. | `PurchaseOrdersController.cs:76`, `GrnController.cs:59`, `InvoiceMatchController.cs:81`, `SuppliersController.cs:131`; manifest yalnız `procurement.suppliers.read` |
| G9 | Denetim okuma izinleri kodda zorunlu, katalogda yok: `hcm.sensitive-access.audit.read`, `tep.*.audit.read`. | `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Controllers/Hcm/SensitiveAccessController.cs:45`, `services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Controllers/Tep/ReviewBoardController.cs:56` |
| G10 | Kiracıda denetim izi ekranı yok; `platform.audit.read` yalnız platform tarafında → GxP denetçisi kayıt geçmişini inceleyemez. | `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/PlatformAuditController.cs:34`, `frontend/Diten.Web/Navigation/PlatformNavigationCatalog.cs:40` |
| G11 | Ekip/kişi bazlı süre raporu izinleri (`time-entry.team-totals.read`, `person-reports.read`) listede ama henüz üretilmedi → yönetici ekibinin toplam süresini göremez. | `ExplicitGrantOnlyPermissions.cs:51,57`, `TimeEntryManifestProvider.cs:25` |
| G12 | 407 kiracı izninin yaklaşık 71'i ekranda kısmen İngilizce (ör. "Minutes Write · Toplantılar", "Görüntüle · Timesheets", "Onayla · Claim", "Confirm · Planlı Ziyaret", "Create Draft · Çalışan"). | `_IndexL10n.cshtml` ActionVerbs listesinde `confirm/audit/retire/correct/reopen/minutes-write/read-all` yok; `SharedResource.tr.resx`'te `Nav.Page.TIMESHEETS/APPROVALS/CLAIM/SAFETYTEXT` yok; `perm-label.js:133-136` |
| G13 | Ad çakışması: `platform.workflow.instances.*` ekranda "Doküman Örnekleri", `platform.workflow.tasks.*` "Görevler" diye görünür. | `SharedResource.tr.resx:609` (`Nav.Page.INSTANCES`), `perm-label.js` entityCode |
| G14 | Rolde şirket/müşteri/saha kısıtı ve rol hiyerarşisi yok → fason müşteriler arası gizlilik rolle sağlanamaz. | `Domain/Entities/Role.cs:7-20` |
| G15 | `platform.meetings.read-all` (bütün toplantılar) (elle) listesinde değil; görevdeki eşi `platform.tasks.read-all` ise listede → Toplantılar açılınca Admin kendiliğinden alır. | `Features/Meetings/MeetingModels.cs:25`, `ExplicitGrantOnlyPermissions.cs:72-77` |
| G16 | Sapma için ayrı CAPA kapanış/onay anahtarı yok (`capa.view/manage` yalnız). | `DataSeeder.cs:826-827` |
| G17 | İlaç grubu için modül yok: Üretim/EBR, QC/LIMS, stok-depo (`InventoryGovernanceController` sabit örnek veriyle çalışan, izinsiz bir sayfa), satış siparişi/dağıtım, fason sözleşme/kalite anlaşması, tedarikçi kalifikasyonu. Farmakovijilans (PVG) üretimde kapalı, Yönetim Yönetişimi yalnız yerel test kimliğiyle. | `frontend/Diten.Web/Controllers/InventoryGovernanceController.cs`, `services/Diten.PvgService/src/Diten.PvgService.Api/PvgServiceApiHost.cs:86`, `services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/Controllers/DwsStructuresController.cs:12` |
| G18 | GQD ve QADocumentation rolleri yalnız test kiracısında ve izinsiz; canlıda kalite onay rotası için karşılığı yok. | `DataSeeder.cs:54-58,2073-2133` |

---

## 9. Örnek veri fikri (demo / eğitim)

Kişiler kurgudur. İlk dördü mevcut test senaryosundaki kadrodur, aynı adlar korunur.

| Rol | Örnek kişi | Sistemde yapacağı bir iş |
|---|---|---|
| R1 Sistem Yöneticisi | Kerem Aksoy, BT Uygulama Yöneticisi | Yeni başlayan QC analisti için hesap açar, "Kalite Kontrol" rolünü atar, Denetim Günlüğü'nde kaydı görür. |
| R2 Bölüm Yöneticisi | Metin Aydın, Katı Formlar Üretim Müdürü | İş Raporu'nda bölümünün geciken "temizlik validasyonu" görevini görür, Burak'a yeniden atar. |
| R3 Ekip Lideri | Burak Şen, Granülasyon Vardiya Amiri | Ayşe'nin haftasını onaylar; vardiya devir toplantısını açıp tutanağını yazar. |
| R4 Çalışan | Ayşe Korkmaz, Üretim Operatörü | "Hat açılış kontrol listesi" görevini tamamlar, 1,5 saat süre girer. |
| R5 İK | Cem Duran, İK Müdürü · Gizem Tekin, İK Uzmanı | Gizem yeni çalışan taslağını açıp gönderir; Cem onaylar ve DEV-ENG pozisyonuna atar. |
| R6 Kalite Güvence | Dr. Leyla Şahin, QA Müdürü (Sorumlu Müdür) | Revize "Hat temizliği SOP'u"nun onay kanıtını kaydeder ve e-imzalar (izin sorunu çözülünce). |
| R7 Kalite Kontrol | Barış Yıldız, QC Analisti | Spesifikasyon dışı (OOS) sonuç için sapma kaydı açar, kontrol görevini tamamlar. |
| R8 Üretim | Hakan Demir, Steril Hat Üretim Şefi | Parti hazırlık görevini yürütür, ilgili SOP'u okur, ekibine görev atar. |
| R9 Fason Müşteri Temsilcisi | Elif Kaplan, CMO Proje Yöneticisi | Müşteri firma "Alpha Pharma AG" hesabına kalite sorumlusunu kişi olarak ekler, teknik transfer toplantısını planlayıp tutanağı yayınlar. |
| R10 Satış / Dağıtım | Onur Çelik, Tıbbi Mümessil · Deniz Ak, Bölge Müdürü | Onur haftalık ziyaret planını üretip uygular, bir hastane ziyaret raporu girer; Deniz bölgedeki tüm planlı ziyaretleri görür. |
| R11 Satın Alma | Serkan Uçar, Satın Alma Uzmanı | Etkin madde (API) tedarikçisinin kaydını Tedarikçiler listesinde açıp inceler. |
| R12 Ürün Ana Veri Sorumlusu | Burcu Er, Ürün Ana Veri Sorumlusu | Fason ürün için Küresel Ürün ve Yerel SKU açar, ürün kısaltması talep eder (onayı başka kişi verir). |
| R13 İç Denetim | Ahmet Bulut, İç Denetçi | Rol İzinleri'nde kimin onay yetkisi olduğunu inceler, Ürün Kısaltmaları denetim kanıtını görüntüler. |
