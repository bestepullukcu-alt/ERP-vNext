# MOD-0023 §17 — sahip kararları 05

- Modül: MOD-0023 Workflow Designer (Approvals/SLAs/Escalations) — platform-shared-services
- Karar veren: depo sahibi, MOD-0023 sahibi (K3 / GMG-ERP)
- Kanal: CT · İş akışı sohbeti, butonlu soru — sahibin kendi tıklaması
- Bu kayıt push, PR ya da merge izni vermez.

| # | Konu | Karar | Cevap | Tarih |
|---|---|---|---|---|
| S17-8 | Paketin saymadığı dördüncü türemiş depo: `WorkflowStartRecoveryStore : TenantRepository<WorkflowInstance>` (`WorkflowStartRecoveryStore.cs:20`; `IWorkflowStartRecoveryStore` ile açık, sealed; devralınan `DeleteAsync`'e bugün çağrı yok). Prompt incelemesi 03 F-1, CT kodda doğruladı. | DL-1 bu yüzden gecikmez. Mimari kural bu depoyu da tarar: arayüzde silme üyesi ya da kaynakta `DeleteAsync` çağrısı eklenirse ve beklenmedik yeni türemiş sınıf çıkarsa kırmızı olur. Override yazılmaz (paket izinli yolu değil); DL-1 raporunda açık kayıt olarak yazılır; kapatma paketin bir sonraki revizyonuna girer. | «Bekçiyle geç, sonra kapat (Önerilen)» | 2026-10-31 |

Etkisi: AC-W1 "no code path deletes" bu kuralla korunur; AC-W15'in override şartı dördüncü depo için bir sonraki revizyona kadar açıktır.
