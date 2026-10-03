# Kaynak ve doğrulama kaydı

2026-09-23; scope spec-only. Yeni readiness audit yapılmadı; mevcut `../mvp6-supplier-seam-readiness-01/` decision set, seam matrix, source map ve boundaries tüketildi. Bu pakette production davranışı PASS veya hazır ilan edilmez.

## Birincil kaynaklar

Repo köküne göre yollar; exact byte hash'leri INPUT-HASHES.tsv içinde.

| Kaynak | İncelenen hüküm / öneriye etkisi |
|---|---|
| docs/analysis/contracts/supplier.openapi.yaml | info owner MOD-0140, SUPPLIER1.0.0; getSupplier, validateSuppliers, SupplierStatus; Tenant scope mevcut, LE ve actor-binding yok. results required değil; known=false example status:null enum ile uyuşmuyor |
| docs/analysis/contracts/supplier-performance.openapi.yaml | Portal actor-derived Supplier; create additionalProperties:false; mevcut statuses, decimal string; create201/replay200, If-Match409, lifecycle422, identity403/base503. Metric revision/formula ve risk transition matrix belirtilmiyor |
| execution/domains/supply-chain-execution/module-packs/MOD-0147-supplier-performance-risk.md | Draft owned evaluation/scorecard/SupplierRisk, Metric/Risk consumer, decimal0..100 ve publication immutability niyeti |
| execution/domains/supply-chain-execution/module-packs/MOD-0148-supplier-portal.md | Draft actor mapping, replay index LE/target/actor içermiyor; trimmed-key hükmü. Bu paketin tuple/key değişikliği açık öneri, uygulanmış değil |
| execution/domains/supply-chain-execution/domain-config.md | Tenant+LE server scope ve Supplier modülleri için ownership reconciliation gereği |
| services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs:30 | GenerateAccessToken sub/user.Id, tenant_id, actor_type=tenant_user ve permissions üretir. İncelenen producer Supplier veya LE mapping üretmez |
| services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/CurrentUserAccessor.cs:23 | NameIdentifier/sub çözümü actor kaynağıdır; Supplier identity değildir |
| services/Diten.Platform.Common/src/Diten.Platform.Common/Authorization/SignedJwtPermissionClaimEvaluator.cs | Authenticated principal + GUID tenant/sub + exact permission kontrolü; Supplier/LE eligibility sağlamaz |
| services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Carriers/CarrierContextMiddleware.cs:55 | Signed tenant/LE/sub ve permission ile scope kontrolü gerçek bir benzerliktir; Carrier davranışı Supplier producer seam'i sayılmadı, değiştirilmedi |
| .antigravity/rules/security-jwt.md | Servis JWT doğrulaması, default deny ve permission gate; Supplier mapping bunun yerine geçmez |

SUPPLIER frozen SHA256: `87a297edfb8eabf9ecc8beff7870955f46b38413a1490a05a36e3f255d77bb00`.
SUPPLIER-PERFORMANCE frozen SHA256: `9100d106527947a0a842bc5c4b580fbb2c3b6a288949798b8416a08d352e0088`.

Producer araması: `rg --files services` üzerinde supplier/metricregistry/riskregister isimleri; `docs/analysis/contracts` ve `execution/domains` içinde METRIC-REGISTRY/RISK-REGISTER ve ilgili adlar, ayrıca security kaynak incelemesi. Bu bounded repository gözlemi canlı harici producer'ın yokluk kanıtı değildir. Erişilebilir kaynaklarda doğrulanmış Supplier binding/LE, Metric Registry veya Risk Register producer seam'i gösterilemediği için GAP tutuldu. Owner exact published pointer ve canlı entegrasyon kanıtı sunmadan kapatılamaz.

## Proposal ile v1 arasındaki açık farklar

- Portal route'larının hepsi yeni403/503/400 durumlarını declare etmiyor; internal submit de yeni base/metric dependency503'ü declare etmiyor.
- Mapping/metric/risk dependency code'ları, idempotency-key conflict409, correlation replay ayrımı yeni proposed wire kararlarıdır.
- Metric revision/snapshot ve binding revision/fence mevcut v1 taşıyıcısı değildir. SS-04 bunları ayrı exact contract review'a bırakır.
- Status eligibility, precision sınırı, policy-authorized weights, ilk slice sourceType kısıtlaması ve risk transition matrix yeni business önerileridir; “frozen contract zaten bunu söylüyor” denmedi.
- Actor-isolated receipt seçildi; read-only security incelemesinin Supplier-shared alternatifine göre daha dar paylaşım sağlar. Aynı Supplier'daki farklı actor aynı key ile bağımsız işlem oluşturabilir; ortak business dedup garantisi verilmez. Owner bunun operasyonel etkisini SS-06 ile birlikte seçmelidir.

## Yapılan kontroller

- Önceki seam INPUT-HASHES.tsv içindeki dokuz kaynak yeniden SHA256 ile karşılaştırıldı: **9/9 eşit**. Contract/governance/pack değişmedi.
- SS-01…09 başlıkları ve AC-01…27 satırları varlık kontrolü PASS.
- Python Decimal ile weighted94×60%+98×40%=95.60 ve beş half-even örneği assertion PASS.
- SHA256SUMS bu klasörün teslim artefaktlarını bağlar; INPUT-HASHES tüketilen kaynakları bağlar. Manifest kendisini hash'lemez.
- Security bağımsız read-only incelemesi actual producer claim'lerini ve binding GAP'ini doğruladı; dosya yazmadı. Son policy seçimi ve yazarlık bu WP'de tek sahibindedir.

Acceptance satırları yürütülmüş runtime testleri değildir. Runtime/build/OAS suite çalıştırılmadı: YAML veya runtime değiştirilmedi; API uyumluluğu PASS iddiası yok. Fixture implement edilmedi; future fixture scope ayrı öneri olarak kaydedildi. Bu WP'nin yazıları yalnız bu çıktı klasöründedir; paralel Lane A/B source'una, shared authority'ye veya git state'ine mutation yapılmadı.
