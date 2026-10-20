# MOD-0140 sahibine bildirim — taslak (gönderilmedi)

> Kime: MOD-0140 Supplier sahibi (procurement / control-tower). Kimden: SCE / MVP-6 (CT). XMC-001: bu bir bildirimdir;
> Procurement'ta hiçbir şey değiştirilmedi ve değiştirilmeyecek.

**Konu: Procurement, `X-Legal-Entity-Id` başlığını kiracıya karşı doğrulamıyor (salt okunur bulgu)**

Merhaba,

DC-02 kararı (`docs/records/decisions/2026-10/mvp6-supplier-dc02-owner-decision-01.md`) ile MOD-0147 ve MOD-0148,
tedarikçi uygunluğunu `POST /api/suppliers/validate` üzerinden okuyacak. Okuma sırasında şunu gördük:

- `TenantResolutionMiddleware.cs:77-100` (`origin/main` `365f260a9`): Legal Entity, JWT'deki `legal_entity_id`
  claim'inden, yoksa `X-Legal-Entity-Id` başlığından alınıyor. İkisi farklıysa 400 dönüyor.
- Başlıktan gelen LE'nin o kiracıya ait ve aktif olduğu **doğrulanmıyor** (MDM'e sorulmuyor).
- Kiracılar arası sızıntı yok (Tenant JWT ile bağlı). Ama claim'siz bir token ile aynı kiracıdaki başka bir LE'nin
  tedarikçileri okunabilir ya da doğrulanabilir.

SupplyChain tarafında risk yok: biz LE'yi önce MDM'le doğrulayıp (R-2) o değeri iletiyoruz (D4). Procurement'ın
kendisi için öneri: SupplyChain'deki `MdmLegalEntityScopeValidator` örneği gibi, başlıktaki LE'nin kiracının aktif
LE'si olduğunun doğrulanması. Karar ve uygulama sizin.

Ayrıca bilginize: 2026-10-11 tarihli bir lane kaydı MOD-0140'ın iki ayrı depoda bağımsız olarak uygulandığını
gösteriyor (`docs/records/audits/2026-10/mod-0140-dual-implementation-01/REPORT.md`). DC-02 bu depodaki Procurement
uygulamasına ve donmuş `supplier.openapi.yaml` v1.2.0'a bağlıdır; asıl kayıt sistemi başka depo seçilirse D1 yeniden
karara bağlanmalı.

Teşekkürler.
