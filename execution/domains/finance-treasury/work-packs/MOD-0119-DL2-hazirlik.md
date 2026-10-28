---
record_id: MOD-0119-DL2-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0119-accounts-payable.md (status draft, sha256 51411aaa5b6c67db1ee27378f66e801577b05bb28859fd4505dad121cb2ff7de (pack sabitleme revizyonu, 2026-10-28 — yalnız MOD-0126 `58976767…b76e`'e yeniden sabitlendi (MOD-0126 KARAR D77 revizyonu — R4 kapalı partide muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, Release'de R11 tier-2 şartı tutmazsa 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`; önceki `8127b3cd…e84c`), bu paketin kuralları, AC'leri ve dilim tablosu değişmedi; önceki sürüm `2c2b6bac…7952` — pack CT-APR-02 Codex b91d61325 KOŞULLU bulguları revizyonu, 2026-10-28 — AC-4 ve AC-5 genişletildi: D72/D73 dalları komut ve dilim başına ölçülür (yazım / niyet / makbuz / denetim olayı yok), Bitiş listeleri "(… kısmı)" deseniyle — DL-1 AC-4 / AC-5 Create / Update / Validate kısmı, DL-2 Submit / Approve kısmı (AC-5 tam kapanış), DL-3 AC-4 ReleaseHold kısmı (tam kapanış); MOD-0126 `8127b3cd…e84c`'e yeniden sabitlendi (önceki `674c73e9…79ea`); yeni AC yok, yeni hata kodu yok; önceki sürüm `f5c21eb5…1048` — pack D76 revizyonu, 2026-10-28 — kullanıcının başlattığı Reconcile da niyetteki ilk aday listesini kullanır — yalnız KARAR D76/3 atfı, davranış değişmedi; MOD-0126 `674c73e9…79ea`'ye yeniden sabitlendi (önceki `b5e749bd…732e`); AC genişletilmedi, yeni AC yok, yeni hata kodu yok; önceki sürüm `72e580c6…45d7` — pack D75 revizyonu, 2026-10-28 — D73/1 çıkarımı teyit (404 `UNKNOWN_SUPPLIER` → 422 `AP_MATCH_REFERENCE_MISMATCH`, KARAR D75/3; AC-5 genişletildi); iç uç kimliği atıfları KARAR D74/1'e çekildi; Reconcile'ın yeniden başlatması niyetteki ilk aday listesini kullanır (D75/2 (b); AC-34 genişletildi); BANK-ACCOUNT-LOOKUP v1'de tüketilmiyor — D75/4 yalnız Follow-up'ta; MOD-0126 `b5e749bd…732e`'ye yeniden sabitlendi (önceki `fb581b3b…cce5`); yeni AC yok, yeni hata kodu yok; önceki sürüm `824c963e…6f68` — pack sabitleme revizyonu, 2026-10-28 — MOD-0126 `fb581b3b…cce5`'e yeniden sabitlendi (MOD-0126 D74/3 — muafiyet atayanı Muafiyet yetkilisi; önceki `1b8f172a…8ddf`), bu paketin kuralları ve AC'leri değişmedi; önceki sürüm `7bd02653…506c` — pack D73 revizyonu, 2026-10-28 — O-23 kapandı (KARAR D73/1): MOD-0143 faturasının `supplierId`'si `getSupplierEligibility` ile kanonik koda çevrilip saklı kanonik kodla ordinal karşılaştırılır; çevirme okunamazsa D72/2 (503 `AP_SUPPLIER_UNAVAILABLE`; 404 `UNKNOWN_LEGAL_ENTITY` → 422 `AP_LEGAL_ENTITY_UNKNOWN`); AC-5 genişletildi, yeni AC yok, yeni hata kodu yok; MOD-0126 sabitlemesi `1b8f172a…8ddf` değişmedi) — önceki sürüm `17c5b4bf…` (pack D72 revizyonu, 2026-10-28 — MOD-0140 Revizyon 5 hizalaması (`b3259e40…ce5c2`): 1.3.0 MOD-0140 DL-1'de FROZEN, uçlar MOD-0140 DL-3'te, DL-3'e kadar sahte istemci / Prism, canlı bağlantı yok; uygunluk `known` ∧ `eligibleForTransactions == true`; MOD-0140 403 / 503 / okunamayan → 503 `AP_SUPPLIER_UNAVAILABLE` (mevcut ad), 404 `UNKNOWN_LEGAL_ENTITY` → 422 `AP_LEGAL_ENTITY_UNKNOWN` (yeni kod); kanonik `SupplierId` + yeni `SupplierMasterId`, ad `getSupplier`; AP rollerine `procurement.suppliers.eligibility.read` / `.read` bağımlılığı; AC-4 genişletildi, yeni AC-44, yeni O-23; MOD-0126 sabitlemesi `1b8f172a…8ddf` değişmedi) — önceki sürüm `d6963438…` (pack D71 revizyonu, 2026-10-28 — D71 aday tabanı `ap-invoices.approve ∩ platform.workflow.tasks.approve` (anahtar başına ayrı DL-6 çağrısı), K-A4b kapandı, AC-10 genişletildi, MOD-0126 `1b8f172a…8ddf`'a yeniden sabitlendi; önceki sürüm `4f12cb30…` (pack D70 revizyonu, 2026-10-28 — D70/2 boş aday listesi 422 `AP_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (AC-10), D70/1 K-A4 kapandı (K-A4b açık), D70/3 Auth paket kapısı, D67 atıfları, MOD-0126 `ec4943aa…e374`'e yeniden sabitlendi; önceki sürüm `eea3d461…` (pack sahip denetimi 61466e504 KOŞULLU revizyonu, 2026-10-28 — MOD-0126 ortak tanımları yeniden sabitlendi; önceki sürüm `f7ee03aa…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `567f59ba…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `bd554f82…` (D58/D59 revizyonu); D57 sürümü `5a6f2bc2…`; D56 sürümü `126063f4…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `faa95829…` içindir)), dal chore/ct/treasury-intake-ct-05 @ec063fee9, "Revizyon (2026-10-27, D54 — MOD-0126 yeniden sabitleme)"; MOD-0126 ortak tanımları 05410477…e42e (D58/D59; önceki 5f69139c…8a5d D57, 1baf8728…0916 D56, 5101b1e8…c88a); CT-APR-02 UYGUN, 0 ENGEL — execution/approvals/reviews.tsv:96; CT-APR-01 satırı yok)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5; pack atıfları @218acaf5, AP dosyaları iki commit'te eşit — DL-1 kaydıyla aynı)
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
> - **MOD-0140 supplier 1.3.0** main'de değil (ölçüm: `supplier.openapi.yaml` `version: 1.2.0`) — DL-1 paket kapısı (DL-2 DL-1 üzerinden bekler).
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol oldu; MOD-0119'un 19 anahtarı ve rolleri değişmedi. MOD-0126 ortak tanımları
> pack "Kaynak ve kanıt"ta sha256 `58976767…b76e` ile sabitli (MOD-0126 KARAR D77 revizyonu — R4 kapalı partide muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, Release'de R11 tier-2 şartı tutmazsa 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`; önceki `8127b3cd…e84c` — MOD-0126 CT-APR-02 Codex b91d61325 KOŞULLU bulguları revizyonu — R4 uygulanma koşulu `IsTier2` ∧ sabitlenmiş `R4Enabled = true`, AC-41 kapanış cümlesi; önceki `674c73e9…79ea` — D76 revizyonu — yalnız D76/3 atfı: kullanıcının başlattığı Reconcile da ilk aday listesini kullanır; önceki `b5e749bd…732e` — D75 (iç uç kimliği D74/1, Reconcile'da ilk aday listesi D75/2 (b)); `fb581b3b…cce5` — D74/3 (muafiyet atayanı Muafiyet yetkilisi); `1b8f172a…8ddf` — D71; `ec4943aa…e374` — D70; daha önce `3a70398a…e925` — D68 + sahip denetimi d5b378e50).
>
> **Güncel durum (2026-10-27).** Pack `status: draft`; bu sürüme CT-APR-02 kaydı var (`reviews.tsv:96`, UYGUN, açık ENGEL 0 —
> dalda, main'de değil); CT-APR-01 yok. `services/Diten.FinanceTreasuryService/` yok — MOD-0126 DL-1 de MOD-0119 DL-1 de
> **başlamadı**. MOD-0119 DL-2 yalnız MOD-0119 DL-1 kabul edildikten sonra başlar. Satır atıfları `yol:satır@ec063fee9`;
> pack `ready-for-dev` revizyonunda değişirse o sürümde yeniden doğrulanır (S-53). `MOD-0119-DL1-hazirlik.md` ile birlikte okunur.

# MOD-0119 DL-2 hazırlığı — AP onay akışı ve onay ayrılığı (SoD)

Bu kayıt, MOD-0119 DL-2 WP prompt'unun DL-1 kabul edildiği gün yazılabilmesi için **kaynaktan ne alınacağını**, **neyin
uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi, kuyruğu ve onay kayıtlarını değiştirmez.

**DL-2 kapsamı** (pack "Teslim dilimleri", `MOD-0119-accounts-payable.md:1658-1662@ec063fee9`): Submit, Reconcile, Approve /
Reject / RequestInfo, SoD politika servisi, MOD-0023 bağdaştırıcısı (zaman aşımlı), trace sorgusu; alacak dekontu rezervasyonu
(Submit), alacaklama (Approve) ve bırakma (Reject / ret / iptal). DL-2 dört bölümü kapatır: Runtime Constraints, Validation Rules
(SoD R1–R8; R9 bekletme / istisna kapısı DL-3), Failure Path to Verify, Authorization Convention. **Muafiyet yoktur** (R7) —
MOD-0126'nın `waive-sod` / D54 kuralları AP'ye uygulanmaz.

## 1. Başlama koşulları (ölçüm: çalışma ağacı @ec063fee9, 2026-10-27)

Kaynak: DL-2 Önkoşul metni (`:1660-1661`), dilim kapıları "— DL-2 için" (`:1458-1465`), paket kapısı (`:1431-1457`).
"DL-2'ye etkisi": **bekletir** / **kabul kapısı** / **DL-3** — MOD-0126 DL-2 kaydıyla aynı anlam.

| Koşul | Sahibi | Bugün | DL-2'ye etkisi |
|---|---|---|---|
| **MOD-0119 DL-1 kabul edildi** — DL-1 WP'si main'de, Bitiş AC'leri kabul kaydında, Auth kayıt kodu main'de ve DL-1 testleri gerçek izinle (D53/2) | MOD-0119 DL-1 WP + CT | **yok** — servis yok; kuyrukta `WP-0119-DL1` PLANNED, `WP-0126-DL1`'e bağlı (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:21`) | bekletir |
| ↳ MOD-0126 DL-1 main'de (iskelet, servis ortak yapı taşları, CentralAudit) — MOD-0119 DL-1 üzerinden | MOD-0126 DL-1 WP | yok (`WP-0126-DL1` PLANNED) | bekletir (dolaylı) |
| ↳ Paket kapısı (DL-1 üzerinden): `ready-for-dev`, CT-APR-02/01, DCP-011, Auth paket metni (D53/1), TRM-14, **MOD-0140 supplier 1.3.0**, BL-565 — paket kapısı değil, DL-1 kabul kapısı (D59/1), küsurat seed'i, tüketici slotları AUTH / LE / SUPPLIER / AUDIT | Beste / Ali / Cihan (MVP-2) / CT | CT-APR-02 **var** (`reviews.tsv:96`, dalda); CT-APR-01 yok; DCP-011 `status: draft` (`execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md:7`); `LegalEntityLookupDto` `ApprovalStatus` taşımıyor (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106`); `supplier.openapi.yaml` main'de 1.2.0 (DL-1 kaydı §1; Y-4 "Cihan'a soruluyor"); BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9477`) | bekletir (dolaylı) |
| O-7 onay kademesi; O-14 alacak dekontu kapsamı; O-8 K2 | — | **kapandı** — D28 (v1 tek kademe; dekontta orijinal bağı zorunlu), D14/3 (Approve / Reject K2); checklist `[x]` (`:1459`) | — |
| O-11 / MOD-0023'te AP (satıcı faturası) iş akışı şablonu — kod, adım sayısı, zaman aşımı | Ali (MOD-0023 sahibi) | **2026-10-28 (Y-12): kod `FT-AP-INVOICE-APPROVAL` geldi** (tek aşama / tek onay adımı / atamasız; adaylar `CandidatePrincipalIds`); şablonu kuran kod main'de değil, WP / tarih yok. Ölçüm anında: **karar var, kod yok** — Y-6: satıcı faturası şablonu **tek adım** (`docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:66-68`); kod, WP numarası, tarih yok; pack O-11 hâlâ "TBD" (`:1619`); Platform'da Treasury şablonu / tohumu 0 (grep); checklist işaretsiz (`:1461`) | **bekletir** (dilim kapısı) |
| Tüketici onayı slotu WORKFLOW (MOD-0023; şablon O-11 + MOD-0023 sahibi) | K1 + Ali | **açık** — pack "Tüketici onayı slotları" WORKFLOW satırı (`:1516`); checklist işaretsiz (`:1465`) | **bekletir** (dilim kapısı) |
| MOD-0023 pack §17 onayı (silmeme D21/1, okuma eşleme D21/3 dahil) | — | **kapandı** — `approvals.tsv:16`, `803fbaa5…05fb2` = bugünkü MOD-0023 pack sha'sı; checklist `[x]` (`:1462-1464`) | — |
| §17 kodu MOD-0023 sahibinin WP'siyle main'de; AC-W1…W17 geçti (pack D56 revizyonu: MOD-0126 ile tek liste; önceki metin AC-W1 / W2 / W5…W14) | Ali yazar, başka biri merge eder (D32/1) | **yok** — `WorkflowInstanceDto` `IdempotencyKey` taşımıyor (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs:406-423`); `WorkflowInstanceHistoryEntryDto` `ApprovalTaskId` / `IdempotencyKey` taşımıyor (`:451-465`); `includeDeleted` 0; AC-W izi 0 | **kabul kapısı** — DL-2 başlar; kod yokken 404 / boş okumada anahtarlı yeniden yazma kapalı, niyet `Unknown` (AC-37, AC-38, AC-41 gerçek MOD-0023 kanıtı bu kodu ister) |
| MOD-0023 HTTP zaman aşımı `FinanceTreasury:Workflow:TimeoutSeconds` (MOD-0126 ile aynı ayar) | Ali (O-11) | öneri 15 sn (`:798`); değer gelmedi · **2026-10-28 (Y-12):** 15 sn tüketici ayarıdır (MOD-0126 ile aynı) | prompt değeri |
| MOD-0023 kapanış tablosu "Platform commit SHA'sı DL-2 başında yazılır" (`:609`) | Treasury CT | ölçülecek | prompt günü ölçüm |
| O-17 (Submit'te kanıt zorunlu) | — | **kapandı** — D28 (`:1625`); pack DL-3'e koyuyor: "DL-2'de Submit kanıtsız çalışır ve bu fark DL-2 kaydına yazılır" (`:1664-1665`) | DL-3 — fark aşağıda (§3) kayıtlıdır |
| O-13; EVIDENCE slotu (MOD-0031 ölçümü) | Treasury CT | O-13 kapandı (D28); MOD-0031 ölçülmedi (`:1466`) | DL-3 |
| **D66/1 — Auth aday sorgusu** (MOD-0018 Treasury Auth eki **DL-6**; D67/1) | Treasury yazar, MOD-0018 sahibi teyit eder | **2026-10-28:** DL-6 taslağı ve D66–D67 main'de (#281); ek `draft`, MOD-0018 sahibinin DL-6 teyidi yok; K-A1…K-A4 açık. Aday tabanı `finance-treasury.ap-invoices.approve`; R1–R3 ve şirket kapsamı sonrası boş liste → MOD-0023 400 → kesin başlatma reddi (pack AC-10, Failure Path) | paket metni **bekletir** (dilim kapısı); kodu **kabul kapısı** |
| MOD-0126 DL-2 (iş akışı deseni; ortak MOD-0023 bağdaştırıcısı ve aday kaynağı) | MOD-0126 DL-2 WP | **2026-10-28:** pack DL-2 Önkoşul'unda MOD-0126 DL-2 var — MOD-0023 bağdaştırıcısı (D57/5) ve `IFinanceTreasuryApproverCandidateSource` (D67/3) servis ortak, tek yazarı MOD-0126 DL-2 (§5 bulgu 4 kapandı). Ölçüm anında: pack MOD-0119 DL-2'yi MOD-0126 DL-2'ye bağlamıyor; niyet / karar durum adları MOD-0126 **pack**'indeki tek geçiş matrisinden (kod değil) | **bekletir** (D57/5, D67/3) |

**DL-2'yi ilk bekleten:** MOD-0119 DL-1'in kabul edilmemiş olması — zincir MOD-0126 DL-1 → paket kapısı (Auth paket metni,
supplier 1.3.0). DL-2'ye özgü en erken engel **MOD-0023 AP şablon kodu (Ali, O-11)** ve ona bağlı **WORKFLOW slotu**:
karar "tek adım" verildi, kod ve tarih yok (2026-10-28, Y-12: kod `FT-AP-INVOICE-APPROVAL` geldi; şablonu kuran kod ve
tarih yok). Ayrıca **MOD-0126 DL-2** (ortak MOD-0023 bağdaştırıcısı ve aday kaynağı — D57/5, D67/3) ve **MOD-0018 Treasury Auth eki
DL-6 paket metninin teyidi** (aday sorgusu — D66/1, D67/1); DL-6 kodu DL-2 kabul kapısı.
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-2)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). 2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext
archive da4abb20 services/DitenErpTreasuryService` ile (salt okunur) **yeniden ölçüldü — 17 AP kaynak + 7 test dosyasının özet ve
satır sayısı DL-1 ölçümüyle aynı**. **Kaynakta AP onay akışı yoktur:** `ApApprovalStatus` var ama karar kaydı, iş akışı
istemcisi ve HTTP işlemi yok (paket §C "not HTTP-exposed"; pack Owned Objects `:191-192`); SoD yalnız yer tutucu. Bu yüzden DL-2
kodu büyük ölçüde **yenidir**; davranış deseni MOD-0126 pack'inin Runtime Constraints'i ve tek geçiş matrisidir (aynı servis).
Karar dili DL-1 kaydıyla aynı; "madde N" = pack "Taşınmayacaklar" (`:1522-…`).

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `ApApprovalStatus` alt kümesi (DL-1'de alındı, DL-2'de kullanılır); onay geçişlerinin durum korumaları (`ApprovalPending` → `Approved` / `Rejected`); izin sabitlerinin anlam eşlemesi (yalnız iz için) | kaynak karar izinleri (`ap.invoice.approve` / `reject` / `request_evidence`), MOD-0126 kaynak iş akışı istemcisi ve SoD servisi (aynı serviste desen) | `FoundationOnlyAccountsPayableSodPolicy` yer tutucu, break-glass isteği ve hata kodları, `string` aktör, `ap.*` anahtarlar, `Delegated` / `InfoRequested` dışı kaynak onay durumları, ödeme önerisi adayı ve projeksiyon akışları |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/AccountsPayable/AccountsPayableEnums.cs` | `2c68e99a11b54ea6` · 248 | DL-1'de alındı; DL-2 kullanır | `Domain/ApInvoices/` | `ApApprovalStatus` :42-52 — DL-1 8 değerden 6'sını aldı; `Submitted`, `PendingDecision`, `Approved`, `Rejected` DL-2'de yazılır; `Delegated` / `InfoRequested` durumu pack "Durum makinesi"ne göre (RequestInfo görevi `ApprovalPending`'de tutar) |
| `S/src/…Domain/AccountsPayable/AccountsPayableStateGuards.cs` | `2f1c9699440bfa8d` · 38 | yalnız referans (:9-15 onay geçişleri) | `ApInvoiceStateMachine` (DL-1'de açıldı; DL-2 geçişleri eklenir) | `Validated → ApprovalPending` (Submit), `ApprovalPending → Approved / Rejected / Validated` (karar, kesin ret, iptal); `Matched`, `PaymentProposed`, `OnHold` geçişleri **alma** / DL-3 |
| `S/src/…Domain/AccountsPayable/AccountsPayableEntities.cs` | `a3eda3c6b01cdd83` · 394 | yalnız referans (`ApprovalStatus` :37) | — | `ApPolicyConfig` :355-367 **alma** (O-7 tek kademe) |
| — | — | **yeni** | `ApWorkflowStartIntent` (`ap_workflow_start_intents`), `ApApprovalDecision` (`ap_approval_decisions`) | pack Entity Fields `:410-433`; uzak anahtarlar `mod0119:{invoiceId:N}:start:{SubmissionSequence}`, `mod0119:{decisionId:N}:transition`; muafiyet alanı yok (R7) |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/AccountsPayable/Security/AccountsPayableSecurity.cs` | `0ac98fe26ab96f70` · 436 | **alma** | `[HasPermission]` + `Features/ApInvoice/` SoD politika servisi | `IAccountsPayableSodPolicy` :297-312 ve `FoundationOnlyAccountsPayableSodPolicy` :328-347 her çağrıda red — yer tutucu (madde 3); `AccountsPayableBreakGlassRequest` :314-317 ve `ValidateBreakGlassAsync` :348-360 **alma** (R7, AC-9 mimari testi); `string ActorUserId` :302, 324 **alma** (madde 10); `ap.invoice.approve` :36-38 yalnız iz |
| `…/AccountsPayable/Common/AccountsPayableErrorCodes.cs` | `399ff52e8dc1a21e` · 45 | yalnız referans | `Features/ApInvoice/` hata sözlüğü (DL-2 alt kümesi) | `AP_SOD_BLOCKED` :26, `AP_BREAK_GLASS_*` :27-28 **alma**; Recovery DL-2 kodları pack "Hata sözlüğü"nden: `AP_SOD_DENIED`, `AP_WORKFLOW_START_OUTCOME_UNKNOWN` / `_FAILED` / `_PENDING`, `AP_DECISION_OUTCOME_UNKNOWN` / `_PENDING`, `AP_CREDIT_NOTE_EXCEEDS_CAPACITY`, `AP_REQUEST_IN_PROGRESS` vb.; ileti resx anahtarı |
| `…/AccountsPayable/Services/AccountsPayableFoundationService.cs` | `a7beb5c9f606c40a` · 3333 | **alma** (DL-2 için) | — | iş akışı / karar yolu yok (`workflow` geçişi 1, yorum); önce-yaz-sonra-denetle (madde 1) DL-2 komutlarına da taşınmaz |
| `…/AccountsPayable/Commands/AccountsPayableCommands.cs`, `…/Contracts/AccountsPayableDtos.cs` | `bb22e46c97187dbc` · 272, `215e61394e33f8c3` · 559 | **alma** (DL-2 için) | `SubmitApInvoiceCommand`, `ApproveApInvoiceCommand`, `RejectApInvoiceCommand`, `RequestApInvoiceInfoCommand`, `ReconcileApInvoiceWorkflowCommand`; `ApDecisionRequest`, `ApWorkflowStateDto`, `ApDecisionDto`, `ApInvoiceTraceDto` | kaynakta karşılığı yok; pack "Recovery ekleri" (`:223-238`) |
| MOD-0126 kaynağı: `S/src/…Application/PaymentProposals/Services/PaymentWorkflowService.cs`, `…/PaymentSodPolicyService.cs`, `…/Infrastructure/PaymentProposals/WorkflowClientAdapters.cs` | `4195c53f81e4da40` · 1092, `0ae005b4f758c5c3` · 126, `79a422425b8add5b` · 350 | yalnız referans (desen) | AP SoD servisi, `IApWorkflowPort`, `Infrastructure/ApWorkflow/` | AP koduna **taşınmaz** (MOD-0126 kod alma izni D27/5 MOD-0126'nındır, AP davranışı farklı); uzak-önce-yerel, belirsizi ret sayma, 404'ü ret sayma, `FailClosed` varsayılanı, zaman aşımsız istemci (MOD-0126 madde 1, 5, 15) AP'de de yasaktır |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/AccountsPayable/MongoAccountsPayableRepository.cs` | `b781355dba943d51` · 1086 | yalnız referans (CAS deseni `RowVersion == expectedVersion` :73-74) | `Persistence/ApInvoices/` niyet ve karar depoları | tx1 (fatura `ApprovalPending`, niyet `Pending` / karar `Requested`, dekont rezervasyonu, makbuz `Pending`, deneme olayı) → MOD-0023 → tx2; orijinal faturanın `Version` CAS'ı dekont rezervasyonu / alacaklama için (`:801-…`); her geri çağrı `CentralAuditTransaction.EnlistAsync` |
| `…/Persistence/AccountsPayable/AccountsPayableMongoIndexes.cs` | `5bdf3339fe07c4c2` · 251 | **alma** (adlar) | indeks kataloğu | **yeni** `ux_ap_start_intent_remote_key`, `ux_ap_start_intent_open`, `ux_ap_decision_open`, `ix_ap_invoice_workflow_instance`, `ix_ap_decision_invoice` (pack `:458-460`, `:464`, `:466`); AC-30 sabotajı tekil kısmi indeksleri kaldırır |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| — (kaynakta AP iş akışı istemcisi yok) | — | **yeni** | `Infrastructure/ApWorkflow/` — `IApWorkflowPort` bağdaştırıcısı (pack Repo Scope `:525`) | zaman aşımlı `HttpClient`, kullanıcı token'ı; başlatma `POST /api/v1/workflow/instances`, geçiş `tasks/{taskId}/approve` / `reject` / `request-info`, Reconcile okumaları `by-objects`, `instances/{id}`, `history` (MOD-0023 §17.4a) |
| `…/Api/Controllers/AccountsPayableController.cs` | `4882e500a881744e` · 524 | **alma** (DL-2 için) | `ApInvoicesController.cs`: `POST /api/ap-invoices/{id}/submit`, `/workflow/reconcile`, `/approve`, `/reject`, `/request-info`; `GET /api/ap-invoices/{id}/trace` | kaynakta karar ucu yok; `[Authorize]`-only :17 ve `EnsureAccess` :215 **alma** (madde 4) |

### 2.5 Testler (yalnız referans)

| Kaynak | Özet · satır | DL-2'de karşılığı |
|---|---|---|
| `S/tests/…/AccountsPayable/AccountsPayableFoundationTests.cs` | `e4a744c40274b367` · 2822 | SoD / onay vakası yok — DL-2 testleri **yeni** |
| `…/AccountsPayableTransactionTests.cs` | `b998538815ef936c` · 891 | `Stale_writer_cannot_replace_committed_invoice` → kararlarda CAS 409; `Reused_key_with_different_payload_is_conflict` → AC-15 karar kısmı |
| `…/AccountsPayableControllerTests.cs` | `5b44763b69fb0f6f` · 722 | izin vakası listesi deseni (karar uçları yeni) |
| MOD-0126 kaynak testleri `PaymentWorkflowServiceTests.cs`, `GatewayWorkflowClientTests.cs` | `71f9c9dc0f495aab` · 1206, `adaca86b067568d4` · 300 | desen; "2xx dışı = ret" beklentisi **alma** (AC-41) |

Kaynakta olmayan testler DL-2'de zorunludur: başlatma zaman aşımı / boş 2xx (AC-11), 404 belirsiz (AC-41), üç yarış (AC-30),
dekont yarışı ve belirsizliği (AC-36), dekont formülünde çift sayım yok (AC-39), silinmiş uzak kayıt (AC-37), eski
`SubmissionSequence` (AC-38), Reconcile önce okur (AC-34).

### 2.6 Yetki ve roller — DL-2 (D46, D47, D49, D50)

Pack "Authorization Convention" (`:1076-1123`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-338`; D46 tablosu tarihçedir — D49/5).

- **DL-2 uçlarının anahtarları:** `finance-treasury.ap-invoices.submit` (T3), `.reconcile` (T3), `.approve` (T2), `.reject` (T2),
  `.request-info` (T3), `finance-treasury.ap-invoice-trace.read` (T1). Bağımlı: `platform.workflow.instances.start` (gönderen),
  `platform.workflow.tasks.approve` / `.reject` / `.request-info` (onaylayıcı), `mdm.legal-entities.read`, MOD-0140 / MOD-0143
  okuma (Approve uygunluğu yeniden okur — R8).
- **Test kişileri matristen** (`treasury-intake-owner-decisions-05.md:172-174`, `:241-245`): Submit → **Borç (AP) operatörü** ya
  da **AP operasyon yöneticisi**; Approve / Reject / RequestInfo → **Borç (AP) onaylayıcı** (D50/1: "AP onay yöneticisi" kalktı,
  karar ve mutabakat onaylayıcıda); Reconcile → AP operatörü, AP onaylayıcı, AP operasyon yöneticisi; trace → AP görüntüleyici
  (+ miras), Hazine konsolide görüntüleyici. **AP operasyon yöneticisi approve / reject / request-info taşımaz** (D49/3) —
  negatif test: 403. Ödeme / AR / nakit rolleri AP anahtarı taşımaz (D49/1).
- **SoD rol değil çalışma anıdır:** bir kişide AP operatörü + AP onaylayıcı olabilir; R1 (oluşturan), R2 (gönderen — onay **ve**
  ret), R3 (gönderme anındaki son güncelleyen) her kararda MOD-0023 çağrısından önce denetlenir; aktör Guid (R5). Test: aynı
  kişiye iki rol verilip kendi faturasını onaylaması 403 `AP_SOD_DENIED`.
- **Muafiyet yok (R7):** AP'de `waive-sod` benzeri anahtar yok; D47/1, D49/4 ve D54 AP'ye uygulanmaz. Mimari test: kodda muafiyet /
  break-glass komutu ya da alanı yok (AC-9).
- **`read-tenant-wide`** açık atama; okuma kapsamı yazmayı genişletmez — AC-33 Submit / karar / Reconcile kısmı.
- **D50/3** (istisna ayrılığı) DL-3'tedir. **D47/4:** AP rollerine banka okuması verilmez.
- **Denetim:** Approve / Reject K2 (D14/3); kategori `Finance = 18` (MOD-0126 DL-1 ile main'e girer).

## 3. Kaynakta olmayan, DL-2'de yeni yazılacaklar

- `ApWorkflowStartIntent`, `ApApprovalDecision` + indeksler; iki aşamalı Submit ve kararlar (pack Runtime Constraints
  `:684-…`, MOD-0126 tek geçiş matrisi); belirsiz → 202 `AP_*_OUTCOME_UNKNOWN`, kesin ret → 502, 404 → `Unknown` (AC-41).
- `ReconcileApInvoiceWorkflowCommand` — önce okur; eşleme alanı (uzak anahtar, görev kimliği) yoksa kapatmaz; yeniden yazma yalnız
  asıl aktöre ve gap 6/P1-a koşulları kapalıyken.
- Gönderme çözümleyicisi (aday tabanı Auth aday sorgusu — **KARAR D66/1**, MOD-0018 Treasury Auth eki DL-6, servis ortak
  `IFinanceTreasuryApproverCandidateSource`, anahtar `finance-treasury.ap-invoices.approve`; R1–R3 aday dışı — önceki "MOD-0023
  şablon ataması" metni şablon atamasız olduğu için geçersiz, Y-12) ve SoD politika servisi (tek kademe — O-7; eşik yok).
- Alacak dekontu: Submit rezervasyonu (`CreditReservedAmount`), Approve kesin alacaklama (`CreditedAmount`, `CreditStatus`), Reject /
  kesin ret / iptal bırakma — orijinal faturanın `Version` CAS'ıyla (`:801-…`; AC-35, AC-36, AC-39).
- Approve anında uygunluğun yeniden okunması: LE (TRM-14), tedarikçi ilişkisi (MOD-0140 1.3.0), PO'lu faturada MOD-0143 (R8;
  AC-40 Submit / Approve kısmı).
- `GetApInvoiceTraceQuery` (`GET /api/ap-invoices/{id}/trace`).
- `ap-invoice.openapi.yaml` DL-2 uçlarıyla (sürüm artışı CT belirler).
- **Bilinçli fark (pack `:1664-1665` gereği yazılır):** O-17 (D28) "her belge türünde en az 1 kanıt" kararı verilmiş olsa da kanıt
  bağlama (MOD-0031) DL-3'tedir; **DL-2'de Submit kanıtsız çalışır**. DL-3 Submit'e 422 `AP_EVIDENCE_REQUIRED` kuralını ekler.
  Aynı şekilde R9 (engelleyici istisna / `OnHold` varken Approve 409) DL-2'de uygulanamaz — bu kayıtlar DL-3'te oluşur.

## 4. DL-2 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir. Hedef yol (öneri): `execution/domains/finance-treasury/work-packs/prompts/WP-0119-DL2-v1.0.md`.
Kuyrukta `WP-0119-DL2` satırı **yok** — bu kayıt kuyruğa yazmaz.

**Başlık satırları**

| Alan | Değer (taslak) | DL-2 başladığı gün doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — `/add-module` yok | — |
| WP / Prompt | `WP-0119-DL2` · `TRM-WP-0119-DL2-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0119-dl2` — **öneri — D51/3 / D52/1 deseni, sahip teyidi gerekir** | Expected HEAD = MOD-0119 DL-1 merge'ü ve kabul kaydı main'deyken alınan commit (S-53); worktree yolu |
| Risk | HIGH — kiracı, yetki, SoD, uzak çağrı belirsizliği, borç tutarı (dekont kapasitesi) | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı; MOD-0126 ortak tanımları sha ile sabit | sha |
| Kapsanan dilim | **yalnız DL-2** — başlama koşulu: DL-1 kabul edildi + "— DL-2 için" dilim kapıları (MOD-0023 AP şablonu O-11, WORKFLOW slotu; O-7 / O-8 / O-14 kapandı); §17 kodu ve AC-W **kabul kapısı** | — |

**DL-2 Bitiş AC'leri** (`:1661-1662`): AC-41, AC-7…15, **40 (Submit / Approve kısmı — AC-40 tam kapanışı)**, **17 (iki aşamalı)**,
30, **32 (iz ve karar uçları kısmı: trace, Submit, Approve / Reject / RequestInfo, Reconcile)**, **33 (Submit, kararlar, Reconcile
kısmı)**, 34, 35, 36, 37, 38, 39. AC metinleri pack'ten tırnakla alıntılanır. Kısmi AC'lerin kalanı: AC-17 sabotaj, AC-32 tam
kapanış, AC-33 Hold / istisna / kanıt → DL-3. AC-15'in DL-3 komutları kısmı için §5 bulgu 1.

**İzinli yollar** (pack Repo Scope `:516-528`): `services/Diten.FinanceTreasuryService/src/…Application/Features/ApInvoice/**`,
`…Domain/ApInvoices/**`, `…Persistence/ApInvoices/**`, `…Infrastructure/ApWorkflow/**`, `…Api/Controllers/ApInvoicesController.cs`,
`services/Diten.FinanceTreasuryService/tests/**` altında MOD-0119 klasörü; `docs/analysis/contracts/ap-invoice.openapi.yaml`.
`Infrastructure/ApEvidence/**` DL-3'tür.

**Korunan yollar:** `.antigravity/**`, `AGENTS.md`, `gateway/**`, `services/Diten.Platform/**` (MOD-0023 §17 kodu — Ali),
`services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.ProcurementService/**`, `services/Diten.Building.Blocks/**`;
servis ortak yollar (`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`,
`Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`) ve MOD-0126 / 0120 / 0124 feature yolları (MOD-0126'nın
`Infrastructure/Workflow/**` dahil — XMC-001); `frontend/**`; `supplier.openapi.yaml`, `invoice-match.openapi.yaml`; `execution/**`;
kaynak depo.

**Önce oku:** pack (`ready-for-dev` sürümü), MOD-0126 pack (ortak tanımlar, sha `3a70398a…e925` — MOD-0119 "Kaynak ve kanıt" sabitlemesi, D70; önceki `3a70398a…e925` D68 + sahip denetimi d5b378e50 P2; önceki `5e1ea803…25a7` sahip denetimi 61466e504, tier-2 aday kesişimi, `f496790f…0b0c` ready-for-dev 1. tur eki D66, `81b9b505…161d` ready-for-dev 1. tur (Y-12, D62–D65), `05410477…e42e` D58/D59, `5f69139c…8a5d` D57, `1baf8728…` D56 — ya da o günkü sabit), MOD-0119 DL-1 WP
raporu ve kabul kaydı, MOD-0023 pack §17 (`803fbaa5…`), AGENTS.md, domain-config, `.antigravity/rules/` multi-tenancy, security-jwt,
permission-key-standard, git-safety, cross-module-change-consent, code-style (S-56), audit-trail-standard, data-scope-enforcement,
mongo-indexing, handler-design; karar kayıtları `treasury-intake-owner-decisions-0{1..5}.md` (D14, D21, D22, D25, D26, D28, D32,
D47, D49, D50, D57/5, D62/2, D66, D67), MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`, DL-6),
MOD-0023 sahip kararları (`docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, W-2, W-6), bu hazırlık kaydı.

**Sözleşme yüzeyleri:** API `ap-invoice.openapi.yaml` (DL-2 uçları); tüketilen MOD-0023 `WorkflowModels.cs` @ DL-2 başındaki
Platform commit'i + AP şablon kodu; `supplier.openapi.yaml` 1.3.0, `invoice-match.openapi.yaml` 1.1.1 (sha DL-2 başında yeniden
ölçülür), MOD-0220 TRM-14 sözleşmesi. İzin §2.6.

**Gerekli testler:** birim (R1–R8, Guid aktör, dekont kapasite formülü: Validate / Submit / Approve ayrı); **gerçek Mongo replica
set** entegrasyonu (tx1 / tx2, makbuz, AC-30 üç yarış + tekil kısmi indeks sabotajı, AC-36 dekont yarışı, CentralAudit commit /
rollback, AC-17 kurtarma testi) — her takım kendi URI değişkenini okur, varsayılan porta düşmez, replica set `enableTestCommands` ile;
**sahte MOD-0023 sunucusu** (zaman aşımı, bağlantı kopması, boş 2xx, 5xx, 403, 404, anahtar başına tek örnek / geçiş, silinmiş
örnek, eski örnek); sahte MOD-0220 / MOD-0140 / MOD-0143; mimari (her aksiyonda `[HasPermission]`, muafiyet / break-glass yok);
sözleşme testi; build 0 hata; yerel kapıda `scripts/` Python testleri. Sahte istemcili test canlı kanıt sayılmaz.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş**: hedef servis + proje yolu; aggregate / entity + alanlar (`ApWorkflowStartIntent`,
  `ApApprovalDecision`; `ApInvoice` onay ve dekont alanları `SubmittedBy`, `UpdatedByBeforeSubmit`, `SubmissionSequence`,
  `WorkflowInstanceId`, `CreditReservedAmount`, `CreditedAmount`, `CreditStatus`); komut / sorgu listesi (Submit, Reconcile,
  Approve, Reject, RequestInfo; trace); validator kuralları (pack Validation Rules SoD + dekont satırı); izin anahtarları §2.6;
  `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 (S-52); `prompt_gate.py review-check` (S-54); HIGH iş — kabul kanıtı betiğini CT yazar (S-83); Expected HEAD
DL-2 başladığı gün doldurulur (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22.

**Kod alma izi:** DL-2'de **al-uyarla** satırı yoktur (kaynakta AP onay akışı yok); yalnız referans okunan dosyalar WP raporunda
`path@da4abb20 + sha256` ile anılır.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive` → kazı dizini); kaynak ve Recovery testleri **çalıştırılmadı**; Recovery'de
  yalnız dosya okuma ve `grep` (@ec063fee9).
- MOD-0023 AP şablon kodunun, §17 kodunun, supplier 1.3.0'ın ve DL-1'in tarihi sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi):**

1. **AC-15 DL-2'de kapanıyor ama DL-3 yazma komutları da onu ister.** DL-2 Bitiş "AC-7…15" (`:1662`) AC-15'i kısmisiz sayıyor; AC-15
   "her yazma komutu" der (`:1256`) ve Hold / ReleaseHold / istisna / LinkEvidence DL-3 yazma komutlarıdır. MOD-0126 aynı durumu
   "16 (kararlar)" + DL-3 "AC-16 (kalan)" ile çözüyor. Öneri: DL-2 "15 (Submit, kararlar, Reconcile kısmı)", DL-3 "15 (kalan)". **Kapandı** (pack D56 revizyonu, bulgu 1).
2. **AC-W listesi tutarsız.** Dilim kapısı "AC-W5…W14 DL-2 uygulama kabul kapısında" (`:1462`), DL-2 Önkoşul "AC-W1/W2/W5…W14"
   (`:1661`); MOD-0126 "AC-W1…W17" diyor (D32'nin AC-W15 tek silme işareti, W16 `includeDeleted` kapsamı, W17 tüketici uyumu).
   Aynı MOD-0023 koduna bağlı dört paket için tek liste gerekir. **Kapandı** (pack D56 revizyonu: AC-W1…W17).
3. **O-11 Y-6 ile güncellenmedi.** O-11 "çok adımlı onay adımları … TBD" (`:1619`); Y-6 satıcı faturası şablonunu **tek adım**
   olarak bildirdi (kaynak yanıtları tablosu bunu "kodlar gelince ready-for-dev revizyonunda" diye bekletiyor). AC-14'ün "çok adımlı
   akışta son adım olmayan onay" dalı (`:1253-1255`) tek adımlı şablonda test edilemez — prompt'ta "şablon tek adımsa bu dal sahte
   MOD-0023 ile koşulur" diye yazılmalı ya da pack revizyonunda daraltılmalı. **Kapandı (KARAR D66/4, D62/2):** çok adımlı dal
   AC-14'ten kalktı; AC-14 "v1'de ara onay durumu yok" testine yeniden yazıldı (numara aynı).
4. **MOD-0023 bağdaştırıcısının sahipliği paketler arasında çelişik** (ayrıntı MOD-0126 DL-2 kaydı §5 bulgu 4): MOD-0119 ayrı
   `Infrastructure/ApWorkflow/**`, MOD-0120 "MOD-0126'nın bağdaştırıcısıyla ortak", MOD-0124 `Infrastructure/Workflow/**` içinde kendi
   bağdaştırıcısı. MOD-0119 için çelişki yok, ama ortak karar verilirse bu DL-2'nin yolları değişir. **Kapandı — KARAR D57/5**
   (servis ortak, tek yazarı MOD-0126 DL-2; `ApWorkflow` yalnız AP portu; DL-2 Önkoşul'a MOD-0126 DL-2).
5. **DL-3 Önkoşul'da kararı verilmiş O-17 koşullu yazılmış:** "O-17 'evet' ise bu dilimde eklenir" (`:1664`) — O-17 D28 ile "evet"
   (`:1625`). Anlam değişmez; metin karar diline çekilmeli. **Kapandı** (pack D56 revizyonu, bulgu 5).
