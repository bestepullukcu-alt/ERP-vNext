---
record_id: TREASURY-PRODUCER-ANSWER-MOD0023-01
kind: üretici cevabı (MOD-0023 sahibi, platform-shared-services) — Treasury'nin 2026-10-25 sorusuna; onay değildir
received_at_local: 2026-10-25
received_from: MOD-0023 kod sahibi (platform-shared-services); sahip (K1) sohbete yapıştırdı
measured_by_producer_at: origin/main @05f311784 (salt okuma, test koşulmadı — üreticinin beyanı)
note: üretici bizim `4e01e986…` commit'imizi göremedi — Treasury dalı push edilmedi (D18); atıflarımız yerel dal içindir
decisions: D25 (bu cevaba göre sahip kararları)
---

# MOD-0023 sahibinin cevabı (özet; kaynak metin sohbette)

1. **Saklama / silmeme:** `WorkflowInstance`, `ApprovalTask`, `WorkflowTransitionLog` için bugün silme yolu yok (hard / soft
   delete, `HttpDelete`, TTL, arşiv yok; repository arayüzleri `DeleteAsync` açmıyor — `IWorkflowRepositories.cs`;
   TransitionLog ekleme-yalnız — `WorkflowTransitionLog.cs:30`). Üretici bağlayıcı politika yazacak: "bu üç kayıt silinmez ve
   süreyle temizlenmez; değişiklik sahip kararı ve tüketicilere önceden bildirim ister" + sabotaj kanıtlı mimari guard.
2. **Okuma alanları (yalnız ekleme, D9):** by-objects (`WorkflowInstanceStatusDto`, `WorkflowModels.cs:200-207`):
   `idempotencyKey`, `isDeleted` (silinmiş gizlenmez, işaretli döner; eski davranış varsayılan, öneri `includeDeleted=true`);
   history (`WorkflowInstanceHistoryEntryDto`, `:451-465`): `approvalTaskId`, `idempotencyKey`, `isDeleted`. Sözleşme ek
   sürümü ve tüketici testleriyle.
3. **Aynı anahtarlı çağrılar (bugün):**
   - Start, aynı gövde, eşzamanlı: kaybeden kazananın sonucunu 200 alır (`StartWorkflowInstanceHandler.cs:290-295, 385-391`);
     kazanan tamamlamadıysa yeniden denenebilir 503 `WORKFLOW_TRANSITION_CONFLICT`; yanıtta replay bayrağı yok; test
     `Parallel_submits_of_one_key_write_one_task_and_answer_200_or_a_retryable_503`.
   - **Start, aynı anahtar + farklı gövde, start tamamlanmış: bugün gövde karşılaştırılmadan 200 + mevcut örnek döner
     (`:385-391`) — üretici kusur sayıyor ve düzeltecek:** fingerprint karşılaştırması, fark → 409
     `WORKFLOW_INSTANCE_START_CONFLICT`; yanıta `idempotentReplay` bayrağı.
   - Görev geçişi: sıralı tekrar 200 + `isIdempotent=true` (`WorkflowTaskTransitionSupport.cs:1054-1084`); yalnız aktör
     karşılaştırılıyor (fark 403), gövde değil. Eşzamanlı kaybeden genelde 409 `WORKFLOW_TRANSITION_CONFLICT`; işlem
     kullanılamazsa ikiz kayıt kontrolüyle 200 replay / 409 / 503 `WORKFLOW_TRANSACTION_UNAVAILABLE`. Eklenecek testler:
     aynı anahtarla eşzamanlı iki approve / reject; aynı anahtar farklı gövde (sıralı ve eşzamanlı). Öneri: geçişte de gövde
     farkı → 409.
4. **Öneri:** üç madde bir WP'de (politika + guard, eklemeli alanlar, fingerprint düzeltmesi + testler). O gelene kadar
   Treasury Reconcile'ın "okumaya bakarak kapatmama" kuralı doğru; aynı anahtarı farklı niyet için asla yeniden kullanmayın.

## Treasury Intake CT'nin düzeltmeleri (bu cevaptan)

- **Hatalı ifade düzeltildi:** MOD-0023 §17.3/4'te "aynı anahtar + farklı olgu → 409" bütünleşmiş başlatma için de yazılmıştı;
  CT'nin `:419-438` atfı yalnız **tamamlanmamış** başlatmanın tamamlama yolu içindir. Bütünleşmiş başlatmada bugün 200 döner
  (üretici beyanı, `:385-391`). AC-W11 bugünkü kodla geçmez; üreticinin düzeltmesiyle geçer.
- **Eşzamanlı geçiş kaybedeni 409 `WORKFLOW_TRANSITION_CONFLICT` alabilir** — §17 tablosu "ikisi de 200" diyordu. Treasury
  tx2 eşlemesinde bu 409 ve 503 `WORKFLOW_TRANSACTION_UNAVAILABLE` kesin ret değildir → `Unknown` (D21/7 uygulaması, D25).
