---
module: MOD-0173
work_package: WP-MVP1-CLOSURE-01
evidence_level: E4
decision: conditional
commit: 3a4bf51b1
scope: "Stok defteri, hareketler, bakiye / kullanılabilirlik, lokasyon ana verisi ve ekranları; kiracı + şirket kapsamlı, RBAC, merkezi denetim (BL-501)"
next_missing: "PM-LIVE: gerçek MDM PRODUCT-MASTER'a geçiş (G1 madde 1–4)"
---

# CT karar kaydı — MOD-0173 Inventory Ledger & Valuation (MVP-1 kapanışı, 2026-10-21)

**Karar:** `conditional` · **Kanıt seviyesi:** E4 · **Tamamlanma:** WP Done (SOP §29.1; Module Done değildir — paketin
zorunlu kabul ölçütleri ayrıca `acceptance_record.py module-check` ile ölçülür, SOP §29.2).

## Dayanak

- **G1 canlı koşusu (2026-10-20):** madde 5–9 ve 14 / 17 (E4: ekran + API + Mongo + denetim) canlıda geçti; madde 10 2026-10-02 canlı koşusundan (G10G21Z).
- **Koşullu:** G1-D1 (madde 12, şirketler arası sızıntı — merkezî kullanıcı ↔ şirket ataması FU-LE-01 gelene kadar W-0173-02 uzatıldı) ve G1-D2 (madde 1–4 PM-LIVE'da kapanır). İkisi de sahip kararı (2026-10-20).
- Pack §19'daki CT-ACCEPTED notları: S1, S2, K1, LE1, UI1/UI2, LOC1/LOC2, INT1/INT2, OPS1, PM0–PM2, FUB1/FUB2, SYNC1, NAMES1 (INVENTORY 1.5.0), FIX-LIVE3, FIX-LIVE4.

## Kanıt belgeleri (depoda)

- `docs/records/audits/2026-10/mod-0173-g1-closure-measurement-2026-10-02.md`
- `docs/records/audits/2026-10/mod-0173-pm1-evidence-2026-10-09.md`
- `docs/records/audits/2026-10/mod-0173-pm2-evidence-2026-10-09.md`
- `docs/records/audits/2026-10/mvp1-names1-evidence-2026-10-19.md`
- `docs/records/audits/2026-10/mvp1-fix-live3-evidence-2026-10-20.md`
- `docs/records/audits/2026-10/mvp1-fix-live4-evidence-2026-10-20.md`
- `docs/records/audits/2026-10/mvp1-sync1-evidence-2026-10-12.md`
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0173-inventory-ledger-valuation.md (§19 "G1 LIVE RUN")`

## Test koşusu (bu kaydın kanıtı)

Koşu MVP-1 dalının main ile senkron ucunda yapıldı (SYNC4, `3a4bf51b1`; main `dfb5de610` ile 0 commit geride). Tam
assembly, filtre yok. Çıktılar: `docs/records/audits/2026-10/mvp1-closure-rep001/test-run/summary-sync4-3a4bf51b1.txt`, `docs/records/audits/2026-10/mvp1-closure-rep001/test-run/failed-*.txt`, `docs/records/audits/2026-10/mvp1-closure-rep001/test-run/reruns.txt`.

| takım | sonuç | not |
|---|---|---|
| Web | 4061 / 4065 | 4 kırmızı başka ekiplerin (MRP / SafetyStock nav, S&OP, Capacity) — temiz main `dfb5de610`'da da kırmızı |
| Inventory | 1335 + 4 | ilk koşuda 16 Prism soğuk-açılış zaman aşımı; sınıf tek başına 16 / 16 |
| Mimari | 139 / 146 | aynı 7 kırmızı (DocsPath ×5, AuditTrail ×2); hiçbiri Inventory değil |
| Gateway | 313 / 313 | — |
| Platform | 8695 / 8714 | 19 kırmızı bu makinedeki temiz-main tabanıyla birebir (ortam) |
| Auth | tabanla aynı + 1 | o bir test eşzamanlı dosya erişimi; tek başına 7 / 7 |

## Sıradaki tek eksik

PM-LIVE: gerçek MDM PRODUCT-MASTER'a geçiş (G1 madde 1–4).

Kayıt CT tarafından yazıldı (MVP-1 Control Tower); karar ve kanıt seviyesi sahip tarafından onaylandı (2026-10-21).
