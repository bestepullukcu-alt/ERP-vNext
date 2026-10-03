# MVP6 effort baseline R1 — Shipment UI reconciliation

Bu successor, `mvp6-effort-baseline-01` yöntemini ve 103 satırlık efor paydasını korur. R0 değişmez. Yalnız
MOD-0183 Shipment/POD satırları, son UI scope paketinin teslimat bazlı tahminiyle uzlaştırılmıştır.

## Birim ve dönüşüm

R0 birimi “consistent reference pace at equivalent specialist active hours” olarak tanımlar; kişi-gününün kaç saat
olduğunu tanımlamaz. Shipment UI paketi ise kişi-günü kullanır. Bu rapor karşılaştırma yapabilmek için
**1 kişi-günü = 8 aktif uzman saati** varsayar. Bu bir bordro, ölçülmüş çalışma süresi, ajan runtime'ı, takvim günü
veya kurumsal kapasite standardı değildir. Sonraki owner standardı farklıysa yalnız dönüşüm ve ona bağlı R1 satırları
yeniden hesaplanmalıdır.

| Shipment teslimatı | Gün O/M/P | Saat O/M/P | EFFORT.tsv karşılığı |
|---|---:|---:|---|
| Pack kararı/uygulaması ve UI Phase 1.5 | 0.5 / 1.0 / 2.0 | 4 / 8 / 16 | `0183-1-REMAINING` yerine geçer |
| UI-owned uygulama ve yedi dil | 5.0 / 8.0 / 13.0 | 40 / 64 / 104 | `0183-4-REMAINING` yerine geçer |
| Shared integration | 1.5 / 3.0 / 5.0 | 12 / 24 / 40 | `0183-5-REMAINING` yerine geçer |
| Composed runtime ve bağımsız browser VER | 2.5 / 4.5 / 8.0 | 20 / 36 / 64 | `0183-6-REMAINING` yerine geçer |
| **Toplam** | **9.5 / 16.5 / 28.0** | **76 / 132 / 224** | Dört eski rezervin yerine geçer; üzerine eklenmez |

Contract ve Backend satırları bu UI tahmininin parçası değildir ve değişmedi. Shared Auth düzeltmesi de Shipment
integration/VER tahminine gömülmedi; R0 `SHARED` ve Carrier bağımlılık sınırları korunmuştur.

## Tamamlanan hazırlık ve kalan iş

UI scope hazırlığı; contract parity, Compact kararı, exact acceptance, owned-path ve shared handoff belgeleriyle E1
olarak tamamlandı. Bunun için ölçülmüş veya ayrıca elicited bir efor değeri yoktur; bu nedenle yeni delivered saat
uydurulmadı. R0 delivered Pack/tasarım kredisi `16 h` kaldı. Kalan Pack/tasarım rezervi `6 → 8 h` olarak, yalnız owner
kararı, pack uygulaması ve kanıtlı UI Phase 1.5 kapanışını kapsayacak biçimde değiştirildi; hazırlık raporlarının yeniden
üretilmesi değildir.

Frontend, integration ve VER henüz uygulanmadı. Bunların yeni 64/24/36 saatlik most-likely değerleri eski
56/32/24 saatlik belirsiz rezervlerin yerini alır. Dosya, test, prompt veya rapor sayısı katsayı olarak kullanılmadı.

## Değişim sınıflandırması

| Tür | Delivered etkisi | Remaining etkisi | Toplam tahmin etkisi | Yorum |
|---|---:|---:|---:|---|
| Tamamlanan iş | 0 h | 0 h | 0 h | Hazırlık E1 olarak tamamlandı; kaynak efor vermediği için sayısal kredi uydurulmadı |
| Tahmin düzeltmesi | — | +14 h | +14 h | Exact UI teslimatları eski genel rezervlerin yerini aldı |
| Kapsam değişimi | 0 h | 0 h | 0 h | İlk kullanılabilir bounded UI zaten R0 rezervindeydi; upload/lookup/edit/delete/bulk/E5/G5 eklenmedi |

Shipment: Delivered `140 → 140`, Remaining `140 → 154`, toplam `280 → 294`. Portföy: Delivered
`1292 → 1292`, Remaining `1498 → 1512`, toplam `2790 → 2804` saat. Orandaki düşüş tahmin paydasının
net büyümesidir; tamamlanmış işin geri alınması değildir.

## Ayrı yönetim göstergeleri

Readiness R1'in **%69.8 development checklist** (`44/63`) ve **%31.1 release gates** (`14/45`) değerleri binary/
rubric tabanlı yönetim göstergeleridir. Bu raporun saat-ağırlıklı **%46.1** değeriyle toplanmaz, çıkarılmaz veya
yüzde-puan ilerlemesi olarak karşılaştırılmaz.
