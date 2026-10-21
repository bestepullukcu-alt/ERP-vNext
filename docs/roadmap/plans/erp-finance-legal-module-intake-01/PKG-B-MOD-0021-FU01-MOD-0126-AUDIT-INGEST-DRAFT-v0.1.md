# PAKET B — MOD-0021-FU01 · MOD-0126 Trusted Audit Ingest

> ## `DRAFT / OWNER DECISION REQUIRED / NOT AUTHORIZED FOR IMPLEMENTATION`
>
> Bu belge bir **karar/kapsam paketidir**. Pack status değişikliği, DCP onayı, CT
> acceptance, implementation, test, config/client/grant/secret değişikliği, deployment
> veya production erişimi **vermez ve ima etmez**. Hiçbir approval veya görev ataması
> içermez.

| | |
|---|---|
| **Depo** | `ERP-vNext-recovery` (yalnız) |
| **HEAD** | `0b255a4ce8f3a4d21148a6c40064910f0d131376` |
| **HEAD doğrulaması** | `git rev-parse HEAD` ile teyit; `git diff --stat` ile `Contracts/Audit/`, `Features/Audit/`, `API/Security/`, `API/Controllers/Internal/` yollarında **fark yok** → önceki ölçümler güncel |
| **Tarih** | 2026-10-18 |
| **Evidence level** | **E2 — kod ve pack metni okuması.** Hiçbir test koşulmadı, hiçbir HTTP çağrısı yapılmadı, hiçbir runtime gözlemi yok. |

**Kapsam cümlesi.** Trusted audit intent ingest yüzeyinin bugün `Diten.MDM` /
`mod-0290.audit-intent.v1` profiline kilitli olduğunu, initiator'ın gövde beyanı olarak
alındığını ve receipt'in merkezî outbox kabulünü bildirdiğini kanıtla sabitler; MOD-0126
için gerekebilecek alanları **öneri** olarak ayırır ve audit/security sahiplerinin
önüne karar sırasını koyar. Kapsamı kendiliğinden genişletmez.

---

## 1 · Ölçülen gerçek

### 1.1 Yüzey ve admission

| | |
|---|---|
| method/path | `POST api/internal/v1/audit/source-intents/accept` |
| controller | `InternalTrustedSourceAuditIntentController.cs:12` (`[Route]`), `:22` (`[HttpPost("accept")]`), `[AllowAnonymous]` — kimlik executor içinde |
| kabul edilen `SourceService` | **`"Diten.MDM"`** — `TrustedSourceAuditIntentAcceptanceService.cs:11` (`private const`) |
| kabul edilen `ContractVersion` | **`"mod-0290.audit-intent.v1"`** — aynı dosya `:12` |
| eşleşmezse | `ContractUnsupported` → **409** `AUDIT_SOURCE_INTENT_CONTRACT_UNSUPPORTED` (`:40-45`) |
| audience | **`TRUSTED_AUDIT_SOURCE_INGEST`** — workflow audience'ından farklı sabit |
| gövde sınırı | 32 KiB (`TrustedSourceAuditIntentRequestExecutor.cs:12`) |

Pack tarafı aynı sınırı yazıyor (`MOD-0021-FU01`):
```
:25   "The first source is Diten.MDM. The first consumer is MOD-0290 Product / Item / SKU Master…"
:35   "One Platform-internal source-intent acceptance endpoint for Diten.MDM."
:102  | SourceService | string | Yes | Exact ordinal `Diten.MDM`; max 64. |
:125  Merkezî anahtar: "Diten.MDM:{TenantId:N}:{IntentId:N}:mod-0290.audit-intent.v1"
:150  "All rows map to AuditCategory.MasterData, AuditOutcome.Succeeded,
       SourceService=Diten.MDM, SourceModule=product-item-sku-master.
       Any other aggregate/operation pair is rejected…"
```

> **MOD-0126 admission/profile: YOK.** Onaylı plan da **yok**. `:150` ayrıca iki ek
> sınır getirir: `SourceModule` sabit `product-item-sku-master` ve her satır
> `AuditOutcome.Succeeded` eşlenir — yani bu yüzey bugün **başarısız bir outcome
> ifade edemez**.

### 1.2 Envelope DTO — 18 alan (`TrustedSourceAuditIntentEnvelope.cs`)

`SourceService`, `ContractVersion`, `IntentId`, `TenantId`, `AggregateType`,
`AggregateId`, `PreVersion`, `PostVersion`, `Operation`, `ActorId`, `CorrelationId`,
`CausationId`, `CommandId`, `Sequence`, `TimestampUtc`, `EvidenceHash`,
`SnapshotReference?`, `IdempotencyKey`.

### 1.3 Validation (`TrustedSourceAuditIntentAcceptanceService.IsValid`, `:119-140`)

```
IntentId/TenantId/AggregateId/CorrelationId != Guid.Empty
PreVersion >= -1 · PostVersion >= 0 · PostVersion == PreVersion + 1
Sequence >= 0
TimestampUtc.Offset == TimeSpan.Zero  ve  TimestampUtc <= now + 5 dakika
AggregateType / Operation / ActorId / CausationId / CommandId : boş değil, trim'li, <= 160
IdempotencyKey : boş değil, trim'li, <= 240
EvidenceHash : ^[0-9A-F]{64}$   (büyük harf SHA-256)
SnapshotReference : null veya ^[A-Za-z0-9._:/-]{1,256}$
```
Sapma → `Invalid` → **400** `AUDIT_SOURCE_INTENT_INVALID`.

### 1.4 Receipt (`TrustedSourceAuditIntentAcceptanceReceipt.cs`) — 5 alan

`CentralAcknowledgement`, `CentralIdempotencyKey`, `ContractVersion`, `AcceptedAt`,
`Duplicate`.

### Evidence paths + SHA-256

```
59eb2208e0c538bf18f468a1e94b45f98efcd3a064f0bc22309ec874fab83b33  services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedSourceAuditIntentController.cs
0abe5574a2c18afbd1f19777b1d4266f956a8aa0e3b2e162a82d56e7e66a5c47  services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSourceAuditIntentServiceIdentity.cs
855fe7465aef78a50a9e785a6ac36ab63b585f9f187f4cb241b1e939cfb0c44a  services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentAcceptanceService.cs
f8d17e90b6f9de42603e8f5b55b617db1f23033b9d0cf33add5343ca56ed16fc  services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentEnvelope.cs
9754216c84aba56b748b3179e3d73d444aef89bf78b2a9b9a64d3f53f43e3161  services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentAcceptanceReceipt.cs
1ce2fd79a7d863c6fed08d4efba2fe0e9d4535b59e59865915de60b3fba21d44  services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditActor.cs
c7c02a8c57e0cec35fe8c120e3e3b527c2b2508077de49519583edee3cb218f9  execution/domains/platform-shared-services/module-packs/MOD-0021-FU01-trusted-durable-source-audit-intent-ingestion.md
6bd2934944287662959a45c2d3b5618451d8b706253b4e933b787584d6fa218f  docs/records/audits/2026-10/global-product-bring-up-inventory-2026-10-01.md
```

---

## 2 · MOD-0126 için gerekebilecek alanlar — **ÖNERİ, sözleşme değil**

Aşağısı **onaysız bir taslak alan listesidir**; hiçbiri sabitlenmemiş, hiçbir sürüm
dizesi üretilmemiştir. Değerler sahip kararıdır.

| alan | bugünkü durum | MOD-0126 için neden gerekebilir |
|---|---|---|
| `SourceService` | sabit `Diten.MDM` | Treasury servisinin kendi adı — Paket A D1/D3'e bağlı |
| `ContractVersion` | sabit `mod-0290.audit-intent.v1` | MOD-0126'ya ait **ayrı sürümlü profil**; ad **üretilmedi** |
| `SourceModule` | pack `:150`'de `product-item-sku-master`'a sabit | ödeme/onay satırlarının kendi modül kodu |
| `AggregateType` / `AggregateId` | serbest, <=160 / GUID | ödeme partisi veya onay nesnesi |
| `Operation` | eşlenemezse `MappingUnsupported` → 409 | ödeme akışının işlemleri |
| `AuditOutcome` | pack `:150` her satırı `Succeeded`'a eşler | ⚠ **reddedilen bir ödeme kararı bugün ifade edilemez.** Platform enum'u `Unknown/Succeeded/Failed/Denied` değerlerini tanımlar (`Diten.Platform.Domain/Enums/AuditEnums.cs`), ama ingest profili bunu kullanmaz |
| `reason` / `severity` | envelope'ta **böyle alan yok** | gerekiyorsa profil genişlemesi — sahip kararı |
| `AuditCategory` | pack `:150` `MasterData`'ya sabitler | ödeme için uygun kategori ayrı karardır |

> **Reddedilen kararın kaydı** bu paketin en belirgin boşluğudur: daha önce iletilen
> ölçüte göre *"A refused decision should be audited without becoming an accepted
> business decision"* — bugünkü ingest profili `Succeeded` dışına izin vermiyor.

---

## 3 · Authenticated sender ile initiating user

### 3.1 Sender — doğrulanır

`TrustedSourceAuditIntentServiceIdentity.cs:36-53`: **tam claim sözleşmesi**, her biri
**tek değer** olmak zorunda:
`actor_type == "service"` · `service_name == RequiredServiceName` (`Diten.MDM`) ·
`tenant_id` boş olmayan GUID · `aud == TRUSTED_AUDIT_SOURCE_INGEST`.
Sapma → `Forbidden`. Pack `:249` tamamlıyor: RS256, bilinen süresi geçmemiş `kid`, tam
issuer, **tam 300 saniye ömür**.

### 3.2 Initiating user — **güvenilir doğrulama yolu YOK**

`ActorId` **gövdeden** gelir. Sunucunun yaptığı tek işlem, GUID ise maskelenmiş görünen
ad çözümlemesidir (`TrustedSourceAuditIntentAcceptanceService.cs:84-95`); çözülemezse
satır isimsiz yazılır ve **kabul başarısız olmaz**, GUID değilse satır servis satırı
sayılır (`TrustedSourceAuditActor.None`).

`ActorId`'yi çağırana, bir oturuma veya ikinci bir kimlik kanıtına bağlayan **hiçbir
kontrol bulunamadı.** Güven modeli `TrustedSourceAuditActor.cs` yorumunda açıktır:
*"the source names its actor by id only"*. Pack `:249` de sınırı ters yönden koyar:
*"a self-declared body actor cannot act as the transport service"* — yani gövde aktörü
kimlik doğrulaması için kullanılamaz, ama audit satırının "kim"i olarak **beyan edildiği
gibi kabul edilir**.

> **Sonuç: `ActorId` tek başına gerçek kullanıcı kanıtı değildir.** Bugün desteklenen
> bir initiator doğrulama yolu **yoktur**. MDM'in grant'i veya kimliği Treasury'ye
> aktarılmaz; bu paket öyle bir yol önermez, kararı sahibe bırakır.
>
> Karşılaştırma notu (ölçüm): workflow yüzeyi ikinci bir kimlik taşır
> (`X-Delegated-Authorization` → `sub` + `tenant_id`, servis tenant'ıyla eşleşme zorunlu).
> Audit ingest yüzeyinde böyle bir mekanizma **yoktur**.

---

## 4 · Tenant binding, idempotency, duplicate, unknown, ret ve başarısızlık etkisi

| konu | ölçülen davranış |
|---|---|
| tenant binding | Sender tenant'ı **JWT `tenant_id`** claim'inden (tek, boş olmayan GUID). Gövdede ayrıca `TenantId` var ve merkezî anahtara giriyor (`:125`). ⚠ Gövde `TenantId` ile JWT `tenant_id`'nin eşitlendiği **bu incelemede izlenmedi** — açık ölçüm boşluğu |
| idempotency | Merkezî anahtar ordinal: `"{SourceService}:{TenantId:N}:{IntentId:N}:{ContractVersion}"` (`:125`); gövdede ayrıca `IdempotencyKey` (<=240) |
| duplicate | `Duplicate` → **200 OK** + aynı receipt, `Duplicate = true` |
| idempotency çakışması | `IdempotencyConflict` → **409** `AUDIT_SOURCE_INTENT_IDEMPOTENCY_CONFLICT` |
| contract/mapping desteklenmiyor | **409** `AUDIT_SOURCE_INTENT_CONTRACT_UNSUPPORTED` / `…MAPPING_UNSUPPORTED` |
| geçersiz | **400** `AUDIT_SOURCE_INTENT_INVALID` |
| erişilemez | **503** `AUDIT_SOURCE_INTENT_UNAVAILABLE` |
| **unknown outcome** | ⚠ `_ =>` dalı da **503**'e düşer (`InternalTrustedSourceAuditIntentController.cs:46`). Belirsizlik, erişilememe gibi raporlanır; ayrı bir "unknown" kodu **yoktur** |
| audit başarısızlığının çağıran işleme etkisi | Bu yüzey **çağıranın işlemini bilmez**; yalnız intent kabul eder. Çağıran tarafta ret/erişilememe'nin iş kararını durdurup durdurmayacağı **Recovery'de ölçülmedi** ve MOD-0290'ın kendi retry/delivery sorumluluğundadır (MOD-0033-FU02 §2: *"MOD-0290 owns its MDM token client, caching and audit delivery retry"*) |

---

## 5 · Receipt hangi aşamayı kanıtlar — seçenekler ayrı

| seçenek | bugünkü karşılığı |
|---|---|
| **(a) yerel (kaynak tarafı) outbox kabulü** | Bu yüzeyin kapsamı **değil**; kaynak servisin kendi dayanıklılığı |
| **(b) merkezî outbox kabulü** | ✅ **BUGÜNKÜ DAVRANIŞ.** Kabul `ITrustedSourceAuditIntentOutbox.AcceptAsync(...)` üzerinden geçer. Pack `:23`: *"receive an acknowledgement **only after Platform has durably accepted that intent into the existing `audit_outbox` collection**"* |
| **(c) canonical audit sink'e kalıcı teslim** | ❌ **bildirilmiyor.** Teslim `AuditOutboxWorker`'ın işidir; receipt onu kapsamaz |

> Receipt'i (c) gibi okumak **mevcut davranıştan daha güçlü bir iddiadır** ve bu belgede
> yapılmamıştır. 201/200, satırın canonical audit sorgu yüzeyinde göründüğü anlamına
> gelmez.
>
> Mevcut insan kabul referansı da bu sınırı taşır:
> `docs/records/audits/2026-10/global-product-bring-up-inventory-2026-10-01.md:50` —
> *"RS256 token, audience `TRUSTED_AUDIT_SOURCE_INGEST` (**only with the audit worker on**)"*.
> Kapsamı **MDM**; Treasury veya MOD-0126 **yok**.

---

## 6 · Gereken kararlar ve onay sırası

| sıra | karar | merci | neyin önkoşulu |
|---|---|---|---|
| E1 | MOD-0126'nın bu yüzeyin **ikinci kaynağı** olarak kabul edilip edilmeyeceği | `audit-owner` + `security-owner` | her şeyin |
| E2 | MOD-0021-FU01 `review` statüsünün ne olacağı; iş bu pack altında mı yürüyecek | MOD-0021-FU01 sahipleri + CT | E3'ten önce |
| E3 | MOD-0126 için **ayrı sürümlü profil** açılacak mı; `SourceService`/`SourceModule`/`ContractVersion` değerleri | `audit-owner` | kod değişikliğinden önce |
| E4 | `AuditOutcome` sabitinin (`Succeeded`) gevşetilip gevşetilmeyeceği; reddedilen kararın kaydı | `audit-owner` + `security-owner` | E3 ile birlikte |
| E5 | `reason` / `severity` alanlarının envelope'a girip girmeyeceği (sürüm artışı) | `audit-owner` | E3'ten sonra |
| E6 | **Initiator güven modeli**: gövde beyanı yeterli mi, ikinci kimlik kanıtı mı gerekli | `security-owner` | E1 ile birlikte; ödeme bağlamında en ağır karar |
| E7 | Gövde `TenantId` ↔ JWT `tenant_id` eşitliğinin şart koşulup koşulmayacağı | `security-owner` + `platform-owner` | önce §4'teki ölçüm boşluğunun kapatılması |
| E8 | `unknown` sonucun 503'ten ayrılıp ayrılmayacağı | `audit-owner` | profil değişikliğiyle birlikte |
| E9 | Receipt semantiğinin (b)'de kalması mı, (c)'ye taşınması mı | `audit-owner` + `platform-owner` | tüketici sözleşmesi yazılmadan önce |
| E10 | Servis kimliği/audience — **Paket A'ya bağlı** (`AllowedAudience` tekil) | MOD-0033 sahibi | E1 sonrası, kod öncesi |

⚠ **Workflow audience onayı audit audience onayı değildir.** Paket A'nın D1–D5'i bu
paketi açmaz; E1 ayrı bir karardır.

---

## 7 · Önerilen dosya ledger'ı — **ÖNERİ, uygulama değil**

| # | dosya | değişim | gerektiren karar |
|---|---|---|---|
| M1 | `…/Features/Audit/TrustedSourceAuditIntentAcceptanceService.cs` | `SourceService`/`ContractVersion` sabitleri → profil tablosu | E3 |
| M2 | aynı dosya, eşleme bölümü | `AggregateType`/`Operation` → kategori/outcome eşlemesi | E3, E4 |
| M3 | `…/Contracts/Audit/TrustedSourceAuditIntentEnvelope.cs` | yeni alanlar yalnız **yeni sürümde** | E5 |
| M4 | `…/API/Security/TrustedSourceAuditIntentServiceIdentity.cs` | `RequiredServiceName` bağı (Paket A L3 ile ortak) | Paket A D4 + E10 |
| M5 | `…/API/Controllers/Internal/InternalTrustedSourceAuditIntentController.cs` | `unknown` dalının ayrılması | E8 |
| M6 | (varsa) sürümlü profil dosyası | **bu pakette oluşturulmadı** | E3 |

**Protected path uyarısı:** `.antigravity/**` ve sealed kayıtlar ledger dışındadır.

### Test matrisi — **plan, koşturulmadı**

| # | senaryo | beklenen |
|---|---|---|
| U1 | MOD-0126 profili + doğru kimlik | 201 + receipt |
| U2 | MOD-0126 gövdesi, `Diten.MDM` kimliği | **reddedilmeli** — grant yayılmasın (E1/E10 kabul kriteri) |
| U3 | tanınmayan `ContractVersion` | **409** `…CONTRACT_UNSUPPORTED` |
| U4 | tanınmayan `SourceModule` / aggregate-operation çifti | **409** `…MAPPING_UNSUPPORTED` |
| U5 | aynı `IdempotencyKey`, aynı gövde | **200** + `Duplicate = true`, aynı receipt |
| U6 | aynı anahtar, farklı gövde | **409** `…IDEMPOTENCY_CONFLICT` |
| U7 | gövde `TenantId` ≠ JWT `tenant_id` | E7'nin kararına göre; bugün **ölçülmedi** |
| U8 | `ActorId` başka bir kullanıcının id'si | E6'nın kararına göre; bugün **kabul edilir** |
| U9 | `TimestampUtc` offset ≠ 0 veya +5 dk'dan ileri | **400** `…INVALID` |
| U10 | `EvidenceHash` küçük harf veya 64 hane değil | **400** `…INVALID` |
| U11 | outbox erişilemez | **503** `…UNAVAILABLE`, intent **kabul edilmiş sayılmaz** |
| U12 | MOD-0290 akışı regresyonu | değişmez |

### Fail-closed kabul kriterleri

- **F1 · Sabotaj kanıtı.** Profil tablosu kaldırıldığında U3 kırmızıya döner.
- **F2 · Grant yayılmaz.** U2 kırmızı kalmalıdır: MDM kimliği MOD-0126 satırı yazamaz.
- **F3 · Kabul = dayanıklı kabul.** Outbox yazımı başarısızsa 2xx dönmez (U11).
- **F4 · Değer sızıntısı yok.** Hiçbir hata gövdesi actor id, tenant id, hash veya
  secret yazmaz.
- **F5 · Receipt abartılmaz.** Receipt metni ve dokümantasyonu canonical sink teslimi
  iddia etmez (§5).

---

## Tek sonraki kapı

**E1 — MOD-0126 bu yüzeyin ikinci kaynağı olarak kabul edilsin mi?**
Sahip: `audit-owner` + `security-owner`. Bu karar verilmeden E3–E10 anlamsızdır,
ledger'ın hiçbir satırı açılamaz ve E6 (initiator güven modeli) ödeme bağlamında
bağımsız olarak çözülemez.

---

*Ölçüm · öneri · owner kararı · CT kabulü ayrımı: §1, §3, §4, §5 **ölçüm**; §2 ve §7
**öneri**; §6 **karar talebi**. Bu belgede CT kabulü **yoktur**. `review` statüsündeki
pack onaylı sayılmamıştır. Hiçbir test koşulmadı; hiçbir runtime gözlemi yoktur.*
