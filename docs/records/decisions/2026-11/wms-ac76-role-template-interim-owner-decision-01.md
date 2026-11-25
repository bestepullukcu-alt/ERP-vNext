# WMS rol şablonları — AC-76 geçici kabul kararı (Beste, 2026-11-24)

- **Konu:** MOD-0178 AC-76 (a)–(d) Auth kabul kapısı — `WarehouseOperator` / `WarehouseSupervisor` / `ShippingPlanner` varsayılan rol
  şablonları ve Viewer dışlaması; aynı Auth işine bağlı MOD-0180 (dalga rolleri), MOD-0181 (sayım rolleri) ve MOD-0182 (barkod izinleri)
  rol şablonları. Kod sahibi Ali (MOD-0018-FU31, ardından FU32).
- **Karar (seçenek b):** FU31 main'e girene kadar AC-76 kapısı **geçici istisnayla** kabul edilir: test ve canlı kiracılarda gerekli
  izinler Roller → İzinler ekranından **elle atanır**; kabul kayıtları "AC-76 geçici istisna (elle atama), FU31 bekleniyor" diye anar.
  Viewer dışlaması da elle (Viewer rolüne bu izinler atanmaz) doğrulanır.
- **Kapanış:** FU31 (ve 0180/0181/0182 için FU32) main'e girince AC-76 (a)–(d) gerçek şablonla yeniden ölçülür ve istisna kapanır;
  elle verilmiş atamalar o gün gözden geçirilir.
- **Sınır:** Bu kayıt Auth koduna dokunma izni değildir; FU31 / FU32 Ali'nin işidir. Elle atama yalnız Roller → İzinler ekranından,
  kişinin kendi oturumuyla yapılır.
