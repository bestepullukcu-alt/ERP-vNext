# E1 — MOD-0126, trusted audit ingest'in ayrı kaynak/profil tasarımına alınsın mı?

> ## `DRAFT / OWNER DECISION REQUIRED / NOT AUTHORIZED FOR IMPLEMENTATION`
>
> Bu bir **tasarım/kapsam kararı önerisidir**. Kod, profil yayımı, token issuance,
> grant, rol, CT kabulü, deployment veya canlı aktivasyon **onayı değildir ve öyle
> sunulamaz**. İmzalanması hâlinde yalnız *tasarımın başlamasına* izin verir.

| | |
|---|---|
| **Depo / HEAD** | `ERP-vNext-recovery` · `49f0de1342c08f7e9bad35a640545a2a8323628e` |
| **Tarih** | 2026-10-18 |
| **Önceki sürüm** | `PKG-B-MOD-0021-FU01-MOD-0126-AUDIT-INGEST-DRAFT-v0.1.md` (**korunur, değiştirilmedi**) · sha256 `7a8b8bd2007c35e668434389ed2274cb87de3951ee557655c9843515c4b906fa` |
| **İlgili pack** | `MOD-0021-FU01` — **`status: review`** (onaylı/uygulanabilir **değil**) |
| **Evidence level** | E2 — kod ve pack metni okuması; hiçbir test koşulmadı |

---

## Karar rolleri — teyit sonucu

Bildirilen roller: `audit-owner` + `security-owner`.

**Bu rollerin gerçek karar yetkisi teyit edilemedi.** `AGENTS.md` ve
`.antigravity/rules/*.md` içinde bu adlar **hiç geçmiyor**; yalnız pack frontmatter'ında
`owner:` dizesi olarak varlar (`audit-owner` 2 pack, `security-owner` 3 pack).
`docs/records/decisions/` altındaki 74 kayıtta imzalayan merci istisnasız
**`ny@gmgroup.ch`**. Rol adları danışılacak taraflardır; imzalayan merci product
owner'dır. Kişi ataması yapılmamıştır. *(Aynı tespit D1'de de geçerlidir.)*

---

## A · Karar önerisi (imzalanabilir)

> **Önerilen hüküm.** MOD-0126, trusted audit ingest yüzeyinin **ayrı bir kaynak ve
> ayrı bir sürümlü profil** tasarımına alınır. MDM / MOD-0290 profili Treasury adına
> **kullanılmaz**. Bu karar yalnız tasarım ve kapsam kararıdır.

Hükmün ayrılmaz parçaları:

1. **Sender ile initiating user ayrı tutulur.** Sender, servis token'ının doğrulanmış
   claim sözleşmesidir. Initiating user ayrı bir kavramdır ve sender'dan türetilemez.
2. **Yalnız gövdedeki `ActorId` güvenilir kullanıcı kanıtı sayılmaz.** Ölçülen durum:
   bugün gövde beyanı olduğu gibi kabul ediliyor ve çağırana bağlanmıyor; bugün
   desteklenen bir initiator doğrulama yolu **yoktur**. Ödeme bağlamında bunun yeterli
   olup olmadığı ve kanıt yönteminin ne olacağı **security-owner'a ayrı bir karar
   olarak sunulur**. Bu öneri bir yöntem dayatmaz.
3. **Alan eşlemesi tasarlanır:** tenant, aggregate (tip + id), sürüm (`PreVersion` /
   `PostVersion`), operation, correlation/causation, olay zamanı, evidence ve
   idempotency. Mevcut zarfın 18 alanı başlangıç noktasıdır; **yeni alan ancak yeni
   sürümlü profille** gelir.
4. **Dört sonuç ayrı değerlendirilir ve karıştırılmaz:**
   **(a)** başarıyla kaydedilmiş **Reject** kararı — reddedilen bir iş kararı, kabul
   edilmiş iş kararına dönüşmeden audit edilebilmelidir;
   **(b)** yetkisiz istek reddi;
   **(c)** başarısız mutation;
   **(d)** belirsiz sonuç.
   ⚠ Ölçülen engel: profil bugün her satırı `AuditOutcome.Succeeded`'a eşliyor
   (`MOD-0021-FU01:150`), oysa platform enum'u `Unknown/Succeeded/Failed/Denied`
   tanımlıyor. (a) ve (c) bugün **ifade edilemiyor**.
5. **Receipt yalnız gerçekten garanti ettiği aşamayı bildirir.** Ölçülen aşama
   **merkezî outbox kabulü**dür (`MOD-0021-FU01:23`). **Central outbox kabulü
   canonical audit teslimi sayılmaz** ve belgelerde öyle sunulmaz.
6. **Replay/conflict ve eksik/geçersiz receipt davranışı tanımlanır.** Duplicate'in
   aynı receipt'i döndürmesi, çakışmanın ayrı kalması ve istemcinin receipt'i hiç
   alamaması durumunun nasıl ele alınacağı tasarımın çıktısıdır.
7. **Yerel mutation/audit atomikliği ile merkezî teslim ayrı kalır.** Kaynak tarafın
   kendi işlem atomikliği, merkezî teslim garantisi değildir; ikisi tek bir "audit
   edildi" ifadesinde birleştirilmez.
8. **MDM grant'i veya kimliği Treasury'ye aktarılmaz.** Profil ayrımı, kimlik ayrımı
   olmadan anlamsızdır — bu nedenle E1, D1'siz uygulanabilir değildir.

---

## B · Bu karar neyi onaylar, neyi onaylamaz

| onaylar | onaylamaz |
|---|---|
| MOD-0126 için ayrı kaynak/profil **tasarımının başlamasını** | profilin yayımlanmasını veya koda girmesini |
| Sender ↔ initiator ayrımının tasarım ilkesi olmasını | bir initiator doğrulama yönteminin seçilmesini |
| Dört sonucun ayrı ele alınmasını | `AuditOutcome` sabitinin gevşetilmesini |
| Receipt semantiğinin olduğu gibi bildirilmesini | receipt'in canonical teslim iddia etmesini |
| | `MOD-0021-FU01`'in `review` statüsünün değişmesini |
| | yeni sürüm dizesi, `SourceService` veya `SourceModule` değeri üretilmesini |
| | DCP onayını, CT acceptance'ını, canlı kabulü |

Kaynak sahibinin bildirilen kararları — *MOD-0126 akışı sözleşmeler hazır olana kadar
**kapalı***; sıra *MOD-0126 → MOD-0217 → MOD-0124* — bu öneriyle **çelişmez ve onun
tarafından onaylanmaz**. Recovery producer kapsamı bu kararla yetkilendirilmez.

---

## C · Uygulamaya geçilmeden çözülmesi zorunlu koşullar

| # | koşul | neden bloke |
|---|---|---|
| K1 | `MOD-0021-FU01`'in `review` statüsünün ne olacağı | `review` pack uygulanabilir değildir |
| K2 | **Initiator güven modeli** — gövde beyanı yeterli mi, ikinci kimlik kanıtı mı | ödeme bağlamında en ağır karar; profil bundan önce yazılamaz |
| K3 | `AuditOutcome` sabitinin gevşetilip gevşetilmeyeceği (Reject kaydı) | A/4(a) bu olmadan karşılanamaz |
| K4 | `reason` / `severity` alanlarının gerekip gerekmediği | sürüm artışını belirler |
| K5 | `SourceService` / `SourceModule` / `ContractVersion` değerleri | **bu belgede üretilmemiştir** |
| K6 | `unknown` sonucun 503'ten ayrılıp ayrılmayacağı | A/4(d)'nin önkoşulu |
| K7 | Receipt semantiğinin outbox kabulünde kalması mı, ileri taşınması mı | tüketici sözleşmesinden önce |
| K8 | **D1** — ayrı servis kimliği ve audience | E1 tek başına yüzeyi açamaz; tek-audience sınırı iki kimlik gerektirebilir |

---

## D · Sonraki uygulama paketinin sınırları

**İzin verilen kapsam (E1 imzalanırsa, ayrı bir uygulama kararıyla):** yalnız v0.1
§7'deki M1–M6 ledger'ı. **Dışarıda kalan:** `.antigravity/**`, sealed kayıtlar,
MOD-0290'ın mevcut profili ve akışı, workflow yüzeyi, reference-data yüzeyi,
`AuditOutboxWorker`'ın teslim davranışı.

**Planlanan kabul testleri** (v0.1 §7 U1–U12'den türetilmiş; hiçbiri koşulmadı):

| öncelik | test | kabul |
|---|---|---|
| P0 | `Diten.MDM` kimliğiyle MOD-0126 gövdesi | **reddedilir** — grant yayılmaz (A/8'in kanıtı) |
| P0 | Profil tablosu kaldırıldığında tanınmayan sürüm testi **kırmızıya döner** | sabotaj kanıtı |
| P0 | Outbox yazımı başarısızsa **2xx dönmez** | kabul = dayanıklı kabul (A/5) |
| P0 | MOD-0290 akışı regresyonsuz | mevcut davranış korunur |
| P1 | tanınmayan `ContractVersion` | **409** `…CONTRACT_UNSUPPORTED` |
| P1 | tanınmayan aggregate/operation | **409** `…MAPPING_UNSUPPORTED` |
| P1 | aynı anahtar + aynı gövde | **200** + `Duplicate = true`, aynı receipt |
| P1 | aynı anahtar + farklı gövde | **409** `…IDEMPOTENCY_CONFLICT` |
| P1 | Reject kararı kaydı | K3'ün kararına göre; bugün **ifade edilemiyor** |
| P2 | `ActorId` başka kullanıcının id'si | K2'nin kararına göre; bugün **kabul ediliyor** |
| P2 | hata gövdelerinde actor id / tenant id / hash / secret yok | sızıntı yok |
| P2 | receipt metni canonical teslim iddia etmiyor | A/5'in belge kabulü |

---

## E · Referanslar

```
HEAD  49f0de1342c08f7e9bad35a640545a2a8323628e

docs/roadmap/plans/erp-finance-legal-module-intake-01/
  PKG-B-MOD-0021-FU01-MOD-0126-AUDIT-INGEST-DRAFT-v0.1.md
    7a8b8bd2007c35e668434389ed2274cb87de3951ee557655c9843515c4b906fa
  PKG-B-E1-DECISION-PROPOSAL-v0.2.md   (bu belge)

execution/domains/platform-shared-services/module-packs/
  MOD-0021-FU01-trusted-durable-source-audit-intent-ingestion.md
    c7c02a8c57e0cec35fe8c120e3e3b527c2b2508077de49519583edee3cb218f9

services/Diten.Platform/src/
  Diten.Platform.API/Security/TrustedSourceAuditIntentRequestExecutor.cs
    a97a5ac3f672152550d18e31051b717472d12738e781c530132d99339de6361d
  Diten.Platform.API/Security/TrustedSourceAuditIntentServiceIdentity.cs
    0abe5574a2c18afbd1f19777b1d4266f956a8aa0e3b2e162a82d56e7e66a5c47
  Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentAcceptanceService.cs
    855fe7465aef78a50a9e785a6ac36ab63b585f9f187f4cb241b1e939cfb0c44a
  Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentEnvelope.cs
    f8d17e90b6f9de42603e8f5b55b617db1f23033b9d0cf33add5343ca56ed16fc
  Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentAcceptanceReceipt.cs
    9754216c84aba56b748b3179e3d73d444aef89bf78b2a9b9a64d3f53f43e3161

docs/records/audits/2026-10/global-product-bring-up-inventory-2026-10-01.md
    6bd2934944287662959a45c2d3b5618451d8b706253b4e933b787584d6fa218f
    (:50 — mevcut tek insan kabul referansı; kapsamı MDM, Treasury/MOD-0126 değil)
```

---

## İmza bloğu — boş bırakılmıştır

```
- owner:   ____________________          (karar mercii; bu belge kimse atamamıştır)
- status:  ____________________          (ACCEPTED / REJECTED / DEFERRED)
- scope:   tasarım ve kapsam kararı — profil yayımı, issuance, grant, CT kabulü, aktivasyon DEĞİL
- expiry:  ____________________
- date:    ____________________
```

*Bu belge bir öneridir. İmzalanana kadar hiçbir hükmü yürürlükte değildir; imzalandıktan
sonra da yalnız tasarımı açar. E1, D1 olmadan uygulamaya dönüşemez (K8).*
