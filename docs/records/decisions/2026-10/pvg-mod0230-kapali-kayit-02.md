---
status: recorded — CT düzeltmesi; sahibin kararı, onay, kapsam ya da dağıtım izni değildir
decided: 2026-10-31
module: MOD-0230
work_packages: WP-0230-08 (dilim adı paket revizyon 15'te verilir)
---

# PVG-MOD0230-KAPALI-KAYIT-02 — Kapalı kayıt kararına düzeltme ve yeni ölçümler

> **Kaydı tutan:** PVG Control Tower. Bu kayıt sahibin yeni bir kararı değildir. `pvg-mod0230-kapali-kayit-01.md`'yi (`324ec6600`) düzeltir; o kayıt değiştirilmez.
> **Neden:** Sahibin 2026-10-31 "Evet" sözü, "bir role izin verilince ekranı adresinden açarsınız" cümlesine dayanıyordu. Paket ekinin taslağı yazılırken bu cümlenin eksik olduğu ölçüldü. Sahibe bildirildi ve yeniden soruldu; cevap bu kayıtta değildir.

Ölçüm tarihi 2026-10-31, CT dalı `324ec6600`. Servis çalıştırılmadı; kod okundu.

## Düzeltme 1: sayfa açılır, veri işlemleri reddedilir

Kapalı kayıt izin anahtarlarını oluşturur. Bu, web katmanındaki denetimi geçirir. Servis içindeki dört kapı ise bu kaydın dışındadır ve geliştirme hostunda her çağrıyı reddeder (`PvgServiceApiHost.cs:90-96`).

| Kapı | Etkilediği işlemler | Ölçüm | Gerçeği ne zaman gelir |
|---|---|---|---|
| `DenyAllPermissionGate` | Listeleme, açma, oluşturma, güncelleme, triyaj, yönlendirme | `EnsureContextPermissionGuardrailsAsync` (`PvgIntakeDraftApplicationService.cs:645-676`); altı işlemin hepsi önce buradan geçer | Hiçbir dilimde yok (aşağıda) |
| `DenyAllFieldSecurityPolicy` | Aynı altı işlem | `EnsureFieldSecurityAsync` (`:539`) | MOD-0019 (canlı kullanım kapısı) |
| `DenyAllWorkflowTransitionGate` | Triyaj, yönlendirme | `EnsureWorkflowAsync` (`:561`) | MOD-0023 (canlı kullanım kapısı) |
| `DenyAllEvidenceLinkPort` | Triyaj, yönlendirme; kanıt bağlantısı taşıyan oluşturma ve güncelleme | `EnsureEvidenceAsync` (`:588`) | MOD-0031 (canlı kullanım kapısı) |

Bu reddetme bir hata değil, verilmiş bir karardır: OD-PVG-9 (2026-10-21) denetim yolu gelene kadar izin kapısının reddetmeye devam etmesini söylüyor; AUD-001 K2 denetimsiz yazmaya izin vermiyor. `domain-config.md` de portları "interface + deny default only" diye tanımlıyor.

**Kapalı kayıttan sonra kullanıcının göreceği (koşulmadı, koddan çıkarım):** izinli kullanıcı sayfayı, formu ve seçim listelerini görür (seçim okuması izin kapısından geçmez, AC-34). Liste kayıt getirmez, kaydetme reddedilir. İzinsiz kullanıcı 403 alır.

## Bulgu: pakette çalışan canlı ekrana giden dilim yok

- `IPvgPermissionGate`'in tek üretim uygulaması `DenyAllPermissionGate`'tir. DL-5'in yolları ve bitişi (AC-11, AC-18) denetimi kapsar, kapıyı değiştirmez; DL-6 anahtar sabitlerini kapsar. Kapıyı gerçeğiyle değiştiren bir dilim ya da kabul ölçütü bulunamadı (paket revizyon 14, metin taraması).
- Alan güvenliğinin gerçeği canlı kullanım kapısına bağlı. Yani izin kapısı değişse bile listeleme ve oluşturma yapı/test kapısında yine reddedilir.
- Bu nedenle DL-7'nin gerekli E4 kanıtı, bugünkü paketle canlı ekranda üretilemez. Testler izin veren sahte kapılarla çalışıyor; bu E2'dir.
- CT'nin 2026-10-31 raporundaki "yaklaşık 9–13 prompt" tahmini çalışan canlı ekranı kapsamıyordu; rapor bunu söylemiyordu.

## Düzeltme 2: izin yalnız elle verilmiyor

01 numaralı kayıt "Rol izni geliştirme ortamında elle verilir" diyor. Eksik. Kayıt yolunun var olan Auth kodu kendiliğinden de veriyor:

- `InternalPermissionsController.Sync` → `FullCatalogPermissionGrantService`: yeni anahtarlar varsayılan kiracının `SuperAdmin` rolüne yazılır.
- `DefaultRolePermissionTemplate.SelectFor`: ilk kayıttan sonra oluşturulan varsayılan `Viewer` rolü her `Tenant` kapsamlı `read` anahtarını, dolayısıyla `pvg.case-intake-triage.read` anahtarını alır.

İkisi de yalnız kaydın çalıştığı ortamda olur; bu iş Auth kodu eklemez.

## Düzeltme 3: kapsam ve küçük farklar

- **İki çeviri anahtarı gerekiyor.** `NavManifestL10nGuardTests` her `*ManifestProvider.cs` için, sayfa görünür olmasa da `Nav.Module.*` ve `Nav.Domain.*` anahtarını yedi dilde ister (standart §8). 01'deki "Kod ... değişmez" sınırı yalnız Platform ve Auth içindir; kayıt dilimi yedi `SharedResource.*.resx` dosyasına tam bu iki anahtarı ekler.
- **Eylem sayısı.** 01 "on eylem" diyor; denetleyicide on bir eylem var (AC-2 on birinciyi kapsıyor).

## Gerileme değerlendirmesi

- Bu kayıt hiçbir davranışı değiştirmez; hiçbir kapı açılmaz.
- Çalışan canlı ekran için yol açmak (örneğin yalnız geliştirme ortamına özel, yapay veriyle çalışan kapılar) bu kayıtla yetkilendirilmez. Yetkilendirilirse K2 gereği denetim yazımından önce olamaz ve `domain-config.md`'deki port kuralını değiştirmeyi gerektirir.
