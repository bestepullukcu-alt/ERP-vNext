---
decision_id: SHIPMENT-BUNDLE-VERSION-ASSIGNMENT-CT-01
status: decided
decided_at_local: 2026-11-25
decided_by: MVP-6 Control Tower (CT), sahibin yetki devriyle
delegated_by: Beste (bestepullukcu-alt, MVP-6 / MOD-0183 / MOD-0186 sahibi, 2026-11-17 devrinden beri)
scope: SHIPMENT-BUNDLE (docs/analysis/contracts/shipment-bundle.openapi.yaml) 3.6.0 ve 3.7.0 numaralarının atanması
---

# SHIPMENT-BUNDLE sürüm ataması — 3.6.0 iade korelasyonu, 3.7.0 depo alımı (CT kararı)

## Yetki

MVP-6 sahibi Beste, iki iş aynı numarayı (3.6.0) beklediğinde numarayı CT'ye bıraktı: **"buna sen karar ver"**
(CT konuşması, 2026-11-25). Bu kayıt o yetkiyle verilen CT kararıdır; sözleşme dosyasını değiştirmez, onay satırı değildir.

## Çakışma

- **MOD-0183 r10 / r11** (onaylı; r11 satırı `6c8c5c95`): DL-5 depo alımı (intake) için SHIPMENT-BUNDLE 3.6.0 öngörüyordu
  (R10-5, R10-7, R10-9; `mod0183-r10-owner-decisions-2026-11-11.md`). Yama dosyası **yazılmadı**.
- **MOD-0186 r12** (çağıran CT'ye göre Ali onaylı; bu çalışma ağacının `execution/approvals/approvals.tsv` dosyasında MOD-0186 r12
  satırı görülmedi — ölçüldü, kayıt yeri bu karara konu değil): iade korelasyon kökü, seçenek C, SHIPMENT-BUNDLE 3.6.0. Öneri ve yama
  **yazıldı ve incelendi**: `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.6.0-PROPOSAL.md` / `SHIPMENT-BUNDLE-3.6.0.patch`
  (taban 3.5.1; `mod-0186-correlation-c-path-owner-decision-01.md`, O-R11-1 seçenek C).

## Karar (CT, 2026-11-25)

1. **3.6.0 = MOD-0186 iade korelasyonu** — mevcut öneri ve yama olduğu gibi kalır; MOD-0186 DL-6 ile uygulanır.
2. **3.7.0 = MOD-0183 depo alımı (intake)** — 25.3.1'in üç kalemi (`intakeWarehouseOutbound`, `ShipmentSummary.outboundNumber`,
   `CustomerRef` tipi) değişmeden 3.7.0 olur; **3.6.0 üzerine** kesilir.
3. Intake yamasının yolu: `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.7.0-intake.patch` (yazılacak; tabanı 3.6.0,
   yani MOD-0186 DL-6'nın yayımladığı dosya).

## Gerekçe

- MOD-0186 yaması yazıldı, incelendi ve onaylandı; MOD-0183 intake yaması henüz yazılmadı. Numarayı hazır olana vermek, incelenmiş bir
  yamayı yeniden yazdırmaz ve yeniden incelemeye sokmaz.
- 3.6.0 yalnız açıklamaları, iki iade 409 örneğini ve iade ek belgesini değiştirir (yol, şema, Shipments gövdesi değişmez); intake
  kalemleri ona bağlı değildir. Bu yüzden intake'in 3.6.0'ın üstüne gelmesi yalnız yama tabanını değiştirir, davranışı değil.
- Sıra tek yönlüdür: intake 3.7.0, 3.6.0 olmadan uygulanamaz; böylece iki yamanın aynı tabana yazılıp çakışması önlenir.

## Sıra ve DocsPathGuard

1. MOD-0183 DL-8 — 3.5.1 (yayımlandı, `20aaffc78`; DocsPathGuard sabiti `8f7564b3…e241`).
2. MOD-0186 DL-6 — 3.6.0 uygulanır; **yeniden sabitleme adım 1:** `8f7564b3…e241` → `6024d3f0…d7bb` (LF, öneri §5), kendi sahip kaydıyla.
3. MOD-0183 DL-5 — 3.7.0 uygulanır; **yeniden sabitleme adım 2:** 3.6.0 özeti → intake yaması yazılınca ölçülecek 3.7.0 özeti.
   Adım 2 hiçbir zaman 3.5.1'den başlamaz.

## Etkilenen paketler ve belgeler

| Belge | Etki | Durum |
|---|---|---|
| `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` | r12: intake 3.6.0 → 3.7.0 (§5 DL-5 istisnası, §13.1, §25, 25.3.1, 25.7, IR-17, 25.9, §26.2, dilim tablosu; yeni §27). Davranış ve AC değişmez | taslak; CT-APR-02 dar inceleme + CT-APR-01 satırı bekliyor |
| `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md` | yok (3.6.0 zaten onun) | değişmez |
| `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.6.0-PROPOSAL.md` / `.patch` | yok | değişmez |
| `docs/roadmap/plans/mvp6-development-plan-v10.3.md` D3 (`3.5.1 → 3.6.0 patch`) ve sahip tablosu | intake'i 3.6.0 diye anıyor | ayrı düzeltme (bu kayıt değiştirmez) |
| `docs/records/audits/2026-11/ct-mvp6-treasury-handover-01/HANDOVER.md:24` | DL-5 kapısında "SHIPMENT-BUNDLE 3.6.0 yama dosyası" | tarihsel devir kaydı; değiştirilmez |
| `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md:2996` (R28-4) | `customerRef` "SHIPMENT 3.6.0" | MOD-0178 sahibinin metni (XMC-001); sahibe bildirilir |
| `execution/domains/supply-chain-execution/work-packs/mvp6/WP-0183-DL8-correlation-root.md:35`, `:111` | "DL-5'in 3.6.0'ı" | DL-8 iş paketi (tarihsel); değiştirilmez |
| `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.5.1-PROPOSAL.md:51` | "r10 planned 3.6.0" | tarihsel öneri; değiştirilmez |

## Sıradaki adımlar

1. MOD-0183 r12 için CT-APR-02 dar inceleme ve CT-APR-01 onay satırı (onaylayıcı r11'deki gibi: `bestepullukcu-alt`). Önerilen satır
   notu: "only DL-5, DL-6, DL-7".
2. MOD-0186 DL-6: 3.6.0 yamasını kodla aynı değişiklikte uygular ve adım 1 yeniden sabitlemesini yapar.
3. 3.6.0 `main`'e girince intake önerisi ve `SHIPMENT-BUNDLE-3.7.0-intake.patch` 3.6.0 tabanına yazılır, incelenir (MOD-0183 25.3.1
   denetimleri), sonra DL-5 iş satırı READY olur (25.7).
4. `mvp6-development-plan-v10.3.md` D3 satırının 3.7.0 olarak düzeltilmesi (ayrı, CT işi); MOD-0178 R28-4 metni için sahibe not.
