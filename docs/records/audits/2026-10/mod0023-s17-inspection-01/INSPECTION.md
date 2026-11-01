# MOD-0023 §17 — DL-1 / DL-2 kod incelemesi 01 (salt okunur)

- Kuyruk: `pss-wf` — WF-S17-DL1, WF-S17-DL2
- Ölçüm tabanı: `chore/ct/pss-wf-ct-01` @4c08fc75a (= origin/main, 2026-10-29). Derleme, test, Mongo yok — çalışma testi yapılmadı.
- Paket: `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md`, onay
  `execution/approvals/approvals.tsv` sha256 803fbaa5… (yalnız §17). Bu ölçümde `scripts/pack_approval.py check` çıkış 1:
  "approvals.tsv 0e818e3c155c'de yalnız ekleme değil … onay dosyası güvenilmez — hiçbir onay geçmez" (ana dal sorunu, #241).
- Yöntem: iki alt ajan (Explore, model sonnet, effort belirtilmedi — araç varsayılanı; salt okunur), sonra CT her dilimin
  ana bulgusunu kendi okumasıyla doğruladı (aşağıda "CT doğruladı"). Diğer satırlar alt ajan ölçümüdür, CT tek tek okumadı.

## DL-1 — silmeme + eklemeli okuma alanları

Doğrulanan (paketle aynı): üç depo `TenantRepository<T>`'den türer (`WorkflowRepositories.cs:141, 452, 798`);
`RepositoryBase<T>.DeleteAsync` yalnız `IsDeleted` + `UpdatedAt` yazar (`Platform.Common/.../Repositories.cs:35-46`); üç arayüzde
silme üyesi yok (`IWorkflowRepositories.cs:41, 191, 371`); `services/` altında bu üç tipe `DeleteAsync` çağrısı yok; TTL yok.

**Paketin yanlış ya da eksik kalan yerleri:**

1. **Silinmiş satır zaten depo katmanında gizli (CT doğruladı).** `TenantRepository.ExecutionFilter` = TenantId **ve**
   `IsDeleted == false` (`Repositories.cs:75-79`); `ListByObjectIdsAsync` bu filtreyi kullanır (`WorkflowRepositories.cs:181-185`).
   `by-objects` işleyicisindeki `.Where(x => x.DeletedAt is null)` (`GetWorkflowInstancesByObjectsHandler.cs:51`) gerçek Mongo'da
   etkisizdir. §17.8/2'deki "devralınan yolla silinen satır gizlenmez" yalnız sahte depolarda doğrudur.
2. **`includeDeleted=true` izinli yollarla yapılamaz.** Silinmişi döndürmek için `IsDeleted`'ı atlayan yeni bir depo okuması ve
   arayüz üyesi gerekir (Domain `IWorkflowRepositories.cs`); `history` için de örnek araması (`GetWorkflowInstanceHistoryHandler.cs:51`,
   silinmiş örnekte 404), geçiş kayıtları (:61) ve görevler (:65). §17.8/9 yalnız `DeleteAsync` override'ı, silinmiş filtresi ve
   okuma işleyicilerine izin verir; Domain arayüz dosyası izinli yollarda yok.
3. Kişi alanı listesi eksik (§17.8/4): ayrıca `WorkflowTransitionLog.EvidenceRef`, `SignatureId`; `WorkflowInstance.ObjectRef`,
   `DisplayContext` (serbest metin). `RuntimeAssignmentSnapshot` ölçülmedi.
4. Tüketici listesi (AC-W3): Platform'un kendi `by-objects` testleri (`WorkflowCrossServiceTests.cs:448-480`) eksik; MDM
   `GatewayWorkflowClient` tüketici testi hâlâ yok (D32/5, MDM sahibi); Web'de geçmiş gövdesi testi yok; Treasury kodu ana dalda yok.
5. Şema manifest yolu `Persistence/Schema/PlatformSchemaManifest.WorkflowWorkCenter.cs` (paket klasörsüz yazıyor).

## DL-2 — aynı anahtar yanıtı

Doğrulanan (CT doğruladı): bütün başlatma tekrarı 200 döner, olgular ve başlatan karşılaştırılmaz
(`StartWorkflowInstanceHandler.cs:385-391`); `idempotentReplay` alanı yok. Sıralı geçiş tekrarı yalnız aktörü karşılaştırır
(`WorkflowTaskTransitionSupport.cs:1018-1030`).

**Paketin yanlış ya da eksik kalan yerleri:**

1. **Hata kodu adı (CT doğruladı):** farklı aktör → 403 `WORKFLOW_ACTOR_DENIED` (`:1024-1028`), `WORKFLOW_ACTOR_MISMATCH` değil
   (o, denetleyicinin gövde-aktör kontrolü). §17.7/5 ve AC-W14 düzeltilmeli.
2. **İptal tekrarı hiçbir şeyi karşılaştırmıyor (CT doğruladı):** `:622-630` doğrudan `BuildIdempotentResponseAsync`; başka aktörün
   iptal tekrarı 200 alır. Pakette yok.
3. AC-W8: iki farklı karar yarışında kaybeden 409 `WORKFLOW_TRANSITION_CONFLICT` alır (testler bunu bekliyor:
   `WorkflowCandidateDecisionMongoTests.cs:118, 142`); `WORKFLOW_TASK_INVALID_STATE` yalnız geç gelen içindir (`:82-89`).
4. Aynı anahtarlı eşzamanlı özdeş geçişte kaybeden çoğunlukla 409 alır; 200 yalnız "işlem kullanılamaz" yeniden okumasından sonra.
5. Pakette olmayan güvenilir yol: `Services/WorkflowInstanceStartCoordinator.cs:228-255` başlatmayı zaten parmak iziyle karşılaştırır,
   409 `WORKFLOW_START_IDEMPOTENCY_CONFLICT` (farklı kod); `TrustedWorkflowCancellationCoordinator.cs` de öyle. DL-2'nin kapsamı
   bunlara karar vermeli.
6. AC-W17 tüketici listesi eksik: Platform içi çağıranlar (Tasks, Timesheet, WorkCenter dağıtıcısı) aynı anahtarla değişen olgu
   gönderebilir; Timesheet bugün 200 bekliyor (`TimesheetApprovalService.cs:188-197`). Web'de anahtar alanı kullanıcıca
   düzenlenebilir (`workflow.api.js:127-136`).
7. Aynı anahtarlı eşzamanlı özdeş geçiş testi (AC-W6/W7) ve terminal sonrası tekrar testi (AC-W9/W10) yok.
8. DL-2 prompt'unun adlandırması gereken dosyalar: `StartWorkflowInstanceHandler.cs`, `WorkflowTaskTransitionSupport.cs`,
   `WorkflowTerminalTransitionWriter.cs` (yalnız `SamePayload` paylaşılırsa), `WorkflowModels.cs`, `RejectWorkflowTaskHandler.cs:79-80`.

## Sonuç (CT)

- İki dilim de bugünkü paket metniyle prompt kapısına gidemez: DL-1'in izinli yolları işi yapmaya yetmiyor (Domain arayüzü),
  DL-2'nin kabul ölçütlerinde yanlış hata kodları ve eksik kapsam var. Gereken: §17 revizyon 3 (paket yazarı, bağımsız
  inceleme, yeni onay satırı — HIGH).
- Yeni onay, ana daldaki onay dosyası sorunu (#241) düzelmeden geçemez; revizyon taslağı onaydan bağımsız yazılabilir.
- Kuyruk satırları DISCOVERED kalır; bu not WF-S17-DL1 / WF-S17-DL2'nin inceleme kaydıdır.
