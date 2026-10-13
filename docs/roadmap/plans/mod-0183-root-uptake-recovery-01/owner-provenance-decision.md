# Owner karar önerisi — ONAYLANMADI

Gerçek önceki kullanıcı root tasarım onayı konuşmada mevcut: “MVP6-SHIPMENT-ROOT-DISPOSITION-01 tasarımını ve
producer-owned contract amendment adayının hazırlanmasını onaylıyorum.” Optional nullable UUID, upgraded producer always emit,
persisted authoritative root, no derivation/backfill ve mevcut read isolation kapsanır. Bu yeni kapsam/Phase1.5/runtime onayı değildir.
Son provenance metni öneridir; onaylanmış default değildir. Eski artifact hashleri yeniden doğrulanmadı.

## Mevcut kaynak dayanağı
- CreateShipmentHandler.cs:22,42 context root'u başlangıç Shipment'a atar.
- ShipmentContextMiddleware.cs:10–27 tek non-nil header ve authenticated trusted scope denetler.
- ShipmentRepository.cs:67–106 aggregate/history/audit/outbox/receipt aynı transaction'da; :123 snapshot/majority.
- Repository:50–58 receipt replay kontrolüdür; arbitrary transition receipt original-root authority değildir.
- Shipment.cs:24 nonnullable Guid; Projection.cs:19–26 bugün public root alanını emit etmiyor.
Bu yollar services/Diten.SupplyChainService/src/ altındaki ilgili Api/Application/Domain/Persistence katmanlarındadır;
exact hashler input-manifest.json içindedir. Kaynak garantisi belirli legacy kaydın geçmişini kanıtlamaz.

## Önerilen response tablosu
Otherwise-valid, görünür, yetkili detail için:
| Raw state / provenance | Öneri | Sınır |
|---|---|---|
| Missing |200 explicit null|Default Guid root sayılmaz|
| BSON null |200 explicit null|No repair|
| Malformed UUID/wrong BSON type |200 explicit null|Yalnız root toleransı; safe classification|
| Authoritative gerçek nil |200 nil UUID string|Nil olması otoriteyi yok etmez; kontrollü kanıt gerekir|
| Authoritative geçerli UUID |200 saklanmış UUID|Parse değil producer/source geçmişi otorite sağlar|
| Parse edilebilir, kökeni bilinmeyen/çelişkili |Rollout disposition açık|Runtime bu geçmişi algılayamıyorsa per-record null/failure vaat edilmez|

## Kopyalanabilir tek karar metni
> Presence-aware detail tasarımını ve aşağıdaki üç ayrı kapıyı tasarım tercihi olarak onaylıyorum.
> Missing/null/malformed root explicit null; provenance'ı doğrulanmış UUID nil dahil aynen korunur.
> Parse başarısı historical-authority değildir. Yeni runtime per-request history/receipt taraması veya provenance storage eklemez.
> Canonical publication ayrı contract-owner/guard sürecidir. Exact approved contract hedefiyle açıkça yetkilendirilmiş izole
> DEV/VER çalışması, operasyonel legacy incelemesi yapılmadan yürütülebilir; bu çalışma canonical uptake veya rollout değildir.
> Hedef veri kümesinin root provenance'ı deployment öncesinde ayrı data-owner disposition gerektirir. Bilinmeyen/çelişkili geçmiş
> runtime tarafından ayrılamıyorsa per-record null vaat edilmez. Mevcut uygulanabilir bir hedef ayrımı yoksa etkilenen deployment
> bekler; tüm modülün spec/izole fixture çalışması engellenmez. Fixture provenance'ı operasyonel veri kanıtı değildir.
> Yeni catalogue/storage marker, backfill/migration veya history okuyucusu ayrı scope ve yetki ister.
> Bu karar yalnız tasarım içindir; Phase1.5, runtime yazımı, canonical publication, guard, pack promotion ve DEV GO açmaz.

Alternatif: runtime per-record provenance resolution mevcut tek-document read tasarımını genişletir; authoritative kanıt kaynağı,
consistency, retention ve failure politikası ile ek exact paths onayı gerektirir. Bu pakete eklenmedi.
