# MVP6 — efor ilerleme güncellemesi R2

**Cut-off:** 2026-09-24. **Verdict:** Yeni kanıtlar baseline toplam ağırlığını değiştirmeden gerçek teslimat ilerlemesine bağlandı.

Predecessor `mvp6-effort-baseline-reconcile-02` korunmuştur. Bu successor yalnız raporlama ve aritmetik uzlaştırmadır; ürün veya Git değişikliği yapmaz.

## Portföy sonucu

- Delivered most-likely: **1292 → 1372 saat** (`+80`).
- Remaining most-likely: **1512 → 1432 saat** (`-80`).
- Toplam most-likely tahmin: **2804 saat**, değişmedi.
- Efor ağırlıklı tamamlanma: **%46,1 → %48,9**.
- O/M/P toplam payda değişmedi. Bu ilerleme tahmin artışı değil, mevcut rezervlerin kanıtlı teslimata aktarımıdır.

80 saatlik transfer iki mevcut teslimattan gelir: Shipment bounded frontend uygulaması `64 saat`, shared NumericDate uygulaması `16 saat`. Dosya veya test sayısı katsayı olarak kullanılmamıştır.

## On teslimat başlığı

| Başlık | Durum | Delivered / remaining / total ML saat | Genel ilerleme | Yeni kanıtın etkisi |
|---|---|---:|---:|---|
| 0183 Shipment/POD | PARTIAL — ilerledi | 204 / 90 / 294 | **%69,4** | Bounded UI ve odaklı testler tamamlandı; composed build/runtime/browser/bağımsız VER açık. |
| 0184 Carrier | PARTIAL — değişmedi | 160 / 48 / 208 | **%76,9** | NumericDate ortak Auth eforudur; Carrier satırında tekrar sayılmadı. Real-Auth E2E hâlâ ayrı kapı. |
| 0185 Loads | PARTIAL — değişmedi | 136 / 134 / 270 | **%50,4** | Yeni kanıt yok. |
| 0186 Returns | PARTIAL — değişmedi | 172 / 106 / 278 | **%61,9** | Yeni kanıt yok. |
| 0187 Claims | PARTIAL — değişmedi | 200 / 114 / 314 | **%63,7** | Yeni kanıt yok. |
| 0190 S&OP | PARTIAL — değişmedi | 148 / 136 / 284 | **%52,1** | Yeni kanıt yok. |
| 0192 Capacity | PARTIAL — değişmedi | 220 / 144 / 364 | **%60,4** | Yeni kanıt yok. |
| 0147 Supplier Performance | PRE-DEV / PARTIAL | 20 / 248 / 268 | **%7,5** | Yeni kanıt yok; owner/seam kapıları korunuyor. |
| 0148 Supplier Portal | PRE-DEV / PARTIAL | 20 / 232 / 252 | **%7,9** | Yeni kanıt yok; owner/seam kapıları korunuyor. |
| Ortak işler | PARTIAL — ilerledi | 92 / 180 / 272 | **%33,8** | NumericDate uygulaması teslimata taşındı; F-01/F-02 bağımsız kapandı; gerçek Auth login/refresh açık. |

## Modül ve kategori yüzdeleri

| Kapsam | Pack/tasarım | Contract | Backend | Frontend | Entegrasyon | Test/VER | Genel |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0183 Shipment/POD | 66,7% | 72,7% | 80,0% | **100,0%** | 33,3% | 47,1% | **69,4%** |
| 0184 Carrier | 80,0% | 80,0% | 92,3% | 76,9% | 66,7% | 60,0% | **76,9%** |
| 0185 Loads | 72,7% | 66,7% | 80,0% | 0,0% | 20,0% | 57,1% | **50,4%** |
| 0186 Returns | 76,9% | 83,3% | 90,0% | 0,0% | 50,0% | 66,7% | **61,9%** |
| 0187 Claims | 76,9% | 85,7% | 91,7% | 0,0% | 50,0% | 70,6% | **63,7%** |
| 0190 S&OP | 71,4% | 83,3% | 84,2% | 0,0% | 27,3% | 57,1% | **52,1%** |
| 0192 Capacity | 75,0% | 85,7% | 89,7% | 0,0% | 38,5% | 66,7% | **60,4%** |
| 0147 Supplier Performance | 42,9% | 33,3% | 0,0% | 0,0% | 0,0% | 0,0% | **7,5%** |
| 0148 Supplier Portal | 42,9% | 33,3% | 0,0% | 0,0% | 0,0% | 0,0% | **7,9%** |
| Ortak işler | N/A | 0,0% | **100,0%** | 0,0% | 0,0% | 23,8% | **33,8%** |
| MVP6 toplam | 66,7% | 60,8% | 72,4% | **20,3%** | 25,2% | 47,3% | **48,9%** |

## NumericDate eşlemesi

`mvp6-carrier-numericdate-exec-01` exact iki dosyalı uygulamayı writer-complete yaptı. Bu, `S1-REMAINING` içindeki F-02 implementation/authority/application rezervini tamamen kapatır: O/M/P `9,6/16/32` saat `S1-DELIVERED` satırına aktarıldı.

`mvp6-carrier-numericdate-exec-ver-01` F-01 cardinality ve F-02 raw-JSON NumericDate typing'i bağımsız kapattı. Ancak `S2-REMAINING` tek ağırlıkta hem F-02 doğrulamasını hem de tamamlanmayan gerçek Auth login/refresh/re-resolution işini taşır. Kanıt bu 16 saatin güvenilir alt dağılımını vermiyor. Bu nedenle F-01/F-02 kapanışı nitel olarak delivered satırına bağlandı, fakat ek sayısal kredi üretilmedi; kalan 16 saat gerçek Auth zinciri için korunmuştur.

Bu shared çalışma 0184 Carrier backend veya Test/VER satırlarına yazılmadı. Böylece ortak Auth eforu iki kez sayılmadı.

## Shipment UI eşlemesi

`mvp6-shipment-pod-ui-exec-01` exact bounded list/create/detail/transition/POD UI, Compact yüzey, yedi dil ve ilgili odaklı controller/form/JavaScript kontrollerini tamamladı. Mevcut `0183-4-REMAINING` teslimatı tam olarak bu kapsamı tanımladığı için O/M/P `40/64/104` saat delivered duruma çevrildi. Bu kredi 28 dosya veya 14 test sayısından türetilmedi; baseline'da önceden tahmin edilmiş teslimatın kapsam kapanışına dayanır.

Aşağıdakiler tamamlanmış sayılmadı:

- composed SupplyChain build;
- runtime ve browser;
- bağımsız VER;
- E4/E5/G5 veya full-module kabulü;
- pack/Phase 1.5, integration ve Test/VER satırları.

İzole frontend build sonucu mevcut olsa da bu successor build teslimatına ayrı kredi vermez. Controlling birleşik build 23 missing-namespace hatasıyla, Carrier gateway port testi ise 5065/5061 çatışmasıyla kapalıdır.

## Yeni entegrasyon rework

Shipment uygulaması iki exact rework ihtiyacını ortaya çıkardı:

1. Carrier predecessor `Program.cs` hedefinin yetkili transfer kapsamı dışındaki feature ailelerine bağımlılığı;
2. CRM gateway patch'inin Carrier rotalarını onaylı 5061 yerine 5065'e taşıması.

Bu işler tamamlanan UI kredisinden ayrıdır ve `REWORK-REGISTER.tsv` içinde tutulur. Mevcut `0183-5-REMAINING` 24 saatlik integration rezervi korunmuştur. Yeni bulgular için güvenilir O/M/P tahmini bulunmadığından toplam tahmine ek saat uydurulmamıştır; `0` değişim burada “iş yok” değil, “estimate pending” anlamındadır.

## Tahmin ve ilerleme ayrımı

| Sınıf | Delivered ML değişimi | Remaining ML değişimi | Toplam tahmin değişimi |
|---|---:|---:|---:|
| Kanıtlı Shipment UI teslimatı | +64 | -64 | 0 |
| Kanıtlı shared NumericDate uygulaması | +16 | -16 | 0 |
| Bağımsız F-01/F-02 kapanışı | 0 sayısal; nitel kapanış | 0 | 0 |
| Yeni Shipment integration rework | 0; estimate pending | 0 | 0 |
| **Toplam** | **+80** | **-80** | **0** |

Readiness checklist ve release gate değerleri bu efor yüzdesinden ayrıdır. Build/runtime/browser/independent VER kapıları kanıtsız kapatılmadı.
