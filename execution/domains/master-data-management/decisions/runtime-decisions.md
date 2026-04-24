# MDM — Runtime Decisions

> Domain seviyesinde operasyonel yürütme kararları.

## Kararlar

- **Karar:** MDM bootstrap aşamasında modül planı Excel parser çıktısı ile birebir hizalanır.
  - **Tarih:** 2026-04-15
  - **Gerekçe:** Tek kaynak üzerinden modül kapsamını başlatmak ve belirsizliği azaltmak.
  - **Uygulama:** Her kaynak modül için ayrı `MDM-XXX` module pack oluşturulur.

- **Karar:** Modül yürütmesi wave önerisi korunarak ilerletilir.
  - **Tarih:** 2026-04-15
  - **Gerekçe:** Bağımlı modüllerde sıralama riskini azaltmak.
  - **Uygulama:** Module pack içinde `suggested_wave` bilgisi başlangıç planı olarak tutulur.
