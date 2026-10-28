---
record_id: MOD-0119-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0119-accounts-payable.md (status draft, sha256 51411aaa5b6c67db1ee27378f66e801577b05bb28859fd4505dad121cb2ff7de (pack sabitleme revizyonu, 2026-10-28 — yalnız MOD-0126 `58976767…b76e`'e yeniden sabitlendi (MOD-0126 KARAR D77 revizyonu — R4 kapalı partide muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, Release'de R11 tier-2 şartı tutmazsa 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`; önceki `8127b3cd…e84c`), bu paketin kuralları, AC'leri ve dilim tablosu değişmedi; önceki sürüm `2c2b6bac…7952` — pack CT-APR-02 Codex b91d61325 KOŞULLU bulguları revizyonu, 2026-10-28 — AC-4 ve AC-5 genişletildi: D72/D73 dalları komut ve dilim başına ölçülür (yazım / niyet / makbuz / denetim olayı yok), Bitiş listeleri "(… kısmı)" deseniyle — DL-1 AC-4 / AC-5 Create / Update / Validate kısmı, DL-2 Submit / Approve kısmı (AC-5 tam kapanış), DL-3 AC-4 ReleaseHold kısmı (tam kapanış); MOD-0126 `8127b3cd…e84c`'e yeniden sabitlendi (önceki `674c73e9…79ea`); yeni AC yok, yeni hata kodu yok; önceki sürüm `f5c21eb5…1048` — pack D76 revizyonu, 2026-10-28 — kullanıcının başlattığı Reconcile da niyetteki ilk aday listesini kullanır — yalnız KARAR D76/3 atfı, davranış değişmedi; MOD-0126 `674c73e9…79ea`'ye yeniden sabitlendi (önceki `b5e749bd…732e`); AC genişletilmedi, yeni AC yok, yeni hata kodu yok; önceki sürüm `72e580c6…45d7` — pack D75 revizyonu, 2026-10-28 — D73/1 çıkarımı teyit (404 `UNKNOWN_SUPPLIER` → 422 `AP_MATCH_REFERENCE_MISMATCH`, KARAR D75/3; AC-5 genişletildi); iç uç kimliği atıfları KARAR D74/1'e çekildi; Reconcile'ın yeniden başlatması niyetteki ilk aday listesini kullanır (D75/2 (b); AC-34 genişletildi); BANK-ACCOUNT-LOOKUP v1'de tüketilmiyor — D75/4 yalnız Follow-up'ta; MOD-0126 `b5e749bd…732e`'ye yeniden sabitlendi (önceki `fb581b3b…cce5`); yeni AC yok, yeni hata kodu yok; önceki sürüm `824c963e…6f68` — pack sabitleme revizyonu, 2026-10-28 — MOD-0126 `fb581b3b…cce5`'e yeniden sabitlendi (MOD-0126 D74/3 — muafiyet atayanı Muafiyet yetkilisi; önceki `1b8f172a…8ddf`), bu paketin kuralları ve AC'leri değişmedi; önceki sürüm `7bd02653…506c` — pack D73 revizyonu, 2026-10-28 — O-23 kapandı (KARAR D73/1): MOD-0143 faturasının `supplierId`'si `getSupplierEligibility` ile kanonik koda çevrilip saklı kanonik kodla ordinal karşılaştırılır; çevirme okunamazsa D72/2 (503 `AP_SUPPLIER_UNAVAILABLE`; 404 `UNKNOWN_LEGAL_ENTITY` → 422 `AP_LEGAL_ENTITY_UNKNOWN`); AC-5 genişletildi, yeni AC yok, yeni hata kodu yok; MOD-0126 sabitlemesi `1b8f172a…8ddf` değişmedi) — önceki sürüm `17c5b4bf…` (pack D72 revizyonu, 2026-10-28 — MOD-0140 Revizyon 5 hizalaması (`b3259e40…ce5c2`): 1.3.0 MOD-0140 DL-1'de FROZEN, uçlar MOD-0140 DL-3'te, DL-3'e kadar sahte istemci / Prism, canlı bağlantı yok; uygunluk `known` ∧ `eligibleForTransactions == true`; MOD-0140 403 / 503 / okunamayan → 503 `AP_SUPPLIER_UNAVAILABLE` (mevcut ad), 404 `UNKNOWN_LEGAL_ENTITY` → 422 `AP_LEGAL_ENTITY_UNKNOWN` (yeni kod); kanonik `SupplierId` + yeni `SupplierMasterId`, ad `getSupplier`; AP rollerine `procurement.suppliers.eligibility.read` / `.read` bağımlılığı; AC-4 genişletildi, yeni AC-44, yeni O-23; MOD-0126 sabitlemesi `1b8f172a…8ddf` değişmedi) — önceki sürüm `d6963438…` (pack D71 revizyonu, 2026-10-28 — D71 aday tabanı `ap-invoices.approve ∩ platform.workflow.tasks.approve` (anahtar başına ayrı DL-6 çağrısı), K-A4b kapandı, AC-10 genişletildi, MOD-0126 `1b8f172a…8ddf`'a yeniden sabitlendi; önceki sürüm `4f12cb30…` (pack D70 revizyonu, 2026-10-28 — D70/2 boş aday listesi 422 `AP_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (AC-10), D70/1 K-A4 kapandı (K-A4b açık), D70/3 Auth paket kapısı, D67 atıfları, MOD-0126 `ec4943aa…e374`'e yeniden sabitlendi; önceki sürüm `eea3d461…` (pack sahip denetimi 61466e504 KOŞULLU revizyonu, 2026-10-28 — MOD-0126 ortak tanımları yeniden sabitlendi; önceki sürüm `f7ee03aa…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `567f59ba…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `bd554f82…` (D58/D59 revizyonu); D57 sürümü `5a6f2bc2…`; D56 sürümü `126063f4…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `faa95829…` içindir)), dal chore/ct/treasury-intake-ct-05, "Revizyon (2026-10-27, D54 — MOD-0126 yeniden sabitleme)"; önceki b634d6c3…; satır atıfları @00d3f2b41 sürümüne (9262000a…07c5); main @904dfd5c2 sürümü b4d15bab…2c4d)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5; pack atıfları @218acaf5, AP dosyaları iki commit'te eşit — pack "Kaynak ve kanıt")
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve bu pack sürümü dahil; D69 main'de değil ve MOD-0119'a dokunmaz).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU numarası açık — K-1). Taslak main'de ama `draft` ve teyitsiz — pack'teki "Auth paket metni
> main'de" maddesi işaretsiz, paket kapısı açık (D53/1). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-1**'i; kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **MOD-0023 şablon kodu geldi:** `FT-AP-INVOICE-APPROVAL` (Y-12; W-2, W-6 — `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, #277 ile
> main'de) — tek aşama, tek onay adımı, atamasız; adaylar `CandidatePrincipalIds`. Şablonu kuran MOD-0023 kodu main'de **değil**,
> WP numarası ve tarih yok (DL-2 dilim kapısı). Çok adımlı dal v1'den çıktı: AC-14 "ara onay durumu yok" testi (D66/4, D62/2);
> "son adım değil" gelirse fatura `ApprovalPending` / `PendingDecision`'da kalır (D66/2).
> - **Onaylayıcı aday tabanı** = ekin **DL-6** aday sorgusu, anahtar `finance-treasury.ap-invoices.approve` (D66/1; D67/1 — DL-6
> paket metni MOD-0119 DL-2 önkoşulu, kodu DL-2 kabul kapısı; DL-1 DL-6'yı beklemez). R1–R3 ve şirket kapsamı sonrası boş liste
> → pack'e göre MOD-0023 400 → kesin başlatma reddi (AC-10; D68 MOD-0126 içindir).
> - Aday kaynağı (`IFinanceTreasuryApproverCandidateSource`) ve MOD-0023 bağdaştırıcısı servis ortak yapı taşıdır; tek yazarı
> MOD-0126 DL-2 (D67/3, D57/5); MOD-0119 DL-2 onu bekler.
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **MOD-0048 seed'leri** (`currency-minor-units`): Treasury CT ekler, Beste onaylar (D59/2, D60); ölçüm: main'de yok
> (`services/Diten.Platform/**`'te 0). `currency-minor-units` değer sorusu Beste'de (D60/3). `Finance = 18` main'de yok
> (`AuditEnums.cs` son değer `ESignature = 17`).
> - **MOD-0140 supplier 1.3.0** main'de değil (ölçüm: `supplier.openapi.yaml` `version: 1.2.0`) — DL-1 paket kapısı.
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol oldu; MOD-0119'un 19 anahtarı ve rolleri değişmedi. MOD-0126 ortak tanımları
> pack "Kaynak ve kanıt"ta sha256 `58976767…b76e` ile sabitli (MOD-0126 KARAR D77 revizyonu — R4 kapalı partide muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, Release'de R11 tier-2 şartı tutmazsa 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`; önceki `8127b3cd…e84c` — MOD-0126 CT-APR-02 Codex b91d61325 KOŞULLU bulguları revizyonu — R4 uygulanma koşulu `IsTier2` ∧ sabitlenmiş `R4Enabled = true`, AC-41 kapanış cümlesi; önceki `674c73e9…79ea` — D76 revizyonu — yalnız D76/3 atfı: kullanıcının başlattığı Reconcile da ilk aday listesini kullanır; önceki `b5e749bd…732e` — D75 (iç uç kimliği D74/1, Reconcile'da ilk aday listesi D75/2 (b)); `fb581b3b…cce5` — D74/3 (muafiyet atayanı Muafiyet yetkilisi); `1b8f172a…8ddf` — D71; `ec4943aa…e374` — D70; daha önce `3a70398a…e925` — D68 + sahip denetimi d5b378e50).
>
> **Güncel durum (2026-10-27).** Bu kayıt pack'in çalışma ağacındaki sürümüne bağlıdır: sha256 `9262000a…c5`, `status: draft`,
> dal `chore/ct/treasury-intake-ct-05` @00d3f2b41 (= main @904dfd5c2 + beş yerel docs commit'i; `git diff --stat 904dfd5c2 HEAD`
> yalnız 11 docs / execution dosyası — kod ve kural yolları main ile aynı). Bu sürüme CT-APR-02 ve CT-APR-01 satırı yok. Önceki
> sürümlere (`701cb9e9…`, `f97cd3fa…`) yapılan "pack satır N" atıfları **geçersizdir**; bu kayıt pack'e bölüm adıyla ya da
> `yol:satır@00d3f2b41` ile atıf yapar. Pack `ready-for-dev` revizyonunda yeniden değişirse atıflar o sürümde yeniden doğrulanır (S-53).
>
> **D51 sonrası (2026-10-27).** Pack "Revizyon (2026-10-27, D51)" ve "D51 denetimi" ile (`MOD-0119-accounts-payable.md:28-42@00d3f2b41`):
> (D51/1) ENGELLER **paket kapısı** (işaretsiz) ↔ **dilim kapıları** ("— DL-n için") olarak ayrıldı — DL-1 yalnız paket kapısını
> bekler; Auth izin / rol kaydı (MOD-0018 paketi ve kodu, Ali, Y-6) paket kapısına girdi. (D51/2) SEC-002 kapsamı soran kod + kanıt
> testi (AC-32) **DL-1 kabul kapısında**; AC-32 kısmi kapanır (DL-1 uçları DL-1'de, iz ve karar uçları DL-2'de, bekletme / istisna /
> kanıt uçları DL-3'te). (D51/3) MOD-0126 DL-1 WP'sinin ilk adımı servis iskeletidir; MOD-0119 iskeleti açmaz. 2026-10-26
> kaydındaki "pack'te tutarsızlık" maddeleri gap listesi 21 ile pack'e işlenmişti; açık kalanlar §5'tedir.
> D53 sonrası: Auth paket metni paket kapısı, kayıt kodu DL-1 kabul kapısı.
>
> **D52 sonrası (2026-10-27).** Pack "Revizyon (2026-10-27, D52)" ile (sha256 `45c136f3…76c0`): §5 bulgu 1 (MOD-0126 ortak
> tanımları sha256 `0b58ae58…8f59` ile sabitlendi), 2 (`invoice-match.openapi.yaml` 1.1.1, `aa88132c…7370`), 3 (§17 onay maddeleri
> `[x]`, `approvals.tsv:16`), 4 (RCS-001 sahibi Beste, D44) ve 5 (yinelenen `status: draft`) kapandı; DL-1 WP dalı KARAR D52/1.
> Aşağıdaki `@00d3f2b41` satır atıfları o sürüme aittir; D52 revizyonu satırları kaydırdı.

# MOD-0119 DL-1 hazırlığı — AP kaydı çekirdeği ve doğrulama

Bu kayıt, MOD-0119 `ready-for-dev` olduğunda DL-1 WP prompt'unun aynı gün yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

**DL-1 kapsamı** (pack "Teslim dilimleri", `MOD-0119-accounts-payable.md:1628-1638@00d3f2b41`): `Features/ApInvoice/` içinde
Create / Update / Delete / BulkDelete / Validate + liste / detay sorguları, Domain / Persistence, AP'ye özgü bağdaştırıcılar
(`Infrastructure/Suppliers/` — MOD-0140 `ISupplierEligibilityPort`, `Infrastructure/InvoiceMatch/` — MOD-0143 `IInvoiceMatchPort`),
`ap-invoice.openapi.yaml`; alacak dekontunun Validate ön kontrolü (kapasite, orijinal kuralı).

**Servis ortak yapı taşları MOD-0119'un değildir.** `Diten.FinanceTreasuryService` iskeleti, `FinanceTreasuryCommandReceipt`
+ iki indeksi, `IFinanceTreasuryDataScopeSource`, `ILegalEntityEligibilityPort`, `ICurrencyReferencePort`, `IRecordCodeAllocator`,
bağdaştırıcıları (`LegalEntities`, `Currency`, `RecordCodes`, `DataScope`) ve CentralAudit kaydı **yalnız MOD-0126 DL-1'de**
açılır (pack "Repo Scope", `:502-518@00d3f2b41`). MOD-0119 DL-1 bunları yeniden tanımlamaz, açmaz, değiştirmez; yalnız kullanır.
Ortak tipte değişiklik dört Treasury paketinin onayını ister (pack "Protected Paths", `:546-549@00d3f2b41`).

## 1. Başlama koşulları (ölçüm: çalışma ağacı @00d3f2b41 = main @904dfd5c2 + yerel docs commit'leri, 2026-10-27)

Kaynak: pack "Ready-for-dev Checklist" paket kapısı (`:1417-1441@00d3f2b41`), dilim kapıları (`:1443-1451`) ve DL-1 Önkoşul
metni (`:1634-1637`). "DL-1'e etkisi": **bekletir** = pack'in işaretsiz paket kapısı maddesi, DL-1 kodu bu kapanmadan başlamaz;
**DL-2 / DL-3** = "— DL-n için" dilim kapısı, DL-1'i bekletmez (D51/1).

| Koşul | Sahibi | Bugün | DL-1'e etkisi |
|---|---|---|---|
| Pack `ready-for-dev` + o sürüme CT-APR-02 | Treasury CT + bağımsız inceleyici | `status: draft` (`:11`); güncel sha `824c963e…6f68` (sabitleme revizyonu — MOD-0126 D74/3; önceki `7bd02653…506c`) için `execution/approvals/reviews.tsv`'de satır yok (son MOD-0119 incelemesi `db55f09a…`, `reviews.tsv:79`) | bekletir |
| CT-APR-01 sürüme bağlı onay (`high: evet`, paketi yazmamış onaylayıcı; D34/2) | Beste | `execution/approvals/approvals.tsv`'de MOD-0119 satırı yok (grep 0) | bekletir |
| DCP-011 `approved` | Beste | `execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md:7` `status: draft`; numara atandı (Y-4/2) | bekletir |
| Auth paket metni — MOD-0018 paketi (anahtarlar, açık atama listesi, rol şablonları D46–D50) | Treasury yazar, MOD-0018 sahibi (Ali) teyit eder (Y-15, Y-16; önceki: Y-6; numara / tarih yok) | **2026-10-28: taslak main'de** (#281; origin/main @ `027ca2e38`) — `…/MOD-0018-FU-TBD-treasury-permission-onboarding.md` `draft`, FU numarası açık (K-1), MOD-0018 sahibinin teyidi yok, pack checklist maddesi işaretsiz; Y-14 geçersiz (kullanılmadı); ölçüm anında: **yok** — Treasury rolleri için MOD-0018 paketi yok; depoda yalnız `MOD-0018-rbac-abac-authorization.md` ve FU9…FU22; hiçbirinde `finance-treasury` geçmiyor (grep 0) | paket kapısı (D53/1) — DL-1'i bekletir |
| Auth kayıt kodu (MOD-0018 Treasury Auth eki DL-1 kodu) | Treasury yazar, MOD-0018 sahibi teyit eder (Y-15, Y-16) | **yok** — `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`'te `finance-treasury.*` 0 | DL-1 kabul kapısı (D53/2) — DL-1 başlamasını bekletmez, kabulünü bekletir |
| **MOD-0126 DL-1 main'de** — iskelet, servis ortak yapı taşları, CentralAudit kaydı, defter | MOD-0126 DL-1 WP | `services/Diten.FinanceTreasuryService/` **yok**; MOD-0126 pack `draft`; kuyrukta `WP-0126-DL1` PLANNED (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:19@00d3f2b41`) — MOD-0126 DL-1'in kendi paket kapısı (aşağıdaki üç satır + Auth paket metni + BL-565 + MOD-0048 seed'leri) açık | bekletir |
| ↳ Platform `AuditCategory`'ye `Finance = 18` (D35/3, Y-4/7) — MOD-0126 DL-1 üzerinden | Beste | **yok** — enum `Unknown = 0 … ESignature = 17` (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-27@00d3f2b41`, son değer `:26`); kopya `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs:29` son değer `ESignature = 17` | bekletir (dolaylı: "MOD-0126 DL-1 bu değer main'e girmeden başlamaz" — pack Audited Events `:1122-1126`) |
| Port 5071 | — | **kapandı** — `AGENTS.md:84`, `.antigravity/rules/ports.md:29` (D30) | — |
| TRM-14: MOD-0220 `lookup-validation` yanıtında `ApprovalStatus` + sözleşme belgesi | Ali (kod MOD-0023 kulesinin sırasında, Y-5/3) | **yok** — `LegalEntityLookupDto(LegalEntityId, Code, LegalName, DisplayName, LifecycleState, Referenceable)` (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106@00d3f2b41`); sözleşme yalnız taslak `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` | bekletir (AC-2) |
| MOD-0140 `supplier.openapi.yaml` **1.3.0** main'de | Cihan (MVP-2) | main'de **1.2.0** (`docs/analysis/contracts/supplier.openapi.yaml:13@00d3f2b41`) | bekletir (AC-4; pack: "1.3.0 main'e girmeden DL-1 başlamaz", Dependencies `:567`) |
| SEC-002: tasarım (Y-4/6) + Auth'ta `read-tenant-wide` açık atamalı tanımı | Beste (yol), Ali (Auth) | yol **belli** (Y-4/6); Auth tanımı **yok** (`ExplicitGrantOnlyPermissions.cs`'te `finance-treasury.ap-invoices.read-tenant-wide` 0). Kapsamı soran kod + AC-32 bu kapının maddesi değil — **DL-1 kabul kapısı** (D51/2; pack `:1434`) | bekletir — yalnız Auth paket metnindeki açık atama tanımı (D53/1); `ExplicitGrantOnlyPermissions.cs` kaydı DL-1 kabul kapısı (D53/2) |
| O-4, O-5, O-6 (DL-1 kapsamı, MOD-0143 kuralı, mükerrer anahtarı) | — | **kapandı** — D26, D28 (`:1435`) | — |
| Kod alma izni | — | **kapandı** — D27/5, kaynak `da4abb20` (`:1436`) | — |
| O-9 RCS-001 ortak kod servisi (BL-565) — önek `APD` | Beste (D44) | **yok** — `services/**`'te kod servisi / `IRecordCodePort` yok; BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9473-9477@00d3f2b41`); önek **var** (`.antigravity/rules/record-coding-standard.md:64@00d3f2b41`). Backlog sahip alanı hâlâ "CT (Platform + MDM …)" — D44 ile uyumsuz (D51/5: Beste'ye bildirilecek) | **DL-1 kabul kapısı (D59/1)** — başlamayı bekletmez; DL-1 `IRecordCodeAllocator`'a karşı sahte uygulamayla yazılır, gerçek servis DL-1 kabulünden önce main'de (v0 sözleşmesi Y-8, 6 Kasım); üretimde port bağlı değilse 503 `AP_RECORD_CODE_UNAVAILABLE` |
| O-10 küsurat hanesi — MOD-0048 `currency-minor-units` seed + izin listesi | Beste (Y-4/3: başlangıç listesi kabul) | **yok** — `services/Diten.Platform/**`'te `currency-minor-units` 0 (grep); `BusinessReferenceData:ConsumableSets` (`services/Diten.Platform/src/Diten.Platform.API/appsettings.json:104@00d3f2b41`) listesinde yok | bekletir (Create; 503 `AP_CURRENCY_PRECISION_UNAVAILABLE`) |
| MOD-0143 `totalAmount` tabanı | — | **kapandı** (D26/1–3; `:1439-1440`). Bilgi: main'de `invoice-match.openapi.yaml` artık **1.1.1** (`docs/analysis/contracts/invoice-match.openapi.yaml:15@00d3f2b41`), sha256 `aa88132c85b87e59…7370` — pack'teki `41fe1f43…` (1.1.0) eskidi; pack "DL-1 başında yeniden `shasum`" diyor (§5 bulgu 2) | — (ölçüm DL-1 başında) |
| Tüketici onayı slotları — DL-1'in kullandıkları (AUTH, LE, SUPPLIER, AUDIT, DATA-CONTRACT) | K1 + slot sahipleri | AUTH ↔ Auth paket metni (açık), LE ↔ TRM-14 (açık), SUPPLIER ↔ 1.3.0 (açık), AUDIT ↔ MOD-0126 DL-1 (açık), DATA-CONTRACT ↔ D27/1 (kapandı) — pack "Tüketici onayı slotları" `:1486-1501` | bekletir |
| O-7, O-8 (K2), O-14; MOD-0023 AP şablonu (O-11); WORKFLOW slotu; §17 maddeleri (silmeme, okuma eşleme) | Ali (şablon) / — | O-7, O-8, O-14 kapandı (D28, D14/3); şablon **yok** (2026-10-28, Y-12: kod `FT-AP-INVOICE-APPROVAL` geldi; kuran kod main'de değil); MOD-0023 §17 rev 2 **onaylı** (`approvals.tsv:16`, `803fbaa5…` — mevcut MOD-0023 sha'sıyla aynı); pack checkbox'u D52 ile `[x]` (§5 bulgu 3 kapandı) | DL-2 |
| O-13; EVIDENCE slotu (MOD-0031 ölçümü) | Treasury CT | O-13 kapandı (D28); MOD-0031 ölçümü yapılmadı | DL-3 |
| Gateway rotası `/api/ap-invoices` | integration-agent | yok | DL-4 |

**DL-1'i ilk bekleten:** MOD-0126 DL-1'in main'de olmaması — o da kendi paket kapısını bekliyor. Zincirin en dış, tarihi olmayan
halkası: **MOD-0018 Treasury Auth ekinin MOD-0018 sahibince teyidi** (taslak #281 ile main'de; Treasury yazar, MOD-0018 sahibi
teyit eder — Y-15, Y-16; FU numarası K-1); MOD-0119'a özgü ayrıca **supplier 1.3.0 (MVP-2)**.
Auth kayıt kodu başlamayı değil DL-1 kabulünü bekletir (D53/2). **BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet: `shasum -a 256` (ilk 16 hane), satır sayısı `wc -l`,
ikisi de @da4abb20; 2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext archive da4abb20 services/DitenErpTreasuryService` ile
(salt okunur) **yeniden ölçüldü — tablolardaki 27 özet ve satır sayısının hepsi 2026-10-26 ölçümüyle aynı**. Pack atıfları
`@218acaf5` biçimindedir; pack'e göre bağlı AP dosyaları iki commit'te eşittir (pack "Kaynak ve kanıt", `:1463-1466@00d3f2b41`).
Karar dili: **al-uyarla** (doğrula, Recovery düzenine göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`), **yalnız referans**
(davranış okunur, kod alınmaz), **alma** ("madde N" = pack "Taşınmayacaklar"), **sonraki dilim**. Pack'in kod alma sınırı:
"varlık 1–3, durum korumaları, doğrulayıcının para birimi/satır/negatif tutar kuralları" (pack "Kaynak ve kanıt", `:1469-1470`).

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `ApInvoiceControlRecord` → `ApInvoice`, `ApInvoiceLine`; `ApDocumentType`, `ApDocumentDirection`, `ApInvoiceStatus` 6 / `ApApprovalStatus` 6 değer; durum korumaları (daraltarak); hata kodları (DL-1 alt kümesi, `AP_` önekiyle); detay / arama sorguları; doğrulayıcının dört kuralı; Mongo deposu (fatura + satır), bağlam, indeks kurulum deseni; denetleyicinin AP-01 / AP-02 / AP-09 davranışı | komut dosyası, DTO'lar, depo arayüzü, `AccountsPayableFoundationService` (davranış), kimlik bağlamı, kaynak testleri | `TenantEntity`, `IdempotencyKey` ve banka alanları, 26 `mod0119_` indeks adı ve tekil olmayan iş imzası, NoOp denetim / `FailClosed` / `FoundationOnly` güvenlik sınıfları, `ap.*` anahtarlar, gövde kiracısı, `[Authorize]`-only + `EnsureAccess`, `api/v{version}/mod0119/…` rotası, kaynak DI, `InMemory*` test depoları |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/AccountsPayable/AccountsPayableEntities.cs` | `a3eda3c6b01cdd83` · 394 | al-uyarla (yalnız `ApInvoiceControlRecord` :5-55 → `ApInvoice`, `ApInvoiceLine` :57-69) | `Domain/ApInvoices/` | `TenantEntity` yerine `EntityBase`, `DateTimeOffset`, `int Version`, Guid aktör; `Guid SupplierReferenceId` → `string SupplierId` (pack "Entity Fields" ApInvoice); `IdempotencyKey` :47 **alma**; banka alanları :50-53 **alma**; satırda `PurchaseOrderRef` / `GoodsReceiptRef` ve dış ERP alanları taşınmaz (pack "Owned Objects"). `ApException` :84-102 → DL-3; diğer 13 sınıf **alma** |
| `S/src/…Domain/AccountsPayable/AccountsPayableEnums.cs` | `2c68e99a11b54ea6` · 248 | al-uyarla (alt küme) | `Domain/ApInvoices/` | `ApDocumentType` :3-8, `ApDocumentDirection` :10-14 aynen; `ApInvoiceStatus` 11 değerden 6'sı, sayısal değerler korunur (Received 0, Validated 1, ApprovalPending 5, Approved 6, Rejected 7, OnHold 8 — pack "Durum makinesi"); `ApApprovalStatus` 8 değerden 6'sı; `ApHoldStatus` ve istisna enum'ları DL-3 |
| `S/src/…Domain/AccountsPayable/AccountsPayableStateGuards.cs` | `2f1c9699440bfa8d` · 38 | al-uyarla (daraltarak) | `ApInvoiceStateMachine` (tek domain tablosu, pack "Backend File Convention") | DL-1 geçişleri: Received → Validated, Validated → Received (Update; kaynakta yok, yeni); silme yalnız `SubmissionSequence = 0`. İstisna geçişleri :21-31 → DL-3 |
| `S/src/…Domain/Common/TenantEntity.cs` | `2ef3028110d99e9e` · 26 | alma | servis `EntityBase` (MOD-0126 DL-1 iskeleti) | `string` aktör, `DateTime` |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/AccountsPayable/Common/AccountsPayableErrorCodes.cs` | `399ff52e8dc1a21e` · 45 | al-uyarla (DL-1 alt kümesi, yeniden adlandırarak) | `Features/ApInvoice/` hata sözlüğü | 40 kod (pack "Hata sözlüğü" başlığıyla eşit); tek `AP_` öneki, `MOD0119_*` **alma**; taşınan / yeni / taşınmayan listesi pack "Hata sözlüğü"nde; `AP_CURRENCY_CODE_INVALID` 400 (listede olmayan para birimi) dahil. İleti resx anahtarı |
| `…/AccountsPayable/Commands/AccountsPayableCommands.cs` | `bb22e46c97187dbc` · 272 | yalnız referans (`RegisterApInvoiceControlRecordCommand` :8-25, `ProcessApInvoiceImportCommand` :27-44) | `Commands/CreateApInvoiceCommand.cs` + `Handlers/CommandHandlers/CreateApInvoiceHandler.cs` | 14 komut + handler tek dosyada **alma**; AP-09 dış ERP yükü yerine elle kayıt; ödeme / projeksiyon / kanıt / olay komutları v1 dışı (pack "Kaynak işlemleri") |
| `…/AccountsPayable/Contracts/AccountsPayableDtos.cs` | `215e61394e33f8c3` · 559 | yalnız referans (`ApInvoiceListItemDto` :197, `ApInvoiceDetailDto` :219, `ApInvoiceLineDto` :225, `ApInvoiceSearchCriteria` :236) | `ApInvoiceModels.cs` (tek dosya) | `ApInvoiceImportRequest` :16-52 **alma** — gövdede `TenantId` / `LegalEntityId` :17-18, istemciden kontrolsüz tutarlar :28-38 |
| `…/AccountsPayable/Contracts/AccountsPayablePersistenceContracts.cs` | `fdcc056b4b9f1e0a` · 210 | yalnız referans (`IAccountsPayableRepository` :59, fatura yöntemleri) | `Persistence/ApInvoices/` depo arayüzü | projeksiyon / mutabakat / içe aktarma ölçütleri alma |
| `…/AccountsPayable/Queries/AccountsPayableQueries.cs` | `b3b5a5eef4f7ff2e` · 165 | al-uyarla (`GetApInvoiceControlRecordDetailQuery` :8-25, `SearchApInvoiceControlRecordsQuery` :27-44) | `Queries/GetApInvoiceListQuery.cs`, `GetApInvoiceByIdQuery.cs`, `ApInvoiceListSort.cs` + `Handlers/QueryHandlers/` | sorgu + handler bölünür; DataTable sunucu tarafı; `X-Legal-Entity-Id` bağlamı; kapsam süzgeci `IFinanceTreasuryDataScopeSource` (MOD-0126 DL-1'in; AC-32 DL-1 uçları kısmı) |
| `…/AccountsPayable/Security/AccountsPayableSecurity.cs` | `0ac98fe26ab96f70` · 436 | alma | `[HasPermission]`, `ILegalEntityEligibilityPort` (ortak), SoD DL-2 | NoOp denetim :269-273; `FailClosed…LegalEntityAccessGuard` :284-294, `FoundationOnly…SodPolicy` :328-338; break-glass :314-317; `string` aktör :302, 324; `ap.*` anahtarlar :9-38 |
| `…/AccountsPayable/Services/AccountsPayableFoundationService.cs` | `a7beb5c9f606c40a` · 3333 | yalnız referans | Create / Update / Delete / BulkDelete / Validate handler'ları + domain (`handler-design`) | pack'in kod alma sınırı dışında; mükerrer kontrolü :465-481 **alma** (indeksle); `Success` dönen başarısızlık :321-329, :499-507 **alma**; önce-yaz-sonra-denetle :1785→1790, :1948→1953 **alma** (pack Audited Events `:1117-1118@00d3f2b41`) |
| `…/AccountsPayable/Validation/AccountsPayableFoundationValidator.cs` | `af636f4c2e6936df` · 196 | al-uyarla (yalnız `DueDate ≥ InvoiceDate` :35-40, negatif tutar :42-52, en az 1 satır :70-75, satır para birimi = başlık :77-85) | `Validators/CreateApInvoiceValidator.cs`, `UpdateApInvoiceValidator.cs`, `GetApInvoiceListValidator.cs` | maskeleme :55-68 **alma**; `TenantId` / `LegalEntityId` zorunluluğu :22-23 **alma**; kaynak `OrdinalIgnoreCase` (:78) — Recovery önce büyük harfe normalize eder (S-56); yeni kurallar pack "Validation Rules"ta |
| `…/Infrastructure/AccountsPayable/ClaimsAccountsPayableSecurityContextAccessor.cs` | `8e118ea5075734d6` · 134 | yalnız referans | Recovery `ICurrentUser` / JWT | — |
| `…/Application/DependencyInjection.cs` (AP satırları :41-49) | `983d696c8c14e2a2` · 55 | alma | yeni DI | NoOp sink :46, `FailClosed` LE guard :47, `FoundationOnly` SoD :48 |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/AccountsPayable/MongoAccountsPayableRepository.cs` | `b781355dba943d51` · 1086 | al-uyarla (DL-1 kısmı: fatura + satır yazma / okuma) | `Persistence/ApInvoices/` depolar | `RowVersion == expectedVersion` (:73-74) → `int Version` CAS, 409 `AP_INVOICE_VERSION_CONFLICT`; her işlem geri çağrısı `CentralAuditTransaction.EnlistAsync`; başlık + satırlar + makbuz + denetim aynı işlemde; soft delete `IsDeleted` + `DeletedAt` yeni |
| `…/Persistence/AccountsPayable/AccountsPayableMongoIndexes.cs` | `5bdf3339fe07c4c2` · 251 | al-uyarla (yalnız kurulum deseni) | indeks kataloğu | 26 kaynak adı **alınmaz** (`mod0119_`); tekil olmayan `ix_mod0119_invoice_business_signature` :70-79; `ux_mod0119_import_idempotency_key` :98-103 — aşağıdaki DB tekilliği tablosu |
| `…/Persistence/AccountsPayable/AccountsPayableMongoContext.cs` | `4f63d0a8fa3073a6` · 86 | al-uyarla | `Persistence/ApInvoices/` bağlamı | koleksiyonlar `ap_invoices`, `ap_invoice_lines`; kaynağın 17 `mod0119_ap_*` adı alma |
| `…/Persistence/AccountsPayable/AccountsPayableIndexHostedService.cs` | `514acce8893aa6cc` · 29 | yalnız referans | servisin indeks barındırılan hizmetine kayıt | pack: "İndeks kurulumu servisin indeks barındırılan hizmetiyle yapılır" (`:460@00d3f2b41`) — barındırılan hizmet MOD-0126 DL-1'in iskeletinde; ayrı hizmet açılmaz (MOD-0124 pack'iyle aynı çözüm). Taşıma / geri doldurma yok (AC-31) |
| — | — | **kullanılır, yazılmaz** | `Persistence/Common/CommandReceipts/**` | makbuz ve iki indeksi MOD-0126 DL-1'in (pack `:455-458`); AP yalnız `Operation = mod0119:{Komut}`, `TargetId` (Create / BulkDelete `null`) verir (pack `:426-427`) |

**Veritabanı tekilliği (DL-1 indeksleri, pack "Mongo indeksleri" `:437-453@00d3f2b41`; tekillik kod kontrolüyle değil indeksle):**

| Recovery indeksi | Anahtar | Tekil · filtre | Silinmiş satır | DL-1 testi |
|---|---|---|---|---|
| `ux_ap_invoice_code` | TenantId + InvoiceCode | **evet, kısmi filtre YOK** (`:441`, RCS-001 §6) | **dahil** — silinmiş faturanın kodu da dolu sayılır (AC-22) | Kodun RCS-001'den gelmesi veritabanı tekilliğini kaldırmaz; ikisi birlikte. Test: aynı kiracıda aynı kodla ikinci kayıt — silinmiş bir faturanın koduyla dahil — reddedilir. Sabotaj (öneri; pack bu indeks için yazmıyor): `IsDeleted = false` filtresi eklenince silinmiş-kod vakası kırmızı |
| `ux_ap_invoice_business_key` | TenantId + LegalEntityId + SupplierId + DocumentType + InvoiceNumberNormalized | **evet**, kısmi `IsDeleted = false` (`:442`; O-6 D28, `Rejected` de engeller) | **hariç** (AC-22) | AC-6: 409 `AP_INVOICE_DUPLICATE`; eşzamanlı iki Create → biri 201, diğeri 409 (gerçek replica set); indeks kaldırılınca kırmızı |
| `ux_ap_invoice_match_ref` | TenantId + LegalEntityId + MatchInvoiceRef | **evet**, kısmi `MatchInvoiceRef` var **ve** `IsDeleted = false` (`:443`) | hariç | AC-5: aynı MOD-0143 faturasına ikinci kayıt 409 `AP_MATCH_REFERENCE_ALREADY_USED` |
| `ix_ap_invoice_list`, `ix_ap_invoice_supplier`, `ix_ap_invoice_line_invoice` | `:448-449`, `:451` | hayır | — | — |
| `ix_ap_invoice_workflow_instance` | `:450` | hayır | — | dilim pack'te yazılı değil; alan DL-2'de dolar — öneri DL-2 |
| `ux_ap_start_intent_*`, `ux_ap_decision_open`, `ux_ap_exception_active`, `ix_ap_decision_invoice`, `ix_ap_exception_invoice` | `:444-447`, `:452-453` | — | — | **sonraki dilim** (DL-2 / DL-3) |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| — (kaynakta MOD-0140 / MOD-0143 bağdaştırıcısı yok) | — | **yeni** | `Infrastructure/Suppliers/`, `Infrastructure/InvoiceMatch/` (pack "Repo Scope" `:511-513`) | AP'ye özgü portlar; aşağıdaki tablo |
| `…/Infrastructure/DependencyInjection.cs` | `9acf42356e8f8a78` · 640 | alma | yeni DI | başka modül kayıtları, zaman aşımsız HTTP istemcileri; @218acaf5'ten farklı dosya (pack "Kaynak ve kanıt") |
| `…/Api/Controllers/AccountsPayableController.cs` | `4882e500a881744e` · 524 | al-uyarla (yalnız AP-01 :34, AP-02 :68, AP-09 :203 davranışı) | `Api/Controllers/ApInvoicesController.cs` | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]`; `[Authorize]`-only :17 ve `EnsureAccess` :215 **alma**; gövde kiracısı :221 **alma**; rota :18 **alma** → `/api/ap-invoices` |

**Referans veri / bağımlılık portları — DL-1 davranışı:**

| Port | Sahibi | Reddedilir | Erişilemez (503) | Pack dayanağı |
|---|---|---|---|---|
| `ILegalEntityEligibilityPort` (MOD-0220 + `ApprovalStatus`) | MOD-0126 DL-1 | Active + Approved değil → 422 `AP_LEGAL_ENTITY_NOT_ELIGIBLE`, kayıt değişmez; okuma etkilenmez | 503 `AP_LEGAL_ENTITY_UNAVAILABLE` | Dependencies `:566`, AC-2 |
| `ICurrencyReferencePort` (MOD-0048 liste + küsurat) | MOD-0126 DL-1 | listede değil → 400 `AP_CURRENCY_CODE_INVALID`; hane aşımı → 400 (yuvarlama yok) | 503 `AP_CURRENCY_PRECISION_UNAVAILABLE`; sabit tablo uydurulmaz | Dependencies `:569`, AC-21 |
| `IRecordCodeAllocator` (RCS-001, `APD`) | MOD-0126 DL-1 | istekte kod → 400 | 503 `AP_RECORD_CODE_UNAVAILABLE` | Dependencies `:573`, AC-1 |
| `IFinanceTreasuryDataScopeSource` (SEC-002) | MOD-0126 DL-1 | kapsam dışı şirket → liste boş, detay / yazma 404; `read-tenant-wide` yazmayı genişletmez | çözücü hatası → boş kapsam; `NoOp` kaydı yasak | Dependencies `:572`, AC-32, AC-33 |
| `ISupplierEligibilityPort` (MOD-0140 `POST /api/suppliers/validate`, **1.3.0**) | **MOD-0119 DL-1** (yeni) | ilişki yok / Active değil → 422 `AP_SUPPLIER_NOT_ELIGIBLE` | 503 `AP_SUPPLIER_UNAVAILABLE` | Dependencies `:567`, AC-4 |
| `IInvoiceMatchPort` (MOD-0143 `GET /api/invoice-match/invoices/{invoiceId}`) | **MOD-0119 DL-1** (yeni) | durum `{Matched, MatchedWithinTolerance}` dışı → 422 `AP_MATCH_OUTCOME_NOT_ELIGIBLE`; fark → 422 `AP_MATCH_REFERENCE_MISMATCH`; ikinci bağ → 409 | 503 `AP_MATCH_UNAVAILABLE` | Dependencies `:568`, AC-5, AC-40 |

Hepsi kullanıcı token'ıyla, yapılandırılmış zaman aşımıyla; adresler yapılandırmadan, eksikse servis başlamaz.

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `S/tests/…/AccountsPayable/AccountsPayableFoundationTests.cs` | `e4a744c40274b367` · 2822 | `RegisterInvoice_*` (kiracı bağlamı, izolasyon, idempotent tekrar, mükerrer) → AC-1, 3, 6, 15; `InMemoryAccountsPayableRepository` :2179 ve `InMemoryAccountsPayableControlAuditSink` :2147 **alma** — Recovery gerçek Mongo replica set ister |
| `…/AccountsPayableTransactionTests.cs` | `b998538815ef936c` · 891 | `Success_and_exact_replay_commit_once`, `Stale_writer_cannot_replace_committed_invoice`, `Reused_key_with_different_payload_is_conflict`, `Index_conflict_is_not_swallowed_by_hosted_startup` → AC-15, CAS 409, indeks kurulumu |
| `…/AccountsPayableControllerTests.cs` | `5b44763b69fb0f6f` · 722 | uç + izin senaryo listesi (AC-20) |
| `…/AccountsPayableVerifiedContextTests.cs` | `883d06cfe0f257fb` · 106 | başlık kiracısının talebi geçememesi, kapsam yoksa boş → AC-3, AC-32 |
| `…/AccountsPayableFailureOwnershipTests.cs` | `7450d64d78a555b7` · 111 | `Business_scope_filter_applies_before_limit` deseni AC-32 için referans; içe aktarma kısmı alma |
| `…/AccountsPayableProtectedPathTests.cs` | `c099492e91ea8938` · 78 | göç / şema dosyası yok → AC-31 benzeri repo taraması |
| `…/AccountsPayableControllerRouteTemplateTests.cs` | `626132efa1b9dd36` · 96 | alma (v1 dışı rotalar) |

Devralınmış kaynak kanıtı (`MOD-0119 TEST 251/251 @ da4abb20`) Recovery hazırlığının ölçüsü değildir; kaynakta olmayan testler
(mükerrer yarışı, satır toplamı tutarlılığı) DL-1'de zorunludur.

### 2.6 Yetki ve roller — pack değişikliklerinin DL-1'e etkisi (D46, D47, D49, D50)

Pack "Authorization Convention" (`MOD-0119-accounts-payable.md:1062-1108@00d3f2b41`) ve işleyen tek matris "D47 rol matrisi (CT04a;
D49 ile güncellendi — işleyen tek matris)" (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-206@00d3f2b41`;
D46 tablosu tarihçedir, uygulanmaz — D49/5).

- **DL-1 uçlarının anahtarları** (`:1075-1081`): `finance-treasury.ap-invoices.read` (T1), `.read-tenant-wide` (T3, yalnız açık
  atama, yalnız okuma), `.create`, `.update`, `.delete` (T1), `.bulk-delete` (T2), `.validate` (T3). Kaynak `ap.*` anahtarları alınmaz.
- **Test kişileri matristen** (`treasury-intake-owner-decisions-05.md:153`, `:172-174`): okuma → **AP görüntüleyici**; Create /
  Update / Delete / Validate → **Borç (AP) operatörü**; BulkDelete → yalnız **AP operasyon yöneticisi** (operatörde `bulk-delete`
  yok — D49/3). Bağımlı izinler: `mdm.legal-entities.read` + MOD-0140 / MOD-0143 okuma (operatör satırı). DL-1 hiçbir rol tohumu
  yazmaz; Auth paket metni MOD-0018 Treasury Auth ekindedir (`…/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16; paket kapısı, D53/1), Auth kayıt kodu DL-1 kabul kapısındadır (D53/2).
- **`read-tenant-wide`** hiçbir rol şablonunda yok — açık atama listesi (`treasury-intake-owner-decisions-05.md:139`); DL-1 manifesti
  onu hiçbir role varsayılan bağlamaz.
- **D50:** D50/1 "AP onay yöneticisi"ni kaldırdı (karar + mutabakat AP onaylayıcıda) — DL-2'yi etkiler; D50/3 istisna ayrılığı
  (`AP_EXCEPTION_RAISER_CANNOT_RESOLVE`, AC-42) DL-3'tedir. DL-1'e etkisi yok.
- **D47/4:** AP rollerine banka okuma izni verilmez; DL-1 banka alanı taşımaz (AC-19 mimari testi).
- **Denetim kategorisi:** `Finance = 18` (D35/3); MOD-0126 DL-1 ile main'e girer. Değer gelmeden kodda başka değer uydurulmaz
  (pack `:1124-1126`); `MasterData` ara çözümü MOD-0119 için seçilmedi.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

- `Features/ApInvoice/` Golden Reference Compact düzeni: `CreateApInvoiceCommand`, `UpdateApInvoiceCommand`,
  `DeleteApInvoiceCommand`, `BulkDeleteApInvoiceCommand`, `ValidateApInvoiceCommand` + handler'lar (suffix yok),
  `GetApInvoiceListQuery`, `GetApInvoiceByIdQuery`, `ApInvoiceListSort`, `ApInvoiceModels.cs`. Her yazma komutu
  `ICentralAuditDeclaration` taşır.
- Sunucu hesapları: `NetAmount` / `TaxAmount` / `GrossAmount` / `OpenAmount`, `DocumentDirection`, `InvoiceNumberNormalized`,
  `SupplierNameSnapshot`, `MatchOutcomeSnapshot` (pack "Entity Fields" ApInvoice). Kredi alanları varlıkta var; yazan komutlar DL-2.
- Alacak dekontu Validate ön kontrolü: `CreditedApInvoiceId` kuralı ve kapasite formülü → 422 `AP_CREDITED_INVOICE_INVALID` /
  `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`.
- `X-Legal-Entity-Id` bağlamı; gövde / sorguda `tenantId` / `legalEntityId` → 400.
- `Infrastructure/Suppliers/` ve `Infrastructure/InvoiceMatch/` bağdaştırıcıları, sahte istemcili sözleşme testleri; canlı kanıt
  dev ortamında (pack kapanış tablosu `:590-591`).
- Denetim olayları (yol `b`): `finance-treasury.ap-invoice.created` / `.updated` / `.deleted` / `.bulk-deleted` / `.validated`;
  `Category = Finance`. Defter MOD-0126 DL-1'indir, AP yeni iz satırı açmaz (AC-18).
- Modül manifestine MOD-0119 girdisi; izin anahtarları §2.6.
- `docs/analysis/contracts/ap-invoice.openapi.yaml` v1.0.0 (x-owner MOD-0119; D27/1 T32 sapmasıyla dondurulur; AC-28).
- Mimari testler: banka alanı adı yasağı (AC-19), `float` / `double` para alanı yok (AC-21), her aksiyonda `[HasPermission]`
  (AC-20), ProcurementService koleksiyonu / Mongo bağlantısı yok (AC-4), MOD-0126 / MOD-0269 / banka çağrısı ve veri taşıma yok
  (AC-31).

## 4. DL-1 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir; prompt'un içermesi gerekenlerin listesidir. Biçim örneği main'de
`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri; pack'te yok):
`execution/domains/finance-treasury/work-packs/prompts/WP-0119-DL1-v1.0.md`. Kuyrukta `WP-0119-DL1` satırı **yok** (CT-QUEUE'da
yalnız WP-0126-DL1 ve WP-0289-DL1; satır `ready-for-dev` turunda CT tarafından açılır — bu kayıt kuyruğa yazmaz).

**Başlık satırları**

| Alan | Değer (taslak) | `ready-for-dev` günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — `/add-module` adımı **yok** (iskelet MOD-0126 DL-1 WP'sinin ilk adımı, D51/3) | — |
| WP / Prompt | `WP-0119-DL1` · `TRM-WP-0119-DL1-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0119-dl1` — **KARAR D52/1** (D51/3 yalnız MOD-0126 ve MOD-0289 dallarını adlandırıyor); pack künyesindeki `feature/fin/mod-0119-accounts-payable` (`:13`) paket dalıdır, WP dalı değil | Expected HEAD = MOD-0126 DL-1 merge'ü **ve** MOD-0119 `ready-for-dev` onayı main'deyken alınan commit (S-53); worktree yolu |
| Risk | HIGH — §9.1 sinyalleri: kiracı, yetki, denetim, sözleşme, para | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kapsanan dilim | **yalnız DL-1** — başlama koşulu yalnız pack'in paket kapısı (§1 "bekletir" satırları); "— DL-n için" maddeleri bu WP'yi bekletmez (D51/1); SEC-002 kapsamı soran kod + AC-32'nin **DL-1 uçları kısmı** bu WP'nin kabul kapısında (D51/2); kabul kapısında ayrıca: Auth kayıt kodu main'de; testler gerçek izinle (D53/2); gerçek RCS-001 servisi (BL-565) main'de ve dev ortamında gerçek kod alındı (D59/1) | — |

**DL-1 Bitiş AC'leri** (`MOD-0119-accounts-payable.md:1638@00d3f2b41`): AC-1…6, **15 (Create / Update / Delete / BulkDelete /
Validate kısmı)**, **17 (tek aşamalı kısmı)**, 18…22, 28, 31, **32 (DL-1 uçları kısmı: liste, detay, Create / Update / Delete /
BulkDelete / Validate — SEC-002 kanıt testi, DL-1 kabul kapısı)**, **33 (Create / Update / Validate kısmı)**, **40 (Validate
kısmı)**. AC metinleri prompt'a pack'ten tırnakla alıntılanır. Kısmi AC'ler sonraki dilimlerde kapanır: AC-15 / 17 / 32 / 33 /
40 → DL-2; AC-17 sabotaj, AC-32 tam kapanış → DL-3.

**İzinli yollar** (pack "Repo Scope" DL-1 satırları, `:507-523`): `services/Diten.FinanceTreasuryService/src/…Application/Features/ApInvoice/**`,
`…Domain/ApInvoices/**`, `…Persistence/ApInvoices/**`, `…Infrastructure/{Suppliers,InvoiceMatch}/**`,
`…Api/Controllers/ApInvoicesController.cs`, modül manifestine MOD-0119 girdisi, `services/Diten.FinanceTreasuryService/tests/**`
altında MOD-0119 klasörü; `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` ve `AuditTrailStandardTests` sayıları
yalnız değişirse ve CT kararıyla (`:519-521`); `docs/analysis/contracts/ap-invoice.openapi.yaml`. `Infrastructure/{ApWorkflow,ApEvidence}/**`
DL-2 / DL-3'tür — DL-1 prompt'una girmez.

**Korunan yollar** (pack "Protected Paths", `:539-555`): `.antigravity/**`, `AGENTS.md`, `gateway/**` (ocelot yalnız
integration-agent; DL-4), `services/Diten.Platform/**`, `services/Diten.MdmService/**`, `services/Diten.ProcurementService/**`,
`services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`, diğer domain servisleri; servis ortak yollar
(`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`,
`Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`) ve MOD-0126'nın `PaymentBatch` yolları (XMC-001);
`frontend/**` (DL-4); `supplier.openapi.yaml`, `invoice-match.openapi.yaml` ve diğer üretici sözleşmeleri; `execution/**`;
kaynak depo ve handoff klasörü.

**Önce oku:** pack (`ready-for-dev` sürümü), MOD-0126 pack (ortak tanımlar — pack "Kaynak ve kanıt" sabitlemesi sha256 `58976767…b76e` (MOD-0126 KARAR D77; önceki `8127b3cd…e84c` CT-APR-02 Codex; önceki `674c73e9…79ea` D76; önceki `b5e749bd…732e` D75; `fb581b3b…cce5` D74/3; `1b8f172a…8ddf` D71); §5 bulgu 1 D52 ile kapandı),
AGENTS.md, `execution/domains/finance-treasury/domain-config.md`, `.antigravity/rules/` multi-tenancy, security-jwt,
permission-key-standard, git-safety, cross-module-change-consent, code-style (S-56), audit-trail-standard, data-scope-enforcement,
record-coding-standard, mongo-indexing, handler-design, module-self-registration-standard; karar kayıtları
`treasury-intake-owner-decisions-0{1..5}.md` (D1–D69; rol matrisi -05, D69 ile güncel), MOD-0018 Treasury Auth eki
(`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`, DL-1), MOD-0126 DL-1 WP raporu, bu hazırlık kaydı.

**Sözleşme yüzeyleri:** API `ap-invoice.openapi.yaml` v1.0.0 (AC-28); tüketilen `supplier.openapi.yaml` **1.3.0** ve
`invoice-match.openapi.yaml` (main'de 1.1.1 — sha DL-1 başında yeniden ölçülür) + MOD-0220 TRM-14 sözleşmesi; izin §2.6; sabotaj:
AC-6 (tekil indeks kaldırılınca), AC-17 (outbox / denetim yazımı zorla başarısız → iş verisi yok), AC-32 (kapsam süzgeci
kaldırılınca), AC-33 (`read-tenant-wide` yazmaya sızınca), `ux_ap_invoice_code` (`IsDeleted = false` eklenince).

**Gerekli testler:** birim (validatorlar, durum makinesi, idempotency parmak izi, RCS port sahtesiyle kod biçimi, alacak dekontu
kapasitesi); **gerçek Mongo replica set** entegrasyonu (kiracı / şirket 404, AC-32, soft delete, idempotent tekrar, CAS 409,
mükerrer yarışı AC-6, CentralAudit commit / rollback) — her test takımı kendi URI değişkenini okur, varsayılan porta düşmez,
replica set `enableTestCommands` ile; bellek içi sahte depo bu AC'leri kapatmaz; sahte MOD-0140 / MOD-0143 / MOD-0220 / MOD-0048
istemcileri (canlı kanıt sayılmaz); mimari (AC-4, 19, 20, 21, 31); sözleşme testi (AC-28); build `Diten.FinanceTreasuryService`
0 hata; yerel kapıda `scripts/` Python testleri.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş** olarak: hedef servis + proje yolu (`services/Diten.FinanceTreasuryService/src/…`);
  aggregate / entity + alanlar (`ApInvoice`, `ApInvoiceLine` — pack "Entity Fields"); komut / sorgu listesi (Create, Update,
  Delete, BulkDelete, Validate; liste, detay); validator kuralları (pack "Validation Rules" alan + MOD-0143 kuralları); izin
  anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 ile verilir (S-52); inceleme kaydı commit'ten önce `prompt_gate.py review-check` ile denenir (S-54); HIGH iş —
kabul kanıtı betiğini CT yazar, kırmızı tabanı kaydeder (S-83); Expected HEAD `ready-for-dev` günü doldurulur, pack onayı değişirse
prompt yeniden tabanlanır (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22.

**Keşif:** `python3 .antigravity/scripts/verify_module_id.py . --discover "accounts payable"` raporu, yol@commit + sha256 —
dağıtım günü koşulur.

**Kod alma izi:** §2'deki **al-uyarla** satırları; her biri WP raporunda `path@da4abb20 + sha256`.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynak düzeltmesi yok. Kaynak testler ve Recovery testleri
  **çalıştırılmadı**; Recovery'de yalnız dosya okuma ve `grep` (@00d3f2b41).
- Auth kaydının, BL-565'in, MOD-0048 seed'lerinin, `Finance = 18`'in ve supplier 1.3.0'ın tarihi sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi; `ready-for-dev` revizyonunda CT işi):**

1. **MOD-0126 ortak tanımları sürümsüz:** "Kaynak ve kanıt" `:1457` "MOD-0126 pack @bu commit sha256 ZIP manifestine bakın" —
   somut sha yok; MOD-0124 pack aynı yerde `9121d9f2…` ile sabitliyor. Çelişkide "o sürüm bağlayıcı" dendiği için prompt'tan önce
   sabitlenmeli.
2. **MOD-0143 sözleşme sürümü eskidi:** Dependencies `:568` ve kapanış tablosu `:591` "1.1.0 FROZEN", hash `41fe1f43…`; main'de
   `invoice-match.openapi.yaml` 1.1.1 (`:15`), sha `aa88132c…`. Kural etkilenmez (D26/3 bu değişikliği öngörmüştü), sabitleme
   güncellenmeli.
3. **MOD-0023 §17 onayı checkbox'u eski:** dilim kapısı `:1447` "[ ] MOD-0023 pack §17 (TRM-15) onaylandı" işaretsiz;
   `approvals.tsv:16` `803fbaa5…` onaylı (DL-2 maddesi; DL-1'i etkilemez).
4. **RCS-001 sahibi:** kapanış tablosu `:593` sahip "CT + `.antigravity` onayı" yazıyor; servis sahibi D44 ile Beste (MOD-0124
   pack aynı satırda D44'ü yazıyor).
5. **Küçük:** baş blokta D50 revizyonunun sonunda `status: draft` iki kez (`:60-61`).
