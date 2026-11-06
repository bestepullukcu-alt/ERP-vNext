# WF-S17-DL2 — bağımsız doğrulama 01 (2026-11-02)

- Teslim: `1e5615697984b98d85be4873991f9322f2442217` · dal `feature/pss/wf-s17-dl2-r2` · taban `8698dbddc` · prompt v1.8 (sha256 `08ebd287…`). Push ve PR yok.
- Doğrulayan: işi yazmayan alt ajan (Claude Opus 5.5 · high; kod, testler, sabotajlar). Oturum yalıtımı nedeniyle hedef ağaçta git ve yazma yok: temizlik 145 izli dosyanın blob karşılaştırmasıyla ölçüldü, sabotajlar `git archive 1e5615697` kopyasında yapıldı (dosya sha256'ları aynı).
- Sonuç: engel yok. İki ORTA test açığı (B-1, B-2) kod hatası değil; CT kararı: aynı iş kapsamında, izinli test yolunda işçiye kapattırılır, sabotaj kanıtıyla ikinci commit. AC-W17 ve AC-W18 sahip kaydı bekler. Durum VERIFYING'de kalır (mimari `check` çıkış 1, DL-1 ile aynı neden).

## Ölçüm

| Ölçüm | Sonuç |
|---|---|
| Platform `~Workflow` + `TimesheetApprovalHttpMongoTests` | 1037 geçti, 0 kaldı, NotExecuted 0 (kendi mongod, slot 7, 37994) |
| AC adlı testler | 118/118; S17 sınıfları ve `WorkflowSameKeyHttpMongoTests` (4) Passed |
| Mimari | 216: 210 geçti, 6 kaldı, NotExecuted 0; yeni bekçi 40/40, eski bekçi 30/30 |
| Parmak izi | Platform boş; mimari 6 satır, işçinin tabanı ve son koşusuyla aynı |
| `check` mimari / `platform-app` | Çıkış 1 (YENİ=3 CiServiceTestsGuardTests, YENİ-AYRINTI=2, KISMEN-DÜZELDİ=1) / çıkış 0 |
| Derleme | Test projesi (API dahil) 0 hata |
| Fark kapsamı | 13 dosya, hepsi İzinli yol; Platform.Common boş; `tests/architecture` DMR boş; iki istisna testinde yalnız hazırlık |

## İşçinin denemediği sabotajlar (her biri geri alındı, sha256 commit ile aynı)

| Sabotaj | Kırmızı |
|---|---|
| Neden kodu sabitinin değeri `WORKFLOW_TRANSITION_CONFLICT` | yok (B-1) |
| `SameBody`'den ReasonCode çıkarıldı | `AC_W14_a_different_body_is_409` ×3 (reason), `AC_W18_cancel_replay_different_body_is_409`, `Same_key_and_actor_whose_stored_row_differs_in_one_field_is_not_my_commit(ReasonCode)` |
| `TwinOrAsync` yalnız karar-verilemez dalından kaldırıldı | `AC_W6_b` ×2, `AC_W7_b`, `AC_W14_b_other_actor_late_is_403` ×2 (sürümsüz), `AC_W14_b_different_body_late_is_409` ×2 |
| `WholeStartAsync` bayrağı false | yok (B-3) |
| Geçiş anahtar okumasından kiracı süzgeci kaldırıldı | yok (B-2) |
| RequestInfo sıralı tekrarında ham `comment` | `AC_W14_target_collision_sequential(AB)`, `AC_W14_legacy_row_identical_retry(sequential,target)`, `Duplicate_request_info_same_idempotency_key_does_not_duplicate_log` |

## AC

| AC | Durum |
|---|---|
| W5, W6, W7, W8, W9, W10, W12 | Geçti |
| W11 | Geçti (`WholeStartAsync` bayrağı ölçülmüyor, B-3) |
| W14 | Geçti (kod değeri sabitlenmemiş, B-1) |
| W17 | Kısmi: Platform içi tüketiciler geçti; servis dışı tüketici ve sahip kaydı bekliyor (MDM dahil, D32/5) |
| W18 | Kısmi: üç test geçti; sahip kaydı bekliyor |
| O-S17-3 = (i) | Geçti |

## Bulgular

- **B-1 ORTA:** `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT` kablo değeri testle sabit değil; testler sabitin kendisiyle karşılaştırıyor. §17.9/7'nin "ayrı kod" şartının guard'ı yok. Bugün değer doğru. Düz dizge assert'i yeterli.
- **B-2 ORTA:** Geçiş anahtar okumasında kiracı süzgecinin guard'ı yok (sabotaj yeşil). Kod DL-2 öncesine ait; DL-2 ona iki yeni çağrı ekledi. Gerilerse başka kiracı 404 yerine 403 alır. Test açığı.
- **B-3 DÜŞÜK:** `WholeStartAsync`'in `idempotentReplay = true` değeri ölçülmüyor; commit sonucu bilinmeyen ilk yazma da true alır (prompt kabul ediyor).
- **B-4 DÜŞÜK:** J-1 tüketici tablosu yarım başlatmadaki DueAt 409'unu DL-2'ye atfediyor; bu Expected HEAD'de de vardı (`CompleteStartAsync` parmak izi, `TaskApprovalService.cs:212`). DL-2 farkı yalnız bütün başlatma tekrarında; Tasks çağıranlarında yalnız eşzamanlı yarışta oluşur, kaybeden loglanır ya da "yeniden deneyin" alır. AC-W17 sahip kaydına bu okuma yazılır.
- **B-5 DÜŞÜK:** Şablon bulunamazsa bütün başlatma tekrarı 200 yerine 409 (okumadan çıkarım, ölçülmedi).
- **B-6 DÜŞÜK:** İşçinin geçici dalı `feature/pss/wf-s17-dl2-r2-wip` (`1a43e453e`) yerelde duruyor; silme sahibin sözüyle.

## Ölçülmedi

İşçinin S1–S19, G1–G8 sabotajları yeniden koşulmadı. Expected HEAD'de taban koşusu yok. DOĞRULA 1'in tam filtresi (Tasks, Meetings, ESignature, WorkAggregation) koşulmadı. CRM, Inventory, MDM, HCM, Web tüketici koşuları yok. İki istisna testinin önceki kırmızısı yok. Hedef ağaçta `git status` yok. Gerçek servis, Gateway, canlı çağrı yok. TRM-15 ve O-S17-1 açık.
