# MOD-0181 — sahip kararı (2026-11-12)

- Sahip: Beste (bestepullukcu-alt, MVP-1 / MVP-5 merkez). Kayıt: MVP-5 WMS CT (oturum `ca7382cf`), sohbetteki sahip sözünden («evet ikisine de evet», 2026-11-12).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md` r8 (sha256 `f943ab48c0bcdcab512cc1fd21dd80e0710649b29bb520fb90bae2f840eaa856`).
- Kaynak: r8 bağımsız incelemesi `docs/records/reviews/MOD-0181/2026-11-11-f943ab48.md`, F-4.

| # | Karar | Gerekçe | Pakette |
|---|---|---|---|
| D-1 | **Onaylayan Ali (`GMG-ERP`).** MOD-0181 r8'in CT-APR-01 onay satırında onaylayan Ali'dir. Satırları Beste'nin hesabı yazdığı için PR'ı da Ali birleştirir. | EES 2.14: yazan ≠ onaylayan; MOD-0178 D-6 ve MOD-0262-FU01 D-3 emsali. | §18 r8 kapısı |

## Ek — 2026-11-13

- **K-2 (Beste, "olur … ikisi de"; kaynak: sahip sohbet cevabı):** mobil sayım cevabındaki (`execution/domains/supply-chain-execution/work-packs/mobile/2026-11-13-cycle-count/WMS-CEVAP-2026-11-13-sayim.md`)
  soru 2 ve 3 için WMS-COUNT-BUNDLE eklemeli **1.2.0** açılır: `CycleCountLine`'a `locationCode`, `skuCode`, `lotNumber` (dil kuralıyla
  `skuName`), liste ucuna `assignedTo` / `unassigned=true` süzgeci. Rol yetkisi genişletilmez. MOD-0181 için yeni FU açılır (kimlik
  DCP-002 ön kontrolüyle). 1.1.0 FROZEN değişmez.
- **K-1 (Beste, aynı cevap):** MVP-1 hat dalı GitHub'a gönderildi — önce origin/main birleştirildi (onay defterleri: main satırları önce,
  ardından hat satırları; main ve eski uzak hat ucu ön ek olarak doğrulandı), `a9fd192ba..a1aba4066`, fast-forward, force yok. Main'e PR
  değil. Etiket `ct/line-ff-2026-11-13h`.
