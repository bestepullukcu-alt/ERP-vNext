---
id: MOD-0175-FU01
name: Quarantine hold closure on source reversal
parent: MOD-0175
parent_name: Quarantine / Blocked Stock
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: QuarantineQueue
    data_mode: server
  - module: BlockedStock
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
risk: HIGH
risk_reason: "Stok değişmezi (açık tutma kaydı miktarı ≤ kova bakiyesi) + GxP kaydı denetimi (AUD-001 K2) + sözleşme (QUARANTINE 1.4.0; DL-3'te INVENTORY ek hata kodu) + ürün kodu yolu + iki XMC-001 (MOD-0173 defter, BL-501 denetim kapsamı)"
status_note: "draft r1 (2026-11-02) — MVP-6 yerel turu F-2 (KRN-2026-0001) ve MVP-1 turu 2026-11-02 (KRN-2026-0004) bulgusu: kaynak hareketin REVERSAL'ı tutma kaydını kapatmıyor, kayıt sıkışıyor. Kod yazılmaz: açık kararlar Q-01…Q-11 (§19.4; sahip Beste, MVP-6 etkisi Natig), CT-APR-02 bağımsız inceleme ve CT-APR-01 onay satırı gerekir. Registry satırı bu paketle yazılmaz (CT)."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-02
target: 2026-11-30
form_field_count: 9
dependencies: [MOD-0175, MOD-0173, MOD-0174, QUARANTINE-BUNDLE-v1.3.0, INVENTORY-BUNDLE-v1.5.0, BL-501 (central audit), MOD-0173-FU03 (yalnız DL-3 — INVENTORY sürüm sırası)]
---

# MOD-0175-FU01 — Quarantine hold closure on source reversal

> **Kimlik (DCP-002) — ölçüldü 2026-11-02, dal `chore/ct/mod0178-dl1-accept-01` @ `e3cbbca4d`:**
> `python3 -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0175-FU01 --name "Quarantine hold closure on source reversal" --parent MOD-0175`
> → çıkış **0** ("OK MOD-0175-FU01: proven against Blueprint/registry"). Boşluk ölçümü: `execution/registries/module-id-registry.md`'de
> MOD-0175'in tek satırı var (satır 306, çocuk satırı yok); `execution/`, `docs/`, `.antigravity/` altında `MOD-0175-FU01` metni 0;
> çalışma ağacında `MOD-0175-FU` geçen `.md` / `.tsv` dosyası 0; `git log --all -S "MOD-0175-FU01"` (487 ref) boş → **FU01 ilk boş numara**.
> Mükerrer iş keşfi CT tarafından koşuldu: `verify_module_id.py . --discover "quarantine reversal"` → yalnız gateway 5067 karantina rotası
> (üretici, çakışma değil); paket / registry / görev bulgusu 0 (bu oturumda yeniden koşulmadı). `FU-0175-*` adlı takip maddeleri (ör.
> `FU-0175-WF-SOD`) MOD kimliği değildir. **Registry satırı bu paketle yazılmaz** (Q-11, CT).
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
> `MOD-0175-quarantine-blocked-stock.md` (`ready-for-dev`) **tabanıdır**; bu paket onun §8 "Hold states" kuralını ("A hold is never deleted";
> ana yol `Open → DecisionPending → Decided`) **genişletir**: yeni bir terminal durum ekler, hiçbir kaydı silmez, hiçbir stok hareketi yazmaz.
> Ebeveynin "No new stock status" (KQ-E) ve DEC-INV-08 kuralları aynen geçerlidir: `Voided` bir **kayıt** durumudur, stok durumu değildir.
> Bu sayılan genişletme dışındaki her çelişkide ebeveyn kazanır ve fark CT'ye sorulur.
>
> **Sözleşme:** yalnız eklemeli. QUARANTINE-BUNDLE 1.3.0 (`docs/analysis/contracts/quarantine.openapi.yaml:12-14`, `x-status: FROZEN`,
> `x-owner: MOD-0175`) → **1.4.0** (DL-1, §8.7). INVENTORY-BUNDLE 1.5.0 (`inventory-bundle.openapi.yaml:9-10`) yalnız DL-3 seçilirse (Q-06)
> bir ek hata koduyla değişir; sürüm numarası MOD-0173-FU03'ün 1.6.0 / 1.7.0 adımlarından sonraki ilk boş sürümdür (§8.8).

## 1. Module Summary

MOD-0175 bir **kaynak hareketten** doğan tutma kaydını (`QuarantineHold`, `KRN-…`) ya da bloke stok kaydını (`BlockedStock`, `BLK-…`, origin
`OtherInbound`) defterin post-commit gözlemcisi ve intake süpürücüsüyle açar; kayıt `SourceMovementId` ile o hareketin `transactionId`'sine
bağlıdır. Hareketin kendisi sonradan `REVERSAL` ile geri alındığında **bugün kayda hiçbir şey olmaz**: stok kovadan çıkar (QUARANTINE 4 → 0),
kayıt `Open` ve miktar 4 kalır. Kayıt karar alamaz, kuyrukta ve lot kalite durumunda "karantinada 4" görünür; karar aşamasındaysa uygulanamaz
ve kilitli kalır (§19.2 ölçümler). Aynı kovada başka bir kaydın stoğu varsa daha kötüsü olur: hayalet kaydın kararı başkasının stoğunu taşır.

Bu paket:

1. **Geri alma kapanışı (DL-1):** kaynak hareketin REVERSAL'ı defterde commit edilince bağlı kayıt — durumu buna izin veriyorsa — yeni terminal
   durum **`Voided`** (neden `SourceReversed`) ile kapanır. Aynı post-commit gözlemci yolu + bir süpürücü, tam bir kez (§8.2).
2. **Karar aşaması kuralı (DL-1):** henüz hiçbir satırı defterde postlanmamış karar (`PendingApproval`, `Approved`, satırsız `ApplyFailed`)
   kayıtla birlikte `Voided` olur; sonradan gelen onay sonucu etkisizdir. Satırı postlanmış ya da uygulanmış karar sonrası geri alma
   **otomatik kapanmaz**: kayda "kaynak geri alındı · uyuşmazlık" işareti ve geçmiş satırı düşer (§8.3).
3. **Denetim (DL-1):** kapanış ve uyuşmazlık işareti AUD-001 yol `b` (merkezi kayıt, K2: işlemle birlikte) + ürün içi karantina geçmişi (§21).
4. **Ekran (DL-2):** kuyruk, detay ve bloke stok listesinde "Geçersiz · Kaynak geri alındı" durumu; 7 dil; sayfa yardımı.
5. **Defter bekçisi (DL-3, Q-06'ya bağlı):** uyuşmazlığa yol açacak REVERSAL'ı ve MOD-0175'in kendi hareketlerinin REVERSAL'ını defter
   post etmeden reddeder (kalite kararını atlayan yol kapanır).

**Kısmi geri alma yoktur (ölçüldü):** REVERSAL miktarı aslınınkine eşit olmak zorundadır ve bir hareket bir kez geri alınır (§19.2 G-4, G-5).
Bu yüzden "miktarı düşür" dalı v1'de yoktur; kapanış hep tam kapanıştır.

## 2. Ownership and Boundaries

**Sahip (tek yazar, K15):** MOD-0175 kayıt durumları (`HoldStatus`, `BlockedStatus`, karar olayları, karantina geçmişi) — ebeveyn §2.

**Tüketir:**
- MOD-0173 defteri: `IPostedMovementObserver` (post-commit, değişmez), REVERSAL bağı `InventoryTransaction.ReversesTransactionId`, salt okuma
  (`QuarantineLedgerReader` deseni). DL-3'te yeni bir ön koşul portu (XMC-001, §5).
- BL-501 merkezi denetim iletici (`ICentralAuditDeclaration`, `RequireEnlistment = true`).

**Yapmaz:**
- Stok hareketi yazmaz (kapanış yalnız kayıt durumu; stok zaten REVERSAL ile çıkmıştır). Yeni stok durumu ya da hareket türü yok (DEC-INV-08).
- Kayıt silmez (`IsDeleted` kullanılmaz; ebeveyn "never deleted").
- Uygulanmış kararı geri almaz, başka kaydın miktarını değiştirmez, kovayı "düzeltmez".
- Platform Workflow örneğini iptal etmez (sistem token'ı yok — ebeveyn CT-Q11; Q-02).
- REVERSAL dışı yollarla (ör. QUARANTINE'den doğrudan `SCRAP`, `STATUS_TRANSFER`) kovadan çıkan stoğu ele almaz → takip maddesi
  FU-0175-FU01-OUTBOUND (§20).
- MOD-0173 / MOD-0186 / MOD-0142 paketlerini değiştirmez; gerekli kod dikişleri §5'te, sahip onayıyla (XMC-001).

## 3. Owned Objects

| Nesne | Tür | Dilim | Not |
|---|---|---|---|
| `ApplySourceReversalToHoldCommand` | iç MediatR komutu (HTTP ucu yok) | DL-1 | girdi: REVERSAL `transactionId`; sonuç `Voided` / `Conflict` / `NoChange`; `ICentralAuditDeclaration` |
| `ApplySourceReversalToBlockedCommand` | iç MediatR komutu | DL-1 | aynı, `BlockedStock` için |
| `ApplySourceReversalToHoldHandler`, `ApplySourceReversalToBlockedHandler` | handler | DL-1 | kural `SourceReversalRules` (saf) |
| `ApplySourceReversalToHoldValidator`, `ApplySourceReversalToBlockedValidator` | validator | DL-1 | §12 V1–V3 |
| `SourceReversalRules` | saf domain kuralı | DL-1 | kaydın durumu + karar olayları + defterdeki satır anahtarları → `Void` / `Conflict` / `NoChange` (§8.3 tablosu) |
| `QuarantineRecordStates` | saf yüklem | DL-1 | `IsOpen(HoldStatus)` = Open \| DecisionPending; `IsTerminal` = Decided \| Voided; 13 sunucu çağrı yeri bunu kullanır (§8.5) |
| `QuarantineSourceReversalObserver` | `IPostedMovementObserver` | DL-1 | yalnız `MovementType = REVERSAL`; komutu gönderir |
| `QuarantineSourceReversalFeed` | çapraz kiracı salt okuma | DL-1 | açık kayıtlar (`SourceMovementId` dolu) + o hareketlerin REVERSAL'ları |
| `QuarantineSourceReversalSweeper` + `…HostedService` | arka plan | DL-1 | açık kayıt tabanlı (zaman penceresi yok), kayıt başına kiracı + şirket kapsamı |
| `QuarantineHold.Voided`, `.SourceReversalConflict`; `BlockedStock` aynısı | alan | DL-1 | §4 |
| `HoldStatus.Voided`, `BlockedStatus.Voided`, `DecisionEventKind.Voided` / `DecisionState.Voided`, `HistoryAction.SourceReversed` / `.SourceReversalConflict` | enum değeri (sona eklenir) | DL-1 | §4 |
| QUARANTINE-BUNDLE 1.4.0 | sözleşme | DL-1 | §8.7 |
| Kuyruk / detay / bloke stok görünümü + JS + resx | Web | DL-2 | §11 |
| `IReversalPrecondition` (MOD-0173 dikişi) + `QuarantineReversalPrecondition` | port + uygulama | DL-3 | Q-06 = A ise |
| INVENTORY ek hata kodu (öneri `REVERSAL_QUALITY_RECORD_LOCKED`) | sözleşme | DL-3 | Q-06 = A ise |

Kayıt kodu (RCS-001): yeni kayıt türü **yok** (`Voided` bir durumdur) → yeni görünen kod yok; `KRN-` / `BLK-` numaraları değişmez.

## 4. Entity Fields

Taban: InventoryService `EntityBase` (ebeveyn §4). Silme yok.

### 4.1 `QuarantineHold` ve `BlockedStock` — eklenen alanlar (ikisinde aynı)

| Alan | Tür | Kural |
|---|---|---|
| `Voided` | `RecordVoid?` | yalnız `Status = Voided` iken dolu; bir kez yazılır |
| `Voided.AtUtc` | DateTimeOffset | kapanış anı |
| `Voided.Reason` | enum `VoidReason` | v1'de tek değer `SourceReversed` (Q-05 elle kapatma seçilirse `Manual` eklenir) |
| `Voided.ReversalTransactionId` | string (UUID D) | REVERSAL'ın `transactionId`'si |
| `Voided.ReversalTransactionNumber` | string | REVERSAL'ın `TransactionNumber`'ı (`INV-…`) |
| `Voided.ReversedByUserId` | Guid | REVERSAL'ı postlayan (`InventoryTransaction.CreatedBy`) |
| `Voided.DecisionId` | Guid? | kapanışla birlikte `Voided` olan aktif karar |
| `SourceReversalConflict` | `SourceReversalMark?` | yalnız uyuşmazlıkta; durum değişmez; bir kez yazılır |
| `SourceReversalConflict.AtUtc`, `.ReversalTransactionId`, `.ReversalTransactionNumber`, `.ReversedByUserId`, `.Cause` | | `Cause` ∈ `Decided` \| `LinesPosted` \| `QuantityChanged` (§8.3) |

`Quantity` **değişmez** (kaydın tuttuğu miktar iz olarak kalır); açık miktar toplamları `QuarantineRecordStates.IsOpen` ile süzer.

### 4.2 Enum ekleri (yalnız sona; mevcut değerlerin sayısal sırası değişmez)

- `HoldStatus` (`Domain/Quarantine/Enums/QuarantineEnums.cs:29-34`) += `Voided`.
- `BlockedStatus` (`:112-117`) += `Voided` (Q-01: `Closed` yeniden kullanılmaz — `Closed` "imha / iade / blokesi kaldırıldı" demektir).
- `DecisionEventKind` (`:79-92`) += `Voided`; `DecisionState` (`:62-76`) += `Voided`; `QuarantineFacts.StateOf` (`QuarantineFacts.cs:101-120`)
  eşlemesi eklenir (eşlenmeyen olay bugün `ArgumentOutOfRangeException` atar — eksik eşleme fail-closed).
- `HistoryAction` (`:125-146`) += `SourceReversed`, `SourceReversalConflict`.
- `QuarantineDecisionEvent.OnceKey` (`QuarantineSupport.cs:131`): `Voided` tek seferlik olaydır (`OnceKey = "Voided"`).

### 4.3 `QuarantineHistoryEntry` — eklenen alan

| Alan | Tür | Kural |
|---|---|---|
| `ReversalTransactionId` | string? | yalnız `SourceReversed` / `SourceReversalConflict` satırında |

`ByUserId` = REVERSAL'ı postlayan; `CorrelationId` = REVERSAL'ın `CorrelationId`'si (defterin `InventoryMovementReversed` denetim satırına
bağlanır); `Reason` = REVERSAL numarası. Koleksiyon yalnız ekleme (mevcut mimari testi).

### 4.4 İndeksler

Yeni indeks gerekmez: kayıt `(TenantId, SourceMovementId)` tekil kısmi indeksiyle bulunur (`QuarantineIndexConfiguration.cs:49-50, 81-82`;
okuma `QuarantineRepository.cs:85, 125` kiracı + şirket kapsamlı). Süpürücü beslemesi için `(TenantId, LegalEntityId, Status)` mevcut
(ebeveyn §4). DL-3 bekçisi aynı tekil indeksle okur.

## 5. Repo Scope

- **DL-1 (`services/Diten.InventoryService/**`):**
  - `src/Diten.InventoryService.Domain/Quarantine/{Entities,Enums,Rules}/…` — §4 alanları, enum ekleri, `SourceReversalRules`, `QuarantineRecordStates`.
  - `src/Diten.InventoryService.Application/Features/Quarantine/Commands/ApplySourceReversalTo{Hold,Blocked}Command.cs`,
    `Handlers/CommandHandlers/ApplySourceReversalTo{Hold,Blocked}Handler.cs`, `Validators/ApplySourceReversalTo{Hold,Blocked}Validator.cs`
    (yeni dosyalar Golden Reference klasör düzeninde, §10).
  - `…/Features/Quarantine/Services/QuarantineIntake.cs` — gözlemci kaydı ve **geç intake** kuralı (§8.4: kaynağı zaten geri alınmış hareket için
    kayıt açılmaz).
  - `…/Features/Quarantine/Services/QuarantineApplyEngine.cs` — `Voided` kararın uygulanmaması, `RecordAppliedAsync`'in `Voided` kaydı
    `Decided`'a çevirmemesi, `ApplyOutcomeAsync` terminal listesi ve onay ↔ kapanış dışlaması (§8.6).
  - `…/Features/Quarantine/Services/QuarantineFacts.cs`, `QuarantineSupport.cs`, `QuarantineMapping.cs`, `QuarantineRecallRequester.cs`,
    `Handlers/{DecisionHandlers,LotQualityStatusHandler,QueryHandlers,RetestHandlers,ReviewHandlers,StockActionHandlers,UpdateHoldHandler}.cs`
    — açık / terminal yüklemi (§8.5).
  - `src/Diten.InventoryService.Persistence/Quarantine/{QuarantineRepository,QuarantineSourceReversalSweeper}.cs` — besleme + süpürücü;
    `QuarantineRepository.cs:108` yüklemi.
  - `src/Diten.InventoryService.Api/Quarantine/QuarantineServiceCollectionExtensions.cs` — gözlemci, süpürücü, seçenekler.
  - Q-03 = A ise `src/Diten.InventoryService.Api/CentralAudit/InventoryCentralAuditScope.cs` — sistem aktörü kipi (**XMC-001: BL-501 bağdaştırıcısı**,
    sahip Beste; izin yazılmadan dokunulmaz).
  - Testler `services/Diten.InventoryService/tests/**`; sabit sayı `CentralAudit/CentralAuditDeclarationTests.cs:37` (71 → 73, aynı değişiklikte).
  - Sözleşme `docs/analysis/contracts/quarantine.openapi.yaml` 1.4.0 + `docs/analysis/contracts/README.md` satırı.
- **DL-2 (`frontend/Diten.Web/**`):** `wwwroot/assets/js/Quarantine/{queue,details,blocked,decide,quarantine-ui}.js`,
  `Views/Quarantine/{Index,Details,Blocked}.cshtml` (yalnız rozet / afiş / süzgeç seçeneği), `Resources/Views/Quarantine/Quarantine.{en,tr,fr,es,zh,ar,ru}.resx`,
  `Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` (QUARANTINEQUEUE, QUARANTINEDETAILS, BLOCKEDSTOCK adımları), `frontend/Diten.Web.Tests/Quarantine/**`.
- **DL-3 (Q-06 = A ise; XMC-001 MOD-0173):** `…/Features/Inventory/Ports/IReversalPrecondition.cs` (yeni),
  `…/Features/Inventory/Services/InternalMovementPostingService.cs` (`BuildReversalAsync` içinde, `:253-256` "zaten geri alındı" denetiminden
  sonra tek çağrı), `Application/Common/InventoryErrorCodes.cs` (ek kod), `…/Features/Quarantine/Services/QuarantineReversalPrecondition.cs`,
  `docs/analysis/contracts/inventory-bundle.openapi.yaml` (ek sürüm).
- **Bu taslak:** yalnız bu dosya.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`, `execution/registries/**`, `domain-config.md`, ebeveyn ve başka paketler (MOD-0173, MOD-0173-FU03,
  MOD-0186, MOD-0183, MOD-0142).
- `gateway/Diten.ApiGateway/ocelot.json` (değişmez; §15).
- `_Layout.cshtml` (FROZEN), Archive.
- `services/Diten.Building.Blocks/**` (BL-501 ortak iletici — değişmez; Q-03 = A yalnız Inventory'nin kendi kapsam bağdaştırıcısını değiştirir).
- `services/Diten.SupplyChainService/**`, `services/Diten.ProcurementService/**` (REVERSAL üreticileri — davranışları değişmez; DL-3 etkisi §7).
- Dondurulmuş sözleşmeler yalnız sahibinin tek yazar adımıyla: QUARANTINE (MOD-0175, bu paket), INVENTORY (MOD-0173, DL-3).

## 7. Dependencies

| Bağımlılık | Durum (ölçüldü `e3cbbca4d`) | Etki |
|---|---|---|
| MOD-0175 | `ready-for-dev`, registry `in-progress` (satır 306) | taban; ekranlar ve kayıtlar canlı |
| MOD-0173 post-commit gözlemci | var (`IPostedMovementObserver.cs:10-13`; `InternalMovementPostingService.cs:155-159, 201-214`) — her commit edilen hareket, REVERSAL dahil, gözlemcilere gider; gözlemci hatası loglanır, posting'i değiştirmez | DL-1 yolu; ek MOD-0173 değişikliği yok |
| MOD-0173 REVERSAL kuralı | tam ve tek (§19.2 G-4, G-5) | kısmi dal yok |
| BL-501 merkezi denetim | `tests/architecture/audit-ledger/Diten.InventoryService.md` → `inventory-merkezi-iletim \| b \| işaret \| ICentralAuditDeclaration`; `RequireEnlistment = true`; K2 borcu yok | yol `b` kullanılabilir; Q-03 sistem aktörü için kapsam bağdaştırıcısı değişikliği |
| REVERSAL üreticileri | S-2 hareket listesi "Reversal" satır eylemi (`InventoryLedgerManifestProvider.cs:76`; JS yalnız `sourceModule = MOD-0173` satırında gösterir — `StockMovements/index.js:15`, **sunucuda böyle bir sınır yok**); "Düzelt" = REVERSAL + yeni hareket (KD-1); MOD-0186 geç yazan (`ReturnLateWriterPolicy.cs:37-43`, MVP-6); MOD-0142 `reverseGrn` (`ReverseGrnHandler.cs:56-59`) | DL-1 hepsini kapsar (gözlemci kaynağa bakmaz); DL-3 seçilirse karar verilmiş kaydın kaynağının REVERSAL'ı bu üreticilere 422 döner (MVP-6: Natig, Q-10) |
| MOD-0173-FU03 | `ready-for-dev`, INVENTORY 1.6.0 / 1.7.0 önerisi | yalnız DL-3: INVENTORY sürüm numarası FU03'ten sonra (Q-07) |
| RabbitMQ | bu makinede kapalı (ebeveyn §8) | Workflow sonucu okuma anında uzlaştırılır; kapanış bundan bağımsız |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam

- `Diten.InventoryService` (port 5067, `AGENTS.md` §3; ebeveyn künyesindeki 5066 eskidir). Süreç içi, aynı Mongo veritabanı.
- Her yazma kiracı + şirket kapsamlıdır: gözlemci REVERSAL'ı postlayan isteğin kapsamında (`ITenantContext`) çalışır; süpürücü kayıt başına
  kapsamı kaydın `TenantId` + `LegalEntityId`'siyle kurar (intake süpürücüsü deseni, `QuarantineLedgerReads.cs:199-206`). Kayıt yalnız kendi
  kiracı + şirketindeki `SourceMovementId` ile bulunur. Başka kiracının kaydı 404 (ebeveyn V23 / §13 #21).

### 8.2 Tetik — gözlemci + süpürücü, tam bir kez

- **Gözlemci (hızlı yol):** `QuarantineSourceReversalObserver.OnPostedAsync(t)` yalnız `t.MovementType = REVERSAL` ve `t.ReversesTransactionId`
  doluyken çalışır; `ReversesTransactionId`'yi `SourceMovementId` olarak taşıyan hold ve blocked kayıtlarının her biri için ilgili komutu
  (`ApplySourceReversalTo{Hold,Blocked}Command(reversalTransactionId)`) gönderir. Mevcut `QuarantineIntakeObserver` REVERSAL'ı zaten dışlar
  (`QuarantineIntake.cs:44`; ayrıca `MovementDirectionRules.cs:29` REVERSAL'ı `Inbound` saymaz) — o davranış değişmez.
- **Süpürücü (kaçanlar ve eski veri):** zaman penceresine **değil açık kayıtlara** dayanır: her turda kiracı / şirket başına `IsOpen` hold'lar ve
  `Open` blocked kayıtlar (`SourceMovementId` dolu, sayfalı ≤ 500) okunur, bu kimliklerden `ReversesTransactionId IN (…)` ile REVERSAL'ı olanlar
  bulunur, her biri için aynı komut gönderilir. Varsayılan aralık 300 sn, `SettleSeconds` 60 (gözlemciye pay); seçenekler `Inventory:Quarantine`
  altında (`QuarantineSweeperOptions` deseni, `QuarantineLedgerReads.cs:149-167`). Gerekçe: intake süpürücüsünün 72 sa penceresi
  (`:157-158`) bu FU'dan önce geri alınmış kayıtları (KRN-2026-0001, KRN-2026-0004) asla görmez; açık kayıt tabanı onları ilk turda kapatır
  ve ayrı bir "geri doldurma" işi gerektirmez (Q-08).
- **Tam bir kez:** komut önce kaydı okur; `Voided` (aynı REVERSAL) ya da `SourceReversalConflict` (aynı REVERSAL) zaten yazılıysa **işlem açmadan**
  `NoChange` döner — merkezi kayıt yazılmaz (`RequireEnlistment` altında işlemsiz başarılı komut kayıt bırakmaz,
  `ICentralAuditDeclaration.cs` `ICentralAuditOutsideTransaction` notu). Değişiklik tek karantina işleminde, kaydın `Version`'ı beklenen
  değerle (`ReplaceHoldAsync` / `ReplaceBlockedAsync`) yazılır; eşzamanlı ikinci çağıran değiştirmede kaybeder, işlem geri alınır (enlist
  edilmiş merkezi kayıt da), yeniden okur ve `NoChange` döner.
- Gözlemci hatası posting'i etkilemez (MOD-0173 davranışı); süpürücü kapatır.

### 8.3 Kural tablosu (`SourceReversalRules`)

"Postlanmış satır" defterden ölçülür: kararın satır anahtarları `quarantine:{decisionId}:{lineNo}` (`QuarantineMovementPoster.cs:55`) için
`FindByIdempotencyKeyAsync` — olaylara güvenilmez, çünkü satır postlanıp `Applied` yazılmadan önce geri alma gelebilir.

| Kayıt durumu | Aktif karar durumu | Postlanmış satır | Sonuç |
|---|---|---|---|
| Hold `Open` (Waiting / InReview; Draft karar olabilir) | — / Draft | — | **Void**: hold `Voided`; Draft karar dokunulmaz, submit'i V6 ile reddedilir |
| Hold `DecisionPending` | `PendingApproval` | 0 | **Void** + karara `Voided` olayı |
| Hold `DecisionPending` | `Approved` (uygulanmamış) / `ApplyFailed` | 0 | **Void** + karara `Voided` olayı |
| Hold `DecisionPending` | `Approved` / `ApplyFailed` | ≥ 1 | **Conflict** (`Cause = LinesPosted`); durum değişmez |
| Hold `Decided` | `Applied` | — | **Conflict** (`Cause = Decided`) |
| Hold `Voided` | — | — | **NoChange** |
| Blocked `Open`, `Quantity` = kaynağın `BaseQuantity`'si | yok / `PendingApproval` / satırsız `Approved` · `ApplyFailed` | 0 | **Void** (+ karara `Voided`) |
| Blocked `Open`, `Quantity` < kaynağın `BaseQuantity`'si | herhangi | — | **Conflict** (`Cause = QuantityChanged`) |
| Blocked `Closed` / `SampleRetained` | — | — | **Conflict** (`Cause = Decided`) |
| Blocked `Voided` | — | — | **NoChange** |

- **Void** aynı işlemde: kayıt `Status = Voided`, `Voided` alanı, `ActiveDecisionId = null`, `UpdatedAt`, `Version + 1`; karar varsa `Voided` olayı
  (`ErrorCode` yok, `Detail` = REVERSAL numarası); geçmiş `SourceReversed`; merkezi kayıt (enlist).
- **Conflict** aynı işlemde: `SourceReversalConflict` alanı, `Version + 1`; geçmiş `SourceReversalConflict`; merkezi kayıt. Hiçbir stok, durum ya
  da başka kayıt değişmez — QA kullanıcısı görür ve düzeltmeyi elle yapar (Q-04). **Uyuşmazlık bugün yalnız aynı kovada başka kaydın stoğu varken
  doğar**; tek kayıtlı kovada defter REVERSAL'ı zaten `INSUFFICIENT_STOCK` ile reddeder (ölçüm: MVP-6 turu R1-b, 422 "available 1, requested 4").
  Önlem DL-3'tür.
- `FourEyesViolation`, `Rejected`, `Cancelled`, `TimedOut`, `Withdrawn`, `StartFailed` sonrası hold zaten `Open`'a döner (ebeveyn §8) → ilk satır.

### 8.4 Geç intake

Intake gözlemcisi kaçmış, REVERSAL intake süpürücüsünden önce gelmişse süpürücü bugün geri alınmış harekete kayıt açar (besleme REVERSAL'ı
sormaz, `QuarantineLedgerReads.cs:138-146`). Kural (Q-09 = A): `QuarantineIntakeService.TakeInAsync` kayıt açmadan önce
`IInventoryLedgerRepository.IsReversedAsync(transactionId)` sorar; geri alınmışsa kayıt açmaz, bilgi logu yazar. Kalan küçük yarış (intake
açtıktan hemen sonra REVERSAL) §8.2 gözlemcisi / süpürücüsüyle kapanır.

### 8.5 Açık / terminal yüklemi

Bugün "açık" 13 sunucu yerinde `!= HoldStatus.Decided` ya da `== HoldStatus.Decided` olarak yazılı (§19.2 G-12). `Voided` eklenince bunların
her biri `Voided`'ı açık sayar (KPI, geç süzgeci, lot kalite durumu miktarları, yeniden test kuyruğu, CT-Q6 rezervasyon çakışma listesi, recall
listesi, `EnteredBefore` süzgeci, V6). Hepsi `QuarantineRecordStates.IsOpen` / `IsTerminal`'a geçer; kaynak taraması `HoldStatus.Decided`
karşılaştırmasını yüklem dosyası dışında yasaklar (AC-18). Blocked tarafında mevcut denetimler `== BlockedStatus.Open` / `!= BlockedStatus.Open`
biçimindedir (`QuarantineDecisionRules.cs:120`, `ReviewHandlers.cs:111`, `QuarantineMapping.cs:183`, `StockActionHandlers.cs:195`,
`LotQualityStatusHandler.cs:114`) → `Voided` kendiliğinden kapalı sayılır; değişmez.

### 8.6 Karar motoru ile dışlama

- `ApplyOutcomeAsync` durumu işlem **dışında** okur (`QuarantineApplyEngine.cs:411-412`) ve `Approved` olayını yeniden okumadan ekler (`:476-489`);
  `StateOf` "son olay kazanır" (`QuarantineFacts.cs:103`). Kapanış ile onay farklı belgelere yazdığı sürece ikisi de commit olur ve `Voided`'dan
  sonra `Approved` gelir → durum `Approved` → motor postlar. **Değişmez:** bir kararda `Voided`'dan sonra `Approved` / `Applied` olayı olmaz.
  Yol (öneri, uygulama serbest, AC-13 bağlayıcı): onay kolu da kaydın `Version`'ını beklenen değerle artırır; iki işlem aynı belgeye yazar, Mongo
  yazma çakışması birini yeniden koşturur, yeniden koşan son durumu görür.
- `ApplyOutcomeAsync` terminal listesine (`:435-437`) `Voided` eklenir: sonradan gelen sonuç `200 Voided` döner, olay yazmaz.
- `ApplyAsync` uygulanabilir durumları (`:81`) `Voided`'ı içermez; her satırı postlamadan hemen önce karar durumunu yeniden okur; `Voided` ise
  durur (satır postlanmaz). `RecordAppliedAsync` (`:168-209`) hold'u `Voided` bulursa `Decided`'a çevirmez, `Conflict` yazar. Bu yeniden okuma ile
  post arasındaki kalan pencere yalnız çok kayıtlı kovada anlamlıdır ve DL-3 ile kapanır (bekçi, `Approved` / `ApplyFailed` kararlı kaydın
  kaynağının REVERSAL'ını reddeder).
- Platform Workflow örneği açık kalır (sistem kimliği yok; Q-02). WorkCenter'daki onay `Voided` kararda etkisizdir; DisplayContext bağlantısı
  kaydı "Geçersiz" gösterir (DL-2).
- Karar e-imzası geçerli kalır: karar yalnız eklemeli, imzalanan özet değişmez (ebeveyn §8 e-signature).

### 8.7 Sözleşme — QUARANTINE 1.4.0 (yalnız ekleme)

- `HoldStatus` += `Voided`; `BlockedStatus` += `Voided`; `DecisionState` += `Voided`; `HistoryAction` += `SourceReversed`, `SourceReversalConflict`
  (`quarantine.openapi.yaml:400, 405, 407, 408`).
- `Hold` / `HoldDetail` / `BlockedStock` (+ ayrıntı) isteğe bağlı `voided` (`RecordVoid`: `atUtc`, `reason` [`SourceReversed`], `reversalTransactionId`,
  `reversalTransactionNumber`, `reversedBy`, `decisionId?`) ve `sourceReversalConflict` (`atUtc`, `reversalTransactionId`, `reversalTransactionNumber`,
  `reversedBy`, `cause` [`Decided`, `LinesPosted`, `QuantityChanged`]); `HistoryEntry` isteğe bağlı `reversalTransactionId`. Hiçbiri `required` değil.
- `listHolds` / `listBlocked` `status` sorgu parametresi aynı enum'u gösterdiği için `Voided` ile süzülebilir; **varsayılan liste değişmez**
  (bugün durum verilmeyince hepsi döner; `Voided` satırlar `Decided` gibi sona sıralanır — Q-08).
- Yeni hata kodu yok: kapanmış kayıtta eylemler mevcut `QUARANTINE_HOLD_CLOSED` (`QuarantineDecisionRules.cs:39`) ve blocked için mevcut
  `QUARANTINE_BLOCKED_CLOSED` (`:40`) kodunu döner; ileti "kaynak geri alındı" der.
- Olay yayını yok (Inventory'de taşıma bağlı değil, ebeveyn §8). `x-changelog` 1.4.0 satırı "No existing path, parameter, property, type, required
  list, status code or enum value changed" cümlesiyle (1.1.0–1.3.0 emsali).

### 8.8 DL-3 — defter bekçisi (Q-06 = A ise)

- MOD-0173 dikişi `IReversalPrecondition.CheckAsync(InventoryTransaction original, CancellationToken)` → `Allow` | `Refuse(code, message, details)`;
  `InternalMovementPostingService.BuildReversalAsync` "zaten geri alındı" denetiminden (`:253-256`) sonra çağırır; ret → 422, hiçbir şey
  yazılmaz, yerel `inventory_audit`'e ret satırı (`ExpiryPostingPolicy.FlushRefusalAuditAsync` emsali).
- MOD-0175 uygulaması iki kural: (1) aslın `SourceModule = MOD-0175` → ret (DEC-INV-08: 0175'in kendi `STATUS_TRANSFER` / `SCRAP` /
  `SUPPLIER_RETURN` / `LOCATION_TRANSFER` hareketinin REVERSAL'ı kalite kararını atlar; düzeltme yeni 0175 kararıyla); (2) aslı bir kaydın
  `SourceMovementId`'si ve §8.3'e göre sonuç `Conflict` ya da karar `Approved` / `ApplyFailed` → ret. Diğer her durumda `Allow`.
- Okuma hatası → 503 `INVENTORY_UNAVAILABLE` (mevcut kod), fail-closed.
- INVENTORY ek kodu (öneri `REVERSAL_QUALITY_RECORD_LOCKED`, 422, `details: { recordNumber, recordType, cause }`): `InventoryErrorCodes.All`
  kapalı kataloğuna bu paketle girer (MOD-0173-FU03 CT-02 emsali); sözleşme sürümü FU03'ün 1.7.0'ından sonraki ilk boş sürüm (Q-07).
- Kalan pencere: bekçi işlem dışında okur; bekçi ile commit arasında karar uygulanırsa DL-1 `Conflict` yazar (görünür, sessiz değil).

### 8.9 Durum ve komut tablosu

| Tetik | Kaynak | Komut | Sonuç | Stok etkisi |
|---|---|---|---|---|
| REVERSAL commit (gözlemci) | MOD-0173 | `ApplySourceReversalToHoldCommand` / `…ToBlockedCommand` | Void / Conflict / NoChange | yok |
| Süpürücü turu | Inventory arka plan | aynı | aynı | yok |
| Geç intake (§8.4) | intake süpürücüsü | — (kayıt açılmaz) | — | yok |
| REVERSAL isteği (DL-3) | MOD-0173 `postMovement` | — (bekçi) | 422 / Allow | ret → yok |

## 9. Layout & Shell Contract

- DL-2 ekranları ebeveynin ekranlarıdır; hepsi `Layout = "_LayoutTenantShell"` (ölçüldü: `Views/Quarantine/Index.cshtml:11`,
  `Details.cshtml:10`, `Blocked.cshtml:11`). Bu FU layout satırına dokunmaz; AC-30 korur.
- Rota değişmez, hiçbiri `/Platform` ile başlamaz (Scope Tenant). UAS-001 aynen: izinsiz kullanıcıya tek açıklama, iskelet yok, yönlendirme yok.
- `golden_reference: compact` ve `form_field_count: 9` ebeveynin Karar Ver formundan miras (ebeveyn §4 "Form field count"); bu FU yeni form alanı
  eklemez.

## 10. Backend File Convention

Yeni dosyalar Golden Reference Compact düzeninde (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`:
`Commands/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/`):

```text
services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Quarantine/
├── Commands/ApplySourceReversalToHoldCommand.cs            (sealed record, ICentralAuditDeclaration)
├── Commands/ApplySourceReversalToBlockedCommand.cs
├── Handlers/CommandHandlers/ApplySourceReversalToHoldHandler.cs     (sealed class, suffix yok)
├── Handlers/CommandHandlers/ApplySourceReversalToBlockedHandler.cs
├── Validators/ApplySourceReversalToHoldValidator.cs
├── Validators/ApplySourceReversalToBlockedValidator.cs
└── Services/QuarantineSourceReversalObserver.cs
services/Diten.InventoryService/src/Diten.InventoryService.Domain/Quarantine/Rules/{SourceReversalRules,QuarantineRecordStates}.cs
services/Diten.InventoryService/src/Diten.InventoryService.Persistence/Quarantine/QuarantineSourceReversalSweeper.cs   (besleme + süpürücü + hosted service)
```

- **Bilinen sapma (ölçüldü, bu FU düzeltmez):** ebeveynin mevcut dosyaları tek dosyada komutlar (`Commands/QuarantineCommands.cs`) ve düz
  `Handlers/*.cs` düzenindedir. Yeni dosyalar Golden düzenine uyar; eskiler taşınmaz (kapsam dışı, XMC değil — aynı modül; taşıma ayrı iş).
- `Response<T>`, MediatR pipeline, `QuarantineErrorCodes` / `QuarantineDecisionRules` kodları (ebeveyn §10). Tenant token'dan, şirket kapsam
  ara katmanından ya da süpürücünün kurduğu kapsamdan.
- Mimari: karantina kodu defter koleksiyonuna yazmaz (mevcut test); karar / olay / geçmiş yalnız ekleme (mevcut test); yeni kaynak taraması
  `HoldStatus.Decided` karşılaştırmasını yüklem dosyası dışında yasaklar (AC-18).

## 11. Frontend File Contract

DL-2, ebeveynin ekran dosyalarında küçük eklemelerdir (yeni sayfa, yeni rota, yeni izin yok):

| Ekran | Dosya | Değişiklik |
|---|---|---|
| Karantina Kuyruğu (`QUARANTINEQUEUE`) | `queue.js` (`:68`, `:120`), `quarantine-ui.js` (`:95-104`) | `Voided` rozeti "Geçersiz · Kaynak geri alındı"; satır eylemi yok (Karar Ver çizilmez); geç rengi yok; KPI sunucudan zaten süzülü |
| Karantina Detayı (`QUARANTINEDETAILS`) | `details.js` (`:31`, `:125`), `Details.cshtml` | afiş: "Bu kayıt geçersiz: kaynak hareket {REVERSAL no} ile {tarih} tarihinde {kişi} tarafından geri alındı"; Karar Ver / İncelemeye al / Kararı geri çek / Yeniden uygula gizli; geçmiş etiketleri; hareketler listesinde REVERSAL satırı |
| Karar Ver (`QUARANTINEDECIDE`) | `decide.js` (`:162-163`) | `Voided` kayıtta mevcut "kapalı" iletisi, durum adıyla |
| Bloke Stok (`BLOCKEDSTOCK`) | `blocked.js` (`:41`, `:260`), `Blocked.cshtml` | durum süzgecine `Voided`; rozet; eylem yok |
| Uyuşmazlık | `quarantine-ui.js`, `details.js`, `blocked.js` | `Decided` / `Closed` kayıtta uyarı rozeti "Kaynak geri alındı · uyuşmazlık" + afiş ("stok bu kaydın dışında değişti; QA kontrolü gerekir") |

Yeni resx anahtarları (7 dil, `Resources/Views/Quarantine/Quarantine.*.resx`; bugün her dilde 451 anahtar, eşit):
`Quarantine.HoldState.Voided`, `Quarantine.Blocked.Status.Voided`, `Quarantine.Workflow.Voided`, `Quarantine.History.SourceReversed`,
`Quarantine.History.SourceReversalConflict`, `Quarantine.Details.VoidedBanner`, `Quarantine.Details.ConflictBanner`,
`Quarantine.Common.ConflictBadge`, `Quarantine.Conflict.Cause.{Decided,LinesPosted,QuantityChanged}`. Sayfa yardımı (UXH-001):
`Help.QUARANTINEQUEUE.*`, `Help.QUARANTINEDETAILS.*`, `Help.BLOCKEDSTOCK.*` içinde bir adım metni güncellenir (yeni anahtar gerekmezse
mevcut adım, 7 dil). JS'de sabit metin yok (`_QuarantineL10n.cshtml` / `quarantine.l10n.js` yolu).

**Liste ekranı dokunma protokolü (BL-440):** DL-2 `Index.cshtml` / `Blocked.cshtml` ve liste JS'ine dokunur → `verify_datatable_page.py`
koşulur, sapmalar sahibe numaralı liste ile sorulur. Ölçüm notu: `--area Quarantine --module Quarantine` bugün dosyaları bulamıyor (modül
`queue.js` / `blocked.js` adlarını kullanıyor, standart `index.js` değil); doğru `--module` / `screens[].module` eşlemesi DL-2'nin ilk adımında
CT ile netleşir.

## 12. Validation Rules

| # | Kural | Sonuç |
|---|---|---|
| V1 | `reversalTransactionId` boş olmayan UUID | 400 `INVALID_REQUEST` (iç çağrı; programlama hatası logu) |
| V2 | işlem kiracı + şirket kapsamında bulunur, `MovementType = REVERSAL`, `ReversesTransactionId` dolu | bulunmazsa `NoChange` (kayıt yok), uyarı logu; kapsam dışı aranmaz |
| V3 | kayıt `SourceMovementId = ReversesTransactionId` ile kendi kiracı + şirketinde | yoksa `NoChange` |
| V4 | §8.3 tablosu | Void / Conflict / NoChange |
| V5 | kapanmış kayıtta karar oluşturma / gönderme | 409 `QUARANTINE_HOLD_CLOSED` (hold), blocked için 409 `QUARANTINE_BLOCKED_CLOSED` |
| V6 | kapanmış kayıtta İncelemeye al / not güncelle / İncelemeyi tamamla | aynı 409 |
| V7 | `Voided` kararı yeniden uygula | 409 `QUARANTINE_DECISION_NOT_APPLICABLE` (`QuarantineApplyEngine.cs:83`) |
| V8 | `Voided` karara gelen Workflow sonucu | 200 `Voided`, olay yazılmaz |
| V9 | geç intake: kaynak geri alınmış | kayıt açılmaz |
| V10 (DL-3) | §8.8 bekçi kuralları | 422 ek kod / 503 `INVENTORY_UNAVAILABLE` |

## 13. Failure Path to Verify

1. `Open` hold'un kaynağı geri alındı → `Voided`, stok hareketi yok, geçmiş 1, merkezi kayıt 1.
2. Aynı REVERSAL için gözlemci + süpürücü (sıralı ve paralel) → tek kapanış, tek geçmiş, tek merkezi kayıt.
3. `PendingApproval` kararlı hold → `Voided`; sonra onay gelir → hareket yok, durum `Voided`.
4. Kapanış ile onay aynı anda → asla `Voided` + `Applied` birlikte değil.
5. Uygulanmış karar + çok kayıtlı kova → `Conflict`, başka kayıt değişmez.
6. Satırı postlanmış ama `Applied` yazılmamış karar → `Conflict` (olaylara değil deftere bakılır).
7. Blocked kısmi imha sonrası kaynak geri alındı → `Conflict`.
8. Kiracı A'nın REVERSAL'ı kiracı B'nin aynı `SourceMovementId`'li kaydına dokunmaz; aynı kiracının başka şirketi de.
9. İlgisiz REVERSAL → hiçbir kayıt, geçmiş, merkezi kayıt yok.
10. Merkezi kayıt yazılamaz (enlist hatası) → kapanış geri alınır, kayıt açık kalır, sonraki tur yeniden dener (K2).
11. Geç intake → kayıt açılmaz.
12. Bu FU'dan önce geri alınmış kayıt (72 sa'ten eski) → ilk süpürücü turunda `Voided`.
13. `Voided` kayıtta Karar Ver → 409; ekran düğmeyi çizmez.
14. Yetkisiz kullanıcı kuyruğu açar → UAS-001 (değişmez).
15. (DL-3) MOD-0175 hareketinin REVERSAL'ı → 422; karar verilmiş kaydın kaynağının REVERSAL'ı → 422; bekçi okuma hatası → 503; hepsinde defter değişmez.

## 14. Authorization Convention

- **Yeni izin anahtarı yok.** Kapanış sistem yoludur; HTTP ucu yoktur. Tetikleyen REVERSAL'ın kendi yetkisi defterindir
  (`inventory.movements.create`, MOD-0173); MOD-0175 bunu yeniden denetlemez.
- Okuma ve ekranlar ebeveynin anahtarlarıyla: `quarantine.holds.read` (kuyruk, detay, bloke stok). Manifest değişmez
  (`QuarantineBlockedStockManifestProvider`); yalnız `quarantine.*` (CAT-01).
- Q-05 = B seçilirse (elle kapatma) yeni Tier-3 anahtar `quarantine.holds.void` + `RowAction` `VOIDHOLD` (`IsDangerous: true`) bu pakete eklenir;
  önerilen karar A (yok).
- Aktör: merkezi kayıtta Q-03'e göre (öneri `System`), ürün geçmişinde REVERSAL'ı postlayan kullanıcı.

## 15. Gateway / API Routing Decision

**Değişiklik yok.** Okuma uçları mevcut `/api/quarantine/{everything}` → 5067 rotasından geçer; yeni uç yok. INVENTORY rotası değişmez
(DL-3 yalnız mevcut `postMovement` cevabına ek hata kodu getirir). `ocelot.json`'a dokunulmaz; integration-agent görevi yok.

## 16. Acceptance Criteria

Ortak fixture: kiracı T1 / şirket LE1; depo W, lokasyon L1; lotlu ürün; test ayarlarında `QuarantineSourceReversalSweeper` ve intake süpürücüsü
**kapalı** (testler süpürücüyü `SweepAsync` ile elle koşar). Her sabotaj tek tek uygulanır; kabul kaydına "kırmızı → geri al → yeşil, diff boş" yazılır.

**DL-1 — arka uç**

- [ ] AC-1 (zorunlu) — `CUSTOMER_RETURN` 4 EA → `QUARANTINE` (manuel hareket) gözlemciyle `Open / Waiting` hold açar; aynı hareketin REVERSAL'ı 201 → aynı istek içinde hold `status Voided`, `voided.reason SourceReversed`, `voided.reversalTransactionId` = REVERSAL kimliği, `voided.reversedBy` = postlayan, `quantity` 4 (değişmez), `activeDecision` yok; geçmişte tam 1 `SourceReversed` satırı. Sabotaj: `QuarantineSourceReversalObserver` kaydını DI'dan kaldır → hold `Open` kalır, test kırmızı.
- [ ] AC-2 (zorunlu) — Tablo güdümlü: `GOODS_RECEIPT_PO` → `QUALITY_INSPECTION`, `OPENING_BALANCE` → `QUARANTINE`, `COUNT_GAIN` → `QUARANTINE` ve `InReview` durumundaki hold'un her biri REVERSAL sonrası `Voided`; `reviewState` korunur. Sabotaj: kuralı `Source = CustomerReturn` ile sınırla → üç satır kırmızı.
- [ ] AC-3 (zorunlu) — `PendingApproval` kararlı hold (test onay portu): REVERSAL → hold `Voided`, karar durumu `Voided` (tek `Voided` olayı), `voided.decisionId` dolu; ardından test portu `Approved` sonucu verir → `ApplyOutcomeAsync` `200 Voided`, yeni olay yok, defterde `quarantine:{decisionId}:*` anahtarlı hareket yok; uzlaştırıcı kararı seçmez. Sabotaj: `ApplyOutcomeAsync` terminal listesinden `Voided`'ı çıkar → 409 ya da `ApplyFailed`, test kırmızı.
- [ ] AC-4 (zorunlu) — `Approved` (uygulanmamış, uygulama test engeliyle durdurulmuş) ve satırsız `ApplyFailed` kararlı iki hold: REVERSAL → ikisi de `Voided`; `reapplyDecision` → 409 `QUARANTINE_DECISION_NOT_APPLICABLE`; defter hareket sayısı yalnız REVERSAL kadar artar. Sabotaj: `ApplyAsync` uygulanabilir durumlarına `Voided` ekle → hareket postlanmaya çalışılır, test kırmızı.
- [ ] AC-5 (zorunlu) — `Voided` hold'da `createDecision`, Draft kararın `submitDecision`'ı, `startReview`, `updateHold` → 409 `QUARANTINE_HOLD_CLOSED`; hiçbir kayıt değişmez. Sabotaj: `QuarantineDecisionRules.HoldState` `Voided`'ı `Open` say → `createDecision` 201, test kırmızı.
- [ ] AC-6 (zorunlu) — Çok kayıtlı kova: hold A (4, kaynak t1) ve hold B (4, kaynak t2) aynı kovada; A'nın Release kararı uygulanır (`Decided`); t1'in REVERSAL'ı defterde 201 (DL-1'de bekçi yok) → A `Decided` kalır, `sourceReversalConflict.cause Decided`, A'da 1 `SourceReversalConflict` geçmiş satırı; B'nin durumu, miktarı, `Version`'ı değişmez. Sabotaj (ayrı ayrı): uyuşmazlık dalını kapanışa çevir → A `Voided`, kırmızı; kovaya göre eşle → B işaretlenir, kırmızı.
- [ ] AC-7 (zorunlu) — Satırı postlanmış ama `Applied` olayı yazılmamış karar (iki satırlı kısmi karar, satır 1 postlandıktan sonra test engeli) ve satırı postlanmış `ApplyFailed`: kaynağın REVERSAL'ı → `Conflict` (`cause LinesPosted`), durum değişmez. Sabotaj: postlanmış satırı olaylardan say (deftere bakma) → `Voided`, kırmızı.
- [ ] AC-8 (zorunlu) — Uygulama motoru `Voided` kararın satırını postlamaz: `ApplyAsync` durum denetimini geçtikten sonra (test engeli) kapanış commit olur → satır postlanmaz, `RecordAppliedAsync` hold'u `Decided`'a çevirmez. Sabotaj: satır öncesi yeniden okumayı kaldır → defterde `quarantine:{id}:1` anahtarlı hareket, kırmızı.
- [ ] AC-9 (zorunlu) — Inbound → `BLOCKED` 3 EA `BlockedStock` (origin `OtherInbound`) açar; REVERSAL → `status Voided`, `voided` dolu, geçmiş `SourceReversed`; bloke stok listesi `status=Open` süzgecinde görünmez. Sabotaj: gözlemciyi yalnız hold'a bağla → blocked `Open` kalır, kırmızı.
- [ ] AC-10 (zorunlu) — (a) `PendingApproval` imha kararlı blocked → `Voided` + karar `Voided`; (b) 1 EA imha uygulanmış (miktar 3 → 2) blocked, aynı kovada başka blocked kaydının 3 EA'sı varken kaynağın REVERSAL'ı 201 → `Conflict` (`cause QuantityChanged`). Sabotaj: miktar karşılaştırmasını kaldır → (b) `Voided`, kırmızı.
- [ ] AC-11 (zorunlu) — Sıralı tam bir kez: gözlemci kapatır; `SweepAsync` iki kez → 0 değişiklik; gözlemci aynı işlemle yeniden çağrılır → 0; sonuç: geçmiş `SourceReversed` 1, `central_audit_outbox`'ta bu kayıt için 1 satır, `Version` bir kez arttı. Ters sıra: gözlemci test dekoratörüyle hata atar (posting yine 201), süpürücü kapatır, gözlemci yeniden çağrılır → 0. Sabotaj: "zaten kapalı" erken dönüşünü kaldır → ikinci geçmiş satırı, kırmızı.
- [ ] AC-12 (zorunlu) — Eşzamanlı tam bir kez: gözlemci ve `SweepAsync` `Task.WhenAll` ile; sonuç AC-11 ile aynı (1 geçmiş, 1 merkezi kayıt); `TransactionRetryCounter` `quarantine` türü > 0 (çakışma dalı koştu; 0 ise test destekleyici sayılır ve yeniden yazılır). Sabotaj: beklenen `Version` olmadan değiştir → 2 geçmiş satırı, kırmızı.
- [ ] AC-13 (zorunlu) — Kapanış ↔ onay yarışı (test dekoratörü onay işlemini okumadan sonra kapanış commit olana kadar bekletir; iki sıra da koşulur): bir kararda `Voided`'dan sonra `Approved` / `Applied` olayı hiçbir koşuda yok; tam biri kazanır: ya `Voided` (defter hareketsiz) ya `Applied` + `Decided` (ardından kapanış `Conflict` yazar). Sabotaj: onay kolunun ortak yazmasını (kayıt `Version` artışı ya da eşdeğeri) kaldır → `Voided` + `Approved` birlikte, kırmızı.
- [ ] AC-14 (zorunlu) — Geç intake: intake gözlemcisi kapalıyken `CUSTOMER_RETURN` ve REVERSAL postlanır, sonra intake `SweepAsync` → hold açılmaz, geçmiş yok. Sabotaj: `IsReversedAsync` denetimini kaldır → `Open` hold açılır, kırmızı.
- [ ] AC-15 (zorunlu) — Eski veri: hold ve REVERSAL'ı `CreatedAt` 30 gün geriye alınmış fixture (gözlemci kapalıydı) → ilk `SweepAsync` turunda `Voided`. Sabotaj: beslemeyi "son N saatteki REVERSAL'lar" penceresine çevir → hold `Open`, kırmızı.
- [ ] AC-16 (zorunlu) — Kiracı ve şirket izolasyonu: (a) T2'de, `SourceMovementId`'si T1'in kaynak hareket kimliğine eşitlenmiş hold (doğrudan yazılmış) T1'in REVERSAL'ından sonra değişmez; (b) aynı kiracının LE2'sindeki aynı kurgu değişmez; (c) süpürücü çapraz kiracı beslemeden her kaydı kendi `TenantId` + `LegalEntityId` kapsamında işler (merkezi kaydın `TenantId` / `CompanyId`'si kaydınki); (d) T1 kullanıcısı T2'nin `Voided` hold'unu `GET` eder → 404. Sabotaj (ayrı ayrı): kaynak hareket aramasından kiracı süzgecini kaldır → (a) kırmızı; şirket süzgecini kaldır → (b) kırmızı.
- [ ] AC-17 (zorunlu) — İlgisiz REVERSAL (kaynak olmayan `GOODS_RECEIPT_PO` → `AVAILABLE`'ın geri alınması) → hiçbir kayıt, geçmiş satırı, merkezi kayıt; bütün kayıt `Version`'ları aynı. Sabotaj: kaydı kovayla (ürün + lot + lokasyon + durum) eşle → aynı kovadaki hold kapanır, kırmızı.
- [ ] AC-18 (zorunlu) — Açık yüklemi: `Voided` hold KPI kartlarında, `late=true` süzgecinde, `lots/{lotId}/quality-status` miktarlarında, yeniden test kuyruğunun açık hold'larında, CT-Q6 rezervasyon çakışma listesinde, MOD-0177 süreç içi recall istekçisinin açık hold listesinde (`QuarantineRecallRequester.cs:59`) ve `EnteredBefore` süzgecinde sayılmaz (her biri ayrı ölçüm); kaynak taraması `HoldStatus.Decided` karşılaştırmasını `QuarantineRecordStates` dışında bulamaz. Sabotaj: `LotQualityStatusHandler.cs:112`'yi `!= HoldStatus.Decided`'a geri çevir → kalite durumu ölçümü ve kaynak taraması kırmızı.
- [ ] AC-19 (zorunlu) — Kapanış stok yazmaz: AC-1, AC-3, AC-9 senaryolarında defter işlem sayısı yalnız REVERSAL kadar artar; `sourceModule = MOD-0175` yeni işlem yok; "0175 defter koleksiyonuna yazmaz" mimari testi yeşil. Sabotaj: handler'a `STATUS_TRANSFER` postlaması ekle → kırmızı.
- [ ] AC-20 (zorunlu) — Merkezi kayıt: her `Voided` / `Conflict` için `central_audit_outbox`'ta tam 1 satır — `RequestType` komut adı, `Category MasterData`, `Operation LifecycleTransition`, `EntityType QuarantineHold` / `BlockedStock`, `EntityId` kayıt kimliği, `SourceModule QUARANTINE-BLOCKED-STOCK`, `ActorType` Q-03 kararı (öneri `System`), `CorrelationId` = REVERSAL'ın `CorrelationId`'si, `WrittenInCommandTransaction = true`; `NoChange` → 0 satır. `CentralAuditDeclarationTests` sabiti 71 → 73. Sabotaj: komuttan `ICentralAuditDeclaration`'ı kaldır → 0 satır ve bildirim testi kırmızı.
- [ ] AC-21 (zorunlu) — K2 fail-closed: test dekoratörü enlist edilen merkezi kaydın yazımını hata attırır → kapanış işlemi geri alınır: hold `Open`, geçmiş satırı yok, karar olayı yok; dekoratör kaldırılınca sonraki `SweepAsync` kapatır. Sabotaj: merkezi kaydı işlem dışında, commit'ten sonra yaz → hold `Voided` ama kayıt yok, kırmızı.
- [ ] AC-22 (zorunlu) — Ürün izi: `SourceReversed` / `SourceReversalConflict` geçmiş satırı `ByUserId` = REVERSAL'ın `CreatedBy`'ı, `ReversalTransactionId`, `Reason` = REVERSAL numarası, `CorrelationId` = REVERSAL'ınki taşır; detay ucunun `history` dizisinde görünür; geçmiş deposu yalnız ekleme (mevcut mimari testi). Sabotaj: `ReversalTransactionId`'yi yazma → kırmızı.
- [ ] AC-23 (zorunlu) — QUARANTINE 1.4.0: `ContractSchemaValidator` yeşil; `Voided` hold / blocked / karar cevapları şemaya uyar; 1.3.0 ile karşılaştırma testi hiçbir yol, parametre, özellik, `required` listesi, durum kodu ya da mevcut enum değerinin değişmediğini ölçer; `x-changelog` 1.4.0 satırı var. Sabotaj (ayrı ayrı): `voided`'ı `required`'a ekle → kırmızı; mevcut bir `HistoryAction` değerini yeniden adlandır → kırmızı.
- [ ] AC-24 (zorunlu) — Toplu kapı: `services/Diten.InventoryService` testleri süzgeçsiz tam koşu yeşil (taban kırmızısı aynı gün `main`'de karşılaştırılır); `tests/architecture/TenantArchitecture.ArchitectureTests` `AuditTrailStandardTests` defter sayıları değişmez (yeni borç / istisna satırı yok). Sabotaj: kapı ölçüsüdür — test sınıfını değil kabul kaydının ölçümünü kırmızıya çevirir (MOD-0173-FU03 AC-32 emsali).

**DL-2 — ekran**

- [ ] AC-25 (zorunlu) — Kuyruk: `Voided` satır "Geçersiz · Kaynak geri alındı" rozetiyle `Decided` satırlarla birlikte sona sıralanır; "Karar Ver" satır eylemi DOM'da yok; geç rengi yok. Web testi + tarayıcı smoke (en, tr, ar). Sabotaj: `queue.js:68`'i `status !== 'Decided'` bırak → Karar Ver çizilir, kırmızı.
- [ ] AC-26 (zorunlu) — Detay: afiş REVERSAL numarası, tarihi ve kişiyle; Karar Ver / İncelemeye al / Kararı geri çek / Yeniden uygula düğmeleri yok; geçmişte iki yeni etiket; hareketler listesinde REVERSAL satırı. Sabotaj: `details.js:125`'i değiştirme → düğmeler çizilir, kırmızı.
- [ ] AC-27 (zorunlu) — Bloke Stok: durum süzgecinde `Voided` seçeneği; rozet; eylem yok. Sabotaj: `blocked.js:260` dalına `Voided`'ı ekleme → Disposition / Blokeyi Kaldır çizilir, kırmızı.
- [ ] AC-28 (zorunlu) — Uyuşmazlık: `sourceReversalConflict` dolu `Decided` hold ve `Closed` blocked "Kaynak geri alındı · uyuşmazlık" rozeti + neden metniyle görünür. Sabotaj: rozet koşulunu kaldır → kırmızı.
- [ ] AC-29 (zorunlu) — 7 dil: §11 anahtarları `Quarantine.{en,tr,fr,es,zh,ar,ru}.resx` hepsinde (anahtar sayıları eşit), `ar` RTL; sayfa yardımı QUARANTINEQUEUE / QUARANTINEDETAILS / BLOCKEDSTOCK adımı 7 dilde güncel; JS'de sabit kullanıcı metni yok. Sabotaj: `zh`'den bir anahtarı sil → eşitlik testi kırmızı.
- [ ] AC-30 (zorunlu) — `Views/Quarantine/{Index,Details,Blocked}.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` AÇIKÇA yazılı ve değişmemiş; `quarantine.holds.read`'siz kullanıcı UAS-001 yüzeyini görür (iskelet yok, yönlendirme yok); `verify_datatable_page.py` koşuldu ve sapmalar sahibe soruldu (kabul kaydında). Sabotaj: `Index.cshtml`'de izin denetimini atla → UAS-001 Web testi kırmızı.

**DL-3 — defter bekçisi (yalnız Q-06 = A)**

- [ ] AC-31 (zorunlu) — Uyuşmazlığa yol açacak REVERSAL reddedilir: AC-6 (Decided), AC-7 (satır postlanmış), AC-10 (b) (miktar değişmiş) ve `Approved` / `ApplyFailed` kararlı kaydın kaynağının REVERSAL'ı → 422 ek kod, `details.recordNumber`; defter işlem sayısı, bakiye, pozisyon değişmez; `inventory_audit`'te `Outcome Failed` ret satırı. Sabotaj: bekçi her zaman `Allow` dönsün → 201, kırmızı.
- [ ] AC-32 (zorunlu) — MOD-0175 hareketlerinin REVERSAL'ı reddedilir: "Karantinaya Al" `STATUS_TRANSFER`, "Stoğu bloke et", karar satırı (Release), Rework, Numune `LOCATION_TRANSFER` için REVERSAL → 422; stok kalite kararı dışında `AVAILABLE`'a dönmez. Sabotaj: `SourceModule = MOD-0175` kuralını kaldır → manuel karantinanın REVERSAL'ı 201, stok `AVAILABLE`, kırmızı.
- [ ] AC-33 (zorunlu) — Bekçi kapanabilir kayıtları engellemez: `Open` ve `PendingApproval` kayıtların kaynağının REVERSAL'ı 201 → DL-1 kapanışı (AC-1, AC-3 yeniden koşulur); MOD-0173 kaynaklı ilgisiz hareketin REVERSAL'ı 201. Sabotaj: `SourceMovementId`'si olan her kaynağı reddet → AC-1 kırmızı.
- [ ] AC-34 (zorunlu) — Fail-closed: bekçinin karantina okuması hata atar → 503 `INVENTORY_UNAVAILABLE`, hiçbir şey yazılmaz. Sabotaj: hatayı yakala ve `Allow` dön → 201, kırmızı.
- [ ] AC-35 (zorunlu) — Kiracı kapsamı: bekçi yalnız postlayanın kiracı + şirketindeki kaydı okur; T2'deki kurgulanmış `Decided` kayıt T1'in REVERSAL'ını reddettirmez. Sabotaj: bekçi okumasından kiracı süzgecini kaldır → T1 422 alır, kırmızı.
- [ ] AC-36 (zorunlu) — INVENTORY ek sürümü: ek kod `InventoryErrorCodes.All`'da ve sözleşmede (`postMovement` 422 açıklaması + kod listesi), `x-changelog` ek sürüm satırı; `ContractSchemaValidatorTests`, `InventoryBundleDescriptionNoteTests` yeşil; 1.5.0'a göre karşılaştırmada yalnız ekleme. Sabotaj: kodu katalogdan çıkar → katalog testi kırmızı.

## 17. Test Expectations

- **Birim:** `SourceReversalRules` (§8.3 tablosunun her satırı, tablo güdümlü), `QuarantineRecordStates`, `StateOf` `Voided` eşlemesi.
- **Entegrasyon (gerçek Mongo replika seti, `InventoryApiFactory`; testler kendi replika setini açar):** AC-1…AC-23, AC-31…AC-35. Süpürücüler test
  ayarlarında kapalı, `SweepAsync` elle.
- **Eşzamanlılık / yarış:** AC-12 (`Task.WhenAll` + `TransactionRetryCounter`), AC-13 ve AC-8 (test dekoratörü / engel; üretim koduna test
  kancası eklenmez — dekoratör `InventoryApiFactory` DI'ında).
- **Kiracı izolasyonu:** AC-16, AC-35. **Denetim:** AC-20, AC-21, AC-22; `CentralAuditDeclarationTests` sabiti 71 → 73.
- **Sözleşme:** AC-23, AC-36; Prism mock smoke QUARANTINE 1.4.0.
- **Web:** `frontend/Diten.Web.Tests/Quarantine/QuarantineWebTests.cs` genişler (AC-25…AC-30); tarayıcı smoke en / tr / ar.
- **Smoke (E3, DL-1 + DL-2):** Gateway 5000 üzerinden gerçek login: manuel `CUSTOMER_RETURN` → kuyrukta hold → S-2 "Reversal" → kuyrukta "Geçersiz".
- **Taban kanıtı:** MVP-6 turu F-2 ve MVP-1 turu 2026-11-02 senaryosu bu FU kabulünde yeniden koşulur (aynı adımlar, hold `Voided`).
- Tam paket kuralı: servis dokunulduğunda süzgeçsiz tam koşu (AC-24).

## 18. Ready-for-dev Checklist

- [x] Golden Reference Compact backend düzeni ve Web Compact dosya seti okundu; yeni dosyalar Golden düzeninde, ebeveynin sapması kayıtlı (§10)
- [x] Frontmatter zorunlu alanlar (`service`, `shell: tenant`, `golden_reference: compact`, `data_mode: server`, `screens`, `entity_base`, `form_field_count`)
- [x] Layout & Shell Contract: `_LayoutTenantShell` açıkça ve AC-30'da test edilebilir (§9)
- [x] Backend File Convention: `Handlers/CommandHandlers/`, suffix'siz handler / validator adları (§10)
- [x] Frontend File Contract: ebeveyn ekranlarına ekleme; `_CreateEditOffcanvas` yok (§11)
- [x] Validation Rules, Failure Path (15 senaryo: tekrar, eksik, yetkisiz, eşzamanlılık, kiracı dışı, K2), Authorization (yeni anahtar yok), Gateway (değişmez)
- [x] Platform lookup kararı: uygulanmaz (kiracı modülü; lookup yok)
- [x] Acceptance criteria ölçülebilir ve sabotajlı — 36 AC'nin her birinde adlı sabotaj (AC-24 kapı ölçüsü)
- [x] Audited Events: modülün mevcut 14 yazma komutu + 2 yeni komut (§21)
- [x] Kimlik ön kontrolü çıkış 0 (`--check-id … --parent MOD-0175`)
- [ ] Sahip kararları Q-01…Q-09 (Beste); MVP-6 bilgisi Q-10 (Natig); registry satırı Q-11 (CT)
- [ ] XMC-001 izinleri: Q-03 = A ise `InventoryCentralAuditScope.cs` (BL-501 bağdaştırıcısı); Q-06 = A ise MOD-0173 `InternalMovementPostingService.cs` + `InventoryErrorCodes.cs` + INVENTORY sözleşmesi
- [ ] Plan satırı (yetenek kimliği, plan MVP) plan sahibinden (§22)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) + CT-APR-01 onay satırı (risk HIGH)
- [ ] `status: ready-for-dev` (şimdi `draft`)

## 19. Implementation Notes

### 19.1 Okunan kaynaklar

`AGENTS.md` Çekirdek (`.claude/rules/agents-core.md`), `.antigravity/rules/module-pack-standard.md` (§2, §7, §10.1, §16), `audit-trail-standard.md`
(§1–§6), `tests/architecture/audit-ledger/Diten.InventoryService.md`, `domain-config.md`, `module-id-registry.md` (satır 306), ebeveyn
`MOD-0175-quarantine-blocked-stock.md` (§1–§18), biçim örneği `MOD-0173-FU03-inventory-lot-location-reservation.md`, Golden Reference Compact
(backend `Features/GoldenReferenceCompact/`, Web `Views/DevEnablement/GoldenReferenceCompact/`), `docs/roadmap/plans/mvp6-integration-wp-p1-01/LOCAL-LIVE-RUN-2026-11-01.md`
ve `DECISION-DRAFT.md`, sözleşmeler `quarantine.openapi.yaml` 1.3.0 / `inventory-bundle.openapi.yaml` 1.5.0, §19.2'deki kod dosyaları.
**Okunmadı:** `execution/portfolio/master-development-plan.md` ve plan dosyası (plan satırı verilmedi → §22 "plan satırı yok"); Blueprint xlsx
doğrudan açılmadı (kimlik `verify_module_id.py` ile ölçüldü). `views-organization.md`, `handler-design.md`, `routes.md`, `entity-base-template.md`
bu oturumda açılmadı — ebeveynin aynı kararları (layout, EntityBase, rota) değişmeden alındı; inceleme bunları doğrulamalıdır.

### 19.2 Ölçülen gerçekler (`e3cbbca4d`, salt okuma)

| # | Gerçek | Kanıt |
|---|---|---|
| G-1 | Intake REVERSAL'ı ve MOD-0175'in kendi hareketini dışlar; yalnız inbound → QUARANTINE / QI hold, → BLOCKED blocked (`OtherInbound`) açar; kayıt `SourceMovementId = transactionId` taşır | `QuarantineIntake.cs:41-57, 108, 139-140` |
| G-2 | REVERSAL zaten `Inbound` sayılmaz; `:44` koşulu ikinci emniyettir | `MovementDirectionRules.cs:20-31` |
| G-3 | Kaynağı geri alınmış kaydı kapatan / azaltan kod **yok**: depoda `HoldStatus` / `BlockedStatus` yazan yerler yalnız intake, karar motoru, gönderim ve inceleme yolları; hiçbiri REVERSAL'a bakmaz | `grep HoldStatus\.\|BlockedStatus\.` (§8.5 listesi) |
| G-4 | REVERSAL tam miktarlıdır: miktar ve birim aslına eşit olmalı; REVERSAL geri alınamaz; yön aslının tersidir | `ReversalRules.cs:36-56`; `MovementDirectionRules.cs:120-126`; `InternalMovementPostingService.cs:265-266` |
| G-5 | Bir hareket bir kez geri alınır: ön denetim `ALREADY_REVERSED` 422, tekil indeks `ux_tenant_le_reversestransactionid` | `InternalMovementPostingService.cs:253-256, 270-272`; `InventoryIndexConfiguration.cs:20, 52` |
| G-6 | Her commit edilen hareket (REVERSAL dahil) gözlemcilere gider; gözlemci hatası loglanır, posting durur değil | `InternalMovementPostingService.cs:155-159, 201-214`; `IPostedMovementObserver.cs:5-13` |
| G-7 | `HoldStatus` yalnız `Open`, `DecisionPending`, `Decided`; `BlockedStatus` `Open`, `Closed`, `SampleRetained`; iptal / geçersiz yok | `QuarantineEnums.cs:29-34, 112-117`; sözleşme `quarantine.openapi.yaml:400, 407` |
| G-8 | **Açık hold'da karar hiç alınamaz:** oluşturma ve gönderme V7 kova denetimini yapar (`onHand` 0 < 4) → 409 `QUARANTINE_STOCK_CHANGED`; INSUFFICIENT_STOCK'a varılmaz | `DecisionHandlers.cs:48-49, 181, 376`; `QuarantineDecisionRules.cs:41, 131-134` |
| G-9 | **Karar aşamasındaki hold kilitlenir:** onaydan sonra motor Release için `STATUS_TRANSFER` QUARANTINE/QI → AVAILABLE, Reject/Block için → BLOCKED postlar (`fromStockStatus` = kova durumu); defter 422 `INSUFFICIENT_STOCK` → `ApplyFailed`; hold `DecisionPending` + `ActiveDecisionId` kalır; "Yeniden uygula" aynı retle biter; geri çekme yalnız `PendingApproval`'da | `QuarantineStockEffects.cs:86-91`; `QuarantineApplyEngine.cs:109-119, 144-166`; `QuarantineMovementPoster.cs:103-107`; `InternalMovementPostingService.cs:163-170`; ebeveyn V21 |
| G-10 | **Değişmez kırığı:** aynı kovada başka kaydın stoğu varsa V7 geçer ve hayalet kaydın kararı o stoğu taşır (defter kova düzeyindedir, kayıt düzeyinde değil); uygulanmış karardan sonra REVERSAL da başka kaydın stoğunu çıkarabilir | `QuarantineDecisionRules.cs:131-134` (kova bakiyesi); MVP-6 R1-b yalnız tek kayıtlı kovada reddi gösterir |
| G-11 | Onay sonucu işlem dışında okunur, `Approved` yeniden okunmadan eklenir; durum "son olay kazanır"; tek seferlik anahtar olay türü adıdır (farklı türler çakışmaz) | `QuarantineApplyEngine.cs:411-412, 433-438, 476-489`; `QuarantineFacts.cs:101-120`; `QuarantineSupport.cs:131`; `QuarantineIndexConfiguration.cs:15, 27, 72` |
| G-12 | "Açık" 13 sunucu yerinde `Decided` karşılaştırmasıyla yazılı; Web'de 5 yerde | `DecisionHandlers.cs:29`; `QuarantineDecisionRules.cs:107`; `LotQualityStatusHandler.cs:112`; `QueryHandlers.cs:149, 155, 159`; `RetestHandlers.cs:70`; `ReviewHandlers.cs:41`; `StockActionHandlers.cs:190`; `UpdateHoldHandler.cs:87`; `QuarantineMapping.cs:59`; `QuarantineRecallRequester.cs:59`; `QuarantineRepository.cs:108`; JS `decide.js:162`, `details.js:125`, `quarantine-ui.js:96`, `queue.js:68, 120` |
| G-13 | Intake süpürücüsü 72 sa geriye, 60 sn yerleşme payıyla, yalnız inbound türlerine bakar | `QuarantineLedgerReads.cs:128-146, 153-161, 187-214` |
| G-14 | Intake (gözlemci + süpürücü) MediatR komutu değildir; işlem enlist eder ama işaretli komut yokken merkezi kayıt yazılmaz — kayıt açılışı bugün yalnız karantina geçmişinde izlenir | `QuarantineRepository.cs:214-226`; `CentralAuditDeclarationTests.cs:73-82` notu |
| G-15 | Inventory merkezi kayıt kapsamı `ActorType`'ı yalnız token'dan okur; `System` üretmez | `Api/CentralAudit/InventoryCentralAuditScope.cs:54-59`; `CentralAuditActorTypes.System` var (`ICentralAuditScope.cs:40`) |
| G-16 | Inventory yazma komutu sayısı sabiti 71 | `CentralAuditDeclarationTests.cs:37` |
| G-17 | S-2 "Reversal" düğmesi yalnız `sourceModule = MOD-0173` satırında (yalnız JS); sunucu her aslı kabul eder → MOD-0175 hareketinin REVERSAL'ı API'den mümkün (DEC-INV-08 atlaması) | `StockMovements/index.js:13-15`; `ReversalRules.cs:19-56` (kaynak modül denetimi yok) |
| G-18 | REVERSAL üreticileri: MOD-0186 geç yazan, MOD-0142 `reverseGrn`, S-2 ve "Düzelt" | `ReturnLateWriterPolicy.cs:13, 37-43`; `ReverseGrnHandler.cs:11-13, 56-59`; `form.js:85` |
| G-19 | Resx eşitliği: `Quarantine.*.resx` 7 dilde 451 anahtar | `grep -c "<data "` |
| G-20 | Liste doğrulayıcısı bu modülün dosya adlarını bulmuyor | `verify_datatable_page.py . --area Quarantine --module Quarantine --format gaps` → 2–6 numaralı "Missing" |

**Kanıt (kusurun canlı izi):**
- MVP-6 yerel turu (`docs/roadmap/plans/mvp6-integration-wp-p1-01/LOCAL-LIVE-RUN-2026-11-01.md:23-24`): F-2 "the REVERSAL takes stock out of QUARANTINE
  (4 → 0) but hold `KRN-2026-0001` stays `Open`, quantity 4"; R1-b "After a partial move, the same REVERSAL answers 422 `INSUFFICIENT_STOCK`
  (available 1, requested 4); balance and hold unchanged".
- MVP-1 turu 2026-11-02: `CUSTOMER_RETURN` `INV-2026-0000015` → `KRN-2026-0004` (2 EA) — **CT bildirimi; depoda ham kayıt yok, bu oturumda
  ölçülmedi.** Ayrım notu: ebeveyn §19 FIX-QUI1 notundaki "önizlemede KRN-2026-0004" başka bir veritabanının sayacıdır (numara kiracı + yıl
  sayacı), aynı kayıt değildir; `INV-2026-0000015` de `mvp1-fix-live3-evidence-2026-10-20.md:116`'da başka bir koşuya aittir.

### 19.3 Varsayımlar

- A-1: REVERSAL'ın `ReversesTransactionId`'si her zaman doludur (`InternalMovementPostingService.cs:265-266, 310`) — kural buna dayanır.
- A-2: Bir kaydın `SourceMovementId`'si bir kez yazılır, değişmez (intake / karar motoru / stok eylemleri yalnız oluştururken yazar).
- A-3: Uyuşmazlık durumunda doğru stok düzeltmesi QA'nın elle yaptığı yeni 0175 eylemi ya da defter hareketidir; sistem tahmin etmez.

### 19.4 Açık karar soruları

| # | Soru | Seçenekler / öneri | Sahip | Hedef |
|---|---|---|---|---|
| Q-01 | Terminal durum adı ve blocked için ayrı değer mi? | **A (öneri):** hold ve blocked için yeni `Voided` (neden `SourceReversed`); **B:** blocked'ta `Closed` + `closeReason` alanı | Beste | DL-1 önce |
| Q-02 | Karar aşamasındaki kayıt: satırı postlanmamış her karar (PendingApproval / Approved / satırsız ApplyFailed) kapanışla `Voided` olsun mu; Workflow örneği? | **A (öneri):** evet; Workflow örneği açık kalır, sonradan gelen onay etkisiz, WorkCenter bağlantısı "Geçersiz" gösterir. **B:** karar aşamasındakiler `Conflict` olur, karar sahibi geri çeker. **C:** A + karar sahibinin bir sonraki okumasında görevi kendi token'ıyla iptal (FU) | Beste | DL-1 önce |
| Q-03 | Merkezi kaydın aktörü | **A (öneri, CT brifi):** `ActorType System`, `ActorId` sabit `00000000-0000-0000-0000-000000000175` (`QuarantineIntakeSweeper.QuarantineSweeperActor`, `QuarantineLedgerReads.cs:217`); geri alan kullanıcı geçmiş satırında ve `CorrelationId` ile defter denetiminde. `InventoryCentralAuditScope.cs`'e sistem kipi gerekir (BL-501 bağdaştırıcısı, XMC-001). **B:** `TenantUser` = REVERSAL'ı postlayan (bugünkü recall süpürücüsü / sonuç tüketicisi deseni, kod değişikliği yok) | Beste (BL-501 merkezi) | DL-1 önce |
| Q-04 | Uygulanmış / satırı postlanmış karardan sonra geri alma | **A (öneri):** DL-1 `Conflict` işareti + geçmiş, otomatik düzeltme yok; önlem DL-3. **B:** yalnız işaret, DL-3 yok (kalan risk yazılı kabul) | Beste | DL-1 önce |
| Q-05 | Operatör için elle kapatma yolu | **A (öneri):** bu FU'da yok — geri alma kaynaklı her kayıt otomatik kapanır, eski kayıtlar ilk süpürücü turunda; REVERSAL dışı hayalet kaynakları FU-0175-FU01-OUTBOUND'da. **B:** `quarantine.holds.void` (Tier-3, tehlikeli, gerekçe ≥ 10, e-imza politika satırı `quarantine.hold.void`, iş akışı yok çünkü stok değişmez) | Beste | DL-1 önce |
| Q-06 | DL-3 defter bekçisi bu FU'da mı? (MOD-0175 hareketlerinin REVERSAL'ı da reddedilir) | **A (öneri):** evet, DL-3 — DEC-INV-08 atlaması (G-17) ve değişmez kırığı (G-10) yalnız ön koşulla kapanır; XMC-001 MOD-0173 + INVENTORY ek kodu. **B:** ayrı takip, bu FU yalnız DL-1 + DL-2 | Beste (MOD-0173 sahibi aynı) | DL-1 kabulünden önce |
| Q-07 | Sürümler | QUARANTINE **1.4.0** tek adım (DL-1). INVENTORY ek sürümü (DL-3) MOD-0173-FU03 1.7.0'dan sonraki ilk boş sürüm; FU03 dondurmadan önce DL-3 açılırsa sıra CT'de | Beste / MVP-1 CT | DL-3 önce |
| Q-08 | Kuyruk varsayılanı: `Voided` satırlar görünür mü? | **A (öneri):** görünür, `Decided` gibi sona sıralı, KPI dışı (varsayılan liste değişmez — sözleşme eklemeli kalır). **B:** varsayılan gizli, süzgeçle görünür (varsayılan davranış değişikliği — eklemeli değil) | Beste | DL-2 önce |
| Q-09 | Geç intake | **A (öneri):** kaynağı geri alınmışsa kayıt açılmaz (§8.4). **B:** açılır ve aynı turda kapanır (iz bırakır, kuyrukta gürültü) | Beste | DL-1 önce |
| Q-10 | MVP-6 etkisi | (1) MOD-0186 tur satırı R1-a'nın beklenen sonucu "hold `Open`" → "hold `Voided`" olur; (2) Q-06 = A ise MOD-0186 geç yazanının, kaydı karar verilmiş iadenin REVERSAL'ı 422 alır → mevcut `ReversalFailed:<kod>` yolu (`ReturnLateWriterPolicy.cs:43`) — kabul mü? MOD-0183'ün REVERSAL'ları (`GOODS_ISSUE_SALES` telafisi) etkilenmez | Natig (MVP-6 CT) | DL-1 / DL-3 kabulünden önce |
| Q-11 | Registry satırı `MOD-0175-FU01` | CT ekler (bu paket kayıt dosyasına yazmaz; MOD-0173-FU03 CT-01 emsali) | MVP-1 CT | onaydan önce |

Ayrıca bilgi: MOD-0142 `reverseGrn` (Procurement) DL-3'ten aynı biçimde etkilenir; sahibine bilgi notu CT'den.

## 20. Follow-up Items

- **FU-0175-FU01-OUTBOUND** — REVERSAL dışı yollarla tutma statüsünden çıkan stok (QUARANTINE / QI / BLOCKED'dan MOD-0175 olmayan `SCRAP`,
  `WRITE_OFF`, `COUNT_LOSS`, transfer, `STATUS_TRANSFER` — `MovementDirectionRules.cs:62-107` yapısal olarak izin verir) de hayalet kayıt doğurur;
  ayrı karar (bekçi ya da kova uzlaştırma raporu).
- **FU-0175-FU01-INTAKE-AUDIT** — intake'in kayıt açılışı merkezi kayıt yazmıyor (G-14); intake'i iç MediatR komutuna taşımak (AUD-001 §2 "yeni
  kodda yazma yolu MediatR komutu").
- **FU-0175-FU01-WFCANCEL** — Q-02 = C seçilirse: `Voided` kararın Workflow görevini karar sahibinin token'ıyla iptal.
- **FU-0175-FU01-LAYOUT** — ebeveynin Quarantine feature klasörünü Golden düzenine taşımak (§10 sapması).
- **FU-0175-FU01-DTVERIFY** — `verify_datatable_page.py` için Quarantine ekranlarının modül eşlemesi (G-20).

## 21. Audited Events

Servisin denetim altyapısı **bugün var:** `tests/architecture/audit-ledger/Diten.InventoryService.md` `## İzler` →
`inventory-merkezi-iletim | b | işaret | ICentralAuditDeclaration`; `RequireEnlistment = true`; K2 borcu yok; MOD-0175 komutları
`InventoryCentralAudit.ForQuarantine` ile `Category MasterData`, `SourceModule QUARANTINE-BLOCKED-STOCK` taşır. Ebeveyn pakette bu bölüm yok;
geçiş kuralı (standart §10.1) gereği tablo modülün **mevcut** yazma komutlarını da kapsar. Merkezi kayıt gövde taşımaz; değerler ürün içi
karantina geçmişinde (`quarantine_history`, yalnız ekleme, kaydın detay ekranında `quarantine.holds.read` ile okunur) ve yerel `inventory_audit`'te.
K2: karantina kaydı GxP kaydıdır → merkezi kayıt iş verisiyle aynı Mongo işleminde (`CentralAuditTransaction.EnlistAsync`), yazılamazsa işlem durur.

| Komut | `Operation` / `EntityType` | Yol | Önceki / sonraki ya da değişen alanlar (ürün izinde) | İstisna |
|---|---|---|---|---|
| `ApplySourceReversalToHoldCommand` (**yeni**) | `LifecycleTransition` / `QuarantineHold` | b (K2) | Void: `Status` Open/DecisionPending → Voided, `Voided.*`, `ActiveDecisionId`; karar olayı `Voided`; geçmiş `SourceReversed` (`ReversalTransactionId`). Conflict: `SourceReversalConflict.*`; geçmiş `SourceReversalConflict`. NoChange → kayıt yok | — |
| `ApplySourceReversalToBlockedCommand` (**yeni**) | `LifecycleTransition` / `BlockedStock` | b (K2) | aynı, blocked alanlarıyla | — |
| `QuarantineStockCommand` (mevcut) | `Create` / `QuarantineHold` | b | değişmez | — |
| `BlockStockCommand` (mevcut) | `Create` / `BlockedStock` | b | değişmez | — |
| `UpdateHoldCommand` (mevcut; `Voided`'da 409) | `Update` / `QuarantineHold` | b | değişmez; ret merkezi kayıt yazmaz (mevcut) | — |
| `StartReviewCommand` (mevcut; `Voided`'da 409) | `LifecycleTransition` / `QuarantineHold` | b | değişmez | — |
| `CompleteBlockedReviewCommand` (mevcut) | `LifecycleTransition` / `BlockedStock` | b | değişmez | — |
| `CreateDecisionCommand` (mevcut; `Voided`'da 409) | `Create` / `QuarantineDecision` | b | değişmez | — |
| `SubmitDecisionCommand` (mevcut; `Voided`'da 409) | `LifecycleTransition` / `QuarantineDecision` | b | değişmez | — |
| `ReapplyDecisionCommand` (mevcut; `Voided` kararda 409) | `Execute` / `QuarantineDecision` | b | değişmez | — |
| `RefreshDecisionCommand` (mevcut; `Voided` kararda sonuç etkisiz) | `Execute` / `QuarantineDecision` | b | değişmez | — |
| `WithdrawDecisionCommand` (mevcut) | `LifecycleTransition` / `QuarantineDecision` | b | değişmez | — |
| `SaveSettingsCommand` (mevcut) | `Update` / `QuarantineSettings` | b | değişmez | — |
| `CreateBlockReasonCommand` (mevcut) | `Create` / `BlockReason` | b | değişmez | — |
| `UpdateBlockReasonCommand` (mevcut) | `Update` / `BlockReason` | b | değişmez | — |
| `PrepareWorkflowTemplateCommand` (mevcut) | `Create` / `WorkflowTemplate` | b (`ICentralAuditOutsideTransaction`) | değişmez | — |
| `PostMovementCommand` (MOD-0173; DL-3 REVERSAL reddi) | `Create` / `InventoryTransaction` (mevcut) | b (K2) | başarıda değişmez; bekçi reddinde merkezi kayıt yok, yerel `inventory_audit` ret satırı `Outcome Failed` (ErrorCode, OriginalTransactionId, RecordNumber) | — |
| Intake gözlemcisi / süpürücüsü (MediatR dışı, mevcut) | — | — | `quarantine_history` `Quarantined` / `Blocked` (G-14) | MediatR dışı yazma yolu: ölçüm görmez (AUD-001 §2, §9); bu FU yalnız geç intake reddini ekler (yazma yok); taşınması FU-0175-FU01-INTAKE-AUDIT |
| `QuarantineSourceReversalSweeper` / gözlemci (yeni, MediatR dışı tetik) | — | — | kendisi yazmaz; yazmayı yukarıdaki iki komut yapar | İ2 — tetikleyicidir; ürettiği her değişiklik kendi komutuyla (yol b) denetlenir |

Kayda yazılmayan alanlar: token, gizli anahtar; kişisel veri yok (kullanıcı kimliği dışında).

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek: kaynak geri alma kapanışı + uyuşmazlık işareti + gözlemci / süpürücü + açık yüklemi + denetim + QUARANTINE 1.4.0 | Owned Objects, Entity Fields, Repo Scope, Runtime Constraints (8.1–8.7, 8.9), Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | ekran: kuyruk / detay / bloke stok durumu, uyuşmazlık rozeti, 7 dil, sayfa yardımı | Layout & Shell Contract, Frontend File Contract | E3 | plan kimliği yok | plan satırı yok |
| DL-3 | defter bekçisi: uyuşmazlık doğuracak ve MOD-0175 kaynaklı REVERSAL reddi + INVENTORY ek kodu (yalnız Q-06 = A; XMC-001 MOD-0173) | Runtime Constraints (8.8), Repo Scope, Protected Paths, Dependencies, Audited Events | E4 | plan kimliği yok | plan satırı yok |

Kabul ölçütleri: DL-1 → AC-1…AC-24; DL-2 → AC-25…AC-30; DL-3 → AC-31…AC-36. DL-2 ve DL-3 DL-1'e dayanır. Plan satırı bu taslağa verilmedi
(plan dosyası depoda değil) → değer uydurulmadı; plan sahibinden alınıp yazılır (§18). Q-06 = B olursa DL-3 satırı ve AC-31…AC-36 "isteğe bağlı"
değil, ayrı takip paketi olarak çıkar (AC'ler silinmez, durumları CT kaydında "kapsam dışı" yazılır).

## 23. Page Help (UXH-001)

DL-2 yeni sayfa açmaz; mevcut üç sayfanın yardımı güncellenir (7 dil, `PageHelpResource.*.resx`):
- `Help.QUARANTINEQUEUE` — durumlar adımına "Geçersiz · Kaynak geri alındı: kaynak stok hareketi geri alındığı için kayıt kapandı; karar gerekmez".
- `Help.QUARANTINEDETAILS` — "Kaynak geri alındı · uyuşmazlık: stok bu kaydın dışında değişti; QA kontrol eder" açıklaması.
- `Help.BLOCKEDSTOCK` — durum süzgecindeki "Geçersiz" seçeneği.
Tur yok (yeni sayfa yok).
