---
record_id: TREASURY-PACKS-GAP-LIST-05
kind: sahip inceleme bulguları ve kapanışları (dört pack taslağı) — onay değildir
received_at_local: 2026-10-24
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — G1, G3, G4, G5 ve AP dekont tasarımı karşılanmış. G2'de bir netleştirme kalıyor")
reviewed_input: .ct-teslim/treasury-intake/treasury-packs-review-e55324ccf.zip (sha256 4ed1272dcbf8778539f2dd50c6e691c89bcdfe4bbedc14b21c0ab1fcae18f0ab)
fixed_in: MOD-0126, MOD-0119, MOD-0120, MOD-0124 pack'leri (bu kaydın commit'i)
previous: GAP-LIST-04-TREASURY-PACKS.md
---

# Treasury pack'leri — gap listesi 5 (G2 netleştirmesi) ve kapanışı

Sahip doğruladı: G1, G3, G4, G5 ve AP dekont tasarımı karşılandı; dört pack'te AC numaraları tekil.

## Kalan G2 noktası — "kesin boş"un uzak sözleşmedeki karşılığı

**Treasury Intake CT ölçümü (Recovery, bu worktree HEAD'i):**

| uzak işlem | aynı anahtarla tekrar güvenli mi | kanıt |
|---|---|---|
| MOD-0023 iş akışı başlatma | evet — tekil indeks + yeniden oynatma | `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131` (`ux_workflow_instances_tenant_idempotency_key_active`), `StartWorkflowInstanceHandler.cs:76-85` |
| MOD-0023 görev geçişleri | evet — tekil indeks + yeniden oynatma | aynı dosya `:211-225` (`ux_workflow_transition_logs_task_action_idempotency_active`), `RejectWorkflowTaskHandler.cs:79-80`, `WorkflowTerminalTransitionWriter.cs:402-407` |
| MOD-0023 kesin kapanış | var | `CloseAbandonedWorkflowStartHandler.cs` (`workflow-start-recovery-close:{instanceId:N}`, `:230`) |
| MOD-0031 kanıt bağlama / Cash Positioning dışa aktarımı | **ölçülmedi — güvence yok** | — |
| aynı anahtarlı eşzamanlı yarış yanıtı | **ölçülmedi** (indeks ikinci kaydı engeller; kaybedene ne döndüğü bilinmiyor) | — |

**Kapanış (dört pack, Reconcile bölümleri MOD-0126 3a, AP 3a, AR 4a, MOD-0124 5a):**
1. "Kesin boş" bir **okumadan** karar verilmez; okumaya dayalı "kesin boş → yeniden başlat" dalı kaldırıldı. 404 / boş /
   zaman aşımı / 5xx → niyet `Unknown`.
2. Uzak yazmanın yeniden yapılması **yalnız** anahtarla idempotent olduğu ölçülen MOD-0023 başlatma ve görev geçişlerinde,
   **aynı** deterministik anahtarla ve LE / SoD / izin / asıl aktör yeniden kontrolünden sonra. Başarı (yeni ya da yeniden
   oynatılmış) niyeti kapatır; yinelenen anahtar / tekil indeks yanıtı **asla** `Refused` sayılmaz; diğer başarısızlıklar
   `Unknown`. Yarış yanıtı ölçümü iş akışı diliminin önkoşulu ve Ready-for-dev maddesi.
3. MOD-0031 kanıt bağlama ve Cash Positioning dışa aktarımı **hiçbir kurtarma yolunda yeniden çağrılmaz**; Reconcile yalnız
   okur; dedupe ölçümü yeniden çağrının önkoşulu (kapanış tablosu, Ready-for-dev).
4. `Cancelled` yalnız kesin uzak iptal / kapanış kaydıyla (MOD-0023 iptal geçişi ya da `CloseAbandonedWorkflowStart`); Cash
   Positioning dışa aktarımı için ölçülmüş kesin iptal yok → şimdilik hiç `Cancelled` olmaz.
5. Kabul ölçütlerinde Reconcile bağlamındaki "uzak çağrı yok" → **"uzak yazma/başlatma çağrısı yok"** (okuma çağrısı vardır).
   Reconcile AC'leri (MOD-0126 AC-34, AP AC-34, AR AC-36, MOD-0124 AC-33) (a)–(e) olarak yeniden yazıldı; sabotaj: farklı
   anahtarla yeniden yazma ya da MOD-0031 / dışa aktarmanın yeniden çağrılması testi kırmızıya düşürür.

**Sahip teyidi bekleyen tanım (yazar ajanın):** MOD-0023 yeniden yazmasında "kesin ret" = mevcut tx2 eşlemesi (400 / 403 /
404 / 409, MOD-0023'ün "başlatma tamamlanmadı" 409'u hariç), yinelenen anahtar / tekil indeks yanıtları açıkça hariç.

Kontroller: dört pack'te 24 bölüm; AC kimlikleri tekil (35 / 36 / 36 / 34); dilim tabloları okunuyor; "zaman aşımı/404 →
Cancelled" geçen her satır olumsuzlama; okumaya dayalı "kesin boşsa yeniden başlat" ifadesi kalmadı. Bu bir belge
incelemesidir; test kriterlerinin eklenmesi testlerin geçtiği anlamına gelmez.
