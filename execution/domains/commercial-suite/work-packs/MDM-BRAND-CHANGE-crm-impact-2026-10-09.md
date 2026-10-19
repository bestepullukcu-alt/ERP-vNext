# MDM Marka değişikliği — CRM etki analizi (CT, 2026-10-09)

> **Kaynak:** MDM ekibinin notu (2026-10-09, kullanıcı iletti). Marka değişiklikleri MDM geliştirme dalında; bir sonraki birleşik sürümle (tek PR) gelecek, henüz canlıda değil.
>
> **CT kod taraması:** `services/Diten.CrmService/src/**`, `frontend/Diten.Web/{Controllers,wwwroot/assets/js}/CRM/**`, Auth seed, ocelot.

## 1. MDM'de ne değişiyor (özet)
| Değişiklik | CRM'e etkisi |
|---|---|
| Marka ve ürün izinleri ayrı modüle taşındı (`brand-product-master`, 9 anahtar) | Kiracı planında "Marka" modülü yoksa `mdm.brands.read` / `mdm.products.read` verilemez. CRM'in doğrulaması o durumda **403 → "MDM'ye ulaşılamadı"** olarak düşer ve kayıt yazılmaz. |
| Yeni bağ kuralı: yalnız Etkin + geçerlilik tarihi içindeki markaya bağ kurulabilir (`isLinkable`) | CRM bugün yalnız "kayıt var mı" diye bakıyor. Arşivli ya da süresi geçmiş marka da geçerli sayılıyor. |
| Marka detayına `isLinkable`, `liveProductCount` eklendi | CRM için yeni bir okunacak alan. |
| Güncelleme ve arşivde `expectedVersion` zorunlu | CRM marka yazmıyor; etkisi yok. (Web'deki Ana Veri Marka / Ürün sayfaları MDM ekibinin; onu MDM PR'ı düzeltir.) |
| Liste sayfalı (`start` / `length`); eski çağrı çalışır | CRM seçicileri `?pageSize=200` gönderiyor. Bu MDM'nin parametresi değil, yok sayılır; eski çağrı gibi davranır. Sürüm gelince doğrulanacak. |
| Yeni hata kodları, sözleşme ucu `reasonCodes` | CRM MDM kodlarını sabit kodlamıyor (yalnız 404 / 2xx / geçici hata ayrımı). Etkisi yok. |
| 503 `store_unavailable` / `write_outcome_unknown`: geçici, bir kez yeniden dene | **Zaten var:** segment doğrulayıcı 502 / 503 / 504'te bir kez yeniden deniyor. |
| "Ürünler" (Marka altı) ≠ Global Ürün; birleştirme kararı bekleniyor, yeni bağımlılık eklenmesin | CRM'de bu kayda bağlı **4 eski yer** var (aşağıda). Yeni işlerde **yalnız Global Ürün / GSKU** kullanılacak. |

## 2. CRM bugün neyi kullanıyor
| Yer | Ne kullanıyor | Marka değişikliğinden etkisi |
|---|---|---|
| **Segment kuralları:** `consent.scope-brand`, `consent.scope-product` | Seçici `api/mdm/brands`, `api/mdm/products` (Web vekili `mdm.brands.read` / `mdm.products.read`). Kayıtta `MdmSegmentProductReferenceValidator` → `GET api/mdm/brands/{id}`, `products/{id}` (kapalı-güvenli) | **Evet:** izin ve plan şartı; `isLinkable` önerisi |
| **Ziyaret Sıklığı Politikası:** marka ve ürün boyutu | Seçici `api/mdm/brands`, `api/mdm/products` (`mdm.brands.read`, yedek yetkiyle). Sunucuda doğrulama YOK; çözümleyici `BrandId` / `ProductId` eşleştiriyor | **Evet:** izin; `isLinkable` önerisi; "Ürünler" bağımlılığı |
| **Kampanya:** `BrandId`, `ProductId` alanları | FU10'dan beri ekranda yok; alanlar duruyor | Yeni giriş yok; eski veri |
| **Bilgi içeriği:** `BrandId` | Ekranda yok (marka kaldırıldı); `ProductId` = **Global Ürün** | Marka: eski veri. Ürün: etkilenmez |
| **Strateji şablonu, ziyaret planlama, ürün adları (4G), iddialar, güvenlilik metni, yolculuk** | **Global Ürün + GSKU** (`mdm.global-products.read`, `mdm.gskus.read`) | **Etkilenmez:** taşınan izinler arasında değil |
| **Auth seed (yerel):** 97c5 rollerine `mdm.brands.*` / `mdm.products.*` verme | `DataSeeder` | MDM PR'ı modül eşlemesini değiştiriyor; main senkronunda **çakışma riski** |

## 3. Yapılacaklar
### A. Zorunlu (kod değil — sürümle birlikte)
1. **Plan:** kiracı planlarına "Marka" modülü. MDM ekibi yapacak, canlıdan önce. **CT:** MDM PR'ı main'e girip senkronlandıktan sonra 97c5'te segment kaydını (marka koşulu) ve Ziyaret Sıklığı seçicisini canlı dener.
2. **Roller:**
   - Hazır Admin ve Viewer rolleri izinleri modülle birlikte alır.
   - Özel CRM rollerinde (segment yazarı, sıklık politikası yöneticisi, pazarlama) `mdm.brands.read` ve `mdm.products.read` **elle** eklenir. Bunu kullanıcı ekrandan yapar.
   - **Saha temsilcisi bu izinlere ihtiyaç duymaz:** marka / ürün seçmiyor; ürün adları Global Ürün'den geliyor.
3. **Main senkronu:** MDM PR'ı Web Ana Veri sayfalarına, Auth seed'e ve ocelot'a dokunuyor. Senkronda bu dosyalarda MDM'nin hali alınır; CRM tarafında bir şey kaybolmamalı.

### B. Öneriler → küçük paket **WP-MDM-BRAND-1** (MDM PR'ı main'e girdikten sonra)
1. **Seçicide aynı kural:** Segment ve Ziyaret Sıklığı marka seçicileri yalnız "Etkin + geçerlilik içinde" markaları listeler (liste süzgeci ya da `isLinkable`). Kayıtlı ama artık bağlanamaz bir marka seçili gelirse "pasif" rozetiyle görünür, kaybolmaz.
2. **Kayıtta aynı kural:**
   - segment kuralına **yeni ya da değişen** marka değeri `isLinkable = false` ise reddedilir → `409 segment_brand_not_linkable` (yeni kod);
   - değişmeyen eski değer yeniden doğrulanmaz (MDM: "var olan bağlar bozulmaz");
   - Ziyaret Sıklığı Politikası'na marka ve ürün için de aynı varlık + bağlanabilirlik doğrulaması eklenir (bugün hiç doğrulanmıyor).
3. **"Ürünler" envanteri:** yukarıdaki 4 eski yer bir sabit test listesinde tutulur. Yeni bir `api/mdm/products` bağımlılığı eklenirse mimari test kırmızı olur ("yeni bağımlılık ekleme" kuralı).
4. Sözleşme ucundan kod okuma: CRM MDM kodlarını sabit kodlamıyor, iş yok.

**Prompt sayısı:** 1 (BE + Web küçük; aynı ajan). Yeni yazma komutu yok; mevcut komutlara doğrulama eklenir.

### C. Ziyaret Çalışma Alanı planına etkisi
- **W4 (K-W6):** endikasyon × hasta profili ve rakip verisi **Global Ürün**'e bağlanmalı, "Ürünler" kaydına değil. K-W6 sorulurken bu bilgi verilecek.
- **W6 (K-W10):** ön sipariş satırları **GSKU**'ya bağlanmalı; fiyat kaynağı da buna göre. "Ürünler" kaydı kullanılmaz.
- **W2 / W3:** etkisi yok (zaten Global Ürün).
- Birleştirme kararı (Marka → Global Ürün) çıkınca: Segment `consent.scope-product` ve Ziyaret Sıklığı ürün boyutunun geçiş planı MDM ile birlikte yazılır.
