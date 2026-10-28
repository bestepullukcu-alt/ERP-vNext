---
record_id: MOD-0217-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/master-data-management/module-packs/MOD-0217-contract-lifecycle-management-clm.md (status draft, approval_status not-approved, sha256 aaf7eacd7a96f2b187a85c3650a0447a2f6bab46d25e202820f75113e68368fb (pack D75 revizyonu, 2026-10-28 — Auth aday sorgusu kimliği KARAR D74/1'e çekildi (ortak sır + iletilen kullanıcı token'ı, servis kimliği kullanılmaz; belge deposu servis kimliği D56/2 değişmedi); aynı anahtarla Submit tekrarı ilk aday listesini kullanır (D75/2 (b)); listenin saklandığı yer yeni açık karar O-16; yeni AC yok, AC genişletilmedi, yeni hata kodu yok; önceki sürüm `3bc065c5…de91` — pack D71 revizyonu, 2026-10-28 — D71 aday tabanı `mdm.contracts.versions.approve ∩ platform.workflow.tasks.approve` (anahtar başına ayrı DL-6 çağrısı), K-A4b kapandı, AC-33 genişletildi; önceki sürüm `7fb0eef9…` (pack D70 revizyonu, 2026-10-28 — D70/2 boş aday listesi 422 `MDM_CONTRACT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (AC-33), D70/1 sözleşme onaylayıcı iş akışı izinleri rol şablonuyla (K-A4 kapandı, K-A4b açık), D70/3 Auth paket kapısı; önceki sürüm `de6f15f9…` (pack KARAR D69 revizyonu, 2026-10-28 — sözleşme sürümü onay anahtarı `mdm.contracts.versions.approve`, O-3 (b) kapandı; önceki sürüm `7e6b9520…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `4c4bf518…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `f5795529…` (D58/D59 revizyonu); D56 sürümü `647a7679…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `76e502d6…` içindir)), dal chore/ct/treasury-intake-ct-05, "Revizyon (2026-10-27, D45/2 — Codex F-1)"; önceki a83901f9…; satır atıfları @00d3f2b41 sürümüne (3fa635e6…f6a9); main @904dfd5c2 sürümü b250d7a7…35e1)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; yalnız başvuru malzemesi — KOD ALMA YOK, D38/3; pack "Kaynak ve kanıt")
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve MOD-0217'nin D66 sürümü dahil; bu pack sürümü (D69) ve D69 main'de **değil** — yerel commit `db69732cb`).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU numarası açık — K-1). Taslak main'de ama `draft` ve teyitsiz — pack'teki "Auth paket metni
> main'de" maddesi işaretsiz, paket kapısı açık (D53/1). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-3**'ü (`mdm.contracts.*` 18 anahtar, 4 rol, 1 açık atama — D69 dahil;
> koşullu: MOD-0217 onayından sonra); kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **SEC-002:** ekin **DL-4**'ü (KARAR D56/1; Platform dosyaları için XMC-001 Beste — D63/2): paket metni paket kapısı, kodu
> DL-1 kabul kapısı. 2026-10-27 ölçümü: bugün çalışmaz (modül kodu iki tarafta `product-item-sku-master`'a sabit).
> - **MOD-0023 şablon kodu geldi:** `MDM-CONTRACT-VERSION-APPROVAL` (Y-12; W-5, W-6 — `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, #277 ile
> main'de; MOD-0217 onayından sonra kullanılır) — tek aşama, tek onay adımı, atamasız, ayrı hukuk adımı yok. Şablonu kuran MOD-0023
> kodu main'de **değil** (O-3 (a)); örnek durum adları DL-3 başında (O-3 (c)) — DL-3 dilim kapısı.
> - **Onaylayıcı aday tabanı** = ekin **DL-6** aday sorgusu (D66/1; D67/1 — DL-6 paket metni DL-3 önkoşulu, kodu DL-3 kabul
> kapısı). **Sorgunun anahtarı `mdm.contracts.versions.approve` — KARAR D69** (O-3 (b) kapandı): yeni rol "Sözleşme onaylayıcı"
> (`mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve`; açık atama değil); sürümü hazırlayan, şirket
> kapsamı dışı ve anahtarı olmayan kullanıcı aday olmaz (AC-33 genişledi); boş liste MOD-0023'te 400 → kesin ret (AC-53). Rolün
> MOD-0023 görev anahtarları D69'da yazılmadı — **D70/1 ile kapandı** (rol şablonuyla taşır; ek K-4 (b), K-A4 kapandı; elle atamada açık nokta K-A4b — **KARAR D71 ile kapandı**: aday listesi `platform.workflow.tasks.approve` ile de kesiştirilir). D69 main'de değil (D62/1: ekin DL-3 paket metninin D69
> kısmı onu bekler).
> - **Belge deposu:** MdmService servis kimliğiyle (KARAR D56/2 — D41/10'un MOD-0262 kısmının ve D46a'nın yerine); kullanıcılara
> `platform.document-repository.*` verilmez; MOD-0262-FU01'de servis kimliği desteği DL-2 dilim kapısı (Platform — Beste).
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol; MOD-0217 18 anahtar / 4 rol.
>
> **Güncel durum (2026-10-27).** İlk hazırlık kaydı. Pack'in çalışma ağacındaki sürümüne bağlıdır: sha256 `3fa635e6…a9`,
> `status: draft`, `approval_status: not-approved` (`MOD-0217-contract-lifecycle-management-clm.md:11-12@00d3f2b41`), dal
> `chore/ct/treasury-intake-ct-05` @00d3f2b41 (= main @904dfd5c2 + beş yerel docs commit'i; kod ve kural yolları main ile aynı).
> Bu sürüme CT-APR-02 ve CT-APR-01 satırı yok. Pack'e bölüm adıyla ya da `yol:satır@00d3f2b41` ile atıf yapılır; pack
> `ready-for-dev` revizyonunda yeniden değişirse atıflar o sürümde yeniden doğrulanır (S-53).
>
> **D51 sonrası (2026-10-27).** Pack "Revizyon (2026-10-27, D51)" (`:150-156@00d3f2b41`): paket kapısı ↔ dilim kapısı ayrımı bu
> pakette zaten tek kuraldı (Revizyon 2, P2-c); "— DL-n için" maddeleri ayrı "Dilim kapıları" alt listesine toplandı. DL-1 yalnız
> paket kapısını bekler; "— DL-1 için" madde yok (`:1478`). D51/3 WP dalını yalnız MOD-0126 ve MOD-0289 için adlandırdı; MOD-0217
> için §4'teki dal adı öneridir.

> **Kod alma yok (D38/3).** Pack baş bloğu (`:24-26@00d3f2b41`) ve "Kaynak ve kanıt" (`:1322-1323`): kaynak (`S@da4abb20`) yalnız
> okunur, başvuru malzemesidir; kod Recovery'de, MDM düzeninde sıfırdan yazılır. Pack'teki "tasarım olarak alınır" ifadesi yalnız
> davranış / alan fikrinin yeniden yazılmasıdır. Bu kayıt §2'de **al-uyarla** kararı vermez; MOD-0289'un D45/3 izni MOD-0217'ye
> uygulanmaz. Pack'e göre kaynak kodu pack yazılırken **okunmadı** — `Dosya.cs:satır@da4abb20` atıfları paket ve
> CONTRACT-STATE'ten aktarılmıştır (`:59-61`); bu kayıt aşağıdaki 20 dosyanın sha'sını ölçtü ve beş atıfı okuyarak doğruladı.
>
> **D52 sonrası (2026-10-27).** Pack "Revizyon (2026-10-27, D52)" ile (sha256 `2ed52107…2ca0`): domain-config + kimlik
> kaydı (#242) ve RCS-001 §3 `CTR` satırı (#265) main'de → `[x]`; §17 rev 2 onay maddesi `[x]`; MOD-0048 satırı ölçümle hizalandı;
> "Kaynak ve kanıt" bu kaydın okumasını anar (kod alma yok, D38/3); Auth sıralama gerilimi Implementation Notes'ta (CT önerisi,
> sahip kararı gerekir); DL-1 WP dalı KARAR D52/1 (yukarıdaki "§4'teki dal adı öneridir" cümlesi buna göre geçersiz).
> D53 sonrası: Auth paket metni paket kapısı, kayıt kodu DL-1 kabul kapısı (sıralama gerilimi D53 ile kararlandı — §5 bulgu 3).

# MOD-0217 DL-1 hazırlığı — sözleşme çekirdeği, denetim izi genişletmesi ve paket iskeleti

Bu kayıt, MOD-0217 `ready-for-dev` olduğunda DL-1 WP prompt'unun aynı gün yazılabilmesi için **neyin yeniden yazılacağını**,
**kaynakta hangi davranışın referans alınacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez.
Prompt, paketin `ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve
prompt kapısından geçer.

**DL-1 kapsamı** (pack "Teslim dilimleri", `:1471-1483@00d3f2b41`): `Domain/Entities/{Contract,ContractVersion}.cs`, enum'lar,
`Features/Contract/**` (Create, Update, Delete, BulkDelete, UpdateContractVersion, liste / detay / sürüm sorguları), depolar,
indeksler, işlem grubu deposu, `ContractsController` (bu aksiyonlar), manifest sağlayıcısı, RCS / MOD-0048 portları, denetim izi
genişletmesi, defter + CT-DECISIONS satırı. Yeni servis ve port yok (MDM 5059, D38/1). Belge (MOD-0262-FU01) DL-2, onay / imza
(MOD-0023) DL-3, ekran DL-4. **MOD-0126'ya bağımlılık yok** — Treasury servis iskeletini beklemez.

## 1. Başlama koşulları (ölçüm: çalışma ağacı @00d3f2b41 = main @904dfd5c2 + yerel docs commit'leri, 2026-10-27)

Kaynak: pack "Ready-for-dev Checklist" paket kapısı (`:1257-1291@00d3f2b41`), dilim kapıları (`:1293-1310`), DL-1 Önkoşul metni
(`:1475-1478`). "DL-1'e etkisi": **bekletir** = işaretsiz paket kapısı maddesi; **DL-n** = "— DL-n için" dilim kapısı.

| Koşul | Sahibi | Bugün | DL-1'e etkisi |
|---|---|---|---|
| Pack `ready-for-dev` + o sürüme CT-APR-02 | Treasury CT + bağımsız inceleyici | `draft` / `not-approved`; güncel sha `3bc065c5…` için `reviews.tsv`'de satır yok (son MOD-0217 incelemesi `78657a26…`, `execution/approvals/reviews.tsv:83`) · **2026-10-28:** güncel sha `3bc065c5…` (D71) için satır yok; son satır `76e502d6…` | bekletir |
| CT-APR-01 sürüme bağlı onay (paketi yazmamış onaylayıcı) | Beste | `approvals.tsv`'de MOD-0217 satırı yok (grep 0) | bekletir |
| MOD-0217 bir DCP'nin onaylı üyesi ve o DCP `approved` | Beste | `DCP-011-finance-treasury-intake.md:7` `status: draft`; MOD-0217 yalnız **aday** (`:17` `candidate_modules: [MOD-0217]`, `:90`; dilim TRM-S4 `:150`) — üyelik DCP değişikliği ister | bekletir (`:1265`) |
| MDM domain-config satırı + kimlik kaydı satırı main'de | CT (D38/2) | **main'de var (ölçüldü)** — `execution/domains/master-data-management/domain-config.md:49-53@00d3f2b41` (yerel commit'ler bu satırı değiştirmedi; diff yalnız MOD-0289 satırı), `execution/registries/module-id-registry.md:155@00d3f2b41` (`reserved / planned`). Pack checkbox'u D52 ile `[x]` (§5 bulgu 1 kapandı) | — (kapandı) |
| Auth paket metni — MOD-0018 paketi (anahtarlar `mdm.contracts.*`, açık atama listesi `read-tenant-wide`, rol şablonları D46–D50) | Treasury yazar, MOD-0018 sahibi (Ali) teyit eder (Y-15, Y-16; önceki: Y-2, Y-6) | **2026-10-28: taslak main'de** (#281; origin/main @ `027ca2e38`) — `…/MOD-0018-FU-TBD-treasury-permission-onboarding.md` `draft`, FU numarası açık (K-1), MOD-0018 sahibinin teyidi yok, pack checklist maddesi işaretsiz; Y-14 geçersiz (kullanılmadı); ölçüm anında: **yok** — `MOD-0018-*` dosyalarında `mdm.contracts` 0 | paket kapısı (D53/1) — DL-1'i bekletir (pack checklist D53'e göre iki maddeye bölündü — kapandı) |
| Auth kayıt kodu — `mdm.contracts.*` kaydı (MOD-0217 onayından sonra; MOD-0018 Treasury Auth eki DL-3 kodu) | Treasury yazar, MOD-0018 sahibi teyit eder (Y-15, Y-16) | **yok** — `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`'te `mdm.*` 0; `services/**`'te `mdm.contracts` 0 (grep) | DL-1 kabul kapısı (D53/2) — DL-1 başlamasını bekletmez, kabulünü bekletir |
| RCS-001 §3 `CTR` satırı main'de | — | **main'de var (ölçüldü)** — `.antigravity/rules/record-coding-standard.md:70@00d3f2b41` (`.antigravity/**` yerel commit'lerde değişmedi; PR #246 birleşmiş). Pack checkbox'u D52 ile `[x]` (§5 bulgu 1 kapandı) | — (kapandı) |
| RCS-001 ortak kod servisi (BL-565) | Beste (D44) | **yok** — BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9473-9477@00d3f2b41`; sahip alanı "CT", D44 ile uyumsuz); `services/**`'te kod servisi yok; MDM'deki `CodeReservation` ürün ailesinin (pack Dependencies `:487`) | **DL-1 kabul kapısı (D59/1)** — başlamayı bekletmez; DL-1 `IRecordCodeAllocator`'a karşı sahte uygulamayla yazılır, gerçek servis DL-1 kabulünden önce main'de (v0 sözleşmesi Y-8, 6 Kasım); üretimde port bağlı değilse 503 `MDM_CONTRACT_RECORD_CODE_UNAVAILABLE` |
| SEC-002 kapsam sağlayıcısı (ilk sürümde: "`mdm.contracts.*` anahtarlarıyla çalıştığı ölçüldü") | MOD-0018 Treasury Auth eki DL-4 (KARAR D56/1): Treasury yazar, MOD-0018 sahibi teyit eder; XMC-001 Beste (Platform, D63/2) + MDM sahibi | **2026-10-28 (D56/1):** ölçüm maddesi değil — ek DL-4 paket metni paket kapısı (D53/1 deseni), ek kodu DL-1 kabul kapısı; 2026-10-27 ölçümü olumsuz (bugün çalışmaz — DL-2 kaydı §1). İlk ölçüm anında: **ölçülmedi.** Okunan: MDM portu `ITrustedLegalEntityScopeProvider.ResolveAsync(tenant, subject, moduleCode, permissionKey)` (`services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs:3-11@00d3f2b41`), Platform istemcisiyle kayıtlı (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs:112@00d3f2b41`); Platform tarafının bu anahtarlar için ne döndürdüğü okunmadı — **kısmi inceleme** | paket kapısı: ek DL-4 paket metni (D56/1) — DL-1'i bekletir; ek kodu DL-1 kabul kapısı |
| `AuditCategory` değeri | — | **kapandı — D45/1: `MasterData`** (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:19@00d3f2b41`, değer 14; `treasury-intake-owner-decisions-05.md:18-21`); `Finance = 18` MOD-0217'ye uygulanmaz | — |
| MOD-0048 para birimi listesinin MdmService'ten tüketim yolu | — | **kapandı (ölçüm)** — bugün okuyucu yok; yol `GatewayReferenceValueReader` deseniyle yeni port (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/GatewayReferenceValueReader.cs:16,30,45-49@00d3f2b41` — `Gateway:BaseUrl`, `X-Tenant-Id` iletimi); portu ilk tüketen dilim (DL-1) yazar (`:1285-1289`) | — (portu DL-1 yazar) |
| O-1 (D39), O-2 (D40), O-4…O-15 (D41), O-11 `OTHER` (D45/2), O-6 defter (D41/3) | — | **kapandı** (`:1267-1291`) | — |
| MOD-0262-FU01 sözleşmesi kodda ölçüldü | — | **kapandı** — pack ölçümü @7c57e8466; bugün de uçlar `DocumentRepositoryController.cs:40,59,87,97,116@00d3f2b41` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/`), izinler `platform.document-repository.read` / `.manage`; pack `MOD-0262-FU01-document-binary-store.md:10` `ready-for-dev`, `:20` `runtime_implementation: in-progress` | DL-2 |
| MOD-0262-FU01 canlı doğrulama kaydı (yükle / indir / başka kiracı 404) | Treasury CT | yok | DL-2 |
| O-13 çağrı kimliği | — | kapandı (D41/10); MOD-0262 kısmı **KARAR D56/2** ile servis kimliği — MOD-0262-FU01'de servis kimliği desteği DL-2 dilim kapısı (Beste) | DL-2 |
| O-3 MOD-0023 MOD-0217 şablonu + örnek durum adları | Ali (D38/4, Y-3) | **yok** (2026-10-28, Y-12: kod `MDM-CONTRACT-VERSION-APPROVAL` geldi, MOD-0217 onayından sonra kullanılır; kuran kod main'de değil — O-3 (a); aday tabanı D66/1, anahtar `mdm.contracts.versions.approve` — **KARAR D69**, O-3 (b) kapandı; durum adları DL-3 başında — O-3 (c)) | DL-3 |
| MOD-0023 §17 rev 2 onayı | — | **onaylı** — `execution/approvals/approvals.tsv:16` (`803fbaa5…` = MOD-0023 pack'in bugünkü sha'sı); pack checkbox'u D52 ile `[x]` (§5 bulgu 1 kapandı). Okuma alanlarının main'de olması ve AC-W2/W12/W13 DL-3 kabul kapısında | DL-3 |
| MOD-0023 başlatma hata gövdesi kodları ölçümü; O-8 imza modeli | Treasury CT / — | ölçülmedi; O-8 kapandı (D41/5) | DL-3 |
| MDM domain-config frontend / gateway kapsam cümlesi; Gateway rotaları | MDM sahibi / integration-agent | domain-config bugün yalnız MOD-0290-FU02 için istisna yazıyor (`domain-config.md:59-65`); rota yok | DL-4 |

**DL-1'i ilk bekleten:** dış sahipli ve tarihsiz madde — **MOD-0018 Treasury Auth ekinin MOD-0018 sahibince teyidi** (taslak #281 ile
main'de; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16; FU numarası K-1): ekin DL-3'ü (`mdm.contracts.*`, 18 anahtar / 4 rol —
D69 dahil) ve DL-4'ü (SEC-002, D56/1) bu paketin paket kapısıdır. Auth kayıt kodu (`mdm.contracts.*`, MOD-0217 onayından sonra) ve ek
DL-4 kodu başlamayı değil DL-1 kabulünü bekletir (D53/2, D56/1). 2026-10-27'deki iki CT maddesi kapandı: SEC-002 ölçümü olumsuz çıktı
ve D56/1 ile ek paketine taşındı; eskimiş checkbox'lar D52 ile `[x]`. **DCP-011 üyeliği** Beste'nin DCP değişikliğini ister.
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-1) — yalnız referans

Kaynak MOD-0217 kodu kaynak MDM servisindedir: `services/DitenMdmService/src/…` (kısaltma: `M/`). Özet: `shasum -a 256` (ilk 16
hane), satır sayısı `wc -l`, ikisi de @da4abb20, 2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext archive da4abb20 <yol…>`
ile salt okunur ölçüldü (20 dosya; pack'in atıf verdiği 13 dosya + `Domain/Entities/` altındaki 5 sözleşme varlığı + iki kaynak
yazma dosyası). **Okunarak doğrulanan pack atıfları:** `ContractsController.cs:15-16` (`api/contracts`, `api/v1/contracts`),
`Mod0217ContractAggregateStore.cs:38` (`CreateWithReceiptAsync`), `ContractQueryHandlers.cs:204` (pencere varsayılan 30, en çok
180), `Mod0217Permissions.cs` (16 sabit = 12 anahtar + 4 değer takma adı), `Mod0217VerifiedRequestContext.cs:43` (`X-Tenant-Id`
başlık karşılaştırması). Karar dili: **yalnız referans** = davranış / alan fikri okunur, kod alınmaz, Recovery'de yeniden yazılır;
**alma** = pack "Taşınmayacaklar" (`:1364-1394`, "madde N"); **sonraki dilim**. **al-uyarla yok (D38/3).**

**Özet (yeniden yazılacak / referans / alınmayacak):**

| Recovery'de sıfırdan yazılacak (DL-1) | Kaynakta referans alınacak davranış | Alınmayacak |
|---|---|---|
| `Contract` (başlık, karşı taraf başlıkta, `RenewalDecisions[]` gömülü — DL-1'de yalnız alan), `ContractVersion` (sürüm 1, taslak sürüm güncelleme); `ContractStatus`, `ContractVersionStatus`, `ContractCounterpartyType`, `ContractVersionKind`, `ContractDocumentRole`; Create / Update / Delete / BulkDelete / UpdateContractVersion; liste / detay / sürüm liste / sürüm detay; `ContractModels.cs`; depolar + işlem grubu deposu; yedi indeks; manifest sağlayıcısı; RCS ve MOD-0048 portları; denetim izi genişletmesi | `Contract` / `ContractVersion` / `ContractAmendment` / `ContractPartyLink` alanları (pack "Varlıklar" #1-5); liste süzgeçleri ve `expiringWithinDays` penceresi; doğrulayıcı alan kuralları (pack "Validation Rules" kazanır) | kaynak rotalar ve Gateway blokları, port 5050; eylem süzgeci kimlik koruması ve `X-Tenant-Id` karşılaştırması; `contract.*` anahtarlar ve takma adlar; denetim rölesi ve `Unavailable*` varsayılanları; Create makbuzu / `create-attempts`; geri çağrı uçları ve gelen kutusu; kendi iş akışı istemcisi; HTTP olayları; web katmanında yerel klasöre yükleme; `mod0217_*` koleksiyon / `uq_mod0217_*` / `idx_mod0217_*` indeks / `Mod0217*` tip / `MOD0217_*` hata adları; çok boyutlu durum modeli; toplu eylemler; kaynak test sayıları |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `M/Diten.MdmService.Domain/Entities/Contract.cs` | `97ef4192d6bcb8f5` · 120 | yalnız referans | `Domain/Entities/Contract.cs : EntityBase` | pack "Varlıklar" #1: 51 alanın çoğu tek durum modeline ve sürüme dağılır; karşı taraf başlıkta (v1 tek karşı taraf, D41/4 — `Supplier` türü kapalı, AC-60); iç sorumlu yalnız oluşturan (AC-61); `TenantId` + `GoverningLegalEntityId` (veri kapsamı) |
| `M/Diten.MdmService.Domain/Entities/ContractVersion.cs` | `337dd2d7a6bdf9fc` · 64 | yalnız referans | `Domain/Entities/ContractVersion.cs : EntityBase` | ticari koşullar, imzacılar sürümde; ticari değer `value.read` / `value.write` ile (D41/9, D49/2) |
| `M/Diten.MdmService.Domain/Entities/ContractAmendment.cs` | `38e30644579fce8b` · 56 | yalnız referans → birleşir | `ContractVersion` (`Kind = Amendment`) | değişiklik komutu DL-2 |
| `M/Diten.MdmService.Domain/Entities/ContractPartyLink.cs` | `7befbeb40c7c51a6` · 22 | yalnız referans | başlıkta karşı taraf, sürümde gömülü imzacılar | — |
| `M/Diten.MdmService.Domain/Entities/ContractCreateReceipt.cs` | `1503770de03d7267` · 17 | **alma** | `Idempotency-Key` + `CreateIdempotencyKey` (D41/7) | madde 6 |
| `M/Diten.MdmService.Domain/Enums/Mod0217ContractEnums.cs` | `0476650d073c341f` · 204 | yalnız referans (tür ve durum adları) | `Domain/Enums/{ContractStatus,ContractVersionStatus,ContractCounterpartyType,ContractVersionKind,ContractDocumentRole}.cs` | çok boyutlu durum (onay / imza / kanıt / gizlilik / risk) **alma** (madde 14); `ContractType` listesinde `PURCHASE` yok (D39, AC-59), `OTHER` var (D45/2) |

### 2.2 Application ve Api

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `M/Diten.MdmService.Api/Controllers/ContractsController.cs` | `ca91ee74fe86d436` · 377 | yalnız referans (CLM-01 :40, CLM-03 :92, CLM-04 :102, CLM-05 :113 uçlarının şekli) | `Api/Controllers/ContractsController.cs` | rota :15-16 **alma** (madde 1) → `/api/mdm/contracts` (`/api/contracts` MOD-0144'ündür); eylem süzgeci :28-38 ve eylem içi izin kontrolü :355, :370 **alma** (madde 2, 3); `lifecycle-preflight` :152 **alma**; her aksiyonda `[HasPermission]` (AC-16) |
| `M/Diten.MdmService.Application/Features/Mod0217/Contracts/ContractCommandHandlers.cs` | `4cca82fe84443ee2` · 2368 | yalnız referans (Create / Update kuralları) | `Features/Contract/{Commands,Handlers,Validators}/**` (Golden Reference Compact) | kurallar pack "Validation Rules" ve "Runtime Constraints"e göre yeniden yazılır (`handler-design`); makbuz, geri çağrı, röle, olay yayını yolları **alma** |
| `M/…/Features/Mod0217/Contracts/ContractCommandValidators.cs` | `2c4184cc93c312ae` · 66 | yalnız referans | `Validators/CreateContractValidator.cs`, `UpdateContractValidator.cs`, `UpdateContractVersionValidator.cs`, `GetContractListValidator.cs` | pack kazanır; `MOD0217_CREATE_*` literalleri alma |
| `M/…/Features/Mod0217/Contracts/ContractQueryHandlers.cs` | `85f71ba77be99002` · 233 | yalnız referans (`expiringWithinDays` penceresi :204) | `Queries/GetContractListQuery.cs` + `ContractListSort.cs`, `GetContractByIdQuery`, `GetContractVersionListQuery`, `GetContractVersionByIdQuery` | DataTable sunucu tarafı, sayfa ≤ 200; kapsam süzgeci `IContractDataScopeSource` (SEC-002, AC-6 DL-1 uçları kısmı); değer maskesi (`value.read` yoksa `null` + `valueMasked`) ve değer okuma niyeti (AC-63) |
| `M/…/Features/Mod0217/Contracts/ContractModels.cs` | `5b92aef68f459cb9` · 126 | yalnız referans | `ContractModels.cs` (tek dosya; pack "Owned Objects" DTO listesi) | DTO'larda `TenantId` yok |
| `M/Diten.MdmService.Application/Authorization/Mod0217Permissions.cs` | `1ed0a000a78382c5` · 22 | **alma** | `mdm.contracts.*` (§2.6) | madde 4 — 12 anahtar + 4 takma ad (ölçüldü: 16 sabit) |
| `M/Diten.MdmService.Infrastructure/UserContext/Mod0217VerifiedRequestContext.cs` | `6cf5b50f6293e9c3` · 52 | **alma** | MDM standart kiracı çözümü + JWT `sub` | madde 2 (`X-Tenant-Id` karşılaştırması :43) |
| `M/Diten.MdmService.Infrastructure/Services/Mod0217IntegrationServices.cs` | `fc8227718ff49bb7` · 161 | **alma** | — | madde 9 (işlem sonrası HTTP olayları) |
| `services/Diten.Platform/src/Diten.Platform.API/Controllers/Mod0217AuditRelayController.cs` | `ad1b7a812d83f111` · 32 | **alma** | MDM `mdm-guvenilir-kaynak-niyeti` yolu | madde 5 |
| `frontend/Diten.Web/Controllers/ContractsController.cs` | `b6fb0cf5c7da2c99` · 1820 | **alma** (DL-4 dahi) | `Controllers/MasterData/ClmContractsController.cs` (DL-4) | madde 10 (yerel klasöre yükleme :712-748); Recovery'de bu sınıf adı MOD-0144'ündür (Protected Paths `:469`) |
| `gateway/DitenApiGateway/Diten.ApiGateway/ocelot.json` | `e2e4c94d988d4227` · 2323 | **alma** | integration-agent (DL-4) | madde 1 (`AuthenticationOptions`'sız bloklar) |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `M/Diten.MdmService.Persistence/Repositories/Mod0217ContractAggregateStore.cs` | `fac6e7c7648a6d48` · 465 | yalnız referans (işlem grubu fikri) | `Persistence/Repositories/{Contract,ContractVersion}Repository.cs` + `ContractAggregateStore.cs` (`MasterDataAtomicWrite`) | `CreateWithReceiptAsync` (:38) üretimde çağrılmaz — **alma** (madde 6); Create = sözleşme + sürüm 1 + iki niyet tek işlemde (AC-4, AC-13); kaynak işlem iddiası alıntılanmaz (TRM-11) |
| `M/Diten.MdmService.Persistence/Repositories/Mod0217CallbackTransactionStore.cs` | `a78343ee553eee64` · 79 | **alma** | — | madde 7 |
| `M/Diten.MdmService.Persistence/Configurations/MongoDbIndexConfigurations.cs` | `83346734fd997073` · 466 | yalnız referans (7 `mod0217_*` koleksiyonu, pack `:219-220`) | MDM depo kurucusu deseniyle indeks kurulumu (`mongo-indexing`) | `uq_mod0217_*` / `idx_mod0217_*` adları **alma** (madde 13); kaynak veri taşınmaz |

**DL-1 veritabanı tekil indeksleri (pack "Mongo indeksleri", `:384-398@00d3f2b41`):**

| İndeks | Anahtar | Tekil · kısmi süzgeç | Test + sabotaj |
|---|---|---|---|
| `ux_mdm_contract_code` | TenantId + Code | **Unique, kısmi filtre YOK** — silinen sözleşmenin kodu da dolu (RCS-001 §6) | aynı kiracıda aynı kodla ikinci kayıt (silinmişin koduyla dahil) reddedilir; sabotaj: `IsDeleted=false` eklenince kırmızı. Kodun RCS'den gelmesi DB tekilliğini kaldırmaz |
| `ux_mdm_contract_create_idempotency` | TenantId + CreateIdempotencyKey | Unique, kısmi `$type: string` (`$ne` / `$not` yok) | aynı anahtarla eşzamanlı iki Create → tek sözleşme (gerçek replica set; AC-10 / AC-14 DL-1 kısmı) |
| `ux_mdm_contract_version_no` | TenantId + ContractId + VersionNo | Unique | sürüm numarası yarışı (DL-1'de sürüm 1; değişiklik DL-2) |
| `ux_mdm_contract_version_workflow_key` | TenantId + WorkflowIdempotencyKey | Unique, kısmi `$type: string` | DL-3'te dolar; tanım DL-1 dilim yolunda (öneri: DL-1'de kurulur, testi DL-3) |
| `ix_mdm_contract_le_status`, `ix_mdm_contract_effective_to`, `ix_mdm_contract_owner` | `:390-392` | — | liste + veri kapsamı + `expiringWithinDays` |

İki `DateTimeOffset` alanı aynı indekste birlikte sıralanmaz (`:396`).

### 2.4 Testler (yalnız referans değil — **kaynak testleri kanıt değildir**)

Pack "Test Expectations" (`:1220-1223`): kaynakta MOD-0217'nin 18 işlem testi URI eksikliğinden koşmadı, 6'sı başarısız (TRM-11);
hiçbiri güvence olarak alıntılanmaz, bütün testler Recovery'de yazılır (Level-1 koşul 1). Bu kayıt kaynak test dosyalarını
ölçmedi.

### 2.5 DL-1'in paylaştığı MDM yolları (MOD-0289 ile çakışma)

Denetim izi genişletmesi MOD-0289 DL-1 ile **aynı dosyalara** eklenir (pack Repo Scope `:437-441`):
`services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs` (bugün dokuz değer, `:3-14@00d3f2b41` —
MOD-0217 `Contract`, `ContractVersion` ekler; MOD-0289 DL-1 `Bank`, `BankBranch` ekler, D51/4), eklemeli
`MasterDataAuditOperation` (bugün kodda **yok** — `Domain/Enums/` altında yalnız `ProductAuditOperation`; "hangi paket önce koda
girerse o ekler") ve `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs`
koleksiyon listesi (bugün sabit dokuz, `:15-23@00d3f2b41`). İki WP aynı dalgada koşarsa sıra / birleştirme CT kararıdır (öneri:
önce giren `MasterDataAuditOperation`'ı açar, ikincisi yalnız ekler; enum sayısal değerleri çakışmasın diye ikinci WP tabana
yeniden alınır).

### 2.6 Yetki, roller ve denetim kategorisi (D41/1, D45/1, D46, D47, D49/2)

Pack "Authorization Convention" (`MOD-0217-contract-lifecycle-management-clm.md:846-889@00d3f2b41`) ve işleyen tek matris "D47 rol
matrisi (CT04a; D49 ile güncellendi — işleyen tek matris)" (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-206@00d3f2b41`).

- **DL-1 anahtarları** (`:860-866`): `mdm.contracts.read` (T1), `.read-tenant-wide` (T3, yalnız açık atama, yalnız okuma),
  `.create`, `.update` (T1; UpdateContract + UpdateContractVersion), `.delete` (T1), `.bulk-delete` (T2), `mdm.contracts.value.read`
  (T1, yalnız görme), `mdm.contracts.value.write` (T3, komut iznine **ek**, handler içi — D49/2). Belge anahtarları DL-2;
  `submit-approval`, `record-signature`, `amendments.create`, `renewal-decisions.create`, `terminate`, `archive` DL-2 / DL-3.
  Kaynak `contract.*` anahtarları alınmaz.
- **Test kişileri matristen** (`treasury-intake-owner-decisions-05.md:199-201`): okuma → **Sözleşme görüntüleyici** (`read`,
  `documents.read`; `value.read` **yok** → değer maskeli döner, AC-63'ün maskeli kolu); yazmalar ve değer → **Sözleşme
  yöneticisi** (create / update / delete / bulk-delete, `value.read`, `value.write`). **Sözleşme imza kaydedici** ve **Sözleşme onaylayıcı** (D69: `read`, `documents.read`, `mdm.contracts.versions.approve`; AC-33 aday
  testi) DL-3'tür.
  AC-65'in 403 `MDM_CONTRACT_VALUE_WRITE_REQUIRED` kolu için matriste hazır rol yok (update var, `value.write` yok) — test kişisi
  izin listesiyle kurulur (öneri; prompt'ta açık yazılır).
- **`mdm.contracts.read-tenant-wide`** açık atama listesinde (`treasury-intake-owner-decisions-05.md:144`); hiçbir şablona
  bağlanmaz.
- **D47/3:** değer okuma denetimi v1'de (`mdm.contract.value-read`, yanıttan önce, niyet yazılamazsa 503) — AC-63 DL-1'de.
  **D47/5** imza kaydedici ayrılığı (AC-64) DL-3. **D50** MOD-0217'yi etkilemez.
- **Denetim kategorisi:** `AuditCategory.MasterData` (14) — D45/1; pack Audited Events `:902-903`. Yol `b`,
  `mdm-guvenilir-kaynak-niyeti` genişletmesi (D41/3; bugün defterde `tests/architecture/audit-ledger/Diten.MdmService.md:14@00d3f2b41`);
  K2 = onay sonucu, imza, fesih (DL-3); DL-1 olayları K2 değil ama aynı kapalı-başarısız niyet yolundan.

## 3. DL-1'de yeni yazılacaklar (hepsi Recovery'de sıfırdan)

Yollar pack "Repo Scope" (`:422-445@00d3f2b41`) ve "Teslim dilimleri" DL-1 satırı (`:1471-1483`); hepsi `services/Diten.MdmService/`
altında.

- `Domain/Entities/{Contract,ContractVersion}.cs` (`EntityBase`, `IAuditIntentAggregate`), `Domain/Enums/{ContractStatus,
  ContractVersionStatus,ContractCounterpartyType,ContractVersionKind,ContractDocumentRole}.cs`, `Domain/Repositories/I{Contract,
  ContractVersion}Repository.cs`.
- `Application/Features/Contract/**` Compact düzeni: `CreateContractCommand` (sözleşme + sürüm 1, tek işlem, `Idempotency-Key`
  zorunlu), `UpdateContractCommand`, `DeleteContractCommand`, `BulkDeleteContractCommand` (yalnız hiç onaya gönderilmemiş taslak,
  soft delete), `UpdateContractVersionCommand`; `GetContractListQuery` (+ `expiringWithinDays`, varsayılan 30, en çok 180),
  `GetContractByIdQuery` (+ `allowedActions`), `GetContractVersionListQuery`, `GetContractVersionByIdQuery`; doğrulayıcılar;
  `ContractModels.cs`.
- Portlar: `IRecordCodeAllocator` (`CTR`, şirket öneki yöneten şirketin kısa kodu — D41/2; `Infrastructure/RecordCodes/**`),
  `ICurrencyReferencePort` (MOD-0048, `GatewayReferenceValueReader` deseni; `Infrastructure/Currency/**`), `IContractDataScopeSource`
  (SEC-002). Şirket için port yok (`ILegalEntityRepository` süreç içi okunur; yok / başka kiracı / silinmiş / `Active` değil → 422
  `MDM_CONTRACT_LEGAL_ENTITY_NOT_ELIGIBLE`).
- `Persistence/Repositories/{Contract,ContractVersion}Repository.cs`, `ContractAggregateStore.cs` (`MasterDataAtomicWrite`),
  §2.3 indeksleri; `If-Match` / `Version` CAS.
- `Api/Controllers/ContractsController.cs` (DL-1 aksiyonları), `Api/ModuleRegistration/ContractLifecycleManifestProvider.cs`
  (`BrandProductMasterManifestProvider.cs` deseni).
- Denetim izi genişletmesi (yalnız ekleme): `AuditAggregateType` (`Contract`, `ContractVersion`), `MasterDataAuditOperation` (yoksa
  açar), teslim deposu koleksiyon listesi; olaylar `mdm.contract.created` + `mdm.contract-version.created` (Create'te tam iki niyet),
  `mdm.contract.updated`, `mdm.contract.deleted`, `mdm.contract-version.updated`, `mdm.contract-version.value-written` (değer
  yazılınca, ek niyet), `mdm.contract.value-read` (okuma niyeti, yanıttan önce). Değer, tutar, metin niyete yazılmaz.
- `tests/architecture/audit-ledger/Diten.MdmService.md` yeni yazma üyeleri + aynı değişiklikte `CT-DECISIONS.md` satırı ve
  `AuditTrailStandardTests` sabitleri (D41/3; borç yok; AC-12).
- Hata sözlüğü DL-1 alt kümesi (pack "Hata sözlüğü" `:400-420`): `MDM_CONTRACT_NOT_FOUND`, `MDM_CONTRACT_VERSION_NOT_FOUND`,
  `MDM_CONTRACT_VALIDATION_FAILED`, `MDM_CONTRACT_CODE_FORBIDDEN`, `MDM_CONTRACT_IDEMPOTENCY_KEY_REQUIRED`,
  `MDM_CONTRACT_IDEMPOTENCY_KEY_REUSED`, `MDM_CONTRACT_VERSION_CONFLICT`, `MDM_CONTRACT_STATE_INVALID`, `MDM_CONTRACT_IMMUTABLE_FIELD`,
  `MDM_CONTRACT_LEGAL_ENTITY_NOT_ELIGIBLE`, `MDM_CONTRACT_COUNTERPARTY_NOT_ELIGIBLE`, `MDM_CONTRACT_COUNTERPARTY_TYPE_UNAVAILABLE`,
  `MDM_CONTRACT_INTERNAL_OWNER_NOT_ALLOWED`, `MDM_CONTRACT_VALUE_WRITE_REQUIRED`, `MDM_CONTRACT_CURRENCY_UNKNOWN`,
  `MDM_CONTRACT_CURRENCY_REFERENCE_UNAVAILABLE`, `MDM_CONTRACT_RECORD_CODE_UNAVAILABLE`, `MDM_CONTRACT_AUDIT_UNAVAILABLE`,
  `MDM_CONTRACT_TRANSACTION_UNAVAILABLE`; ileti resx anahtarı.

## 4. DL-1 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir; prompt'un içermesi gerekenlerin listesidir. Biçim örneği main'de
`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri; pack'te yok):
`execution/domains/master-data-management/work-packs/prompts/WP-0217-DL1-v1.0.md`. Kuyrukta `WP-0217-DL1` satırı **yok** (satır
`ready-for-dev` turunda CT tarafından açılır — bu kayıt kuyruğa yazmaz).

**Başlık satırları**

| Alan | Değer (taslak) | `ready-for-dev` günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — yeni servis yok (MdmService, D38/1); `/add-module` servis iskeleti adımı **yok**; WP "paket iskeleti" (manifest sağlayıcısı + denetim izi genişletmesi) ile başlar | — |
| WP / Prompt | `WP-0217-DL1` · `TRM-WP-0217-DL1-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/mdm/wp-0217-dl1` — **KARAR D52/1**; pack künyesindeki `feature/mdm/mod-0217-contract-lifecycle-management-clm` (`:14`) paket dalıdır, WP dalı değil | Expected HEAD = pack `ready-for-dev` onayının main'deki commit'i (S-53); MdmService commit SHA'sı DL-1 başında; MOD-0289 DL-1 aynı dalgadaysa sıra (§2.5); worktree yolu |
| Risk | HIGH — §9.1 sinyalleri: kiracı, veri kapsamı, denetim, kayıt kodu, yetki, işlem bütünlüğü | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kod alma | **yok (D38/3)** — §2 yalnız referans; WP raporunda "kaynaktan dosya alınmadı" beyanı ve okunan referans dosyaları `path@da4abb20 + sha256` | — |
| Kapsanan dilim | **yalnız DL-1** (belge, değişiklik, onay, imza, ekran, gateway **yok**) — başlama koşulu yalnız paket kapısı; "— DL-n için" maddeleri bu WP'yi bekletmez (O-3 (a) / (c) açıkken ilerler, `:1478`; O-3 (b) KARAR D69 ile kapandı) | — |

**DL-1 Bitiş AC'leri** (`MOD-0217-contract-lifecycle-management-clm.md:1478-1483@00d3f2b41`): AC-1, AC-2, AC-3, AC-4, **AC-5 (DL-1
uçları kısmı)**, **AC-6 (DL-1 uçları kısmı — SEC-002)**, **AC-7 (DL-1 yazmaları kısmı)**, AC-8, AC-9, **AC-10 (DL-1 komutları
kısmı)**, **AC-11 (DL-1 yazmaları kısmı)**, **AC-12 (DL-1 defter satırları kısmı)**, AC-13, **AC-14 (DL-1 yazmaları kısmı)**,
**AC-15 (DL-1 uçları kısmı)**, **AC-16 (DL-1 aksiyonları kısmı)**, **AC-17 (DL-1 alanları kısmı)**, **AC-18 (şirket uygunluğu
kısmı)**, AC-19, AC-20, **AC-21 (DL-1 okuma ve yazmaları kısmı)**, **AC-22 (DL-1 yazmaları kısmı)**, AC-23, AC-32, **AC-42 (DL-1
eylemleri kısmı)**, AC-59, AC-60, AC-61, AC-63, **AC-65 (DL-1 yazmaları kısmı: Create, UpdateContractVersion)**. "Kısmı" ile
işaretli AC onu listeleyen son dilimde tam kapanır (`:1463-1466`): AC-5/6/7/10/11/12/14/15/16/17/21/22/42 → DL-3; AC-18 → DL-3;
AC-65 → DL-2. AC metinleri pack'ten tırnakla alıntılanır.

**Dilim kabul kapısı — DL-1** (`:1518`): Bitiş AC'leri (kısımlarıyla); **gerçek replica set AC'leri (AC-4, AC-10, AC-13, AC-14)
kendi Mongo URI'li suite'te (AC-23)**; `AuditTrailStandard` (AC-12); bir sözleşme oluşturmanın merkezi denetim kaydı (dev ortamı,
AUD-001 §8); `Diten.MdmService` build 0 hata; yerel kapıda `scripts/` Python testleri; Auth kayıt kodu main'de; testler gerçek izinle (D53/2); SEC-002 eki kodu (MOD-0018 Treasury
Auth eki DL-4) main'de (D56/1); gerçek RCS-001 servisi (BL-565) main'de ve dev ortamında gerçek servisle `CTR` kodu alındı (D59/1).

**İzinli yollar** (pack "Repo Scope", DL-1 satırları `:426-445`): `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{Contract,ContractVersion}.cs`,
`Domain/Enums/{ContractStatus,ContractVersionStatus,ContractCounterpartyType,ContractVersionKind,ContractDocumentRole}.cs`,
`Domain/Repositories/I{Contract,ContractVersion}Repository.cs`, `Application/Features/Contract/**`,
`Persistence/Repositories/{Contract,ContractVersion}Repository.cs`, `ContractAggregateStore.cs`, `Infrastructure/{RecordCodes,Currency}/**`,
`Api/Controllers/ContractsController.cs`, `Api/ModuleRegistration/ContractLifecycleManifestProvider.cs`; denetim izi eklemeleri
`Domain/Enums/AuditAggregateType.cs`, `MasterDataAuditOperation` (yoksa yeni dosya), `Persistence/Repositories/AuditIntentDeliveryRepository.cs`
(yalnız ekleme); `services/Diten.MdmService/tests/**`; `tests/architecture/audit-ledger/Diten.MdmService.md`,
`tests/architecture/audit-ledger/CT-DECISIONS.md` (tek satır), `AuditTrailStandardTests` sabitleri; MDM modül kaydına manifest
sağlayıcısının bağlanması (kayıt yerinin dosyası prompt'ta ölçülerek yazılır). `Infrastructure/{DocumentRepository,Workflow}/**`
DL-2 / DL-3'tür — DL-1 prompt'una girmez.

**Korunan yollar** (pack "Protected Paths" `:460-474`): `.antigravity/**` (RCS-001 §3 `CTR` satırı dahil), `AGENTS.md`, `gateway/**`,
`services/Diten.Platform/**` (MOD-0021, 0023, 0028, 0029, 0031, 0048, 0262), `services/Diten.ProcurementService/**` (MOD-0144,
MOD-0140), `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`, diğer domain servisleri;
`services/Diten.MdmService/**` içinde Repo Scope dışı her şey (MOD-0220 `LegalEntity` yalnız okunur, MOD-0290 ürün ailesi, MOD-0289
banka yolları, mevcut niyet üreticileri — ekleme satırları dışında); `frontend/**` (DL-4; özellikle `Controllers/ContractsController.cs`
ve `Views/Procurement/Contracts/**` MOD-0144'ün); `docs/analysis/contracts/**`; `execution/**`; kaynak depo ve handoff klasörü.

**Önce oku:** pack (`ready-for-dev` sürümü), AGENTS.md, `execution/domains/master-data-management/domain-config.md`,
`.antigravity/rules/` multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent, code-style,
audit-trail-standard, data-scope-enforcement, record-coding-standard, mongo-indexing, handler-design,
module-self-registration-standard; karar kayıtları D9, D19, D37–D41, D44–D69 (`treasury-intake-owner-decisions-0{2..5}.md`; rol matrisi
D47 / D49 / D69), MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`, DL-3 ve DL-4),
TRM-11 cevabı (`docs/records/audits/2026-10/treasury-intake-handover-review-01/PRODUCER-ANSWER-TRM11-01.md`), bu hazırlık kaydı.

**Sözleşme yüzeyleri:** API `/api/mdm/contracts`, `…/{id}`, `…/{id}/versions`, `…/{id}/versions/{versionNo}`, `DELETE …/bulk` —
v1'de sahip olunan sözleşme dosyası yok (pack Dependencies `:492`); tüketilen: MOD-0048 `GET /api/lookups/reference/currencies`
(sahte istemci + dev ortamı), RCS-001 (sahte port), SEC-002 sağlayıcısı; izin §2.6; sabotaj: AC-6 (kapsam süzgeci kaldırılınca),
AC-11 (niyet yazımı zorla başarısız → 503, iş verisi yok; teslim listesinden koleksiyon çıkarılınca), AC-13 (ikinci yazım
düşürülünce işlem geri alınır), CAS (`Version` süzgeci kaldırılınca), `ux_mdm_contract_code` (`IsDeleted=false` eklenince), AC-59
(`PURCHASE` kabul edilince), AC-60 / AC-61 (kural kaldırılınca), AC-63 (okuma niyeti atlanınca), AC-65 (`value.write` kontrolü
kaldırılınca).

**Gerekli testler** (pack "Test Expectations" `:1191-1219`): birim (doğrulayıcılar, durum makinesi, `allowedActions`, idempotency
gövde özeti, MOD-0048 / RCS yanıt sınıflandırması, değer maskesi, `ContractType`'ta `PURCHASE` yok — AC-59); **gerçek Mongo replica set** entegrasyonu
(kiracı izolasyonu AC-5, veri kapsamı AC-6, soft delete, CAS 409, tekil indeksler ve yarışlar AC-4 / AC-10, işlem bütünlüğü ve hata
enjeksiyonu AC-13, niyetin iş belgesiyle yazılması AC-11, `Guid.Empty` taraması AC-22) — suite kendi URI değişkenini ister,
varsayılan port ya da başka suite'in değişkenine düşmez, her test sınıfı benzersiz veritabanı, replica set `enableTestCommands` ile
(AC-23); bellek içi sahte depo işlem / yarış AC'lerini kapatmaz; mimari (`[HasPermission]` AC-16; `mod0217_` / `Mod0217` /
`MOD0217_` adları yok; `AuditTrailStandard` AC-12); build `Diten.MdmService` 0 hata; yerel kapıda `scripts/` Python testleri.
Değer okuma niyetinin eşzamanlı CAS yazımlarıyla silinmediği "DL-1 başında MDM niyet deposunun yazım biçimi okunarak ölçülür"
(Audited Events `:939-940`) — prompt'tan önce CT ölçümü.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş** olarak: hedef servis + proje yolu (`services/Diten.MdmService/src/…`); aggregate /
  entity + alanlar (`Contract`, `ContractVersion` — pack "Entity Fields"); komut / sorgu listesi (§3); validator kuralları (pack
  "Validation Rules"); izin anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 ile verilir (S-52); inceleme kaydı commit'ten önce `prompt_gate.py review-check` ile denenir (S-54); HIGH iş —
kabul kanıtı betiğini CT yazar, kırmızı tabanı kaydeder (S-83); Expected HEAD `ready-for-dev` günü doldurulur, pack onayı değişirse
prompt yeniden tabanlanır (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22. Kodu kimin yazacağı (MDM sahibi Ali ile
konuşulacak — Y-2, TBD) dağıtımdan önce netleşmeli.

**Keşif:** `python3 .antigravity/scripts/verify_module_id.py . --discover "Contract Lifecycle Management"` raporu (pack baş bloğundaki
2026-10-26 keşfi `:47-52` @31c448d5d'dir; dağıtım günü yeniden koşulur — MOD-0144 `/api/contracts` ve `Nav.Page.CONTRACTS`
çakışmaları dahil).

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynaktan kod alınmaz (D38/3). Kaynak testler ve Recovery testleri
  **çalıştırılmadı**; Recovery'de yalnız dosya okuma ve `grep` (@00d3f2b41).
- SEC-002 sağlayıcısının Platform tarafı okunmadı — **kısmi inceleme**; ölçüm prompt'tan önce CT'nin işidir.
- Auth kaydının, BL-565'in ve DCP-011 üyeliğinin tarihi sahiplerden gelmedi — ölçülemez.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi; `ready-for-dev` revizyonunda CT işi):**

1. **Üç checkbox main'in gerisinde:** (a) `:1266` "MDM domain-config satırı ve kimlik kaydı satırı main'de (bugün çalışma ağacında,
   commit edilmemiş)" — ikisi de main'de (`domain-config.md:49-53`, `module-id-registry.md:155`); (b) `:1274` "RCS-001 §3 `CTR`
   satırı main'de (PR #246 birleşince)" — main'de (`record-coding-standard.md:70`); Repo Scope `:456`, Protected Paths `:462` ve
   Dependencies `:487` hâlâ "PR #246 ile main'e girer" diyor; (c) `:1304` MOD-0023 §17 rev 2 onayı işaretsiz — `approvals.tsv:16`
   onaylı (DL-3 maddesi). DL-1 Önkoşul metni (`:1476`) de "`CTR` satırı main'de (PR #246)"ı açık madde gibi sayıyor.
2. **Dependencies tablosu checklist'in gerisinde:** MOD-0048 satırı "MDM'den tüketim yolu **ölçülmedi**" (`:488`), checklist
   `:1285-1289` ölçüldü diyor (MOD-0289'da da aynı tür bulgu D51 ile düzeltilmişti).
3. **Auth kaydının zamanı — D53 ile kararlandı:** baş bloktaki Revizyon 9 (`:129`) "Auth kaydı MOD-0217 onayından sonra (Ali,
   2026-10-27)" diyor; D51/1 ve checklist `:1270` Auth kaydını bütünüyle paket kapısında sayıyordu. D53 ikiye ayırdı: Auth paket
   metni paket kapısında (D53/1); `mdm.contracts.*` kayıt kodu MOD-0217 onayından sonra, DL-1 kabulünden önce main'de (D53/2).
   **Kapandı:** pack checklist iki maddeye bölündü (paket metni D53/1, kayıt kodu D53/2 — DL-1 kabul kapısı) ve gerilim notu
   "KARAR D53 ile çözüldü" oldu; D51 revizyon notu tarihçe olarak kalır.
4. **Kaynak okunmadı beyanı:** "Kaynak kodu bu oturumda okunmadı" (`:1321`) — bu kayıt beş atıfı okuyarak doğruladı (§2);
   pack revizyonunda "atıflar 2026-10-27'de doğrulandı" notu eklenebilir.
