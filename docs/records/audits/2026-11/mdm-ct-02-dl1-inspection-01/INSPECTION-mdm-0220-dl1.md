---
record_id: MDM-CT-02-DL1-INSPECTION-MDM-0220-01
status: inceleme notu — dağıtım izni değil, onay değil
prepared_at_local: 2026-11-06
prepared_by: CT · Ürün ana verisi (chore/ct/mdm-ct-02 @13ca54a01)
wp: MDM-0220-DL1 (kuyruk mdm, execution/domains/master-data-management/work-packs/CT-QUEUE.tsv)
pack: execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md (sha256 91248a47b3c3e5bfa045ec2f8c7ce9f459edf0a214a33650ef22cb8f603eecd9)
discovery: docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/DISCOVERY-mod0220.txt @13ca54a01 (rapor-sha256 58deeec5a74ad4fe8de1fce9e6026c8395320dd56e76a1870ef367df05c72ae7)
---

# MDM-0220-DL1 inceleme notu — MOD-0220 §22 (TRM-14, `approvalStatus`)

Salt okunur ölçüm. Derleme, test, servis koşulmadı. Bütün kod okumaları `13ca54a0165b54ad2acab653909c23cf7c9787e7`
(bundan sonra `@13ca54a01`) ağacından, `git show` / `git grep` ile, satır numaralı yapıldı.

## 1. Okunan kaynaklar

| Kaynak | Ölçü |
|---|---|
| Paket §22 ve "Teslim dilimleri" (satır 883–983) | dosya sha256 `91248a47…` = onay satırındaki sha256 (ölçüldü, `shasum -a 256`) |
| `execution/approvals/approvals.tsv:17` ve `:33` | iki onay satırı, ikisi de `whole_pack=TRM-14`; son satır 2026-10-30 |
| `python3 scripts/pack_approval.py check <MOD-0220 pack> --ref 13ca54a01` | çıkış 0 — "onaylı — HIGH · onaylayan bestepullukcu-alt · 2026-10-30"; bu sürüm için 1 inceleme: codex UYGUN |
| `python3 scripts/pack_approval.py check <MOD-0126 pack> --ref 13ca54a01` | çıkış 0 — "onaylı — HIGH · onaylayan bestepullukcu-alt · 2026-11-06"; insan adımı 956f14fe3 (PR #385) |
| `pack_approval.slice_rule(…, 13ca54a01, <MOD-0220 pack>)` (katı ve dal okuması) | ikisi de `(True, ['DL-1'], 'TRM-14')` |
| Keşif raporu (929 satır) | `head -n 928 | shasum -a 256` = `58deeec5…` = rapordaki `rapor-sha256`; dosyanın kendi sha256'sı `7985d6e6…` (son satır dahil — `Keşif:` satırına yazılan değer `rapor-sha256`'dır) |
| `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` D9 (satır 14–31), D10/2 (satır 37–39) | XMC-001 onayı ve şartları |
| `…/treasury-intake-owner-decisions-05.md:886-888` (D82/2), `:1180-1183` (D97/5); `…-06.md:195` (D135/2) | TRM-14'ün Hazine tarafında "DL-1 dilim kapısı" olması |
| `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` §1–§4 | sözleşme, 13 okuma noktası, örnek gövde (satır 115–117) |
| `scripts/prompt_gate.py:967-987` (`slice_problems`), `:1117-1158` (`check_discovery`), `:1397-1406` (`real_names`); `scripts/pack_approval.py:1448-1469` (`slice_rule`), `:418-443` | WP adı / `whole_pack` ve keşif sorgusu eşleşmesi |
| Önceki hazırlık notu `mdm-ct-01/…/MOD-0220-DL1-hazirlik.md` (2026-10-28, @7495c4d27) | yalnız bilgi; her iddia aşağıda yeniden ölçüldü |
| Kod: MdmService LegalEntity dosyaları, `RepositoryBase.cs`, testler, tüketici test dosyaları | §2 |

## 2. §22 iddiaları — Expected HEAD'deki kodla ölçüm

`git diff --stat 03fb1257c 13ca54a01 -- <aşağıdaki MdmService dosyaları>` boş: §22.2'nin ölçtüğü ağaçtan bu yana bu dosyalar
değişmedi. MdmService'in geneli değişti (son commit `32e34dc69`, 2026-11-03, test), `Features/LegalEntity/` klasörünün son
değişikliği `ce58824b7` (2026-09-14).

| # | Paket iddiası | Kod (@13ca54a01) | Sonuç |
|---|---|---|---|
| 1 | Uç `GET /api/legal-entities/{id}/lookup-validation`, `[HasPermission("mdm.legal-entities.read")]` (`LegalEntitiesController.cs:48-54`) | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48-54` aynen; sınıf `[Authorize]` :11, rota `api/legal-entities` :13 | tutuyor (klasör adı `Api`, pakette yol verilmemiş) |
| 2 | 404: kayıt yok ya da `OperationalStatus != Active` (`ValidateLegalEntityReferenceHandler.cs:19-25`) | `…/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-25`; :20 koşul yalnız `OperationalStatus`; :22 `Fail("Legal Entity is not referenceable.", 404)` | tutuyor |
| 3 | Kiracı ve silinmiş süzgeci depoda (`LegalEntityRepository.cs:22, 41-43`) | Davranış tutuyor, **atıf kaymış**: `lookup-validation` `GetByIdAsync`'i kullanır, o da `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/RepositoryBase.cs:20-27`'dedir; süzgeç `TenantFilter` :69-71 (`TenantId == TenantId` ve `IsDeleted == false`). `LegalEntityRepository.cs:22` `ExistsByCodeAsync`, `:41-43` `UpdateEditableFieldsAsync` içindedir. `/lookup` listesi `LegalEntityRepository.cs:89-96` (`TenantFilter` + `Active`) | davranış tutuyor; satır atfı yanlış (belge kayması, kodu etkilemez) |
| 4 | `LegalEntityLookupDto` 6 üyeli (`LegalEntityModels.cs:100-106`); tek eşleme `ToLookupDto` (`:238-245`); `/lookup` da kullanır | `services/Diten.MdmService/src/Diten.MdmService.Application/Features/LegalEntity/LegalEntityModels.cs:100-106` ve `:238-245` aynen; `GetLegalEntitiesLookupHandler.cs:22` `Select(LegalEntityMappings.ToLookupDto)` | tutuyor |
| 5 | Tek yapım noktası `ToLookupDto`; başka `new LegalEntityLookupDto(` yok; tip MdmService dışında derlenmiş başvuru değil (§22.3/1, @4cf291b94) | `git grep -n LegalEntityLookupDto 13ca54a01 -- services frontend tests`: MdmService içinde 5 dosya (handler ×2, sorgu ×2, modeller); yapım yalnız `LegalEntityModels.cs:239` (`=> new(`); dışarıda yalnız açıklama satırı: `frontend/Diten.Web/Models/Inventory/CompanySelection.cs:82`, `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Upstream/LegalEntityMasterHttpClient.cs:10`; testlerde geçiş yok | tutuyor |
| 6 | `ApprovalStatus` varlıkta (`LegalEntity.cs:50`), dört değer | `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:50` (varsayılan `Draft`); `…/Domain/Enums/LegalEntityApprovalStatus.cs:7-10` `Draft=1, Submitted=2, Approved=3, Rejected=4`; `IsReferenceable` :60 = `OperationalStatus == Active && !IsDeleted` | tutuyor; `ToString().ToUpperInvariant()` → `DRAFT` / `SUBMITTED` / `APPROVED` / `REJECTED` |
| 7 | `OperationalStatus` altı değer (§22.2 kayma notu; AC-T2'nin beş Active-dışı değeri) | `…/Domain/Enums/LegalEntityOperationalStatus.cs:8-13` `Draft, InReview, Approved, Active, Suspended, Archived` | tutuyor |
| 8 | Mevcut MDM testi AC-T1/T2 için doğal yer | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityReferenceValidationTests.cs:12-80` (Active 200, yok 404, başka kiracı 404, Draft/Archived 404, silinmiş 404) — `InMemoryLegalEntityRepository`'yle; o çift süzgeci kendisi yazar (`InMemoryLegalEntityRepository.cs:26`) | **not:** kiracı ve silinmiş satırları bu testte üretim süzgecini değil kopyasını ölçer (Çekirdek kural 10). Talimat bu iki satırı gerçek depo üstünden HTTP testinde ister (§5) |
| 9 | HTTP / gerçek depo düzeneği | `services/Diten.MdmService/tests/Diten.MdmService.Api.Tests/MdmHttpHost.cs:26-45` (gerçek Program.cs, JWT, kiracı, `[HasPermission]`, gerçek Mongo depoları; kendi mongod'u, 27017'yi reddeder); `[CollectionDefinition]` :437-441; `lookup-validation` için HTTP testi bugün yok (`git grep` yalnız `TenantContradictionGuardTests.cs` metninde geçiyor) | AC-T2/AC-T3 HTTP testi yeni dosya olur |
| 10 | Mongo kaynağı | `…/Application.Tests/MdmTestMongo.cs` (BL-595): `DITEN_TEST_MONGOD` mutlak yol; macOS'ta verilmezse kırmızı (`OWNED_TEST_MONGO_UNAVAILABLE`); mongod test sürecinindir, PID + başlangıç zamanı + yol kaydıyla durdurulur. `MdmHttpHost.cs:415-430` aynı değişkeni okur. CI: `.github/workflows/service-tests.yml:65-69` (`mdm-app`, `mdm-api`) | işçi kendi mongod'unu elle başlatmaz; test süreçleri kendi mongod'larını açar/kapar (§5 NASIL) |
| 11 | `tests/known-failures.tsv` | MDM satırı yok (8 satır, hepsi `TenantArchitecture.ArchitectureTests`) | `mdm-app` / `mdm-api` için her kırmızı yenidir |
| 12 | "§22.7" (§22.4 AC-T4 metni ve sözleşme §3) | pakette §22.7 yok; kastedilen §22.6a | belge kayması, bilgi |
| 13 | §22 başlığı "(proposal, not approved)" ve "Status … `proposed`" | onay satırı ve inceleme var; D97/5 "Başlık değiştirilmez" | bilgi; yetki onay satırından gelir |

Önceki hazırlık notuyla (10-28) fark: notun §3'teki iki engeli kapandı — mdm kuyruğu kayıtlı
(`execution/delivery/ct-queue-registry.tsv:11`) ve MOD-0126 `ready-for-dev` + onaylı (yukarıda). Not "keşif raporu commit
edilmedi" diyordu; rapor artık `13ca54a01`'de.

## 3. Keşif bulguları — karar

Raporda 19 BULGU var (satır 31–49). Talimattaki `Keşif kararı 1..19` bu tabloyla birebirdir.

| n | Bulgu (rapor satırı) | Karar | Gerekçe |
|---|---|---|---|
| 1 | sor: 'legal entities' sahibi MOD-0220 (31) | parent | Bu iş MOD-0220'nin §22 / DL-1 dilimidir; kayıt sahibi aynı modül, yeni kimlik yok |
| 2 | sor: banka ana verisi / şirket banka hesabı sahibi MOD-0289 (32) | yeni | Ayrı modül (banka ana verisi); yalnız "legal entity" sözcüğü ortak; bu iş ona dokunmaz |
| 3 | registry MOD-0018-FU21 (33) | yeni | Platform yetki kapsamı çözümü (review); yalnız ad benzerliği; bu iş ona dokunmaz |
| 4 | registry MOD-0018-FU22 (34) | yeni | Ürün LE kapsamı izin kaydı (review); yalnız ad benzerliği |
| 5 | registry MOD-0290-FU03 (35) | yeni | Ürün–şirket kapsam ataması; LE deposunu okur ama `LegalEntityLookupDto` / handler kullanmaz (`git grep` ölçüldü); izinli dosyayı paylaşmaz |
| 6 | paket MOD-0220-legal-entity-foundation.md (36) | parent | Bu işin yetki paketi (§22, sha256 `91248a47…`, onay 2026-10-30) |
| 7 | paket MOD-0290-FU03 (37) | yeni | Bulgu 5'in paketi (`status: in-progress`); `LegalEntityModels.cs`'e 2026-09-15'ten beri hiçbir ref'te commit yok (`git log --all`) |
| 8 | paket CAND-CAP-0008-FU03 (38) | yeni | Çalışma takvimine LE kapsamı (Platform); `lookup-validation` tüketicisidir (sözleşme §3, `WorkingCalendarLegalEntityValidator`) — tüketici sınaması §22.6a Platform satırı, bu iş ona dokunmaz |
| 9 | paket MOD-0018-FU21 (39) | yeni | Bulgu 3'ün paketi |
| 10 | paket MOD-0018-FU22 (40) | yeni | Bulgu 4'ün paketi |
| 11 | paket MOD-0362-FU03 (41) | yeni | Yalnız `origin/test/crm-content-visit-e2e` dalında; main'de yok; çalışma takvimi konusu, bu iş ona dokunmaz |
| 12 | gateway 'legal-entities' → 5059 (42) | parent | MOD-0220'nin rota ailesi; §22.3/2 rota ve gateway değişmez |
| 13 | görev Q363 (43) | yeni | MVP-6 pilotu, Platform kapsam kaydı eksikliği (kapandı); bu işle yol paylaşmaz |
| 14 | görev Q396 (44) | yeni | MVP-6 iade akışı kabulü; bu işle ilgisi yok |
| 15 | görev Q469 (45) | yeni | Token'dan `legal_entity_id` kaldırma (Auth/Platform/Web/SupplyChain); `/lookup` tüketicisi — ek alan Web `CompanySelectionTests` koşusuyla izlenir, MdmService'e yazmaz |
| 16 | görev Q470 (46) | yeni | `/lookup` ucunun ölçümü (tüketici tarafı); bu iş `/lookup`'a yalnız aynı eşlemeyle bir alan ekler (§22.3/3) |
| 17 | görev Q477 (47) | yeni | Varsayılan rol izinleri düzeltmesi; izin değişmez (§22.3/2) |
| 18 | görev Q486 (48) | yeni | SupplyChain günlük şablonu; ilgisiz |
| 19 | görev TRM-14 (49) | parent | Hazine kuyruğundaki takip satırı; kod bu kuyruğun MDM-0220-DL1 satırında yazılır |

## 4. Kapı ölçümleri — dağıtımı bekleten bulgular

**A-1 · WP adı ≠ `whole_pack` (ENGEL, ölçüldü).** Paketin tek dilimi var (DL-1). `prompt_gate.py:983-985`: bütün dilimleri alan WP
için `whole != wp` ise ENGEL; `whole`, son onay satırının `whole_pack=` değeridir (`pack_approval.py:1466-1468`) ve ölçülen değeri
`TRM-14`'tür. Talimattaki `WP: MDM-0220-DL1` ile `TRM-14` eşleşmez → `check` dağıtım kipinde "dilim: bütün dilimler (1) tek WP'de
… istisna yalnız onay satırındaki whole_pack=MDM-0220-DL1 ile" ENGEL'i verir (`--prepare` kipinde yalnız BİLGİ). `Kapsanan dilim:
tamamı` aynı karşılaştırmaya gider (:978-979). `WP: TRM-14` yazmak da çözmez: kuyruk satırı `real_queue(queue, wp)` ile `id`'den
okunur (:1435) ve mdm kuyruğunda `TRM-14` satırı yok. Çözüm bir sahip/kule kararıdır (onay satırı ya da kuyruk kimliği); bu not
çözüm yazmaz.

**A-2 · Keşif sorgusu bu işe bağlanmıyor (ENGEL, ölçüldü).** Rapor `sorgu: Legal Entity Foundation`. `check_discovery`
(:1145-1146) sorguyu `real_names(row)` kümesinde arar: kuyruk satırının `item` metni ve MOD-0220'nin kayıt/Blueprint adı. Ölçüm:
kayıt adı da Blueprint adı da `Corporate Secretarial / Entity Management` (`module-id-registry.md:158`; `verify_module_id`
yükleyicisi); `item` metni başka. `norm('Legal Entity Foundation')` kümede yok → "keşif: sorgu … bu işin adı ya da modül adı
değil" ENGEL'i. Keşif, kayıt adıyla yeniden koşulup commit edilmeli; o zaman `Keşif:` satırı, sha256 ve kararlar yeni rapora
göre yeniden yazılır (bulgu kümesi değişebilir).

**A-3 · §22.5 "aynı WP" ile ayrı MDM WP'si.** Paket: "Code lands **together with MOD-0126 DL-1** …, as a separate commit"
(satır 955-956) ve "kod MOD-0126 DL-1 ile aynı WP'de" (satır 983). Kuyruk ayrı bir WP (`MDM-0220-DL1`) açtı ve D9'un 2. şartını
MOD-0126'nın `ready-for-dev` olmasıyla karşılanmış sayıyor. Hazine kararları TRM-14'ü MOD-0126 DL-1'in **dilim kapısı**
yaptı ("DL-1 başlamadan kapanır", D82/2; D135/2), bu da "aynı WP" metniyle çelişir. Yetki sırasında paket kazanır (Çekirdek 1);
paket revizyonu ya da sahip kararı olmadan "ayrı WP" kararı bu notta verilmez. Talimat bu farkı açık kayıt olarak taşır; işçi
yalnız "ayrı commit, izinli yollar" kuralına uyar.

**A-4 · Talimat incelemesi.** HIGH talimat için bağımsız inceleme kaydı (`--review`) henüz yok; Expected HEAD bu not ve
talimat commit edilince kule tarafından güncellenir (bugünkü `13ca54a01` bu notu içermez).

## 5. D9 (XMC-001) şartları

| Şart | Ölçüm |
|---|---|
| 1 en küçük, eklemeli | §22.3: tek alan, son konumsal üye; 404, `Referenceable`, rota, izin değişmez |
| 2 önce pack + CT-APR-02/01; kod yalnız iki pack `ready-for-dev` iken | MOD-0220 künyesi `ready-for-dev` (satır 9), §22 onaylı (HIGH, 2026-10-30); MOD-0126 künyesi `ready-for-dev` (`MOD-0126-payments-approvals.md:11` @13ca54a01), `approvals.tsv:58` sha256 `4fc00697…` = dosya, `pack_approval check` çıkış 0, insan adımı 956f14fe3 (PR #385, 2026-11-06, main'in atası) |
| 3 başka domain sahibine sorulur | D10/2: sahip K1 MDM domain sahibinin kararını kendisi verdi (decisions-02:37-39) |
| 4 tek yazar (K15) | `git log --all --since=2026-09-15` `LegalEntityModels.cs` ve `LegalEntityReferenceValidationTests.cs` için boş (bu makinenin ref'leri; başka makine görülmedi) |

## 6. AC-T4 tüketici testleri ve Platform

- Altı tüketici test dosyası @13ca54a01'de var (Manufacturing, SupplyChain, Planning ×2, Web, Platform). Hiçbiri
  `approvalStatus` geçirmez (`grep -il approvalStatus` boş): kendi gövdelerini kurarlar, değişmeden koşmaları **yeni alanlı gövde
  kanıtı değildir**. Talimat yalnız koşar, değiştirmez; sonuçlar "sahibinin kanıtıyla bağlanır" diye ayrı yazılır.
- Platform `services/Diten.Platform/tests/Diten.Platform.Application.Tests/TenantOrganization/MdmLegalEntityReferenceValidatorTests.cs`:
  §22.4 AC-T4'ün adlı listesinde **yok**; §22.6a'da Platform satırı "run existing tests (contract §3)" sahibi Platform. Sözleşme §3
  Platform'un iki okuma noktasını sayar. Karar: AC-T4 koşu kümesine girmez, talimatta "koşmadı — §22.6a Platform sahibinin" diye
  yazılır. (Önceki hazırlık notu da aynı ölçümü yapmıştı.)
- CRM'in testi yok (§22.4, §22.6a); başka servis, talimatta yalnız açık kayıt.
- MdmService'in tamamı (`mdm-app`, `mdm-api`) yerelde koşulmaz (yük kuralı); talimat süzgeçli koşar, tam koşu PR CI'ındadır
  (`service-tests.yml:65-69`). AC-T4'ün "existing MdmService tests" kısmı CI sonucuyla kapanır — açık kayıt.

## 7. Risk sınıfı

**HIGH.** Gerekçe: paylaşılan sözleşme (13 okuma noktası, sözleşme §3; SOP §9.1 "shared contract"); Hazine ödeme uygunluğu bu
alana bağlı (D10/1); kiracı sınırı AC-T2'de ölçülür; onay satırı HIGH ve `whole_pack` onayı HIGH sayar (`pack_approval.py:418-420`).
§9.1 sinyalleri: `sözleşme`, `kiracı`. Kart S-83 gereği kabul kanıtı betiğini kule yazar; talimatta yer tutucu:
`kule betiği: <yol> sha256 <…>` (kırmızı tabanıyla birlikte doldurulur).

## 8. Talimata giren ölçüm kararları

- AC-T2'nin kiracı ve silinmiş satırları gerçek depo üstünden (MdmHttpHost) ölçülür; in-memory çift yalnız işleyici dalı içindir.
- AC-T3 HTTP testi gövdeyi `JsonElement` ile adla okur, DTO tipine başvurmaz: alan çıkarma sabotajı derleme hatası değil kırmızı test
  verir. Sözleşme örneği testi Application.Tests'te durur.
- 404 kararı sabotajı (S1) korunan `ValidateLegalEntityReferenceHandler.cs:20`'ye geçici yazar; commit'e girmez, `git diff
  --exit-code` ile sıfırlandığı gösterilir. Bu, emsaldeki "korunan dosyaya yazmadan sabotaj" deseninden sapmadır; incelemede
  kabul ya da başka yol kararı gerekir (açık).
- Hedef ajan `backend-architect` (`.antigravity/agents/backend-architect.md:2-3`: .NET 8 / CQRS / Mongo; iş Application DTO'su +
  xUnit testleri); `testing-agent` yalnız test yazar, eşleme değişikliğini kapsamaz.

## 9. Açık kalanlar (kule / sahip)

1. A-1 `whole_pack=TRM-14` ↔ `MDM-0220-DL1` — sahip/kule kararı; çözülmeden dağıtım kapısı ENGEL.
2. A-2 keşfi kayıt adıyla (`Corporate Secretarial / Entity Management`) yeniden koş ve commit et; talimattaki `Keşif:` ve kararlar
   yeni rapora göre güncellenir.
3. A-3 §22.5 "MOD-0126 DL-1 ile aynı WP" ↔ ayrı MDM WP — paket revizyonu ya da sahip kararı.
4. Kule kanıt betiği (S-83) ve kırmızı tabanı; talimattaki yer tutucu.
5. Talimatın bağımsız incelemesi (HIGH) ve Expected HEAD güncellemesi.
6. S1 sabotajının korunan dosyaya geçici yazımı — inceleme kararı.
7. AC-T4: tüketici satırları (SupplyChain, Manufacturing, Planning, Inventory, Platform, HCM, SafetyStock, Web, CRM) sahiplerinin
   kanıtıyla; MdmService tam koşusu CI'da.
8. Belge kaymaları (kod etkisi yok): §22.2 depo satır atfı (§2 #3), "§22.7" atfı, §4/§12 durum adı kayması (§22.6 kapsam dışı).
