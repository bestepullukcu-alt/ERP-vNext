---
id: MOD-0040-FU02
name: Record Code Service
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: draft
approval_status: pending
owner: Beste (platform-shared-services, K2)
branch: feature/pss/mod-0040-fu02-record-code-service
started: 2026-10-30
target: 2026-11-06
form_field_count: 0
parent: MOD-0040
parent_meaning: Blueprint 8.8 MOD-0040 Canonical ID & Correlation Standard — repodaki MOD-0040 / MOD-0040-FU01 takma adları değil
backlog: BL-565
rule: RCS-001
consumer_module: MOD-0126, MOD-0119, MOD-0120, MOD-0124, MOD-0289, MOD-0217, MOD-0178
contract_draft: docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md
code_start: yok — CT-APR-02 bağımsız inceleme kaydı ve CT-APR-01 onay satırı olmadan kod izni yoktur
---

# MOD-0040-FU02 — Record Code Service (ortak kayıt kodu servisi, RCS-001)

> **Taslak — kod başlatma izni değildir.** `status: draft`, `approval_status: pending`. Bu paket, aynı sürümünün
> **CT-APR-02 bağımsız incelemesi** (`execution/approvals/reviews.tsv` + bulgu dosyası) ve **CT-APR-01 onay satırı**
> (`execution/approvals/approvals.tsv`, paketin bu sürümünün SHA-256'sına bağlı) main'e girmeden ve künye
> `ready-for-dev` olmadan hiçbir kod, yapılandırma, gizli bilgi ya da veri değişikliğini yetkilendirmez. Bir WP paketin
> tamamını alamaz; dilim tablosundaki (Bölüm 23) bir ya da birkaç dilimi alır. DL-2 ve sonrası bu sürümde **kod izni
> taşımaz** — her biri kendi paket revizyonu ve onayıyla açılır.
>
> **Kimlik (sahip kararı Beste 2026-10-30).** `parent = Blueprint MOD-0040 (Canonical ID & Correlation Standard)`,
> **repodaki MOD-0040 / MOD-0040-FU01 takma adları değil.** `execution/registries/module-id-registry.md:136` satırındaki
> MOD-0040 eski bir takma addır (Tenant Organization Foundation → MOD-0288) ve `:137` satırındaki MOD-0040-FU01 de eski
> takma addır (→ MOD-0288-FU01); kayıt dosyasının `:354` notu bu ayrımı zaten yazar. Bu paket kayıt dosyasına
> **yazmaz**; önerilen satır Bölüm 0'dadır, satırı CT yazar.
>
> **Kaynak.** RCS-001 (`.antigravity/rules/record-coding-standard.md`, tamamı okundu), sahip kararları K-1…K-8 (Bölüm 0.1),
> Treasury v0 çağrı sözleşmesi (KARAR D59/1, Y-8 — `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:605-610`,
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:80-85`). Bütün satır
> numaraları `origin/main @ 6d49f5823` üzerinde numaralı okumayla ölçüldü (2026-10-30); çıkarım olan yerler "çıkarım"
> ya da "CT önerisi" diye işaretlidir.

## 0. Kimlik ve ön kontrol (DCP-002)

| Adım | Sonuç (2026-10-30, `origin/main @ 6d49f5823`) |
|---|---|
| Blueprint araması | `Blueprint_Data` satırı **MOD-0040**: Module Name `Canonical ID & Correlation Standard`; Domain `1) Platform & Shared Services`; Suite `Integration & Interoperability`; Capability Group `Interface Governance`; Wave `W-2`; SoR `Canonical ID definitions, correlation rules (standard)`; Placement `Control-Plane Standard`; SoR Notes: "Historical project alias: MOD-0040 was once used for Tenant Organization Foundation. That legacy meaning is retired; canonical MOD-0040 remains Canonical ID & Correlation Standard." (Blueprint 8.8, sha256 `edbcb725…5939`) |
| Ön kontrol | `python3 -P -E -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → `OK  MOD-0040-FU02: proven against Blueprint/registry.` · **çıkış 0** |
| Keşif | `… --discover "record code"` → `KEŞİF RAPORU v2`; ÖZET: blueprint 0 · sor 0 · registry 0 · paket 0 · gateway 0 · görev 3 · BİLGİ 0; **çıkış 3** ("inceleme gerekli"); rapor-sha256 `21e60a99332f19ae59b1bcb00b355b15f036302809ff4b3bb38dcf8cef617c49`; 447 ref incelendi |
| Keşif bulguları (3, görev) | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` Q234 (MOD-0183 form alanı), Q406 (kanıtsız paket uyarısı), Q503 (MOD-0186 DL-1) — üçü de "record" / "code" kelimelerinden eşleşti; kayıt kodu servisiyle ilgisi yok (karar: **mükerrer değil**, gerekçe: konu başka modül) |
| Blueprint ek araması (çıkarım değil, ölçüm) | `Blueprint_Data` içinde `number range / numbering / document sequence / code generat / record code / business reference / identifier` deseni: yalnız MOD-0290 (`identifier` kelimesi, ürün kimliği ailesi — RCS-001 §4 KALSIN). Numaralandırma için ayrı Blueprint modülü yok |
| FU02 repoda geçiyor mu | `git grep -n "MOD-0040-FU02" origin/main` → **0 satır**; `git log --all --oneline -S "MOD-0040-FU02"` → **0 commit** (yalnız `astextplain` uyarıları, `.docx` filtresi yok). Araç farklı adla yeniden kullanımı yakalamaz; FU02 hiçbir dalda, hiçbir adla kullanılmamış |
| Ebeveyn kararı | Yeni modül değil; Blueprint MOD-0040'ın (Canonical ID definitions SoR) FU'su. Sahip kararı (Beste 2026-10-30) |

**Önerilen kayıt satırı (CT yazar; bu paket kayıt dosyasına dokunmaz):**

`| MOD-0040-FU02 | Record Code Service | record-code-service | Follow-up | planned / reserved |  | MOD-0040 | platform-shared-services | Child of **Blueprint** MOD-0040 (Canonical ID & Correlation Standard, W-2) — NOT the repo alias row MOD-0040 (Tenant Organization Foundation → MOD-0288) nor MOD-0040-FU01 (→ MOD-0288-FU01). RCS-001 shared record-code service (BL-565). Pack: MOD-0040-FU02-record-code-service.md (draft). Owner decision Beste 2026-10-30. |`

> ⚠ **Kayıt çözümleme riski (ölçüldü, çıkarım değil):** kayıt dosyasında `MOD-0040` satırı `Deprecated Alias → MOD-0288`'dir. Ebeveyni
> kayıt satırından çözen bir araç ya da okuyucu FU02'yi yanlışlıkla MOD-0288'e bağlayabilir. CT satırı yazarken
> ayrım notunu (`:354`) satıra da yazmalıdır (açık karar A-12).

## 0.1 Sahip kararları (bağlayıcı)

| # | Karar (Beste 2026-10-30, aksi yazılmadıkça) | Pakette nerede |
|---|---|---|
| K-1 | **Yapı:** Diten.Platform içinde **ayrı** servis/özellik (`Features/RecordCodes`). Doküman kimlik üreticisi (`DocumentIdentifierAllocationService`) **genişletilmez**; doküman kodları KALSIN. Yalnız deseni örnek alınır | Bölüm 2, 5, 6, 19 |
| K-2 | **v1 kapsamı** (hedef: servis v1 **2026-11-06** main'de; kesin sözleşme **2026-11-03**): ayırma (allocate) iç ucu + atomik sayaç (`findOneAndUpdate` + `$inc`, RCS-001 §5.8) + üç önek kapsamı + RCS-001 §3'teki bütün **OTOMATİK** türlerin önek kaydı + şirket kısa kodu okuması + idempotency + denetim + kiracı izolasyonu. **v1 DIŞI** (sonraki dilimler, kod izni yok): mevcut modüllerin (Organizasyon Birimi, Pozisyon, Görev Türü, PPM, Abonelik Planı) bağlanması; İKİSİ DE türlerinde öneri + gerekçeli değişiklik + AUD-001 kaydı; Tüzel Kişilik kısa kodu önerisi; ekranlar. KALSIN türler (ürün, CRM hesap/kampanya, doküman) ve sınıflandırılmamış mevcut üreticiler (Inventory, e-İmza, Procurement, SupplyChain, DMS) v1'de **değişmez** | Bölüm 2, 23 |
| K-3 | **Uç** (v0 Treasury'ye verildi; değişiklik **yalnız ekleme**): `POST /api/internal/record-codes/allocate`, servisten servise; kiracı gövdeden **değil**, güvenilir bağlamdan. Gövde `{ "recordType", "prefixScope": "LegalEntity" \| "Group" \| "Platform", "legalEntityId": "<guid>\|null", "idempotencyKey" }`. 200 `{ "code": "MYG-PAY-000042", "sequence": 42 }`; aynı `idempotencyKey` + aynı gövde → aynı kod; farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. Hatalar: tanımsız tür 422 `RECORD_TYPE_UNKNOWN`; türün izin vermediği kapsam ya da uyumsuz istek 422 `RECORD_CODE_SCOPE_INVALID`; geçerli kısa kod yok 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; grup öneki yok 409 `GROUP_PREFIX_MISSING`; başka kiracının şirketi 404; servis/kaynak erişilemez 503 (çağıran Create'i yapmaz, fail-closed) | Bölüm 3, 8, 12, 13, sözleşme taslağı |
| K-4 | **prefixScope** (O-14; Treasury önerisi + Beste ekleri): `LegalEntity` (varsayılan; `legalEntityId` zorunlu; önek şirket kısa kodu) · `Group` (`legalEntityId` boş; önek kiracının grup öneki — bugün `GMG`, belge `OrgPrefix` ile aynı değer, RCS-001 §5.3) · `Platform` (`legalEntityId` boş; öneksiz, `PLN-000001`). Tür başına izinli kapsamı **servis** belirler: BNK yalnız Group; BAC LegalEntity (şirketin kendi hesabı — `legalEntityId` = hesabın sahibi şirket) + Group (tedarikçi hesabı); PAY, APD, ARI, CPS, CTP ve MOD-0217 sözleşmesi yalnız LegalEntity; PLN Platform. Sayaç (kiracı, önek, tür) başına (§5.1). RCS-001 §3'e tür başına izinli kapsam sütunu önerilir | Bölüm 4.4, 8.4, 22 |
| K-5 | **Şirket kısa kodu (KARAR D91):** MOD-0220 Tüzel Kişilik `Code` alanından okunur (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:11`), `^[A-Z]{2,5}$` doğrulanır (RCS-001 §5.4: Tüzel Kişiliğin kendi kodu kısa koddur). Bugünkü kısa kodlar MYG, GPL, GMT; grup GMG. Okuma MDM lookup-validation üzerinden (kiracı + aktif). Geçersiz kod → 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; MDM erişilemez → 503. Önek koda bir kez yazılır, sonra değişmez (§5.2). **Platform'da ayrı kısa kod tablosu YOK** | Bölüm 8.3, 12, 13 |
| K-6 | **Şirket öneki biçimi:** RCS-001 §1 bağlayıcıdır (şirket öneki). K1 (`gmg-10`) "tek grup öneki" / "öneksiz" seçeneklerini değerlendiriyor; karar **2026-11-02**'ye kadar gelmezse sözleşme mevcut kurala göre dondurulur | Bölüm 24 (A-1) |
| K-7 | **MOD-0217 öneki `CTR` → `AGR`.** Procurement MOD-0144 bugün `CTR-` + 8 hex üretiyor (`services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Contract/Handlers/CommandHandlers/CreateContractHandler.cs:130-131`); aynı önek iki türde olamaz (§5.5). RCS-001 §3 satır değişikliği ayrı CT adımıdır (`.antigravity` korunan yol, sahip onaylı); bu paket yalnız `AGR`'yi kullanır ve bağımlılığı yazar. MOD-0144 / MOD-0217 kapsam çakışması açık soru (Cihan + Treasury) | Bölüm 7, 22, 24 (A-2, A-3) |
| K-8 | **Kimlik doğrulama:** mevcut Platform iç uç kimlik desenini ölç ve ona uy; çağıran servisin kimliği denetim kaydına **aktör Service** olarak yazılır. Yeni gizli bilgi/yapılandırma gerekiyorsa pakette yazılır, **değer uydurulmaz** | Bölüm 8.2, 14, 21 |
| RCS | RCS-001 ayrıca bağlar: 6 hane alt sınır, `999999`'dan sonra 7 hane (§5.7); boşluk kabul, numara geri verilmez, arşivli kod tekrar kullanılmaz (§5.8); yalnız ASCII `A–Z`, `0–9`, `-` (§5.10); içe aktarmada sayaç en büyük numaranın üstünden devam (§5.11) — **v1'de içe aktarma ucu yok**, kural DL-6'dadır | Bölüm 8, 16, 23 |

## 1. Module Summary

**Amaç.** RCS-001 §5.12'nin "tek ortak kod üretme servisi": kullanıcıya görünen iş kodunu (`{ŞİRKET}-{TÜR}-{SAYAÇ}`,
ör. `MYG-PAY-000042`) tek yerden, atomik, kiracı izole, idempotent ve denetimli olarak verir. Bugün ortak üretici yok;
dört ayrı sayaç var (CRM hesap, CRM kampanya, MDM ürün, Doküman — RCS-001 §5.12) ve Treasury (6 paket) ile WMS
(MOD-0178) yeni bir beşinci/altıncı kopya yazmamak için bu servisi bekliyor (D59/1; MOD-0178 Q-07 B, FU-0178-RCS-01).

**Kullanıcı.** İnsan değil, **servisler**: v1'de Diten.FinanceTreasuryService (PAY, APD, ARI, CPS, CTP) ve
Diten.MdmService (BNK, BAC, AGR). Ayırma her zaman bir kullanıcının başlattığı Create'in içinde olur; çağıran servis
kullanıcının token'ını iletir (Bölüm 8.2). Diten.InventoryService (WMS, MOD-0178) DL-8 ile bağlanır.

**v1 kapasite özeti (DL-1).**

1. `POST /api/internal/record-codes/allocate` — tek iç uç, Gateway'e açılmaz.
2. Atomik sayaç `(kiracı, önek, tür)` başına; hiç sıfırlanmaz; 6 hane alt sınır, sonra 7+ hane.
3. Üç önek kapsamı: `LegalEntity` (MDM `Code`'u), `Group` (kiracının grup öneki kaydı), `Platform` (öneksiz; v1'de
   yalnız süreç içi — açık karar A-6).
4. Tür önek kaydı: RCS-001 §3'teki OTOMATİK türler + `AGR` (K-7), tür başına izinli kapsam; kod kataloğu, §3'e
   testle sabitlenir (Bölüm 4.4; açık karar A-9).
5. İdempotency: `(kiracı, çağıran servis, idempotencyKey)` başına tek kod; gövde parmak iziyle.
6. Denetim: her yeni ayırma, ayırma defteriyle **aynı işlemde** merkezi denetim kutusuna (audit outbox) yazılır,
   aktör `Service` (Bölüm 21).
7. Kiracı izolasyonu: kiracı doğrulanmış kullanıcı token'ının `tenant_id`'sinden; gövde/başlık/sorgu dizesinden asla.

**v1'de olmayanlar** Bölüm 2 "Out of scope" ve Bölüm 23 DL-2…DL-10'dadır.

## 2. Ownership and Boundaries

### 2.1 In scope (DL-1 = v1)

- Ayırma iç ucu, istek yürütücüsü (kimlik + kiracı), komut, doğrulayıcı, işleyici.
- Üç koleksiyon: sayaç (`record_code_counters`), ayırma defteri (`record_code_allocations`), kiracının grup öneki kaydı
  (`record_code_group_prefixes`) — kiracı önce index'lerle (Bölüm 4).
- Tür önek kaydı (kod kataloğu `RecordCodeTypeCatalog`) ve onu RCS-001 §3'e sabitleyen test.
- MDM'den şirket kısa kodu okuyan yeni okuyucu (`MdmLegalEntityShortCodeReader`); mevcut
  `MdmLegalEntityReferenceValidator` **değişmez** (XMC-001; MOD-0288'in).
- Denetim yazıcısı (`RecordCodeAuditIntent`) — yeni iz; defter satırı CT'nin (Bölüm 21, A-10).
- Grup öneki kaydının tek yazma yolu: **açık karar A-5**'in seçtiği kaynak (CT önerisi: Platform yöneticisi için
  UI'sız, bir kez yazılan uç — Bölüm 3, 14).
- Sözleşme taslağı `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md` (bu commit); dondurma ayrı
  CT adımı (2026-11-03).

### 2.2 Out of scope (v1 dışı — kod izni yok)

- **Mevcut modüllerin bağlanması** (Organizasyon Birimi `ORG`, Pozisyon `POS` → DL-2; Görev Türü `TTY`, PPM
  `PFL/PRG/PRJ/INI/INV`, Abonelik Planı `PLN` → DL-4). Bu modüller v1'de bugünkü elle kod davranışını korur.
- **İKİSİ DE akışı** (öneri + gerekçeli değişiklik + AUD-001 eski/yeni kod + gerekçe) → DL-3.
- **Tüzel Kişilik kısa kodu önerisi** ve RCS-001 §5.4 "kullanıldıktan sonra değiştirilemez" kilidi (MDM tarafı) → DL-5.
- **İçe aktarma / sayaç yüksek su işareti** (§5.11) ve WMS geçici sayacından devam (FU-0178-RCS-01 önkoşulu) → DL-6.
- **Ekranlar** (tür kataloğu, sayaç görünümü, grup öneki) → DL-10. v1'de hiçbir `.cshtml`, rota, menü yok.
- **Kodun bağlanması / tüketilmesi** (MDM `CodeReservation` deseninin "tüket" adımı, RCS-001 §5.8 "rezerve et → yaz →
  tüket"): v1 yalnız **ayırır**; ayrılıp kullanılmayan kod bir boşluktur (§5.8 kabul eder). Bağlama ucu DL-3/DL-4 ile
  değerlendirilir (Bölüm 20, FU-RCS-04).
- **KALSIN türler** — CRM `ACC-YYYY-000000`, `CMP-YYYY-000000`; MDM ürün ailesi `GP/GS/LS/FG/IT` (§4); doküman
  `UID-0000001`, `GMG-QMS-SOP-0001`. Bu servis bunlar için kod **vermez** (422 `RECORD_TYPE_UNKNOWN`).
- **Sınıflandırılmamış mevcut üreticiler** (Inventory, e-İmza, Procurement — `CTR-` + 8 hex dahil, SupplyChain, DMS):
  v1'de değişmez; envanter Treasury'den beklenir (A-4).
- Arka plan işlerinden (kullanıcı token'ı olmayan) çağrı → DL-9 (servis kimliği token'ı; Bölüm 8.2).

### 2.3 Ownership decision

- Sahip: **Beste** (platform-shared-services, K2). Platform kodu bu domain'in (`execution/domains/platform-shared-services/domain-config.md` "Domain-Level Repo Scope": `services/Diten.Platform/**`).
- SoR: Blueprint MOD-0040 "Canonical ID definitions" — kayıt kodu kimliğin kullanıcıya görünen yüzüdür; ayırma
  defteri bu kodların tek kaynağıdır. Kayıt türünün kendisi (ödeme partisi, banka…) tüketicinin SoR'udur; bu servis
  yalnız kodu verir, kaydı tutmaz.
- MDM `LegalEntity.Code` MOD-0220'nindir; bu servis yalnız okur (K-5).
- Domain-config'in In-Scope listesinde MOD-0040-FU02 yok (ölçüldü, `domain-config.md` "In-Scope Modules"); ekleme CT
  adımıdır (A-12).

## 3. Owned Objects

| Tür | Ad | Not |
|---|---|---|
| Entity | `RecordCodeCounter` | `HybridEntity` (`TenantId` boşsa Platform kapsamı); Bölüm 4.1 |
| Entity | `RecordCodeAllocation` | `HybridEntity`; ekleme-yalnız defter; Bölüm 4.2 |
| Entity | `RecordCodeGroupPrefix` | `TenantScopedEntity`; kiracı başına tek satır; Bölüm 4.3 |
| Kod kataloğu | `RecordCodeTypeCatalog` | Domain, değişmez; tür öneki + sınıf + izinli kapsam + sahip modül + izinli çağıran; Bölüm 4.4 |
| Değer | `RecordCodeFormat` | biçimleme + doğrulama (`^[A-Z]{2,5}$`, `^[A-Z]{3}$`, 6 hane alt sınır) |
| Enum | `RecordCodePrefixScope` | `LegalEntity = 1`, `Group = 2`, `Platform = 3` (JSON'da ad olarak) |
| Repository | `IRecordCodeCounterRepository` | `NextAsync(prefix, recordType)` — atomik, upsert yarışı kurtarmalı |
| Repository | `IRecordCodeAllocationRepository` | `InsertAsync(session, …)`, `FindByIdempotencyKeyAsync(...)` — **güncelleme / silme / değiştirme yok** |
| Repository | `IRecordCodeGroupPrefixRepository` | `GetAsync()`, `InsertOnceAsync(...)` — güncelleme / silme yok |
| Port (Application) | `ILegalEntityShortCodeReader` | sonuç: `Found(code)` · `NotFound` · `Unavailable(reason)` |
| Komut | `AllocateRecordCodeCommand` | `IRequest<Response<RecordCodeAllocationResult>>`; yazma komutu |
| Komut (A-5 (a)) | `SetRecordCodeGroupPrefixCommand` | `IAuditableCommand` + `IAuditMetadataProvider` (yol a) |
| Sorgu | — | v1'de kiracı çapında satır döndüren sorgu yok |
| DTO | `RecordCodeAllocationResult` (`Code`, `Sequence`) | tek `RecordCodesModels.cs` içinde |
| API (iç) | `POST /api/internal/record-codes/allocate` | `InternalRecordCodesController`; Gateway'e açılmaz |
| API (yönetici, A-5 (a)) | `PUT /api/admin/tenants/{tenantId:guid}/record-code-group-prefix`, `GET` aynı yol | `TenantRecordCodeGroupPrefixController`; mevcut `/api/admin/tenants/{everything}` Gateway rotası (Bölüm 15) |
| Yapılandırma | `TrustedRecordCodeCredential` bölümü | tüketici başına kimlik bilgisi bağlaması; değer depoda yok (Bölüm 8.2) |
| Denetim yazıcısı | `RecordCodeAuditIntent.EnqueueAsync` / `.AppendRefusalAsync` | yeni iz `kayit-kodu-islem-ici` (CT kabulü, A-10) |
| İzin (A-5 (a)) | `platform.record-code-group-prefixes.read`, `platform.record-code-group-prefixes.update` | PKS-001 Tier 1; yalnız Platform aktörü |
| Frontend rota | — | yok (`shell: none`) |

## 4. Entity Fields

Ortak taban (`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:3-14`): `Id`,
`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `Version`. `HybridEntity` (`EntityVariations.cs:13-18`,
`TenantId` boş olabilir; emsal `WorkingCalendar : HybridEntity`, `WorkingCalendar.cs:28`) ve `TenantScopedEntity`
(`EntityVariations.cs:8-11`). **Silme yok:** bu üç koleksiyonda soft delete dahil hiçbir silme yolu yoktur — kod asla
yeniden kullanılmaz (§5.8); `IsDeleted` daima `false`, `DeletedAt` alanı uygulanmaz (gerekçe: silinebilir bir kayıt
"arşivli kodu tekrar kullanma" kuralını zayıflatır; Bölüm 8.7).

### 4.1 RecordCodeCounter — `record_code_counters`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid? | LegalEntity/Group: evet · Platform: **boş** | sunucuda, doğrulanmış token'dan; istekte asla |
| `Prefix` | string | evet (Platform'da boş dize `""`) | LegalEntity: MDM kısa kodu (`^[A-Z]{2,5}$`) · Group: grup öneki · Platform: `""` |
| `RecordType` | string | evet | `^[A-Z]{3}$`, katalogda olmalı |
| `LastNumber` | long | evet | son verilen numara; `$inc` 1; **asla azalmaz**, sıfırlanmaz |
| taban alanlar | — | — | `CreatedBy` = ilk ayırmanın çağıran servisi |

Index: `ux_record_code_counters_tenant_prefix_type` — `(TenantId, Prefix, RecordType)` **unique**. Sayaç anahtarında
`LegalEntityId` yoktur (§5.1: anahtar önek; MOD-0178 §22 "önek devri" notu ile aynı sonuç — kısa kod sonradan başka
şirkete geçerse aynı sayaç sürer, numara çakışmaz).

### 4.2 RecordCodeAllocation — `record_code_allocations` (ekleme-yalnız defter)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid? | LegalEntity/Group: evet · Platform: boş | sunucuda |
| `CallerService` | string | evet | kimlik bilgisinin `ConsumerService` değeri (ör. `DITENFINANCETREASURYSERVICE`); süreç içinde `DITENPLATFORM` |
| `IdempotencyKey` | string | evet | Bölüm 12 biçimi; kiracı + çağıran başına tekil |
| `RequestFingerprint` | string | evet | SHA-256 hex: `recordType` + `\n` + `prefixScope` + `\n` + (`legalEntityId` `D` biçimi ya da boş) |
| `RecordType` | string | evet | katalog türü |
| `PrefixScope` | enum | evet | `LegalEntity` / `Group` / `Platform` |
| `LegalEntityId` | Guid? | LegalEntity: evet · diğer: boş | |
| `Prefix` | string | evet | koda yazılan önek (Platform: `""`); **bir kez yazılır** (§5.2) |
| `Sequence` | long | evet | sayaçtan |
| `Code` | string | evet | `RecordCodeFormat` çıktısı; ASCII |
| `OnBehalfOfUserId` | Guid? | HTTP: evet · süreç içi: boş olabilir | doğrulanmış token'ın `sub`'ı |
| `CorrelationId` | string | evet | isteğin korelasyon kimliği |
| `AllocatedAtUtc` | DateTimeOffset | evet | UTC |

Index'ler: `ux_record_code_allocations_tenant_caller_key` — `(TenantId, CallerService, IdempotencyKey)` unique;
`ux_record_code_allocations_tenant_code` — `(TenantId, Code)` unique (kod kiracıda tekil; kod tür önekini taşıdığı için
türler arası da tekil); `ix_record_code_allocations_tenant_type_prefix_seq` — `(TenantId, RecordType, Prefix,
Sequence desc)` (DL-6 yüksek su işareti ve tanılama için; v1 yazar, okumaz).

### 4.3 RecordCodeGroupPrefix — `record_code_group_prefixes`

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | evet | sunucuda; A-5 (a)'da rota parametresi Platform aktörünün **hedef kiracısı** (`TargetTenantId`), gövdede değil |
| `GroupPrefix` | string | evet | `^[A-Z]{2,5}$`; ASCII; normalleştirme yok |
| `SetAtUtc` / `SetBy` | DateTimeOffset / string | evet | |

Index: `ux_record_code_group_prefixes_tenant` — `(TenantId)` unique. **Bir kez yazılır:** ikinci yazım (aynı ya da
farklı değer) 409 `GROUP_PREFIX_ALREADY_SET`; değiştirme ve silme yolu yok (§5.2, §5.4 ruhu: önek koda yazıldıktan
sonra bağ kopmasın). Değer depoda ya da kodda **varsayılan taşımaz**; `DocumentCodingOptions.OrgPrefix`'in kod
varsayılanı `"GMG"` (`…/DocumentManagementIdentifiersModels.cs:45`) **okunmaz** (A-5).

### 4.4 Tür önek kaydı — `RecordCodeTypeCatalog` (kod, değişmez)

Kayıt yeri **kod kataloğu**dur (CT önerisi; açık karar A-9 — koleksiyon seçeneği orada). Gerekçe: §3 tablosu
`.antigravity` altında CT onaylı bir kuraldır; kataloğu §3'e sabitleyen test kural ile çalışma zamanı arasındaki her
sapmayı kırmızı yapar, bir koleksiyon ise sessizce ayrışabilir ve kendi yönetim + denetim yolunu ister.

| TÜR | Kayıt türü (RCS-001 §3) | Sınıf | Sahip modül | İzinli kapsam | İzinli çağıran (v1) | v1 |
|---|---|---|---|---|---|---|
| `ORG` | Organizasyon Birimi | OTOMATİK | MOD-0288 | LegalEntity (CT önerisi, A-8; `OrganizationUnit.LegalEntityId` zorunlu — `OrganizationUnit.cs:9`) | süreç içi Platform (DL-2) | katalogda; HTTP çağıranı yok |
| `POS` | Pozisyon (kadro) | OTOMATİK | MOD-0288 | LegalEntity (CT önerisi, A-8) | süreç içi Platform (DL-2) | katalogda; HTTP çağıranı yok |
| `PAY` | Ödeme Partisi | OTOMATİK | MOD-0126 | LegalEntity | FinanceTreasury | ✓ |
| `APD` | AP Belgesi | OTOMATİK | MOD-0119 | LegalEntity | FinanceTreasury | ✓ |
| `ARI` | AR Alacak Faturası | OTOMATİK | MOD-0120 | LegalEntity | FinanceTreasury | ✓ |
| `CPS` | Nakit Pozisyonu Anlık Görüntüsü | OTOMATİK | MOD-0124 | LegalEntity | FinanceTreasury | ✓ |
| `CTP` | Nakit Eşik Politikası | OTOMATİK | MOD-0124 | LegalEntity | FinanceTreasury | ✓ |
| `BNK` | Banka | OTOMATİK | MOD-0289 | **yalnız Group** | Mdm | ✓ |
| `BAC` | Banka Hesabı | OTOMATİK | MOD-0289 | LegalEntity (şirket hesabı; `legalEntityId` = sahip şirket) **ve** Group (tedarikçi hesabı) | Mdm | ✓ |
| `AGR` | Sözleşme (MOD-0217) | OTOMATİK | MOD-0217 | LegalEntity (yöneten şirket) | Mdm | ✓ — §3 bugün `CTR` yazar (`record-coding-standard.md:70`); K-7, A-3 |
| `PLN` | Abonelik Planı | İKİSİ DE (§5.3) | Platform | Platform | süreç içi Platform (DL-4) | **açık karar A-7** (CT önerisi: v1 katalogda, yalnız ayırma) |

Katalogda olmayan her değer — KALSIN türleri (`ACC`, `CMP`, `GP`, `GS`, `LS`, `FG`, `IT`, `UID`), İKİSİ DE türleri
(`TTY`, `PFL`, `PRG`, `PRJ`, `INI`, `INV`), eski `CTR`, WMS'in henüz §3'te olmayan `PUT` / `PCK` / `OUT` / `PKG`'si
(FU-0178-RCS-02) — 422 `RECORD_TYPE_UNKNOWN`.

## 5. Repo Scope

**DL-1 (v1) — tam dosya yolları.** Yeni dosyalar (N) ve yalnız kayıt satırı eklenen mevcut dosyalar (M). Sık değişen
kompozisyon dosyalarının (`Program.cs`, `DependencyInjection.cs`) SHA-256'sı sabitlenmez (module-pack-standard §13).

API (`services/Diten.Platform/src/Diten.Platform.API/`):

- N `Controllers/Internal/InternalRecordCodesController.cs`
- N `Security/ITrustedRecordCodeRequestExecutor.cs`
- N `Security/TrustedRecordCodeRequestExecutor.cs`
- N `Security/ITrustedRecordCodeCredentialAuthenticator.cs`
- N `Security/TrustedRecordCodeCredentialAuthenticator.cs`
- N `Security/ITrustedRecordCodeJwtContext.cs`
- N `Security/TrustedRecordCodeJwtContext.cs`
- N `Security/TrustedRecordCodeAllocateRequestParser.cs`
- N `Configuration/TrustedRecordCodeCredentialOptions.cs`
- N `Models/RecordCodes/AllocateRecordCodeTransportRequest.cs`
- N (yalnız A-5 (a)) `Controllers/Admin/TenantRecordCodeGroupPrefixController.cs`
- M `Program.cs` — yalnız DI kaydı ve seçenek bağlama satırları

Application (`services/Diten.Platform/src/Diten.Platform.Application/`):

- N `Features/RecordCodes/Commands/AllocateRecordCodeCommand.cs`
- N `Features/RecordCodes/Handlers/CommandHandlers/AllocateRecordCodeHandler.cs`
- N `Features/RecordCodes/Validators/AllocateRecordCodeValidator.cs`
- N `Features/RecordCodes/RecordCodesModels.cs`
- N `Features/RecordCodes/RecordCodeReasonCodes.cs`
- N `Features/RecordCodes/RecordCodeAuditIntent.cs`
- N `Contracts/RecordCodes/ILegalEntityShortCodeReader.cs`
- N (yalnız A-5 (a)) `Features/RecordCodes/Commands/SetRecordCodeGroupPrefixCommand.cs`
- N (yalnız A-5 (a)) `Features/RecordCodes/Handlers/CommandHandlers/SetRecordCodeGroupPrefixHandler.cs`
- N (yalnız A-5 (a)) `Features/RecordCodes/Validators/SetRecordCodeGroupPrefixValidator.cs`
- M `DependencyInjection.cs` — yalnız kayıt satırları

Domain (`services/Diten.Platform/src/Diten.Platform.Domain/`):

- N `Entities/RecordCodes/RecordCodeCounter.cs`
- N `Entities/RecordCodes/RecordCodeAllocation.cs`
- N `Entities/RecordCodes/RecordCodeGroupPrefix.cs`
- N `Enums/RecordCodes/RecordCodePrefixScope.cs`
- N `Features/RecordCodes/RecordCodeTypeCatalog.cs`
- N `Features/RecordCodes/RecordCodeFormat.cs`
- N `Repositories/IRecordCodeRepositories.cs`

Infrastructure (`services/Diten.Platform/src/Diten.Platform.Infrastructure/`):

- N `Persistence/Repositories/RecordCodeRepositories.cs`
- N `Persistence/Schema/PlatformSchemaManifest.RecordCodes.cs`
- M `Persistence/Schema/PlatformSchemaManifest.cs` — yalnız `.Concat(RecordCodesCollections)` satırı
- M `Persistence/Schema/SchemaProfile.cs` — yalnız `RecordCodes = 14` (ek; mevcut değerler değişmez)
- M `Persistence/Schema/PlatformCollections.cs` — yalnız üç koleksiyon adı sabiti
- N `Services/Mdm/MdmLegalEntityShortCodeReader.cs`
- M `DependencyInjection.cs` — yalnız kayıt satırları (HttpClient + repo)

Yapılandırma: M `services/Diten.Platform/src/Diten.Platform.API/appsettings.json` — yalnız `TrustedRecordCodeCredential`
bölümünün **boş** iskeleti (gizli değer yok); `appsettings.Development.json`'a gizli değer **yazılmaz** (FU21 deseni:
`TrustedLegalEntityScopeCredential` iki dosyada da yok — ölçüldü; değer ortam değişkeni / gizli bilgi deposundan).

Testler (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/`):

- N `RecordCodes/RecordCodeFormatTests.cs`
- N `RecordCodes/RecordCodeTypeCatalogTests.cs`
- N `RecordCodes/AllocateRecordCodeHandlerTests.cs`
- N `RecordCodes/RecordCodeAllocationMongoTests.cs`
- N `RecordCodes/RecordCodeAuditDeliveryMongoTests.cs`
- N `RecordCodes/InternalRecordCodesHttpTests.cs`
- N `RecordCodes/TrustedRecordCodeRequestExecutorRefusalTests.cs`
- N `RecordCodes/MdmLegalEntityShortCodeReaderTests.cs`
- N `RecordCodes/RecordCodeGatewayExposureTests.cs`
- N (yalnız A-5 (a)) `RecordCodes/TenantRecordCodeGroupPrefixHttpTests.cs`
- M `Authorization/LoginOnlyEndpointGuardTests.cs` — yalnız `AnonymousInternalControllers` listesine
  `InternalRecordCodesController` (gerekçe yorumuyla; `:77-128` deseni)
- M `Schema/PlatformSchemaManifestTests.cs` — yalnız yeni profilin beklenen koleksiyon/index sayımı (gerekirse)

**DL-1'in dışında, CT adımı (geliştirici yazmaz):** `tests/architecture/audit-ledger/Diten.Platform.md` `## İzler`
satırı + `tests/architecture/audit-ledger/CT-DECISIONS.md` kararı + `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`
sabit sayısı (A-10); `.antigravity/rules/record-coding-standard.md` §3 (A-3); `execution/registries/module-id-registry.md`
satırı ve domain-config (A-12); sözleşmenin dondurulması (2026-11-03).

DL-2…DL-10'un yol listeleri kendi paket revizyonlarında yazılır.

## 6. Protected Paths

- `.antigravity/**` — RCS-001 §3 değişikliği (`CTR` → `AGR`, izinli kapsam sütunu) ayrı CT adımı, sahip onaylı.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementIdentifiers/**` ve
  `…/Infrastructure/Persistence/Repositories/DocumentManagementIdentifierRepositories.cs`,
  `…/Domain/Entities/DocumentManagement/DocumentIdentifierSequenceCounter.cs` — **genişletilmez** (K-1).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Mdm/MdmLegalEntityReferenceValidator.cs` — MOD-0288
  tüketicisi; değişmez (XMC-001).
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScope*.cs`,
  `…/Security/TrustedServiceTokenValidationExtensions.cs`, `…/Security/TrustedSourceAuditIntent*.cs` — emsal, değişmez.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**` (özellikle `AuditActorTypeResolver.cs`,
  `TransactionOwnedAuditCommands.cs`) ve `…/Domain/Enums/AuditEnums.cs` — değişmez (yeni kategori de eklenmez; Bölüm 21).
- `services/Diten.MdmService/**`, `services/Diten.AuthService/**`, `services/Diten.ProcurementService/**`,
  `services/Diten.InventoryService/**`, `services/Diten.Building.Blocks/**` ve diğer domain servisleri.
- `gateway/Diten.ApiGateway/**/ocelot.json` — integration-agent'ın; bu paket rota eklemez (Bölüm 15).
- `frontend/Diten.Web/**` — v1'de ekran yok.
- `execution/registries/**`, `tests/architecture/audit-ledger/**`, `docs/roadmap/backlog/**` — CT'nin.

## 7. Dependencies

| Bağımlılık | Bugünkü durum (ölçüldü) | DL-1 için |
|---|---|---|
| MDM lookup-validation `GET /api/legal-entities/{id}/lookup-validation` (MOD-0220; izin `mdm.legal-entities.read`) | var — `LegalEntitiesController.cs:48-54` (sözleşme taslağı `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md`); kayıt yok / silinmiş / başka kiracı / `OperationalStatus ≠ Active` → 404; yanıtta `code` | **gerekli**; değişiklik istenmez (1.0.0 alanları yeter, `approvalStatus` okunmaz) |
| Çağıranın kullanıcı token'ında `mdm.legal-entities.read` | MDM, iletilen kullanıcı token'ını yetkilendirir (Platform emsali `MdmLegalEntityReferenceValidator.cs:142-152`). Finans rolleri bu anahtarı bağımlı izin olarak alır (MOD-0018-FU27 K-4 / D72); MDM rolleri MDM'in | LegalEntity kapsamı için **gerekli**; yoksa MDM 403 → bu servis 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` (Bölüm 8.3) |
| Platform Mongo **işlemi** (replica set) | var — `PlatformTransactionExecutor.cs`, `IPlatformTransactionSession`; e-imza aynı yolu kullanıyor | gerekli (defter + denetim kutusu aynı işlemde) |
| Merkezi denetim kutusu `ITransactionalAuditOutboxWriter.TryEnqueueAsync` | var — `ITransactionalAuditOutboxWriter.cs:5-11`; emsal `ESignatureAuditIntent` (`ESignatureServices.cs:103-155`) | gerekli |
| **Yeni denetim izi kabulü** (`kayit-kodu-islem-ici`) | yok — defterde `## İzler` 7 satır (`tests/architecture/audit-ledger/Diten.Platform.md:8-17`), bu iz yok | **DL-1 main'e girmeden önce CT kararı** (A-10); karar yoksa komut "denetimsiz yeni komut" sayılır ve mimari test kırmızıdır |
| RCS-001 §3 `AGR` satırı + izinli kapsam sütunu | §3 bugün `CTR` (`record-coding-standard.md:70`); sütun yok | DL-1'i bekletmez: katalog testi `AGR`'yi adı konmuş tek istisna olarak tutar; DL-7'de istisna kalkar (A-3) |
| Tüketici kimlik bilgileri (`TrustedRecordCodeCredential:FinanceTreasury`, `:Mdm`) | yok | gerçek servisle uçtan uca kabul için gerekli; değerler ortam gizli bilgisi (MOD-0012), **depoda değer yok** |
| Grup öneki değeri (`GMG`) | sahip beyanı (K-4); depoda kiracı başına kayıt yok — `Tenant` / `TenantSettings` alanlarında grup öneki yok (`Tenant.cs:9-75`, `:128-134`) | A-5'in seçtiği kaynakla yazılır; yazılmadıysa Group kapsamı 409 `GROUP_PREFIX_MISSING` |
| Treasury tüketici portu `IRecordCodeAllocator.AllocateAsync(legalEntityId, recordType, idempotencyKey)` | v0 imzası (D59/1); `prefixScope` yok | `BNK` / tedarikçi `BAC` için port `prefixScope` taşımalı (A-11, Treasury CT + MDM sahibi) |
| BL-501 ortak iletici | MOD-0178 §8.8: olduğu gibi | **kullanılmaz** — bu komut Platform içindedir (Bölüm 21); tüketicinin kendi Create denetimi BL-501 ile onun işidir |

**Lookup & Reference Data kararı (module-pack-standard §13.1):** Platform lookup (`/api/lookups/{key}`) kullanılmaz;
yeni Platform lookup anahtarı yoktur. Şirket kısa kodu MDM/reference sınırındadır ve MDM lookup-validation'dan okunur;
Platform'da kopyası tutulmaz (K-5). Hardcoded yedek liste yoktur.

## 8. Runtime Constraints

### 8.1 Port, rota, servis

- Servis Diten.Platform, port **5057** (AGENTS.md §3). Uç yalnız `/api/internal/*` altında; Platform
  `TenantResolutionMiddleware` bu yolu atlar (emsal `InternalAuditController.cs:13`) — kiracıyı yürütücü kurar.
- İstek gövdesi üst sınırı **4 KB** → aşarsa 413 `RECORD_CODE_REQUEST_TOO_LARGE` (emsaller 32 KB taşıyor:
  `TrustedSourceAuditIntentRequestExecutor.cs:12`; bu gövde 4 alanlıdır).
- Zaman bütçesi: MDM okuması **3 s** (emsal `WorkingCalendarLegalEntityValidator`, `MdmLegalEntityReferenceValidator.cs:52-57`
  yorumu), isteğin tamamı **5 s**; bütçe dolarsa 503 (`LEGAL_ENTITY_SOURCE_UNAVAILABLE` ya da `RECORD_CODE_UNAVAILABLE`).
  Sözleşme 503 der (K-3); emsallerin 504'ü kullanılmaz.

### 8.2 İç uç kimliği (K-8) — ölçülen üç desen ve seçim

| Desen (ölçüldü) | Kiracı nereden | Çağıran ayırt edilir mi | Bu uç için |
|---|---|---|---|
| P1 ortak sır `X-Internal-Api-Key` (`AuthService:InternalApiKey`; `InternalAuditController.cs:24, 92-111`) | **gövdeden** (`:56-70`) | hayır (bütün servislerde aynı sır) | **uymaz** — K-3 "kiracı gövdeden değil" ve multi-tenancy kural 1 |
| P2 tüketici başına kimlik bilgisi + iletilen kullanıcı token'ı (MOD-0018-FU21; `TrustedLegalEntityScopeRequestExecutor.cs:12-14, 36-92`, `TrustedLegalEntityScopeJwtContext.cs:11-41`, `TrustedLegalEntityScopeCredentialAuthenticator.cs:58-95`) | doğrulanmış kullanıcı token'ının `tenant_id`'si | evet (kimlik bilgisinin `ConsumerService`'i) | **CT önerisi (A-13)** |
| P3 RS256 servis kimliği token'ı (MOD-0033-FU02; `TrustedServiceTokenValidationExtensions.cs:16-23`, `TrustedSourceAuditIntentServiceIdentity.cs:32-55`) | token'ın `tenant_id`'si | evet (`service_name`) | v1 için **uygulanamaz**: Auth bugün yalnız `Diten.MDM` için ve üç hedef kitle için token verir (`services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5-17`) — Auth değişikliği (Ali) ister; ayrıca kullanıcı token'ı olmadığı için MDM okuması yetkilendirilemez. DL-9 |

**Seçim (CT önerisi, A-13; sahip Beste, 2026-11-03'ten önce):** P2. Uygulama:

1. Başlıklar **tam bir kez**: `X-Record-Code-Credential-Id`, `X-Record-Code-Credential`, `X-Record-Code-Audience`
   (hedef kitle sabiti `TRUSTED_RECORD_CODE_ALLOCATE`), `Authorization: Bearer <kullanıcı JWT'si>`. Eksik/çift → 401
   `RECORD_CODE_UNAUTHENTICATED`.
2. Kimlik bilgisi doğrulaması: sabit zamanlı karşılaştırma, `PreviousSecret` + `PreviousValidUntilUtc` döndürme, `IsRevoked`
   (FU21 `TrustedLegalEntityScopeCredentialAuthenticator.cs:58-95, 117` ile aynı). FU21'den farkı: **birden çok bağlama**
   (`FinanceTreasury`, `Mdm`; DL-8'de `Inventory`) ve bağlama başına `AllowedRecordTypes` listesi. Yapılandırma geçersizse
   (boş, tekrarlı, katalog dışı tür) bütün istekler reddedilir (fail-closed).
3. Kullanıcı token'ı Platform'un varsayılan JwtBearer şemasıyla doğrulanır (`TrustedLegalEntityScopeJwtContext.cs:18`);
   tam bir `tenant_id` (Guid, boş değil), tam bir `sub` (Guid), `actor_type = tenant_user` (`:25-38`). Aksi → 401/403.
4. **`X-Tenant-Id` başlığı varsa 403** `RECORD_CODE_FORBIDDEN` (FU21 deseni `TrustedLegalEntityScopeJwtContext.cs:11-16`);
   gövde ve sorgu dizesindeki kiracı alanı okunmaz — gövdede `tenantId` üyesi 400 (katı ayrıştırıcı).
5. Kiracı bağlamı `TenantScope.Begin(_tenantContext, jwtTenant)` ile kurulur (`TrustedLegalEntityScopeRequestExecutor.cs:74`).
6. Kullanıcı izni **istenmez**: yetki kimlik bilgisidir (D74/1 emsali, MOD-0018-FU27 Bölüm 8.7). Kullanıcı token'ı yalnız
   kiracıyı ve `OnBehalfOfUserId`'yi belirler ve MDM'e iletilir.
7. **Sınır (kabul edilen risk, A-13'te yazılır):** arka plan işi (kullanıcı token'ı yok) bu ucu çağıramaz → 401. v1
   tüketicilerinin hepsi kullanıcı başlatmalı: MOD-0124 "Arka plan işi yok" (`MOD-0124-cash-positioning.md:1488`),
   MOD-0178 S1 görev kaynağı elle (`MOD-0178-putaway-picking-packing.md:506-514`). İleride otomatik tetikleyici (MOD-0178
   Q-M2-1 (B)) gelirse DL-9.

**Yeni yapılandırma (değer yok, K-8):** bölüm `TrustedRecordCodeCredential`; bağlama başına `Identifier`,
`ConsumerService` (`DITENFINANCETREASURYSERVICE`, `DITENMDMSERVICE`), `AllowedAudience`, `ActiveSecret`,
`PreviousSecret`, `PreviousValidUntilUtc`, `IsRevoked`, `AllowedRecordTypes`. Gizli değerler ortam değişkeni / gizli
bilgi deposundan (MOD-0012); `appsettings*.json`'da değer yok. Çağıran tarafların istemci yapılandırması kendi
paketlerinindir (MOD-0126 DL-1, MOD-0289 / MOD-0217 MDM bağdaştırıcısı).

### 8.3 Şirket kısa kodu okuması (K-5)

- Yeni `MdmLegalEntityShortCodeReader` (`ILegalEntityShortCodeReader`): `GET {MdmService:BaseUrl}/api/legal-entities/{id:D}/lookup-validation`;
  `X-Tenant-Id` = doğrulanmış token kiracısı; `Authorization` = çağıranın ilettiği kullanıcı token'ı (emsal
  `MdmLegalEntityReferenceValidator.cs:104-109, 142-152`); tenancy istemcide, `DelegatingHandler`'da değil (`:19-27` uyarısı).
- Sınıflandırma (mevcut doğrulayıcıdan farkı: o her hatayı 404'e çeviriyor — `:115-128, 154-155`; burada 503 ayrı):

| MDM yanıtı | Sonuç | Uç cevabı |
|---|---|---|
| 200, `data.legalEntityId` = istenen, `referenceable = true`, `lifecycleState = ACTIVE`, `code` `^[A-Z]{2,5}$` | `Found(code)` | ayırma sürer |
| 200 tutarlı ama `code` kalıba uymuyor (boş, küçük harf, `GMG-CH`, 1 ya da 6+ harf, ASCII dışı, baş/son boşluk) | geçerli kısa kod yok | 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` (normalleştirme yok: büyütme, kırpma, harf çevirme yapılmaz) |
| 404 | yok / silinmiş / başka kiracı / aktif değil | 404 `LEGAL_ENTITY_NOT_FOUND` (sızıntısız, tek gövde) |
| 200 ama `data` yok, ayrıştırılamıyor, `legalEntityId` farklı, `referenceable = false` ya da `lifecycleState ≠ ACTIVE` | geçersiz / çelişkili yanıt | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |
| 400 (ör. "Tenant mismatch"), 401, 403, 5xx, zaman aşımı (3 s), bağlantı yok | erişilemiyor | 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE` |

- Okuma **her yeni ayırmada** yapılır; önbellek yok (kısa kod değişirse yeni kayıt yeni öneki alır, eski kodlar
  değişmez — §5.2). İdempotent tekrar MDM'i çağırmaz (Bölüm 8.5).

### 8.4 Biçim ve sayaç

- Kod: `LegalEntity` / `Group` → `{ÖNEK}-{TÜR}-{SAYAÇ}`; `Platform` → `{TÜR}-{SAYAÇ}`. `SAYAÇ` = `Sequence` ondalık,
  en az 6 hane sıfır dolgulu; `999999`'dan sonra `1000000` (7 hane), sıfıra dönmez, kesilmez (§5.7). `long`.
- Sayaç: `findOneAndUpdate` + `$inc LastNumber 1`, `upsert`, `ReturnDocument.After` — **işlem dışında** (MDM
  `CodeReservationRepository.cs:549-600` deseni). İlk ayırma yarışında unique index ihlali (E11000) olursa upsert'süz
  yeniden dener (MDM `:579` "Another caller created this tenant's counter…", en çok 3 deneme `:16`). ⚠ Ölçüldü:
  doküman sayacı `DocumentManagementIdentifierRepositories.cs:58-90` bu kurtarmayı **yapmıyor** — burada örnek alınmaz.
- Boşluk kabul: sayaç alındıktan sonra defter/denetim işlemi düşerse numara geri verilmez (§5.8); sonraki ayırma bir
  üst numarayı alır. Ardışıklık garanti değildir; tekillik garantidir.
- Kod tekilliği `(TenantId, Code)` unique index'le de korunur (ikinci savunma).

### 8.5 Değerlendirme sırası ve idempotency

1. Boyut → 413. 2. Başlıklar / kimlik bilgisi / kullanıcı token'ı → 401 / 403. 3. Gövde → 400 `RECORD_CODE_REQUEST_INVALID`.
4. **İdempotency araması** `(TenantId, CallerService, IdempotencyKey)`: varsa parmak izi eşitse **200 aynı kod + aynı
   sequence** (sayaç ilerlemez, MDM çağrılmaz, yeni denetim satırı yok); değilse 400 `IDEMPOTENCY_KEY_REUSED`.
5. Tür katalogda mı → 422 `RECORD_TYPE_UNKNOWN`. 6. Tür bu çağıranın `AllowedRecordTypes`'ında mı → 403 `RECORD_CODE_FORBIDDEN`.
7. Kapsam kuralları (Bölüm 12) → 422 `RECORD_CODE_SCOPE_INVALID`. 8. Önek: Group → grup öneki kaydı (yoksa 409
   `GROUP_PREFIX_MISSING`); LegalEntity → MDM (Bölüm 8.3). 9. Sayaç `$inc` (depo hatası → 503 `RECORD_CODE_UNAVAILABLE`).
10. **Tek işlem:** defter satırı + denetim kutusu satırı. Defterde `(TenantId, CallerService, IdempotencyKey)` ihlali
    (eşzamanlı aynı anahtar) → işlem iptal, kazanan satır okunur, adım 4'teki kurala göre 200 ya da 400 (bu yolda harcanan
    sayaç numarası boşluk olur). Diğer işlem hatası → 503 `RECORD_CODE_UNAVAILABLE`, kod verilmez.
11. 200 `{ code, sequence }`.

- Anahtar kapsamı çağıran servis + kiracıdır: iki servis aynı anahtarı kullanabilir; iki kiracı birbirini göremez.
- Anahtarın ömrü sınırsızdır (defter silinmez); aynı anahtar aylar sonra da aynı kodu döner.
- Çağıranın kuralı: zaman aşımı ya da 503 sonrası **aynı anahtarla** yeniden dener; yeni anahtar yeni kod demektir.

### 8.6 Eşzamanlılık

- Farklı anahtarlı eşzamanlı ayırmalar aynı sayaçta farklı numara alır (tek atomik `$inc`).
- Sayaç işlem dışında olduğu için sıcak sayaçta işlem yazım çakışması (write conflict) doğmaz; işlem yalnız iki yeni
  belgeyi (defter + kutu) ekler.

### 8.7 Platform kapsamı ve `GlobalEntity` gerekçesi (HybridEntity)

- `Platform` kapsamındaki satırlar (`PLN`) kiracıya ait değildir: Abonelik Planı bütün kiracıların ortak kataloğudur
  (RCS-001 §5.3; multi-tenancy "Platform Global Catalog İstisnası"). Bu yüzden sayaç ve defter `HybridEntity`'dir:
  Platform satırında `TenantId` **boş**, kiracı satırında dolu. DTO / istek / form gövdesinde `TenantId` hiçbir zaman yoktur.
- v1'de Platform kapsamı **yalnız süreç içi** (Platform'un kendi modülleri, DL-4) çağrılabilir; HTTP ucundan
  `prefixScope = Platform` → 422 `RECORD_CODE_SCOPE_INVALID` (açık karar A-6). Gerekçe: kiracıya bağlı bir servis
  kimliğinin kiracı-üstü bir kataloğun sayacını ilerletmesi yanlış sahiplik olur; HTTP'de Platform kapsamlı tüketici yok.
- Süreç içi çağrıda kiracı bağlamı Platform bağlamıdır (`SetPlatformContext`, `module-self-registration-standard` emsali);
  "sabit/varsayılan kiracı" değildir — `TenantId` alanı boştur, bir değerle doldurulmaz.

### 8.8 Veri kapsamı (SEC-002) — kapsam cümlesi

v1'de kiracı çapında satır döndüren sorgu **yoktur**; tek uç bir yazma/ayırma ucudur ve yalnız istekte adı verilen
şirket için kod döndürür. Şirket kapsamı (kullanıcı bu şirket için kayıt açabilir mi) **çağıranın** Create komutunda,
kendi kapsam portuyla denetlenir (MOD-0126 / MOD-0289 / MOD-0217 SEC-002 dilimleri); Platform bu uçta kapsam sormaz,
çünkü yetkinin sınırı kimlik bilgisidir ve kod bir şirket verisi sızdırmaz (yalnız sayı). Tenant-wide izni yoktur.
DL-10 ekranları (sayaç / defter listesi) kiracı çapında satır döndüreceği için kapsam cümlesini o revizyonda yazar.

### 8.9 Yerelleştirme

Platform v1'de kullanıcıya görünen metin üretmez (ekran yok). Uç `reason_code` döner; `errors[]` metni İngilizce
tanılama metnidir ve **tüketici ekranında gösterilmez** — tüketici `reason_code`'u kendi kaynak dosyasıyla (kiracı
modülü 7 dil) çevirir. `.resx` değişikliği yok.

### 8.10 Günlük ve gözlem

Her ret `reason_code`, çağıran servis, kiracı, korelasyon kimliğiyle **uyarı** düzeyinde loglanır. Kimlik bilgisi sırrı,
`Authorization` başlığı, JWT ve MDM yanıt gövdesi loglanmaz (security-jwt "Loglama Yasağı").

## 9. Layout & Shell Contract

`shell: none`. Bu FU hiçbir Razor görünümü, ön yüz rotası, DataTable ya da gezinme öğesi sahiplenmez; hiçbir `.cshtml`
oluşturulmaz, bu yüzden `Layout = "_LayoutPlatformAdmin"` ya da `Layout = "_LayoutTenantShell"` yazılacak dosya yoktur.
DL-10 ekranları Platform yönetici kabuğunda olacaktır (`Layout = "_LayoutPlatformAdmin"`, `Views/Platform/RecordCodes/`)
ve o revizyonda bu bölüm yeniden yazılır.

## 10. Backend File Convention

Golden Reference klasör/ad kuralı (`.antigravity/rules/module-pack-standard.md` §4) birebir; `DocumentManagementIdentifiers`'ın
`Services/` alt klasörü (sapma) **örnek alınmaz**:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/RecordCodes/
├── Commands/
│   ├── AllocateRecordCodeCommand.cs            (sealed record, IRequest<Response<RecordCodeAllocationResult>>)
│   └── SetRecordCodeGroupPrefixCommand.cs      (yalnız A-5 (a); IAuditableCommand + IAuditMetadataProvider)
├── Handlers/
│   └── CommandHandlers/
│       ├── AllocateRecordCodeHandler.cs        (sealed class; Command suffix YOK)
│       └── SetRecordCodeGroupPrefixHandler.cs  (yalnız A-5 (a))
├── Validators/
│   ├── AllocateRecordCodeValidator.cs          (Command suffix YOK)
│   └── SetRecordCodeGroupPrefixValidator.cs    (yalnız A-5 (a))
├── RecordCodeReasonCodes.cs
├── RecordCodeAuditIntent.cs
└── RecordCodesModels.cs                        ← DTO'lar tek dosyada
```

- `Queries/` ve `Handlers/QueryHandlers/` v1'de yoktur (sorgu yok); A-5 (a)'daki `GET` grup öneki okuması eklenirse
  `Queries/GetRecordCodeGroupPrefixQuery.cs` + `Handlers/QueryHandlers/GetRecordCodeGroupPrefixHandler.cs` olur.
- Handler içi sorumluluk (`handler-design`): işleyici sırayı yönetir; biçimleme `RecordCodeFormat`'ta (Domain), kapsam
  kuralı `RecordCodeTypeCatalog`'da (Domain), MDM erişimi portta, Mongo erişimi depoda.
- Controller `CustomBaseController`'dan türer ve `Response<T>` döner (`services/Diten.Platform/src/Diten.Platform.Application/Common/Response.cs:7-16`:
  `data`, `statusCode`, `isSuccessful`, `errors`, `reason_code`, `correlation_id`).
- MediatR 4 zorunlu davranış (Validation, Logging, Exception, Performance) geçerlidir; `AuditBehavior` bu komutu
  yazmaz (komut `IAuditableCommand` değildir; denetim işleyicinin içindedir — Bölüm 21).

## 11. Frontend File Contract

Yok. `golden_reference: none`, `form_field_count: 0`. Tarayıcı bu ucu hiçbir zaman çağırmaz (Gateway'de rotası yok,
Bölüm 15). Sayfa yardımı (UXH-001) ve tur gerekmez: sayfa yok.

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB düzeyi | Ön kontrol |
|---|---|---|---|---|
| gövde | evet | JSON nesnesi; **bilinmeyen üye → 400** (ör. `tenantId`, `code`, `sequence`); tekrarlı üye → 400; ≤ 4 KB | — | ayrıştırıcı |
| `recordType` | evet | string, `^[A-Z]{3}$`, büyük/küçük harfe duyarlı; normalleştirme yok (`pay` → 400) | — | katalog (422 `RECORD_TYPE_UNKNOWN`), çağıran listesi (403) |
| `prefixScope` | hayır | `LegalEntity` \| `Group` \| `Platform` (tam yazım, duyarlı); yoksa `LegalEntity` (v0 uyumu); başka değer → 400 | — | türün izinli kapsamı (422 `RECORD_CODE_SCOPE_INVALID`) |
| `legalEntityId` | kapsama bağlı | `LegalEntity`: zorunlu, Guid, `00000000-…` değil · `Group` / `Platform`: yok ya da `null` | — | `LegalEntity` + boş → 422 `RECORD_CODE_SCOPE_INVALID`; `Group`/`Platform` + dolu → 422 `RECORD_CODE_SCOPE_INVALID`; Guid değil → 400 |
| `idempotencyKey` | evet | string, 1–200 karakter, yazdırılabilir ASCII `!`…`~` (boşluk yok) — dondurmada kesinleşir (A-11) | unique `(TenantId, CallerService, IdempotencyKey)` | defter araması |
| Grup öneki (A-5 (a)) | evet | `^[A-Z]{2,5}$`, ASCII, normalleştirme yok | unique `(TenantId)` | var → 409 `GROUP_PREFIX_ALREADY_SET` |
| Üretilen kod | — | LegalEntity/Group `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$`; Platform `^[A-Z]{3}-[0-9]{6,}$` | unique `(TenantId, Code)` | `RecordCodeFormat` |

İlişkili kontroller: `prefixScope = Group` → `legalEntityId` boş **ve** türün izinli kapsamında `Group` olmalı;
`prefixScope = LegalEntity` → `legalEntityId` dolu **ve** izinli kapsamda `LegalEntity` olmalı; `prefixScope = Platform`
→ HTTP'de her zaman 422 (A-6).

## 13. Failure Path to Verify

- **Aynı anahtar, aynı gövde (tekrar)** — Beklenen: 200, ilk kod ve sequence; sayaç ilerlemez; MDM çağrılmaz; yeni denetim satırı yok.
- **Aynı anahtar, farklı gövde** — Beklenen: 400 `IDEMPOTENCY_KEY_REUSED`; sayaç ilerlemez; kayıt yok.
- **Eşzamanlı aynı anahtar** — Beklenen: tek defter satırı; iki istek de aynı kodu alır (ya da biri 400 değilse); en çok bir boşluk.
- **Eşzamanlı farklı anahtarlar (yarış)** — Beklenen: hepsi farklı kod; ilk sayaç yaratma yarışı 500 üretmez.
- **Tanımsız / KALSIN tür** — Beklenen: 422 `RECORD_TYPE_UNKNOWN`; sayaç yaratılmaz.
- **Bilinen tür, çağırana izinsiz** (ör. Mdm kimlik bilgisiyle `PAY`) — Beklenen: 403 `RECORD_CODE_FORBIDDEN`.
- **Uyumsuz kapsam** (`BNK` + LegalEntity; `PAY` + Group; Group + dolu `legalEntityId`; LegalEntity + boş; HTTP Platform) — Beklenen: 422 `RECORD_CODE_SCOPE_INVALID`; MDM çağrılmaz.
- **Geçersiz kısa kod** (`GMG-CH`, `my`, `M`, `ABCDEF`, `MYĞ`, `""`, `" MYG"`) — Beklenen: 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`; sayaç yaratılmaz.
- **Grup öneki yok** — Beklenen: 409 `GROUP_PREFIX_MISSING`; `GMG` varsayılanına düşülmez.
- **Başka kiracının şirketi / olmayan / aktif olmayan şirket** — Beklenen: 404 `LEGAL_ENTITY_NOT_FOUND`; tek gövde (sızıntısız); kayıt yok.
- **MDM erişilemez** (kapalı, 5xx, 3 s aşımı, 401/403, çelişkili gövde) — Beklenen: 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`; kod verilmez; sayaç ilerlemez.
- **Mongo erişilemez / işlem düşer** — Beklenen: 503 `RECORD_CODE_UNAVAILABLE`; kod verilmez; ilerlemiş sayaç numarası boşluk olarak kalır.
- **Denetim kutusu satırı yazılamaz** — Beklenen: işlem iptal, 503 `RECORD_CODE_UNAVAILABLE`; defter satırı yok (kod "denetimsiz" verilmez).
- **Yetkisiz aktör** (kimlik bilgisi yok / yanlış / iptal / yanlış hedef kitle; kullanıcı token'ı yok / süresi dolmuş / `tenant_id` yok / `actor_type ≠ tenant_user`; `X-Tenant-Id` var) — Beklenen: 401 ya da 403; işleyici çağrılmaz; gövde okunmadan önce ret.
- **Gövdede kiracı** (`tenantId` üyesi) — Beklenen: 400; kiracı değişmez.
- **Gövde 4 KB üstü** — Beklenen: 413.
- **Sayaç sınırı** (`999999`) — Beklenen: `…-999999`, sonra `…-1000000`.
- **Eşzamanlılık çakışması (A-5 (a) grup öneki)** — Beklenen: iki eşzamanlı ilk yazımdan biri 200, diğeri 409 `GROUP_PREFIX_ALREADY_SET`; sessiz üzerine yazma YOK.

## 14. Authorization Convention

- **İç uç:** `[ApiController]`, `[AllowAnonymous]` (çerçeve düzeyinde), `[Route("api/internal/record-codes")]`; kapı
  yürütücüdedir (Bölüm 8.2). Emsal ve şart: `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` listesine yalnız
  her reddi adı konmuş bir testi kırmızıya döndüren controller girer (`LoginOnlyEndpointGuardTests.cs:99-113` yorumu);
  bu controller de o şartla girer. `[HasPermission]` yoktur, çünkü çağıran bir servistir (security-jwt "Varsayılan Olarak
  Yasak": anonim erişim yalnız özel mimari izinle — bu paket o izni ister, CT-APR-01).
- **Aktör:** servis (kimlik bilgisi bağlaması) + adına çalışılan kiracı kullanıcısı (`tenant_user`). Denetimde aktör türü
  `Service` (K-8; Bölüm 21).
- **İzinli çağıran × tür** (yapılandırmada, bağlama başına; katalogdaki "İzinli çağıran" sütunu üst sınırdır):
  FinanceTreasury → `PAY, APD, ARI, CPS, CTP`; Mdm → `BNK, BAC, AGR`. Katalog dışı tür içeren yapılandırma geçersizdir.
- **Yönetici ucu (A-5 (a)):** `[Authorize(Policy = "PlatformActor")]` (emsal `Controllers/Admin/TenantsController.cs:15-16`),
  `[HasPermission("platform.record-code-group-prefixes.update")]` (PUT) ve `...read` (GET); PKS-001 Tier 1; aktör
  `platform_admin` (bütün izinlerden geçer). Kiracı kullanıcısı 403.

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği gereksiz.**

- İç uç Gateway'e **açılmaz**. Ölçüldü: `gateway/Diten.ApiGateway/ocelot.json`'da `/internal` geçen rota sayısı **0**;
  `/api/{everything}` gibi genel bir yakalayıcı yok. Tüketiciler Platform'a doğrudan servis adresiyle (5057) gider
  (servisten servise; AGENTS.md §3 kuralı tarayıcı içindir).
- A-5 (a) yönetici ucu mevcut `/api/admin/tenants/{everything}` → 5057 rotasıyla ulaşılır (`ocelot.json`, upstream
  `:1733`); yeni rota yok, integration-agent görevi yok.
- `gateway/Diten.ApiGateway/**/ocelot.json` korunan yoldur; bu paket yazmaz. `RecordCodeGatewayExposureTests` dosyayı okur
  ve `record-codes` geçen rota olmadığını ölçer.

## 16. Acceptance Criteria

**DL-1 — v1 servis (K-2).** Her maddenin sabotajı (koruma kaldırılınca kırmızıya dönmesi gereken test) maddenin içindedir.

- [ ] AC-1 (zorunlu) — Uç yalnız `POST /api/internal/record-codes/allocate`'tır; kimlik bilgisi başlıkları tam bir kez değilse, kimlik bilgisi doğrulanmazsa / iptalse / hedef kitle yanlışsa, kullanıcı token'ı yoksa / geçersizse / süresi dolmuşsa / `tenant_id` ya da `sub` yoksa / `actor_type ≠ tenant_user` ise cevap 401 `RECORD_CODE_UNAUTHENTICATED` ya da 403 `RECORD_CODE_FORBIDDEN`'dır ve işleyici hiç çağrılmaz (katı arabulucu sahtesiyle sayılır); `InternalRecordCodesController` `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` listesindedir.
  Sabotaj: yürütücüdeki her ret dalı tek tek kaldırılınca `TrustedRecordCodeRequestExecutorRefusalTests`'ten adı konmuş bir test kırmızıya döner (FU21 Q1–Q5 deseni).
- [ ] AC-2 (zorunlu) — Kiracı yalnız doğrulanmış kullanıcı token'ının `tenant_id`'sidir: gövdede `tenantId` üyesi 400 `RECORD_CODE_REQUEST_INVALID`, `X-Tenant-Id` başlığı 403 `RECORD_CODE_FORBIDDEN`; sorgu dizesindeki kiracı yok sayılır; oluşan sayaç ve defter satırının `TenantId`'si token kiracısıdır ve MDM'e giden `X-Tenant-Id` aynı değerdir.
  Sabotaj: kiracıyı başlıktan okuyan bir değişiklik yapılınca `InternalRecordCodesHttpTests` kiracı testi kırmızıya döner.
- [ ] AC-3 (zorunlu) — `LegalEntity` kapsamında, MDM `code = MYG` olan şirket için `PAY` ilk ayırması 200 ve `Response<T>` zarfında `data = { "code": "MYG-PAY-000001", "sequence": 1 }`, ikincisi `MYG-PAY-000002` / `2` döner; `prefixScope` gönderilmeyen v0 gövdesi aynı sonucu verir.
  Sabotaj: dolgu 6'dan 5'e indirilince ya da varsayılan kapsam kaldırılınca `RecordCodeFormatTests` / `InternalRecordCodesHttpTests` kırmızı.
- [ ] AC-4 (zorunlu) — Gerçek Mongo'da aynı `(kiracı, MYG, PAY)` için farklı anahtarlı 50 eşzamanlı ayırma 50 farklı kod ve 50 farklı sequence üretir; sayaç belgesi yokken başlayan ilk yarış hiçbir 500 üretmez (upsert yarışı kurtarılır).
  Sabotaj: `$inc` yerine oku-artır-yaz konunca çift kod, upsert yarışı kurtarması kaldırılınca ilk-yarış testi `RecordCodeAllocationMongoTests`'te kırmızı.
- [ ] AC-5 (zorunlu) — Aynı `idempotencyKey` + aynı gövde aynı kodu ve sequence'i döner, sayacı ilerletmez, MDM'i çağırmaz, ikinci defter ve denetim satırı yazmaz; aynı anahtar + farklı gövde (tür, kapsam ya da şirket farklı) 400 `IDEMPOTENCY_KEY_REUSED` döner ve sayacı ilerletmez; aynı anahtarla 10 eşzamanlı istek tek defter satırı ve tek kod üretir; aynı anahtar başka kiracıda ya da başka çağıranda bağımsızdır.
  Sabotaj: parmak izi karşılaştırması kaldırılınca farklı-gövde testi, `(TenantId, CallerService, IdempotencyKey)` unique index'i kaldırılınca eşzamanlı-anahtar testi kırmızı.
- [ ] AC-6 (zorunlu) — Sayaç `(kiracı, önek, tür)` başınadır ve sıfırlanmaz: `MYG-PAY`, `GPL-PAY`, `MYG-APD` ve B kiracısının `MYG-PAY`'i birbirinden bağımsız `000001`'den başlar; defter/denetim işlemi zorla düşürülen bir ayırmadan sonra bir sonraki kod bir üst numarayı alır (boşluk kalır, numara geri verilmez, hiçbir kod iki kez verilmez); depoda sayacı azaltan ya da sıfırlayan üye yoktur.
  Sabotaj: sayaç anahtarından önek çıkarılınca bağımsızlık testi, başarısızlıkta sayacı geri alan bir değişiklik konunca boşluk testi kırmızı.
- [ ] AC-7 (zorunlu) — `LastNumber = 999998` ile tohumlanmış sayaçta iki ayırma `MYG-PAY-999999` ve `MYG-PAY-1000000` üretir; sequence `long`'dur, kesilmez, sıfıra dönmez.
  Sabotaj: biçimleme 6 haneye kesilince ya da `int` taşmasına bırakılınca `RecordCodeFormatTests` ve Mongo testi kırmızı.
- [ ] AC-8 (zorunlu) — Kapsam isteği uyumsuzsa 422 `RECORD_CODE_SCOPE_INVALID` döner ve MDM çağrılmaz, sayaç yaratılmaz: `Group` + dolu `legalEntityId`, `LegalEntity` + boş `legalEntityId`, HTTP'den `Platform`; tanımsız `prefixScope` değeri 400 döner.
  Sabotaj: `Group` + dolu `legalEntityId` kontrolü kaldırılınca `AllocateRecordCodeHandlerTests` kırmızı.
- [ ] AC-9 (zorunlu) — Tür başına izinli kapsamı servis belirler: `BNK` yalnız `Group` (`LegalEntity` → 422); `BAC` hem `LegalEntity` (`legalEntityId` = sahip şirket, önek o şirketin kısa kodu) hem `Group` (grup öneki); `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `AGR` yalnız `LegalEntity` (`Group` → 422); `ORG`, `POS` yalnız `LegalEntity`.
  Sabotaj: `PAY`'e `Group` izni eklenince `RecordCodeTypeCatalogTests` tür×kapsam tablosu kırmızı.
- [ ] AC-10 (zorunlu) — Grup öneki kaydı `GMG` olan kiracıda `BNK` ilk ayırması `GMG-BNK-000001`, tedarikçi `BAC` ilk ayırması `GMG-BAC-000001`'dir; grup öneki kaydı olmayan kiracıda `Group` kapsamı 409 `GROUP_PREFIX_MISSING` döner ve sayaç yaratılmaz; servis grup önekini kod sabitinden, `DocumentCodingOptions.OrgPrefix`'ten ya da başka bir varsayılandan **okumaz**.
  Sabotaj: kayıt yokken `"GMG"`'ye düşen bir yedek eklenince 409 testi kırmızı.
- [ ] AC-11 (zorunlu) — MDM `code` değeri `^[A-Z]{2,5}$`'e birebir uymuyorsa (`GMG-CH`, `my`, `M`, `ABCDEF`, `MYĞ`, boş, baş/son boşluklu) 409 `LEGAL_ENTITY_SHORT_CODE_MISSING` döner, sayaç yaratılmaz; servis kodu büyütmez, kırpmaz, harf çevirmez; geçerli `MYG`, `GPL`, `GMT` kabul edilir.
  Sabotaj: okuyucuya `Trim().ToUpperInvariant()` eklenince `MdmLegalEntityShortCodeReaderTests` kırmızı.
- [ ] AC-12 (zorunlu) — A kiracısının token'ıyla B kiracısının şirketi istenince (MDM 404) cevap 404 `LEGAL_ENTITY_NOT_FOUND`'dır; olmayan ve aktif olmayan şirket için gövde aynıdır; sayaç, defter ya da denetim satırı oluşmaz; MDM'e giden istekte `X-Tenant-Id` = A ve `Authorization` = iletilen kullanıcı token'ıdır.
  Sabotaj: MDM isteğine şirketin kiracısını ya da başlıksız istek gönderen bir değişiklik konunca `InternalRecordCodesHttpTests` kiracılar arası testi kırmızı.
- [ ] AC-13 (zorunlu) — MDM kapalı, 5xx, 3 saniyeyi aşan, 400/401/403 dönen ya da çelişkili gövde (farklı `legalEntityId`, `referenceable = false`, `lifecycleState ≠ ACTIVE`, ayrıştırılamaz) veren durumda cevap 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`'dır; Mongo erişilemezse 503 `RECORD_CODE_UNAVAILABLE`; hiçbir durumda kod dönmez ve sayaç ilerlemez (MDM hatası sayaçtan önce).
  Sabotaj: 5xx'i 404'e çeviren (mevcut doğrulayıcının davranışı) bir değişiklik konunca `MdmLegalEntityShortCodeReaderTests` kırmızı.
- [ ] AC-14 (zorunlu) — Katalog dışı tür (`XYZ`, `CTR`, `ACC`, `CMP`, `GP`, `UID`, `IT`, `TTY`, `PUT`) 422 `RECORD_TYPE_UNKNOWN`; biçimsiz tür (`pay`, `PA`, `PAYX`) 400; katalogda olan ama çağıranın `AllowedRecordTypes`'ında olmayan tür (Mdm kimlik bilgisiyle `PAY`, FinanceTreasury ile `BNK`) 403 `RECORD_CODE_FORBIDDEN` döner.
  Sabotaj: çağıran × tür denetimi kaldırılınca 403 testi kırmızı.
- [ ] AC-15 (zorunlu) — Katalogdaki her tür ve izinli her kapsam için üretilen her kod yalnız ASCII `A–Z`, `0–9`, `-` içerir ve LegalEntity/Group'ta `^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$`, Platform'da `^[A-Z]{3}-[0-9]{6,}$` kalıbına uyar.
  Sabotaj: önek doğrulaması gevşetilip `Ğ` içeren bir kısa kod kabul edilince özellik testi kırmızı.
- [ ] AC-16 (zorunlu) — `RecordCodeTypeCatalog`, RCS-001 §3 tablosunun bütün OTOMATİK satırlarını (`ORG`, `POS`, `PAY`, `APD`, `ARI`, `CPS`, `CTP`, `BNK`, `BAC` ve `CTR` yerine `AGR`) birebir içerir; test kuralı `.antigravity/rules/record-coding-standard.md`'den okur; `CTR` → `AGR` adı konmuş tek istisnadır ve §3 güncellenince istisna testten çıkarılmazsa test kırmızıdır; KALSIN türü katalogda yoktur; `PLN` yalnız açık karar A-7 (a) seçilirse katalogdadır.
  Sabotaj: kataloğa §3'te olmayan bir tür eklenince ya da bir OTOMATİK tür çıkarılınca `RecordCodeTypeCatalogTests` kırmızı.
- [ ] AC-17 (zorunlu) — Her yeni ayırma, defter satırıyla **aynı Mongo işleminde** tek bir denetim kutusu satırı yazar; kutu işleyicisinden sonra `audit_events`'te tek satır vardır: aktör türü `Service`, `SourceService` = kimlik bilgisinin `ConsumerService`'i, `TenantId` = token kiracısı, kategori `PlatformConfiguration`, varlık `RecordCodeAllocation` + defter satırı kimliği, işlem `Create`, sonuç `Succeeded`, metadata'da `recordType`, `prefixScope`, `legalEntityId`, `prefix`, `code`, `sequence`, `idempotencyKey`, `onBehalfOfUserId`; tekrar (aynı anahtar) yeni satır yazmaz; kutu satırı yazılamazsa işlem iptal olur, cevap 503 ve defter satırı yoktur.
  Sabotaj: kutu yazımı işlemin dışına (commit sonrası) taşınınca `RecordCodeAuditDeliveryMongoTests` hata yolu testi kırmızı.
- [ ] AC-18 (zorunlu) — Merkezi denetim bildirimi BL-501 ile değil Platform içi işlem içi yazıcıyla yapılır: `AllocateRecordCodeHandler` `RecordCodeAuditIntent.EnqueueAsync`'i çağırır; CT'nin defter kararı (`kayit-kodu-islem-ici`, yol a, yazıcı) ile `AuditTrailStandardTests` bu komutu denetlenen sayar; Platform'a BL-501 iletici kopyası eklenmez; tüketicinin kendi Create kaydıyla eşleşme metadata'daki `idempotencyKey` + `code` üzerinden yapılır ve bu sözleşme taslağında yazılıdır.
  Sabotaj: işleyiciden yazıcı çağrısı kaldırılınca mimari test komutu "yeni ve denetimsiz" sayar ve kırmızıya döner.
- [ ] AC-19 (zorunlu) — Kimliği doğrulanmış ve kiracısı bilinen her ret (400 `IDEMPOTENCY_KEY_REUSED`, 403 `RECORD_CODE_FORBIDDEN`, 404, 409, 422) `RecordCodeAuditIntent.AppendRefusalAsync` ile en iyi çaba bir denetim satırı yazar (sonuç `Denied` ya da `Failed`, `reason_code` metadata'da, iş verisi yok); yazılamazsa uyarı loglanır ve cevap değişmez; 401 ve kiracı çözülmeden verilen 403 yalnız loglanır (kiracı adlandırılamaz). Açık karar A-14 bu maddeyi değiştirebilir.
  Sabotaj: 404 dalından ret yazımı kaldırılınca `RecordCodeAuditDeliveryMongoTests` ret testi kırmızı.
- [ ] AC-20 (zorunlu) — Defter ve grup öneki depolarının arayüzünde güncelleme, değiştirme ya da silme üyesi yoktur; sayaç deposunda yalnız `NextAsync` vardır; v1'de bir kodu değiştiren, iptal eden ya da yeniden veren uç yoktur; üç koleksiyonda silme yolu yoktur.
  Sabotaj: defter arayüzüne `DeleteAsync` eklenince yansıma testi kırmızı.
- [ ] AC-21 (zorunlu) — `gateway/Diten.ApiGateway/ocelot.json`'da `record-codes` ya da `/internal` içeren upstream/downstream rota yoktur; `frontend/Diten.Web` altında `record-codes/allocate` çağrısı yoktur.
  Sabotaj: test kopyasına `/api/internal/record-codes/{everything}` rotası eklenince `RecordCodeGatewayExposureTests` kırmızı.
- [ ] AC-22 (zorunlu) — Grup önekinin tek kaynağı açık karar A-5'in seçtiği kaynaktır; A-5 (a) seçilirse: yalnız Platform aktörü `PUT /api/admin/tenants/{tenantId}/record-code-group-prefix` ile `^[A-Z]{2,5}$` bir değeri **bir kez** yazar, ikinci yazım (aynı ya da farklı değer) 409 `GROUP_PREFIX_ALREADY_SET`, kiracı kullanıcısı 403, geçersiz değer 400; yazım yol a ile denetlenir (aktör `PlatformAdministrator`, önceki yok / sonraki değer).
  Sabotaj: ikinci yazımı üzerine yazan bir değişiklik konunca `TenantRecordCodeGroupPrefixHttpTests` kırmızı.
- [ ] AC-23 (zorunlu) — İstek ve cevap biçimi `docs/analysis/contracts/drafts/record-code-allocate-v1.0-proposal.md`'deki örneklerle birebir test edilir: örnek gövdeler kabul, bilinmeyen üye 400, her hata durumunda `statusCode` ve `reason_code` tablodakiyle aynı, cevap `data`'sında yalnız `code` ve `sequence` vardır.
  Sabotaj: cevapta alan adı değiştirilince (`code` → `recordCode`) sözleşme testi kırmızı.
- [ ] AC-24 (zorunlu) — Üç koleksiyon `PlatformSchemaManifest`'te yeni `SchemaProfile.RecordCodes` profiliyle kayıtlıdır ve Bölüm 4'teki adlı index'ler (kiracı önce, unique olanlar unique) uygulanır; `PlatformSchemaManifestTests` yeşildir; mevcut profil değerleri değişmez.
  Sabotaj: `ux_record_code_allocations_tenant_code` unique bayrağı kaldırılınca şema testi ve eşzamanlı ayırmanın kod tekilliği testi kırmızı.
- [ ] AC-25 (zorunlu) — Süreç içi komutla (Platform bağlamı) `Platform` kapsamı yalnız izinli kapsamı `Platform` olan tür için çalışır, `TenantId`'si boş sayaç ve defter satırı yazar ve `PLN-000001` biçiminde (öneksiz) kod verir; aynı tür HTTP'den 422 `RECORD_CODE_SCOPE_INVALID` alır. A-7 (b) seçilirse süreç içi test yalnız test kataloğuyla koşar.
  Sabotaj: Platform satırına bir kiracı kimliği yazan değişiklik konunca Mongo testi kırmızı.
- [ ] AC-26 (zorunlu) — MDM 3 saniyede cevap vermezse cevap en geç bütçe içinde 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`'dır; isteğin tamamı 5 saniyeyi aşmaz; günlüklerde kimlik bilgisi sırrı, `Authorization` başlığı ve JWT yoktur.
  Sabotaj: MDM bütçesi kaldırılınca geciktirilmiş sahte MDM'le zaman testi kırmızı.
- [ ] AC-27 (zorunlu) — `dotnet build` Diten.Platform PASS; Platform'un **tam** test takımı (filtre yok) ve `tests/architecture/TenantArchitecture.ArchitectureTests` yeşildir; atlanan test yoktur; ana daldaki bilinen kırmızılar aynı gün main'de ölçülüp ayrıca yazılır.
  Sabotaj: herhangi bir yeni testin `Skip` ile işaretlenmesi kabul kaydında kırmızıdır.

**DL-2…DL-10.** Kabul ölçütleri her dilimin kendi paket revizyonunda bu bölüme yeni numarayla eklenir (numaralar
silinmez, yeniden numaralanmaz).

## 17. Test Expectations

| Test sınıfı | Tür | Kapsadığı kabul ölçütleri |
|---|---|---|
| `RecordCodeFormatTests` | birim | AC-3, AC-7, AC-15 |
| `RecordCodeTypeCatalogTests` | birim (RCS-001 §3 dosyasını `RepoPaths` ile okur) | AC-9, AC-14, AC-16 |
| `AllocateRecordCodeHandlerTests` | birim (sahte depo + sahte okuyucu) | AC-5 (parmak izi), AC-8, AC-10, AC-13 (sıra: MDM hatası sayaçtan önce), AC-19 |
| `RecordCodeAllocationMongoTests` | gerçek Mongo replica set | AC-4, AC-5 (eşzamanlı), AC-6, AC-7, AC-20, AC-24, AC-25 |
| `RecordCodeAuditDeliveryMongoTests` | gerçek Mongo + üretimdeki kutu işleyicisi | AC-17, AC-18 (teslim), AC-19 |
| `InternalRecordCodesHttpTests` | gerçek HTTP ana makinesi (`SignedTenantTokenHttpHost` deseni) + sahte MDM | AC-1, AC-2, AC-3, AC-12, AC-13, AC-23, AC-26 |
| `TrustedRecordCodeRequestExecutorRefusalTests` | birim, her ret dalı | AC-1 |
| `MdmLegalEntityShortCodeReaderTests` | birim, sahte HTTP | AC-11, AC-12, AC-13, AC-26 |
| `RecordCodeGatewayExposureTests` | dosya ölçümü | AC-21 |
| `TenantRecordCodeGroupPrefixHttpTests` (A-5 (a)) | gerçek HTTP | AC-22 |
| `LoginOnlyEndpointGuardTests` (değişen liste) | mimari | AC-1 |
| `AuditTrailStandardTests` (CT'nin sabit sayısı) | mimari | AC-18 |
| Tam takım + mimari testler | CI | AC-27 |

**Sabotaj listesi (kabul kaydına koşu kanıtıyla yazılır; her satır tek tek uygulanıp geri alınır):**

| # | Sabotaj | Kırmızıya dönmesi gereken |
|---|---|---|
| S-1 | Yürütücüde kimlik bilgisi reddi kaldırılır | `TrustedRecordCodeRequestExecutorRefusalTests` (adlı test) |
| S-2 | Kullanıcı token'ı reddi kaldırılır | aynı |
| S-3 | `X-Tenant-Id` reddi kaldırılır ve kiracı başlıktan okunur | `InternalRecordCodesHttpTests` kiracı testi |
| S-4 | `$inc` yerine oku-artır-yaz | `RecordCodeAllocationMongoTests` eşzamanlı test |
| S-5 | Upsert yarışı kurtarması kaldırılır | ilk-yarış testi |
| S-6 | Parmak izi karşılaştırması kaldırılır | farklı-gövde testi |
| S-7 | İdempotency unique index'i kaldırılır | eşzamanlı-anahtar testi |
| S-8 | Sayaç anahtarından önek çıkarılır | bağımsızlık testi |
| S-9 | Biçimleme 6 haneye kesilir | 999999 → 1000000 testi |
| S-10 | `PAY`'e `Group` izni | katalog tür×kapsam testi |
| S-11 | Grup öneki yokken `"GMG"`'ye düşme | 409 testi |
| S-12 | Kısa koda `Trim().ToUpperInvariant()` | okuyucu testi |
| S-13 | MDM 5xx → 404 | okuyucu 503 testi |
| S-14 | Çağıran × tür denetimi kaldırılır | 403 testi |
| S-15 | Kataloğa §3 dışı tür eklenir | katalog sabitleme testi |
| S-16 | Denetim kutusu yazımı commit sonrasına taşınır | denetim hata yolu testi |
| S-17 | İşleyiciden `RecordCodeAuditIntent.EnqueueAsync` çağrısı kaldırılır | `AuditTrailStandardTests` |
| S-18 | 404 dalından ret yazımı kaldırılır | ret denetimi testi |
| S-19 | Defter arayüzüne `DeleteAsync` | yansıma testi |
| S-20 | Test kopyasında Gateway rotası | `RecordCodeGatewayExposureTests` |
| S-21 | Grup öneki ikinci yazımda üzerine yazılır | `TenantRecordCodeGroupPrefixHttpTests` |
| S-22 | Cevap alanı yeniden adlandırılır | sözleşme testi |
| S-23 | Allocations `(TenantId, Code)` unique kaldırılır | şema testi |
| S-24 | Platform satırına kiracı yazılır | Platform kapsamı testi |
| S-25 | MDM bütçesi kaldırılır | zaman testi |

Diğer beklentiler: kiracı izolasyonu (AC-2, AC-12) gerçek HTTP ile; soft delete yok (AC-20 yansıma testi); tarayıcı
smoke yok (ekran yok); RESX parite gerekmiyor (metin yok); DataTable doğrulayıcısı uygulanmaz; Mongo testleri Platform'un
mevcut test sabitleriyle kendi replica set'ini kullanır (yerel 27017'ye güvenilmez).

## 18. Ready-for-dev Checklist

- [x] Golden Reference kararı: `none` (ekran yok); arka uç klasör/ad düzeni Golden Reference'a birebir (Bölüm 10).
- [x] Künye zorunlu alanları dolu (`service`, `shell`, `golden_reference`, `entity_base`); `approval_status: pending`; `code_start` yok.
- [x] Layout & Shell: `shell: none`, yazılacak `Layout` yok (Bölüm 9).
- [x] Validation Rules her alan için (Bölüm 12); Failure Path en az 4 senaryo (Bölüm 13: tekrar, eksik, yetkisiz, eşzamanlı ve fazlası).
- [x] Authorization: iç uç gerekçesi + yönetici ucu izinleri (Bölüm 14); Gateway kararı: gereksiz (Bölüm 15).
- [x] Lookup & Reference Data kararı (Bölüm 7 sonu).
- [x] Audited Events her yazma komutunu tam bir kez içerir (Bölüm 21).
- [x] Kimlik ön kontrolü geçti: `verify_module_id.py --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → `OK`, çıkış 0 (Bölüm 0).
- [x] `pack_approval.slices()` bu paket için DL listesi döndürür (None değil) — `PYTHONUTF8=1 python3 -P -c "import sys; sys.path.insert(0,'scripts'); import pack_approval as p; t=open('execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md',encoding='utf-8').read(); print(p.slices(t)); print(p.kunye(t)['status'])"` → `['DL-1', 'DL-2', 'DL-3', 'DL-4', 'DL-5', 'DL-6', 'DL-7', 'DL-8', 'DL-9', 'DL-10']` ve `draft` (2026-10-30, bu commit).
- [ ] Açık kararlar A-1 (K-6, 2026-11-02), A-5, A-6, A-7, A-9, A-11, A-13, A-14 (2026-11-03) kapandı ve pakete işlendi.
- [ ] Sözleşme 2026-11-03'te CT adımıyla donduruldu (taslak → dondurulmuş sürüm; yol CT belirler).
- [ ] A-10: CT defter kararı (`kayit-kodu-islem-ici` izi) yazıldı — DL-1 **main'e girmeden önce** (kodu başlatmadan önce değil).
- [ ] A-12: kayıt satırı (Bölüm 0 önerisi) ve domain-config In-Scope satırı CT tarafından yazıldı.
- [ ] CT-APR-02 bağımsız incelemesi (yazandan ayrı oturum) bu sürüm için kaydedildi; açık ENGEL yok.
- [ ] CT-APR-01 onay satırı (yüksek risk: yeni iç uç, kiracı, şema, kimlik bilgisi; `high: evet` onaylayıcı, paketi yazmamış biri) main'de; künye `ready-for-dev`.

## 19. Implementation Notes

**Örnek alınan çalışan kod (yalnız okundu, satırlar ölçüldü @ `6d49f5823`).**

- Doküman kimlik üreticisi (K-1: genişletilmez, desen örnek): `services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementIdentifiers/Services/DocumentIdentifierAllocationService.cs`
  — sınıf `:19`; sayaçtan değer çekip defterde var mı diye yoklayan döngü `:317-335` (en çok 100 deneme `:28`); biçim
  `PadLeft` `:337-343`; elle rezervasyon (ManualImport, LegacyCode) `:102-153`; iptal = durum değişikliği, değer asla
  yeniden kullanılmaz `:157-213`. Sayaç deposu `…/Infrastructure/Persistence/Repositories/DocumentManagementIdentifierRepositories.cs:52-90`
  (`$inc` + `upsert` + `ReturnDocument.After` `:68-86`; **E11000 kurtarması yok**), silinmişleri de gören tekillik
  yoklaması `:22-30`; sayaç varlığı `…/Domain/Entities/DocumentManagement/DocumentIdentifierSequenceCounter.cs:12-21`;
  sayaç unique index'i `…/Persistence/Schema/PlatformSchemaManifest.DocumentManagementFollowUps.cs:282-295`
  (`ux_dm_identifier_sequence_counters_key` `:294`); koleksiyon adı `PlatformCollections.cs:66`. `DocumentCodingOptions`
  `…/DocumentManagementIdentifiersModels.cs:41-50` (bölüm `DocumentManagement:Coding` `:43`, `OrgPrefix = "GMG"` kod
  varsayılanı `:45`; bağlama `Infrastructure/DependencyInjection.cs:171-172`; `appsettings*.json`'da bu bölüm **yok** —
  çalışan değer kod varsayılanıdır). Tür kodu çözücüsü `:65`: `QualityTechnicalAgreementSdea → "AGR"` (A-3c).
- MDM `CodeReservation` deseni (RCS-001 §5.8): `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/CodeReservationRepository.cs`
  — önce idempotency araması, sonra sayaç, sonra ekleme, ekleme E11000'de kazanan okunur `:37-110`; sayaç `:549-600`
  (upsert yarışı kurtarması `:579`, 3 deneme `:16`, koleksiyon `mdm_canonical_code_counters` `:18`); varlık
  `…/Domain/Entities/CodeReservation.cs:5-27` (rezerve → tüket → yak durumları — v1 yalnız "rezerve"yi alır).
- İç uç kimlik desenleri: Bölüm 8.2 tablosu. `LoginOnlyEndpointGuardTests.cs:77-128` listeye giriş şartı.
- Denetim: `AuditActorTypeResolver.FromActorTypeClaim` `"service"`'i tanımıyor (`…/Contracts/Audit/AuditActorTypeResolver.cs:28-37`)
  ve `AuditService` `Unknown` aktörü reddediyor (`:17-20` yorumu) — bu yüzden yol a'nın `AuditBehavior`'ı aktörü
  `Service` yazamaz; e-imza emsali (`ESignatureAuditIntent`, `…/Features/ESignature/Services/ESignatureServices.cs:103-155`,
  kutu `:139`, eklenmezse istisna `:150-153`) aktörü açıkça verir. `AuditActorType.Service = 5` (`…/Domain/Enums/AuditEnums.cs:71-79`),
  kullanım emsali `…/ESignature/Handlers/CatalogHandlers.cs:314`. Kategori: yeni enum değeri **eklenmez** (Finance = 18
  eklemesi BuildingBlocks `CentralAuditPlatformValues` ile eşleme ve sapma testi istedi — Y-8); paylaşılan servis emsali
  MOD-0027 gibi `PlatformConfiguration` (ör. `…/Notifications/Commands/CancelNotificationDispatchCommand.cs:11`).
- `Response<T>` zarfı `…/Application/Common/Response.cs:7-16` (`reason_code`, `correlation_id` JSON adları).
- MOD-0178 tüketici beklentisi: §8.7 (`MOD-0178-putaway-picking-packing.md:815-822`) ve §22 (`:1956-1974`) — şirket kodu
  `^[A-Z]{2,5}$`, uymazsa WMS kendi 422 `WMS_COMPANY_CODE_INVALID`'ini döner; bu servisin 409
  `LEGAL_ENTITY_SHORT_CODE_MISSING`'ini WMS kendi koduna eşler (FU-0178-RCS-01'in işi). Geçici sayaçtan bu servise
  geçişte merkezi sayaç WMS'in en büyük numarasının üstünden başlamalıdır, yoksa çift kod doğar (§5.11) → DL-6 önkoşulu.
- MOD-0220: `LegalEntity.Code` `…/MdmService.Domain/Entities/LegalEntity.cs:11` (tek kod alanı, ayrı kısa kod yok);
  lookup-validation sözleşme taslağı v1.1 — bu servis yalnız 1.0.0 alanlarını okur.
- **Önek çakışması (bilgi):** bir şirketin kısa kodu grup önekiyle aynıysa (ör. ikisi de `GMG`), `(kiracı, GMG, BAC)`
  sayacı şirket ve grup `BAC`'ı için ortak olur: kodlar çakışmaz (tek sayaç), ama önek tek başına şirket mi grup mu
  söylemez — sahip bilgisi kaydın `LegalEntityId`'sindedir (§5.2). v1 bunu engellemez; DL-5 (kısa kod kilidi) ile değerlendirilir.
- **AUD-001 §3 metni ile K-8:** kural tablosunun 3. satırı "Servis çağrısıysa servis kimliği: `ActorType = System` +
  `SourceService`" der (`.antigravity/rules/audit-trail-standard.md:103`); K-8 `Service` der ve enum'da `Service` var.
  Paket (yetki sırası) K-8'i uygular; kural metninin hizalanması CT adımıdır (A-15).

## 20. Follow-up Items

| # | İş | Sahip | Dilim |
|---|---|---|---|
| FU-RCS-01 | ORG / POS bağlanması (MOD-0288 Organizasyon Birimi, Pozisyon; süreç içi port) | Beste | DL-2 |
| FU-RCS-02 | İKİSİ DE motoru: öneri + gerekçeli değişiklik + AUD-001 eski/yeni kod + gerekçe; §5.9 elle kod deseni | Beste | DL-3 |
| FU-RCS-03 | Görev Türü (TTY), PPM (PFL/PRG/PRJ/INI/INV — PPM sahibi Codex), Abonelik Planı (PLN) bağlanması | Beste + PPM sahibi | DL-4 |
| FU-RCS-04 | Kodu kayda bağlama / tüketme adımı (rezerve → yaz → tüket), kullanılmayan kod raporu | Beste | DL-3 / DL-4 ile değerlendirilir |
| FU-RCS-05 | Tüzel Kişilik kısa kodu önerisi + §5.4 kilidi (MDM; "bu kısa kod önekte kullanıldı mı" sorgusu) | MDM sahibi + Beste (XMC-001) | DL-5 |
| FU-RCS-06 | İçe aktarma / sayaç yüksek su işareti (§5.11); WMS geçici sayacından devam (FU-0178-RCS-01 önkoşulu) | Beste + MOD-0178 sahibi | DL-6 |
| FU-RCS-07 | RCS-001 §3 `CTR` → `AGR` + tür başına izinli kapsam sütunu (`.antigravity`) | CT (sahip onaylı) | DL-7 |
| FU-RCS-08 | WMS türleri `PUT` / `PCK` / `OUT` / `PKG` kataloğa + Inventory kimlik bilgisi (FU-0178-RCS-02 sonrası) | Beste + MOD-0178 sahibi | DL-8 |
| FU-RCS-09 | Arka plan çağıranları için servis kimliği token'ı (P3; Auth değişikliği Ali) | Beste + Ali | DL-9 |
| FU-RCS-10 | Platform yönetici ekranları (tür kataloğu, sayaç, defter, grup öneki) — 2 dil, `_LayoutPlatformAdmin` | Beste | DL-10 |
| FU-RCS-11 | Askıya alınmış kiracının ayırması: v1 kiracı durumunu denetlemez (iç uç kiracı ara katmanını atlar); çağıranın kendi ara katmanı denetliyor mu ölçülmedi | Beste | DL-2 öncesi ölçüm |
| FU-RCS-12 | Doküman sayacındaki E11000 yarışı (`DocumentManagementIdentifierRepositories.cs:58-90`) — bu paketin işi değil; MOD-0029 sahibine bildirilir (XMC-001) | MOD-0029 sahibi | — |
| FU-RCS-13 | Geliştirme ortamında kimlik bilgisi sağlama adımı (dev-runbook) — değer depoya yazılmaz | Beste | DL-1 kabulü |

## 21. Audited Events

Servisin denetim altyapısı bugün (`tests/architecture/audit-ledger/Diten.Platform.md:8-17`): `platform-merkezi` (yol a,
işaret), `platform-islem-ici` (yol a, yazıcı), `esign-islem-ici-denetim` (yol a, yazıcı) kabul edilmiş. Bu paketin yazıcısı
için iz **yok** → yeni iz `kayit-kodu-islem-ici` (yol a, tür yazıcı, belirteç `RecordCodeAuditIntent.EnqueueAsync/RecordCodeAuditIntent.AppendRefusalAsync`)
CT kararı ister (A-10; esign emsali CT kararı 2026-10-14). Karar yoksa DL-1 main'e giremez (kural §1: yeni denetimsiz
komut kırmızıdır).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `AllocateRecordCodeCommand` (başarı) | `platform.record-code.allocated` | `RecordCodeAllocation` | a — işlem içi yazıcı `RecordCodeAuditIntent.EnqueueAsync` → `ITransactionalAuditOutboxWriter` → `audit_outbox` → `audit_events` (iz `kayit-kodu-islem-ici`, CT kabulü bekliyor) | AfterState: `Code`, `Sequence`, `RecordType`, `PrefixScope`, `Prefix`, `LegalEntityId`; Metadata: `CallerService`, `OnBehalfOfUserId`, `IdempotencyKey`, `CorrelationId`; aktör `Service`, `SourceService` = çağıran | — |
| `AllocateRecordCodeCommand` (ret, kiracı bilinen) | `platform.record-code.allocation-refused` | `RecordCodeAllocation` (kimliksiz) | a — aynı iz, `RecordCodeAuditIntent.AppendRefusalAsync` (en iyi çaba, sessiz değil: düşerse uyarı logu) | Metadata: `reason_code`, `RecordType`, `PrefixScope`, `LegalEntityId`; sonuç `Denied` / `Failed` | — (A-14) |
| `SetRecordCodeGroupPrefixCommand` (yalnız A-5 (a)) | `platform.record-code.group-prefix.set` | `RecordCodeGroupPrefix` | a — `IAuditableCommand` + `IAuditMetadataProvider` (`AuditBehavior`), aktör `PlatformAdministrator`, `TargetTenantId` | Before: yok · After: `GroupPrefix` | — |

- **K2 sınıfı değil:** kimlik, rol/izin, kiracı durumu, GxP ya da KVKK özel veri değişmez (AUD-001 §4.3). Yine de başarılı
  ayırmanın kaydı işlem içidir (kayıt yoksa kod yok) — bilinçli olarak en iyi çabadan güçlü seçildi: denetimsiz bir kod
  tüketicide denetimli bir Create'e dönüşür ve izi kopar.
- **Kayda yazılmayan:** kimlik bilgisi sırrı, `Authorization` başlığı, JWT, MDM yanıt gövdesi; kişi verisi yok (yalnız
  kimlikler).
- **Okuma denetimi:** yok (okuma ucu yok; AUD-001 §7'nin üç sınıfına girmez).
- **BL-501:** kullanılmaz; bu komut Platform içindedir (AUD-001 §5 yol a, "başka seçenek aranmaz"). Tüketicinin kendi
  Create komutunun denetimi (BL-501 ya da kendi yolu) tüketicinin paketindedir; eşleşme `idempotencyKey` + `code` ile.

## 22. Record Codes

Bu paket kendi kullanıcıya görünen kayıt türünü **açmaz** (ayırma defteri satırının kullanıcıya görünen kodu yoktur; satır
başka türlerin kodunu taşır). Servisin verdiği türler ve kuralları Bölüm 4.4 tablosundadır (RCS-001 §6 kontrol listesinin
servis tarafı):

- Sınıf: katalogdaki her tür RCS-001 §3'teki sınıfıyla (OTOMATİK; `PLN` İKİSİ DE, A-7).
- Önek: §5.5 — 3 büyük harf, §3'te merkezi; `AGR` için §3 güncellemesi DL-7 (A-3). `AGR` harfleri doküman tür kodunda da
  geçiyor (`GMG-QMS-AGR-0001`, `DocumentManagementIdentifiersModels.cs:65`) — biçimler farklı (`{ORG}-{DOMAIN}-{TÜR}-{4 hane}`
  ile `{ŞİRKET}-{TÜR}-{6+ hane}`), değer çakışması yok; §5.5 anlamında "aynı önek iki türde" sayılıp sayılmayacağı A-3c.
- Ortak servis (§5.12): bu paket o servistir; tüketicinin kendi sayacı yasak kalır (§6).
- Değiştirilemezlik: OTOMATİK kod v1'de değiştirilemez; servis değiştirme ucu sunmaz; tüketici API'si de reddeder (onların AC'si).
- Benzersizlik: `(TenantId, Code)` unique (Platform satırında `TenantId` boş).
- Testler: eşzamanlı iki ayırma farklı kod (AC-4), arşivli kod yeniden kullanılmaz (AC-6, AC-20), 999999 → 7 hane (AC-7),
  ASCII (AC-15); "değiştirme gerekçesiz reddedilir" İKİSİ DE'dedir (DL-3).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | kabul ölçütleri ve kapsam notu |
|---|---|---|---|
| DL-1 | v1 servis — allocate iç ucu, atomik sayaç, üç önek kapsamı, OTOMATİK tür önek kaydı, şirket kısa kodu okuması, idempotency, denetim, kiracı izolasyonu, grup öneki kaydı (A-5) | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Acceptance Criteria, Test Expectations, Audited Events, Record Codes | AC-1…AC-27; hedef main 2026-11-06; sözleşme 2026-11-03'te donar; main'e girmeden önce A-10 CT defter kararı |
| DL-2 | OTOMATİK mevcut modüllerin bağlanması — Organizasyon Birimi (ORG) ve Pozisyon (POS), süreç içi port | Ownership and Boundaries, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; A-8 (izinli kapsam) önkoşul; kod izni yok |
| DL-3 | İKİSİ DE motoru — öneri, gerekçeli değişiklik, AUD-001 eski/yeni kod + gerekçe, RCS-001 §5.9 elle kod deseni | Ownership and Boundaries, Audited Events, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; kod izni yok |
| DL-4 | İKİSİ DE mevcut modüllerin bağlanması — Görev Türü (TTY), PPM (PFL/PRG/PRJ/INI/INV), Abonelik Planı (PLN, Platform kapsamı) | Ownership and Boundaries, Record Codes, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; PPM kısmı PPM sahibiyle; kod izni yok |
| DL-5 | Tüzel Kişilik kısa kodu önerisi ve RCS-001 §5.4 kullanım kilidi (MDM tarafı, XMC-001) | Dependencies, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; MDM sahibi onayı; kod izni yok |
| DL-6 | İçe aktarma ve sayaç yüksek su işareti (RCS-001 §5.11), WMS geçici sayacından devam | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; FU-0178-RCS-01'in önkoşulu; kod izni yok |
| DL-7 | RCS-001 §3 tür başına izinli kapsam sütunu ve CTR → AGR satırı (CT, .antigravity) | Record Codes, Açık kararlar | kabul ölçütleri bu dilimin paket revizyonunda yazılır; katalog testindeki adlı istisna kalkar; sahip onaylı CT adımı |
| DL-8 | WMS türleri (PUT, PCK, OUT, PKG) kataloğa ve Inventory kimlik bilgisi | Entity Fields, Authorization Convention, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; FU-0178-RCS-02 (§3 satırları) önkoşul; kod izni yok |
| DL-9 | Arka plan çağıranları için servis kimliği token'ı (P3) | Runtime Constraints, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; Auth değişikliği (Ali) ister; kod izni yok |
| DL-10 | Platform yönetici ekranları (tür kataloğu, sayaç, defter, grup öneki) | Layout & Shell Contract, Frontend File Contract, Follow-up Items | kabul ölçütleri bu dilimin paket revizyonunda yazılır; 2 dil, `_LayoutPlatformAdmin`, SEC-002 kapsam cümlesi; kod izni yok |

Dilim sırası: DL-1 tek başına teslim edilebilir. DL-2 ve DL-4 DL-1'i, DL-4 ayrıca DL-3'ü bekler. DL-6, MOD-0178'in
FU-0178-RCS-01 geçişinden önce main'de olmalıdır. DL-7 DL-1'i bekletmez. DL-8, DL-6 ve FU-0178-RCS-02'yi bekler.
Tüketici kapıları: Treasury paketlerinin DL-1 kabulü bu paketin DL-1 kodunun main'de olmasını bekler (D59/1); MOD-0289 ve
MOD-0217 için ayrıca MDM bağdaştırıcısı ve `prefixScope` (A-11).

## 24. Açık kararlar

| # | Konu | Seçenekler | CT önerisi | Sahip | Son tarih |
|---|---|---|---|---|---|
| A-1 | Şirket öneki biçimi (K-6) | RCS-001 §1 şirket öneki (bugünkü) · tek grup öneki · öneksiz | Karar gelmezse sözleşme §1'e göre donar; "öneksiz" seçilirse `code` kalıbı ve tüketici doğrulamaları değişir (sözleşme §7) | K1 (`gmg-10`) | 2026-11-02 |
| A-2 | MOD-0144 / MOD-0217 kapsam çakışması (sözleşme iki modülde mi) | — | AÇIK KARAR; DL-1'in `AGR` kullanımını bekletmez | Cihan + Treasury | TBD |
| A-3 | RCS-001 §3 `CTR` → `AGR` satırı ve tür başına izinli kapsam sütunu (`.antigravity`) | — | DL-7 CT adımı; o güne kadar katalog testinde adlı tek istisna | CT (sahip onaylı) | 2026-11-03 (öneri) |
| A-3c | `AGR` harflerinin doküman tür kodunda (`GMG-QMS-AGR-0001`) da geçmesi §5.5 çakışması mı | çakışma değil (biçim farklı) · çakışma (başka önek) | çakışma değil, gerekçesi Bölüm 22; A-3 ile birlikte karara bağlanmalı | CT + Beste | A-3 ile |
| A-4 | Sınıflandırılmamış mevcut üreticilerin envanteri (Inventory, e-İmza, Procurement, SupplyChain, DMS) | — | v1'de değişmez; envanter gelince sınıf kararı (§5.6) | Treasury (gönderecek) | TBD |
| A-5 | Grup önekinin kaynağı (v1) | (a) kiracı başına kayıt + Platform yöneticisi için UI'sız, bir kez yazılan uç · (b) `DocumentManagement:Coding:OrgPrefix` yapılandırması (kiracı başına değil; kod varsayılanı okunmamalı) · (c) kiracı başına yapılandırma haritası | (a) — çok kiracılı doğru, varsayılan yok, mevcut `/api/admin/tenants/{everything}` rotası yeter | Beste | 2026-11-03 |
| A-6 | Platform kapsamı HTTP'den kapalı, sayaç kiracısız (`TenantId` boş) | kapalı (süreç içi) · açık (kiracı kimlik bilgisiyle) | kapalı; v1'de HTTP tüketicisi yok | Beste | 2026-11-03 |
| A-7 | `PLN` v1 kataloğunda mı (İKİSİ DE türü, K-2 yalnız OTOMATİK der) | (a) v1'de, yalnız ayırma · (b) DL-4'e | (a) — Platform kapsamı gerçek bir türle test edilir; öneri/gerekçe akışı yine DL-3 | Beste | 2026-11-03 |
| A-8 | `ORG` / `POS` izinli kapsamı | LegalEntity · Group · ikisi | LegalEntity (`MG-ORG-000042` örneği; `OrganizationUnit.LegalEntityId` zorunlu) | Beste | DL-2 başlamadan |
| A-9 | Tür önek kaydı kod kataloğu mu koleksiyon mu | kod kataloğu + §3 sabitleme testi · `GlobalEntity` koleksiyonu + tohum | kod kataloğu (Bölüm 4.4 gerekçesi) | Beste | 2026-11-03 |
| A-10 | Yeni denetim izi `kayit-kodu-islem-ici` (yol a, yazıcı) — defter satırı, CT-DECISIONS, `AuditTrailStandardTests` sabiti | — | kabul (esign emsali 2026-10-14) | CT | DL-1 main'e girmeden önce |
| A-11 | Sözleşme dondurma ayrıntıları: `Response<T>` zarfı (v0 "200 {code, sequence}" yazıyordu), `idempotencyKey` biçimi (1–200 yazdırılabilir ASCII), 503 neden kodlarının adları, Treasury / MDM portuna `prefixScope` eklenmesi | — | taslaktaki gibi | Beste + Treasury CT + MDM sahibi | 2026-11-03 |
| A-12 | Kayıt satırı (Bölüm 0) ve domain-config In-Scope satırı; ebeveyn çözümleme riski | — | satır Bölüm 0'daki gibi, ayrım notuyla | CT | paket onayından önce |
| A-13 | İç uç kimlik deseni (K-8) | P2 (kimlik bilgisi + kullanıcı token'ı) · P3 (servis kimliği token'ı) · P1 (ortak sır) | P2; arka plan çağıranı yok kabulüyle; P3 DL-9 | Beste | 2026-11-03 |
| A-14 | Ret denetimi (AC-19) | kiracı bilinen her ret en iyi çaba kayıt · yalnız log (MOD-0178 E-2'deki "yalnız başarılı" kararı gibi) | kayıt (AUD-001 §3 "reddedilen komut da kaydedilir") | Beste + CT | 2026-11-03 |
| A-15 | AUD-001 §3 satır 3 (`ActorType = System` + `SourceService`) ile K-8 (`Service`) farkı | kural metnini `Service`'e hizala · K-8'i `System`'e çevir | kural metnini hizala (CT, `.antigravity`) | CT | DL-1 main'e girmeden önce |
