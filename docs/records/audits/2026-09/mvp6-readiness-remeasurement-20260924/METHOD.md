# Ölçüm yöntemi v1 — 2026-09-24

Bu bir **ağırlıklı teslimat kilometre taşı endeksidir**. Kalan efor, takvim veya tüm Blueprint ürün kapsamının tamamlanma yüzdesi DEĞİLDİR. Eski ≈48 ile karşılaştırılıp ilerleme farkı çıkarılamaz.

Dokuz modül eşit ağırlıklı. Her kategori dört binary kontrol içerir; kanıtlı=1, açık/belirsiz=0. Her kategori oranı 25×tamamlanan kontrol. Modül endeksi = %10 pack + %10 contract + %30 bounded backend + %20 frontend + %15 integration + %15 Test/VER. Genel endeks dokuz modülün ortalaması. Ağırlıklar bu audit için analistçe seçilmiş yönetim modelidir; owner onaylı kapsam veya efor tahmini değildir. Aynı rubric sonraki turlarda korunmalıdır.

Backend %100 yalnız onaylanmış bounded dilimin dört teslimat kilometre taşını ifade eder; tam backend ürün backlog’unun %100’ü değildir. Fixture/injection acceptance kendi sınırında backend/test kredisi alır, live upstream integration kredisi almaz. UI scope’u bilinmeyen modüllerde dört UI slotu paydada sıfır krediyle tutulur; bu yokluk kanıtı değildir. Shared Auth işi Carrier entegrasyonunu açan dependency’dir, onuncu modül gibi tekrar sayılmaz. Tam kullanıcı golden flow ve E5/G5 ayrı açık kalır.

Pack maddeleri: kimlik/spec, acceptance/paths, izole backend Phase1.5 yetkisi, UI Phase1.5 yetkisi. Common pack frontmatter tek başına izole kabulü geçersiz kılmaz. Contract maddeleri: owned wire baseline, seam haritası, bounded yayın/consent, tüm live seam kararları. Supplier frozen wire ve seam haritası kredi alır; DC kararları almaz. Carrier'ın yeni Auth güvenlik seam'i henüz bağımsız kabul almadığından dördüncü contract/integration maddesi açık tutuldu.

Kapsam sınırı: Mevcut bounded pack ve raporları incelendi; tüm final UI/product backlog henüz ayrıntılı değildir. Dolayısıyla full-finish yüzde hâlâ belirlenemez. Bu endeksin paydası 9×6×4=216 sabit yönetim checkpoint’idir; gerçek iş sayısı veya büyüklüğü değildir.

Doğrulama: Rapor dosyaları ve pack’ler okundu, mevcut byte hash’leri kaydedildi. Test/runtime tekrar çalıştırılmadı; nested raw archive hash’lerinin tamamı yeniden doğrulanmadı. Sonuç, kaynak kodun yeniden bağımsız kabulü değil kayıt-temelli audit’tir. Eski VER'in writer yok ifadesi yeni DEV tarafından aşılmıştır; bağımsız Phase2 kapanışı bu girdilerde henüz yok.

Release sonucu: İncelenen kayıtlarda tam E5/G5 kabulü 0/9. Bu yalnız kapanmış modül kapısı oranıdır; gate alt-maddelerinin %0 yapıldığı anlamına gelmez. Ortak checkout, rollout, live fixture replacement ve UI kapsamı ayrıca açık.
