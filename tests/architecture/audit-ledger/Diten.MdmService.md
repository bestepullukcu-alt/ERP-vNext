# Denetim defteri — Diten.MdmService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.MdmService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| mdm-merkezi-iletim | b | işaret | IAuditableCommand+IAuditMetadataProvider |
| mdm-kisaltma-gecmisi | c | yazıcı | IProductAbbreviationHistoryRepository.AppendIfAbsentAsync |
| mdm-guvenilir-kaynak-niyeti | b | yazıcı | IProductLegalEntityScopePolicyRepository.CreateAsync/IProductLegalEntityScopePolicyRepository.ReplaceAsync/IProductLegalEntityScopePolicyRepository.UpdateAsync/IGlobalProductRepository.CreateDraftAsync/IGlobalProductRepository.UpdateDraftAsync/IGlobalProductRepository.RetireIdentityAsync/IGlobalProductRepository.SubmitIdentityAsync/IGlobalProductRepository.AcquireLifecycleOperationAsync/IGlobalProductRepository.WithdrawIdentityApprovalAsync/IGskuRepository.MarkIdentityPendingAsync/IGskuRepository.AcquireCorrectionAsync/IGskuRepository.AcquireRetirementRequestAsync/IProductDefinitionRevisionRepository.MarkIdentityPendingAsync/ILskuRepository.SubmitIdentityAsync/ILskuRepository.AcquireLifecycleOperationAsync/IFirstGskuIdentityWorkflowOperationRepository.ApplyWithdrawalAsync/ILskuIdentityWorkflowOperationRepository.ApplyWithdrawalAsync/IProductAbbreviationRegisterRepository.AppendAuditIntentIfAbsentAsync |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

> **mdm-merkezi-iletim** — `AuditForwardingBehavior` → `IPlatformAuditForwarder` → `POST /api/internal/audit/append`. En iyi çaba: iletim hatası yutulur.
> **mdm-kisaltma-gecmisi** — `mdm_product_abbreviation_history`: aktör, olay, önce/sonra durum, korelasyon; yalnız ekleme; `…/product-abbreviations/{id}/evidence` ile okunur. CT onayı: 2026-10-02.
> **mdm-guvenilir-kaynak-niyeti** — durum değişikliğiyle AYNI koşullu yazımda (tek belge, sürüm + niyet sınırı süzgeci) ya da aynı Mongo işleminde (geri çekmeler) toplanan denetim niyeti (`LocalAuditIntent`, `*AuditIntentFactory`); `AuditIntentDeliveryProcessor` onu Platform'un güvenilir kaynak ucuna iletir, merkezi günlük Platform'dur. Belirteçteki her üye bir YAZMA üyesidir ve niyeti kendi durum değişikliğiyle taşır; `AppendAuditIntentIfAbsentAsync` yalnız `DispatchProductAbbreviationWorkItemAction`'ın tekrar yolunda eksik niyeti tamamlar (durum zaten son hâlinde). CT kararı: 2026-10-07 (WP-MDM-SETTLE-CAP-01).
> **Pencere (🟡):** GP / FirstGsku / LSKU kimlik başlangıcında niyet Platform başlangıcından sonra yazılır; çağrı niyetten önce dönebilir (bekleyen / tekrar / yeniden dene), niyeti kurtarma yazar. Kurtarma işçisi kapalıysa ya da başlangıç kişiye düşerse (takılı) gönderim kaydı o sürede merkezde yoktur; ürün de onay bekleyen duruma geçmemiştir (niyetle aynı yazım), yani kayıtsız bir durum değişikliği oluşmaz — eksik olan "başlatıldı" izidir. Düzeltme / emeklilik başlangıçlarında niyet kilitle aynı yazımda, Platform başlangıcından ÖNCE yazılır (pencere yok).

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|
| RetireGskuIdentityPairCommand | İ5 | Hiçbir kalıcı kaydı değiştirmez: her çağrı 403 DirectRetirementIsSystemOnly döner; GSKU çifti emekliliği onaylı istek akışından (StartGskuRetirementRequestWorkflow) geçer. İşleyici 403 taslağı kaldıkça geçerli; sistem aktörüne açılırsa İ5 geçersiz olur (komut ayrıca Kapsam borcunda: ProductLegalEntityScopeActivationDebt.KnownUnguardedWriters). |
| RetireLskuIdentityCommand | İ5 | Hiçbir kalıcı kaydı değiştirmez: her çağrı 403 DirectRetirementIsSystemOnly döner; LSKU emekliliği onaylı istek akışından (StartLskuRetirementRequestWorkflow) geçer. İşleyici 403 taslağı kaldıkça geçerli; sistem aktörüne açılırsa İ5 geçersiz olur (komut ayrıca Kapsam borcunda: ProductLegalEntityScopeActivationDebt.KnownUnguardedWriters). |

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|
| ApproveProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| ApproveProductAbbreviationRetirementCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| CancelProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| InitiateProductAbbreviationCorrectionCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RejectProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RejectProductAbbreviationRetirementCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RequestProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RequestProductAbbreviationRetirementCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| StartFirstGskuIdentityWorkflowCommand | mdm-guvenilir-kaynak-niyeti | FirstGskuIdentityWorkflowProcessor |
| StartGlobalProductCorrectionWorkflowCommand | mdm-guvenilir-kaynak-niyeti | GlobalProductCorrectionWorkflowProcessor |
| StartGlobalProductIdentityWorkflowCommand | mdm-guvenilir-kaynak-niyeti | GlobalProductIdentityWorkflowProcessor |
| StartGlobalProductRetirementRequestWorkflowCommand | mdm-guvenilir-kaynak-niyeti | GlobalProductRetirementRequestWorkflowProcessor |
| StartGskuCorrectionWorkflowCommand | mdm-guvenilir-kaynak-niyeti | GskuCorrectionWorkflowProcessor |
| StartGskuRetirementRequestWorkflowCommand | mdm-guvenilir-kaynak-niyeti | GskuRetirementRequestWorkflowProcessor |
| StartLskuIdentityWorkflowCommand | mdm-guvenilir-kaynak-niyeti | LskuIdentityWorkflowProcessor |
| StartLskuRetirementRequestWorkflowCommand | mdm-guvenilir-kaynak-niyeti | LskuRetirementRequestWorkflowProcessor |
| WithdrawFirstGskuIdentityApprovalCommand | mdm-guvenilir-kaynak-niyeti | FirstGskuIdentityWorkflowProcessor |
| WithdrawGlobalProductIdentityApprovalCommand | mdm-guvenilir-kaynak-niyeti | GlobalProductIdentityWorkflowProcessor |
| WithdrawLskuIdentityApprovalCommand | mdm-guvenilir-kaynak-niyeti | LskuIdentityWorkflowProcessor |

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- ArchiveProductCommand
- CreateFinishedGoodDraftCommand
- CreateFirstGskuDraftCommand
- CreateFirstGskuDraftFacadeCommand
- CreateLskuDraftCommand
- CreateProductCommand
- ReserveCanonicalCodeCommand
- UpdateGskuDraftCommand
- UpdateProductCommand
