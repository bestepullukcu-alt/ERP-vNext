---
record_id: MOD-0126-DL2-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (status draft, sha256 ec4943aa8f2eb995ef502ee81f710b6895f4b5e1b1b5d3b698f9415e7c0fe374 (pack D70 revizyonu, 2026-10-28 — D70/1 K-A4 kapandı (K-A4b açık), D70/3 Auth paket kapısı, D67 atıfları (ApproverCandidates D67/3), D64 / D66 main'de; önceki sürüm `3a70398a…` (pack D68 revizyonu + sahip denetimi d5b378e50 P2, 2026-10-28 — boş aday listesi yerel 422, MOD-0023 çağrılmaz; önceki sürüm `14b1bcc5…` (D68 revizyonu); daha önce `5e1ea803…` (sahip denetimi 61466e504 KOŞULLU revizyonu — tier-2 aday kesişimi); daha önce `f496790f…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `81b9b505…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `05410477…` (D58/D59 revizyonu); D57 sürümü `5f69139c…`; D56 sürümü `1baf8728…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `5101b1e8…` içindir), dal chore/ct/treasury-intake-ct-05 @ec063fee9, "Revizyon (2026-10-27, D54 — Codex F-1)"; CT-APR-02 UYGUN, 0 ENGEL — execution/approvals/reviews.tsv:93; CT-APR-01 satırı yok)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5; DL-1 kaydıyla aynı commit)
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve bu pack sürümü dahil; D69 main'de değil ve MOD-0126'ya dokunmaz).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU numarası açık — K-1). Taslak main'de ama `draft` ve teyitsiz — pack'teki "Auth paket metni
> main'de" maddesi işaretsiz, paket kapısı açık (D53/1). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-1**'i; kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **MOD-0023 şablon kodu geldi:** `FT-PAYMENT-BATCH-APPROVAL` (Y-12; W-1, W-6 — `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, #277 ile
> main'de) — tek aşama, tek onay adımı, atamasız; adaylar `CandidatePrincipalIds`. Şablonu kuran MOD-0023 kodu main'de **değil**,
> WP numarası ve tarih yok (DL-2 dilim kapısı). `PartiallyApproved` v1'de kapalı (D62/2, AC-46); "son adım değil" gelirse parti
> `PendingApproval`'da kalır (D66/2).
> - **Muafiyet:** Auth yapısı ekin **DL-5**'i (D64; D63/3 geçersiz). DL-5 main'e girene kadar muafiyet yolu 403
> `PAYMENT_SOD_WAIVER_FORBIDDEN` ile kapalı (D54); DL-2 bu kapalı yolu kanıtlayan testle kabul edilir, muafiyet DL-5 gelince
> ayrıca kabul edilir (D66/3).
> - **Onaylayıcı aday tabanı** = ekin **DL-6** aday sorgusu (D66/1; D67/1 — DL-6 paket metni DL-2 önkoşulu, kodu DL-2 kabul
> kapısı; DL-1 DL-6'yı beklemez). Tier-2 partide `finance-treasury.payment-batches.approve ∩ .tier2-approve` (sahip denetimi
> 61466e504); muafiyet adayı aynı sorgunun `waive-sod` + parti biçimi (D67/2). R1–R4, şirket kapsamı ve muafiyet adayından
> sonra liste boşsa Submit 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER`, MOD-0023 çağrılmaz, niyet yazılmaz (D68).
> - Aday kaynağı (`IFinanceTreasuryApproverCandidateSource`) ve MOD-0023 bağdaştırıcısı servis ortak yapı taşıdır; tek yazarı
> MOD-0126 DL-2 (D67/3, D57/5).
> - **SoD ayarı:** FinanceTreasuryService'te kiracı başına tek `PaymentSodSettings` kaydı, DL-2 yazar (D56/4, AC-44); izinler
> `finance-treasury.payment-sod-settings.read` / `.update` yalnız Hazine yöneticisinde (D57/4).
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **MOD-0048 seed'leri** (`currency-minor-units`, `payment-method`): Treasury CT ekler, Beste onaylar (D59/2, D60); ölçüm: main'de yok
> (`services/Diten.Platform/**`'te 0). `currency-minor-units` değer sorusu Beste'de (D60/3). `Finance = 18` main'de yok
> (`AuditEnums.cs` son değer `ESignature = 17`).
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol oldu; MOD-0126'nın 22 anahtarı ve rolleri değişmedi.
>
> **Güncel durum (2026-10-27).** Pack `status: draft`; bu sürüme CT-APR-02 kaydı var (`reviews.tsv:93`, UYGUN, açık ENGEL 0) ama
> satır `chore/ct/treasury-intake-ct-05` dalındadır, main'de değildir; CT-APR-01 yok. `services/Diten.FinanceTreasuryService/`
> yok — **DL-1 başlamadı**. DL-2 yalnız DL-1 kabul edildikten sonra başlar. Satır atıfları `yol:satır@ec063fee9` biçimindedir;
> pack `ready-for-dev` revizyonunda değişirse o sürümde yeniden doğrulanır (S-53). Bu kayıt
> `MOD-0126-DL1-hazirlik.md` (MOD-0126-DL1-HAZIRLIK-01) ile birlikte okunur; DL-1 kararları burada yinelenmez.
>
> **D66 eki (2026-10-28; karar dosyası D66 — yazıldığı anda main'de değildi; ölçüm 2026-10-28: PR #281 ile main'de).** Pack D66 revizyonuna göre bu kayıtta güncellenen satırlar: §1 O-10
> satırı ve yeni D66/1 satırı (aday tabanı Auth aday sorgusu — MOD-0018 Treasury Auth eki **DL-6**; paket metni DL-2 dilim kapısı,
> kodu DL-2 kabul kapısı), §1 D54 satırı ve §4 "Kapsanan dilim" (**KARAR D66/3**: DL-5 kodu DL-2 kabul kapısı değil — muafiyet yolu
> 403 ile kapalıyken DL-2 kabul edilir, muafiyet DL-5 gelince ayrı kabul), §5 bulgu 1 ve 3 (kapandı). Bu kayıttaki diğer ölçümler
> ve satır atıfları değişmedi (önceki sürüm için).
>
> **Sahip denetimi 61466e504 eki (2026-10-28, KOŞULLU — P2).** Pack "Revizyon (2026-10-28, sahip denetimi 61466e504 KOŞULLU —
> tier-2 aday kesişimi)": tier-2 partide aday tabanı `finance-treasury.payment-batches.approve ∩ .tier2-approve` — çözümleyici
> DL-6 sorgusunu iki anahtar için çağırıp kesişimi alır, sonra R1–R4 (pack Validation Rules → "Aday tabanı"; AC-11 genişledi).
> DL-2 WP prompt'u §1 D66/1 satırını bu kuralla okur; matris rolleri (Ödeme onaylayıcı (2. kademe) = onaylayıcı + `tier2-approve`)
> zaten iki izni birlikte taşır. Bu kayıttaki diğer ölçümler değişmedi.

# MOD-0126 DL-2 hazırlığı — iş akışı ve onay ayrılığı (SoD)

Bu kayıt, MOD-0126 DL-2 WP prompt'unun DL-1 kabul edildiği gün yazılabilmesi için **kaynaktan ne alınacağını**, **neyin
uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi, kuyruğu ve onay kayıtlarını değiştirmez.

**DL-2 kapsamı** (pack "Teslim dilimleri", `MOD-0126-payments-approvals.md:1613-1617@ec063fee9`): Submit, Reconcile,
Approve / Reject / RequestEvidence / ApproveWithSodWaiver, SoD politika servisi ve tek eşik yapılandırması, MOD-0023
bağdaştırıcısı (zaman aşımlı). DL-2 dört bölümü kapatır: Runtime Constraints, Validation Rules (SoD kuralları R1–R10; R11
DL-3), Failure Path to Verify, Authorization Convention (muafiyet kuralları D47/1, D49/4, D54 dahil).

## 1. Başlama koşulları (ölçüm: çalışma ağacı @ec063fee9, 2026-10-27)

Kaynak: pack "Teslim dilimleri" DL-2 Önkoşul metni (`:1614-1617`), "Ready-for-dev Checklist" dilim kapıları "— DL-2 için"
(`:1460-1465`) ve paket kapısı (`:1437-1458`). "DL-2'ye etkisi": **bekletir** = DL-2 kodu bu kapanmadan başlamaz; **kabul
kapısı** = DL-2 başlar, ama "Bitiş" AC'leri bu kapanmadan kabul edilmez (pack: "AC-W1…W17 geçişi dilimin kabul kapısıdır,
önkoşulu değil"); **DL-3** = sonraki dilimin kapısı.

| Koşul | Sahibi | Bugün | DL-2'ye etkisi |
|---|---|---|---|
| **DL-1 kabul edildi** — DL-1 WP'si main'de, DL-1 Bitiş AC'leri kabul kaydında geçti (`scripts/acceptance_record.py`), Auth kayıt kodu main'de ve DL-1 testleri gerçek izinle koştu (D53/2) | MOD-0126 DL-1 WP + CT | **yok** — `services/Diten.FinanceTreasuryService/` yok; kuyrukta `WP-0126-DL1` PLANNED (`docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv:19`); DL-1'in paket kapısı açık (aşağıdaki iki satır ve DL-1 kaydı §1) | bekletir |
| ↳ Paket kapısı (DL-1 üzerinden): `ready-for-dev`, CT-APR-02/01, DCP-011 `approved`, Auth paket metni (D53/1), TRM-14, BL-565 — paket kapısı değil, DL-1 kabul kapısı (D59/1), `Finance = 18`, O-11 / O-13 seed'leri, `read-tenant-wide` Auth tanımı | Beste / Ali / CT | CT-APR-02 bu sürüme **var** (`reviews.tsv:93`, UYGUN, 0 ENGEL — dalda); CT-APR-01 **yok** (`approvals.tsv`'de MOD-0126 satırı yok); DCP-011 `status: draft` (`execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md:7`); `LegalEntityLookupDto` hâlâ `ApprovalStatus` taşımıyor (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106`); `AuditEnums.cs`'te `Finance` 0; `appsettings.json`'da `currency-minor-units` / `payment-method` 0; BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9477`) | bekletir (dolaylı) |
| O-4 SoD eşiği modeli | — | **kapandı** — D11/1 (kiracı ayarı, para birimli; ayar yoksa her onay tier-2); checklist `[x]` (`:1461`) | — |
| O-7 onay yüzeyi ve başlatma kimliği; O-10 onaylayıcı adayları | — | **kapandı** — D29/1, D42, Ali teyidi Y-1 (v1: kullanıcı token'ıyla genel MOD-0023 uçları; aday = MOD-0023 şablon ataması + R1–R4 dışlaması) — **2026-10-28 KARAR D66/1 ile aday kısmı değişti:** şablon atamasız (Y-12); aday tabanı Auth aday sorgusu (MOD-0018 Treasury Auth eki DL-6), servis R1–R4'ü uygular, `CandidatePrincipalIds` | — |
| **D66/1 — Auth aday sorgusu** ("bu izne sahip kullanıcılar"; MOD-0018 Treasury Auth eki **DL-6**, `waive-sod` + parti biçimi muafiyet adayları için) | Treasury yazar, MOD-0018 sahibi teyit eder | **2026-10-28:** DL-6 taslağı ve D66–D68 main'de (#281); ek `draft`, MOD-0018 sahibinin DL-6 teyidi yok; K-A1…K-A4 açık. Tier-2: `approve ∩ tier2-approve`; boş liste 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER`, MOD-0023 çağrılmaz (D68); aday kaynağı `IFinanceTreasuryApproverCandidateSource` servis ortak, tek yazarı bu DL-2 (D67/3) | paket metni **bekletir** (dilim kapısı); kodu **kabul kapısı** |
| O-8 / MOD-0023'te ödeme partisi iş akışı şablonu (kod, adım sayısı, zaman aşımı) | Ali (MOD-0023 sahibi) | **2026-10-28 (Y-12): kod `FT-PAYMENT-BATCH-APPROVAL` geldi** (tek aşama / tek onay adımı / atamasız; adaylar `CandidatePrincipalIds`); şablonu kuran kod main'de değil, WP / tarih yok. Ölçüm anında: **karar var, kod yok** — Y-6: ödeme partisi şablonu **tek adım** (`docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:66-68`); şablon kodu, WP numarası ve tarih gelmedi; Platform'da Treasury şablonu / tohumu yok (`services/Diten.Platform/**`'te `payment-batch` / `PAYMENT_BATCH` / `finance-treasury` 0 — yalnız ilgisiz iki test fikstürü); checklist işaretsiz (`:1462`) | **bekletir** (dilim kapısı) |
| MOD-0023 pack §17 rev 2 onayı | — | **kapandı** — `approvals.tsv:16`, sha256 `803fbaa5…05fb2` = bugünkü MOD-0023 pack sha'sı (yeniden ölçüldü) | — |
| §17 kodu (silmeme D21/1, okuma eşleme D21/3, tek silme işareti D32/2, `includeDeleted` D32/6) MOD-0023 sahibinin WP'siyle main'de; AC-W1…W17 geçti | Ali yazar, başka biri merge eder (D32/1) | **yok** — `WorkflowInstanceDto` `IdempotencyKey` / `isDeleted` taşımıyor (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs:406-423`); `WorkflowInstanceHistoryEntryDto` `ApprovalTaskId` / `IdempotencyKey` taşımıyor (`:451-465`); Platform'da `includeDeleted` 0; `services/**` ve `tests/**`'te AC-W izi 0; kuyrukta TRM-15 yalnız pack satırı, kod WP'si yok (`CT-QUEUE.tsv:16`) | **kabul kapısı** — DL-2 başlar; kod gelene kadar 404 / boş okumada anahtarlı yeniden yazma kapalı, niyet `Unknown` (gap 6/P1-a, P1-b); AC-34 (d), AC-35, AC-36 ve AC-40'ın gerçek MOD-0023'e karşı kanıtı bu kodu ister |
| **D54 — `waive-sod` açık atamasının alanları Auth'ta** (bitiş, başlangıç, atamayı onaylayanın aktör kimliği, gerekçe kodu, **hedef parti kimliği**) ve okuma yolu | Treasury yazar, MOD-0018 sahibi teyit eder — MOD-0018 Treasury Auth eki DL-5 (**KARAR D64**; önceki: Ali) | **2026-10-28 (D64):** taslak DL-5 `…/MOD-0018-FU-TBD-treasury-permission-onboarding.md` main'de (#281, `draft`, teyitsiz); DL-5 kodu yok; main'e girene kadar muafiyet yolu 403 (D54). Ölçüm anında: **yok** — Auth'ta atama yalnız rol → izin (`RolePermission`: `AssignedAt`, `AssignedBy`, `GrantSource`, `SourceModuleCode` — `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/RolePermission.cs:29-38`) ve kullanıcı → rol (`UserRole.cs:18-21`); süre, başlangıç, onaylayan, gerekçe ve hedef nesne alanı yok; Treasury MOD-0018 paketi yok; `ExplicitGrantOnlyPermissions.cs`'te `finance-treasury` 0 | pack'te dilim kapısı olarak **yazılı değil** (§5 bulgu 1). Fiilen: DL-2 başlar ve muafiyetli onay kapalı-başarısız yazılır (403, D54); ama AC-9'un "Waived kaydedilir" dalı ve AC-43'ün geçerli atama dalı Auth alanları gelmeden kabul edilemez → **kabul kapısı** · **2026-10-28 KARAR D66/3 (D56/3'ün kabul kapısı kuralının yerine):** kabul kapısı **değil** — muafiyet yolu 403 ile kapalıyken (pack AC-43 (h)) DL-2 kabul edilir; AC-9 `Waived` dalı, AC-42 aday dalları ve AC-43 geçerli atama dalları DL-5 main'e girince **muafiyet açılış kabulünde** |
| SoD ayarının kayıt yeri (kiracı başına `PolicyVersion`, `Tier2Threshold.Amount`, `.CurrencyCode`) | sahip / CT | **kapandı — KARAR D56/4 + D57/4:** FinanceTreasuryService'te kiracı başına tek `PaymentSodSettings` kaydı (tek eşik + R4; R1–R3 kapatılamaz), DL-2 yazar (AC-44); izinler `finance-treasury.payment-sod-settings.read` / `.update` (Hazine yöneticisi). Ölçüm anında: **açık** — pack "Ayarın kayıt yeri (MOD-0126 içi kiracı ayarı ya da Platform ayarları) ve ayar ekranı Follow-up'tadır" (`:815-816`); DL-2 Yollar "tek eşik yapılandırması" ve AC-7 "ayar deposu erişilemezse 503" ister | — (kapandı; §5 bulgu 2) |
| MOD-0023 HTTP zaman aşımı `FinanceTreasury:Workflow:TimeoutSeconds` | Ali (O-8) | öneri 15 sn (`:810-811`); değer gelmedi · **2026-10-28 (Y-12):** 15 sn tüketici ayarıdır (MOD-0023 şablonunun parçası değil) | prompt değeri; yoksa öneri yazılır, servis değer yoksa açılmaz |
| MOD-0023 kapanış tablosu "Platform commit SHA'sı DL-2 başında yazılır" (`:589`) | Treasury CT | ölçülecek | prompt günü ölçüm |
| Auth kayıt kodu (`finance-treasury.payment-batches.submit` / `.reconcile` / `.approve` / `.tier2-approve` / `.reject` / `.request-evidence` / `.waive-sod`) | Treasury yazar (MOD-0018 eki DL-1 kodu), MOD-0018 sahibi teyit eder | yok (DL-1 kabul kapısı, D53/2) | DL-1 üzerinden; DL-2 testleri gerçek izinle |
| MOD-0031 dedupe ölçümü; MOD-0269 BANK-DISPATCH | Treasury CT / — | ölçülmedi / dondu (D27/7) | DL-3 |

**DL-2'yi ilk bekleten:** DL-1'in kabul edilmemiş olması (servis yok) — o da DL-1'in paket kapısını bekliyor (Auth paket metni
en dış, tarihi olmayan halka; DL-1 kaydı §1). DL-2'ye özgü en erken engel **MOD-0023 ödeme partisi şablon kodu
(Ali, O-8)**: karar "tek adım" verildi, kod ve tarih yok (2026-10-28, Y-12: kod `FT-PAYMENT-BATCH-APPROVAL` geldi;
şablonu kuran kod ve tarih yok). DL-2'ye özgü ikinci önkoşul **MOD-0018 Treasury Auth eki DL-6 paket metninin MOD-0018 sahibince teyidi** (aday sorgusu; K-A1…K-A4
açık — D66/1, D67/1). Kabul tarafında: DL-6 kodu ve §17 kodu (AC-W1…W17). **D54 atama alanları (DL-5, D64) DL-2 kabul kapısı
değildir (D66/3):** muafiyet yolu 403 ile kapalıyken DL-2 kabul edilir; muafiyet DL-5 gelince ayrıca kabul edilir.
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-2)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet `shasum -a 256` (ilk 16 hane) · satır `wc -l`,
ikisi de @da4abb20; 2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext archive da4abb20 services/DitenErpTreasuryService`
ile (salt okunur, kazı dizinine) **ölçüldü**. DL-1 kaydıyla ortak sekiz dosya (`568fe3cf…`, `72e767bd…`, `ce3cc9e4…`,
`1e659de1…`, `f1dc4aa9…`, `2dd642c8…`, `2bcb2622…`, `205cd802…`) DL-1 ölçümüyle **aynı**; DL-2'ye özgü dosyalar ilk kez
ölçüldü. Karar dili DL-1 kaydıyla aynı (**al-uyarla**, **yalnız referans**, **alma** — "madde N" = pack "Taşınmayacaklar",
`:1495-1524`).

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `PaymentApprovalDecision` (+ `State`), `PaymentException`; `PaymentApprovalDecisionType` (Escalate hariç), `PaymentSodResult` alt kümesi; `PaymentSodPolicyService` kuralları (R1–R4, muafiyetin üç dalı); DL-2 hata kodları; deponun karar / gönderim / istisna yazma kısmı; karar ve iş akışı indekslerinin deseni | iş akışı servisi (`PaymentWorkflowService` — akış sırası hariç), MOD-0023 istemci sözleşmeleri ve HTTP şekli (`GatewayWorkflowClient`), onaylayıcı çözümleyici (R1–R3 aday dışı), Submit / karar komutları, istek parmak izi, kaynak testleri | uzak-önce-yerel sıra, belirsiz sonucu ret saymak, 404'ü ret saymak, `FailClosed` / `Fixture` istemci varsayılanları, zaman aşımsız istemci, `50_000m` ve ikinci eşik, AdminDirect (PP-05) + `PassedByAdminProxy`, Escalate, çift istisna satırı, `string` aktör, `mod0126.*` izinler ve rotalar, kaynak onaylayıcı dizini istemcileri, kararda istemci anahtarı tekil indeksi, kararda kanıt değerlendirme |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/PaymentProposals/PaymentProposalEntities.cs` | `568fe3cf50abdcb3` · 163 | al-uyarla (yalnız `PaymentApprovalDecision` :68-97, `PaymentException` :99-114) | `Domain/PaymentBatches/` | `TenantEntity` → `EntityBase`; `DecidedBy` `string` → Guid (madde 11); kararda istemci `IdempotencyKey` :82 **alma** — istemci anahtarı yalnız makbuzda, kararda deterministik `RemoteIdempotencyKey`; yeni `State` (`Requested` / `Recorded` / `Refused` / `Unknown` …, pack "Entity Fields" PaymentApprovalDecision `:383-394`); `DecisionPath` :91 ve `AdminDirectProxy` **alma** (madde 7); `SodPolicyVersion` kod varsayılanı `"mod0126.sod.v1"` :96 **alma** (sürüm kiracı ayarından, D11/1); kanıt alanları :73-76 DL-3 |
| — | — | **yeni** | `Domain/PaymentBatches/PaymentWorkflowStartIntent` (`payment_workflow_start_intents`) | kaynakta yok (level-1 koşul 2); uzak anahtar `mod0126:{batchId:N}:start:{SubmissionSequence}` (pack `:422`); durum matrisi pack `:271` |
| `S/src/…Domain/PaymentProposals/PaymentProposalEnums.cs` | `72e767bd46390e85` · 132 | al-uyarla (alt küme) | `Domain/PaymentBatches/` | `PaymentApprovalDecisionType` :23-29 — `Escalate = 4` **alma** (D22/1); `PaymentSodResult` :31-43 — `Passed`, `FailedSelfApproval`, `FailedSubmitterApproval`, `FailedRoleConflict`, `FailedAmountTier`, `Waived` korunur; `FailedUpdaterApproval` **yeni** (AC-5); `PassedByAdminProxy` (madde 7), `NotEvaluatedByWorkflowProxy`, `NotApplicableForManualTreasury`, `DeferredExternalSource` **alma**; `PaymentDecisionPath` :45-49 **alma**; istisna enum'ları :51-76 al-uyarla |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/PaymentProposals/Services/PaymentSodPolicyService.cs` | `0ae005b4f758c5c3` · 126 | al-uyarla (kurallar) | `Features/PaymentBatch/` SoD politika servisi + `IPaymentSodPolicyProvider` | R1 :38-44 (pack Validation Rules `:962`); aktör karşılaştırması Guid (AC-10; madde 11); muafiyet yalnız R4'ü kaldırır (R9, D12/1); D49/4 + D54 çalışma anı denetimleri **yeni** (Auth alanları okunamıyorsa 403) |
| `…/Contracts/SodPolicyContracts.cs` | `9e8be912e7ad2fb3` · 34 | yalnız referans | tek eşik tipi (AC-7 mimari testi) | `Tier2AmountThreshold = 50_000m` :10 **alma** (madde 6); `string ApproverUserId` :19 **alma** |
| `…/Contracts/ApproverPolicyContracts.cs` | `082d664f0e55928d` · 72 | yalnız referans | gönderme çözümleyicisi | ikinci, para birimsiz eşik :13 **alma** (madde 6); aday kümesi deseni AC-42 `IsWaiverCandidate` için okunur; `WaiverApprovers` **alma** (madde 16) |
| `…/Contracts/WorkflowClientContracts.cs` | `5de8251e0ba88e9a` · 51 | yalnız referans | `IPaymentWorkflowPort` (Application) | başlatma ve geçiş istek / sonuç şekli; Recovery portu Reconcile için üç okumayı da taşır (`by-objects`, `instances/{id}`, `history` — MOD-0023 §17.4a) |
| `…/Commands/SubmitWorkflowCommand.cs` | `730c1d7ea52e67c6` · 44 | yalnız referans | `SubmitPaymentBatchCommand`, `ApprovePaymentBatchCommand`, `RejectPaymentBatchCommand`, `RequestPaymentBatchEvidenceCommand`, `ApprovePaymentBatchWithSodWaiverCommand`, `ReconcilePaymentBatchWorkflowCommand` + handler'lar | komut + handler aynı dosyada **alma**; tek `RecordWorkflowDecisionCommand` :27-44 karar türü başına bölünür (pack PP-09) |
| `…/Commands/AdminDirectDecisionCommand.cs` | `d0615f4e39044ef5` · 25 | **alma** | — | PP-05, D11/2, madde 7 |
| `…/Services/PaymentWorkflowService.cs` | `4195c53f81e4da40` · 1092 | yalnız referans (davranış) | Submit / karar / Reconcile handler'ları | uzak-önce-yerel: başlatma :158 → kayıt :235, geçiş :474 → kayıt :609 **alma** (madde 5); SoD reddinde çift istisna :390-412 **alma** (madde 10); Escalate :621-725 **alma** (D22/1); kararda kanıt bağlama / değerlendirme :859-948 **alma** (O-16, DL-3); istek parmak izi :726-789 makbuz parmak izi için referans |
| `…/Common/PaymentProposalErrorCodes.cs` | `ce3cc9e42e035587` · 57 | al-uyarla (DL-2 alt kümesi) | `Features/PaymentBatch/` hata sözlüğü | DL-2 kodları pack "Hata sözlüğü" (`:458-…`) ile: `PAYMENT_SOD_DENIED`, `PAYMENT_TIER2_APPROVER_REQUIRED`, `PAYMENT_SOD_WAIVER_FORBIDDEN` / `_REASON_REQUIRED` / `_REQUIRED` / `_NOT_ALLOWED`, `PAYMENT_SOD_POLICY_UNAVAILABLE`, `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` / `_FAILED` / `_PENDING`, `PAYMENT_DECISION_OUTCOME_UNKNOWN` / `_PENDING`, `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`; ileti resx anahtarı |
| `…/Contracts/PaymentProposalContracts.cs` | `1e659de1d2867029` · 197 | al-uyarla (karar istekleri) | `PaymentBatchModels.cs` → `PaymentDecisionRequest`, `PaymentSodWaiverRequest`, `PaymentWorkflowStateDto`, `PaymentDecisionDto` | `TenantId` / `LegalEntityId` yok |
| `…/Contracts/PermissionContracts.cs` | `afa6e8be76b3343e` · 29 | **alma** | `[HasPermission]` | madde 8, 13 |
| `…/Services/PaymentLifecycleService.cs` | `de4bde70ae299112` · 1133 | alma (AdminDirect :13-15, :271-276, :436-437) / DL-3 | — | madde 7; kalan yaşam döngüsü DL-3 |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/PaymentProposals/MongoPaymentBatchRepository.cs` | `2dd642c8c777e253` · 718 | al-uyarla (DL-2 kısmı: `SaveDecisionAsync` :233-311, `SaveWorkflowSubmissionAsync` :505-555, `FindByWorkflowInstanceIdAsync` :556, `SaveExceptionAsync` :439) | `Persistence/PaymentBatches/` | tx1 / tx2 ayrımı: tx1 niyet (`Pending` / `Requested`) + parti + makbuz `Pending` + deneme olayı, tx2 sonuç; her geri çağrı `CentralAuditTransaction.EnlistAsync`; `FindDecisionByIdempotencyKeyAsync` :225 **alma** (makbuz araması yerine geçer); outbox yöntemleri :564-… **alma** (madde 12) |
| `…/PaymentProposalsMongoIndexes.cs` | `2bcb2622103f0e39` · 198 | al-uyarla (desen) | indeks kataloğu | `ix_mod0126_payment_decision_batch` :119 → `ix_payment_decision_batch`; `ix_mod0126_payment_batch_workflow_instance` :84 → `ix_payment_batch_workflow_instance`; `ux_mod0126_payment_decision_idempotency` :127 **alma** (makbuz indeksi yerine geçer, pack `:442`); **yeni** `ux_payment_start_intent_remote_key`, `ux_payment_start_intent_open`, `ux_payment_decision_open` (pack `:443-445`) — AC-31 sabotajı bu üçünü kaldırır |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Infrastructure/PaymentProposals/WorkflowClientAdapters.cs` | `79a422425b8add5b` · 350 | yalnız referans (`GatewayWorkflowClient` :134-… HTTP şekli) | `Infrastructure/Workflow/` MOD-0023 bağdaştırıcısı | `FailClosedWorkflowClient` :13 ve `FixtureWorkflowClient` :39 **alma** (madde 1; sahte MOD-0023 test projesinde yazılır); 2xx dışı her yanıtı "refused" saymak :173-178 **alma** (404 kesin ret değil — AC-40, D21/7); her istisnayı başarısız saymak :189-195, :262-269 **alma** (zaman aşımı / bağlantı kopması / boş 2xx → `Unknown`, AC-12, AC-15) |
| `…/WorkflowClientOptions.cs` | `256e72672335aa86` · 19 | **alma** | `FinanceTreasury:Workflow:*` yapılandırması | `Mode = "FailClosed"` varsayılanı :7 (madde 1); zaman aşımı alanı yok (madde 15); yollar `/api/workflow/instances/start` :9 — Recovery ucu `POST /api/v1/workflow/instances` (pack Dependencies `:563`); yapılandırılmış `BearerToken` :14 **alma** (kullanıcı token'ı iletilir, O-7) |
| `…/ConfiguredApproverResolver.cs` | `a16eee8d9d912ea6` · 203 | yalnız referans (R1–R3 aday dışı bırakma) | gönderme çözümleyicisi | yapılandırma listesinden onaylayıcı **alma** (O-10: aday MOD-0023 şablon atamasından) |
| `…/GatewayAuthoritativeApproverProvider.cs`, `…/FailClosedAuthoritativeApproverProvider.cs`, `…/ApproverDirectoryClientOptions.cs` | `81a31889964d7dd7` · 180, `1248b3e192178364` · 31, `6c59a7b3601d4ee6` · 22 | **alma** | — | O-10: "ayrı dizin istemcisi taşınmaz" |
| `…/ClaimsPaymentPermissionEvaluator.cs` | `6e4f69ae36314747` · 67 | **alma** | `[HasPermission]` | madde 8 |
| `…/Api/Controllers/PaymentBatchesController.cs` | `205cd802bd8801db` · 346 | yalnız referans (`submit-workflow` :207, `workflow-decision` :232) | `PaymentBatchesController.cs`: `POST /api/payment-batches/{id}/submit`, `/workflow/reconcile`, `/approve`, `/reject`, `/request-evidence`, `/approve-with-sod-waiver` | `[Authorize]`-only :17 **alma**; `admin-direct-decision` :137 **alma**; her aksiyonda `[HasPermission]`; `If-Match` + `Idempotency-Key` |

### 2.5 Testler (yalnız referans — davranış listesi; Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-2'de karşılığı |
|---|---|---|
| `S/tests/…/PaymentProposals/PaymentWorkflowServiceTests.cs` | `71f9c9dc0f495aab` · 1206 | SoD dalları (AC-4, 5, 6, 9), karar türleri; uzak-önce-yerel varsayan vakalar **alma** |
| `…/PaymentWorkflowSubmissionRepositoryTests.cs` | `ac41b6a51c5e53dc` · 283 | gönderim kaydı (AC-11) |
| `…/PaymentWorkflowSubmissionTransactionTests.cs` | `99cc4e60d84aae54` · 190 | tx1 commit (AC-11, AC-13) |
| `…/PaymentDecisionRepositoryTests.cs` | `80358f50e2477ad3` · 211 | karar niyeti (AC-15) |
| `…/PaymentDecisionTransactionTests.cs` | `68e9f70c20d96c72` · 142 | karar işlemi + denetim (AC-16 kararlar) |
| `…/GatewayWorkflowClientTests.cs` | `adaca86b067568d4` · 300 | HTTP şekli; "2xx dışı = ret" beklentileri **alma** (AC-40) |
| `…/ConfiguredApproverResolverTests.cs` | `63a0ff1edfaa00d3` · 409 | R1–R3 aday dışı (AC-42 öncesi) |
| `…/PaymentActionAuthorizationTests.cs` | `75beb2eb2b205f0e` · 94 | izin vakaları (AC-6, AC-9) |
| `…/GatewayAuthoritativeApproverProviderTests.cs`, `…/ClaimsPaymentPermissionEvaluatorTests.cs`, `…/FixturePermissionEvaluator.cs` | `e9eed235587717ab` · 282, `fba13d1ce6fd03a9` · 120, `731c886af6873630` · 140 | **alma** |

Kaynakta olmayan testler DL-2'de zorunludur: başlatma zaman aşımı / boş 2xx (AC-12), 404 belirsiz (AC-40), aynı anahtarlı yarış
(AC-31), silinmiş uzak kayıt (AC-35), eski `SubmissionSequence` örneği (AC-36), muafiyet ataması yedi dalı + parti bağı (AC-43).

### 2.6 Yetki ve roller — DL-2 (D46, D47, D49, D50, D54)

Pack "Authorization Convention" (`:1060-1125`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-338`; D46 tablosu tarihçedir — D49/5).

- **DL-2 uçlarının anahtarları:** `finance-treasury.payment-batches.submit` (T3), `.reconcile` (T3), `.approve` (T2),
  `.tier2-approve` (T3, handler içi), `.reject` (T2), `.request-evidence` (T3), `.waive-sod` (T3, yalnız açık atama).
  Bağımlı: `platform.workflow.instances.start` (gönderen), `platform.workflow.tasks.approve` / `.reject` / `.request-info`
  (onaylayıcı), `mdm.legal-entities.read` (AC-2, her karar).
- **Test kişileri matristen** (`treasury-intake-owner-decisions-05.md:162-166`, `:221-229`): Submit → **Ödeme hazırlayıcı**;
  Approve / Reject / RequestEvidence → **Ödeme onaylayıcı**; tier-2 Approve → **Ödeme onaylayıcı (2. kademe)**; Reconcile →
  hazırlayıcı, onaylayıcı (+ 2. kademe), **Ödeme serbest bırakıcı** ve **Hazine yöneticisi** (dört rol); okuma → Ödeme
  görüntüleyici. **Hazine yöneticisi** approve / reject / request-evidence / tier2-approve / waive-sod **taşımaz** (D47/1) —
  negatif test: Hazine yöneticisi Approve → 403. AP / AR rolleri ödeme anahtarı taşımaz (D49/1).
- **`waive-sod` (D47/1, D49/4, D54):** hiçbir rol şablonunda yok; `ExplicitGrantOnlyPermissions` listesinde. Servis çalışma
  anında (MOD-0023 çağrısından önce) şunları denetler: bitiş gelecekte ve başlangıçtan ≤ 24 saat; çağıran ≠ atamayı onaylayan;
  atamayı onaylayan ∉ {oluşturan, gönderen, son güncelleyen}; `ReasonCode ∈ {ACIL-ODEME, TEK-YETKILI, DENETIM-ISTEGI}`;
  **atamanın hedef parti kimliği var, okunabilir ve rotadaki `{id}` ile eşit** (D54); herhangi bir alan okunamıyorsa 403
  `PAYMENT_SOD_WAIVER_FORBIDDEN`, karar / niyet / makbuz / MOD-0023 çağrısı yok. "Hazine yöneticisi ya da K1 onaylar" Auth'un
  atama onayı akışıdır — servis rol adını denetlemez. 5 iş günü K1 incelemesi süreçtir; servisin tek katkısı atlanamaz K2
  olayıdır (`…approved-with-sod-waiver`: başlangıç, bitiş, onaylayan, gerekçe kodu, hedef parti).
- **Test kişisi (muafiyet):** tier-2 izni olmayan Ödeme onaylayıcı + açık `waive-sod` ataması (hedef parti A, ≤ 24 saat,
  onaylayan ≠ kendisi ve ≠ R1–R3 kişileri). Bu atama bugün Auth'ta kurulamaz (§1, D54 satırı) — sahte Auth atama kaynağıyla
  birim / entegrasyon testi yazılabilir, gerçek izinle kanıt Auth alanlarını bekler.
- **D50** yalnız AP / AR rollerini değiştirdi; MOD-0126 DL-2'ye etkisi yok. **D47/4:** ödeme rollerine banka okuması verilmez;
  DL-2 banka verisi okumaz.
- **Denetim:** onay / ret / muafiyet K2 (D11/3); kategori `Finance = 18` (D35/3; DL-1 ile main'e girer).

## 3. Kaynakta olmayan, DL-2'de yeni yazılacaklar

- `PaymentWorkflowStartIntent` + üç tekil indeks; `PaymentApprovalDecision.State` ve deterministik uzak karar anahtarı.
- İki aşamalı akış (pack Runtime Constraints `:599-846`): tx1 (parti `Submitted` / karar `Requested`, niyet, makbuz `Pending`,
  deneme olayı) → MOD-0023 → tx2 (sonuç, makbuz `Completed`, sonuç olayı); belirsiz → 202 `…_OUTCOME_UNKNOWN`; kesin ret → 502;
  404 → `Unknown` (AC-40).
- `ReconcilePaymentBatchWorkflowCommand` — önce okur (`by-objects`, `instances/{id}`, `history`); kendi eşleme alanıyla eşleşmeyen
  sonucu kapatmaz (AC-36); yeniden yazma yalnız asıl aktöre ve yalnız gap 6/P1-a koşulları kapalıyken (AC-34 (d), AC-35);
  MOD-0031'i hiç çağırmaz (AC-34 (e)).
- SoD politika servisi + `IPaymentSodPolicyProvider` (tek eşik tipi, kiracı ayarı, `SodPolicySource = DefaultAllTier2`, ayar
  deposu erişilemezse 503) — ayarın yeri `PaymentSodSettings` (KARAR D56/4, AC-44; §5 bulgu 2 kapandı).
- Muafiyet ataması okuma portu (Auth'tan beş alan; D54) — `IPaymentSodWaiverAssignmentSource`, bağdaştırıcı
  `Infrastructure/SodWaivers/**` (pack DL-2 Yollar, KARAR D56/3; §5 bulgu 1 kapandı); aday kaynağı
  `IFinanceTreasuryApproverCandidateSource` (`Application/Common/ApproverCandidates/**`, `Infrastructure/ApproverCandidates/**`;
  servis ortak, tek yazarı bu dilim — D66/1, D67/3).
- `IPaymentWorkflowPort` + `Infrastructure/Workflow/` bağdaştırıcısı (zaman aşımlı `HttpClient`, kullanıcı token'ı iletilir).
- `payment-batch.openapi.yaml` DL-2 uçlarıyla güncellenir (sürüm artışı — pack DL-1'de v1.0.0 diyor; DL-2 sürüm numarası pack'te
  yok, prompt yazılırken CT belirler).

## 4. DL-2 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir. Hedef yol (öneri): `execution/domains/finance-treasury/work-packs/prompts/WP-0126-DL2-v1.0.md`.
Kuyrukta `WP-0126-DL2` satırı **yok** — DL-1 kabulünden sonra CT açar; bu kayıt kuyruğa yazmaz.

**Başlık satırları**

| Alan | Değer (taslak) | DL-2 başladığı gün doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — `/add-module` yok (iskelet DL-1'de açıldı) | — |
| WP / Prompt | `WP-0126-DL2` · `TRM-WP-0126-DL2-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0126-dl2` — **öneri — D51/3 / D52/1 deseni, sahip teyidi gerekir** | Expected HEAD = DL-1 merge'ü ve DL-1 kabul kaydı main'deyken alınan commit (S-53); worktree yolu |
| Risk | HIGH — §9.1: kiracı, yetki, SoD / muafiyet, uzak çağrı belirsizliği, para | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kapsanan dilim | **yalnız DL-2** — başlama koşulu: DL-1 kabul edildi + "— DL-2 için" dilim kapıları (MOD-0023 ödeme şablonu kodu O-8; O-4, O-7 kapandı); §17 kodu ve AC-W1…W17 **kabul kapısı**; D54 Auth atama alanları — **KARAR D66/3 ile kabul kapısı değil** (muafiyet yolu 403 ile kapalıyken DL-2 kabul edilir; AC-9 Waived dalı, AC-42 aday dalları, AC-43 geçerli atama dalı muafiyet açılış kabulünde); D66/1 aday sorgusu (MOD-0018 eki DL-6) paket metni dilim kapısı, kodu **kabul kapısı** | — |

**DL-2 Bitiş AC'leri** (`:1617`): AC-40, AC-4…15, AC-42, AC-43, **41 (d: Approve kısmı)**, **39 (Reconcile kısmı)**,
**16 (kararlar)**, 31, **32 (karar uçları kısmı)**, **33 (Submit, kararlar, Reconcile kısmı)**, 34, 35, 36. AC metinleri pack'ten
tırnakla alıntılanır. Kısmi AC'lerin kalanı: AC-16 kalan, AC-32 Release / kalan okumalar, AC-33 Release / kanıt / onay belgesi,
AC-41 (d) Release → DL-3. AC-43 yedi dal + parti bağı sabotajı (A partisine bağlı atama B'de 403, yazım ve MOD-0023 çağrısı yok).

**İzinli yollar** (pack Repo Scope `:497-520`, DL-2 Yollar `:1613-1614`):
`services/Diten.FinanceTreasuryService/src/…Application/Features/PaymentBatch/**`, `…Domain/PaymentBatches/**`,
`…Persistence/PaymentBatches/**`, `…Infrastructure/Workflow/**`, `…Api/Controllers/PaymentBatchesController.cs`,
`services/Diten.FinanceTreasuryService/tests/**` altında MOD-0126 klasörü; `docs/analysis/contracts/payment-batch.openapi.yaml`;
pack DL-2 Yollar'ındaki `PaymentSodSettings` dosyaları, `…Infrastructure/SodWaivers/**` (D56/3), `…Application/Common/ApproverCandidates/**`
ve `…Infrastructure/ApproverCandidates/**` (servis ortak, tek yazarı bu dilim — D66/1, D67/3).

**Korunan yollar:** `.antigravity/**`, `AGENTS.md`, `gateway/**`, `services/Diten.Platform/**` (MOD-0023 §17 kodu dahil — Ali'nin
WP'si), `services/Diten.AuthService/**` (D54 atama alanları dahil — MOD-0018 Treasury Auth eki DL-5, D64), `services/Diten.MdmService/**`,
`services/Diten.Building.Blocks/**`, diğer servisler; servis ortak yollar `Application/Common/{CommandReceipts,DataScope}/**`,
`Persistence/Common/CommandReceipts/**`, `Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**` (DL-1'de açıldı;
değişiklik dört paketin onayını ister); MOD-0119 / 0120 / 0124 feature yolları; `Infrastructure/{Evidence,BankDispatch}/**` (DL-3);
`frontend/**` (DL-4); `tests/architecture/audit-ledger/**` (yeni iz yok; sayaç değişirse CT kararı); `execution/**`; kaynak depo.

**Önce oku:** pack (`ready-for-dev` sürümü), DL-1 WP raporu ve kabul kaydı, MOD-0023 pack §17 (`803fbaa5…`), AGENTS.md,
`execution/domains/finance-treasury/domain-config.md`, `.antigravity/rules/` multi-tenancy, security-jwt, permission-key-standard,
git-safety, cross-module-change-consent, code-style (S-56), audit-trail-standard, data-scope-enforcement, mongo-indexing,
handler-design; karar kayıtları `treasury-intake-owner-decisions-0{1..5}.md` (D11–D13, D21, D22, D25, D32, D42, D47, D49, D54, D56/3–4, D57/4–5,
D62/2, D64, D66, D67, D68), MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`, DL-5 ve DL-6),
MOD-0023 sahip kararları (`docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, W-1, W-6), bu hazırlık kaydı.

**Sözleşme yüzeyleri:** API `payment-batch.openapi.yaml` (DL-2 uçları + hata kodları + `Idempotency-Key` / `If-Match`); tüketilen
MOD-0023 `WorkflowModels.cs` @ DL-2 başındaki Platform commit'i (sha yazılır) + şablon kodu; MOD-0220 TRM-14 sözleşmesi; Auth
atama alanları (D54). İzin §2.6.

**Gerekli testler:** birim (SoD R1–R10, tier-2 sınırı `TotalAmount == eşik`, para birimi farkı AC-8, muafiyetin üç dalı + liste
dışı gerekçe AC-9, Guid aktör AC-10, D49/4 + D54 yedi dal AC-43); **gerçek Mongo replica set** entegrasyonu (tx1 / tx2, makbuz
`Pending` / `Completed`, AC-31 üç yarış + tekil kısmi indeks sabotajı, CentralAudit commit / rollback, AC-35 silinmiş kayıt) — her
takım kendi URI değişkenini okur, varsayılan porta düşmez, replica set `enableTestCommands` ile; **sahte MOD-0023 sunucusu**
(zaman aşımı, bağlantı kopması, boş 2xx, 5xx, 403, 404, "başlatma tamamlanmadı" 409, anahtar başına tek örnek / geçiş);
sahte MOD-0220; sahte Auth atama kaynağı; mimari (her aksiyonda `[HasPermission]`, tek eşik tipi AC-7, kodda muafiyet dışı
break-glass yok); sözleşme testi; build `Diten.FinanceTreasuryService` 0 hata; yerel kapıda `scripts/` Python testleri. Sahte
istemciyle geçen test canlı kanıt sayılmaz; MOD-0023 canlı kanıtı (gerçek başlatma + onay + ret + aynı anahtarlı yarış) §17 kodu
main'deyken dev ortamında (pack kapanış tablosu `:589`).

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş**: hedef servis + proje yolu; aggregate / entity + alanlar (`PaymentWorkflowStartIntent`,
  `PaymentApprovalDecision` + `State`, `PaymentException`; `PaymentBatch` SoD alanları `SodPolicyVersion` / `Tier2ThresholdAmount` /
  `Tier2ThresholdCurrency` / `IsTier2` — pack `:330`); komut listesi (Submit, Reconcile, Approve, Reject, RequestEvidence,
  ApproveWithSodWaiver); validator kuralları (pack Validation Rules SoD + muafiyet gerekçe kodu kapalı listesi); izin anahtarları
  §2.6; `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 ile (S-52); inceleme kaydı commit'ten önce `prompt_gate.py review-check` (S-54); HIGH iş — kabul kanıtı
betiğini CT yazar, kırmızı tabanı kaydeder (S-83); Expected HEAD DL-2 başladığı gün doldurulur, pack onayı değişirse prompt
yeniden tabanlanır (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22.

**Kod alma izi:** §2'deki **al-uyarla** satırları; her biri WP raporunda `path@da4abb20 + sha256`.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive` → kazı dizini); kaynak düzeltmesi yok. Kaynak ve Recovery testleri
  **çalıştırılmadı**; Recovery'de yalnız dosya okuma ve `grep` (@ec063fee9).
- MOD-0023 şablon kodunun, §17 kodunun, Auth atama alanlarının ve DL-1'in tarihi sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi; `ready-for-dev` revizyonunda CT / sahip işi):**

1. **D54 Auth bağımlılığı hiçbir kapıda yok.** Authorization Convention (`:1121-1124`) ve Dependencies MOD-0018 satırı Auth'un
   beş atama alanını (bitiş, başlangıç, onaylayan, gerekçe kodu, hedef parti) "bağımlılık" olarak yazıyor; ama ne paket kapısındaki
   "Auth paket metni" maddesi (`:1446`) bu alanları anıyor ne de "— DL-2 için" dilim kapıları (`:1460-1465`) bir Auth maddesi
   taşıyor. Bugünkü Auth modelinde süreli / onaylı / hedef nesneli kullanıcı ataması yok (§1). Sonuç: AC-9 "Waived" dalı ve AC-43
   geçerli atama dalı hangi kapıda bekleyeceğini bilmiyor. Ayrıca muafiyet ataması okuma portunun adı ve bağdaştırıcı yolu Repo
   Scope'ta yok. Öneri: "— DL-2 için" kapısına (ya da DL-2 kabul kapısına) "MOD-0018: `waive-sod` atamasında beş alan + okuma
   yolu main'de (Ali, D54)" maddesi; Repo Scope'a port + bağdaştırıcı yolu. **Kapandı:** D56/3 (alanlar DL-5 paket metninde — DL-2
   dilim kapısı), D64 (Treasury yazar), **KARAR D66/3** (DL-5 kodu DL-2 kabul kapısı değil; muafiyet açılış kabulü).
2. **SoD ayarının kayıt yeri açık ama DL-2 onu yazıyor.** Runtime Constraints "Ayarın kayıt yeri … Follow-up'tadır" (`:815-816`);
   DL-2 Yollar "tek eşik yapılandırması" (`:1614`) ve AC-7 "ayar deposu erişilemezse 503" (`:1221-1225`) bir depo varsayıyor. Ayar
   ekranı Follow-up olabilir, ama deponun yeri (servis içi koleksiyon mu, Platform ayarı mı) DL-2 prompt'undan önce karar ister. **Kapandı — KARAR D56/4** (`PaymentSodSettings`, AC-44) ve D57/4 (izinler).
3. **AC-42 muafiyet adayının kaynağı tanımsız.** AC-42 "tier-2 aday yoksa `waive-sod` izni olanlar `IsWaiverCandidate = true` ile
   aday olur" diyor; O-10 adayları MOD-0023 şablon atamasından alıyor ve "ayrı dizin istemcisi taşınmaz". `waive-sod` yalnız açık
   atamalı bir izin olduğuna göre, gönderme anında kimin bu izne (ve bu partiye bağlı atamaya, D54) sahip olduğunun nereden
   okunacağı yazılı değil (MOD-0023 ataması izin bilgisi taşımaz). Prompt'tan önce sahip / Ali ile netleşmeli. **Kapandı:** D56/3
   (kaynak "bu partiye geçerli `waive-sod` ataması olanlar") ve **KARAR D66/1** (tek aday sorgusu — MOD-0018 eki DL-6'nın
   `waive-sod` + parti biçimi; normal ve tier-2 aday tabanı aynı sorgudan).
4. **MOD-0023 bağdaştırıcısının sahipliği paketler arasında çelişik.** MOD-0126 Repo Scope `Infrastructure/Workflow/**`'u MOD-0126
   port bağdaştırıcıları arasında sayıyor; servis ortak listesi (D14/1, D14/2: makbuz, veri kapsamı, LE, para birimi, kod) iş
   akışını içermiyor. MOD-0120 Repo Scope ise "iş akışı bağdaştırıcısı MOD-0126'nın MOD-0023 bağdaştırıcısıyla ortaktır" diyor;
   MOD-0119 ayrı `Infrastructure/ApWorkflow/**`, MOD-0124 `Infrastructure/Workflow/**` içinde "MOD-0124 bağdaştırıcıları" açıyor.
   Ortaksa D14/2 listesine ve dört paketin ortak onayına girmeli; değilse MOD-0120 cümlesi düzeltilmeli. **Kapandı — KARAR
   D57/5:** MOD-0023 bağdaştırıcısı servis ortak yapı taşı, tek yazarı MOD-0126 DL-2; MOD-0119 DL-2, MOD-0120 / MOD-0124 DL-3 tüketir.
5. **AC-W kapsamı paketler arasında farklı.** MOD-0126 dilim kapısı "AC-W1…W17" (`:1463`) diyor; MOD-0119 / 0120 / 0124
   "AC-W1/W2/W5…W14" ya da "AC-W5…W14" diyor (D32'nin AC-W15 / W16 / W17'si yok). Aynı MOD-0023 koduna bağlı dört tüketici için
   kabul kapısı tek liste olmalı. **Kapandı** (pack D56 revizyonu: dört paket AC-W1…W17).
6. **Küçük:** Validation Rules DL-2'de kapanıyor (`:1587`) ama R11 DL-3'te uygulanıyor (`:1591-1594` notu) — "her bölüm kapandığı
   dilimde yazılıdır" kuralıyla yan yana; not açıklıyor, ama kabul kaydında Validation Rules'un DL-2'de "kapandı" sayılmaması
   için prompt R11'i açıkça DL-3'e bırakmalı. `payment-batch.openapi.yaml`'ın DL-2 sürüm numarası pack'te yok. **Kapandı** (pack D56 revizyonu: DL-2 Bitiş'te "Validation
   Rules R11 hariç" notu; `payment-batch.openapi.yaml` DL-2 sürümü 1.1.0).
