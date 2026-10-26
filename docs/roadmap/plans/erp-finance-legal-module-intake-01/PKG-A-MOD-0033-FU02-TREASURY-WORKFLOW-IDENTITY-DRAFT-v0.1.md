# PAKET A — MOD-0033-FU02 · Treasury Workflow Service Identity

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
| **HEAD doğrulaması** | `git rev-parse HEAD` ile teyit; `git diff --stat` ile ilgili dört yolda **fark yok** → önceki ölçümler güncel |
| **Tarih** | 2026-10-18 |
| **Evidence level** | **E2 — kod ve pack metni okuması.** Hiçbir test koşulmadı, hiçbir HTTP çağrısı yapılmadı, hiçbir runtime gözlemi yok. |

**Kapsam cümlesi.** Treasury (MOD-0126) için MDM kimliğini ödünç almayan, ayrı ve
güvenilir bir workflow trusted-consumer servis kimliğinin bugün neden mümkün olmadığını
ölçer; etkilenecek katmanları, sahiplerini, bağlama sözleşmesini ve karar sırasını
sahiplerin önüne koyar. Çözümü uygulamaz, değer üretmez, onay istemez — karar ister.

---

## 1 · Ölçülen gerçek — Treasury kimliği dört katmanda engelli

| # | katman | engel | dosya:satır |
|---|---|---|---|
| 1 | **issuer ihraç politikası** | `IsAllowedPair(serviceName, audience)` yalnız `MdmServiceName` kabul eder | `Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs:5`, `:17-19` |
| 2 | **client registry doğrulayıcı** | kabul listesi dışı servis adı: *"is not a service the issuer accepts"* | `Diten.AuthService.Application/Features/ServiceIdentityTokens/Registry/ServiceClientRegistryValidator.cs:44` |
| 3 | **Platform token validator** | `RequiredServiceName = "Diten.MDM"`, **üç audience'ın paylaştığı** `HasExactServiceClaims` içinde | `Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs:18`, `:275` |
| 4 | **workflow start allowlist** | girdi `ServiceName` ordinal `"Diten.MDM"` değilse **tüm yapılandırma** reddedilir | `Diten.Platform.API/Security/ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs:79` |

Dördü bağımsız olarak fail-closed. Treasury 1'i geçemediği için token alamaz; alsa 3'te
claim sözleşmesine, geçse 4'te allowlist'e takılır.

**Aday tanımlayıcılar hedefte yok** (ölçüm): `mod0126.payment_batch` 0 dosya ·
`MOD0126_PAYMENT_APPROVAL` 0 dosya · `payment_batch` 0 dosya · `PAYMENT_APPROVAL` 0 dosya.
Kayıtlı bir Treasury servis istemcisi **yok**.

### Evidence paths + SHA-256

```
d8d091cfb3f0b49cbe9c5fadade4351377d27a6f7c0f7ccaa3e2326b90321d19  services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs
cbe325f758572500fe92a26b6f8cd4c9bde815c25ccf0f3c0344cf1e64f62259  services/Diten.Platform/src/Diten.Platform.API/Security/ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs
5a9ce4df891223d0f8dbdb4f93228114986b66f567928167df37b0b46f4ea4ca  services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs
af837a9b3d5ab922249fd7d72e3372cbcfc71c3334bc68db993e85b8bea2788f  services/Diten.Platform/src/Diten.Platform.API/Security/TrustedWorkflowConsumerRequestExecutor.cs
5b9213830d120a5eefddcdcd9e9e58b3d2023df71aed0ad10f1cb4a0402b50e9  services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedWorkflowConsumerController.cs
3f243b25cb82c10077a1bba7453ccb022d7abb01d13ef867e2eee689b8264df9  execution/domains/platform-shared-services/module-packs/MOD-0033-FU02-service-identity-token-issuance-foundation.md
```

---

## 2 · Etkilenecek katmanlar ve sahipleri

| katman | değişim türü | kayıtlı sahip | kayıt |
|---|---|---|---|
| issuer ihraç politikası (1) | **KOD** | `api-consumer-credential-owner / auth-security-owner / platform-owner` | MOD-0033-FU02 frontmatter, **status `review`** |
| client registry / config (2) | **CONFIG** — `ServiceClientRegistry:Clients[]` | aynı | MOD-0033-FU02 §F.1 (`:508-528`) |
| Platform token validator (3) | **KOD** | `platform-owner` (MOD-0033-FU02 §2: *"Platform validates issuer public keys and token claims"*) | MOD-0033-FU02 §2 |
| workflow start allowlist (4) | **KOD** + **CONFIG** | `platform-shared-services` | MOD-0023 frontmatter, **status `ready-for-dev`** |

⚠ **`review` statüsündeki bir pack onaylı/uygulanabilir değildir.** MOD-0033-FU02
`review`; kendi metni de sınırı yazıyor (`:8-11`): *"Operational configuration, secret,
credential, tenant-grant, data, Production/Staging and push mutations remain
unauthorized."*

Registry'nin kayıtlı iki davranışı taşıma planını belirler (MOD-0033-FU02 `:510-520`):
mevcut kimlik **asla değiştirilmez** (farklı hash/version/servis adı/audience yalnız
başlangıçta raporlanır); config'den girdi kaldırmak hiçbir şeyi silmez veya iptal etmez.
Ayrıca doğrulama fail-closed ve yazımdan önce: kapalı listenin dışında bir
`ServiceName`/`AllowedAudience` **Auth'un başlamasını durdurur**.

> **Ölçümün sonucu:** katman 2 config'dir ama katman 1 olmadan çalışmaz — kapalı liste
> kodda. Yani "sadece config ile Treasury eklenir" **doğru değildir**.

---

## 3 · Kimlik başına tek audience sınırı

MOD-0033-FU02 `:225`: *"The exact pair is `Diten.MDM/TRUSTED_REFERENCE_DATA_CONSUMER`;
**identity AllowedAudience remains singular**."* Registry girdisi de tekil alan taşıyor:
`AllowedAudience` (`:508-510`).

**Mevcut kurala göre sonuç:** Treasury hem workflow (`TRUSTED_WORKFLOW_CONSUMER`) hem
audit (`TRUSTED_AUDIT_SOURCE_INGEST`) yüzeyini kullanacaksa, bugünkü kural altında
**iki ayrı kimlik/client girdisi** gerekir — tek kimliğe iki audience verilemez.

Bu, Paket B ile Paket A'yı birbirine bağlar ama **birleştirmez**: iki ayrı audience, iki
ayrı sahip (`platform/auth-security` ve `audit-owner/security-owner`), iki ayrı karar.
Workflow audience onayı audit audience onayı **sayılmaz**.

---

## 4 · Bağlama sözleşmesi (ölçülen)

| bağ | nereden | nasıl doğrulanır |
|---|---|---|
| `service_name` | servis token claim'i | tek değer, ordinal tam eşleşme (`TrustedServiceTokenValidationExtensions.cs:275`) |
| `issuer` | `TrustedServiceTokenValidation:Issuer` | `ValidateIssuer = true` + claim tam eşleşme |
| `audience` | servis token `aud` | `WorkflowRequiredAudience = "TRUSTED_WORKFLOW_CONSUMER"` tam eşleşme |
| `tenant` | **servis token `tenant_id` claim'i** | header **yasak** → `X-Tenant-Id` gönderilirse 403 `WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN` |
| `subject` (servis) | `sub` GUID, tek | claim sözleşmesi; ayrıca `actor_type="service"`, `jti` GUID, `iat==nbf`, `exp==nbf+300` |
| `delegated user` | `X-Delegated-Authorization` → `sub` + `tenant_id` | `delegatedUser.TenantId != serviceIdentity.TenantId` → 403 `WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH` |
| delegated izin | `permission` claim | `platform.workflow.instances.start`; yoksa 403 `WORKFLOW_DELEGATED_USER_FORBIDDEN` (**yalnız `start`**; `cancel`'da izin kontrolü yok) |
| `object/template` | **allowlist girdisi** | `ClientId + ServiceName + Audience + ObjectType + (TemplateId XOR TemplateCode)`, ordinal tam eşleşme (`ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs:32-42`) |

⚠ **Allowlist girdisinde tenant alanı yoktur.** Tenant izolasyonu yalnız token
claim'inden ve delegated kullanıcı eşleşmesinden gelir. Bir girdi, o `ClientId`'nin
**her tenant'ında** aynı object/template için geçerlidir. Tenant başına daraltma
isteniyorsa bu ayrı bir karardır.

⚠ Üst sınır 64 girdi (`TrustedWorkflowStartAuthorizationOptions.MaximumEntries`).
Treasury'nin her iş nesnesi tipi kendi girdisini tüketir.

---

## 5 · Açık bırakılan değerler — onboarding girdisi olarak sahibe

Bu pakette **hiçbiri üretilmemiştir** ve üretilmemelidir:

| değer | kim verir |
|---|---|
| `ClientCode` | MOD-0033 sahibi |
| `ClientId` (GUID) | registry kaydı sırasında |
| `ServiceName` (Treasury servisinin tam adı) | platform/auth-security sahibi |
| `AllowedAudience` | karar: workflow mu, audit mi, ikisi için iki kimlik mi (§3) |
| `CredentialHash` / `CredentialVersion` | offline credential generator (MOD-0033-FU02 §F.2); **raw secret asla Auth'a ulaşmaz** |
| `Tenants[]` | tenant-grant sahibi |
| `ObjectType` / `TemplateCode` | MOD-0126 iş sahibi + MOD-0023 sahibi birlikte |

Kaynak taraftan aday olarak anılan `mod0126.payment_batch` ve `MOD0126_PAYMENT_APPROVAL`
hedefte **yoktur**; burada aday olarak dahi sabitlenmemiştir.

---

## 6 · Gereken kararlar ve sırası

Her karar, hangi değişiklikten **önce** gerektiğiyle birlikte:

| sıra | karar | merci | neyin önkoşulu |
|---|---|---|---|
| D1 | Treasury'nin ayrı bir first-party servis istemcisi olarak **tanınması** | `api-consumer-credential-owner` + `platform-owner` | her şeyin |
| D2 | MOD-0033-FU02'nin `review` → ileri statüye taşınıp taşınmayacağı; yoksa bu işin hangi pack altında yürüyeceği | MOD-0033-FU02 sahipleri + CT | D3'ten önce |
| D3 | İhraç kapalı listesinin genişletilmesi (katman 1, **KOD**) | `auth-security-owner` | katman 2 config'inden önce |
| D4 | Platform validator sabitinin **audience başına** ayrıştırılması (katman 3, **KOD**) | `platform-owner` + security | ⚠ D4 alınmadan katman 3 genelleştirilirse `TRUSTED_AUDIT_SOURCE_INGEST` ve `TRUSTED_REFERENCE_DATA_CONSUMER` yüzeyleri de açılır |
| D5 | Workflow allowlist'in başka servis adı kabul etmesi (katman 4, **KOD**) | `platform-shared-services` (MOD-0023) | allowlist girdisinden önce |
| D6 | `AllowedAudience` tekil kuralı korunacak mı; Treasury iki kimlik mi alacak | MOD-0033 sahibi | D1 ile birlikte |
| D7 | Allowlist girdisinin tenant bağı olmaması kabul mü, tenant başına daraltma mı | platform + security | girdi yazımından önce |
| D8 | `ObjectType`/`TemplateCode` adlandırması ve girdi sayısı | MOD-0126 iş sahibi + MOD-0023 sahibi | girdi yazımından önce |
| D9 | Credential üretimi, saklanması ve rotasyon sorumlusu | `api-consumer-credential-owner` | hiçbir config yazımından önce |
| D10 | Tenant grant kapsamı | tenant-grant sahibi | hiçbir config yazımından önce |

**D4 bu paketin en kritik maddesidir.** Sabit, üç audience'ın paylaştığı doğrulayıcıdadır
(`:275`); workflow dışındaki iki üretim tüketicisi `TrustedSourceAuditIntentServiceIdentity.cs:41`
ve `VerifiedReferenceDataServiceTenantContext.cs:47`.

---

## 7 · Önerilen dosya ledger'ı — **ÖNERİ, uygulama değil**

| # | dosya | değişim | gerektiren karar |
|---|---|---|---|
| L1 | `…/ServiceIdentityTokens/ServiceIdentityTokenAudiencePolicy.cs` | `IsAllowedPair` kapalı listesi | D3 |
| L2 | `…/ServiceIdentityTokens/Registry/ServiceClientRegistryValidator.cs` | kabul edilen servis adı kümesi | D3 |
| L3 | `…/Security/TrustedServiceTokenValidationExtensions.cs` | `HasExactServiceClaims`'e beklenen servis adı **parametre**; workflow dışı iki çağrı `Diten.MDM`'de kalır | D4 |
| L4 | `…/Security/ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs` | `IsValid`'deki ordinal `"Diten.MDM"` şartı | D5 |
| L5 | `…/Diten.Platform.API/appsettings*.json` | yeni allowlist girdileri | D7, D8, D10 |
| L6 | AuthService config | `ServiceClientRegistry:Clients[]` girdisi | D9, D10 |

**Protected path uyarısı:** `.antigravity/**` ve sealed kayıtlar bu ledger'ın dışındadır
ve hiçbir koşulda değiştirilmez.

### Test matrisi — **plan, koşturulmadı**

| # | senaryo | beklenen |
|---|---|---|
| T1 | Treasury token, allowlist girdisi var, doğru object/template | start kabul |
| T2 | Treasury token, allowlist girdisi **yok** | **403** `WORKFLOW_TRUSTED_START_FORBIDDEN` |
| T3 | Treasury token, girdideki `ObjectType` farklı | **403** `WORKFLOW_TRUSTED_START_FORBIDDEN` |
| T4 | Treasury token, `aud = TRUSTED_AUDIT_SOURCE_INGEST` | **401** — şema audience'ı reddeder |
| T5 | Treasury token + `X-Tenant-Id` header | **403** `WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN` |
| T6 | delegated user tenant ≠ servis tenant | **403** `WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH` |
| T7 | delegated user `platform.workflow.instances.start` izni yok | **403** `WORKFLOW_DELEGATED_USER_FORBIDDEN` |
| T8 | token ömrü ≠ 300 sn veya `iat != nbf` | **401** |
| T9 | claim seti fazla/eksik (tek fazladan claim) | **401** |
| T10 | config'de tanınmayan `ServiceName` | **Auth başlangıçta durur**, entry index + alan adı raporlanır, **değer raporlanmaz** |
| T11 | MDM kimliği hâlâ çalışıyor (regresyon) | mevcut MDM akışları değişmez |

### Negatif / fail-closed kabul kriterleri

- **N1 · Sabotaj kanıtı.** L3'teki parametreleştirme geri alındığında T4 kırmızıya döner.
  Sabotaj kanıtı olmayan guard, guard sayılmaz.
- **N2 · Audience sızıntısı yok.** Treasury kimliğiyle `source-intents/accept` ve
  reference-data yüzeyine erişim **401/403**. Bu, D4'ün kabul kriteridir.
- **N3 · Fail-closed config.** Tek geçersiz allowlist girdisi tüm yapılandırmayı
  reddettirmeye devam eder.
- **N4 · Değer sızıntısı yok.** Hiçbir hata mesajı hash, secret, tenant id veya claim
  değeri yazmaz.
- **N5 · Mevcut kimlik dokunulmazlığı.** Registry mevcut MDM kimliğini değiştirmez.

---

## Tek sonraki kapı

**D1 — Treasury ayrı bir first-party servis istemcisi olarak tanınsın mı?**
Sahip: `api-consumer-credential-owner` + `platform-owner`. Bu karar verilmeden D2–D10'un
hiçbiri anlamlı değildir ve ledger'ın hiçbir satırı açılamaz.

---

*Ölçüm · öneri · owner kararı · CT kabulü ayrımı: §1 ve §4 **ölçüm**; §3'ün sonucu
ölçülen kuraldan **çıkarım**; §5, §7 **öneri**; §6 **karar talebi**. Bu belgede CT kabulü
**yoktur**. Hiçbir test koşulmadı; hiçbir runtime gözlemi yoktur.*
