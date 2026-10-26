---
decision_id: PVG-MOD0230-DAGITIM-PLANI-01
status: approved — sahip Control Tower'ın ilk dalga planını kabul etti (2026-10-23, sohbette "plan-onay"); (a) ve (b) için CT önerileri uygulanır
prepared_at_local: 2026-10-23
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
authority: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 10fdbe5c6a5b… (onaylı; `pack_approval.py check` çıkış 0 @ 95563dae1, onaylayan bestepullukcu-alt, 2026-10-23) — "Teslim dilimleri" DL-1…DL-9
scope: MOD-0230 geliştirmesinin ilk dalgası için kuyruk satırları; dağıtım izni değildir (her satır READY olmadan ve CT-PQG-01 çıkış 0 olmadan dağıtılmaz)
---

# MOD-0230 — ilk dalga dağıtım planı ve eski iş paketlerinin yerine geçmesi

## Olgu (ölçüldü, 2026-10-23)

- MOD-0230 `main`'de var ama bitmemiş: `services/Diten.PvgService` (4 katman, 117 C# dosyası, 5 test süiti; son kod
  değişikliği 2026-08-25), bellek içi depo, yalnız "hepsini reddet" adaptörleri, 4 Gateway route'u (kimlik doğrulama
  ayarı yok), kiracı ekranı (`[HasPermission]` yok), yazılmayan denetim niyeti. Ayrıntı:
  `docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md` @ afe613de8.
- PVG kuyruğunda (`execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv`) yalnız inceleme ve bağımlılık takibi
  satırları var (PVG-M1-01…15); geliştirme iş paketi satırı yok.
- `execution/domains/pharmacovigilance/work-packs/WP-0230-01…08` dosyaları paketin eski hâline göre yazılmıştır (Mongo
  kalıcılığı, eski CQRS biçimi, başka bir geliştiricinin klasör yolu); kuyrukta satırları yoktur. Onaylı paket bunların
  bir kısmıyla çelişir (ör. DL-2 "Mongo koleksiyonu, indeks, migration kapsam dışı"; WP-0230-03 Mongo kurar).

## Kararlar

| # | Karar (sahip, 2026-10-23) |
|---|---|
| DP-1 | İlk dalga dört iş paketidir; dilim, kabul ölçütü ve yollar paketin "Teslim dilimleri"nden birebir alınır: **WP-0230-DL2A** (DL-2: kayıt sürümü ve eşzamanlılık; BG-12, BG-13), **WP-0230-DL2B** (DL-2: alım alanlarının ölçülebilir kısmı; BG-21, BG-23, BG-25, BG-27, BG-29; DL2A'dan sonra, aynı dosyalar), **WP-0230-DL3** (DL-3: host açılışı; BG-8, BG-9; BG-10 OD-PVG-6 Kurumsal Mimari onayından sonra), **WP-0230-DL8** (DL-8: Gateway route'ları; BG-16; yalnız `integration-agent`). |
| DP-2 | DL-2 ikiye bölünür (b): iki parça aynı dosyalara dokunur, aynı hatta sırayla gider. |
| DP-3 | Sözleşme ya da onay bekleyen kabul ölçütleri (BG-22 MOD-0290, BG-24 MOD-0288, BG-26 MOD-0048, BG-28 QPPV, BG-30 MOD-0150 / MOD-0288) ilk dalgaya girmez. |
| DP-4 | Eski `WP-0230-01…08` dosyaları **dağıtılmaz** ve değiştirilmez (a); yerlerini bu kayıttaki iş paketleri ve sonraki dalgalar (DL-4, DL-5, DL-6, DL-7) alır. Eşleme: WP-0230-01/02/03 → DL-2 / DL-3; WP-0230-04 → DL-4; WP-0230-05 → DL-2…DL-5'in hata yolu testleri; WP-0230-06 → DL-8; WP-0230-07 → DL-7; WP-0230-08 (blocked, manifest) → paketteki "Control Tower: WP-0230-08 manifest kararı" ile açık kalır. |
| DP-5 | PVG-M1-02 `INSPECTION_REQUIRED → INSPECTED` (kanıt: inceleme notu @ afe613de8). Notun paketin bildirdiği iki yanlışı ayrı bir kayıtla düzeltilir (kayıtlar commit'ten sonra değişmez). PVG-M1-03 (operasyonel runtime sahip onayları) açık kalır; DL-9'un konusudur. |
| DP-6 | Yeni satırlar `PLANNED` başlar; `READY`'ye DoR (kart §1) ve hazır prompt ile geçer. Kuyruk değişikliği tek PR ile `main`'e girer; prompt kapısı (`prompt_gate.py check`, risk HIGH) dağıtım kipinde ancak bundan sonra koşar. |
| DP-7 | Uygulamayı / geliştirme sunucusunu aynı anda yalnız bir CT çalıştırır (kart §7a/5): DL8'in canlı denemesinden önce sahibe sorulur. DL-7'den önce MOD-0286 (UI tasarım sistemi) durumu ölçülür. |
| DP-8 | Kuyruktaki PVG-M1-02 / M1-03 sahip alanı paketin istediği rol etiketiyle hizalanır: "PVG paket sahibi". |

## Yerini alan / yerine geçen

- `execution/domains/pharmacovigilance/work-packs/WP-0230-01…08` → bu kayıttaki DP-4 eşlemesi (dosyalar silinmez).
