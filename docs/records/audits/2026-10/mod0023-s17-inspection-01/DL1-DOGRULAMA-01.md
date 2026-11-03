# WF-S17-DL1 — bağımsız doğrulama 01 (2026-10-31)

- Teslim: `d30e93c36395ec4e7435e4482cbd82deb66100c0` · dal `feature/pss/wf-s17-dl1-r1` · taban `3096aadda` · prompt v1.7 (sha256 `759b0200…`). Push ve PR yok.
- Doğrulayan: işi yazmayan alt ajan (testler, sabotajlar, kod); CT üretim kodu farkını ayrıca okudu.
- Sonuç: engel yok. Durum VERIFYING'de kalır: `ct-sop7-transitions.tsv` VERIFIED için `known_failures.py check` çıkış 0 ister; mimari süitte çıkış 1'dir (YENİ=0; üç kırmızı işten önce var ve ana dalda da aynı, bkz. `DL1-TABAN-DURMA-01.md`). CT bu şartı kendi kendine atlamıyor; kapanması ana dalın yeşile dönmesine ya da kaydın tazelenmesine bağlıdır.

## Ölçüm

| Ölçüm | Sonuç |
|---|---|
| Platform `~Workflow` | 902 geçti, 0 kaldı, NotExecuted 0 |
| `WorkflowRetentionReadMongoTests` | 18/18, gerçek Mongo (kendi mongod, slot 6) |
| Mimari | 176: 173 geçti, 3 kaldı, NotExecuted 0; yeni kural 30/30 |
| Parmak izi | Tam 3 satır, prompt v1.7 ile aynı (`items:5:d9773291e8efe9fa`, `items:55:6ee8c6dc31c3a246`, `msg:13385a5e6ed357ef`) |
| `check` mimari / `platform-app` | Çıkış 1 (YENİ=0, YENİ-AYRINTI=2, KISMEN-DÜZELDİ=1) / çıkış 0 |
| API build | 0 hata |
| CRM / Inventory (gövde kanıtı değil) | 71/0, 22/0 |
| Fark kapsamı | 10 dosya, hepsi İzinli yol; Platform.Common boş; `tests/architecture` DMR boş |

## İşçinin denemediği sabotajlar (her biri geri alındı, sha256 commit ile aynı)

| Sabotaj | Kırmızı |
|---|---|
| `WorkflowTransitionLogRepository` override yok | `AC_W15_WorkflowTransitionLogRepository_refuses_delete_and_the_row_is_unchanged`, `AC_W1_the_three_repositories_override_DeleteAsync_and_the_override_only_throws` |
| `WorkflowInstanceRepository` override yok | `AC_W15_WorkflowInstanceRepository_refuses_delete_and_the_row_is_unchanged`, `AC_W1_the_three_repositories_override_…` |
| history `IdempotencyKey` null | `AC_W2_` üç test, `AC_W3_by_objects_and_history_keep_every_existing_field_and_add_the_new_ones` |
| by-objects `[HasPermission]` yok | `AC_W16_the_three_reads_require_exactly_instances_view`, `Batch_endpoint_uses_the_existing_view_permission`, `AC_W16_on_the_wire_a_caller_without_instances_view_is_refused_on_all_three_actions` |

Kiracı koşulu: `ListByObjectIdsAllRowsAsync` süzgeci `ExecutionFilter` ile aynı kiracı koşulunu üretir; çözülmemiş kiracıda iki okuma da aynı hatayı verir, `Guid.Empty`'de başka kiracı satırı gelmez (gerçek Mongo).

## AC

| AC | Durum |
|---|---|
| W1 | Geçti (üç tip). Kapsam sınırı: B-1 |
| W2 | Geçti |
| W3 | Kısmi: Platform satırları geçti; tüketici kanıtı sahiplerde (MDM dahil, D32/5) |
| W4 | Geçti |
| W13 | Geçti |
| W15 | Kısmi: üç depoda geçti; dördüncü depo S17-8 ile açık (B-2) |
| W16 | Geçti |

## Bulgular ve CT kararı

- **B-1 ORTA:** `WorkflowRetentionGuardTests` dördüncü deponun gövdesini ve `Features/Workflow` içi işaret yazımını taramıyor: `WorkflowStartRecoveryStore` içine `DeleteManyAsync` ya da `Update.Set(x => x.IsDeleted, true)` yazılırsa kural yeşil kalır. Kural prompt v1.7 ile S17-8 kararının yazdığı kapsamı uyguluyor, işçi hatası değil. `mod0023-s17-owner-decisions-05.md`'deki "AC-W1 bu kuralla korunur" cümlesi ölçümden geniştir; kayıt değişmez, bu satır onu sınırlar.
  **Karar:** DL-1 yeniden açılmaz; kapanış DL-2 promptuna girer (yalnız yeni kural dosyası). **Gerileme değerlendirmesi:** bugün Platform `src` altında bu üç tipe silme ya da işaret yazan yol yok (arama); DL-2 girene kadar yeni bir yol kural tarafından yakalanmaz, kod incelemesine kalır. Risk dar, süre DL-2'ye kadar.
- **B-2 ORTA (açık kayıt):** `WorkflowStartRecoveryStore.DeleteAsync` gerçek Mongo'da `IsDeleted = true` yazar; üretimde çağıran yok. Sonraki paket revizyonunda override (S17-8).
- **B-3 DÜŞÜK:** Kaynak-metin kuralı `Set("IsDeleted", true)`, `Set(nameof(...), true)`, `BulkWriteAsync` + `DeleteOneModel`, ara temel sınıf gibi yazımları kaçırır; başlık yalnız takma ad ve yansımayı anıyor. DL-2 kural dosyasında yazılır.
- **B-4 DÜŞÜK:** Varsayılan daldaki `DeletedAt` süzgeci, `IsDeleted = false` + `DeletedAt` dolu satırı gizler. Prompt gereği kaldı, bu üç tipe `DeletedAt` yazan kod yok.
- **B-5 DÜŞÜK:** `IsDeleted` alanı olmayan eski belge varsayılanda gizli, bayrakla `isDeleted = false` döner. Sahibin §17.8/2 ortam sayımına `{IsDeleted:{$exists:false}}` ve `{IsDeleted:false, DeletedAt:{$ne:null}}` eklenir.
- **B-6 DÜŞÜK:** İşçi raporu AC-W2 ve AC-W15 satırlarında geniş yazmış: yazım handler'ları sahte depoda koşuyor, Mongo testleri ham seed kullanıyor; "Common unchanged" tam kanıtı `git diff`'tir (boş, ölçüldü).

## Ölçülmedi

İşçinin S1 (ApprovalTask), S2, S3, S4, S5, S6 sabotajları yeniden koşulmadı. Taban koşuları (877, 146). Gerçek servis, üretim `Program.cs`, Gateway geçişi, canlı çağrı. `~Workflow` dışı Platform testleri. Tüketici gövde kanıtı (MDM, Web, Treasury). Ortam verisi.
