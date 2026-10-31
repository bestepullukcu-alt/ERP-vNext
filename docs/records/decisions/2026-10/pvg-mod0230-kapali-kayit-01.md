---
status: approved
decided: 2026-10-31
module: MOD-0230
work_packages: WP-0230-08 (dilim adı paket revizyon 15'te verilir)
---

# PVG-MOD0230-KAPALI-KAYIT-01 — Vaka girişi kataloğa kapalı kayıtla girer

> **Karar sahibi:** sahip. 2026-10-31'de PVG Control Tower sohbetinde "Kapalı kayıt yolunu onaylıyor musunuz?" sorusuna "Evet" dedi. Soruda bu sözün paket ekini açma sözü de sayıldığı, birleştirmenin Beste'de kaldığı yazıyordu.
> **Kaydı tutan:** PVG Control Tower.
> **Sınır:** Bu kayıt kod, push, PR ya da dağıtım izni değildir. Paket revizyon 15 onaylanmadan iş yazılmaz.

## Durum

Ölçüm tarihi 2026-10-31, CT dalı `c89fda3c4`. Hiçbir ölçümde servis çalıştırılmadı; kod ve belge okundu.

- Ekranın on eylemi de izin ister (AC-2). `pvg.case-intake-triage.*` anahtarları `services/Diten.AuthService` altında yok; bu yüzden hiçbir rol bu izni alamaz ve ekranı kimse açamaz.
- OD-PVG-1 izni modül kataloğu yoluna bağladı, ayrı seed yazılmaz (`pvg-mod0230-od-pvg-1-izin-01.md`, madde 3).
- Katalog işi kapalı: paketin "Not authorized" satırı, `domain-config.md` satır 127, DCP-004 satır 339–342 ve `WP-0230-08-module-manifest.md` (BLOCKED).
- WP-0230-08'in önerdiği A seçeneği artık geçerli değil. A, ekranın doğrudan adresle açılacağını varsayıyordu; DL-7'nin izin denetiminden sonra izinsiz kullanıcı o adreste 403 alır.

## Karar

| # | Karar |
|---|---|
| KK-1 | WP-0230-08'in B seçeneği daraltılarak uygulanır: modül kataloğa `IsTenantAssignable: false` ile girer, hiçbir sayfası menüde görünmez. Hiçbir kiracı aboneliğe alamaz. |
| KK-2 | Kayıt yalnız `Diten.PvgService` çalışırken ve iç anahtar verilmişse yapılır. Kayıt kodu Platform'a değil servise yazılır; Platform'a yazılsa Platform'un çalıştığı her ortama giderdi. Anahtar depoya yazılmaz. |
| KK-3 | Yetki yolu OD-PVG-1'deki katalog yoludur. Seed yazılmaz. Platform ve Auth kodu değişmez. |
| KK-4 | Değişecek belgeler: paket revizyon 15, DCP-004, `domain-config.md`, `WP-0230-08-module-manifest.md`. Değişiklik ön incelemeden, bağımsız incelemeden ve HIGH onaydan geçer. |
| KK-5 | Sıra: kalıcı depo işi önce biter, ek ondan sonra açılır (deneme kuralı, Yönetici duyurusu 2026-10-31). Taslak bu arada hazırlanabilir. |
| KK-6 | Canlı kullanım kapısı kapalı kalır, koşulları gevşemez. Arka plan işi (tek seferlik açılış gönderimi dışında) ve menü yine yetkisizdir. |

## Sonuç

- Bu yol ekranı canlıda görmeyi sağlar, DL-7'yi kapatmaz. DL-7'nin gerekli kanıtı E4'tür (SOP §23: kalıcılık, RBAC/kiracı, denetim, hata yolları). Kabul için kalıcı depo ve denetim yazımı da gerekir.
- Rol izni geliştirme ortamında elle verilir; bu adımı sahip yapar, CT ölçer.

## CT okumaları

Bunlar sahibin sözü değildir. Paket revizyon 15'e CT önerisi olarak yazılır, sahip ekin onayında görür.

1. **`route` anahtarı da kayda girer.** Anahtar gerçek ve serviste her zaman reddediliyor (`DenyAllWorkflowTransitionGate`). WP-0230-08'in kaygısı, kiracının kullanamayacağı bir yetkiyi iyi niyetle vermesiydi. Canlı kapı gerçek MOD-0023'ü şart koştuğu için anahtar canlıya ondan önce gitmez. Kayda girmesi retin canlıda ölçülmesini de sağlar.
2. **Canlı kapı şartı eklenir.** `GetTenantAssignablePermissionsQueryHandler` ve `AssignPermissionCommandHandler` yalnız izin kapsamına bakıyor; `TenantEffectivePermissionResolver` aboneliğe göre yalnız PPM anahtarlarını süzüyor. Yani `IsTenantAssignable: false` aboneliği kapatır, rol ekranını kapatmaz: izin bir ortama girince o ortamdaki kiracı yöneticisi de görür ve verebilir. Canlıya açmadan önce `pvg.*` anahtarları da aboneliğe bağlanmalı. Bu değişiklik Auth kodudur; o gün XMC-001 ile sorulur.
3. **Yeni hosted servis.** Kalıcı depo işi hosted servisleri adla sayan bir test getiriyor (AC-13 (f)). Kayıt işi bu listeyi değiştirir; ikisi aynı host dosyasına dokunduğu için kayıt işi sonra koşar.

## Karşılaştırma

- **Blueprint** (8.8, MOD-0230): modül SSO+RBAC/ABAC, PHI/PII maskeleme, AuditEvent ve Workflow/Inbox tabanını şart koşuyor. Bunlar yokken aboneliğe açmak (C seçeneği) buna aykırı.
- **SAP S/4HANA Cloud:** kapsam öğeleri sistem sistem etkinleşir, izin kataloğu kapsam öğesi etkinleşmeden kullanılamaz (KBA 3760060, 3786872). Rollerin kalite sisteminde denenip üretime taşınması yalnız topluluk yazısının arama özetinden okundu.
- **Oracle Fusion:** teklif ve özellikler ayrı açılır; ayarlar test ortamında doğrulanıp üretime taşınır (Functional Setup Manager belgeleri).
- **Veeva Vault:** uyum etkisi olan özellikler kapalı gelir, yönetici açar; müşteri kendi kullanıcı kabul ya da performans yeterlilik testini koşar, Veeva doğrulanmış ön sürüm deneme ortamları sunar (Vault validation product brief).
- **Kaynak bulunamayan:** Oracle ve Veeva'da iznin modül açılmadan önce var olup olmadığı; Oracle Argus uygulaması. Araştırma alt ajanla yapıldı.

Kolay yol bu değildi. Elle izin kod istemiyordu; ama kuralın istediği koddan kayıt yolunu atlıyordu, veri sıfırlanınca gidiyordu ve gerçek yolu ölçmüyordu.

## Gerileme değerlendirmesi

- Kayıt başarısız olursa servis açılır, ekran yine izinsiz açılmaz. Hata kapalı kalır.
- Geliştirme ortamında kiracı yöneticileri rol ekranında beş yeni izin görür. Üretim etkilenmez; servis orada çalışmaz.
- `IsTenantAssignable` tek sefer yazılır (`RegisterModuleManifestCommandHandler`). Modül canlıya açılırken bu alan katalogda ayrıca değiştirilmelidir.
