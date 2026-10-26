---
id: MOD-0217
name: Contract Lifecycle Management (CLM)
domain: master-data-management
service: Diten.MdmService
service_port: 5059
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
approval_status: not-approved
owner: mdm-domain-team
branch: feature/mdm/mod-0217-contract-lifecycle-management-clm
started: 2026-10-26
target: 2027-03-31
form_field_count: 12
slice: "TRM-13 — sözleşme kaydı + sözleşme sürümleri + belge bağlantısı (MOD-0028 + MOD-0262-FU01) + onay (MOD-0023) + imza kaydı + değişiklik / yenileme / fesih (ödeme, e-imza sağlayıcısı, olay yayını YOK)"
---

# MOD-0217 — Contract Lifecycle Management (CLM)

> **Durum: `draft` (planlama belgesi), `approval_status: not-approved`.** Bu paket kod, servis değişikliği, gateway
> rotası, sözleşme dosyası ya da kaynak projeden kod alma izni vermez. **Kod alma yok (KARAR D38/3):** kaynak (`S@da4abb20`)
> yalnız okunur, başvuru malzemesidir; kod Recovery'de, MDM düzeninde sıfırdan yazılır. Kod ancak paket `approved` /
> `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten sonra
> yazılır. Engeller: "Ready-for-dev Checklist" (işaretsiz maddeler paket düzeyi; "— DL-n için" maddeleri o dilimin
> önkoşulu) ve "Açık kararlar".
>
> **Bağlanan kararlar (yeniden karar verilmez):** D9 (başka modüldeki en küçük eklemeli değişiklik onayı; sorulmasını
> yetkilendirir, o modül sahibinin cevabının yerine geçmez) ve **D19/1** (MOD-0217 → `master-data-management`; pack TRM-11
> cevabından ve "Document Management" kapısı bir modüle çözüldükten sonra) —
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`; D29/2 (RCS-001 önek yolu: önek önerisi + `.antigravity`
> yaması sahibe gösterilir — bu paketin öneki için **uygulanmadı**, O-5); **D37** (Document Management kapısı = MOD-0028 belge
> kaydı ve sürümleri + MOD-0262-FU01 dosya deposu; MOD-0217 yalnız tüketicidir, o modüllerde değişiklik XMC-001 ile sorulur)
> ve **D38/1–4** (yerleşim `master-data-management` / `Diten.MdmService`, sahip MDM sahibi yerine karar verdi — D19/1'in MDM
> sahibi onayı koşulu kapandı; taslağı Treasury CT yazar; kod alma yok; MOD-0023 iş akışı şablonu MOD-0023 sahibinin
> cevabını gerektirir) — `docs/records/decisions/2026-10/treasury-intake-owner-decisions-03.md`. TRM-11 cevabı:
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/PRODUCER-ANSWER-TRM11-01.md` (kaynakta işlem atomikliği
> doğrulanmış sayılmaz; Recovery kendi gerçek replica set testleriyle kanıtlar). Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0217-v1.0.md` — dört koşul, bkz. "Implementation Notes →
> Level-1 koşulları". Kuyruk: `treasury-intake` TRM-13.
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-26, bu worktree (HEAD `31c448d5d`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0217 --name "Contract Lifecycle Management (CLM)"` →
> `OK  MOD-0217: proven against Blueprint/registry.` (çıkış 0). `--discover "Contract Lifecycle Management"`: Blueprint 1
> aday (MOD-0217), kayıt 1 satır (`reserved / planned` — çalışma ağacında, commit edilmemiş; aşağıda), eski paket
> `MDM-070-contract-lifecycle-management-clm.md` yalnız 8 başka ref'te (HEAD ağacında yok), gateway `contracts` → 5065
> (MOD-0144'ün rotası — "Ownership and Boundaries").
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.0
> (`/Users/natig/Projects/treasury-transfer-handoff/source-batch4-v1.0/docs/roadmap/plans/treasury-source-transfer-preparation/mod0217-transfer-packet-v1.0/`;
> `manifest.json` sha256 `0d822c4f93eea64e623b1ef163488e50fceee8034ca0a77cc8b27de53cfd507b` — bu oturumda `shasum -a 256` ile
> ölçüldü, level-1 kaydıyla aynı; paket dosyası sha256 `9d12625d…b2fa`, manifest değeriyle aynı), `AUTHOR-REPORT.md` ve yalnız
> arama için `CONTRACT-STATE.json` (`operations`, `data_catalog`, `security`, `mutation_guarantees`, `consumer_acks`,
> `decisions_and_open_items`). `SOURCE-BINDINGS.json` açılmadı. Kaynak kodun kendisi (`S@da4abb20`) bu oturumda
> **okunmadı**; `Dosya.cs:satır@da4abb20` biçimindeki atıflar paketten ve CONTRACT-STATE'ten aktarılmıştır (paket atıfı;
> kaynak MDM yolu `services/DitenMdmService/src/…`). Recovery atıfları `yol:satır@31c448d5d` (bu worktree, bu oturumda ölçüldü).
>
> **Revizyon 1 (2026-10-26):** ilk taslak. Dayanak: D19/1 (yerleşim + koşullar), D37 (belge kapısı), D38 (MDM yerleşimi,
> kod alma yok, taslağı CT yazar), TRM-11 (işlem güvenceleri Recovery testiyle). MDM domain-config satırı ve kimlik kaydı
> satırı aynı öneride CT tarafından yazılıyor (bu oturumun başında çalışma ağacında **commit edilmemiş** değişiklik olarak
> görüldü; bu paket onları değiştirmez). Bu revizyon pack'i `ready-for-dev` yapmaz.
>
> **Revizyon 2 (2026-10-26): sahip denetimi (gap listesi 22) kapatıldı —** P1-a belge işaretçisi yazımında kesin geri alma ile
> sonucu bilinmeyen yazım ayrıldı; telafi yalnız işaretçinin bağlı olmadığı okunarak kanıtlanınca, okuma yoksa durum
> "bilinmiyor" ve sonra `ReconcileContractDocumentCommand` (Runtime Constraints → "Uzak çağrı sırası", AC-26, AC-49, AC-50);
> P1-b Sync yalnız güncel onay denemesinin anahtarı ve örnek kimliğiyle eşleşen MOD-0023 örneğini kabul eder, MOD-0023 §17 rev 2
> okuma alanlarıyla (AC-51, AC-52); P1-c MOD-0023 başlatma yanıtları kesin ret / geçici / bilinmiyor tablosuna ayrıldı, kesin
> ret sürümü denetim kaydı ve gerekçe koduyla `Draft`'a döndürür, yeniden başlatmadan önce izin / şirket / aktör bağı yeniden
> denetlenir (AC-53…AC-55); P2-a kesişen AC'ler dilimlere "(… kısmı)" ile bölündü, kapanış kuralı yazıldı; P2-b imzada
> kullanılan belge geri çekilemez, son imza bütün imzaların belge başvurusunu aynı işlemde yeniden doğrular (AC-56, AC-57);
> P2-c tek kapı kuralı: işaretsiz checklist maddeleri paket düzeyi, "— DL-n için" maddeleri o dilimin önkoşulu (O-2 → DL-2);
> küçük: Create'in iki niyeti AC-4 ve işlem tablosunda adıyla yazıldı. Açık kararlar O-1…O-15 açık kalır; bu revizyon pack'i
> `ready-for-dev` yapmaz.

## Module Summary

Kiracının **hukuki sözleşme kaydı** için system-of-record: sözleşme başlığı, sözleşme sürümleri (ilk metin + değişiklikler),
onay ve imza kaydı, yenileme kararı, fesih ve arşiv. Blueprint (paket §A, ölçüm üreticinin): Wave W-3, Domain `5) Assurance,
Risk, and Controls`, Suite `Legal & Contracts`, Capability Group `Legal Governance`; SoR "contracts, contract versions,
approvals/sign-offs, contract metadata"; Dependency Gate "Document Management; Workflow Designer; Audit Trail Service"; asgari
sözleşme kodu `LEGAL-BUNDLE` (kaynakta eseri yok — paket §B "ABSENT").

- **Hedef kullanıcı:** sözleşme sahibi (iç sorumlu; sözleşmeyi açar, sürüm hazırlar, belge ekler), onaylayıcı (MOD-0023
  görevinde onaylar — bu modülde onay ucu yoktur), imza kaydını giren hukuk / idari kullanıcı, ticari değeri görme yetkisi
  olan sınırlı kullanıcı, kiracı geneli okuyucu (hukuk birimi).
- **Çekirdek:** bir sözleşmenin hukuken geçerli hâli, **imzası tamamlanmış (Effective)** sürümüdür. Sürüm onaydan önce
  düzenlenir, onaya girdiğinde donar, imzalanınca yürürlüğe girer, sonraki değişiklik yürürlüğe girince geçersizleşir
  (Superseded). Her geçiş tek bir denetimli komuttur.
- **Belgeler (D37):** sözleşme dosyası MDM'de saklanmaz; ikili içerik MOD-0262-FU01 belge dosya deposunda, belge kaydı ve
  sürümleri MOD-0028 üzerinden (MOD-0028 yüzeyi bugün ölçülemedi — O-2). MOD-0217 yalnız işaretçi (`ContentId`) tutar.
- **Onay (MOD-0023):** sürüm onaya MOD-0023 iş akışı örneği başlatılarak girer; sonuç MOD-0023'ten **okunur**. Kaynaktaki
  geri çağrı uçları taşınmaz.
- **Yok:** ödeme, e-imza sağlayıcısı, entegrasyon olayı yayını, kanıt paketi (MOD-0031), toplu işlem (Ownership and
  Boundaries).

## Ownership and Boundaries

- **In-scope (v1):** `Contract` başlığı (oluştur, düzenle, taslak silme, fesih, arşiv); `ContractVersion` (ilk sürüm +
  değişiklik sürümleri; düzenle, geri çek); sürüme imzacı listesi ve imza kaydı; sürüme belge işaretçisi ekleme / geri çekme
  (dosya MOD-0262-FU01'e, D37); sürümün MOD-0023'e onaya gönderilmesi ve sonucun okunması; yenileme kararı; bitişi yaklaşan
  sözleşmeler süzgeci; ticari değer görünürlüğü; veri kapsamı (SEC-002, yöneten şirket); kiracı ekranı (`Views/MasterData/Contracts/`).
- **Out-of-scope (v1):** satın alma / tedarik sözleşmeleri ve madde kütüphanesi (**MOD-0144 Contracting & Clause Library**,
  procurement, `ready-for-dev`, kod var — sınır O-1); e-imza sağlayıcısı ve imza geri çağrısı (O-8); kanıt değerlendirmesi ve
  kanıt bağlantısı (MOD-0031, O-9); gizlilik düzeyi ve risk derecesi alanları (O-9); entegrasyon olayları `contract.*.v1`
  (O-15); toplu eylem ve dışa aktarma; saklama / yasal tutma (MOD-0030 — belge ikilisi MOD-0262'nin işidir); ödeme sırası
  (kaynakta devralınmış "0126 → 0217 → 0124" sırası, paket §J.2 — benimsenmedi, O-15); müşteri karşı tarafı (CRM hesap
  kimliği bu oturumda ölçülmedi — kimlik uydurulmaz, O-7).
- **MOD-0144 ile çakışma (ölçüldü, @31c448d5d):** Recovery'de MOD-0144 "Contracting & Clause Library" (`procurement`,
  `ready-for-dev`, SoR "sourcing/procurement contracts, clause deviations", asgari sözleşme `CLM-CONTRACT-BUNDLE` —
  `execution/registries/module-id-registry.md:339`) `/api/contracts` rotasını (Gateway → 5065,
  `gateway/Diten.ApiGateway/ocelot.json:850,870`; `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/ContractsController.cs:29`),
  `procurement.contracts.*` izinlerini, web rotası `/Contracts`'ı (`frontend/Diten.Web/Controllers/ContractsController.cs:14,22`)
  ve `Nav.Page.CONTRACTS` anahtarını (`frontend/Diten.Web/Resources/SharedResource.en.resx:1043`) kullanıyor. Bu paket
  bunların hiçbirine dokunmaz ve hiçbirini yeniden kullanmaz (Gateway, Frontend, Page Help bölümleri). İki modülün sınırı
  açık karardır (O-1); karar çıkmadan `ready-for-dev` yok.
- **Sahip olunan sözleşme:** yok (v1'de tüketiciye açılan okuma sözleşmesi yazılmaz; ilk tüketici çıkınca Follow-up).
  Kaynakta resmi sözleşme yoktur (paket §B "ABSENT"; `LEGAL-BUNDLE` eseri yok).
- **Tüketilen yapılar:** MOD-0220 `LegalEntity` (aynı servis — depo üzerinden süreç içi okuma); MOD-0262-FU01
  `/api/v1/document-repository/objects` (D37); MOD-0028 belge kaydı (D37 — yüzey ölçülemedi, O-2); MOD-0023
  `/api/v1/workflow/instances` (O-3); MOD-0048 para birimi listesi; RCS-001 ortak kod servisi (BL-565); SEC-002 kapsam
  sağlayıcısı; MDM denetim izi `mdm-guvenilir-kaynak-niyeti`.
- **Domain-config ile ilişki:** D38/2 ile MDM domain-config'e "In-Scope Reserved Modules (Treasury intake)" satırı CT
  tarafından öneriliyor (çalışma ağacında, commit edilmemiş — ölçüldü); onaylanıp main'e girmeden `ready-for-dev` yok. Yetki
  sırası gereği module pack domain-config'in üstündedir; bu paket domain-config'i kendisi değiştirmez.

## Owned Objects

### Varlıklar (kaynak 7 varlık sınıfı → Recovery karşılığı)

Kaynak: paket §E, CONTRACT-STATE `data_catalog` (hepsi kaynakta `: EntityBase`, 7 `mod0217_*` koleksiyonu,
`MongoDbIndexConfigurations.cs:85-270@da4abb20`).

| # | Kaynak varlık (özellik sayısı) | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `Contract` (51) | **tasarım olarak alınır, uyarlanır** — `Contract : EntityBase` (başlık) | çekirdek; 51 alanın çoğu (onay / imza / kanıt / gizlilik / risk boyutları) tek durum modeline ve sürüme dağılır |
| 2 | `ContractVersion` (13) | **tasarım olarak alınır, uyarlanır** — `ContractVersion : EntityBase` | Blueprint SoR "contract versions"; ticari koşullar, imzacılar, belge işaretçileri sürümde |
| 3 | `ContractAmendment` (11) | **birleşir** → `ContractVersion` (`Kind = Amendment`) | değişiklik yeni bir sürümdür; ayrı tip ayrı bir soy ağacı kuralı ve ayrı işlem grubu doğurur |
| 4 | `ContractPartyLink` (8) | **uyarlanır** — karşı taraf başlıkta, imzacılar sürümde (gömülü) | ayrı koleksiyon çok belgeli işlem ister; v1'de tek karşı taraf (O-7) |
| 5 | `RenewalDecision` (8) | **uyarlanır** — `Contract.RenewalDecisions[]` (gömülü, yalnız ekleme) | küçük, sözleşmeye bağlı liste |
| 6 | `ContractCreateReceipt` (8) | **taşınmaz** | üretimde yazanı yok (`CreateWithReceiptAsync` 0 üretim çağıranı, `Mod0217ContractAggregateStore.cs:38@da4abb20`, paket §H); yerine `Idempotency-Key` + belgedeki oluşturma anahtarı (O-10) |
| 7 | `Mod0217CallbackReceipt` (9) | **taşınmaz** | geri çağrı üreticisi bağlı değil (paket §L.5); sonuç MOD-0023'ten okunur |

### Kaynak işlemleri (19 satır, `CONTRACT-STATE.json.operations`) → Recovery

Kaynak tabanı `api/contracts` ve `api/v1/contracts` (`services/DitenMdmService/src/Diten.MdmService.Api/Controllers/ContractsController.cs:15-16@da4abb20`); ikisi de taşınmaz.

| Kaynak (satır `@da4abb20`) | Recovery komut / sorgu | Recovery uç (Gateway `/api/mdm/…`) | Karar |
|---|---|---|---|
| CLM-01 `GET` liste (`:40`, `contract.view`) | `GetContractListQuery` | `GET /api/mdm/contracts` | tasarım alınır; sayfa ≤ 200 (paket §K) |
| CLM-02 `GET create-attempts/{attemptId}` (`:81`) | — | — | **taşınmaz** (makbuz araması kapalı, yazanı yok); yerine `Idempotency-Key` tekrarı |
| CLM-03 `GET {contractId}` (`:92`) | `GetContractByIdQuery` (+ `GetContractVersionListQuery`, `GetContractVersionByIdQuery`) | `GET /api/mdm/contracts/{id}`, `…/{id}/versions`, `…/{id}/versions/{versionNo}` | uyarlanır; ticari değer izinle |
| CLM-04 `POST` oluştur (`:102`) | `CreateContractCommand` (sözleşme + sürüm 1, tek işlem) | `POST /api/mdm/contracts` | uyarlanır + `Idempotency-Key` zorunlu (O-10) |
| CLM-05 `PUT {contractId}` (`:113`) | `UpdateContractCommand` (başlık), `UpdateContractVersionCommand` (taslak sürüm) | `PUT /api/mdm/contracts/{id}`, `PUT …/{id}/versions/{versionNo}` | bölünür |
| CLM-06 `submit-review` (`:126`), CLM-07 `submit-approval` (`:139`) | `SubmitContractVersionForApprovalCommand` | `POST …/{id}/versions/{versionNo}/submit-approval` | **birleşir**: tek onay girişi MOD-0023'tür (inceleme adımı iş akışı şablonunda, O-3) |
| CLM-08 `lifecycle-preflight` (`:152`, `[HasPermission]` yok) | — | — | **taşınmaz**; izin verilen eylemler detay DTO'sunda sunucu tarafında hesaplanır (`allowedActions`) |
| CLM-09 `review/request-info` (`:187`), CLM-10 `review/reject` (`:202`) | — | — | **taşınmaz**; MOD-0023 görev uçları (`tasks/{taskId}/request-info`, `…/reject` — `services/Diten.Platform/src/Diten.Platform.API/Controllers/WorkflowDefinitionsController.cs:221,261@31c448d5d`) |
| CLM-11 `send-signature` (`:217`) | — | — | **taşınmaz** (e-imza sağlayıcısı yok, O-8) |
| CLM-12 `record-signature` (`:230`) | `RecordContractSignatureCommand` | `POST …/{id}/versions/{versionNo}/signatures` | uyarlanır: imzalı kopya belgesi zorunlu |
| CLM-13 `amendments` (`:243`) | `CreateContractAmendmentCommand` | `POST /api/mdm/contracts/{id}/amendments` | uyarlanır (yeni taslak sürüm) |
| CLM-14 `renewal-decision` (`:256`) | `RecordContractRenewalDecisionCommand` | `POST …/{id}/renewal-decisions` | uyarlanır |
| CLM-15 `terminate` (`:268`) | `TerminateContractCommand` | `POST …/{id}/terminate` | tasarım alınır |
| CLM-16 `archive` (`:281`) | `ArchiveContractCommand` | `POST …/{id}/archive` | tasarım alınır |
| CLM-17 `callbacks/workflow` (`:294`), CLM-18 `callbacks/signature` (`:313`) | `SyncContractVersionApprovalCommand` (yalnız iş akışı; MOD-0023'ten okur) | `POST …/{id}/versions/{versionNo}/approval-sync` | **taşınmaz** (geri çağrı); yerine okuma |
| CLM-19 `dashboard/renewals` (`:332`) | `GetContractListQuery` süzgeci `expiringWithinDays` | `GET /api/mdm/contracts?expiringWithinDays=n` | uyarlanır: ayrı pano ucu yok; pencere varsayılan 30, en çok 180 (paket §K, `ContractQueryHandlers.cs:204@da4abb20`) |

### Recovery ekleri (kaynakta yok)

| Komut / sorgu | Uç | Neden |
|---|---|---|
| `DeleteContractCommand`, `BulkDeleteContractCommand` | `DELETE /api/mdm/contracts/{id}`, `DELETE /api/mdm/contracts/bulk` | kaynakta API silme yok (paket §F); Golden Reference silme seti — yalnız hiç onaya gönderilmemiş taslak, soft delete |
| `AddContractDocumentCommand`, `WithdrawContractDocumentCommand`, `GetContractDocumentContentQuery` | `POST …/{id}/versions/{versionNo}/documents` (multipart), `POST …/documents/{contentId}/withdraw`, `GET …/documents/{contentId}/content` | kaynakta yükleme web katmanında yerel geçici klasöre yazıyordu (`frontend/Diten.Web/Controllers/ContractsController.cs:712-748@da4abb20`, paket §D) — taşınmaz; D37 yolu |
| `ReconcileContractDocumentCommand` | `POST …/{id}/versions/{versionNo}/documents/{contentId}/reconcile` | işaretçi yazımının sonucu bilinmediğinde (Runtime Constraints → "Uzak çağrı sırası") bağın sonradan okunarak çözülmesi; MDM'ye yazmaz, telafiyi yalnız kanıtla çağırır (Revizyon 2, P1-a) |
| `WithdrawContractVersionCommand` | `POST …/{id}/versions/{versionNo}/withdraw` | açık değişiklik taslağını kapatma (silme değil) |

- **DTO'lar (tek dosya, Golden Reference):** `ContractModels.cs` — `ContractListItemDto`, `ContractDetailDto` (+
  `allowedActions`), `ContractWriteRequest`, `ContractVersionDto`, `ContractVersionWriteRequest`, `ContractSignatoryDto`,
  `ContractDocumentDto`, `ContractSignatureRequest`, `ContractAmendmentRequest`, `ContractRenewalDecisionRequest`,
  `ContractTerminateRequest`. DTO'larda ve isteklerde `TenantId` **yoktur**. Ticari değer alanları (`contractValue`,
  `currencyCode`) yalnız `mdm.contracts.value.read` sahibine dolu döner; diğerlerine `null` + `valueMasked: true`.
- **Portlar (Application):** `IRecordCodePort` (RCS-001), `ICurrencyReferencePort` (MOD-0048), `IDocumentBinaryStorePort`
  (MOD-0262-FU01: sakla, meta oku, akış oku, telafi), `IDocumentRecordPort` (MOD-0028 — yüzeyi O-2'ye bağlı),
  `IContractWorkflowPort` (MOD-0023: başlat, nesneye göre örnek oku `includeDeleted=true`, örnek geçmişi oku — MOD-0023 §17
  rev 2 alanlarıyla), `IContractDataScopeSource` (SEC-002). Şirket için port
  yok: `ILegalEntityRepository` süreç içi okunur (aynı servis). Tedarikçi karşı tarafı için port O-7'ye bağlı.
- **Frontend rotaları:** `/MasterData/Contracts` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`).

### Durumlar

Kaynakta `ContractStatus` 8 değer ("Draft…Archived"), onay 4, imza 5, kanıt 4, gizlilik 4, risk 4 değerli ayrı boyutlar
vardı (`Mod0217ContractEnums.cs@da4abb20`, paket §E); ara değerlerin adları paket metninde yok ve kopyalanmaz. Recovery v1
**iki tek boyutlu durum** kullanır (öneri, O-11): sözleşme durumu ve sürüm durumu.

`ContractStatus` (başlık):

| Durum | Değer | Anlam | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 1 | henüz yürürlüğe girmiş sürümü yok | Create | Active (ilk sürüm Effective), Delete (hiç onaya gönderilmemişse), Archive (açık sürüm onayda değilse) |
| Active | 2 | imzası tamamlanmış bir sürümü var | ilk sürümün Effective olması | Terminate; Archive (yalnız `CurrentEffectiveTo` geçmişse) |
| Terminated | 3 | feshedildi; açık sürüm kalmaz | Terminate | Archive |
| Archived | 4 | salt okunur; listede varsayılan süzgeç dışında | Archive | — |

`ContractVersionStatus` (sürüm):

| Durum | Değer | Anlam | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 1 | düzenlenebilir | Create (sürüm 1), CreateAmendment, onay reddi / iptali, MOD-0023 başlatmasının **kesin reddi** (Runtime Constraints → "Başlatma yanıtı sınıfları") | Submit, Withdraw (yalnız değişiklik sürümü) |
| InApproval | 2 | donmuş; MOD-0023 örneği bekleniyor. `WorkflowInstanceId` boşsa **başlatma bekliyor** alt hâli (başlatma sonucu geçici hata ya da bilinmiyor) | Submit | Sync → Approved / Draft; Submit (kesin ret) → Draft; başlatma bekliyorken yalnız Sync ya da aynı anahtarla Submit tekrarı |
| Approved | 3 | onaylı; imza bekliyor; yalnız imza ve imzalı kopya eklenir | Sync (onay) | RecordSignature (son imza) → Effective |
| Effective | 4 | yürürlükteki sürüm (sözleşmede tek) | son imzanın kaydı | Superseded |
| Superseded | 5 | yerini sonraki sürüm aldı | sonraki sürümün Effective olması | — |
| Withdrawn | 6 | geri çekilmiş değişiklik taslağı | Withdraw | — |

- Sözleşmede aynı anda en çok **bir açık sürüm** (Draft / InApproval / Approved) bulunur (`Contract.OpenVersionNo`).
- **"Süresi doldu" saklanmaz:** `CurrentEffectiveTo` bugünden (UTC tarih) önceyse okuma modeli `expired = true` döner; bunu
  yazan arka plan işi yoktur (İ2 satırı gerekmez). Bitiş tarihinin geçmesi sözleşmeyi otomatik feshetmez.

## Entity Fields

Taban: MDM'nin `EntityBase`'i (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:3-12@31c448d5d`):
`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş kaydı olduğu
için `CreatedBy` / `UpdatedBy` (`Guid?`, token `sub`'ından; DTO'dan değil) **zorunlu**.

**Kiracı:** `TenantId` depo tabanında sunucuda yazılır ve her okuma `TenantFilter` (TenantId + `IsDeleted = false`) ile
süzülür (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:69-71@31c448d5d`).
Gövdede / sorguda `tenantId` gelirse 400. `X-Tenant-Id` başlığı kiracı kaynağı değildir. `GlobalEntity` kullanılmaz.

**Soft delete:** taban `DeleteAsync` `IsDeleted` + `DeletedAt` + `UpdatedAt` yazar (`RepositoryBase.cs:54-67@31c448d5d`).
**Dikkat (ölçüldü):** taban `UpdateAsync` `Version`'ı artırır ama süzgeçte sürüm yoktur (`RepositoryBase.cs:40-52@31c448d5d`)
— iyimser eşzamanlılık sağlamaz. Bu modül süzgeçte `Version == expectedVersion` taşıyan koşullu güncelleme yazar; taban
`UpdateAsync` çağrılmaz.

### Contract (`mdm_contracts`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| Code | string | RCS-001 OTOMATİK; tür öneki **önerisi `CTR`** (O-5 — karar değil); şirket öneki = yöneten şirketin kısa kodu (RCS-001 §5.4; kısa kodun `LegalEntity`'deki alanı **ölçülmedi**); istekte gelirse 400; silinen kaydın kodu yeniden kullanılmaz | kaynak `ContractNumber` (tekil `(TenantId, ContractNumber)`, paket §E) |
| Title | string | zorunlu, trim, 1..200, kontrol karakteri yok | yeni ad |
| ContractType | string | zorunlu; sabit çekirdek kod listesinden (O-11 önerisi: `SALES`, `SERVICE`, `NDA`, `LEASE`, `LICENSE`, `PARTNERSHIP`, `INTRA_GROUP`, `OTHER`); etiket resx'te | kaynakta referans veriden ("contract types" lookup, paket §I) |
| GoverningLegalEntityId | Guid | zorunlu; aynı kiracıda MOD-0220 `LegalEntity`, Create'te `OperationalStatus = Active`; sürüm 1 bir kez onaya gönderildikten sonra **değişmez**; SEC-002 kapsamını taşır | kaynak `GoverningLegalEntityId` (yalnız süzgeç, paket §G) |
| CounterpartyType | enum `LegalEntity`=1, `Supplier`=2, `External`=3 | zorunlu (O-7 önerisi); kural GoverningLegalEntityId ile aynı (değişmezlik) | kaynak `ContractCounterpartyType` 6 değer (adlar paket metninde yok) |
| CounterpartyLegalEntityId | Guid? | `LegalEntity` türünde zorunlu; aynı kiracı; yöneten şirketten farklı | — |
| CounterpartySupplierId | string? | `Supplier` türünde zorunlu; MOD-0140 kimliği **olduğu gibi** (string); doğrulama yolu O-7 | — |
| CounterpartyName | string? | `External` türünde zorunlu, 1..200; diğer türlerde boş (ad okumada çözülür, kopya saklanmaz) | — |
| InternalOwnerUserId | Guid | zorunlu; varsayılan oluşturan kullanıcı; başka kullanıcıya atama doğrulama yolu O-14 | kaynak `InternalOwnerUserId` |
| Status | `ContractStatus` | Durumlar | — |
| OpenVersionNo | int? | açık sürüm numarası; açık sürüm yoksa `null` | yeni |
| LatestVersionNo | int | sürüm sayacı; her yeni sürümde aynı işlemde CAS ile +1 | yeni |
| CurrentEffectiveVersionNo | int? | yürürlükteki sürüm | yeni |
| CurrentEffectiveTo | DateOnly? | yürürlükteki sürümün bitiş tarihi (aynı işlemde kopyalanır; süzgeç ve sıralama için) | yeni |
| RenewalDecisions | `List<ContractRenewalDecision>` | yalnız ekleme: `Id`, `Decision` (O-11 önerisi `RENEW` \| `RENEGOTIATE` \| `NOT_RENEW`), `ForVersionNo`, `Note` ≤ 500, `DecidedAt`, `DecidedBy` | kaynak `RenewalDecision` (6 değer, adlar paket metninde yok) |
| TerminatedAt / TerminatedBy / TerminationReasonCode / TerminationEffectiveDate | DateTimeOffset? / Guid? / string? / DateOnly? | Terminate'te zorunlu (gerekçe 1..64) | — |
| ArchivedAt / ArchivedBy | DateTimeOffset? / Guid? | Archive | — |
| CreateIdempotencyKey / CreateRequestHash | string? / string? | `Idempotency-Key` (1..100, `^[A-Za-z0-9._:-]+$`) ve normalize istek gövdesinin SHA-256'sı (O-10) | kaynak `IdempotencyKey` alanı Create'te desteklenmiyordu (paket §H) |
| CreatedBy / UpdatedBy | Guid? | token `sub` | — |
| AuditIntents / AuditIntentReceipts | `List<LocalAuditIntent>` / `List<LocalAuditIntentReceipt>` | `IAuditIntentAggregate` (Audited Events) | MDM deseni |

### ContractVersion (`mdm_contract_versions`)

| Alan | Tip | Kural |
|---|---|---|
| ContractId | Guid | aynı kiracıda var olan, silinmemiş sözleşme |
| VersionNo | int | 1'den başlar; `Contract.LatestVersionNo` ile aynı işlemde atanır; `(TenantId, ContractId, VersionNo)` tekil |
| Kind | enum `Original`=1, `Amendment`=2 | sürüm 1 `Original`, diğerleri `Amendment` |
| AmendmentReason | string? | `Amendment`'ta zorunlu, 1..500 |
| Status | `ContractVersionStatus` | Durumlar |
| EffectiveFrom | DateOnly | zorunlu |
| EffectiveTo | DateOnly? | boş = süresiz; doluysa ≥ EffectiveFrom |
| NoticePeriodDays | int? | 0..3650 |
| CurrencyCode | string(3)? | `ContractValue` doluysa zorunlu; `^[A-Z]{3}$`, MOD-0048 listesinde |
| ContractValue | decimal? | ≥ 0; küsurat hanesi MOD-0048'den (MOD-0289 D24/8 deseni — bu paket için karar değil, O-11) |
| Summary | string? | ≤ 2000 |
| Signatories | `List<ContractSignatory>` | `Id` (sunucu), `Side` (`Internal`=1, `Counterparty`=2), `Name` 1..140, `RoleTitle` ≤ 100, `InternalUserId` Guid? (`Internal`'da isteğe bağlı), `SignedAt` DateTimeOffset?, `SignatureRecordedBy` Guid?, `SignedCopyContentId` Guid?; onaya göndermeden önce en az bir `Internal` ve bir `Counterparty` (öneri). **`SignedCopyContentId` imza kaydında bir kez yazılır, sonra değişmez**; gösterdiği belge geri çekilemez (Validation Rules → WithdrawContractDocument) |
| Documents | `List<ContractDocumentRef>` | `ContentId` (MOD-0262-FU01; sürüm içinde belge anahtarı), `DocumentRole` (`Draft`=1, `SignedCopy`=2, `Annex`=3), `FileName`, `MediaType`, `ByteSize`, `Checksum` (MOD-0262'nin hesapladığı, istemciden alınmaz), `AddedAt`, `AddedBy`, `WithdrawnAt` / `WithdrawnBy`, `DocumentRecordRef` string? (MOD-0028, O-2). Bir imzacının `SignedCopyContentId`'si olan belge **imzaya bağlıdır**: geri çekme 409 `MDM_CONTRACT_DOCUMENT_IN_USE` |
| WorkflowIdempotencyKey | string? | **güncel onay denemesinin** anahtarı `mdm-contract:{ContractId}:v{VersionNo}:a{ApprovalAttempt}`; her Submit'te sunucu yeni denemeyle yazar (önceki denemenin anahtarı yeniden kullanılmaz) |
| WorkflowInstanceId | Guid? | güncel denemenin MOD-0023 örneği; yalnız başlatma **doğrulandığında** yazılır (başlatma 200/201 yanıtı ya da Sync'te güncel anahtarla tek eşleşen satır); her Submit'te `null`'a döner |
| ApprovalAttempt | int | her Submit'te +1; güncel deneme = (`ApprovalAttempt`, `WorkflowIdempotencyKey`, `WorkflowInstanceId`) üçlüsü |
| SubmittedAt / SubmittedBy | DateTimeOffset? / Guid? | Submit (güncel deneme); aynı anahtarla tekrar yalnız `SubmittedBy` kullanıcısınca |
| LastApprovalOutcome / LastApprovalOutcomeAt | enum `None`=0, `Approved`=1, `Rejected`=2, `Cancelled`=3, `StartRefused`=4 / DateTimeOffset? | Sync; `StartRefused` Submit'in kesin ret yolunda |
| LastApprovalReasonCode | string? | yalnız `StartRefused`'da: `WORKFLOW_REQUEST_INVALID` \| `WORKFLOW_FORBIDDEN` \| `WORKFLOW_SOD_VIOLATION` \| `WORKFLOW_TEMPLATE_NOT_FOUND` \| `WORKFLOW_START_CLOSED` (öneri; MOD-0023 hata gövdesi kodları DL-3 başında ölçülür, izin ile SoD ayırt edilemezse ikisi de `WORKFLOW_FORBIDDEN`) |
| CreatedBy / UpdatedBy | Guid? | token `sub` |
| AuditIntents / AuditIntentReceipts | — | `IAuditIntentAggregate` |

**Taşınmayan kaynak alanları:** onay / imza / kanıt durumu ayrı boyutları, gizlilik düzeyi, risk derecesi (O-9), kanıt
bağlantıları, ayrı taraf bağı koleksiyonu, makbuz ve geri çağrı makbuzu alanları, string aktör kimlikleri, ticari değer takma
ad talepleri (`contract.value.view` vb. 4 takma ad — `Mod0217VerifiedRequestContext.cs:17-29@da4abb20`).

> **`form_field_count: 12` → `golden_reference: compact`.** Contract oluştur formundaki kullanıcı alanları: Title,
> ContractType, GoverningLegalEntityId, CounterpartyType, karşı taraf (türüne göre tek seçici / ad alanı), InternalOwnerUserId,
> EffectiveFrom, EffectiveTo, NoticePeriodDays, CurrencyCode, ContractValue, Summary = 12. **Sayılmayanlar:** Code (RCS-001
> OTOMATİK), Status, sürüm numarası, imzacılar ve belgeler (Details'ta alt tablolar), `Idempotency-Key` (MVC vekili üretir),
> TenantId. 12 > 8 → Compact (ayrı Create / Edit / Details).

### Mongo indeksleri

| İndeks | Anahtar | Tekil | Not |
|---|---|---|---|
| `ux_mdm_contract_code` | TenantId + Code | evet, **kısmi filtre yok** (silinen kaydın kodu dolu sayılır, RCS-001 §6) | — |
| `ux_mdm_contract_create_idempotency` | TenantId + CreateIdempotencyKey | evet, kısmi: `CreateIdempotencyKey` `$type: string` (`$ne` / `$not` kullanılmaz — `mongo-indexing`) | O-10 |
| `ix_mdm_contract_le_status` | TenantId + GoverningLegalEntityId + Status + CreatedAt(desc) | — | liste + veri kapsamı |
| `ix_mdm_contract_effective_to` | TenantId + Status + CurrentEffectiveTo | — | `expiringWithinDays` süzgeci |
| `ix_mdm_contract_owner` | TenantId + InternalOwnerUserId + Status | — | — |
| `ux_mdm_contract_version_no` | TenantId + ContractId + VersionNo | evet | sürüm numarası yarışı |
| `ux_mdm_contract_version_workflow_key` | TenantId + WorkflowIdempotencyKey | evet, kısmi `$type: string` | bir deneme = bir MOD-0023 örneği |

İki `DateTimeOffset` alanı aynı indekste birlikte sıralanmaz (MOD-0262-FU01 pack §4 uyarısı). İndeks kurulumu `mongo-indexing`
kuralına ve MDM depo kurucusu desenine göre; kaynak indeks adları (`uq_mod0217_*`, `idx_mod0217_*`) taşınmaz, kaynak veri
taşınmaz (paket §L.12: kurulu indeks, göç, backfill yok).

### Hata sözlüğü (öneri; kaynakta 9 `MOD0217_CREATE_*` literali, yaşam döngüsü hataları serbest metin — paket §E; kopyalanmaz)

`MDM_CONTRACT_NOT_FOUND` 404 (başka kiracı, kapsam dışı şirket ve silinmiş dahil — varlık sızdırılmaz),
`MDM_CONTRACT_VERSION_NOT_FOUND` 404, `MDM_CONTRACT_DOCUMENT_NOT_FOUND` 404, `MDM_CONTRACT_VALIDATION_FAILED` 400,
`MDM_CONTRACT_CODE_FORBIDDEN` 400, `MDM_CONTRACT_IDEMPOTENCY_KEY_REQUIRED` 400, `MDM_CONTRACT_IDEMPOTENCY_KEY_REUSED` 409
(aynı anahtar, farklı gövde), `MDM_CONTRACT_VERSION_CONFLICT` 409 (bayat `If-Match`), `MDM_CONTRACT_STATE_INVALID` 409,
`MDM_CONTRACT_OPEN_VERSION_EXISTS` 409, `MDM_CONTRACT_IMMUTABLE_FIELD` 409, `MDM_CONTRACT_LEGAL_ENTITY_NOT_ELIGIBLE` 422,
`MDM_CONTRACT_COUNTERPARTY_NOT_ELIGIBLE` 422, `MDM_CONTRACT_SIGNATORIES_INCOMPLETE` 422, `MDM_CONTRACT_SIGNED_COPY_REQUIRED`
422, `MDM_CONTRACT_VALUE_ACCESS_REQUIRED` 403, `MDM_CONTRACT_CURRENCY_UNKNOWN` 400, `MDM_CONTRACT_CURRENCY_REFERENCE_UNAVAILABLE`
503, `MDM_CONTRACT_RECORD_CODE_UNAVAILABLE` 503, `MDM_CONTRACT_AUDIT_UNAVAILABLE` 503 (niyet yazılamadı; iş verisi yok),
`MDM_CONTRACT_DOCUMENT_STORE_UNAVAILABLE` 503, `MDM_CONTRACT_DOCUMENT_REJECTED` 400 (MOD-0262 biçim / tür / boyut reddi),
`MDM_CONTRACT_DOCUMENT_TOO_LARGE` 413, `MDM_CONTRACT_DOCUMENT_RECORD_UNAVAILABLE` 503 (MOD-0028, O-2),
`MDM_CONTRACT_DOCUMENT_IN_USE` 409 (imzaya bağlı belgeyi geri çekme), `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN` 503
(işaretçi yazımının sonucu bilinmiyor ve okunamadı; yanıtta `contentId`, telafi yok),
`MDM_CONTRACT_WORKFLOW_UNAVAILABLE` 503, `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN` 503 (MOD-0023 yanıtı tanınmıyor / çelişkili /
birden çok eşleşme / başlatma sonucu bilinmiyor), `MDM_CONTRACT_WORKFLOW_START_REFUSED` 422 (MOD-0023 başlatmayı kesin
reddetti; yanıtta `reasonCode`), `MDM_CONTRACT_SUBMITTER_MISMATCH` 409 (aynı anahtarla tekrarı `SubmittedBy` dışındaki kullanıcı istedi),
`MDM_CONTRACT_TRANSACTION_UNAVAILABLE` 503 (işlem desteklenmiyor ya da denemeler tükendi). Kullanıcıya giden ileti resx
anahtarıdır.

## Repo Scope

Hepsi `ready-for-dev`'den sonra; kaynaktan kod alınmaz (D38/3). Dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{Contract,ContractVersion}.cs`,
  `Domain/Enums/{ContractStatus,ContractVersionStatus,ContractCounterpartyType,ContractVersionKind,ContractDocumentRole}.cs`,
  `Domain/Repositories/I{Contract,ContractVersion}Repository.cs` (DL-1)
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/Contract/**` (Golden Reference Compact düzeni) (DL-1…DL-3)
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/{Contract,ContractVersion}Repository.cs` ve işlem
  grubu deposu (`ContractAggregateStore.cs`, `MasterDataAtomicWrite` politikasıyla) (DL-1)
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/{RecordCodes,Currency,DocumentRepository,DocumentRecords,Workflow}/**`
  (RCS-001 portu, MOD-0048, MOD-0262-FU01, MOD-0028, MOD-0023 bağdaştırıcıları) (DL-1 / DL-2 / DL-3)
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ContractsController.cs` ve
  `Api/ModuleRegistration/ContractLifecycleManifestProvider.cs` (izin + sayfa manifesti,
  `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/BrandProductMasterManifestProvider.cs:13@31c448d5d` deseni) (DL-1…DL-3)
- **Denetim izinin genişletilmesi (DL-1):** `Domain/Enums/AuditAggregateType.cs` (ekleme: `Contract`, `ContractVersion` —
  bugün dokuz değer, `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs:3-14@31c448d5d`), eklemeli
  genel denetim işlem enum'u (MOD-0289 D24/2'nin önerdiği `MasterDataAuditOperation` bugün kodda **yok** — ölçüldü, Enums
  klasöründe yalnız `ProductAuditOperation`; hangi paket önce koda girerse o ekler), teslim deposu koleksiyon listesi (bugün
  sabit dokuz koleksiyon, `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs:15-23@31c448d5d`) — hepsi yalnız ekleme
- `services/Diten.MdmService/tests/**` (DL-1…DL-4)
- `tests/architecture/audit-ledger/Diten.MdmService.md` — `mdm-guvenilir-kaynak-niyeti` belirtecine yeni yazma üyeleri + aynı
  değişiklikte `tests/architecture/audit-ledger/CT-DECISIONS.md` satırı ve `AuditTrailStandardTests` sabitleri — **CT kararı
  gerekli (O-6)**; D27/11 yalnız MOD-0289 içindir (DL-1)
- `frontend/Diten.Web/Views/MasterData/Contracts/**`, `frontend/Diten.Web/wwwroot/assets/js/MasterData/Contracts/**`,
  `frontend/Diten.Web/Resources/Views/MasterData/Contracts/ContractsIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/MasterData/ClmContractsController.cs` (`MasterDataGatewayController`'dan türeyen same-origin
  vekil; sınıf adı MOD-0144'ün `Diten.Web.Controllers.ContractsController`'ı ile çakışmasın diye `Clm` önekli — öneri),
  `PageHelpResource.{7 dil}.resx` içinde `Help.LEGALCONTRACTS.*`, `Services/Help/PageHelpCatalog.cs` satırı,
  `SharedResource.{7 dil}.resx` içinde `Nav.Page.LEGALCONTRACTS` (DL-4)

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9 / XMC-001; burada
yazılmaz): `ocelot.json` rotaları (integration-agent); MOD-0028'de belge kaydı yüzeyi (O-2, platform-shared-services);
MOD-0262-FU01 `ContentStorageScope`'a eklemeli bir değer gerekirse (bugün `Documents`, `Templates`, `TaskAttachments`,
`ContentMessagingArtifacts` … — `services/Diten.Platform/src/Diten.Platform.Application/Contracts/DocumentRepository/IContentStorageGateway.cs:65-80@31c448d5d`);
MOD-0023'te MOD-0217 iş akışı şablonu (O-3, MOD-0023 sahibi); `.antigravity/rules/record-coding-standard.md` §3 satırı (O-5);
MOD-0018 / Auth izin kataloğu ve açık atama listesi (O-4); MDM domain-config ve kimlik kaydı satırları (CT, D38/2).

## Protected Paths

- `.antigravity/**` (RCS-001 §3 satırı dahil — ayrı sahip onayı, O-5)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.Platform/**` (MOD-0021, MOD-0023, MOD-0028, MOD-0029, MOD-0031, MOD-0048, MOD-0262 — platform-shared-services),
  `services/Diten.ProcurementService/**` (MOD-0144, MOD-0140), `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`,
  diğer domain servisleri
- `services/Diten.MdmService/**` içinde bu paketin Repo Scope'u dışındaki her şey: MOD-0220 `LegalEntity` ve depoları **yalnız
  okunur**, MOD-0290 ürün ailesi, mevcut denetim niyeti üreticileri (yalnız ekleme satırları dışında)
- `frontend/Diten.Web/Views/Procurement/Contracts/**`, `frontend/Diten.Web/Controllers/ContractsController.cs` ve
  `Nav.Page.CONTRACTS` (MOD-0144); `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone`
  (kaynak layout'u; Recovery'de kullanılmaz), `_LayoutTenantShell.cshtml`
- `docs/analysis/contracts/**` (üretici sözleşmeleri; `contracting.openapi.yaml` MOD-0144'ündür)
- `execution/registries/**`, `execution/domains/master-data-management/domain-config.md`, CT kuyruğu — bu paketle değişmez
- Kaynak depo ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz, kod kopyalanmaz (D38/3)

## Dependencies

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm @31c448d5d) | Bu pakete etkisi |
|---|---|---|---|
| **Document Management — MOD-0028** (D37) | belge kaydı ve sürümleri | pack `approved` (`execution/domains/platform-shared-services/module-packs/MOD-0028-document-management.md:9`); sahip olunan nesneler koleksiyon / baz / şablon / `ContentRef` (`:128-145`); **kapsam dışı:** "Controlled document lifecycle; owned by MOD-0029" ve "Binary upload/download" (`:116-126`). Platform'daki belge kaydı + sürüm uçları (`api/v1/document-management/controlled-documents/{documentId}/versions`) kodda **MOD-0029-FU01** olarak işaretli (`services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementControlledDocumentsController.cs:18,105-122`) | **D37'nin MOD-0028 kısmını karşılayan bir iş belgesi kaydı yüzeyi bu oturumda bulunamadı** → O-2; çözülmeden DL-2 başlamaz. MOD-0217 tüketicidir; MOD-0028'de değişiklik XMC-001 ile sahibine sorulur |
| **Document Management — MOD-0262-FU01** (D37) | dosya ikilisi | pack `ready-for-dev`, `runtime_implementation: in-progress` (`MOD-0262-FU01-document-binary-store.md:10,20`); kod var: `POST /api/v1/document-repository/objects` (multipart: `file`, `owningItemId`, `owningVersionId`, `companyId`, `scope`), `GET objects/{contentId}`, `GET objects/{contentId}/content`, `POST objects/{contentId}/compensate` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs:40,59-119`); izinler `platform.document-repository.read` / `.manage`; Gateway rotası var (`ocelot.json:1857-1885`); pack kapanışı "KISMİ", çalışma zamanı smoke koşulmadı (pack §19) | MdmService'ten hangi kimlikle çağrılacağı **ölçülmedi** (O-13); canlı doğrulama kaydı yokken DL-2 kabul kapısı geçmez |
| MOD-0023 Workflow Designer | onay | pack `ready-for-dev` (`MOD-0023-workflow-config-approval-templates.md:8`); `POST /api/v1/workflow/instances` (`WorkflowDefinitionsController.cs:87@31c448d5d`), `GET instances/by-objects` (`:121`), görev uçları (`:196-277`); aynı `IdempotencyKey` sıralı tekrarda kayıtlı sonucu döner, eşzamanlı yarış **ölçülmedi** (D21/4) | MOD-0217 iş akışı şablonu yok — **O-3, MOD-0023 sahibi (Ali), D38/4**; örnek durum adları ve eşlemesi DL-3 başında ölçülür. Sync'in güncel denemeye bağlanması ve başlatma yanıtı sınıfları MOD-0023 §17 rev 2'ye (803fbaa5…, PR #235: `by-objects` / `history` `idempotencyKey`, `isDeleted`, `approvalTaskId`; `includeDeleted=true`; kapanış kaydı) dayanır — kodu MOD-0023 sahibinin WP'siyle gelir, bugün main'de **ölçülmedi** |
| Audit Trail — MDM izi + MOD-0021 | yazma kaydı | `mdm-guvenilir-kaynak-niyeti` (yol `b`, CT kararı 2026-10-07) ve `mdm-merkezi-iletim` (yol `b`, en iyi çaba) defterde kabul edilmiş (`tests/architecture/audit-ledger/Diten.MdmService.md:10-14`) | **bugün var**; yeni koleksiyonlar için genişletme + defter belirteci ayrı CT kararı (O-6). Kaynağın röle bağdaştırıcısı taşınmaz (paket §D, §L.2) |
| MOD-0220 Legal Entity | yöneten şirket, şirket karşı tarafı | **var, aynı servis** — `OperationalStatus`, `ApprovalStatus` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:49-50`), `IsReferenceable` (`:60`) | süreç içi depo okuması; LE kaydına yazılmaz |
| MOD-0018 RBAC | `mdm.contracts.*` anahtarları | MDM manifest sağlayıcısıyla izin kaydı deseni var (`BrandProductMasterManifestProvider.cs:13`); `mdm.contracts` anahtarı kodda yok (`git grep` 0) | anahtar listesi öneri — O-4 (MOD-0018 sahibi); açık atama listesi Auth değişikliği (D9) |
| SEC-002 veri kapsamı | yöneten şirkete göre görünürlük | MDM'de `ITrustedLegalEntityScopeProvider.ResolveAsync(tenant, subject, moduleCode, permissionKey)` → `LegalEntityIds` (`services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs:3-11`); `mdm.contracts.*` anahtarlarıyla çalıştığı **ölçülmedi** | sağlayıcı ölçülmeden `ready-for-dev` yok |
| RCS-001 ortak kod servisi | sözleşme kodu | **yok** (BL-565, `.antigravity/rules/record-coding-standard.md:163`); §3 tablosunda sözleşme satırı **yok** (`:45-69`); MDM'deki `CodeReservation` ürün ailesinindir | engel: modül kendi sayacını yazamaz (RCS-001 §6); önek O-5; BL-565 sahibi belirsiz |
| MOD-0048 para birimi | kod listesi | `GET /api/lookups/reference/currencies` Platform'da (MOD-0126 / MOD-0289 pack ölçümü — bu oturumda yeniden ölçülmedi); MDM'den tüketim yolu **ölçülmedi** | `ICurrencyReferencePort`; erişilemezse 503, sabit liste yok |
| MOD-0140 Supplier | tedarikçi karşı tarafı | `supplier.openapi.yaml` (MOD-0289 pack ölçümü: 1.2.0, `validateSuppliers`); MOD-0217 için **ölçülmedi** | O-7; karar `Supplier` türünü v1'e alırsa sözleşme ölçümü DL-1 engeli |
| MOD-0144 Contracting & Clause Library | sınır | `ready-for-dev`, kod var (Ownership and Boundaries) | O-1 — `ready-for-dev` engeli |
| MOD-0031 Evidence | kanıt | var (Platform) | v1'de kullanılmaz (O-9 önerisi) |
| MOD-0003 Data Contract Registry | sözleşme kaydı | kayıt satırı `planned / missing` (`module-id-registry.md:61`) | v1'de sahip olunan sözleşme yok |
| Port | servis | MDM 5059 (`AGENTS.md:75`) | yeni servis / port yok (D38/1); kaynak portu 5050 taşınmaz |
| DCP | dilim kapısı | MOD-0217 için DCP numarası **yok**; Treasury paketleri `UNASSIGNED-finance-treasury-intake` taslağını kullanıyor | DCP numarası ve `approved` olmadan `ready-for-dev` yok |

### Bağımlılık sözleşmeleri — kapanış tablosu

Her satır kapanmadan ilgili dilim başlamaz. **Sahte istemciyle geçen test canlı entegrasyon kanıtı değildir:** "sözleşme
testi" sahte istemcili testi, "canlı kanıt" gerçek servisle dev ortamında koşulmuş kaydı (`scripts/run_record.py`) ister.

| Bağımlılık | Sözleşme / sürüm | Sahibi | Hata davranışı (MOD-0217) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|
| MOD-0220 LegalEntity (süreç içi) | `ILegalEntityRepository` (kod) | master-data-management | yok / başka kiracı / silinmiş / `Active` değil → 422 `MDM_CONTRACT_LEGAL_ENTITY_NOT_ELIGIBLE` | birim + entegrasyon · — (aynı süreç) | DL-1 |
| RCS-001 | **yok** (BL-565) | CT + `.antigravity` onayı | servis yoksa Create yazılamaz; erişilemezse 503 | sahte port · — | DL-1 önkoşulu |
| MOD-0048 para birimi | `GET /api/lookups/reference/currencies` | platform-shared-services | erişilemez → 503; listede yok → 400 | sahte istemci · dev ortamında TRY / EUR | DL-1 |
| SEC-002 kapsam sağlayıcısı | `ITrustedLegalEntityScopeProvider` | platform-shared-services | hata → boş kapsam, boş liste, detay / yazma 404 | AC-6 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-1 |
| MDM denetim niyeti (genişletilmiş) | `LocalAuditIntent` + `AuditIntentDeliveryProcessor` (kod) | master-data-management + CT | niyet yazılamazsa komut 503, iş verisi yok | AC-11 (sabotaj) · bir sözleşme oluşturmanın merkezi kaydı (AUD-001 §8) | DL-1 |
| MOD-0262-FU01 | `/api/v1/document-repository/objects` (kod; sözleşme dosyası yok) | platform-shared-services | erişilemez / 5xx / zaman aşımı → 503, işaretçi yok; ret → 400 / 413; işaretçi yazımı kesin geri alındı → `compensate`; sonucu bilinmiyor → önce okuma, telafi yalnız bağsızlık kanıtlanınca; okuma yok → bilinmiyor, telafi yok | AC-24…AC-29, AC-49, AC-50 (sahte istemci) · dev ortamında yükle / indir / başka kiracı 404 | DL-2 |
| MOD-0028 belge kaydı | **TBD (O-2)** | platform-shared-services | erişilemez → 503 `MDM_CONTRACT_DOCUMENT_RECORD_UNAVAILABLE` | O-2 kararından sonra yazılır | DL-2 |
| MOD-0023 | `/api/v1/workflow/instances` + `instances/by-objects` + `instances/{id}/history` (kod) + MOD-0023 §17 rev 2 okuma alanları (803fbaa5…, PR #235) + MOD-0217 şablonu (**yok**, O-3) | platform-shared-services | başlatma: kesin ret → `Draft` + gerekçe kodu, 422; geçici → `InApproval` başlatma bekliyor, 503; bilinmiyor → aynı, 503 + uyarı; Sync yalnız güncel anahtar + örnek kimliğiyle eşleşen tek satırı kabul eder; birden çok / silinmiş / tanınmayan → 503, durum değişmez | AC-33…AC-36, AC-51…AC-55 · dev ortamında onay ve ret turu | DL-3 |

## Runtime Constraints

- Servis `Diten.MdmService`, port 5059; yeni servis ya da port yok (D38/1). Tarayıcı ve `Diten.Web` yalnız Gateway (5000)
  üzerinden çağırır; tarayıcı JS 5059'a gitmez. MdmService'ten Platform'a (MOD-0262, MOD-0023, MOD-0048) çağrılar Gateway
  üzerinden; adres yapılandırmadan gelir, yoksa servis açılmaz. Kaynak portu 5050 ve kaynak Gateway blokları
  (`ocelot.json:1621-1660@da4abb20`, `AuthenticationOptions` yok) taşınmaz.
- **Kiracı izolasyonu:** `TenantId` sunucuda; her sorgu `TenantFilter`'la; başka kiracının sözleşmesi, sürümü, belgesi
  **404**. Üretimde sabit / varsayılan kiracı yok. **Hiçbir belge `TenantId = Guid.Empty` ile yazılmaz**; depo boş kiracıyı
  reddeder. `X-Tenant-Id` başlığı kiracıyı belirlemez (kaynaktaki başlık karşılaştırmalı koruma, `Mod0217VerifiedRequestContext.cs:31-51@da4abb20`, taşınmaz; MDM'nin standart kiracı çözümü kullanılır).
- **Soft delete:** `IsDeleted` + `DeletedAt` (UTC); yalnız sürüm 1'i hiç onaya gönderilmemiş (`ApprovalAttempt = 0`) `Draft`
  sözleşme silinir; silme sözleşmeyi ve sürüm 1'i aynı işlemde işaretler. Belge ikilisi silinmez (MOD-0262 saklama).
- **Eşzamanlılık:** her Update / Submit / Sync / RecordSignature / Amendment / Withdraw / Terminate / Archive `If-Match`
  (sözleşme ya da sürüm `Version`) ve durum CAS'ı taşır; bayat istek 409 `MDM_CONTRACT_VERSION_CONFLICT`; iki eşzamanlı
  geçişten biri 409.

### İdempotency (öneri, O-10 — Recovery testleriyle kanıtlanacak gereksinim)

1. `POST /api/mdm/contracts` `Idempotency-Key` başlığı **ister** (yoksa 400). Anahtar ve normalize gövdenin SHA-256'sı
   sözleşme belgesine yazılır; `ux_mdm_contract_create_idempotency` tekildir.
2. Aynı anahtar + aynı gövde → var olan sözleşme **200** ile döner (yeni kayıt, yeni kod, yeni niyet yok). Aynı anahtar +
   farklı gövde → 409 `MDM_CONTRACT_IDEMPOTENCY_KEY_REUSED`. Eşzamanlı aynı anahtarlı iki istek → tek sözleşme; kaybeden
   tekil indeks çakışmasını yakalar, var olanı okuyup aynı sonucu döner.
3. Bu, kaynağın "Create sonucu bilinmiyor" boşluğunu (paket §L.3) kapatır: istemci zaman aşımında aynı anahtarla yeniden
   dener. Ayrı makbuz koleksiyonu ve makbuz arama ucu yoktur. Anahtar saklama süresi: kayıtla birlikte kalıcıdır (TTL yok).
4. Diğer yazmalar `If-Match` + durum CAS'ı ile tekrar edilemez; MOD-0023 başlatması belirli deneme anahtarıyla tekrar güvenlidir.

### İşlem bütünlüğü (TRM-11 — kaynak güvencesi alıntılanmaz, Recovery gerçek replica set testiyle kanıtlar)

Kaynakta `StartSession` / `WithTransaction` vardı (`Mod0217ContractAggregateStore.cs:153,405@da4abb20`,
`Mod0217CallbackTransactionStore.cs:34-35@da4abb20`) ama işlem testlerinin 18'i koşmadı, 6'sı başarısızdı (TRM-11). Recovery
şu grupları **tek Mongo işleminde** yazar; her biri gerçek replica set'te hata enjeksiyonlu testle kanıtlanır:

| Grup | Belgeler | Test |
|---|---|---|
| Create | `Contract` + `ContractVersion` (v1) + **tam iki** denetim niyeti, aynı işlemde: `Contract.AuditIntents`'e `mdm.contract.created`, `ContractVersion.AuditIntents`'e `mdm.contract-version.created` (başka niyet yok) | AC-4, AC-13 |
| CreateAmendment | `Contract` (`LatestVersionNo`, `OpenVersionNo` CAS) + yeni `ContractVersion` + niyetler | AC-31, AC-39 |
| RecordSignature (son imza) | aynı işlemde önce **bütün imzacıların** `SignedCopyContentId`'si yeniden doğrulanır (her biri bu sürümde `SignedCopy` rolünde, `WithdrawnAt` boş; biri bile tutmazsa işlem iptal ve 422 `MDM_CONTRACT_SIGNED_COPY_REQUIRED`, hiçbir değişiklik kalmaz); sonra sürüm → Effective + önceki Effective → Superseded + `Contract` (Status, `CurrentEffective*`, `OpenVersionNo = null`) + niyetler | AC-37, AC-56 |
| Delete / BulkDelete | `Contract` + sürüm 1 (+ kimlik başına niyet) | AC-8, AC-9 |
| Withdraw (sürüm) | sürüm + `Contract.OpenVersionNo` + niyetler | AC-39 |
| Terminate | `Contract` + açık sürüm (varsa Withdrawn) + niyetler | AC-41 |

`MasterDataAtomicWrite` politikası geçerlidir (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs:20-40@31c448d5d`:
snapshot okuma + majority yazma, geçici hata yeniden denemesi, işlem desteklenmiyorsa Development dışında reddet → 503).
Development'taki sıralı yazma geri dönüşü **kanıt sayılmaz**.

### Uzak çağrı sırası

- **Belge ekleme (MOD-0262-FU01):** (1) sözleşme ve sürüm kapsam / durum kontrolü (Draft; `SignedCopy` için Approved da);
  (2) MOD-0262'ye akışla yükle (`owningItemId = ContractId`, `owningVersionId = ContractVersion.Id`, `companyId =
  GoverningLegalEntityId`, `scope` O-13'te ölçülür); (3) dönen `ContentRef` işaretçisini sürüme koşullu yaz + niyet
  (`Version` CAS; yazım sunucu tarafı süre sınırı taşır); (4) (3)'ün sonucu aşağıdaki üç sınıftan biridir. **Telafi
  (`POST objects/{contentId}/compensate`) yalnız yazımın uygulanmadığı kesinse ya da okunarak kanıtlanınca çağrılır;
  körlemesine telafi yasaktır** — yazım işlenmiş, yalnız yanıt kaybolmuş olabilir; o durumda telafi, sürümün gösterdiği
  dosyayı siler.

  | Sınıf | (3)'te görülen | Yapılan | Yanıt |
  |---|---|---|---|
  | (4a) kesin geri alma — yazım **kesinlikle uygulanmadı** | CAS eşleşmedi (`matchedCount = 0`: bayat sürüm ya da durum değişti); işlem commit çağrılmadan iptal edildi; doğrulama ya da tekil indeks hatası; niyet yazımı reddedildi | telafi tam bir kez | 409 (CAS) / 503 |
  | (4b) sonuç bilinmiyor — yazım işlenmiş olabilir | zaman aşımı; bağlantı sıfırlama / kopma; `UnknownTransactionCommitResult`; yazma onayı (write concern) zaman aşımı ya da hatası; tanınmayan sürücü hatası | **önce mutabakat okuması:** sürüm (TenantId, ContractId, VersionNo) çoğunluk (`majority`) okumasıyla, (3)'ün süre sınırı geçtikten sonra okunur ve belge anahtarı `Documents[].ContentId = contentId` aranır. Bulundu → bağlı: telafi **yok**, işaretçi yerinde, sonuç başarılı. Bulunmadı → bağsızlık kanıtlandı: telafi tam bir kez | bağlı → 201 (aynı `ContentId`, tek niyet); bağsız → 503 |
  | (4c) okuma da yok | (4b)'nin okuması erişilemez, zaman aşımına uğradı ya da çoğunluk okuması verilemedi | telafi **yok**; durum **bilinmiyor**; uygulama günlüğüne yapılandırılmış uyarı (kiracı, sözleşme, sürüm, `contentId`; içerik ve dosya adı yok); nesne yetim sayılmaz, MOD-0262-FU03 mutabakatına verilmez | 503 `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN` + `contentId` |

  **(4c)'nin sonradan çözümü:** `ReconcileContractDocumentCommand` (izin `mdm.contracts.documents.create`, aynı kiracı / kapsam
  / durum denetimi) (4b)'nin okumasını yeniden yapar: bağlı → 200, telafi yok; bağsızlık kanıtlandı **ve** MOD-0262 meta okuması
  nesnenin bu sözleşmenin bu sürümüne ait olduğunu gösterdi (`owningItemId = ContractId`, `owningVersionId = ContractVersion.Id`;
  bu alanların meta yanıtında döndüğü DL-2 başında ölçülür — dönmüyorsa telafi yok, 503) → telafi tam bir kez, 200; okuma
  yine yok → 503 `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN`, telafi yok; nesne başka sözleşmenin / sürümün ya da başka kiracının
  → 404, telafi yok. Komut MDM'ye yazmaz. Kanıtlı telafi de başarısızsa yetim ikili MOD-0262-FU03 mutabakatına kalır ve
  uygulama günlüğüne (içerik olmadan) yazılır. Belge dosyası MdmService'te diske ya da geçici klasöre yazılmaz; istek bedeni
  ara belleğe tümüyle alınmaz.
- **Onaya gönderme (MOD-0023):** (1) tek koşullu yazımda sürüm `Draft → InApproval`, `ApprovalAttempt + 1`, yeni deneme
  anahtarı `WorkflowIdempotencyKey`, `WorkflowInstanceId = null`, `SubmittedAt` / `SubmittedBy` + niyet
  `mdm.contract-version.submitted`; (2) aynı anahtarla `POST /api/v1/workflow/instances`; (3) yanıt aşağıdaki tabloya göre
  işlenir. Başarı (200 / 201): dönen örnek kimliği koşullu yazılır + niyet `mdm.contract-version.workflow-start-confirmed`
  (başlatma doğrulandı). Sürüm `InApproval`'da donmuştur; içerik / anahtar uyuşmazlığı oluşamaz.

  **Başlatma yanıtı sınıfları** (MOD-0023 §17 rev 2 anlamıyla — 803fbaa5…, PR #235; yalnız MOD-0023'ün tanınan hata
  gövdesiyle sınıflanır, tanınmayan gövdeli 4xx — ör. Gateway rota 404'ü — "bilinmiyor"dur):

  | Sınıf | MOD-0023 yanıtı | Sürüm | MOD-0217 yanıtı |
  |---|---|---|---|
  | Kesin ret — başlatma kesinlikle olmadı ya da üreticinin kapanış kaydıyla kapandı | 400 doğrulama; 403 izin ya da SoD; 404 şablon bulunamadı; 409 `WORKFLOW_INSTANCE_START_CONFLICT` **yalnız** güncel anahtarlı örneğin `history`'sinde üreticinin kapanış kaydı (`workflow-start-recovery-close:{instanceId:N}` anahtarlı satır — MOD-0023 AC-W12: hiç bütün olmamış başlatma kapatıldı) okunduğunda | aynı koşullu yazımda `InApproval → Draft` (Submit öncesi hâl), `LastApprovalOutcome = StartRefused`, `LastApprovalReasonCode`, `WorkflowInstanceId = null` + niyet `mdm.contract-version.submission-refused`; `ApprovalAttempt` geri alınmaz (anahtar yandı; sonraki Submit `a{n+1}`) | 422 `MDM_CONTRACT_WORKFLOW_START_REFUSED` + `reasonCode` |
  | Geçici — gönderilmedi ya da işlenmedi | bağlantı kurulamadı ya da gönderimden önce zaman aşımı; 5xx; 503 (MOD-0023'ün ara "aynı anahtarla tekrar" yanıtı dahil); 401 (oturum yenilenir) | `InApproval`, başlatma bekliyor (`WorkflowInstanceId = null`); değişiklik yok | 503 `MDM_CONTRACT_WORKFLOW_UNAVAILABLE` |
  | Bilinmiyor — işlenmiş olabilir | gönderimden **sonra** zaman aşımı ya da bağlantı kopması; 409 `WORKFLOW_TRANSITION_CONFLICT`; 503 `WORKFLOW_TRANSACTION_UNAVAILABLE`; 503 `WORKFLOW_TRANSITION_CONFLICT`; kapanış kaydı okunamayan ya da bulunmayan 409 `WORKFLOW_INSTANCE_START_CONFLICT` (anahtar kötüye kullanımı dahil); tanınmayan gövde | `InApproval`, başlatma bekliyor; değişiklik yok; uyarı günlüğü | 503 `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN` |

  - **Zaman aşımı ve `WORKFLOW_TRANSITION_CONFLICT` hiçbir zaman kesin ret değildir** (MOD-0023 §17.7/6 tüketici kuralı):
    sürüm bunlarda `Draft`'a dönmez — dönseydi sonraki Submit yeni anahtarla ikinci bir örnek açabilirdi.
  - Geçici ve bilinmiyor sınıfı **yalnız** Sync (güncel anahtarla okuma) ya da aynı anahtarla Submit tekrarı ile çözülür; yeni
    anahtar üretilmez. Tekrar yalnız `WorkflowInstanceId` boşken ve tekrardan hemen önceki `instances/by-objects`
    (`includeDeleted=true`) okumasında güncel anahtarlı satır yokken gönderilir; satır varsa tekrar yerine Sync'in eşleme kuralı
    uygulanır; silinmiş satır görüldüyse tekrar gönderilmez.
  - **Yeniden başlatmadan önce yeniden denetim** (aynı anahtarla tekrar ve kesin retten sonraki yeni Submit): çağıranın
    `mdm.contracts.submit-approval` izni ve yöneten şirketteki veri kapsamı (SEC-002); yöneten şirketin uygunluğu (aynı kiracı,
    silinmemiş, `OperationalStatus = Active` — AC-18 kuralı); aktör bağı / SoD: aynı anahtarla tekrarı yalnız `SubmittedBy`
    yapar (MOD-0023 farklı başlatanı anahtar kötüye kullanımı sayar — §17 AC-W11), başkası 409
    `MDM_CONTRACT_SUBMITTER_MISMATCH`; onaylayıcı SoD'si MOD-0023'te yeniden değerlendirilir (403 → kesin ret). Denetimlerden
    biri tutmazsa MOD-0023'e çağrı yapılmaz ve sürüm değişmez.
  - `SubmittedBy` artık tekrar edemiyorsa (ayrıldı, yetkisi alındı) başlatma bekleyen sürüm yalnız Sync ile çözülür; yönetici
    çözüm yolu Follow-up.
- **Onay sonucu (Sync) — güncel denemeye bağlı:** MOD-0023 `GET instances/by-objects` `includeDeleted=true` ile okunur.
  Okuma alanları MOD-0023 §17 rev 2 (803fbaa5…, PR #235): `by-objects` → `idempotencyKey`, `isDeleted`; `history` →
  `approvalTaskId`, `idempotencyKey`, `isDeleted`. Eşleme kuralı:
  1. Aday satırlar yalnız `idempotencyKey = WorkflowIdempotencyKey` (güncel deneme) olanlardır. Önceki denemelerin (reddedilmiş,
     iptal edilmiş, kesin reddedilmiş) ve başka nesnenin örnekleri anahtarı farklı olduğu için **yok sayılır**; nesneye göre
     "en yeni örnek" alınmaz. `idempotencyKey`'i boş dönen satır eşleşmez (alan yoksa okuma durumu değiştirmez).
  2. `WorkflowInstanceId` doluysa adayın kimliği ona da eşit olmalıdır; kimlik ile anahtardan yalnız biri eşitse → bilinmiyor.
  3. Tam bir aday, silinmemiş → durum eşlemesi: terminal onay → `Approved`; terminal ret / iptal → `Draft` +
     `LastApprovalOutcome`; terminal olmayan → durum değişmez, 200. `WorkflowInstanceId` boşsa aynı yazımda örnek kimliği
     yazılır (başlatma doğrulandı, niyet `mdm.contract-version.workflow-start-confirmed`).
  4. Birden çok aday → durum **değiştirilmez**; 503 `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN` + uyarı günlüğü.
  5. Aday silinmiş (`isDeleted = true`) → durum değişmez, bilinmiyor + uyarı; aynı anahtarla tekrar gönderilmez.
  6. Aday yok: `WorkflowInstanceId` boşsa başlatma görünmüyor → değişiklik yok, 200 (aynı anahtarla tekrar açık); doluysa →
     bilinmiyor + uyarı, 503.
  7. Tanınmayan / çelişkili durum → 503 `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN`, değişiklik yok.

  MOD-0023 durum adlarının eşleme tablosu DL-3 başında `WorkflowInstanceStatusDto` okunarak yazılır (bu oturumda
  ölçülmedi). §17 rev 2 okuma alanları main'de değilken Sync hiçbir sürümü `Approved`'a geçirmez (kural 1). Sync'i yalnız
  kullanıcı komutu tetikler (arka plan işi v1'de yok — Follow-up).

### Veri kapsamı (SEC-002) — kapsam cümlesi

- **Kapsamı taşıyan alan:** `Contract.GoverningLegalEntityId`; çevrilen kapsam türü yalnız `LegalEntity` (`OrgUnit` /
  `Position` çevrilmez → daraltır, sızdırmaz); çözümleme tek yerde (`IContractDataScopeSource` — aday MDM'deki
  `ITrustedLegalEntityScopeProvider`). Kapsam dışı şirketin sözleşmeleri listede çıkmaz (200, 0 satır); detay, sürüm, belge
  ve her yazma 404. Çözücü hatası, kiracı ya da kullanıcı yokluğu → boş kapsam.
- **Kiracı geneli izin:** `mdm.contracts.read-tenant-wide` (öneri, O-4; yalnız açık atama, **yalnız okuma**; yazma kapsamını
  genişletmez — her yazma kullanıcının o şirketteki kendi yetkisini ister).
- **Karşı taraf şirket:** `CounterpartyLegalEntityId` kapsam taşımaz (karşı taraf şirketin kullanıcısı sözleşmeyi bu
  yüzden görmez).

### Ticari değer görünürlüğü (öneri, O-12)

`ContractValue` / `CurrencyCode` liste, detay ve sürüm DTO'larında yalnız `mdm.contracts.value.read` sahibine döner;
diğerlerine `null` + `valueMasked: true`. Değer içeren yazma bu izni ister (yoksa 403 `MDM_CONTRACT_VALUE_ACCESS_REQUIRED`).
Değerin okunması denetlenmez (AUD-001 §7'nin üç sınıfına girmez); değer günlüğe ve denetim niyetine yazılmaz.

### Hata sınıflandırması

- **400** — biçim / zorunlu alan / bilinmeyen kod / istekte `tenantId` / istekte `Code` / `Idempotency-Key` yok.
- **422** — tanımlı ama iş kuralına uymayan girdi (şirket aktif değil, karşı taraf uygun değil, imzacı eksik, imzalı kopya yok);
  MOD-0023'ün başlatmayı **kesin** reddi (`MDM_CONTRACT_WORKFLOW_START_REFUSED`, yalnız "Başlatma yanıtı sınıfları"nın kesin ret
  satırı — erişilemezlik ya da bilinmeyen sonuç hiçbir zaman 422 değildir).
- **404** — kayıt yok, silinmiş, başka kiracı, kapsam dışı.
- **409** — durum geçişi geçersiz, bayat `If-Match`, açık sürüm var, değişmez alan, anahtar yeniden kullanımı, imzaya bağlı
  belgeyi geri çekme (`MDM_CONTRACT_DOCUMENT_IN_USE`), başka kullanıcının aynı anahtarla tekrar isteği.
- **503** — bağımlılık erişilemez ya da yanıtı tanınmıyor (RCS-001, MOD-0048, MOD-0262, MOD-0028, MOD-0023, denetim niyeti,
  işlem) ya da işaretçi / başlatma sonucu bilinmiyor. **Bağımlılık hatası hiçbir zaman 400 / 422 olarak dönmez; geçersiz girdi
  hiçbir zaman 503 dönmez** (AC-19, AC-25, AC-34, AC-54).

## Layout & Shell Contract

- `shell: tenant` → `Views/MasterData/Contracts/` altındaki **tüm** `.cshtml` dosyalarında `Layout = "_LayoutTenantShell";`
  **AÇIKÇA** yazılır (canlı örnek `frontend/Diten.Web/Views/MasterData/Brands/Index.cshtml:7@31c448d5d`); `_ViewStart.cshtml`
  varsayılanı değiştirilmez. Kaynak ekranlardaki `Layout = "_LayoutBackbone"` (6 sayfa, paket §I) taşınmaz.
- View klasörü `Views/MasterData/Contracts/` · rota `/MasterData/Contracts` (`frontend/Diten.Web/Controllers/MasterData/BrandsController.cs:29@31c448d5d`
  deseni). MOD-0144'ün `/Contracts` rotası ve `Views/Procurement/Contracts/` klasörü kullanılmaz.
- Nav: modül manifestinden (sayfa kodu `LEGALCONTRACTS`, `RoutePath`, `RequiredPermission = mdm.contracts.read`), anahtar
  `Nav.Page.LEGALCONTRACTS` (`Nav.Page.CONTRACTS` MOD-0144'ündür).
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `mdm.contracts.read` yoksa kabuk içinde `_AccessDenied` partial'ı;
  başlık, kart, boş tablo, düğme çizilmez; yönlendirme yok; 401 tam sayfa.

## Backend File Convention

Golden Reference **Compact** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`:
`Commands/`, `Queries/`, `Handlers/{CommandHandlers,QueryHandlers}`, `Validators/`, tek `…Models.cs`; MDM'de aynı düzen
`Features/Brand/`'de var):

```text
Features/Contract/
  Commands/        CreateContractCommand.cs, UpdateContractCommand.cs, DeleteContractCommand.cs, BulkDeleteContractCommand.cs,
                   UpdateContractVersionCommand.cs, AddContractDocumentCommand.cs, WithdrawContractDocumentCommand.cs,
                   ReconcileContractDocumentCommand.cs, SubmitContractVersionForApprovalCommand.cs, SyncContractVersionApprovalCommand.cs,
                   RecordContractSignatureCommand.cs, CreateContractAmendmentCommand.cs, WithdrawContractVersionCommand.cs,
                   RecordContractRenewalDecisionCommand.cs, TerminateContractCommand.cs, ArchiveContractCommand.cs
  Queries/         GetContractListQuery.cs, GetContractByIdQuery.cs, GetContractVersionListQuery.cs,
                   GetContractVersionByIdQuery.cs, GetContractDocumentContentQuery.cs, ContractListSort.cs
  Handlers/CommandHandlers/   {Verb}Contract{…}Handler.cs (suffix YOK)
  Handlers/QueryHandlers/     Get{…}Handler.cs
  Validators/      CreateContractValidator.cs, UpdateContractValidator.cs, UpdateContractVersionValidator.cs,
                   RecordContractSignatureValidator.cs, CreateContractAmendmentValidator.cs,
                   RecordContractRenewalDecisionValidator.cs, TerminateContractValidator.cs, GetContractListValidator.cs
  Rules/           ContractStateMachine.cs, ContractAllowedActions.cs, ContractTypeCatalog.cs,
                   WorkflowStartOutcomeClassifier.cs (başlatma yanıtı sınıfları), WorkflowAttemptMatcher.cs (Sync eşleme kuralı)
                   (sabit çekirdek kodlar; saf işlevler)
  ContractModels.cs
Infrastructure/DocumentRepository/   DocumentBinaryStoreClient.cs (MOD-0262-FU01)
Infrastructure/DocumentRecords/      DocumentRecordClient.cs (MOD-0028 — O-2)
Infrastructure/Workflow/             ContractWorkflowClient.cs (MOD-0023)
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` — **Command/Query suffix YOK**. Tek
  dosyada tek public tip. Controller `CustomBaseController`'dan türer (`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:13-14@31c448d5d`
  deseni), `[Route("api/mdm/contracts")]`, `Response<T>` döner, her aksiyonda `[HasPermission]`.
- İş kuralı handler + domain'de (`handler-design`); kaynaktaki toplu servis ve mağaza sınıfları alınmaz (D38/3).
- Her yazma komutu denetim niyetini iş belgesiyle **aynı koşullu yazımda ya da aynı Mongo işleminde** taşır (Audited Events).
- Kaynak tip önekleri (`Mod0217*`), kaynak koleksiyon adları (`mod0217_*`) ve kaynak hata literalleri (`MOD0217_*`) kullanılmaz.

## Frontend File Contract

Golden Reference **Compact** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/`):

```text
Views/MasterData/Contracts/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml · Edit.cshtml
├── Details.cshtml            (sekmeler: Sürümler · İmzacılar ve imzalar · Belgeler · Onay · Yenileme kararları)
├── _Form.cshtml · _Filter.cshtml (durum, yöneten şirket, sözleşme türü, karşı taraf türü, iç sorumlu, bitişi yaklaşan [gün])
├── _DataTable.cshtml         (data-dt-standard="v2", data-dt-data-mode="server")
├── _IndexL10n.cshtml
└── ContractsIndex.cs
wwwroot/assets/js/MasterData/Contracts/   index.js · index.l10n.js · form.js · details.js
Resources/Views/MasterData/Contracts/     ContractsIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- DataTable sütunları (kaynak `dt-contract-register` ölçülmüş dizininden uyarlanır, paket §I): Kod, Başlık, Karşı taraf,
  Yöneten şirket, İç sorumlu, Durum, Açık sürüm durumu, Değer (yalnız izinle; aksi hâlde maske simgesi), Bitiş tarihi,
  İşlemler. Kaynaktaki onay / imza / kanıt / risk sütunları ve "Next Action" taşınmaz (tek durum modeli, O-9).
- Detay eylem düğmeleri yalnız sunucunun `allowedActions` listesindekilerdir; istemci durum ya da izin hesaplamaz.
- Create isteğinde `Idempotency-Key`'i **MVC vekili** form başına bir kez üretir ve yeniden gönderimde aynısını kullanır
  (kaynaktaki sayfa oturumu koruması, paket §I, sunucu kuralının yerine geçmez).
- Belge yükleme vekil üzerinden akışla (ara bellek yok); sınır önerisi 50 MB (kaynak web sınırı, paket §K) ve MOD-0262'nin
  yapılandırılmış üst sınırından küçük olanı (MOD-0262 değeri ölçülmedi); yerel geçici klasör yok.
- API profili: same-origin MVC vekil (`/MasterData/Contracts/api/...`, `MasterDataGatewayController` deseni —
  `frontend/Diten.Web/Controllers/MasterData/MasterDataGatewayController.cs:13-21@31c448d5d`: HttpOnly erişim çerezi sunucuda
  okunur, tarayıcı 5059'u ya da token'ı görmez).
- Sözleşme türü, yenileme kararı ve durum etiketleri resx'ten; para birimi `GET /api/lookups/reference/currencies`'ten; şirket
  seçici MOD-0220 okumasından; sabit liste ve hardcoded metin yok. Toplu eylem çubuğunda yalnız taslak toplu silme.

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| Gövde/sorguda `tenantId` | — | bulunursa 400 | — | — |
| Contract.Code | — | istekte bulunursa 400 `MDM_CONTRACT_CODE_FORBIDDEN` (OTOMATİK) | Unique (TenantId, Code), kısmi değil | `IRecordCodePort` |
| `Idempotency-Key` (Create) | Evet | 1..100, `^[A-Za-z0-9._:-]+$` | Unique (TenantId, CreateIdempotencyKey) kısmi | — |
| Title | Evet | trim, 1..200, kontrol karakteri yok | — | — |
| ContractType | Evet | çekirdek kod listesinde (O-11); büyük harf, sıralı karşılaştırma | — | `ContractTypeCatalog` |
| GoverningLegalEntityId | Evet | Guid ≠ Empty; aynı kiracı, `OperationalStatus = Active` (Create); kullanıcının veri kapsamında; ilk Submit'ten sonra değişmez (409) | — | `ILegalEntityRepository`, `IContractDataScopeSource` |
| CounterpartyType + karşı taraf alanı | Evet | türüne göre tam bir alan dolu; LE: aynı kiracı, silinmemiş, yöneten şirketten farklı; Supplier: 1..64, doğrulama O-7; External: ad 1..200; ilk Submit'ten sonra değişmez | — | O-7 |
| InternalOwnerUserId | Evet | Guid ≠ Empty; oluşturan dışındaki değer O-14 doğrulamasına bağlı | — | O-14 |
| EffectiveFrom / EffectiveTo | Evet / Hayır | ISO tarih; `EffectiveTo ≥ EffectiveFrom` | — | — |
| NoticePeriodDays | Hayır | tam sayı 0..3650 | — | — |
| ContractValue / CurrencyCode | Hayır / değer varsa Evet | değer ≥ 0, küsurat MOD-0048 (O-11); `^[A-Z]{3}$`, MOD-0048 listesinde; `mdm.contracts.value.read` ister (403) | — | `ICurrencyReferencePort` |
| Summary | Hayır | ≤ 2000 | — | — |
| AmendmentReason | Amendment'ta Evet | 1..500 | — | — |
| Signatories[] | Submit'te Evet | en az bir `Internal` + bir `Counterparty` (422); `Name` 1..140; `RoleTitle` ≤ 100; en çok 20 imzacı (öneri) | — | — |
| RecordSignature | Evet | sürüm `Approved`; imzacı bu sürümde ve imzasız; `SignedAt` ≤ şimdi ve ≥ `SubmittedAt`; `SignedCopyContentId` bu sürümde `SignedCopy` rolünde, geri çekilmemiş belge (422 `MDM_CONTRACT_SIGNED_COPY_REQUIRED`) — bu koşul ön kontrolle değil, imzayı yazan koşullu yazımın / işlemin süzgecinde (`Version` CAS + belgenin `WithdrawnAt` boş olması) denetlenir; son imzada bütün imzaların belge başvurusu aynı işlemde yeniden doğrulanır | CAS süzgeci | — |
| WithdrawContractDocument | Evet | belge bu sürümde ve geri çekilmemiş; hiçbir imzacının `SignedCopyContentId`'si bu belgeyi göstermiyor (gösteriyorsa 409 `MDM_CONTRACT_DOCUMENT_IN_USE`) — bu koşul geri çekmeyi yazan koşullu yazımın süzgecindedir (`Version` CAS ile birlikte); imzaya bağlı belgenin yerine yenisini koymak da aynı kodla 409 (değiştirme ucu yok) | CAS süzgeci | — |
| Document (yükleme) | Evet | `DocumentRole` tanımlı; dosya boş değil; boyut ≤ sınır (413); tür / uzantı MOD-0262 izin listesi (400) | — | MOD-0262 |
| RenewalDecision | Evet | `Decision` O-11 listesinde; `Note` ≤ 500; sözleşme `Active` ve `CurrentEffectiveTo` dolu | — | — |
| Terminate | Evet | `TerminationReasonCode` 1..64; `TerminationEffectiveDate` ≥ yürürlükteki sürümün `EffectiveFrom`'u | — | — |
| `expiringWithinDays` (liste) | Hayır | 1..180, varsayılan 30 (paket §K) | — | — |
| `pageSize` (liste) | Hayır | 1..200, varsayılan 20 (paket §K) | — | — |
| `If-Match` | durum değiştiren her komutta Evet | sözleşme ya da sürüm `Version` | CAS süzgeci | — |

**Durum kuralları:** UpdateContract — `Draft` ve `ApprovalAttempt = 0` iken bütün başlık alanları; diğer durumlarda yalnız
`Title` ve `InternalOwnerUserId` (Terminated / Archived'da hiçbiri). UpdateContractVersion — yalnız `Draft` sürüm.
AddContractDocument — `Draft` sürümde her rol; `Approved` sürümde yalnız `SignedCopy`; diğer durumlarda 409.
WithdrawContractDocument — `Draft` sürümde her rol; `Approved` sürümde yalnız imzaya bağlı olmayan `SignedCopy`; diğer
durumlarda 409.
CreateAmendment — sözleşme `Active`, açık sürüm yok (409 `MDM_CONTRACT_OPEN_VERSION_EXISTS`). Delete / BulkDelete — yalnız
`Draft` + `ApprovalAttempt = 0`; BulkDelete'te kurala uymayan tek kimlik bütün isteği 409 yapar (kısmi silme yok). Archive —
Terminated; ya da Active + `CurrentEffectiveTo` < bugün; ya da Draft + açık sürüm `InApproval` değil.

## Failure Path to Verify

- **Yinelenen oluşturma / idempotency:** aynı `Idempotency-Key` + aynı gövde → 200, tek kayıt; farklı gövde → 409
  `MDM_CONTRACT_IDEMPOTENCY_KEY_REUSED`; eşzamanlı iki aynı anahtar → tek sözleşme, tek kod, bir Create'in iki niyeti
  (`mdm.contract.created` + `mdm.contract-version.created`). Anahtar yok → 400.
- **Yinelenen sürüm numarası** (iki eşzamanlı CreateAmendment) → biri 409; `ux_mdm_contract_version_no` kaldırıldığında yarış
  testi kırmızı.
- **Eksik alan** (Title, ContractType, şirket, karşı taraf, EffectiveFrom) → 400 + alan düzeyi resx iletisi; kayıt yok.
- **Başka kiracının kaydı** (okuma, güncelleme, silme, sürüm, belge, imza, onay, fesih, arşiv) → 404, varlık sızdırılmaz.
- **Kapsam dışı şirketin sözleşmesi** → liste boş, detay / yazma 404; çözücü hatası → aynı; yalnız `read-tenant-wide` ile
  görünen sözleşmeye yazma → 404, okuma 200.
- **Yetkisiz aktör** → API 403; UI kabuk içinde erişim açıklaması (UAS-001). Değer izni olmadan değer yazma → 403.
- **Eşzamanlılık:** bayat `If-Match` → 409 `MDM_CONTRACT_VERSION_CONFLICT`; iki eşzamanlı Submit → biri 409, tek MOD-0023
  örneği; iki eşzamanlı son imza → biri 409, tek Effective sürüm.
- **Bağımlılık erişilemez vs geçersiz girdi:** RCS-001 / MOD-0048 / MOD-0262 / MOD-0028 / MOD-0023 erişilemez, 5xx, zaman aşımı,
  tanınmayan gövde → 503, kayıt değişmez; para birimi listede yok → 400; şirket aktif değil → 422; MOD-0262 dosyayı reddetti →
  400 / 413 (503 değil).
- **Belge işaretçisi kesin yazılamadı** (CAS eşleşmedi / işlem iptal) → `compensate` tam bir kez, sürümde işaretçi yok,
  409 / 503; telafi de başarısız → yine 503, günlük kaydı (içerik yok).
- **İşaretçi yazıldı, yanıt kayboldu** (yazım işlendi, sonra zaman aşımı) → mutabakat okuması işaretçiyi bulur; telafi **yok**,
  işaretçi yerinde, 201. **Mutabakat okuması da yok** → telafi yok, 503 `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN`; sonra
  Reconcile çözer.
- **Onaya gönderme MOD-0023 düşükken** (geçici) → 503; sürüm `InApproval`, örnek kimliği boş; yeniden Submit aynı anahtarla tek
  örnek açar; Sync örneği bulur. **Gönderimden sonra zaman aşımı / 409 `WORKFLOW_TRANSITION_CONFLICT`** → sürüm `Draft`'a
  dönmez (bilinmiyor). **Kesin ret** (400 / 403 / 404 / kapanış kayıtlı 409) → `Draft` + gerekçe kodu + niyet, 422.
- **Eski deneme / başka nesne / birden çok eşleşme** → Sync durumu değiştirmez (eski reddin sonucu yeni denemeyi `Draft`'a
  çekmez; başka nesnenin onayı bu sürümü `Approved` yapmaz).
- **MOD-0023 tanınmayan durum** → Sync 503, sürüm değişmez.
- **İmzada kullanılan belgeyi geri çekme** → 409 `MDM_CONTRACT_DOCUMENT_IN_USE`; geri çekme ile imza yarışında biri kazanır,
  hiçbir imza geri çekilmiş belgeyi göstermez.
- **Onaylanmamış sürüme imza** → 409; **imzalı kopya olmadan imza** → 422; **imzalı sürümün içeriğini değiştirme** → 409
  `MDM_CONTRACT_IMMUTABLE_FIELD`.
- **Açık sürüm varken değişiklik** → 409 `MDM_CONTRACT_OPEN_VERSION_EXISTS`.
- **Onaya gönderilmiş sözleşmeyi silme** → 409 `MDM_CONTRACT_STATE_INVALID`.
- **Denetim niyeti yazılamıyor** (zorla hata) → komut 503, iş verisi yazılmaz.
- **İşlem desteği yok** (Development dışı) → 503 `MDM_CONTRACT_TRANSACTION_UNAVAILABLE`, kısmi yazma yok.
- **İşlem grubunun ikinci yazımı başarısız** (zorla hata) → hiçbir belge yazılmamış (AC-13, AC-37).

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (MDM deseni
  `LegalEntitiesController.cs:25-97@31c448d5d`). Kaynaktaki eylem içi `HasPermission` (SuperAdmin rolü ya da `permission`
  talebi, `ContractsController.cs:355,370@da4abb20`) ve `[HasPermission]`'sız `lifecycle-preflight` taşınmaz.
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça; ad alanı `mdm.*`; son parça sözlükten (Tier 1 / Tier 2) ya da bu pakette
  kaydedilen Tier 3 eylemi. Kaynak 12 `contract.*` anahtarı ve 4 değer takma adı (`Mod0217Permissions.cs@da4abb20`, paket §E)
  **kopyalanmaz**; hiçbiri Recovery'de yayımlanmadığı için takma ad gerekmez.
- **Aşağıdaki anahtar listesi önerisidir, karar değildir (O-4, MOD-0018 sahibi + K1).** Uydurulmuş bir "var olan" anahtar yoktur:
  `git grep "mdm.contracts"` → 0 (ölçüldü).
- Aktör: `tenant_user`. Arka plan aktörü yoktur (v1'de iş yok).

| Önerilen anahtar | Katman | Uçlar | Kaynak karşılığı |
|---|---|---|---|
| `mdm.contracts.read` | T1 | liste, detay, sürüm liste / detay, `expiringWithinDays` | `contract.view`, `contract.view_dashboard` |
| `mdm.contracts.read-tenant-wide` | T3 (yalnız açık atama) | kiracıdaki tüm şirketlerin sözleşmeleri — **yalnız okuma** | — |
| `mdm.contracts.create` | T1 | Create | `contract.create` |
| `mdm.contracts.update` | T1 | UpdateContract, UpdateContractVersion, WithdrawContractVersion | `contract.edit` |
| `mdm.contracts.delete` / `mdm.contracts.bulk-delete` | T1 / T2 | taslak silme | — |
| `mdm.contracts.value.read` | T1 (alt kaynak `value`) | ticari değeri görme ve yazma | 4 takma ad (`contract.value.view` …) |
| `mdm.contracts.documents.read` / `mdm.contracts.documents.create` / `mdm.contracts.documents.delete` | T1 | belge içerik okuma / ekleme (+ bağ mutabakatı `reconcile`) / geri çekme | — |
| `mdm.contracts.submit-approval` | T3 | Submit, Sync | `contract.submit_review`, `contract.submit_approval` |
| `mdm.contracts.record-signature` | T3 | RecordSignature | `contract.record_signature` |
| `mdm.contracts.amendments.create` | T1 (alt kaynak) | CreateAmendment | `contract.amend` |
| `mdm.contracts.renewal-decisions.create` | T1 (alt kaynak) | RecordRenewalDecision | `contract.record_renewal` |
| `mdm.contracts.terminate` | T3 | Terminate | `contract.terminate` |
| `mdm.contracts.archive` | T2 | Archive | `contract.archive` |

- Taşınmayan kaynak anahtarlar: `contract.send_signature` (e-imza yok, O-8).
- Onay **kararı** bu modülde yetkilendirilmez: onaylayıcı MOD-0023 görevinde karar verir (`platform.workflow.*` izinleri MOD-0023'ün).
  Başlatanın kendi örneğini onaylaması MOD-0023'te `SodViolation` ile reddedilir (MOD-0126 pack ölçümü, D21) — bu paket
  ayrıca onay ucu açmaz.
- MdmService'in MOD-0262 / MOD-0023 çağrılarında kullanıcının `platform.document-repository.*` ve `platform.workflow.*`
  izinlerine ihtiyaç olup olmadığı çağrı kimliğine bağlıdır (O-13).

## Audited Events

**Yol `b` — `mdm-guvenilir-kaynak-niyeti` izinin genişletilmesi (öneri; defter belirteci ve K2 sınıfı CT kararı — O-6).**
Ölçüldü (@31c448d5d): defterde `mdm-guvenilir-kaynak-niyeti` yol `b`, CT kararı 2026-10-07 — niyet iş belgesiyle aynı koşullu
yazımda ya da aynı Mongo işleminde yazılır, `AuditIntentDeliveryProcessor` Platform'un güvenilir kaynak ucuna iletir
(`tests/architecture/audit-ledger/Diten.MdmService.md:14,20`); niyet yazılamazsa iş verisi de yazılmaz → kapalı-başarısız.
`mdm-merkezi-iletim` en iyi çabadır, kullanılmaz. Bu yol bugün yalnız ürün ailesini taşıyor (`AuditAggregateType` dokuz değer,
teslim deposu dokuz koleksiyon); genişletme yalnız eklemedir ama defter belirteci değişir → **CT kararı gerekli (O-6)**; bu paket
deftere satır yazıldığını iddia etmez. Kaynağın `IMod0217AuditRelay` bağdaştırıcısı hiçbir komuttan çağrılmıyordu ve
`Unavailable*` varsayılanları uygulama değildir (paket §D, §L.2) — taşınmaz, uyum sayılmaz.

`SourceModule`: `MOD-0217`. `Category`: Platform `AuditCategory`'de sözleşme / hukuk değeri ölçülmedi — değer uydurulmaz
(O-6). **K2 önerisi (O-6):** onay sonucunun yazılması, imza kaydı, fesih ve değişikliğin yürürlüğe girmesi hukuken bağlayıcı
kayıttır; AUD-001 §4.3 listesinde açıkça sayılmaz, MOD-0289 D24/2 deseniyle K2 sayılması önerilir. Diğer yazmalar aynı yoldan
yazıldığı için pratikte kapalı-başarısızdır.

| Komut | Olay adı | Nesne | Yol | Önceki/sonraki ya da değişen alanlar | K2 (öneri) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreateContractCommand | mdm.contract.created (Contract'ta) + mdm.contract-version.created (sürüm 1'de) — tam iki niyet, aynı işlemde | Contract, ContractVersion | b | AfterState: Code, ContractType, GoverningLegalEntityId, CounterpartyType, Status; sürüm: VersionNo, Status (**değer ve tutar yok**) | hayır | — |
| UpdateContractCommand | mdm.contract.updated | Contract | b | değişen alan **adları** | hayır | — |
| DeleteContractCommand | mdm.contract.deleted | Contract | b | BeforeState: Code, Status | hayır | — |
| BulkDeleteContractCommand | mdm.contract.deleted (kimlik başına bir niyet) | Contract | b | aynı | hayır | — |
| UpdateContractVersionCommand | mdm.contract-version.updated | ContractVersion | b | değişen alan **adları** (tutar yok) | hayır | — |
| AddContractDocumentCommand | mdm.contract-version.document-added | ContractVersion | b | ContentId, DocumentRole, MediaType, ByteSize, Checksum (dosya adı ve içerik yok) | hayır | — |
| WithdrawContractDocumentCommand | mdm.contract-version.document-withdrawn | ContractVersion | b | ContentId, DocumentRole | hayır | — |
| ReconcileContractDocumentCommand | — (MDM'ye yazmaz) | ContractVersion (yalnız okuma) | — | — | hayır | MDM'de kalıcı değişiklik yok → niyet yok (Sync'in durum değişmeyen yoluyla aynı gerekçe: komut yazmıyor); kanıtlı telafi MOD-0262'nin kendi yazmasıdır ve onun denetim yolundadır |
| SubmitContractVersionForApprovalCommand | (1) mdm.contract-version.submitted; (3) başarıda mdm.contract-version.workflow-start-confirmed; kesin retde mdm.contract-version.submission-refused | ContractVersion | b | submitted: Status önce→sonra, ApprovalAttempt, WorkflowIdempotencyKey; start-confirmed: WorkflowInstanceId, ApprovalAttempt; submission-refused: Status önce→sonra, ApprovalAttempt, LastApprovalReasonCode | hayır | geçici / bilinmiyor sınıfında (3) yazmaz → ek niyet yok |
| SyncContractVersionApprovalCommand | mdm.contract-version.approval-recorded (yalnız durum değiştiğinde); örnek kimliğini ilk kez yazdığında mdm.contract-version.workflow-start-confirmed | ContractVersion | b | Status önce→sonra, LastApprovalOutcome, WorkflowInstanceId, ApprovalAttempt | **evet (O-6)** | durum ve örnek kimliği değişmeyen Sync yazmaz → niyet yok (kalıcı değişiklik yok; İ5 değil, komut yazmıyor) |
| RecordContractSignatureCommand | mdm.contract-version.signature-recorded (+ son imzada mdm.contract-version.effective, mdm.contract.activated / superseded) | ContractVersion, Contract | b | SignatoryId, Side, SignedAt, SignedCopyContentId; Status önce→sonra (imzacı adı yok) | **evet (O-6)** | — |
| CreateContractAmendmentCommand | mdm.contract-version.created (Kind = Amendment) | ContractVersion, Contract | b | VersionNo, AmendmentReason **var/yok**, OpenVersionNo | hayır | — |
| WithdrawContractVersionCommand | mdm.contract-version.withdrawn | ContractVersion | b | Status önce→sonra | hayır | — |
| RecordContractRenewalDecisionCommand | mdm.contract.renewal-decision-recorded | Contract | b | Decision, ForVersionNo (not metni yok) | hayır | — |
| TerminateContractCommand | mdm.contract.terminated | Contract | b | Status, TerminationReasonCode, TerminationEffectiveDate | **evet (O-6)** | — |
| ArchiveContractCommand | mdm.contract.archived | Contract | b | Status önce→sonra | hayır | — |

- **Kayda yazılmayanlar:** ticari değer ve para birimi tutarı, sözleşme metni, belge içeriği ve dosya adı, imzacı adı ve
  unvanı, yenileme notu, değişiklik gerekçesi metni (yalnız var/yok).
- **Okuma denetimi:** liste, detay, belge içeriği ve değer okuması denetlenmez (AUD-001 §7'nin üç sınıfına girmez). Sahip
  sözleşme belgesini "gizli kayıt" sayarsa (O-9 gizlilik düzeyi) okuma satırı eklenir.
- **Bilinen boşluk:** reddedilen komut merkezi kayıt bırakmaz (MOD-0126 G-AUD-1 / MOD-0289 ile aynı sınıf); merkezi ret
  kaydı Follow-up.
- **Arka plan işi:** yok.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/mdm/{everything}` catch-all yok; MDM rotaları kaynak başına
  açık yazılmış (ör. `/api/mdm/brands`, `/api/mdm/brands/{everything}` — `gateway/Diten.ApiGateway/ocelot.json:3781,3798@31c448d5d`).
  Gerekli rotalar: `/api/mdm/contracts`, `/api/mdm/contracts/{everything}` → `localhost:5059`; `UpstreamHttpMethod` GET,
  POST, PUT, DELETE, OPTIONS; açık rotalar catch-all'dan önce (`routes.md`). Belge yükleme / indirme alt yolları için
  multipart ve akış geçişi doğrulanır (MOD-0262-FU01 pack §15 uyarısı).
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür.
- **Dokunulmaz:** MOD-0144'ün `/api/contracts` ve `/api/contracts/{everything}` → 5065 rotaları (`ocelot.json:850,870`).
- **Taşınmaz:** kaynak `/api/v1/contracts` ve `/api/v1/contracts/{everything}` → 5050 (`ocelot.json:1621-1660@da4abb20`, paket §C),
  kaynak Platform alıcısı `api/internal/audit/mod0217/r1` (`Mod0217AuditRelayController.cs:13-14@da4abb20`).
- MdmService → Platform (`/api/v1/document-repository/*`, `/api/v1/workflow/*`, para birimi) rotaları Gateway'de var
  (`ocelot.json:1857-1885` belge deposu ölçüldü; iş akışı ve para birimi rotaları bu oturumda ölçülmedi — DL-3 / DL-1 başında).

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir, pack'te
asla izlenmez; bu pack'teki her AC yalnız **"tanımlı"**dır. "Gerçek replica set" = işlem ve yazma çakışması olan, suite'in
kendi Mongo URI'siyle açılan küme (Test Expectations).

- [ ] AC-1 (zorunlu) — `POST /api/mdm/contracts` geçerli istek + `Idempotency-Key` ile 201 döner; sözleşme `Draft`, sürüm 1
  `Draft`; `Code` RCS-001 biçiminde ortak servisten (istekte gönderilen kod 400); `TenantId` sunucuda yazılı, yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — Aynı `Idempotency-Key` + aynı gövdeyle ikinci istek 200 ve aynı sözleşme kimliği / kodu döner; kayıt
  sayısı, kod sayacı ve denetim niyeti sayısı değişmez. Anahtarsız istek 400.
- [ ] AC-3 (zorunlu) — Aynı anahtar + farklı gövde 409 `MDM_CONTRACT_IDEMPOTENCY_KEY_REUSED`; ilk kayıt değişmez.
- [ ] AC-4 (zorunlu) — Gerçek replica set'te aynı anahtarlı 20 eşzamanlı Create → tek sözleşme, tek sürüm 1, tek kod ve tam
  iki denetim niyeti (tek Create işleminde: sözleşmede `mdm.contract.created`, sürüm 1'de `mdm.contract-version.created`);
  hepsi aynı kimliği döner. `ux_mdm_contract_create_idempotency` kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-5 (zorunlu) — Başka kiracının sözleşmesi, sürümü ve belgesi için okuma, güncelleme, silme, belge ekleme / indirme,
  Submit, Sync, imza, değişiklik, yenileme, fesih ve arşiv 404 döner. Depo `TenantFilter`'ı kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-6 (zorunlu) — Veri kapsamı (SEC-002 kanıt testi): A şirketi kapsamlı kullanıcı B şirketinin sözleşmesini listede
  görmez, detayı / sürümü / belgesi / her yazması 404; kapsamı olmayan üçüncü kullanıcı hiçbir sözleşme görmez; çözücü hata
  verirse liste boş. Kapsam daraltması kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-7 (zorunlu) — `mdm.contracts.read-tenant-wide` sahibi bütün şirketlerin sözleşmelerini görür ama kapsamı dışındaki
  sözleşmeye her yazma 404 döner.
- [ ] AC-8 (zorunlu) — Soft delete yalnız `Draft` + `ApprovalAttempt = 0` sözleşmede; sözleşme ve sürüm 1 aynı işlemde
  `IsDeleted` + `DeletedAt` (UTC) alır; silinen kayıt listede görünmez, kodu yeniden kullanılmaz; onaya gönderilmiş
  sözleşmede silme 409.
- [ ] AC-9 (zorunlu) — BulkDelete'te tek uygun olmayan kimlik bütün isteği 409 yapar ve hiçbir kayıt silinmez; uygun listede
  kimlik başına bir niyet yazılır.
- [ ] AC-10 (zorunlu) — Bayat `If-Match` 409 `MDM_CONTRACT_VERSION_CONFLICT`; gerçek replica set'te iki eşzamanlı UpdateContract'tan
  biri 409, kaybeden değişiklik belgede yok. Süzgeçten sürüm koşulu kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-11 (zorunlu) — Her yazma komutu Audited Events tablosundaki niyet(ler)i iş belgesiyle aynı koşullu yazımda / işlemde
  bırakır; niyet yazımı zorla başarısız edildiğinde komut 503 döner ve iş verisi yazılmaz (sabotaj testi);
  `AuditIntentDeliveryProcessor` `mdm_contracts` ve `mdm_contract_versions` niyetlerini teslim eder (koleksiyon teslim deposu
  listesinden çıkarıldığında teslim testi kırmızıya döner).
- [ ] AC-12 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard` yeşil;
  `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş (borç satırı yok); defter değişikliğinin
  CT-DECISIONS satırı aynı değişiklikte (O-6 kararıyla).
- [ ] AC-13 (zorunlu) — İşlem bütünlüğü (Create): gerçek replica set'te sürüm 1 yazımı zorla başarısız edildiğinde sözleşme
  belgesi, sürüm ve iki niyetten hiçbiri yazılmamıştır; yeniden deneme aynı `Idempotency-Key` ile başarılı olur. İşlem sarmalayıcısı
  kaldırılıp sıralı yazıma çevrildiğinde yetim sözleşme bulunur ve test kırmızıya döner (sabotaj).
- [ ] AC-14 (zorunlu) — İşlem desteklemeyen sunucuda (Development dışı yapılandırma) her yazma 503
  `MDM_CONTRACT_TRANSACTION_UNAVAILABLE` döner ve hiçbir belge yazılmaz.
- [ ] AC-15 (zorunlu) — Gövdede ya da sorguda `tenantId` 400; `X-Tenant-Id` başlığı token kiracısından farklı olsa bile işlem
  token kiracısında kalır ya da reddedilir — başlıktaki kiracıya hiçbir şey yazılmaz.
- [ ] AC-16 (zorunlu) — Mimari test: `ContractsController`'ın her aksiyonunda `[HasPermission]` var ve anahtar `mdm.contracts.`
  ile başlar; aksiyon içi izin kontrolü yok. Bir aksiyondan öznitelik kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-17 (zorunlu) — Validation Rules tablosundaki her alan için geçersiz örnek 400 + alan düzeyi resx anahtarı döner;
  kayıt yazılmaz.
- [ ] AC-18 (zorunlu) — Yöneten şirket yok / silinmiş / başka kiracıda / `Active` değil → 422
  `MDM_CONTRACT_LEGAL_ENTITY_NOT_ELIGIBLE`; ilk Submit'ten sonra şirket ya da karşı taraf değişikliği 409.
- [ ] AC-19 (zorunlu) — MOD-0048 erişilemez (sahte istemci 500 / zaman aşımı / geçersiz gövde) → 503
  `MDM_CONTRACT_CURRENCY_REFERENCE_UNAVAILABLE`; listede olmayan kod → 400 `MDM_CONTRACT_CURRENCY_UNKNOWN`. İki durumun
  sınıflandırması yer değiştirildiğinde test kırmızıya döner (sabotaj).
- [ ] AC-20 (zorunlu) — RCS-001 portu erişilemezse Create 503 `MDM_CONTRACT_RECORD_CODE_UNAVAILABLE`, kayıt yok; eşzamanlı iki
  Create farklı kod alır.
- [ ] AC-21 (zorunlu) — `mdm.contracts.value.read` olmayan kullanıcıya liste, detay ve sürüm yanıtlarında `contractValue` ve
  `currencyCode` `null`, `valueMasked = true`; değer içeren yazma 403 `MDM_CONTRACT_VALUE_ACCESS_REQUIRED`; denetim niyetinde
  ve uygulama günlüğünde değer yok. Maskeleme kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-22 (zorunlu) — Ham belge taraması: testlerin yazdığı `mdm_contracts` ve `mdm_contract_versions` belgelerinde
  `TenantId = Guid.Empty` yok; depo boş kiracıyla yazmayı reddeder.
- [ ] AC-23 (zorunlu) — Test altyapısı: MOD-0217 entegrasyon suite'i kendi Mongo URI değişkenini ister; değişken yoksa testler
  **başarısız** olur (atlanmaz, varsayılan porta düşmez); her test sınıfı kendi benzersiz veritabanını açar; indeks değiştiren
  testler kendi veritabanında çalışır (TRM-11 bulguları).
- [ ] AC-24 (zorunlu) — Belge ekleme (DL-2): `Draft` sürüme yükleme MOD-0262-FU01'e akışla gider (`owningItemId`,
  `owningVersionId`, `companyId` doğru), dönen `ContentId` / `Checksum` / `ByteSize` sürüme ve niyete yazılır; MdmService diske ya
  da geçici klasöre yazmaz (mimari test: `Path.GetTempPath` / `File.Create` kullanımı yok).
- [ ] AC-25 (zorunlu) — MOD-0262 erişilemez / 5xx / zaman aşımı → 503 `MDM_CONTRACT_DOCUMENT_STORE_UNAVAILABLE`, işaretçi yok;
  MOD-0262 ret (tür / boyut) → 400 / 413, 503 değil.
- [ ] AC-26 (zorunlu) — **Kesin geri alma:** işaretçi yazımı kesinlikle uygulanmadan başarısız edildiğinde (CAS eşleşmedi; işlem
  commit'ten önce iptal) `compensate` tam bir kez çağrılır ve sürümde işaretçi yoktur; telafi çağrısı kaldırıldığında test
  kırmızıya döner (sabotaj).
- [ ] AC-27 (zorunlu) — Belge içerik okuma önce sözleşme kapsamını ve `mdm.contracts.documents.read`'i denetler: kapsam dışı
  sözleşmenin belgesi 404 ve MOD-0262'ye çağrı yapılmaz; başka kiracının `ContentId`'si 404. Kapsam denetimi kaldırıldığında
  test kırmızıya döner (sabotaj).
- [ ] AC-28 (zorunlu) — `Approved` sürüme yalnız `SignedCopy` eklenir; `Effective` / `Superseded` sürüme her belge ekleme ve
  geri çekme 409.
- [ ] AC-29 (zorunlu) — MOD-0028 belge kaydı bağlantısı O-2 kararındaki yüzeyle yapılır ve o yüzeyin sözleşme testi geçer;
  MOD-0028 erişilemezse 503 `MDM_CONTRACT_DOCUMENT_RECORD_UNAVAILABLE` ve işaretçi yazılmaz. (O-2 karara bağlanmadan bu AC
  uygulanamaz; DL-2 başlamaz.)
- [ ] AC-30 (zorunlu) — Sürüm değişmezliği: `InApproval`, `Approved`, `Effective`, `Superseded` sürümde UpdateContractVersion
  409 `MDM_CONTRACT_IMMUTABLE_FIELD`; belge sürümü değişmez.
- [ ] AC-31 (zorunlu) — Gerçek replica set'te iki eşzamanlı CreateAmendment → biri 201, diğeri 409; tek açık sürüm, ardışık
  `VersionNo`. `ux_mdm_contract_version_no` ya da `OpenVersionNo` CAS'ı kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-32 (zorunlu) — Liste `expiringWithinDays` süzgeci yalnız `Active` ve `CurrentEffectiveTo` bugün ile bugün+n arasında
  olan sözleşmeleri döner; varsayılan 30, 181 → 400; `pageSize` 201 → 400.
- [ ] AC-33 (zorunlu) — Submit (DL-3): sürüm `InApproval`, `ApprovalAttempt` +1, MOD-0023'e `WorkflowIdempotencyKey` ile tek
  başlatma; dönen örnek kimliği yazılır. Gerçek replica set'te iki eşzamanlı Submit → biri 409, sahte MOD-0023'te tek başlatma
  çağrısı (aynı anahtarla).
- [ ] AC-34 (zorunlu) — MOD-0023 başlatma erişilemez → 503 `MDM_CONTRACT_WORKFLOW_UNAVAILABLE`; sürüm `InApproval` + örnek
  kimliği boş; yeniden Submit aynı anahtarla başlatır ve tek örnek oluşur; geçersiz girdi hiçbir zaman 503 dönmez.
- [ ] AC-35 (zorunlu) — Sync eşleme tablosu (DL-3 başında yazılır): terminal onay → `Approved`; ret / iptal → `Draft` +
  `LastApprovalOutcome`; terminal olmayan → değişiklik yok; tanınmayan / çelişkili → 503 `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN`,
  değişiklik yok. Tanınmayan durumu "onay" sayan eşleme yazıldığında test kırmızıya döner (sabotaj).
- [ ] AC-36 (zorunlu) — Mimari test: MOD-0217'de onay kararı veren (approve / reject) ve geri çağrı alan uç yoktur; sürüm
  `Approved`'a yalnız Sync ile geçer.
- [ ] AC-37 (zorunlu) — İmza ve yürürlük: `Approved` sürümde imzalı kopya olmadan imza 422; her imzacı bir kez; son imza tek
  işlemde sürümü `Effective`, önceki Effective sürümü `Superseded`, sözleşmeyi `Active` yapar ve `CurrentEffective*`'i yazar.
  Gerçek replica set'te ikinci belge yazımı zorla başarısız edildiğinde hiçbir değişiklik kalmaz; iki eşzamanlı son imzadan biri
  409, tek Effective sürüm (sabotaj: işlem kaldırıldığında iki Effective ya da yetim durum bulunur).
- [ ] AC-38 (zorunlu) — `Draft` ya da `InApproval` sürüme imza 409; `SignedAt` gelecekte ya da `SubmittedAt`'tan önce 400.
- [ ] AC-39 (zorunlu) — Değişiklik akışı: `Active` sözleşmede CreateAmendment yeni `Draft` sürüm açar (Kind = Amendment,
  gerekçe zorunlu); Withdraw sürümü `Withdrawn` yapar ve `OpenVersionNo`'yu temizler; değişiklik yürürlüğe girene kadar
  yürürlükteki sürüm ve `CurrentEffectiveTo` değişmez.
- [ ] AC-40 (zorunlu) — Yenileme kararı yalnız `Active` + `CurrentEffectiveTo` dolu sözleşmede kaydedilir, listeye eklenir,
  önceki kararlar değişmez; diğer durumlarda 409.
- [ ] AC-41 (zorunlu) — Fesih yalnız `Active` sözleşmede; açık sürüm varsa aynı işlemde `Withdrawn`; Archive yalnız Durumlar
  tablosundaki koşullarda; `Archived` sözleşmede her yazma 409.
- [ ] AC-42 (zorunlu) — Detay DTO'sundaki `allowedActions` yalnız kullanıcının izni ve kaydın durumu birlikte izin verdiği
  eylemleri listeler; listede olmayan eylemin ucu çağrıldığında da sunucu reddeder (403 / 409).
- [ ] AC-43 (zorunlu) — Ekran (DL-4): `Views/MasterData/Contracts/*.cshtml`'de `Layout = "_LayoutTenantShell"` açık;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area MasterData --module Contracts --format gaps` sapma vermez ve
  `data-dt-data-mode="server"` pack `data_mode` ile eşleşir; `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` yok.
- [ ] AC-44 (zorunlu) — `ContractsIndex` resx 7 dilde (`en, tr, fr, es, zh, ar, ru`) parite PASS; sayfada sabit metin yok;
  `Help.LEGALCONTRACTS.*` ve `Nav.Page.LEGALCONTRACTS` 7 dilde; `python3 scripts/check_pack_nav_keys.py` PASS.
- [ ] AC-45 (zorunlu) — `mdm.contracts.read` olmayan kullanıcıya UAS-001 erişim açıklaması (kabuk içinde, yönlendirme yok;
  başlık, tablo, düğme çizilmez); canlı deneme kaydı.
- [ ] AC-46 (zorunlu) — Tarayıcı ağ kaydında 5059'a ya da Gateway'e token'lı doğrudan çağrı yok; bütün çağrılar
  `/MasterData/Contracts/api/...` vekilinden; vekil Create'te `Idempotency-Key`'i üretir ve yeniden gönderimde aynısını kullanır.
- [ ] AC-47 (zorunlu) — MOD-0144 bozulmaz: `/api/contracts` Gateway rotaları 5065'e gitmeye devam eder, `/Contracts` web
  rotası ve `Nav.Page.CONTRACTS` değişmez; MOD-0217 ekranı ve nav anahtarı ayrı (`/MasterData/Contracts`, `Nav.Page.LEGALCONTRACTS`).
- [ ] AC-48 (zorunlu) — Gateway: `/api/mdm/contracts` ve `/api/mdm/contracts/{everything}` 5059'a gider (integration-agent
  teslimi); belge yükleme ve indirme Gateway üzerinden multipart / akışla uçtan uca çalışır (dev ortamı kaydı).
- [ ] AC-49 (zorunlu) — **Yanıt kaybı (P1-a):** gerçek replica set'te işaretçi yazımı işlenir, ardından depo dikişi yanıtı
  zaman aşımı / bağlantı sıfırlama olarak düşürür → mutabakat okuması işaretçiyi bulur; `compensate` **hiç** çağrılmaz;
  işaretçi ve tek niyet sürümde yerinde; yanıt 201 ve aynı `ContentId`. Sabotaj: (4b) okuması atlanıp doğrudan telafi
  çağrıldığında bu test kırmızıya döner (telafi çağrısı sayısı 1, sürümün gösterdiği nesne telafi edilmiş).
- [ ] AC-50 (zorunlu) — **Okuma da yok (P1-a):** işaretçi yazımının sonucu bilinmiyor ve mutabakat okuması da erişilemez →
  `compensate` hiç çağrılmaz; 503 `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN` + `contentId`; içeriksiz uyarı günlüğü; nesne
  MOD-0262-FU03'e yetim olarak verilmez. Ardından `ReconcileContractDocumentCommand`: işaretçi bulunursa 200 ve telafi yok;
  bağsızlık kanıtlanır ve MOD-0262 meta `owningItemId` / `owningVersionId` bu sürümü gösterirse telafi tam bir kez; okuma yine
  yoksa 503 ve telafi yok; nesne başka sürümün / sözleşmenin / kiracının ise 404 ve telafi yok. Sabotaj: Reconcile'da sahiplik
  ya da okuma denetimi kaldırıldığında ilgili durum kırmızıya döner.
- [ ] AC-51 (zorunlu) — **Sync güncel denemeye bağlı (P1-b):** sahte MOD-0023 `by-objects` (`includeDeleted=true`) yanıtlarıyla
  (a) önceki reddedilmiş denemenin anahtarlı örneği "Rejected", güncel denemenin örneği terminal değil → durum değişmez (sürüm
  `Draft`'a çekilmez); (b) başka nesnenin anahtarlı "Approved" örneği → durum değişmez; (c) güncel anahtarla iki satır → durum
  değişmez, 503 `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN` + uyarı; (d) güncel anahtar ve `WorkflowInstanceId` ile eşleşen tek
  "Approved" satır → `Approved`. Sabotaj: eşleme anahtar yerine nesneye ya da "en yeni örneğe" göre yapıldığında (a) ve (b)
  kırmızıya döner.
- [ ] AC-52 (zorunlu) — **§17 rev 2 alanları (P1-b):** Sync isteği `includeDeleted=true` taşır; güncel anahtarlı satır
  `isDeleted = true` → durum değişmez, bilinmiyor + uyarı ve aynı anahtarla Submit tekrarı gönderilmez; `idempotencyKey`'i boş
  satır → eşleşmez, durum değişmez; kimlik eşit anahtar farklı → bilinmiyor. `WorkflowInstanceId` boşken tek eşleşen satır
  örnek kimliğini ve `mdm.contract-version.workflow-start-confirmed` niyetini yazar.
- [ ] AC-53 (zorunlu) — **Kesin ret (P1-c):** MOD-0023 başlatması 400, 403 (izin), 403 (SoD), 404 (şablon yok) ve kapanış
  kayıtlı 409 `WORKFLOW_INSTANCE_START_CONFLICT` (history'de `workflow-start-recovery-close:{instanceId:N}` satırı) döndüğünde
  sürüm aynı koşullu yazımda `Draft`'a döner, `LastApprovalOutcome = StartRefused`, beklenen `LastApprovalReasonCode` yazılı,
  `mdm.contract-version.submission-refused` niyeti tam bir kez; yanıt 422 `MDM_CONTRACT_WORKFLOW_START_REFUSED`; sonraki Submit
  `a{n+1}` anahtarıyla başlar. Kapanış kaydı olmayan 409 `WORKFLOW_INSTANCE_START_CONFLICT` ve tanınmayan gövdeli 404 bu yola
  girmez (bilinmiyor).
- [ ] AC-54 (zorunlu) — **Geçici / bilinmiyor (P1-c):** gönderimden önce bağlantı hatası, 500, 503 → 503
  `MDM_CONTRACT_WORKFLOW_UNAVAILABLE`; gönderimden sonra zaman aşımı, 409 `WORKFLOW_TRANSITION_CONFLICT`, 503
  `WORKFLOW_TRANSACTION_UNAVAILABLE`, 503 `WORKFLOW_TRANSITION_CONFLICT` → 503 `MDM_CONTRACT_WORKFLOW_STATE_UNKNOWN`; her
  durumda sürüm `InApproval`, `WorkflowInstanceId` boş, `ApprovalAttempt` ve anahtar aynı; çözüm yalnız Sync ya da aynı
  anahtarla tekrar (tekrar sonrası sahte MOD-0023'te tek örnek). Sabotaj: zaman aşımı ya da `WORKFLOW_TRANSITION_CONFLICT`
  kesin ret sayıldığında sürüm `Draft`'a döner, sonraki Submit ikinci örneği açar ve test kırmızıya döner.
- [ ] AC-55 (zorunlu) — **Yeniden başlatma denetimi (P1-c):** aynı anahtarla tekrardan ve kesin retten sonraki yeni Submit'ten
  önce izin, veri kapsamı, yöneten şirket uygunluğu (`Active`) ve aktör bağı yeniden denetlenir: izni alınmış kullanıcı 403,
  kapsam dışı 404, şirket artık `Active` değil 422, `SubmittedBy` dışındaki kullanıcının tekrarı 409
  `MDM_CONTRACT_SUBMITTER_MISMATCH`; her birinde MOD-0023'e çağrı yok ve sürüm değişmez. Tekrar öncesi okumada güncel anahtarlı
  satır varsa tekrar gönderilmez. Sabotaj: denetim kaldırıldığında sahte MOD-0023'e çağrı gider ve test kırmızıya döner.
- [ ] AC-56 (zorunlu) — **İmzaya bağlı belge (P2-b):** kısmen imzalı `Approved` sürümde bir imzanın `SignedCopyContentId`'sinin
  gösterdiği belgeyi geri çekme 409 `MDM_CONTRACT_DOCUMENT_IN_USE`, belge ve imza değişmez; imzaya bağlı olmayan `SignedCopy`
  geri çekilebilir. Son imza, önceki imzalardan birinin belgesi (test fikstürüyle) geri çekilmiş gösterilirse 422
  `MDM_CONTRACT_SIGNED_COPY_REQUIRED` döner ve hiçbir değişiklik kalmaz (sürüm `Effective` olmaz). Sabotaj: geri çekme
  süzgecinden "imzaya bağlı değil" koşulu ya da son imzadaki yeniden doğrulama kaldırıldığında test kırmızıya döner.
- [ ] AC-57 (zorunlu) — **Geri çekme ile imza yarışı (P2-b):** gerçek replica set'te aynı `SignedCopy` belgesi için eşzamanlı
  WithdrawContractDocument ve RecordSignature (tekrarlı koşu) → biri başarılı, diğeri 409; sonuçta hiçbir imzanın
  `SignedCopyContentId`'si geri çekilmiş bir belgeyi göstermez. Sabotaj: iki yazımdan birinin süzgecinden belge koşulu
  kaldırıldığında imza geri çekilmiş belgeyi gösterir ve test kırmızıya döner.

## Test Expectations

- **Birim:** doğrulayıcılar (her alan); durum makinesi (Durumlar tabloları); `allowedActions`; idempotency gövde özeti
  (normalize, alan sırası bağımsız); MOD-0023 sonuç eşlemesi (tanınmayan → 503); başlatma yanıtı sınıflayıcısı (her yanıt tam
  bir sınıfa; zaman aşımı ve `WORKFLOW_TRANSITION_CONFLICT` kesin ret değil) ve güncel deneme eşleyicisi (AC-51…AC-54); MOD-0048 / MOD-0262 / MOD-0028 / RCS-001 yanıt
  sınıflandırması (erişilemez → 503, ret → 400 / 413 / 422); ticari değer maskesi.
- **Entegrasyon (gerçek Mongo replica set, işlem):** kiracı izolasyonu (AC-5), veri kapsamı (AC-6), soft delete, CAS 409,
  tekil indeksler ve yarışlar (AC-4, AC-10, AC-31, AC-33, AC-37, AC-57), işlem bütünlüğü ve hata enjeksiyonu (AC-13, AC-37,
  AC-49), niyetin iş
  belgesiyle yazılması ve sabotajı (AC-11), `Guid.Empty` taraması (AC-22).
- **Mongo yalıtımı (TRM-11 dersleri, AC-23):** suite kendi URI değişkenini ister (ad DL-1'de belirlenir; varsayılan port ya
  da başka suite'in değişkenine düşmez); her test sınıfı benzersiz veritabanı adı açar (paylaşılan sabit veritabanı yok);
  indeks oluşturan / değiştiren testler kendi veritabanında; aynı adlı indeksin farklı seçeneklerle iki kez kurulması testle
  yakalanır. Bellek içi sahte depo işlem / yarış AC'lerini kapatmaz.
- **Hata enjeksiyonu:** depo / işlem sarmalayıcısı dikişiyle ikinci yazımı düşürme; **yazımı uygulayıp yanıtı düşürme**
  (yanıt kaybı, AC-49) ve yazımı uygulamadan düşürme (kesin geri alma, AC-26) ayrı dikiş durumlarıdır; mutabakat okumasını
  düşürme (AC-50). Mongo `failCommand` kullanılacaksa kümenin buna izin verdiği ölçülür (bu oturumda ölçülmedi).
- **Sahte bağımlılıklar:** MOD-0262 (201, 400, 413, 500, zaman aşımı, telafi başarısız, meta okumasında sahiplik alanları var /
  yok), MOD-0023 (başlatma 201 / tekrar / 400 / 403 izin / 403 SoD / 404 şablon / 409 `WORKFLOW_INSTANCE_START_CONFLICT` kapanış
  kayıtlı ve kayıtsız / 409 `WORKFLOW_TRANSITION_CONFLICT` / 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` / gönderimden sonra zaman
  aşımı; `by-objects` satırları: güncel ve önceki anahtar, başka nesne, iki eşleşme, `isDeleted`, boş anahtar; `history`
  kapanış kaydı; örnek durumları; tanınmayan durum), MOD-0048, RCS-001, SEC-002 sağlayıcısı. Sahte istemci canlı kanıt değildir
  (Dependencies → kapanış tablosu).
- **Mimari:** her aksiyonda `[HasPermission]` (AC-16); onay / geri çağrı ucu yok (AC-36); MdmService'te dosya sistemi yazımı
  yok (AC-24); `mod0217_` / `Mod0217` / `MOD0217_` adları yok; `AuditTrailStandard` yeşil (AC-12).
- **Frontend:** browser smoke (liste, oluştur, düzenle, detay sekmeleri, belge yükle / indir, onaya gönder, imza kaydı, değişiklik,
  fesih); UAS-001 canlı denemesi; DataTable doğrulayıcı PASS; RESX 7 dil parite PASS; nav anahtar denetimi.
- **Build:** `Diten.MdmService` + `Diten.Web` 0 hata; yerel kapıda `scripts/` Python testleri de koşulur.
- **Kaynak testleri kanıt değildir:** kaynakta MOD-0217'nin 18 işlem testi URI eksikliğinden koşmadı, 6'sı başarısız (indeks
  seçenek çakışması, beklenmeyen 503, değişmezlik karşılaştırması; kök neden doğrulanmadı — TRM-11); spec zincirindeki test
  sayıları devralınmış etikettir (paket §J.3, §N.3). Hiçbiri güvence olarak alıntılanmaz; bütün testler Recovery'de yazılır
  (level-1 koşul 1).

## Page Help (UXH-001)

- Sayfa kodu `LEGALCONTRACTS` (`Nav.Page.LEGALCONTRACTS`); Create / Edit / Details aynı kodun alt sayfalarıdır.
- `Help.LEGALCONTRACTS.Purpose` (kiracının hukuki sözleşme kaydı; satın alma sözleşmelerinin MOD-0144 ekranında olduğu ve
  ödeme yapılmadığı yazılır), `Help.LEGALCONTRACTS.Audience` (sözleşme sahibi, hukuk birimi, imza kaydını giren kullanıcı),
  `Help.LEGALCONTRACTS.Step1…Step4` + `Step{n}Title` (Sözleşme ve ilk sürümü oluştur · Belgeleri ekle ve onaya gönder · İmzaları
  kaydet · Değişiklik ya da yenileme kararı gir). `Immediate` yok.
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact okundu (backend + frontend klasörü)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, entity_base, form_field_count, approval_status)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; durum kuralları
- [x] Failure Path ≥ 4 senaryo (duplicate/idempotency, missing, unauthorized, concurrency) + kiracılar arası + 503 / 400-422 ayrımı
- [x] Authorization: izin listesi (öneri) + policy + aktör
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor
- [x] Veri kapsamı cümlesi (SEC-002 §4) ve kanıt testi (AC-6)
- [x] Sayfa yardımı anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir; kural koruyan AC'lerde sabotaj

**İki kapı (MOD-0289 gap 16/2 deseni):** (a) **tasarım / onay kapısı = `ready-for-dev`** — aşağıdaki ENGELLER: kararlar,
onaylar, sözleşmeler ve yalnız var olan kodun / sözleşmenin **okunarak** ölçülmesi. Yeni kodun test geçmesi bu kapının
maddesi değildir. (b) **Dilim kabul kapısı** — "Dilim kabul kapısı" bölümünde.

**ENGELLER — tasarım / onay kapısı. Tek kural (MOD-0289 ve Treasury paketlerinin deseni):** işaretsiz maddeler **paket
düzeyidir** — hepsi kapanmadan `ready-for-dev` olmaz ve dolayısıyla her dilimin örtük önkoşuludur. **"— DL-n için"** işaretli
maddeler paket düzeyinde engel **değildir**: yalnız o dilimin (ve sonrakilerin) önkoşuludur, "Teslim dilimleri"nde o dilimin
Önkoşul satırında yazılıdır ve o dilim başlamadan kapanır; daha önceki dilimler o madde açıkken ilerler (ör. O-2 → DL-2
önkoşulu; DL-1 O-2 açıkken ilerler). Dilim önkoşulu "Dilim kabul kapısı"ndan ayrıdır: önkoşul karar / onay / okunarak ölçümdür,
kabul kapısı yeni kodun testlerinin geçmesidir.
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); paketi yazmamış onaylayıcı
- [ ] DCP numarası atandı ve DCP `approved` (MOD-0217 bugün hiçbir DCP'de değil)
- [ ] MDM domain-config satırı (D38/2 önerisi) ve kimlik kaydı satırı main'de (bugün çalışma ağacında, commit edilmemiş)
- [ ] O-1 MOD-0144 sınırı karara bağlandı (K1 + procurement sahibi)
- [ ] O-4 `mdm.contracts.*` anahtar listesi MOD-0018 sahibince kabul; açık atama anahtarları (`read-tenant-wide`) Auth'a (D9)
- [ ] O-5 RCS-001 öneki (`CTR` önerisi) sahip + `.antigravity` onayıyla §3'e yazıldı; RCS-001 ortak kod servisi (BL-565) ve sahibi
- [ ] SEC-002 kapsam sağlayıcısının `mdm.contracts.*` anahtarlarıyla çalıştığı ölçüldü
- [ ] O-6 denetim defteri (yeni satırlar, belirteç, borç yok) ve K2 sınıfı CT kararı; `AuditCategory` değeri
- [ ] O-7 karşı taraf modeli (Supplier seçilirse MOD-0140 sözleşme ölçümü)
- [ ] O-9, O-10, O-11, O-12, O-14, O-15 sahip kararı
- [ ] MOD-0048 para birimi listesinin MdmService'ten tüketim yolu ölçüldü
- [ ] O-2 D37'nin MOD-0028 kısmı için yüzey belirlendi ve okunarak ölçüldü (platform-shared-services) — DL-2 için
- [ ] MOD-0262-FU01 sözleşmesi ölçüldü: çağrı kimliği (O-13), `scope` değeri, boyut üst sınırı, izin listesi, meta okumasında
  `owningItemId` / `owningVersionId` alanları (Reconcile'ın sahiplik denetimi); canlı doğrulama kaydı (pack §19'da açık) — DL-2 için
- [ ] O-13 çağrı kimliği kararı — DL-2 için (DL-3'teki MOD-0023 çağrısı da buna bağlıdır)
- [ ] O-3 MOD-0023'te MOD-0217 iş akışı şablonu (MOD-0023 sahibi, D38/4) ve örnek durum adları ölçüldü — DL-3 için
- [ ] MOD-0023 §17 rev 2 (803fbaa5…, PR #235) onaylı (CT-APR-02/01): `by-objects` / `history` okuma alanları (`idempotencyKey`,
  `isDeleted`, `approvalTaskId`), `includeDeleted=true`, kapanış kaydı anlamı — DL-3 için. Kod MOD-0023 sahibinin WP'siyle gelir;
  okuma alanlarının main'de olması ve AC-W2 / AC-W12 / AC-W13'ün geçmesi DL-3 **dilim kabul kapısındadır**, önkoşulu değil
- [ ] MOD-0023 başlatma hata gövdesi kodları (400, 403 izin / SoD, 404 şablon) okunarak ölçüldü (başlatma yanıtı sınıfları) — DL-3 için
- [ ] O-8 imza modeli sahip kararı — DL-3 için
- [ ] MDM domain-config frontend/gateway kapsam cümlesi — DL-4 için
- [ ] Gateway rotaları (integration-agent) — DL-4 için

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Producer paketi MOD-0217 v1.0 (TRM-B4-02), `manifest.json` sha256
  `0d822c4f93eea64e623b1ef163488e50fceee8034ca0a77cc8b27de53cfd507b` (bu oturumda ölçüldü; level-1 kaydındaki değerle aynı);
  paket dosyası `MOD-0217-TRANSFER-PACKET-v1.0.md` sha256 `9d12625da29c443298f88f2d6abf0670fcdc3c2528e9d85992e540a20b56b2fa`,
  `CONTRACT-STATE.json` `0ee1174b…234f`, `SOURCE-BINDINGS.json` `333eaefd…983c`, `AUTHOR-REPORT.md` `5df5dad2…1c09` (dördü de
  manifest değerleriyle aynı); kaynak commit `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d` (bağlı 242 S dosyası, paket §O).
  Kanıt seviyesi E0. Kaynak kodu bu oturumda okunmadı.
- **Kod alma yok (D38/3):** kaynak dosyaları alınmaz, uyarlanarak da alınmaz; bu paketteki "tasarım olarak alınır" ifadesi
  yalnız davranış / alan fikrinin Recovery'de yeniden yazılmasıdır.
- Taban paket MDM-070 (`owner: ai-agent`, `draft`, sha256 `4b7d52ce…de46`, paket §A) bu paketin yerine geçmez; HEAD ağacında
  yok (keşif: yalnız 8 başka ref'te).
- `target: 2027-03-31` planlama yer tutucusudur, sahip kararı değildir.

### Level-1 koşulları (`MOD-0217-v1.0.md`) → bu paket

1. **Kanıt (6 başarısız, 18 koşmayan işlem testi):** kaynak işlem iddiası alıntılanmaz → Runtime Constraints → "İşlem
   bütünlüğü", AC-13, AC-37, AC-4, AC-31, AC-33; Test Expectations → Mongo yalıtımı (AC-23). Alındı ucu (`create-attempts`)
   taşınmaz; kiracılar / şirketler arası red AC-5, AC-6, AC-27.
2. **Taban paket eski kimlik MDM-070:** bu paket kanonik MOD-0217 kimliğiyle yazıldı (DCP-002 çıkış 0); MDM-070'in
   kayıtta takma ad olarak yazılması Follow-up.
3. **"Document Management" kapısı:** D37 ile MOD-0028 + MOD-0262-FU01'e çözüldü; ölçümde MOD-0028'de iş belgesi kaydı yüzeyi
   bulunamadı → O-2.
4. **TRM-13 kararı:** D19/1 + D38 (MDM yerleşimi).

### Tüketici onay slotları (paket §P, 9 slot) → bu paket

AUTH → `[HasPermission]` + `mdm.contracts.*` önerisi (O-4); LE → süreç içi `LegalEntity` okuması; WORKFLOW → MOD-0023 genel
uçları + şablon (O-3); AUDIT → MDM izi yol `b` (O-6), kaynak röle alıcısı taşınmaz; SIGNATURE → elle imza kaydı, geri çağrı yok
(O-8); EVIDENCE → v1'de yok (O-9); DOCUMENT → D37 (O-2); PAYMENTS → v1'de bağ yok (O-15); EVENTS → v1'de olay yok (O-15). Slot
kapanışları §U kaydında izlenir; bu paket hiçbir ACK oluşturmaz.

### D37 / D38 uygulaması

| Karar | Seçim | Bu pakette | AC |
|---|---|---|---|
| D37/1 | MOD-0028 kayıt + sürümler, MOD-0262-FU01 dosya | Ownership, Owned Objects (portlar), Entity Fields (`Documents`), Dependencies, Runtime Constraints → "Uzak çağrı sırası" | AC-24…AC-29, AC-49, AC-50 |
| D37/2 | MOD-0217 tüketici; değişiklik XMC-001 ile | Repo Scope "kapsamında olmayan" listesi, Protected Paths, O-2, O-13 | — |
| D37/3 | kalan koşul MDM sahibi onayı | D38/1 ile kapandı | — |
| D38/1 | MDM / `Diten.MdmService` | frontmatter, Runtime Constraints | — |
| D38/2 | taslağı CT yazar; domain-config + kayıt satırı önerilir | bu dosya; satırlar CT'de (Ready-for-dev engeli) | — |
| D38/3 | kod alma yok | durum notu, Repo Scope, Implementation Notes | — |
| D38/4 | MOD-0023 şablonu MOD-0023 sahibinin cevabını gerektirir | O-3, Dependencies, Runtime Constraints → "Uzak çağrı sırası" | AC-33…AC-35, AC-51…AC-55 |

### Taşınmayacaklar (do-not-carry)

1. Kaynak rotaları `api/contracts` ve `api/v1/contracts` (`ContractsController.cs:15-16@da4abb20`), kaynak Gateway blokları
   (`ocelot.json:1621-1660@da4abb20`, `AuthenticationOptions` yok) ve kaynak portu **5050** — Recovery'de `/api/contracts`
   zaten MOD-0144'ündür.
2. Denetleyici düzeyi `IAsyncActionFilter` kimlik koruması (`ContractsController.cs:28-38@da4abb20`) ve `X-Tenant-Id` başlık
   karşılaştırması (`Mod0217VerifiedRequestContext.cs:43-45@da4abb20`) — yalnız başlıkla kiracı hiçbir zaman kabul edilmez;
   MDM'nin standart kiracı çözümü + `[HasPermission]` kullanılır.
3. `[HasPermission]`'sız `lifecycle-preflight` ve eylem içi SuperAdmin / `permission` talebi kontrolü (`:152`, `:355`, `:370`).
4. 12 `contract.*` izin anahtarı ve 4 değer takma adı (`Mod0217Permissions.cs`, `Mod0217VerifiedRequestContext.cs:17-29@da4abb20`).
5. Denetim rölesi: `IMod0217AuditRelay` / `Mod0217AuditRelayAdapter` (DI :77), `Unavailable*` varsayılanları (DI :67-69,
   Platform DI :249), Platform alıcısı `api/internal/audit/mod0217/r1`, `Diten.Platform`'daki 9 `Mod0217AuditRelay*` dosyası,
   `Diten.Building.Blocks` röle temeli (21 dosya) ve Platform `Contracts/Audit` (46 dosya) — **`Unavailable*` varsayılanları
   uygulama sayılmaz** (paket §D T6, §L.2).
6. Create makbuz deposu ve araması (`CreateWithReceiptAsync`, `GetCreateReceipt`, `ContractCreateReconciliationOptions`,
   9 `MOD0217_CREATE_*` kodu) ve taslak ekler (create-receipt-reconciliation v1.0 / v1.1, create-uncertainty-ux v1.0 — sahip
   kararı 3 ile taslak).
7. Geri çağrı uçları ve gelen kutusu (`callbacks/workflow`, `callbacks/signature`, `Mod0217CallbackReceipt`,
   `Mod0217CallbackTransactionStore`).
8. Kendi iş akışı HTTP istemcisi `Mod0217ContractWorkflowGateway` (Infrastructure DI :44) — yerine MOD-0023 genel uçları.
9. `Mod0217EventPublisher` ile işlem sonrası HTTP olayları `contract.*.v1` (`Mod0217IntegrationServices.cs:53-139@da4abb20`;
   kalıcı değil, outbox yok).
10. Web katmanında **yerel geçici klasöre yükleme** (`frontend/Diten.Web/Controllers/ContractsController.cs:712-748@da4abb20`).
11. `UnavailableContractCreateLegalEntityAccess` (DI :97, `null` döner) — şirket erişimi SEC-002 ile.
12. `_LayoutBackbone`, `Views/MDM/Contracts/*` (16 dosya), kaynak JS (10), `contracts.css`, 35 kaynak resx — paket §I yalnız
    ölçülmüş dizin olarak kullanıldı; dosyalar taşınmaz.
13. `mod0217_*` koleksiyon adları, `uq_mod0217_*` / `idx_mod0217_*` indeks adları, `Mod0217*` tip önekleri, `MOD0217_*`
    hata literalleri, serbest metin yaşam döngüsü hataları.
14. Çok boyutlu durum modeli (onay 4, imza 5, kanıt 4, gizlilik 4, risk 4 ayrı boyut).
15. Yalnız onay bildiren toplu eylemler (`bulk-actions/{action}`, paket §I).
16. Kaynak test sayıları ve kaynak işlem testleri güvence olarak (TRM-11, paket §J.3, §N.3).

### Tasarım notları

- Sürümü ayrı belgede, karşı tarafı ve yenileme kararlarını başlıkta tutmak, kaynağın yedi koleksiyonlu işlem gruplarını üç
  işlem grubuna indirir (Create, son imza, değişiklik); her biri ayrı ölçülebilir AC'ye bağlıdır.
- "Süresi doldu" saklanmadığı için zamanlanmış iş, İ2 satırı ve iş ile kullanıcı arasında yarış yoktur.
- Onay sonucunun okunarak alınması, kaynağın en büyük açık maddesini (geri çağrı üreticisi bağı, paket §J.2) gereksiz kılar;
  bedeli kullanıcının "yenile" adımıdır (arka plan işi Follow-up).
- Bulgular (bu oturum, @31c448d5d): MOD-0144 aynı alanda kodlu bir modül; `Nav.Page.CONTRACTS` ve `/Contracts` alınmış;
  Platform'daki belge kaydı + sürüm uçları MOD-0029-FU01'e ait — üçü de bu paketin adlandırma ve O-1 / O-2 kararlarını etkiledi.

## Follow-up Items

- MOD-0217 okuma sözleşmesi (ör. sözleşme `lookup-validation`) — ilk tüketici (MOD-0126 ya da başka) istediğinde, kendi
  sürüm kuralıyla.
- Entegrasyon olayları (`contract.*`) kalıcı yolla (outbox) — tüketici adlandırılınca (O-15).
- e-imza sağlayıcısı ve imza geri çağrısı (O-8).
- MOD-0031 kanıt bağlantısı, gizlilik düzeyi ve risk derecesi (O-9).
- Onay sonucunu periyodik okuyan arka plan işi (varsayılan kapalı, İ2 satırıyla).
- `SubmittedBy` artık tekrar edemediğinde başlatma bekleyen sürüm için yönetici çözüm yolu ve sonucu bilinmeyen belge
  bağlarının (`MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN`) dönemsel mutabakatı (v1'de yalnız kullanıcı tetikli Reconcile).
- Merkezi ret kaydı (`Outcome = Denied`) — denetim sahibine.
- MOD-0144 sözleşmesine bağlantı (O-1 kararına göre).
- Müşteri karşı tarafı (CRM hesap kimliği ölçülünce, O-7).
- Toplu dışa aktarma (AUD-001 §7 okuma denetimiyle).
- Kimlik kaydında MDM-070'in MOD-0217 takma adı olarak yazılması (DCP-002).
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | MOD-0217 ile MOD-0144 (Contracting & Clause Library) sınırı | (a) MOD-0144 satın alma / tedarik sözleşmelerinin SoR'u kalır; MOD-0217 satın alma dışı hukuki sözleşmeler (satış, hizmet, NDA, kira, lisans, ortaklık, grup içi) (b) MOD-0217 bütün sözleşmelerin kurumsal kaydı, satın alma sözleşmeleri MOD-0144'e başvuruyla (c) MOD-0217 satın alma sözleşmelerini de üstlenir | **(a)** v1 için; (b) bağlantısı Follow-up. Bu yüzden önerilen `ContractType` listesinde `PURCHASE` yok | sahip (K1) + procurement sahibi (MOD-0144'ü onaylayan) + Blueprint sahibi |
| O-2 | D37'nin "MOD-0028 belge kaydı ve sürümleri" kısmının uygulanma yolu (ölçüm: MOD-0028'de iş belgesi kaydı yüzeyi yok; kayıt + sürüm uçları MOD-0029-FU01'de) | (a) MOD-0028 sahibine XMC-001 ile en küçük eklemeli "iş belgesi kaydı" yüzeyi önerisi; DL-2 cevaba kadar bekler (b) MOD-0217 yalnız `ContractVersion` (Blueprint SoR) + MOD-0262-FU01 işaretçisi; MOD-0028 bağı sonra — D37'nin yorumunu değiştirir (c) MOD-0029 denetimli belge — D37'de seçilmedi | **(a)** — D37'yi korur; (b) yalnız sahip D37'yi yeniden yorumlarsa | sahip (K1) + platform-shared-services (MOD-0028 sahibi) |
| O-3 | MOD-0023'te MOD-0217 onay iş akışı şablonu (şablon kodu, adımlar, onaylayıcı rolleri, inceleme adımı) | MOD-0023 sahibinin tasarımı | — (karar MOD-0023 sahibinin; D38/4) | MOD-0023 sahibi (Ali) |
| O-4 | İzin anahtarları | Authorization tablosundaki `mdm.contracts.*` listesi · başka ad alanı / ayrıntı | **tablodaki liste**; `read-tenant-wide` yalnız açık atama | MOD-0018 sahibi + sahip (K1) |
| O-5 | RCS-001 sözleşme kodu | (a) OTOMATİK, tür öneki `CTR`, şirket öneki yöneten şirketin kısa kodu (b) İKİSİ DE (c) başka önek | **(a) `CTR`** (§3'te çakışmadığı ölçüldü; `CTP` nakit eşik politikasınındır). Karar değil: §3 satırı `.antigravity` değişikliğidir; BL-565 servisinin sahibi belirsiz → engel | sahip (K1) + `.antigravity` onayı; BL-565 sahibi TBD |
| O-6 | Denetim defteri ve K2 | (a) `mdm-guvenilir-kaynak-niyeti` genişletmesi, yeni K2 satırları, borç yok, CT-DECISIONS kod aşamasında (D27/11 deseni); K2 = onay sonucu, imza, fesih (b) yalnız defter satırı, K2 yok | **(a)**; `AuditCategory` değeri denetim sahibiyle ayrıca | CT + sahip (K1) |
| O-7 | Karşı taraf modeli | (a) türlü başvuru: `LegalEntity` (süreç içi), `Supplier` (MOD-0140 sözleşmesiyle), `External` (yalnız ad) (b) yalnız serbest ad (c) taraf ana verisi gelene kadar bekle | **(a)**; `Supplier` MOD-0140 sözleşmesi ölçülene kadar kapalı; müşteri v1'de yok | sahip (K1); MOD-0140 kullanımı procurement sahibi |
| O-8 | İmza modeli v1 | (a) elle imza kaydı + imzalı kopya belgesi, sağlayıcı ve geri çağrı yok (b) e-imza sağlayıcısı + geri çağrı (c) imza kaydı yok | **(a)** | sahip (K1) |
| O-9 | Kanıt (MOD-0031), gizlilik düzeyi, risk derecesi | (a) v1 dışı, Follow-up (b) v1'de alanlar, kural yok (c) v1'de MOD-0031 kapısı | **(a)** | sahip (K1) |
| O-10 | Create idempotency | (a) `Idempotency-Key` zorunlu + belgede anahtar ve gövde özeti (b) idempotency yok (MOD-0289 gibi) | **(a)** — kaynağın bilinmeyen sonuç boşluğunu kapatır | sahip (K1, MDM sahibi rolü D38) |
| O-11 | Kod listeleri ve durum modeli | Durumlar tabloları, `ContractType` çekirdek listesi, yenileme kararı değerleri, küsurat MOD-0048 · başka | **pack'teki öneriler**; kiracı ek türleri v1'de yok | sahip (K1) |
| O-12 | Ticari değer görünürlüğü | (a) `mdm.contracts.value.read`, okuma denetimsiz (b) okuma denetimli (c) maskesiz | **(a)** | sahip (K1) + MOD-0018 sahibi |
| O-13 | MdmService → Platform (MOD-0262, MOD-0023) çağrı kimliği ve MOD-0262 `scope` değeri | (a) kullanıcı token'ı Gateway üzerinden (MOD-0126 O-7 deseni) — kullanıcı `platform.*` izinlerine de ihtiyaç duyar (b) güvenilir servis kimliği (c) `scope`: var olan `Documents` · eklemeli yeni değer | **ölçümden sonra karar** — öneri (a) + var olan `Documents`; yeni değer gerekirse XMC-001 | sahip (K1) + platform-shared-services |
| O-14 | İç sorumlu kullanıcı başvurusunun doğrulanması | (a) Auth kullanıcı aramasıyla (sözleşme ölçülmedi) (b) doğrulamasız Guid (c) yalnız oluşturan | **(c) v1'de varsayılan + (a) ölçülünce atama** | sahip (K1) + MOD-0018 / Auth sahibi |
| O-15 | Olaylar ve ödeme sırası (EVENTS, PAYMENTS slotları) | (a) v1'de olay ve ödeme bağı yok (b) outbox'lı olaylar | **(a)**; kaynak "0126 → 0217 → 0124" sırası benimsenmez | sahip (K1) |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | sözleşme çekirdeği ve denetim izi ve paket iskeleti | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Backend File Convention, Validation Rules, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar, Teslim dilimleri |
| DL-2 | sözleşme sürümleri ve belgeler (MOD-0028 ve MOD-0262-FU01) | Runtime Constraints, Authorization Convention |
| DL-3 | onay ve imza ve yaşam döngüsü (MOD-0023) | Dependencies, Failure Path to Verify, Audited Events |
| DL-4 | kiracı ekranı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events'in sözleşme satırları DL-1'de, belge satırları DL-2'de gelir, bölüm DL-3'te onay / imza satırlarıyla
kapanır; Runtime Constraints'in kiracı, idempotency ve işlem kuralları DL-1'de, uzak çağrı sırası DL-2 / DL-3'te uygulanır;
Authorization Convention'ın temel anahtarları DL-1'de, belge anahtarları DL-2'de gelir). Ortak bölümler (Acceptance
Criteria, Test Expectations, Ready-for-dev Checklist, Follow-up Items) her dilimde aşağıdaki AC kimlikleriyle ölçülür.

**Kesişen AC kuralı:** birden çok dilimin eylemlerine uzanan AC (ör. AC-5 bütün uçlar, AC-11 her yazma, AC-16 her aksiyon)
her dilimin Bitiş listesinde "(… kısmı)" ile yazılır ve o dilimde yalnız o dilimin eylemleri için kanıtlanır; **"kısmı" ile
işaretli bir AC, onu listeleyen son dilimde — son eylemleri ekleyen dilimde — tam kapanır.** İşaretsiz yazılan AC o dilimde
tam kapanır. **Önkoşul kuralı:** her dilimin önkoşulu, paket düzeyi ENGELLER'in tümü (`ready-for-dev`) ile Ready-for-dev
Checklist'te "— DL-n için" işaretli kendi maddeleridir; başka dilimin maddesi bu dilimi bekletmez.

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: `Domain/Entities/{Contract,ContractVersion}.cs`, enum'lar, `Features/Contract/**` (Create, Update, Delete,
  BulkDelete, UpdateContractVersion, liste / detay / sürüm sorguları), depolar, indeksler, işlem grubu deposu,
  `ContractsController` (bu aksiyonlar), manifest sağlayıcısı, RCS / MOD-0048 portları, denetim izi genişletmesi, defter +
  CT-DECISIONS satırı. **Neden önce:** belge ve iş akışı olmadan sözleşme kaydını, kiracı / kapsam / idempotency / işlem
  kurallarını en düşük riskle kanıtlar. Önkoşul: paket düzeyi ENGELLER'in tümü (`ready-for-dev`: CT-APR-02/01, DCP,
  domain-config + kayıt satırı, O-1, O-4, O-5 + RCS-001 servisi, SEC-002 sağlayıcı ölçümü, O-6, O-7, O-9, O-10, O-11, O-12,
  O-14, O-15, MOD-0048 tüketim yolu); "— DL-1 için" işaretli madde yok — O-2, O-3, O-8, O-13 açıkken ilerler. Bitiş: AC-1,
  AC-2, AC-3, AC-4, AC-5 (DL-1 uçları kısmı), AC-6 (DL-1 uçları kısmı), AC-7 (DL-1 yazmaları kısmı), AC-8, AC-9, AC-10 (DL-1
  komutları kısmı), AC-11 (DL-1 yazmaları kısmı), AC-12 (DL-1 defter satırları kısmı), AC-13, AC-14 (DL-1 yazmaları kısmı),
  AC-15 (DL-1 uçları kısmı), AC-16 (DL-1 aksiyonları kısmı), AC-17 (DL-1 alanları kısmı), AC-18 (şirket uygunluğu kısmı), AC-19,
  AC-20, AC-21 (DL-1 okuma ve yazmaları kısmı), AC-22 (DL-1 yazmaları kısmı), AC-23, AC-32, AC-42 (DL-1 eylemleri kısmı).
- **DL-2** — Yollar: `AddContractDocumentCommand`, `WithdrawContractDocumentCommand`, `ReconcileContractDocumentCommand`,
  `GetContractDocumentContentQuery`, `CreateContractAmendmentCommand`, `WithdrawContractVersionCommand`,
  `Infrastructure/{DocumentRepository,DocumentRecords}/**`, belge uçları. **Neden ayrı:** iki dış bağımlılığa (D37) bağlıdır ve
  O-2 cevabını bekler; DL-1'i bekletmesin. Önkoşul: DL-1 ve "— DL-2 için" maddeleri: O-2 kararı + yüzey ölçümü, O-13,
  MOD-0262-FU01 sözleşme ölçümü (meta sahiplik alanları dahil). Bitiş: AC-5 (belge ve değişiklik uçları kısmı), AC-6 (belge ve
  değişiklik uçları kısmı), AC-7 (DL-2 yazmaları kısmı), AC-10 (DL-2 komutları kısmı), AC-11 (DL-2 yazmaları kısmı), AC-12
  (DL-2 defter satırları kısmı), AC-14 (DL-2 yazmaları kısmı), AC-15 (DL-2 uçları kısmı), AC-16 (DL-2 aksiyonları kısmı), AC-17
  (belge ve değişiklik alanları kısmı), AC-21 (DL-2 yazmaları kısmı), AC-22 (DL-2 yazmaları kısmı), AC-24…AC-31, AC-39, AC-42
  (belge ve değişiklik eylemleri kısmı), AC-49, AC-50.
- **DL-3** — Yollar: `SubmitContractVersionForApprovalCommand`, `SyncContractVersionApprovalCommand`,
  `RecordContractSignatureCommand`, `RecordContractRenewalDecisionCommand`, `TerminateContractCommand`, `ArchiveContractCommand`,
  `Rules/{WorkflowStartOutcomeClassifier,WorkflowAttemptMatcher}.cs`, `Infrastructure/Workflow/**`. **Neden ayrı:** MOD-0023
  şablonuna (O-3) bağlıdır. Önkoşul: DL-2 (imzalı kopya belgesi için) ve "— DL-3 için" maddeleri: O-3 şablonu ve MOD-0023
  durum adları ölçümü, MOD-0023 §17 rev 2 onayı (803fbaa5…, PR #235), MOD-0023 başlatma hata gövdesi kodları ölçümü, O-8,
  O-13. Bitiş (son arka uç dilimi — kesişen AC'ler burada tam kapanır): AC-5 (onay / imza / yaşam döngüsü uçları kısmı), AC-6
  (onay / imza / yaşam döngüsü uçları kısmı), AC-7 (DL-3 yazmaları kısmı), AC-10 (DL-3 komutları kısmı), AC-11 (DL-3 yazmaları kısmı), AC-12 (DL-3 defter
  satırları kısmı), AC-14 (DL-3 yazmaları kısmı), AC-15 (DL-3 uçları kısmı), AC-16 (DL-3 aksiyonları kısmı), AC-17 (imza /
  yenileme / fesih alanları kısmı), AC-18 (ilk Submit sonrası değişmezlik kısmı), AC-21 (DL-3 yazmaları kısmı), AC-22 (DL-3
  yazmaları kısmı), AC-33…AC-38, AC-40, AC-41, AC-42 (onay / imza / yaşam döngüsü eylemleri kısmı), AC-51…AC-57.
- **DL-4** — Yollar: `Views/MasterData/Contracts/**`, JS, resx (7 dil), MVC vekili, sayfa yardımı, nav anahtarı; Gateway
  rotaları integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** ekran API'ye bağlıdır, arka uç değişmeden
  ilerler. Önkoşul: DL-3 ve "— DL-4 için" maddeleri: Gateway rotaları, domain-config frontend/gateway kapsam cümlesi. Bitiş:
  AC-43…AC-48.

## Dilim kabul kapısı

**(MOD-0289 gap 16/2 deseni; ayrı başlık — `Teslim dilimleri` bölümünde tek tablo kuralı, CT-APR-02) her dilim için, kabul /
birleştirmeden önce (`ready-for-dev`'den önce değil):** Kabul kapısı dilimin önkoşulundan ayrıdır (Ready-for-dev Checklist'teki
tek kural): önkoşul dilim başlamadan kapanan karar / onay / okunarak ölçümdür, kabul kapısı o dilimin yeni kodunun testlerinin
geçmesidir. "(… kısmı)" ile listelenen AC o dilimde yalnız o kısım için geçer; tam geçişi onu listeleyen son dilimin kabul
kapısındadır.

| Dilim | Geçmesi gerekenler (çalıştırmak geçmek değildir) |
|---|---|
| DL-1 | "Bitiş" listesindeki AC'ler (kısımlarıyla); gerçek replica set AC'leri (AC-4, AC-10, AC-13, AC-14) kendi Mongo URI'li suite'te (AC-23); `AuditTrailStandard` (AC-12); bir sözleşme oluşturmanın merkezi denetim kaydı (dev ortamı, AUD-001 §8); `Diten.MdmService` build 0 hata; yerel kapıda `scripts/` Python testleri |
| DL-2 | "Bitiş" listesindeki AC'ler (kısımlarıyla); AC-31 ve AC-49 gerçek replica set'te; MOD-0262-FU01'e karşı dev ortamı kaydı (yükle, indir, başka kiracı 404, kesin geri almada telafi); O-2 yüzeyinin sözleşme testi (AC-29); build |
| DL-3 | "Bitiş" listesindeki AC'ler (kesişen AC'lerin tam kapanışı dahil); AC-33, AC-37 ve AC-57 gerçek replica set'te; MOD-0023 §17 rev 2 okuma alanları main'de ve MOD-0023 AC-W2 / AC-W12 / AC-W13 geçmiş (MOD-0023 sahibinin WP'si); MOD-0023'e karşı dev ortamı onay ve ret turu kaydı; build |
| DL-4 | "Bitiş" listesindeki AC'ler; tarayıcı smoke ve UAS-001 canlı denemesi; DataTable doğrulayıcı; RESX 7 dil parite; nav anahtar denetimi; Gateway uçtan uca belge yükleme / indirme; `Diten.Web` build |

AC durumu kabul kaydında (`scripts/acceptance_record.py`) izlenir; bu pakette her AC yalnız "tanımlı"dır.
