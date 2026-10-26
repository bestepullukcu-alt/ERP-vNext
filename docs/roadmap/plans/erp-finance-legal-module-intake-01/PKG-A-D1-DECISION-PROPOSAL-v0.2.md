# D1 — Treasury, ayrı bir first-party servis tüketicisi olarak kimlik tasarımına alınsın mı?

> ## `DRAFT / OWNER DECISION REQUIRED / NOT AUTHORIZED FOR IMPLEMENTATION`
>
> Bu bir **tasarım/kapsam kararı önerisidir**. Kod, token issuance, client kaydı,
> credential, grant, rol, CT kabulü, deployment veya canlı aktivasyon **onayı değildir
> ve öyle sunulamaz**. İmzalanması hâlinde yalnız *tasarımın başlamasına* izin verir.

| | |
|---|---|
| **Depo / HEAD** | `ERP-vNext-recovery` · `49f0de1342c08f7e9bad35a640545a2a8323628e` |
| **Tarih** | 2026-10-18 |
| **Önceki sürüm** | `PKG-A-MOD-0033-FU02-TREASURY-WORKFLOW-IDENTITY-DRAFT-v0.1.md` (**korunur, değiştirilmedi**) · sha256 `14eabb94f1fc9a13250cd38afb1212a8dd71b448aa39236753daf9a7f1160f60` |
| **İlgili pack** | `MOD-0033-FU02` — **`status: review`** (onaylı/uygulanabilir **değil**) |
| **Evidence level** | E2 — kod ve pack metni okuması; hiçbir test koşulmadı |

---

## Karar rolleri — teyit sonucu

Bildirilen roller: `api-consumer-credential-owner` + `platform-owner`.

**Bu rollerin gerçek karar yetkisi teyit edilemedi.** Ölçüm:

- `AGENTS.md` ve `.antigravity/rules/*.md` içinde bu rol adları **hiç geçmiyor** (0 eşleşme).
- Yalnız module pack frontmatter'larında `owner:` dizesi olarak varlar
  (`api-consumer-credential-owner` 1 pack, `platform-owner` 3 pack).
- Buna karşılık `docs/records/decisions/` altındaki **74 kayıtta** imzalanan merci
  istisnasız **`ny@gmgroup.ch`** (product owner / product-integration owner), kararlar
  Control Tower tarafından iletiliyor.

**Sonuç:** pack'teki rol adları bir *yetki kütüğü* değil, *sorumluluk etiketi*. Bu
öneriyi imzalayacak merci, gözlenen desene göre **product owner**'dır; rol adları
danışılacak taraflar olarak kalır. Kişi ataması yapılmamıştır.

---

## A · Karar önerisi (imzalanabilir)

> **Önerilen hüküm.** Treasury (MOD-0126), MDM kimliğini ödünç almayan, **ayrı bir
> first-party servis tüketicisi** olarak kimlik tasarımına alınır. Bu karar yalnız
> tasarım ve kapsam kararıdır.

Hükmün ayrılmaz parçaları — imzalanırsa bunlar da imzalanmış olur:

1. **MDM kimliği kullanılmaz.** Treasury akışları `Diten.MDM` `service_name`'i,
   `ClientId`'si veya grant'i üzerinden geçirilmez. Geçici çözüm olarak dahi
   önerilmez; aksi hâlde audit izinde çağıran MDM görünür.
2. **Tek-audience sınırı korunur.** `MOD-0033-FU02:225` — *"identity AllowedAudience
   remains singular"*. Treasury hem workflow hem audit yüzeyine ihtiyaç duyuyorsa
   **iki ayrı client/kimlik bağı** tasarlanır. `ServiceName` ile audience arasındaki
   ilişki (tek ad + iki client mı, iki ad mı) tasarımın ilk çıktısıdır.
3. **Dört katman için değişiklik sınırı yazılır** — issuer politikası, client registry
   doğrulaması, Platform validator, workflow start allowlist. Sınırlar v0.1 §2 ve
   §7'deki ledger'dan alınır; bu kararla **uygulanmaz**, yalnız sınırı sabitlenir.
4. **Ortak validator genişlemesi yasaktır.** `HasExactServiceClaims` üç audience'ın
   paylaştığı metottur (`TrustedServiceTokenValidationExtensions.cs:275`). Değişiklik
   **audience başına** ayrıştırılır; `TRUSTED_AUDIT_SOURCE_INGEST` ve
   `TRUSTED_REFERENCE_DATA_CONSUMER` yüzeyleri kendiliğinden açılamaz. Bu, tasarımın
   kabul kriteridir, iyi niyet beyanı değil.
5. **Tenant, object/template ve delegated-user sınırları tanımlanır.** Allowlist
   girdisinde tenant alanı olmadığı ölçülmüştür (v0.1 §4); tenant başına daraltmanın
   gerekip gerekmediği tasarımın cevaplayacağı sorudur.
6. **Hiçbir gerçek değer üretilmez.** Bu kararla ClientId, GUID, `ServiceName`,
   credential, hash, tenant grant veya allowlist girdisi **oluşturulmaz**. Hepsi
   onboarding girdisi olarak açık kalır (v0.1 §5).

---

## B · Bu karar neyi onaylar, neyi onaylamaz

| onaylar | onaylamaz |
|---|---|
| Treasury için ayrı kimlik **tasarımının başlamasını** | kodun yazılmasını veya değiştirilmesini |
| Dört katmanın değişiklik sınırının **yazılmasını** | o katmanların değiştirilmesini |
| İki ayrı client/kimlik bağının **tasarlanmasını** | client kaydı, credential veya grant oluşturulmasını |
| Audience ayrıştırmasının tasarım kabul kriteri olmasını | token ihracını veya herhangi bir aktivasyonu |
| | `MOD-0033-FU02`'nin `review` statüsünün değişmesini |
| | DCP onayını veya CT acceptance'ını |
| | Treasury'nin canlı kabulünü |

Kaynak sahibinin bildirilen kararları — *MOD-0126 güvenilir akışı gerekli sözleşmeler
hazır olana kadar **kapalı***; sıra *MOD-0126 → MOD-0217 → MOD-0124* — bu öneriyle
**çelişmez ve onun tarafından onaylanmaz**. Recovery producer kapsamı veya uygulaması
bu kararla yetkilendirilmez.

---

## C · Uygulamaya geçilmeden çözülmesi zorunlu koşullar

| # | koşul | neden bloke |
|---|---|---|
| C1 | `MOD-0033-FU02`'nin `review` statüsünün ne olacağı; iş bu pack altında mı yürüyecek | `review` pack uygulanabilir değildir |
| C2 | `ServiceName` ↔ audience ilişkisinin seçilmesi (tek ad + iki client / iki ad) | registry girdisi bu olmadan yazılamaz |
| C3 | Ortak validator'ın audience başına ayrıştırılma tasarımı | aksi hâlde iki yüzey istemeden açılır |
| C4 | Allowlist girdisinde tenant bağı olmaması kabul mü, daraltma mı | girdi yazımının önkoşulu |
| C5 | `ObjectType` / `TemplateCode` adlandırması ve girdi sayısı (64 üst sınır) | girdi yazımının önkoşulu |
| C6 | Credential üretimi, saklanması, rotasyon sorumlusu | hiçbir config yazımından önce |
| C7 | Tenant grant kapsamı | hiçbir config yazımından önce |
| C8 | PKG-B / E1'in ayrı karara bağlanması | audit audience'ı bu kararla açılmaz |

---

## D · Sonraki uygulama paketinin sınırları

**İzin verilen kapsam (D1 imzalanırsa, ayrı bir uygulama kararıyla):** yalnız v0.1
§7'deki L1–L6 ledger'ı. **Dışarıda kalan:** `.antigravity/**`, sealed kayıtlar, MDM'in
mevcut kimliği ve grant'leri, audit ingest yüzeyi, reference-data yüzeyi.

**Planlanan kabul testleri** (v0.1 §7 T1–T11'den türetilmiş; hiçbiri koşulmadı):

| öncelik | test | kabul |
|---|---|---|
| P0 | Treasury kimliğiyle `source-intents/accept` ve reference-data yüzeyi | **401/403** — C3'ün kanıtı |
| P0 | Ortak validator parametreleştirmesi geri alındığında yukarıdaki test **kırmızıya döner** | sabotaj kanıtı; olmazsa guard sayılmaz |
| P0 | MDM akışları regresyonsuz | mevcut davranış korunur |
| P1 | allowlist girdisi yokken start | **403** `WORKFLOW_TRUSTED_START_FORBIDDEN` |
| P1 | `ObjectType` sapması | **403** |
| P1 | delegated tenant ≠ servis tenant | **403** `WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH` |
| P1 | `platform.workflow.instances.start` izni yok | **403** `WORKFLOW_DELEGATED_USER_FORBIDDEN` |
| P2 | tanınmayan `ServiceName` config'de | Auth başlangıçta durur; entry index + alan adı, **değer yok** |
| P2 | hata gövdelerinde hash/secret/tenant/claim değeri yok | sızıntı yok |

---

## E · Referanslar

```
HEAD  49f0de1342c08f7e9bad35a640545a2a8323628e

docs/roadmap/plans/erp-finance-legal-module-intake-01/
  PKG-A-MOD-0033-FU02-TREASURY-WORKFLOW-IDENTITY-DRAFT-v0.1.md
    14eabb94f1fc9a13250cd38afb1212a8dd71b448aa39236753daf9a7f1160f60
  PKG-A-D1-DECISION-PROPOSAL-v0.2.md   (bu belge)

execution/domains/platform-shared-services/module-packs/
  MOD-0033-FU02-service-identity-token-issuance-foundation.md
    3f243b25cb82c10077a1bba7453ccb022d7abb01d13ef867e2eee689b8264df9

services/Diten.Platform/src/Diten.Platform.API/Security/
  TrustedServiceTokenValidationExtensions.cs
    d8d091cfb3f0b49cbe9c5fadade4351377d27a6f7c0f7ccaa3e2326b90321d19
  ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs
    cbe325f758572500fe92a26b6f8cd4c9bde815c25ccf0f3c0344cf1e64f62259
services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/
  ServiceIdentityTokenAudiencePolicy.cs
    5a9ce4df891223d0f8dbdb4f93228114986b66f567928167df37b0b46f4ea4ca
```

---

## İmza bloğu — boş bırakılmıştır

```
- owner:   ____________________          (karar mercii; bu belge kimse atamamıştır)
- status:  ____________________          (ACCEPTED / REJECTED / DEFERRED)
- scope:   tasarım ve kapsam kararı — kod, issuance, grant, CT kabulü, aktivasyon DEĞİL
- expiry:  ____________________
- date:    ____________________
```

*Bu belge bir öneridir. İmzalanana kadar hiçbir hükmü yürürlükte değildir; imzalandıktan
sonra da yalnız tasarımı açar.*
