# MOD-0178 DL-2 — Control Tower kabulü (WMS-MVP5-DL2)

- Tarih: 2026-11-01. CT: MVP-5 CT (W-10), oturum `ca7382cf`. Geliştirme oturumu `local_52ece788`.
- Kabul edilen: `9d7bab02b` (DL-2) + `61edb8c41` (origin/main `--no-ff`, 413 commit, çakışmasız) + `cf79c85c3` (CT F-1 / F-2 ek commit'i).
  MVP-1 hattına (`feature/sce/mod-0173-inventory-ledger-valuation`) alındı; push ve PR yok (sahip kararı: push / PR geliştirme bitince tek sefer).
- Sonuç: **VERIFIED** — CT doğrulaması geçti; F-3 (DÜŞÜK) sahibi ve tarihiyle açık. ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).

## Ölçülenler (CT'nin kendi worktree'si, `--detach`)

| Kontrol | `61edb8c41` | `cf79c85c3` |
|---|---|---|
| Kapsam (`7706c9dd9..9d7bab02b`) | yalnız `services/Diten.InventoryService/**`: 69 yeni, 20 değişen dosya | + 9 dosya (üretimde yalnız toplu atama sonucu) |
| Derleme | 0 hata / **2 uyarı** (F-1) | temiz derleme 0 hata / 0 uyarı |
| Tam Inventory takımı (filtresiz, `INVENTORY_TEST_MONGO` boş, takımın kendi replica set'i) | 1565 / 1569, 4 atlandı, 0 kırmızı | 1576 / 1580, 4 atlandı, 0 kırmızı |
| Repo mimari testleri | 176'da 7 kırmızı = main baseline-red (DocsPathGuard 5 — Windows CRLF; AuditTrail 2 — CRM / Platform) | — |

Pack §5 seam'leri: `IInventoryLedgerRepository.FindReversalOfAsync` / `InventoryLedgerRepository` (eklemeli okuma, `IsReversedAsync` ile aynı filtre).
Donmuş WAREHOUSE-OUTBOUND GET'leri (`OutboundShipmentsController`, AC-18: S1'de boş liste / 404 `UNKNOWN_OUTBOUND`).

Sahip onaylı istisnalar (XMC-001, 2026-10-31; pack §5 güncellemesi FU-0178-PACK-SEAM-01):
- seam 9 `LedgerImmutabilityArchitectureTests`: yalnız `PutawayTasksController:putaway-tasks/{taskId}` PUT;
- seam 10 `SingleSystemOfRecordArchitectureTests`: `wms_tasks`, `wms_task_events` ad ad;
- seam 11 `ExpiryQuarantineTypeExemptionTests` (MOD-0176): yalnız 0178'in tek stok kapısı (`Mod0178StockDoor`).

Q-13 (A): atanan kullanıcı AUTH `GET /api/users/{userId}/lookup-validation` (`auth.users.lookup-validation`) ile doğrulanır; geçersiz → 422
`WMS_ASSIGNEE_INVALID`, AUTH erişilemez → 503 ve yazım yok. Açık nokta: uç şirket uygunluğunu denetlemez.

## CT sabotajları (uygulandı → sonuç → byte-eşit geri alındı)

| Sabotaj | Sonuç |
|---|---|
| Görev deposu `Company<T>()`: yalnız `TenantId` yüklemi kaldırıldı | 1 test kırmızı (`PutawayAccessAuditTests.AC13_another_tenant_even_with_the_same_company_and_user…`) |
| Q-13: `PutawayChecks.AssigneeAsync` doğrulamayı atladı | 4 test kırmızı (`PutawayAssignmentTests.Q13_*`) |
| Seam 9 sınırı: `PutawayTasksController`'a `DELETE putaway-tasks/{taskId}` eklendi | `No_controller_in_the_service_exposes_put_or_delete` kırmızı |
| `cf79c85c3` sonrası: toplu atamada bütünlük hatalı görev için eski `IntegrityError` geri kondu | **yeşil (254 / 254) — F-3** |

## Bulgular

F-1 | DÜŞÜK | PutawayRecoveryTests.cs:354 ve :371 iki CS8604 uyarısı (AC-1 sıfır yeni uyarı ister) | kapandı | - | -
F-2 | ORTA | Raporun kendi listesindeki zorunlu AC parçaları testsizdi: AC-15 çökme, AC-19 sözleşme şeması, AC-64 C2 / E / D-ii / D-iii, AC-70 bariyerli sıra | kapandı | - | -
F-3 | DÜŞÜK | Toplu atamada bütünlük hatalı görevin InvalidTransition dönmesini ölçen test yok; doğrulayıcı (WmsDraftContract.cs:290) bu yolu hiç görmüyor | açık | MOD-0178 geliştirici | 2026-11-04

F-2 kapanışı (`cf79c85c3`): AC-15 tek başına koşan çökme testi (`PutawayCentralAuditCrashTests`); AC-19 taslaktan türetilmiş kapalı JSON şeması
(`WmsDraftContract`), bütün `/api/warehouse` cevaplarına uygulanır; AC-64 / AC-70 yarış testleri (`PutawayRaceTests`). Geliştiricinin her yeni testin
sabotajı kırmızı; (bo) tek dosyalık hali kırmızıya düşmedi, iki dosyalı hali kırmızı (kontrol kodda iki yerde) — gerekçe geliştirici raporu §11.

## Açık kalanlar

- F-3: DL-3'ün ilk commit'inde test + sabotaj (eski değer kırmızı).
- §19.7 dondurma adımına öneri: onay hatasındaki `reason` ve toplu sonuçlardaki `contractVersion` taslakta yok.
- FU-0178-PACK-SEAM-01 (seam 6–11), FU-0178-LOC-MOVE-01.

Geliştirici raporu: side-session-notes `local_75b9d993…/mod-0178-dl2-01-rapor.md` (§11 CT F-1 / F-2 eki).
