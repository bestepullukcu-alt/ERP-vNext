---
record_id: MOD-0289-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-26
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/master-data-management/module-packs/MOD-0289-bank-master-bank-account-registry.md (status draft, approval_status not-approved, sha256 ff2c556f4e4ff076cea02a45d3d0f5d88adb995887baf90d031da1f606affd97 (pack ready-for-dev 1. tur revizyonu, 2026-10-28 — Y-12, D62–D65; önceki sürüm `aab4187d…` (sahip denetimi 5cfd67813 düzeltmesi + D65); D58/D59 sürümü `698e135e…`; D57 sürümü `5af910d6…`; D56 sürümü `5137e84f…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `ef905628…` içindir), dal chore/ct/treasury-intake-ct-05 @00d3f2b41 — D51 revizyonu, D52'de değişmedi; satır atıfları main @904dfd5c2 sürümüne (1850f37c…e8b3))
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; MOD-0289 kod alma izni D45/3 — D27/5 koşullarıyla: kod yalnız onaylı paket sürümüyle ve dilim sırasıyla, sabit kiracı taşınmaz)
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve bu pack sürümü dahil; D69 main'de değil ve MOD-0289'a dokunmaz).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU numarası açık — K-1). Taslak main'de ama `draft` ve teyitsiz — pack'teki "Auth paket metni
> main'de" maddesi işaretsiz, paket kapısı açık (D53/1). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-2**'si (MOD-0289: 23 anahtar, 3 rol, 2 açık atama); kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **SEC-002:** ekin **DL-4**'ü (KARAR D56/1; Platform dosyaları için XMC-001 Beste — D63/2): paket metni paket kapısı, kodu
> DL-1 kabul kapısı; modül kodu değeri ekin K-1b'si. 2026-10-27 ölçümü: bugün çalışmaz (iki tarafta `product-item-sku-master` sabit).
> - **MOD-0023 yok** (D23/2; v1'de dört göz) — Y-12 şablon kodları bu pakete dokunmaz.
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **O-14** (grup önekli kayıtlarda — banka `BNK`, tedarikçi hesabı `BAC` — allocator'a gidecek `legalEntityId`; şirket hesabı
> `BAC` için sahip şirketin `LegalEntityId`'si önerisinin teyidi, D65/2) **Beste'de, cevap yok** (2026-10-28): DL-1 kabul kapısı,
> tedarikçi `BAC` kısmı DL-3 kabulünde; değer pakette seçilmez. AC-60 üç dilime bölündü (banka DL-1, şirket hesabı DL-2, tedarikçi
> hesabı DL-3).
> - **DL-2 kabul kapısı:** Treasury tüketici fikstür koşusu aranmaz (KARAR D57/1); `BANK-ACCOUNT-LOOKUP` kendi sözleşme testiyle
> (AC-20, AC-42).
> - **DL-3:** MOD-0140 1.3.0 `GET /api/suppliers/eligible` — Active ilişkili bütün tedarikçiler (KARAR D58); ölçüm: main'de
> `supplier.openapi.yaml` hâlâ `version: 1.2.0`.
> - **MDM domain-config** MOD-0289 cümlesi (D51/5) main'de (`domain-config.md:48`); pack'teki ilgili checklist maddesi henüz işaretsiz.
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol oldu; MOD-0289'un 23 anahtarı ve 3 rolü değişmedi.
>
> **Güncel durum (2026-10-27).** Bu kayıt pack'in main @904dfd5c2 sürümüne bağlıdır (sha256 `1850f37c…b3`, `status: draft`,
> `approval_status: not-approved`; bu sürüme CT-APR-02 ve CT-APR-01 satırı yok). 2026-10-26 sürümlerine (`0746eeae…`, `785291d2…`)
> yapılan "pack satır N" atıfları geçersizdir; bu kayıt pack'e bölüm adıyla ya da `yol:satır@904dfd5c2` ile atıf yapar. Bu
> sürümdeki büyük fark: **kod alma izni verildi (D45/3)** — §2 yeniden sınıflandırıldı. Pack `ready-for-dev` revizyonunda yeniden
> değişirse atıflar o sürümde yeniden doğrulanır (S-53).
>
> **D51 güncellemesi (2026-10-27).** Sahip kararı D51 (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md`)
> bu kaydın açık sorularını ve yeni bulgularını kapattı; pack ve MDM domain-config çalışma ağacında (dal
> `chore/ct/treasury-intake-ct-05`; tarihçe: yazıldığı anda commit edilmemişti; commit 1c444a2c5) revize edildi ("Revizyon (2026-10-27, D51)"): (D51/1) ENGELLER paket kapısı ↔
> "— DL-n için" dilim kapısı — DL-1 yalnız paket kapısını bekler (§1 notu kapandı); (D51/3) WP dalı `feature/mdm/wp-0289-dl1`;
> (D51/4) banka araması `Regex.Escape` + büyük / küçük harf duyarsız + en çok 100 karakter (yeni AC-59, DL-1), `AuditAggregateType`'a
> DL-1'de yalnız `Bank` ve `BankBranch`; (D51/5) domain-config "no code-take permission (D27/5)" cümlesi D45/3'e çekildi. Yeni
> bulgular 1, 3, 4, 5 pack / domain-config'te düzeltildi (bkz. §5). Aşağıdaki `@904dfd5c2` satır atıfları eski sürüme aittir;
> D51 revizyonu satırları kaydırdı — atıflar `ready-for-dev` sürümünde yeniden doğrulanır.
>
> **D51 denetimi (2026-10-27).** Paket kapısı D51/1'e göre: Auth izin / rol kaydı (MOD-0018 paketi ve kodu, Ali, Y-6)
> `ready-for-dev`'den önce main'de olmalıdır — pack'e işaretsiz madde olarak eklendi; aşağıdaki MOD-0018 satırındaki eski
> "DL-1 kodunu bekletmez" çıkarımı kaldırıldı. Açık atama eki DL-2'de kalır.
> D53 sonrası: Auth paket metni paket kapısı, kayıt kodu DL-1 kabul kapısı.
>
> **D52 sonrası (2026-10-27).** Pack D52'de değişmedi (eskimiş metin taraması: RCS-001 sahibi zaten Beste (D44), "PR #246"
> ifadesi yok, §17 maddesi yok). Front-matter sha256 çalışma ağacındaki sürüme (`a2d8cb89…90e6`, D51 revizyonu) çekildi;
> satır atıfları main @904dfd5c2 sürümündedir.

# MOD-0289 DL-1 hazırlığı — banka ana verisi, denetim izi genişletmesi ve paket iskeleti

Bu kayıt, MOD-0289 `ready-for-dev` olduğunda DL-1 WP prompt'unun aynı gün yazılabilmesi için **kaynaktan ne alınacağını**,
**neyin uyarlanacağını** ve **neyin alınmayacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt, paketin
`ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve prompt
kapısından geçer.

> **Kod alma izni verildi — ama uygulama kapalı.** D45/3 (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:24-26@904dfd5c2`)
> MOD-0289 için kod alma iznini D27/5 ile aynı koşullarda verdi: kod yalnız onaylı (`ready-for-dev` + CT-APR-01 satırı main'de) paket
> sürümüyle ve dilim sırasıyla yazılır; kaynak koddaki sabit kiracı (`TenantId = Guid.Empty`) taşınmaz; karar onay satırının yerine
> geçmez. Pack baş bloğu (`MOD-0289-bank-master-bank-account-registry.md:31-35@904dfd5c2`) ve checklist (`:1583-1584`) bunu yazıyor.
> Bu yüzden §2'de pack'in "taşınır, uyarlanır" dediği satırlar artık **al-uyarla**dır; alınan her dosya `path@da4abb20 + sha256` ile iz
> kaydına yazılır (pack "Kaynak ve kanıt").

## 1. Başlama koşulları (ölçüm: main @904dfd5c2, 2026-10-27)

Kaynak: pack "Teslim dilimleri" DL-1 önkoşulu (`:1836-1837@904dfd5c2`: CT-APR-02/01, kod alma izni, DCP, RCS-001 servisi, CT defter
kararı) ve "Ready-for-dev Checklist" işaretsiz maddeleri (`:1579-1615@904dfd5c2`). "DL-1'e etkisi": **bekletir** = DL-1 kodu bu
kapanmadan başlamaz. **D51/1 sonrası:** "bekletir" satırları pack'in paket kapısı (işaretsiz ENGELLER) ile birebirdir; "DL-2 / DL-3 /
DL-4" satırları pack'te "— DL-n için" işaretli dilim kapısıdır ve DL-1'i bekletmez.

| Koşul | Sahibi | Bugün | DL-1'e etkisi |
|---|---|---|---|
| Pack `ready-for-dev` + o sürüme CT-APR-02 | Treasury CT + bağımsız inceleyici | `draft` / `not-approved`; güncel sha `1850f37c…` için `execution/approvals/reviews.tsv`'de satır yok (son MOD-0289 incelemesi `785291d2…`, `reviews.tsv:43`) · **2026-10-28:** güncel sha `ff2c556f…` için satır yok; son satır `ef905628…` (UYGUN) | bekletir |
| CT-APR-01 sürüme bağlı onay (`high: evet`, paketi yazmamış onaylayıcı) | Beste (onaylayıcı) | `approvals.tsv`'de MOD-0289 satırı yok | bekletir |
| Kod alma izni (D1) | sahip | **kapandı — D45/3** (2026-10-27) | — |
| DCP-011 `approved`, TRM-S3 dilimi tanımlı | Beste | `DCP-011-finance-treasury-intake.md` `status: draft` (`:7`); numara atandı (Y-4/2) | bekletir |
| MDM domain-config: MOD-0289 satırı + frontend/gateway kapsam cümlesi | MDM sahibi (Ali) | satır **var** ama eski: "no code-take permission (D27/5); not `ready-for-dev`" (`execution/domains/master-data-management/domain-config.md:46-48@904dfd5c2`) — D45/3 ile çelişiyordu; **D51/5 ile düzeltildi** ("kod alma izni D45/3 (2026-10-27); kod yalnız onaylı paket ve dilim sırasıyla") — 2026-10-28 ölçümünde main'de (`domain-config.md:48` @ `027ca2e38`), pack checklist maddesi henüz işaretsiz; frontend/gateway istisnası yalnız MOD-0290-FU02 (`:57`) | satır: paket kapısı (main'de güncel cümleyle); frontend/gateway cümlesi: DL-4 (D51/1) |
| RCS-001 ortak kod servisi (BL-565) — `BNK` için | Beste (D44) | **yok** — `services/Diten.Building.Blocks/src/` altında kod servisi yok, `services/**`'te `IRecordCodePort` 0; BL-565 `DURUM: AÇIK` (`docs/roadmap/backlog/product-backlog.md:9473-9477@904dfd5c2`); çağrı sözleşmesi gelmedi (Y-4). Backlog sahibi alanı hâlâ "CT (Platform + MDM …)" — D44 ile uyumsuz | **DL-1 kabul kapısı (D59/1)** — başlamayı bekletmez; DL-1 `IRecordCodeAllocator`'a karşı sahte uygulamayla yazılır, gerçek servis DL-1 kabulünden önce main'de (v0 sözleşmesi Y-8, 6 Kasım); üretimde port bağlı değilse 503 `MDM_BANK_RECORD_CODE_UNAVAILABLE` |
| `BNK` / `BAC` önekleri RCS-001 §3'te | — | **var** — `.antigravity/rules/record-coding-standard.md:68-69@904dfd5c2` (D29/2, D30) | — |
| Denetim defteri kararı | — | **verildi — D27/11** (yeni K2 satırları, `mdm-guvenilir-kaynak-niyeti` belirteci, borç yok); CT-DECISIONS satırı DL-1 kodu ile yazılır | — |
| `AuditCategory` değeri | — | **kapandı — D48/1: `MasterData`** (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:19@904dfd5c2`, değer 14); MOD-0126'nın `Finance = 18` bağı kalktı | — |
| MDM denetim niyeti yolunun DL-1 başındaki hâli | Treasury CT (ölçüm) | `AuditAggregateType` dokuz değer (`services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs:3-14@904dfd5c2`); işlem enum'u yalnız `ProductAuditOperation`; teslim deposu sabit dokuz koleksiyon (`…/Persistence/Repositories/AuditIntentDeliveryRepository.cs:15-23@904dfd5c2`) — pack ölçümüyle aynı. "MdmService commit SHA'sı DL-1 başında" (pack kapanış tablosu) dağıtım günü yazılır | — (dağıtım günü SHA) |
| Auth paket metni — MOD-0018 paketi (anahtarlar, açık atama listesi, rol şablonları D46–D50) | Treasury yazar, MOD-0018 sahibi (Ali) teyit eder (Y-15, Y-16; önceki: Y-6; numara / tarih yok) | **2026-10-28: taslak main'de** (#281; origin/main @ `027ca2e38`) — `…/MOD-0018-FU-TBD-treasury-permission-onboarding.md` `draft`, FU numarası açık (K-1), MOD-0018 sahibinin teyidi yok, pack checklist maddesi işaretsiz; Y-14 geçersiz (kullanılmadı); ölçüm anında: **yok** — Treasury / banka rolleri için MOD-0018 paketi depoda yok | paket kapısı (D53/1) — DL-1'i bekletir: paket metni `ready-for-dev`'den önce main'de olmalı; pack checklist'indeki işaretsiz madde "Auth izin / rol kaydı — MOD-0018 paketi ve kodu" (D51 denetimi) D53 ile ikiye ayrıldı (pack metni bu kayıtta değiştirilmedi); açık atama eki ayrı madde (DL-2) |
| Auth kayıt kodu (MOD-0018 Treasury Auth eki DL-2 kodu) | Treasury yazar, MOD-0018 sahibi teyit eder (Y-15, Y-16) | **yok** — `services/Diten.AuthService/**`'te `mdm.bank` 0 (grep, 2026-10-27) | DL-1 kabul kapısı (D53/2) — DL-1 başlamasını bekletmez, kabulünü bekletir |
| Auth `ExplicitGrantOnlyPermissions`'a `read-unmasked`, `read-tenant-wide` | Treasury yazar (MOD-0018 Treasury Auth eki DL-2), MOD-0018 sahibi teyit eder | yok — `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`'te `mdm.bank*` 0 | DL-2 |
| SEC-002 kapsam sağlayıcısı (ilk sürümde: "MOD-0289 anahtarlarıyla çalışması") | MOD-0018 Treasury Auth eki DL-4 (KARAR D56/1): Treasury yazar, MOD-0018 sahibi teyit eder; XMC-001 Beste (Platform) + MDM sahibi | **2026-10-28 (D56/1):** ölçüm maddesi değil — ek DL-4 paket metni paket kapısı, ek kodu DL-1 kabul kapısı (pack checklist); 2026-10-27 ölçümü olumsuz (DL-2 kaydı §1). İlk ölçüm anında: ölçülmedi | paket kapısı (ek DL-4 paket metni) — DL-1'i bekletir; ek kodu DL-1 kabul kapısı |
| MOD-0048 para birimi listesinin MDM'den okunması | Beste (Platform ucu) / DL-2 yazarı (port) | **ölçüldü (pack, @7c57e8466; bugün aynı):** MdmService'te para birimi listesi okuyan istemci yok; yol: mevcut `GatewayReferenceValueReader` deseniyle yeni port (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/GatewayReferenceValueReader.cs:16-50@904dfd5c2` — `Gateway:BaseUrl`, kullanıcı token'ı + `X-Tenant-Id` iletimi); Platform ucu `GET api/lookups/reference/currencies` | DL-2 (DL-1'in `Bank` / `BankBranch` alanlarında para birimi yok) |
| MDM `ValidationBehavior` girilen değeri döndürüyor mu | — | **ölçüldü: döndürmüyor** — gövde yalnız `error.ErrorMessage` listesi (`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/ValidationBehavior.cs:26-31,46@904dfd5c2`). Kalan: MOD-0289 doğrulayıcı mesajlarında `{PropertyValue}` yer tutucusu kullanılmaz | DL-2 (öneri: DL-1 doğrulayıcılarına da aynı kural) |
| MOD-0140 `supplier.openapi.yaml` 1.3.0 | Cihan (MVP-2) | main'de **1.2.0** (`docs/analysis/contracts/supplier.openapi.yaml:13@904dfd5c2`) | DL-3 |
| Gateway rotaları | integration-agent | yok | DL-4 |
| Güvenlik onayı (O-6) | — | **verildi — D33/1**; gerçek Mongo testleri ve AC-50 DL-2 kabul kapısında | — |

**Not — ENGELLER başlığının kapsamı.** Pack "iki kapı"yı (tasarım kapısı ↔ dilim kabul kapısı, `:1573-1577`) yazıyor, ama "ENGELLER
— tasarım / onay kapısı (hepsi kapanmadan `ready-for-dev` olmaz)" listesi "DL-2 için", "DL-3 için", "DL-4 için" etiketli maddeleri
de içeriyor (Auth, SEC-002 ölçümü, MOD-0140 1.3.0, Gateway rotaları). Metne göre DL-1 bile supplier 1.3.0'ı ve Gateway rotalarını
bekler. Bu maddelerin `ready-for-dev`'i bekletip bekletmeyeceği CT / sahip kararıdır; bu kayıt karar vermez. **Kapandı — D51/1:**
pack ENGELLER'i paket kapısı ve "Dilim kapıları" alt listesi olarak ayrıldı; bu maddeler kendi dilimlerinin kapısıdır, DL-1'i ve
`ready-for-dev`'i bekletmez.

**Üretici beklemeleri (DL-1 dışı, prompt'a "bu dilimde yok" olarak yazılır):** `supplier.openapi.yaml` 1.3.0 (Cihan) → DL-3;
Auth açık atama (MOD-0018 Treasury Auth eki DL-2; Treasury yazar, MOD-0018 sahibi teyit eder) → DL-2; MOD-0048 para birimi portu → DL-2. DL-1'i doğrudan bekleten üretici paket kapısındaki
**Auth paket metni** (MOD-0018 Treasury Auth eki DL-2 — taslak #281 ile main'de, teyitsiz; Treasury yazar, MOD-0018 sahibi teyit
eder — Y-15, Y-16; D53/1) ve ekin DL-4 SEC-002 paket metnidir (D56/1). Auth kayıt kodu başlamayı değil
DL-1 kabulünü bekletir (D53/2). **RCS-001 BL-565** (Beste, D44) paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu
sahte allocator ile yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım). **O-14** (grup önekli kayıtlarda
allocator'a gidecek `legalEntityId`; Beste, cevap yok) de DL-1 kabul kapısıdır (pack "Dilim kabul kapısı" DL-1).

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak MOD-0289 kodu `services/Diten.Platform/` altındadır (kısaltma: `P/` = `services/Diten.Platform/src/`); MDM'ye
taşınmaz, kurallar MDM düzenine yeniden yazılır (pack "Kaynak ve kanıt", "Backend File Convention"). Özet: `shasum -a 256` (ilk
16 hane), satır sayısı `wc -l`, ikisi de @da4abb20; 2026-10-27'de `git show da4abb20:<yol>` ile **yeniden ölçüldü, tablodaki 31
dosyanın hepsi 2026-10-26 ölçümüyle aynı**. Satır atıfları 2026-10-26'da okunarak doğrulandı. Karar dili: **al-uyarla** = kaynaktan
alınacak (D45/3; doğrula, MDM düzenine göre yeniden yaz, iz kaydına `path@da4abb20 + sha256`), **yalnız referans** = uyarlanacak
davranış (okunur, kod alınmaz), **alma** = alınmayacak ("madde N" = pack "Taşınmayacaklar", `:1730-1758@904dfd5c2`),
**sonraki dilim**.

**Özet (alınacak / uyarlanacak / alınmayacak):**

| Kaynaktan alınacak (al-uyarla) | Uyarlanacak (yalnız referans) | Alınmayacak |
|---|---|---|
| `BankMasterDraft` → `Bank`, `BankBranchDraft` → `BankBranch` alanları; `BankMasterLifecycleStatus` (Draft=1/Active=2/Inactive=3); depo arayüzünün banka / şube okuma yöntemleri; `Mod0289BankService` banka / şube kuralları ve doğrulayıcıları (bölerek, pack kazanır); banka / şube modelleri; depo okuma sorguları (banka / şube); denetleyicinin banka / şube uçları; istek sözleşmeleri (`:5-33`, kod alanı hariç) | kaynak testleri (davranış listesi; F02 / PAIR olanlar hariç) | `TenantScopedEntity` / `EntityVariations`, depo yansımasıyla kiracı damgalama (`Repositories.cs`), `TenantId = Guid.Empty` atamaları, Guid'in BSON string saklanması, kullanıcıdan `BankCode`, `MOD0289_*` izin / hata literalleri, `/api/v1/mod0289`, `mod0289_*` koleksiyonları, `mod0140_suppliers` ve `mod0048_reference_data_*` doğrudan okumaları, Platform kimlik koruması / `ResolveActorId`, seed işçisi, F02 / PAIR, token / ödeme referansı aktarma, eski UI |

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/Diten.Platform.Domain/Entities/Mod0289/BankMasterEntities.cs` | `3ee3c4f67a756ecf` · 721 | **al-uyarla** (yalnız `BankMasterDraft` :8-20, `BankBranchDraft` :23-38) | `Domain/Entities/{Bank,BankBranch}.cs : EntityBase, IAuditIntentAggregate` | pack "Varlıklar" #1-2 "taşınır, uyarlanır"; taban `TenantScopedEntity` **alma** (madde 4); Guid'in BSON string saklanması (:10, :25, :28; hesapta :52, :55; durum enum'u string :18) **alma** (madde 12); `BankCode` kullanıcı alanı **alma** (madde 11) → `Code` RCS `BNK`; yeni alanlar `NationalBankCode`, `DomesticKeyFenceVersion`, `DomesticKeyIncludesBranch`, `CreatedBy`/`UpdatedBy` Guid? (pack "Entity Fields" Bank / BankBranch); hesap ve diğer 23 sınıf DL-2 / v1 dışı / alma |
| `P/Diten.Platform.Domain/Entities/Mod0289/BankMasterEnums.cs` | `51f1254aed62f22b` · 213 | **al-uyarla** (yalnız `BankMasterLifecycleStatus` :3-8) | `Domain/Enums/BankLifecycleStatus.cs` | değerler Draft=1/Active=2/Inactive=3 korunur (pack "Durumlar"); diğer durum boyutları (:10-40) **alma** (madde 15) |
| `P/Diten.Platform.Domain/Repositories/Mod0289/IMod0289BankRepository.cs` | `5af68246cabf3a41` · 210 | **al-uyarla** (:108-117, :208 — banka / şube okuma) | `Domain/Repositories/I{Bank,BankBranch}Repository.cs` | `GetByBankCodeAsync` (:111) **alma** — kod RCS'den gelir; sürüm süzgeçli güncelleme yöntemleri yeni (kaynakta Update yok) |
| `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/EntityVariations.cs` | `9a333d562385bce7` · 18 | **alma** | — | `TenantScopedEntity` (:8-11) — madde 4 |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/…Application/Features/Mod0289/Services/Mod0289BankService.cs` | `44617d01fb258eab` · 5353 | **al-uyarla, bölerek** (yalnız :152-322 banka / şube, :3644-3750 doğrulayıcılar) | `Features/BankMaster/{Commands,Queries,Handlers,Validators}/**` (pack "Backend File Convention") | kurallar handler + domain'e bölünerek yazılır (`handler-design`). Kaynak farkları (pack kazanır): BIC 6..11 alfanümerik (:3675-3681) ↔ pack ISO 9362 8/11 + ülke eşleşmesi (pack "Validation Rules"); şube `City` ≤ 120 (:3746-3747) ↔ pack ≤ 100 ("Entity Fields" BankBranch); ülke kodu MOD-0048 referans kümesine karşı (`EnsureActiveReferenceValueAsync("COUNTRY_CODE")` :186, :274) ↔ pack yalnız ISO biçimi (D24/5) — **alma**; banka ve şube oluşturmada `TenantId = Guid.Empty` (:209, :293) **alma** (D45/3, madde 1, AC-26); liste N+1 şube sayımı (:164-169) alınmaz; serbest İngilizce iletiler alınmaz (resx anahtarı) |
| aynı dosya, diğer bölümler | — | sonraki dilim / alma | — | hesap, token, iş akışı, istisna, içe aktarma, outbox (:2446-2470, `TenantId = Guid.Empty` :2450) — DL-2 / v1 dışı / madde 1, 7, 8 |
| `P/…Features/Mod0289/Models/Mod0289BankModels.cs` | `f189b970caed2d0b` · 888 | **al-uyarla** (:5-78 banka / şube modelleri) | `Features/BankMaster/BankModels.cs` (tek dosya: `BankListItemDto`, `BankDetailDto`, `BankWriteRequest`, `BankBranchDto`, `BankBranchWriteRequest`) | DTO'larda `TenantId` yok; `CreatedBy` string (:41, :78) **alma** (madde 12) → Guid `sub` |
| `P/…Features/Mod0289/Mod0289Permissions.cs` | `7731c1e0f1869cbc` · 43 | **alma** | `mdm.banks.*` (pack "Authorization Convention") | `MOD0289_BANK_*` / `MOD0289_BRANCH_*` literalleri (:5-8) — madde 10 |
| `P/…Features/Mod0289/{Commands,Contracts,Handlers,Queries,Validators}/**` (F02, 7 dosya), `Models/Mod0289AccountCreatePilotOptions.cs`, `Services/Mod0289AccountCreate{Fingerprint,Participant}.cs` | ölçüldü, tabloya alınmadı (DL-1 dışı) | **alma** | — | F02 / PAIR — madde 3 |
| `P/…Features/Mod0289/Services/Mod0289PaymentReferenceHandoffService.cs` | `8fe015e359f0e688` · 445 | **alma** | — | token / ödeme referansı aktarma — madde 7 |
| `P/…Features/Mod0289/Services/Mod0289WorkflowGateway.cs` | `27647ef035142372` · 440 | v1 dışı | — | MOD-0023 akışı v1'de yok (D23/2) |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/…Infrastructure/Persistence/Repositories/Mod0289/Mod0289BankRepository.cs` | `9e61cbcd4b3c5a97` · 2228 | **al-uyarla** (yalnız :75-196 banka / şube okuma sorguları, :1927-1934) | `Persistence/Repositories/{Bank,BankBranch}Repository.cs` | MDM `RepositoryBase` + `TenantFilter` deseni; taban `UpdateAsync` değil, `Version == expected` süzgeçli koşullu güncelleme (`LegalEntityRepository.UpdateEditableFieldsAsync` deseni, pack "Entity Fields"); her yazma niyetiyle aynı koşullu yazım / işlem. Kaynakta **indeks kurulumu yok** — BIC / kod tekilliği yalnız ön sorgu + `DuplicateKey` yakalama (servis :189-220); arama süzgeci kullanıcı girdisini kaçışsız regex yapar (:83, :149) — **alma**; Recovery kaçışlı ya da sabit önekli arama yazar (pack'te kural yok — prompt'ta açık soru). `mod0140_suppliers` okuması (:51) **alma** (madde 2); `mod0048_reference_data_*` doğrudan okuması (:71-72) **alma** (D24/5 + başka modül koleksiyonu); `mod0289_*` koleksiyon adları **alma** (madde 10) → `mdm_banks`, `mdm_bank_branches` |
| `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs` | `9d35765f0be11430` · 110 | **alma** | — | yansımayla kiracı damgalama (:81-90) — madde 1 |
| `P/…Repositories/Mod0289/Mod0289AccountCreateStore.cs` | `68cde5a947e43897` · 89 | **alma** | — | ayrı sahiplik bağı / F02 — madde 3, 6 |
| — | — | **yeni** | MDM indeks kurulumu (depo kurucusunda `EnsureIndexes`, `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MongoIndexReadiness.cs`) | kaynakta karşılığı yok; aşağıdaki DL-1 tekil indeksleri |

**DL-1 veritabanı tekil indeksleri (pack "Mongo indeksleri", `:470-473@904dfd5c2`) — açıkça:**

| İndeks | Anahtar | Tekil · kısmi süzgeç | Test + sabotaj |
|---|---|---|---|
| `ux_mdm_bank_code` | TenantId + Code | **Unique, kısmi filtre YOK** — silinen bankanın kodu da dolu sayılır (RCS-001 §6) | aynı kiracıda aynı kodla ikinci kayıt — silinmiş bankanın koduyla dahil — reddedilir; sabotaj: `IsDeleted=false` süzgeci eklenince silinmiş-kod vakası kırmızı. Kodun RCS'den gelmesi DB tekilliğini kaldırmaz; ikisi birlikte (AC-4 "kodu yeniden kullanılmaz") |
| `ux_mdm_bank_bic` | TenantId + Bic | Unique, kısmi: `Bic` var **ve** `IsDeleted = false` | AC-2: gerçek replica set'te iki eşzamanlı aynı-BIC oluşturmadan biri 409 `MDM_BANK_BIC_CONFLICT`; indeks kaldırılınca kırmızı |
| `ux_mdm_bank_national_code` | TenantId + CountryCode + NationalBankCode | Unique, kısmi: `NationalBankCode` var **ve** `IsDeleted = false` | aynı ülke + ulusal kod 409 `MDM_BANK_NATIONAL_CODE_CONFLICT` (AC-2) |
| `ux_mdm_bank_branch_code` | TenantId + BankId + BranchCode | Unique, kısmi `IsDeleted = false` | aynı şube kodu 409 `MDM_BANK_BRANCH_CODE_CONFLICT` (AC-2) |

Hesap, amaç, çit ve okuma kaydı indeksleri DL-2'dedir. Kaynağın kod çakışması kontrolü (servis :189-195, `IsDeleted=false`
süzgeçli ön sorgu, repo :118-127) **alınmaz**: kod kullanıcıdan gelmez ve tekillik silinmişleri de kapsar.

**Banka listesi araması (D51/4):** kaynağın kaçışsız regex süzgeci (repo :83, :149) alınmaz; Recovery'de arama metni
`Regex.Escape` ile kaçırılır, büyük / küçük harf duyarsız eşleşir, en çok 100 karakterdir (aşarsa 400
`MDM_BANK_VALIDATION_FAILED`, sorgu çalışmaz) — pack "Validation Rules", "Failure Path", **AC-59** (DL-1, sabotajlı).

### 2.4 Api, Infrastructure ve frontend

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/Diten.Platform.API/Controllers/Mod0289Controller.cs` | `7ba4e18ed40c2d45` · 2451 | **al-uyarla** (yalnız :72-238 banka / şube uçlarının şekli) | `Api/Controllers/{BanksController,BankBranchesController}.cs` | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]` (AC-8); eylem süzgeciyle kimlik koruması (:43-62), denetleyicide izin kontrolü (:246 vb.), istisna atan `ResolveActorId` (:2292-2298), Platform denetim alıcısına bağlı ileti (:2333) **alma** (madde 4); şube detayında `BranchRead` **veya** `BankRead` (:225) → tek anahtar `mdm.banks.read`; rota `/api/v1/mod0289` (:21) **alma** (madde 10) → `/api/mdm/banks`, `/api/mdm/banks/{bankId}/branches`, `/api/mdm/bank-branches/{id}` (pack "Kaynak işlemleri", "Recovery ekleri") |
| `P/Diten.Platform.API/Models/Mod0289/Mod0289Contracts.cs` | `f440007a804b6ad5` · 467 | **al-uyarla** (:5-33) | `BankWriteRequest`, `BankBranchWriteRequest` (`BankModels.cs`) | `CreateBankDraftRequest.BankCode` (:7-8) **alma** (madde 11) — istekte kod 400 `MDM_BANK_CODE_FORBIDDEN` |
| `P/Diten.Platform.API/Controllers/Mod0289AccountTransactionsController.cs` | `9bd2ec1eb631d604` · 88 | **alma** | — | F02 — madde 3 |
| `P/Diten.Platform.API/Services/Mod0289/Mod0289RequestIdentity.cs`, `Mod0289AccountCreateIdentityGuard.cs` | `cb9b88d93cbe2461` · 128; `73a6ad7d3f4d94bb` · 47 | **alma** | MDM JWT / `sub` | madde 4 |
| `P/Diten.Platform.API/Services/Mod0289/Mod0289SeedBaselineWorker.cs`, `P/…Infrastructure/Services/Mod0289/Mod0289SeedBaselineService.cs` | `5a30d5d64d7f540d` · 69; `e45cb2daabbb6dba` · 172 | **alma** | — | madde 9 |
| `P/…Infrastructure/Services/Mod0289/Mod0289AccountCreateEligibilityUnavailable.cs` | `b71e970d3f1ed9ea` · 14 | **alma** | — | madde 3 |
| `frontend/Diten.Web/Controllers/Platform/BankMasterRegistryController.cs`, `Views/Platform/BankMasterRegistry/Index.cshtml` | `d1c1ad00381e85c1` · 58; `7ee9b57eb133d81c` · 313 | sonraki dilim (DL-4) | `Views/MasterData/Banks/**` | `_LayoutBackbone` / hardcoded metin **alma** (madde 14) |

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `services/Diten.Platform/tests/Diten.Platform.API.Tests/Mod0289ContractBootstrapTests.cs` | `324b62a7563857d6` · 4075 (79 test) | yalnız :27-367 banka / şube vakaları → AC-1, 2, 3, 4, 5 (banka ve şube kısmı); kod-çakışması testi (:318) RCS'li modelde anlamını yitirir |
| `…/Mod0289RequestIdentityTests.cs`, `…/Mod0289RequestIdentityHttpTests.cs` | `57e513bba79dbfc4` · 269; `2fa7357c457f2b7b` · 215 | alma (madde 4) — Recovery'de AC-8 mimari testi |
| `…/Mod0289SeedBaselineServiceTests.cs` | `b13daf37d56d6d7f` · 83 | alma (madde 9) |
| `…/Mod0289AccountCreateTransactionHttpTests.cs`, `…Infrastructure.Tests/Mod0289AccountCreateTransactionMongoTests.cs`, `…/Mod0289LegalEntityAccountAggregateMongoTests.cs` | `5bdba821d3ecf134` · 151; `559c192224667f97` · 379; `a747245a73092dc9` · 111 | alma (madde 3) — güvence olarak alıntılanmaz |
| `…/Mod0289PaymentReferenceHandoffServiceTests.cs` | `b80fd06622039e14` · 433 | alma (madde 7) |
| `frontend/Diten.Web/tests/mod0289-bank-master-registry-ui.test.js` | `2576e9228d51fd0b` · 427 | DL-4 |

Devralınmış kaynak kanıtı (R2 API 104 geçti / 72 kaldı, R2S 1 / 45 — pack "Kaynak ve kanıt") Recovery hazırlığının ölçüsü değildir.

### 2.6 Yetki, roller ve denetim kategorisi — pack değişikliklerinin DL-1'e etkisi (D46, D47, D48)

Pack "Authorization Convention" (`:1128-1170@904dfd5c2`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`treasury-intake-owner-decisions-05.md:109-405@904dfd5c2`; D46 tablosu tarihçedir, uygulanmaz).

- **DL-1 anahtarları:** `mdm.banks.read`, `.create`, `.update`, `.activate`, `.deactivate` (T2), `.delete`, `.bulk-delete` (T2) — banka
  **ve** şube için tek aile. Kaynak `MOD0289_*` literalleri alınmaz.
- **Roller (kod değil, Auth'ta — MOD-0018 Treasury Auth eki DL-2; Treasury yazar, MOD-0018 sahibi teyit eder):** Banka verisi görüntüleyici (`mdm.banks.read`), Banka verisi yöneticisi (banka / şube
  yazmalarının hepsi, durum ve silme dahil). Banka hesabı etkinleştirici (D47/2) yalnız **hesap** etkinleştirmesidir → DL-2. Dört göz
  (D23/2) yalnız banka hesabındadır; banka / şube etkinleştirmesinde dört göz **yok** (matris "örtüşmeler" notu) — DL-1 testleri
  bunu beklememeli.
- **D47/4:** finans rollerine `mdm.banks.read` verilmez; DL-1'in tüketicisi yalnız MOD-0289 kendi rolleridir.
- **Kayıt kodu:** banka `BNK`, grup öneki (kiracı geneli kayıt, D24/4); şube kodu doğal anahtar (RCS kodu yok); `BAC` DL-2.
- **Denetim kategorisi:** `AuditCategory.MasterData` (14) — D48/1; pack "Audited Events" `Category` cümlesi (`:1193-1197@904dfd5c2`).
  Banka / şube olayları K2 değil ama aynı kapalı-başarısız niyet yolundan yazılır.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

Yollar pack "Teslim dilimleri" DL-1 satırı (`:1833-1838@904dfd5c2`) ve "Repo Scope" (`:518-557@904dfd5c2`); hepsi
`services/Diten.MdmService/` altında, yeni servis ve port yok (MDM 5059, D20/1).

- `Domain/Entities/{Bank,BankBranch}.cs` (`EntityBase`, `IAuditIntentAggregate`), `Domain/Enums/BankLifecycleStatus.cs`,
  `Domain/Repositories/I{Bank,BankBranch}Repository.cs`.
- `Features/BankMaster/**` Compact düzeni: banka için Create / Update / Activate / Deactivate / Delete / BulkDelete, şube için Create /
  Update / Activate / Deactivate / Delete; sorgular `GetBankListQuery`, `GetBankByIdQuery`, `GetBankBranchListQuery`,
  `GetBankBranchByIdQuery` + `BankListSort`; doğrulayıcılar; `BankModels.cs`. **Kaynakta karşılığı olmayan** komutlar: Update / Activate
  / Deactivate / Delete / BulkDelete (banka) ve Update / Activate / Deactivate / Delete (şube) — pack "Recovery ekleri".
- `Persistence/Repositories/{Bank,BankBranch}Repository.cs` + §2.3'teki dört tekil indeks; `If-Match` / `Version` CAS (AC-9).
- `Api/Controllers/{BanksController,BankBranchesController}.cs`, `Api/ModuleRegistration/BankMasterManifestProvider.cs`
  (`BrandProductMasterManifestProvider.cs` deseni; ölçüldü, `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/`).
- **RCS kod portu** (`IRecordCodeAllocator`, `Infrastructure/RecordCodes/**`): `BNK`, grup öneki; servis erişilemezse 503
  `MDM_BANK_RECORD_CODE_UNAVAILABLE`. BL-565 yokken port sahte uygulamaya bağlanır; gerçek servis DL-1 kabul kapısıdır (D59/1, §1).
- **Denetim izi genişletmesi (yalnız ekleme):** `Domain/Enums/AuditAggregateType.cs` — DL-1'de **yalnız `Bank` ve `BankBranch`**;
  `BankAccount`, `BankAccountSensitiveRead` ve hesap tarafının diğer değerleri kendi dilimlerinde (DL-2) eklenir (D51/4; pack Repo
  Scope ve Audited Events buna çekildi), eklemeli genel
  `MasterDataAuditOperation` (D24/2), `AuditIntentDeliveryRepository.cs` koleksiyon listesi (bugün sabit dokuz). Olaylar
  `mdm.bank.*`, `mdm.bank-branch.*` (pack "Audited Events"); `Category = MasterData` (D48/1).
- `tests/architecture/audit-ledger/Diten.MdmService.md` yeni K2 satırları + aynı değişiklikte `CT-DECISIONS.md` satırı ve
  `AuditTrailStandardTests` sabitleri, borç satırı yok (D27/11; AC-7).
- İzin anahtarları `mdm.banks.*` (§2.6) — manifest sağlayıcısında; rol şablonu Auth'ta (MOD-0018 Treasury Auth eki DL-2).
- Hata sözlüğü DL-1 alt kümesi (pack "Hata sözlüğü"): `MDM_BANK_NOT_FOUND`, `MDM_BANK_BRANCH_NOT_FOUND`, `MDM_BANK_VALIDATION_FAILED`,
  `MDM_BANK_CODE_FORBIDDEN`, `MDM_BANK_BIC_CONFLICT`, `MDM_BANK_NATIONAL_CODE_CONFLICT`, `MDM_BANK_BRANCH_CODE_CONFLICT`,
  `MDM_BANK_BRANCH_BANK_MISMATCH`, `MDM_BANK_VERSION_CONFLICT`, `MDM_BANK_STATE_INVALID`, `MDM_BANK_RECORD_CODE_UNAVAILABLE`,
  `MDM_BANK_AUDIT_UNAVAILABLE`, `MDM_BANK_DOMESTIC_KEY_LOCKED`; ileti resx anahtarı.

## 4. DL-1 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir; prompt'un içermesi gerekenlerin listesidir. Biçim örneği main'de
`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri; pack'te yok):
`execution/domains/master-data-management/work-packs/prompts/WP-0289-DL1-v1.0.md`. Kuyruk satırı `WP-0289-DL1` (CT-QUEUE `PLANNED`,
TRM-16; bu dalda 242ef03df).

**Başlık satırları**

| Alan | Değer (taslak) | `ready-for-dev` günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` | — (yeni servis yok — MdmService, D20/1; `/add-module` servis iskeleti adımı bu modülde yoktur, D51/3'ün iskelet adımı MOD-0126 içindir; WP "paket iskeleti" — manifest sağlayıcısı + denetim izi genişletmesi — ile başlar) |
| WP / Prompt | `WP-0289-DL1` · `TRM-WP-0289-DL1-P v1.0` | — |
| Repository · Branch · Expected HEAD · Worktree | `feature/mdm/wp-0289-dl1` — her WP kendi dalında (D51/3); pack künyesindeki `feature/mdm/mod-0289-bank-master-bank-account-registry` paket dalıdır, WP dalı değil | Expected HEAD = pack `ready-for-dev` onayının main'deki commit'i (S-53); MdmService commit SHA'sı (pack kapanış tablosu, "DL-1 başında"); worktree yolu |
| Risk | HIGH — §9.1 sinyalleri: kiracı, denetim, kayıt kodu, yetki | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kod alma | **al-uyarla** — §2'deki satırlar, D45/3; her dosya `path@da4abb20 + sha256` WP raporunda | — |
| Kapsanan dilim | **yalnız DL-1** (hesap, hassas veri, amaçlar, çit, ekranlar, gateway **yok**) — başlama koşulu yalnız pack'in paket kapısı; "— DL-n için" maddeleri bu WP'yi bekletmez (D51/1); kabul kapısında: Auth kayıt kodu ve SEC-002 eki kodu main'de (D53/2, D56/1), gerçek RCS-001 servisi (BL-565) main'de ve dev ortamında gerçek `BNK` kodu (D59/1), O-14 kararı (Beste) | — |

**İzinli yollar** (pack Repo Scope, DL-1): `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{Bank,BankBranch}.cs`,
`Domain/Enums/BankLifecycleStatus.cs`, `Domain/Repositories/I{Bank,BankBranch}Repository.cs`,
`Application/Features/BankMaster/**`, `Persistence/Repositories/{Bank,BankBranch}Repository.cs`, `Infrastructure/RecordCodes/**`,
`Api/Controllers/{Banks,BankBranches}Controller.cs`, `Api/ModuleRegistration/BankMasterManifestProvider.cs`; denetim izi eklemeleri
`Domain/Enums/AuditAggregateType.cs`, `MasterDataAuditOperation` (yeni dosya), `Persistence/Repositories/AuditIntentDeliveryRepository.cs`
(yalnız ekleme); `services/Diten.MdmService/tests/**`; `tests/architecture/audit-ledger/Diten.MdmService.md`,
`tests/architecture/audit-ledger/CT-DECISIONS.md` (tek satır), `AuditTrailStandardTests` sabitleri; MDM modül kaydına banka manifest
sağlayıcısının bağlanması (kayıt yerinin dosyası prompt'ta ölçülerek yazılır).

**Korunan yollar** (pack Protected Paths): `.antigravity/**`, `AGENTS.md`, `gateway/**`, `services/Diten.Platform/**`,
`services/Diten.ProcurementService/**`, `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**`, Treasury servisi ve
diğer servisler; `services/Diten.MdmService/**` içinde Repo Scope dışı her şey (MOD-0220 `LegalEntity` yalnız okunur, MOD-0290 ürün
ailesi, mevcut niyet üreticileri — ekleme satırları dışında); `frontend/**` (DL-4); `docs/analysis/contracts/**` (BANK-ACCOUNT-LOOKUP
taslağı DL-2); `execution/**`; kaynak depo ve handoff klasörü.

**Önce oku:** pack (`ready-for-dev` sürümü), AGENTS.md, `execution/domains/master-data-management/domain-config.md`, `.antigravity/rules/`
multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent, code-style, audit-trail-standard,
record-coding-standard, mongo-indexing, handler-design, module-self-registration-standard; karar kayıtları D1, D20, D23, D24, D27,
D33, D44, D45–D69 (`treasury-intake-owner-decisions-0{1..5}.md`; özellikle D51/4, D53, D56/1, D59/1, D65/2), MOD-0018
Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU-TBD-treasury-permission-onboarding.md`, DL-2 ve DL-4), bu hazırlık kaydı.

**Sözleşme yüzeyleri:**
- API: `/api/mdm/banks`, `/api/mdm/banks/{bankId}/branches`, `/api/mdm/bank-branches/{id}` (+ activate / deactivate / bulk) — DL-1 için
  sözleşme dosyası pack'te yok (BANK-ACCOUNT-LOOKUP DL-2).
- İzin: `mdm.banks.*` (§2.6); her aksiyonda `[HasPermission]`.
- Test — **DL-1 Bitiş AC'leri** (`:1837-1838@904dfd5c2`): AC-1, 2, 3 (banka ve şube kısmı), 4 (banka kısmı), 5 (banka ve şube
  kısmı), 6 (banka kısmı), 7, 8 (banka kısmı), 9 (banka kısmı), 27 (banka kısmı), **59** (banka araması — D51/4 ile eklendi). AC
  metinleri pack'ten tırnakla alıntılanır (AC-1…AC-9 `:1259-1281`, AC-27 `:1345-1348` — `@904dfd5c2`; AC-59 D51 revizyonunda,
  Acceptance Criteria sonu). AC-27 banka kısmı dev ortamı kanıtıdır (bir banka oluşturmanın merkezi kaydı, AUD-001 §8).
- Sabotaj: AC-2 (tekil indeks kaldırılınca), AC-3 (`TenantFilter` kaldırılınca), AC-6 (niyet yazımı zorla başarısız → 503, iş verisi
  yok; teslim listesinden koleksiyon çıkarılınca), AC-9 (`Version` süzgeci kaldırılınca), `ux_mdm_bank_code` (`IsDeleted=false` süzgeci
  eklenince), AC-59 (`Regex.Escape` kaldırılınca `.*` vakası, harf duyarsızlığı kaldırılınca büyük harf vakası, 100 karakter kuralı
  kaldırılınca 101 karakter vakası kırmızı).

**Gerekli testler — DL-1 dilim kabul kapısı** (pack "Dilim kabul kapısı", `:1864@904dfd5c2`): Bitiş AC'leri; **AC-2 ve AC-9'un gerçek
replica set yarış testleri** (her takım kendi URI değişkenini okur, varsayılan porta düşmez, replica set `enableTestCommands` ile;
bellek içi sahte depo bu AC'leri kapatmaz); `AuditTrailStandard` (AC-7); `Diten.MdmService` build 0 hata; yerel kapıda `scripts/`
Python testleri; Auth kayıt kodu main'de; testler gerçek izinle (D53/2). Ayrıca birim: BIC (ISO 9362, ülke eşleşmesi, şube BIC'inin ilk 8'i), doğrulayıcılar, durum makinesi, RCS port sahtesiyle
kod biçimi; `Guid.Empty` taraması banka / şube yazımları için (AC-26'nın DL-1'e düşen kısmı — pack AC-26'yı DL-2 bitişine yazıyor;
prompt'ta "erken uygulanır, kabulü DL-2" diye açık yazılmalı).

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş** olarak: hedef servis + proje yolu (`services/Diten.MdmService/src/…`); aggregate / entity +
  alanlar (`Bank`, `BankBranch` — pack "Entity Fields"); komut / sorgu listesi (§3); validator kuralları (pack "Validation Rules"un
  banka / şube satırları); izin anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: ajanın araçları İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`.

**Kart kuralları:** "push ve PR yok; commit sonrası dur ve bildir" (S-63; dal adı, HEAD sha, test sonucu); prompt ~10 bin token'ı
aşarsa yol + SHA-256 ile verilir (S-52); inceleme kaydı commit'ten önce `prompt_gate.py review-check` ile denenir (S-54); HIGH iş —
kabul kanıtı betiğini CT yazar, kırmızı tabanı kaydeder (S-83); Expected HEAD `ready-for-dev` günü doldurulur, pack onayı değişirse
prompt yeniden tabanlanır (S-53); dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22. Kodu kimin yazacağı (MDM sahibi Ali ile
konuşulacak — Y-2) dağıtımdan önce netleşmeli.

**Keşif:** `python3 .antigravity/scripts/verify_module_id.py . --discover "bank master"` raporu, yol@commit + sha256, keşif kararları —
dağıtım günü koşulur.

**Açık sorular (prompt'tan önce CT):** banka listesinde arama deseni (kaçışlı regex / sabit önek — pack'te kural yok); `AuditAggregateType`'a
dört değerin hepsi mi yalnız `Bank` / `BankBranch` mı DL-1'de eklenir. **İkisi de kapandı — D51/4:** `Regex.Escape` + büyük / küçük harf
duyarsız + en çok 100 karakter (AC-59); DL-1'de yalnız `Bank` ve `BankBranch`.

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git show`); kaynak düzeltmesi yok. Kaynak testler ve Recovery testleri **çalıştırılmadı**;
  Recovery'de yalnız dosya okuma ve `grep` (@904dfd5c2).
- Rol şablonlarının Auth'taki karşılığı, BL-565 çağrı sözleşmesi ve zamanı, DCP-011 onay tarihi sahiplerden gelmedi — ölçülemez. (2026-10-28 eki: BL-565 v0 sözleşmesi D59/1 ile geldi, kesin hali 6 Kasım.)
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

### Önceki çelişkilerin durumu ve yeni bulgular (CT'ye; pack değişikliği yeni inceleme ister)

Önceki kaydın beş maddesi pack revizyon 8'de kapandı (`:98-100@904dfd5c2`): servis boyu artık 5353 (`:984`), `Guid.Empty` atıfları
genişledi (`:172`), AC-27 banka kısmı DL-1 / tam kapanış DL-3, `bank-account-purposes` Gateway listesinde, RCS §3 satır durumu D30.

Yeni bulgular (bu sürümde ölçüldü):

1. **MDM domain-config eski:** "no code-take permission (D27/5)" yazıyor (`domain-config.md:46-48`); D45/3 ile çelişiyor. Domain-config bu
   paketle değişmez (Protected Paths) — MDM sahibine / CT'ye.
2. **ENGELLER listesinde sonraki dilim maddeleri:** bkz. §1 notu.
3. **Dependencies tablosu checklist'in gerisinde:** MOD-0048 satırı "MDM'den tüketim yolu **ölçülmedi**" (`:582`) diyor, checklist
   ölçüldü diyor (`:1605-1609`); SEC-002 satırı "sağlayıcı ölçülmeden `ready-for-dev` yok" (`:584`), checklist "DL-2 için" (`:1601`).
4. **RCS-001 sahibi:** kapanış tablosu sahibi "CT + `.antigravity` onayı" yazıyor (`:607`); servis sahibi D44 ile Beste.
5. **"Kaynak ve kanıt" eski cümleleri:** "Kaynak kodu bu oturumda okunmadı" (`:1631`) ve "izin verilirse alınan her dosya" (`:1635`) —
   D45/3 sonrası "izin verildi" olarak güncellenmeli.

**D51 sonrası durum (2026-10-27, çalışma ağacı; tarihçe: yazıldığı anda commit edilmemişti; commit 1c444a2c5):** 1 → domain-config cümlesi D51/5 ile düzeltildi (MDM sahibi
rolünde sahip, D38); 2 → D51/1 ile kapandı (§1 notu); 3 → Dependencies MOD-0048 satırı "ölçüldü — bugün okuyucu yok, yeni port
`GatewayReferenceValueReader` deseniyle, DL-2"; SEC-002 satırı "dilim kapısı — DL-2 için" olarak checklist'le hizalandı; 4 → kapanış
tablosunda RCS-001 sahibi "Beste (D44)"; BL-565 backlog satırındaki sahip ("CT") Beste'ye bildirilecek — backlog onun alanıdır, CT
yazmaz (D51/5); 5 → "Kaynak ve kanıt" cümleleri D45/3 sonrasına çekildi.
