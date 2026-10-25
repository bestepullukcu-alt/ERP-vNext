# LEGAL-ENTITY-LOOKUP 1.1.0 — MOD-0220 şirket arama / doğrulama sözleşmesi (TASLAK / öneri)

    contract       : LEGAL-ENTITY-LOOKUP
    version        : 1.1.0 (önceki: 1.0.0 — yazılı sözleşme belgesi yoktu; MOD-0220 pack §12 "OD-MDM-le-contract" ve kod)
    x-status       : DRAFT — öneri; MOD-0220 pack §22 (TRM-14) CT-APR-02 / CT-APR-01'den geçmeden dondurulmaz, kod yazılmaz
    x-owner        : MOD-0220 Corporate Secretarial / Entity Management (master-data-management, Diten.MdmService)
    x-consumers    : mevcut — Diten.CrmService, Diten.SupplyChainService, Diten.ManufacturingService, Diten.PlanningService (2 okuyucu),
                     Diten.Web (`/lookup` listesi); yeni — Diten.FinanceTreasuryService (MOD-0126, MOD-0119, MOD-0120, MOD-0124)
    x-decision     : D9 (Treasury için başka modülde en küçük eklemeli değişiklik), D10/1 (şirket Active + Approved), D10/2 (bu ek)
                     — docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md
    x-queue        : treasury-intake TRM-14
    x-measured-at  : Recovery dal chore/ct/treasury-intake-ct-01 @03fb1257c (MdmService son değişikliği e87fa5a4 bu dalda; servis kodu origin/main ile aynı)

Ölçülen kaynaklar (satır numaraları numaralı okumadan):

- Uç: `GET /api/legal-entities/{legalEntityId:guid}/lookup-validation`, izin `mdm.legal-entities.read` —
  `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48-54`.
- İşleyici: `ValidateLegalEntityReferenceHandler.cs:19-25` (sha256 `6bbee405…d88f`) — kayıt yoksa ya da
  `OperationalStatus ≠ Active` ise 404 `"Legal Entity is not referenceable."`; yoksa `ToLookupDto`.
- Yanıt: `LegalEntityLookupDto` (`LegalEntityModels.cs:100-106`, sha256 `b14d36b6…4c20`) — `LegalEntityId`, `Code`,
  `LegalName`, `DisplayName`, `LifecycleState` (`OperationalStatus` büyük harf), `Referenceable`; eşleme `:238-245`.
  Aynı DTO `GET /api/legal-entities/lookup` listesinde de döner (`GetLegalEntitiesLookupHandler.cs:21-22`).
- Durumlar: `LegalEntityOperationalStatus` Draft 1, InReview 2, Approved 3, Active 4, Suspended 5, Archived 6;
  `LegalEntityApprovalStatus` Draft 1, Submitted 2, Approved 3, Rejected 4; entity alanları `LegalEntity.cs:49-50`,
  `IsReferenceable = OperationalStatus == Active && !IsDeleted` (`:60`).
- Kiracı: depo her sorguda `TenantFilter` ve `IsDeleted = false` uygular (`LegalEntityRepository.cs:22, 41-43`);
  başka kiracının kaydı 404.

## 1. Değişiklik (yalnız ekleme)

`LegalEntityLookupDto`'ya **son alan** olarak eklenir:

| Alan (JSON) | Tip | Değerler | Kaynak |
|---|---|---|---|
| `approvalStatus` | string | `DRAFT` \| `SUBMITTED` \| `APPROVED` \| `REJECTED` | `LegalEntity.ApprovalStatus.ToString().ToUpperInvariant()` (`lifecycleState` ile aynı biçim) |

Değişmeyenler (sözleşmenin parçası, test edilir):

1. **404 davranışı aynı:** kayıt yok, silinmiş, başka kiracıda ya da `OperationalStatus ≠ Active` → 404, gövde aynı.
   `approvalStatus` 404 kararına **katılmaz**.
2. `referenceable` kuralı aynı (`Active && !IsDeleted`); onay durumu `referenceable`'ı **değiştirmez**.
3. `lifecycleState` aynı (`ACTIVE` referans verilebilir kayıtlar için; `LegalEntityModels.cs:98-99` uyarısı).
4. Alan adları, sıraları ve tipleri aynı; mevcut alan kaldırılmaz, yeniden adlandırılmaz.
5. Yetki aynı (`mdm.legal-entities.read`), rota aynı, Gateway değişmez.
6. Tek eşleme (`ToLookupDto`) kullanıldığı için alan `GET /api/legal-entities/lookup` listesine de eklenir — ayrı DTO
   açılmaz (iki yanıtın ayrışmasını önlemek için; liste tüketicileri için de eklemeli).

## 2. Kimin neyi kontrol ettiği

- MOD-0220 yalnız **bilgiyi** verir; "onaylı değilse reddet" kuralı MOD-0220'de **yoktur**.
- Treasury (D10/1, D14/4): para hareketi yaratan / değiştiren yazmalarda `lookup-validation` 200 **ve**
  `approvalStatus = APPROVED` ister; değilse 422 `…_LEGAL_ENTITY_NOT_ELIGIBLE`; 404 → aynı 422; 5xx / zaman aşımı /
  ulaşılamıyor → 503 `…_LEGAL_ENTITY_UNAVAILABLE` (geçiş yok). Reconcile muaf (D15/2).
- `approvalStatus` alanı **yoksa** (eski MdmService) Treasury bunu "onaylı" saymaz: 503 `…_LEGAL_ENTITY_UNAVAILABLE`
  (kapalı-güvenli; sessizce geçmez).

## 3. Mevcut tüketicilere etki (ölçüldü, @03fb1257c)

| Tüketici | Yanıtı nasıl okuyor | Ek alanın etkisi |
|---|---|---|
| `Diten.CrmService` `MdmCyclePeriodLegalEntityValidator.cs:98-107` | tipli `ReadFromJsonAsync<GatewayEnvelope<LegalEntityLookup>>`, `Referenceable` | yok (bilinmeyen alan yok sayılır) |
| `Diten.SupplyChainService` `MdmLegalEntityScopeValidator.cs:51-58` | yalnız HTTP durum kodu | yok |
| `Diten.ManufacturingService` `MdmLegalEntityReferenceValidator.cs:84` | tipli `ReadFromJsonAsync<Envelope>` | yok |
| `Diten.PlanningService` `MdmLegalEntityScopeReader.cs:23-29` | `JsonDocument`, `TryGetProperty` | yok |
| `Diten.PlanningService` `LiveHistoryImportReferences.cs:117-138` | `JsonDocument`, adla okuma | yok |
| `Diten.Web` `CompanySelection.cs:66`, `OrganizationUnitsController.cs:74` (`/lookup`) | gövde ayrıştırma / doğrudan vekil | yok bekleniyor — kod yazılırken `CompanySelectionTests` ile doğrulanır (MOD-0220 pack §22 AC-T4) |

CRM ayrıştırıcısının testi yok (ölçüldü); dayanıklılık `System.Text.Json` varsayılanına dayanır — takip maddesi.

Repoda `JsonUnmappedMemberHandling.Disallow` yalnız CRM istek modellerinde var (`ContentEngagementJourneyRequests.cs`),
bu yanıtı okuyan bir tipte yok.

## 4. Örnek (200)

```json
{ "data": { "legalEntityId": "6f1c…", "code": "GMG-CH", "legalName": "…", "displayName": null,
            "lifecycleState": "ACTIVE", "referenceable": true, "approvalStatus": "APPROVED" } }
```

## 5. Sürüm kuralı

- 1.1.0 yalnız ekleme; 1.0.0 tüketicileri değişmeden çalışır.
- Bundan sonraki her değişiklik de yalnız ekleme olur; 404 kararını genişletmek (ör. onaysızı 404 yapmak) **büyük sürüm**
  sayılır ve bu sözleşmenin kapsamı dışındadır.

## 6. Değişiklik geçmişi

- 1.1.0 (2026-10-25) — ilk yazılı sürüm; `approvalStatus` eklendi (TRM-14, D10/2).
