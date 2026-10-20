---
status: decided
decided_in: docs/records/decisions/2026-10/mvp6-supplier-dc03-owner-decision-01.md
kind: owner-decision-draft
gate: DC-03 (supplier actor binding + authorization + revocation)
modules: [MOD-0148]
also_unblocks: MOD-0148 eligibility caller identity left open by DC-02
measured_at: origin/main 673995c15 (2026-10-20)
---

# DC-03 — karar taslağı (öneri, onay değil)

> **2026-10-20: kararlaştırıldı.** D1–D8 önerildiği gibi seçildi, D9 açık; bağlayıcı metin karar kaydıdır:
> `docs/records/decisions/2026-10/mvp6-supplier-dc03-owner-decision-01.md`. Bu taslak tarihsel kanıt olarak kalır.

> Bu belge **taslaktır**. Hiçbir kararı kapatmaz; kod, paket durumu ya da dağıtım yetkisi vermez. Ölçüm salt okunur
> kod okumasıdır; **çalışma testi yapılmadı**. Uygulama Auth/Platform'da olacağı için her kod değişikliği XMC-001 onayı
> ister. Aynı içerik daha önce `docs/roadmap/plans/mvp6-supplier-seam-readiness-01/OWNER-DECISION-SET.md` SS-05/SS-06
> olarak önerilmiş, onaylanmamıştı.

## 1. DC-03 nedir

MOD-0148 Supplier Portal'ın kullanıcısı bir **tedarikçi kişisidir**. Her istekte sunucu şunu bilmeli: bu kişi hangi
kiracının, hangi Legal Entity'sinin, **hangi tek tedarikçisi** adına konuşuyor — ve bu bağ hâlâ geçerli mi? Paket
(`MOD-0148 §18`) bunu "one active Supplier per actor/Tenant/LE, binding revision and revocation fencing, trusted LE
issuance" diye tanımlar; portalın beş ucunun beşi de buna bağlı (BLOKE). DC-02 kararı (`mvp6-supplier-dc02-owner-
decision-01.md`) MOD-0148'in Procurement'ı **kimin adına** çağıracağını da buraya bıraktı.

Sahip ataması (`mvp6-supplier-dc01-dc05-owner-assignment-01.md`): DC-03 = Platform auth/security owner (Auth/Platform),
**build commitment**.

## 2. Ölçülen durum (olgu, `673995c15`)

| # | Olgu | Kaynak |
|---|---|---|
| F1 | Hesap türleri yalnız `Unknown`, `Human`, `Service` — tedarikçi türü yok | `services/Diten.AuthService/src/Diten.AuthService.Domain/Enums/AccountKind.cs:17-27` |
| F2 | Token'daki aktör türleri: `tenant_user`, `platform_admin`, `partner_admin`. Tedarikçi aktörü yok | `.../Infrastructure/Services/TokenService.cs:39,113` |
| F3 | Token `tenant_id`, `actor_type`, roller ve `permission` taşır; **`legal_entity_id` claim'i yok**, tedarikçi kimliği yok | `TokenService.cs:39-53,113-135` |
| F4 | Erişim token'ı **120 dakika**, refresh token 7 gün geçerli | `.../Api/appsettings.json` `JwtSettings` |
| F5 | Kiracı üyeliği `UserId`, `TenantId`, `Email` taşır; LE ya da tedarikçi bağı yok | `.../Domain/Entities/TenantUserMembership.cs:14-16` |
| F6 | Yetki önbelleği rol-atama sürümüne bağlı (`auth:{tenant}:{user}:v{version}`, TTL 300 sn); rol/izin değişikliği sürümü artırır | `.../Application/Common/Authorization/AuthCacheContract.cs` |
| F7 | Servis kimlikleri (S2S) mevcut: `ServiceClientIdentity` | `.../Domain/Entities/ServiceClientIdentity.cs` |
| F8 | Servisler LE'yi `X-Legal-Entity-Id` başlığından alır; SupplyChain bunu MDM'le **kiracıya ait ve aktif** diye doğrular (R-2) — ama kullanıcının o LE'de **yetkili** olduğunu kimse doğrulamaz | `MdmLegalEntityScopeValidator` (SupplyChain); DC-02 F7 |
| F9 | Auth'ta "supplier" geçen tek kod yok (izin anahtarları hariç) | `git grep -i supplier services/Diten.AuthService/src` |

**Sonuç:** DC-03 için Auth'ta hiçbir yapı yok; sıfırdan kurulacak. Mevcut iki parça yeniden kullanılabilir:
rol-atama sürümü fikri (F6, iptal sınırı için) ve servis kimlikleri (F7, Procurement çağrısı için).

## 3. Sahip kararları (her biri için önerilen seçenek)

| # | Karar | Seçenekler | Öneri |
|---|---|---|---|
| **D1** | Bağın (binding) sahibi ve deposu | (a) **Auth**'ta yeni bir kayıt: `SupplierActorBinding(UserId, TenantId, LegalEntityId, SupplierId, Status, Revision)`; (b) Procurement'ta (MOD-0140 tedarikçi kişileri); (c) SupplyChain'de (MOD-0148 kendi eşlemesi) | **(a)** — kimlik ve yetki Auth'un işi; DC-03 sahibi Auth/Platform. (b) başka domain'e kimlik sorumluluğu yükler; (c) MOD-0148'in kendi yetkisini kendisinin vermesi olur |
| **D2** | Aktör türü | (a) yeni `actor_type = supplier_user` + `AccountKind` değişmez (`Human`); (b) `tenant_user` + rol | **(a)** — tedarikçi kişisi kiracı kullanıcısı değildir; tenant rolleri ve menüleri ona hiç verilmez. Token yalnız **aktif bir bağ** varsa verilir |
| **D3** | Bağ bilgisinin taşınması | (a) token claim'leri: `supplier_id`, `legal_entity_id`, `binding_rev`; (b) her istekte Auth'a sorgu; (c) ikisi birlikte | **(c)**: okumada claim'ler; **her yazmada** Auth'tan güncel `binding_rev` kontrolü (Auth erişilemezse 503, yazma yok) |
| **D4** | İptalin etkili olma süresi | (a) anında (her istekte kontrol); (b) yazmalarda anında, okumalarda en çok token ömrü; (c) en çok token ömrü (bugün 120 dk) | **(b)** + `supplier_user` token ömrü **15 dk** (kiracı kullanıcılarınınki değişmez). 120 dk boyunca iptal edilmiş bir tedarikçinin okumaya devam etmesi kabul edilemez |
| **D5** | Kardinalite | Bir kişi, bir kiracı + LE'de **tek** aktif tedarikçi (paket); bir kişinin birden çok kiracı/LE'de bağı olabilir mi | Paket kuralı: Tenant+LE başına tek aktif bağ. Çok kiracılı kişi: oturum açarken **seçim** (bugünkü kiracı seçimi gibi); token tek bağ taşır |
| **D6** | Bağı kim kurar ve kaldırır | (a) kiracı Admin'i, Auth ekranından (yeni izin `auth.supplier-bindings.manage`); (b) Procurement onboarding akışı otomatik | **(a)** ilk sürüm için; bağ kurulurken tedarikçinin Procurement'ta **Active** olduğu (DC-02 D1, yöneticinin token'ıyla) doğrulanır. (b) sonraki sürüm |
| **D7** | MOD-0148'in Procurement'ı çağırma kimliği (DC-02'den açık) | (a) SupplyChain servis kimliği (S2S, F7) + bağdan gelen `supplierId`/LE; (b) tedarikçi kişisinin token'ı | **(a)** — tedarikçi kişisine `procurement.suppliers.read` verilmez (LE'deki her tedarikçiyi okur). S2S kapsamı yalnız `validate` |
| **D8** | Hata önceliği | Önerilen sıra | 401 (token yok/geçersiz) → 403 (`supplier_user` değil / bağ yok / bağ iptal / `binding_rev` eski) → 403 (tedarikçi `Active` değil, AC-05) → 400 (sözdizimi, istemcinin `supplierId`/LE geçmeye çalışması) → 503 (Auth ya da Procurement erişilemez); her ret sıfır yazma |
| **D9** | İnşa sahibi ve zamanı | — | **TBD** — Auth lane; XMC-001 ve kod onayı gerekir |

## 4. Önerilen ilk adım (öneri — onay değil)

D1–D9 onaylanırsa: (1) bir karar kaydı; (2) MOD-0148 paketine "Supplier actor binding (DC-03)" bölümü ve test kimlikleri
(yeni paket sürümü → yeni CT-APR-01); (3) Auth için ayrı bir module/WP paketi (bağ kaydı, `supplier_user` token'ı,
`binding_rev` uç noktası, yönetim ekranı izni) — Auth'un kendi pack/onay süreciyle. MOD-0148 kodu bu üçü ve DC-02'nin
inşası tamamlanmadan başlamaz.

## 5. Açık riskler

- **Kişisel veri:** tedarikçi kişileri kiracı dışından gelir; davet, şifre/MFA ve hesap kapatma akışları kiracı
  kullanıcılarınınkiyle aynı mı olacak — Auth sahibi karar verir.
- **Kiracı izolasyonu:** `supplier_user` token'ı hiçbir kiracı API'sine (tenant rolleri) erişmemeli; gateway ve her
  serviste `actor_type` kontrolü gerekir. Bu, bugünkü `[HasPermission]` modelinin dışında yeni bir kural ister.
- **MOD-0140 iki depo sorusu** (DC-02 kaydında): bağ `supplierId`'si hangi depodaki tedarikçiyi gösteriyor — DC-02 D1
  ile aynı koşula bağlı.

## 6. Ölçülmeyenler

- Çalışma testi yok. Gateway'in `actor_type`'a göre davranışı ölçülmedi.
- Diğer servislerin `actor_type` kontrolü yapıp yapmadığı (supplier_user'ın sızma yüzeyi) taranmadı.
- Auth'un mevcut davet/MFA akışlarının dış kişiye uygunluğu incelenmedi.
