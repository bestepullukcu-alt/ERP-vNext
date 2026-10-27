# LEGAL-ENTITY-LOOKUP 1.1.0 — MOD-0220 şirket arama / doğrulama sözleşmesi (TASLAK / öneri)

    contract       : LEGAL-ENTITY-LOOKUP
    version        : 1.1.0 (önceki: 1.0.0 — yazılı sözleşme belgesi yoktu; MOD-0220 pack §12 "OD-MDM-le-contract" ve kod)
    x-status       : DRAFT (rev 3, gap listeleri 9–10 uygulandı) — öneri; MOD-0220 pack §22 (TRM-14) CT-APR-02 / CT-APR-01'den
                     geçmeden ve §22 AC-T1…T5 **geçmeden** (çalıştırmak geçmek değildir; AC-T4 tüketici sonuçları sahiplerinin
                     kanıtına bağlı) dondurulmaz; TRM-14 kapanmış sayılmaz
    x-owner        : MOD-0220 Corporate Secretarial / Entity Management (master-data-management, Diten.MdmService)
    x-consumers    : mevcut — §3 tablosu (9 servis + Web, 13 okuma noktası); yeni — Diten.FinanceTreasuryService
                     (MOD-0126, MOD-0119, MOD-0120, MOD-0124)
    x-decision     : D9 (Treasury için başka modülde en küçük eklemeli değişiklik), D10/1 (şirket Active + Approved), D10/2 (bu ek)
                     — docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md
    x-queue        : treasury-intake TRM-14
    x-review       : docs/records/audits/2026-10/treasury-intake-handover-review-01/GAP-LIST-09-TRM14.md
    x-measured-at  : Recovery dal chore/ct/treasury-intake-ct-01 @4cf291b94 (MdmService son değişikliği e87fa5a4 bu dalda; servis kodu origin/main ile aynı)

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

`LegalEntityLookupDto`'ya bir alan eklenir:

| Alan (JSON) | Tip | Değerler | Kaynak |
|---|---|---|---|
| `approvalStatus` | string, boş olmayan | `DRAFT` \| `SUBMITTED` \| `APPROVED` \| `REJECTED` | `LegalEntity.ApprovalStatus.ToString().ToUpperInvariant()` (`lifecycleState` ile aynı biçim) |

Değişmeyenler (sözleşmenin parçası, test edilir):

1. **404 davranışı aynı:** kayıt yok, silinmiş, başka kiracıda ya da `OperationalStatus ≠ Active` → 404, gövde aynı.
   `approvalStatus` 404 kararına **katılmaz**.
2. `referenceable` kuralı aynı (`Active && !IsDeleted`); onay durumu `referenceable`'ı **değiştirmez**.
3. `lifecycleState` aynı (`ACTIVE` referans verilebilir kayıtlar için; `LegalEntityModels.cs:98-99` uyarısı).
4. Mevcut altı alanın **adı, tipi ve anlamı** aynı; hiçbiri kaldırılmaz ya da yeniden adlandırılmaz. **JSON özellik sırası
   sözleşmenin parçası değildir** (gap 9/3); tüketiciler ada göre okur.
5. Yetki aynı (`mdm.legal-entities.read`), rota aynı, Gateway değişmez.
6. Tek eşleme (`ToLookupDto`) kullanıldığı için alan `GET /api/legal-entities/lookup` listesine de eklenir — ayrı DTO
   açılmaz (iki yanıtın ayrışmasını önlemek için).

**Kod boyutu (gap 9/3) — JSON uyumluluğundan ayrıdır:** `LegalEntityLookupDto` konumsal (positional) bir `record`'dur;
yeni üye kurucu imzasını değiştirir. Ölçüm (@4cf291b94): repoda tek oluşturma noktası `LegalEntityMappings.ToLookupDto`
(`LegalEntityModels.cs:238-245`); `services/`, `frontend/`, `tests/` altında başka `new LegalEntityLookupDto(` ya da
hedef tipli `new(…)` ile oluşturma yok; tip MdmService dışına derlenmiş olarak paylaşılmaz (diğer servisler yalnız belge
yorumunda adını anar: `LegalEntityMasterHttpClient.cs:10`, `CompanySelection.cs:82`). Kod yazılırken MdmService build'i
ve bu taramanın yeniden koşulması zorunludur (MOD-0220 pack §22 AC-T5).

## 2. Kimin neyi kontrol ettiği

- MOD-0220 yalnız **bilgiyi** verir; "onaylı değilse reddet" kuralı MOD-0220'de **yoktur**.
- Treasury, para hareketi yaratan / değiştiren her yazmada yanıtı aşağıdaki tabloyla sınıflandırır (D10/1, D14/4):

| MOD-0220 yanıtı | Sınıf | Treasury sonucu |
|---|---|---|
| 200; `data.legalEntityId` istenen kimliğe eşit; `referenceable = true`; `lifecycleState = ACTIVE`; `approvalStatus = APPROVED` | uygun | işlem sürer |
| 200, yukarıdaki gibi tutarlı, `approvalStatus ∈ {DRAFT, SUBMITTED, REJECTED}` | tanımlı, onaysız | 422 `…_LEGAL_ENTITY_NOT_ELIGIBLE` |
| 404 | tanımlı, uygun değil (yok / başka kiracı / aktif değil) | 422 `…_LEGAL_ENTITY_NOT_ELIGIBLE` |
| 200 ama `approvalStatus` yok, `null`, boş ya da bu dört değerin dışında | geçersiz yanıt | 503 `…_LEGAL_ENTITY_UNAVAILABLE` |
| 200 ama `data.legalEntityId` yok ya da istenen kimlikten farklı | geçersiz yanıt | 503 `…_LEGAL_ENTITY_UNAVAILABLE` |
| 200 ama `referenceable = false` ya da `lifecycleState ≠ ACTIVE` (MOD-0220'nin 200 kuralıyla çelişir) | çelişkili yanıt | 503 `…_LEGAL_ENTITY_UNAVAILABLE` |
| 200, gövde ayrıştırılamıyor / `data` yok | geçersiz yanıt | 503 `…_LEGAL_ENTITY_UNAVAILABLE` |
| 401 / 403, 5xx, zaman aşımı, ulaşılamıyor | erişilemiyor | 503 `…_LEGAL_ENTITY_UNAVAILABLE` |

  Kural: **geçersiz ya da çelişkili yanıt 503**, **tanımlı ama uygun olmayan durum 422**; hiçbir belirsiz yanıt "onaylı"
  sayılmaz ve hiçbirinde iş verisi ya da uzak çağrı yazılmaz. Karşılaştırmalar büyük-küçük harfe duyarlıdır
  (MOD-0220 büyük harf üretir).
- **Reconcile muafiyetinin sınırı (gap 9/1; MOD-0126 Runtime Constraints, gap 4/G2, D15/2 ile aynı):** muafiyet
  **yalnız**, Reconcile'ın uzakta **var olan** bir örneğin / geçişin sonucunu yerel niyete kaydettiği yol içindir — yeni
  işlem başlatmaz. Reconcile uzak yazmayı aynı anahtarla **yeniden yapacaksa** (MOD-0023 başlatma / geçiş yeniden
  yazımı) bu yeni bir uzak işlemdir: yukarıdaki tabloyla uygunluk **yeniden kontrol edilir**; uygun değilse ya da
  erişilemiyorsa uzak çağrı yapılmaz, niyet `Unknown` kalır ve sebep kodu kaydedilir.

## 3. Mevcut tüketicilere etki (ölçüldü, @4cf291b94)

Durum sütunu: **beklenen uyumluluk, test edilmedi** — okuma biçimine göre ek alan sorun çıkarmamalı; ama hiçbir tüketici
testi henüz `approvalStatus` içeren bir gövdeyle koşulmadı (gap 9/4). Tüketici testi her servisin kendi sahibinin
işidir (XMC-001) ve takip edilir (MOD-0220 pack §22 AC-T4, §22.7).

| Tüketici (uç) | Okuma biçimi | Mevcut test | Durum |
|---|---|---|---|
| CRM `MdmCyclePeriodLegalEntityValidator.cs:98-107` (lookup-validation) | tipli `ReadFromJsonAsync<GatewayEnvelope<LegalEntityLookup>>` | yok | beklenen, test edilmedi |
| CRM `MdmCyclePeriodLegalEntityCatalog.cs:47, 72` (`/lookup`) | tipli `ReadFromJsonAsync<GatewayEnvelope<List<LegalEntityLookupItem>>>` | yok | beklenen, test edilmedi |
| SupplyChain `MdmLegalEntityScopeValidator.cs:51-58` | yalnız HTTP durum kodu | `LegalEntityScopeValidatorTests` | beklenen (gövde okunmuyor), test edilmedi |
| Manufacturing `MdmLegalEntityReferenceValidator.cs:84` | tipli `ReadFromJsonAsync<Envelope>` | `MdmLegalEntityReferenceValidatorTests` | beklenen, test edilmedi |
| Planning `MdmLegalEntityScopeReader.cs:23-29` | `JsonDocument`, `TryGetProperty` | `MrpUpstreamReaderTests` | beklenen, test edilmedi |
| Planning `LiveHistoryImportReferences.cs:117-138` | `JsonDocument`, adla okuma | `LiveHistoryImportReferencesTests` | beklenen, test edilmedi |
| Inventory `LegalEntityMasterHttpClient.cs:59-60` | `JsonDocument`, `TryGetProperty` | `UpstreamDisplayReadsTests` | beklenen, test edilmedi |
| Platform `MdmLegalEntityReferenceValidator.cs:106, 120` | tipli `ReadFromJsonAsync<MdmResponse<LegalEntityReferenceDto>>` | `MdmLegalEntityReferenceValidatorTests` (Platform) | beklenen, test edilmedi |
| Platform `WorkingCalendarLegalEntityValidator.cs:50, 72` | tipli `ReadFromJsonAsync<GatewayEnvelope<LegalEntityLookup>>` | `WorkingCalendarLegalEntityValidatorTests` | beklenen, test edilmedi |
| HCM `GatewayReferenceValidationClient.cs:42, 148-176` | `JsonDocument`, `TryGetProperty` | `GatewayReferenceValidationClientTests` | beklenen, test edilmedi |
| SafetyStock `MdmLegalEntityReferenceValidator.cs:27-34` | `JsonDocument` | `MdmReferenceValidatorTests` | beklenen, test edilmedi |
| Web `CompanySelection.cs:66, 82` (`/lookup`) | gövde ayrıştırma | `CompanySelectionTests` | beklenen, test edilmedi |
| Web `OrganizationUnitsController.cs:74` (`/lookup`) | doğrudan vekil (gövde değişmeden tarayıcıya) | — | tarayıcı JS'i ek alanı görür; beklenen, test edilmedi |

Tipli okumalar `System.Text.Json` varsayılanına dayanır (bilinmeyen üye yok sayılır); repoda
`JsonUnmappedMemberHandling.Disallow` yalnız CRM istek modellerinde (`ContentEngagementJourneyRequests.cs`) var, bu
yanıtı okuyan bir tipte yok. Bu bir **beklentidir, kanıt değildir**.

## 4. Örnek (200)

Örnek, sözleşme testinde (MOD-0220 pack §22 AC-T3) olduğu gibi kullanılabilir:

```json
{ "data": { "legalEntityId": "6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7", "code": "GMG-CH",
            "legalName": "GMG Example AG", "displayName": null,
            "lifecycleState": "ACTIVE", "referenceable": true, "approvalStatus": "APPROVED" } }
```

## 5. Sürüm kuralı

- 1.1.0 yalnız ekleme; 1.0.0 tüketicileri değişmeden çalışması beklenir (§3, test edilmedi).
- Bundan sonraki her değişiklik de yalnız ekleme olur; 404 kararını genişletmek (ör. onaysızı 404 yapmak) **büyük sürüm**
  sayılır ve bu sözleşmenin kapsamı dışındadır.

## 6. Değişiklik geçmişi

- 1.1.0 rev 3 (2026-10-25) — gap listesi 10: kapanış koşulu "AC'ler geçmiş, AC-T4 sahip kanıtına bağlı"; MOD-0126 AC-39
  kayıt yasağı yalnız reddedilen / geçersiz yanıt vakalarına daraltıldı.
- 1.1.0 rev 2 (2026-10-25) — gap listesi 9: Reconcile muafiyeti yalnız var olan sonucu kaydetmeyle sınırlandı; geçersiz /
  çelişkili yanıt (503) ve tanımlı onaysız durum (422) tablosu; JSON ve kod uyumluluğu ayrıldı, özellik sırası şart değil,
  DTO oluşturma noktaları ölçüldü; tüketici taraması 7'den 13 noktaya genişletildi ve hepsi "beklenen uyumluluk, test
  edilmedi" olarak işaretlendi; örnekte tam UUID.
- 1.1.0 (2026-10-25) — ilk yazılı sürüm; `approvalStatus` eklendi (TRM-14, D10/2).
