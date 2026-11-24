---
id: MOD-0360-FU01
name: Supplier User Binding
domain: platform-shared-services
service: Diten.AuthService
shell: tenant
golden_reference: slim
data_mode: server
entity_base: EntityBase
status: draft
status_note: "r4 (2026-11-24) TASLAK — draft / approval pending. Yazar: Beste (merkez, K2); onaylayan: MOD-0360 sahibi (GMG-ERP, K3) — paketi yazmamış onaylayıcı. Ali 2026-11-24'te r3 içeriğini (sha256 068646c1…) onayladı (F-2 ve sırası, F-8: 403 ve 404 kodu), bir koşul ekledi (AC-29 sabotaj 3) ve SQ-18'i kapattı; bu içerik onayıdır, CT-APR-01 satırı değildir ve r4 satırı yerine geçmez. r3 bağımsız incelemesi (docs/records/reviews/MOD-0360-FU01/2026-11-24-068646c1.md) KOŞULLU; F-1…F-3 r4'te kapandı. r4 için yeni CT-APR-02 bağımsız inceleme + yeni CT-APR-01 satırı gerekir; r2 (8086bf9a) CT-APR-01 satırı (approvals.tsv, Ali, 2026-11-13) bu sürüme geçmez. Onay satırı gelene kadar r2 metni bağlayıcıdır; r3/r4 maddeleri (AC-28, AC-29, AC-30 ve S2S ucunun Auth kiracı ara katmanı muafiyeti) için kod yetkisi yoktur. Onaydan sonra künye ready-for-dev'e döner (S-49); kod yetkisi yine Bölüm 0.4 S0 ve Bölüm 22 dilim kapılarına bağlıdır — adlı istisna DL-2.0: S0'ı beklemez, yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001 iznini bekler (SQ-18 kapandı, Ali 2026-11-24)."
approval_status: pending
revision: "r4 (2026-11-24) — r3 bağımsız incelemesi docs/records/reviews/MOD-0360-FU01/2026-11-24-068646c1.md (KOŞULLU) F-1…F-3 ve SQ-18 kapanışı (Ali 2026-11-24, dc03-supplier-user-binding-owner-decisions-01.md son ek) işlendi: F-1 — S2S binding_rev yolu (/api/internal/supplier-bindings) Auth kiracı ara katmanında (TenantResolutionMiddleware.cs) kiracı zorunluluğundan, dev bypass'tan ve BL-324 çelişki 400'ünden muaf, kiracı kararı uç politikasında; sıra 401 → 403 → 404 ara katmanla da korunur (Bölüm 0.3, 5, 7, 8, 13, 14, 17, AC-19, AC-30, Teslim dilimleri); F-2 — AC-28'e DefaultRolePermissionTemplate.cs:182 ve EntitlementPermissionSyncService.cs:294 için tek yol sabotajları (AC-28, Bölüm 17); F-3 — Ali kararı ve XMC-001 izni 2026-11-24, öneri 2026-11-19 (künye, r3 notu, Bölüm 0.3, 5, AC-28, 19); SQ-18 KAPANDI — DL-2.0 Bölüm 0.4 S0'ı beklemez, yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001'i bekler (Bölüm 0.4, 5, 18, 22, 23, Teslim dilimleri). Başka kapsam değişikliği yok. r3 (2026-11-19 taslak; Ali kararı 2026-11-24) — Auth sahibi kararı (Ali, MOD-0018 / MOD-0360 sahibi, 2026-11-24; Beste önerdi 2026-11-19; XMC-001 izni Ali'den, 2026-11-24): MOD-0148 incelemesi docs/records/reviews/MOD-0148/2026-11-15-75b33fcb.md F-2 ve F-8 (+ 2026-11-17-dab26a67.md F-2) kabul. (1) supplychain.supplier-portal.read / .submit Auth ExplicitGrantOnlyPermissions listesine girer — varsayılan rol şablonu, tam katalog / SuperAdmin ve modül yetkilendirme eşitlemesi bu iki anahtarı vermez; şirket kullanıcılarına yalnız Roller → İzinler (MOD-0148 D2 / K-5) — AC-28. (2) S2S binding_rev ucunda bu kiracıda bulunmayan bağ (başka kiracının bağı dahil) adlandırılmış 404 SUPPLIER_BINDING_NOT_FOUND — AC-29; listede olmayan service_name 403 (SQ-3 kaydı) — AC-30; AC-19 ve Bölüm 13'teki 401 ifadesi 403'e hizalandı. Uygulama DL-2'de. Kesinleştirme (Beste önerdi 2026-11-19, MOD-0018/0360 sahibi Ali onayladı 2026-11-24): SQ-16 kod adı SUPPLIER_BINDING_NOT_FOUND karar (başka kiracının bağı ve olmayan kimlik aynı gövde); SQ-17 ExplicitGrantOnlyPermissions eklemesi DL-2'nin ilk commit'i (DL-2.0), DL-2'nin katalog kapısından muaf, MOD-0148 P4'ten önce main'de, DL-1 kabulünü beklemez; S2S kiracı X-Tenant-Id başlığından — yok / GUID değil / token'ın izinli kiracı kapsamında değil → 403, bağ okunmaz, aynı gövde, ayrı kod adı yok; 403'e kod adı verilmez; DL-1 AC-4 404 kodu ve §23 SQ-1…SQ-15 güncellemesi kapsam dışı (sonraki revizyon). Yazar Beste (merkez, K2), onaylayan MOD-0360 sahibi (GMG-ERP, K3). Başka kapsam değişikliği yok. r2 (2026-11-13, 535475f7b) — r1 incelemesi F-1…F-12. r1 (2026-11-13, f032a41a3) — ilk taslak."
owner: platform-shared-services / merkez (Beste) — DC-03 sahibi
author: Beste (merkez, K2) — r3 ve r4 yazarı
approver: Ali (GMG-ERP) — Auth sahibi (MOD-0017 / MOD-0018) ve MOD-0360 sahibi; CT-APR-01 yüksek risk, paketi yazmamış onaylayıcı (r3: onaylayan MOD-0360 sahibi, GMG-ERP, K3)
risk: HIGH
branch: feature/pss/mod-0360-fu01-supplier-user-binding (henüz açılmadı)
started: 2026-11-13
target: TBD
form_field_count: 5
parent: MOD-0360
consumer_module: MOD-0148
decision_gate: DC-03 (MVP-6 plan v10.0 C4 / C7)
---

# MOD-0360-FU01 — Supplier User Binding (DC-03)

> **r4 (2026-11-24, TASLAK — onay bekliyor) — r3 bağımsız incelemesi `docs/records/reviews/MOD-0360-FU01/2026-11-24-068646c1.md`
> (KOŞULLU, F-1…F-3) ve SQ-18 kapanışı işlendi.** Yalnız şu kapanışlar; başka kapsam değişmedi (AC-1…AC-27, DL-1, DL-3, DL-4 aynı):
> 1. **F-1 (ORTA) — Auth kiracı ara katmanı S2S ucunun önünde:** `Program.cs:221-223` sırası `UseAuthentication` →
>    `UseTenantResolution` → `UseAuthorization`; `TenantResolutionMiddleware.cs` başlıksız / GUID olmayan başlıklı istekte uç
>    politikasından önce 400 `Missing Tenant` döner (`:101-111`, `:110`), geliştirme ortamında dev bypass'ı uygular (`:87-94`);
>    `/api/internal` yolu `:141`'deki `/internal` muafiyetine girmez. Kural: `/api/internal/supplier-bindings` öneki ara katmanda
>    kiracı zorunluluğundan, dev bypass'tan ve BL-324 çelişki 400'ünden muaf tutulur (emsal `IsServiceTokenIssuancePath`,
>    `:148-162`; dar önek, `/api/internal`'ın tamamı değil); kiracı kararı uç politikasındadır. Yerler: Bölüm 0.3, 5 (DL-2 r4
>    eklenenler), 7, 8, 13, 14, AC-19, AC-30 (sabotaj 5-7), 17, Teslim dilimleri.
> 2. **F-2 (DÜŞÜK) — AC-28 tek yol sabotajları:** `DefaultRolePermissionTemplate.cs:182` süzgeci tek başına kalkınca (a),
>    `EntitlementPermissionSyncService.cs:294` süzgeci tek başına kalkınca (c) kırmızı (AC-28 sabotaj 5 ve 6; Bölüm 17).
> 3. **F-3 (DÜŞÜK) — tarih ve atıf:** Ali'nin kararı ve XMC-001 izni **2026-11-24**'tür (Beste'nin önerisi 2026-11-19); künye
>    (`revision`, `status_note`), r3 notu, Bölüm 0.3, 5, AC-28 ve 19 hizalandı; `status_note`'taki sayı-liste uyuşmazlığı giderildi.
> 4. **SQ-18 KAPANDI (Ali, 2026-11-24; `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` son ek):**
>    DL-2.0 Bölüm 0.4 S0 kapanışını **beklemez**; yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001 iznini bekler — Bölüm 0.4, 5, 18, 22,
>    23, Teslim dilimleri.
> 5. **Bilgi notu 1:** Bölüm 8'deki (incelemede "Bölüm 9" diye anılan) "her sorgu `TenantId` (JWT) ile sınırlıdır" satırına
>    S2S istisnası yazıldı.
>
> **Onay durumu:** Ali'nin 2026-11-24 onayı r3 içeriği içindir (paket sha256 `068646c1…`); r4 satırı için **yeni CT-APR-02**
> bağımsız inceleme ve **yeni CT-APR-01** satırı gerekir. r2 satırı (`8086bf9a…`) bu sürüme geçmez. Künye `status: draft`,
> `approval_status: pending`, revizyon r4 (2026-11-24). Ölçüm tabanı HEAD `86c5dfe9b` (salt okunur kod okuması; test koşulmadı).
> Yazar: Beste (merkez, K2); onaylayan: MOD-0360 sahibi (GMG-ERP, K3).
>
> **r3 (2026-11-19 taslak, TASLAK — onay bekliyor) — Auth sahibi kararı (Ali, MOD-0018 / MOD-0360 sahibi, 2026-11-24; Beste
> önerdi 2026-11-19; XMC-001 izni Ali'den, 2026-11-24):** MOD-0148 bağımsız incelemesi `docs/records/reviews/MOD-0148/2026-11-15-75b33fcb.md` **F-2** ve **F-8** (üçüncü tur
> `2026-11-17-dab26a67.md` **F-2** aynı konuyu taşır) ve MOD-0148 karar kaydı
> `docs/records/decisions/2026-11/mod0148-dc03-consumer-owner-decisions-2026-11-15.md` "Açık kalanlar" (:55-57) Auth'a yönelik
> iki istek kabul edildi:
> 1. **Otomatik verme yolu yok (F-2, K-5):** `supplychain.supplier-portal.read` ve `.submit` Auth'taki
>    `ExplicitGrantOnlyPermissions` listesine eklenir; varsayılan rol şablonu, tam katalog / SuperAdmin ve modül yetkilendirme
>    eşitlemesi bu iki anahtarı vermez; şirket kullanıcılarına yalnız Roller → İzinler ile verilir (MOD-0148 D2 / K-5). Tedarikçi
>    token'ının sabit kümesi (K-1, AC-12) bundan etkilenmez — Bölüm 14, AC-28.
> 2. **S2S ucunun hata kodları (F-8, BREV-01):** bu kiracıda bulunmayan bağ (başka kiracının bağı ya da hiç olmayan kimlik) için
>    adlandırılmış 404 `SUPPLIER_BINDING_NOT_FOUND` (**karar — SQ-16**); listede olmayan `service_name` için **403** (SQ-3
>    kaydı, `dc03-supplier-user-binding-owner-decisions-01.md:34`) esas alınır; AC-19 ve Bölüm 13'teki "401" ifadesi 403'e
>    hizalandı — Bölüm 13, 14, AC-19, AC-29, AC-30.
>
> Uygulama **DL-2**'dedir (Bölüm 5, 22, Teslim dilimleri). Bu revizyon yalnız bu iki maddeyi (ve S2S ucunun kiracı başlığı
> kuralını) içerir; başka kapsam değişmedi.
>
> **Kesinleştirme — Beste önerdi (2026-11-19), MOD-0018/0360 sahibi Ali onayladı (2026-11-24; `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` son ek):**
> (a) **SQ-16:** 404 kodu `SUPPLIER_BINDING_NOT_FOUND` — karar; başka kiracının bağı ve var olmayan kimlik aynı gövdeyi alır.
> (b) **SQ-17 (bağlayıcı sıra):** `ExplicitGrantOnlyPermissions` eklemesi DL-2'nin **ilk commit'i** (DL-2.0); DL-2'nin katalog
>     kapısından muaftır; MOD-0148 P4'ten (anahtarların kataloğa girmesi) **önce** main'de olmalıdır; DL-1 kabulünü beklemez
>     (Bölüm 5, 22, Teslim dilimleri; kanıt AC-28 (f)). Bu, "dilim sırasıyla (DL-1 → DL-4)" kuralının adlı istisnasıdır.
> (c) **S2S kiracı bilgisi:** `X-Tenant-Id` başlığından okunur; başlık yok / GUID değil / servis token'ının izinli kiracı
>     kapsamında değil → **403**, bağ okunmaz, aynı 403 gövdesi, ayrı kod adı yok (Bölüm 13, 14, AC-30).
> (d) 403'e kod adı verilmez; DL-1 AC-4 404 kodu ve §23 SQ-1…SQ-15 güncellemesi kapsam dışı — sonraki revizyon (Bölüm 23).
> (e) **Ali'nin koşulu (2026-11-24):** hiç var olmayan bağ için cevap, başka kiracının bağına verilen cevapla tıpatıp aynıdır
>     (aynı durum kodu, aynı kod, aynı gövde; istek başına değişen izleme alanları hariç) ve bu bir testle gösterilir (AC-29,
>     sabotaj 3).
> Yazar: Beste (merkez, K2); onaylayan: MOD-0360 sahibi (GMG-ERP, K3). Künye `draft` / `approval_status: pending`.
> r2'nin CT-APR-01 satırı (`execution/approvals/approvals.tsv`, pack sha256 `8086bf9a…`, Ali 2026-11-13) bu sürüme geçmez; r3 için
> CT-APR-02 + yeni CT-APR-01 gerekir. Ölçüm tabanı origin/main `7236d9608` (salt okunur kod okuması; test koşulmadı).
>
> **r2 (2026-11-13) — inceleme `docs/records/reviews/MOD-0360-FU01/2026-11-13-ce91314f.md` (KOŞULLU, F-1…F-12) işlendi.**
> Sahip kararları (Beste, "hepsi öneri gibi"; `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md`
> son madde): **F-2** erişim token'ının bitişi oturum bitişini asla geçmez — `exp = min(nbf + 15 dk, oturum sonu)`, oturum tam 8
> saatte biter (Bölüm 8, 14; AC-12, AC-13); **F-5** bağlamada `eligibleForTransactions` yanında `onboardingStatus = Approved` ve
> `purchasingEnabled = true` da denetlenir (Bölüm 12, 13; AC-27, AC-8). Diğerleri: **F-1** F4 sertleştirmesi yalnız kimliği
> doğrulanmış istekte (Bölüm 15; AC-22); **F-3** DL-2 dosya listesi tamamlandı (Bölüm 3, 4, 5); **F-4, F-6, F-7, F-8, F-9** sahipli
> açık sorular SQ-11…SQ-15 (Bölüm 23; S0 kapanışı 0.4); **F-10** Gateway satır atıfları düzeltildi; **F-11** AC-13'e 7 sa 50 dk
> yenileme vakası; **F-12** künye `ready-for-dev` (S-49), `approval_status: pending`. Ölçüm tabanı HEAD `70cb6ca01`.
>
> **ready-for-dev ≠ kod yetkisi.** Bu paket kod, şema, izin kataloğu, rol, token, gateway rotası ya da dağıtım yetkisi
> vermez. Kod yalnız CT-APR-02 (bu sürüm) ve CT-APR-01 onay satırı yazıldıktan ve Bölüm 0'daki S0 kapısı kapandıktan sonra,
> dilim sırasıyla (DL-1 → DL-4) ve Bölüm 22'deki dilim kapılarıyla yazılır (adlı istisna DL-2.0: S0'ı ve DL-1 kabulünü beklemez —
> SQ-17, SQ-18; Bölüm 22). Auth ve Gateway dosyalarına dokunmak ayrıca
> XMC-001 izni ister (Bölüm 0.3).
>
> **Kimlik kapısı (DCP-002, 2026-11-13):** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0360-FU01
> --name "Supplier User Binding" --parent MOD-0360` → `OK  MOD-0360-FU01: proven against Blueprint/registry.` (çıkış 0).
> Blueprint SoR haritası "external identity bindings" sahipliğini MOD-0360 Tenant User & Identity Management'a verir
> (`execution/registries/module-id-registry.md` MOD-0360 satırı). Registry'ye `MOD-0360-FU01` satırının yazılması S0 işidir;
> bu paket registry'yi değiştirmez.
>
> **Sahip kararları (bağlayıcı):** `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` —
> M2 (kanonik `SUP-` kodu saklanır, değişince merkez düzeltir), kimlik MOD-0360-FU01, token süresi (erişim 15 dk sessiz
> yenileme, oturum en çok 8 saat sonra yeniden giriş + e-posta kodu), taban D1–D8 taslağı, kiracı modeli.

## 0. S0 — karar ve izin kapısı (kod dilimi değildir)

S0 kod üretmez; DL-1'in başlaması için kapanması gereken kararları ve izinleri toplar. Bağlayıcı kayıt
`docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` dosyasına eklenir.

### 0.1 Taban: D1–D8 (main dışı taslak) ve bu paketteki karşılığı

Kaynak: `git show origin/feature/sce/mod-0148-dc03-decision-prep-01:docs/records/decisions/2026-10/mvp6-supplier-dc03-owner-decision-01.md`
(`67a30c7f0`) ve aynı dalda `docs/roadmap/plans/mvp6-supplier-dc03-decision-prep-01/DECISION-DRAFT.md` /
`ACTOR-TYPE-SCAN.md`; ilk taslak `origin/docs/sce/dc03-supplier-actor-binding-draft-01` (`eaa7bdd06`). #172 birleştirilmeden
kapandığı için bu metinler main'de yoktur; S0'da sahibe yeniden sunulur.

| # | Taslak metni (özet) | Bu paketteki biçim | S0'da sahibe sorulan |
|---|---|---|---|
| D1 | Bağ Auth'ta: `SupplierActorBinding(UserId, TenantId, LegalEntityId, SupplierId, Status, Revision)` | Aynı; `SupplierId` yerine **kanonik `SUP-` kodu** (`SupplierCode`) — M2 (2026-11-13) | yeniden onay |
| D2 | Yeni `actor_type = supplier_user`; hesap `Human` kalır; token yalnız aktif bağ varken | `actor_type = supplier_user` aynı; **hesap türü ayırt edicisi SQ-1'e bağlı** (öneri: hesap düzeyinde ayırt edici, `Human` kalmaz) | SQ-1 |
| D3 | Token `supplier_id`, `legal_entity_id`, `binding_rev` taşır; her yazma Auth'tan güncel `binding_rev` sorar; Auth yoksa 503 | Aynı; S2S `binding_rev` ucu DL-2'de (AC-19); ucun audience'ı SQ-3 | yeniden onay + SQ-3 |
| D4 | Yazmada anında, okumada token ömrü içinde iptal; erişim token'ı 15 dk | 15 dk erişim + sessiz yenileme + **en çok 8 saat oturum**, sonra yeniden giriş + e-posta kodu (sahip, 2026-11-13); erişim token'ı oturum sonunu geçmez: `exp = min(nbf + 15 dk, oturum sonu)` (r2, F-2) | yeniden onay (8 saat eki + F-2) |
| D5 | Kiracı + LE başına kişi başına en çok bir aktif bağ; çoklu bağda girişte seçim; token tek bağ | Aynı (tekil indeks + seçim bileti, AC-3, AC-17) | yeniden onay |
| D6 | Bağı kiracı Admin'i kurar/kaldırır (`auth.supplier-bindings.manage`); bağlama anında tedarikçi Procurement'ta `Active`, Admin'in token'ıyla okunur | Aynı; okuma MOD-0140 `GET /api/suppliers/{supplierId}/eligibility`; ölçüt üç alanın birlikte doğruluğu: `eligibleForTransactions = true` (MOD-0140 S-1), `onboardingStatus = Approved`, `purchasingEnabled = true` (r2, F-5); kod düzeltme komutu eklendi (M2) | yeniden onay (F-5 eki) |
| D7 | MOD-0148 Procurement'ı SupplyChain servis kimliğiyle çağırır; S2S kapsamı yalnız `validate` | MOD-0148'in işi, bu paketin kodu değil; **MOD-0140 S-4 ile çelişir** (S-4: `POST /validate` **ve** tekil `GET /{supplierId}/eligibility`) | SQ-4 |
| D8 | Hata önceliği 401 → 403 (aktör/bağ/rev) → 403 (tedarikçi Active değil) → 400 → 503; her ret sıfır yazma | Tedarikçi kapısı ve S2S ucu için aynı sıra (Bölüm 13); portal tarafı MOD-0148'de | yeniden onay |

D9 (inşa sahibi ve zamanı) taslakta boş bırakılmıştı; MVP-6 plan v10.0 C4 satırına göre Beste (merkez), MOD-0148
başladığında (`docs/roadmap/plans/mvp6-development-plan-v10.0.md:90`). MOD-0148 2026-10-22'de başladı (MOD-0148 §0.0).

### 0.2 DC-03 C4 onaylı beş madde (plan v10.0 :90, 2026-10-21) → bu paket

| C4 maddesi | Bu paketteki yeri |
|---|---|
| Ayrı portal alt alanı (yalnız host'a bağlı çerezler oturumları ayırır) | v1'de UI yok (MOD-0148 §0.0); tedarikçi kapısı ayrı API önekidir (`/api/supplier-auth/*`). Alt alan + çerez ayrımı portal ekranıyla gelir — Follow-up FU-B |
| Tedarikçi hesaplarında zorunlu iki adımlı doğrulama | DL-2: kiracı ayarından bağımsız zorunlu e-posta kodu (AC-11) |
| Bir kiracıda aynı e-postayla şirket + tedarikçi hesabı yok | Mevcut tekil indeks `{Email, TenantId}` (canlı kayıt) + kur komutunun ret dalı (AC-6) |
| Gateway izin listesi: `supplier_user` → `/api/supplier-performance/portal/*` + kendi oturum uçları | DL-3 (AC-21) |
| İnternete açılmadan önce `TrustedProxies`, gateway rate limit (MOD-0032), paylaşılan limiter deposu | **Kapsam dışı** — ayrı MOD-0032 FU (numarası ve sahibi TBD). v1 iç ağdadır (Bölüm 8) |

C7 (`docs/roadmap/plans/mvp6-development-plan-v10.0.md:93`): "MOD-0148 için aynı yol + bağ, `supplier_user` token'ı ve
gateway kuralı için ayrı Auth/Platform paketi" — bu paket o ayrı pakettir.

### 0.3 XMC-001 izinleri (kod öncesi)

| Yol | Sahip | Ne istenir |
|---|---|---|
| `services/Diten.AuthService/**` (yeni özellik klasörü + mevcut kapı dosyaları, Bölüm 5) | Ali — MOD-0017 / MOD-0018 sahibi | DL-1 ve DL-2 dosya listesine izin; `TenantDoorPlatformAccountRefusal` emsalinin tedarikçi hesabına genişletilmesi; `AssignRoleCommand` reddi; AccountKind oluşturma yolu bekçisi (`AccountKindCreationPathsGuardTests`) değişikliği |
| **r3 (DL-2):** `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` — sınıf `:8`, `Keys` kümesi `:108-114` (iki sabit + iki küme girdisi) | Ali — **izin verildi 2026-11-24** (r3 sahip kararı; Beste önerisi 2026-11-19) | yalnız ekleme: `supplychain.supplier-portal.read`, `supplychain.supplier-portal.submit`. Listeyi okuyan üç otomatik yol **değişmez**, yalnız ölçülür: `.../Domain/Authorization/DefaultRolePermissionTemplate.cs:181-183` (SuperAdmin / Admin / Viewer şablonu; çağıran `DefaultRoleProvisioningRule.cs:27`), `.../Application/Common/Services/FullCatalogPermissionGrantService.cs:39` (tam katalog / SuperAdmin; çağıran `.../Api/Controllers/InternalPermissionsController.cs:96` ilk kayıt, `:117` yeniden etkinleştirme), `.../Application/Common/Services/EntitlementPermissionSyncService.cs:293-295` (modül yetkilendirme eşitlemesi) |
| **r3 (DL-2):** `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AccountKindTests.cs:60-80` | Ali — izin verildi 2026-11-24 | küme sayısı bekçisi `Assert.Equal(13, …)` (`:78`) → 15, test adı ve iki ada göre `Contains` satırı; başka değişiklik yok |
| **r4 (DL-2, F-1):** `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs` — Auth'un kiracı ara katmanı (Gateway'deki aynı adlı dosya değil); bugün çelişki 400 `:68-81`, dev bypass `:87-94`, `Missing Tenant` 400 `:101-111`, `/internal` muafiyeti `:141`, emsal `IsServiceTokenIssuancePath` `:148-162` | Ali — izin verildi 2026-11-24 (r3/r4 kararının parçası) | yalnız ekleme: `/api/internal/supplier-bindings` öneki için dar muafiyet (Bölüm 5 r4 kuralı); başka yolun davranışı değişmez. Yeni test `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SupplierAuth/BindingRevisionTenantMiddlewareExemptionTests.cs` (emsal `Tenancy/ServiceTokenEndpointTenantExemptionTests.cs`) |
| `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` | MOD-0032 (Gateway) sahibi / `integration-agent` | DL-3 izin listesi + F4 sertleştirmesi |
| `gateway/Diten.ApiGateway/ocelot.json` | yalnız `integration-agent` (AGENTS.md §4) | iki yeni rota (Bölüm 15) — ayrı görev |
| `tests/architecture/audit-ledger/Diten.AuthService.md` + `AuditTrailStandardTests.cs` | Control Tower (defter kararı) | yeni iz satırı (Bölüm 21) |
| `frontend/Diten.Web/**` (yalnız DL-4) | Web / Governance ekran sahibi | yeni ekran klasörü |

### 0.4 S0 kapanış ölçütü

S0, (1) D1–D8'in bu paketteki biçimiyle yeniden onayı karar dosyasına yazıldığında, (2) SQ-1, SQ-3, SQ-4, SQ-7, SQ-11, SQ-12,
SQ-13 cevaplandığında, (3) 0.3 tablosundaki Auth ve Gateway izinleri yazılı alındığında ve (4) Bölüm 21'deki denetim izi için
Control Tower defter kararı verildiğinde kapanır. SQ-2, SQ-8, SQ-9, SQ-10, SQ-14, SQ-15 DL-1'i bekletmez; ilgili dilimin
başında kapanır (Bölüm 22). Sahipli maddeler (r2): SQ-11 ve SQ-14 Natig; SQ-12, SQ-13 ve SQ-15 Ali — inceleme bulguları F-4,
F-6, F-7, F-8, F-9.

**r4 — DL-2.0 S0'ı beklemez (SQ-18 KAPANDI, Ali 2026-11-24;** `dc03-supplier-user-binding-owner-decisions-01.md` son ek**):**
S0 DL-1'in kapısıdır; DL-2.0 (`ExplicitGrantOnlyPermissions` eklemesi, AC-28) yalnız r4 CT-APR-02 + CT-APR-01 ve 0.3'teki
XMC-001 iznini (Auth, Ali) bekler (Bölüm 22).

## 1. Module Summary

MOD-0148 Supplier Portal'ın kullanıcısı bir tedarikçi kişisidir. Bu paket o kişinin kimliğini kurar:

- **Bağ (DL-1):** kiracı Admin'i, bir kişiyi kendi kiracısının bir tüzel kişiliğinde (LE) tek bir tedarikçiye bağlar.
  Bağ, bağlama anında MOD-0140'ın döndürdüğü **kanonik `SUP-` kodunu** saklar (M2). Kod değişirse merkez düzeltir.
- **Tedarikçi kapısı (DL-2):** ayrı giriş / yenileme / çıkış / `me`; her girişte zorunlu e-posta kodu; `supplier_user`
  erişim token'ı 15 dk, oturum en çok 8 saat; token yalnız `supplychain.supplier-portal.read` ve `.submit` taşır.
- **Gateway (DL-3):** `supplier_user` yalnız portal uçlarına ve kendi oturum uçlarına geçer; `actor_type`'ı olmayan token
  kiracı uçlarında 403 alır (F4 açığı kapanır).
- **Bağlama ekranı (DL-4, isteğe bağlı):** kiracı kabuğunda liste + kur / kaldır / kod düzelt, 7 dil.

**Kiracı modeli (sahip açıklaması, 2026-11-13):** tedarikçi kendi kiracısını açmaz; hizmet verdiği firmanın kiracısında
(ör. GMG) bir `supplier_user` hesabıdır. Başka bir kiracıda ayrı hesaptır — kiracı başına e-posta başına tek hesap.

**Kapsam v1:** yalnız iç ağ. İnternete açılma kapıları (TrustedProxies, MOD-0032 rate limit, paylaşılan limiter) bu
paketin dışındadır.

## 2. Ownership and Boundaries

**Kapsam içi**
- `SupplierActorBinding` kaydı (Auth, tenant-owned), üç yazma komutu, liste / tekil / geçmiş / sapma sorguları.
- Tedarikçi hesabının ayırt edicisi (SQ-1) ve bu hesabın kiracı kapısından, kiracı hesabının tedarikçi kapısından reddi.
- Tedarikçi kapısı uçları ve `supplier_user` token'ı; S2S `binding_rev` ucu (D3).
- Gateway `supplier_user` izin listesi ve F4 sertleştirmesi (DL-3; kod `integration-agent` / MOD-0032 sahibi iznine bağlı).
- Bağlama ekranı (DL-4, isteğe bağlı).

**Kapsam dışı**
- MOD-0148 portal uçları, `supplychain.supplier-portal.*` anahtarlarının tanımı, portalın `actor_type` / `binding_rev`
  denetimi, replay receipt (MOD-0148 §0.1) — MOD-0148'in işi.
- MOD-0140 uygunluk ucu ve servis token'ı (MOD-0140 DL-3, DL-7) — Cihan / Procurement.
- İnternete açılma: `TrustedProxies`, gateway rate limit, paylaşılan limiter deposu — ayrı MOD-0032 FU.
- Portal alt alanı, tedarikçi giriş ekranı, çerez oturumu — portal UI ile (Follow-up FU-B).
- Tedarikçinin self-servis "parolamı unuttum" akışı — Follow-up FU-C (v1'de Admin yeniden davet eder).
- Onboarding akışından otomatik bağ (D6 "sonraki sürüm") — Follow-up FU-D.
- Tedarikçi hesaplarının kiracı Kullanıcılar ekranındaki görünümü — MOD-0360 ekran işi, XMC-001 (Follow-up FU-E).

## 3. Owned Objects

- **Varlık:** `SupplierActorBinding` (koleksiyon `supplierActorBindings`).
- **Varlık (DL-2, r2 F-3):** `SupplierSelectionTicket` (koleksiyon `supplierSelectionTickets`, TTL indeksli) + `ISupplierSelectionTicketRepository`.
- **Mevcut varlıklara ek alan (DL-2, r2 F-3; XMC-001):** `RefreshToken` — `SessionStartedAtUtc`, `SessionEndsAtUtc` (mutlak oturum);
  `MfaChallenge` — `Door` (`tenant` / `supplier`, SQ-2). Ayrıntı Bölüm 4.
- **Servis token'ı doğrulama şeması (DL-2, r2 F-3):** Auth'ta bugün yalnız kullanıcı `JwtBearer` şeması var
  (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/DependencyInjection.cs:42-43`); `service_name` claim'ini yalnız
  belirteç üretici yazar (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/ServiceIdentityTokenIssuer.cs:49`).
  AC-19 için ayrı adlı bir kimlik doğrulama şeması + doğrulayıcı (`SupplierBindingRevisionServiceTokenValidator`) ve DI kaydı kurulur;
  biçim MOD-0140 DL-7'nin Platform emsaliyle aynı (zorunlu claim kümesi tam, audience tek — SQ-3).
- **Repository:** `ISupplierActorBindingRepository` + Mongo uygulaması (oturumlu yazma: iş yazımı + denetim sonuç satırı
  aynı işlemde — Bölüm 21).
- **Komutlar (DL-1):** `CreateSupplierActorBindingCommand` (kur; gerekirse tedarikçi hesabını açar ve davet gönderir),
  `RevokeSupplierActorBindingCommand` (kaldır), `CorrectSupplierActorBindingCodeCommand` (kod düzelt, M2).
- **Komutlar (DL-2):** `SupplierLoginCommand`, `VerifySupplierMfaCommand`, `ResendSupplierMfaCommand`,
  `SelectSupplierBindingCommand`, `RefreshSupplierTokenCommand`, `LogoutSupplierCommand`.
- **Sorgular:** `GetSupplierActorBindingListQuery`, `GetSupplierActorBindingByIdQuery`,
  `GetSupplierActorBindingHistoryQuery`, `GetSupplierActorBindingDriftQuery` (isteğe bağlı, AC-10),
  `GetSupplierSessionMeQuery`, `GetSupplierBindingRevisionQuery` (S2S, D3).
- **Dış okuma istemcisi:** `ISupplierEligibilityClient` (Auth → Gateway → Procurement `GET /api/suppliers/{supplierId}/eligibility`
  ve isteğe bağlı `POST /api/suppliers/validate`; çağıran Admin'in bearer token'ı ve `X-Legal-Entity-Id` = bağın LE'si iletilir — D6).
- **Denetim:** `ISupplierAccessAuditWriter` (önce-yaz, deneme + sonuç satırı) + `ISupplierAccessAuditReader` (yalnız okuma).
- **API uçları (Auth, 5056; Gateway 5000 üzerinden):**
  - `GET /api/users/supplier-bindings` · `GET /api/users/supplier-bindings/{id}` · `GET /api/users/supplier-bindings/{id}/history`
  - `POST /api/users/supplier-bindings` · `POST /api/users/supplier-bindings/{id}/revoke` ·
    `POST /api/users/supplier-bindings/{id}/correct-code` · `GET /api/users/supplier-bindings/drift` (isteğe bağlı)
  - `POST /api/supplier-auth/login` · `POST /api/supplier-auth/mfa/verify` · `POST /api/supplier-auth/mfa/resend` ·
    `POST /api/supplier-auth/select` · `POST /api/supplier-auth/refresh-token` · `POST /api/supplier-auth/logout` ·
    `GET /api/supplier-auth/me`
  - `GET /api/internal/supplier-bindings/{bindingId}/revision` (S2S; audience SQ-3)
- **İzinler (PKS-001):** `auth.supplier-bindings.manage` (üç yazma komutu; D6), `auth.supplier-bindings.read` (liste /
  tekil / geçmiş / sapma — **öneri**, SQ-6). `supplier_user` token'ına konan `supplychain.supplier-portal.read` ve `.submit`
  bu paketin anahtarı değildir (MOD-0148 §14); verilme yolu SQ-7. **r3:** iki anahtar Auth'ta `ExplicitGrantOnlyPermissions`
  listesindedir — şirket kullanıcılarına otomatik yoldan (varsayılan rol şablonu, tam katalog / SuperAdmin, modül eşitlemesi)
  verilmez, yalnız Roller → İzinler ile (MOD-0148 D2 / K-5; AC-28).
- **Frontend rotası (DL-4):** `/Governance/SupplierBindings`.

## 4. Entity Fields

`SupplierActorBinding : EntityBase` (Auth; tenant-owned; `TenantId` sunucuda JWT'den çözülür, DTO'da yoktur).

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TenantId` | Guid | Evet | JWT `tenant_id`; istemciden alınmaz |
| `LegalEntityId` | Guid | Evet | İstekte verilir; uygunluk cevabı 200 olmadan kayıt yazılmaz (Bölüm 12) |
| `UserId` | Guid | Evet | Tedarikçi hesabının `User.Id`'si (aynı kiracı) |
| `SupplierCode` | string (≤ 32) | Evet | **Uygunluk cevabının `supplierId` alanı** (kanonik `SUP-xxxxxxxx`); istekteki kod değil (M2) |
| `SupplierMasterId` | Guid? | Hayır | Uygunluk cevabının `supplierMasterId` alanı; bilgi amaçlı, karar alanı değil |
| `Status` | enum `Active` / `Revoked` | Evet | Kurulunca `Active`; kaldırılınca `Revoked` |
| `Revision` | int | Evet | 1'den başlar; kaldırma ve kod düzeltmede +1; token'daki `binding_rev`. Adı `Version` değildir (entity-base-template: `Version` eşzamanlılık alanıdır) |
| `BoundAt` / `BoundBy` | DateTime / Guid | Evet | Kuran Admin |
| `RevokedAt` / `RevokedBy` / `RevokeReason` | DateTime? / Guid? / string? (≤ 500) | Kaldırmada | Gerekçe zorunlu |
| `IsDeleted` / `DeletedAt` | bool / DateTime? | Evet | Kaldırma = soft delete (`IsDeleted = true`, `DeletedAt`) + `Status = Revoked` |

**İndeksler (mongo-indexing):**
- `ux_supplier_binding_tenant_le_user_active` — tekil `{TenantId, LegalEntityId, UserId}`, kısmi `{Status: Active, IsDeleted: false}` (D5).
- `ix_supplier_binding_tenant_user` — `{TenantId, UserId, Status}` (giriş ve yenilemede bağ okuması).
- `ix_supplier_binding_tenant_code` — `{TenantId, SupplierCode}` (sapma sorgusu ve kod düzeltme).

**Tedarikçi hesabı (mevcut `User` kaydı):** e-posta tekilliği mevcut indeksle sağlanır — `ux_users_email_tenant_active`,
`{Email, TenantId}` kısmi `IsDeleted = false` (`services/Diten.AuthService/src/Diten.AuthService.Persistence/Configurations/MongoDbIndexConfigurations.cs:11-15`).
Ayırt edici alanın biçimi SQ-1'e bağlıdır; öneri `IsPlatformAccount` emsalidir (`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/User.cs:50`).

**`SupplierSelectionTicket : EntityBase` (DL-2, r2 F-3)** — çoklu bağda kod doğrulamasından sonra verilen tek kullanımlık bilet.

| Alan | Tip | Kural |
|---|---|---|
| `TenantId` / `UserId` | Guid | doğrulanan challenge'ın kiracısı ve kullanıcısı |
| `TicketHash` | string | biletin SHA-256 karması; bilet kendisi saklanmaz; tekil indeks `ux_supplier_selection_ticket_hash` |
| `AllowedBindingIds` | Guid[] | verildiği anda kişinin aktif bağları; seçim bu kümeyle ve güncel bağ okumasıyla sınanır |
| `SessionStartedAtUtc` | DateTime | kod doğrulaması anı; seçimle başlayan oturumun 8 saati buradan sayılır |
| `ExpiresAtUtc` | DateTime | verilişten 5 dk sonra; TTL indeksi `ix_supplier_selection_ticket_expires` (`ExpireAfter = 0`, `mfaChallenges` emsali `MongoDbIndexConfigurations.cs:98-99`) |
| `ConsumedAtUtc` | DateTime? | koşullu güncellemeyle bir kez yazılır (yarışta tek kazanan) |

**`RefreshToken` ek alanları (DL-2, r2 F-3; XMC-001):** bugün `SessionId` var, oturum başlangıcı ya da bitişi yok
(`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/RefreshToken.cs:33`, `:39`). Yalnız ekleme: `SessionStartedAtUtc`
(DateTime?) ve `SessionEndsAtUtc` (DateTime?); tedarikçi oturumunda dolu, döndürmede (rotation) değişmeden taşınır; eski ve kiracı
kayıtlarında boş kalır. `IRefreshTokenRepository` / `RefreshTokenRepository` döndürme yolu bu iki alanı yeni kayda kopyalar.

**`MfaChallenge` ek alanı (DL-2, r2 F-3; SQ-2):** `Door` (string, `tenant` varsayılan; tedarikçi kapısı `supplier` yazar). Bugün
kanal dışında kapı bilgisi yok (`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/MfaChallenge.cs:31-41`). Her
kapının `mfa/verify`'ı yalnız kendi kapısının challenge'ını kabul eder.

**RCS-001:** bağ kullanıcıya görünen bir iş belgesi değildir (kişi ↔ tedarikçi eşlemesi); ekranda e-posta, LE ve `SUP-` kodu
görünür, ayrı kayıt kodu üretilmez. **Öneri** — SQ-6 ile sahibe sorulur.

## 5. Repo Scope

Yollar DL başında yeniden ölçülür; burada adı geçmeyen dosyaya dokunulmaz (XMC-001).

**DL-1 (Auth)**
- Yeni: `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/SupplierActorBinding.cs`,
  `.../Domain/Enums/SupplierActorBindingStatus.cs`
- Yeni: `services/Diten.AuthService/src/Diten.AuthService.Application/Features/SupplierActorBindings/` (Bölüm 10 yapısı)
- Yeni: `.../Application/Common/Interfaces/ISupplierActorBindingRepository.cs`, `ISupplierEligibilityClient.cs`,
  `ISupplierAccessAuditWriter.cs`, `ISupplierAccessAuditReader.cs`
- Yeni: `.../Persistence/Repositories/SupplierActorBindingRepository.cs`, `SupplierAccessAuditWriter.cs`, `SupplierAccessAuditReader.cs`
- Yeni: `.../Infrastructure/Services/SupplierEligibilityClient.cs`
- Yeni: `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/SupplierActorBindingsController.cs` (`[Route("api/users/supplier-bindings")]`)
- Değişir: `.../Persistence/Configurations/MongoDbIndexConfigurations.cs` (üç indeks), `.../Persistence/DependencyInjection.cs`,
  `.../Infrastructure/DependencyInjection.cs`, `.../Domain/Entities/User.cs` + `.../Domain/Enums/AccountKind.cs` (SQ-1 cevabına göre)
- Değişir (XMC-001, Ali): `AssignRole` işleyicisi — tedarikçi hesabına rol reddi (AC-26)
- İzin kaydı: `auth.supplier-bindings.manage` / `.read` — Auth izin kataloğu tohumu (yer DL-1 başında ölçülür)
- Testler: `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SupplierBindings/` (yeni)
- Defter: `tests/architecture/audit-ledger/Diten.AuthService.md` (`## İzler` yeni satır),
  `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs` (`AcceptedTrails` yeni üçlü),
  `tests/architecture/audit-ledger/CT-DECISIONS.md` (Control Tower satırı)

**DL-2 (Auth)**
- Yeni: `.../Application/Features/SupplierAuth/` (komutlar, sorgular, işleyiciler, doğrulayıcılar)
- Yeni: `.../Api/Controllers/SupplierAuthController.cs` (`[Route("api/supplier-auth")]`),
  `.../Api/Controllers/SupplierBindingRevisionController.cs` (`[Route("api/internal/supplier-bindings")]`)
- Değişir: `.../Infrastructure/Services/TokenService.cs` (`supplier_user` token üreticisi), `.../Infrastructure/Settings/JwtSettings.cs`
  (tedarikçi ömürleri), `.../Api/appsettings.json` (`JwtSettings` altına iki yeni anahtar; mevcut iki anahtar değişmez)
- Değişir (XMC-001, Ali): `LoginCommandHandler.cs`, `VerifyMfaCommandHandler.cs`, `RefreshTokenCommandHandler.cs`,
  `ForcedChangeTenantPasswordCommandHandler.cs`, `TenantDoorPlatformAccountRefusal.cs` — kiracı kapısının tedarikçi hesabını reddi;
  `AuthController.cs` (`me`, `refresh-token`, `logout` uçlarında `supplier_user` reddi); `MfaChallengeService.cs` (challenge'ın kapısı — SQ-2)
- **r2 (F-3) eklenenler:**
  - Servis token'ı doğrulama (AC-19): yeni `.../Infrastructure/Security/SupplierBindingRevisionServiceTokenValidator.cs`; değişir
    `.../Infrastructure/DependencyInjection.cs` (`:42-43` kullanıcı `JwtBearer` şemasının yanına ayrı adlı ikinci şema + yetki politikası).
  - Seçim bileti (AC-17): yeni `.../Domain/Entities/SupplierSelectionTicket.cs`, `.../Application/Common/Interfaces/ISupplierSelectionTicketRepository.cs`,
    `.../Persistence/Repositories/SupplierSelectionTicketRepository.cs`; değişir `.../Persistence/Configurations/MongoDbIndexConfigurations.cs`
    (`supplierSelectionTickets` için tekil karma indeksi + TTL indeksi), `.../Persistence/DependencyInjection.cs`.
  - Challenge kapısı (SQ-2): değişir `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/MfaChallenge.cs` (`Door` alanı).
  - Mutlak oturum (AC-13): değişir `services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/RefreshToken.cs` (iki oturum alanı),
    `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IRefreshTokenRepository.cs` ve
    `services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RefreshTokenRepository.cs` (döndürmede iki alanın taşınması).
  - Testler: `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SupplierAuth/` (yeni).
- **r3 eklenenler (sahip kararı Ali 2026-11-24, Beste önerisi 2026-11-19; XMC-001 izni Ali'den, 2026-11-24):**
  - **DL-2.0 — otomatik verme dışı (AC-28):** değişir `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`
    (`:8` sınıf; `:108-114` `Keys` kümesi — iki sabit + iki girdi, yalnız ekleme). Listeyi okuyan yollar (`DefaultRolePermissionTemplate.cs:181-183`,
    `FullCatalogPermissionGrantService.cs:39`, `EntitlementPermissionSyncService.cs:293-295`) **değişmez**. Değişir (yalnız sayı / ad):
    `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Users/AccountKindTests.cs:60-80` (`:78` 13 → 15). Yeni test:
    `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/SupplierPortalExplicitGrantPermissionTests.cs`
    (emsal `Roles/LeaveExplicitGrantPermissionTests.cs`, `Permissions/FullCatalogPermissionGrantServiceTests.cs:61-64`,
    `Roles/EntitlementPermissionSyncServiceTests.cs:312-316`).
    **Sıra (bağlayıcı; SQ-17, Beste 2026-11-19, Ali onayı 2026-11-24):** DL-2.0 DL-2'nin **ilk commit'idir**; DL-2'nin katalog kapısından ("MOD-0148
    anahtarları Auth kataloğunda", Bölüm 22) muaftır; MOD-0148 P4'ten (anahtarların kataloğa girmesi) **önce** main'de olmalıdır;
    DL-1 kabulünü ve Bölüm 0.4 S0 kapanışını beklemez (SQ-18 KAPANDI, Ali 2026-11-24); yalnız r4'ün CT-APR-02 + CT-APR-01 onayını
    ve XMC-001 iznini (Bölüm 0.3) bekler. Kanıt AC-28 (f). DL-2'nin geri kalanı Bölüm 22 kapılarına bağlıdır.
  - **S2S ucunun kodları (AC-29, AC-30):** hata kodu sabiti `SUPPLIER_BINDING_NOT_FOUND` `SupplierAuth` özelliğinin kod sınıfında
    (yer DL-2 başında ölçülür; emsal `services/Diten.AuthService/src/Diten.AuthService.Application/Common/AuthRefusalCodes.cs` —
    kod `errorCodes`'ta taşınır); `service_name` kabul listesi denetimi (`Diten.SupplyChain`, SQ-3) yukarıdaki
    `SupplierBindingRevisionServiceTokenValidator.cs` / yetki politikasında; `X-Tenant-Id` başlık denetimi (yok / GUID değil / token'ın
    izinli kiracı kapsamında değil → 403; bugün servis token'ı tek `tenant_id` claim'i taşır,
    `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/ServiceIdentityTokenIssuer.cs:50`) aynı politikada,
    bağ okumasından önce. Ek dosya: Auth kiracı ara katmanı (aşağıda, r4).
- **r4 eklenenler (r3 incelemesi F-1; XMC-001 izni Ali'den, 2026-11-24):**
  - **Auth kiracı ara katmanı muafiyeti (AC-30):** değişir
    `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs` (yalnız ekleme). Neden:
    hat sırası `UseAuthentication` → `UseTenantResolution` → `UseAuthorization`
    (`services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs:221-223`); varsayılan şema kullanıcı `JwtBearer`
    (`.../Infrastructure/DependencyInjection.cs:42`), servis token'ı ayrı RSA anahtarı ve issuer ile imzalanır
    (`ServiceIdentityTokenIssuer.cs:44`, `:55`), bu yüzden ara katman servis token'ının `tenant_id`'sini görmez. Bugün bu uçta ara
    katman, politika 403 vermeden önce başlıksız ya da GUID olmayan başlıklı isteği 400 `Missing Tenant` ile çevirir
    (`TenantResolutionMiddleware.cs:101-111`, yazım `:110`), geliştirme ortamında dev bypass ile varsayılan kiracıya düşer
    (`:87-94`), kullanıcı token'ı + farklı kiracı başlığında 400 `Tenant mismatch` döner (`:68-81`); `/api/internal` yolu `:141`'deki
    `/internal` muafiyetine girmez.
  - **Kural (bağlayıcı):** `/api/internal/supplier-bindings` öneki (bu paketin `SupplierBindingRevisionController` rotası; uç
    `GET /api/internal/supplier-bindings/{bindingId}/revision`) için ara katmana adlı bir yol denetimi eklenir — emsal
    `IsServiceTokenIssuancePath` (`TenantResolutionMiddleware.cs:148-162`; özet yorumu `:148-160`, yöntem `:161-162`; kullanıldığı yerler
    `:84`, `:87`, `:103`): `StartsWithSegments`, büyük / küçük harften bağımsız, **yalnız bu önek** — `/api/internal`'ın tamamı ya da
    `/api/internal/supplier-bindings-…` gibi kardeş adlar değil. Bu önekte ara katman (1) kiracı zorunluluğunu (`:101-111`)
    uygulamaz, (2) dev bypass'ı (`:87-94`) uygulamaz, (3) BL-324 çelişki 400'ünü (`:68-81`) uygulamaz ve (4) `TenantContext`'i
    doldurmaz; isteği uca geçirir. Kiracı kararı uç politikasındadır: `X-Tenant-Id` yok / GUID değil / servis token'ının
    `tenant_id`'sine eşit değil → 403, bağ okunmaz (Bölüm 13, 14, AC-30). İşleyici kiracıyı politikanın doğruladığı başlık
    değerinden alır ve sorguya açıkça geçirir; ortam `TenantContext`'ini okumaz. Bu önekin altına başka uç eklenmesi paket
    revizyonu ister.
  - **BL-324 / DCP-004 §7.4 ile ilişki:** başlık ile kullanıcı token'ının farklı kiracı göstermesi bütün öteki yollarda 400
    `Tenant mismatch` kalır (`TenantResolutionMiddleware.cs:59-64` yorumu, `:68-81`); emsal `IsServiceTokenIssuancePath` de çelişkiyi
    400 tutar. Bu önekte çelişki denetimi bilerek uygulanmaz: uç yalnız kendi adlı şemasındaki servis token'ını kabul eder, kullanıcı
    token'ı burada **şema gereği 401**'dir (Bölüm 14); servis token'ı ile başlığın farklı kiracı göstermesi biçim hatası değil yetki
    kararıdır → 403 (Ali, F-8, 2026-11-24). Çelişki denetimi bu önekte kalsaydı kullanıcı token'ı + farklı başlık 401 yerine 400
    alır, 401 → 403 → 404 sırası bozulurdu.
  - Uygulama S2S ucuyla aynı commit'tedir (DL-2.0'da değil); DL-2 kapılarına bağlıdır (Bölüm 22).
  - Testler: yeni `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/SupplierAuth/BindingRevisionTenantMiddlewareExemptionTests.cs`
    (gerçek ara katman tek başına, iki ortam; emsal `Tenancy/ServiceTokenEndpointTenantExemptionTests.cs:36-110`) ve
    `SupplierAuth/BindingRevisionPipelineHttpTests.cs` (gerçek Api hattı, Development dışı ortam; emsal aynı dosya `:176-297`).
    Emsal testler (`ServiceTokenEndpointTenantExemptionTests.cs:55-72`, `:267-285`) değişmez ve yeşil kalır.

**DL-3 (Gateway — integration-agent / MOD-0032 sahibi)**
- Değişir: `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` (izin listesi + F4)
- Değişir (yalnız `integration-agent`): `gateway/Diten.ApiGateway/ocelot.json` (iki rota)
- Testler: gateway test projesi (yer DL-3 başında ölçülür)

**DL-4 (Web, isteğe bağlı)**
- Yeni: `frontend/Diten.Web/Views/Governance/SupplierBindings/` (Bölüm 11), `wwwroot/assets/js/Governance/SupplierBindings/`,
  `Resources/Views/Governance/SupplierBindings/SupplierBindingsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, MVC vekil denetleyici
  (`Controllers/SupplierBindingsController.cs`), sayfa yardımı kaydı.

## 6. Protected Paths

- `.antigravity/**` — kullanıcı onayı olmadan değişmez.
- `gateway/Diten.ApiGateway/ocelot.json` — yalnız `integration-agent`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `Controllers/Archive/**`, `Views/Archive/**` — FROZEN.
- `services/Diten.SupplyChainService/**` (MOD-0148), `services/Diten.ProcurementService/**` (MOD-0140),
  `services/Diten.MdmService/**`, `services/Diten.Platform/**` — bu paket yalnız uçlarını çağırır, dosyalarına dokunmaz.
- `docs/analysis/contracts/supplier.openapi.yaml` — MOD-0140 sözleşmesi; bu paket yalnız okur.
- Auth'un bu paketin listesinde olmayan her dosyası (XMC-001).
- `services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.json` içindeki `AccessTokenExpirationMinutes` /
  `RefreshTokenExpirationDays` değerleri — kiracı kullanıcılarının ömrü değişmez (D4).

## 7. Dependencies

| Bağımlılık | Durum (2026-11-13, HEAD `e8128c224`) | Etkilediği dilim |
|---|---|---|
| MOD-0140 DL-3 uygunluk ucu `GET /api/suppliers/{supplierId}/eligibility` main'de + Gateway üzerinden canlı kanıt (gerçek 200, gerçek 404 `UNKNOWN_SUPPLIER`, kabul edilmeyen token reddi) | main'de değil (SQ-9) | **DL-1 başlamaz** |
| Admin rolünde `procurement.suppliers.eligibility.read` (ya da `.read`) ve MOD-0140 S-2 ara çözümü `mdm.legal-entities.read` | rol yapılandırması; DL-1 başında ölçülür | DL-1 |
| Auth K2 denetim yolu: önce-yaz `IAuthAuditService.WriteOnceAsync` | var (`services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IAuthAuditService.cs:18`) | DL-1, DL-2 |
| Kabul edilmiş Auth denetim izi | `auth-gunlugu` `aday` (`tests/architecture/audit-ledger/Diten.AuthService.md`, `## İzler`); yeni iz için Control Tower defter kararı gerekir (Bölüm 21) | **DL-1 başlamaz** |
| Auth → Platform merkezi iletici (meta-denetim) | `IPlatformAuditForwarder` var; anahtar boşsa sessiz düşer — geçmiş ucu bunu 503 ile karşılar (MOD-0018-FU27 K-W5 emsali) | DL-1 |
| MOD-0148 izin anahtarları `supplychain.supplier-portal.read` / `.submit` Auth kataloğunda | MOD-0148 manifestiyle girer; DL-2 başında ölçülür | DL-2 |
| **r3:** iki anahtar `ExplicitGrantOnlyPermissions`'ta **MOD-0148 P4 itilmeden önce** (MOD-0148 §0.0 P1: "requested from the Auth owner (Ali) before P4 is pushed"). Liste yalnız **yeni** otomatik vermeyi durdurur, var olan rol-izin satırını silmez (`ExplicitGrantOnlyPermissions.cs:38-40` yorumu); P4 önce gelirse manifest kaydı `InternalPermissionsController.cs:96`'da anahtarı SuperAdmin'e anında verir | DL-2.0 (DL-2'nin ilk commit'i; DL-1 kabulünü beklemez — SQ-17); main'de değil | **MOD-0148 P4'ü bekletir** (Bölüm 22) |
| S2S `binding_rev` ucu için audience ve SupplyChain `service_name` | SQ-3 (MOD-0140 S-5 ile aynı sahip) | DL-2 (AC-19) |
| **r4 (F-1):** Auth kiracı ara katmanı (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs`) `/api/internal/supplier-bindings` önekini kiracı zorunluluğundan, dev bypass'tan ve BL-324 çelişki 400'ünden muaf tutar (Bölüm 5 r4 kuralı); bugün tutmaz — `Program.cs:221-223` sırasıyla ara katman politikadan önce çalışır, `:101-111` 400 `Missing Tenant`, `:87-94` dev bypass, `:141` muafiyeti `/api/internal`'a işlemez | XMC-001 izni Ali'den, 2026-11-24 (Bölüm 0.3); kod yok | DL-2 (AC-19, AC-30) |
| MOD-0140 DL-7 servis token'ı (MOD-0148'in uygunluk okuması) | Cihan; SQ-10 | bu paketi bekletmez; MOD-0148'i bekletir |
| `integration-agent` görevi (ocelot iki rota) | açılmadı | DL-3 |
| MOD-0032 FU (internete açılma kapıları) | numarası ve sahibi TBD | portalın internete açılması (bu paket değil) |

## 8. Runtime Constraints

- Servis: Auth 5056; dış erişim yalnız Gateway 5000 üzerinden (AGENTS.md §3). Web 5001 servislere doğrudan gitmez.
- Auth → Procurement çağrısı Gateway 5000 üzerinden `/api/suppliers/{everything}` rotasıyla (mevcut) yapılır; adres
  yapılandırmadan okunur, port koda yazılmaz.
- Kiracı izolasyonu: her sorgu ve komut `TenantId` (JWT) ile sınırlıdır; başka kiracının bağı 404. **S2S istisnası (r4, r3
  incelemesi bilgi notu 1):** `binding_rev` ucunda kullanıcı JWT'si yoktur; kiracı `X-Tenant-Id` başlığından okunur ve uç
  politikasında servis token'ının `tenant_id`'siyle karşılaştırılır (eşit değil / yok / GUID değil → 403, bağ okunmaz); sorgu bu
  doğrulanmış kiracıyla sınırlıdır, başka kiracının bağı 404 `SUPPLIER_BINDING_NOT_FOUND` (Bölüm 5 r4, 13, 14; AC-29, AC-30).
- Soft delete: kaldırma `IsDeleted` + `DeletedAt`; kayıt fiziksel silinmez.
- Eşzamanlılık: kaldırma ve kod düzeltme `expectedRevision` taşır; uyuşmazlık 409, sessiz üzerine yazma yok.
- Token ömürleri: `supplier_user` erişim 15 dk, oturum (yenileme anahtarı) mutlak 8 saat — yeni `JwtSettings` anahtarları
  `SupplierAccessTokenExpirationMinutes = 15`, `SupplierSessionMaxHours = 8`. Kiracı ömürleri (`appsettings.json:49-50`,
  120 dk / 7 gün) değişmez.
- **Erişim token'ı oturum sonunu geçmez (sahip, r2 F-2):** `exp = min(nbf + 15 dk, SessionEndsAtUtc)`. Oturum, kod doğrulamasının
  anından tam 8 saat sonra biter (`SessionEndsAtUtc = SessionStartedAtUtc + 8 sa`); ör. 7 sa 50 dk'da yenilenen token'ın ömrü
  10 dk'dır, 7 sa 58 dk'da yenilenenin 2 dk. Yenileme anahtarının bitişi de `SessionEndsAtUtc`'dir.
- Oturum mutlaktır: kiracı yenilemesi her dönüşte ömrü kiracı ayarından yeniden başlatır
  (`services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs:246-249`);
  tedarikçi yenilemesi ilk oturumun bitişini taşır, uzatmaz.
- **Yalnız iç ağ (v1).** `TrustedProxies`, MOD-0032 rate limit ve paylaşılan limiter deposu olmadan tedarikçi kapısı ve
  portal internete açılmaz (C4). Bu kapıların inşası bu paketin dışındadır.
- Kişisel veri: tedarikçi kişisinin e-postası, adı ve oturum kayıtları kiracı dışından gelen kişisel veridir; denetim
  satırına parola, kod, token ve bilet yazılmaz (Bölüm 21).

## 9. Layout & Shell Contract

- `shell: tenant` (yalnız DL-4 ekranı; DL-1…DL-3 ekransızdır).
- `frontend/Diten.Web/Views/Governance/SupplierBindings/*.cshtml` dosyalarında **`Layout = "_LayoutTenantShell";` AÇIKÇA**
  yazılır; `_ViewStart.cshtml` varsayılanına güvenilmez. Emsal: `frontend/Diten.Web/Views/Governance/Users/Index.cshtml:7`.
- View klasörü: `Views/Governance/SupplierBindings/`; rota `/Governance/SupplierBindings`.
- Tedarikçi kişisinin göreceği ekran (portal) bu paketin değildir.

## 10. Backend File Convention

Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`):

```text
services/Diten.AuthService/src/Diten.AuthService.Application/Features/SupplierActorBindings/
├── Commands/
│   ├── CreateSupplierActorBindingCommand.cs
│   ├── RevokeSupplierActorBindingCommand.cs
│   └── CorrectSupplierActorBindingCodeCommand.cs
├── Queries/
│   ├── GetSupplierActorBindingListQuery.cs
│   ├── GetSupplierActorBindingByIdQuery.cs
│   ├── GetSupplierActorBindingHistoryQuery.cs
│   └── GetSupplierActorBindingDriftQuery.cs
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateSupplierActorBindingHandler.cs
│   │   ├── RevokeSupplierActorBindingHandler.cs
│   │   └── CorrectSupplierActorBindingCodeHandler.cs
│   └── QueryHandlers/
│       ├── GetSupplierActorBindingListHandler.cs
│       ├── GetSupplierActorBindingByIdHandler.cs
│       ├── GetSupplierActorBindingHistoryHandler.cs
│       └── GetSupplierActorBindingDriftHandler.cs
├── Validators/
│   ├── CreateSupplierActorBindingValidator.cs
│   ├── RevokeSupplierActorBindingValidator.cs
│   └── CorrectSupplierActorBindingCodeValidator.cs
└── SupplierActorBindingsModels.cs

services/Diten.AuthService/src/Diten.AuthService.Application/Features/SupplierAuth/
├── Commands/   SupplierLoginCommand, VerifySupplierMfaCommand, ResendSupplierMfaCommand,
│               SelectSupplierBindingCommand, RefreshSupplierTokenCommand, LogoutSupplierCommand
├── Queries/    GetSupplierSessionMeQuery, GetSupplierBindingRevisionQuery
├── Handlers/CommandHandlers/  {Verb}Supplier...Handler (6)
├── Handlers/QueryHandlers/    GetSupplierSessionMeHandler, GetSupplierBindingRevisionHandler
├── Validators/ SupplierLoginValidator, VerifySupplierMfaValidator, SelectSupplierBindingValidator
└── SupplierAuthModels.cs
```

- Komut / sorgu `sealed record`, işleyici `sealed class`; işleyici ve doğrulayıcı adında `Command` / `Query` eki YOK.
- `BulkDelete` yok: toplu kaldırma kapsam dışıdır (her kaldırma gerekçe ve revizyon ister).
- Denetleyiciler `CustomBaseController` + `Response<T>` (response-envelope).

## 11. Frontend File Contract

DL-4 (isteğe bağlı) — Slim seti, `golden_reference: slim`, `data_mode: server` (bir kiracının tedarikçi kişisi sayısı
tasarımda sınırlı değildir):

```text
frontend/Diten.Web/Views/Governance/SupplierBindings/
├── Index.cshtml                    (Layout = "_LayoutTenantShell" AÇIKÇA)
├── _Filter.cshtml                  (LE, durum, tedarikçi kodu, e-posta)
├── _DataTable.cshtml               (data-dt-standard="v2", data-dt-data-mode="server", skeleton)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml     (kur formu; ayrı "kod düzelt" ve "kaldır" panelleri aynı dosyada iki form)
├── _DetailsQuickView.cshtml        (bağ + geçmiş satırları)
└── SupplierBindingsIndex.cs

frontend/Diten.Web/wwwroot/assets/js/Governance/SupplierBindings/
├── index.js
└── index.l10n.js
```

- Kur formu kullanıcı alanları (form_field_count = 5): e-posta, ad, soyad, tüzel kişilik (LE), tedarikçi kodu.
- Lookup kararı: LE seçimi mevcut MDM LE okumasından (Web vekili + Gateway); tedarikçi seçimi MOD-0140 `GET /api/suppliers/eligible`
  (Admin'in kullanıcı izniyle, seçilen LE için). Sabit yedek liste YOK; tarayıcı JS servis portlarını çağırmaz.
- Tek mantıksal ekran; liste sayfası dokunma protokolü (`verify_datatable_page.py --area Governance --module SupplierBindings`) uygulanır.

## 12. Validation Rules

| Alan / koşul | Zorunlu | Kural | DB-level | Ön kontrol |
|---|---|---|---|---|
| `Email` (kur) | Evet | trim, küçük harf, e-posta biçimi, ≤ 256 | `ux_users_email_tenant_active` | aynı kiracıda hesap var mı; varsa tedarikçi hesabı mı (AC-6) |
| `FirstName` / `LastName` (kur, yeni hesapta) | Evet (yeni hesap) | trim, ≤ 100 | — | — |
| `LegalEntityId` | Evet | boş olmayan Guid | — | uygunluk cevabı 200 (Procurement LE'yi kiracıda çözer) |
| `SupplierCode` (istek) | Evet | trim, ≤ 64; kanonik ya da eski kod ya da uuid (MOD-0140 §3.1 kod çözümü) | — | uygunluk okuması |
| Saklanan `SupplierCode` | — | **cevabın `supplierId`'si**, `^SUP-` ile başlar; değilse 502/503 sınıfı ret, yazma yok | — | — |
| Uygunluk | Evet | üçü birden: `relationshipExists = true` ve `eligibleForTransactions = true` (MOD-0140 S-1); `onboardingStatus = Approved`; `purchasingEnabled = true` (r2, F-5). `null` değer "uygun değil" sayılır. Alanlar sözleşme 1.3.0'da tanımlı: `docs/analysis/contracts/supplier.openapi.yaml:362-372` (`SupplierEligibility`), `OnboardingStatus` enum `Draft / InReview / Approved / Rejected` (`:319`); `eligibleForTransactions` onboarding ve `purchasingEnabled`'ı hesaba katmaz, bu yüzden ikisi ayrıca denetlenir (`:372`) | — | — |
| Tekil aktif bağ | Evet | kiracı + LE + kullanıcı başına bir `Active` | `ux_supplier_binding_tenant_le_user_active` | var mı |
| `Reason` (kaldır, kod düzelt) | Evet | trim, 3–500 | — | — |
| `expectedRevision` (kaldır, kod düzelt) | Evet | pozitif tam sayı, kayıttakine eşit | koşullu güncelleme | — |
| Yeni kanonik kod (düzelt) | Evet | cevabın `supplierId`'si kayıttakinden farklı | — | — |
| Tedarikçi girişi | Evet | e-posta + parola; isteğe bağlı `legalEntityId` | — | aktif bağ ve tedarikçi hesabı (yoksa genel 401) |
| OTP | Evet | 6 hane, challenge tek kullanımlık, süresi dolmamış, **tedarikçi kapısına ait** | `ux_mfa_challenge_hash` | — |
| Seçim bileti | Evet (çoklu bağda) | tek kullanımlık, 5 dk, karması saklanır; seçilen LE kişinin aktif bağı | — | — |
| İstemcinin `supplierId` / LE taşıması (portal) | — | MOD-0148'in kuralı (D8: 400); bu paket token'a yalnız bağdan koyar | — | — |

## 13. Failure Path to Verify

Bağ yönetimi (DL-1):
- **Yinelenen aktif bağ** (aynı kiracı + LE + kullanıcı): 409 `SUPPLIER_BINDING_EXISTS`; eşzamanlı iki kurulumda biri kazanır,
  diğeri 409 (tekil indeks); sıfır ek yazma.
- **E-posta kiracı hesabına ait** (aynı kiracı, tedarikçi olmayan hesap): 409 `SUPPLIER_BINDING_EMAIL_IS_TENANT_ACCOUNT`; yazma yok.
- **Tedarikçi uygun değil**: 422 `SUPPLIER_NOT_ELIGIBLE`, `details.reason` ilk tutmayan koşul — `NOT_ACTIVE`
  (`eligibleForTransactions = false`), `ONBOARDING_NOT_APPROVED` (`onboardingStatus` `Approved` değil ya da `null`),
  `PURCHASING_DISABLED` (`purchasingEnabled` `true` değil ya da `null`) (r2, F-5); yazma yok. Kod düzeltmede aynı kural.
- **Tedarikçi bilinmiyor / LE'de ilişki yok** (Procurement 404): 422 `SUPPLIER_BINDING_SUPPLIER_UNKNOWN`; yazma yok.
- **Admin'in uygunluk okuma izni yok** (Procurement 403): 403 `SUPPLIER_BINDING_ELIGIBILITY_FORBIDDEN`; yazma yok.
- **Procurement erişilemez / 5xx / zaman aşımı / beklenmeyen gövde**: 503 `SUPPLIER_BINDING_ELIGIBILITY_UNAVAILABLE`; yazma yok.
- **Eksik alan**: 400 + doğrulayıcı mesajı; yazma yok.
- **Eşzamanlılık** (`expectedRevision` eski): 409 `SUPPLIER_BINDING_REVISION_CONFLICT`; sessiz üzerine yazma yok.
- **Kod değişmedi** (düzeltmede yeni kanonik = eski): 422 `SUPPLIER_BINDING_CODE_UNCHANGED`.
- **Yetkisiz aktör** (`auth.supplier-bindings.manage` yok): 403; yazma yok. Başka kiracının bağı: 404.
- **Denetim deneme satırı yazılamıyor**: 503 `SUPPLIER_BINDING_AUDIT_UNAVAILABLE`; iş verisi yazılmaz (K2).
- **İş yazımı ya da sonuç satırı düşer**: işlem geri alınır, komut başarısız; geçmişte deneme "sonuçlanmadı" görünür.

Tedarikçi kapısı (DL-2), D8 sırası:
- Token yok / geçersiz → 401.
- Tedarikçi kapısında: bilinmeyen e-posta, kiracı hesabı, platform hesabı, aktif bağı olmayan tedarikçi hesabı, yanlış parola,
  kilitli hesap → **aynı genel 401** (kod, gövde ve süre aynı; `SignInTiming` emsali, LoginCommandHandler.cs:103-114).
- Kiracı kapısında tedarikçi hesabı (giriş, MFA doğrulama, yenileme, zorunlu parola değişimi) → kiracı kapısının genel 401'i.
- `supplier_user` token'ıyla `/api/auth/me`, `/api/auth/refresh-token`, `/api/auth/logout`, `/api/tenant-auth/*` → 401/403, token
  verilmez.
- Yenilemede bağ kaldırılmış / silinmiş → 401 + oturum ailesi iptal.
- Oturum 8 saati aşmış → 401 `SUPPLIER_SESSION_EXPIRED`; yeniden giriş + e-posta kodu.
- Başka kapının challenge'ı ile doğrulama → 401.
- S2S `binding_rev` ucunda (r3, sıra 401 → 403 → 404): servis token'ı yok / imza geçersiz / yanlış audience / kullanıcı token'ı
  → **401**; token geçerli ama `service_name` kabul listesinde (`Diten.SupplyChain`, SQ-3) değil → **403** (SQ-3 kaydı
  `dc03-supplier-user-binding-owner-decisions-01.md:34`; bağ okunmaz, kiracı başlığına bakılmaz); kiracı yalnız `X-Tenant-Id`
  başlığından okunur — başlık yok / GUID değil / servis token'ının izinli kiracı kapsamında değil → **403** (aynı 403 gövdesi, ayrı
  kod adı yok; bağ okunmaz; Beste 2026-11-19, Ali onayı 2026-11-24); bu kiracıda bağ yok — başka kiracının bağı ya da hiç olmayan kimlik, ikisi aynı
  gövde → **404 `SUPPLIER_BINDING_NOT_FOUND`** (SQ-16 kararı).
  Kaldırılmış (soft delete) bağ 404 değildir: 200 `active = false` (AC-19).
  **Auth kiracı ara katmanı bu sırayı bozmaz (r4, F-1):** `/api/internal/supplier-bindings` önekinde ara katman kiracı kararı
  vermez (Bölüm 5 r4 kuralı) — token da başlık da yok → **401** (400 `Missing Tenant` değil); kullanıcı token'ı + farklı kiracı
  başlığı → **401** (400 `Tenant mismatch` değil); geçerli servis token'ı + başlık yok / GUID değil → **403** (400 değil); geliştirme
  ortamında dev bypass varsayılan kiracıya düşmez, başlıksız istek orada da 403'tür.
- Denetim satırı yazılamıyor → 503, token / oturum verilmez.

## 14. Authorization Convention

- **Bağ yönetimi:** `[Authorize]` + `[HasPermission("auth.supplier-bindings.manage")]` (kur, kaldır, kod düzelt);
  `[HasPermission("auth.supplier-bindings.read")]` (liste, tekil, geçmiş, sapma — öneri, SQ-6). Aktör `tenant_user`;
  `supplier_user` bu uçlarda 403. Uçlar `actor_type = tenant_user` dışını ayrıca reddeder (emsal
  `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/AuthController.cs:112`, sabit `:177`).
- **PKS-001:** `auth.supplier-bindings.manage` ve `.read` — 3 segment, küçük harf, çok kelime kebab-case; `auth` ad alanı
  Auth'undur. `manage` Tier 3 eylemdir ve bu pakette kaydedilir (tek anahtar üç komutu kapsar — D6 metni).
- **Tedarikçi kapısı:** `login`, `mfa/verify`, `mfa/resend`, `select`, `refresh-token` anonim (kendi kanıtıyla — parola,
  challenge, bilet, yenileme anahtarı); `logout` ve `me` yalnız `actor_type = supplier_user` token'ı. Emsal kapı ayrımı:
  `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs:617` (`IsPlatformActorToken`) ve
  `:717-721` (`NormalizeActorType`).
- **`supplier_user` token içeriği:** `sub`, `tenant_id`, `actor_type = supplier_user`, `supplier_id` (kanonik `SUP-`),
  `legal_entity_id`, `binding_id`, `binding_rev`, `amr = ["pwd","otp"]`, `jti`, `iat`, `nbf`, `exp`
  (`exp = min(nbf + 15 dk, oturum sonu)`, r2 F-2);
  `permission` claim'leri **tam olarak** `supplychain.supplier-portal.read` ve `supplychain.supplier-portal.submit`; rol claim'i
  yok. Verilme yolu (sabit küme mi, kiracı rolü mü) SQ-7; öneri sabit küme (D2: kiracı rolü taşımaz).
- **S2S `binding_rev` ucu:** yalnız güvenilir servis token'ı (Auth'un kendi `ServiceClientIdentity` kaydı; audience ve
  `service_name` listesi SQ-3); kullanıcı token'ı bu uçta 401. **r3:** kimlik doğrulama (imza, audience) başarısız → 401;
  doğrulanmış ama listede olmayan `service_name` → **403** (yetki reddi; 401 değil — SQ-3 kaydı :34, MOD-0148 inceleme F-8);
  kiracı yalnız `X-Tenant-Id` başlığından okunur — başlık yok / GUID değil / token'ın izinli kiracı kapsamında değil (bugün tek
  `tenant_id` claim'i, `ServiceIdentityTokenIssuer.cs:50`) → **403**, aynı gövde, ayrı kod adı yok (Beste 2026-11-19, Ali onayı 2026-11-24; AC-30);
  bu dallarda bağ okunmaz. Sıra: 401 → 403 (`service_name`, sonra kiracı başlığı) → 404. Bu kiracıda bulunmayan bağ → 404
  `SUPPLIER_BINDING_NOT_FOUND` (SQ-16 kararı, Beste 2026-11-19, Ali onayı 2026-11-24; AC-29). MOD-0148
  P3 bu kodu "bağ eskidi" diye tanır, öteki 404'leri 503 sayar (MOD-0148 §0.0 P3, T-0148-V1-BREV-01).
  **r4 (F-1):** bu sıra uç politikasındadır ve Auth kiracı ara katmanı önünde bozulmaz — ara katman
  (`Program.cs:221-223`'te `UseAuthorization`'dan önce) `/api/internal/supplier-bindings` önekinde kiracı zorunluluğunu, dev bypass'ı
  ve BL-324 çelişki 400'ünü uygulamaz, `TenantContext`'i doldurmaz (Bölüm 5 r4 kuralı); 400 bu uçta hiçbir dalda dönmez. BL-324
  / DCP-004 §7.4'ün 400'ü öteki bütün yollarda kalır; bu uçta kullanıcı token'ı şema gereği 401'dir.
- **Portal anahtarlarının şirket kullanıcılarına verilmesi (r3, AC-28):** `supplychain.supplier-portal.read` ve `.submit`
  `ExplicitGrantOnlyPermissions` listesindedir; varsayılan rol şablonu (SuperAdmin tam katalog dalı, Admin, Viewer okuma dalı),
  `FullCatalogPermissionGrantService` ve modül yetkilendirme eşitlemesi bu iki anahtarı vermez; tek yol bir yetkilinin Roller →
  İzinler ile açık ataması (`AssignPermissionCommandHandler`). Anahtarların anlamı değişmez (PKS-001 "published key" kuralı;
  liste yalnız verme yolunu daraltır). `supplier_user` token'ının sabit kümesi rol / şablon yolundan geçmez, liste onu etkilemez
  (K-1, AC-12).
- **Kiracı kapısı:** tedarikçi hesabını hesap düzeyinde reddeder (`IsPlatformAccount` emsali,
  `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs:108-114`).
- **Rol ataması:** tedarikçi hesabına kiracı rolü atanamaz → 422 `SUPPLIER_ACCOUNT_ROLE_FORBIDDEN` (AC-26, XMC-001).

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gerekli** (DL-3); `ocelot.json` kısmı `integration-agent` görevidir, bu paket yazmaz.

- Bağ yönetimi `/api/users/supplier-bindings/**` altında kalır → mevcut `/api/users/{everything}` rotası kapsar
  (`gateway/Diten.ApiGateway/ocelot.json:2243-2251`, Auth 5056) → **rota değişikliği gerekmez**.
- Tedarikçi kapısı `/api/supplier-auth/{everything}` → Auth 5056: **yeni rota** (emsal `platform-auth` ve `tenant-auth`
  rotaları, `ocelot.json:2167-2194`), OPTIONS dahil.
- Portal `/api/supplier-performance/portal/{everything}` → SupplyChain 5066: **yeni rota**; bugün `ocelot.json`'da
  `supplier-performance` geçmez (2026-11-13 ölçümü).
- S2S `/api/internal/supplier-bindings/**` gateway'den **geçmez** (servisler arası iç çağrı); Gateway'e rota eklenmez.
- `TenantResolutionMiddleware`:
  - `supplier_user` için izin listesi: yalnız `/api/supplier-performance/portal/*` ve `/api/supplier-auth/*`; başka her yolda
    403 "Forbidden Actor". Bugün `tenant_user` dışı her aktör kiracı uçlarında 403 alır — portal dahil
    (`gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:229-236`), bu yüzden izin listesi zorunludur.
  - `/api/supplier-auth/login`, `/mfa/verify`, `/mfa/resend`, `/select`, `/refresh-token` kamu uçları listesine eklenir
    (emsal `IsPublicEndpoint`, `TenantResolutionMiddleware.cs:438-449`); kiracı çözümü kiracı kapısıyla aynı (host ya da `X-Tenant-Id`).
  - **F4 sertleştirmesi:** üç dal da `!string.IsNullOrWhiteSpace(actorType) &&` ile başlıyor — `actor_type` claim'i olmayan imzalı
    token kiracı kullanıcısı gibi geçer: kişiselleştirme `:147-151`, servis kararlı başlık çatışması `:186-191`, genel kiracı ucu
    `:229-236` (r2, F-10: satır atıfları HEAD `70cb6ca01`'de yeniden ölçüldü). DL-3'te claim yoksa 403. **Bütün kiracı uçlarını
    etkiler** — regresyon testi zorunlu (AC-22).
  - **Yalnız kimliği doğrulanmış istek (r2, F-1):** `actorType` kimliği doğrulanmamış `context.User`'dan da `null` döner
    (`TenantResolutionMiddleware.cs:69`, `ReadActorType(context.User)`); kişiselleştirme ve servis kararlı dallar kamu uçlarını
    dışarıda bırakmaz. Bu yüzden yeni kural `context.User.Identity?.IsAuthenticated == true` **ve** claim yok → 403 biçimindedir.
    Tokensız istek ve süresi dolmuş / reddedilmiş token'la gelen istek bugünkü davranışını korur (kamu uçlarında geçer; korumalı
    uçta 401 — 403 değil), böylece istemcinin 401'e bağlı sessiz yenilemesi bozulmaz. Süresi dolmuş token'ın Gateway kimlik
    doğrulayıcısındaki davranışı (`GatewayJwtAuthenticationHandler`) DL-3 başında ölçülür ve AC-22 testine sabitlenir.
  - `tenant_user` token'ı portal yoluna gateway'den geçer; ret MOD-0148'in bildirdiği kodlarla serviste verilir (MOD-0148 §0.0).

## 16. Acceptance Criteria

Her madde üretim koduyla ölçülür; testin kopyası değil, gerçek işleyici / uç / middleware sınanır. Her maddenin adlı
sabotajı vardır: üretim kodunda yazılan değişiklik testi kırmızıya çevirmelidir; çevirmiyorsa test bekçi sayılmaz.

**DL-1 — bağ kaydı**

- [ ] AC-1 (zorunlu) — Kur: Admin istekte eski kod ya da uuid verdiğinde kayıt, uygunluk cevabının `supplierId`'sini
  (kanonik `SUP-`) saklar; `Status = Active`, `Revision = 1`, `TenantId` JWT'den. Sabotaj: işleyicide saklanan değeri istekteki
  koda çevirmek → test kırmızı.
- [ ] AC-2 (zorunlu) — Uygunluk kapalı-başarısız: `eligibleForTransactions = false` → 422 `SUPPLIER_NOT_ELIGIBLE`; Procurement 404 →
  422 `SUPPLIER_BINDING_SUPPLIER_UNKNOWN`; 403 → 403 `SUPPLIER_BINDING_ELIGIBILITY_FORBIDDEN`; zaman aşımı / 5xx / bağlantı hatası /
  `supplierId` `SUP-` ile başlamayan gövde → 503; her dalda `supplierActorBindings` ve `users` koleksiyonunda sıfır yazma. Sabotaj:
  istemcide zaman aşımını "uygun" sonuca eşlemek ya da uygunluk çağrısını atlamak → test kırmızı.
- [ ] AC-3 (zorunlu) — Tekil aktif bağ: aynı kiracı + LE + kullanıcıya ikinci kur → 409 `SUPPLIER_BINDING_EXISTS`; eşzamanlı iki
  kurulumdan biri 409 alır (indeks). Aynı kişinin aynı kiracıda başka LE'de bağı kurulabilir (D5). Sabotaj: indeksi
  `MongoDbIndexConfigurations.cs`'ten çıkarmak ve ön kontrolü kaldırmak → eşzamanlılık testi kırmızı.
- [ ] AC-4 (zorunlu) — Kiracı izolasyonu: kiracı B token'ıyla kiracı A'nın bağı için tekil / geçmiş / kaldır / düzelt → 404; liste
  yalnız kendi kiracısını döner. Sabotaj: repository okumasından `TenantId` filtresini çıkarmak → test kırmızı.
- [ ] AC-5 (zorunlu) — Yetki: `auth.supplier-bindings.manage` olmadan üç komut 403, sıfır yazma; `.read` olmadan sorgular 403;
  `supplier_user` token'ı bu uçlarda 403. Sabotaj: denetleyicide `[HasPermission]` özniteliğini kaldırmak → test kırmızı.
- [ ] AC-6 (zorunlu) — Hesap: yeni e-posta → aynı kiracıda tedarikçi hesabı açılır (SQ-1 ayırt edicisiyle) ve davet bağlantısı
  gönderilir; e-posta aynı kiracıda tedarikçi olmayan bir hesaba aitse 409 `SUPPLIER_BINDING_EMAIL_IS_TENANT_ACCOUNT`, yazma yok;
  aynı e-posta başka kiracıda ayrı hesaptır. Sabotaj: tedarikçi olmayan hesabı bağlamaya izin vermek → test kırmızı.
- [ ] AC-7 (zorunlu) — Kaldır = soft delete: `IsDeleted = true`, `DeletedAt`, `Status = Revoked`, `Revision + 1`, gerekçe zorunlu;
  kayıt listeden düşer, geçmişi okunur; fiziksel silme yok. Sabotaj: repository'de `DeleteOneAsync` kullanmak → test kırmızı.
- [ ] AC-8 (zorunlu) — Kod düzelt (M2): yeni uygunluk okuması yapılır, AC-27'nin üç koşulu yeniden sınanır, cevabın kanonik kodu
  saklanır, `Revision + 1`, gerekçe zorunlu; `expectedRevision` eski → 409; kanonik kod aynı → 422 `SUPPLIER_BINDING_CODE_UNCHANGED`.
  Sabotaj: düzeltmede `Revision` artışını kaldırmak → test kırmızı.
- [ ] AC-27 (zorunlu) — Onay ve satın alma denetimi (sahip, r2 F-5): `eligibleForTransactions = true` olsa da `onboardingStatus`
  `Approved` değilse (`Draft`, `InReview`, `Rejected`, `null`) → 422 `SUPPLIER_NOT_ELIGIBLE` `details.reason = ONBOARDING_NOT_APPROVED`;
  `purchasingEnabled` `true` değilse (`false`, `null`) → 422 `details.reason = PURCHASING_DISABLED`; her iki dalda sıfır yazma; üçü
  birden doğruysa bağ kurulur. Kur ve kod düzelt komutlarının ikisi de sınanır. Sabotaj: uygunluk denetimini yalnız
  `eligibleForTransactions`'a indirmek (onboarding ya da satın alma koşulunu silmek) → `Rejected` / `purchasingEnabled = false` testi kırmızı.
- [ ] AC-9 (zorunlu) — Denetim K2: üç komutun her biri iş yazımından önce deneme satırı yazar; deneme yazılamazsa 503, iş verisi
  yok; iş yazımı ve sonuç satırı aynı Mongo işlemindedir; geçmiş ucu bağın satırlarını sırayla döner ve sonuçsuz denemeyi
  "sonuçlanmadı" gösterir; defterde yeni iz satırı, `PinnedCounts` değişmez. Sabotaj: yazıcının hatasını işleyicide yutmak ya da
  sonuç satırını işlem dışına almak → test kırmızı.
- [ ] AC-10 (isteğe bağlı) — Sapma sorgusu: aktif bağlardan, saklanan kodu MOD-0140'ın bugünkü kanonik cevabından farklı olanları
  listeler; Procurement erişilemezse 503 (boş liste değil). Sabotaj: hata durumunda boş liste dönmek → test kırmızı.
- [ ] AC-26 (zorunlu) — Tedarikçi hesabına kiracı rolü atanamaz: `AssignRole` → 422 `SUPPLIER_ACCOUNT_ROLE_FORBIDDEN`, yazma ve
  sürüm artışı yok. Sabotaj: işleyicideki tedarikçi hesabı denetimini kaldırmak → test kırmızı.

**DL-2 — tedarikçi kapısı**

- [ ] AC-11 (zorunlu) — Tedarikçi girişi kiracının `MfaRequired` ayarından bağımsız olarak her zaman e-posta kodu ister; doğru
  parola tek başına token vermez. Sabotaj: tedarikçi giriş işleyicisinde kiracı ayarına (`LoginCommandHandler.cs:151` deseni)
  bağlanmak → `MfaRequired = false` testi kırmızı.
- [ ] AC-12 (zorunlu) — Token: `actor_type = supplier_user`; `supplier_id`, `legal_entity_id`, `binding_id`, `binding_rev`,
  `tenant_id`, `amr` var; `exp = min(nbf + 900 sn, oturum sonu)` — oturumun ilk 7 sa 45 dk'sında `exp − nbf = 900 sn`, sonrasında
  `exp = oturum sonu` (r2, F-2); `permission` claim kümesi tam olarak iki portal anahtarı; rol claim'i yok. Sabotaj: ömrü
  `AccessTokenExpirationMinutes`'ten (120) okumak ya da kiracı izinlerini eklemek → test kırmızı.
- [ ] AC-13 (zorunlu) — Oturum mutlak 8 saat (r2, F-2, F-11): art arda yenilemeler ilk girişin bitişini aşmaz; **7 sa 50 dk'da
  yenilemede** yeni erişim token'ının `exp`'i tam oturum sonudur (ömür 10 dk, 15 değil) ve yeni yenileme anahtarının bitişi hâlâ ilk
  girişe göre 8 sa'dir; 8 saatten sonra yenileme 401 `SUPPLIER_SESSION_EXPIRED`. Sabotaj 1: yeni yenileme anahtarının bitişini
  `UtcNow + 8 sa` yapmak (kayan pencere) ya da platform emsalindeki `Math.Max(1, …)` gün tabanını kopyalamak → 7 sa 50 dk testi
  kırmızı. Sabotaj 2: son yenilemede oturum sonu tavanını kaldırmak (`exp = nbf + 15 dk`) → 7 sa 50 dk testinin `exp` beyanı kırmızı.
- [ ] AC-14 (zorunlu) — Yenileme bağı yeniden okur: kaldırılmış bağ → 401 + oturum ailesi iptal; kod düzeltilmiş bağ → yeni token
  güncel `binding_rev` ve `supplier_id` taşır. Sabotaj: yenilemede bağ okumasını atlayıp eski claim'leri kopyalamak → test kırmızı.
- [ ] AC-15 (zorunlu) — Kapı ayrımı: tedarikçi hesabı kiracı kapısında (giriş, MFA doğrulama, yenileme, zorunlu parola değişimi)
  kiracı kapısının genel 401'ini alır; kiracı hesabı ve platform hesabı tedarikçi kapısında genel 401; `supplier_user` token'ı
  `/api/auth/me`, `/api/auth/refresh-token`, `/api/auth/logout` ve `/api/tenant-auth/*` uçlarında token almaz. Sabotaj: kiracı
  kapısındaki tedarikçi hesabı reddini kaldırmak → test kırmızı.
- [ ] AC-16 (zorunlu) — Aktif bağı olmayan tedarikçi hesabı, bilinmeyen e-posta ve yanlış parola aynı 401'i (kod, gövde, ölçülen
  süre bandı) alır. Sabotaj: bağ yokken ayrı bir hata kodu döndürmek → test kırmızı.
- [ ] AC-17 (zorunlu) — Çoklu bağ: kişinin kiracıda birden çok aktif bağı varsa kod doğrulaması token yerine seçim bileti ve
  yalnız kendi bağlarının listesini (LE, `SUP-` kodu) döner; `select` seçilen tek bağla token verir; bağlı olmayan LE ile seçim
  401; bilet ikinci kez 401. Sabotaj: bileti tek kullanımlık işaretlememek → ikinci kullanım testi kırmızı.
- [ ] AC-18 (zorunlu) — `logout` oturum ailesini iptal eder (sonraki yenileme 401); `me` bağ bilgisini ve oturum bitişini döner;
  `tenant_user` token'ıyla `me` 403. Sabotaj: `logout`'ta yalnız sunulan anahtarı iptal etmek → aile testi kırmızı.
- [ ] AC-19 (zorunlu) — S2S `binding_rev` ucu: geçerli servis token'ıyla `{active, revision, supplierCode, legalEntityId}`
  döner; kaldırılmış bağ `active = false`; kullanıcı token'ı / yanlış audience / token yok 401 (başlık ne olursa olsun; Auth kiracı
  ara katmanı bu uçta 400 vermez — r4, AC-30); listede olmayan `service_name` **403** (r3,
  AC-30); bu kiracıda bulunmayan bağ (başka kiracının bağı dahil) 404 `SUPPLIER_BINDING_NOT_FOUND` (r3, AC-29). Sabotaj: ucu
  `[Authorize]` kullanıcı şemasına açmak → kullanıcı token'ı testi kırmızı.
- [ ] AC-28 (zorunlu, r3) — Otomatik verme yok (sahip kararı Ali 2026-11-24, Beste önerisi 2026-11-19; MOD-0148 D2 / K-5, inceleme F-2):
  `ExplicitGrantOnlyPermissions.Keys` tam adlarıyla `supplychain.supplier-portal.read` ve `.submit`'i içerir (büyük / küçük
  harften bağımsız) ve küme 15 anahtardır. Üretim kodunda, iki anahtar Admin dalının ulaştığı bir modülün altına ve yanına sıradan
  bir okuma anahtarı konarak (boş geçmesin diye): (a) `DefaultRolePermissionTemplate.SelectFor` SuperAdmin, Admin ve Viewer için iki
  anahtarı döndürmez, sıradan kardeş anahtar Admin ve Viewer'a ulaşır; (b) `FullCatalogPermissionGrantService.GrantToFullCatalogRolesAsync`
  iki anahtar için rol-izin satırı yazmaz; (c) modül yetkilendirme eşitlemesi (`EntitlementPermissionSyncService`) iki anahtarı
  Admin'e ve Viewer'a vermez, kardeşi verir; (d) Roller → İzinler açık ataması (`AssignPermissionCommandHandler`) iki anahtarı bir
  role verebilir; (e) `supplier_user` token'ı iki anahtarı hâlâ taşır (AC-12 değişmez). Sabotaj 1: iki anahtarı
  `ExplicitGrantOnlyPermissions.Keys`'ten çıkarmak → (a) SuperAdmin ve Viewer `.read`, (b) ve (c) testleri kırmızı. Sabotaj 2:
  `FullCatalogPermissionGrantService.cs:39` denetimini kaldırmak → (b) kırmızı (yolun ayrı ölçüldüğünün kanıtı). Sabotaj 3:
  token üreticisinde izin kümesini rol / şablon yolundan ya da `ExplicitGrantOnlyPermissions` süzgecinden geçirmek → (e) kırmızı.
  (f) **Sıra kanıtı — P4'ten önce main'de (SQ-17, bağlayıcı):** DL-2.0 commit'i MOD-0148 P4 commit'inden önce main'dedir —
  `git merge-base --is-ancestor <DL-2.0> <P4>` çıkışı 0; ve P4'ün ölçülen ortamda ilk kaydı anında çalışan Auth ikilisi DL-2.0'ı
  içerir, kayıttan sonra iki anahtarda `GrantSource.System` / `GrantSource.Module` satırı sıfırdır (Bölüm 17 DL-2.0 kanıtı); ölçüm
  REP-001 koşu kaydına yazılır. Sabotaj 4 (sıra): P4'ü DL-2.0'dan önce birleştirmek / kaydetmek → ancestor denetimi çıkış 1 ve
  ortam ölçümünde SuperAdmin'in `GrantSource.System` satırı görünür (`InternalPermissionsController.cs:96`), kanıt kırmızı.
  **Yol başına tek sabotaj (r4, r3 incelemesi F-2):** her otomatik yolun kendi süzgeci ayrı ölçülür; sabotaj 2 tam katalog yolu
  içindir. Sabotaj 5: yalnız `DefaultRolePermissionTemplate.cs:182`'deki süzgeci (`&& !ExplicitGrantOnlyPermissions.Keys.Contains(p.Key)`)
  kaldırmak, liste ve öteki iki yol olduğu gibi → (a) kırmızı (SuperAdmin tam katalog dalı iki anahtarı, Viewer okuma dalı `.read`'i
  alır); (b) ve (c) yeşil kalır. Sabotaj 6: yalnız `EntitlementPermissionSyncService.cs:294`'teki süzgeci
  (`.Where(permission => !ExplicitGrantOnlyPermissions.Keys.Contains(permission.Key))`) kaldırmak → (c) kırmızı (Admin iki anahtarı,
  Viewer `.read`'i `GrantSource.Module` ile alır); (a) ve (b) yeşil kalır.
- [ ] AC-29 (zorunlu, r3) — Adlandırılmış 404 (inceleme F-8; MOD-0148 BREV-01 "once its code is named"): geçerli
  `Diten.SupplyChain` servis token'ıyla kiracı A bağlamında (`X-Tenant-Id` = A, token kiracısı A) kiracı B'nin bağ kimliği **ve** hiç var olmayan bir kimlik → ikisi de 404,
  gövde `errorCodes = [SUPPLIER_BINDING_NOT_FOUND]` (SQ-16 kararı, Beste 2026-11-19, Ali onayı 2026-11-24); iki cevabın durum kodu, kodu ve gövdesi aynıdır
  (varlık sızmaz); kaldırılmış bağ 404 değil 200 `active = false`; cevapta bağın alanları yoktur. Sabotaj 1: kiracı filtresini
  sorgudan çıkarmak → başka kiracı vakası 200 döner, kırmızı. Sabotaj 2: 404'ü gövdesiz / kodsuz döndürmek (ya da başka kiracı
  için ayrı bir kod) → kod beyanı ve iki cevabın eşitliği kırmızı. **Ali'nin koşulu (2026-11-24):** test iki cevabı yan yana
  karşılaştırır — durum kodu, `errorCodes` ve gövdenin geri kalanı, istek başına değişen izleme alanları (`traceId`, korelasyon
  kimliği) dışında birebir aynıdır. Sabotaj 3: hiç var olmayan kimlik için gövdeye farklı bir `title` / `detail` metni yazmak (ya da
  başka kiracı vakasında farklı) → eşitlik testi kırmızı.
- [ ] AC-30 (zorunlu, r3) — Listede olmayan `service_name` ve geçersiz kiracı başlığı 403 (SQ-3 kaydı :34; inceleme F-8): imzası ve audience'ı
  (`TRUSTED_SUPPLIER_BINDING_CONSUMER`) geçerli, `service_name` `Diten.SupplyChain` olmayan servis token'ı → **403** (401 değil),
  bağ okunmaz (repository çağrısı sıfır); aynı token'la başka kiracının bağı istenince de 403 (404 değil — sıra 401 → 403 → 404);
  yanlış audience ve kullanıcı token'ı 401 kalır. Sabotaj 1: `service_name` denetimini kaldırmak → yabancı servis 200 alır,
  kırmızı. Sabotaj 2: listede olmayan adı kimlik doğrulama şemasında reddetmek (401) → 403 beyanı kırmızı. Sabotaj 3: bağı
  `service_name` denetiminden önce okumak → "repository çağrısı sıfır" ve 403-önce-404 vakası kırmızı.
  **Kiracı başlığı (Beste 2026-11-19, Ali onayı 2026-11-24):** geçerli `Diten.SupplyChain` token'ıyla `X-Tenant-Id` yok, GUID değil ya da token'ın izinli
  kiracı kapsamında değil (bugün tek `tenant_id` claim'i, `ServiceIdentityTokenIssuer.cs:50`) → **403**, liste dışı adla aynı 403
  gövdesi (ayrı kod adı yok), repository çağrısı sıfır; başlık geçersiz + başka kiracının bağı → 403 (404 değil). Sabotaj 4: başlık
  yokken token'ın `tenant_id`'sine ya da varsayılan kiracıya düşüp bağı okumak → başlıksız vaka 200 / 404 döner, 403 ve
  "repository çağrısı sıfır" beyanı kırmızı.
  **Gerçek hat, Auth kiracı ara katmanı dahil (r4, r3 incelemesi F-1):** yukarıdaki vakalar yalnız politika ya da denetleyici
  düzeyinde değil, `Program.cs:221-223` sırasıyla `UseAuthentication` → `UseTenantResolution` → `UseAuthorization`'dan geçen
  gerçek Api hattında da sınanır (Development dışı ortam, dev bypass kapalı; sayaçlı repository): geçerli `Diten.SupplyChain`
  token'ıyla (1) `X-Tenant-Id` yok, (2) GUID olmayan başlık, (3) başlık ≠ token kiracısı → üçü de **403**, gövdede `Missing Tenant`
  / `Tenant mismatch` yok, repository çağrısı sıfır; (4) token yok ve başlık yok → **401** (400 `Missing Tenant` değil);
  (5) kullanıcı token'ı + başka kiracının başlığı → **401** (400 `Tenant mismatch` değil). Ara katman tek başına (iki ortam): bu
  önekte başlıksız istek uca ulaşır ve `TenantContext.IsResolved = false`; Development + dev bypass açıkken de `IsResolved = false`
  (denetim: aynı yapılandırma `/api/users`'ta bypass'ı uygular); `/api/internal`, `/api/internal/supplier`,
  `/api/internal/supplier-bindings-admin` gibi kardeş yollar başlıksız 400 `Missing Tenant` almaya devam eder. Sabotaj 5: ara
  katmandaki muafiyeti kaldırmak → başlıksız vaka 403 yerine 400 `Missing Tenant` döner (Development'ta dev bypass `TenantContext`'e
  varsayılan kiracıyı yazar — `IsResolved = false` beyanı kırmızı; uç bu kiracıyı okursa 200 döner), vaka (4) 401 yerine 400 —
  kırmızı. Sabotaj 6: muafiyeti `/api/internal` önekine genişletmek → kardeş yol vakası kırmızı. Sabotaj 7: bu önekte BL-324
  çelişki denetimini yerinde bırakmak → vaka (5) 401 yerine 400 `Tenant mismatch` döner, kırmızı.
- [ ] AC-20 (zorunlu) — Oturum olayları (giriş challenge'ı, oturum başlangıcı, yenileme, çıkış) token verilmeden önce denetim
  satırı yazar; yazılamazsa 503 ve token yok; satırlarda parola, kod, token ve bilet yoktur. Sabotaj: yazımı token üretiminden
  sonraya almak → arıza enjeksiyonu testi kırmızı.

**DL-3 — Gateway**

- [ ] AC-21 (zorunlu) — `supplier_user` token'ı `/api/supplier-performance/portal/*` ve `/api/supplier-auth/*` dışında her yolda
  (ör. `/api/suppliers`, `/api/users`, `/api/inventory/...`) gateway'de 403 "Forbidden Actor"; izinli iki önekte servise iletilir.
  Sabotaj: izin listesi denetimini kaldırmak → portal dışı yol testi kırmızı.
- [ ] AC-22 (zorunlu) — F4: `actor_type` claim'i olmayan, **kimliği doğrulanmış** imzalı token kişiselleştirme, servis kararlı ve
  genel kiracı uçlarında 403. Kimliği doğrulanmamış istekler bugünkü davranışı korur (r2, F-1): **tokensız** istek kamu ucunda
  (ör. `/api/tenant-auth/login`) geçer, korumalı kiracı ucunda 401 (403 değil); **süresi dolmuş** token'la korumalı uç 401 (403 değil)
  ve `/api/auth/refresh-token` geçer. Regresyon: `tenant_user` token'ıyla örnek kiracı rotaları (en az Auth, Procurement,
  SupplyChain, Inventory, MDM birer uç) ve platform token'ıyla platform uçları önceki davranışla aynı. Sabotaj 1:
  `TenantResolutionMiddleware.cs`'te `!string.IsNullOrWhiteSpace(actorType) &&` kısa devresini geri koymak → claim'siz token testi
  kırmızı. Sabotaj 2: yeni kuraldan `IsAuthenticated` koşulunu kaldırmak → tokensız ve süresi dolmuş istek testleri kırmızı (403 döner).
- [ ] AC-23 (zorunlu) — `ocelot.json`'da `/api/supplier-auth/{everything}` → 5056 ve `/api/supplier-performance/portal/{everything}`
  → 5066 rotaları OPTIONS dahil var; gateway rota testi her iki öneki doğru servise iletir. Değişikliği `integration-agent` yapar.
  Sabotaj: portal rotasını kaldırmak → rota testi kırmızı.

**DL-4 — bağlama ekranı (isteğe bağlı)**

- [ ] AC-24 (isteğe bağlı) — `Views/Governance/SupplierBindings/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` açıkça
  yazılı; `verify_datatable_page.py --area Governance --module SupplierBindings` PASS; `data-dt-data-mode="server"`.
  Sabotaj: `Layout` satırını silmek → doğrulayıcı kırmızı.
- [ ] AC-25 (isteğe bağlı) — 7 dil `.resx` paritesi (en, tr, fr, es, zh, ar, ru); `auth.supplier-bindings.read` olmayan kullanıcıya
  iskelet çizilmez, tek açıklama gösterilir (UAS-001); sabit yedek lookup listesi yok; sayfa yardımı anahtarları var (UXH-001).
  Sabotaj: bir dilden bir anahtarı silmek → parite testi kırmızı.

## 17. Test Expectations

| AC | Test (öneri adı) | Tür | Sabotaj (üretim kodu → test kırmızı) |
|---|---|---|---|
| AC-1 | `SupplierBindings/CreateStoresCanonicalCodeTests` | işleyici + sahte uygunluk istemcisi | sabotaj: saklanan değer istekteki kod olur |
| AC-2 | `SupplierBindings/CreateEligibilityFailClosedTests` (her dal ayrı) | işleyici + HTTP istemci (sahte sunucu) | sabotaj: zaman aşımı "uygun"a eşlenir / çağrı atlanır |
| AC-3 | `SupplierBindings/SingleActiveBindingConcurrencyTests` | gerçek Mongo (replika seti) | sabotaj: tekil indeks + ön kontrol kaldırılır |
| AC-4 | `SupplierBindings/SupplierBindingTenantIsolationHttpTests` | HTTP (WebApplicationFactory) | sabotaj: repository'den `TenantId` filtresi çıkar |
| AC-5 | `SupplierBindings/SupplierBindingPermissionHttpTests` | HTTP | sabotaj: `[HasPermission]` kaldırılır |
| AC-6 | `SupplierBindings/SupplierAccountEmailRulesTests` | işleyici + gerçek indeks | sabotaj: tedarikçi olmayan hesap bağlanabilir |
| AC-7 | `SupplierBindings/RevokeIsSoftDeleteTests` | repository + gerçek Mongo | sabotaj: `DeleteOneAsync` ile fiziksel silme |
| AC-8 | `SupplierBindings/CorrectCodeTests` | işleyici | sabotaj: `Revision` artışı kaldırılır |
| AC-27 | `SupplierBindings/BindRequiresApprovedAndPurchasingTests` (kur + düzelt; `Draft`, `InReview`, `Rejected`, `null`; `purchasingEnabled` `false` / `null`) | işleyici + sahte uygunluk istemcisi (sözleşme 1.3.0 örnek gövdeleri) | sabotaj: denetim yalnız `eligibleForTransactions`'a indirilir |
| AC-9 | `SupplierBindings/SupplierAccessAuditWriterTests`, `SupplierBindingHistoryHttpTests`, `AuditTrailStandardTests` | yazıcı + arıza enjeksiyonu + mimari test | sabotaj: yazıcı hatası yutulur / sonuç satırı işlem dışı |
| AC-10 | `SupplierBindings/DriftQueryTests` | işleyici | sabotaj: hata durumunda boş liste |
| AC-26 | `SupplierBindings/AssignRoleRefusesSupplierAccountTests` | işleyici (XMC-001) | sabotaj: tedarikçi hesabı denetimi kaldırılır |
| AC-11 | `SupplierAuth/SupplierLoginAlwaysRequiresOtpTests` | işleyici, `MfaRequired` her iki değer | sabotaj: kiracı `MfaRequired` ayarına bağlanır |
| AC-12 | `SupplierAuth/SupplierTokenClaimsTests` (oturumun başında ve son 15 dk'sında) | TokenService + JWT çözümü | sabotaj: ömür 120 dk anahtarından / kiracı izinleri eklenir |
| AC-13 | `SupplierAuth/SupplierSessionAbsoluteLifetimeTests` (7 sa 50 dk ve 8 sa + 1 sn vakaları) | işleyici + sahte saat + gerçek Mongo (döndürmede oturum alanları) | sabotaj: kayan pencere (`UtcNow + 8 sa`) / `Math.Max(1, …)` gün tabanı / son yenilemede oturum sonu tavanı kaldırılır |
| AC-14 | `SupplierAuth/SupplierRefreshRereadsBindingTests` | işleyici | sabotaj: yenilemede bağ okuması atlanır |
| AC-15 | `SupplierAuth/DoorSeparationHttpTests` (kiracı kapısının dört işleyicisi + `/api/auth/*`) | HTTP | sabotaj: kiracı kapısındaki tedarikçi reddi kaldırılır |
| AC-16 | `SupplierAuth/SupplierLoginUniformRefusalTests` | HTTP + süre bandı | sabotaj: bağ yokken ayrı hata kodu |
| AC-17 | `SupplierAuth/SupplierBindingSelectionTests` (eşzamanlı iki seçim, TTL indeksi var) | HTTP + gerçek Mongo | sabotaj: bilet tek kullanımlık işaretlenmez |
| AC-18 | `SupplierAuth/SupplierLogoutAndMeTests` | HTTP | sabotaj: yalnız sunulan anahtar iptal edilir |
| AC-19 | `SupplierAuth/BindingRevisionServiceEndpointTests` | HTTP + Platform biçimli servis token'ı | sabotaj: uç kullanıcı şemasına açılır |
| AC-28 (r3) | `Roles/SupplierPortalExplicitGrantPermissionTests` (şablon üç rol + kardeş anahtar; tam katalog yolu; modül eşitlemesi; açık atama) + (f) sıra kanıtı (`git merge-base --is-ancestor`, ortam ölçümü) + `Users/AccountKindTests` (küme 15, iki ad) + `SupplierAuth/SupplierTokenClaimsTests` (AC-12, iki anahtar token'da) | üretim `DefaultRolePermissionTemplate` / `FullCatalogPermissionGrantService` / `EntitlementPermissionSyncService` / `AssignPermissionCommandHandler` + sahte repository'ler (emsal `LeaveExplicitGrantPermissionTests`, `FullCatalogPermissionGrantServiceTests.cs:61-64`, `EntitlementPermissionSyncServiceTests.cs:312-316`) | sabotaj 1: iki anahtar `Keys`'ten çıkar; sabotaj 2: `FullCatalogPermissionGrantService.cs:39` denetimi kalkar → (b); sabotaj 3: token izin kümesi şablon / liste süzgecinden geçer; sabotaj 4: P4 DL-2.0'dan önce birleşir; sabotaj 5 (r4, F-2): yalnız `DefaultRolePermissionTemplate.cs:182` süzgeci kalkar → (a), (b) ve (c) yeşil; sabotaj 6 (r4, F-2): yalnız `EntitlementPermissionSyncService.cs:294` süzgeci kalkar → (c), (a) ve (b) yeşil |
| AC-29 (r3) | `SupplierAuth/BindingRevisionNotFoundCodeTests` (başka kiracı + olmayan kimlik + kaldırılmış bağ) | HTTP + gerçek Mongo + servis token'ı | sabotaj 1: kiracı filtresi kalkar; sabotaj 2: 404 gövdesiz / ayrı kodlu; sabotaj 3: olmayan kimlikte gövde metni farklı (Ali koşulu) |
| AC-30 (r3) | `SupplierAuth/BindingRevisionServiceNameForbiddenTests` (yabancı ad; yabancı ad + başka kiracı; yanlış audience; kullanıcı token'ı) + `SupplierAuth/BindingRevisionTenantHeaderTests` (başlık yok; GUID değil; token kiracısından farklı; geçersiz başlık + başka kiracının bağı) | HTTP + sayaçlı repository | sabotaj 1: `service_name` denetimi kalkar; sabotaj 2: liste dışı ad 401'e çevrilir; sabotaj 3: bağ denetimden önce okunur; sabotaj 4: başlık yokken token kiracısına / varsayılan kiracıya düşülüp bağ okunur |
| AC-30 (r4, F-1) | `SupplierAuth/BindingRevisionPipelineHttpTests` (gerçek Api hattı, Auth kiracı ara katmanı dahil, Development dışı: servis token'ı + başlık yok / GUID değil / token kiracısından farklı → 403 ve repository çağrısı sıfır; token ve başlık yok → 401; kullanıcı token'ı + başka kiracı başlığı → 401) + `SupplierAuth/BindingRevisionTenantMiddlewareExemptionTests` (gerçek ara katman, Production ve Development + dev bypass; kardeş yollar 400 `Missing Tenant`) | HTTP (gerçek hat) + sayaçlı repository; ara katman birim testi (emsal `Tenancy/ServiceTokenEndpointTenantExemptionTests.cs:36-110`, `:176-297`) | sabotaj 5: ara katman muafiyeti kalkar → başlıksız vaka 400 `Missing Tenant` (Development'ta dev bypass kiracı yazar, `IsResolved` beyanı kırmızı), token+başlık yok vakası 400; sabotaj 6: muafiyet `/api/internal`'a genişler → kardeş yol vakası kırmızı; sabotaj 7: bu önekte BL-324 çelişki denetimi kalır → kullanıcı token'ı + başka başlık 400 |
| AC-20 | `SupplierAuth/SupplierSessionAuditFailClosedTests` | arıza enjeksiyonu | sabotaj: denetim yazımı token üretiminden sonraya alınır |
| AC-21 | Gateway `SupplierUserAllowListTests` | gateway middleware testi | sabotaj: izin listesi denetimi kaldırılır |
| AC-22 | Gateway `MissingActorTypeRefusedTests` + `UnauthenticatedRequestUnchangedTests` (tokensız, süresi dolmuş) + `TenantRouteRegressionTests` | gateway middleware testi | sabotaj: `IsNullOrWhiteSpace` kısa devresi geri konur / `IsAuthenticated` koşulu kaldırılır |
| AC-23 | Gateway `SupplierRoutesOcelotTests` | rota testi | sabotaj: portal rotası kaldırılır |
| AC-24 | `verify_datatable_page.py`, `quality-gate-datatable` | doğrulayıcı | sabotaj: `Layout` satırı silinir |
| AC-25 | RESX parite testi, vitest (UAS-001), sayfa yardımı testi | frontend | sabotaj: bir dilden bir anahtar silinir |

- Her dilimde: ilgili servisin **tam** test takımı (filtre yok), `dotnet build` (Auth, Gateway; DL-4'te Web),
  `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`.
- Sabotaj kanıtı: her AC'nin sabotajı üretim kodunda uygulanır, kırmızı çıktı kaydedilir, geri alınır, yeşil çıktı kaydedilir
  (REP-001 koşu kaydı).
- DL-1 kabulü canlı kanıt ister: Gateway üzerinden gerçek MOD-0140 ucuna karşı bir başarılı kur ve bir 422 (E4).
- DL-3 kabulü canlı kanıt ister: `supplier_user` token'ıyla portal dışı bir yolda 403 ve portal yolunda servise ulaşma (E5).
- **r3 — DL-2.0 kanıtı (AC-28 (f)):** önce sıra — DL-2.0 commit'i MOD-0148 P4'ün atası (`git merge-base --is-ancestor`, çıkış 0;
  SQ-17 bağlayıcı); sonra MOD-0148 P4 kaydından sonra, DL-2'nin ölçtüğü ortamda iki anahtar katalogda `Tenant` kapsamıyla
  görünür ve iki anahtarı tutan rol-izin satırlarının hiçbiri otomatik kaynaklı değildir — `GrantSource.System` (şablon ve tam
  katalog `RolePermission.SystemGrant`, `FullCatalogPermissionGrantService.cs:78-80`) ya da `GrantSource.Module` satırı sıfır; yalnız
  `GrantSource.Manual` olabilir (`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/GrantSource.cs:14`, `:20`, `:23`);
  kayıt REP-001 koşu kaydına yazılır. Sıfır değilse DL-2 durur ve satırlar
  açık kararla temizlenir (liste var olan satırı silmez).

## 18. Ready-for-dev Checklist

- [ ] S0 kapandı (Bölüm 0.4): D1–D8 yeniden onayı karar dosyasında; SQ-1, SQ-3, SQ-4, SQ-7, SQ-11, SQ-12, SQ-13 cevaplı
- [ ] CT-APR-02 r2 teyidi: F-1…F-12'nin bu sürümde kapanışı (r2 notu, başlık altı)
- [ ] XMC-001: Auth (Ali) ve Gateway (MOD-0032 sahibi / `integration-agent`) izinleri yazılı
- [ ] Control Tower defter kararı: yeni Auth denetim izi (Bölüm 21) — DL-1 kodundan önce
- [ ] MOD-0140 DL-3 main'de + Gateway üzerinden canlı kanıt (SQ-9) — DL-1 kodundan önce
- [ ] Registry'de `MOD-0360-FU01` satırı (S0)
- [ ] Golden Reference Slim okundu; Bölüm 10 yapısı birebir
- [ ] Frontmatter zorunlu alanlar dolu (`service`, `shell`, `golden_reference`, `data_mode`, `entity_base`)
- [ ] Layout & Shell Contract: `Layout = "_LayoutTenantShell"` açıkça (DL-4)
- [ ] Validation Rules her alan için yazılı; Failure Path en az dört senaryo (yinelenen, eksik, yetkisiz, eşzamanlılık) — var
- [ ] Authorization Convention: izin listesi + policy + aktör türü — var
- [ ] Gateway routing kararı açık + `integration-agent` görevi — DL-3 başında açılır
- [ ] Lookup kararı (DL-4): LE ve tedarikçi seçimi, sabit yedek yok — var
- [ ] Audited Events her yazma komutunu tam bir kez içeriyor; K2 sınıfı kapalı-başarısız ve testli — var
- [ ] Sayfa yardımı (DL-4): `Help.SUPPLIERBINDINGS.*` kiracı 7 dil; tur yok (beş akıştan biri değil)
- [ ] CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı (yüksek risk: yeni aktör türü, kimlik doğrulama yolu, gateway, kiracı)
- [x] **r3:** CT-APR-02 bağımsız inceleme (r3 farkı: AC-19, AC-28, AC-29, AC-30; Bölüm 0.3, 3, 5, 7, 13, 14, 17, 22) — yapıldı,
  KOŞULLU (`docs/records/reviews/MOD-0360-FU01/2026-11-24-068646c1.md`, F-1…F-3); bulgular r4'te kapandı
- [ ] **r4:** yeni CT-APR-02 bağımsız inceleme (r4 farkı: F-1 → Bölüm 0.3, 5, 7, 8, 13, 14, 17, AC-19, AC-30; F-2 → AC-28, 17;
  F-3 → künye, r3 notu, 0.3, 5, AC-28, 19; SQ-18 → 0.4, 5, 18, 22, 23) + yeni CT-APR-01 satırı — Ali'nin 2026-11-24 onayı r3
  içeriği (`068646c1…`) içindir, r4 satırı yerine geçmez; r2 satırı (`8086bf9a…`) bu sürüme geçmez
- [x] **r3:** `SUPPLIER_BINDING_NOT_FOUND` adı kararlaştırıldı (SQ-16; Beste önerdi 2026-11-19, MOD-0018/0360 sahibi Ali onayladı 2026-11-24)
- [x] **r4:** SQ-18 kapandı (Ali, 2026-11-24): DL-2.0 Bölüm 0.4 S0'ı beklemez; yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001
- [ ] **r3:** DL-2.0 MOD-0148 P4'ten önce main'de (AC-28 (f); SQ-17 bağlayıcı)
- [x] **r4:** XMC-001 Auth izni — `TenantResolutionMiddleware.cs` (Auth) dahil (Bölüm 0.3; Ali, 2026-11-24, r3/r4 kararının parçası)

## 19. Implementation Notes

- **Plan hizalaması:** plan kaynağı `docs/roadmap/plans/mvp6-development-plan-v10.0.md` C4 (`:90`) ve C7 (`:93`). Planda bu iş için
  `1.x.y` yetenek kimliği yok → `plan kimliği yok`; plan MVP'si MVP-6 (C4/C7 satırları).
- **Neden hesap düzeyinde ayırt edici (SQ-1 önerisi):** kiracı kapısı platform hesabını şifreye bakmadan hesap alanıyla reddeder
  (`LoginCommandHandler.cs:108-114`, `User.cs:50`). Bağdan türetilen ayırt edici, bağ kaldırılınca hesabı kiracı kullanıcısına
  çevirir ve tedarikçi kişisi kiracı kapısından girebilir. Bu yüzden D2'deki "hesap `Human` kalır" metni öneriyle çelişir; S0'da
  D2 yeniden yazılır. `AccountKind` bugün `Unknown / Human / Service` (`services/Diten.AuthService/src/Diten.AuthService.Domain/Enums/AccountKind.cs:20-26`)
  ve otomatik oluşturma yolu `Unknown` bırakmak zorundadır (aynı dosya, özet yorumu; `AccountKindCreationPathsGuardTests`) — kur
  komutu yeni bir oluşturma yoludur, bekçi değişikliği XMC-001'dir.
- **MFA:** bugün yalnız e-posta kanalı var (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/MfaChallengeService.cs:43`)
  ve zorunluluk kiracı genelidir (`LoginCommandHandler.cs:151`). Tedarikçi kapısı ayarı okumaz, her girişte challenge açar.
  Challenge kaydının hangi kapıya ait olduğu tutulmalıdır (SQ-2).
- **Token ömrü:** `appsettings.json:49-50` bugün 120 dk / 7 gün; `JwtSettings.cs` varsayılanı 15 dk. Tedarikçi ömrü ayrı anahtardan
  okunur; kiracı değerine dokunulmaz.
- **Aktör türü yazımı:** Auth bugün iki yerde `actor_type` yazar (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs:39`,
  `:113`); `supplier_user` üçüncü üreticidir.
- **Yenileme ve aktör:** yenileme, yenileme anahtarındaki aktörü erişim token'ınınkiyle karşılaştırır (`RefreshTokenCommandHandler.cs:110`);
  tedarikçi yenilemesi ayrı uçtadır ama aynı karşılaştırmayı korur.
- **E-posta tekilliği ve 409:** yarış durumunda indeks `DuplicateUserEmailException` üretir
  (`services/Diten.AuthService/src/Diten.AuthService.Application/Common/Exceptions/DuplicateUserEmailException.cs:8`); kur komutu bunu
  `SUPPLIER_BINDING_EMAIL_IS_TENANT_ACCOUNT` ya da `SUPPLIER_BINDING_EXISTS`'e (hesap tedarikçiyse) eşler.
- **Denetim deseni:** MOD-0018-FU27 K-W5 deseni (ayrı yazıcı tipi, deneme + sonuç satırı, iş yazımı ile sonuç satırı aynı işlem,
  okuma arayüzü, geçmiş ucu, meta-denetim, defter satırı) emsal alınır:
  `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md:485-545`, `:744`.
  FU27 DL-5 bugün kodda yok (`ISupplierAccessAuditWriter`'ın emsal tipi `IPaymentSodWaiverAuditWriter` yok); bu paket kendi yazıcısını kurar.
- **Tüketici:** MOD-0148 §0.0 (`execution/domains/supply-chain-execution/module-packs/MOD-0148-supplier-portal.md:31-61`) bu
  paketten bağ, kanonik kod, `supplier_user` token'ı, izin listesi ve iki adımlı doğrulamayı bekler; §0.1 replay receipt'in
  `validatedIssuer` / `subject` alanları bu paketin token'ından gelir (`:210-221`); §14 ve §15 portal izinleri ve rota sahipliği.
- **MOD-0140:** bağlama okuması DL-3 ucudur; S2S yolu (DL-7, S-4) MOD-0148'indir
  (`execution/domains/procurement/module-packs/MOD-0140-supplier-onboarding.md:457`, S-5 `:714`).
- **Sahip kararı kaynağı:** `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md`; Natig'in isteği
  `docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/DC03-BINDING-CANONICAL-CODE-REQUEST.md` (M2 ile kabul).
- Ölçümler HEAD `e8128c224` (r1) ve `70cb6ca01` (r2) üzerinde kod okumasıdır; çalışma testi yapılmadı. İncelemeye göre Auth ve
  Gateway üretim kodu iki taban arasında değişmedi; r2'de Gateway satırları, `MfaChallenge.cs`, `RefreshToken.cs`,
  `IRefreshTokenRepository.cs`, Auth `DependencyInjection.cs:42-43`, `ServiceIdentityTokenIssuer.cs:49`, `AuthController.cs:72-84`,
  `CreateUserCommandHandler.cs` ve `supplier.openapi.yaml:319`, `:362-372` yeniden ölçüldü.
- **r2 bulgu → yer eşlemesi:** F-1 → Bölüm 15, AC-22; F-2 → Bölüm 0.1 (D4), 8, 14, AC-12, AC-13; F-3 → Bölüm 3, 4, 5; F-4 → SQ-11;
  F-5 → Bölüm 0.1 (D6), 12, 13, AC-27, AC-8; F-6 → SQ-12; F-7 → SQ-13; F-8 → SQ-14; F-9 → SQ-15; F-10 → Bölüm 15; F-11 → AC-13;
  F-12 → künye.
- **r3 kaynakları ve eşleme (taslak 2026-11-19):** sahip kararı Ali, 2026-11-24 (MOD-0018 / MOD-0360 sahibi; Beste önerisi 2026-11-19;
  XMC-001 izni Ali'den, 2026-11-24) — MOD-0148 inceleme
  `docs/records/reviews/MOD-0148/2026-11-15-75b33fcb.md` F-2 → Bölüm 0.3, 3, 5 (DL-2.0), 7, 14, 17, 22, AC-28; F-8 ve
  `2026-11-17-dab26a67.md` F-2 → Bölüm 13, 14, AC-19, AC-29, AC-30. Karar kaydındaki istekler:
  `docs/records/decisions/2026-11/mod0148-dc03-consumer-owner-decisions-2026-11-15.md:30-32` (K-5), `:55-57` (Açık kalanlar). MOD-0148
  tarafı: §0.0 P1 (`MOD-0148-supplier-portal.md:81-93`, "before P4 is pushed"; kontrol listesi `:598`), P3 (`:113-117`, "until it is, no 404 is treated as a
  mismatch"), T-0148-V1-BREV-01 (`:278-281`, "once its code is named"). Ölçüm origin/main `7236d9608`: `ExplicitGrantOnlyPermissions.cs:8`,
  `:38-40`, `:108-114`; `DefaultRolePermissionTemplate.cs:181-183`, Viewer dalı `:207-212` (iki anahtardan `.read` liste olmadan
  Viewer'a düşerdi: `Tenant` kapsamı + `read` eylemi); `DefaultRoleProvisioningRule.cs:27`; `FullCatalogPermissionGrantService.cs:39`,
  `:78-80`; `InternalPermissionsController.cs:96`, `:117`; `EntitlementPermissionSyncService.cs:293-295`; `AccountKindTests.cs:60-80`;
  `GrantSource.cs:14-23`; `AuthRefusalCodes.cs` (kod `errorCodes`'ta). Test koşulmadı, kod okumasıdır.
- **r3 — hata kodu adı:** `SUPPLIER_BINDING_NOT_FOUND` paketin `SUPPLIER_BINDING_*` adlandırmasını izler (Bölüm 13: `…_EXISTS`,
  `…_REVISION_CONFLICT`, `…_SUPPLIER_UNKNOWN`, …). Başka kiracının bağı ile var olmayan kimlik aynı kodu alır: kiracılar arası
  erişim 404'tür ve varlık sızdırmaz (AGENTS.md Çekirdek 2). **Karar** (SQ-16; Beste önerdi 2026-11-19, MOD-0018/0360 sahibi Ali onayladı 2026-11-24).
- **r3 — S2S kiracı başlığı:** servis token'ı bugün tek `tenant_id` claim'i taşır
  (`services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/ServiceIdentityTokenIssuer.cs:50`); "izinli kiracı
  kapsamı" bu değerdir, biçimi DL-2 başında yeniden ölçülür. Geçersiz başlık 403'tür (Beste 2026-11-19, Ali onayı 2026-11-24), 400 değil.
- **r3 — MOD-0148'e geri bildirim (bu paket MOD-0148'i değiştirmez):** ad kararlaştırıldı (SQ-16); MOD-0148 sahibi P3 ve BREV-01'deki
  "once its code is named" ifadesini bu kodla sabitler; listede olmayan `service_name` artık yalnız 403'tür (MOD-0148 401 / 403'ü
  ayırt etmeden 503'e eşlediği için MOD-0148 davranışı değişmez — inceleme F-8).
- **r4 bulgu → yer eşlemesi (2026-11-24; inceleme `docs/records/reviews/MOD-0360-FU01/2026-11-24-068646c1.md`, KOŞULLU):**
  F-1 → Bölüm 0.3 (Auth `TenantResolutionMiddleware.cs` satırı), 5 (DL-2 "r4 eklenenler": kural, BL-324 / DCP-004 §7.4 ilişkisi,
  testler), 7, 8 (S2S istisnası, bilgi notu 1), 13, 14, AC-19, AC-30 (gerçek hat vakaları, sabotaj 5-7), 17, Teslim dilimleri;
  F-2 → AC-28 (sabotaj 5, 6), 17; F-3 → künye (`revision`, `status_note`), r3 notu, Bölüm 0.3, 5, AC-28, 19; SQ-18 (Ali 2026-11-24,
  `dc03-supplier-user-binding-owner-decisions-01.md` son ek) → Bölüm 0.4, 5, 18, 22, 23, Teslim dilimleri. Bilgi notu 2 (GUID olmayan
  `bindingId` rota kısıtı) bu revizyonun kapsamı dışında bırakıldı.
- **r4 ölçümleri (HEAD `86c5dfe9b`, salt okunur kod okuması; test koşulmadı):** `services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs:221-223`
  (`UseAuthentication`, `UseTenantResolution`, `UseAuthorization`); `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs`
  — BL-324 yorumu `:59-64`, çelişki 400 `:68-81`, `isServiceTokenIssuance` `:84`, dev bypass `:87-94`, kiracı yokken yol `:101-111`
  (400 `Missing Tenant` `:110`), `IsBypassPath` `:134-143` (`/internal` `:141`), `IsServiceTokenIssuancePath` `:148-162`;
  `.../Infrastructure/DependencyInjection.cs:42-55` (varsayılan kullanıcı `JwtBearer`); `ServiceIdentityTokenIssuer.cs:44`, `:49`,
  `:50`, `:55`; `DefaultRolePermissionTemplate.cs:182`; `EntitlementPermissionSyncService.cs:294`; emsal testler
  `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Tenancy/ServiceTokenEndpointTenantExemptionTests.cs:36-110`,
  `:176-297`.

## 20. Follow-up Items

- **FU-A — MOD-0032 FU: internete açılma kapıları** (`TrustedProxies`, gateway rate limit, paylaşılan limiter deposu). Numara ve
  sahip TBD; portal internete açılmadan önce zorunlu (C4).
- **FU-B — portal alt alanı ve tedarikçi giriş ekranı** (host'a bağlı çerezler, 7 dil) — MOD-0148 portal UI ile.
- **FU-C — tedarikçinin self-servis parola sıfırlaması** — v1'de Admin yeniden davet eder.
- **FU-D — onboarding'den otomatik bağ** (D6 "sonraki sürüm").
- **FU-E — kiracı Kullanıcılar ekranında tedarikçi hesaplarının ayrımı** (MOD-0360 ekranı, XMC-001).
- **FU-F — MOD-0140 kanonik kod değişikliği bildirimi** (olay) — bugün tespit AC-10 sapma sorgusuyla elle yapılır.
- **FU-G — ikinci MFA kanalı** (TOTP vb.) — SQ-2 cevabına göre.

## 21. Audited Events

Servisin bugünkü durumu (`tests/architecture/audit-ledger/Diten.AuthService.md`, `## İzler`): kabul edilmiş tek iz
`auth-kullanici-iletimi` (yol b, en iyi çaba — K2 borcu); `auth-gunlugu` ve `auth-rbac-gunlugu` `aday`. `PinnedCounts` Auth için
`Debt 18 / Exceptions 1 / K2Debt 11` (`tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs:61`).
Bu paketin bütün yazma komutları **K2 sınıfıdır** (kimlik / yetki): kayıt yazılamazsa komut durur.

**Yol:** `c` — yeni iz `auth-tedarikci-erisim-gecmisi`, yazıcı `ISupplierAccessAuditWriter` (önce-yaz, `WriteOnceAsync` üzerine;
`authAuditLogs`'a `EntityType` + `EntityId` ile), okuma `ISupplierAccessAuditReader` + geçmiş ucu, meta-denetim
`IPlatformAuditForwarder` ile; kuralın §5.c beş koşulu DL-1'de tek tek kanıtlanır. **Engel:** iz için Control Tower defter kararı
(`CT-DECISIONS.md` satırı) DL-1 kodundan önce; karar gelmezse komutlar yazılmaz (AUD-001 §4.4). `PinnedCounts` değişmez — yeni
borç yok.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateSupplierActorBindingCommand | `auth.supplier-binding.created` (+ yeni hesapta `auth.supplier-account.created`) | SupplierActorBinding (+ User) | c | Sonra: LE, UserId, SupplierCode, SupplierMasterId, Status, Revision; istekteki kod; hesap açıldıysa e-posta | — |
| RevokeSupplierActorBindingCommand | `auth.supplier-binding.revoked` | SupplierActorBinding | c | Önce/sonra: Status, Revision, IsDeleted; gerekçe | — |
| CorrectSupplierActorBindingCodeCommand | `auth.supplier-binding.code-corrected` | SupplierActorBinding | c | Önce/sonra: SupplierCode, Revision; gerekçe; istekteki kod | — |
| SupplierLoginCommand | `auth.supplier-login.challenge-created` | MfaChallenge | c | kanal, maskeli hedef, LE seçimi (varsa) | — |
| VerifySupplierMfaCommand | `auth.supplier-session.started` ya da `auth.supplier-login.selection-issued` | RefreshToken (oturum) | c | SessionId, bindingId, binding_rev, oturum bitişi | — |
| ResendSupplierMfaCommand | `auth.supplier-login.challenge-resent` | MfaChallenge | c | kanal, sonuç | — |
| SelectSupplierBindingCommand | `auth.supplier-session.started` | RefreshToken (oturum) | c | SessionId, seçilen bindingId, binding_rev | — |
| RefreshSupplierTokenCommand | `auth.supplier-session.refreshed` / `auth.supplier-session.refused` | RefreshToken (oturum) | c | SessionId, binding_rev önce/sonra, ret nedeni | — |
| LogoutSupplierCommand | `auth.supplier-session.ended` | RefreshToken (oturum) | c | SessionId, iptal edilen anahtar sayısı | — |

- **Kayda yazılmayan alanlar:** parola, OTP kodu, erişim / yenileme token'ı ve karması, seçim bileti ve karması, davet bağlantısı.
- **Yazma hatası davranışı (K2):** deneme satırı iş yazımından önce; yazılamazsa 503 ve iş verisi / token yok; iş yazımı + sonuç
  satırı aynı Mongo işleminde (AC-9, AC-20). Belirsiz commit'te ikinci mutasyon yapılmaz, denetim kimliğiyle okunur (FU27 emsali).
- Sorgular (liste, tekil, geçmiş, sapma, `me`, `binding_rev`) yazma komutu değildir; geçmiş okuması meta-denetim satırı yazar.
- **r3:** yeni yazma komutu yok. `ExplicitGrantOnlyPermissions` statik bir listedir (çalışma anında yazma yapmaz); S2S ucunun
  401 / 403 / 404 cevapları sorgu cevabıdır. Tablo değişmez, `PinnedCounts` değişmez.

## 22. Bağımlılık kapıları (dilim başına)

| Dilim | Başlamadan önce |
|---|---|
| DL-1 | S0 kapalı (0.4; SQ-12 ve SQ-13 dahil); XMC-001 Auth izni; Control Tower defter kararı (Bölüm 21); **MOD-0140 DL-3 main'de + Gateway canlı kanıtı** (SQ-9); CT-APR-02 + CT-APR-01 |
| DL-2 | DL-1 kabul; SQ-2 (challenge kapısı), SQ-3 (audience), SQ-7 (izin verme yolu), SQ-14 (katalog tek yazarı), SQ-15 (son bağdan sonra hesap); MOD-0148 anahtarları Auth kataloğunda; SQ-11 cevabı MOD-0148 paketine kapı olarak yazılmış (D3 tüketicisi); **r3:** SQ-16 kararlı (`SUPPLIER_BINDING_NOT_FOUND`). **r3 — DL-2.0 (AC-28) sırası — bağlayıcı (SQ-17; Beste önerdi 2026-11-19, MOD-0018/0360 sahibi Ali onayladı 2026-11-24):** DL-2.0 DL-2'nin **ilk commit'idir**; bu satırdaki "DL-1 kabul" ve "MOD-0148 anahtarları Auth kataloğunda" kapılarından **muaftır** (liste anahtar adıdır, katalog kaydı gerektirmez; DL-1 kabulünü beklemez); MOD-0148 P4 itilmeden **önce** main'de olmalıdır (MOD-0148 P1; Bölüm 7; kanıt AC-28 (f)). **r4 — SQ-18 KAPANDI (Ali, 2026-11-24; `dc03-supplier-user-binding-owner-decisions-01.md` son ek):** DL-2.0 Bölüm 0.4 S0 kapanışını **beklemez**; yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001 iznini (Auth, Ali — Bölüm 0.3) bekler. DL-2'nin geri kalanı (Auth kiracı ara katmanı muafiyeti dahil, r4 F-1) bu satırın bütün kapılarına bağlıdır ve P4'ten sonra başlar |
| DL-3 | DL-2 kabul; Gateway izni (MOD-0032 sahibi); `integration-agent` görevi; regresyon rota listesi onaylı |
| DL-4 | DL-1 kabul; Web ekran sahibi izni; isteğe bağlı — sahip isterse |

## 23. Açık sorular

**Ali'ye (Auth sahibi, GMG-ERP)**
- **SQ-1 — hesap türü ayırt edicisi:** (a) `AccountKind.Supplier = 3`; (b) `IsPlatformAccount` gibi ayrı bir hesap alanı;
  (c) bağdan türetme. Öneri (a) ya da (b) — hesap düzeyi; (c) bağ kaldırılınca hesabı kiracı kullanıcısına çevirir. Taslağın
  "hesap `Human` kalır" metni bu öneriyle değişir.
- **SQ-2 — MFA kanalı:** v1 yalnız e-posta kodu mu (bugünkü tek kanal)? Challenge kaydına kapı alanı eklensin mi (kiracı kapısının
  `mfa/verify`'ı tedarikçi challenge'ını kabul etmesin)?
- **SQ-3 — S-5 audience / `service_name`:** S2S `binding_rev` ucu için audience adı ve kabul edilecek `service_name` listesi
  (MVP-6 SupplyChain); MOD-0140 DL-7 audience'ı ile aynı mı, ayrı mı?
- **SQ-4 — D7 ile MOD-0140 S-4 çelişkisi:** D7 S2S kapsamını "yalnız `validate`" yazıyor; S-4 servis token'ını `POST /validate` ve
  tekil `GET /{supplierId}/eligibility` için açıyor; MOD-0148 §0.0 tekil uygunluk okumasını kullanıyor. Öneri: D7 S-4'e hizalanır.
- **SQ-5 — XMC-001 kapsamı:** Bölüm 5'teki Auth dosya listesi (kiracı kapısının dört işleyicisi, `AuthController`, `AssignRole`,
  `AccountKind` bekçisi) için izin.
- **SQ-6 — `auth.supplier-bindings.read` ayrı anahtar mı** (öneri: evet, yalnız okuma rolleri için), RCS-001 "görünen kod yok" önerisi
  ve merkezin kod düzeltmesini kiracıda hangi hesapla yürüteceği.
- **SQ-12 — yalnız `[Authorize]` taşıyan uçlar (r2, F-6; S0'ı bekletir):** taslak koşulu "`actor_type` denetimi Gateway'de **ve her
  serviste**" idi; paket bunu Gateway izin listesi + token'ın tam izin kümesiyle karşılıyor. Yalnız `[Authorize]` taşıyan uçlar
  servise doğrudan erişimde `supplier_user` token'ını kabul eder — ör. `POST /api/auth/change-password`
  (`services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/AuthController.cs:72-84`); v1 iç ağda servis portları
  erişilebilir. Karar: (a) bu uçların envanteri çıkarılır ve her biri AC-15 kapsamına (tedarikçi token'ı reddi) eklenir; (b) daraltma
  yazılı karar olarak kabul edilir ve servis portları ağda kapatılır. Öneri (a) — en azından Auth uçları için. Sahip: Ali.
- **SQ-13 — kota, davet bağlantısı, parola belirleme sayfası (r2, F-7; S0'ı bekletir):** (1) tedarikçi hesabı kiracının kullanıcı
  kotasına sayılır mı (BL-459; `CreateUserCommandHandler.cs` `IUserQuotaClient`, kota aşımı reddi)? Öneri: sayılmaz, ayrı sayım —
  ama karar sahibindir. (2) Davet bağlantısı hangi yoldan gider (Platform bildirim akışı korunan yol)? (3) v1'de tedarikçi UI'ı
  yokken parolayı hangi sayfada belirler (mevcut anonim `/api/users/set-password` + kiracı Web sayfası mı, ayrı sayfa mı)? AC-6 bu
  cevaplara göre kesinleşir. Sahip: Ali.
- **SQ-15 — son bağ kaldırılınca hesap (r2, F-9):** tedarikçi hesabı kapatılır mı, pasifleştirilir mi, ne kadar saklanır (kişisel
  veri)? Öneri: son aktif bağ kaldırılınca hesap pasif (giriş zaten bağ yokken genel 401), kişisel veri saklama süresi kiracının
  kullanıcı saklama kuralını izler. DL-2'den önce. Sahip: Ali.
**r3 kapanışları (2026-11-19) — Beste önerdi 2026-11-19, MOD-0018/0360 sahibi Ali onayladı 2026-11-24**
- ~~**SQ-16 — S2S 404 kodunun adı**~~ — **KAPANDI:** `SUPPLIER_BINDING_NOT_FOUND`; başka kiracının bağı ve var olmayan kimlik aynı
  gövde (Bölüm 13, 14, AC-29). MOD-0148 BREV-01 / P3'ün bu adla sabitlenmesi MOD-0148 sahibinin işidir.
- ~~**SQ-17 — DL-2.0 sırası**~~ — **KAPANDI (bağlayıcı):** `ExplicitGrantOnlyPermissions` eklemesi DL-2'nin ilk commit'i (DL-2.0);
  DL-2'nin katalog kapısından muaf; MOD-0148 P4'ten önce main'de; DL-1 kabulünü beklemez (Bölüm 5, 22, Teslim dilimleri; AC-28 (f)).
- ~~**S2S kiracı bilgisi**~~ — **KAPANDI:** `X-Tenant-Id` başlığından okunur; başlık yok / GUID değil / token'ın izinli kiracı
  kapsamında değil → 403, bağ okunmaz, aynı gövde, ayrı kod adı yok (Bölüm 13, 14, AC-30).
- ~~**403 gövdesine kod adı**~~ — **KAPANDI: hayır.** Liste dışı `service_name` ve geçersiz kiracı başlığı 403'ü kod adı taşımaz
  (MOD-0148 401 / 403'ü ayırt etmeden 503'e eşler).
- ~~**DL-1 uçlarında (AC-4) 404 kodu**~~ — **kapsam dışı / sonraki revizyon.**
- ~~**§23 SQ-1…SQ-15'in güncellenmesi**~~ (2026-11-13 `dc03-…-owner-decisions-01.md` ve 2026-11-15 MOD-0148 K-1…K-4 cevapları) —
  **kapsam dışı / sonraki revizyon.** Bu bölümdeki SQ-1…SQ-15 metni r2'deki gibi bırakıldı.
- ~~**SQ-18 — DL-2.0 ve S0 (r3)**~~ — **KAPANDI (Ali, 2026-11-24; Beste iletti;
  `docs/records/decisions/2026-11/dc03-supplier-user-binding-owner-decisions-01.md` son ek, commit `76276e123`):** DL-2.0 Bölüm 0.4 S0
  kapanışını (D1–D8 yeniden onayı, Control Tower defter kararı, MOD-0140 kanıtı vb.) **beklemez**; yalnız r4 CT-APR-02 + CT-APR-01
  ve XMC-001 iznini bekler (öneri gibi). Gerekçe: liste değişikliği S0 maddelerinin hiçbirine bağlı değil; S0'ı beklerse P4 S0
  kapanışına kadar gecikirdi. Yerler: Bölüm 0.4, 5 (DL-2.0), 18, 22, Teslim dilimleri.

**Natig'e (MOD-0148 sahibi)**
- **SQ-7 — izin verme yolu:** `supplier_user` token'ına `.read` / `.submit` sabit küme olarak mı (öneri) yoksa kiracıda tohumlanan bir
  rolle mi girsin?
- **SQ-8 — iki adımlı doğrulama claim'i:** portal `amr` claim'ini yeniden denetler mi, yoksa Auth'a mı güvenir (MOD-0148 §0.0 bu
  pakete bırakıyor)?
- **SQ-11 — D3'ün tüketicisi (r2, F-4; S0'ı bekletir):** D3 "her yazmada Auth'tan güncel `binding_rev` sor" diyor; MOD-0148 paketinde
  `binding_rev` karşılaştırması ya da S2S revizyon ucu çağrısı yok. Yapılmazsa bağ kaldırma yazmalarda anında değil, erişim
  token'ının ömrü sonunda (en çok 15 dk) etkili olur. Soru: MOD-0148 `createOwnPortalSubmission` ve `submitOwnPortalSubmission`
  öncesinde AC-19 ucunu çağırıp `binding_rev` uyuşmazlığında 403, Auth erişilemezse 503 (D8) dönecek mi — evetse MOD-0148 paketine
  kapı olarak yazılır; hayırsa D3 "yazmada token ömrü içinde" diye yeniden yazılır. Sahip: Natig.
- **SQ-14 — portal izin anahtarlarının kataloğa tek yazarı (r2, F-8):** `supplychain.supplier-portal.read` / `.submit` Auth kataloğuna
  hangi tek yazarla (MOD-0148 manifesti / §14 "permission/composition single writer") ve ne zaman girer? DL-2 başında anahtarlar
  katalogda olmalıdır. Sahip: Natig.

**Cihan'a (MOD-0140 / Procurement)**
- **SQ-9 — DL-3 main + canlı kanıt:** uygunluk ucu ne zaman main'de; Gateway üzerinden gerçek 200, gerçek 404 `UNKNOWN_SUPPLIER` ve
  kabul edilmeyen token reddi kanıtı ne zaman? DL-1 bunu bekler. Admin'in kullanıcı token'ıyla (D6) okuma için gereken izinler
  (`procurement.suppliers.eligibility.read`, S-2 `mdm.legal-entities.read`) onaylı mı?
- **SQ-10 — DL-7 zamanı:** MOD-0148'in servis token'ıyla okuması (bu paketi değil MOD-0148'i bekletir).

**Merkez / Control Tower (Beste)**
- Bölüm 21 yeni iz için defter kararı (yol `c`, beş koşul) — DL-1'den önce.
- MOD-0032 FU (internete açılma) numarası ve sahibi — TBD.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | S1 çekirdek: SupplierActorBinding kaydı, kur / kaldır / kod düzelt, uygunluk okuması, tedarikçi hesabı, K2 denetim izi | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
| DL-2 | S2 tedarikçi kapısı: login / OTP / seçim / refresh / logout / me, supplier_user token, S2S binding_rev ucu (r3: adlandırılmış 404 `SUPPLIER_BINDING_NOT_FOUND`, liste dışı `service_name` ve geçersiz `X-Tenant-Id` 403 — AC-29, AC-30; sıra 401 → 403 → 404; r4: Auth kiracı ara katmanında `/api/internal/supplier-bindings` muafiyeti — bu uçta 400 yok, AC-30 gerçek hat vakaları), kiracı kapısı reddi; r3 DL-2.0 (bağlayıcı sıra, SQ-17: DL-2'nin ilk commit'i, katalog kapısından muaf, DL-1 kabulünü beklemez, MOD-0148 P4'ten önce main'de; r4: S0'ı beklemez, yalnız r4 CT-APR-02 + CT-APR-01 ve XMC-001 — SQ-18 kapandı): portal anahtarları `ExplicitGrantOnlyPermissions`'ta (AC-28) | Owned Objects, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
| DL-3 | S3 Gateway: supplier_user izin listesi, F4 sertleştirmesi, iki ocelot rotası (integration-agent) | Gateway / API Routing Decision, Repo Scope, Acceptance Criteria, Test Expectations | E5 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
| DL-4 | S4 bağlama ekranı (isteğe bağlı), 7 dil | Layout & Shell Contract, Frontend File Contract, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | MVP-6 (plan v10.0 C4/C7) |
