# MOD-0023 §17 — sahip kararları 04

- Modül: MOD-0023 Workflow Designer (Approvals/SLAs/Escalations) — platform-shared-services
- Karar veren: depo sahibi, MOD-0023 sahibi (K3 / GMG-ERP)
- Kanal: CT · İş akışı sohbeti, butonlu soru (S-92, S-99) — sahibin kendi tıklaması
- Bu kayıt push, PR ya da merge izni vermez.

| # | Konu | Karar | Cevap | Tarih |
|---|---|---|---|---|
| S17-6 | O-S17-3 — Approve / Reject geç gelen özdeş ikiz (paket §17.9/17) | **(i):** ret vermeden önce (task, action, key) kaydı yeniden okunur; kayıtlı ikiz aktöre bağlı olarak 200 `isIdempotent` döner. DL-2 kapsamına girer. Paket metni değişmez; seçim bu kayıttadır. | «Sistem yeniden baksın (Önerilen)» | 2026-10-30 |
| S17-7 | WF-S17-DL1 işçisinin başlatılması | Prompt incelemesi ve dağıtım kapısı geçince ayrı işçi klasörü açılır; işçi test koşar, yerel commit yapar, durur ve CT'ye bildirir. | «Kapıdan geçince başlat (Önerilen)» | 2026-10-30 |

O-S17-3 bu kayıtla kapanır; O-S17-1 (hukuk görüşü) açıktır.
