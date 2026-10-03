# MVP6-MOD0186-R01-CT-CLOSE-01 — SOP §22 Control Tower kararı

**Tarih:** 2026-09-21  
**Rol:** Control Tower acceptance reviewer  
**CT kararı:** **ACCEPTED — yalnız bounded R01 ürün düzeltmesi ve ona bağlı evidence-package kapanışı**

Bu karar, önceki `MVP6-MOD0186-HTTP-01` raporunda R01 için kalan tek `REWORK` bulgusunu kapatır. MOD-0186 full-module acceptance, E5/G5, gateway/rollout, deployment veya downstream DEV/GO vermez.

## Branch / HEAD / çalışma durumu

- İncelenen repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- CT incelemesi sırasında repository zaten dirty idi (`git status --short`: 152 satır). Bu lane yalnız bu raporu oluşturdu; ürün kaynağına, contract'a, pack'e, guard'a veya git state'ine dokunmadı.
- İncelenen DEV ve bağımsız VER dizinleri repository'de untracked durumdadır; karar onların byte-exact SHA bağlarına dayanır, commit/promotion anlamına gelmez.

## Exact girdiler ve bütünlük

| Girdi | SHA256 / sonuç | CT ölçümü |
|---|---|---|
| DEV `product.patch` | `0bb36d02d3972f65d6644a5b12eaabc7a8820a1a50da13b17769ef7a5148e82c` | DEV ve VER kopyaları byte-identical |
| DEV evidence archive | `12c254757a64e4e5e159ec66d151f86e5a86e1d8d0d6f87b1f846fde0b869b2e` | Bağımsız VER input doğrulamasına bağlı |
| Bağımsız VER raw archive | `1191b5d9c8e0f46a304ce65a131a462143e0c015e2e088de0576258fa8f85473` | Arşiv listesi okunabilir; raw HTTP/DB/process/discarded kayıtları mevcut |
| `source-final-341.tsv` | `60ab3d68de8196d3a390a087884ca46c1e1530d64fa0a72e3074a15d62561052` | 341 veri girdisi doğrulandı |
| Source-file digest list | `17b3aa3c7b3368a18586b0164da4cec53759418c8f81a940686e7bf16e6eda49` | 341/341 target doğrulamasının bağlı listesi |
| Bağımsız `SHA256SUMS` | tüm girdiler `OK` | Rapor, authority, baseline, manifest, patch, raw ve script dosyaları doğrulandı |

Patch, bağımsız CT disposable dizininde iki exact baseline dosyasına yeniden uygulandı. Çıkan target hash'leri:

- `ReturnReferenceReader.cs`: `eaa0aa73d1a2c6e296d57dbfc6ac278cef2081ec1a48cf6510f4b27c0dc2eea5`
- `ReturnReferenceTests.cs`: `c36c462153aadaa3774af592be3a3d9905287a0b1c388a67c718fd594c33f7c7`

`baseline-target-341.tsv`, yinelenmiş iki özdeş header satırı taşır. Header'lar çıkarıldığında tam 341 veri satırı vardır: **2 `CHANGED_VERIFIED`, 339 `UNCHANGED`**. Bu biçim kusuru hash'i veya kapsam sonucunu değiştirmediği için kapanışı engellemez; sonraki paketlerde yinelenen header üretilmemelidir.

## Değişiklik sınırı

Yalnız iki Returns-owned dosya değişmiştir:

1. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs`
2. `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Returns/ReturnReferenceTests.cs`

Patch 65 ek satırdan oluşur; başka source/test yolu değiştirmez. 341-entry snapshot'ta kalan 339 dosya byte-identical'dır. Bağımsız `dev-combined-97-drift.tsv` ve `dev-returns-source-test-manifest.tsv` de Claims, `Program.cs`, shared helper, producer, canonical, guard ve pack alanlarında yeni drift göstermemektedir.

## R01 davranış kararı

| Senaryo | Önce | Exact target | CT sonucu |
|---|---|---|---|
| Shipment detail `500 SHIPMENT_ROOT_INVALID`, `contractVersion: v1` | Returns `503 DEPENDENCY_UNAVAILABLE` | Returns `502 RETURN_SHIPMENT_ROOT_INVALID` | **CLOSED** |
| Başarılı response'ta missing/null root | `503 RETURN_SHIPMENT_ROOT_UNAVAILABLE` | aynı | Korundu |
| Başarılı response'ta malformed root | `502 RETURN_SHIPMENT_ROOT_INVALID` | aynı | Korundu |
| İlgisiz `500`, malformed 500 body, wrong contract version, `501`, producer `401/403` | `503 DEPENDENCY_UNAVAILABLE` | aynı | Korundu |
| Connection refusal / timeout | `503 DEPENDENCY_UNAVAILABLE` | aynı | Korundu |
| Root mismatch + payload drift | root mismatch önceliği | aynı | Korundu |

RED kanıtı exact eski reader ile `expected 502 / actual 503` ve test-process exit `1` üretir. GREEN hedefte 10/10 bounded mapping testi geçer. Composed HTTP kanıtı aynı malformed persisted Shipment için doğrudan producer'da `500 SHIPMENT_ROOT_INVALID`, Returns create'ta `502 RETURN_SHIPMENT_ROOT_INVALID` gösterir. Önceki HTTP raporundaki tarihsel “null” açıklaması değiştirilmemiş, `raw/historical-null-record.txt` ile korunmuştur.

## Source → build → binary → process zinciri

- Exact patch sonrası source tree `source-final-341.tsv` ile 341/341 eşleşir.
- Fresh Release build: 0 warning, 0 error.
- API DLL SHA256: `daeefa6c9b4b2b7159cabcf397852b23c82c962a3b0e18262b4e8032004549b7`.
- DLL mtime: `2026-09-21T19:57:15+0300`.
- Main PID `95316`, başlangıç `2026-09-21 20:04:31 +0300`; restart PID `96056`, başlangıç `2026-09-21 20:07:51 +0300`.
- İki process de build timestamp'inden sonra aynı binary'yi çalıştırır; restart öncesi/sonrası binary hash'i aynıdır.
- API `51863` ve isolated Mongo `27286` kapanış sonrası listener bırakmamıştır.

## Test ve runtime kanıtlarının ayrımı

| Kanıt kümesi | Sonuç | Kabulde kullanım |
|---|---:|---|
| Bounded R01 GREEN | 10/10 | 78 testin alt kümesi; ayrıca toplanmaz |
| Returns namespace core/regression | 78/78 | Core ve mapping regression kanıtı |
| Composed HTTP/DB | 52/52 | Gerçek Kestrel, JWT/RBAC, producer uptake ve isolated Mongo kanıtı |
| Yeni process restart | 4/4 | Kalıcı replay/list, Pending outbox ve no-stock kanıtı |

Bu sayılar farklı katmanlarda örtüşür; **144 benzersiz kabul testi** veya toplam kabul yüzdesi olarak yorumlanmamıştır.

52 HTTP/DB sonucu JWT `401`, RBAC `403`, tenant/LE izolasyonu, soft-delete, gerçek Shipment detail uptake, create/list/transition, replay ve changed-payload, root önceliği, UoM/entitlement, 6+6 ve 4+6 yarışları, snapshot drift, beş ayrı transaction rollback noktası, unknown-commit ve response-loss recovery'yi içerir. Son state 11 return, 9 entitlement, 19 receipt, 19 audit ve 19 Pending outbox'tır; non-Pending ve Inventory/Warehouse/stock collection sayısı sıfırdır.

## Evidence-package disposition

- `scripts/http_probe.py` (`8a15ee...`) ve redacted launch script (`d76bd5...`) kalıcı pakettedir.
- `COMMANDS.tsv`, stdout/stderr, direct numeric probe/DB/listener exit kayıtları, raw HTTP ve DB ölçümleri arşivdedir.
- İlk wrapper'ın bazı RED/GREEN/build/78-test `.exit` dosyaları zsh `PIPESTATUS[0]` indeks hatası nedeniyle boştur. Orijinaller korunmuş; `EXIT-CODE-NOTE.md`, tam log sonuçları ve `COMMANDS.tsv` sonucu bağlamıştır. Kanıt yeniden yazılmadığı ve loglarda sonuç açık olduğu için bu, kapanışı engellemez.
- İki başarısız harness denemesi korunmuş ve PASS sayılarına dahil edilmemiştir:
  1. sandbox VSTest local communication socket açamadı;
  2. ilk 78-test çağrısı `RETURNS_MONGO_URI` olmadan kullanılmayan `27886` portuna gitti.
- Başarılı bağımsız çalışma isolated replica set `returns_r01_ind_ver` / port `27286` kullandı.

## Controlling acceptance matrisi

Önceki `MVP6-MOD0186-HTTP-01` matrisinde overall `PARTIAL / REWORK` sonucunu kontrol eden tek açık satır R01 idi. R02–R11 aynı raporda PASS veya bounded PASS idi. Bağımsız VER, R01'i exact target üzerinde kapattığı için **controlling R01–R11 HTTP acceptance matrisinde açık satır kalmamıştır**.

Bu sonuç aşağıdakileri kapsamaz ve onlar için karar üretmez:

- MOD-0186 full work-package veya full-module CT acceptance kaydı;
- E5/G5 ve cross-module capability acceptance;
- gateway, deployment, rollout veya operasyonel veri doğrulaması;
- worker/publisher aktivasyonu; outbox yalnız Pending olarak kanıtlandı;
- downstream module authorization veya DEV GO;
- commit, push, pack promotion ya da canonical/guard değişikliği.

UI/lookup bu backend-only pack'te mevcut Phase 1.5 kaydına göre N/A'dır; yeni bir UI kabul açığı yaratılmaz.

## Nihai CT disposition

**R01 CLOSED / ACCEPTED (bounded).** Ürün düzeltmesi contract ayrımını dar biçimde uygular, iki Returns-owned dosyayla sınırlıdır ve bağımsız E4 HTTP/DB/restart kanıtıyla doğrulanmıştır. Evidence-package bulguları izlenebilir şekilde korunmuştur; R01 kapanışını engelleyen açık madde yoktur.

**Kalan R01 maddesi:** yok.  
**Daha geniş kabul:** ayrı CT işlemi gerektirir; bu rapor E5/G5 veya downstream GO değildir.

