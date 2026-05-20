# Frontend wwwroot Cleanup — Execution Audit

**Tarih:** 2026-05-18
**Branch:** feature/notification
**Yöntem:** Substep-by-substep silme + her adımda browser smoke test
**Toplam etki:** 439 dosya silindi (staged), ~13 600 satır deletion. `wwwroot/` boyutu **~60 MB → ~33 MB** (asıl kazanç img/'de: 28 MB → 1.4 MB).

---

## 1. Neden Bu Audit Var

Daha önceki bir temizleme denemesinde Sneat customizer paneli için gereken görseller (özellikle `img/customizer/` ve potansiyel olarak `img/layouts/`) yanlışlıkla silinmişti. Bu çalışma:
- Sadece grep ile değil, **template-customizer.js dinamik referansları** incelenerek planlandı.
- Her substep ayrı yapıldı, kullanıcı browser'da test etti, "sorun yok" onayı alınınca bir sonrakine geçildi.
- Sorun çıkması durumunda her substep için rollback komutu hazır tutuldu.

## 2. Silinen Dosyalar (Kategori Bazında)

### Repo Kökünde
| Klasör/Dosya | Boyut | Adet | Sebep |
|---|---:|---:|---|
| `archive/` | 140 K | ~30 md | `_ARCHIVED.md` ile zaten arşivlenmiş eski domain dokümanları |
| `logs/` | 648 K | 16 | Eski watch script log'ları |
| `diten_logs.txt` | 344 K | 1 | Tek dosya log dump |
| `.DS_Store` (16 adet) | — | 16 | macOS metadata, `.gitignore`'da zaten |

### `frontend/Diten.Web/wwwroot/`
| Yol | Adet | Sebep |
|---|---:|---|
| `js/` (legacy duplicate) | 13 | Hiçbir view referansı yok. **`js/inventory-governance/` korundu** ([InventoryGovernance/Index.cshtml](../../frontend/Diten.Web/Views/InventoryGovernance/Index.cshtml) aktif kullanıyor) |
| `lib/` (ASP.NET scaffold) | 7 | `_ValidationScriptsPartial.cshtml` CDN kullanıyor, local lib'lere ihtiyaç yok |
| `css/SCR-20260220-lvbm.png` | 1 | Yanlışlıkla commit edilmiş ekran görüntüsü |
| `css/demo.css` | 1 | Aktif olan `assets/css/demo.css`; bu kök seviyesindeki kopya kullanılmıyor |

### `frontend/Diten.Web/wwwroot/assets/img/` (28 MB → 1.4 MB)
| Klasör | Adet | Neden silindi |
|---|---:|---|
| `elements/` | 57 | Sneat element galeri demo — referans yok |
| `front-pages/` | 58 | Landing page demo — referans yok |
| `backgrounds/` | 19 | Demo arkaplanlar — referans yok |
| `illustrations/` | 37 | Demo illüstrasyonlar — referans yok |
| `pages/` | 10 | Sayfa demoları — referans yok |
| `ecommerce-images/` | 26 | E-ticaret demo |
| `products/` | 13 | Ürün demo |
| `icons/brands/` | (içinde) | Sosyal/brand ikonları — referans yok |
| `icons/payments/` | (içinde) | Ödeme provider ikonları — referans yok |
| `icons/unicons/` | (içinde) | Unicons koleksiyonu — referans yok |
| `icons/misc/` (17 dosya) | 17 | `search-doc.png`, `search-jpg.png`, `search-xls.png` korundu (search-*.json refs) |
| `avatars/` (11 dosya) | 11 | `1, 2, 3, 5, 6, 7, 9, 10, 12` korundu (statik + search-*.json refs) |
| `layouts/` | 22 | **⚠️ Önceki audit "risky" diye işaretlemişti.** template-customizer.js incelendi: `img/layouts/` referansı yok, customizer SADECE `img/customizer/` SVG'lerini kullanıyor. Browser'da customizer paneli tüm seçenekleriyle test edildi — preview görselleri sağlam. |

## 3. Korunan (Kesinlikle Silinmeyen)

```
frontend/Diten.Web/wwwroot/
├── favicon.ico
├── assets/
│   ├── css/                              (demo.css, rtl, vendor)
│   ├── js/
│   │   ├── config.js, main.js            (tüm sayfalar yüklüyor)
│   │   ├── Account/, Platform/, DevEnablement/, WorkCenter/, pages/
│   │   └── ... (view'larda referansı olan diğer hepsi)
│   ├── img/
│   │   ├── avatars/ (9 dosya: 1,2,3,5,6,7,9,10,12)
│   │   ├── branding/ (5 dosya: favicon-32, platform-{icon,logo}-{dark,light})
│   │   ├── customizer/ (13 SVG — template-customizer.js dinamik kullanır)
│   │   ├── favicon/ (favicon.ico)
│   │   └── icons/misc/ (search-doc, search-jpg, search-xls)
│   ├── json/                              (search-*.json + uygulama JSON'ları)
│   ├── lang/, vendor/
│   └── svg/
├── css/site.css                           (LayoutPlatformAdmin + LayoutTenantShell)
└── js/inventory-governance/               (InventoryGovernance/Index.cshtml aktif)
```

## 4. Yapılan Doğrulamalar

Her substep sonrası kullanıcı tarafından browser smoke test yapıldı:

| Substep | Test edilen | Sonuç |
|---|---|---|
| 1a — .DS_Store | — (runtime'a etki yok) | ✅ |
| 1b — logs/+archive/+diten_logs | — (runtime'a etki yok) | ✅ |
| 1c — wwwroot/js/ legacy | Platform login + Inventory Governance sayfası | ✅ |
| 2a — img/ saf demo | Login + Platform shell | ✅ |
| 2b — backgrounds/illustrations/pages | Login arkaplanı + Platform shell | ✅ |
| 2c — icons/ | Sidebar (boxicons font), arama, Network 404 | ✅ |
| 2d — avatars/ kullanılmayanlar | Sağ üst kullanıcı avatarı + arama | ✅ |
| 2e — layouts/ ⚠️ | **Customizer paneli: Style, Layout, Menu, Navbar, Direction seçeneklerinin preview görselleri** | ✅ |
| 1d — wwwroot/lib/ | Form validation (CDN üzerinden çalışmalı) | ✅ |
| 1e — css/SCR-*.png + demo.css | Network 404 | ✅ |

## 5. Bilinen Pre-Existing Sorunlar (Bu Temizlikten Önce de Vardı)

- [InventoryGovernance/Index.cshtml](../../frontend/Diten.Web/Views/InventoryGovernance/Index.cshtml) iki dosyayı çağırıyor ama dosyalar repoda hiç yok:
  - `/js/services/inventoryGovernanceService.js` (404)
  - `/js/utils/igFormatters.js` (404)
- Bunlar bu temizliğin sonucu değil — silmeden önce de 404 dönüyorlardı.

## 6. Sorun Çıkarsa Rollback

**Hepsini geri al (henüz commit edilmediyse):**
```bash
git restore --staged --worktree .
```

**Sadece belirli bir klasörü geri al:**
```bash
# Örneğin layouts şüphelendin
git restore --staged --worktree frontend/Diten.Web/wwwroot/assets/img/layouts

# Veya tüm img/
git restore --staged --worktree frontend/Diten.Web/wwwroot/assets/img/
```

**Commit edildikten sonra geri al:**
```bash
# Bu commit'in hash'ini bul
git log --oneline | head -5

# Sadece belirli dosyaları o commit'ten önceki haline döndür
git checkout <commit-hash>^ -- frontend/Diten.Web/wwwroot/assets/img/layouts
```

## 7. Henüz Yapılmadı — Gelecek Adımlar

İleride risk almak istersen şu kategoriler de temizlenebilir (her biri için browser smoke test gerekir):

### Adım 3: Demo JSON dosyaları (~400 KB)
Hiçbir referansı olmayan demo JSON'ları:
- `assets/json/ecommerce-*.json`
- `assets/json/app-ecommerce-*.json`
- `assets/json/invoice-list.json`
- `assets/json/kanban.json`
- `assets/json/table-datatable.json`
- `assets/json/table-ecommerce.json`
- `assets/json/user-list.json`, `user-profile.json`
- `assets/json/permissions-list.json`, `projects-list.json`
- `assets/json/typeahead*.json`, `jstree-data.json`
- `assets/json/logistics-dashboard.json`, `app-academy-dashboard.json`
- `assets/json/ajax.php`

### Adım 4: Kullanılmayan vendor libs (~5+ MB)
View'larda VE app JS'lerinde tek bir referansı olmayanlar:

| Lib | Boyut |
|---|---:|
| `mapbox-gl/` | 1.4 M |
| `chartjs/` | 568 K |
| `leaflet/` | 520 K |
| `jstree/` | 456 K |
| `swiper/` | 396 K |
| `dropzone/` | 368 K |
| `shepherd/` | 176 K |
| `plyr/` | 152 K |
| `sortablejs/` | 132 K |
| `pickr/`, `nouislider/`, `numeral/` | ~340 K |
| `animate-on-scroll/`, `masonry/`, `jkanban/`, `jquery-repeater/`, `jquery-idletimer/`, `raty-js/`, `maxLength/`, `spinkit/` | ~280 K |
| `datatables-{fixedcolumns,fixedheader,rowgroup,select}-bs5/` | ~20 K |

### Adım 5: Diğer
- `frontend/_Reference/Theme/full-version/` (126 MB) — Sneat full reference. Kullanıcı **örnek olarak saklamak** istediği için dokunulmadı.
- Build çıktıları (46 adet `bin/` ve `obj/`, ~200 MB) — `dotnet clean` ile zaten temizlenebilir, `.gitignore`'da.

## 8. Bu Audit'in Geçmişi

- **2026-05-17:** İlk plan raporu (`wwwroot-cleanup-safety-report.md`) hazırlandı.
- **2026-05-18:** Plan adım adım uygulandı, bu rapor execution log + post-mortem olarak yazıldı.
