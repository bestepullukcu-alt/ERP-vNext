---
record_id: MOD-0120-DL2-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0120-accounts-receivable.md (status draft, sha256 2732616deb519a4c6c885b5b2a52f4d69be5a389265af53eca2e78dead74d3ae (pack D71 revizyonu, 2026-10-28 — D71 aday tabanı iş akışı izniyle de kesişir (`approve ∩ platform.workflow.tasks.approve`, tier-2 talepte `∩ tier2-approve`; anahtar başına ayrı DL-6 çağrısı), K-A4b kapandı, AC-9 genişletildi, MOD-0126 `a92c4a49…2446`'a yeniden sabitlendi; önceki sürüm `6064158a…` (pack D70 revizyonu, 2026-10-28 — D70/1 K-A4 kapandı (K-A4b açık), D70/3 Auth paket kapısı, D67 atıfları (D67/4), MOD-0126 `ec4943aa…e374`'e yeniden sabitlendi; boş liste 422 zaten vardı; önceki sürüm `38eaf632…` (pack sahip denetimi 61466e504 KOŞULLU revizyonu, 2026-10-28 — tier-2 aday kesişimi + MOD-0126 yeniden sabitleme; önceki sürüm `09ffc073…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `f61a93af…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `bb51d877…` (D58/D59 revizyonu); D57 sürümü `0e83cbc8…`; D56 sürümü `db147bcc…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `51f53042…` içindir)), dal chore/ct/treasury-intake-ct-05 @ec063fee9, "Revizyon (2026-10-27, D54 — MOD-0126 yeniden sabitleme)"; CT-APR-02 UYGUN, 0 ENGEL — execution/approvals/reviews.tsv:97; CT-APR-01 satırı yok)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5; pack atıfları @218acaf5, AR dosyaları iki commit'te eşit — DL-1 kaydıyla aynı)
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve bu pack sürümü dahil; D69 main'de değil ve MOD-0120'ye dokunmaz).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU numarası açık — K-1). Taslak main'de ama `draft` ve teyitsiz — pack'teki "Auth paket metni
> main'de" maddesi işaretsiz, paket kapısı açık (D53/1). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-1**'i; kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **MOD-0023 şablon kodu geldi:** `FT-AR-ADJUSTMENT-APPROVAL` (Y-12; W-4, W-6 — `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, #277 ile
> main'de) — iptal / alacak silme / tahsilat geri alma tek şablon, tek adım, atamasız; adaylar `CandidatePrincipalIds`. Şablonu
> kuran MOD-0023 kodu main'de **değil**, WP numarası ve tarih yok (DL-3 dilim kapısı). "Son adım değil" gelirse düzeltme talebi
> `PendingApproval`'da kalır, etki uygulanmaz (D67/4).
> - **Onaylayıcı aday tabanı** = ekin **DL-6** aday sorgusu (D66/1; D67/1 — DL-6 paket metni MOD-0120 DL-3 önkoşulu, kodu DL-3
> kabul kapısı; DL-1 ve DL-2 DL-6'yı beklemez). Eşik üstü düzeltmede `finance-treasury.receivable-adjustments.approve ∩
> .tier2-approve`; kurallar sonrası boş liste → 422 `RECEIVABLE_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (pack; D68 MOD-0126 için aynı
> deseni yazar).
> - Aday kaynağı (`IFinanceTreasuryApproverCandidateSource`) ve MOD-0023 bağdaştırıcısı servis ortak yapı taşıdır; tek yazarı
> MOD-0126 DL-2 (D67/3, D57/5); MOD-0120 DL-3 onu bekler.
> - **Denetim sınıfı:** Issue K2, SetCollectionStage K1 (D57/2).
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **MOD-0048 seed'leri** (`currency-minor-units`): Treasury CT ekler, Beste onaylar (D59/2, D60); ölçüm: main'de yok
> (`services/Diten.Platform/**`'te 0). `currency-minor-units` değer sorusu Beste'de (D60/3). `Finance = 18` main'de yok
> (`AuditEnums.cs` son değer `ESignature = 17`).
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol oldu; MOD-0120'nin 15 anahtarı ve rolleri değişmedi. MOD-0126 ortak tanımları
> pack "Kaynak ve kanıt"ta sha256 `a92c4a49…2446` ile sabitli (MOD-0126 D71 revizyonu; önceki `ec4943aa…e374` — D70; daha önce `3a70398a…e925` — D68 + sahip denetimi d5b378e50).
>
> **Güncel durum (2026-10-27).** Pack `status: draft`; bu sürüme CT-APR-02 kaydı var (`reviews.tsv:97`, UYGUN, açık ENGEL 0 —
> dalda, main'de değil); CT-APR-01 yok. `services/Diten.FinanceTreasuryService/` yok — MOD-0126 DL-1 de MOD-0120 DL-1 de
> **başlamadı**. MOD-0120 DL-2 yalnız MOD-0120 DL-1 kabul edildikten sonra başlar. Satır atıfları `yol:satır@ec063fee9`; pack
> `ready-for-dev` revizyonunda değişirse o sürümde yeniden doğrulanır (S-53). `MOD-0120-DL1-hazirlik.md` ile birlikte okunur.
>
> **MOD-0120'de DL-2 iş akışı dilimi değildir.** Pack "Teslim dilimleri" (`:1694-1697`): DL-2 = **açma ve tahsilat aşaması**
> (Issue, SetCollectionStage, yaşlandırma türetme). Onaylı düzeltme, elle tahsilat (D16), SoD, MOD-0023 ve belirsiz sonuç
> **DL-3**'tür. Bu kayıt DL-2'yi buna göre yazar; MOD-0023 şablonu, §17 kodu ve muafiyet DL-2'nin konusu değildir.

# MOD-0120 DL-2 hazırlığı — açma (Issue) ve tahsilat aşaması

Bu kayıt, MOD-0120 DL-2 WP prompt'unun DL-1 kabul edildiği gün yazılabilmesi için **kaynaktan ne alınacağını**, **neyin
uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi, kuyruğu ve onay kayıtlarını değiştirmez.

**DL-2 kapsamı** (`MOD-0120-accounts-receivable.md:1723-1726@ec063fee9`): `IssueReceivableInvoiceCommand` (`Draft → Open`;
tutarlar ve müşteri anlık görüntüsü kilitlenir, `IssuedBy` / `IssuedAt` yazılır), `SetReceivableInvoiceCollectionStageCommand`
(aşama tablosu, `CollectionStageHistory`), okuma anında yaşlandırma türetme (`NotDue` / `Due` / `Overdue` + `DaysOverdue`). Neden
ayrı: fatura kilitlenmesi ve aşama tablosu bağımsız incelenebilir; DL-3'ün önkoşulu. DL-2 tek bölümü kapatır: **Validation Rules**
(alan kuralları DL-1'de, aşama kuralları DL-2'de, SoD A1–A7 ve düzeltme / tahsilat satırları DL-3'te uygulanır — §5 bulgu 3).

## 1. Başlama koşulları (ölçüm: çalışma ağacı @ec063fee9, 2026-10-27)

Kaynak: DL-2 Önkoşul metni "Önkoşul: DL-1" (`:1724-1725`); pack'te **"— DL-2 için" işaretli dilim kapısı yok** — dilim kapıları
listesindeki bütün maddeler "— DL-3 için"dir (`:1479-1490`). "DL-2'ye etkisi": **bekletir** = DL-2 kodu bu kapanmadan başlamaz;
**kabul kapısı** = DL-2 başlar, Bitiş AC'leri bu kapanmadan kabul edilmez; **DL-3** = sonraki dilim.

| Koşul | Sahibi | Bugün | DL-2'ye etkisi |
|---|---|---|---|
| **MOD-0120 DL-1 kabul edildi** — DL-1 WP'si main'de, Bitiş AC'leri kabul kaydında, Auth kayıt kodu main'de ve DL-1 testleri gerçek izinle (D53/2) | MOD-0120 DL-1 WP + CT | **yok** — servis yok; kuyrukta `WP-0120-DL1` PLANNED, `WP-0126-DL1`'e bağlı (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:22`) | bekletir |
| ↳ MOD-0126 DL-1 main'de (iskelet, servis ortak yapı taşları, CentralAudit, defter) — MOD-0120 DL-1 üzerinden | MOD-0126 DL-1 WP | yok (`WP-0126-DL1` PLANNED) | bekletir (dolaylı) |
| ↳ Paket kapısı (DL-1 üzerinden): `ready-for-dev`, CT-APR-02/01, DCP-011, Auth paket metni (D53/1), `Finance = 18`, **MOD-0149 eki (O-3)**, TRM-14, `read-tenant-wide` Auth tanımı, BL-565 (`ARI`) — paket kapısı değil, DL-1 kabul kapısı (D59/1), küsurat seed'i | Beste / Ali / commercial-suite | CT-APR-02 **var** (`reviews.tsv:97`, dalda); CT-APR-01 yok; DCP-011 `status: draft`; MOD-0149 pack revizyonu gelmedi (checklist işaretsiz `:1472`; Y-4/4b "ek kabul edildi"); `LegalEntityLookupDto` `ApprovalStatus` taşımıyor (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106`); `AuditEnums.cs`'te `Finance` 0; BL-565 `DURUM: AÇIK` | bekletir (dolaylı) |
| MOD-0149 müşteri doğrulaması **Issue anında** (Validation Rules "Issue yalnız `Draft` ve müşteri + LE uygun", `:1027-1028`; O-4: faturalanabilir yalnız `active`, Y-4/4c) | commercial-suite | DL-1 paket kapısıyla aynı madde (O-3); DL-1 kabul edildiyse bağdaştırıcı (`Infrastructure/Customers/`) vardır | DL-1 üzerinden — ayrı kapı değil |
| MOD-0220 `ApprovalStatus` (TRM-14) — AC-2'nin Issue / SetCollectionStage kısmı | Ali | yok (yukarıda) | DL-1 üzerinden |
| O-12 "bugün"ün saat dilimi (yaşlandırma, `PromiseToPayDate ≥ bugün`) | — | **kapandı** — D28: UTC takvim günü (`:1685`) | — |
| Auth kayıt kodu `finance-treasury.receivable-invoices.issue` / `.collection-manage` | Treasury yazar (MOD-0018 eki DL-1 kodu), MOD-0018 sahibi teyit eder | yok (`ExplicitGrantOnlyPermissions.cs`'te ve Auth'ta `finance-treasury` 0; MOD-0018 Treasury Auth eki taslağı #281 ile main'de, kodu yok) | DL-1 kabul kapısı (D53/2) üzerinden; DL-2 testleri gerçek izinle |
| MOD-0023 AR düzeltme şablonu; §17 kodu; O-7 / O-8 / O-9 / O-13; MOD-0031 ölçümü | Ali / CT | **2026-10-28 (Y-12): karar ve kod geldi — `FT-AR-ADJUSTMENT-APPROVAL`** (Void / WriteOff / PaymentReversal tek şablon, tek adım, atamasız); şablonu kuran kod main'de değil; aday tabanı MOD-0018 Treasury Auth eki DL-6 (D66/1, D67/1 — DL-3 önkoşulu / kabul kapısı); ortak MOD-0023 bağdaştırıcısı ve aday kaynağı MOD-0126 DL-2'de (D57/5, D67/3). Ölçüm anında: şablon **kararı da yok** — Y-6: "müşteri faturası düzeltmesi" şablonu henüz karara bağlanmadı (`SOURCE-ANSWERS-2026-10-27.md:66-68`); §17 kodu yok | **DL-3** — DL-2'yi bekletmez |
| MOD-0262 / MOD-0031 | — | DL-2'de çağrı yok (Issue / aşama / yaşlandırma belge ya da kanıt okumaz) | — |

**DL-2'yi ilk bekleten:** MOD-0120 DL-1'in kabul edilmemiş olması — zincir MOD-0126 DL-1 → paket kapısı (Auth paket metni,
`Finance = 18`) ve MOD-0120'ye özgü **MOD-0149 eki (O-3, commercial-suite)**. DL-2'nin **kendine özgü engeli yoktur**;
DL-1 kabul edildiği gün başlayabilir.
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-2)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). 2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext archive
da4abb20 services/DitenErpTreasuryService` ile (salt okunur) **yeniden ölçüldü — DL-1 kaydındaki bütün AR özetleri ve satır
sayıları aynı**. **Kaynakta Issue ve elle aşama komutu yoktur:** fatura içe aktarmayla `ISSUED` / `OPEN` gelir; aşama
(`CollectionState`) içe aktarma yükünden yazılır ve projeksiyon uygunluğunda okunur. DL-2 kodu bu yüzden çoğunlukla **yenidir**;
kaynaktan yalnız geçiş tabloları ve enum değerleri uyarlanır. Karar dili DL-1 kaydıyla aynı; "madde N" = pack "Taşınmayacaklar"
(`:1604-…`).

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `CollectionState` → `ReceivableCollectionStage` 7 değer (DL-1'de enum alındı, DL-2 geçişleri yazar); aşama geçiş tablosu `CollectionStateTransitions` (uyarlanmış: tarih türevli `NOT_DUE` / `DUE` / `OVERDUE` hedefleri `None`'a karşılık gelir) | yaşam döngüsü geçişleri (`DRAFT → ISSUED → OPEN` → Recovery `Draft → Open`); kaynak olay adı (yalnız iz için) | saklanan `NOT_DUE` / `DUE` / `OVERDUE` aşamaları ve `OverdueFlag` / `OverdueDays` projeksiyon alanları (yaşlandırma okuma anında türer), serbest `DunningStage` / `CollectionStrategyCode`, `mod0120.*` olay adı, içe aktarmadan aşama yazımı, projeksiyon uygunluk sınıflandırması |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/AccountsReceivable/AccountsReceivableStateGuards.cs` | `607b3a8389dca36c` · 166 | al-uyarla (:48-106 aşama geçişleri — uyarlanmış) / yalnız referans (:9-45 yaşam döngüsü) | `ReceivableInvoiceStateRules` (DL-1'de açıldı; DL-2 aşama tablosu eklenir) | Recovery tablosu pack "Tahsilat aşaması" (`:293-315`): `None → InCollection / PromiseToPay / Disputed`, `InCollection → Dunning / PromiseToPay / Disputed / None`, `Dunning → InCollection / PromiseToPay / Disputed`, `PromiseToPay → None / InCollection / Disputed`, `Disputed → None / InCollection`; `WriteOffProposed` / `WrittenOff` elle girilemez (AC-7). Kaynak `NOT_DUE` / `DUE` / `OVERDUE` düğümleri (:51-70) ve `OVERDUE → WRITE_OFF_PROPOSED` (:69) **alma**. İstisna yaşam döngüsü :15-… **alma** |
| `S/src/…Domain/AccountsReceivable/AccountsReceivableEnums.cs` | `c00173f897d2497c` · 282 | DL-1'de alındı; DL-2 kullanır | `Domain/ReceivableInvoices/` | `CollectionState` :28-39 → `ReceivableCollectionStage` (None 0 … WrittenOff 6); `InvoiceLifecycleStatus` :7-17 → 5 değer (`ISSUED` + `OPEN` → `Open`) |
| `S/src/…Domain/AccountsReceivable/AccountsReceivableEntities.cs` | `c93d26919388244f` · 613 | yalnız referans (`CollectionState` :36, `DunningStage` :38) | `ReceivableInvoice` (`CollectionStage`, `PromiseToPayDate`, `CollectionReasonCode`, `CollectionStageHistory`, `IssuedBy`, `IssuedAt` — pack Entity Fields `:389-392`) | `DunningStage` serbest string **alma**; `CollectionStageHistory` ekleme-yalnız, ≤ 500 — **yeni** |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/AccountsReceivable/Services/AccountsReceivableFoundationService.cs` | `77212d1e9acc241f` · 327 | **alma** (DL-2 için) | — | `ClassifyProjectionEligibilityAsync` :163-210 aşamayı projeksiyon için okur — MOD-0125 alt sistemi (madde 10); Issue / aşama komutu yok |
| `…/Contracts/AccountsReceivableEventNames.cs` | `d073847d2b3a226c` · 95 | **alma** | `finance-treasury.receivable-invoice.issued`, `.collection-stage-changed` (pack Audited Events `:1182-1183`) | `mod0120.receivable.collection_status.changed` :10 yalnız iz (madde 7) |
| `…/Common/AccountsReceivableErrorCodes.cs` | `42e9ab7dcafce150` · 171 | yalnız referans | `Features/ReceivableInvoice/` hata sözlüğü (DL-2 alt kümesi) | kaynakta aşama geçiş hatası yok; Recovery kodları pack "Hata sözlüğü"nden: `RECEIVABLE_COLLECTION_TRANSITION_INVALID` (409), Issue sonrası değişiklik 409 (AC-4), `RECEIVABLE_LEGAL_ENTITY_NOT_ELIGIBLE` (422), `RECEIVABLE_CUSTOMER_NOT_ELIGIBLE` (422), sürüm çakışması 409; `MOD0120_*` **alma** |
| `…/Commands/AccountsReceivableV041Commands.cs`, `…/Services/AccountsReceivableV041Services.cs` | `3e69d754bd8481af` · 142, `517820651556face` · 897 | yalnız referans (CAS deseni — DL-1 kaydıyla aynı) | Issue / SetCollectionStage `If-Match` + `int Version` | — |
| `…/Contracts/AccountsReceivableReadContracts.cs` | `6c37d98143f2b7e6` · 199 | yalnız referans | liste / detay DTO'larına yaşlandırma alanları (`AgingState`, `DaysOverdue`) ve aşama süzgeci | yaşlandırma saklanmaz; süzgeç okuma anında hesaplanır (AC-8); aşama süzgeci `ix_receivable_invoice_stage` (pack `:525`) |
| — | — | **yeni** | `Commands/IssueReceivableInvoiceCommand.cs`, `Commands/SetReceivableInvoiceCollectionStageCommand.cs` + handler'lar + validator'lar (`ReceivableCollectionStageRequest`), yaşlandırma hesaplayıcısı (saf domain) | Golden Reference Compact; her komut `ICentralAuditDeclaration`; `Idempotency-Key` + `If-Match` zorunlu |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/AccountsReceivable/MongoAccountsReceivableRepository.cs` | `e4dec20044e50b81` · 1143 | yalnız referans (DL-2 için) | `Persistence/ReceivableInvoices/` — Issue / aşama yazımı (tek belge: fatura + gömülü `CollectionStageHistory`) | `Version` CAS; Mongo işlemi + `CentralAuditTransaction.EnlistAsync` (kaynakta `StartSession` 0 — madde 1) |
| `…/AccountsReceivableMongoIndexes.cs` | `888540018ceadb13` · 571 | **alma** | `ix_receivable_invoice_stage` (TenantId + LegalEntityId + CollectionStage), `ix_receivable_invoice_list` (… + DueDate — yaşlandırma süzgeci) | kaynak indekslerinin hiçbiri olduğu gibi taşınmaz (pack `:497`); indeks DL-1'de tanımlanmadıysa DL-2 ekler |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Api/Controllers/AccountsReceivableController.cs` | `ef24cfe6cd8d79d9` · 1689 | **alma** (DL-2 için) | `ReceivableInvoicesController.cs`: `POST /api/receivable-invoices/{id}/issue`, `POST /api/receivable-invoices/{id}/collection-stage` | kaynakta Issue / aşama ucu yok; `[Authorize]`-only + `EnsureAccess` **alma** (madde 6) |
| `…/Infrastructure/AccountsReceivable/GatewayBackedAccountsReceivableReferenceValidators.cs` | `250f9e364dfea47f` · 415 | yalnız referans (DL-1'deki gibi) | Issue anında `ICustomerReferencePort` (DL-1'de yazıldı) ve servis ortak `ILegalEntityEligibilityPort` | DL-2 yeni bağdaştırıcı yazmaz; LE doğrulayıcısı yalnız Active bakar (:13-123) → TRM-14 |

### 2.5 Testler (yalnız referans)

| Kaynak | Özet · satır | DL-2'de karşılığı |
|---|---|---|
| `S/tests/…/AccountsReceivable/AccountsReceivableFoundationTests.cs` | `14b4912cf714a05f` · 576 | aşama / Issue vakası yok — DL-2 testleri **yeni** |
| `…/AccountsReceivableV041ContractTests.cs` | `98cb95120a7ceebd` · 949 | `Concurrency_StaleRecordVersionReturnsControlledError` → Issue / aşama CAS 409 |
| `…/AccountsReceivableProjectionHandoffServiceTests.cs`, `…ProjectionPropagationTests.cs` | `0bea99fd1e4b2adc` · 380, `5aee6fabaaf7aed7` · 762 | **alma** (projeksiyon) |

### 2.6 Yetki ve roller — DL-2 (D46, D47, D49, D50)

Pack "Authorization Convention" (`:1118-1157`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-338`; D46 tablosu tarihçedir — D49/5).

- **DL-2 uçlarının anahtarları:** `finance-treasury.receivable-invoices.issue` (T2, `:1136`), `.collection-manage` (T2, `:1137`).
  Okuma (yaşlandırma liste / detayda) `.read` (T1), `.read-tenant-wide` (T3, yalnız açık atama, yalnız okuma). Bağımlı:
  `mdm.legal-entities.read` (AC-2), MOD-0149 müşteri okuma (O-3 sonucu — Issue müşteri uygunluğunu yeniden okur).
- **Test kişileri matristen** (`treasury-intake-owner-decisions-05.md:180-182`, `:259-260`): Issue ve SetCollectionStage →
  **Alacak (AR) operatörü** ya da **AR operasyon yöneticisi**; okuma → **AR görüntüleyici** (+ miras). **AR onaylayıcı** ve **AR 2.
  kademe düzeltme onaylayıcı** `.issue` / `.collection-manage` **taşımaz** — negatif test: 403. Ödeme / AP / nakit rolleri AR
  anahtarı taşımaz (D49/1).
- **SoD:** DL-2 komutlarında SoD kuralı yok (A1–A7 düzeltme kararlarına aittir, DL-3). Ama DL-2 **A2'nin girdisini** yazar:
  `IssuedBy` (faturayı açan, DL-3'te o faturanın düzeltmesine karar veremez). `IssuedBy` Guid (`sub`), sunucu yazar; istekte gelirse
  400.
- **D50** AR rollerini değiştirdi (AR onay yöneticisi kalktı, AR 2. kademe onaylayıcı tek başına atanır) — etkisi DL-3'tedir;
  DL-2'ye etkisi yok. **D47/4:** AR rollerine banka okuması verilmez.
- **`read-tenant-wide`** okuma kapsamını genişletir, yazmayı değil — AC-35 Issue / SetCollectionStage kısmı (LE-B'de 404, MOD-0220 /
  MOD-0149'a çağrı yok, kayıt / makbuz / olay yok).

## 3. Kaynakta olmayan, DL-2'de yeni yazılacaklar

- **Issue:** yalnız `Draft`; müşteri (MOD-0149, yalnız `active`) ve LE (Active + Approved) **o anda** uygun; `Status → Open`,
  `IssuedBy` / `IssuedAt`; tutarlar, satırlar, taksitler, para birimi, müşteri ve anlık görüntü kilitlenir — sonrasında bunları
  değiştiren her istek 409, Update / Delete yalnız `Draft` (AC-4). Olay `finance-treasury.receivable-invoice.issued` (Status,
  GrossTotal, CurrencyCode, CustomerId).
- **SetCollectionStage:** yalnız `Open` / `PartiallyPaid` ve açık düzeltme yokken; tablo dışı geçiş 409
  `RECEIVABLE_COLLECTION_TRANSITION_INVALID`; `PromiseToPay` → `PromiseToPayDate ≥ bugün (UTC)` zorunlu, `Disputed` → `ReasonCode`
  zorunlu; her geçiş `CollectionStageHistory`'ye (From, To, ReasonCode, ActorId Guid, At UTC) ve merkezi denetime (AC-7). **DL-2'de
  düzeltme talebi henüz yoktur** — "açık düzeltme yokken" koşulu DL-3'te `ux_receivable_adjustment_open` ile bağlanır; DL-2 bu
  kontrolü bir genişleme noktası olarak bırakır, sahte "her zaman yok" kodu yazmaz (prompt bunu açık yazmalı).
- **Yaşlandırma:** saf domain hesaplayıcısı; `NotDue` (bugün < vade), `Due` (=), `Overdue` (>) + `DaysOverdue`; taksitli faturada en
  eski ödenmemiş taksit — **DL-2'de tahsilat yoktur** (D16 elle tahsilat DL-3), bu yüzden "ödenmemiş" DL-2'de bütün taksitlerdir;
  tahsilat DL-3'te bağlanır. Saklanan alan yok (AC-8; birim testi saat sahtesiyle dün / bugün / yarın).
- `ix_receivable_invoice_stage` (DL-1 tanımlamadıysa), aşama süzgeci ve yaşlandırma süzgeci (DataTable sunucu tarafı).
- `accounts-receivable.openapi.yaml` iki uçla (sürüm artışı CT belirler).
- AC-31 açma kısmı: dev ortamında bir Issue, merkezi kaydı AUD-001 §8 sorgusuyla, on alan işaretli — AC-31 bu dilimde kapanır.

## 4. DL-2 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir. Hedef yol (öneri): `execution/domains/finance-treasury/work-packs/prompts/WP-0120-DL2-v1.0.md`.
Kuyrukta `WP-0120-DL2` satırı **yok** — bu kayıt kuyruğa yazmaz.

**Başlık satırları**

| Alan | Değer (taslak) | DL-2 başladığı gün doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — `/add-module` yok | — |
| WP / Prompt | `WP-0120-DL2` · `TRM-WP-0120-DL2-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0120-dl2` — **öneri — D51/3 / D52/1 deseni, sahip teyidi gerekir** | Expected HEAD = MOD-0120 DL-1 merge'ü ve kabul kaydı main'deyken alınan commit (S-53); worktree yolu |
| Risk | HIGH — kiracı, yetki, alacak tutarının kilitlenmesi, denetim | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı; MOD-0126 ortak tanımları sha ile sabit | sha |
| Kapsanan dilim | **yalnız DL-2** (açma ve tahsilat aşaması) — başlama koşulu yalnız DL-1 kabulü; pack'te "— DL-2 için" dilim kapısı yok; MOD-0023 / SoD / düzeltme / elle tahsilat **DL-3** — bu WP'ye girmez | — |

**DL-2 Bitiş AC'leri** (`:1725-1726`): **AC-2 (Issue / SetCollectionStage kısmı)**, 4, **5 (Issue / SetCollectionStage kısmı)**, 7, 8,
**28 (arka uç kısmı** — enum değerleri 5 / 7 / 3 birebir; rozet ve süzgeç ekranı DL-4), **31 (açma kısmı — AC-31 bu dilimde
kapanır)**, **35 (Issue, SetCollectionStage kısmı)**. AC metinleri pack'ten tırnakla alıntılanır. Kısmi AC'lerin kalanı: AC-2, AC-5,
AC-35 → DL-3 (orada kapanır). Idempotency (AC-17) ve tek aşamalı denetim olayı (AC-18) DL-2 listesinde yok — §5 bulgu 1; prompt bu
iki davranışı yine de Issue / SetCollectionStage için test ettirmelidir (D12/2, AUD-001).

**İzinli yollar** (pack Repo Scope `:582-597`): `services/Diten.FinanceTreasuryService/src/…Application/Features/ReceivableInvoice/**`,
`…Domain/ReceivableInvoices/**`, `…Persistence/ReceivableInvoices/**`, `…Api/Controllers/ReceivableInvoicesController.cs`,
`services/Diten.FinanceTreasuryService/tests/<servis test projesi>/ReceivableInvoices/**`; `docs/analysis/contracts/accounts-receivable.openapi.yaml`.
`Infrastructure/Customers/**` DL-1'de yazıldı — DL-2 yalnız kullanır (değişiklik gerekirse CT'ye sorulur).

**Korunan yollar:** `.antigravity/**`, `AGENTS.md`, `gateway/**`, `services/Diten.Platform/**`, `services/Diten.AuthService/**`,
`services/Diten.MdmService/**`, CRM / commercial-suite servisi (MOD-0149), `services/Diten.Building.Blocks/**`; servis ortak yollar
(`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`,
`Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`); MOD-0126 / 0119 / 0124 feature yolları ve
`Infrastructure/Workflow/**` (DL-3 konusu); `tests/architecture/audit-ledger/**` (yeni iz yok; sayaç değişirse CT kararı, AC-19);
`frontend/**` (DL-4); `execution/**`; kaynak depo.

**Önce oku:** pack (`ready-for-dev` sürümü), MOD-0126 pack (ortak tanımlar — makbuz, veri kapsamı), MOD-0120 DL-1 WP raporu ve
kabul kaydı, AGENTS.md, domain-config, `.antigravity/rules/` multi-tenancy, security-jwt, permission-key-standard, git-safety,
cross-module-change-consent, code-style (S-56), audit-trail-standard, data-scope-enforcement, mongo-indexing, handler-design;
karar kayıtları `treasury-intake-owner-decisions-0{1..5}.md` (D14/4, D27/2, D28 O-12, D29/1 O-3, D47, D49, D50, D52/2, D57/2,
D59/1), bu kayıt.

**Sözleşme yüzeyleri:** API `accounts-receivable.openapi.yaml` (Issue, collection-stage; `Idempotency-Key`, `If-Match`); tüketilen
MOD-0149 eki (lookup-validation, sözleşme belgesi) ve MOD-0220 TRM-14. İzin §2.6.

**Gerekli testler:** birim (aşama tablosu — her izinli ve izinsiz geçiş, tablo güdümlü; yaşlandırma dün / bugün / yarın + taksitli
vade, saat sahtesiyle; `PromiseToPayDate` / `ReasonCode` kuralları); **gerçek Mongo replica set** entegrasyonu (Issue sonrası
değişiklik 409, CAS 409, kiracı / şirket 404, AC-35 yazma kapsamı, makbuz tekrarı, CentralAudit commit / rollback ve outbox sabotajı)
— her takım kendi URI değişkenini okur, varsayılan porta düşmez, replica set `enableTestCommands` ile; sahte MOD-0220 (Active+Approved /
Active+Draft / Inactive / erişilemez) ve sahte MOD-0149 (`active` / `suspended` / yok / erişilemez); mimari (her aksiyonda
`[HasPermission]`, saklanan yaşlandırma alanı yok); sözleşme testi; build 0 hata; yerel kapıda `scripts/` Python testleri.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş**: hedef servis + proje yolu; aggregate + alanlar (`ReceivableInvoice`: `Status`,
  `CollectionStage`, `PromiseToPayDate`, `CollectionReasonCode`, `CollectionStageHistory`, `IssuedBy`, `IssuedAt`, `Version`);
  komut listesi (Issue, SetCollectionStage) ve yaşlandırma hesaplayıcısı; validator kuralları (pack Validation Rules aşama satırları ve
  "Durum kuralları" `:1027-1031`); izin anahtarları §2.6; `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 (S-52); `prompt_gate.py review-check` (S-54); HIGH iş — kabul kanıtı betiğini CT yazar (S-83); Expected HEAD
DL-2 başladığı gün doldurulur (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22.

**Kod alma izi:** §2'deki **al-uyarla** satırı (aşama geçiş tablosu, uyarlanmış) WP raporunda `path@da4abb20 + sha256` ile.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive` → kazı dizini); kaynak ve Recovery testleri **çalıştırılmadı**; Recovery'de
  yalnız dosya okuma ve `grep` (@ec063fee9).
- MOD-0149 pack revizyonunun, Auth kaydının ve DL-1'in tarihi sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi):**

1. **DL-2 yazma komutlarının idempotency ve denetim AC'leri hiçbir dilimde sayılmıyor.** AC-17 (idempotency, "her yazma komutu")
   DL-1'de "(Create kısmı)", DL-3'te "(kararlar)" olarak geçiyor; AC-18 (tek aşamalı komut tam bir olay) DL-1'de "(tek aşamalı)",
   DL-3'te "(iki aşamalı + sabotaj + kurtarma)". Issue ve SetCollectionStage (DL-2'nin iki yazma komutu) ne AC-17 ne AC-18 kısmında
   anılıyor (`:1725-1726`). Ayrıca DL-1 AC-17'yi yalnız "Create kısmı" ile sınırlıyor — Update / Delete / BulkDelete idempotency'si de
   adsız kalıyor. Öneri: DL-2 "17 (Issue / SetCollectionStage kısmı)", "18 (Issue / SetCollectionStage kısmı)". **Kapandı** (pack D56
   revizyonu, bulgu 1).
2. **"Açık düzeltme yokken" koşulu DL-2'de doğrulanamaz.** Validation Rules SetCollectionStage'i "açık düzeltme yokken" kısıtlıyor
   (`:1029`); düzeltme talebi DL-3'te doğar. Pack bunun DL-2'de nasıl yazılacağını söylemiyor (boş koşul mu, DL-3'te mi bağlanır).
   Aynı şekilde yaşlandırmanın "en eski ödenmemiş taksit" kuralı tahsilatı (DL-3) varsayıyor. **Kapandı** (pack D56 revizyonu: DL-2
   notu, bulgu 2).
3. **Validation Rules DL-2'de "kapanıyor" ama satırlarının çoğu DL-3'te uygulanıyor.** "Her bölüm tabloda bir kez, **kapandığı**
   dilimde yazılıdır" (`:1699`) — Validation Rules DL-2'de (`:1695`), ama SoD A1–A7, düzeltme, `PaymentId`, tahsilat satırları DL-3
   komutlarına aittir; pack notu "SoD kuralları DL-3'te uygulanır" diyor. Kabul kaydında Validation Rules DL-2'de "kapandı" sayılırsa
   DL-3 kuralları kapanmamış kalır. Öneri: Validation Rules'u DL-3'e taşımak ya da DL-2'yi "aşama kuralları kısmı" olarak yazmak.
   **Kapandı** (pack D56 revizyonu: DL-2'de yalnız aşama kuralları kısmı, tam kapanış DL-3 kabulünde — bulgu 3).
4. **AC-2 / AC-5 / AC-35 DL-2 kısmı dışında AC-6 DL-1'de tam kapanıyor** — AC-6 (veri kapsamı kanıt testi) yalnız liste / detay
   anıyor; Issue / SetCollectionStage'in kapsam dışı şirkette 404 vermesi AC-35'ten geliyor. Çelişki yok, ama veri kapsamı sabotajının
   (kapsam süzgeci kaldırılınca kırmızı) DL-2 yazma uçlarında da koşulacağı yazılı değil; prompt AC-35 sabotajını Issue /
   SetCollectionStage için açık istemeli. **Kapandı** (pack D56 revizyonu: DL-2 AC-35 kısmına kapsam sabotajı — bulgu 4).
5. **Küçük:** Audited Events'te Issue / SetCollectionStage için "K2 (fail-closed): evet (öneri)" (`:1182-1183`) — D14/3 AR'de K2'yi
   düzeltme talebi, onay, ret ve elle tahsilat için verdi; Issue / aşama için karar yok, "öneri" olarak kaldı. `RequireEnlistment`
   ile davranış aynı (olay yazılamazsa komut başarısız), ama sınıf kararı prompt'tan önce netleşmeli. **Kapandı — KARAR D57/2:**
   Issue K2, SetCollectionStage K1.
