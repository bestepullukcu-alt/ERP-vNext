# MOD-0029-FU23 — Electronic Signature Integration Foundation

**Tarih:** 2026-07-21
**Branch:** `feature/crm-integration` (commit/push yapılmadı; tüm değişiklikler working tree'de)
**SOP:** GMG-QMS-SOP-0001 §11, §11.2
**Verdict:** PASS

---

## 1. Initial audit özeti

| # | İnceleme | Bulgu |
|---|---|---|
| 1 | FU09 approval evidence | `DocumentApprovalEvidence` — append-only, `EvidenceReference` string, segregation sonucu. Repo'da **`GetByIdAsync` yok** (yalnız `GetByRegisterEntry`/`GetByRequirement`). |
| 2 | FU10 release gate evidence | `DocumentReleaseGateEvidence` — zorunlu `EvidenceReference`, `VerifiedByUserId`. Repo'da **`GetByIdAsync` yok**. |
| 3 | FU11 training | `DocumentTrainingAssignment` — completion + effectiveness evidence aynı aggregate'te. `GetByIdAsync` var. |
| 4 | FU21 GDocP | `DocumentGDocPCorrectionRecord` (`GetByIdAsync` var) + `DocumentGDocPCorrectionReview` (**yalnız `GetByCorrectionAsync`**). |
| 5 | FU22 QE/Deviation/CAPA | Üçü de `GetByIdAsync` içeriyor; `ExternalQualitySystemReference` seam mevcut. |
| 6 | FU16 repository boundary | `RepositoryType {ValidatedDms, ApprovedInterimRepository, SeparateApprovalMechanism, UnapprovedRepository}`; evaluator zaten `CanSupportRegulatedESignature` ve `NativeESignatureMisuseRisk` finding'i üretiyor. FU23 bunu **okur, değiştirmez**. |
| 7 | Mevcut e-signature modeli | **Yok.** Yalnızca 20+ dosyada "no e-signature" boundary yorumu. Signature/approval-token/password re-auth modeli bulunamadı. |
| 8 | User identity | `ICurrentUserContext {UserId, Email, DisplayName, ActorName, IsAuthenticated}`. **2FA / re-auth context alanı yok.** |
| 9 | Audit pattern | `IAuditableCommand` + `AuditRequestMetadata` + `SourceModule` + `CorrelationId`; reason code sabitleri feature başına `*ReasonCodes` sınıfı. |
| 10 | FU15 RetentionSubjectType | 0–40 dolu (`Other = 27` sabit kalmış, sonrası append). 41+ **boş → güvenli append**. |
| 11 | Mevcut testler | Evidence-only davranış bekliyor; signature ile ilgili hiçbir assertion yok → additive ekleme güvenli. |

**Audit sonucu:** FU23 tamamen sidecar olarak kurulabilir. Mevcut FU repository interface'lerinin **hiçbirine metot eklenmedi** (eklemek tüm mevcut test fake'lerini kırardı).

---

## 2. Scope

Kurulan: document-control scoped **electronic signature foundation**.
Kurulmayan: qualified e-signature, provider entegrasyonu, certificate validation, compliance claim.

---

## 3. Domain modeli

4 yeni sidecar aggregate (`TenantScopedEntity`, append-only, hard-delete yok):

| Aggregate | Collection | Rol |
|---|---|---|
| `DocumentSignaturePolicy` | `document_management_signature_policies` | Subject type + meaning başına kurallar |
| `DocumentSignatureRequest` | `document_management_signature_requests` | Kimden, hangi anlamla, ne zamana kadar imza isteniyor |
| `DocumentSignatureRecord` | `document_management_signature_records` | İmzanın kendisi — append-only attestation |
| `DocumentSignedObjectFingerprint` | `document_management_signed_object_fingerprints` | İmzalanan nesnenin o andaki canonical metadata projeksiyonu |

Yeni enum dosyası: `ElectronicSignatureEnums.cs` (`SignableSubjectType`, `SignatureMeaning`, `SignaturePolicyStatus`, `SignatureRequestStatus`, `SignatureMethod`, `SignatureStatus`, `SignatureValidationResult`, `SignatureFingerprintAlgorithm`, `SignatureVerificationOutcome`). Mevcut hiçbir enum dosyası düzenlenmedi.

---

## 4. Signed object binding (özün özü)

`DocumentSignableSubjectResolver` her subject'i **governance metadata projeksiyonuna** indirger → key-sorted canonical JSON → SHA-256.

- Projeksiyona giren: status, tarihler, identifier'lar, kararlar, evidence **referansları**.
- Projeksiyona **asla girmeyen**: doküman byte'ları, dosya içeriği, ek. FU23 content okumaz.
- Fingerprint **çözümlenmiş subject'ten** üretilir, caller input'undan değil → caller ne imzaladığını beyan edemez.

**Çözümlenen 15 subject type:** ApprovalEvidence*, ReleaseGateEvidence*, TrainingAssignment, TrainingEffectiveness, GDocPCorrectionRecord, QualityEvent, Deviation, CAPAAction, RepositoryAssessment, LegalHold, DispositionRequest, TemporaryControlledIssue, ControlledCopyWithdrawal, ExternalImpactAssessment, DocumentMasterRegisterEntry.

`*` = repo'da `GetByIdAsync` olmadığı için **`RegisterEntryId` zorunlu** (`SIGNATURE_SUBJECT_REQUIRES_REGISTER_ENTRY_ID`). Mevcut FU09/FU10 interface'lerini genişletmemek için bilinçli tercih.

---

## 5. Ürün kararları (raporlanması istenenler)

| Konu | Karar | Gerekçe |
|---|---|---|
| **Second factor** | Policy `RequiresSecondFactor=true` ise imza **501 + `SECOND_FACTOR_NOT_AVAILABLE` ile bloklanır**. Client'ın `SecondFactorPerformed` claim'i hiçbir yerde kabul edilmez; alan her zaman `false`. | Platformda 2FA authentication context yok. Uydurma bir 2FA kaydı, eksik özellikten **daha kötü** delil üretir. |
| **Re-authentication** | Yalnızca `AuthenticationContextReference` varsa `ReAuthenticationPerformed=true`. Yoksa policy talep ediyorsa 400. | Aynı gerekçe — client-asserted flag delil uydurmaktır. |
| **Duplicate signature** | Aynı subject + meaning + fingerprint + signer + Valid → **mevcut kayıt döner (200)**, ikinci kayıt yazılmaz. | Idempotent retry yaygın; aynı nesne durumu için iki özdeş geçerli imza ek delil değil, invalidation'ın kovalaması gereken gürültüdür. |
| **UnapprovedRepository** | **Blok** (`UNAPPROVED_REPOSITORY_BLOCKS_REGULATED_SIGNATURE`), uyarı değil. | Değerlendirilmemiş bir repository, güvenilmemesi gereken delil üretir. |
| **Repository assessment yok** | Policy talep etmiyorsa imza yazılır ama boundary statement "**boundary UNKNOWN**" der. Policy talep ediyorsa blok. | Her assessment'sız imzayı bloklamak kullanıcıyı hiç kayıt tutmamaya iter; attribution delili tamamen kaybolur. |
| **Çözümlenemeyen subject** | Blok (`SIGNATURE_SUBJECT_NOT_RESOLVABLE`). | Okuyamadığımız nesne üzerinde fingerprint anlamsızdır. |
| **Verify + subject unresolvable** | `RequiresResign` — asla `Valid`. | Fail-closed. |

---

## 6. Repository boundary davranışı

`DocumentSignatureBoundaryEvaluator` **her zaman** bir statement üretir ve bunu signature record'a **kalıcı yazar** (dokümantasyonda kalmaz, delille birlikte seyahat eder):

- `ValidatedDms` → "repository validated DMS olarak **değerlendirilmiş**, ancak FU23 provider/certificate validation yapmaz; bu imza kaydedildi, doğrulanmadı."
- `ApprovedInterimRepository` → "validated DMS olarak **sunulamaz**; native approval yeteneği regulated e-signature **değildir**."
- `SeparateApprovalMechanism` → "onay ayrı mekanizmada; bu kayıt o mekanizmanın delilini **referanslar**."
- `UnapprovedRepository` → blok.
- Assessment yok → "boundary UNKNOWN; validated DMS ve regulated e-signature claim'ine izin verilmez."

Tenant'ın policy metni statement'a **eklenir, yerine geçmez**.

---

## 7. FU09/FU10/FU11/FU21/FU22 ilişkisi

Hiçbir mevcut davranış yeniden yazılmadı. **İmzalama subject'i mutate etmez** — approval evidence imzalamak hiçbir şeyi onaylamaz. FU23 paralel bir attestation katmanıdır. Mevcut string evidence referansları kaldırılmadı, migrate edilmedi.

---

## 8. FU15 retention entegrasyonu

`RetentionSubjectType` additive genişletildi: `41 DocumentSignaturePolicy`, `42 DocumentSignatureRequest`, `43 DocumentSignatureRecord`, `44 DocumentSignedObjectFingerprint`. **Mevcut ordinal'lerin hiçbiri kaymadı** (test ile assert edildi: `Other=27`, `GDocPCorrectionRecord=34`, `DocumentQualityEventSourceLink=40`).

---

## 9. API

`DocumentManagementSignaturesController` — 16 endpoint, `api/v1/document-management` altında:
`signature-policies` (list/detail/create/activate/retire), `signature-requests` (list/detail/create/cancel/reject), `signatures` (list/detail/by-subject/fingerprints/sign/verify/invalidate).

**DELETE verb yok.** TenantId client'tan okunmaz. RBAC mevcut `ControlledDocumentsView/Create` key'lerini reuse eder.

### Önerilen (seed EDİLMEDİ — AuthService'e dokunulmadı)
```
platform.document-management.signatures.view
platform.document-management.signatures.request
platform.document-management.signatures.sign
platform.document-management.signatures.verify
platform.document-management.signatures.invalidate
platform.document-management.signature-policies.manage
```

---

## 10. Build / test

```
dotnet build ...Diten.Platform.API.csproj -c Debug --no-restore   → Başarılı, 0 Uyarı, 0 Hata
dotnet test  ...Diten.Platform.Application.Tests.csproj            → 1796/1796 Başarılı, 0 Başarısız
  └─ DocumentElectronicSignatureTests                             → 38/38 Başarılı
dotnet test  ...Diten.Platform.Eventing.Tests.csproj               → 56 Başarılı, 3 Atlanan
```

Regresyon yok: FU06–FU22, ControlledDocument, TemplateMaster/Variant testlerinin tamamı yeşil.

### Guardrail grep (FU23 dosyaları üzerinde)
| Kontrol | Sonuç |
|---|---|
| Direct port 5057 / `X-Tenant-Id` | CLEAN |
| Raw bytes / IFormFile / GridFS | CLEAN |
| Destructive delete / purge | CLEAN (tek eşleşme: yorumdaki "drops to RequiresResign") |
| External provider HTTP/API | CLEAN |
| Certificate validation (X509/RSA/PAdES/XAdES) | CLEAN (yalnız **olumsuzlama** metinleri) |
| Compliance claim | CLEAN (yalnız olumsuzlama) |
| MOD-0023 workflow runtime | CLEAN |
| AuthService / Gateway / Frontend | FU23 tarafından **dokunulmadı** (mevcut değişiklikler oturum öncesinden) |
| MOD-0028 mutation | Yok |

---

## 11. Remaining gaps

1. **`GDocPCorrectionReview` subject resolver yok** — repo'da `GetByIdAsync` yok, yalnız `GetByCorrectionAsync`. İmzalama fail-closed olarak bloklanıyor. Çözüm: FU21 repo'suna additive `GetByIdAsync` + tüm fake'lerin güncellenmesi (ayrı task).
2. **`ApprovalEvidence` / `ReleaseGateEvidence` için `RegisterEntryId` zorunlu** — aynı kök neden.
3. **Second factor implement edilmedi** — authentication context gerektirir; şu an blok davranışı.
4. **Re-auth gerçek password prompt değil** — yalnızca opak `AuthenticationContextReference`.
5. **RBAC key'leri seed edilmedi** — controlled-documents key'leri reuse ediliyor.
6. **Frontend UI yok** — task kapsamında zorunlu değildi.
7. **Provider port boş** — `ExternalProviderReference` + `ValidationResult.ValidatedByProvider` seam olarak duruyor, hiçbir kod yolu bunları set etmiyor.
8. **`SignatureRequestStatus.Expired` otomatik geçmiyor** — scheduler yok; `IsOverdue` hesaplanan alan olarak dönüyor.

---

## 12. Doğrulamalar

- ✅ External e-signature provider entegrasyonu **yok**
- ✅ Certificate chain validation **yok**
- ✅ 21 CFR Part 11 / Annex 11 compliance claim **yok**
- ✅ Validated DMS claim **yok**; interim repository validated DMS gibi sunulmuyor
- ✅ MOD-0023 workflow entegrasyonu **yok**
- ✅ AuthService seed / Gateway ocelot değişikliği **yok**
- ✅ MOD-0028 baseline lifecycle mutation **yok**
- ✅ Hard delete / purge **yok**; signature record append-only
- ✅ `SignedAt` yalnızca server-side (`DateTimeOffset.UtcNow`) — backdating kod yolu yok
- ✅ Mevcut evidence kayıtları silinmedi/değiştirilmedi; string referanslar kaldırılmadı

**Final verdict: PASS**
