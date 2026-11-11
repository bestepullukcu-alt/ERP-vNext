---
decision_id: HCM-CT-OWNERSHIP-01
status: decided — İK (human-capital-management) alanında modül → Control Tower dağılımı
decided_at_local: 2026-10-27
decided_by: sahip (GMG-ERP, K3); bu kayıt ad yazmaz
decision_source: "CT · Çalışan kaydı" oturumu (Windows bilgisayar, Claude Code), sahibin o sohbette yazdığı yanıt "dagilim-evet"; CT · Setup sohbetinde sahip aynı soruyu okuyup bu cevaba yönlendirildi; CT · Setup yanıtı kendisi görmedi, "CT · Çalışan kaydı" oturumunun 2026-10-27 bildiriminden aldı
scope: yalnız human-capital-management içindeki üç CT'nin yazacağı modüller; kart §8 satırı bu kayda dayanır
bound_to: taban origin/main 826150ef4b8f27cbfbd4879c8a5f7255c20724db
recorded_by: CT · Setup; bu kayıt kendi adına karar vermez, onay, paket ya da dağıtım izni değildir
---

# İK alanı: hangi Control Tower hangi modülü yazar

| Control Tower | Modül | Not |
|---|---|---|
| CT · Çalışan kaydı (kuyruk önerisi `hcm-employee`) | MOD-0251 Core HR / Employee Master | Çalışan–kullanıcı bağı sözleşmesini tek yazar olarak bu CT yazar |
| CT · İzin ve devamsızlık (kuyruk önerisi `hr-leave`) | MOD-0280 Time, Attendance & Leave Management — izin ve devamsızlık parçaları | Kendi parçası için ayrı alt modül kimliği `verify_module_id.py` ile araştırılır; çalışan kaydına dokunmaz, sözleşmeyi okur |
| CT · Time Entry (duraklatıldı) | MOD-0280-FU01 Time Entry & Weekly Timesheet | Olduğu gibi |
| — | MOD-0298, MOD-0299, MOD-0314 ve diğer İK modülleri | Şimdilik atanmadı |

Seçilmeyen seçenek: "tek-kule" (Çalışan kaydı ve İzin kulesi birleşir). Gerekçe: her modülün tek yazarı olsun, iki iş
paralel ilerlesin.
