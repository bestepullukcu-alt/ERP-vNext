# GMG-CSV-URS-0001 v0.3 — ERP-vNext Document Management Gap Analizi

**Analiz sürümü:** **v2** — 2026-08-24 (v1 aynı gün; URS sahibinin yanıtı sonrası revize)
**Referans setleri:** GMG-CSV-URS-0001 v0.3 DRAFT (103 requirement) · GMG-QMS-LOG-0007 v0.36 Provisioning Register · Annex Pack v0.2 (A1/A2/A3, B, C, D) · `_HANDOVER_INDEX.txt` · **URS sahibi yanıtı (2026-08-24)**
**İncelenen implementasyon:** MOD-0028 (Documentation Structure / QMS Baseline) + MOD-0029 (Controlled Documents), `services/Diten.Platform` + `frontend/Diten.Web`
**Tür:** ANALİZ-ONLY — hiçbir kod, DB veya migration değişikliği yapılmadı.

> **Kanıt kuralı.** Bu analizde bir capability yalnızca gerçek persistence + API contract + runtime enforcement + test kanıtı varsa PASS sayılmıştır. Sınıf/dosya varlığı tek başına kanıt kabul edilmemiştir. Kanıtlar dosya yolu, sınıf adı, endpoint, permission key ve test adı düzeyinde verilmiştir.
>
> **v2 kuralı — çift yönlü kanıt.** v1 yalnızca yazılımı requirement'a karşı ölçüyordu. v2, requirement'ı da kanıta karşı ölçer: ölçülmemiş bir sayıya veya bir mekanizma dayatmasına dayanan requirement, karşılanmadığında **yazılımın gap'i olarak puanlanmaz**. Bu tür maddeler §0b'de ayrı tutulur.

## v1 → v2 değişiklik özeti

| # | Değişiklik | Kaynak |
|---|---|---|
| 1 | **P0-15 (dosya boyutu) P2'ye indi.** URS-131b ≥500 MB → **≥100 MB** olarak amend edildi (estate ölçüldü: en büyük dosya 16 MB, 50 MB üstü sıfır dosya, medyan 65 KB, toplam 210 MB). | URS sahibi |
| 2 | **W-8 (base64 → streaming rework) kritik yoldan çıktı.** 100 MB'da base64 tolere edilebilir; refactor değil, sabit değişikliği. | URS sahibi |
| 3 | **URS-060 PARTIAL → PASS.** "One transaction" bir mekanizma dayatmasıydı; sonuç olarak yeniden yazıldı, mevcut saga as-built karşılıyor. | URS sahibi |
| 4 | **R-10 ve R-11 Faz 0'a taşındı.** Admin bypass açıkken hiçbir OQ sonucu delil değeri taşımaz. | URS sahibi |
| 5 | **G-2 kapandı: ERP authoritative.** URS-123 BLOCKED'dan çıktı, canlı requirement oldu. | URS sahibi |
| 6 | **R-08 P1 → P0.** G-2'nin türev sonucu: authoritative ERP, URS-123 gereği klasör ağacını *üretmek* zorunda; üretim metadata→export path resolver'a bağlı. | Bu analiz |
| 7 | **R-09 → R-26 sıralaması kilitlendi** (geri alınamaz karar). | URS sahibi |
| 8 | **P0-5 ikiye bölündü**: signature gate (koşulsuz, şimdi) / qualified-signature stack (G-1'e bağlı, harcama yok). | URS sahibi |
| 9 | **D-1 kapandı.** Annex D artık 103 satır, 45 bağlı, 58 script bekliyor. | URS sahibi |
| 10 | **Yeni §16**: S-1…S-14'ün URS'e eklenmesi önerilen requirement karşılıkları ("spec'i yukarı çek, yazılımı aşağı değil"). | URS sahibi talimatı |
| 11 | **Yeni §0b**: URS'in kanıtla temas edip düşen requirement'ları. | Bu analiz |
| 12 | Genel completion **%32 → %33**; P0 sayısı **18 → 17**. | Hesap |

---

## 0. Önce: Doküman Setinin Kendi İç Tutarsızlıkları

Bunlar kod eksikliği değildir; **referans setinin** kendi içindeki uyumsuzluklarıdır ve remediation'dan önce kapatılmalıdır.

| # | Bulgu | Doğrulama | Etki |
|---|---|---|---|
| D-1 | ~~**URS v0.3 = 103 requirement, Annex D = 101 traced.**~~ → **KAPANDI (v2)** | v1 bulgusu: URS docx'ten 103 benzersiz URS-ID; `D_traceability_matrix.csv` = 101 veri satırı; küme farkı tam olarak `URS-071a` + `URS-071b`. **v2:** Annex D **103 satıra** tamamlandı — 45 bağlı, 58 script bekliyor (45+58=103 ✓). | Kapandı. **Kalan gözlem:** traceability boşluğu tesadüfen dağılmamıştı — tam olarak **açık blocker'ın üstünde** oturuyordu; eksik iki requirement 54 compound profile'ı kapatan URS-071a/071b'ydi. Bu, traceability'nin bir formalite değil erken uyarı sinyali olduğunun kanıtı. |
| D-2 | ~~Handover "58 of 101 requirements need a test script authored" diyor.~~ → **KAPANDI (v2)** | Baseline 103'e düzeltildi; 58 sayısı korunuyor, 45 requirement bağlı. | Kapandı. Qualification kapsamı artık tam baseline üzerinden planlanıyor. |
| D-3 | **AÇIK (v2).** Annex D artık 103 satır — ancak **tamamı hâlâ `NOT EXECUTED`**; 45 bağlı, 58 script bekliyor. | `D_traceability_matrix.csv` Status kolonu. | IQ/OQ/UAT hiç koşulmamış — URS-141/142/143/144 için kanıt sıfır. **v2 uyarısı:** P0-1 (admin bypass) kapanmadan yürütülecek qualification delil değeri taşımaz. Sıra: R-10 → sonra IQ/OQ/UAT. |
| D-4 | A1 = **32 permission profile**, A2 = **115 security group**, tamamı `PENDING` / `NOT PROVISIONED`. | `A1_permission_profiles.csv` 32 satır; `A2_security_groups.csv` 115 satır; Members kolonu tamamı `[TBC — populate from JML]`. | Permission modeli henüz onaylanmamış; ERP-vNext'te birebir karşılık üretmek **governance-blocked**. |
| D-5 | A1'de `PS-MED-RESTRICTED` durumu `BLOCKED_TAXONOMY`. | `A1_permission_profiles.csv` son kolon. | Bu profil implementasyona alınamaz. |
| D-6 | URS §19'da **10 governance kararı** açık; §21 "konfigürasyon başlamadan kapatılmalı" diyor. | URS §19, §21. | Aşağıda BLOCKED işaretli satırların kök nedeni. |
| D-7 | URS bu dokümanın kendi filing location'ının **var olmadığını** söylüyor (08_CSV'de URS type folder yok). | URS §21. | Referans setinin kendisi controlled değil — DRAFT, "confers no authority", "NOT A BUILD AUTHORISATION". |

---

## 0b. URS'in Kendi Kusurları — Kanıtla Temas Edip Düşen Requirement'lar

Gap analizinin yönü tek taraflı olamaz. Aşağıdaki maddeler **yazılımın eksiği değil, spesifikasyonun kusurudur**; URS sahibi tarafından yazılı olarak kabul edilmiş ve amend edilmiştir. Bunlar completion yüzdesinde **yazılım aleyhine puanlanmaz**.

| # | Requirement | Kusurun niteliği | Kanıt | Karar | Yazılıma etkisi |
|---|---|---|---|---|---|
| S-DEF-1 | **URS-131b** — "individual objects of at least **500 MB**" | **Ölçülmemiş sayı.** URS sahibinin ifadesi: *"a number I invented without measuring."* | Estate ölçümü: en büyük dosya **16 MB**, 50 MB üstü **sıfır** dosya, medyan **65 KB**, tüm estate **210 MB**. Mevcut 50 MB limit, var olan en büyük dosyanın **3 katı**. | **Amend → ≥100 MB** | P0-15 **P2'ye iner**; W-8 (streaming/base64 rework) **kritik yoldan çıkar**. Kalan iş: tek sabit değişikliği (aşağıda). |
| S-DEF-2 | **URS-060** — "Creation of the object and creation of its register entry shall be **one transaction**" | **Mekanizma dayatması, sonuç değil.** Bir document store'da ACID transaction şart koşmak, requirement'ı platform seçimine bağlar. | Mevcut implementasyon: `IdempotencyKey` + immutable scope snapshot + retry + fail-state ile durable saga. Sınıf yorumu zaten açıkça *"No Mongo transaction is assumed."* | **Sonuç olarak yeniden yaz** ("register kaydı olmayan controlled document kalıcı olarak var olamaz") | URS-060 **PARTIAL → PASS**, as built. |
| S-DEF-3 | **URS-143** — "A traceability matrix shall link **every** requirement… " | **Kendi annex'i tarafından ihlal edilmiş.** URS 103 requirement tanımlarken Annex D 101 satır taşıyordu. | D-1 (yukarıda). | **Annex D 103'e tamamlandı** | Kapandı. Yazılım tarafında karşılığı yok. |

> **v2'nin ikinci risk tespiti.** v1, "mevcut olgunluğun URS uyumu sanılması" riskini yazmıştı. Bunun aynası da gerçek: **URS'ü yanlış olduğu yerde doğru saymak.** Üç requirement kanıtla temas edince düştü. Bir requirement'ın "M — mandatory" işaretli olması, doğru olduğunun kanıtı değildir; test edilebilir olmasının kanıtıdır — ve test edilebilir olan, yanlış olduğunda düşer.

---

## 1. Document Management Foundation

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| document/folder/repository modeli | URS-020: konum sınıflandırmayı kodlamayacak | Model **klasör-merkezli**. `CollectionDefinition` düğümleri `PathSegment` + `FullPath` taşıyor; `ControlledDocument` zorunlu olarak bir `CollectionInstanceId` + `CollectionPath` snapshot'ına bağlanıyor. Sınıflandırma kolonları (`DepartmentDomain`, `FolderType`, `AccessProfile`, `RetentionClass`, `ControlledByGqms`, `SourceOfTruth`) **klasörde** duruyor, dokümanda değil. | `Diten.Platform.Domain/Entities/DocumentManagement/CollectionDefinition.cs` (PathSegment/FullPath/AccessProfile/DepartmentDomain); `ControlledDocument.cs` (`required Guid CollectionInstanceId`, `required string CollectionPath`) | **FAIL** | URS §5'in tam olarak yasakladığı mimari: klasör ağacı bir veritabanı içinde yeniden üretilmiş. Zone/Entity/Domain/RecordClass doküman attribute'u değil. | Sınıflandırmayı `DocumentMasterRegisterEntry` üzerinde zorunlu metadata alanlarına taşı (Zone, Entity, Domain, Type, RecordClass); klasör ağacını türetilmiş görünüme indir. | **P0** |
| document identity ve Permanent UID | URS-062: UID kontrollü sequence'ten | `DocumentIdentifierAllocationService` monotonik `DocumentIdentifierSequenceCounter` + append-only allocation ledger; iptal edilen değerler dahil **asla yeniden kullanılmıyor**. | `DocumentManagementIdentifiers/Services/DocumentIdentifierAllocationService.cs` (`GenerateUniqueAsync`, `ExistsValueIncludingDeletedAsync`, `CancelAsync` status-only); test `DocumentIdentifierAllocationTests.cs` | **PASS** | — | — | — |
| document code allocation | URS-061: allocation anında uniqueness | `GetByDocumentCodeAsync` / `GetByPermanentUidAsync` duplicate guard, 409 `DuplicateDocumentCode`. | `DocumentMasterRegisterService.cs` (create yolu, satır ~72-80) | **PASS** | Case/separator normalizasyonu (URS-017) yok. | Trim + case-insensitive + separator-normalize karşılaştırma. | P1 |
| manuel allocation yasağı | URS-062: manuel allocation'a izin verilmeyecek | `ReserveAsync` manuel/legacy değer rezerve etmeye **izin veriyor** (`IsSystemAllocated=false`, `ManualImport`). | `DocumentIdentifierAllocationService.ReserveAsync`; permission `platform.document-management.identifiers.reserve` | **PARTIAL** | Migration için makul, ama URS-062 mutlak yazılmış. | `reserve` yolunu migration-only feature flag + ayrı role arkasına al, provenance'ı raporla. | P2 |
| metadata-driven architecture | URS-021: display/export path metadata'dan türetilecek | **Yok.** Path türetme kodu yok; `FullPath` import edilmiş klasör yolunun kendisi. | `exportpath\|displaypath\|generatepath` araması → 0 hit | **NOT IMPLEMENTED** | Türetilmiş path servisi hiç yok. **v2'de kritiklik arttı:** G-2 kapandığı (ERP authoritative) için URS-123 klasör ağacının bu sistemden **üretilmesini** şart koşuyor — üretim doğrudan bu resolver'a bağlı. | Metadata → path resolver (display + export), configurable root. **R-08 · P1 → P0** | **P0** |
| storage path ile classification ayrımı | URS-022/023 | Sınıflandırma değişimi = klasör değişimi. Link'ler `Guid` üzerinden stabil (URS-023 kısmen karşılanıyor). | `ControlledDocument.FolderId`, `CollectionPath`; `DocumentMasterRegisterEntry.FolderId` | **PARTIAL** | Attribute değişimi objeyi hareket ettiriyor. | Yukarıdaki P0 refactor ile birlikte. | **P0** |
| file upload / ingestion | — | `IContentStorageGateway` üzerinden storage-first yazım; metadata ancak storage başarılı olunca commit; hata halinde best-effort orphan temizliği. | `DocumentVersioningService.StoreAsync`, `TryDeleteAsync` | **PASS** | — | — | — |
| cryptographic hash | URS-063: ingestion'da hash hesapla ve sakla | **SHA-256** hesaplanıyor ve `ContentRef.Checksum` + `ControlledDocumentVersion.Checksum` olarak saklanıyor. | `DocumentVersioningService.ComputeChecksum` (`SHA256.HashData`, lowercase hex); `ContentRef.cs` | **PASS** | — | — | — |
| duplicate detection | URS-064: hash ile mevcut içeriği tespit et, **explicit disposition** iste | Yalnızca **aynı dokümanın** aktif versiyonuna karşı byte-identical re-upload reddi. Estate genelinde hash araması yok, disposition akışı yok. | `ControlledDocumentService.cs` ~507-512 (`uploadChecksum == activeVersion.Checksum`) | **PARTIAL** | Annex B'de 25 grup byte-identical duplicate var; sistem bunları tespit edemez. | Tenant genelinde checksum index + duplicate disposition kararı (link / reject / new-instance). | P1 |
| quarantine / unclassified handling | URS-066/112: sınıflandırılamayanı reddet veya karantinaya al | Karantina/held state **yok**. `DocumentClass` ve `Criticality` default değer alıyor (`Other`, `Minor`). | `DocumentMasterRegisterEntry.cs` (`= ControlledDocumentClass.Other`, `= DocumentCriticality.Minor`) | **NOT IMPLEMENTED** | URS-113'ün açıkça yasakladığı davranış: belirsiz sınıflandırma default'a düşüyor. | `Held`/`Unclassified` register status + operasyonel kullanım engeli. | **P0** |
| OS metadata artefact filtresi | URS-065 | Yok. | — | **NOT IMPLEMENTED** | Annex B'de 140 OS metadata dosyası var; sayımlar reconcile edilemez. | Configurable exclusion rule + excluded-report. | P1 |
| export / generated path | URS-024/025/026 | Yok. 255 karakter uyarısı yok; ASCII segment kısıtı export için yok (klasör segment normalizasyonu var). | `QmsFolderTreeValidator.cs` (segment normalize / forbidden char reject) | **NOT IMPLEMENTED** | Ölçülen worst case 302 karakter; uyarı mekanizması yok. | Export path generator + uzunluk/ASCII guard. **v2: R-08 kapsamında P1 → P0** — G-2 sonrası URS-123'ün "generated" yolu doğrudan buna bağlı. | **P0** |
| search / retrieval | URS-100..104 | Klasör-çapalı, permission-filtered mixed search. Filtreler: DocumentType, Status, MasterRegisterLifecycleStatus. | `ControlledDocumentExplorerService.cs` sınıf yorumu + `DocumentManagementControlledDocumentsModels.cs` filtre record'ları | **PARTIAL** | Entity/Zone/Domain/RecordClass üzerinden arama yok; arama seçili bir Documentation Structure içine sınırlı. | Metadata-attribute arama motoru. | P1 |
| version history | URS-104 | `ControlledDocumentVersion` immutable; `VersionNumber = max+1`; UI'da VersionHistory ekranı. | `ControlledDocumentService.cs` ~519; `Views/DocumentManagement/ControlledDocuments/VersionHistory.cshtml` | **PASS** | — | — | — |

**Foundation tamamlanma (§4 + §5 + §9 = 24 requirement): 6.5 puan ≈ %27** *(v1: 6.1 ≈ %25; fark URS-060'ın PASS'a dönmesi)*

---

## 2. Controlled Documents

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| 6 onaylı state | Draft, In_Review, Approved_Pending_Effective, Effective, Superseded, Retired | 9 state modellenmiş: `Draft, InReview, ApprovedPendingEffective, Effective, UnderRevision, Suspended, Superseded, Retired, ObsoleteCopy` | `Enums/DocumentManagement/MasterRegisterEnums.cs` → `ControlledDocumentLifecycleStatus` | **PASS (fazlasıyla)** | — | GMG'nin 6 state'i tamamen kapsanıyor. `UnderRevision` / `Suspended` / `ObsoleteCopy` **GMG hedefinden daha zengin** (SOP §6.2 türevi) — korunmalı. | — |
| lifecycle state machine | URS-030/031: tanımsız transition **imkânsız** olacak | Saf, yan etkisiz transition matrisi; tanımsız geçiş 409 `INVALID_TRANSITION`. Terminal state'ler açıkça tanımlı. | `Enums/DocumentManagement/ControlledDocumentLifecyclePolicy.cs` → `AllowedTargets` / `CanTransition`; `DocumentLifecycleService.TransitionAsync`; testler: `Superseded_is_terminal_blocks_effective`, `MarkEffective_blocks_from_Draft`, `Suspended_to_Effective_reinstatement_is_blocked` | **PASS** | — | — | — |
| transition authorization | URS-032: **her transition kendi yetkili rolünü** isteyecek | Tüm transition'lar **tek** permission ile: `platform.document-management.master-register.lifecycle.manage` (generic endpoint, target status body'de). | `DocumentManagementLifecycleController.cs` — `[HasPermission(DocumentLifecyclePermissions.Manage)]` tek transition endpoint'inde | **PARTIAL** | Draft→InReview yapabilen aynı kullanıcı MarkEffective ve Retire de yapabilir. Per-transition yetki ayrımı yok. | Transition başına permission key (`...lifecycle.review`, `.approve`, `.effective`, `.retire`, `.suspend`) + fail-closed eşleme. | **P0** |
| reason / actor / timestamp / evidence | URS-032 | `DocumentLifecycleTransitionRecord`: From/To, `TransitionReason`, `EvidenceReference`, `PerformedBy`, `PerformedAt`, `CorrelationId`. Reason Suspended/Retired/Superseded için zorunlu. | `DocumentLifecycleService.RecordTransitionAsync` + `RequiresReason`; testler `Transition_record_created_for_each_transition`, `Effective_to_Suspended_requires_reason` | **PASS** | Evidence reference zorunlu değil (yalnızca warning üretiliyor). | Effective için evidence reference'ı zorunlu yap. | P1 |
| tek Effective version | URS-033: aynı document code için aynı anda **tam bir** Effective | Kural var ama **`PermanentUid`** üzerinden ve **read-then-check** ile: tüm Effective'ler listelenip conflict aranıyor. | `DocumentLifecycleService.ApplyMarkEffectiveGuardsAsync` — `_register.ListAsync(...Effective)` + `FirstOrDefault`; test `Duplicate_effective_for_same_uid_is_blocked` | **PARTIAL** | (a) Anahtar `DocumentCode` değil `PermanentUid`. (b) Unique index yok → **race condition**: iki eşzamanlı MarkEffective iki Effective üretebilir. (c) Guard yalnız `ApprovedPendingEffective → Effective` yolunda; `UnderRevision → Effective` yolunda **hiç çalışmıyor**. | Partial unique index (`PermanentUid` + `LifecycleStatus=Effective`) + guard'ı kaynak state'ten bağımsız, hedef-state bazlı hale getir. | **P0** |
| atomik supersession | URS-034: yeni Effective olurken önceki **otomatik ve atomik** Superseded olacak | Supersession **çağıran tarafından** `RelatedReplacementRegisterEntryId` ile bildirilmek zorunda; otomatik değil. İki ayrı `UpdateAsync`, transaction yok. | `DocumentLifecycleService.SupersedePreviousAsync` → `_register.UpdateAsync(previous)`, ardından ana akışta `_register.UpdateAsync(entry)` | **PARTIAL / FAIL** | **Otomatik değil** (parametre verilmezse 409 ile fail-safe durur — bu iyi, ama URS "automatically" diyor). **Atomik değil**: `previous` Superseded yazıldıktan sonra `entry` update'i düşerse **sıfır Effective** kalır. | Aynı transaction/compensation kapsamında yürüt; replacement'ı `PermanentUid`'den otomatik çöz. | **P0** |
| Superseded vs Retired ayrımı | URS-035 | İki ayrı terminal state, ayrı raporlanabilir; `SupersededByRegisterEntryId` / `SupersedesRegisterEntryId` linkajı var. | `MasterRegisterEnums.cs`; `DocumentMasterRegisterEntry.cs` supersession alanları | **PASS** | — | Annex B'deki 141 belirsiz dosyanın kararı hâlâ governance'ta (URS §19 #4). | — |
| VOID davranışı | URS-036: VOID için modellenmiş state ya da Retired'a **açık mapping** | Ne VOID state var, ne dokümante edilmiş mapping. | `ControlledDocumentLifecycleStatus` — VOID yok | **NOT IMPLEMENTED** | Effective olmadan geri çekilen içerik için ayrım yok. | VOID state ekle veya `Retired` + `RetirementReason=Void` mapping'ini kodda zorunlu kıl. | P1 · governance **BLOCKED** (URS §19 #5) |
| approval olmadan Effective'a geçememe | URS-037 (approval kaydı kısmı) | `ApprovalEvidenceStatus == "Complete"` zorunlu; değilse 409 `APPROVAL_EVIDENCE_MISSING`. Ayrıca `IApprovedPendingEffectiveGate` adapter'ı yoksa InReview→APE **fail-closed** 409. | `DocumentLifecycleService.ApplyMarkEffectiveGuardsAsync`; `TransitionAsync` approval gate bloğu | **PASS** | — | — | — |
| electronic signature gereksinimi | URS-037 (**uygulanmış e-imza** kısmı) | Effective guard'ı e-imzayı **kontrol etmiyor**. `DocumentSignatureRecord` ayrı bir feature; lifecycle'a bağlı değil. Ayrıca kayıt kendini açıkça "Part 11 / Annex 11 iddiası değildir, `ValidationResult = NotValidated`" olarak işaretliyor. | `ApplyMarkEffectiveGuardsAsync` içinde signature kontrolü yok; `DocumentSignatureRecord.cs` sınıf yorumu | **FAIL** | Approval evidence "Complete" olabilirken hiç imza atılmamış olabilir. | MarkEffective guard'ına: `Valid` statülü + doğru `SignatureMeaning` + fingerprint'i eşleşen imza şartı ekle. | **P0** |
| system-controlled version number | URS-038 | Doküman seviyesinde `nextNumber = GetMaxVersionNumberAsync + 1` (client girdisi değil). Register'daki `CurrentVersionLabel` / `CurrentVersionNumber` metadata-update yolundan **korunuyor**. | `ControlledDocumentService.cs` ~519; `DocumentMasterRegisterService.cs` protected-fields bloğu; test `Protected_uid_and_code_unchanged_by_lifecycle_transitions` | **PASS** | Draft 0.x / minor 1.x / major 2.0 semantiği yok (SOP §6.3). | Semantik versiyon etiketleme (opsiyonel). | P2 |
| parent/child components ve annex | URS-050..054 | **Yok.** Mevcut `DocumentVariant` / `TemplateVariant` = varyant/çeviri modeli, component/annex değil. `ParentDocumentUid` yalnızca bir string alan. | `DocumentVariant.cs` (`ParentControlledDocumentId`); `DocumentMasterRegisterEntry.cs` (`ParentDocumentUid/Code/VersionLabel`); component/annex aggregate için 0 hit | **NOT IMPLEMENTED** | 44 PV annex'i (MAN-0001/0002) modellenemez. Parent transition component'i taşımaz. Bağımsız Effective engeli yok. | Component aggregate + document-type başına versioning rule (with-parent / independent) + parent transition cascade. | **P0** · governance **BLOCKED** (URS §19 #3) |

**Controlled Documents tamamlanma (§6 = 9 requirement): 5.8 puan ≈ %64.** Component'ler (§8) dahil edilirse 6.0 / 14 ≈ **%43**.

---

## 3. Quality Records

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| Controlled Document'tan ayrı object class | URS-040 | Ayrım **bir flag** ile: `RegistrationKind.Record` → `IsRecord=true`, `IsControlledDocument=false`, `Controlled=false`. Aynı aggregate, aynı register, aynı storage yolu. Identifier / approval / release-gate akışları atlanıyor (doğru davranış). | `ControlledDocumentRegistrationService.cs` (`isRecord` dalları, `Controlled = !isRecord`, `IsRecord = isRecord`) | **PARTIAL** | Ayrı object class değil, aynı sınıfın bir bayrağı. Kural ayrımı kısmen var. | Ayrı `QualityRecord` aggregate + kendi repository/servis/permission ailesi. | **P0** |
| controlled-document lifecycle almaması | URS-041 | **İhlal.** Record kaydı oluşturulurken register satırına doğrudan `LifecycleStatus = Effective` yazılıyor. | `ControlledDocumentRegistrationService.cs` — `register.LifecycleStatus = isRecord ? ControlledDocumentLifecycleStatus.Effective : ControlledDocumentLifecycleStatus.Draft;` | **FAIL** | Quality record'a controlled-document lifecycle state'i **atanıyor** — requirement'ın kelimesi kelimesine yasakladığı şey. | Record'lar için `LifecycleStatus` alanını hiç yazma; ayrı `RecordState` (Open / Completed / Corrected) modeli kur. | **P0** |
| completed record overwrite/delete engeli | URS-042 (**admin dahil hiçbir rol**) | Guard **yok**. Record'lar aynı `ControlledDocumentService` version yolunu kullanıyor; bu serviste `IsRecord` hiç kontrol edilmiyor. Ayrıca `platform_admin` / `partner_admin` aktörü tüm permission kontrolünü **bypass** ediyor. | `ControlledDocumentService.cs` içinde `IsRecord` → 0 hit; `Diten.Platform.API/Security/HasPermissionAttribute.cs` — `if (isPlatformActor \|\| PermissionClaimEvaluator...) return;` | **NOT IMPLEMENTED** | Tamamlanmış bir record'un üzerine yeni versiyon yüklenebilir; platform admin her endpoint'te sınırsız. | Completed record'a version upload/edit engeli + bu kaynak sınıfı için admin bypass'ını kaldır. | **P0** |
| controlled correction / append-only | URS-043 | `DocumentGDocPCorrectionRecord` + policy + review akışı var — ancak **register alanları** için (GDocP single line-through), record içeriği için değil. | `DocumentManagementGDocPCorrection/`; test `DocumentGDocPCorrectionTrailTests.cs` | **PARTIAL** | Record içeriği için controlled correction / append yolu yok. | GDocP correction motorunu record içeriğine genişlet. | P1 |
| Record Class | URS-044: onaylı listeden **zorunlu** record class | **Hiç yok.** `RecordClass` için repoda 0 hit. `RetentionSubjectType` bir sistem-nesnesi enum'u (ControlledDocument, ApprovalEvidence, TrainingAssignment…), GMG'nin 7 quality-record class'ı değil. | `RecordClass` araması → 0 hit; `RetentionEnums.cs` → `RetentionSubjectType` | **NOT IMPLEMENTED** | Annex C'deki record class vocabulary'si hiç modellenmemiş. | Record Class controlled vocabulary + record'da zorunlu alan. | **P0** |
| retention entegrasyonu | URS-045 / URS-084 | Retention policy `RetentionSubjectType` + `RetentionClass` (serbest string) üzerinden. Record class ve legal-entity boyutu yok. | `DocumentRetentionPolicy.cs` (`SubjectType`, `RetentionClass`) | **PARTIAL** | Record class bazlı retention imkânsız (class kavramı yok). | Record Class geldikten sonra retention'ı ona bağla. | P1 |

**Quality Records tamamlanma (§7 = 6 requirement): 0.8 puan ≈ %13**

---

## 4. Register / Controlled Document Integrity

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| master register ↔ document creation bağlantısı | URS-060 **(v2'de amend edildi — sonuç bazlı: "register kaydı olmayan controlled document kalıcı olarak var olamaz")** | Durable, idempotent orkestrasyon (saga). Doküman + versiyon + register satırı tek operasyon içinde; `IdempotencyKey`, immutable scope snapshot, retry ve fail-state var. Sınıf yorumu açıkça: *"No Mongo transaction is assumed."* | `ControlledDocumentRegistrationService.cs` (`CreateAsync` / `ExecuteAsync` / `RetryAsync`); permission `platform.document-management.master-register.registration.create`; test `ControlledDocumentRegistrationFoundationTests.cs` | **PASS** *(v1: PARTIAL — bkz. §0b/S-DEF-2)* | — | Kalan iyileştirme (gap değil): `Failed` operasyonlar için reconcile job + "register'sız controlled document" invariant alarmı — geçici tutarsızlık penceresini ölçülebilir kılar. | P2 |
| register kaydı olmayan controlled document engeli | URS-060 | Kayıt akışı her zaman register satırı üretiyor; okuma tarafında register'a bağlı **olmayan** doküman ordinary user için **fail-closed**. | `DocumentAccessEvaluator.CanConsumeControlledDocumentLifecycleAsync` — "Unlinked documents deliberately fail closed for ordinary users" | **PASS** | — | — | — |
| document code uniqueness | URS-061 | Create ve record-code yollarında duplicate guard (409). | `DocumentMasterRegisterService.cs`; `ControlledDocumentRegistrationService.cs` (`DuplicateRecordCode`) | **PASS** | — | — | — |
| controlled code sequence | URS-062 | Monotonik counter + `MaxSequenceProbes` çakışma denemesi + ledger. | `DocumentIdentifierAllocationService` | **PASS** | Manuel `reserve` yolu istisna (yukarıda). | — | P2 |
| Permanent UID | URS-062 | Ayrı `DocumentIdentifierType.PermanentUid` sequence'i; register'da `PermanentUid`; lifecycle transition'ları UID'yi değiştirmiyor. | test `Protected_uid_and_code_unchanged_by_lifecycle_transitions` | **PASS** | — | — | — |
| historical code preservation | URS-016 / URS-062 | `LegacyCode`, `SourceSystem`, `SourceLegacyId` alanları register ve allocation ledger'da; iptal edilen değerler ledger'da kalıyor. | `DocumentMasterRegisterEntry.cs`; `DocumentIdentifierAllocation` | **PASS** | Legal-entity kodu kavramı yok (URS-016 entity boyutu ayrı, aşağıda). | — | — |

---

## 5. Controlled Vocabulary / Reference Data

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| vocabulary = configurable reference table | URS-010: **hard-coded değer veya klasör adı olmayacak** | Vocabulary'lerin tamamı **C# enum**: `DocumentType`, `ControlledDocumentClass`, `DocumentCriticality`, `ControlledDocumentLifecycleStatus`. Reference-data servisine (MOD-0048 BRD) **hiç bağlanmamış**. | `Enums/DocumentManagement/*.cs`; DocumentManagement feature'larında `ReferenceData` araması → 0 hit | **FAIL** | URS-010'un birebir yasakladığı şey: hard-coded değerler. Değişiklik = kod deploy. | Zone/Entity/Domain/Type/RecordClass/LifecycleState/PermissionProfile'ı MOD-0048 BRD setlerine taşı. | **P0** |
| Zone | Annex C: 13 governed zone | **Yok.** Zone kavramı domain modelinde hiç geçmiyor. | `Zone` araması (Domain projesi) → 0 hit | **NOT IMPLEMENTED** | — | Zone vocabulary + register'da zorunlu alan. | **P0** |
| Entity (legal entity) | Annex C: 7 entity container; URS-016/016a/016b: kod asla yeniden atanmaz, retired kod kalıcı rezerve, status + effective date | **Yok.** `OwnerCompanyId` / `ScopeOwnerId` var ama bunlar tenant-içi company id'leri; GMG legal-entity kod vocabulary'si (`10_GMG_AG_CH`, `20_Setonda_SL_ES` = RETIRED…) yok. Retire/yeniden-atama kuralı yok. | `DocumentMasterRegisterEntry.OwnerCompanyId`; entity-code vocabulary → 0 hit | **NOT IMPLEMENTED** | URS-016 serisinin tamamı karşılanamıyor. Setonda→GMG Ilaclari geçişi modellenemez. | Entity code vocabulary + retire/never-reassign guard. | **P0** · governance **BLOCKED** (URS §19 #10) |
| Domain | Annex C: 19 GQMS domain | Yalnızca `CollectionDefinition.DepartmentDomain` (serbest string, klasör metadata'sı). Doküman attribute'u değil. | `CollectionDefinition.cs` | **PARTIAL** | Doküman seviyesinde domain yok; TYPE↔domain kuralı (URS-015) uygulanamaz. | Domain vocabulary + doküman alanı. | **P0** |
| Function | Annex C: 15 BPG business function | `OwnerFunction` serbest string. | `DocumentMasterRegisterEntry.OwnerFunction` | **PARTIAL** | Controlled değil. | Function vocabulary. | P1 |
| Document Type | Domain başına onaylı TYPE seti (URS-015) | Global 6 değerlik enum (`Sop`, `WorkInstruction`, `Policy`, `Form`, `Template`, `Other`). Domain'e göre geçerlilik kuralı **yok**. | `ControlledDocumentEnums.cs` → `DocumentType` | **FAIL** | Annex B'deki `MAN`, `MTX`, `AGR`, `CHK`, `PLN`, `WIN`, `TPL`, `LOG` gibi TYPE kodları temsil edilemiyor; 330 HELD dosyanın büyük kısmı "NO TARGET TYPE FOLDER" gerekçeli. | Domain-scoped TYPE vocabulary. | **P0** |
| Lifecycle State | 6 state, vocabulary olarak | Enum olarak var (9 değer, GMG'nin 6'sını kapsıyor) ama configurable değil. | `MasterRegisterEnums.cs` | **PARTIAL** | Vocabulary tablosu değil. | — | P2 |
| Record Class | Annex C: 7 quality-record class | **Yok.** | 0 hit | **NOT IMPLEMENTED** | — | Record Class vocabulary. | **P0** |
| Permission Profile | A1: 32 profil | 8 profil, farklı isimlerle (`GQMS-Controlled`, `Enterprise-Restricted`, `Business-Controlled`, `Country-Controlled`, `Archive-Restricted`, `Confidential`, `Controlled-Where-Regulated`, `Site-Controlled`). GMG PS-* setiyle **hiç örtüşmüyor**. | `AccessProfileTemplateCatalog.KnownProfiles` | **PARTIAL** | 32'nin 8'i kadar; isim ve semantik farklı. | Aşağıda §6'ya bakınız. | **P0** · governance **BLOCKED** |
| free-text yerine controlled reference enforcement | URS-013 | Enum alanlarda enforce ediliyor (parse başarısız → 400). Ancak `OwnerFunction`, `ProcessOwnerRole`, `RetentionClass`, `GoverningLanguage`, `ApprovedRepositoryName` serbest metin. | `MasterRegisterWire.ParseClass/ParseCriticality`; `DocumentMasterRegisterEntry` string alanları | **PARTIAL** | Vocabulary'si olması gereken beş alan serbest metin. | BRD'ye bağla. | P1 |
| code / display name / owner / status / effective date | URS-011 | Enum'larda bu beş nitelik **yok**. Klasör tarafında register kolonları kısmen taşınıyor. | `CollectionDefinition` register kolonları | **NOT IMPLEMENTED** | — | Vocabulary entity'si bu beş alanı taşımalı. | **P0** |
| versioning / change control | URS-012 | Klasör baseline'ı için var (`BaselineRelease` Draft→Approved→Effective→Superseded + import + reconcile). Vocabulary'ler için yok. | `DocumentManagementQmsBaseline/`; test `QmsBaselineLifecycleTests.cs` | **PARTIAL** | Baseline versiyonlama mekanizması **iyi tasarlanmış** ve vocabulary'lere yeniden kullanılabilir. | Aynı deseni vocabulary setlerine uygula. | P1 |
| LOG-0007'den import + on-demand reconcile | URS-014 | **Var, ama klasör ağacı için.** CSV/XLSX/JSON parser + dry-run + commit + read-back reconciliation + deviation raporu. | `QmsBaselineImportService`, `CsvQmsFolderImportParser`, `XlsxQmsFolderImportParser`, `CollectionTreeReconciliationEngine`; UI `Views/DocumentManagement/QmsBaselines/Import.cshtml`; testler `QmsBaselineImportFoundationTests`, `QmsRegisterImportFoundationTests`, `ReconciliationAndEvidenceTests` | **PARTIAL** | Vocabulary sheet'leri (sheet 08 permission profiles, Annex C) import edilmiyor. | Vocabulary import lane'i ekle. | P1 |

**Vocabulary tamamlanma (§4 = 10 requirement): 1.45 puan ≈ %15**

---

## 6. Permissions / Access Control

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| A1'deki 32 permission profile | URS-071 | **8** access profile şablonu var, GMG PS-* isimleriyle örtüşmüyor. | `AccessProfileTemplateCatalog.KnownProfiles`; test `AccessProfileTemplateTests.cs` | **PARTIAL** | 24 profil yok; mevcut 8'i GMG semantiğine haritalanmamış. | 32 PS-* profilini vocabulary olarak tanımla, mevcut 8'i bunlara map et. | **P0** · governance **BLOCKED** (A1 tamamı `PENDING`) |
| A2 security-group modeli | 115 grup, entity-scope bazlı | Group principal tipi **var ama placeholder**: `DocumentAccessPrincipalType.Group` — enum yorumunda "placeholder until a group source exists". | `DocumentAccessMatrixEnums.cs` | **NOT IMPLEMENTED** | Grup kaynağı yok; A2'nin `[TBC — populate from JML]` üyeleri karşılanamaz. | IdP/directory group entegrasyonu + group principal resolver. | **P0** |
| A3 permission scope mapping | Location → profile eşlemesi | `CollectionDefinition.AccessProfile` kolonu ile klasör başına profil taşınıyor; `AccessProfilePolicyPlanner` bunlardan policy üretiyor. | `AccessProfilePolicyPlanner.cs`; `CollectionDefinition.AccessProfile` | **PARTIAL** | Eşleme **klasöre** bağlı — URS-072'nin yasakladığı model. | Scope eşlemesini metadata kombinasyonuna taşı. | **P0** |
| role/group-based access | URS-070: erişim **rollere** verilecek, asla istisnayla adlandırılmış kişilere | `DocumentAccessPrincipalType.User` mevcut ve resolver tarafından **onurlandırılıyor**; ayrıca `DocumentShareRecord` doğrudan kullanıcıya paylaşım sağlıyor. | `DocumentAccessMatrixEnums.cs`; `DocumentAccessEvaluator` (`_shares`) | **FAIL** | Named-user grant hem mümkün hem de aktif kullanımda. | `User` principal'ı controlled-document scope'unda kapat veya "exception + expiry + approval" zorunlu kıl. | **P0** |
| named-user exception engeli | URS-070 | Engel yok. | Yukarıdaki | **NOT IMPLEMENTED** | — | — | **P0** |
| entity segregation | URS-078 | Tenant ve company izolasyonu **güçlü** (`TenantGuard.RequireTenant`, `TenantScopedEntity`, cross-tenant testleri). Ancak GMG "legal entity" boyutu yok. | test `Cross_tenant_transition_is_blocked`, `Transition_records_are_tenant_scoped` | **PARTIAL** | Legal-entity kodu olmadan URS-078 birebir doğrulanamaz; company≠legal entity. | Entity vocabulary sonrası entity-scope guard. | P1 |
| read / create / modify / approve / dispose / external-share ayrımı | URS-071: altısı **bağımsız** | 14 değerlik action seti (View, Download, CreateDocument, CreateTemplate, EditMetadata, UploadVersion, Publish, Archive, Share, ManageAccess + 4 approval placeholder) + 123 fonksiyonel permission key. | `DocumentAccessMatrixEnums.DocumentAccessMatrixAction`; controller `[HasPermission(...)]` anahtarları | **PASS (GMG'den daha zengin)** | Approval-family action'ları (`RequestApproval/Approve/Reject/Review`) matriste **INERT** — policy'de saklanır, runtime etkisi yok. Dispose ayrı permission (`disposition.approve`) olarak var. | Approval action'larını matrise gerçekten bağla. | P1 |
| negative access enforcement | URS-074 | Deny-precedence + bilinmeyen profile **hiç grant yok** (fail-safe) + resolver default-deny. | `AccessProfileTemplateCatalog.Build` (`known=false` → boş spec); `DocumentAccessResolver` | **PASS** | — | — | — |
| denied access auditing | URS-074 / URS-091: reddedilen **her** erişim kaydedilecek | **Kaydedilmiyor.** `HasPermissionAttribute` 403 döndürüyor, audit event yazmıyor. `AuditOutcome.Denied` yalnızca `PlatformEntitlementAuditSink` ve `SelfAccessExplainService` içinde kullanılıyor — document-management enforcement yolunda değil. | `HasPermissionAttribute.PermissionDenied` (audit çağrısı yok); `AuditOutcome.Denied` yazım noktaları | **NOT IMPLEMENTED** | URS-074 ve URS-091'in ikisi birden karşılanamıyor. | Enforcement filtresine audit sink ekle (actor, permission key, resource, correlationId). | **P0** |
| **administrative bypass** | URS-070/073/082: "hiçbir rol, **administrative rol dahil**" | `actor_type = platform_admin` veya `partner_admin` ise permission değerlendirmesi **tamamen atlanıyor**. | `HasPermissionAttribute.cs` — `if (isPlatformActor \|\| PermissionClaimEvaluator.Evaluate(...).IsSatisfied) return;` | **FAIL** | Platform admin: her dokümanı okur, tamamlanmış record'u ezebilir, legal hold altındaki kaydı dispose edebilir. URS-073 ve URS-082'nin doğrudan ihlali. | Regulated kaynak sınıflarını admin bypass'ından muaf tut; break-glass'ı ayrı, audit'li ve zaman sınırlı yap. | **P0** |
| joiner / mover / leaver | URS-075 | Document-management içinde yok. | — | **NOT IMPLEMENTED** | — | JML entegrasyonu + mover'da recertification. | P1 |
| periodic access review | URS-076 | `platform.document-management.access.audit.view` ve access-matrix UI var; **review kampanyası / kanıt üretimi yok**. | `DocumentManagementAccessMatrixController`; `Views/DocumentManagement/AccessMatrix/` | **PARTIAL** | Review evidence objesi yok. | Access review campaign + attestation kaydı. | P1 |
| external sharing governance | URS-077: onaylı rota, **süre sınırlı**, kayıtlı | `DocumentSharePolicy` + `DocumentShareRecord` + `FolderShareOperation` (dry-run/execute) + `share` permission'ları. | `DocumentManagementFolderSharesController`; `FolderShareService`, `FolderSharePlanner`; UI `ShareDocument.cshtml`, `FolderShare.cshtml` | **PARTIAL** | Kayıt var; **süre sınırı (expiry) ve onay rotası** zorunlu değil; harici (organizasyon dışı) alıcı kavramı yok. | Zorunlu expiry + approval + revoke + external-recipient modeli. | P1 |
| 54 compound profile / pass-through container | URS-071a / URS-071b | Kavram **yok**. Ne compound reddi, ne pass-through container tipi. | — | **NOT IMPLEMENTED** | Handover'ın "STILL OPEN #1" maddesi. | Pass-through container node tipi (no direct filing, no independent grant) + compound value reddi. | **P0** · governance **BLOCKED — OWNER DECISION REQUIRED** (QA Documentation onayı) |

**Permissions tamamlanma (§10 = 11 requirement): 1.65 puan ≈ %15**

---

## 7. Workflow / Approval / E-signature

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| document review | SOP/URS-032 | `InReview` state + `DocumentApprovalRequirement` + route resolver. | `DocumentManagementApproval/Services/DocumentApprovalRouteResolver.cs`; test `DocumentApprovalRouteTests.cs` | **PASS** | — | — | — |
| approval + segregation | URS-032; SOP §5.1 author ≠ approver | `DocumentSegregationRuleEvaluator`; `SegregationResult.Passed/Failed`; `ApprovalReasonCodes.SegregationFailed`; `AuthorUserId` / `RequestedByUserId` / `PreparedByUserId` register'da. | `DocumentApprovalService.cs`; `DocumentMasterRegisterEntry` approval identity alanları | **PASS (GMG'den daha ayrıntılı)** | — | Korunmalı. | — |
| Effective transition gating | URS-037 | `ApprovalEvidenceStatus == Complete` zorunlu; `IApprovedPendingEffectiveGate` yoksa fail-closed. Ek olarak `IReleaseGateEvaluationPort` (FU10, 6 non-waivable gate). | `DocumentLifecycleService`; `DocumentManagementReleaseGates/`; test `DocumentReleaseGateTests.cs` | **PASS** | — | — | — |
| approval evidence | URS-032 | `DocumentApprovalEvidence` + `ApprovalEvidenceStatus` (NotRequired/Pending/Complete/Rejected/Blocked/SegregationFailed). | `DocumentApprovalEvidence.cs`; permission `...approval.evidence.record` | **PASS** | — | — | — |
| request / reject davranışı | — | Approval requirement + reject reason code'ları mevcut. | `DocumentApprovalService` | **PASS** | — | — | — |
| e-signature — **kayıt modeli** | URS-095: kayda bağlı, signer/date/time/**meaning** | `DocumentSignatureRecord`: zorunlu `MeaningStatement`, server-stamped `SignedAt` (client değeri **asla** kabul edilmiyor), `ObjectFingerprint` (canonical JSON SHA-256) ile obje state'ine bağlama, obje değişince `RequiresResign`, append-only. | `DocumentSignatureRecord.cs`; `DocumentManagementElectronicSignature/`; test `DocumentElectronicSignatureTests.cs`; UI `MasterRegister/Details.cshtml` → `tab-signatures` | **PARTIAL** | Kayıt kendini açıkça sınırlıyor: *"NOT a qualified electronic signature… `ValidationResult = NotValidated` for every signature"*. Harici sağlayıcı/sertifika doğrulaması yok. Fingerprint **metadata** hash'i, doküman byte'larının değil. | **v2: harcama G-1'e bağlandı.** Part 11 predicate rule başına, **record class başına** uygulanır; kapsam sistem geneli bir qualified-signature satın almasından çok daha dar çıkabilir. G-1 kapanmadan sağlayıcı seçimi yapılmayacak. | **P1** *(v1: P0)* · **BLOCKED (G-1)** |
| e-imza ↔ Effective bağlantısı (**signature gate**) | URS-037 | **Yok.** MarkEffective guard'ı yalnızca `ApprovalEvidenceStatus == Complete` bakıyor; imza kontrolü yok. | `DocumentLifecycleService.ApplyMarkEffectiveGuardsAsync` — signature kontrolü yok | **FAIL** | İmza atılmamışken Effective release mümkün. | **v2: koşulsuz, şimdi.** Gate bir **kontroldür** ve ucuzdur; qualified-signature kararını beklemez. Mevcut `SignatureStatus.Valid` + `SignatureMeaning` + fingerprint eşleşmesi üzerinden uygulanır. | **P0** |
| immutable approval history | URS-092 | Lifecycle ledger + approval evidence + signature append-only; hard delete yok. | `DocumentLifecycleTransitionRecord`; `AuditEvent.ValidateAppend` | **PASS** | — | — | — |
| MOD-0023 shared workflow reuse | Duplicate engine yaratılmaması | Reuse **yok**. `WorkflowIntegration` feature flag **default kapalı** (`WorkflowIntegrationEnabled` initializer'sız → false); approval feature'ında `Workflow` referansı 0 hit. | `DocumentManagementContractModels.cs` → `DocumentManagementFeatureFlagOptions`; `DocumentManagementApproval/` içinde `Workflow` → 0 hit | **PARTIAL** | MOD-0023 kullanılmıyor. | Yine de **duplicate engine değil**: DM tarafı generic workflow motoru değil, domain-specific route resolver + evidence. Bu kabul edilebilir bir tasarım; MOD-0023'e taşınırsa regulated determinizm riski artar. Karar kayda geçmeli. | P2 |

**Workflow/E-signature tamamlanma ≈ %45** (approval altyapısı güçlü; e-imzanın regulated değeri ve lifecycle bağlantısı yok)

---

## 8. Audit & Evidence

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| ALCOA+ audit trail | URS-090 | `AuditEvent`: actor (type/id/masked email/display name), `CorrelationId`, `OccurredAtUtc` + `WrittenAtUtc`, `BeforeState`/`AfterState`, `Outcome`, `SourceService`/`SourceModule`. | `Entities/Audit/AuditEvent.cs` | **PASS** | — | — | — |
| create / update | URS-091 | `IAuditableCommand` + `AuditRequestMetadata` ile komut bazlı audit. | `CommitQmsBaselineImportCommand.GetAuditMetadata()` (`AuditCategory.DocumentManagement`) | **PASS** | Tüm DM komutlarında tek tip uygulanıp uygulanmadığı endpoint bazında doğrulanmalı. | Audit coverage matrisi çıkar. | P1 |
| lifecycle transition | URS-091 | Ayrıca **domain-level ledger**: `DocumentLifecycleTransitionRecord` (from/to/reason/evidence/actor/timestamp/correlation). | `DocumentLifecycleService.RecordTransitionAsync`; test `Transition_record_created_for_each_transition` | **PASS (GMG'den daha iyi)** | — | Korunmalı. | — |
| permission change | URS-091 | Access matrix değişiklikleri audit'e giriyor; `...access.audit.view` permission'ı var. | `DocumentManagementAccessMatrix/` | **PARTIAL** | Uçtan uca doğrulanmadı. | — | P1 |
| **denied access** | URS-091: **her** reddedilen erişim | **Kaydedilmiyor.** | `HasPermissionAttribute.PermissionDenied` — audit yok | **NOT IMPLEMENTED** | — | Enforcement filtresine audit sink. | **P0** |
| legal hold | URS-091 | Hold issuance/release kararları kendi timestamp + evidence reference'larıyla saklanıyor; backdating yasak. | `DocumentLegalHold.cs` sınıf yorumu + alanlar | **PASS** | — | — | — |
| disposition | URS-091 | `DocumentDispositionRequest` + `disposition.approve` permission. | `DocumentManagementRetention/Services/DocumentDispositionService.cs` | **PASS** | — | — | — |
| approval / e-signature | URS-091/095 | Evidence + signature append-only. | Yukarıda | **PASS** | — | — | — |
| immutable audit | URS-092: **hiçbir rol** düzenleyemez/silemez | `ValidateAppend()` insert'te immutability zorluyor (IsDeleted, UpdatedAt/UpdatedBy yasak). Ancak `RedactionStatus` / `RedactedByActorId` / `RedactionReason` alanları **redaction yeteneği** olduğunu gösteriyor. | `AuditEvent.cs` | **PARTIAL** | Redaction bir mutasyon yoludur; URS-092 mutlak yazılmış. GxP kapsamında gerekçelendirilmesi gerekir. | Redaction'ı GxP kapsamı dışına kilitle veya meta-audit ile ikinci kayıt zorunlu kıl. | P1 |
| audit retention | URS-093 | `AuditEventRetentionPolicy` + `TenantAuditPreference`. | `Entities/Audit/` | **PARTIAL** | "Tanımladığı kaydın retention'ı kadar" bağlantısı kurulmuyor. | Audit retention'ı subject retention'a bağla. | P1 |
| exportable audit | URS-094 | `...master-register.audit.view` ile okuma; dokümante export yok. | permission listesi | **PARTIAL** | Vendor bağımsız export kanıtı yok. | Audit export endpoint (open format). | P1 |
| correlation / evidence linkage | URS-090 | `CorrelationId` uçtan uca (`ICorrelationContext` → command → entity → audit). | `DocumentManagementLifecycleController.CorrelationId`; entity `CorrelationId` alanları | **PASS (GMG'den daha iyi)** | — | Korunmalı. | — |

**Audit/Evidence tamamlanma (§12 = 6 requirement): 4.0 puan ≈ %67**

---

## 9. Retention / Legal Hold / Disposition

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| record-class/entity bazlı retention | URS-084 | `DocumentRetentionPolicy`: `SubjectType` + opsiyonel `RetentionClass` string + `RetentionTrigger` + `MinimumRetentionYears` + `RetainWhileEffective` + `RetainAfterRetirement/SupersessionYears` + `IsPermanentRetention`. "Longest applicable" kuralı uygulanıyor. | `DocumentRetentionPolicy.cs`; `DocumentRetentionEvaluator.cs`, `DocumentRetentionTriggerDateResolver.cs`; test `DocumentRetentionLitigationHoldTests.cs` | **PARTIAL** | **Record class boyutu yok** (vocabulary yok) ve **entity boyutu yok**. `SubjectType` sistem-nesnesi tipidir, GMG record class'ı değil. | Record Class + Entity boyutlarını policy anahtar setine ekle. | **P0** |
| legal hold object modeli | URS-080: tek obje, entity/domain/record class'ı **kapsayabilecek** | `DocumentLegalHold`: `HoldKey`, `HoldTitle`, `HoldReason`, scope tipleri (`RegisterEntry`, `ControlledDocument`, `SubjectType`, `ExternalDocument`, `Repository`, `CustomQuery`, `GlobalDocumentControl`), + `DocumentLegalHoldSubject` açık üyelik. | `DocumentLegalHold.cs`; `DocumentLegalHoldEvaluator.cs` | **PARTIAL** | `Repository` ve `CustomQuery` scope'ları **`=> false`** döndürüyor — yani engellemiyor. Entity/Domain/RecordClass ekseninde scope yok (vocabulary yok). | Eksik scope tiplerini uygula veya reddet; entity/domain/record-class scope'u ekle. | **P0** |
| hold'un retention/disposition'ı override etmesi | URS-081 | Disposition servisi hold evaluator'ı **tüketiyor**; aktif hold varsa disposition bloklanıyor. Açık `DocumentLegalHoldSubject` üyeliği scope tipi ne olursa olsun **her zaman** bloklar. | `DocumentDispositionService.cs` (`DocumentLegalHoldEvaluator _holdEvaluator`); `DocumentLegalHoldEvaluator.GetBlockingHoldsAsync` | **PARTIAL** | Disposition bloklanıyor; **archive/soft-delete** yollarında hold kontrolü kanıtlanmadı. | Tüm destructive/archival yolları hold gate'inden geçir. | P1 |
| admin dahil silememe | URS-082 | Sistemde **hard delete yok** (her yerde archive/status change) — bu yapısal olarak güçlü. Ancak `platform_admin` permission bypass'ı nedeniyle admin her endpoint'i çağırabilir. | `HasPermissionAttribute` bypass; entity'lerde `DeletedAt` soft-delete deseni | **PARTIAL** | Yapısal koruma iyi; yetki katmanında admin muafiyeti var. | Admin bypass'ını kaldır (§6 P0 ile aynı iş). | **P0** |
| disposition approval | URS-085: onay gerekecek, kayıtlı, **asla kullanıcı eylemi olmayacak** | `DocumentDispositionRequest` + ayrı `disposition.approve` / `disposition.manage` permission'ları. | `DocumentManagementRetentionController`; permission listesi | **PASS** | — | — | — |
| automatic deletion olmaması | URS-086 | **Tasarım gereği yok**: *"a policy computes a due date. It NEVER deletes, purges or archives anything — FU15 has no destruction engine."* | `DocumentRetentionPolicy.cs` sınıf yorumu | **PASS (GMG hedefiyle birebir)** | — | Korunmalı. | — |
| hold kapsamındaki her objeyi tek sorguda raporlama | URS-083 | Açık subject üyeliği için mümkün; scope-tabanlı süpürme eksik (Repository/CustomQuery inert). | `DocumentLegalHoldEvaluator` | **PARTIAL** | Tam kapsam raporu üretilemez. | Scope resolver'ı tamamla + tek sorgulu hold report endpoint'i. | P1 |
| disposition evidence | URS-085 | Hold release **hem Legal approval hem GQD concurrence** istiyor (tek onay asla yeterli değil); tüm kararlar timestamp + evidence reference taşıyor. | `DocumentLegalHold.cs` (`ReleaseLegalApprovalReference` + `ReleaseGqdConcurrenceReference`) | **PASS (GMG'den daha katı)** | — | Korunmalı. | — |

**Retention/Legal Hold tamamlanma (§11 = 7 requirement): 4.4 puan ≈ %63**

---

## 10. Search / Navigation

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| Entity / Domain / Type / Lifecycle / Record Class görünümleri | URS-101: aynı obje, çoklu navigasyon, **duplikasyon olmadan** | Yalnızca **iki** görünüm: klasör ağacı (Documentation Structure) ve Master Register grid'i. Entity/Domain/RecordClass görünümü **yok** (bu boyutlar modelde yok). Lifecycle ve Type filtreleri var. | `ControlledDocumentExplorerService`; `Views/DocumentManagement/MasterRegister/Index.cshtml` + `_Filter.cshtml` | **PARTIAL** | 5 görünümün 2'si. | Metadata boyutları geldikten sonra saved-view motoru. | P1 |
| duplicate physical object yaratılmaması | URS-101 | Register + doküman tek kayıt; ancak `DocumentShareMode.CopyOnAdopt` ve `FolderShareOutcomeStatus.Copied` **fiziksel kopya üretiyor** (`CopiedFromDocumentId` lineage ile). | `ControlledDocumentEnums.cs`; `ControlledDocument.CopiedFromDocumentId` | **PARTIAL** | Paylaşımda kopya üretimi URS-101 ile gerilimde; lineage tutulduğu için izlenebilir. | `CopyOnAdopt`'u controlled documents için kapat, `Reference` modunu zorunlu kıl. | P1 |
| permission-filtered search | URS-102 | Server-side, permission-filtered. | `ControlledDocumentExplorerService` sınıf yorumu: *"server-side, permission-filtered mixed search"*; `DocumentAccessEvaluator`; test `CollectionInstanceAccessFilterTests.cs` | **PASS** | — | — | — |
| unauthorized record existence leakage engeli | URS-102 | Yetkisiz/olmayan kayıt için tek tip `404` + `NOT_FOUND_NON_LEAKAGE` reason code — varlık ifşa edilmiyor. | `LifecycleReasonCodes.NotFoundNonLeakage`; `MasterRegisterReasonCodes`, `IdentifierAllocationReasonCodes` aynı deseni kullanıyor | **PASS (GMG'den daha disiplinli)** | — | Korunmalı. | — |
| current Effective version'a tek adımda ulaşma | URS-103 | Register'da `LifecycleStatus=Effective` filtresi + `CurrentVersionId`. Document code ile tek çağrıda "current effective" endpoint'i yok. | `MasterRegisterListFilter(LifecycleStatus)`; `ControlledDocument.CurrentVersionId` | **PARTIAL** | Tek adım değil (önce register araması, sonra doküman/versiyon çözümü). | `GET /document-master-register/by-code/{code}/effective` endpoint'i. | P1 |
| version history | URS-104 | Tam versiyon listesi + lifecycle transition ledger'ı, yetkiye bağlı. | `ControlledDocumentService` (`OrderByDescending(v => v.VersionNumber)`); `VersionHistory.cshtml`; lifecycle `transitions` endpoint'i | **PASS** | — | — | — |
| arama: her classification attribute ve kombinasyonu | URS-100 | DocumentType, Status, MasterRegisterLifecycleStatus, metin araması. Entity/Zone/Domain/RecordClass yok. | Filtre record'ları (`DocumentManagementControlledDocumentsModels.cs`) | **PARTIAL** | — | — | P1 |

**Search/Navigation tamamlanma (§13 = 5 requirement): 3.1 puan ≈ %62**

---

## 11. Migration

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| Annex B migration modeline uyum | 1.631 kaynak dosya: 1.236 promoted / 330 held / 65 staged | **Doküman migration lane'i yok.** Mevcut import **klasör tanımlarını** taşıyor, dosyaları değil. | `QmsBaselineImportService` (folder definitions); `B_migration_manifest.csv` karşılığı bir ingest yolu yok | **NOT IMPLEMENTED** | Annex B manifest'i sisteme yüklenemez. | Manifest-driven document ingestion lane. | **P0** |
| source hash verification | URS-110: kayıtlı source hash ile birlikte al ve **sonrasında doğrula** | Ingest'te SHA-256 **hesaplanıyor** ama Annex B'nin önceden kayıtlı hash'iyle **karşılaştırılmıyor** (böyle bir alan/akış yok). | `DocumentVersioningService.ComputeChecksum`; `SourceHash` karşılaştırması → yok | **PARTIAL** | Hash üretimi var, doğrulama yok. | `ExpectedSourceHash` alanı + post-ingest verify + mismatch raporu. | **P0** |
| promoted / held / staged handling | Annex B disposition kolonu | Karşılık yok. | — | **NOT IMPLEMENTED** | 330 HELD kaydın gerekçeleriyle taşınması imkânsız. | Disposition state modeli. | **P0** |
| classification bilinmeyenin default edilmemesi | URS-113 | `DocumentClass = Other`, `Criticality = Minor` **default atanıyor**. Access profile tarafında ise bilinmeyen profil **hiç grant almıyor** (doğru davranış). | `DocumentMasterRegisterEntry.cs` default'lar; `AccessProfileTemplateCatalog.Build` | **FAIL** | Sınıflandırma default'a düşüyor — URS-113'ün yasağı. | Zorunlu alan + `Unclassified` state. | **P0** |
| reversible migration | URS-114 | Yok. Baseline import'unda dry-run var, geri alma yok. | `DryRunQmsBaselineImportCommand` | **NOT IMPLEMENTED** | — | Migration set kimliği + toplu geri alma + prior-state restore. | P1 |
| source estate'in değiştirilmemesi | URS-115 | Reconciliation motoru açıkça: *"It only detects and reports differences — it never mutates, creates, moves, renames or deletes anything."* | `CollectionTreeReconciliationEngine.cs` sınıf yorumu | **PASS** | — | Korunmalı. | — |
| reconciliation report | URS-111: beklenen vs gerçekleşen, **her sapma kalem kalem** | Var ve olgun: `MissingFolder`, `RenameMismatch`, `Move`, `DuplicateFullPath`, `DuplicateSibling`, `Orphan` + severity + remediation önerisi + UI. | `CollectionTreeReconciliationEngine.Compare`; `Views/DocumentManagement/Reconciliation/Deviations.cshtml`; test `ReconciliationAndEvidenceTests.cs` | **PASS (klasör kapsamında) / PARTIAL (doküman kapsamında)** | Doküman düzeyi reconciliation yok. | Aynı motoru doküman manifest'ine genişlet. | P1 |
| idempotency | — | `IdempotencyKey` + immutable scope snapshot + `Completed` kısa devresi. | `ControlledDocumentRegistrationService.CreateAsync` | **PASS (GMG'de yok, ERP-vNext'te var)** | — | Korunmalı. | — |
| duplicate handling | URS-064 | Yukarıda §1. | — | **PARTIAL** | — | — | P1 |

**Migration tamamlanma (§14 = 6 requirement): 1.7 puan ≈ %28**

---

## 12. System of Record / Integration

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| ERP/CRM/HRIS/RIM/PV/QMS authoritative ise duplicate master oluşturmama | URS-120 | `CollectionDefinition.SourceOfTruth` kolonu register'dan taşınıyor ama **yalnızca açıklayıcı** — hiçbir runtime davranışı yok. | `CollectionDefinition.SourceOfTruth`; `CsvQmsFolderImportParser` (`source_of_truth`) | **PARTIAL** | Duplicate master engellenmiyor. | SoR bazlı yazma kilidi. | P1 |
| controlled link / read-only view | URS-120 | `ExternalDocumentRegisterEntry` + `ExternalDocumentInternalLink` + `ExternalDocumentImpactAssessment` + monitoring check — dış dokümanı **düzenlemeden** kaydediyor. | `DocumentManagementExternalDocuments/`; test `ExternalDocumentRegisterTests.cs` | **PASS (external doc kapsamında)** | ERP/CRM/HRIS/RIM sistemlerine canlı link/read-only view yok. | Sistem-bazlı connector. | P1 |
| SoR bilgisinin record-class seviyesinde yönetimi | URS-121 | Klasör seviyesinde (`SourceOfTruth`), record class seviyesinde **değil** (record class yok). | Yukarıdaki | **PARTIAL** | — | Record Class + SoR alanı. | P1 |
| export'un authoritative copy gibi davranmaması | URS-122 | **Güçlü**: `DocumentControlledCopy` + `DocumentCopyWithdrawalPlan` + `DocumentObsoleteCopyFinding` + `ObsoleteCopy` lifecycle state + `controlled-copy.reconcile` permission'ı. | `DocumentManagementControlledCopy/`; test `DocumentControlledCopyTests.cs`; UI `Details.cshtml` → `sub-copies` | **PASS (GMG'den daha olgun)** | Genel "export" çıktıları için copy-marking yok (yalnızca controlled copy nesnesi için). | Tüm export'lara watermark/copy marking. | P1 |
| go-live'da klasör ağacının bağımsız sürdürülmemesi | URS-123 | Mevcut mimari klasör ağacını **birincil** yapı olarak sürdürüyor. | §1'deki mimari bulgu (W-1) | **FAIL** *(v1: BLOCKED)* | **G-2 v2'de kapandı: ERP authoritative.** Gerekçe (URS sahibi): ERP'de lifecycle enforcement, identifier ledger, correlation-ID audit, çift onaylı legal hold, optimistic concurrency, 860 test var; klasör ağacında bunların **hiçbiri** yok. Karar verildiğine göre URS-123 artık canlı bir requirement: ağaç ya bu sistemden **üretilecek** ya resmen emekli edilecek — bugün ikisi de yapılamıyor. | **Türev sonuç:** "üretmek" = metadata→export path resolver = **R-08**. Bu nedenle R-08 **P1'den P0'a çıkarıldı**. Emekli etme yolu seçilirse R-08 P1'de kalabilir; karar kayda geçmeli. | **P0** |

---

## 13. API / AI Security

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| document search/retrieve API | URS-137: **dokümante** API | REST API `api/v1/document-management/**`, Ocelot gateway'de wildcard route, endpoint başına `[HasPermission]`. Yayımlanmış dış contract dokümanı yok (`/contract` endpoint'i modül self-description veriyor). | `gateway/Diten.ApiGateway/ocelot.json`; `DocumentManagementContractController` (`api/v1/document-management/contract`) | **PARTIAL** | Harici tüketiciler için dokümante edilmiş API spesifikasyonu yok. | OpenAPI + versioned contract yayını. | P1 |
| caller permission enforcement | URS-137/138 | Endpoint başına permission + tenant guard + kaynak seviyesinde `DocumentAccessEvaluator`. | `HasPermissionAttribute`; `TenantGuard.RequireTenant` | **PASS** — *ancak admin bypass'ı ile çelişiyor* | — | — | — |
| service-account blanket read | URS-138: **kullanılmayacak** | `platform_admin` / `partner_admin` aktörü tüm permission kontrolünü atlıyor — fiilen blanket access. | `HasPermissionAttribute.cs` | **FAIL** | URS-138'in doğrudan ihlali. | Admin bypass'ını kaldır / scope'la. | **P0** |
| AI retrieval user context | URS-138 | AI/RAG retrieval yolu **yok**; dolayısıyla ihlal de yok, karşılama da yok. | — | **NOT IMPLEMENTED** | — | Eklenirse: her zaman çağıran kullanıcının efektif izinleriyle. | P2 (bugün), P0 (AI eklenirse) |
| AI/API retrieval audit | URS-139: kullanıcı + sorgu + **dönen objeler** | Yok. Genel audit sorgu ve dönen obje listesini kaydetmiyor. | `AuditEvent` alanları | **NOT IMPLEMENTED** | — | Retrieval audit kategorisi. | P1 |

---

## 14. Non-functional

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| organizasyon sahipliği | URS-130 | Tenant-scoped; hiçbir varlık bireysel hesaba bağlı değil. | `TenantScopedEntity`; `TenantGuard` | **PASS** | — | — | — |
| backup/restore, RPO 4h / RTO 8h | URS-131 | Kod tabanında kanıt yok; yıllık restore testi kanıtı yok. | — | **NOT IMPLEMENTED / NOT EVIDENCED** | — | DR planı + yıllık restore kanıtı. | P1 |
| file size **≥ 100 MB** | URS-131b **(v2'de amend edildi: ≥500 MB → ≥100 MB, estate ölçümü sonrası — bkz. §0b/S-DEF-1)** | **50 MB** sınırı. İçerik base64 olarak taşınıyor (bellekte ~1.33x şişme). Estate'te 50 MB üstü **sıfır** dosya var; en büyük dosya 16 MB. | `DocumentManagementControlledDocumentsModels.cs` → `MaxFileSizeBytes = 52_428_800` (50 MB); `LocalFileSystemContentStorageGateway.cs` enforcement; `FileUploadInput.ContentBase64` | **PARTIAL** *(v1: FAIL)* | 50 MB hâlâ amended 100 MB'ın altında — **madde tam kapanmıyor**. Ancak fark artık mimari değil sayısal: bugünkü estate'te operasyonel etkisi sıfır. | **Tek sabit değişikliği:** `MaxFileSizeBytes` 52_428_800 → 104_857_600. Base64 transport 100 MB'da ~133 MB bellek — tolere edilebilir; streaming refactor **gerekmiyor**. | **P2** *(v1: P0)* |
| supported formats, no transformation | URS-131b | İçerik dönüştürülmüyor (byte-preserving, checksum korunuyor); format allow-list kanıtı görülmedi. | `ContentStoreRequest(... byte[] Content ...)` | **PARTIAL** | Format allow-list'i doğrulanmadı. | Format policy. | P2 |
| ingestion throughput 1.000 obje/saat | URS-131c (S) | Batch ingestion lane yok. | — | **NOT IMPLEMENTED** | — | Batch lane + performans kanıtı. | P1 |
| retrieval < 3 sn p95 | URS-131a | Performans kanıtı yok. | — | **NOT EVIDENCED** | — | Yük testi. | P1 |
| kapasite 50k obje / 500 GB / 100 eşzamanlı | URS-133 | Kanıt yok. | — | **NOT EVIDENCED** | — | Kapasite testi. | P1 |
| monitoring / alerting | URS-131d: **failed ingestion, failed backup, failed transition, permission change** | Observability altyapısı var (`ICorrelationContext`, `Diten.Platform.API/Observability`, `observability/` dizini); bu **dört** olay için alert tanımı yok. | `observability/` | **PARTIAL** | — | Dört alert kuralı. | P1 |
| IdP entegrasyonu + group membership | URS-132 | Kendi AuthService'i + JWT. Harici IdP / directory group senkronu yok. | `services/Diten.AuthService`; `DocumentAccessPrincipalType.Group` placeholder | **PARTIAL** | A2'nin 115 security group'u beslenemez. | OIDC/SAML + group claim mapping. | **P0** |
| classification depth limiti | URS-134 | Ağaç derinliği sınırsız; `ParentCanonicalId` ile serbest hiyerarşi. | `CollectionDefinition.ParentCanonicalId` | **PASS** | — | — | — |
| open-format export (termination) | URS-136: metadata + audit trail dahil | Yok. | — | **NOT IMPLEMENTED** | — | Full-tenant export (JSON/CSV + audit). | P1 |
| API | URS-137 | Yukarıda §13. | — | **PARTIAL** | — | — | P1 |
| SLA | URS-135 (S) | Sözleşmesel; kod kapsamı dışı. | — | **NOT APPLICABLE** | — | — | — |
| concurrency/capacity kontrolü | — | Optimistic concurrency: `ExpectedVersion` → 409 `STALE_VERSION`. | `DocumentLifecycleService.TransitionAsync`; test `Stale_expected_version_is_rejected` | **PASS (GMG'de istenmemiş, ERP-vNext'te var)** | — | Korunmalı. | — |

---

## 15. Validation / Qualification

| Alan | GMG Gereksinimi | Mevcut ERP-vNext Durumu | Kanıt | Durum | Eksik / Yanlış Olan | Gerekli Değişiklik | Öncelik |
|---|---|---|---|---|---|---|---|
| URS requirement → test traceability | URS-143 | **Yok.** Depoda 860 DM test metodu var ama hiçbiri URS-ID'ye bağlı değil; testler SOP-0001 diliyle yazılmış. | `services/Diten.Platform/tests/.../DocumentManagement/*.cs` — 41 dosya, 860 `[Fact]`/`[Theory]`; URS-ID referansı → 0 hit | **NOT IMPLEMENTED** | Annex D v2'de 103 satıra tamamlandı ama satırların **tamamı hâlâ `NOT EXECUTED`** (45 bağlı, 58 script bekliyor). | Test → URS-ID etiketleme + otomatik traceability raporu. **Ön koşul: R-10** — admin bypass açıkken üretilen traceability delil taşımaz. | **P0** |
| IQ | URS-142; Annex D: IQ-01..IQ-07 | Yürütülmedi. | Annex D | **NOT IMPLEMENTED** | — | IQ protokolü + yürütme. | **P0** |
| OQ | OQ-01..OQ-04 | Yürütülmedi. | Annex D | **NOT IMPLEMENTED** | — | OQ protokolü + yürütme. | **P0** |
| UAT | UAT-01, UAT-02 | Yürütülmedi. | Annex D | **NOT IMPLEMENTED** | — | UAT protokolü. | **P0** |
| negative permission tests | URS-144: **her** permission profile için | 32 profil için negatif test yok. Depoda erişim/deny testleri var ama profil-bazlı değil (`CollectionInstanceAccessFilterTests`, `AccessProfileTemplateTests` 8 şablonu kapsıyor). | Test dosyaları; A1 32 profil | **PARTIAL** | 32 profilin 8'i, negatif senaryolar eksik. | Profil başına negatif test matrisi. | **P0** |
| overwrite-prevention testleri | URS-144 | **Yok** — çünkü davranışın kendisi yok (§3). | — | **NOT IMPLEMENTED** | — | Davranış + testi birlikte. | **P0** |
| migration reconciliation | URS-111 | Klasör kapsamında test var (`ReconciliationAndEvidenceTests`), doküman kapsamında yok. | test dosyası | **PARTIAL** | — | — | P1 |
| runtime evidence | URS-145: vendor iddiasıyla kapatılamaz | Module registry'de MOD-0029-FU36/FU37 için **runtime BLOCKED** notu var: "Runtime blocked by Language 403, Retention 500 and Controlled Documents list 500". | `execution/registries/module-implementation-status.md` | **BLOCKED** | Runtime smoke şu anda geçmiyor. | Önce runtime hatalarını kapat. | **P0** |
| Annex D uygulanma durumu | — | Hiç uygulanmamış. | Annex D | **NOT IMPLEMENTED** | Ayrıca D-1: 2 requirement Annex D'de hiç yok. | Annex D'yi 103'e tamamla, sonra yürüt. | **P0** |

**Validation/Qualification tamamlanma: %0**

---

# SONUÇ

## 1. Executive Summary

Yüzdeler, ilgili URS bölümündeki requirement'lara verilen kanıt-temelli puanların (PASS=1, PARTIAL=0.2–0.9, FAIL/NOT IMPLEMENTED=0) toplamıdır. Tahmin veya iyimser yuvarlama yapılmamıştır.

| Alan | URS kapsamı | Puan | **Tamamlanma** | v1'den fark |
|---|---|---|---|---|
| Controlled Documents | §6 (9 req) | 5.8 / 9 | **%64** | — |
| — components dahil | §6+§8 (14 req) | 6.0 / 14 | %43 | — |
| Document Management Foundation | §4+§5+§9 (24 req) | **6.5** / 24 | **%27** | ▲ URS-060 PASS |
| Quality Records | §7 (6 req) | 0.8 / 6 | **%13** | — |
| Permissions | §10 (11 req) | 1.65 / 11 | **%15** | — |
| Workflow / E-signature | §6 kısmi + URS-095 + approval altyapısı | — | **%45** | — |
| Audit / Evidence | §12 (6 req) | 4.0 / 6 | **%67** | — |
| Retention / Legal Hold | §11 (7 req) | 4.4 / 7 | **%63** | — |
| Migration | §14 (6 req) | 1.7 / 6 | **%28** | — |
| Validation / Qualification | §17 (4 test edilebilir req) | 0 / 4 | **%0** | — |
| Search / Navigation | §13 (5 req) | 3.1 / 5 | %62 | — |
| System of Record | §15 (4 req) | 1.0 / 4 | %25 | — |
| Non-functional / API / AI | §16 (13 req) | **3.9** / 13 | **%30** | ▲ URS-131b amend |

### Genel weighted completion: **33.05 / 100 ≈ %33** *(v1: %32)*

*(103 requirement'ın 3'ü — URS-135 SLA, URS-140 supplier assessment, URS-145 closure policy — yazılım kapsamı dışı sayılarak 100 üzerinden hesaplanmıştır. URS-131b, amended ≥100 MB hedefine karşı **tam puan** almıştır: kalan 50→100 MB farkı tek sabit değişikliğidir ve mimari bir eksiklik değildir.)*

> **Rakamın az oynaması kasıtlıdır.** İki spec düzeltmesi puanı **1.4 puan** hareket ettirdi. Bu, düzeltmelerin skoru şişirmek için değil, **adalet için** yapıldığının kanıtıdır: spesifikasyonun kendi kusurlarını yazılımın hanesine yazmamak, tabloyu kurtarmıyor — sadece doğru kılıyor. %33, %32 kadar ciddi bir sayıdır.

**Tek cümlelik değerlendirme:** ERP-vNext, GMG-QMS-SOP-0001'e (Document Control SOP) göre **olgun ve yer yer hedefin üzerinde** bir sistemdir; GMG-CSV-URS-0001'e (URS) göre ise **temel mimari varsayımı farklı** bir sistemdir — URS metadata-driven bir model isterken, mevcut implementasyon klasör-merkezli bir modeli veritabanına taşımıştır. Bu, kod kalitesi sorunu değil, **hedef mimari sapması**dır. **v2'nin eklediği:** bu sapma artık *kararlaştırılmış* bir yönü var (G-2: ERP authoritative) ve düzeltmenin maliyeti bugün **ölçülü olarak sıfıra yakın** — çünkü ERP'de henüz sıfır doküman dosyalanmış durumda.

---

## 2. P0 Stop-Ship Gaps

Controlled/production kullanım için kesin engel. **17 madde** *(v1: 18 — P0-15 §0b/S-DEF-1 uyarınca P2'ye indi)*.

> ### P0-1 diğer on altısının önündedir
>
> Bu bir öncelik tercihi değil, **delil mantığı**dır. `platform_admin` permission değerlendirmesini atladığı sürece **hiçbir OQ sonucu delil değeri taşımaz**: her negatif test "ama admin zaten yapabiliyor" ile cevaplanabilir, dolayısıyla hiçbiri **çürütülebilir** değildir. Çürütülemeyen bir test, test değildir.
>
> Sonuç: P0-1 (R-10) ve onun kanıt ayağı P0-2 (R-11) **Faz 0'a taşınmıştır**. İkisi de hiçbir governance kararına bağlı değildir ve diğer on beş P0'ın doğrulanabilmesinin ön koşuludur.

| # | Gap | URS | Neden stop-ship | Faz |
|---|---|---|---|---|
| **P0-1** | **Administrative bypass**: `platform_admin`/`partner_admin` tüm permission kontrolünü atlıyor | URS-070, 073, 082, 138 | **Diğer 16 P0'ı doğrulanamaz kılar** (yukarıdaki kutu). Tek bir aktör tipi tamamlanmış record'u ezebilir ve legal hold altındaki kaydı dispose edebilir. URS'in "hiçbir rol, administrative rol dahil" ifadesinin doğrudan ihlali. | **0** |
| **P0-2** | **Denied access audit edilmiyor** | URS-074, 091 | Negatif erişim kanıtı üretilemez; OQ-02 geçilemez. P0-1'in kanıt ayağı. | **0** |
| P0-3 | **Quality record'a Effective lifecycle state'i atanıyor** | URS-041 | Requirement'ın kelimesi kelimesine yasakladığı davranış, kodda açık. | 2 |
| P0-4 | **Completed record overwrite koruması yok** | URS-042, 073, 144 | Veri bütünlüğü çekirdeği; negatif testi bile mümkün değil. | 2 |
| P0-5 | **Effective geçişi e-imza istemiyor** (signature **gate**) | URS-037 | İmzasız Effective release. **v2: qualified-signature satın almasından ayrıldı** — gate bir kontroldür, ucuzdur, G-1'i beklemez. Sağlayıcı/sertifika tarafı P1'e ve G-1'e taşındı. | 2 |
| P0-6 | **Supersession otomatik ve atomik değil** | URS-034 | Kısmi hata durumunda **sıfır Effective** kalabilir. | 2 |
| P0-7 | **Tek-Effective kuralı race'e açık ve UnderRevision→Effective yolunda hiç çalışmıyor** | URS-033 | İki eşzamanlı Effective mümkün. | 2 |
| P0-8 | **Zone / Entity / Domain / Record Class vocabulary'leri yok**; vocabulary'ler hard-coded enum | URS-010, 011, 015, 016, 044, 072, 084 | Sınıflandırma modelinin yarısı temsil edilemiyor. | 1 |
| P0-9 | **Klasör-encoded classification** (storage location = classification) | URS-020, 022, 072 | URS §5'in "core requirement" dediği madde karşılanmıyor. **v2: G-2 kapandığı için artık kararlaştırılmış bir yönü var ve maliyeti bugün en düşük seviyede.** | 1 |
| P0-10 | **Metadata'dan path türetme yok** — **R-08, v2'de P1→P0** | URS-021, 024, **123** | Metadata-driven mimarinin ikinci yarısı. **v2 yükseltme gerekçesi:** G-2 ile ERP authoritative oldu; URS-123 klasör ağacının bu sistemden **üretilmesini** şart koşuyor; üretim doğrudan bu resolver'a bağlı. | 1 |
| P0-11 | **Sınıflandırılamayan obje default'a düşüyor** (`Other`/`Minor`); quarantine/held state yok | URS-066, 112, 113 | Annex B'nin 330 HELD kaydı sessizce "çözülür". | 1 |
| P0-12 | **Parent/child component (annex) modeli yok** | URS-050..054 | 44 PV annex'i modellenemez. | 2 · G-3 |
| P0-13 | **Per-transition authorization yok** (tek `lifecycle.manage` key) | URS-032 | Draft ilerletebilen kullanıcı Effective yapabiliyor. | 2 |
| P0-14 | **Doküman migration lane'i ve source-hash doğrulaması yok** | URS-110, 111 (doküman), 114 | Annex B yüklenemez, doğrulanamaz, geri alınamaz. **R-09'dan sonra koşulmalı** (bkz. §7 sıralama kilidi). | 3 |
| P0-15 | **IdP / security group entegrasyonu yok** | URS-132, 071 | A2'nin 115 grubu provizyonlanamaz. | 3 · G-8 |
| P0-16 | **IQ/OQ/UAT yürütülmemiş, traceability yok** | URS-141..144 | Validation kanıtı sıfır. **P0-1 kapanmadan yürütmenin anlamı yok.** | 5 |
| P0-17 | **Runtime şu anda blocked** (Language 403, Retention 500, Controlled Documents list 500) | URS-145 | Smoke kanıtı üretilemiyor. | **0** |

**v1'den düşen:** ~~P0-15 (File size 50 MB vs ≥500 MB)~~ → **P2**. URS-131b ölçüme dayanmadan yazılmıştı; ≥100 MB'a amend edildi, kalan iş tek sabit değişikliği. Bkz. §0b/S-DEF-1.

## 2b. BLOCKED / OWNER DECISION REQUIRED

Bunlar **kod eksikliği değil**, açık governance kararlarıdır. Remediation planına dahil edilmeli ama gap olarak puanlanmamalıdır. **11 karardan 1'i v2'de kapandı; 10'u açık.**

| # | Karar | Sahip | Engellediği | Durum |
|---|---|---|---|---|
| G-1 | GxP sınıflandırması; Annex 11 / Part 11 record-class bazlı uygulanabilirlik | Group Quality Director + CSV | E-imza **sağlayıcı harcaması**, audit kapsamı, validation eforu (URS §19 #1). **v2 notu:** signature *gate* (P0-5) bu karara bağlı **değildir** ve beklemeden yapılır. Bekleyen yalnızca qualified-signature satın alması — Part 11 predicate rule başına, record class başına uygulandığı için kapsam sistem geneli bir alımdan çok daha dar çıkabilir. | AÇIK · **harcama kilidi** |
| ~~G-2~~ | ~~Go-live'da hangi sistem authoritative~~ → **KARAR: ERP authoritative** | GQD / IT / Records Management | — | **KAPANDI (v2)** — Gerekçe: ERP'de lifecycle enforcement, identifier ledger, correlation-ID audit, çift onaylı legal hold, optimistic concurrency ve 860 test var; klasör ağacında bunların hiçbiri yok. **Türev etkiler:** URS-123 canlı requirement oldu; R-08 P0'a çıktı; W-1 (klasör-merkezli sınıflandırma refactoru) yetkilendirildi. |
| G-3 | Component modeli: parent ile mi bağımsız mı versiyonlanacak | QA Documentation | URS-050..054, 44 PV annex (§19 #3) |
| G-4 | 141 dosya için Superseded vs Retired | QA Documentation | Migration disposition (§19 #4) |
| G-5 | VOID modellenmiş state mi, Retired'a mı map edilecek | QA Documentation | URS-036 (§19 #5) |
| G-6 | Domain/function kod onayı (LOG-0006 hiç yazılmamış; 22 branch onaysız) | GQD / GRA / QPPV | Domain vocabulary (§19 #6) |
| G-7 | Record class'lar ve class bazlı retention takvimleri | Records Management / Legal | URS-044, 084 (§19 #7) |
| G-8 | IdP, security group modeli, 32 profil için membership approver | IT / ISM | URS-070..078, A2 (§19 #8) |
| G-9 | AUDIT branch disposition'ı | QA Documentation | Migration (§19 #9) |
| G-10 | Entity modeli (Setonda → GMG Ilaclari, Setonda PV kayıtlarının akıbeti) | Legal / Finance / HR | URS-016 serisi (§19 #10) |
| G-11 | **54 compound permission profile / pass-through container** — URS-071b çözüm öneriyor ama QA Documentation onayı yok | QA Documentation | URS-071a/071b; handover "STILL OPEN #1" |

## 3. P1 Major Gaps

| # | Gap | URS |
|---|---|---|
| P1-1 | Estate genelinde hash duplicate detection + explicit disposition | URS-064 |
| P1-2 | OS metadata artefact exclusion + excluded-report | URS-065 |
| ~~P1-3~~ | **P0'a taşındı (v2 · R-08)** — Export path generator: 255 karakter uyarısı, ASCII segment kısıtı. Gerekçe: G-2 kapandı (ERP authoritative) ⇒ URS-123 klasör ağacının bu sistemden üretilmesini şart koşuyor ⇒ üretim bu resolver'a bağlı. | URS-024, 025, **123** |
| P1-4 | Metadata-attribute arama + çoklu navigasyon görünümleri | URS-100, 101 |
| P1-5 | Document code ile tek adımda current-Effective retrieval | URS-103 |
| P1-6 | JML (joiner/mover/leaver) + periodic access review kanıtı | URS-075, 076 |
| P1-7 | External sharing: zorunlu expiry + approval rotası + revoke | URS-077 |
| P1-8 | Legal hold: `Repository` ve `CustomQuery` scope'larının uygulanması + tek sorgulu hold raporu | URS-080, 083 |
| P1-9 | Hold gate'inin archive/soft-delete yollarına genişletilmesi | URS-081 |
| P1-10 | Audit export (open format, vendor bağımsız) + audit retention ↔ subject retention bağı | URS-093, 094 |
| P1-11 | Audit redaction'ının GxP kapsamında kilitlenmesi | URS-092 |
| P1-12 | Reversible migration (set-level rollback) | URS-114 |
| P1-13 | Doküman düzeyinde reconciliation | URS-111 |
| P1-14 | Register'sız controlled document invariant kontrolü + alarm | URS-060 |
| P1-15 | Vocabulary import lane'i (LOG-0007 sheet 08 + Annex C) | URS-014 |
| P1-16 | Approval-family matrix action'larının inert placeholder'dan gerçek enforcement'a çevrilmesi | URS-071 |
| P1-17 | Monitoring/alerting: failed ingestion / backup / transition / permission change | URS-131d |
| P1-18 | Open-format tenant export (metadata + audit) | URS-136 |
| P1-19 | OpenAPI / dokümante dış API contract | URS-137 |
| P1-20 | API retrieval audit (kullanıcı + sorgu + dönen objeler) | URS-139 |
| P1-21 | Batch ingestion lane + throughput/latency/kapasite kanıtı | URS-131a, 131c, 133 |
| P1-22 | DR planı, RPO/RTO ve yıllık restore kanıtı | URS-131 |
| P1-23 | `CopyOnAdopt` paylaşımının controlled documents için kapatılması | URS-101 |
| P1-24 | SoR bazlı yazma kilidi + record-class seviyesinde SoR | URS-120, 121 |
| P1-25 | Free-text alanların (`OwnerFunction`, `ProcessOwnerRole`, `RetentionClass`, `GoverningLanguage`) BRD'ye bağlanması | URS-013 |
| P1-26 | Vocabulary code uniqueness'ında case/separator normalizasyonu | URS-017 |
| P1-27 | Effective transition için evidence reference'ın zorunlu kılınması | URS-032 |
| P1-28 | GDocP correction motorunun record içeriğine genişletilmesi | URS-043 |
| P1-29 | Tüm export çıktılarına copy-marking | URS-122 |

## 4. P2 Deferred / Enhancement

| # | Konu | URS |
|---|---|---|
| P2-1 | Semantik versiyon etiketleme (0.x / 1.x / 2.0) | URS-038 ötesi, SOP §6.3 |
| P2-2 | Manuel `identifiers.reserve` yolunun migration-only flag arkasına alınması | URS-062 |
| P2-3 | Lifecycle state'in de configurable vocabulary olması | URS-010 |
| P2-4 | Format allow-list politikası | URS-131b |
| P2-5 | Tam filesystem export (approved topology reproduksiyonu) | URS-026 (S) |
| P2-6 | MOD-0023 shared workflow ile ilişkinin resmî kararı ve kayda geçirilmesi | — |
| P2-7 | AI/RAG retrieval yolu (eklenirse P0 kurallarıyla) | URS-138, 139 |
| **P2-8** | **`MaxFileSizeBytes` 50 MB → 100 MB** (tek sabit; v1'de P0-15 idi) | URS-131b (amended) |
| **P2-9** | **Register `Failed` operasyonları için reconcile job + invariant alarmı** (v1'de URS-060 PARTIAL gerekçesiydi; artık gap değil, gözlemlenebilirlik iyileştirmesi) | URS-060 (amended) |
| **P2-10** | **Estate dosya-boyutu profili izleme**: 100 MB üstü dosya girerse streaming yeniden değerlendirilir (W-8 dönüş koşulu) | URS-131b, 133 |

## 5. Wrong Architecture / Rework Needed

Bunlar "eksik" değil, **yanlış yönde tamamlanmış** alanlardır — düzeltmek ekleme değil refactor gerektirir.

> ### W-1'in maliyet penceresi şu anda açık — ve kapanacak
>
> **ERP'de bugün sıfır doküman dosyalanmış durumda.** Bu proje refactor ekonomisini iki kez ölçmüş:
> - **Setonda → GMG İlaçları** entity değişimi **271 path'i bedavaya** taşıdı,
> - **v0.32 type eklemeleri 75 dosyayı** önemsiz maliyetle serbest bıraktı,
>
> her ikisi de **henüz hiçbir şey dosyalanmamış olduğu için**. Aynı refactor, migration koştuktan sonra 1.236 promoted dosyanın yeniden sınıflandırılması anlamına gelir.
>
> **Bu yüzden §7'deki tek geri alınamaz sıralama kuralı şudur: R-26 (migration lane) hiçbir koşulda R-09'dan (sınıflandırma değişiminin objeyi taşımaması / link stabilitesi) önce koşulmaz.** Diğer her sıralama hatası pahalıdır; bu tek hata kalıcıdır.

| # | Alan | Sorun | Rework |
|---|---|---|---|
| W-1 | **Klasör-merkezli sınıflandırma** | `CollectionDefinition` klasör düğümleri sınıflandırmayı taşıyor (`AccessProfile`, `DepartmentDomain`, `FolderType`, `RetentionClass`, `SourceOfTruth`); `ControlledDocument` zorunlu `CollectionInstanceId` + `CollectionPath` ile bir klasöre çivileniyor. URS §5'in "core requirement" dediği maddenin tersi. | Sınıflandırmayı doküman/register metadata'sına taşı; klasör ağacını türetilmiş, opsiyonel bir görünüm haline getir. **v2: G-2 kapandığı için yetkilendirildi ve ŞİMDİ yapılmalı** — yukarıdaki maliyet penceresi. Migration gerektirir; bugün taşınacak doküman yok. |
| W-2 | **Status-folder'a dayalı erişim kuralları** | `AccessProfileTemplateCatalog.ApplyStatusFolderRules` read-only kararını **klasör adından** (`"Effective"`, `"Superseded_Retired"`) üretiyor. Lifecycle bir alan olmasına rağmen izin bir klasör adına bakıyor. | Read-only kararını `LifecycleStatus` alanından türet; `ReadOnlyStatusFolders`/`RestrictedStatusFolders` string dizilerini kaldır. |
| W-3 | **Access profile taksonomisi uyumsuz** | 8 profil (`GQMS-Controlled`, `Site-Controlled`…) vs A1'in 32 PS-* profili. İsim ve semantik örtüşmesi sıfır. | 32 PS-* profilini vocabulary olarak tanımla; mevcut 8'i bunlara map et veya emekli et. |
| W-4 | **Quality record = controlled document + flag** | Ayrı object class yerine `IsRecord` bayrağı; aynı aggregate, aynı register, aynı version servisi, üstelik `LifecycleStatus=Effective` yazılıyor. | Ayrı `QualityRecord` aggregate + `RecordState` + kendi permission ailesi. |
| W-5 | **Variant modeli component modeli sanılabilir** | `DocumentVariant`/`TemplateVariant` çeviri/lokalizasyon içindir; annex/component değildir. İkisi farklı requirement ailesi. | Ayrı `DocumentComponent` aggregate; variant'ı olduğu gibi bırak. |
| W-6 | **Tek-Effective kuralı `PermanentUid`'e bağlı** | URS "document code" diyor; kod UID kullanıyor. İkisi 1:1 değilse kural yanlış anahtarda çalışır. | Anahtar seçimini açıkça karara bağla; DB unique index ile destekle. |
| W-7 | **Admin bypass'ı authorization filtresinde** | Regulated kaynaklar için muafiyet yok; bypass merkezî filtrede. | Regulated resource sınıflarını bypass'tan muaf tut; break-glass'ı ayrı, audit'li, süreli yap. |
| ~~W-8~~ | ~~**Base64 content transport**~~ → **KRİTİK YOLDAN ÇIKTI (v2)** | v1 gerekçesi 500 MB gereksinimiydi. URS-131b **≥100 MB**'a amend edilince gerekçe düştü: 100 MB'ın base64'ü ~133 MB bellek — tolere edilebilir; 500 MB'ınki ~667 MB'dı, edilemezdi. | **Rework gerekmiyor.** Tek sabit değişikliği (`MaxFileSizeBytes` → 104_857_600) yeterli. **Yeniden dönme koşulu:** estate profili değişir de (URS-133'ün 5 yıllık büyüme varsayımı altında) 100 MB üstü dosyalar girerse streaming yeniden gündeme gelir — bu bir izleme maddesidir, bugün bir borç değil. |

## 6. Already Strong / Reusable

Bunlar **korunmalı** ve remediation sırasında bozulmamalıdır. Birkaçı GMG hedefinden daha iyidir.

| # | Alan | Neden güçlü | Kanıt |
|---|---|---|---|
| S-1 | **Lifecycle state machine** | Saf, yan etkisiz, tek tanım (`ControlledDocumentLifecyclePolicy`), terminal state'ler açık, tanımsız geçiş 409. GMG'nin 6 state'ini kapsayıp `UnderRevision`/`Suspended`/`ObsoleteCopy` ile **genişletiyor**. | `ControlledDocumentLifecyclePolicy.cs`; 24 lifecycle testi |
| S-2 | **Identifier allocation ledger** | Monotonik counter + append-only ledger + iptal edilenler dahil **asla yeniden kullanma**. URS-062'yi tam karşılıyor. | `DocumentIdentifierAllocationService`; `ExistsValueIncludingDeletedAsync` |
| S-3 | **Approval segregation** | Author ≠ sole approver, `SegregationResult`, criticality/impact overlay route resolver. GMG'nin URS'de bile ayrıntılandırmadığı bir derinlik. | `DocumentSegregationRuleEvaluator`; `DocumentApprovalRouteResolver` |
| S-4 | **Non-leakage disipliniyle 404** | Yetkisiz ve olmayan kayıt ayırt edilemiyor; `NOT_FOUND_NON_LEAKAGE` reason code'u tüm feature'larda tutarlı. URS-102'yi karşılıyor. | `LifecycleReasonCodes`, `MasterRegisterReasonCodes`, `IdentifierAllocationReasonCodes` |
| S-5 | **Legal hold release'in çift onayı** | Legal approval **ve** GQD concurrence; tek onay asla yeterli değil. GMG'den **daha katı**. | `DocumentLegalHold.cs` |
| S-6 | **"No destruction engine" ilkesi** | Retention politikası tarih hesaplar, asla silmez. URS-086 ile birebir. Sistemde hiçbir yerde hard delete yok. | `DocumentRetentionPolicy.cs` sınıf yorumu; her yerde `DeletedAt` soft-delete |
| S-7 | **E-imzanın kendi sınırını kayda yazması** | `RepositoryBoundaryStatement` ve `ValidationResult=NotValidated` kanıtla birlikte seyahat ediyor — abartılı uyum iddiasını yapısal olarak imkânsız kılıyor. Regulated ortam için örnek dürüstlük. | `DocumentSignatureRecord.cs` |
| S-8 | **Reconciliation motoru** | Saf, deterministik, hiçbir şeyi mutate etmeyen read-back karşılaştırması + severity + remediation önerisi + UI. URS-111 ve URS-115'i klasör kapsamında karşılıyor; doküman kapsamına genişletilebilir. | `CollectionTreeReconciliationEngine.cs`; `Deviations.cshtml` |
| S-9 | **Idempotent registration saga** | `IdempotencyKey` + immutable scope snapshot + retry + fail-state. URS'de istenmemiş, ama URS-060'ın transaction ihtiyacına en yakın pratik cevap. | `ControlledDocumentRegistrationService` |
| S-10 | **Optimistic concurrency + correlation ID** | `ExpectedVersion` → 409; `CorrelationId` uçtan uca audit'e kadar. | `DocumentLifecycleService`; `ICorrelationContext` |
| S-11 | **Controlled copy / obsolete copy yönetimi** | `DocumentControlledCopy` + withdrawal plan + obsolete finding + `ObsoleteCopy` state. URS-122'yi **fazlasıyla** karşılıyor. | `DocumentManagementControlledCopy/` |
| S-12 | **Test yoğunluğu** | 41 dosyada 860 test metodu; lifecycle, allocation, hold, approval, gate, correction hepsi kapsanmış. Altyapı sağlam — eksik olan URS-ID etiketlemesi. | `tests/.../DocumentManagement/` |
| S-13 | **Fail-safe default'lar** | Bilinmeyen access profile **hiç grant almaz**; approval gate adapter'ı yoksa transition bloklanır; unlinked doküman ordinary user'a kapalı. | `AccessProfileTemplateCatalog.Build`; `DocumentLifecycleService`; `DocumentAccessEvaluator` |
| S-14 | **UI derinliği** | Master Register Details'te 11 sekme (identifiers, lifecycle, approval, gates, training, repository, copies, retention, signatures, quality) — backend feature'larının çoğunun gerçek ekran karşılığı var. | `Views/DocumentManagement/MasterRegister/Details.cshtml` (1793 satır) |

## 7. Exact Remediation Backlog

Sıra, bağımlılık zincirine göredir. **Faz 0 kapanmadan Faz 1'e geçilmemelidir** — çünkü Faz 1'in çoğu vocabulary'ye bağlıdır.

> ### İki sıralama kuralı — biri geri alınabilir, biri değil
>
> **Kural 1 (delil):** R-10 ve R-11 Faz 0'dadır. Admin bypass açıkken koşulan hiçbir OQ/negatif test delil değeri taşımaz; erken koşulursa **yeniden koşulması gerekir**. Pahalı ama geri alınabilir.
>
> **Kural 2 (kalıcı): R-26, R-09'dan önce koşulmaz.** Bugün ERP'de sıfır doküman var; W-1 refactoru bu yüzden bedavaya yakın. Migration'dan sonra aynı refactor 1.236 promoted dosyanın yeniden sınıflandırılmasıdır. **Bu, planın ucuz şekilde geri alınamayan tek kararıdır.**

### Faz 0 — Governance + Runtime + Delil Geçerliliği (paralel)

*v2'de genişledi: R-10 ve R-11 buraya taşındı.*

| # | MOD/FU | Task | Bağımlılık | Acceptance Criteria |
|---|---|---|---|---|
| R-01 | — | URS §19'daki **kalan 9 kararın** + 54 compound profile kararının (G-11) kapatılması *(G-2 v2'de kapandı)* | Yok | Her karar için yazılı, tarihli, sahibi imzalı kayıt; Annex A1 status `PENDING` → `APPROVED` |
| R-02 | — | ~~Annex D'yi 103 requirement'a tamamla~~ | — | **TAMAMLANDI (v2).** 103 satır, 45 bağlı, 58 script bekliyor. |
| R-03 | MOD-0029-FU38 | Runtime blocker'ları kapat: Language 403, Retention 500, Controlled Documents list 500 | Yok | Authenticated smoke: 3 endpoint 200; register'da `Runtime blocked` notu kalkar |
| **R-10** | MOD-0029-FU45 | **Admin bypass kaldırma** *(v2'de Faz 2 → Faz 0)*: regulated resource sınıfları için `isPlatformActor` muafiyeti yok; break-glass ayrı, süreli, audit'li | Yok | Platform admin ile completed record overwrite denemesi **403 + audit**; URS-070/073/082/138 negatif testleri geçer. **Bu kapanmadan hiçbir OQ sonucu delil sayılmaz.** |
| **R-11** | MOD-0029-FU46 | **Denied access audit** *(v2'de Faz 2 → Faz 0)*: enforcement filtresine audit sink (actor, permission key, resource, correlationId, outcome=Denied) | Yok | Her 403 için bir `AuditEvent(Outcome=Denied)`; URS-074/091 testleri geçer. R-10'un kanıt ayağı. |

### Faz 1 — Metadata Model (P0'ların çoğunun ön koşulu)

| # | MOD/FU | Task | Bağımlılık | Acceptance Criteria |
|---|---|---|---|---|
| R-04 | MOD-0048-FU / MOD-0029-FU39 | Controlled vocabulary setleri: Zone (13), Entity (7), Domain (19), Function (15), Document Type (domain-scoped), Lifecycle State (6), Record Class (7), Permission Profile (32) — BRD üzerinden, code/display/owner/status/effective date ile | R-01 (G-6, G-7, G-10) | Her set BRD'de published; her entry 5 niteliği taşıyor; enum hard-code kaldırıldı; URS-010/011/012 testleri geçer |
| R-05 | MOD-0029-FU40 | Entity code guard: asla yeniden atanmaz, retired kod kalıcı rezerve, status + effective date, historical resolve | R-04 | Retired koda allocation 409; historical referans ambiguity'siz çözülür; URS-016/016a/016b testleri geçer |
| R-06 | MOD-0029-FU41 | TYPE ↔ Domain geçerlilik kuralı | R-04 | Domain'de onaylı olmayan TYPE 400; URS-015 testi geçer |
| R-07 | MOD-0029-FU42 | Register entry'ye zorunlu classification alanları (Zone, Entity, Domain, Type, RecordClass) + default'ların kaldırılması + `Unclassified`/`Held` state | R-04 | Sınıflandırmasız kayıt `Held`; operasyonel kullanım bloklu; default atama yok; URS-066/112/113 testleri geçer |
| **R-08** | MOD-0029-FU43 | Metadata → display path + export path resolver (configurable root, 255 uyarısı, ASCII segment) — **v2: P1 → P0**, çünkü G-2 sonrası URS-123 klasör ağacının bu sistemden üretilmesini şart koşuyor | R-07 | Path metadata'dan türetiliyor; 302 karakter senaryosu uyarı üretiyor; **onaylı topoloji ağacı ERP'den üretilebiliyor** (URS-123 "generated" yolu); URS-021/024/025 testleri geçer |
| **R-09** | MOD-0029-FU44 | Sınıflandırma değişiminin objeyi taşımaması; link stabilitesi | R-07, R-08 | Attribute değişimi sonrası `Guid` ve tüm referanslar geçerli; obje kopyalanmıyor; URS-022/023 testleri geçer. **⛔ R-26 bu madde kapanmadan başlatılamaz** (§5 maliyet penceresi). |

### Faz 2 — Regulated Integrity (stop-ship'lerin çekirdeği)

*v2'de daraldı: R-10 ve R-11 Faz 0'a taşındı.*

| # | MOD/FU | Task | Bağımlılık | Acceptance Criteria |
|---|---|---|---|---|
| R-12 | MOD-0029-FU47 | **QualityRecord ayrı aggregate**: `RecordState` (Open/Completed/Corrected), `LifecycleStatus` yazımı kaldırılır, zorunlu Record Class | R-04, R-07 | Record'da `ControlledDocumentLifecycleStatus` asla set edilmiyor; Record Class zorunlu; URS-040/041/044/045 testleri geçer |
| R-13 | MOD-0029-FU48 | **Completed record overwrite/delete engeli** — admin dahil | R-10 *(Faz 0)*, R-12 | Completed record'a version upload 409; her rol için negatif test; URS-042/073/144 testleri geçer |
| R-14 | MOD-0029-FU49 | **Controlled correction / append-only** record içeriği için | R-12 | Orijinal içerik korunuyor; reason/actor/timestamp kaydediliyor; URS-043 testi geçer |
| R-15 | MOD-0029-FU50 | **Per-transition authorization**: transition başına permission key + fail-closed map + AuthService seed | Yok | `lifecycle.manage` yerine 5+ ayrı key; yetkisiz transition 403 + audit; URS-032 testi geçer |
| R-16 | MOD-0029-FU51 | **Tek-Effective + atomik supersession**: partial unique index (`PermanentUid`/`DocumentCode` + Effective), guard'ın tüm hedef-Effective yollarına uygulanması, supersession'ın otomatik çözümü ve transaction/compensation ile atomikleştirilmesi | R-03 | Eşzamanlı iki MarkEffective'den biri 409; `UnderRevision→Effective` guard'lı; kısmi hata sonrası Effective sayısı asla 0 veya 2; URS-033/034 testleri geçer |
| R-17 | MOD-0029-FU52 | **E-imza ↔ Effective bağlantısı (signature gate)**: MarkEffective, `Valid` + doğru meaning + fingerprint'i eşleşen imza ister. **v2: G-1'den bağımsız — koşulsuz yapılır.** Mevcut `DocumentSignatureRecord` üzerinden; yeni sağlayıcı gerekmez. | Yok | İmzasız Effective 409 `SIGNATURE_REQUIRED`; URS-037 testi geçer |
| **R-17b** | — | **Qualified-signature kapsam kararı** (yeni, v2): Part 11 predicate-rule uygulanabilirliğini **record class başına** belirle; ancak ondan sonra sağlayıcı/sertifika harcaması yap | **G-1** | Her record class için Part 11 uygulanır/uygulanmaz kararı yazılı; kapsam dışı sınıflar için `NotValidated` beyanı **meşru kalır ve korunur**; kapsam içi sınıflar için sağlayıcı gereksinimi ayrı fiyatlanır. **Karar öncesi harcama yok.** |
| R-18 | MOD-0029-FU53 | **VOID kararı implementasyonu** (state veya zorunlu Retired mapping) | R-01 (G-5) | VOID içerik tek bir modellenmiş yola düşüyor; URS-036 testi geçer |
| R-19 | MOD-0029-FU54 | **Document component (annex) aggregate**: parent-child, per-type versioning rule, parent transition cascade, bağımsız Effective engeli | R-01 (G-3), R-07 | 44 PV annex'i modellenebiliyor; with-parent kuralında component bağımsız Effective olamıyor; URS-050..054 testleri geçer |

### Faz 3 — Access Control & Migration

| # | MOD/FU | Task | Bağımlılık | Acceptance Criteria |
|---|---|---|---|---|
| R-20 | MOD-0029-FU55 | **32 PS-* permission profile** + mevcut 8'in map/emekliliği; 6 action bağımsız (read/create/modify/approve/dispose/external-share) | R-01 (G-8), R-04 | 32 profil vocabulary'de; her biri 6 action'ı bağımsız ifade ediyor; URS-071 testi geçer |
| R-21 | MOD-0029-FU56 | **URS-071a/071b**: compound profile reddi + pass-through container node tipi (no direct filing, no independent grant) | R-01 (G-11), R-20 | 54 lokasyon pass-through olarak işaretli; compound değer register ve sistemde reddediliyor; URS-071a/071b testleri geçer |
| R-22 | MOD-0029-FU57 | **Effective permission = entity+zone+domain+record class** (klasörden değil); status-folder kurallarının kaldırılması | R-04, R-07, R-20 | `ReadOnlyStatusFolders` string dizisi kaldırıldı; izin metadata kombinasyonundan; URS-072 testi geçer |
| R-23 | MOD-0029-FU58 | **IdP + security group entegrasyonu**; named-user grant'ın kapatılması | R-01 (G-8) | `DocumentAccessPrincipalType.Group` gerçek kaynağa bağlı; `User` principal controlled scope'ta reddediliyor; A2'nin 115 grubu provizyonlanabiliyor; URS-070/132 testleri geçer |
| R-24 | MOD-0029-FU59 | **JML + periodic access review** kanıt üretimiyle | R-23 | Leaver'da erişim kalkıyor, mover'da recertification isteniyor; review kampanyası attestation kaydı üretiyor; URS-075/076 testleri geçer |
| R-25 | MOD-0029-FU60 | **External sharing governance**: zorunlu expiry + approval rotası + revoke + external recipient | R-20 | Süresiz paylaşım imkânsız; her paylaşım kayıtlı; URS-077 testi geçer |
| **R-26** | MOD-0029-FU61 | **Migration lane**: Annex B manifest ingest, `ExpectedSourceHash` post-ingest verify, promoted/held/staged disposition, reconciliation raporu, idempotency, duplicate handling, reversibility | **⛔ R-09 (KİLİT — geri alınamaz)**, R-07 | 1.631 kayıt yüklenip doğrulanıyor; 330 HELD gerekçesiyle held kalıyor; hash mismatch kalem kalem raporlanıyor; set-level rollback çalışıyor; kaynak estate değişmiyor; URS-110..115 testleri geçer. **Ön koşul kontrolü: başlatmadan önce R-09'un kapalı olduğu yazılı olarak doğrulanacak.** |
| R-27 | MOD-0029-FU62 | **Duplicate detection + OS artefact exclusion** | R-26 | 25 duplicate grubu tespit ediliyor ve explicit disposition istiyor; 140 OS dosyası dışlanıp raporlanıyor; sayımlar reconcile ediyor; URS-064/065 testleri geçer |

### Faz 4 — NFR, API, Search

| # | MOD/FU | Task | Bağımlılık | Acceptance Criteria |
|---|---|---|---|---|
| R-28 | MOD-0029-FU63 | ~~Streaming upload + ≥500 MB~~ → **v2: tek sabit değişikliği.** `MaxFileSizeBytes` 52_428_800 → **104_857_600**. Streaming/multipart refactor **iptal** (bkz. §0b/S-DEF-1, W-8). | Yok | 100 MB dosya kabul ediliyor, içerik dönüştürülmüyor, checksum korunuyor; URS-131b (amended) testi geçer. **P2 — Faz 4'te değil, herhangi bir sprint'e sığar.** |
| R-29 | MOD-0029-FU64 | **Metadata-attribute search + 5 navigasyon görünümü** (entity/domain/type/lifecycle/record class), duplikasyonsuz; document code ile tek adımda current-Effective | R-07 | Aynı obje 5 görünümde tek kayıt olarak görünüyor; permission-filtered; URS-100/101/103 testleri geçer |
| R-30 | MOD-0029-FU65 | **Legal hold scope tamamlama** (Repository/CustomQuery) + entity/domain/record-class scope + tek sorgulu hold raporu + archive yollarında hold gate | R-04 | Hold kapsamındaki her obje tek sorguda listeleniyor; inert scope kalmıyor; URS-080/081/083 testleri geçer |
| R-31 | MOD-0029-FU66 | **Retention: record class + entity boyutu** | R-04, R-12 | Retention record class ve entity'ye göre çözülüyor, longest-applicable korunuyor; URS-084 testi geçer |
| R-32 | MOD-0029-FU67 | **Audit export (open format) + audit retention ↔ subject retention + redaction kilidi** | Yok | Vendor müdahalesi olmadan export; audit süresi ≥ subject süresi; GxP kapsamında redaction kapalı; URS-092/093/094 testleri geçer |
| R-33 | MOD-0029-FU68 | **API contract yayını + retrieval audit + AI kuralları** | R-10 | OpenAPI yayında; her programatik retrieval kullanıcı+sorgu+dönen objelerle audit'te; blanket-read service account yok; URS-137/138/139 testleri geçer |
| R-34 | MOD-0029-FU69 | **Monitoring/alerting** (4 olay) + DR/RPO/RTO + kapasite & performans kanıtı + tenant export | Yok | 4 alert kuralı aktif; restore testi kanıtlı; 50k/500GB/100 concurrent ölçülmüş; p95 < 3 sn; URS-131/131a/131c/131d/133/136 testleri geçer |
| R-35 | MOD-0029-FU70 | **SoR enforcement**: record-class seviyesinde authoritative sistem + duplicate master yazma kilidi + export copy-marking | R-04 | Authoritative sistemi olan record class'ta yerel master yazımı reddediliyor; her export "COPY" işaretli; URS-120/121/122 testleri geçer |

### Faz 5 — Validation

| # | MOD/FU | Task | Bağımlılık | Acceptance Criteria |
|---|---|---|---|---|
| R-36 | — | **URS-ID → test traceability**: 860 mevcut testin ve yeni testlerin URS-ID ile etiketlenmesi + otomatik matris üretimi | R-02, Faz 1–4 | 103 requirement'ın her biri ≥1 teste bağlı; matris CI'da üretiliyor; URS-143 karşılanır |
| R-37 | — | **IQ yürütme** (IQ-01..07; absolute path uzunluğu dahil) | R-08 | 7 IQ testi executed, named executor + independent review; URS-142 |
| R-38 | — | **OQ yürütme** (OQ-01..04) | Faz 2, Faz 3 | 4 OQ testi executed | 
| R-39 | — | **Negative permission testing**: 32 profilin **her biri** için | R-20, R-21, R-23 | 32 profil için negatif senaryolar executed; overwrite-prevention testi executed; URS-144 |
| R-40 | — | **UAT yürütme** (UAT-01, UAT-02) | R-29, R-26 | 2 UAT testi executed; URS-141/142/145 kapatılır |

---

## 7b. URS'e Eklenmesi Önerilen Requirement'lar — "Spec'i Yukarı Çek, Yazılımı Aşağı Değil"

*(v2'de eklendi — URS sahibinin açık talimatı.)*

**Gerekçe.** §6'daki S-1…S-14'ün bir bölümü URS'te **hiç istenmemiştir**. Bu bir avantaj değil, **bir risk**tir: URS'te karşılığı olmayan bir capability, refactor sırasında kimsenin fark etmeyeceği şekilde silinebilir — ve silindiğinde hiçbir test kırılmaz, hiçbir requirement düşmez. Gap analizinin ürünü bu nedenle yalnızca "yazılıma ne eklenecek" değil, **"spesifikasyona ne eklenecek"** olmalıdır.

Aşağıdaki maddeler yazılımda **zaten mevcut ve çalışıyor**; önerilen aksiyon kod değil, **URS revizyonu**dur.

| Öneri | Önerilen URS ID | Requirement metni (taslak) | Koruduğu mevcut capability | Kanıt | Pri |
|---|---|---|---|---|---|
| A-1 | **URS-032a** | Bir onay adımının yazarı, aynı dokümanın tek onaylayıcısı olamaz; sistem segregation ihlalini transition anında reddeder. | S-3 — `DocumentSegregationRuleEvaluator`, `SegregationResult`, `ApprovalReasonCodes.SegregationFailed` | `DocumentApprovalService.cs` | **M** |
| A-2 | **URS-085a** | Legal hold'un serbest bırakılması **iki bağımsız onay** gerektirir (Legal approval + GQD concurrence); tek onay hiçbir koşulda yeterli değildir. | S-5 — `ReleaseLegalApprovalReference` + `ReleaseGqdConcurrenceReference` | `DocumentLegalHold.cs` | **M** |
| A-3 | **URS-102a** | Yetkisiz kayıt ile var olmayan kayıt, çağırana **ayırt edilemez** yanıt döner (tek tip 404 + tek tip reason code). | S-4 — `NOT_FOUND_NON_LEAKAGE` deseni, tüm feature'larda tutarlı | `LifecycleReasonCodes`, `MasterRegisterReasonCodes` | **M** |
| A-4 | **URS-060a** | Register kaydı üreten işlemler **idempotent** olacak; tekrarlanan çağrı ikinci bir kayıt üretmeyecek. | S-9 — `IdempotencyKey` + immutable scope snapshot | `ControlledDocumentRegistrationService` | **M** |
| A-5 | **URS-090a** | Her regüle işlem, talep–karar–kayıt–audit zinciri boyunca **tek bir correlation ID** taşıyacak. | S-10 — `ICorrelationContext` → command → entity → `AuditEvent` | `DocumentManagementLifecycleController.CorrelationId` | **M** |
| A-6 | **URS-031a** | Eşzamanlı değişiklik denemesi, kaybedilen güncelleme üretmeden reddedilecek (optimistic concurrency). | S-10 — `ExpectedVersion` → 409 `STALE_VERSION` | test `Stale_expected_version_is_rejected` | **M** |
| A-7 | **URS-030a** | Lifecycle geçiş matrisi **tek bir yerde**, yan etkisiz ve saf olarak tanımlanacak; her katman aynı tanımı tüketecek. | S-1 — `ControlledDocumentLifecyclePolicy` | `ControlledDocumentLifecyclePolicy.cs` | **S** |
| A-8 | **URS-095a** | Sistemin ürettiği imza, kendi doğrulama sınırını (`ValidationResult`, repository boundary) **kaydın üstünde** taşıyacak; sınır yalnızca dokümantasyonda kalmayacak. | S-7 — `RepositoryBoundaryStatement`, `ValidationResult=NotValidated` | `DocumentSignatureRecord.cs` | **M** |
| A-9 | **URS-086a** | Retention motoru tarih hesaplar; **hiçbir koşulda** silme, purge veya arşivleme yürütmez. Yıkım yalnızca onaylı disposition yoluyla olur. | S-6 — "no destruction engine" ilkesi; sistemde hard delete yok | `DocumentRetentionPolicy.cs` | **M** |
| A-10 | **URS-122a** | Kontrollü kopyalar için geri çekme planı ve obsolete-copy tespiti bulunacak; kullanımdaki eski kopya raporlanabilir olacak. | S-11 — `DocumentControlledCopy`, `DocumentCopyWithdrawalPlan`, `ObsoleteCopy` | `DocumentManagementControlledCopy/` | **S** |
| A-11 | **URS-071c** | Tanınmayan bir access profile **hiçbir** yetki üretmez (fail-safe default). | S-13 — `AccessProfileTemplateCatalog.Build` → `known=false` ⇒ boş spec | `AccessProfileTemplateCatalog.cs` | **M** |
| A-12 | **URS-111a** | Reconciliation motoru salt-okunur olacak; karşılaştırma hiçbir nesneyi mutate, create, move, rename veya delete etmeyecek. | S-8 — `CollectionTreeReconciliationEngine` sınıf sözleşmesi | `CollectionTreeReconciliationEngine.cs` | **M** |

**Etki.** Bu 12 madde kabul edilirse URS **103 → 115** requirement olur; hiçbiri yeni geliştirme gerektirmez, tamamı **as-built PASS**'tir. Kazanç, completion yüzdesi değil — **regresyon koruması**: bugün mevcut olan 12 kontrol, yarın sessizce kaybolamaz hale gelir.

> **Not.** Bu, gap analizinin genellikle üretmediği bir çıktıdır. Bir spesifikasyon, ölçtüğü sistemden **öğrenebilir**; S-1…S-14, URS'ün yazıldığı sırada bilinmeyen ama doğru olduğu kanıtlanmış kararlardır.

---

## 8. Final Verdict

# PARTIAL — MAJOR GAPS

*(v2'de değişmedi. İki spec düzeltmesi ve bir governance kararı tabloyu **doğru** kıldı, **iyi** kılmadı.)*

**Neden READY değil.** Genel kanıt-temelli tamamlanma **%33**. URS'in mandatory (M) requirement'larının çoğunluğu ya karşılanmıyor ya da kısmen karşılanıyor. **On yedi** P0 stop-ship gap'in her biri tek başına controlled kullanımı engeller; bunlardan üçü (administrative bypass, quality record'a Effective state atanması, completed record overwrite korumasının yokluğu) requirement metninin **kelimesi kelimesine yasakladığı** davranışların kodda mevcut olduğu durumlardır. Validation tarafında kanıt sıfırdır: Annex D artık 103 satır taşıyor ama **hiçbiri executed değil**, IQ/OQ/UAT hiç koşulmamış, 32 permission profile için negatif test yok ve runtime şu anda üç hata ile blocked.

**Ve v2'nin eklediği daha sert gerçek:** P0-1 (admin bypass) kapanmadan koşulacak hiçbir qualification **delil üretmez**. Bugün IQ/OQ/UAT'ı baştan sona yürütmek mümkün olsa bile sonuç hukuken boş olurdu — çünkü her negatif test "ama admin zaten yapabiliyor" ile çürütülebilir. Sistem yalnızca uyumsuz değil; şu anda **uyumluluğu kanıtlanamaz** durumda. Bu ikisi farklı sorunlardır ve ikincisi tek bir maddenin arkasındadır.

**Neden NOT READY de değil.** Sistem boş bir iskelet değildir. Lifecycle state machine, identifier allocation ledger, approval segregation, legal hold çift onayı, "no destruction engine" ilkesi, non-leakage 404 disiplini, reconciliation motoru, controlled copy yönetimi, idempotency, correlation ID ve 860 testlik kapsam gerçek, çalışan ve yer yer **GMG hedefinin üzerinde** capability'lerdir. Bunlar atılacak değil, üzerine inşa edilecek varlıklardır — ve §7b uyarınca artık **spesifikasyona yazılarak korunacak** varlıklardır.

**Neden BLOCKED BY GOVERNANCE tek başına doğru değil.** On bir governance kararından **biri kapandı (G-2), onu açık.** Açık kalanların bir kısmı gerçekten P0 ön koşuludur (G-3 component modeli, G-7 record class, G-8 IdP/grup modeli, G-11 54 compound profile). Ancak governance kapanmadan ilerletilebilecek P0'lar hâlâ çoktur ve **en kritik ikisi bunların içindedir**: R-10 (admin bypass) ve R-11 (denied access audit) hiçbir karara bağlı değildir ve artık Faz 0'dadır. Ayrıca R-13, R-15, R-16, R-17 (imza gate'i) ve R-28 de bağımsızdır. Proje "governance bekliyor" diye durdurulamaz.

**Asıl risk — ve v2'de ortaya çıkan aynası.**
En büyük tehlike, mevcut olgunluğun **URS uyumu sanılmasıdır**. Sistem GMG-QMS-SOP-0001'e göre olgundur; URS'e göre farklı bir mimari varsayım üzerine kuruludur. Klasör-merkezli sınıflandırma (W-1) ve status-folder'a dayalı erişim kuralları (W-2), URS §5'in "bu spesifikasyonun çekirdek requirement'ı" dediği maddenin tam tersidir.

**Aynası:** URS'ü **yanlış olduğu yerde doğru saymak**. v2'de üç requirement kanıtla temas edince düştü — URS-131b ölçülmemiş bir sayıydı, URS-060 bir mekanizma dayatmasıydı, URS-143'ü kendi annex'i ihlal ediyordu. Bir gap analizi tek yönlü çalışırsa, bu üçü yazılımın hanesine yazılır ve **yanlış iş yaptırır**: 500 MB için streaming refactor'ı, document store üzerinde ACID transaction arayışı. İkisi de gereksizdi.

**Zamanlama — v2'nin en önemli bulgusu.**
W-1'in maliyeti bugün ölçülü olarak en düşük seviyededir: **ERP'de sıfır doküman dosyalanmış durumda.** Proje bu ekonomiyi iki kez kanıtladı — Setonda→İlaçları değişimi 271 path'i bedavaya taşıdı, v0.32 eklemeleri 75 dosyayı önemsizce serbest bıraktı; her ikisi de henüz hiçbir şey dosyalanmadığı için. G-2 kapandığına göre yön de bellidir. **Pencere açık, ama kalıcı değil.**

**Tavsiye edilen yol.**
1. **Faz 0'ı derhal başlat** — artık beş maddeli: R-01 (kalan 9 karar), R-03 (runtime), **R-10 (admin bypass)**, **R-11 (denied audit)**. R-02 kapandı.
2. **Faz 1'i (metadata modeli) vocabulary kararları gelir gelmez uygula** — R-08 artık P0.
3. **R-26'yı hiçbir koşulda R-09'dan önce koşma.** Planın ucuz şekilde geri alınamayan tek kararı budur.
4. **G-1 kapanmadan qualified-signature harcaması yapma**; ama R-17'yi (gate) beklemeden yap.
5. **§7b'yi URS revizyonuna sok** — yazılımı spec'e indirme, spec'i yazılıma çek.

Bu sıra izlenirse verdict'in bir sonraki yayında **READY WITH REMEDIATION**'a taşınması gerçekçidir. Faz 0 tek başına verdict'i değiştirmez ama **verdict'i kanıtlanabilir kılar** — ki bugün olmayan şey odur.

---

*Bu doküman ANALİZ-ONLY'dir. Kod, veritabanı, migration veya commit değişikliği yapılmamıştır. Tüm durum atamaları gösterilen dosya/sınıf/endpoint/test kanıtlarına dayanır; kanıt bulunamayan hiçbir capability PASS sayılmamıştır.*
*v2, URS sahibinin 2026-08-24 tarihli yanıtı üzerine revize edilmiştir. §0b'deki üç madde spesifikasyon kusuru olarak ayrılmış ve yazılım aleyhine puanlanmamıştır. Kapatılan governance kararı: G-2 (ERP authoritative).*
*Açık teyit talebi: URS sahibi "üç requirement kanıtla temas edince düştü" diyor; bu analizde ikisi adlandırılmıştır (URS-131b, URS-060). Üçüncüsü §0b/S-DEF-3'te URS-143 olarak varsayılmıştır — teyit edilmelidir.*
