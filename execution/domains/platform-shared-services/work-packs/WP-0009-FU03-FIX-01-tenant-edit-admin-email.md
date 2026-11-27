# WP-0009-FU03-FIX-01 — Kiracı düzenleme formu "Yönetici E-postası" boş geliyor ve kaydı engelliyor

| Alan | Değer |
| :--- | :--- |
| Durum | **draft** |
| Onaylayıcı | Beste (merkezi ekip, Platform sahibi) |
| Açılış | 2026-11-25 — sahip onayı: "evet açalım" |
| Modül | **MOD-0009-FU03** Tenant Core UI (üst: MOD-0009 Tenant / Environment Management). Ön kontrol: `verify_module_id.py . --check-id MOD-0009-FU03 --name "Tenant Core UI" --parent MOD-0009` → `OK … proven against Blueprint/registry` (exit 0, 2026-11-25) |
| Pack | `execution/domains/platform-shared-services/module-packs/MOD-0009-FU03-tenant-core-ui.md` (status `in-progress`) |
| Katman | Yalnız Web (Diten.Web): view + JS + resx. **Backend değişikliği yok.** |
| Kaynak gözlem | 2026-11-25: `/Platform/Tenants/Edit/62d31535-08af-41ed-9dda-844d7b928a98` formu "Admin Email" alanını boş yüklüyor; kaydet → "Admin email is required." |
| Kod tabanı | HEAD `c815d95d0` = origin/main `82af5c24a` = MVP-1 hattı `8c32f4d90` (ilgili dosyaların blob'ları aynı) |
| Dil | Platform ekranı → **2 dil: en, tr** |

## 0. Pack kapsamı (neden MOD-0009-FU03)

- Pack Repo Scope: `frontend/Diten.Web/Controllers/TenantsController.cs`, `Views/Platform/Tenants/**`,
  `wwwroot/assets/js/Platform/Tenants/**`, `Resources/Views/Platform/Tenants/**` — bu işin dokunduğu dosyaların tamamı.
- Pack AC: "Create Tenant is route-based (`/Platform/Tenants/Create`) and supports the full Tenant Core request payload";
  Edit aynı formu kullanır (`TenantsController.cs:58-64` → `Create.cshtml`, `FormMode = Edit`). Pack AC listesi Edit rotasını
  ayrıca adlandırmıyor (bkz. §8 soru 3).
- Backend sözleşmesi MOD-0009-FU02'nin (`PUT /api/admin/tenants/{id}`); bu FIX onu **değiştirmez**.

## 1. Kök neden — ölçüldü

1. **Alan, var olmayan bir değerle dolduruluyor.** Edit yükleyicisi `create.js:557`:
   `setValue('initialAdmin.email', tenant.contactEmail || tenant.ContactEmail)` — "Yönetici E-postası" alanına kiracının
   **iletişim** e-postası yazılıyor. Detay DTO'sunda `ContactEmail` var (`TenantContracts.cs:101`) ama yönetici e-postası yok;
   yöneticiler ayrı koleksiyon/uç nokta (`TenantsController.cs:102-130`, `api/{id}/admin-users`).
2. **ContactEmail UI'dan hiç dolmuyor.** Create payload'ı (`create.js:144-171`) `contactEmail` göndermez;
   `RegisterTenantCommandHandler.cs:161` onu `request.ContactEmail` (null) ile yazar. Dev ölçümü (salt okuma,
   `diten_personalization_dev.tenants`, 2026-11-25): silinmemiş 3 kiracının **3'ünde** `ContactEmail` boş; test kiracısında boş,
   `AdminUsers` = 2 kayıt.
3. **Edit'te gereksiz zorunluluk.** `create.js:131-132` `validateClient` `initialAdmin.email`'i **her modda** zorunlu tutar;
   `create.js:134` plan için yapılan `!isEditMode` istisnası burada yok. Alan `Create.cshtml:49-53` ile her modda `required` çizilir.
4. **Edit'te alanın hiçbir etkisi yok.** `create.js:162-171` `initialAdmin`'i yalnız `!isEditMode` iken payload'a koyar;
   `TenantUpdateRequest` (`TenantContracts.cs:181-191`) yönetici/iletişim e-postası taşımaz; `UpdateTenantCommandHandler.cs:73-101`
   yalnız ad, görünen ad, slug, domain, tür, ülke, saat dilimi, dil, para birimini yazar.

Kusur 2026-05-07'den (`dcb2b7f77`) beri var (alan 2026-06-22 `1deb2947e` ile bugünkü biçimine geldi); ContactEmail'i boş
olan her kiracıda (UI'dan açılan **tüm** kiracılar) Edit kaydedilemez — operatör alana herhangi bir adres yazmadıkça.

## 2. "Edit'te bu alanı değiştirmek ne yapıyor?" — yan etki analizi

- **Yönetici değişmez, davet gitmez, Auth hesabı oluşmaz/değişmez.** Değer istemcide doğrulanır ve atılır (madde 4).
- **Risk yanlış inanç:** operatör yöneticiyi değiştirdiğini sanar; sonraki davetler/askı postaları eski yöneticilere gider.
- **Risk yanlış gösterim:** ContactEmail dolu bir kiracıda alan, iletişim adresini "Yönetici E-postası" etiketiyle gösterir;
  birden çok yönetici olan kiracıda (test kiracısı: 2) "tek yönetici e-postası" kavramı zaten yanlış.
- **Kayıtlı gözlem:** test kiracısında Edit ile dil `es`'e çevrilmiş (dispatch kaydı: dil değişiminden sonra `es / 1.2.0`
  davet) — operatör kaydı geçmek için alana bir adres yazmak zorunda kalmış olmalı; yazdığı adres hiçbir yere gitmedi.
- Yönetici ekleme/düzenleme/silme/davet zaten Details sayfasının varsayılan sekmesinde (`Details.cshtml:171-202`,
  `#tabAdminUsers`) ayrı, denetlenen komutlarla yapılıyor (`UpdateTenantAdminUserCommand` → `AuditCategory.IdentityAccess`).

## 3. Çözüm seçenekleri

| # | Seçenek | Karar |
| :--- | :--- | :--- |
| A | Yalnız doğrulamayı `!isEditMode`'a bağla | Yetersiz: boş/yanıltıcı, düzenlenebilir ama etkisiz alan kalır |
| B | Edit'te alanı gizle; yerine salt-okunur bilgi + Details › Yöneticiler bağlantısı | **Önerilen** |
| C | Edit'te alanı salt-okunur yap, `api/{id}/admin-users`'tan yöneticileri listele | Olası; ek çağrı + çoklu yönetici gösterimi; sahip isterse (§8 soru 1) |
| D | `TenantUpdateRequest`'e yönetici e-postası ekle | **Reddedildi:** yönetici değişimi Auth hesabı, davet, oturum sıfırlama doğurur; zaten Admin Users akışının işi; MOD-0009-FU02 sözleşmesini değiştirir |

## 4. Önerilen tek çözüm (B)

**F-1 · View (`Create.cshtml:49-53`).** `adminEmailField` bloğu yalnız `!isEditMode` iken çizilir. Edit'te aynı ızgara
hücresinde salt-okunur bilgi: etiket `AdminUsers` ("Yöneticiler"), metin `AdminUsersManagedInDetails`
("Yöneticiler kiracı ayrıntılarındaki Yöneticiler sekmesinden eklenir ve değiştirilir.") ve bağlantı `OpenAdminUsers`
("Yöneticileri aç") → `asp-action="Details" asp-route-id="@tenantId"` (Details'te Yöneticiler sekmesi zaten varsayılan aktif).
Hardcoded metin yok.

**F-2 · JS (`create.js`).**
- `:131-132` iki satır `if (!isEditMode)` altına (plan kuralı `:134` ile aynı desen); alan yoksa `form.elements[...]` erişimi yapılmaz.
- `:557` `setValue('initialAdmin.email', …contactEmail…)` silinir (iletişim e-postası yönetici e-postası değildir).
- Create modu davranışı (zorunluluk, geçerlilik, `initialAdmin` payload'ı) **bayt-eşit** kalır.

**F-3 · L10n.** `Resources/Views/Platform/Tenants/TenantsIndex.en.resx` ve `.tr.resx`'e yeni anahtarlar (yalnız view'da
kullanılıyorsa `_IndexL10n.cshtml`'e eklemek gerekmez; JS kullanırsa eklenir). İki dil eksiksiz; L10n eşlik testi yeşil.

## 5. KORU / YAPMA

- Backend (`Diten.Platform`), `TenantUpdateRequest`, `UpdateTenantCommandHandler`, Gateway, `ocelot.json`: **dokunma**.
- Details sayfası, admin-users akışı, davet yolu: dokunma (WP-0027-FIX-01 ayrı paket).
- Create sayfasının görünümü ve doğrulaması değişmez.
- `_Layout.cshtml`, Archive: dokunma. Liste ekranı dosyalarına (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`,
  `index.js`) dokunulmaz; dokunulursa liste ekranı protokolü (`verify_datatable_page.py`) uygulanır.
- **DUR:** başka bir sayfa/JS `initialAdmin.email`'i edit modunda okuyorsa (ör. ortak bir yardımcı) → raporla.

## 6. Kabul ölçütleri (AC)

1. AC-1 — Edit sayfasında `initialAdmin.email` girişi DOM'da yok; yerine Yöneticiler bilgisi ve Details bağlantısı var (en ve tr metni).
2. AC-2 — ContactEmail'i boş bir kiracıda Edit → değişiklik → Kaydet: istemci hatası yok, `PUT /Platform/Tenants/api/{id}`
   gider, başarı bildirimi; payload'da `initialAdmin` yok.
3. AC-3 — Create sayfasında yönetici e-postası boş → "Admin email is required." / "Yönetici e-postası zorunludur."; geçersiz
   biçim → geçersiz uyarısı; geçerli → payload'da `initialAdmin.email` (bugünkü gibi).
4. AC-4 — Bağlantı `/Platform/Tenants/Details/{id}`'e gider; Yöneticiler sekmesi açık gelir.
5. AC-5 — `TenantsIndex.en.resx` / `.tr.resx` anahtar eşliği tam; yeni metinlerin hiçbiri hardcoded değil.
6. AC-6 (E4, CT) — test kiracısı `62d31535…` Edit: alan yok, dil değişikliği tek tıkla kaydedilir.

## 7. Testler ve sabotaj

| Test | Yer | Sabotaj (kırmızı beklenir) |
| :--- | :--- | :--- |
| Edit modunda gerçek `create.js` (jsdom, `data-mode="edit"`, ContactEmail boş detay yanıtı) → Kaydet PUT gönderir, hata yok | `frontend/Diten.Web/tests/platform-tenants-edit-admin-email.test.js` (yeni; desen `platform-administrators-email-readonly.test.js`, `load-script`) | `validateClient`'teki `!isEditMode` korumasını kaldır |
| Create modunda boş yönetici e-postası → zorunluluk hatası, istek yok | aynı | Create kuralını da koşula bağla (her iki modda atla) |
| Edit payload'ında `initialAdmin` yok | aynı | `buildPayload`'daki `!isEditMode` koşulunu kaldır |
| Edit view'ında giriş yok, bağlantı Details'e (Razor render) | `frontend/Diten.Web.Tests` (view render / mevcut view test deseni) | `@if (!isEditMode)` koşulunu kaldır |
| resx en/tr eşliği | mevcut L10n eşlik testi | tr anahtarını sil |

Davranış testi kaynak metni aramaz; sayfanın gerçek betiği koşulur. Her sabotaj: kırmızı → bayt-eşit geri al → yeşil.
Web testleri filtresiz (.NET + JS). `node --check create.js` temiz.

## 8. Açık sorular (sahip: Beste)

1. B yerine C mi: Edit'te mevcut yöneticilerin e-postaları salt-okunur listelensin mi (ek `admin-users` çağrısı)?
2. ContactEmail Create formunda hiç sorulmuyor ve detayda "iletişim" olarak gösteriliyor; ayrı bir "İletişim e-postası" alanı
   isteniyor mu? (Bu FIX'te yok; istenirse takip paketi.)
3. MOD-0009-FU03 pack AC'sine "Edit rotası Create formunu kullanır; yönetici e-postası yalnız Create'te" satırı eklensin mi
   (pack r+1, sahip onayı)?
