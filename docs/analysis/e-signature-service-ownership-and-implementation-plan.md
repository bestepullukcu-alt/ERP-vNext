# MOD-0022 e-Signature Service

## Mimari Konumlandırma ve Yapılma Planı

**Tarih:** 15 Haziran 2026  
**Modül:** `MOD-0022 — e-Signature Service`  
**Domain:** Platform & Shared Services  
**Önerilen servis sahibi:** `Diten.Platform`  
**Doküman türü:** Kod üretimi öncesi mimari karar ve uygulama planı

## 1. Kaynak Sınırı

Bu analiz yalnızca aşağıdaki kaynaklar dikkate alınarak hazırlanmıştır:

- `AGENTS.md`
- `PROMPT-GUIDE .md`
- `agent-usage-guide.md`
- `Project ongoing status report 31 May 2026 (1).xlsx`
- `System Capability & Implementation Blueprint - master 5.xlsx`

Mevcut ERP-vNext kaynak kodu, mevcut module pack'ler ve daha önce oluşturulmuş analiz dokümanları bu kararın kaynağı olarak kullanılmamıştır.

## 2. Kesin Mimari Karar

e-Signature Service, tenant domainlerinden birinin içinde geliştirilmemelidir. Modülün sahibi:

```text
Platform & Shared Services
└── MOD-0022 e-Signature Service
    └── Platform Control Service
        └── Diten.Platform
```

Servis platform tarafından sahiplenilecek, ancak imza kayıtları tenant-scoped tutulacaktır.

Doğru mimari model:

> Platform-owned servis + tenant-scoped veri + tenant-facing kullanım ekranları

Ayrı bir tenant e-Signature mikroservisi oluşturulmayacaktır. Kaynaklarda bağımsız bir `Diten.ESignatureService` deployment unit kararı bulunmadığından ilk uygulama `Diten.Platform` içinde yapılmalıdır.

## 3. Kararın Dayanağı

Blueprint içinde MOD-0022 için aşağıdaki kararlar açıkça verilmiştir:

| Başlık | Karar |
|---|---|
| Domain | `1) Platform & Shared Services` |
| Suite | `Identity, Access & Trust` |
| Capability Group | `Controls & Non-repudiation` |
| Placement | `Platform Control Service` |
| Deployment Unit | `Platform Backbone` |
| Build/Buy/Partner | `Build` |
| SLO | `Tier 1` |
| SoR | Signature envelopes, signer attestations, verification artifacts |
| Operasyon sahibi | Platform Ops |
| Teknik sahibi | Platform Engineering |
| Minimum contract | `ESIGN-BUNDLE` |

Proje durum raporunda MOD-0022 ayrıca:

- `Backbone / Approval Control`
- Supply Chain ve Quality için ortak foundation
- Controlled document lifecycle ve records retention ile birlikte yatay omurga

olarak konumlandırılmıştır.

Bu nedenle modül tek bir tenant iş alanına ait değildir. Birden fazla domain tarafından tüketilecek ortak bir non-repudiation ve imza kabiliyetidir.

## 4. Ownership Sınırları

### 4.1 e-Signature Service'in Sahip Olacağı Nesneler

MOD-0022 aşağıdaki nesnelerin System of Record sahibi olacaktır:

- Signature envelope
- Signature request
- Signer identity binding
- Signature participant
- Signer attestation
- Signature status
- Provider request ve provider reference
- Signature verification artifact
- Signed artifact reference
- Evidence link
- Provider callback işleme durumu
- Signature audit export
- Reconciliation sonucu

### 4.2 Tenant Domainlerinin Sahip Olacağı Nesneler

Tenant/domain modülleri kendi iş nesnelerinin sahibi olmaya devam edecektir:

- Sözleşmeler
- Satın alma ve ödeme onayları
- Kalite onayları
- CoA kayıtları
- CAPA kayıtları
- Batch record
- Regulatory submission
- Labeling change
- Policy attestation
- Kontrollü iş dokümanları

Tenant domaini imza envelope verisini kendi içinde yeniden modellemeyecektir. İlgili iş nesnesinde yalnızca şu referanslardan gerekli olanları tutacaktır:

- `SignatureEnvelopeId`
- `SignatureStatus`
- `SignedArtifactId`
- `VerificationArtifactId`

### 4.3 Diğer Servislerle Sorumluluk Ayrımı

| Nesne veya davranış | SoR sahibi |
|---|---|
| İş nesnesi | İlgili tenant/domain modülü |
| Kontrollü doküman ve versiyonu | Controlled Documents |
| Approval workflow | Workflow Designer |
| Signature envelope | e-Signature Service |
| Signer attestation | e-Signature Service |
| Verification artifact | e-Signature Service |
| Audit event | Audit Trail Service |
| Signed artifact/evidence | Document/Evidence servisi |
| Retention ve legal hold | Records Management |
| Harici imza işlemi | DocuSign/Adobe Sign |

e-Signature Service, imzalanan iş dokümanını kendi ana verisine dönüştürmeyecektir. İş nesnesi ve immutable document version referanslarıyla çalışacaktır.

## 5. Hedef Domain Modeli

### 5.1 SignatureEnvelope

Ana imza işlem kaydıdır.

Zorunlu alanlar:

- `Id`
- `TenantId`
- `EnvelopeNumber`
- `SubjectType`
- `SubjectId`
- `SubjectVersion`
- `DocumentArtifactId`
- `ProviderType`
- `ProviderEnvelopeId`
- `Status`
- `RequestedBy`
- `RequestedAt`
- `ExpiresAt`
- `CompletedAt`
- `CorrelationId`
- `IsDeleted`
- `DeletedAt`
- Audit alanları

Durumlar:

- `Draft`
- `Pending`
- `PartiallySigned`
- `Completed`
- `Declined`
- `Expired`
- `Cancelled`
- `Failed`

### 5.2 SignatureParticipant

Envelope içindeki imzalayan tarafı ve imza sırasını temsil eder.

Zorunlu alanlar:

- `Id`
- `TenantId`
- `EnvelopeId`
- `SignerUserId`
- `SignerEmail`
- `SignerDisplayName`
- `SigningOrder`
- `Role`
- `Status`
- `RequestedAt`
- `ViewedAt`
- `SignedAt`
- `DeclinedAt`
- `DeclineReason`
- `ProviderRecipientId`
- `IdentityVerificationMethod`

### 5.3 SignerAttestation

İmzalayan kişinin beyanını, imza anlamını ve kimlik snapshot'ını saklar.

Zorunlu alanlar:

- `Id`
- `TenantId`
- `EnvelopeId`
- `ParticipantId`
- `AttestationType`
- `AttestationText`
- `Meaning`
- `SignedAt`
- `SignerIdentitySnapshot`
- `IpAddress`
- `UserAgent`
- `ProviderEvidenceReference`
- `CorrelationId`

### 5.4 SignatureVerificationArtifact

İmzalı belgenin doğrulanmasına ilişkin kanıtı tutar.

Zorunlu alanlar:

- `Id`
- `TenantId`
- `EnvelopeId`
- `SignedArtifactId`
- `ArtifactHash`
- `HashAlgorithm`
- `ProviderCertificateReference`
- `VerificationStatus`
- `VerifiedAt`
- `VerificationDetails`
- `EvidenceLinkId`
- `CorrelationId`

## 6. Tenant İzolasyonu

MOD-0022 tenant-scoped veri üretecektir.

Zorunlu kurallar:

- Tüm tenant kayıtları `TenantId` içerir.
- `TenantId` request body'den kabul edilmez.
- Tenant context JWT ve Gateway üzerinden server-side çözülür.
- Tüm repository sorguları `TenantId + IsDeleted=false` filtresi kullanır.
- Cross-tenant kayıt erişimi `404` döndürür.
- Handler katmanından doğrudan MongoDB collection erişimi yapılmaz.
- Platform Admin cross-tenant işlemleri ayrı permission ve açık target-tenant context gerektirir.
- Provider callback içindeki tenant bilgisi güvenilir kabul edilmez.
- Callback tenant'ı `ProviderEnvelopeId` üzerinden server-side çözülür.
- Tekrarlanan callback'ler idempotent işlenir.

Bu kurallarla cross-tenant read/write tasarım gereği engellenmelidir.

## 7. Provider Mimarisi

Kaynaklarda harici provider olarak DocuSign ve Adobe Sign belirtilmiştir.

Provider bağımsız mimari:

```text
Tenant Domain
    |
    v
e-Signature Application API
    |
    v
ISignatureProvider
    |-- DocuSignProvider
    `-- AdobeSignProvider
```

Application ve Domain katmanları provider-specific payload bilmemelidir.

Provider adapter sorumlulukları:

- Envelope oluşturmak
- Signer eklemek
- Envelope'ı imzaya göndermek
- Provider status sorgulamak
- Callback signature doğrulamak
- Signed artifact almak
- Certificate ve verification evidence almak
- Provider hatalarını canonical hata modeline çevirmek

Provider credential ve secret değerleri:

- Request payload'larında taşınmaz.
- Business collection içinde açık metin tutulmaz.
- Merkezi secret/configuration mekanizmasından okunur.

## 8. API Planı

Önerilen Gateway base path:

```text
/api/platform/e-signatures
```

Tüm frontend çağrıları Gateway portu `5000` üzerinden yapılacaktır. Platform portu `5057` internal kalacaktır. `ocelot.json` değişikliği yalnızca integration-agent tarafından yapılacaktır.

### 8.1 Tenant Operasyonları

- `POST /requests`
- `GET /requests`
- `GET /requests/{id}`
- `POST /requests/{id}/send`
- `POST /requests/{id}/cancel`
- `GET /requests/{id}/participants`
- `GET /requests/{id}/verification`
- `GET /requests/{id}/audit-export`

### 8.2 Signer Operasyonları

- `GET /my-signatures`
- `GET /my-signatures/{id}`
- `POST /my-signatures/{id}/sign`
- `POST /my-signatures/{id}/decline`

### 8.3 Provider Callback

- `POST /providers/{provider}/callbacks`

Callback endpoint'i:

- Provider signature doğrulaması yapar.
- Idempotency key veya provider event id kontrol eder.
- Envelope ve participant durumunu atomik günceller.
- Audit event ve evidence link üretir.

### 8.4 Platform Admin Operasyonları

- `GET /admin/requests`
- `GET /admin/requests/{id}`
- `GET /admin/provider-health`
- `POST /admin/requests/{id}/reconcile`
- `GET /admin/audit-exports`
- `GET /admin/failed-callbacks`

## 9. Yetkilendirme Planı

### 9.1 Tenant Permission'ları

- `ESignature.Request.Create`
- `ESignature.Request.Read`
- `ESignature.Request.Send`
- `ESignature.Request.Cancel`
- `ESignature.Sign`
- `ESignature.Decline`
- `ESignature.Verification.Read`
- `ESignature.AuditExport.Read`

### 9.2 Platform Permission'ları

- `ESignature.Admin.Read`
- `ESignature.Admin.Reconcile`
- `ESignature.Provider.Read`
- `ESignature.Provider.Manage`
- `ESignature.AuditExport.ReadAll`

İmzalama işlemi yalnız RBAC ile yetkilendirilmeyecektir. Kullanıcı:

1. Gerekli permission'a sahip olmalı,
2. Aynı tenant kapsamında bulunmalı,
3. Envelope içinde aktif signer olarak atanmış olmalı,
4. İmza sırası varsa kendi sırasının açılmış olması gerekir.

## 10. UI Yerleşimi

### 10.1 Tenant Shell

Layout:

```text
_LayoutTenantShell.cshtml
```

Planlanan ekranlar:

- My Signature Requests
- Pending My Signatures
- Signature Request Details
- Sign/Decline
- Signature Verification Viewer
- Signed Artifact Viewer
- Signature Trace

Tenant kullanıcıları yalnız kendi tenant kayıtlarını ve kendilerine görünür envelope'ları görebilir.

### 10.2 Platform Admin

Layout:

```text
_LayoutPlatformAdmin.cshtml
```

Planlanan ekranlar:

- Global Signature Request Monitor
- Provider Health
- Reconciliation Queue
- Failed Callback Monitor
- Signature Audit Export
- Provider Configuration

Blueprint'te tanımlanan ana sayfalar korunacaktır:

- Signature Requests
- Signature Verification Viewer
- Signature Audit Export

Bu sayfalar tenant operasyon yüzeyi ve Platform Admin operasyon yüzeyi olarak ayrıştırılacaktır.

## 11. Zorunlu Bağımlılık Kapıları

Blueprint aşağıdaki bağımlılıkları `HARD` olarak tanımlar:

1. `MOD-0021 — Audit Trail Service`
2. `MOD-0029 — Controlled Documents`
3. `MOD-0005 — Policy & Control Library`
4. `MOD-0264 — e-Sign Vendor`

MOD-0022 geliştirmesi başlamadan önce aşağıdaki sorular module pack içinde kapatılmalıdır:

- Audit event contract kullanılabilir durumda mı?
- İmzalanacak immutable document version üretilebiliyor mu?
- Signature policy ve signer rule nereden okunacak?
- Provider credential yönetimi hazır mı?
- Signed artifact hangi serviste saklanacak?
- Evidence link nasıl kurulacak?
- Retention ve legal hold hangi servis tarafından uygulanacak?
- Provider callback güvenlik ve idempotency standardı tanımlı mı?

Hard dependency hazır değilse ilgili özellik module pack içinde production blocker olarak işaretlenmelidir.

## 12. MVP ve Production Ayrımı

Kaynaklarda iki farklı teslimat yaklaşımı bulunuyor:

- Durum raporu MVP substitute olarak signed PDF kullanımına izin veriyor.
- Blueprint production modelinde DocuSign/Adobe Sign provider bağımlılığı tanımlıyor.

### 12.1 MVP Substitute

MVP aşağıdaki kabiliyetlerle sınırlandırılabilir:

- Kontrollü PDF üretimi
- Belge hash'i
- Kullanıcı kimliği ve timestamp bağlama
- Attestation kaydı
- Audit event
- Signed artifact/evidence saklama
- Signature verification ekranı

Bu çıktı hukuki nitelikli elektronik imza olarak adlandırılmayacaktır. Ürün terminolojisinde `Internal Approval Evidence` veya `Signed Approval PDF` kullanılmalıdır.

### 12.2 Production e-Signature

Production kapsamı:

- DocuSign veya Adobe Sign adapter
- Provider identity binding
- Callback signature verification
- Signed artifact alma
- Certificate ve verification evidence
- Signature audit export
- Callback idempotency
- Reconciliation
- Retention/legal hold entegrasyonu
- Provider health ve support ekranları

## 13. Yapılma Planı

### Aşama 1 — Capability ve Module Pack Hazırlığı

1. MOD-0022 için PSS module pack hazırlanır.
2. Module pack `status: draft` olarak oluşturulur.
3. Service değeri `Diten.Platform` olarak yazılır.
4. İki shell açıkça tanımlanır:
   - `platform-admin`
   - `tenant`
5. `ESIGN-BUNDLE` minimum contract içeriği module pack'e eklenir.
6. Owned objects ve SoR sınırları tanımlanır.
7. Dört hard dependency için readiness checklist oluşturulur.
8. MVP substitute ile production provider kapsamı birbirinden ayrılır.
9. Kullanıcı incelemesi sonrası pack `approved` veya `ready-for-dev` yapılır.

Bu yetenek platform backend, tenant UI, admin UI, provider integration, audit ve document/evidence entegrasyonlarını kapsadığı için `/prepare-capability-pack` değerlendirilmelidir. Hazırlık sırasında tek module pack'in yeterli olduğu doğrulanırsa `/prepare-module-pack` akışına dönülmelidir.

### Aşama 2 — Foundation Contract

1. Signature envelope state machine tanımlanır.
2. Signer identity binding kuralları yazılır.
3. Attestation ve verification artifact contract'ları tamamlanır.
4. Audit event tipleri tanımlanır.
5. Evidence ve document reference contract'ları tanımlanır.
6. Provider abstraction oluşturulacak şekilde interface sınırı belirlenir.
7. Callback idempotency ve signature verification kuralları tamamlanır.

### Aşama 3 — Platform Backend

1. Domain entity ve enum'lar uygulanır.
2. MongoDB tenant-scoped repository'leri uygulanır.
3. Unique ve query index'leri hazırlanır.
4. CQRS command/query/handler/validator yapısı kurulur.
5. Tenant, signer, provider callback ve admin endpoint'leri eklenir.
6. `Response<T>` ve `CustomBaseController` standardı uygulanır.
7. Validation, Logging, Exception ve Performance pipeline behavior'ları kullanılır.

### Aşama 4 — Provider Integration

1. `ISignatureProvider` contract'ı uygulanır.
2. Seçilen ilk production provider adapter'ı geliştirilir.
3. Provider callback doğrulaması yapılır.
4. Signed artifact ve verification evidence alınır.
5. Retry, timeout ve reconciliation davranışları eklenir.
6. Provider health kontrolü eklenir.

İlk provider tercihi module pack onayında kesinleştirilmelidir. Kaynaklar DocuSign ve Adobe Sign seçeneklerini desteklemektedir.

### Aşama 5 — Tenant UI

1. Signature request listesi hazırlanır.
2. Pending My Signatures ekranı hazırlanır.
3. Sign/Decline akışı hazırlanır.
4. Verification Viewer hazırlanır.
5. Signed Artifact Viewer hazırlanır.
6. Signature Trace hazırlanır.
7. `_LayoutTenantShell.cshtml` kullanılır.
8. DataTable v2 ve 7 dil localization uygulanır.

### Aşama 6 — Platform Admin UI

1. Global request monitor hazırlanır.
2. Provider health ekranı hazırlanır.
3. Reconciliation queue hazırlanır.
4. Failed callback monitor hazırlanır.
5. Audit export ekranı hazırlanır.
6. `_LayoutPlatformAdmin.cshtml` kullanılır.
7. DataTable v2 ve 7 dil localization uygulanır.

### Aşama 7 — Gateway ve Entegrasyon

1. `/api/platform/e-signatures` route contract'ı doğrulanır.
2. Route değişikliği integration-agent tarafından yapılır.
3. Frontend'in yalnız Gateway `5000` kullandığı doğrulanır.
4. `5057` doğrudan frontend çağrısı bulunmadığı kontrol edilir.
5. Correlation ID servisler arasında taşınır.

### Aşama 8 — Test ve Doğrulama

Zorunlu test alanları:

- Tenant isolation
- Cross-tenant `404`
- Signer assignment kontrolü
- Signing order
- Invalid state transition
- Duplicate callback idempotency
- Callback signature verification
- Provider timeout/retry
- Audit event üretimi
- Evidence link oluşturma
- Signed artifact hash doğrulaması
- RBAC ve participant authorization
- Platform Admin target-tenant güvenliği
- 7 dil localization
- Tenant ve Platform Admin browser smoke testleri

## 14. Teslimat ve Release Kararı

Kaynaklar arasında release zamanlaması açısından fark vardır:

- Blueprint version log, e-Signature modülünü `W-4` seviyesinden `W-2` seviyesine yükseltmiştir.
- Bazı görünüm ve backlog sayfaları halen `W-4` göstermektedir.
- Release Candidates tablosunda modül `Not in release scope` ve candidate `N` durumundadır.

Bu nedenle:

- Mimari ownership kararı kesindir.
- Production release tarihi kesin değildir.
- Module pack hazırlanabilir.
- Kod geliştirme ancak module pack onayı ve release kapsamı kararı sonrası başlatılmalıdır.

## 15. Sonuç

| Karar | Sonuç |
|---|---|
| Domain | Platform & Shared Services |
| Module ID | MOD-0022 |
| Servis | Diten.Platform |
| Veri modeli | Tenant-scoped |
| İş nesnesi sahipliği | İlgili tenant/domain modülü |
| Tenant UI | Request, sign, decline, verify, trace |
| Platform UI | Provider, monitor, reconcile, audit |
| External provider | DocuSign veya Adobe Sign adapter |
| Ayrı tenant servisi | Oluşturulmayacak |
| İlk adım | Capability/module pack hazırlığı |
| Kodlama kapısı | Approved veya ready-for-dev module pack |

