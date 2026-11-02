# MOD-0178 DL-3 — Control Tower kabulü (WMS-MVP5-DL3)

- Tarih: 2026-11-02. CT: MVP-5 CT (W-10), oturum `ca7382cf`. Doğrulama: CT içi alt ajan (Opus 5.5 · high), depo dışı detached ağaçlar
  (`a98e9c54e` ve taban `618929f7d`), iş sonunda kaldırıldı. Geliştirme oturumu `local_da5462be`, aktaran `local_75b9d993`.
- İncelenen: `8f972b86a` (DL-2 F-3 testi) · `db6127ba5` (manifest: PUTAWAYTASKS + WAREHOUSELOCATIONROLES, §11.3'ün 14 eylemi) ·
  `a98e9c54e` (iki Golden Slim ekran, 7 dil, sayfa yardımı). MVP-1 hattında (`feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok.
- Paket: `MOD-0178-putaway-picking-packing.md` @ sha256 `9b4be5e3…1decf` (r19, onaylı).
- Sonuç: **KOŞULLU** — engel yok; F-1 ve F-2 kapanmadan, F-3 DL-8 sonrasına açık AC olarak yazılmadan VERIFIED yazılmaz.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`618929f7d..a98e9c54e`) | 62 dosya MOD-0178 (Web, Web.Tests, Inventory manifest, 2 test) + 2 doküman dosyası `7e5d7ca7a`'dan (MOD-0040-FU02); `ocelot.json`, `_Layout.cshtml`, `.antigravity`, başka modül servisi yok |
| Derleme | Inventory 0 hata / 0 uyarı (taban 0 / 0); Web 0 hata, uyarılar tabanla birebir |
| Tam Inventory takımı (filtresiz, kendi replica set'i) | 1578 / 1582, 4 atlandı, 0 kırmızı |
| Web takımı | 4180 / 4184; 4 kırmızının dördü tabanda da kırmızı (MRP / SafetyStock nav, Capacity, S&OP); yeni kırmızı 0 |
| Repo mimari testleri | 10 kırmızı, tabanla ad ad aynı (approvals-append-only.yml kaynaklı CiServiceTestsGuard 3, DocsPathGuard 5, AuditTrail 2); Inventory 0 |
| 7 dil | PutawayTasksIndex 206, WarehouseLocationRolesIndex 104 anahtar; yeni 22 yardım + 3 nav anahtarı; eksik / boş / İngilizceyle aynı 0; hardcoded metin yok |
| Manifest ↔ controller | 14 eylem bire bir; Web proxy'leri gövdede `_permissions.Has(...)` (repo emsali), otorite servis `[HasPermission]` |
| UAS-001 | yetkisiz kullanıcıya `_AccessDenied`, yönlendirme ve iskelet yok |
| AC-22 doğrulayıcı | paketteki komutla FAIL (varsayılan direct-gateway profili); `--api-profile proxy` ile iki ekran 104 kontrol, 0 sapma |

## CT sabotajları (uygulandı → sonuç → byte-eşit geri alındı)

| Sabotaj | Sonuç |
|---|---|
| F-3: toplu atamaya eski `IntegrityError` dalı | kırmızı (`/results/0/outcome` enum ihlali) — DL-2 F-3 kapandı |
| `PutawayTasksController.Confirm` izin kontrolü silindi | kırmızı |
| `PutawayTasksController.Edit` izin kontrolü silindi | **yeşil (tam Web takımı) — F-1** |
| `WarehouseLocationRolesController` Edit + BulkDelete izin kontrolü silindi | **yeşil — F-1** |
| `SharedResource.ar.resx`'ten `Nav.Page.PUTAWAYTASKS` silindi | kırmızı |
| Manifestten `PUTAWAY_TASK_CLAIM` silindi | kırmızı |
| Yetkisiz daldan `return;` kaldırıldı (UAS-001) | kırmızı |

## Bulgular

F-1 | ORTA | Web proxy yazma eylemlerinin izin kontrolü testsiz: PutawayTasks.Edit ve WarehouseLocationRoles.Edit / BulkDelete'ten izin kontrolü silinince tam Web takımı yeşil; eylem başına anahtar testi Edit, rol Edit / BulkDelete, Export, Warehouses, Bins, People, Nodes ve rol ayrıntısını kapsamıyor | açık | MOD-0178 geliştirici | 2026-11-04
F-2 | ORTA | §11.3a'nın iki ekran durumu (saklama koşulu bilinmiyor rozeti + pasif hedef; şirket kodu geçersizken pasif Yeni görev) yok, yalnız gönderim sonrası hata; kök neden aday DTO'sunda storageConditions yok ve şirket kodu ön okuması yok | açık | MOD-0178 geliştirici + sahip kararı | 2026-11-04
F-3 | ORTA | AC-45 (375 px, 48 px hedef, ekran görüntüsü + DOM ölçümü) ve §17 E4 çalışma zamanı kanıtı yok; Gateway rotası (DL-8) gelince koşulur | açık | integration-agent + MOD-0178 geliştirici + CT | DL-8 sonrası
F-4 | DÜŞÜK | AC-22 doğrulayıcı komutuna --api-profile proxy eklenmeli | açık | paket sahibi | sonraki paket revizyonu
F-5 | DÜŞÜK | §11.2 Slim seti listesinde rules.js ve warehouse.css yok | açık | paket sahibi | sonraki paket revizyonu
F-6 | DÜŞÜK | Raf önerisi yalnız düzenleme formunda (sözleşmede adayda öneri yok); §8.6 / AC-42'ye sahip onaylı not | açık | sahip kararı + paket sahibi | sonraki paket revizyonu

## Açık kalanlar

- WMS-BUNDLE dondurması (§19.7 / AC-60) DL-3 dışında; dondurma sonrası Web DTO'ları yeniden kıyaslanır.
- Geliştiricinin 14 mimari kırmızısına karşı CT ağacında 10: fark `.claude/worktrees` kopyalarının taranmasından (CT ağacı depo dışında; ölçülmedi).
- Taban kırmızıları DL-3 bulgusu değil; sahiplerine bildirim ayrı takip.
