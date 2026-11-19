# MOD-0178 DL-8 — Control Tower kabulü (WMS-MVP5-DL8, INT-WMS)

- Tarih: 2026-11-02. CT: MVP-5 CT (W-10), oturum `ca7382cf`. Geliştirme: integration-agent rolünde ayrı oturum (aktaran `local_75b9d993`).
- İncelenen: `83b01ef62` (MVP-1 hattı `feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok.
- Paket: `MOD-0178-putaway-picking-packing.md` @ sha256 `9b4be5e3…1decf` (r19, onaylı); kapsam §15, §23 DL-8, AC-25.
- Sonuç: **VERIFIED** — CT doğrulaması geçti. ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).

## Ölçülenler (CT'nin depo dışı detached ağacı, iş sonunda kaldırıldı)

| Kontrol | Sonuç |
|---|---|
| Kapsam | 2 dosya: `gateway/Diten.ApiGateway/ocelot.json` (+38, iki rota bloğu, `/api/recall/{everything}`'in arkasında) ve yeni `Mod0178WarehouseRouteTests.cs`; başka rota ya da dosya yok |
| Rotalar | `/api/warehouse` ve `/api/warehouse/{everything}` → `localhost:5067`; GET / POST / PUT / DELETE / OPTIONS, PATCH yok; port §3 tablosuyla aynı |
| Gateway takımı | 345 / 345 yeşil |
| Canlı | token'sız `GET :5000/api/warehouse/putaway-tasks` → 401, servisin doğrudan cevabıyla aynı (önce 404) |
| CT sabotajı | `{everything}` rotasından DELETE çıkarıldı → 5 test kırmızı; byte-eşit geri alındı, `git status` boş |

Geliştiricinin sabotajları (rapor): iki rota silindi 19 kırmızı; `{everything}` silindi 18; dört başlık tek tek bozuldu her biri 6;
X-Correlation-Id Gateway kodunda (`CorrelationPropagationDelegatingHandler`) yeniden yazıldığından kodun gitignore'lu kopyasında bozuldu.

## CT kararları

- Host `localhost` (pakette `127.0.0.1`): kabul — dosyadaki bütün rotaların düzeni bu; port aynı. Paketin bir sonraki revizyonunda §15 metni hizalanır.
- Kök `/api/warehouse` servisten 404: bilgi notu (serviste kök uç yok; recall ile aynı).
- Kapsama testi 22 uca sabit: S2 (DL-4 / DL-5) uçları eklenince güncellenir — DL-4 prompt'una yazılır.
- Sabotaj sırasında bozuk `ocelot.json` kısa süre canlı Gateway'e yüklendi (yalnız yeni rotalar etkilendi): bundan sonra rota sabotajları canlı klasörde değil ayrı ağaçta yapılır.

## Açık kalanlar

- DL-3 F-3 (AC-45 375 px ekran görüntüsü + §17 E4 çalışma zamanı kanıtı) artık koşulabilir; DL-3 düzeltmesinden sonra CT koşar.
