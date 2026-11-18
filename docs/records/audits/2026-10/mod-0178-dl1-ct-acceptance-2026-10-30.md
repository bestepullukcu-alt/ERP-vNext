# MOD-0178 DL-1 — Control Tower kabulü (WMS-MVP5-DL1)

- Tarih: 2026-10-30. CT: MVP-5 CT (W-10), oturum `ca7382cf`. Geliştirme oturumu `local_52ece788` (MOD-0178 oturumu
  `local_75b9d993` başlattı).
- Kabul edilen: `feature/sce/mod0178-dl1-01` @ `7706c9dd9` (`c5ffb454c` + F-1 test commit'i), taban origin/main `317a48b13`.
  Yerel; push ve PR yok (sahip kararı: push / PR geliştirme bitince tek sefer).
- Sonuç: **VERIFIED** — CT doğrulaması geçti. ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).

## Ölçülenler (CT'nin kendi worktree'si, `--detach`)

| Kontrol | Sonuç |
|---|---|
| Pack blob SHA-256 | `9b4be5e3…1decf` (r19, onay satırı #258) |
| Kapsam | `317a48b13..7706c9dd9`: yalnız `services/Diten.InventoryService/**`; 45 yeni dosya pack §5 klasörlerinde; 8 değişen dosya = pack §5'in 5 seam'i + 3 sahip onaylı istisna |
| Korunan yollar | gateway, `.antigravity`, sözleşmeler, registries, başka servisler, frontend: dokunulmamış |
| Derleme | 0 hata / 0 uyarı |
| Tam Inventory takımı (filtresiz, `INVENTORY_TEST_MONGO` boş, takımın kendi replica set'i) | `c5ffb454c`: 1435 / 1431 geçti / 4 atlandı / 0 kırmızı. `7706c9dd9`: 1437 / 1433 / 4 / 0. Atlananlar `INVENTORY_PERF` bayraklı performans testleri |
| `CentralAuditDeclarationTests` | 71 komut, `seen` 11 |

## Seam'ler

Pack §5: `Program.cs` (manifest + `AddWarehouse()`), `InventoryScopeResolutionMiddleware` (`/api/warehouse`), `SkuReference.StorageConditions`
(null = bilinmiyor), `ProductMasterHttpClient` (dizi değilse null), `CentralAuditDeclarationTests` (67 → 71).

Sahip onaylı istisnalar (XMC-001, Beste 2026-10-29; pack §5 güncellemesi FU-0178-PACK-SEAM-01):
- `LedgerImmutabilityArchitectureTests`: yalnız `WarehouseLocationRolesController:{roleId}` PUT ve DELETE (`Assert.Single` ile sabit).
- `SingleSystemOfRecordArchitectureTests`: `WarehouseCollections.All` üç ada sabit; `wms_` öneki bildirilen kümede aranır.
- `InventoryLedgerManifestCompletenessTests`: yalnız `.Controllers.Warehouse`.

## CT sabotajları (uygulandı → sonuç → byte-eşit geri alındı)

| Sabotaj | `c5ffb454c` | `7706c9dd9` |
|---|---|---|
| Rol deposu: yalnız `TenantId` yüklemi kaldırıldı | **yeşil (109/109) — F-1** | 2 yeni test kırmızı |
| İki sıcaklık koduna izin (`> 1` → `> 2`) | 4 test kırmızı | — |
| `wms_` istisna sınırı: bildirilmemiş `wms_ct_sabotage_extra` adı | `SingleSystemOfRecord…declared_set` kırmızı | — |

F-1 | ORTA | Rol deposunda yalnız TenantId yüklemi kaldırılınca hiçbir test kırılmıyordu; yabancı kullanıcı hem başka kiracıda hem başka şirketteydi. | kapandı | - | -

Kapanış: `7706c9dd9`, `WarehouseLocationRoleAccessTests` iki test (aynı LegalEntityId ve kullanıcı id'siyle başka kiracı; aynı anahtarlarla
geçmiş ve anahtar okuması). Geliştiricinin beş tek-yüklem sabotajı (rol deposu, `HistoryAsync`, `FindBulkKeyEventsAsync`,
`FindByCreateKeyAsync`, sayaç) kırmızı; CT rol deposu sabotajını yeniden koştu, kırmızı. Üretim kodu değişmedi.

## Baseline-red (main'de de kırmızı, DL-1'den değil)

`TenantArchitecture.ArchitectureTests` 146'da 8: DocsPathGuard 5 (Windows CRLF; git blob SHA'ları pinlerle aynı), CiServiceTestsGuard 1
(`approvals-append-only.yml`, `ui-compliance.yml` listede yok), AuditTrailStandard 2 (CRM 53 + Platform 2 denetimsiz komut; Platform e-imza
5 aynı ad). Inventory komutu yok.

## Açık kalanlar

- FU-0178-PACK-SEAM-01: pack §5 / §6 / §7'ye 6.–8. seam'ler.
- FU-0178-LOC-MOVE-01: K-07 taşımasından sonra liste filtresindeki saklanan `WarehouseId`.
- `tests/architecture/audit-ledger/Diten.InventoryService.md` notu (10 → 11 geri çağrı, 66 → 71 komut) — belge notu.
- DL-2'ye: `WMS_LOCATION_HAS_OPEN_TASKS` / AC-58, görev numaralandırması + `(TenantId, Number)` index'i, AC-37.
- Yinelenen dal `feature/sce/mod0178-dl1-01b` (`2b77f975c`, CT oturumunun yanlışlıkla açtığı kopya): kullanılmaz; son push / PR'dan sonra silinir.

Geliştirici raporu: side-session-notes `local_75b9d993…/mod-0178-dl1-01-rapor.md` (§10 F-1 eki).

Önerilen module-implementation-status satırı (main'e girişte yazılır):
`| MOD-0178 | DL-1 VERIFIED (main'e girmedi) | 15 | Lokasyon rolleri + saklama koşulu etiketleri / kalıtım + geçici RCS sayacı (backend, /api/warehouse/location-roles); 7706c9dd9, Inventory 1433/1437 (4 perf skip) | DL-2…DL-8, FU-0178-PACK-SEAM-01, FU-0178-RCS-01 |`
