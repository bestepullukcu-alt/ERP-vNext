# Authority matrisi

Bütün kararların durumu **PROPOSED / concurrence BEKLİYOR**. Aşağıdaki owner adları sorumlu rollerdir; belirlenmiş kişinin imzası veya yapılmış onay değildir. Tek koordinatör: Central Control Tower Supplier Seam Owner. INS/module-pack-author koordinasyonu yalnız bu spec önerisini hazırlar; tek başına üretici/security policy'si yayınlamaz.

| Karar | Accountable karar sahibi | Ayrı gerekli concurrence | Somut karar / kalan GAP |
|---|---|---|---|
| SS-01 | Enterprise/domain owner | CT, SCE, 0147/0148 | SCE/service tercihi; durable domain/DCP/registry amendment ayrı |
| SS-02 | MOD-0140 | 0147/0148 design consumers | Mevcut SUPPLIER v1 pointer; live producer kanıtı ayrı |
| SS-03 | MOD-0140 eligibility owner + consumer owners | Security Tenant/LE | Status tablosu ve getSupplier kullanımı; authoritative LE eligibility carrier GAP |
| SS-04 | CT contract single writer | MOD-0140, 0147, 0148, security ve etkilenen strict consumers | Exact successor diff/version/hata kapsamı henüz yok; bu WP yayın yetkisi vermez |
| SS-05 | Platform security identity owner | MOD-0140, 0148 | Single active binding, revocation fencing; issuer/sub/LE issuance, binding provider ve atomik protokol GAP |
| SS-06 | MOD-0148 | Security + CT contract writer | Actor-isolated exact tuple, retention, precedence; receipt/fingerprint/error contract amendment ayrı |
| SS-07 | Metric Registry owner | MOD-0147; Risk owner bandlar için | Yüzde scoring/precision/rounding/revision; canlı registry pointer ve immutable revision taşıyıcısı GAP |
| SS-08 | Risk Register owner + MOD-0147 lifecycle owner | MOD-0148 source resolver; Metric owner score-band sınırı | Taxonomy-only authority, linear lifecycle; taxonomy/source validation seam GAP |
| SS-09 | CT fixture coordinator | MOD-0140, security, Metric/Risk, iki consumer | Test-only fixture scope; implementation ayrı bounded WP, canlı producer yerine geçmez |

## Onaydan ayrı kalan seam teslimleri

1. MOD-0140: canlı getSupplier identity/status, Tenant+LE eligibility authority, failure/correlation ve scope proof; v1 required/null kusurlarının exact disposition'ı.
2. Security: validated actor+LE issuance, mapping provider, current permission/membership kontrolü ve revocation/commit fence güvencesi. Generic tenant_user rolü Supplier actor yetkisi değildir.
3. Metric: policy1'e uyan effective immutable revisions, scope/UoM/weight doğrulaması ve withdrawal/authenticity sorgusu. Yeni endpoint ismi bu paketle verilmez.
4. Risk: taxonomy revision source'u ve portal-source scoped validation. MANUAL/EXTERNAL_SIGNAL resolver yokluğu ilk slice kısıtlaması olarak açık kalır.
5. CT/consumers: yeni response kapsamları, mapping/metric/risk503 ve replay409, metadata carrier'ları, compatibility/version değerlendirmesi; exact diff olmadan frozen contract'a eklenmiş sayılmaz.

Bunlar “tanımlanacak ama onaylanmış” business kuralları değildir. Yukarıdaki somut policy seçenekleri seçilebilir; eksik authoritative seam üretimi, wire tasarımı ve runtime yetkisi ayrı kalır. Kullanıcı karar metni DECISION-PACK.md sonunda tek yerde bulunur. Bu WP hiçbir governance kaydını promote etmez.
