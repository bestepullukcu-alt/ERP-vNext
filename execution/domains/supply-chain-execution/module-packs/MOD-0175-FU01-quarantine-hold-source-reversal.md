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
risk_reason: "Stok değişmezi (açık tutma kaydı miktarı ≤ kova bakiyesi) + GxP kaydı denetimi (AUD-001 K2) + sözleşme (QUARANTINE 1.4.0; DL-3'te INVENTORY 1.8.0 ek hata kodu) + ürün kodu yolu + iki XMC-001 (MOD-0173 defter, MOD-0173 / BL-501 denetim kapsamı bağdaştırıcısı)"
status_note: "draft r4 (2026-11-03) — bağımsız inceleme 01 tur 2 (r3 `54d736e1`, KOŞULLU, 0 ENGEL / 1 ORTA / 4 DÜŞÜK) bulguları F-1…F-5 tek geçişte kapatıldı, CT kararı MVP-1 CT 2026-11-03 (hepsi kabul) — §19.7. Kod yazılmaz: r4'ün kendi CT-APR-02 incelemesi ve CT-APR-01 onay satırı gerekir. DL-3 Web dosyaları için XMC-001 izni: sahip izni bekleniyor (DL-3 Web adımının kapısı, ready-for-dev önkoşulu değil). Önceki: draft r3 (2026-11-03) — bağımsız inceleme 01 tur 1 (r2 `ae3fed7d`, UYGUN DEĞİL, 2 ENGEL / 3 ORTA / 8 DÜŞÜK) bulguları F-1…F-13 tek geçişte kapatıldı, CT kararı MVP-1 CT 2026-11-03 (hepsi kabul) — §19.6. Kod yazılmaz: r3'ün kendi CT-APR-02 incelemesi ve CT-APR-01 onay satırı gerekir. Önceki: draft r2 (2026-11-03) — sahip kararları Q-01…Q-06, Q-08, Q-09 (Beste, 2026-11-03, \"hepsi öneri gibi\") ve CT kararları Q-07 (QUARANTINE 1.4.0; INVENTORY 1.8.0, FU03 1.7.0'dan sonra), Q-11 (registry satırını CT ekler) işlendi; iki XMC-001 izni (InventoryCentralAuditScope.cs; MOD-0173 defter bekçisi dikişi) yazıldı — §19.5. Açık kalan: Q-10 (MVP-6 CT Natig; DL-3 kabul kapısı, ready-for-dev önkoşulu değil), registry satırı (CT), plan satırı. Kod yazılmaz: CT-APR-02 bağımsız inceleme (r2'nin kendi incelemesi) ve CT-APR-01 onay satırı gerekir; ready-for-dev'i inceleme sonrası CT yapar. r1 (2026-11-02): MVP-6 yerel turu F-2 (KRN-2026-0001) ve MVP-1 turu 2026-11-02 (KRN-2026-0004) bulgusu — kaynak hareketin REVERSAL'ı tutma kaydını kapatmıyor, kayıt sıkışıyor."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-02
target: 2026-11-30
form_field_count: 9
dependencies: [MOD-0175, MOD-0173, MOD-0174, QUARANTINE-BUNDLE-v1.3.0, INVENTORY-BUNDLE-v1.5.0, BL-501 (central audit), MOD-0173-FU03 (yalnız DL-3 — INVENTORY 1.7.0 sonrası 1.8.0), MOD-0186 (yalnız DL-3 kabul kapısı — Q-10 MVP-6)]
---

# MOD-0175-FU01 — Quarantine hold closure on source reversal

> **Kimlik (DCP-002) — ölçüldü 2026-11-02, dal `chore/ct/mod0178-dl1-accept-01` @ `e3cbbca4d`:**
> `python3 -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0175-FU01 --name "Quarantine hold closure on source reversal" --parent MOD-0175`
> → çıkış **0** ("OK MOD-0175-FU01: proven against Blueprint/registry"). Boşluk ölçümü: `execution/registries/module-id-registry.md`'de
> MOD-0175'in tek satırı var (satır 306, çocuk satırı yok); `execution/`, `docs/`, `.antigravity/` altında `MOD-0175-FU01` metni 0;
> çalışma ağacında `MOD-0175-FU` geçen `.md` / `.tsv` dosyası 0; `git log --all -S "MOD-0175-FU01"` (487 ref) boş → **FU01 ilk boş numara**.
> Mükerrer iş keşfi CT tarafından koşuldu: `verify_module_id.py . --discover "quarantine reversal"` → yalnız gateway 5067 karantina rotası
> (üretici, çakışma değil); paket / registry / görev bulgusu 0 (bu oturumda yeniden koşulmadı). `FU-0175-*` adlı takip maddeleri (ör.
> `FU-0175-WF-SOD`) MOD kimliği değildir. **Registry satırını CT ekler** (Q-11, CT kararı 2026-11-03); bu paket kayıt dosyasına yazmaz.
>
> **r2 (2026-11-03):** sahip kararları Q-01…Q-06, Q-08, Q-09 (Beste, "hepsi öneri gibi") ve CT kararları Q-07, Q-11 işlendi; iki XMC-001
> izni kapsamıyla yazıldı (§5). Kararların hepsi r1'in önerisidir; değişen, koşullu yazılmış gövdenin ("… seçilirse", "… = A ise")
> kesinleşmesidir — r1'in önerdiği davranış değişmez. Ek olarak: INVENTORY sürümü 1.8.0 (§8.8, AC-36 tabanı 1.7.0), sistem aktörü kipinin
> sınırı AC-20'de üç yol ve adlı sabotajla ölçülür, iki donmuş sözleşme kopyası Repo Scope'a girdi (`quarantine-v1.3.0`, `inventory-bundle-v1.7.0`),
> §8.2'deki yanlış "Q-08" atfı Q-05'e düzeltildi. Kayıt §19.5; açık kalan Q-10 (MVP-6 CT, DL-3 kabul kapısı). Künye `draft` / `pending`:
> r2'nin kendi CT-APR-02 incelemesi ve CT-APR-01 onay satırı gerekir.
>
> **r3 (2026-11-03):** bağımsız inceleme 01 tur 1 (`docs/records/reviews/MOD-0175-FU01/2026-11-03-ae3fed7d.md`, hedef r2 `ae3fed7d…`) 13 bulgusu
> tek geçişte kapatıldı (S-101; CT kararı MVP-1 CT 2026-11-03, hepsi kabul). Davranış değişiklikleri: (1) `Voided` yalnız **intake kökenli**
> kayıtlara uygulanır; MOD-0175'in kendi hareketinden doğan kayıtta REVERSAL uyuşmazlık işareti üretir (`Cause = OwnMovementReversed`; §8.3);
> (2) karar `Submitted` penceresi kural tablosunda; kapanış ile karar yazarları kaydın `Version`'ını ortak yazma kilidi olarak paylaşır ve
> `Voided` emici son durumdur (§8.6); (3) süpürücü terminal kayıtları da ayrı imleçle tarar, defter sorgusu kiracı + şirket önekli (§8.2);
> (4) DL-1 uyuşmazlık / geç-kapanış ölçütleri DL-3 etkin iken de geçerli kurguyla yazıldı (§16 kurgu kuralı); (5) DL-3'e Web hata metni (7 dil).
> AC numaraları r2 ile aynı kalır; yeni ölçütler sona eklenir (AC-37 DL-1, AC-38 DL-3). Kapanış tablosu §19.6. Künye `draft` / `pending`.
>
> **r4 (2026-11-03):** bağımsız inceleme 01 tur 2 (`docs/records/reviews/MOD-0175-FU01/2026-11-03-54d736e1.md`, hedef r3 `54d736e1…`) beş
> bulgusu tek geçişte kapatıldı (S-101; CT kararı MVP-1 CT 2026-11-03, hepsi kabul). Davranış değişiklikleri: (1) W5 / W6 `Voided` kararda
> **hiçbir şey yazmaz**; satırı postlanmışsa uyuşmazlık işaretini uygulama motoru ayrı iç komutla (`ApplySourceReversalTo{Hold,Blocked}Command`,
> kendi merkezi kaydı, aktör `System`) yazdırır, kaçarsa süpürücünün yeni **geçersiz beslemesi** yazar (§8.2, §8.3 R0a, §8.6); (2) W3 onay
> işlemine W2 / W4 ile aynı ≤ 3 deneme (§8.6); (3) W1 satırı bugünkü koda hizalandı (`QUARANTINE_DECISION_ALREADY_PENDING`, kod değişmez);
> (4) §8.3'e eşleşmeyen durum kuralı (R13, `NoChange (Unmatched)` + hata logu, fail-closed); (5) açık kayıttaki uyuşmazlık kabul edilen
> sınır olarak yazıldı (§8.3, §23). Yeni ölçüt AC-39 (DL-1); AC-2, AC-13, AC-20, AC-28, AC-29 genişledi. Kapanış tablosu §19.7. Künye
> `draft` / `pending`. DL-3 Web dosyalarının XMC-001 izni: **sahip izni bekleniyor** (§5; ready-for-dev önkoşulu değil).
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
> `MOD-0175-quarantine-blocked-stock.md` (`ready-for-dev`) **tabanıdır**; bu paket onun §8 "Hold states" kuralını ("A hold is never deleted";
> ana yol `Open → DecisionPending → Decided`) **genişletir**: yeni bir terminal durum ekler, hiçbir kaydı silmez, hiçbir stok hareketi yazmaz.
> Ebeveynin "No new stock status" (KQ-E) ve DEC-INV-08 kuralları aynen geçerlidir: `Voided` bir **kayıt** durumudur, stok durumu değildir.
> Bu sayılan genişletme dışındaki her çelişkide ebeveyn kazanır ve fark CT'ye sorulur.
>
> **Sözleşme:** yalnız eklemeli. QUARANTINE-BUNDLE 1.3.0 (`docs/analysis/contracts/quarantine.openapi.yaml:12-14`, `x-status: FROZEN`,
> `x-owner: MOD-0175`) → **1.4.0** (DL-1, §8.7). INVENTORY-BUNDLE (bugün 1.5.0, `inventory-bundle.openapi.yaml:9-10`) DL-3'te (Q-06 = A)
> bir ek hata koduyla **1.8.0** olur: MOD-0173-FU03'ün 1.6.0 (DL-1) / 1.7.0 (DL-2) adımlarından sonraki ek sürüm (Q-07, CT kararı; FU03
> onaylı — `execution/approvals/approvals.tsv`, `c03ac5511`). DL-3'ün sözleşme adımı 1.7.0 dalda olduktan sonra yazılır (§8.8).

## 1. Module Summary

MOD-0175 bir **kaynak hareketten** doğan tutma kaydını (`QuarantineHold`, `KRN-…`) ya da bloke stok kaydını (`BlockedStock`, `BLK-…`, origin
`OtherInbound`) defterin post-commit gözlemcisi ve intake süpürücüsüyle açar; kayıt `SourceMovementId` ile o hareketin `transactionId`'sine
bağlıdır. Hareketin kendisi sonradan `REVERSAL` ile geri alındığında **bugün kayda hiçbir şey olmaz**: stok kovadan çıkar (QUARANTINE 4 → 0),
kayıt `Open` ve miktar 4 kalır. Kayıt karar alamaz, kuyrukta ve lot kalite durumunda "karantinada 4" görünür; karar aşamasındaysa uygulanamaz
ve kilitli kalır (§19.2 ölçümler). Aynı kovada başka bir kaydın stoğu varsa daha kötüsü olur: hayalet kaydın kararı başkasının stoğunu taşır.

Bu paket:

1. **Geri alma kapanışı (DL-1):** kaynak hareketin REVERSAL'ı defterde commit edilince bağlı kayıt — **kaydın kökeni intake ise** (MOD-0175
   dışı inbound hareketten doğan hold ya da `OtherInbound` bloke kaydı) ve durumu buna izin veriyorsa — yeni terminal durum **`Voided`**
   (neden `SourceReversed`) ile kapanır. Aynı post-commit gözlemci yolu + bir süpürücü, tam bir kez (§8.2).
2. **Karar aşaması kuralı (DL-1):** intake kökenli kayıtta henüz hiçbir satırı defterde postlanmamış karar (`Submitted`, `PendingApproval`,
   `Approved`, satırsız `ApplyFailed`) kayıtla birlikte `Voided` olur; `Voided` kararın emici son durumudur, sonradan gelen onay / ret / iptal /
   zaman aşımı sonucu etkisizdir (§8.6). Satırı postlanmış ya da uygulanmış karar sonrası geri alma **otomatik kapanmaz**: kayda "kaynak geri
   alındı · uyuşmazlık" işareti ve geçmiş satırı düşer (§8.3).
2a. **MOD-0175 kökenli kayıt (DL-1):** kaydın kaynak hareketi MOD-0175'in kendi hareketiyse (Manual / Retest / Recall / Rework hold,
   `ManualBlock` bloke) REVERSAL kaydı **hiçbir durumda kapatmaz**: uyuşmazlık işareti (`Cause = OwnMovementReversed`) + geçmiş satırı düşer;
   bu REVERSAL'ı DL-3 bekçisi reddeder (§8.3, §8.8 kural 1).
3. **Denetim (DL-1):** kapanış ve uyuşmazlık işareti AUD-001 yol `b` (merkezi kayıt, K2: işlemle birlikte) + ürün içi karantina geçmişi (§21).
4. **Ekran (DL-2):** kuyruk, detay ve bloke stok listesinde "Geçersiz · Kaynak geri alındı" durumu; 7 dil; sayfa yardımı.
5. **Defter bekçisi (DL-3, Q-06 = A):** uyuşmazlığa yol açacak REVERSAL'ı ve MOD-0175'in kendi hareketlerinin REVERSAL'ını defter
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
- Platform Workflow örneğini iptal etmez (sistem token'ı yok — ebeveyn CT-Q11; Q-02 = A: örnek açık kalır, sonradan gelen onay etkisiz).
- Operatöre elle kapatma yolu açmaz (Q-05 = A): yeni izin anahtarı, satır eylemi ya da HTTP ucu yok.
- REVERSAL dışı yollarla (ör. QUARANTINE'den doğrudan `SCRAP`, `STATUS_TRANSFER`) kovadan çıkan stoğu ele almaz → takip maddesi
  FU-0175-FU01-OUTBOUND (§20; Q-05 = A).
- MOD-0173 / MOD-0186 / MOD-0142 paketlerini değiştirmez; gerekli kod dikişleri §5'te, sahip onayıyla (XMC-001).

## 3. Owned Objects

| Nesne | Tür | Dilim | Not |
|---|---|---|---|
| `ApplySourceReversalToHoldCommand` | iç MediatR komutu (HTTP ucu yok) | DL-1 | girdi: REVERSAL `transactionId`; sonuç `Voided` / `Conflict` / `NoChange`; `ICentralAuditDeclaration`; gönderenler: gözlemci, süpürücü, uygulama motoru (W5 / W6 `Voided` dalı, §8.6) |
| `ApplySourceReversalToBlockedCommand` | iç MediatR komutu | DL-1 | aynı, `BlockedStock` için |
| `ApplySourceReversalToHoldHandler`, `ApplySourceReversalToBlockedHandler` | handler | DL-1 | kural `SourceReversalRules` (saf) |
| `ApplySourceReversalToHoldValidator`, `ApplySourceReversalToBlockedValidator` | validator | DL-1 | §12 V1–V3 |
| `SourceReversalRules` | saf domain kuralı | DL-1 | kaydın kökeni (`HoldSource` / `BlockedOrigin` + aslın `SourceModule`'ü) + durumu + karar olayları + defterdeki satır anahtarları → `Void` / `Conflict` / `NoChange` (§8.3 tablosu) |
| `QuarantineRecordStates` | saf yüklem | DL-1 | `IsOpen(HoldStatus)` = Open \| DecisionPending; `IsTerminal` = Decided \| Voided; 13 sunucu çağrı yeri bunu kullanır (§8.5) |
| `QuarantineSourceReversalObserver` | `IPostedMovementObserver` | DL-1 | yalnız `MovementType = REVERSAL`; komutu gönderir |
| `QuarantineSourceReversalFeed` | salt okuma | DL-1 | üç besleme (açık kayıtlar; işaretsiz terminal kayıtlar; işaretsiz, kararlı `Voided` kayıtlar — r4), kayıt koleksiyonunda `Id` imleçli sayfa; defter sorgusu kiracı + şirket önekli (§8.2) |
| `QuarantineSourceReversalSweeper` + `…HostedService` | arka plan | DL-1 | kayıt tabanlı (zaman penceresi yok), tur başına besleme başına bir sayfa, imleç ilerler; kayıt başına kiracı + şirket kapsamı |
| `QuarantineHold.Voided`, `.SourceReversalConflict`; `BlockedStock` aynısı | alan | DL-1 | §4 |
| `HoldStatus.Voided`, `BlockedStatus.Voided`, `DecisionEventKind.Voided` / `DecisionState.Voided`, `HistoryAction.SourceReversed` / `.SourceReversalConflict` | enum değeri (sona eklenir) | DL-1 | §4 |
| QUARANTINE-BUNDLE 1.4.0 | sözleşme | DL-1 | §8.7 |
| Kuyruk / detay / bloke stok görünümü + JS + resx | Web | DL-2 | §11 |
| `IReversalPrecondition` (MOD-0173 dikişi) + `QuarantineReversalPrecondition` | port + uygulama | DL-3 | Q-06 = A; XMC-001 MOD-0173 (§5) |
| INVENTORY ek hata kodu `REVERSAL_QUALITY_RECORD_LOCKED` (INVENTORY 1.8.0) | sözleşme | DL-3 | Q-06 = A, Q-07 (§8.8) |
| Web hata metni `ErrReversalQualityRecordLocked` (Inventory ekranlarının ortak kod haritası `InventoryErrorMessages`; 7 dil × 2 resx ailesi `StockLedgerIndex`, `StockMovementsIndex`) | Web | DL-3 | §8.8; AC-38 |
| Inventory merkezi kayıt kapsamının sistem aktörü kipi (`InventoryCentralAuditScope.cs`) | bağdaştırıcı değişikliği | DL-1 | Q-03 = A; XMC-001 MOD-0173 / BL-501 (§5) |

Kayıt kodu (RCS-001): yeni kayıt türü **yok** (`Voided` bir durumdur) → yeni görünen kod yok; `KRN-` / `BLK-` numaraları değişmez.

## 4. Entity Fields

Taban: InventoryService `EntityBase` (ebeveyn §4). Silme yok.

### 4.1 `QuarantineHold` ve `BlockedStock` — eklenen alanlar (ikisinde aynı)

| Alan | Tür | Kural |
|---|---|---|
| `Voided` | `RecordVoid?` | yalnız `Status = Voided` iken dolu; bir kez yazılır |
| `Voided.AtUtc` | DateTimeOffset | kapanış anı |
| `Voided.Reason` | enum `VoidReason` | tek değer `SourceReversed` (Q-05 = A: elle kapatma yok, `Manual` değeri yok) |
| `Voided.ReversalTransactionId` | string (UUID D) | REVERSAL'ın `transactionId`'si |
| `Voided.ReversalTransactionNumber` | string | REVERSAL'ın `TransactionNumber`'ı (`INV-…`) |
| `Voided.ReversedByUserId` | Guid | REVERSAL'ı postlayan (`InventoryTransaction.CreatedBy`) |
| `Voided.DecisionId` | Guid? | kapanışla birlikte `Voided` olan aktif karar |
| `SourceReversalConflict` | `SourceReversalMark?` | yalnız uyuşmazlıkta; durum değişmez; bir kez yazılır; `Voided` kayıtta da yazılabilir (§8.3 R0a — geçersiz kararın satırı kapanıştan sonra postlandı) |
| `SourceReversalConflict.AtUtc`, `.ReversalTransactionId`, `.ReversalTransactionNumber`, `.ReversedByUserId`, `.Cause` | | `Cause` ∈ `Decided` \| `LinesPosted` \| `QuantityChanged` \| `OwnMovementReversed` (§8.3) |

`Quantity` **değişmez** (kaydın tuttuğu miktar iz olarak kalır); açık miktar toplamları `QuarantineRecordStates.IsOpen` ile süzer.

**Köken (yeni alan yok, F-1@ae3fed7d):** kaydın kökeni oluşturulurken bir kez yazılan mevcut alandan okunur (A-2):

| Kayıt | Alan | Intake kökenli (Void dalı uygulanır) | MOD-0175 kökenli (yalnız Conflict) | `SourceMovementId` yok (kural dışı) |
|---|---|---|---|---|
| `QuarantineHold` | `Source` (`HoldSource`, `QuarantineEnums.cs:7-19`) | `GoodsReceipt`, `Production`, `CustomerReturn`, `OtherInbound` — yalnız `QuarantineIntakeService` yazar (`QuarantineIntake.cs:59-65, 107`), o da `SourceModule = MOD-0175` hareketi dışlar (`:43`) | `Manual`, `Retest`, `Recall` (`StockActionHandlers.cs:346-348`, kaynak MOD-0175'in `STATUS_TRANSFER`'ı), `Rework` (`QuarantineApplyEngine.cs:287-288`, kaynak Rework `STATUS_TRANSFER`'ı) | — |
| `BlockedStock` | `Origin` (`BlockedOrigin`, `QuarantineEnums.cs:103-110`) | `OtherInbound` (`QuarantineIntake.cs:139-140`) | `ManualBlock` (`StockActionHandlers.cs:455-456`) | `QuarantineDecision` (`NewBlocked`, `QuarantineApplyEngine.cs:227`, `SourceMovementId` yazmaz), `LotBlocked` (bugün yazan yer yok) — depoda `SourceMovementId` yazan yer yalnız beştir (G-21) |

`SourceReversalRules.OriginOf(record, original)` kaydın alanını okur ve aslın `SourceModule`'üyle (V2'de okunan REVERSAL'ın
`ReversesTransactionId`'siyle bulunan işlem) çapraz denetler: alan intake diyor ama aslın `SourceModule = MOD-0175`
(`QuarantineContract.ModuleId`, `QuarantineContract.cs:11`) ise ya da alan değeri tabloda yoksa (gelecekte eklenen değer) köken **MOD-0175
sayılır** (fail-closed: kayıt kapanmaz, işaretlenir) ve uyarı logu yazılır.

### 4.2 Enum ekleri (yalnız sona; mevcut değerlerin sayısal sırası değişmez)

- `HoldStatus` (`Domain/Quarantine/Enums/QuarantineEnums.cs:29-34`) += `Voided`.
- `BlockedStatus` (`:112-117`) += `Voided` (Q-01 = A: `Closed` yeniden kullanılmaz — `Closed` "imha / iade / blokesi kaldırıldı" demektir).
- `DecisionEventKind` (`:79-92`) += `Voided`; `DecisionState` (`:62-76`) += `Voided`; `QuarantineFacts.StateOf` (`QuarantineFacts.cs:101-120`)
  eşlemesi eklenir (eşlenmeyen olay bugün `ArgumentOutOfRangeException` atar — eksik eşleme fail-closed). **`Voided` emicidir** (F-2, F-12):
  olaylarda `Voided` varsa `StateOf` sıradan bağımsız `Voided` döner; yoksa bugünkü "son olay kazanır" (§8.6).
- `HistoryAction` (`:125-146`) += `SourceReversed`, `SourceReversalConflict`.
- `QuarantineDecisionEvent.OnceKey` (`QuarantineSupport.cs:131`): `Voided` tek seferlik olaydır (`OnceKey = "Voided"`).

### 4.3 `QuarantineHistoryEntry` — eklenen alan

| Alan | Tür | Kural |
|---|---|---|
| `ReversalTransactionId` | string? | yalnız `SourceReversed` / `SourceReversalConflict` satırında |

`ByUserId` = REVERSAL'ı postlayan; `CorrelationId` = REVERSAL'ın `CorrelationId`'si (defterin `InventoryMovementReversed` denetim satırına
bağlanır); `Reason` = REVERSAL numarası. Koleksiyon yalnız ekleme (mevcut mimari testi).

### 4.4 İndeksler

Yeni indeks gerekmez. Kayıt `(TenantId, SourceMovementId)` tekil kısmi indeksiyle bulunur (`QuarantineIndexConfiguration.cs:49-50, 81-82`).
**İndeks yalnız kiracı kapsamlıdır, şirket alanı yoktur** (F-10@ae3fed7d): bir kiracıda aynı `SourceMovementId` yalnız bir hold'da (ve yalnız bir
bloke kayıtta) bulunabilir, şirket fark etmez. Şirket süzgeci okumadadır: `FindHoldBySourceMovementAsync` / `FindBlockedBySourceMovementAsync`
`Scope<T>()` ile kiracı + şirket süzer (`QuarantineRepository.cs:85, 125`). Süpürücünün kayıt sayfası `_id` imleciyle, defter sorgusu
`ux_tenant_le_reversestransactionid` (`TenantId, LegalEntityId, ReversesTransactionId`; `InventoryIndexConfiguration.cs:20, 49-56`) önekiyle
okunur (§8.2). DL-3 bekçisi kaydı aynı kiracı + şirket kapsamlı okumayla bulur.

## 5. Repo Scope

- **DL-1 (`services/Diten.InventoryService/**`):**
  - `src/Diten.InventoryService.Domain/Quarantine/{Entities,Enums,Rules}/…` — §4 alanları, enum ekleri, `SourceReversalRules`, `QuarantineRecordStates`.
  - `src/Diten.InventoryService.Application/Features/Quarantine/Commands/ApplySourceReversalTo{Hold,Blocked}Command.cs`,
    `Handlers/CommandHandlers/ApplySourceReversalTo{Hold,Blocked}Handler.cs`, `Validators/ApplySourceReversalTo{Hold,Blocked}Validator.cs`
    (yeni dosyalar Golden Reference klasör düzeninde, §10).
  - `…/Features/Quarantine/Services/QuarantineIntake.cs` — gözlemci kaydı ve **geç intake** kuralı (§8.4: kaynağı zaten geri alınmış hareket için
    kayıt açılmaz).
  - `…/Features/Quarantine/Services/QuarantineApplyEngine.cs` — `Voided` kararın uygulanmaması, `RecordAppliedAsync` / `RecordFailureAsync`'in
    `Voided` kararda hiçbir şey yazmaması ve satırı postlanmışsa işlem dışında iç komutu göndermesi (W5 / W6, r4), `ApplyOutcomeAsync`
    onay işleminin ≤ 3 denemeli yeniden okuması (W3, r4) ve `CloseWithoutMovementAsync`'in işlem içi yeniden okuması, ortak yazma kilidi (§8.6).
  - `…/Features/Quarantine/Handlers/DecisionHandlers.cs` — ayrıca `SubmitDecisionHandler`'ın ikinci işlemi (`:422-434`): işlem içi yeniden
    okuma, sürüm uyuşmazlığında yeniden deneme, `Voided` kaydı `DecisionPending`'e çevirmeme (§8.6 yazar 2).
  - `src/Diten.InventoryService.Domain/Quarantine/Repositories/QuarantineRepositories.cs` + `Persistence/Quarantine/QuarantineRepository.cs`
    `WriteSession` — yalnız gerekirse `IQuarantineWriteSession`'a işlem içi kayıt okuması (`FindHoldAsync` / `FindBlockedAsync` eşdeğeri; §8.6
    mekanizması uygulamaya açık).
  - `…/Features/Quarantine/Services/QuarantineFacts.cs`, `QuarantineSupport.cs`, `QuarantineMapping.cs`, `QuarantineRecallRequester.cs`,
    `Handlers/{DecisionHandlers,LotQualityStatusHandler,QueryHandlers,RetestHandlers,ReviewHandlers,StockActionHandlers,UpdateHoldHandler}.cs`
    — açık / terminal yüklemi (§8.5).
  - `src/Diten.InventoryService.Persistence/Quarantine/{QuarantineRepository,QuarantineSourceReversalSweeper}.cs` — besleme + süpürücü;
    `QuarantineRepository.cs:108` yüklemi; `QuarantineLedgerReads.cs` `QuarantineSweeperOptions` (`:149-167`) yeni seçenekler (§8.2).
  - `src/Diten.InventoryService.Api/Quarantine/QuarantineServiceCollectionExtensions.cs` — gözlemci, süpürücü, seçenekler.
  - `src/Diten.InventoryService.Api/CentralAudit/InventoryCentralAuditScope.cs` — sistem aktörü kipi (Q-03 = A). **XMC-001 izni:** Beste
    2026-11-03 (MOD-0173 / BL-501 bağdaştırıcısı; MOD-0173 ve BL-501 sahibi aynı). **İznin kapsamı:** kayıt kapsamı (`Begin` anında okunur,
    `CentralAuditRecorder.cs:82-121`) yalnız `ApplySourceReversalTo{Hold,Blocked}Command` gönderimi süresince `ActorType = System`,
    `ActorId = 00000000-0000-0000-0000-000000000175`, `ActorDisplayName = null` ve `CorrelationId` = REVERSAL'ın kayıtlı `CorrelationId`'si
    (`InventoryTransaction.cs:82`; süpürücü yolunda istek bağlamı yoktur) döner; gönderim bitince — hata dahil — kip kapanır. **Kip gönderene
    değil komutun kendi gönderimine bağlıdır** (r4, F-1@54d736e1): gözlemci, süpürücü ya da uygulama motoru (W5 / W6 — `RefreshDecisionCommand`,
    `ReapplyDecisionCommand`, okuma anı uzlaştırması yapan sorgu handler'ları ya da MediatR dışı `QuarantineWorkflowOutcomeConsumer` içinden)
    göndersin, kip `CentralAuditBehavior`'ın `Begin`'inden önce açılır (davranış en içtedir, `CentralAuditBehavior.cs:15-16`); iç içe gönderimde
    dıştaki komutun kaydı kendi `Begin` anında okunmuştur, değişmez (kayıt yığını `CentralAuditRecorder.cs:57, 119, 130`). Bu, iznin
    kuralı içindedir: kip yine yalnız bu iki komutun gönderimi süresince açıktır. `TenantId` /
    `CompanyId` değişmez (kaydın kapsamı, §8.1). Başka komutun, aynı istekteki REVERSAL'ın kendi `PostMovementCommand` kaydı dahil, aktörü
    değişmez (AC-20). `services/Diten.Building.Blocks/**` değişmez (§6); mekanizma (kapsamlı geçersiz kılma servisi ya da eşdeğeri) uygulamaya
    açık, AC-20 bağlayıcı.
  - Testler `services/Diten.InventoryService/tests/**`; sabit sayı `CentralAudit/CentralAuditDeclarationTests.cs:37` — **geliştirme anında
    dalda ölçülen değer N → N + 2**, aynı değişiklikte (F-7@ae3fed7d, CT kuralı: sabit geliştirme anında ölçülür). Bugün N = 71 (`e89bce84c`);
    aynı tek çalışma dalında MOD-0178 DL-2 önce birleşirse N = 82 (`:36` yorumu "82 after DL-2 (the 11 task commands)") → 84;
    `Contract/Fixtures/quarantine-v1.3.0.openapi.yaml` yeni donmuş kopya (AC-23 karşılaştırma tabanı; bugün `v1.0.0`…`v1.2.0` var, 1.3.0 yok —
    ölçüldü 2026-11-03).
  - Sözleşme `docs/analysis/contracts/quarantine.openapi.yaml` 1.4.0 + `docs/analysis/contracts/README.md` satırı.
- **DL-2 (`frontend/Diten.Web/**`):** `wwwroot/assets/js/Quarantine/{queue,details,blocked,decide,quarantine-ui}.js`,
  `Views/Quarantine/{Index,Details,Blocked}.cshtml` (yalnız rozet / afiş / süzgeç seçeneği), `Resources/Views/Quarantine/Quarantine.{en,tr,fr,es,zh,ar,ru}.resx`,
  `Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` (QUARANTINEQUEUE, QUARANTINEDETAILS, BLOCKEDSTOCK adımları),
  **`Views/Quarantine/Quarantine.cs`** (`Keys` listesi — F-8@ae3fed7d: `window.QuarantineL10n` yalnız bu listedeki anahtarları taşır ve
  `QuarantineWebTests` 7 resx'i bu listeye sabitler, `Quarantine.cs:3-8`; §11'in yeni anahtarları bu listeye girmeden JS'e ulaşmaz),
  `frontend/Diten.Web.Tests/Quarantine/**`.
- **DL-3 (Q-06 = A; XMC-001 MOD-0173 izni Beste 2026-11-03 — MOD-0173 sahibi aynı):** `…/Features/Inventory/Ports/IReversalPrecondition.cs` (yeni),
  `…/Features/Inventory/Services/InternalMovementPostingService.cs` (`BuildReversalAsync` içinde, `:253-256` "zaten geri alındı" denetiminden
  sonra tek çağrı), `Application/Common/InventoryErrorCodes.cs` (ek kod `REVERSAL_QUALITY_RECORD_LOCKED`), `…/Features/Quarantine/Services/QuarantineReversalPrecondition.cs`,
  `docs/analysis/contracts/inventory-bundle.openapi.yaml` (1.7.0 → **1.8.0**, Q-07), `Contract/Fixtures/inventory-bundle-v1.7.0.openapi.yaml`
  (yeni donmuş kopya, AC-36 karşılaştırma tabanı; FU03 deseni). **İznin kapsamı:** yalnız bu dosyalar ve §8.8'in iki kuralı; MOD-0173'ün
  başka posting, REVERSAL ya da katalog davranışı değişmez.
  - **DL-3 Web (F-5@ae3fed7d; CT kabulü 2026-11-03).** XMC-001: bu dosyalar MOD-0173-FU01 S-2 / S-1 ekranlarınındır (`StockMovements/index.js:2`);
    r2'nin MOD-0173 izni (Beste 2026-11-03) dosya listesinde **yoktular** → izin bu dosyalara genişletilmesi için sahip (Beste) onayı ister —
    **sahip izni bekleniyor** (r4'te de; DL-3 Web adımının kapısı, ready-for-dev önkoşulu değil; §19.5 "Açık kalanlar"). İzin gelmeden bu
    dosyalara dokunulmaz: `frontend/Diten.Web/Models/Inventory/InventoryErrorMessages.cs`
    (`ByCode` += `REVERSAL_QUALITY_RECORD_LOCKED` → `ErrReversalQualityRecordLocked`; `:10-40`), `frontend/Diten.Web/Resources/Views/Inventory/StockLedger/StockLedgerIndex.{en,tr,fr,es,zh,ar,ru}.resx`
    ve `…/StockMovements/StockMovementsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (yeni anahtar 14 dosyada — sınıf yorumu: anahtarlar iki ailede, 7 dilde),
    `frontend/Diten.Web.Tests/Inventory/InventoryResxParityTests.cs` (kod listesi `:81-86`'ya kod eklenir). JS değişmez: ileti MVC tarafında kod
    haritasından gelir (`StockMovementsController.cs:525, 648`; `StockLedgerController.cs:122`), S-2 onay penceresi reddi kendi metniyle gösterir
    (`StockMovements/index.js:16-18`).
- **Bu taslak:** yalnız bu dosya.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`, `execution/registries/**`, `domain-config.md`, ebeveyn ve başka paketler (MOD-0173, MOD-0173-FU03,
  MOD-0186, MOD-0183, MOD-0142).
- `gateway/Diten.ApiGateway/ocelot.json` (değişmez; §15).
- `_Layout.cshtml` (FROZEN), Archive.
- `services/Diten.Building.Blocks/**` (BL-501 ortak iletici — değişmez; Q-03 = A yalnız Inventory'nin kendi kapsam bağdaştırıcısını, §5'teki izin kapsamında değiştirir).
- `services/Diten.SupplyChainService/**`, `services/Diten.ProcurementService/**` (REVERSAL üreticileri — davranışları değişmez; DL-3 etkisi §7).
- Dondurulmuş sözleşmeler yalnız sahibinin tek yazar adımıyla: QUARANTINE (MOD-0175, bu paket), INVENTORY (MOD-0173, DL-3).

## 7. Dependencies

| Bağımlılık | Durum (ölçüldü `e3cbbca4d`) | Etki |
|---|---|---|
| MOD-0175 | `ready-for-dev`, registry `in-progress` (satır 306) | taban; ekranlar ve kayıtlar canlı |
| MOD-0173 post-commit gözlemci | var (`IPostedMovementObserver.cs:10-13`; `InternalMovementPostingService.cs:155-159, 201-214`) — her commit edilen hareket, REVERSAL dahil, gözlemcilere gider; gözlemci hatası loglanır, posting'i değiştirmez | DL-1 yolu; ek MOD-0173 değişikliği yok |
| MOD-0173 REVERSAL kuralı | tam ve tek (§19.2 G-4, G-5) | kısmi dal yok |
| BL-501 merkezi denetim | `tests/architecture/audit-ledger/Diten.InventoryService.md` → `inventory-merkezi-iletim \| b \| işaret \| ICentralAuditDeclaration`; `RequireEnlistment = true`; K2 borcu yok | yol `b` kullanılabilir; Q-03 = A: sistem aktörü için kapsam bağdaştırıcısı değişikliği, XMC-001 izni Beste 2026-11-03 (§5) |
| REVERSAL üreticileri | S-2 hareket listesi "Reversal" satır eylemi (`InventoryLedgerManifestProvider.cs:76`; JS yalnız `sourceModule = MOD-0173` satırında gösterir — `StockMovements/index.js:15`, **sunucuda böyle bir sınır yok**); "Düzelt" = REVERSAL + yeni hareket (KD-1); MOD-0186 geç yazan (`ReturnLateWriterPolicy.cs:37-43`, MVP-6). **MOD-0142 `reverseGrn` bugün Inventory'ye ulaşmıyor** (F-6@ae3fed7d): REVERSAL'ı `SourceModule MOD-0142` / `SourceType GOODS_RECEIPT` ile kurar (`ReverseGrnHandler.cs:57-73`, `GrnModels.cs:25-26`), Inventory zarfı `MOD-0173` / `REVERSAL` ister (`ReversalRules.cs:22-26`) → 400; ayrıca Procurement DI'da `MockInventoryPostingClient` bağlı (`Persistence/DependencyInjection.cs:61`) | DL-1 Inventory'ye ulaşan her REVERSAL'ı kapsar (gözlemci üreticiye bakmaz, kural kaydın kökenine bakar — §8.3); DL-3'te (Q-06 = A) karar verilmiş ya da MOD-0175 kökenli kaydın kaynağının REVERSAL'ı S-2 ve MOD-0186'ya 422 döner. **MOD-0142: DL-3 etkisi yok** (bugün ulaşmıyor; gerçek istemci bağlandığında zarfı da değişmelidir — sahibine bilgi notu CT'den, karar değil). MOD-0186 etkisinin kabulü **Q-10 — açık, MVP-6 CT (Natig); DL-3 kabul kapısı**, ready-for-dev önkoşulu değil (§19.4, §22) |
| MOD-0173-FU03 | `ready-for-dev`, CT-APR-01 onaylı (r4 `0e93c1ba`, `execution/approvals/approvals.tsv`, `c03ac5511`); INVENTORY 1.6.0 (DL-1) / 1.7.0 (DL-2) | yalnız DL-3: INVENTORY ek sürümü **1.8.0** (Q-07); DL-3'ün sözleşme adımı FU03'ün 1.7.0'ı dalda olduktan sonra yazılır, olmadan DL-3 sözleşme adımı bekler (§8.8) |
| RabbitMQ | bu makinede kapalı (ebeveyn §8) | Workflow sonucu okuma anında uzlaştırılır; kapanış bundan bağımsız |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam

- `Diten.InventoryService` (port 5067, `AGENTS.md` §3; ebeveyn künyesindeki 5066 eskidir). Süreç içi, aynı Mongo veritabanı.
- Her yazma kiracı + şirket kapsamlıdır: gözlemci REVERSAL'ı postlayan isteğin kapsamında (`ITenantContext`) çalışır; süpürücü kayıt başına
  kapsamı kaydın `TenantId` + `LegalEntityId`'siyle kurar (intake süpürücüsü deseni, `QuarantineLedgerReads.cs:199-206`). Kayıt yalnız kendi
  kiracı + şirketindeki `SourceMovementId` ile bulunur. Başka kiracının kaydı 404 (ebeveyn V23 / §13 #21).

### 8.2 Tetik — gözlemci + süpürücü; tam bir kez

- **Gözlemci (hızlı yol):** `QuarantineSourceReversalObserver.OnPostedAsync(t)` yalnız `t.MovementType = REVERSAL` ve `t.ReversesTransactionId`
  doluyken çalışır; `ReversesTransactionId`'yi `SourceMovementId` olarak taşıyan hold ve blocked kayıtlarının her biri için ilgili komutu
  (`ApplySourceReversalTo{Hold,Blocked}Command(reversalTransactionId)`) gönderir. Mevcut `QuarantineIntakeObserver` REVERSAL'ı zaten dışlar
  (`QuarantineIntake.cs:44`; ayrıca `MovementDirectionRules.cs:29` REVERSAL'ı `Inbound` saymaz) — o davranış değişmez.
- **Süpürücü (kaçanlar ve eski veri):** zaman penceresine **değil kayıtlara** dayanır. Üç besleme (F-3@ae3fed7d, F-11@ae3fed7d; üçüncüsü r4,
  F-1@54d736e1):
  - **Açık besleme (kapanış):** `IsOpen` hold'lar ve `Open` blocked kayıtlar;
  - **Terminal besleme (uyuşmazlık emniyet ağı):** `Decided` hold'lar ve `Closed` / `SampleRetained` blocked kayıtlar;
  - **Geçersiz besleme (W5 / W6 emniyet ağı, r4):** `Voided` hold / blocked kayıtlar, `Voided.DecisionId` dolu, `Voided.AtUtc` son
    `SourceReversalVoidedWatchHours` (varsayılan 72) saat içinde ve `< şimdi − SettleSeconds`. Bu beslemede defter sorgusu REVERSAL'ı değil
    **geçersiz kararın satır anahtarlarını** (`quarantine:{Voided.DecisionId}:{lineNo}`, `QuarantineMovementPoster.cs:55`;
    `FindByIdempotencyKeyAsync`, kiracı + şirket kapsamında) sorar; satırı postlanmış kayıt için komut `Voided.ReversalTransactionId` ile
    gönderilir (§8.3 R0a). Satırı olmayan kayıt yazılmaz (`NoChange`), pencere dolunca beslemeden çıkar.

  Üçünde de süzgeç: `SourceMovementId` dolu (string) ve `SourceReversalConflict` boş (işaretli kayıt bir daha okunmaz; bir hareket bir kez
  geri alınır, G-5). Her tur, her besleme ve her koleksiyon (hold, blocked) için **tek sayfa** okunur: kayıt koleksiyonunda çapraz kiracı,
  `Id > imleç` sıralı, en çok `SourceReversalPageSize` (varsayılan 500) kayıt. Sayfa kiracı + şirkete göre gruplanır; açık ve terminal
  beslemede her grup için defter sorgusu `TenantId = t ∧ LegalEntityId = le ∧ ReversesTransactionId IN (sayfadaki SourceMovementId'ler) ∧ CreatedAt < şimdi − SettleSeconds`
  — `ux_tenant_le_reversestransactionid` öneki (`InventoryIndexConfiguration.cs:20, 49-56`); çapraz kiracı defter sorgusu **yoktur**. REVERSAL'ı
  bulunan her kayıt için komut kaydın kapsamında gönderilir (§8.1). **İmleç:** sayfanın son `Id`'si; sayfa `PageSize`'dan kısa gelirse imleç
  başa döner. Altı imleç (besleme × koleksiyon) bellekte tutulur; süreç yeniden başlarsa baştan başlar (komut idempotent, §8.2 "tam bir kez").
  Böylece N kayıtlık beslemede her kayıt en geç ⌈N / PageSize⌉ turda okunur; ilk sayfa sonrakileri aç bırakmaz. Tur maliyeti sabittir
  (≤ 6 kayıt sayfası + grup başına bir indeksli defter sorgusu; geçersiz beslemede kayıt başına karar satırı kadar anahtar okuması).
- **Geçersiz beslemede zaman penceresi neden kabul (r4):** F-3'te reddedilen pencere eski veriyi (bu FU'dan önce geri alınmış kayıtları)
  kaçırdığı içindi; `Voided` kaydın hepsi bu FU'yla doğar, eski verisi yoktur. `Voided`'dan sonra satır postlanabilecek tek yol, satır öncesi
  yeniden okumayı `Voided`'dan önce geçmiş, sürmekte olan bir `ApplyAsync`'tir (`ApplyAsync` `Voided` kararı uygulamaz, `QuarantineApplyEngine.cs:81`; V7 yeniden
  uygulamayı reddeder) → satırın `CreatedAt`'i kapanıştan en çok bir posting çağrısı sonradır. 72 saat süreç yeniden başlamalarını ve
  süpürücü kesintilerini karşılar. Kalan sınır: satır sonrası iç komut gönderimi (§8.6 W5) başarısız **ve** süpürücü 72 saat boyunca kapalı /
  durmuş ise işaret yazılmaz — W5 hata logu (`quarantine.source_reversal.mark_dispatch_failed`, kayıt numarası, karar kimliği) bunu adıyla
  bırakır.
- **Seçim gerekçesi (F-3):** terminal kayıtlar için ayrı bir REVERSAL tabanlı tarama (defterde `MovementType = REVERSAL` + zaman damgası) seçilmedi:
  defterde bu sorguya uyan kiracı önekli indeks yok ve kalıcı bir filigran gerekirdi; terminal besleme mevcut kayıt koleksiyonlarını ve aynı defter
  indeksini kullanır, tur maliyetini sabit tutar. Bedeli: terminal kayıt sayısı büyüdükçe işaretin gecikmesi ⌈N / 500⌉ tur × 300 sn'ye çıkar —
  işaret stok durumu değil bilgi işaretidir (Q-04 = A), gecikme kabul edilir; DL-3 bu sınıfı canlı yoldan kapatır (§8.8).
- Seçenekler `Inventory:Quarantine` altında `QuarantineSweeperOptions`'a eklenir (`QuarantineLedgerReads.cs:149-167` deseni):
  `SourceReversalSweeperEnabled` (true), `SourceReversalSweepIntervalSeconds` (300), `SourceReversalSettleSeconds` (60, gözlemciye pay),
  `SourceReversalPageSize` (500), `SourceReversalVoidedWatchHours` (72, yalnız geçersiz besleme; r4). Gerekçe: intake süpürücüsünün 72 sa penceresi (`:157-158`) bu FU'dan önce geri alınmış kayıtları
  (KRN-2026-0001, KRN-2026-0004) asla görmez; kayıt tabanı onları ilk tam döngüde kapatır ve ayrı bir "geri doldurma" işi ya da elle kapatma
  yolu gerektirmez (Q-05 = A; r1'deki "Q-08" atfı yanlıştı, Q-08 kuyruk varsayılanıdır).
- **Tam bir kez:** komut önce kaydı okur; `SourceReversalConflict` (aynı REVERSAL) zaten yazılıysa, ya da `Voided` (aynı REVERSAL) yazılı ve
  §8.3 R0a eşleşmiyorsa (karar yok ya da geçersiz kararın defterde postlanmış satırı yok) **işlem açmadan** `NoChange` döner — merkezi kayıt yazılmaz (`RequireEnlistment` altında işlemsiz başarılı komut kayıt bırakmaz,
  `ICentralAuditDeclaration.cs` `ICentralAuditOutsideTransaction` notu). Değişiklik tek karantina işleminde, kaydın `Version`'ı beklenen
  değerle (`ReplaceHoldAsync` / `ReplaceBlockedAsync`) yazılır; eşzamanlı ikinci çağıran değiştirmede kaybeder, işlem geri alınır (enlist
  edilmiş merkezi kayıt da), yeniden okur ve §8.3 tablosunu yeniden uygular: kazanan aynı REVERSAL'ın kapanışı / işaretiyse R0 → `NoChange`;
  kazanan başka bir yazar ise (ör. §8.6 W2 gönderim) yeni duruma göre sonuç (≤ 3 deneme, sonra hata logu; süpürücü sonraki turda dener).
- **Komut sonucu adlıdır** (F-13@ae3fed7d; AC-11, AC-12 bunu ölçer): `Voided`, `Conflict` ya da `NoChange`; `NoChange` nedeni
  `AlreadyRecorded` (ilk okumada işaret / kapanış vardı, işlem açılmadı), `LostRace` (yazmada kaybetti — sürüm uyuşmazlığı → `QuarantineRetryException`
  ya da Mongo yazma çakışmasıyla yeniden koşan işlemin sürüm uyuşmazlığı; iki yol da handler'ın aynı yeniden okuma dalına düşer — ve yeniden
  okumada R0'a düştü), `NoRecord` (V2 / V3), `Unmatched` (§8.3 R13 — tablo dışı durum; yazma yok, hata logu; r4).
- Gözlemci hatası posting'i etkilemez (MOD-0173 davranışı); süpürücü kapatır ya da işaretler (açık besleme kapanışı, terminal besleme
  uyuşmazlık işaretini; F-3; geçersiz besleme kapanıştan sonra postlanmış satırın işaretini, r4). K2 enlist hatası (AC-21) işlemi geri alır; işaret / kapanış sonraki turda yeniden denenir.

### 8.3 Kural tablosu (`SourceReversalRules`)

"Postlanmış satır" defterden ölçülür: kararın satır anahtarları `quarantine:{decisionId}:{lineNo}` (`QuarantineMovementPoster.cs:55`) için
`FindByIdempotencyKeyAsync` — olaylara güvenilmez, çünkü satır postlanıp `Applied` yazılmadan önce geri alma gelebilir.

Satırlar yukarıdan aşağı okunur; ilk eşleşen kazanır; **hiçbiri eşleşmezse R13** (fail-closed, r4). "Köken" §4.1 köken tablosudur (`OriginOf`).

| # | Köken | Kayıt durumu | Aktif karar durumu | Postlanmış satır | Sonuç |
|---|---|---|---|---|---|
| R0a | intake | aynı REVERSAL için `Voided`, `Voided.DecisionId` dolu, `SourceReversalConflict` boş (r4, F-1@54d736e1) | `Voided` (kapanışla geçersiz olan karar) | ≥ 1 (kapanıştan sonra postlandı — §8.6 W5 / W6 penceresi) | **Conflict** (`Cause = LinesPosted`); durum `Voided` kalır, `Voided.*` değişmez, karar olayı yazılmaz |
| R0 | herhangi | aynı REVERSAL için `SourceReversalConflict` zaten yazılı, ya da `Voided` yazılı ve R0a eşleşmedi | — | — | **NoChange** (`AlreadyRecorded`) |
| R1 | MOD-0175 | herhangi (hold `Open` / `DecisionPending` / `Decided`; blocked `Open` / `Closed` / `SampleRetained`) | herhangi | — | **Conflict** (`Cause = OwnMovementReversed`); durum, aktif karar, miktar değişmez; karar olayı yazılmaz |
| R2 | intake | Hold `Open` (Waiting / InReview; `ActiveDecisionId` yok; Draft ya da kayda dönmüş `StartFailed` / `Rejected` / … karar olabilir) | — / Draft / kapanmış karar | — | **Void**: hold `Voided`; aktif olmayan karar dokunulmaz, Draft'ın submit'i V5 ile reddedilir |
| R3 | intake | Hold `Open` + `ActiveDecisionId` dolu (**gönderim penceresi**: `SubmitDecisionHandler` birinci işlemi commit, Workflow henüz başlamadı — `DecisionHandlers.cs:396-402`) | `Submitted` | 0 | **Void** + karara `Voided` olayı; `ActiveDecisionId = null` (F-2@ae3fed7d) |
| R4 | intake | Hold `DecisionPending` | `PendingApproval` | 0 | **Void** + karara `Voided` olayı |
| R5 | intake | Hold `DecisionPending` | `Approved` (uygulanmamış) / `ApplyFailed` | 0 | **Void** + karara `Voided` olayı |
| R6 | intake | Hold `DecisionPending` | `Approved` / `ApplyFailed` | ≥ 1 | **Conflict** (`Cause = LinesPosted`); durum değişmez |
| R7 | intake | Hold `Decided` | `Applied` | — | **Conflict** (`Cause = Decided`) |
| R8 | intake | Blocked `Open`, `Quantity` = aslın `BaseQuantity`'si | yok / `Submitted` / `PendingApproval` / satırsız `Approved` · `ApplyFailed` | 0 | **Void** (+ aktif karara `Voided`) |
| R9 | intake | Blocked `Open`, `Quantity` < aslın `BaseQuantity`'si | herhangi | — | **Conflict** (`Cause = QuantityChanged`) |
| R10 | intake | Blocked `Open`, aktif karar satırı postlanmış | `Approved` / `ApplyFailed` | ≥ 1 | **Conflict** (`Cause = LinesPosted`) |
| R11 | intake | Blocked `Closed` / `SampleRetained` | — | — | **Conflict** (`Cause = Decided`) |
| R12 | herhangi | `Voided` (başka REVERSAL — olamaz, G-5; savunma) | — | — | **NoChange** (`AlreadyRecorded`) |
| R13 | herhangi | **R0a–R12'nin hiçbirine uymayan birleşim** (r4, F-4@54d736e1) — ör. hold `DecisionPending` + aktif karar `Draft` / `Submitted` / `StartFailed` / `Rejected` / `Cancelled` / `TimedOut` / `Withdrawn` / `FourEyesViolation`; hold `Open` + `ActiveDecisionId` dolu + `Submitted` dışı karar; gelecekte eklenen `HoldStatus` / `BlockedStatus` / `DecisionState` değeri | herhangi | herhangi | **NoChange** (`Unmatched`); hiçbir yazma, olay, geçmiş ya da merkezi kayıt yok; hata logu `quarantine.source_reversal.unmatched` (kayıt numarası, kayıt durumu, karar durumu, REVERSAL numarası) |

- **Köken kuralı (F-1@ae3fed7d, CT kararı 2026-11-03):** `Voided` yalnız intake kökenli kayıtlara uygulanır. MOD-0175'in kendi hareketi
  (Manual / Retest / Recall hold'un `STATUS_TRANSFER`'ı, Rework'ün BLOCKED → QUARANTINE hareketi, `ManualBlock`'un hareketi) kalite kararının
  parçasıdır; onun REVERSAL'ı DEC-INV-08'i atlar. DL-1 bunu **meşrulaştırmaz**: kayıt kapanmaz, "Kaynak geri alındı · uyuşmazlık" işareti ve
  geçmiş satırı düşer (Recall hold'u açık kalır → MOD-0177 recall isteği kaydı açık görmeye devam eder; Rework hold'unun REVERSAL'ında stok
  BLOCKED'a döner ama kaynak blocked kaydı azaltılmış / `Closed` kalır — işaret bu kayıtsız BLOCKED stoğu QA'ya gösterir). DL-3 bu REVERSAL'ı
  defterde reddeder (§8.8 kural 1).
- **DL-3 sonrası erişilebilirlik (F-4@ae3fed7d):** DL-3 etkinken bekçi R5, R6, R7, R9, R10, R11'e yol açacak REVERSAL'ı ve R1'i (aslın
  `SourceModule = MOD-0175`) reddeder. Bu satırlar yine de yürürlükte kalır ve yalnız şu yollardan erişilir: (a) bekçi ↔ commit penceresi
  (bekçi işlem dışında okur, §8.8); (b) REVERSAL bekçinin izin verdiği durumda (R2–R4, R8) commit oldu, gözlemci kaçtı ve kayıt süpürücüden
  önce ilerledi (karar onaylandı / uygulandı / kısmi imha); (c) eski veri — DL-3'ten önce commit olmuş REVERSAL. R1 için yalnız (c) vardır
  (aslın `SourceModule`'ü değişmez, pencere yok). R0a (r4) bekçinin reddettiği bir satır değildir ama DL-3 etkinken de yalnız (b) yoluyla doğar:
  REVERSAL kayıt `PendingApproval` iken commit olur (R4 `Allow`), gözlemci kaçar, karar onaylanır ve `ApplyAsync` satır öncesi yeniden okumayı
  geçtikten sonra süpürücü kaydı R5 ile kapatır, satır sonra postlanır. DL-1 ölçütleri bu yollarla kurulur (§16 kurgu kuralı).
- **Eşleşmeyen durum (R13, F-4@54d736e1, r4):** köken için yazılan fail-closed kural (§4.1: eşlenmeyen köken MOD-0175 sayılır) durum için de
  geçerlidir — tablo dışı birleşimde kayıt **asla `Voided` olmaz**. `Conflict` yerine `NoChange (Unmatched)` seçildi: (1) `Cause` kapalı
  listesinde bu durumu adlandıran değer yok ve yeni değer QUARANTINE / INVENTORY sözleşmesini değiştirir; (2) işaret kaydı bütün beslemelerden
  çıkarır, tablo genişletildiğinde kayıt yeniden değerlendirilemez. `NoChange` yazmadığı için kayıt beslemede kalır; her turda bir hata logu
  satırı kayıt numarasıyla düşer (okuma yüzeyi: log + kuyruktaki kayıt); tablo genişletilene kadar sürer. Kod `switch`'in varsayılan dalı
  R13'tür (`ArgumentOutOfRangeException` atmaz — süpürücü turu diğer kayıtlarla sürer).

- **Void** aynı işlemde: kayıt `Status = Voided`, `Voided` alanı, `ActiveDecisionId = null`, `UpdatedAt`, `Version + 1`; aktif karar (`ActiveDecisionId`) varsa ona `Voided` olayı
  (`ErrorCode` yok, `Detail` = REVERSAL numarası); geçmiş `SourceReversed`; merkezi kayıt (enlist).
- **Conflict** aynı işlemde: `SourceReversalConflict` alanı, `Version + 1`; geçmiş `SourceReversalConflict`; merkezi kayıt. Hiçbir stok, durum ya
  da başka kayıt değişmez; aktif karar varsa ona olay yazılmaz — QA kullanıcısı görür (Q-04 = A: işaret + geçmiş, otomatik düzeltme yok);
  düzeltme kayıtta değil stok tarafındadır (A-3), açık kayıtta kayıt üzerinde eylem yoktur (aşağıdaki sınır). **Intake kökenli kayıtta uyuşmazlık (R0a, R6, R7, R9–R11) bugün yalnız aynı kovada başka kaydın stoğu varken doğar**;
  tek kayıtlı kovada defter REVERSAL'ı zaten `INSUFFICIENT_STOCK` ile reddeder (ölçüm: MVP-6 turu R1-b, 422 "available 1, requested 4").
  **R1 tek kayıtlı kovada da doğar** (manuel karantinanın REVERSAL'ı QUARANTINE'deki kendi stoğunu AVAILABLE'a geri taşır). Önlem DL-3'tür (Q-06 = A).
- **Açık kayıtta uyuşmazlık — kabul edilen sınır (F-5@54d736e1, CT kararı MVP-1 CT 2026-11-03; karar yeniden açılmaz, yalnız sonucun adı ve
  sayıma etkisi):** R1 (Manual / Retest / Recall hold `Open` / `DecisionPending`, `ManualBlock` blocked `Open`), R6 (`DecisionPending`, satırı
  postlanmış `Approved` / `ApplyFailed`), R9 ve R10 (blocked `Open`) kayıtları işaretlenir ama **açık kalır**.
  - **Kapanış yolu yok:** Q-05 = A değişmez (elle kapatma, `quarantine.holds.void`, `VoidReason.Manual` yok). Tek kayıtlı kovada R1 sonrası
    kova 0'dır → yeni karar oluşturma ve gönderme V7 ile 409 `QUARANTINE_STOCK_CHANGED` alır (`QuarantineDecisionRules.cs:131-134`,
    `DecisionHandlers.cs:48-49, 376`); R6 / R10'da "Yeniden uygula" kalan satırı kova yetmediği için `ApplyFailed` ile bitirebilir (G-9) ve kayıt
    `DecisionPending` + `ActiveDecisionId` ile kilitli kalır. Bu kayıtlar **kalıcı açık** olabilir. Q-04'ün "QA elle düzeltir"i bu kayıtta
    kayıt üzerinde bir eyleme karşılık gelmez: QA'nın elindeki yalnız işarettir; stok düzeltmesi defterdedir (A-3).
  - **Sayıma etkisi (açıkça):** kayıt `IsOpen`'dır ve `Quantity` değişmez (iz, §4.1) → §8.5'in her yüklemi onu tam miktarıyla sayar: KPI kartları
    (açık kayıt sayısı ve miktarı), `late=true` süzgeci, `lots/{lotId}/quality-status` miktarları ("karantinada N" — kova bakiyesinden fazla
    görünür), yeniden test kuyruğu, CT-Q6 rezervasyon çakışma listesi, MOD-0177 recall istekçisinin açık hold listesi
    (`QuarantineRecallRequester.cs:59` — Recall hold'u açık görmeye devam eder), `EnteredBefore` süzgeci; blocked tarafında `status=Open`
    listesi ve lot kalite durumunun bloke miktarı (`LotQualityStatusHandler.cs:114`). Sayımlar kayıt düzeyindedir, defter düzeyinde değil;
    fark kayıt sayısıyla değil uyuşmazlık rozetiyle görünür (DL-2 §11, AC-28) ve sayfa yardımında yazılıdır (§23). KPI'dan çıkarma yoktur
    (Q-08 yalnız `Voided`'ı dışarıda tutar).
  - **DL-3 ile sınırı:** R1'in canlı yolu bekçiyle kapanır (§8.8 kural 1); R6 / R9 / R10'a yol açan REVERSAL'ı bekçi reddeder — DL-3 etkinken
    açık kayıtta uyuşmazlık yalnız §8.3 "DL-3 sonrası erişilebilirlik" (a)–(c) yollarıyla doğar. Kalıcı açık kaydın kapatılması bu FU'nun
    kapsamında değildir; aynı "kayıtsız kova farkı" sınıfı FU-0175-FU01-OUTBOUND'un kararına not düşüldü (§20).
- `FourEyesViolation`, `Rejected`, `Cancelled`, `TimedOut`, `Withdrawn`, `StartFailed` sonrası hold zaten `Open`'a döner, `ActiveDecisionId`
  boşalır (ebeveyn §8; `QuarantineReopen.ReopenAsync`, `QuarantineApplyEngine.cs:562-573`) → R2.

### 8.4 Geç intake

Intake gözlemcisi kaçmış, REVERSAL intake süpürücüsünden önce gelmişse süpürücü bugün geri alınmış harekete kayıt açar (besleme REVERSAL'ı
sormaz, `QuarantineLedgerReads.cs:138-146`). Kural (Q-09 = A, Beste 2026-11-03): `QuarantineIntakeService.TakeInAsync` kayıt açmadan önce
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

**Sorun (ölçüldü):** karar yazarları durumu işlem **dışında** okur ve olay ekler; `StateOf` "son olay kazanır" (`QuarantineFacts.cs:103`).
Kapanış ile bu yazarlar farklı belgelere yazdığı sürece ikisi de commit olur ve `Voided`'dan sonra başka olay gelir:
- onay: `ApplyOutcomeAsync` okur (`QuarantineApplyEngine.cs:411-412`), `Approved`'ı yeniden okumadan ekler (`:476-489`) → durum `Approved` → motor postlar;
- **gönderim (F-2@ae3fed7d):** `SubmitDecisionHandler` ikinci işlemi Workflow başladıktan sonra hold'u taze okur ve koşulsuz `DecisionPending`'e
  çevirir (`DecisionHandlers.cs:422-434`; sürüm denetimi taze okunan değerle geçer), `WorkflowStarted` `Voided`'dan sonra eklenir → `StateOf`
  `PendingApproval` → onay gelir → postlanır;
- **ret / iptal / zaman aşımı / dört göz (F-12@ae3fed7d):** `CloseWithoutMovementAsync` (`:501-533`) `Voided`'dan sonra `Rejected` / `Cancelled` /
  `TimedOut` / `FourEyesViolation` ekler; `ReopenAsync` `ActiveDecisionId ≠ decisionId` iken kayda yazmadığı için (`:565`) ortak belge de yazılmaz →
  karar "Reddedildi" görünür, `Voided` kaybolur.

**Değişmezler (CT kararı 2026-11-03):**
1. **`Voided` emici son durumdur:** `StateOf` olaylarda `Voided` varsa sıradan bağımsız `Voided` döner (§4.2). `Voided`'dan sonra yazılmış her
   olay (`WorkflowStarted`, `StartFailed`, `Rejected`, `Cancelled`, `TimedOut`, `FourEyesViolation`, `ApplyFailed`) durumu değiştirmez.
2. Bir kararda `Voided`'dan sonra **`Approved` / `Applied` olayı yazılmaz** (2. değişmez 1.'den bağımsız ölçülür — AC-13).
3. `Voided` kayıt (hold / blocked) hiçbir karar yazarınca başka duruma çevrilmez.

**Ortak yazma kilidi = kaydın (hold / blocked) `Version`'ı.** Kapanış (Void / Conflict) kaydı beklenen sürümle yazar. Aynı kararın sonucunu
yazan her yol da **aynı belgeyi beklenen sürümle yazar** ve **işlem içinde** kararın olaylarını yeniden okur (`IQuarantineWriteSession.DecisionEventsAsync`,
`QuarantineRepositories.cs:114`; snapshot okuma). İki işlem aynı belgeye yazdığı için biri Mongo yazma çakışmasıyla yeniden koşar ya da sürüm
denetiminde kaybedip yeniden okur; yeniden koşan son durumu görür. Yazarlar ve `Voided` gördüklerindeki davranış:

| # | Yazar (bugün) | Ortak belge yazımı | `Voided` görürse |
|---|---|---|---|
| W1 | `SubmitDecisionHandler` 1. işlem `LockAsync` (`DecisionHandlers.cs:396-410`, `:453-475`) | var (bugün de sürümlü) | **kod değişmez** (r4, F-3@54d736e1 — bugünkü koda hizalandı): (i) kayıt gönderimin işlem dışı okumasında (`:371`) zaten `Voided` ise ön denetim V5 → 409 `QUARANTINE_HOLD_CLOSED` / `QUARANTINE_BLOCKED_CLOSED` (§8.5 yüklemiyle); (ii) okuma ile kilit işlemi arasında `Voided` olursa `LockAsync` sürüm uyuşmazlığıyla `false` döner (`:464-468`, blocked `:472-476`) → 409 **`QUARANTINE_DECISION_ALREADY_PENDING`** ("The record changed or another decision is waiting for approval.", `:411-413`); `Submitted` olayı ve geçmiş yazılmaz, `StartAsync` çağrılmaz, karar `Draft` / `StartFailed` kalır. Aynı kararın sonraki gönderimi ilk yazmadır, tekrar değil → (i) yolundan 409 `QUARANTINE_HOLD_CLOSED`. Seçim gerekçesi: kilit başarısızlığında kaydı yeniden okuyup `HOLD_CLOSED` döndürmek kodu değiştirir ve kazanç yalnız ileti metnidir; sonraki deneme doğru kodu zaten alır. AC-13 (d) |
| W2 | `SubmitDecisionHandler` 2. işlem (`:422-434`) | var; sürüm uyuşmazlığı bugün `QuarantineRetryException`'ı handler dışına sızdırır (yeniden deneme yok) → `CloseWithoutMovementAsync` deseninde ≤ 3 deneme, her denemede yeniden okuma | kayıt `DecisionPending`'e **çevrilmez**, yazılmaz; `WorkflowStarted` olayı **yazılır** (Q-02: örnek açık kalır; örnek kimliğini taşır — V8'in örnek denetimi buna bakar). Cevap bugünkü gibi `202` + karar gövdesi; gövdede `state Voided` |
| W3 | `ApplyOutcomeAsync` onay işlemi (`:476-489`) | **eklenir:** kayıt `Version + 1`, `UpdatedAt` (başka alan değişmez). **Sürüm uyuşmazlığı dalı (r4, F-2@54d736e1):** bugün yalnız `QuarantineDuplicateException` yakalanır (`:491-495`); eklenen sürüm yazımının `QuarantineRetryException`'ı W2 / W4 ile aynı biçimde ele alınır — `CloseWithoutMovementAsync` deseninde (`:504-527`) ≤ 3 deneme, her denemede kayıt işlem dışında yeniden okunur (`FindHoldAsync` / `FindBlockedAsync`) ve işlem içinde olaylar yeniden okunur; yeniden okumada `Voided` → aşağıdaki sütun, değilse `Approved` yazılır ve akış sürer. Üçüncü uyuşmazlıktan sonra `QuarantineRetryException` W2 / W4 gibi yukarı çıkar (işlem geri alınmıştır, `Approved` olayı yoktur); tüketici yolunda gelen kutusu satırı yazılmış olduğundan (`QuarantineWorkflowOutcomeConsumer.cs:92`) sonucu okuma anı uzlaştırması sonraki okumada uygular (`QuarantineWorkflow.cs:228-270`). `QuarantineDuplicateException` dalı değişmez | `Approved` ve geçmiş yazılmaz; cevap `200 Voided`; `ApplyAsync` çağrılmaz |
| W4 | `CloseWithoutMovementAsync` (`:501-533`) | **eklenir:** kayıt `Version + 1` (`ReopenAsync` yazmasa da) | olay, geçmiş, `ReopenAsync` yazılmaz; cevap `200 Voided` |
| W5 | `RecordAppliedAsync` (`:168-209`) | var (`ApplyHoldDecisionAsync` / `ApplyDispositionAsync` sürümlü; kayıt işlem dışında `:172-173`'te okunur, kapanış araya girerse `ReplaceHoldAsync` / `ReplaceBlockedAsync` `false` → `QuarantineRetryException` → `ApplyAsync`'in mevcut döngüsü yeniden okur, `:124-140`) | **hiçbir şey yazmaz** (r4, F-1@54d736e1): `Applied` olayı, kayıt değişikliği, geçmiş ve işaret yok; işlem içi olay okumasında (`:177`) `Voided` görülünce işlem yazmadan biter. Satır postlanmışsa (`posted` ≥ 1) uyuşmazlık işaretini **ayrı iç komut** yazar — aşağıdaki "W5 / W6 `Voided` dalı". `ApplyAsync` `ApplyResult(Voided, …)` döner |
| W6 | `RecordFailureAsync` (`:144-166`) | gerekmez (olay durumu değiştirmez, değişmez 1) | `ApplyFailed` ve geçmiş yazılmaz; reddedilen satırdan önce postlanmış satır varsa (`postedIds` ≥ 1) W5 ile aynı iç komut dalı |

- `ApplyOutcomeAsync`'in işlem dışı okumasında (`:411-412`) `state == Voided` denetimi örnek kimliği denetiminden (`:414-418`) ve `Approved`
  dalından **önce** gelir → `200 Voided`, olay yok. Terminal listesine (`:435-437`) `Voided` eklenir.
- `ApplyAsync` uygulanabilir durumları (`:81`) `Voided`'ı içermez; her satırı postlamadan hemen önce karar durumunu yeniden okur; `Voided` ise
  durur (satır postlanmaz). Bu yeniden okuma ile post arasındaki kalan pencerede satır postlanırsa W5 / W6 `Voided` dalı işaretletir (görünür,
  sessiz değil). Pencere yalnız çok kayıtlı kovada anlamlıdır; DL-3 onu daraltır ama kapatmaz (§8.3 "DL-3 sonrası erişilebilirlik" (a), (b)).
- **W5 / W6 `Voided` dalı — seçim ve gerekçe (r4, F-1@54d736e1; CT'nin iki seçeneğinden birincisi):** uyuşmazlık işareti
  **`ApplySourceReversalTo{Hold,Blocked}Command` kipine taşındı**; AC-20 için W5 istisnası **açılmadı**. Gerekçe: W5 / W6 `ApplySourceReversal`
  komutlarının dışında koşar ve dört ayrı bağlamda çağrılır — `RefreshDecisionCommand` (`WorkflowHandlers.cs:61` → uzlaştırıcı, `Operation Execute`,
  aktör onaylayan / çağıran), `ReapplyDecisionCommand` (`DecisionHandlers.cs:515`), okuma anı uzlaştırması yapan sorgu handler'ları
  (`QueryHandlers.cs:142, 196, 232, 269, 309`, `LotQualityStatusHandler.cs:72` → `QuarantineWorkflow.cs:270`; işaretli komut yok) ve MediatR
  dışı Workflow tüketicisi (`QuarantineWorkflowOutcomeConsumer.cs:98-99`; işaretli komut yok). W5'te yazılan işaretin merkezi kaydı bu
  bağlamların ikisinde hiç oluşmaz (işaretli komut yokken kayıt yazılmaz, G-14), ikisinde yanlış aktör ve işlem adıyla oluşur; ayrı ölçüm dört
  yolu ayrı ayrı istisna yapardı. Komut kipi tek yoldur: tam bir kez, `ActorType System`, `RequestType` komut adı (AC-20 değişmeden geçerli).
  - **Akış:** W5 / W6 işlemi yazmadan bittikten sonra, **hiçbir işlemin içinde değilken**, `ApplyAsync` kaydı yeniden okur
    (`FindHoldAsync` / `FindBlockedAsync`) ve `_sender.Send(new ApplySourceReversalTo{Hold,Blocked}Command(record.Voided.ReversalTransactionId))`
    gönderir — `ApplyAsync`'in aynı `ISender` ile iç komut göndermesinin emsali var (`ApplyApprovedLotStatusChangeCommand`, `QuarantineApplyEngine.cs:93`;
    bu da aynı dört bağlamdan çağrılır). Komut kendi işleminde §8.3'ü uygular → R0a → `Conflict (LinesPosted)`.
  - **REVERSAL kimliği / numarası / postlayan nereden okunur:** komutun girdisi kaydın kapanışta yazılmış `Voided.ReversalTransactionId`'sidir
    (§4.1; W5 bunu yalnız kayıttan okur, ödünç değer üretmez). Komut REVERSAL'ı V2 ile defterden kiracı + şirket kapsamında yeniden okur;
    `SourceReversalConflict.ReversalTransactionNumber` = REVERSAL'ın `TransactionNumber`'ı, `.ReversedByUserId` = `InventoryTransaction.CreatedBy`,
    geçmiş satırının `ByUserId` / `Reason` / `CorrelationId`'si aynı işlemden (AC-22) — gözlemci ve süpürücü yoluyla aynı kaynak. Onaylayan
    kullanıcı ya da tüketicinin kapsamındaki aktör hiçbir alana girmez.
  - **Merkezi kayıt:** komutun kendi `ICentralAuditDeclaration`'ı, sistem aktörü kipi gönderene bağlı değildir (§5) → `ActorType System`,
    `ActorId …0175`, `CorrelationId` = REVERSAL'ınki, `WrittenInCommandTransaction = true`, tam 1 satır. İç içe gönderimde dıştaki
    `RefreshDecisionCommand` / `ReapplyDecisionCommand` kaydı kendi aktörüyle ayrı kalır (yığın, §5); W5 / W6 dıştaki komut için yazma
    yapmadığından dıştaki komut bu dalda merkezi kayıt bırakmaz (`RequireEnlistment`, `CentralAuditRecorder.cs:160-168`). Tüketici yolunda kapsam
    tüketicinin kurduğu kiracı + şirkettir (`QuarantineWorkflowOutcomeConsumer.cs:89`) → `TenantId` / `CompanyId` kaydınki.
  - **Gönderim başarısız olursa** (istisna ya da başarısız `Response`; ör. K2 enlist hatası): `ApplyAsync` hata logu
    `quarantine.source_reversal.mark_dispatch_failed` yazar ve yine `ApplyResult(Voided, …)` döner (çağıranın cevabı değişmez); işareti
    süpürücünün geçersiz beslemesi yazar (§8.2). Gönderim aynı işlem için ikinci kez gelirse komut R0 → `NoChange (AlreadyRecorded)`.
- Mekanizma uygulamaya açıktır (ör. işlem içi kayıt okuması için `IQuarantineWriteSession`'a okuma eklemek, §5); AC-13 (a)–(d) ve AC-39 bağlayıcıdır.
- Platform Workflow örneği açık kalır (sistem kimliği yok; Q-02 = A). WorkCenter'daki onay `Voided` kararda etkisizdir; DisplayContext bağlantısı
  kaydı "Geçersiz" gösterir (DL-2).
- Karar e-imzası geçerli kalır: karar yalnız eklemeli, imzalanan özet değişmez (ebeveyn §8 e-signature).

### 8.7 Sözleşme — QUARANTINE 1.4.0 (yalnız ekleme)

- `HoldStatus` += `Voided`; `BlockedStatus` += `Voided`; `DecisionState` += `Voided`; `HistoryAction` += `SourceReversed`, `SourceReversalConflict`
  (`quarantine.openapi.yaml:400, 405, 407, 408`).
- `Hold` / `HoldDetail` / `BlockedStock` (+ ayrıntı) isteğe bağlı `voided` (`RecordVoid`: `atUtc`, `reason` [`SourceReversed`], `reversalTransactionId`,
  `reversalTransactionNumber`, `reversedBy`, `decisionId?`) ve `sourceReversalConflict` (`atUtc`, `reversalTransactionId`, `reversalTransactionNumber`,
  `reversedBy`, `cause` [`Decided`, `LinesPosted`, `QuantityChanged`, `OwnMovementReversed`]); `HistoryEntry` isteğe bağlı `reversalTransactionId`.
  Hiçbiri `required` değil. `cause` yeni bir enum'dur (1.3.0'da yok) → dört değeri birlikte 1.4.0'da girer, mevcut enum değişmez.
- `listHolds` / `listBlocked` `status` sorgu parametresi aynı enum'u gösterdiği için `Voided` ile süzülebilir; **varsayılan liste değişmez**
  (bugün durum verilmeyince hepsi döner; `Voided` satırlar görünür, `Decided` gibi sona sıralanır, KPI'ya girmez — Q-08 = A).
- Yeni hata kodu yok: kapanmış kayıtta eylemler mevcut `QUARANTINE_HOLD_CLOSED` (`QuarantineDecisionRules.cs:39`) ve blocked için mevcut
  `QUARANTINE_BLOCKED_CLOSED` (`:40`) kodunu döner; ileti "kaynak geri alındı" der.
- Olay yayını yok (Inventory'de taşıma bağlı değil, ebeveyn §8). `x-changelog` 1.4.0 satırı "No existing path, parameter, property, type, required
  list, status code or enum value changed" cümlesiyle (1.1.0–1.3.0 emsali).

### 8.8 DL-3 — defter bekçisi (Q-06 = A)

Karar: Q-06 = A (Beste 2026-11-03; §19.5).

- MOD-0173 dikişi `IReversalPrecondition.CheckAsync(InventoryTransaction original, CancellationToken)` → `Allow` | `Refuse(code, message, details)`;
  `InternalMovementPostingService.BuildReversalAsync` "zaten geri alındı" denetiminden (`:253-256`) sonra çağırır; ret → 422, hiçbir şey
  yazılmaz, yerel `inventory_audit`'e ret satırı (`ExpiryPostingPolicy.FlushRefusalAuditAsync` emsali).
- MOD-0175 uygulaması iki kural: (1) aslın `SourceModule = MOD-0175` → ret (DEC-INV-08: 0175'in kendi `STATUS_TRANSFER` / `SCRAP` /
  `SUPPLIER_RETURN` / `LOCATION_TRANSFER` hareketinin REVERSAL'ı kalite kararını atlar; düzeltme yeni 0175 kararıyla) — bu, §8.3 R1'in (MOD-0175
  kökenli kayıt) canlı yolunu kapatır; `details.cause = OwnMovementReversed`, `recordNumber` / `recordType` bu hareketi `SourceMovementId` olarak
  taşıyan kayıt varsa dolu, yoksa (ör. karar satırının `STATUS_TRANSFER`'ı) yok; (2) aslı intake kökenli bir kaydın `SourceMovementId`'si ve §8.3'e
  göre sonuç `Conflict` (R6, R7, R9–R11) ya da karar `Approved` / `ApplyFailed` (R5) ya da tablo dışı (R13, r4 — fail-closed: DL-1 bu kaydı
  kapatmayacağı ve işaretlemeyeceği için REVERSAL'a izin vermek kayıtsız kova farkı doğurur) → ret. `details.cause` kapalı listesi (INVENTORY 1.8.0):
  `OwnMovementReversed` (kural 1), `Decided` / `LinesPosted` / `QuantityChanged` (kural 2, §8.3'ün `Cause`'u), `DecisionInProgress` (kural 2, R5),
  `Unmatched` (kural 2, R13; r4 — INVENTORY 1.8.0 henüz yazılmadığı için liste bu sürümde genişler, yayımlanmış sözleşme değişmez).
  Diğer her durumda (R2–R4, R8, kayıt yok) `Allow`; bekçi R0 / R0a / R12'ye (kayıt zaten `Voided` ya da işaretli) varmaz — `Voided` kaydın
  kaynağı zaten geri alınmıştır, ikinci REVERSAL `ALREADY_REVERSED` ile bekçiden önce reddedilir (G-5).
- Okuma hatası → 503 `INVENTORY_UNAVAILABLE` (mevcut kod), fail-closed.
- INVENTORY ek kodu `REVERSAL_QUALITY_RECORD_LOCKED` (422, `details: { recordNumber, recordType, cause }`): `InventoryErrorCodes.All`
  kapalı kataloğuna bu paketle girer (MOD-0173-FU03 CT-02 emsali; XMC-001 MOD-0173 izni, §5).
- **Sürüm (Q-07, CT kararı 2026-11-03):** INVENTORY **1.8.0** — MOD-0173-FU03'ün 1.6.0 (DL-1) ve 1.7.0 (DL-2) adımlarından sonraki ek sürüm
  (FU03 onaylı, `c03ac5511`). `x-changelog` 1.8.0 satırı yalnız ekleme cümlesiyle; karşılaştırma tabanı 1.7.0'ın donmuş kopyası. Sıra:
  DL-3'ün sözleşme adımı FU03'ün 1.7.0'ı dalda (`feature/sce/mod-0173-inventory-ledger-valuation`) olduktan sonra yazılır; 1.7.0 dalda değilken
  DL-3'ün sözleşme adımı bekler (numara atlanmaz, 1.6.0 / 1.7.0 bu paketle yazılmaz). Sıra değişirse yeni numara CT kararıyla ve yeni paket
  sürümüyle yazılır.
- **MVP-6 kapısı (Q-10, açık — Natig):** DL-3, MOD-0186 geç yazanının kaydı karar verilmiş iadenin REVERSAL'ını 422 ile reddeder; mevcut
  `ReversalFailed:<kod>` yolu (`ReturnLateWriterPolicy.cs:43`) bunu taşır. Bu etkinin MVP-6 CT kabulü **DL-3 kabul kapısıdır** (§22): DL-3 kabul
  kaydı Q-10 kararını (tarih, veren) taşımadan kapanmaz. Kabul edilmezse DL-3 yeni paket sürümüyle yeniden kararlaştırılır; DL-1 / DL-2 etkilenmez.
- Kalan pencere: bekçi işlem dışında okur; bekçi ile commit arasında karar uygulanırsa DL-1 `Conflict` yazar (görünür, sessiz değil). DL-3 etkinken
  DL-1'in hangi satırlarının hangi yoldan erişilir kaldığı §8.3 "DL-3 sonrası erişilebilirlik"tedir.
- **Web (F-5@ae3fed7d):** S-2 "Reversal" onay penceresi ve "Düzelt" akışı reddi okurun dilinde söyler: `InventoryErrorMessages.ByCode`
  `REVERSAL_QUALITY_RECORD_LOCKED` → `ErrReversalQualityRecordLocked`; metin (tr) "Bu hareket geri alınamaz: kaynağı olduğu karantina / bloke kaydı
  kalite kararına bağlı. Düzeltmeyi Karantina ekranından yeni kararla yapın." — 7 dil, `StockLedgerIndex` ve `StockMovementsIndex` ailelerinde.
  Bugün kod haritada yok → 422 genel cümleye düşer (`InventoryErrorMessages.cs:10-40`, `KeyFor`).

### 8.9 Durum ve komut tablosu

| Tetik | Kaynak | Komut | Sonuç | Stok etkisi |
|---|---|---|---|---|
| REVERSAL commit (gözlemci) | MOD-0173 | `ApplySourceReversalToHoldCommand` / `…ToBlockedCommand` | Void / Conflict / NoChange (§8.3 R0a–R13) | yok |
| Süpürücü turu — açık besleme | Inventory arka plan | aynı | aynı | yok |
| Süpürücü turu — terminal besleme (F-3) | Inventory arka plan | aynı | Conflict / NoChange (terminal kayıtta Void satırı yok) | yok |
| Süpürücü turu — geçersiz besleme (r4) | Inventory arka plan | aynı (`Voided.ReversalTransactionId` ile) | R0a → Conflict (`LinesPosted`); satırsız → NoChange, yazma yok | yok |
| Gönderimin kilit işlemi, kayıt okuma–kilit arasında `Voided` oldu (r4, F-3@54d736e1) | `SubmitDecisionHandler` (W1) | — | 409 `QUARANTINE_DECISION_ALREADY_PENDING` (bugünkü kod); `Submitted` yok, `StartAsync` yok; sonraki gönderim 409 `QUARANTINE_HOLD_CLOSED` | yok |
| Gönderim 2. işlemi `Voided` kayıtta (F-2) | `SubmitDecisionHandler` | — | kayıt değişmez, `WorkflowStarted` yazılır, karar `Voided` kalır | yok |
| Workflow sonucu `Voided` kararda (F-12) | `ApplyOutcomeAsync` / uzlaştırıcı | — | `200 Voided`, olay yok | yok |
| Onay işleminde kayıt sürümü değişti, `Voided` değil (r4, F-2@54d736e1) | `ApplyOutcomeAsync` (W3) | — | ≤ 3 deneme, yeniden okuma → `Approved`, uygulama sürer; üçüncü uyuşmazlıkta hata yukarı, `Approved` yok | uygulama kadar |
| Satır `Voided`'dan sonra postlandı (r4, F-1@54d736e1) | `ApplyAsync` (W5 / W6) | `ApplySourceReversalTo{Hold,Blocked}Command` (iç gönderim, işlem dışında) | W5 / W6 hiçbir şey yazmaz; komut R0a → Conflict (`LinesPosted`), merkezi kayıt `System` | postlanan satır kadar (önlenemeyen pencere, işaretli) |
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
| Uyuşmazlık | `quarantine-ui.js`, `details.js`, `blocked.js` | `sourceReversalConflict` dolu her kayıtta (durumdan bağımsız: `Open`, `DecisionPending`, `Decided`, `Closed`, `SampleRetained` ve — r4, §8.3 R0a — `Voided`; `Voided` kayıtta "Geçersiz" afişiyle birlikte) uyarı rozeti "Kaynak geri alındı · uyuşmazlık" + neden metni + afiş ("stok bu kaydın dışında değişti; QA kontrolü gerekir") |

Yeni resx anahtarları (7 dil, `Resources/Views/Quarantine/Quarantine.*.resx`; bugün her dilde 451 anahtar, eşit):
`Quarantine.HoldState.Voided`, `Quarantine.Blocked.Status.Voided`, `Quarantine.Workflow.Voided`, `Quarantine.History.SourceReversed`,
`Quarantine.History.SourceReversalConflict`, `Quarantine.Details.VoidedBanner`, `Quarantine.Details.ConflictBanner`,
`Quarantine.Common.ConflictBadge`, `Quarantine.Conflict.Cause.{Decided,LinesPosted,QuantityChanged,OwnMovementReversed}` (12 anahtar).
Her yeni anahtar aynı değişiklikte `Views/Quarantine/Quarantine.cs` `Keys` listesine de eklenir (F-8@ae3fed7d; liste dışındaki anahtar
`window.QuarantineL10n`'a girmez, `QuarantineWebTests` 7 resx'i bu listeye sabitler — `Quarantine.cs:3-8`). Uyuşmazlık rozeti `Open` /
`DecisionPending` hold'da ve `Open` blocked'ta da çizilir (R1, R6, R9, R10 açık kayıtta, R0a `Voided` kayıtta işaret bırakır). Sayfa yardımı (UXH-001):
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
| V4 | §8.3 tablosu; köken §4.1 (`OriginOf`, fail-closed: eşlenmeyen köken MOD-0175 sayılır); durum fail-closed: eşleşmeyen birleşim R13 (r4) | Void / Conflict / NoChange (`AlreadyRecorded` / `LostRace` / `NoRecord` / `Unmatched`) |
| V5 | kapanmış kayıtta karar oluşturma / gönderme | 409 `QUARANTINE_HOLD_CLOSED` (hold), blocked için 409 `QUARANTINE_BLOCKED_CLOSED` |
| V6 | kapanmış kayıtta İncelemeye al / not güncelle / İncelemeyi tamamla | aynı 409 |
| V7 | `Voided` kararı yeniden uygula | 409 `QUARANTINE_DECISION_NOT_APPLICABLE` (`QuarantineApplyEngine.cs:83`) |
| V8 | `Voided` karara gelen Workflow sonucu (onay, ret, iptal, zaman aşımı; örnek kimliği verilmiş ya da verilmemiş) | 200 `Voided`, olay yazılmaz (§8.6 W3, W4; `Voided` denetimi örnek kimliği denetiminden önce) |
| V8a | gönderimin ikinci işlemi kaydı `Voided` bulur | kayıt değişmez; `WorkflowStarted` yazılır; cevap bugünkü `202` + karar gövdesi, `state Voided` (§8.6 W2) |
| V8b (r4) | gönderimin kilit işlemi, kayıt okunduktan sonra `Voided` olmuş kayda yazamaz | 409 `QUARANTINE_DECISION_ALREADY_PENDING` (bugünkü kod, değişmez; §8.6 W1 (ii)); sonraki gönderim V5 |
| V8c (r4) | `Voided` kararın satırı kapanıştan sonra postlandı | W5 / W6 yazmaz; iç komut → `Conflict (LinesPosted)` (§8.3 R0a, §8.6) |
| V9 | geç intake: kaynak geri alınmış | kayıt açılmaz |
| V10 (DL-3) | §8.8 bekçi kuralları | 422 `REVERSAL_QUALITY_RECORD_LOCKED` / 503 `INVENTORY_UNAVAILABLE` |

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
16. MOD-0175 kökenli kayıt (Manual / Retest / Recall / Rework hold, `ManualBlock`) kaynağının REVERSAL'ı (DL-1, ya da DL-3'ten önceki eski veri) → `Voided` **değil**, `Conflict` (`OwnMovementReversed`); durum ve aktif karar değişmez.
17. Gönderim penceresi: karar `Submitted`, Workflow başlarken kapanış commit olur → hold `Voided`, karar `Voided`; gönderimin ikinci işlemi hold'u `DecisionPending`'e çevirmez; sonraki onay → hareket yok.
18. Kapanıştan sonra ya da kapanışla yarışan ret / iptal / zaman aşımı → karar `Voided` kalır, `Rejected` / `Cancelled` / `TimedOut` olayı ve hold'u `Open`'a çeviren yazım yok.
19. Uygulanmış kararın kaynağı gözlemci kaçarken geri alındı → terminal besleme `Conflict` yazar (süpürücü emniyet ağı).
20. Açık kayıt sayısı sayfa boyutunu aşar → imleç ilerler, geri alınmış kayıt ⌈N / PageSize⌉ turda kapanır.
21. (r4) `Voided` kararın satırı kapanıştan sonra postlandı (uygulama motoru satır öncesi yeniden okumayı kapanıştan önce geçmişti) → `Applied` yok, kayıt `Voided` kalır, iç komut `Conflict (LinesPosted)` yazar (merkezi kayıt `System`, tam 1); iç gönderim başarısızsa geçersiz besleme yazar.
22. (r4) Gönderimin kayıt okuması ile kilit işlemi arasında kapanış commit olur → 409 `QUARANTINE_DECISION_ALREADY_PENDING`, Workflow başlamaz; sonraki gönderim 409 `QUARANTINE_HOLD_CLOSED`.
23. (r4) Onay işleminde kayıt sürümü başka bir yazarla değişti (kapanış değil) → yeniden okuma, ikinci denemede `Approved`; uyuşmazlık sızmaz.
24. (r4) Tablo dışı durum birleşimi → kayıt kapanmaz, yazma yok, hata logu (`Unmatched`).

## 14. Authorization Convention

- **Yeni izin anahtarı yok.** Kapanış sistem yoludur; HTTP ucu yoktur. Tetikleyen REVERSAL'ın kendi yetkisi defterindir
  (`inventory.movements.create`, MOD-0173); MOD-0175 bunu yeniden denetlemez.
- Okuma ve ekranlar ebeveynin anahtarlarıyla: `quarantine.holds.read` (kuyruk, detay, bloke stok). Manifest değişmez
  (`QuarantineBlockedStockManifestProvider`); yalnız `quarantine.*` (CAT-01).
- Elle kapatma yolu yok (Q-05 = A, Beste 2026-11-03): `quarantine.holds.void` anahtarı ve `VOIDHOLD` satır eylemi bu pakette **yoktur**;
  manifest değişmez.
- Aktör (Q-03 = A, Beste 2026-11-03): merkezi kayıtta `ActorType System`, `ActorId` sabit `00000000-0000-0000-0000-000000000175`
  (`QuarantineIntakeSweeper.QuarantineSweeperActor`, `QuarantineLedgerReads.cs:217`; kapsam §5 XMC-001); ürün geçmişinde REVERSAL'ı postlayan
  kullanıcı (`ByUserId`); ikisi `CorrelationId` ile defterin `InventoryMovementReversed` denetim satırına bağlanır.

## 15. Gateway / API Routing Decision

**Değişiklik yok.** Okuma uçları mevcut `/api/quarantine/{everything}` → 5067 rotasından geçer; yeni uç yok. INVENTORY rotası değişmez
(DL-3 yalnız mevcut `postMovement` cevabına ek hata kodu getirir). `ocelot.json`'a dokunulmaz; integration-agent görevi yok.

## 16. Acceptance Criteria

Ortak fixture: kiracı T1 / şirket LE1; depo W, lokasyon L1; lotlu ürün; test ayarlarında `QuarantineSourceReversalSweeper` ve intake süpürücüsü
**kapalı** (testler süpürücüyü `SweepAsync` ile elle koşar). Her sabotaj tek tek uygulanır; kabul kaydına "kırmızı → geri al → yeşil, diff boş" yazılır.

**Kurgu kuralı (F-4@ae3fed7d) — DL-1 ölçütleri DL-3 etkin iken de geçerlidir.** DL-3 bekçisi §8.3 R1, R5–R7, R9–R11'e yol açacak REVERSAL'ı
reddeder (§8.8). Bu satırları ölçen DL-1 ölçütleri (AC-4, AC-6, AC-7, AC-8, AC-10 (b), AC-11 Conflict dalı, AC-12, AC-15 (b), AC-37, AC-39) REVERSAL'ı
ya bekçinin izin verdiği anda postlar ya da eski veriyi kurar; kayıt REVERSAL'dan **sonra** ilerletilir (§8.3 "DL-3 sonrası erişilebilirlik" (b), (c)):
(i) **geç ilerleme kurgusu** — `InventoryApiFactory`'nin gözlemcisiz varyantı (DI'da `QuarantineSourceReversalObserver` kayıtlı değil; kural,
handler, süpürücü ve bekçi üretim kodudur, test kancası yok): REVERSAL kayıt `Open` / `PendingApproval` iken 201 alır (bekçi `Allow` — R2 / R4 / R8),
sonra karar onaylanır / uygulanır / kısmi imha yapılır, kapanış `SweepAsync` ile ya da test gözlemciyi doğrudan çağırarak koşar;
(ii) **eski veri kurgusu** (R1 için tek yol) — REVERSAL işlemi `inventory_transactions`'a doğrudan yazılır (DL-3'ten önce commit olmuş REVERSAL'ın
temsili; AC-16 (a)'daki doğrudan yazılmış kayıt emsali). Bekçi hiçbir testte `Allow`'a alınmaz. AC-24'ün DL-3 sonrası süzgeçsiz tam koşusu bu
ölçütleri değiştirmeden içerir; AC-33 bunu DL-3 kabulünde adıyla ölçer.

**DL-1 — arka uç**

- [ ] AC-1 (zorunlu) — `CUSTOMER_RETURN` 4 EA → `QUARANTINE` (manuel hareket) gözlemciyle `Open / Waiting` hold açar; aynı hareketin REVERSAL'ı 201 → aynı istek içinde hold `status Voided`, `voided.reason SourceReversed`, `voided.reversalTransactionId` = REVERSAL kimliği, `voided.reversedBy` = postlayan, `quantity` 4 (değişmez), `activeDecision` yok; geçmişte tam 1 `SourceReversed` satırı. Sabotaj: `QuarantineSourceReversalObserver` kaydını DI'dan kaldır → hold `Open` kalır, test kırmızı.
- [ ] AC-2 (zorunlu) — Tablo güdümlü: `GOODS_RECEIPT_PO` → `QUALITY_INSPECTION`, `OPENING_BALANCE` → `QUARANTINE`, `COUNT_GAIN` → `QUARANTINE` ve `InReview` durumundaki hold'un her biri REVERSAL sonrası `Voided`; `reviewState` korunur. (b) Eşleşmeyen durum (r4, F-4@54d736e1, §8.3 R13): `SourceReversalRules` birim testinde tablo dışı satırlar — intake hold `DecisionPending` + aktif karar `Draft`, `Withdrawn` ve `StartFailed`; intake hold `Open` + `ActiveDecisionId` dolu + karar `PendingApproval`; tanımsız `HoldStatus` değeri (`(HoldStatus)99`) ve tanımsız `DecisionState` değeri — her biri `NoChange (Unmatched)`, asla `Void`; entegrasyonda doğrudan yazılmış `DecisionPending` + `Draft` aktif kararlı intake hold'un REVERSAL'ı → kayıt, `Version`, karar olayları, geçmiş ve `central_audit_outbox` değişmez, `quarantine.source_reversal.unmatched` logunda kayıt numarası var; aynı turdaki ikinci (eşleşen) kayıt yine `Voided` olur (tur durmaz). Sabotaj (ayrı ayrı): kuralı `Source = CustomerReturn` ile sınırla → (a) üç satır kırmızı; varsayılan dalı `Void`'e çevir → (b) tablo dışı satırlar `Voided`, kırmızı; varsayılan dalda `ArgumentOutOfRangeException` at → (b) ikinci kayıt kapanmaz, kırmızı.
- [ ] AC-3 (zorunlu) — (a) `PendingApproval` kararlı hold (test onay portu): REVERSAL → hold `Voided`, karar durumu `Voided` (tek `Voided` olayı), `voided.decisionId` dolu; ardından test portu `Approved` sonucu verir → `ApplyOutcomeAsync` `200 Voided`, yeni olay yok, defterde `quarantine:{decisionId}:*` anahtarlı hareket yok; uzlaştırıcı kararı seçmez. (b) Ret sonucu (F-12@ae3fed7d): aynı kurgu üç ayrı hold / kararla; her birinde `Voided`'dan sonra test portu sırasıyla `Rejected`, `Cancelled`, `TimedOut` verir → her biri `200 Voided`; karar olaylarında `Rejected` / `Cancelled` / `TimedOut` yok; hold `Voided`, `activeDecision` yok; geçmişte `Rejected` / `Cancelled` / `TimedOut` satırı yok; ayrıca örnek kimliği verilmiş sonuç da `200 Voided` (V8). Sabotaj (ayrı ayrı): `ApplyOutcomeAsync`'te `Voided` denetimini kaldır → (a) 409 ya da `ApplyFailed`, kırmızı; `CloseWithoutMovementAsync`'in `Voided` denetimini kaldır → (b) `Rejected` olayı yazılır, kırmızı; `StateOf` emiciliğini kaldır → (b) karar durumu `Rejected`, kırmızı.
- [ ] AC-4 (zorunlu) — Kurgu (i), çok kayıtlı kova değil: hold X (`Approved`, uygulama test engeliyle satır postlanmadan durdurulmuş) ve hold Y (satırsız `ApplyFailed`): her ikisinin kaynağının REVERSAL'ı kayıt `PendingApproval` iken 201 (gözlemcisiz); sonra X onaylanır (engel), Y onaylanır → kova boş olduğu için defter `INSUFFICIENT_STOCK` → `ApplyFailed`, 0 satır; `SweepAsync` → ikisi de `Voided` + karar `Voided` (R5); `reapplyDecision` → 409 `QUARANTINE_DECISION_NOT_APPLICABLE`; engel kalkınca X'in satırı postlanmaz; defter hareket sayısı yalnız iki REVERSAL kadar artar. Sabotaj: `ApplyAsync` uygulanabilir durumlarına `Voided` ekle → X'in satırı postlanmaya çalışılır, kırmızı.
- [ ] AC-5 (zorunlu) — `Voided` hold'da `createDecision`, Draft kararın `submitDecision`'ı, `startReview`, `updateHold` → 409 `QUARANTINE_HOLD_CLOSED`; hiçbir kayıt değişmez. Sabotaj: `QuarantineDecisionRules.HoldState` `Voided`'ı `Open` say → `createDecision` 201, test kırmızı.
- [ ] AC-6 (zorunlu) — Kurgu (i), çok kayıtlı kova: hold A (4, kaynak t1) ve hold B (4, kaynak t2) aynı kovada; gözlemcisiz fabrikada t1'in REVERSAL'ı A `Open` iken 201 (kovada B'nin 4'ü kalır); sonra A'nın Release kararı onaylanır ve uygulanır (V7 kova bakiyesine bakar, G-10) → A `Decided`; `SweepAsync` (terminal besleme) → A `Decided` kalır, `sourceReversalConflict.cause Decided`, A'da 1 `SourceReversalConflict` geçmiş satırı; B'nin durumu, miktarı, `Version`'ı değişmez. Sabotaj (ayrı ayrı): uyuşmazlık dalını kapanışa çevir → A `Voided`, kırmızı; kovaya göre eşle → B işaretlenir, kırmızı; terminal beslemeyi kaldır (F-3) → A işaretsiz, kırmızı.
- [ ] AC-7 (zorunlu) — Kurgu (i): hold A (4, kaynak t1) ve hold B (2) aynı kovada; t1'in REVERSAL'ı A'nın iki satırlı kısmi kararı (Release 2 + Block 2) `PendingApproval` iken 201 (kovada 2 kalır); onay → (a) satır 1 postlandıktan sonra test engeli, `SweepAsync` → `Conflict` (`cause LinesPosted`, R6), durum `DecisionPending`; (b) ayrı koşu, engelsiz: satır 1 postlanır, satır 2 `INSUFFICIENT_STOCK` → satırı postlanmış `ApplyFailed`, `SweepAsync` → `Conflict` (`LinesPosted`). İkisinde karar olayı yazılmaz. Sabotaj: postlanmış satırı olaylardan say (deftere bakma) → (a) `Voided`, kırmızı.
- [ ] AC-8 (zorunlu) — Uygulama motoru `Voided` kararın satırını postlamaz. Kurgu (i), çok kayıtlı kova: hold A (4, `PendingApproval`) ve hold B (4) aynı kovada; A'nın kaynağının REVERSAL'ı 201 (gözlemcisiz; kovada B'nin 4'ü kalır); onay → `Approved` yazılır, `ApplyAsync` durum denetimini geçtikten sonra test engeli; `SweepAsync` kapanışı commit eder (R5) → engel kalkar → satır postlanmaz, `Applied` yazılmaz, `RecordAppliedAsync` hold'u `Decided`'a çevirmez. Sabotaj: satır öncesi yeniden okumayı kaldır → B'nin stoğuyla defterde `quarantine:{id}:1` anahtarlı hareket, kırmızı.
- [ ] AC-9 (zorunlu) — Inbound → `BLOCKED` 3 EA `BlockedStock` (origin `OtherInbound`) açar; REVERSAL → `status Voided`, `voided` dolu, geçmiş `SourceReversed`; bloke stok listesi `status=Open` süzgecinde görünmez. Sabotaj: gözlemciyi yalnız hold'a bağla → blocked `Open` kalır, kırmızı.
- [ ] AC-10 (zorunlu) — (a) `PendingApproval` imha kararlı intake blocked (`OtherInbound`, 3 EA) → REVERSAL 201 → `Voided` + karar `Voided` (R8). (b) Kurgu (i): intake blocked X (3, kaynak t) ve blocked Y (3) aynı kovada; t'nin REVERSAL'ı X `Open` ve kararsızken 201 (R8 `Allow`; kovada 3 kalır); sonra X'te 1 EA imha uygulanır (miktar 3 → 2); `SweepAsync` → `Conflict` (`cause QuantityChanged`, R9); Y değişmez. Sabotaj: miktar karşılaştırmasını kaldır → (b) `Voided`, kırmızı.
- [ ] AC-11 (zorunlu) — Sıralı tam bir kez: (a) Void dalı: gözlemci kapatır; `SweepAsync` iki kez → 0 değişiklik; gözlemci aynı işlemle yeniden çağrılır → `NoChange (AlreadyRecorded)`; sonuç: geçmiş `SourceReversed` 1, `central_audit_outbox`'ta bu kayıt için 1 satır, `Version` bir kez arttı. Ters sıra: gözlemci test dekoratörüyle hata atar (posting yine 201), süpürücü kapatır, gözlemci yeniden çağrılır → 0. (b) Conflict dalı (F-3@ae3fed7d): AC-6 kurgusunda `SweepAsync` işaretler; `SweepAsync` iki kez daha → 0 (işaretli kayıt beslemeye girmez); gözlemci REVERSAL işlemiyle doğrudan çağrılır → `NoChange (AlreadyRecorded)`; 1 `SourceReversalConflict` geçmiş satırı, 1 merkezi kayıt. Sabotaj (ayrı ayrı): "zaten kapalı" erken dönüşünü (R0) kaldır → (a) ikinci geçmiş satırı, kırmızı; beslemenin `SourceReversalConflict` boş süzgecini ve R0'ı kaldır → (b) ikinci işaret satırı, kırmızı.
- [ ] AC-12 (zorunlu) — Eşzamanlı tam bir kez (F-13@ae3fed7d): gözlemcisiz fabrikada REVERSAL 201 (hold `Open`); `QuarantineSourceReversalObserver.OnPostedAsync(REVERSAL)` ve `SweepAsync` `Task.WhenAll` ile; test dekoratörü (`IQuarantineRepository` sarmalayıcısı) iki çağıranın ilk kayıt okumasını (`FindHoldBySourceMovementAsync` ya da uygulamadaki eşdeğer ilk okuma) ikisi de okuyana kadar bekletir (engel = 2) → ikisi de işleme girer. Sonuç: 1 geçmiş, 1 merkezi kayıt, `Version` bir kez arttı; komut sonuçlarından biri `Voided`, diğeri **`NoChange (LostRace)`** — kaybedenin yeniden okuma dalı adıyla sayılır (sürüm uyuşmazlığı ya da Mongo yazma çakışması sonrası sürüm uyuşmazlığı; iki yol aynı dala düşer, §8.2). `TransactionRetryCounter` `quarantine` sayısı kabul kaydına bilgi olarak yazılır, ölçüt değildir. Sabotaj (ayrı ayrı): beklenen `Version` olmadan değiştir → 2 geçmiş satırı, kırmızı; `LostRace` dalında yeniden okumayı kaldır (hatayı yukarı at) → kaybeden hata döner, sonuç `LostRace` değil, kırmızı.
- [ ] AC-13 (zorunlu) — Kapanış ↔ karar yazarları (F-2@ae3fed7d, F-12@ae3fed7d; §8.6). Her yarış iki sırayla koşulur; test dekoratörleri `InventoryApiFactory` DI'ında. (a) Kapanış ↔ onay (W3): dekoratör onay işlemini işlem içi olay okumasından sonra kapanış commit olana kadar bekletir → bir kararda `Voided`'dan sonra `Approved` / `Applied` olayı hiçbir koşuda yok; tam biri kazanır: ya `Voided` (defter hareketsiz) ya `Applied` + `Decided` (ardından kapanış `Conflict` yazar). (a2) Sürüm uyuşmazlığı, `Voided` değil (r4, F-2@54d736e1): MOD-0175 kökenli Manual hold, `PendingApproval` kararlı; kaynağının REVERSAL'ı doğrudan yazılmış (kurgu (ii)); dekoratör onay işlemini kayıt okumasından sonra bekletir, bu arada `SweepAsync` R1 `Conflict (OwnMovementReversed)` yazar (`Version + 1`, durum değişmez) → onayın ilk denemesi sürüm uyuşmazlığına düşer, ikinci denemede kaydı yeniden okur, `Approved` tam 1 kez yazılır ve uygulama sürer (tek kayıtlı kova boş olduğundan `ApplyFailed` beklenir); `ApplyOutcomeAsync` hata atmaz, dekoratörün saydığı deneme sayısı 2; ayrı koşu: dekoratör her denemede sürümü artırır → üç deneme, `QuarantineRetryException` yukarı çıkar, `Approved` olayı ve `Approved` geçmiş satırı yok (işlem geri alındı). (b) Kapanış ↔ gönderim, `Submitted` penceresi (W2): (b1) dekoratör `IQuarantineApprovalPort.StartAsync`'i (gönderimin iki işlemi arası) kapanış commit olana kadar bekletir; (b2) dekoratör ikinci işlemin taze kayıt okumasından sonra kapanış commit olana kadar bekletir → ikisinde: hold `Voided` kalır (`DecisionPending` değil), karar `Voided`, `WorkflowStarted` olayı var, cevap `202` gövdesi `state Voided`; ardından test portu `Approved` → `200 Voided`, `Approved` olayı yok, defter hareketsiz. (c) Kapanış ↔ ret (W4): dekoratör `CloseWithoutMovementAsync`'i işlem içi olay okumasından sonra kapanış commit olana kadar bekletir → karar `Voided`, `Rejected` olayı yok, hold `Voided`; ters sırada (ret önce) → karar `Rejected`, hold `Open` olmuş ve sonra `Voided` (R2), `Voided` olayı yok. (d) Kapanış ↔ gönderimin kilidi (W1, r4, F-3@54d736e1): Draft kararlı intake hold; dekoratör gönderimin kayıt okumasından (`DecisionHandlers.cs:371`) sonra, kilit işleminden önce kapanış commit olana kadar bekletir → cevap 409 `QUARANTINE_DECISION_ALREADY_PENDING`; karar olaylarında `Submitted` yok, `IQuarantineApprovalPort.StartAsync` çağrılmadı (dekoratör sayar: 0), hold `Voided` ve `ActiveDecisionId` boş, `DecisionSubmitted` geçmiş satırı yok; aynı kararın ikinci gönderimi → 409 `QUARANTINE_HOLD_CLOSED`. Sabotaj (ayrı ayrı): onay kolunun kayıt `Version` artışını kaldır → (a) `Voided` + `Approved` birlikte, kırmızı; onay kolunun yeniden deneme döngüsünü kaldır → (a2) `QuarantineRetryException` sızar, kırmızı; `LockAsync`'i beklenen sürüm olmadan yaz → (d) eski kopya `Voided`'ı ezer, hold `Open` + `ActiveDecisionId` dolu, kırmızı; W2'nin `Voided` denetimini kaldır → (b1) hold `DecisionPending`, kırmızı; W2'nin yeniden deneme döngüsünü kaldır → (b2) gönderim hata döner, kırmızı; `StateOf` emiciliğini kaldır → (b) karar `PendingApproval`, onay satır postlar, kırmızı; W4'ün kayıt `Version` artışını kaldır → (c) `Rejected` olayı `Voided`'dan sonra yazılır, kırmızı.
- [ ] AC-14 (zorunlu) — Geç intake: intake gözlemcisi kapalıyken `CUSTOMER_RETURN` ve REVERSAL postlanır, sonra intake `SweepAsync` → hold açılmaz, geçmiş yok. Sabotaj: `IsReversedAsync` denetimini kaldır → `Open` hold açılır, kırmızı.
- [ ] AC-15 (zorunlu) — Eski veri ve sayfalama (F-3, F-11@ae3fed7d): (a) intake hold ve REVERSAL'ı `CreatedAt` 30 gün geriye alınmış fixture (gözlemci kapalıydı) → ilk `SweepAsync` turunda `Voided`; (b) kurgu (ii): `Decided` intake hold ve 30 gün önceki doğrudan yazılmış REVERSAL'ı → terminal besleme `Conflict (Decided)`; (c) `SourceReversalPageSize = 2` test ayarıyla 5 açık intake hold, yalnız `Id`'si sıralamada sonuncu olanın (kimlikler fixture'da seçilir) kaynağı geri alınmış → 1. ve 2. tur o kaydı değiştirmez, 3. tur `Voided`, 4. tur imleç başa döner ve 0 değişiklik; varsayılan `SourceReversalPageSize` 500; (d) defter sorgusunun planı (`explain`) `ux_tenant_le_reversestransactionid` indeksini kullanır (`IXSCAN`, çapraz kiracı defter taraması yok). Sabotaj (ayrı ayrı): beslemeyi "son N saatteki REVERSAL'lar" penceresine çevir → (a) `Open`, kırmızı; terminal beslemeyi kaldır → (b) işaretsiz, kırmızı; imleç ilerletmesini kaldır (her tur ilk sayfa) → (c) 3. turdan sonra `Open`, kırmızı; defter sorgusundan `TenantId` / `LegalEntityId`'yi kaldır → (d) plan `COLLSCAN`, kırmızı.
- [ ] AC-16 (zorunlu) — Kiracı ve şirket izolasyonu: (a) T2'de, `SourceMovementId`'si T1'in kaynak hareket kimliğine eşitlenmiş hold (doğrudan yazılmış) T1'in REVERSAL'ından sonra değişmez; (b) aynı kiracıda (F-10@ae3fed7d): LE1'de kayıt açmayan hareket t (`GOODS_RECEIPT_PO` → `AVAILABLE`) ve LE2'de `SourceMovementId = t` ile doğrudan yazılmış hold — tekil indeks kiracı kapsamlıdır, LE1'de t'li kayıt olmadığı için yazılabilir (§4.4) — t'nin LE1'deki REVERSAL'ı → LE2 hold değişmez; (c) süpürücü her kaydı kendi `TenantId` + `LegalEntityId` kapsamında işler (merkezi kaydın `TenantId` / `CompanyId`'si kaydınki); (d) T1 kullanıcısı T2'nin `Voided` hold'unu `GET` eder → 404. Sabotaj (ayrı ayrı): kaynak hareket aramasından kiracı süzgecini kaldır → (a) kırmızı; şirket süzgecini kaldır → (b) kırmızı.
- [ ] AC-17 (zorunlu) — İlgisiz REVERSAL (kaynak olmayan `GOODS_RECEIPT_PO` → `AVAILABLE`'ın geri alınması) → hiçbir kayıt, geçmiş satırı, merkezi kayıt; bütün kayıt `Version`'ları aynı. Sabotaj: kaydı kovayla (ürün + lot + lokasyon + durum) eşle → aynı kovadaki hold kapanır, kırmızı.
- [ ] AC-18 (zorunlu) — Açık yüklemi: `Voided` hold KPI kartlarında, `late=true` süzgecinde, `lots/{lotId}/quality-status` miktarlarında, yeniden test kuyruğunun açık hold'larında, CT-Q6 rezervasyon çakışma listesinde, MOD-0177 süreç içi recall istekçisinin açık hold listesinde (`QuarantineRecallRequester.cs:59`) ve `EnteredBefore` süzgecinde sayılmaz (her biri ayrı ölçüm); kaynak taraması `HoldStatus.Decided` karşılaştırmasını `QuarantineRecordStates` dışında bulamaz. Sabotaj: `LotQualityStatusHandler.cs:112`'yi `!= HoldStatus.Decided`'a geri çevir → kalite durumu ölçümü ve kaynak taraması kırmızı.
- [ ] AC-19 (zorunlu) — Kapanış stok yazmaz: AC-1, AC-3, AC-9 senaryolarında defter işlem sayısı yalnız REVERSAL kadar artar; `sourceModule = MOD-0175` yeni işlem yok; "0175 defter koleksiyonuna yazmaz" mimari testi yeşil. Sabotaj: handler'a `STATUS_TRANSFER` postlaması ekle → kırmızı.
- [ ] AC-20 (zorunlu) — Merkezi kayıt: her `Voided` / `Conflict` için `central_audit_outbox`'ta tam 1 satır — `RequestType` komut adı, `Category MasterData`, `Operation LifecycleTransition`, `EntityType QuarantineHold` / `BlockedStock`, `EntityId` kayıt kimliği, `SourceModule QUARANTINE-BLOCKED-STOCK`, `ActorType System`, `ActorId` `00000000-0000-0000-0000-000000000175`, `ActorDisplayName` boş (Q-03 = A), `CorrelationId` = REVERSAL'ın `CorrelationId`'si, `WrittenInCommandTransaction = true`; `NoChange` → 0 satır. Yollar ayrı ayrı ölçülür (r4: dört yol): (a) gözlemci yolu — REVERSAL'ı adı ve `actor_type` claim'i dolu bir kiracı kullanıcısı postlar; aynı istekteki REVERSAL'ın kendi `PostMovementCommand` satırı `ActorType TenantUser`, `ActorId` = postlayan kalır; (b) süpürücü yolu (gözlemci kapalı, `SweepAsync`) — `ActorType System`, `CorrelationId` = REVERSAL'ınki (olay kimliği değil); (c) kip sızmaz — aynı DI kapsamında kapanıştan sonra gönderilen başka işaretli komutun (test: ikinci bir hold'da `UpdateHoldCommand`) satırı `ActorType TenantUser`, `ActorId` = çağıran; (d) uygulama motoru yolu (r4, F-1@54d736e1; W5 / W6 `Voided` dalı, AC-39 kurgusu) — iç komutun satırı `ActorType System`, `RequestType` `ApplySourceReversalToHoldCommand`, `CorrelationId` = REVERSAL'ınki; aynı akıştaki dış komutun satırı kendi kimliğiyle ayrı kalır ya da hiç oluşmaz (dış komut bu dalda yazmaz). W5 için istisna yoktur: işareti hiçbir yolda W5 yazmaz. `CentralAuditDeclarationTests` sabiti geliştirme anında dalda ölçülen N → N + 2 (bugün 71 → 73; MOD-0178 DL-2 önce birleşirse 82 → 84; F-7@ae3fed7d, §5). Sabotaj (ayrı ayrı): komuttan `ICentralAuditDeclaration`'ı kaldır → 0 satır ve bildirim testi kırmızı; sistem kipini kaldır → (a) `ActorType TenantUser` / `ActorId` postlayan, kırmızı; kipi gönderimden sonra kapatma → (c) `System`, kırmızı; süpürücü yolunda REVERSAL'ın `CorrelationId`'sini taşıma → (b) `CorrelationId` = olay kimliği, kırmızı; kipi yalnız gözlemci / süpürücü gönderiminde aç (gönderene bağla) → (d) iç komut satırı `ActorType TenantUser`, kırmızı.
- [ ] AC-21 (zorunlu) — K2 fail-closed: test dekoratörü enlist edilen merkezi kaydın yazımını hata attırır → kapanış işlemi geri alınır: hold `Open`, geçmiş satırı yok, karar olayı yok; dekoratör kaldırılınca sonraki `SweepAsync` kapatır. Sabotaj: merkezi kaydı işlem dışında, commit'ten sonra yaz → hold `Voided` ama kayıt yok, kırmızı.
- [ ] AC-22 (zorunlu) — Ürün izi: `SourceReversed` / `SourceReversalConflict` geçmiş satırı `ByUserId` = REVERSAL'ın `CreatedBy`'ı, `ReversalTransactionId`, `Reason` = REVERSAL numarası, `CorrelationId` = REVERSAL'ınki taşır; detay ucunun `history` dizisinde görünür; geçmiş deposu yalnız ekleme (mevcut mimari testi). Sabotaj: `ReversalTransactionId`'yi yazma → kırmızı.
- [ ] AC-23 (zorunlu) — QUARANTINE 1.4.0: `ContractSchemaValidator` yeşil; `Voided` hold / blocked / karar cevapları ve dört `cause` değerli (`Decided`, `LinesPosted`, `QuantityChanged`, `OwnMovementReversed`) `sourceReversalConflict` şemaya uyar; 1.3.0 ile karşılaştırma testi hiçbir yol, parametre, özellik, `required` listesi, durum kodu ya da mevcut enum değerinin değişmediğini ölçer; `x-changelog` 1.4.0 satırı var. Sabotaj (ayrı ayrı): `voided`'ı `required`'a ekle → kırmızı; mevcut bir `HistoryAction` değerini yeniden adlandır → kırmızı.
- [ ] AC-24 (zorunlu) — Toplu kapı: `services/Diten.InventoryService` testleri süzgeçsiz tam koşu yeşil (taban kırmızısı aynı gün `main`'de karşılaştırılır); `tests/architecture/TenantArchitecture.ArchitectureTests` `AuditTrailStandardTests` defter sayıları değişmez (yeni borç / istisna satırı yok). DL-3 kabulündeki tam koşu DL-1 ölçütlerini kurgu kuralıyla değiştirmeden içerir (AC-33). Sabotaj: kapı ölçüsüdür — test sınıfını değil kabul kaydının ölçümünü kırmızıya çevirir (MOD-0173-FU03 AC-32 emsali).
- [ ] AC-37 (zorunlu) — MOD-0175 kökenli kayıt kapanmaz (F-1@ae3fed7d, §8.3 R1; DL-1, AC-24'ten sonra eklendi — numaralar r2 ile sabit). Kurgu (ii), tablo güdümlü beş satır: Manual hold (`quarantineStock` Manual), Retest hold (`quarantineStock` Retest), Recall hold (`quarantineStock`, origin MOD-0177), Rework hold (Rework kararı uygulanmış blocked'tan), `ManualBlock` blocked (`blockStock`); her birinin kaynak hareketinin REVERSAL'ı doğrudan yazılır, kapanış iki yoldan ayrı ayrı koşar — gözlemci (`OnPostedAsync` test tarafından o işlemle çağrılır) ve `SweepAsync` (açık besleme; Rework'ün kaynak blocked'ı `Closed` iken bile hold açık) → kayıt `status`, `activeDecision`, `quantity` değişmez, `voided` yok, `sourceReversalConflict.cause OwnMovementReversed`, 1 `SourceReversalConflict` geçmiş satırı, 1 merkezi kayıt (`Operation LifecycleTransition`, `ActorType System`); aktif kararı olan Manual hold'da karara olay yazılmaz. Çapraz denetim satırı: `Source = CustomerReturn` ile doğrudan yazılmış ama aslının `SourceModule = MOD-0175` olduğu hold → `Conflict (OwnMovementReversed)` + uyarı logu (fail-closed). Sabotaj (ayrı ayrı): köken denetimini kaldır (her kayda intake kuralı) → Manual hold `Voided`, kırmızı; aslın `SourceModule` çapraz denetimini kaldır → çapraz denetim satırı `Voided`, kırmızı; `ManualBlock`'u intake köken listesine ekle → blocked `Voided`, kırmızı.
- [ ] AC-39 (zorunlu) — Kapanıştan sonra postlanan satırın işareti komut kipinde (F-1@54d736e1, §8.3 R0a, §8.6 W5 / W6 `Voided` dalı; DL-1, r4'te sona eklendi). Kurgu (i), çok kayıtlı kova: intake hold A (4, kaynak t1, Release 4 kararı `PendingApproval`) ve intake hold B (4) aynı kovada; gözlemcisiz fabrikada t1'in REVERSAL'ı A `PendingApproval` iken 201 (R4 `Allow`; kovada B'nin 4'ü kalır); REVERSAL'ı postlayan U1, onaylayan U2 (farklı kullanıcılar). Onay sonucu gelir → `Approved` yazılır → test engeli `ApplyAsync`'i satır öncesi yeniden okumadan **sonra**, `PostAsync`'ten **önce** durdurur → `SweepAsync` A'yı R5 ile kapatır (A `Voided`, karar `Voided`) → engel kalkar → `quarantine:{A kararı}:1` postlanır. Onayın giriş yolu tablo güdümlü, her biri ayrı koşu: (a1) `RefreshDecisionCommand` (`POST …/decisions/{id}/refresh`, test Workflow istemcisi `approved` döner); (a2) `QuarantineWorkflowOutcomeConsumer.ConsumeAsync` doğrudan (MediatR dışı); (a3) `GET` hold detayı (okuma anı uzlaştırması); (a4) W6: A'nın kararı iki satırlı (Release 2 + Block 2), B 2 EA → satır 1 postlanır, satır 2 `INSUFFICIENT_STOCK` ile reddedilir. Her koşuda: A `status Voided`, `voided.*` değişmedi; karar olaylarında `Applied` ve `ApplyFailed` yok; A `Decided` olmadı; `sourceReversalConflict.cause LinesPosted`, `.reversalTransactionId` = A'nın `voided.reversalTransactionId`'si, `.reversalTransactionNumber` = REVERSAL numarası, `.reversedBy` = U1 (U2 değil); geçmişte 1 `SourceReversed` + 1 `SourceReversalConflict` (`ByUserId` U1, `CorrelationId` REVERSAL'ınki); `central_audit_outbox`'ta A için 2 satır (`Void` + `Conflict`), ikisi `ActorType System`, `RequestType ApplySourceReversalToHoldCommand`; (a1)'de ayrıca `RefreshDecisionCommand`'ın kendi satırı (onay yazımı, aktör U2) değişmeden 1, (a2) / (a3)'te başka satır yok; B'nin kaydı ve `Version`'ı değişmez (pencerenin bilinen sonucu — işaret A'dadır); gönderilen iç komutların sayısı (dekoratör) 1. (b) İç gönderim başarısız: dekoratör iç komutun merkezi kayıt enlist'ini hata attırır → A `Voided` ve işaretsiz, `quarantine.source_reversal.mark_dispatch_failed` logunda A'nın numarası ve karar kimliği, giriş yolunun cevabı (a1) ile aynı; dekoratör kalkar, `SweepAsync` (geçersiz besleme) → `Conflict (LinesPosted)`, 1 merkezi kayıt `System`; `SweepAsync` bir kez daha → 0 değişiklik. (c) Geçersiz besleme sınırı: satırsız `Voided` kayıt (AC-3 (a) kurgusu) beslemede okunur, yazma yok; `Voided.AtUtc`'si `SourceReversalVoidedWatchHours` + 1 saat önceye alınmış, satırı postlanmış, doğrudan yazılmış `Voided` kayıt okunmaz (kabul edilen sınır, §8.2). (d) Tekrar: iç gönderim sonrası `SweepAsync` → `NoChange (AlreadyRecorded)`, işaret / geçmiş / merkezi kayıt 1'de kalır. Sabotaj (ayrı ayrı): işareti W5'in kendi işlemine yazdır (r3 davranışı) → (a2), (a3) merkezi kayıt 0 ve (a1) satırı `RequestType RefreshDecisionCommand` / aktör U2, kırmızı; iç gönderimi kaldır → (a1) işaretsiz, kırmızı; W6 dalından iç gönderimi kaldır → (a4) işaretsiz, kırmızı; geçersiz beslemeyi kaldır → (b) işaretsiz, kırmızı; R0a'yı kaldır (`Voided` → R0) → (a1) işaretsiz, kırmızı; W5'in işlem içi `Voided` denetimini kaldır → `Applied` yazılır ve A `Decided`, kırmızı.

**DL-2 — ekran**

- [ ] AC-25 (zorunlu) — Kuyruk (Q-08 = A; durum süzgeci verilmemiş varsayılan liste): `Voided` satır görünür, "Geçersiz · Kaynak geri alındı" rozetiyle `Decided` satırlarla birlikte sona sıralanır; "Karar Ver" satır eylemi DOM'da yok; geç rengi yok. Web testi + tarayıcı smoke (en, tr, ar). Sabotaj: `queue.js:68`'i `status !== 'Decided'` bırak → Karar Ver çizilir, kırmızı.
- [ ] AC-26 (zorunlu) — Detay: afiş REVERSAL numarası, tarihi ve kişiyle; Karar Ver / İncelemeye al / Kararı geri çek / Yeniden uygula düğmeleri yok; geçmişte iki yeni etiket; hareketler listesinde REVERSAL satırı. Sabotaj: `details.js:125`'i değiştirme → düğmeler çizilir, kırmızı.
- [ ] AC-27 (zorunlu) — Bloke Stok: durum süzgecinde `Voided` seçeneği; rozet; eylem yok. Sabotaj: `blocked.js:260` dalına `Voided`'ı ekleme → Disposition / Blokeyi Kaldır çizilir, kırmızı.
- [ ] AC-28 (zorunlu) — Uyuşmazlık: `sourceReversalConflict` dolu kayıt durumdan bağımsız rozetle görünür — `Decided` hold, `Closed` blocked, `Open` blocked (`QuantityChanged`), `Open` MOD-0175 kökenli hold (`OwnMovementReversed`) ve — r4, §8.3 R0a — `Voided` intake hold (`LinesPosted`; "Geçersiz" afişi ile uyuşmazlık rozeti birlikte); her birinde "Kaynak geri alındı · uyuşmazlık" rozeti + neden metni (dört neden). Sabotaj (ayrı ayrı): rozet koşulunu `Decided` / `Closed` ile sınırla → `Open` satırlar rozetsiz, kırmızı; `Voided` kayıtta rozeti çizme (yalnız afiş) → `Voided` satır rozetsiz, kırmızı.
- [ ] AC-29 (zorunlu) — 7 dil: §11'in 12 anahtarı `Quarantine.{en,tr,fr,es,zh,ar,ru}.resx` hepsinde (anahtar sayıları eşit) ve `Views/Quarantine/Quarantine.cs` `Keys` listesinde (F-8@ae3fed7d); `ar` RTL; sayfa yardımı QUARANTINEQUEUE / QUARANTINEDETAILS / BLOCKEDSTOCK adımı 7 dilde güncel ve QUARANTINEDETAILS / BLOCKEDSTOCK adımı açık kayıttaki uyuşmazlığın kabul edilen sınırını söyler (kayıt açık kalır, açık sayımlarda görünür, kayıt üzerinde kapatma eylemi yoktur — §8.3, §23; r4, F-5@54d736e1; Web testi: ilgili adım anahtarı `PageHelpResource.*.resx` 7 dilde dolu, en / tr metni "kapatma eylemi yok" anlamını taşır — kabul kaydında metin alıntısıyla); JS'de sabit kullanıcı metni yok. Sabotaj (ayrı ayrı): `zh`'den bir anahtarı sil → eşitlik testi kırmızı; yeni bir anahtarı `Quarantine.cs` `Keys`'ten çıkar → `QuarantineWebTests` kırmızı; `ru`'dan açık kayıt sınırı adımının metnini boşalt → sayfa yardımı dolu-metin testi kırmızı.
- [ ] AC-30 (zorunlu) — `Views/Quarantine/{Index,Details,Blocked}.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` AÇIKÇA yazılı ve değişmemiş; `quarantine.holds.read`'siz kullanıcı UAS-001 yüzeyini görür (iskelet yok, yönlendirme yok); `verify_datatable_page.py` koşuldu ve sapmalar sahibe soruldu (kabul kaydında). Sabotaj: `Index.cshtml`'de izin denetimini atla → UAS-001 Web testi kırmızı.

**DL-3 — defter bekçisi (Q-06 = A; kabul kapısı Q-10, §22)**

- [ ] AC-31 (zorunlu) — Uyuşmazlığa yol açacak REVERSAL reddedilir (doğal sıra: kayıt önce ilerler, REVERSAL sonra istenir): AC-6 kovasında A `Decided` iken (R7), AC-7 kovasında satır 1 postlanmış iken (R6), AC-10 kovasında X 1 EA imha sonrası (R9), `Approved` / `ApplyFailed` kararlı intake kaydın (R5) ve — r4, §8.3 R13 — doğrudan yazılmış `DecisionPending` + `Draft` aktif kararlı intake hold'un kaynağının REVERSAL'ı → 422 `REVERSAL_QUALITY_RECORD_LOCKED`, `details.recordNumber`, `details.cause` (sırasıyla `Decided`, `LinesPosted`, `QuantityChanged`, `DecisionInProgress`, `Unmatched`); defter işlem sayısı, bakiye, pozisyon değişmez; `inventory_audit`'te `Outcome Failed` ret satırı. Sabotaj (ayrı ayrı): bekçi her zaman `Allow` dönsün → 201, kırmızı; bekçide tablo dışı durumu `Allow`'a çevir → R13 satırı 201, kırmızı.
- [ ] AC-32 (zorunlu) — MOD-0175 hareketlerinin REVERSAL'ı reddedilir (kural 1, §8.3 R1'in canlı yolu): "Karantinaya Al" `STATUS_TRANSFER` (Manual, Retest, Recall), "Stoğu bloke et" (`ManualBlock`), karar satırı (Release), Rework, Numune `LOCATION_TRANSFER` için REVERSAL → 422, `details.cause OwnMovementReversed`, kayıt kaynağı olan hareketlerde `details.recordNumber` dolu; stok kalite kararı dışında `AVAILABLE`'a dönmez. Sabotaj: `SourceModule = MOD-0175` kuralını kaldır → manuel karantinanın REVERSAL'ı 201, stok `AVAILABLE`, kırmızı.
- [ ] AC-33 (zorunlu) — Bekçi kapanabilir kayıtları engellemez: `Open`, `Submitted` penceresindeki ve `PendingApproval` intake kayıtların kaynağının REVERSAL'ı 201 → DL-1 kapanışı (AC-1, AC-3, AC-13 (b) yeniden koşulur); MOD-0173 kaynaklı ilgisiz hareketin REVERSAL'ı 201; DL-3 etkin iken AC-4, AC-6, AC-7, AC-8, AC-10, AC-11, AC-12, AC-15, AC-37, AC-39 değiştirilmeden yeşil (kurgu kuralı, F-4@ae3fed7d; AC-39 r4). Sabotaj: `SourceMovementId`'si olan her kaynağı reddet → AC-1 ve AC-4 kurgusu kırmızı.
- [ ] AC-34 (zorunlu) — Fail-closed: bekçinin karantina okuması hata atar → 503 `INVENTORY_UNAVAILABLE`, hiçbir şey yazılmaz. Sabotaj: hatayı yakala ve `Allow` dön → 201, kırmızı.
- [ ] AC-35 (zorunlu) — Kiracı kapsamı: bekçi yalnız postlayanın kiracı + şirketindeki kaydı okur; T2'deki kurgulanmış `Decided` kayıt T1'in REVERSAL'ını reddettirmez. Sabotaj: bekçi okumasından kiracı süzgecini kaldır → T1 422 alır, kırmızı.
- [ ] AC-36 (zorunlu) — INVENTORY 1.8.0 (Q-07): `info.version` 1.8.0; `REVERSAL_QUALITY_RECORD_LOCKED` `InventoryErrorCodes.All`'da ve sözleşmede (`postMovement` 422 açıklaması + kod listesi; `details.cause` kapalı listesi `OwnMovementReversed`, `Decided`, `LinesPosted`, `QuantityChanged`, `DecisionInProgress`, `Unmatched` — §8.8; `Unmatched` r4), `x-changelog` 1.8.0 satırı; `ContractSchemaValidatorTests`, `InventoryBundleDescriptionNoteTests` yeşil; 1.7.0 donmuş kopyasına (`Contract/Fixtures/inventory-bundle-v1.7.0.openapi.yaml`) göre karşılaştırmada yalnız ekleme. Sabotaj (ayrı ayrı): kodu katalogdan çıkar → katalog testi kırmızı; mevcut bir hata kodunu yeniden adlandır → karşılaştırma testi kırmızı.
- [ ] AC-38 (zorunlu) — Web hata metni (F-5@ae3fed7d; DL-3, AC-36'dan sonra eklendi): `InventoryErrorMessages.KeyFor("REVERSAL_QUALITY_RECORD_LOCKED", 422)` → `ErrReversalQualityRecordLocked` (genel cümle değil); anahtar `StockLedgerIndex` ve `StockMovementsIndex` ailelerinin 7 dilinde (14 dosya) gerçek metinle; `InventoryResxParityTests` kod listesi kodu içerir; S-2 tarayıcı smoke (tr, en, ar): karar verilmiş kaydın kaynağının "Reversal" onayı → pencere açık kalır, ret metni okurun dilinde, liste yeniden yüklenmez. Sabotaj (ayrı ayrı): `ByCode`'dan kodu çıkar → kod listesi testi (genel cümleye düşer) kırmızı; `zh`'den anahtarı sil → resx eşitlik testi kırmızı.

## 17. Test Expectations

- **Birim:** `SourceReversalRules` (§8.3 tablosunun her satırı R0a–R13, tablo güdümlü; R13 için tablo dışı durum satırları ve tanımsız enum değerleri — AC-2 (b); `OriginOf` köken tablosu §4.1 ve çapraz denetim), `QuarantineRecordStates`, `StateOf` `Voided` eşlemesi ve emiciliği (`Voided`'dan sonra her olay türü → `Voided`).
- **Entegrasyon (gerçek Mongo replika seti, `InventoryApiFactory`; testler kendi replika setini açar):** AC-1…AC-23, AC-37, AC-39, AC-31…AC-35. Süpürücüler test
  ayarlarında kapalı, `SweepAsync` elle. Kurgu kuralı (§16): gözlemcisiz fabrika varyantı ve doğrudan yazılmış eski REVERSAL; bekçi hiçbir testte `Allow`'a alınmaz.
- **Eşzamanlılık / yarış:** AC-12 (`Task.WhenAll` + iki okuma engeli; ölçüt `NoChange (LostRace)`, `TransactionRetryCounter` yalnız bilgi), AC-13 (a), (a2), (b)–(d), AC-8 ve AC-39 (test dekoratörü / engel; üretim koduna test
  kancası eklenmez — dekoratör `InventoryApiFactory` DI'ında).
- **Kiracı izolasyonu:** AC-16, AC-35. **Denetim:** AC-20 (sistem aktörü, Q-03 = A: gözlemci, süpürücü, sızma ve uygulama motoru yolu ayrı), AC-21, AC-22, AC-39; `CentralAuditDeclarationTests` sabiti geliştirme anında dalda ölçülen N → N + 2 (bugün 71 → 73; MOD-0178 DL-2 önce birleşirse 82 → 84; F-7@ae3fed7d, §5).
- **Sözleşme:** AC-23, AC-36; Web kod haritası AC-38; Prism mock smoke QUARANTINE 1.4.0 (DL-1) ve INVENTORY 1.8.0 (DL-3).
- **Web:** `frontend/Diten.Web.Tests/Quarantine/QuarantineWebTests.cs` genişler (AC-25…AC-30; `Quarantine.cs` `Keys`); DL-3'te `frontend/Diten.Web.Tests/Inventory/InventoryResxParityTests.cs` (AC-38); tarayıcı smoke en / tr / ar.
- **Smoke (E3, DL-1 + DL-2):** Gateway 5000 üzerinden gerçek login: manuel `CUSTOMER_RETURN` → kuyrukta hold → S-2 "Reversal" → kuyrukta "Geçersiz".
- **Taban kanıtı:** MVP-6 turu F-2 ve MVP-1 turu 2026-11-02 senaryosu bu FU kabulünde yeniden koşulur (aynı adımlar, hold `Voided`).
- Tam paket kuralı: servis dokunulduğunda süzgeçsiz tam koşu (AC-24).

## 18. Ready-for-dev Checklist

- [x] Golden Reference Compact backend düzeni ve Web Compact dosya seti okundu; yeni dosyalar Golden düzeninde, ebeveynin sapması kayıtlı (§10)
- [x] Frontmatter zorunlu alanlar (`service`, `shell: tenant`, `golden_reference: compact`, `data_mode: server`, `screens`, `entity_base`, `form_field_count`)
- [x] Layout & Shell Contract: `_LayoutTenantShell` açıkça ve AC-30'da test edilebilir (§9)
- [x] Backend File Convention: `Handlers/CommandHandlers/`, suffix'siz handler / validator adları (§10)
- [x] Frontend File Contract: ebeveyn ekranlarına ekleme; `_CreateEditOffcanvas` yok (§11)
- [x] Validation Rules, Failure Path (24 senaryo: tekrar, eksik, yetkisiz, eşzamanlılık, kiracı dışı, K2, köken, gönderim penceresi, ret yarışı, sayfalama; r4: kapanış sonrası postlanan satır, kilit yarışı, onay sürüm uyuşmazlığı, tablo dışı durum), Authorization (yeni anahtar yok), Gateway (değişmez)
- [x] Platform lookup kararı: uygulanmaz (kiracı modülü; lookup yok)
- [x] Acceptance criteria ölçülebilir ve sabotajlı — 39 AC'nin her birinde adlı sabotaj (AC-24 kapı ölçüsü); DL-1 ölçütleri DL-3 etkin iken de geçerli kurguyla (§16 kurgu kuralı, AC-33)
- [x] Audited Events: modülün mevcut 14 yazma komutu + 2 yeni komut (§21)
- [x] Kimlik ön kontrolü çıkış 0 (`--check-id … --parent MOD-0175`)
- [x] Sahip kararları Q-01…Q-06, Q-08, Q-09 (Beste 2026-11-03, "hepsi öneri gibi") ve CT kararı Q-07 (sürüm sırası) verildi — §19.5
- [x] XMC-001 izinleri yazıldı (Beste 2026-11-03, §5): `InventoryCentralAuditScope.cs` sistem aktörü kipi (MOD-0173 / BL-501 bağdaştırıcısı); MOD-0173 `InternalMovementPostingService.cs` + `IReversalPrecondition.cs` + `InventoryErrorCodes.cs` + INVENTORY 1.8.0
- Not: DL-3 Web dosyaları (`InventoryErrorMessages.cs`, `StockLedgerIndex` / `StockMovementsIndex` resx, `InventoryResxParityTests.cs`; F-5@ae3fed7d) için XMC-001 izninin genişletilmesi — **sahip izni bekleniyor** (Beste; r4'te de açık) — **ready-for-dev önkoşulu değildir**; DL-3 Web adımının kapısıdır (§5, §22)
- [ ] Registry satırı `MOD-0175-FU01` — **CT ekler** (Q-11, CT kararı 2026-11-03; onaydan önce); bu paket kayıt dosyasına yazmaz
- Not: Q-10 (MVP-6 CT, Natig — DL-3'ün MOD-0186 etkisi) **ready-for-dev önkoşulu değildir**; DL-3 kabul kapısıdır (§8.8, §22)
- [ ] Plan satırı (yetenek kimliği, plan MVP) plan sahibinden (§22)
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) — **r4'ün kendi incelemesi** (r2 incelemesi 01 tur 1 UYGUN DEĞİL, bulgular §19.6'da; r3 incelemesi 01 tur 2 KOŞULLU, bulgular §19.7'de kapandı) + CT-APR-01 onay satırı (risk HIGH)
- [ ] `status: ready-for-dev` (şimdi `draft`, `approval_status: pending`; inceleme sonrası CT çeker)

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
**r2'de ek okunan (2026-11-03, `c189cb0cd`):** `Api/CentralAudit/InventoryCentralAuditScope.cs` (kapsam okuması, `:28`, `:54-59`),
`Diten.BuildingBlocks.CentralAudit/CentralAuditRecorder.cs:82-121` (kapsam `Begin` anında okunur), `Domain/Entities/InventoryTransaction.cs:82`
(`CorrelationId`), `QuarantineLedgerReads.cs:217` (sabit aktör), `tests/…/Contract/Fixtures/` (donmuş kopyalar), `execution/approvals/approvals.tsv`
(FU03 r4 onay satırı, `c03ac5511`), `MOD-0173-FU03-inventory-lot-location-reservation.md` §8.8, §19.4–§19.6 (sürüm adımları, karar tablosu biçimi),
`docs/guides/operations/pack-review-common-findings.md`.
**r4'te ek okunan (2026-11-03, `0be75c8c6`, salt okuma; `023755fa4` ile kod farkı yok):** `docs/records/reviews/MOD-0175-FU01/2026-11-03-54d736e1.md`,
`QuarantineApplyEngine.cs` (tamamı), `QuarantineWorkflowOutcomeConsumer.cs:51-104`, `QuarantineWorkflow.cs:188-286` (uzlaştırıcı),
`WorkflowHandlers.cs:1-68` (`RefreshDecisionCommand`), `DecisionHandlers.cs:355-523` (gönderim, kilit, yeniden uygulama), `UpdateHoldHandler.cs:60-130`,
`QuarantineDecisionRules.cs:100-140`, `QueryHandlers.cs` ve `LotQualityStatusHandler.cs` uzlaştırıcı çağrı satırları,
`Diten.BuildingBlocks.CentralAudit/CentralAuditBehavior.cs:1-85`, `CentralAuditRecorder.cs:40-200`, `ApplyApprovedLotStatusChange.cs:48`,
`tests/architecture/audit-ledger/Diten.InventoryService.md`, `quarantine.openapi.yaml:151-153`. **Okunmadı:** `StockMovementsController.cs` /
`StockLedgerController.cs` (G-26 atıfları r3'ten, yeniden ölçülmedi), Mongo plan ölçümü ve testler (koşu yok).

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
| G-16 | Inventory yazma komutu sayısı sabiti 71; yorum aynı dalda MOD-0178 DL-2 sonrası 82 öngörür → sabit geliştirme anında ölçülür (F-7) | `CentralAuditDeclarationTests.cs:36-37` |
| G-17 | S-2 "Reversal" düğmesi yalnız `sourceModule = MOD-0173` satırında (yalnız JS); sunucu her aslı kabul eder → MOD-0175 hareketinin REVERSAL'ı API'den mümkün (DEC-INV-08 atlaması) | `StockMovements/index.js:13-15`; `ReversalRules.cs:19-56` (kaynak modül denetimi yok) |
| G-18 | Inventory'ye ulaşan REVERSAL üreticileri: MOD-0186 geç yazan, S-2 ve "Düzelt". MOD-0142 `reverseGrn` bugün ulaşmıyor: zarfı `MOD-0142` / `GOODS_RECEIPT` (Inventory `MOD-0173` / `REVERSAL` ister → 400) ve Procurement'ta mock istemci bağlı (F-6) | `ReturnLateWriterPolicy.cs:13, 37-43`; `form.js:85`; `ReverseGrnHandler.cs:57-73`, `GrnModels.cs:25-26`, `ReversalRules.cs:22-26`, Procurement `Persistence/DependencyInjection.cs:61` |
| G-19 | Resx eşitliği: `Quarantine.*.resx` 7 dilde 451 anahtar | `grep -c "<data "` |
| G-20 | Liste doğrulayıcısı bu modülün dosya adlarını bulmuyor | `verify_datatable_page.py . --area Quarantine --module Quarantine --format gaps` → 2–6 numaralı "Missing" |
| G-21 | (r3, `049bbb89d`) `SourceMovementId` yazan yer beştir: intake hold / blocked (`QuarantineIntake.cs:108, 140`), Manual / Retest / Recall hold (`StockActionHandlers.cs:346-348`), `ManualBlock` (`:455-456`), Rework hold (`QuarantineApplyEngine.cs:287-288`); MOD-0175 hareketleri `SourceModule = MOD-0175` ile postlanır (`QuarantineMovementPoster.cs:95`, `QuarantineContract.cs:11`) | `grep SourceMovementId` (F-1) |
| G-22 | (r3) `SubmitDecisionHandler` ikinci işlemi hold'u işlem dışı depo okumasıyla tazeler ve koşulsuz `DecisionPending` yazar; sürüm uyuşmazlığında yeniden deneme yok; `WorkflowStarted` olayı koşulsuz | `DecisionHandlers.cs:416-437` (F-2) |
| G-23 | (r3) `CloseWithoutMovementAsync` olayları dış okumadan sonra ekler; `ReopenAsync` yalnız `ActiveDecisionId == decisionId` iken kayda yazar | `QuarantineApplyEngine.cs:501-533, 562-573` (F-12) |
| G-24 | (r3) `IQuarantineWriteSession` işlem içi okuma olarak yalnız `DecisionEventsAsync` taşır; `InTransactionAsync` `WithTransactionAsync` + snapshot okuma; `TransactionRetryCounter` `quarantine` yalnız işlem yeniden koşusunda artar | `QuarantineRepositories.cs:88-123`; `QuarantineRepository.cs:214-226` (F-13) |
| G-25 | (r3) Hold / blocked kaynak indeksi `(TenantId, SourceMovementId)` — şirket yok; defter REVERSAL indeksi `(TenantId, LegalEntityId, ReversesTransactionId)` | `QuarantineIndexConfiguration.cs:49-50, 81-82`; `InventoryIndexConfiguration.cs:20, 49-56` (F-10, F-11) |
| G-26 | (r3) Web Inventory kod haritası `REVERSAL_QUALITY_RECORD_LOCKED`'i bilmez; ileti MVC'de `KeyFor` ile seçilir; kod listesi testi `InventoryResxParityTests.cs:81-86`; `Quarantine.cs` `Keys` 7 resx'i sabitler | `InventoryErrorMessages.cs:10-40`; `StockMovementsController.cs:525, 648`; `Quarantine.cs:3-8` (F-5, F-8) |
| G-27 | (r4) `RecordAppliedAsync` dört bağlamdan gelen akışta koşar: `RefreshDecisionCommand` (uzlaştırıcı, `Execute`), `ReapplyDecisionCommand`, okuma anı uzlaştırması yapan sorgu handler'ları (işaretli komut yok), MediatR dışı Workflow tüketicisi (işaretli komut yok); tüketici gelen kutusu satırını `ApplyOutcomeAsync`'ten önce yazar | `WorkflowHandlers.cs:61`; `DecisionHandlers.cs:515`; `QueryHandlers.cs:142, 196, 232, 269, 309`; `LotQualityStatusHandler.cs:72`; `QuarantineWorkflow.cs:270`; `QuarantineWorkflowOutcomeConsumer.cs:89-99` (F-1) |
| G-28 | (r4) Merkezi kayıt iç içe komutu destekler: bekleyen olaylar yığında, `EnlistAsync` en üsttekini yazar, kapsam `Begin` anında okunur; `CentralAuditBehavior` en içtedir; `ApplyAsync` bugün de iç komut gönderir (`ApplyApprovedLotStatusChangeCommand`, işaretli) | `CentralAuditRecorder.cs:57, 82-121, 123-140, 160-168`; `CentralAuditBehavior.cs:15-16, 36-57`; `QuarantineApplyEngine.cs:93`; `ApplyApprovedLotStatusChange.cs:48` (F-1) |
| G-29 | (r4) Onay işlemi yalnız `QuarantineDuplicateException` yakalar; `CloseWithoutMovementAsync` `QuarantineRetryException`'ı `attempt < 3` iken yutar, üçüncüde yukarı atar | `QuarantineApplyEngine.cs:474-495, 504-531` (F-2) |
| G-30 | (r4) Gönderimin kilit işlemi kayıt işlem dışında `:371`'de okunduktan sonra sürümlü yazar; başarısızlık tek iletiyle 409 `QUARANTINE_DECISION_ALREADY_PENDING` döner; `UpdateHoldCommand` aktif kararlı kayıtta 409 verir (onay sırasında ikinci yazar olamaz) | `DecisionHandlers.cs:371, 396-413, 453-477`; `UpdateHoldHandler.cs:92-96` (F-2, F-3) |
| G-31 | (r4) V7 kova denetimi `QUARANTINE_STOCK_CHANGED` 409; kurtarma yolu olmayan açık kayıt | `QuarantineDecisionRules.cs:41, 131-134`; `DecisionHandlers.cs:48-49, 376` (F-5) |

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

### 19.4 Karar soruları (r1'de açık; r2'de Q-10 dışında kapandı)

| # | Soru | Seçenekler / karar | Sahip | Hedef |
|---|---|---|---|---|
| Q-01 | Terminal durum adı ve blocked için ayrı değer mi? | **KAPANDI — A:** hold ve blocked için yeni `Voided` (neden `SourceReversed`); `Closed` yeniden kullanılmaz. (B: blocked'ta `Closed` + `closeReason` — seçilmedi) | Beste (sahip kararı 2026-11-03) | §19.5 |
| Q-02 | Karar aşamasındaki kayıt: satırı postlanmamış her karar (PendingApproval / Approved / satırsız ApplyFailed) kapanışla `Voided` olsun mu; Workflow örneği? | **KAPANDI — A:** evet, kayıtla birlikte `Voided`; sonradan gelen onay etkisiz; Workflow örneği açık kalır; WorkCenter bağlantısı "Geçersiz" gösterir. (B, C — seçilmedi; FU-0175-FU01-WFCANCEL açılmaz) | Beste (sahip kararı 2026-11-03) | §19.5 |
| Q-03 | Merkezi kaydın aktörü | **KAPANDI — A:** `ActorType System`, `ActorId` sabit `00000000-0000-0000-0000-000000000175`; geri alan kullanıcı ürün geçmişinde ve `CorrelationId` ile defter denetiminde. `InventoryCentralAuditScope.cs` değişikliği XMC-001 izniyle (MOD-0173 / BL-501 bağdaştırıcısı; sahip aynı, Beste), kapsam §5. (B: `TenantUser` — seçilmedi) | Beste (sahip kararı + XMC-001 izni 2026-11-03) | §19.5 |
| Q-04 | Uygulanmış / satırı postlanmış karardan sonra geri alma | **KAPANDI — A:** DL-1 uyuşmazlık işareti (`SourceReversalConflict`) + geçmiş, otomatik düzeltme yok; önlem DL-3. (B — seçilmedi) | Beste (sahip kararı 2026-11-03) | §19.5 |
| Q-05 | Operatör için elle kapatma yolu | **KAPANDI — A:** yok; geri alma kaynaklı her kayıt otomatik kapanır, eski kayıtlar ilk süpürücü turunda; REVERSAL dışı hayalet kayıtlar FU-0175-FU01-OUTBOUND'a. (B: `quarantine.holds.void` — seçilmedi) | Beste (sahip kararı 2026-11-03) | §19.5 |
| Q-06 | DL-3 defter bekçisi bu FU'da mı? | **KAPANDI — A:** evet, DL-3 bu pakette; XMC-001 MOD-0173 izni ve INVENTORY ek hata kodu `REVERSAL_QUALITY_RECORD_LOCKED`. (B: ayrı takip — seçilmedi) | Beste (sahip kararı + XMC-001 izni 2026-11-03; MOD-0173 sahibi aynı) | §19.5 |
| Q-07 | Sürümler | **KAPANDI (CT):** QUARANTINE **1.4.0** tek adım (DL-1); INVENTORY ek kodu FU03'ün 1.7.0'ından sonraki ek sürüm **1.8.0** (DL-3); FU03 onaylı (`execution/approvals/approvals.tsv`, `c03ac5511`) | MVP-1 CT (2026-11-03) | §19.5 |
| Q-08 | Kuyruk varsayılanı: `Voided` satırlar görünür mü? | **KAPANDI — A:** görünür, sona sıralı, KPI'ya girmez; varsayılan liste değişmez. (B — seçilmedi) | Beste (sahip kararı 2026-11-03) | §19.5 |
| Q-09 | Geç intake | **KAPANDI — A:** kaynağı geri alınmış hareket için kayıt açılmaz (§8.4). (B — seçilmedi) | Beste (sahip kararı 2026-11-03) | §19.5 |
| Q-10 | MVP-6 etkisi | **AÇIK.** (1) Bilgi: MOD-0186 tur satırı R1-a'nın beklenen sonucu "hold `Open`" → "hold `Voided`" olur (DL-1; MVP-6'nın tur kaydı, bu paketin kapısı değil). (2) Karar: DL-3'te MOD-0186 geç yazanının, kaydı karar verilmiş iadenin REVERSAL'ı 422 `REVERSAL_QUALITY_RECORD_LOCKED` alır → mevcut `ReversalFailed:<kod>` yolu (`ReturnLateWriterPolicy.cs:43`) — kabul mü? MOD-0183'ün REVERSAL'ları (`GOODS_ISSUE_SALES` telafisi) etkilenmez. **DL-3 kabul kapısı; ready-for-dev önkoşulu değil** (§8.8, §22) | Natig (MVP-6 CT) — soruldu | **TBD** — DL-3 kabulünden önce |
| Q-11 | Registry satırı `MOD-0175-FU01` | **KAPANDI (CT):** CT ekler; bu paket kayıt dosyasına yazmaz (MOD-0173-FU03 CT-01 emsali) | MVP-1 CT (2026-11-03) | satır onaydan önce |

Ayrıca bilgi (F-6@ae3fed7d): MOD-0142 `reverseGrn` (Procurement) bugün Inventory'ye ulaşmıyor (G-18) → **DL-3 etkisi yok**; gerçek istemci bağlandığında REVERSAL zarfı da `MOD-0173` / `REVERSAL`'a uymalıdır. Sahibine bilgi notu CT'den (karar değil).

### 19.5 Verilen kararlar (2026-11-03)

Sahip kararları Beste'nin ("hepsi öneri gibi", 2026-11-03), CT kararları MVP-1 CT'nin. Her karar r1'in önerisini aynen kabul eder; paket
gövdesi bu davranışı r1'de zaten yazıyordu. r2'de değişen: koşullu ifadelerin ("… seçilirse", "… = A ise") kesinleşmesi, iki XMC-001
izninin kapsamıyla yazılması (§5), INVENTORY sürüm numarası (1.8.0), Q-03'ün AC-20'de ölçülmesi ve Q-10'un DL-3 kabul kapısı olarak yazılması.

| # | Karar | Veren | Pakette nerede |
|---|---|---|---|
| Q-01 | A — yeni `Voided` durumu hold ve blocked için (`HoldStatus`, `BlockedStatus`, `DecisionState` / `DecisionEventKind`); neden `SourceReversed`; `Closed` yeniden kullanılmaz | Beste (sahip) | §1, §3, §4.1, §4.2, §8.7; AC-1, AC-9, AC-23 |
| Q-02 | A — satırı postlanmamış karar (PendingApproval / Approved / satırsız ApplyFailed) kayıtla birlikte `Voided`; sonradan gelen onay etkisiz; Platform Workflow örneği açık kalır | Beste (sahip) | §1, §2, §8.3, §8.6, §12 V8, §20; AC-3, AC-4, AC-13 |
| Q-03 | A — merkezi kayıtta aktör `System`, `ActorId` sabit `…0175`. XMC-001 izni: `InventoryCentralAuditScope.cs` sistem aktörü kipi (MOD-0173 / BL-501 bağdaştırıcısı; sahip aynı, Beste); kapsam yalnız iki yeni komutun gönderimi, `Building.Blocks` değişmez | Beste (sahip kararı + XMC-001 izni) | §3, §5, §6, §7, §14, §21; AC-20 |
| Q-04 | A — uygulanmış / satırı postlanmış karardan sonraki geri alma uyuşmazlık işareti + geçmiş; otomatik düzeltme yok | Beste (sahip) | §1, §8.3, §11; AC-6, AC-7, AC-10, AC-28 |
| Q-05 | A — elle kapatma yolu yok (`quarantine.holds.void` / `VOIDHOLD` yok, `VoidReason.Manual` yok); REVERSAL dışı hayalet kayıtlar FU-0175-FU01-OUTBOUND | Beste (sahip) | §2, §4.1, §8.2, §14, §20; AC-15 |
| Q-06 | A — DL-3 defter bekçisi bu pakette; XMC-001 MOD-0173 izni (`IReversalPrecondition.cs`, `InternalMovementPostingService.cs`, `InventoryErrorCodes.cs`, INVENTORY sözleşmesi); ek kod `REVERSAL_QUALITY_RECORD_LOCKED` | Beste (sahip kararı + XMC-001 izni; MOD-0173 sahibi aynı) | §1, §3, §5, §8.3, §8.8, §12 V10, §22; AC-31…AC-36 |
| Q-07 | QUARANTINE 1.4.0 (DL-1); INVENTORY 1.8.0 (DL-3) — FU03'ün 1.7.0'ından sonraki ek sürüm; FU03 onaylı (`c03ac5511`); DL-3'ün sözleşme adımı 1.7.0 dalda olduktan sonra | MVP-1 CT | künye, §7, §8.7, §8.8; AC-23, AC-36 |
| Q-08 | A — `Voided` satırlar kuyrukta görünür, sona sıralanır, KPI'ya girmez; varsayılan liste değişmez | Beste (sahip) | §8.7, §11; AC-18, AC-25 |
| Q-09 | A — geç intake: kaynağı geri alınmış hareket için kayıt açılmaz | Beste (sahip) | §8.4, §12 V9; AC-14 |
| Q-11 | Registry satırını CT ekler; paket kayıt dosyasına yazmaz | MVP-1 CT | kimlik bloğu (başlık altı), §6, §18 |
| Dal | `branch: feature/sce/mod-0173-inventory-ledger-valuation` — tek çalışma dalı kuralı; servis portu 5067 (`AGENTS.md` §3) | MVP-1 CT teyidi 2026-11-03 | künye, §8.1 |

**Açık kalanlar:** Q-10 (MVP-6 CT Natig — DL-3'ün MOD-0186 etkisi; DL-3 kabul kapısı, ready-for-dev önkoşulu değil); registry satırı (CT
ekler, Q-11); plan satırı (yetenek kimliği, plan MVP — plan sahibinden, §22); CT-APR-02 bağımsız inceleme (r4) ve CT-APR-01 onay satırı; **XMC-001 izninin
DL-3 Web dosyalarına genişletilmesi — sahip izni bekleniyor** (F-5@ae3fed7d; sahip Beste — `InventoryErrorMessages.cs`, `StockLedgerIndex` / `StockMovementsIndex` resx,
`InventoryResxParityTests.cs`; DL-3 Web adımının kapısı, ready-for-dev önkoşulu değil, §5; r4'te değişmedi).

### 19.6 Bağımsız inceleme 01 — kapanış tablosu (tur 1 → r3)

Bulgu dosyası `docs/records/reviews/MOD-0175-FU01/2026-11-03-ae3fed7d.md` (`ac98304e8`; dosya özeti `0d6412cd…74b4fc1`,
`git show 049bbb89d:<yol> | sha256sum`); hedef r2 pack sha256 `ae3fed7d…cd1db787`, kaynak `e89bce84c`; UYGUN DEĞİL, 2 ENGEL, 3 ORTA, 8 DÜŞÜK.
CT kararı MVP-1 CT 2026-11-03: hepsi kabul, r3'te tek geçiş (S-101). Sahip kararları Q-01…Q-09 yeniden açılmadı. Her düzeltme kodla ölçüldü
(`049bbb89d`, salt okuma; G-21…G-26).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@ae3fed7d (ENGEL) | `Voided` yalnız intake kökenli kayıtlara (hold `Source` ∈ GoodsReceipt / Production / CustomerReturn / OtherInbound; blocked `Origin = OtherInbound`); köken `HoldSource` / `BlockedOrigin` alanından okunur, aslın `SourceModule`'üyle çapraz denetlenir, eşlenmeyen köken MOD-0175 sayılır (fail-closed); MOD-0175 kökenli kayıtta (Manual / Retest / Recall / Rework hold, `ManualBlock`) REVERSAL kapanış değil `Conflict (OwnMovementReversed)`; DL-3 kural 1 bunu reddeder (`details.cause OwnMovementReversed`); Recall / Rework sonuçları yazıldı | §1, §3, §4.1, §8.3 (R1), §8.7, §8.8, §11, §12 V4, §13 #16, §17; AC-28, AC-32, AC-37 |
| F-2@ae3fed7d (ENGEL) | `Submitted` penceresi kural tablosunda (R3, R8); ortak yazma kilidi = kayıt `Version`'ı, karar yazarları W1–W6 işlem içinde olayları yeniden okur; gönderimin ikinci işlemi `Voided` kaydı `DecisionPending`'e çevirmez, sürüm uyuşmazlığında yeniden dener, `WorkflowStarted` yazılır; `StateOf`'ta `Voided` emici | §1, §4.2, §5, §8.3, §8.6, §8.9, §12 V8a, §13 #17; AC-13 (b), AC-33 |
| F-3@ae3fed7d | Süpürücüye terminal besleme (Decided hold, Closed / SampleRetained blocked; işaretsiz); seçim gerekçesi (REVERSAL tabanlı tarama yerine) ve gecikme sınırı yazıldı | §3, §8.2, §8.9, §13 #19; AC-6, AC-11 (b), AC-15 (b) |
| F-4@ae3fed7d | Kurgu kuralı: DL-1 ölçütleri REVERSAL'ı bekçinin izin verdiği anda postlar (gözlemcisiz fabrika) ya da eski veriyi doğrudan yazar, kayıt sonra ilerler; bekçi testte `Allow`'a alınmaz; DL-3 sonrası erişilebilirlik sınırı §8.3'te | §8.3, §8.8, §16 kurgu kuralı, §17; AC-4, AC-6, AC-7, AC-8, AC-10, AC-24, AC-31, AC-33 |
| F-5@ae3fed7d | `REVERSAL_QUALITY_RECORD_LOCKED` Web metni: `InventoryErrorMessages.ByCode`, `StockLedgerIndex` / `StockMovementsIndex` 7 dil, `InventoryResxParityTests` kod listesi DL-3 kapsamında; XMC-001 genişletmesi açık nokta | §3, §5, §8.8, §17, §18, §19.5, §22; AC-38 |
| F-6@ae3fed7d | MOD-0142 `reverseGrn` bugün Inventory'ye ulaşmıyor (zarf `MOD-0142` / `GOODS_RECEIPT` → 400; mock istemci) → DL-3 etkisi yok; bilgi notu buna göre | §7, §19.2 G-18, §19.4 |
| F-7@ae3fed7d | Sabit geliştirme anında ölçülür: N → N + 2 (bugün 71 → 73; MOD-0178 DL-2 önce birleşirse 82 → 84) | §5, §17, §19.2 G-16; AC-20 |
| F-8@ae3fed7d | `Views/Quarantine/Quarantine.cs` `Keys` listesi DL-2 Repo Scope'ta; yeni anahtarlar listeye de girer | §5, §11; AC-29 |
| F-9@ae3fed7d | DL-1 tek başına kullanıcıya açık ortama çıkmaz; açılış DL-1 + DL-2 birlikte; ara dönem bilinen geçici sapma | §22 |
| F-10@ae3fed7d | Tekil indeks yalnız kiracı kapsamlı (şirket yok), şirket süzgeci okumada; AC-16 (b) fixture'ı LE1'de kayıt açmayan hareketle | §4.4, §19.2 G-25; AC-16 |
| F-11@ae3fed7d | Besleme: kayıt koleksiyonunda `Id` imleçli sayfa (tur başına besleme başına bir sayfa, kısa sayfada başa döner), defter sorgusu kiracı + şirket önekli `ux_tenant_le_reversestransactionid`; sayfa boyutu seçeneği; 500'ü aşan kayıt ve plan ölçümü | §4.4, §8.2, §13 #20; AC-15 (c), (d) |
| F-12@ae3fed7d | `Voided` emici (F-2 ile ortak); `CloseWithoutMovementAsync` (W4) işlem içi yeniden okuma + kayıt `Version` yazımı; ret / iptal / zaman aşımı sonucu `200 Voided`, olay yok | §4.2, §8.6, §12 V8, §13 #18; AC-3 (b), AC-13 (c) |
| F-13@ae3fed7d | Komut sonucu adlı (`NoChange` nedeni `AlreadyRecorded` / `LostRace` / `NoRecord`); AC-12 iki okuma engeliyle yarışı zorlar ve kaybedenin `LostRace` dalını sayar; `TransactionRetryCounter` yalnız bilgi | §8.2, §12 V4, §17; AC-11, AC-12 |

**pack-review-common-findings taraması (r3):** §1 kendisiyle çelişki — AC numaraları ile dilim aralıkları, hazırlık listesi ile dilim kapıları
(Q-10, XMC-001 Web genişletmesi: ikisi de dilim kapısı, ready-for-dev önkoşulu değil), 71 / 73 sabiti dört yerde aynı kurala bağlandı; geçici
ölçüt yok (DL-1 ölçütleri DL-3 sonrasında da geçerli, §16 kurgu kuralı). §2 kodla tutmayan iddia — adlar ve satırlar `049bbb89d`'de grep ile
ölçüldü (G-21…G-26); indeks kapsamı düzeltildi (F-10); bağımlı modül sınırı (MOD-0142) dosya ve satırla yazıldı; arka plan işinin kimliği
(sistem aktörü) AC-20'de. §3 ölçülemeyen ölçüt — yarış ölçütleri zorlayan dekoratörle ve adlı sonuçla (AC-12, AC-13); sayfa boyutu ve imleç
kuralı yazıldı (AC-15); her yazma yolu için ayrı senaryo (W1–W6, AC-13 (a)–(c)). §3a değişmez — `Voided`'ı bozabilecek her yazar sayıldı (§8.6
W1–W6); ortak belge yazımı olmayan yollara sürüm yazımı eklendi (W3, W4). §3a durum × komut — §8.3 R0–R12 ve §8.9. §4 araç biçimi — ölçütler
liste satırı, hücre içi `|` yok. §5 dış sistem — Workflow örneği açık kalır (Q-02), geç gelen sonuç son durumla yanıtlanır (V8).

### 19.7 Bağımsız inceleme 01 — kapanış tablosu (tur 2 → r4)

Bulgu dosyası `docs/records/reviews/MOD-0175-FU01/2026-11-03-54d736e1.md` (`df4f48933`; dosya özeti `6e8caf9c…ddeb04812`,
`git show 0be75c8c6:<yol> | sha256sum`); hedef r3 pack sha256 `54d736e1…e6f394e8c5`, kaynak `023755fa4`; KOŞULLU, 0 ENGEL, 1 ORTA, 4 DÜŞÜK.
CT kararı MVP-1 CT 2026-11-03: hepsi kabul, r4'te tek geçiş (S-101). Sahip kararları Q-01…Q-09 ve tur 1'de verilen yazar seçimleri yeniden
açılmadı. Her düzeltme kodla ölçüldü (`0be75c8c6`, salt okuma; `023755fa4` ile kod farkı yok; G-27…G-31).

| Bulgu | Düzeltme | Bölümler |
|---|---|---|
| F-1@54d736e1 (ORTA) | **Seçim: işaret komut kipine taşındı** (AC-20 istisnası açılmadı). W5 / W6 `Voided` kararda hiçbir şey yazmaz; satırı postlanmışsa `ApplyAsync` işlem dışında `ApplySourceReversalTo{Hold,Blocked}Command(record.Voided.ReversalTransactionId)` gönderir (emsal `ApplyApprovedLotStatusChangeCommand`, `:93`); yeni R0a `Voided` kayıtta `Conflict (LinesPosted)` yazar. REVERSAL kimliği kaydın `Voided.ReversalTransactionId`'sinden, numarası / postlayanı / `CorrelationId`'si V2'nin defter okumasından. Sistem aktörü kipi gönderene değil komutun gönderimine bağlı → `RefreshDecisionCommand`, `ReapplyDecisionCommand`, okuma anı uzlaştırması ve MediatR dışı `QuarantineWorkflowOutcomeConsumer` yolunda da tam 1 merkezi kayıt `System`; iç içe gönderimde dış komutun kaydı ayrı (yığın). Gönderim kaçarsa süpürücünün yeni geçersiz beslemesi (72 sa penceresi, gerekçeli) yazar. §21 `RefreshDecisionCommand` / `ReapplyDecisionCommand` satırları buna göre | künye, §3, §4.1, §5, §8.2, §8.3 (R0a), §8.6 (W5, W6, "W5 / W6 `Voided` dalı"), §8.9, §11, §12 V8c, §13 #21, §19.2 G-27, G-28, §21; AC-20 (d), AC-28, AC-39 |
| F-2@54d736e1 | W3 onay işlemine W2 / W4 ile aynı ≤ 3 deneme + her denemede kayıt ve olay yeniden okuması; üçüncü uyuşmazlıkta hata yukarı (işlem geri alınmış, `Approved` yok), tüketici yolunda uzlaştırıcı sonraki okumada uygular; ikinci yazar olarak gerçek R1 işareti (onay sırasında `UpdateHoldCommand` 409 verdiği için) | §8.6 W3, §8.9, §13 #23, §19.2 G-29, G-30; AC-13 (a2) |
| F-3@54d736e1 | W1 satırı bugünkü koda hizalandı (CT kararı): okuma–kilit arası `Voided` → 409 `QUARANTINE_DECISION_ALREADY_PENDING` (kod değişmez), `Submitted` / Workflow yok; sonraki gönderim ilk yazma sayılır → 409 `QUARANTINE_HOLD_CLOSED`; seçim gerekçesi | §8.6 W1, §8.9, §12 V8b, §13 #22, §19.2 G-30; AC-13 (d) |
| F-4@54d736e1 | §8.3'e eşleşmeyen satır R13: `NoChange (Unmatched)`, yazma yok, hata logu (kayıt numaralı), asla `Void`; `Conflict` yerine seçilme gerekçesi (kapalı `Cause` listesi, yeniden değerlendirme); varsayılan dal istisna atmaz. Tutarlılık için DL-3 bekçisi R13'ü reddeder (`details.cause Unmatched`, INVENTORY 1.8.0 henüz yazılmadığından liste genişler) | §8.2, §8.3 (R13 ve "Eşleşmeyen durum"), §8.8, §12 V4, §13 #24, §17; AC-2 (b), AC-31, AC-36 |
| F-5@54d736e1 | Açık kayıttaki uyuşmazlık (R1, R6, R9, R10) kabul edilen sınır: kapanış yolu yok (Q-05 değişmez), tek kayıtlı kovada V7 409 `QUARANTINE_STOCK_CHANGED` ile kayıt kalıcı açık; Q-04'ün "QA elle düzeltir"i kayıt üzerinde eylem değildir; KPI, lot kalite durumu, recall istekçisi ve diğer §8.5 sayımlarına etkisi açıkça yazıldı; sayfa yardımında aynı sınır | §8.3 ("Açık kayıtta uyuşmazlık — kabul edilen sınır"), §19.2 G-31, §20, §23; AC-29 |
| Sahip izni (XMC-001, DL-3 Web) | r4'e işlenmedi: CT bildirimi (2026-11-03) "izin henüz gelmedi" → **sahip izni bekleniyor**, DL-3 Web adımının kapısı, ready-for-dev önkoşulu değil; izin gelince yeni paket sürümüyle yazılır | künye, §5, §18, §19.5 |

**Yeni gözlemler (r4 ölçümü sırasında; karar değil, bu sürümde davranış değişmedi — CT'ye):**
- `ApplyAsync`'in lot adımı (`ApplyApprovedLotStatusChangeCommand`, `QuarantineApplyEngine.cs:91-106`) satırlardan önce koşar; §8.6'nın
  "her satırdan önce yeniden okuma" kuralı bu adımı anmaz. Lotu değiştiren hold kararında (`NewRetestDate` / `LotStatusTarget`,
  `ChangesLot`, `:61-65`) onay ile kapanış yarışırsa lot `Voided` karara rağmen değişebilir. Bu bulgu tur 2 listesinde yok; ele alınması CT'nin
  kararıdır (ör. lot adımından önce de aynı yeniden okuma).
- Okuma anı uzlaştırması sorgu handler'larında yazar (`Approved` / `Applied` / ret olayları, W3 / W4'ün eklenen sürüm yazımı); bu yolun merkezi
  kaydı bugün yoktur (işaretli komut yok) ve denetim defterinin "Yazan sorgular" bölümü boştur. Ebeveynin mevcut davranışıdır; bu FU yeni
  yazma yolu açmaz, W5 / W6 işaretini komut kipine taşıyarak bu yoldan ayırır.

**pack-review-common-findings taraması (r4):** §1 kendisiyle çelişki — W5'in "aynı işlemde Conflict yazar" cümlesi §8.6 tablosu, akış
maddesi, §8.9, §11, §21 ve AC-20'de tek kurala (komut kipi) bağlandı; R0 ile §8.2 "tam bir kez" erken dönüşü R0a'yı içerecek biçimde birlikte
güncellendi; AC sayısı (39), dilim aralıkları (§22) ve hazırlık listesi aynı; DL-3 Web izni künye, §5, §18, §19.5'te aynı ifadeyle ("sahip
izni bekleniyor"). §2 kodla tutmayan iddia — W1 cevabı koddaki kodla yazıldı (F-3); W3 dalı koddaki yakalama listesiyle (G-29); bağlam sayısı ve
satırlar grep ile ölçüldü (G-27…G-31); "arka plan işinin kimliği" maddesi: iç komutun aktörü her bağlamda `System` (§5, AC-20 (d)).
§3 ölçülemeyen ölçüt — R13 için tablo dışı satırlar adıyla (AC-2 (b)); W1 / W3 yarışları dekoratörle zorlanır (AC-13 (a2), (d)); geçersiz
beslemenin pencere sınırı ölçülür (AC-39 (c)); her giriş yolu ayrı satır (AC-39 (a1)–(a4)). "Bir sayı raporlanıyor ama okuma yüzeyi yok" —
`Unmatched` ve `mark_dispatch_failed` loglarında kayıt numarası var, kayıt kuyrukta görünür. §3a — `Voided`'ı bozabilecek yazar listesi W1–W6
değişmedi; W5 / W6'nın yeni dalı yazmaz; "tamamlandı işareti ilk yazılıyor" — satır önce postlanır, işaret sonra ve iki yoldan (iç gönderim,
geçersiz besleme). §3a durum × komut — §8.3 R0a–R13 (eşleşmeyen satır dahil) ve §8.9. §4 araç biçimi — ölçütler liste satırı, hücre içi `|`
yok. §5 dış sistem — tüketicinin gelen kutusu satırı uygulamadan önce yazıldığı için W3'ün son hatasında sonuç uzlaştırıcıya kalır (§8.6 W3).

## 20. Follow-up Items

- **FU-0175-FU01-OUTBOUND** — REVERSAL dışı yollarla tutma statüsünden çıkan stok (QUARANTINE / QI / BLOCKED'dan MOD-0175 olmayan `SCRAP`,
  `WRITE_OFF`, `COUNT_LOSS`, transfer, `STATUS_TRANSFER` — `MovementDirectionRules.cs:62-107` yapısal olarak izin verir) de hayalet kayıt doğurur;
  ayrı karar (bekçi ya da kova uzlaştırma raporu). Q-05 = A (2026-11-03): bu FU'da elle kapatma yok, bu hayalet kayıtların yeri bu maddedir.
  **Not (r4, F-5@54d736e1):** aynı "kayıtsız kova farkı" sınıfına giren, bu FU'da işaretlenip açık kalan kayıtlar (§8.3 "Açık kayıtta uyuşmazlık
  — kabul edilen sınır": R1, R6, R9, R10) bu maddenin kararında birlikte ele alınır; bu FU onları kapatmaz.
- **FU-0175-FU01-INTAKE-AUDIT** — intake'in kayıt açılışı merkezi kayıt yazmıyor (G-14); intake'i iç MediatR komutuna taşımak (AUD-001 §2 "yeni
  kodda yazma yolu MediatR komutu").
- ~~FU-0175-FU01-WFCANCEL~~ — **açılmaz:** Q-02 = A (2026-11-03), C seçilmedi; Workflow örneği açık kalır, sonradan gelen onay etkisiz (§8.6).
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
| `ApplySourceReversalToHoldCommand` (**yeni**) | `LifecycleTransition` / `QuarantineHold` | b (K2; aktör `System` `…0175`, Q-03 = A) | Void: `Status` Open/DecisionPending → Voided, `Voided.*`, `ActiveDecisionId`; karar olayı `Voided`; geçmiş `SourceReversed` (`ReversalTransactionId`). Conflict (intake kökenli R6, R7, R9–R11; `Voided` kayıtta R0a `LinesPosted` — r4; MOD-0175 kökenli R1 `OwnMovementReversed`): `SourceReversalConflict.*`; geçmiş `SourceReversalConflict`. NoChange (`Unmatched` dahil) → kayıt yok. Gönderenler: gözlemci, süpürücü, uygulama motoru (W5 / W6); aktör her gönderende `System` | — |
| `ApplySourceReversalToBlockedCommand` (**yeni**) | `LifecycleTransition` / `BlockedStock` | b (K2; aktör `System` `…0175`, Q-03 = A) | aynı, blocked alanlarıyla | — |
| `QuarantineStockCommand` (mevcut) | `Create` / `QuarantineHold` | b | değişmez | — |
| `BlockStockCommand` (mevcut) | `Create` / `BlockedStock` | b | değişmez | — |
| `UpdateHoldCommand` (mevcut; `Voided`'da 409) | `Update` / `QuarantineHold` | b | değişmez; ret merkezi kayıt yazmaz (mevcut) | — |
| `StartReviewCommand` (mevcut; `Voided`'da 409) | `LifecycleTransition` / `QuarantineHold` | b | değişmez | — |
| `CompleteBlockedReviewCommand` (mevcut) | `LifecycleTransition` / `BlockedStock` | b | değişmez | — |
| `CreateDecisionCommand` (mevcut; `Voided`'da 409) | `Create` / `QuarantineDecision` | b | değişmez | — |
| `SubmitDecisionCommand` (mevcut; `Voided`'da 409) | `LifecycleTransition` / `QuarantineDecision` | b | değişmez; ikinci işlemin `Voided` dalı (§8.6 W2) yeni yazma yolu açmaz, aynı komutun işlemidir | — |
| `ReapplyDecisionCommand` (mevcut; `Voided` kararda 409) | `Execute` / `QuarantineDecision` | b | değişmez; yarışta `Voided` olan kararın satırı postlanmışsa (§8.6 W5 / W6) bu komut o dalda **yazmaz**, uyuşmazlık işaretini iç `ApplySourceReversalTo{Hold,Blocked}Command` kendi kaydıyla (`System`) yazar (r4, F-1@54d736e1) | — |
| `RefreshDecisionCommand` (mevcut; `Voided` kararda sonuç etkisiz) | `Execute` / `QuarantineDecision` | b | değişmez; W3 / W4'ün kayıt `Version` yazımı aynı komut işleminde (yeni komut yok). W5 / W6 `Voided` dalında bu komut uyuşmazlık işaretini **yazmaz**: işaret iç `ApplySourceReversalTo{Hold,Blocked}Command`'ın kendi işlemi ve kendi merkezi kaydıdır (`System`, tam 1); bu komutun kaydı onay yazımı için kendi aktörüyle ayrı kalır (r4, F-1@54d736e1; AC-20 (d), AC-39) | — |
| Okuma anı uzlaştırması (sorgu handler'ları) ve `QuarantineWorkflowOutcomeConsumer` (mevcut, işaretli komut yok) | — | — | onay / ret olayları ve W3 / W4 sürüm yazımı (ebeveyn davranışı; merkezi kayıt bugün yok, G-27); W5 / W6 işareti bu yollarda da yalnız iç komutla, kendi merkezi kaydıyla yazılır. Bu FU yeni yazma yolu açmaz; mevcut yolun merkezi kaydı ebeveyn kapsamıdır — CT'ye gözlem (§19.7) | — |
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
| DL-1 | çekirdek: kaynak geri alma kapanışı (intake kökenli) + uyuşmazlık işareti (MOD-0175 kökenli dahil) + gözlemci / süpürücü (açık + terminal + geçersiz besleme) + karar yazarlarıyla ortak kilit, `Voided` emici + açık yüklemi + denetim + QUARANTINE 1.4.0 | Owned Objects, Entity Fields, Repo Scope, Servis ve kapsam, Tetik — gözlemci + süpürücü; tam bir kez, Kural tablosu (`SourceReversalRules`), Geç intake, Açık / terminal yüklemi, Karar motoru ile dışlama, Sözleşme — QUARANTINE 1.4.0 (yalnız ekleme), Durum ve komut tablosu, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | ekran: kuyruk / detay / bloke stok durumu, uyuşmazlık rozeti, 7 dil, sayfa yardımı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001) | E3 | plan kimliği yok | plan satırı yok |
| DL-3 | defter bekçisi: uyuşmazlık doğuracak ve MOD-0175 kaynaklı REVERSAL reddi + INVENTORY 1.8.0 ek kodu + Web hata metni 7 dil (Q-06 = A; XMC-001 MOD-0173; Web adımı: XMC-001 sahip izni bekleniyor; kabul kapısı Q-10) | DL-3 — defter bekçisi (Q-06 = A), Repo Scope, Protected Paths, Dependencies, Audited Events | E4 | plan kimliği yok | plan satırı yok |

Kabul ölçütleri: DL-1 → AC-1…AC-24, AC-37, AC-39; DL-2 → AC-25…AC-30; DL-3 → AC-31…AC-36, AC-38 (r3'te eklenen iki ölçüt ve r4'te eklenen
AC-39 sona numaralandı, r2 numaraları değişmedi; toplam 39). DL-2 ve DL-3 DL-1'e dayanır. **Açılış sırası (F-9@ae3fed7d):** DL-1 tek başına kullanıcıya açık ortama (canlı ya da MVP
turu ortamı) çıkmaz; açılış DL-1 + DL-2 birlikte yapılır (§17 E3 smoke ikisini birlikte koşar). Gerekçe: DL-2'siz ekranda `Voided` hold kuyrukta
"Bekliyor" rozetiyle (`quarantine-ui.js:95-104`), Karar Ver eylemiyle (`queue.js:68`) ve detayda karar düğmeleriyle (`details.js:125`) görünür;
sunucu 409 döner ama ekran yanıltır; DL-1'e geçici rozet koymak DL-2'nin dosyalarını iki dilime böler. DL-1 kabulü ile DL-2 kabulü arasında
dalda bu görünüm **bilinen geçici sapmadır** ve DL-1 kabul kaydına yazılır. Plan satırı bu taslağa verilmedi
(plan dosyası depoda değil) → değer uydurulmadı; plan sahibinden alınıp yazılır (§18). DL-3 bu paketin dilimidir (Q-06 = A, Beste
2026-11-03). **DL-3 kapıları:** (1) sözleşme adımı — FU03'ün INVENTORY 1.7.0'ı dalda olmadan 1.8.0 yazılmaz (Q-07, §8.8); (2) **kabul kapısı
Q-10** — DL-3 kabul kaydı MVP-6 CT (Natig) kararını (tarih, veren) taşımadan kapanmaz; Q-10 ready-for-dev önkoşulu değildir, DL-1 / DL-2
kabulünü bağlamaz.

## 23. Page Help (UXH-001)

DL-2 yeni sayfa açmaz; mevcut üç sayfanın yardımı güncellenir (7 dil, `PageHelpResource.*.resx`):
- `Help.QUARANTINEQUEUE` — durumlar adımına "Geçersiz · Kaynak geri alındı: kaynak stok hareketi geri alındığı için kayıt kapandı; karar gerekmez".
- `Help.QUARANTINEDETAILS` — "Kaynak geri alındı · uyuşmazlık: stok bu kaydın dışında değişti; QA kontrol eder" açıklaması. Açık kayıt sınırı
  (r4, F-5@54d736e1, §8.3): "Uyuşmazlık işaretli kayıt açıksa açık kalır ve açık sayımlarda (KPI, lot kalite durumu, recall) tam miktarıyla
  görünür; kayıt üzerinde kapatma eylemi yoktur — QA düzeltmeyi stok tarafında yapar." Geçersiz kayıtta işaret: "Geçersiz kaydın kararına ait bir
  satır kapanıştan sonra postlandı; stok başka bir kaydın kovasından çıktı."
- `Help.BLOCKEDSTOCK` — durum süzgecindeki "Geçersiz" seçeneği; uyuşmazlık işaretli açık bloke kayıt için aynı açık kayıt sınırı cümlesi.
Tur yok (yeni sayfa yok).
