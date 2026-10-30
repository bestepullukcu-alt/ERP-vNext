---
record_id: MOD-0126-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/finance-treasury/module-packs/MOD-0126-payments-approvals.md (status draft, sha256 d86c2a564c02765196cb8aac924a0ade967f715f26c4df120df62b4d335fa6dc (pack D80 / FU27 atıf revizyonu, 2026-10-29 — MOD-0018 Treasury Auth eki atıfları `MOD-0018-FU27-treasury-permission-onboarding.md`'ye çekildi (K-1 kapandı, eski geçici ad FU-TBD — KARAR D80/1); Auth paket kapısı maddesi şartlı teyide çekildi (Y-19 / KARAR D80/2: DL-1'i bekletmez; iki şart ekin DL-5 kodundan önce); revizyon notu satırı eklenmedi (satır numaraları korundu); kurallar, AC'ler ve dilim tablosu değişmedi; önceki sürüm `37d7f682…99af` — pack KARAR D78 revizyonu, 2026-10-28 — R4 açık (sabitlenmiş `R4Enabled = true`) ama tier-2 olmayan (`IsTier2 = false`) partide muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, `Waived` kaydı, yazım ve MOD-0023 çağrısı yok (D78; AC-43 genişletildi — (j), DL-2); yeni AC yok, yeni hata kodu yok; Codex fark incelemesi F-1'in kapanış önerisi uygulandı (`docs/records/reviews/MOD-0126/2026-10-28-58976767.md`, KOŞULLU, `reviews.tsv:129`); önceki sürüm `58976767…b76e` — pack KARAR D77 revizyonu, 2026-10-28 — R4 kapalı (sabitlenmiş `R4Enabled = false`) partide muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, yazım ve MOD-0023 çağrısı yok (D77/1; AC-43 genişletildi — (i), DL-2); Release'de R11 tier-2 şartı tutmazsa 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`, parti `Approved` kalır, `PaymentDispatchAttempt` yazılmaz (D77/2; AC-19 genişletildi, DL-3); yeni hata kodu `PAYMENT_TIER2_RELEASE_FORBIDDEN`, yeni AC yok; önceki sürüm `8127b3cd…e84c` — pack CT-APR-02 Codex b91d61325 KOŞULLU bulguları revizyonu, 2026-10-28 — R4 uygulanma koşulu `IsTier2` ∧ sabitlenmiş `R4Enabled = true` (aday tabanı, Approve kontrolü, muafiyet adayı ve Release R11 hizalandı); AC-44 (d) ve AC-19 genişletildi (R4 kapalı Submit → Approve → Release, koşulun kaldırılma / ters çevrilme sabotajı), AC-6 netleşti; AC-41 kapanış cümlesi dilim listeleriyle hizalandı; yeni AC yok, yeni hata kodu yok; önceki sürüm `674c73e9…79ea` — pack D76 revizyonu, 2026-10-28 — kullanıcının başlattığı Reconcile da niyetteki ilk aday listesini kullanır — yalnız KARAR D76/3 atfı, davranış değişmedi; AC genişletilmedi, yeni AC yok, yeni hata kodu yok; önceki sürüm `b5e749bd…732e` — pack D75 revizyonu, 2026-10-28 — iç uç kimliği atıfları KARAR D74/1'e çekildi (servis kimliği kullanılmaz); Reconcile'ın yeniden başlatması niyetteki ilk aday listesini kullanır — önceki metin çözümleyiciyi yeniden çalıştırıyordu (D75/2 (b); AC-34 genişletildi); muafiyet atayanı izin + rol üyeliği (D75/1, Auth tarafı); BANK-ACCOUNT-LOOKUP v1'de tüketilmiyor — D75/4 yalnız Follow-up'ta; yeni AC yok, yeni hata kodu yok; önceki sürüm `fb581b3b…cce5` — pack D74/3 revizyonu, 2026-10-28 — muafiyet atayanı Muafiyet yetkilisi rolü (MOD-0018 Treasury Auth eki 27. rol; D49/4'teki "Hazine yöneticisi ya da K1" kalktı), servis davranışı ve AC'ler değişmedi; önceki sürüm `1b8f172a…8ddf` — pack D71 revizyonu, 2026-10-28 — D71 aday tabanı iş akışı izniyle de kesişir (`approve ∩ platform.workflow.tasks.approve`, tier-2 partide `∩ tier2-approve`; anahtar başına ayrı DL-6 çağrısı), K-A4b kapandı, AC-11 genişletildi; önceki sürüm `ec4943aa…` (pack D70 revizyonu, 2026-10-28 — D70/1 K-A4 kapandı (K-A4b açık), D70/3 Auth paket kapısı, D67 atıfları (ApproverCandidates D67/3), D64 / D66 main'de; önceki sürüm `3a70398a…` (pack D68 revizyonu + sahip denetimi d5b378e50 P2, 2026-10-28 — boş aday listesi yerel 422, MOD-0023 çağrılmaz; önceki sürüm `14b1bcc5…` (D68 revizyonu); daha önce `5e1ea803…` (sahip denetimi 61466e504 KOŞULLU revizyonu — tier-2 aday kesişimi); daha önce `f496790f…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `81b9b505…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `05410477…` (D58/D59 revizyonu); D57 sürümü `5f69139c…`; D56 sürümü `1baf8728…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `5101b1e8…` içindir)), dal chore/ct/treasury-intake-ct-05, "Revizyon (2026-10-27, D54 — Codex F-1)"; önceki 699b0708…; satır atıfları main @904dfd5c2 sürümüne (91090dd6…13cc))
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; D1 doğrula ve uyarlayarak al, kod alma izni D27/5)
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve bu pack sürümü dahil; D69 main'de değil ve MOD-0126'ya dokunmaz).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU27 — K-1 kapandı, Y-19 / D80/1). Taslak main'de, `draft`; **2026-10-29: MOD-0018 sahibi D69–D78 sürümünü (main `4c08fc75a`) şartla teyit etti — KARAR D80/2, Y-19:** DL-1'i bekletmez; ekin DL-5 kodundan önce K-W5 defter kararı + rol atama adımlarının denetim borcu; D79'dan sonraki değişiklikler ayrıca teyide gider — pack'teki "Auth paket metni
> main'de" maddesi şartlı işaretli (D80 main'de, `317a48b13`; KARAR D82/1), paket kapısı şartlı (D53/1, D80/2). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-1**'i; kayıt
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
> **Güncel durum (2026-10-27).** Bu kayıt pack'in main @904dfd5c2 sürümüne bağlıdır (sha256 `91090dd6…13cc`, `status: draft`;
> bu sürüme CT-APR-02 ve CT-APR-01 satırı yok). 2026-10-26 sürümlerine (`684e89d6…`, `94d3ad93…`) yapılan "pack satır N" atıfları
> geçersizdir; bu kayıt pack'e bölüm adıyla ya da `yol:satır@904dfd5c2` ile atıf yapar. Pack `ready-for-dev` revizyonunda yeniden
> değişirse atıflar o sürümde yeniden doğrulanır (S-53).
>
> **D51 güncellemesi (2026-10-27).** Sahip kararı D51 (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md`)
> bu kaydın açık bıraktığı üç noktayı kapattı ve pack çalışma ağacında (dal `chore/ct/treasury-intake-ct-05`; tarihçe: yazıldığı anda commit edilmemişti; commit 1c444a2c5)
> buna göre revize edildi ("Revizyon (2026-10-27, D51)"): (D51/1) ENGELLER paket kapısı ↔ "— DL-n için" dilim kapısı olarak
> ayrıldı — DL-1 yalnız paket kapısını bekler (Not 1 kapandı); (D51/2) SEC-002 kapsamı soran kod + kanıt testi (AC-32) DL-1 kabul
> kapısında, Auth'ta `read-tenant-wide` tanımı paket kapısında (Not 2 kapandı); (D51/3) WP dalı `feature/fin/wp-0126-dl1`,
> `/add-module` servis iskeleti DL-1 WP'sinin ilk adımı (§4 başlık satırları). Aşağıdaki `@904dfd5c2` satır atıfları eski sürüme
> aittir; D51 revizyonu satırları kaydırdı — atıflar `ready-for-dev` sürümünde yeniden doğrulanır.
>
> **D51 denetimi (2026-10-27).** Paket kapısı D51/1'e göre: Auth izin / rol kaydı (MOD-0018 paketi ve kodu, Ali, Y-6)
> `ready-for-dev`'den önce main'de olmalıdır — pack'e işaretsiz madde olarak eklendi; aşağıdaki MOD-0018 satırındaki eski
> "DL-1'i bekletmez" çıkarımı kaldırıldı. AC-32 kısmi kapanır: DL-1 uçları DL-1'de, karar uçları DL-2'de, Release ve kalan
> okumalar DL-3'te.
> D53 sonrası: Auth paket metni paket kapısı, kayıt kodu DL-1 kabul kapısı.
>
> **D52 sonrası (2026-10-27).** Pack "Revizyon (2026-10-27, D52)" (sha256 `0b58ae58…8f59`): yalnız metin temizliği — §17 rev 2
> onay maddeleri `[x]` (`approvals.tsv:16`), küsurat satırında MOD-0048-FU01 §21 adı. MOD-0119, MOD-0120 ve MOD-0124 ortak
> tanımları bu sha ile sabitler.

# MOD-0126 DL-1 hazırlığı — servis iskeleti, parti çekirdeği ve denetim altyapısı

Bu kayıt, MOD-0126 `ready-for-dev` olduğunda DL-1 WP prompt'unun aynı gün yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

## 1. Başlama koşulları (ölçüm: main @904dfd5c2, 2026-10-27)

Kaynak: pack "Teslim dilimleri" DL-1 önkoşulu (`MOD-0126-payments-approvals.md:1557-1559@904dfd5c2`) ve "Ready-for-dev
Checklist" işaretsiz maddeleri (`:1398-1420@904dfd5c2`). "DL-1'e etkisi" sütunu: **bekletir** = DL-1 kodu bu kapanmadan
başlamaz; **DL-2/DL-3** = pack'e göre sonraki dilimin önkoşulu. **D51/1 sonrası:** "bekletir" satırları pack'in paket kapısı
(işaretsiz ENGELLER) ile birebirdir; "DL-2 / DL-3" satırları pack'te "— DL-n için" işaretli dilim kapısıdır ve DL-1'i bekletmez.

| Koşul | Sahibi | Bugün | DL-1'e etkisi |
|---|---|---|---|
| Pack `ready-for-dev` + o sürüme CT-APR-02 (bağımsız inceleme) | Treasury CT + bağımsız inceleyici | `status: draft`; **2026-10-29:** güncel sha `d86c2a56…a6dc` (D80 / FU27 atıf revizyonu) için `reviews.tsv`'de satır yok (son satır `37d7f682…99af`, `reviews.tsv:136`, UYGUN) · önceki ölçüm: güncel sha `37d7f682…` (KARAR D78 revizyonu; önceki `58976767…` D77 — Codex fark incelemesi KOŞULLU, `reviews.tsv:129`; önceki `8127b3cd…` CT-APR-02 Codex b91d61325 KOŞULLU bulguları revizyonu; önceki `674c73e9…` D76 — Codex tam incelemesi KOŞULLU, `reviews.tsv:120`; `b5e749bd…` D75, `fb581b3b…` D74/3) için `execution/approvals/reviews.tsv`'de satır yok (ölçüm 2026-10-28) | bekletir |
| CT-APR-01 sürüme bağlı onay satırı (`high: evet`, paketi yazmamış onaylayıcı; D34/2: taslak paketler tek turda) | Beste (onaylayıcı) | `execution/approvals/approvals.tsv`'de MOD-0126 satırı yok | bekletir |
| DCP-011 `approved` | Beste | dosya `execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md` var, `status: draft` (`:7`); numara atandı (Y-4/2), onay `ready-for-dev` turunda | bekletir |
| TRM-14 (a) MOD-0220 §22 pack onayı | Beste (onay verildi) | **kapandı** — `approvals.tsv:17` (MOD-0220 `91248a47…`) | — |
| TRM-14 (b) `lookup-validation` yanıtında `ApprovalStatus` | Ali (kod MOD-0023 kulesinin sırasında, Y-5/3) | **yok** — `LegalEntityLookupDto` alanları `LegalEntityId, Code, LegalName, DisplayName, LifecycleState, Referenceable` (`services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106@904dfd5c2`); işleyici `Active` değilse 404 (`…/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-23@904dfd5c2`) | bekletir (AC-2, AC-39) |
| TRM-14 (c) MOD-0220 sözleşme belgesi | Ali | taslak var: `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` (LEGAL-ENTITY-LOOKUP 1.1.0); dondurulmadı | bekletir (AC-39 sözleşme testi bu sürüme) |
| O-9 RCS-001 ortak kod servisi (BL-565) + çağrı sözleşmesi | Beste (D44) | **yok** — `services/Diten.Building.Blocks/src/` altında kod servisi yok, `services/**`'te `IRecordCodePort` / kod servisi istemcisi 0; BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9473-9477@904dfd5c2`); çağrı sözleşmesi gelmedi (Y-4). Not: backlog satırında sahip hâlâ "CT (Platform + MDM …)" yazıyor, D44 (Beste) ile uyumsuz | **DL-1 kabul kapısı (D59/1)** — başlamayı bekletmez; DL-1 `IRecordCodeAllocator`'a karşı sahte uygulamayla yazılır, gerçek servis DL-1 kabulünden önce main'de (v0 sözleşmesi Y-8, 6 Kasım); üretimde port bağlı değilse 503 `PAYMENT_RECORD_CODE_UNAVAILABLE` |
| `PAY` öneki RCS-001 §3'te | — | **var** — `.antigravity/rules/record-coding-standard.md:63@904dfd5c2` (D29/2, D30) | — |
| Platform `AuditCategory`'ye `Finance = 18` (D35/3, Y-4/7) | Beste | **yok** — enum `Unknown = 0 … ESignature = 17` (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-27@904dfd5c2`); kopya `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs` son değer `ESignature = 17` (`:29`), `Finance` yok | bekletir (pack: "DL-1 bu değer main'e girmeden başlamaz") |
| O-11 MOD-0048 `currency-minor-units` seed + izin listesi | Beste (Y-4/3: başlangıç listesi kabul) | **yok** — `services/Diten.Platform/**`'te `currency-minor-units` geçmiyor; `BusinessReferenceData:ConsumableSets` listesinde yok (`services/Diten.Platform/src/Diten.Platform.API/appsettings.json:104-…@904dfd5c2`) | bekletir (Create; yoksa 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE`) |
| O-13 MOD-0048 `payment-method` kümesi seed + izin listesi | Beste (Y-4/3) | **yok** — aynı ölçüm, `payment-method` 0 | bekletir (Create; yoksa 503 `PAYMENT_METHOD_SOURCE_UNAVAILABLE`) |
| O-19 SEC-002 tüketim yolu | Beste (yol), Ali (Auth) | yol **belli** (Y-4/6: servis içi `IFinanceTreasuryDataScopeSource` + Platform `IDataScopeResolver` bağdaştırıcısı, fail-closed). Açık: `read-tenant-wide`'ın Auth'ta açık atamalı tanımı — `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`'te `finance-treasury.*` anahtarı yok (grep 0). "Kapsamı soran kod + kanıt testi" DL-1'in kendi işidir (AC-32) | bekletir — yalnız Auth paket metnindeki `read-tenant-wide` açık atama tanımı (paket kapısı, D53/1); `ExplicitGrantOnlyPermissions.cs` kaydı (D53/2) ve kapsamı soran kod + AC-32 **DL-1 kabul kapısı** (D51/2); bkz. not 2 |
| Auth paket metni — MOD-0018 paketi (anahtarlar, açık atama listesi, rol şablonları D46–D50) | Treasury yazar, MOD-0018 sahibi (Ali) teyit eder (Y-15, Y-16; önceki: Y-6; numara ve tarih yok) | **2026-10-28: taslak main'de** (#281; origin/main @ `027ca2e38`) — `…/MOD-0018-FU27-treasury-permission-onboarding.md` `draft` (eski geçici ad FU-TBD), FU numarası açık (K-1), MOD-0018 sahibinin teyidi yok, pack checklist maddesi işaretsiz [**2026-10-29:** FU27 — K-1 kapandı, Y-19 / D80/1; MOD-0018 sahibi D69–D78 sürümünü (main `4c08fc75a`) **şartla teyit etti** — KARAR D80/2: DL-1'i bekletmez; ekin DL-5 kodundan önce K-W5 defter kararı + rol atama adımlarının denetim borcu; D79 sonrası değişiklikler ayrıca teyide gider]; Y-14 geçersiz (kullanılmadı); ölçüm anında: **yok** — Treasury rolleri için MOD-0018 paketi depoda yok (`execution/domains/*/module-packs/MOD-0018-*` yalnız FU9…FU22) | paket kapısı (D53/1) — **2026-10-29: şartlı teyitle DL-1'i bekletmez (KARAR D80/2, Y-19)**; önceki ölçüm: DL-1'i bekletir: paket metni `ready-for-dev`'den önce main'de olmalı; pack checklist'indeki işaretsiz madde "Auth izin / rol kaydı — MOD-0018 paketi ve kodu" (D51 denetimi) D53 ile ikiye ayrıldı (pack metni bu kayıtta değiştirilmedi); `read-tenant-wide` tanımı O-19 satırındadır |
| Auth kayıt kodu (MOD-0018 Treasury Auth eki DL-1 kodu) | Treasury yazar, MOD-0018 sahibi teyit eder (Y-15, Y-16) | **yok** — Auth'ta anahtar yok (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`'te `finance-treasury.*` 0) | DL-1 kabul kapısı (D53/2) — DL-1 başlamasını bekletmez, kabulünü bekletir |
| MOD-0023 şablon kodu (O-8) | Ali | **2026-10-28 (Y-12): kod `FT-PAYMENT-BATCH-APPROVAL`**; şablonu kuran kod, iş paketi numarası ve tarih yok (Y-6: tek adım) | DL-2 |
| MOD-0023 §17 rev 2 onayı | Beste (onay verildi) | **kapandı** — `approvals.tsv:16` (`803fbaa5…`); AC-W1…W17 DL-2 kabul kapısında | DL-2 |
| MOD-0031 dedupe ölçümü | Treasury CT (ölçüm) | ölçülmedi | DL-3 |
| Verilmiş olanlar | — | port 5071 (`AGENTS.md:84`, `.antigravity/rules/ports.md:29`; D30); denetim defteri CT kararı D35; kod alma D27/5; ad alanı `finance-treasury.*` D11/4; servis `services/Diten.FinanceTreasuryService/` bugün yok (beklenen) | — |

**Not 1 — ENGELLER başlığının kapsamı.** Pack "ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz)" listesi DL-2 / DL-3 maddelerini de
içeriyor (MOD-0023 şablonu, §17 rev 2 maddeleri, MOD-0031 dedupe). Pack metnine göre `ready-for-dev` bunları da bekler; MOD-0289'daki
"iki kapı" ayrımı (tasarım kapısı ↔ dilim kabul kapısı) MOD-0126'da yok. DL-1'in DL-2 maddelerini beklemesi isteniyor mu — CT /
sahip kararı; bu kayıt karar vermez. **Kapandı — D51/1:** pack ENGELLER'i paket kapısı (işaretsiz) ve dilim kapıları ("— DL-n
için": MOD-0023 şablonu, §17 rev 2 maddeleri, O-4 → DL-2; MOD-0269 sözleşmesi, MOD-0031 dedupe → DL-3) olarak ayrıldı; DL-1
yalnız paket kapısını bekler.

**Not 2 — O-19 maddesinde kilitlenme.** Checklist maddesi "kapsamı soran kod + kanıt testi" ister; kod `ready-for-dev`'den önce
yazılamaz. MOD-0289 gap 16/2'deki gibi bu kısım dilim kabul kapısına taşınmadıkça madde kapanamaz. Pack revizyonu CT işidir
(öneri: Auth tanımı ready-for-dev maddesi, kod + AC-32 DL-1 kabul kapısı). **Kapandı — D51/2:** öneri olduğu gibi uygulandı;
AC-32 DL-1 "Bitiş"te zaten vardı.

**DCP numarası:** DCP-011 atandı ve dosya adı değişti (Y-4/2, 2026-10-27). D32/8'in numarasız onay istisnası D34/2 ile kullanılmaz;
pack checklist satırı ("DCP `approved` ve numarası atanmış") olduğu gibi kalır — açık kalan yalnız `approved`.

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak servis `services/DitenErpTreasuryService/` (kısaltma: `S/`). Özet: `shasum -a 256` (ilk 16 hane), satır sayısı `wc -l`,
ikisi de @da4abb20; 2026-10-27'de `git show da4abb20:<yol>` ile **yeniden ölçüldü, 25 dosyanın hepsi 2026-10-26 ölçümüyle aynı**.
Karar dili: **al-uyarla** = kaynaktan alınacak (doğrula, Recovery düzenine göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`),
**yalnız referans** = uyarlanacak davranış (davranış okunur, kod alınmaz, Recovery'de yeniden yazılır), **alma** = alınmayacak
(pack "Taşınmayacaklar" maddesiyle), **sonraki dilim**.

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `PaymentBatch`, `PaymentInstruction`, `PaymentEvidenceLinkReference` tipleri; `PaymentBatchStatus` 10 değer; DL-1 hata kodları; Create / Validate komutları; sözleşme DTO'ları; liste / detay sorguları; `PaymentProposalService` kuralları (bölünerek); Mongo deposu (DL-1 kısmı), indeks kataloğu, bağlam, indeks hizmeti; denetleyicinin DL-1 uçları | para birimi hanesi arayüzü (→ `ICurrencyReferencePort`), kimlik bağlamı (→ JWT `sub`), kaynak testleri (davranış listesi) | `TenantEntity` (`"system"` aktör), `PassedByAdminProxy`, outbox sınıfları, `PermissionContracts` + talep değerlendirici, `CurrencyPrecisionFallback` sabit tablosu, kaynak DI (zaman aşımsız `HttpClient`), `Program.cs` / port 5058, `/api/v1/mod0126` rotaları, `mod0126_*` adlar, `ux_mod0126_payment_batch_number` kısmi indeksi, kullanıcıdan parti no + `CountAsync()+1`, `InMemoryPaymentBatchRepository` |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `S/src/…Domain/PaymentProposals/PaymentProposalEntities.cs` | `568fe3cf50abdcb3` · 163 | al-uyarla (yalnız `PaymentBatch` :5-31, `PaymentInstruction` :33-53, `PaymentEvidenceLinkReference` :132-149) | `Domain/PaymentBatches/` | `TenantEntity` yerine `EntityBase`; `string` aktör → Guid (madde 11); outbox sınıfları (:55-66, :151-163) **alma** (madde 12); karar / istisna / onay belgesi DL-2/DL-3; banka referansları yalnız opak Guid (AC-20; pack "Entity Fields") |
| `S/src/…Domain/PaymentProposals/PaymentProposalEnums.cs` | `72e767bd46390e85` · 132 | al-uyarla | `Domain/PaymentBatches/` | `PaymentBatchStatus` 10 değer aynen (:9-21; pack "Durum makinesi"); `PassedByAdminProxy` **alma** (madde 7); `ApLinked` kaynak türü bu dilimde reddedilir (pack "Ownership and Boundaries") |
| `S/src/…Domain/Common/TenantEntity.cs` | — | alma | — | madde 11 (`"system"` varsayılan `CreatedBy`) |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Common/PaymentProposalErrorCodes.cs` | `ce3cc9e42e035587` · 57 | al-uyarla (DL-1'in kullandığı kodlar) | `Features/PaymentBatch/` hata sözlüğü | 52 kodun DL-1 alt kümesi + pack "Hata sözlüğü"nün DL-1'e düşen **yeni** kodları: `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`, `PAYMENT_REQUEST_IN_PROGRESS`, `PAYMENT_RECORD_CODE_UNAVAILABLE`, `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`, `PAYMENT_CURRENCY_SOURCE_UNAVAILABLE`, `PAYMENT_CURRENCY_CODE_INVALID`, `PAYMENT_METHOD_NOT_ALLOWED`, `PAYMENT_METHOD_SOURCE_UNAVAILABLE`; `PAYMENT_TENANT_FORBIDDEN` kullanılmaz (404); serbest İngilizce ileti **alma** (madde 3) — ileti resx anahtarı |
| `…/Commands/CreateManualPaymentBatchCommand.cs` | `ce2481db5037955c` · 25 | al-uyarla | `Commands/CreatePaymentBatchCommand.cs` + `Handlers/CommandHandlers/CreatePaymentBatchHandler.cs` | komut + handler aynı dosyada **alma** (:8-25); kullanıcıdan parti no ve `CountAsync()+1` **alma** (madde 9) → `IRecordCodeAllocator` (`PAY`, RCS-001 §3) |
| `…/Commands/ValidatePaymentBatchCommand.cs` | `7481d77d5ecd98fe` · 25 | al-uyarla | `Commands/ValidatePaymentBatchCommand.cs` + handler | idempotency makbuzu ile (AC-16) |
| `…/Contracts/PaymentProposalContracts.cs` | `1e659de1d2867029` · 197 | al-uyarla | `PaymentBatchModels.cs` (tek dosya) | DTO'larda `TenantId` / `LegalEntityId` yok (AC-1); banka alanı adı yok (AC-20) |
| `…/Contracts/CurrencyPrecisionContracts.cs` | `69adb336a829e8c5` · 8 | yalnız referans | `ICurrencyReferencePort` (servis ortak) | kaynak arayüz tenant + kod; Recovery portu MOD-0048 `currency-minor-units` kümesini okur |
| `…/Contracts/IPaymentVerifiedIdentityContext.cs` | `5510bddb215ca7c4` · 8 | yalnız referans | Recovery `ICurrentUser` / JWT | servis içi kimlik bağlamı yeniden yazılmaz |
| `…/Queries/PaymentBatchQueries.cs` | `470bd7db2ca4e7f0` · 58 | al-uyarla | `Queries/GetPaymentBatchListQuery.cs`, `GetPaymentBatchByIdQuery.cs`, `PaymentBatchListSort.cs` + handler'lar | DataTable sunucu tarafı; kapsam süzgeci `IFinanceTreasuryDataScopeSource` (AC-32); `read-tenant-wide` yalnız okuma, yazma kapsamını genişletmez (AC-33) |
| `…/Services/PaymentProposalService.cs` | `f1dc4aa93bc0b636` · 489 | al-uyarla (bölerek) | Create / Update / Delete / BulkDelete / Validate handler'ları + domain | kuralları handler + domain'e böl (`handler-design`); kanıt bağlama :235-298 **alma** (madde 14, DL-3 ayrı komut); sayaç :126-128, :471-475 **alma** (madde 9) |
| `…/Contracts/PermissionContracts.cs`, `Infrastructure/…/ClaimsPaymentPermissionEvaluator.cs` | — | alma | `[HasPermission]` | madde 8, 13; DL-1 anahtarları aşağıda §2.6 |
| `…/Contracts/{SodPolicy,ApproverPolicy,WorkflowClient,PaymentLifecycle,PaymentEvidence,AuditDispatch}Contracts.cs`, `Services/{PaymentWorkflow,PaymentSodPolicy,PaymentLifecycle,PaymentEvidence,PaymentAuditDispatch}Service.cs`, `Commands/{SubmitWorkflow,AdminDirectDecision,GeneratePaymentAttestation,DispatchOutboxOnce}Command.cs`, `Queries/PaymentLifecycleQueries.cs` | — | sonraki dilim / alma | DL-2 (iş akışı, SoD, muafiyet — AC-9, AC-42, AC-43), DL-3 (kanıt, onay belgesi, Release); AdminDirect ve DispatchOutboxOnce **alma** (madde 7, 12; O-1, O-2) | |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Persistence/PaymentProposals/MongoPaymentBatchRepository.cs` | `2dd642c8c777e253` · 718 | al-uyarla (DL-1 kısmı) | `Persistence/PaymentBatches/` depolar | her işlem geri çağrısı `CentralAuditTransaction.EnlistAsync`; soft delete (`IsDeleted`, `DeletedAt`) |
| `…/PaymentProposalsMongoIndexes.cs` | `2bcb2622103f0e39` · 198 | al-uyarla | indeks kataloğu | 18 ad (4 tekil); `mod0126_` önekleri **alma** (madde 13). Kaynağın `ux_mod0126_payment_batch_number` indeksi (kısmi `IsDeleted=false`, `:47-52`) **alınmaz** (madde 9); yerine pack'in `ux_payment_batch_code` = **Unique(TenantId, BatchCode), kısmi filtre YOK** (`MOD-0126-payments-approvals.md:409@904dfd5c2`, RCS-001 §6). Kodun RCS'den gelmesi DB tekilliğini kaldırmaz. Test `PaymentBatchCodeUniquenessTests` (gerçek replica set) + sabotaj: AC-23 (`:1241-1247@904dfd5c2`) |
| `…/PaymentProposalsMongoContext.cs` | `977aa0d20bbbb5f1` · 26 | al-uyarla | `Persistence` bağlamı | koleksiyon adları `payment_batches`, `payment_instructions` |
| `…/PaymentProposalsIndexHostedService.cs` | `43c53eaad05a9717` · 29 | al-uyarla | indeks hizmeti | Recovery şema manifest deseni varsa ona uyulur (`mongo-indexing`) |
| — | — | **yeni** | `Persistence/Common/CommandReceipts/**` | `FinanceTreasuryCommandReceipt` + `ux_finance_treasury_command_receipt_scope`, `ix_finance_treasury_command_receipt_pending` (servis ortak; kaynakta yok; pack "Mongo indeksleri") |

### 2.4 Infrastructure ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `…/Infrastructure/PaymentProposals/CurrencyPrecisionFallback.cs` | `e00db36958d3b402` · 60 | **alma** | — | sabit ISO tablosu (USD/EUR/GBP/JPY/CHF/TRY) "test only" fallback'i; pack: sabit tablo uydurulmaz, hane yoksa 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` |
| `…/ClaimsPaymentVerifiedIdentityContext.cs` | `a8a89f1e80e0152e` · 35 | yalnız referans | — | Recovery JWT bağlamı |
| `…/Infrastructure/DependencyInjection.cs` | — | alma | yeni DI | zaman aşımsız `HttpClient` (madde 15); FailClosed varsayılanları uygulama sayılmaz (madde 1) |
| `…/Api/Controllers/PaymentBatchesController.cs` | `205cd802bd8801db` · 346 | al-uyarla (DL-1 uçları) | `Api/Controllers/PaymentBatchesController.cs` (+ pack Repo Scope `PaymentExceptionsController.cs` — DL-1 okuma ucu olarak, bkz. §3) | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]` (AC-21); rota `/api/payment-batches` (madde 13: `/api/v1/mod0126` alma) |
| `…/Api/Program.cs`, `appsettings.json` | — | alma | `@orchestrator /add-module` iskeleti | port 5071 (5058 alma, madde 13); `AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)`; `Category = Finance (18)` main'e girince (D35/3) |

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `S/tests/…/PaymentProposals/PaymentProposalServiceTests.cs` | `f903ba73b30245c1` · 313 | Create / Validate kuralları (AC-1, 2, 3, 18, 20) |
| `…/PaymentDraftRepositoryTests.cs` | `fb4efcf0dde92419` · 243 | taslak deposu, kiracı süzgeci (AC-3, 32) |
| `…/PaymentDraftTransactionTests.cs` | `be4b3a1186dcb409` · 171 | işlem + denetim kaydı birlikte (AC-17) |
| `…/PaymentValidationRepositoryTests.cs` | `6e47ab7fc3664fac` · 260 | Validate deposu |
| `…/PaymentValidationTransactionTests.cs` | `845bc61fae32d989` · 156 | Validate işlemi |
| `…/PaymentBatchesControllerTests.cs` | `74571d371cb37634` · 321 | uç + izin (AC-21, 22) |
| `…/InMemoryPaymentBatchRepository.cs` | `3dd093f30a0de809` · 570 | **alma** — Recovery gerçek Mongo replica set ister |
| `…/FixtureCurrencyPrecisionResolver.cs` | `46e912f8ccf373b5` · 25 | test fikstürü deseni; sabit tablo üretim koduna girmez |

Devralınmış kaynak kanıtı (`MOD-0126 TEST 510/510 @ da4abb20`) Recovery hazırlığının ölçüsü değildir (pack "Kaynak ve kanıt").

### 2.6 Yetki ve roller — pack değişikliklerinin DL-1'e etkisi (D46, D47, D49, D50)

Pack "Authorization Convention" (`:1024-1088@904dfd5c2`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-405@904dfd5c2`; D46 tablosu tarihçedir, uygulanmaz).

- **DL-1 uçlarının anahtarları:** `finance-treasury.payment-batches.read`, `.read-tenant-wide` (T3, yalnız açık atama, yalnız okuma),
  `.create`, `.update`, `.delete`, `.bulk-delete` (T2), `.validate` (T3); DL-1'in "dört sorgu"su (pack "Backend File Convention":
  liste, detay, iz, istisna listesi — `:837-838@904dfd5c2`) için ayrıca `finance-treasury.payment-trace.read` ve
  `finance-treasury.payment-exceptions.read`. Kaynak `mod0126.*` anahtarları **alınmaz**.
- **Rol şablonları kod değildir:** Auth paket metni MOD-0018 Treasury Auth ekindedir (`…/MOD-0018-FU27-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16; paket kapısı, D53/1); Auth kayıt kodu DL-1
  kabul kapısındadır (D53/2). DL-1 yalnız manifestte anahtarları kaydeder; hiçbir
  tohum / varsayılan rol yazmaz. Test kişileri matristen alınır: Ödeme hazırlayıcı (create / update / delete / validate), toplu silme
  için **Hazine yöneticisi** (`bulk-delete` yalnız onda — D47/1; hazırlayıcıda yok), okuma için Ödeme görüntüleyici (D49/1).
- **`waive-sod` DL-1'de yok:** muafiyet kuralları (D47/1, D49/4; AC-9, AC-42, AC-43) DL-2'dedir. DL-1'in tek etkisi: manifest kaydı
  `waive-sod` ve `read-tenant-wide`'ı hiçbir rol şablonuna varsayılan bağlamaz (açık atama listesi Auth'ta, D47/1).
- **D50** yalnız AP / AR rollerini değiştirir; MOD-0126 DL-1'e etkisi yok. **D47/4:** finans rollerine `mdm.banks.read` /
  `mdm.bank-accounts.read` verilmez; DL-1 banka verisi okumaz (opak Guid).
- **Denetim kategorisi:** MOD-0126 `Finance = 18` bekler (D35/3); `MasterData` ara çözümü seçilmedi. D48/1 yalnız MOD-0289'u
  `MasterData`'ya bağlar, MOD-0126'yı değiştirmez.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

Yollar pack "Repo Scope" (`:461-512@904dfd5c2`) ve "Teslim dilimleri" DL-1 satırı (`:1551-1560@904dfd5c2`).

- Servis iskeleti `Diten.FinanceTreasuryService` (5 katman + `tests/`), port 5071, JWT, kiracı izolasyonu, 4 pipeline
  davranışı, modül manifest kaydı (`module-self-registration-standard`).
- Servis ortak yapı taşları (tek sahibi MOD-0126 DL-1; MOD-0119/0120/0124 bunları yeniden tanımlamaz):
  `FinanceTreasuryCommandReceipt`, `IFinanceTreasuryDataScopeSource` (okuma + yazma kapsamı; Y-4/6 yolu — Platform
  `IDataScopeResolver` bağdaştırıcısı, fail-closed, MDM `ITrustedLegalEntityScopeProvider` kullanılmaz), `ILegalEntityEligibilityPort`
  (MOD-0220 + `ApprovalStatus`), `ICurrencyReferencePort` (MOD-0048 `currency-minor-units`), `IRecordCodeAllocator` (RCS-001, `PAY`),
  `DataScope` bağdaştırıcısı (`NoOp` kaydı yasak), CentralAudit kaydı + `FinanceTreasuryCentralAuditScope`.
- **Ödeme yöntemi kaynağı — artık pack'te:** `IPaymentMethodReferencePort` (`Features/PaymentBatch/**`) + MOD-0048 `payment-method`
  bağdaştırıcısı `Infrastructure/Currency/**` (zaman aşımlı `HttpClient`; uç `GET /api/lookups/reference-data/consumable-sets/payment-method/published-values`).
  MOD-0126'ya özgüdür, servis ortak değildir (pack "Repo Scope"). DL-1 kabulü AC-41 (a), (b), (c), (e) Create / Update için;
  (d) DL-2 / DL-3. Karşılaştırma `StringComparison.Ordinal` (S-56). Sabotaj: küme kontrolü kaldırılınca (b), harfe duyarsız
  karşılaştırma konunca (e) kırmızı.
- `UpdatePaymentBatchCommand`, `DeletePaymentBatchCommand`, `BulkDeletePaymentBatchCommand` (O-3; yalnız gönderilmemiş taslak,
  soft delete; AC-23).
- Dört sorgu: `GetPaymentBatchListQuery`, `GetPaymentBatchByIdQuery`, `GetPaymentBatchTraceQuery`, `GetPaymentExceptionListQuery`
  + `PaymentBatchListSort` (pack "Backend File Convention"); istisna okuma ucu `PaymentExceptionsController` (Repo Scope). İstisna
  kayıtları DL-2'de (SoD) oluşur; DL-1'de liste boş da olsa kapsam süzgeci AC-32 ile test edilir.
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` + `CT-DECISIONS.md` satırı ("kararı veren": Natig,
  2026-10-26 — D35) + `AuditTrailStandardTests` `AcceptedTrails` / `PinnedCounts` — üçü aynı değişiklikte (D35/4); borç / istisna 0.
- `docs/analysis/contracts/payment-batch.openapi.yaml` v1.0.0 (AC-29).
- **MOD-0220 §22 kodu DL-1 WP'sinde değil:** pack "Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler" listesinde; kod Ali'nin
  MOD-0023 kulesinin sırasındadır (Y-5/3). DL-1 yalnız tüketir (önceki sürümdeki "aynı dalga, ayrı yazar kapsamı" önerisi geçersiz).

## 4. DL-1 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir; prompt'un içermesi gerekenlerin listesidir. Biçim örneği main'de
`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri):
`execution/domains/finance-treasury/work-packs/prompts/WP-0126-DL1-v1.0.md`. Kuyruk satırı `WP-0126-DL1` (CT-QUEUE `PLANNED`,
bu dalda 242ef03df).

**Başlık satırları**

| Alan | Değer (taslak) | `ready-for-dev` günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]`; **ilk adım** `@orchestrator /add-module` ile `Diten.FinanceTreasuryService` servis iskeleti — aynı WP'de, ayrı ön WP yok (D51/3) | — |
| WP / Prompt | `WP-0126-DL1` · `TRM-WP-0126-DL1-P v1.0` | — |
| Repository · Branch · Expected HEAD · Worktree | `feature/fin/wp-0126-dl1` — her WP kendi dalında (D51/3); pack künyesindeki `feature/fin/mod-0126-payments-approvals` paket dalıdır, WP dalı değil | Expected HEAD = pack `ready-for-dev` onayının main'deki commit'i (S-53); worktree yolu |
| Risk | HIGH — §9.1 sinyalleri: kiracı, yetki, denetim, sözleşme, para | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kapsanan dilim | **yalnız DL-1** — başlama koşulu yalnız pack'in paket kapısı (işaretsiz ENGELLER); "— DL-n için" maddeleri bu WP'yi bekletmez (D51/1); SEC-002 kapsamı soran kod + AC-32'nin DL-1 uçları kısmı bu WP'nin kabul kapısında (D51/2); kabul kapısında ayrıca: Auth kayıt kodu main'de; testler gerçek izinle (D53/2); gerçek RCS-001 servisi (BL-565) main'de ve dev ortamında gerçek kod alındı (D59/1) | — |

**İzinli yollar** (pack Repo Scope, DL-1 satırları): `services/Diten.FinanceTreasuryService/**` (Api / Application / Domain /
Persistence / Infrastructure + `tests/`; `Features/PaymentBatch/**`, `Domain/PaymentBatches/**`, `Persistence/PaymentBatches/**`,
`Application/Common/{CommandReceipts,DataScope}/**`, `Persistence/Common/CommandReceipts/**`,
`Infrastructure/{LegalEntities,Currency,RecordCodes,DataScope}/**`, `Api/Controllers/{PaymentBatches,PaymentExceptions}Controller.cs`,
`Api/CentralAudit/FinanceTreasuryCentralAuditScope.cs`); `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md`,
`tests/architecture/audit-ledger/CT-DECISIONS.md` (tek satır), `AuditTrailStandardTests` sabitleri; `docs/analysis/contracts/payment-batch.openapi.yaml`.
`Infrastructure/{Workflow,Evidence,BankDispatch}/**` DL-2 / DL-3'tür — DL-1 prompt'una girmez.

**Korunan yollar** (pack Protected Paths): `.antigravity/**`, `AGENTS.md`, `gateway/**` (ocelot yalnız integration-agent; DL-4),
`services/Diten.Platform/**`, `services/Diten.MdmService/**` (MOD-0220 kodu dahil), `services/Diten.ProcurementService/**`,
`services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`, diğer servisler, `frontend/**` (DL-4),
`docs/analysis/contracts/` altındaki diğer üretici sözleşmeleri, `execution/**` (registries, domain-config, kuyruk, approvals),
kaynak depo ve handoff klasörü.

**Önce oku:** pack (`ready-for-dev` sürümü), AGENTS.md, `execution/domains/finance-treasury/domain-config.md`, `.antigravity/rules/`
multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent, code-style (S-56), audit-trail-standard,
data-scope-enforcement, record-coding-standard, mongo-indexing, handler-design, module-self-registration-standard, ports; karar
kayıtları `treasury-intake-owner-decisions-0{1..5}.md` (D1–D69; rol matrisi -05, D69 ile güncel), MOD-0018 Treasury Auth eki
(`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`, DL-1), bu hazırlık kaydı.

**Sözleşme yüzeyleri:**
- API: `payment-batch.openapi.yaml` v1.0.0 (AC-29) — uçlar ve hata kodları pack "Hata sözlüğü"nden alıntıyla.
- İzin: §2.6'daki DL-1 anahtarları; her aksiyonda `[HasPermission]` (AC-21).
- Test — **DL-1 Bitiş AC'leri** (`MOD-0126-payments-approvals.md:2120-2122` · paket @ `d86c2a56…a6dc`): AC-1, 2, 3, 39 (Create
  kısmı), 41 (a–c, e; Create / Update), 16 (Create kısmı), 17, 18, 20, 21, 22, 23, 29, 32 (DL-1 uçları kısmı: liste, detay, Create /
  Update / Delete / Validate — SEC-002 kapsamı soran kod + kanıt testi DL-1 kabul kapısı, D51/2), 33 (Create / Update / Delete /
  Validate kısmı), 45. Kısmi AC'lerin kalanı (paket @ `d86c2a56…a6dc`, `:2135`, `:2141-2142`): AC-39 Reconcile, AC-41 (d) Approve,
  AC-16 kararlar, AC-32 karar uçları, AC-33 Submit / kararlar / Reconcile → DL-2; AC-41 (d) Release (tam kapanış), AC-16 kalan,
  AC-32 Release ve kalan okumalar (tam kapanış), AC-33 Release / kanıt / onay belgesi → DL-3. AC metinleri prompt'a pack'ten tırnakla
  alıntılanır. Not: AC-17 DL-3 bitişinde de "17 (sabotaj)" olarak geçiyor; DL-1'de tek aşamalı komutların tek olayı + outbox
  sabotajı (Test Expectations), K2 sabotajı DL-3 — prompt bu ayrımı açık yazmalı.
- Sabotaj: AC-17 (outbox yazımı zorla başarısız → iş verisi yok), AC-23 (`IsDeleted=false` kısmi filtresi), AC-32 (kapsam süzgeci
  kaldırılınca), AC-33 (`TenantWideScope()` yazmaya sızınca), AC-39 (`approvalStatus` yoksa onaylı sayılınca / kimlik karşılaştırması
  kaldırılınca), AC-41 (b), (e).

**Gerekli testler:** birim (validatorlar, idempotency parmak izi, RCS port sahtesiyle kod biçimi); **gerçek Mongo replica set**
entegrasyonu (kiracı / şirket 404, AC-32, soft delete, idempotent tekrar ve ilk yanıtın kaybolduğu senaryo, CAS 409, CentralAudit
commit / rollback, `PaymentBatchCodeUniquenessTests`) — her test takımı kendi URI değişkenini okur, varsayılan porta düşmez,
replica set `enableTestCommands` ile; sahte MOD-0220 (AC-39 vaka listesi), sahte MOD-0048 (AC-41, para birimi hanesi);
mimari (`[HasPermission]`, banka alan adı yasağı, `float`/`double` para alanı yok, `AuditTrailStandard` — AC-18); sözleşme testi
(AC-29); build `Diten.FinanceTreasuryService` 0 hata; yerel kapıda `scripts/` Python testleri. Sahte istemciyle geçen test canlı
kanıt sayılmaz (pack kapanış tablosu).

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md` (+ `/add-module` kullanılıyorsa `.antigravity/agents/orchestrator.md`
  ve ilgili workflow), yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş** olarak: hedef servis + proje yolu (`services/Diten.FinanceTreasuryService/src/…`);
  aggregate / entity + alanlar (`PaymentBatch`, `PaymentInstruction`, `FinanceTreasuryCommandReceipt` — pack "Entity Fields");
  komut / sorgu listesi (Create, Update, Delete, BulkDelete, Validate; liste, detay, iz, istisna listesi); validator kuralları
  (`CreatePaymentBatchValidator`, `UpdatePaymentBatchValidator`, `GetPaymentBatchListValidator` — pack "Validation Rules"un alan kuralları); izin anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- `/add-module` alanları (iskelet bu WP'nin ilk adımı — D51/3): pack yolu + status, domain `finance-treasury`, servis `Diten.FinanceTreasuryService`,
  shell `tenant`, `golden_reference: compact`, `form_field_count: 16`, dal, alt ajan zinciri.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa işçiye yol + SHA-256 ile verilir (S-52); inceleme kaydı commit'ten önce `prompt_gate.py review-check` ile denenir (S-54);
HIGH iş — kabul kanıtı betiğini CT yazar, kırmızı tabanı kaydeder, prompt'a yol + SHA-256 ile verir (S-83); Expected HEAD
`ready-for-dev` günü doldurulur, pack onayı değişirse prompt yeniden tabanlanır ve incelenir (S-53); dalga sonunda tek PR (S-76,
S-77). Rapor biçimi SOP §22.

**Keşif:** `python3 .antigravity/scripts/verify_module_id.py . --discover "payments approvals"` raporu, yol@commit + sha256, keşif
kararları (parent / child / yeni) — dağıtım günü koşulur.

**Kod alma izi:** §2'deki **al-uyarla** satırları; her biri WP raporunda `path@da4abb20 + sha256`.

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git show`); kaynak düzeltmesi yok. Kaynak testler **çalıştırılmadı**.
- Recovery'de hiçbir test ya da build koşulmadı; ölçümler dosya okuma ve `grep` ile @904dfd5c2.
- Rol şablonlarının Auth'taki karşılığı, MOD-0048 seed'lerinin zamanı, BL-565 çağrı sözleşmesi ve `Finance = 18` tarihi
  sahiplerden gelmedi — ölçülemez. (2026-10-28 eki: BL-565 v0 sözleşmesi D59/1 ile geldi, kesin hali 6 Kasım; MOD-0048 seed'lerini Treasury CT hazırlıyor, D59/2 + D60.)
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.
