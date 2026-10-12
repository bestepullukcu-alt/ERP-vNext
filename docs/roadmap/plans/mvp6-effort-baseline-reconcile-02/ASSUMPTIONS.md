# Varsayımlar ve sınırlar

1. **Dönüşüm varsayımı:** 1 kişi-günü = 8 aktif uzman saati. Kaynaklarda tanımlı değildir; karşılaştırma için seçilmiştir.
2. O/M/P değerleri ölçüm veya istatistiksel güven aralığı değildir. Shipment paketi tarafından teslimat bazında verilmiş
   planlama aralığıdır.
3. UI scope hazırlığı tamamlanmış E1 çalışmasıdır. Pack uygulaması, Phase 1.5 kapanışı, frontend, shared integration,
   runtime/browser evidence ve bağımsız VER tamamlanmış sayılmaz.
4. 9.5/16.5/28 gün dört eski Shipment rezervinin **yerine geçer**. Contract ve Backend satırlarına eklenmez.
5. Shared Auth/NumericDate veya genel token doğrulama rework'ü Shipment satırına eklenmez. Shipment VER yalnız hazır ve
   yetkili Auth bağımlılığını tüketme eforunu içerir; Auth ürün düzeltmesi Carrier/SHARED sınırında kalır.
6. Gateway/module registration/navigation değişiklikleri Shipment integration satırında bir kez sayılır. Ortak bir
   implementation başka modüllere de hizmet ederse sonraki baseline bunu owner-attributed shared satıra taşımalı; modül
   ve SHARED altında iki kez tutmamalıdır.
7. Lookup, binary upload, edit/delete/bulk, live producer, stock mutation, E5/G5 ve rollout rezerv dışıdır. Eklenmeleri
   gerçek kapsam değişimi ve ayrı tahmin gerektirir.
8. Diğer sekiz modül ve SHARED satırları R0 değerleriyle byte-for-byte korunur.
9. Hazırlık tamamlanmış olarak işaretlenir fakat ölçülmüş/elicited efor bulunmadığı için yeni delivered saat verilmez.
10. Yuvarlama: modül/kategori yüzdeleri bir ondalık, portföy hassasiyeti tam yüzdeye yuvarlanır; hesaplama yuvarlanmamış
    saatlerle yapılır.
