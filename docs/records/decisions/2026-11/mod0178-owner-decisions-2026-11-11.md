# MOD-0178 — sahip kararları (2026-11-10 / 2026-11-11)

- Sahip: Beste (bestepullukcu-alt, MVP-1 / MVP-5 merkez). Kayıt: MVP-5 WMS CT (oturum `ca7382cf`), sohbetteki sahip sözlerinden.
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` (r26 / r27).

| # | Karar | Sahip sözü | Tarih | Pakette |
|---|---|---|---|---|
| D-1 | **K-1:** `listPackages` tahsis satırı kodun yazdığı gibi `lotId` + `lotNumber` taşır; taslağın `lot` üyesi kullanılmaz. | «1. ve 2. için önerileri kabul ediyorum» | 2026-11-10 | §8.20, §19.7 |
| D-2 | **K-2:** `WMS_PACKAGE_NOT_FOUND` (404, koli yolları) ve `WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE` (503, atanan denetimi) WMS-BUNDLE 1.1.0 hata kataloğuna eklenir. | aynı söz | 2026-11-10 | §12, §19.7 |
| D-3 | **Depo rolleri var olan kiracılarda da:** `WarehouseSupervisor`, `ShippingPlanner`, `WarehouseOperator` PUTAWAY-PICKING-PACKING hakkı olan her kiracıda, var olanlar dahil, hibe profiliyle kurulur. Şartlar: (1) yalnız modül hakkı olan kiracılar; (2) kiracının aynı adlı kendi rolüne dokunulmaz (D-4); (3) kullanıcı ataması yapılmaz; (4) her kurulum merkezi denetime düşer; (5) tekrar koşuda ikinci kopya oluşmaz. "Yalnız yeni kiracılar" (r25 §14.3) kalkar. Ali'nin sorusuna cevap (Auth WP MOD-0018-FU31 — kimlik ve iki dilim / ~28 sa Ali'nin 2026-11-11 mesajından; WP repoda henüz yok). | Ali'ye cevap metni sahipçe kabul edildi | 2026-11-11 | §7, §14.3, AC-76 (f) |
| D-4 | **Aynı adlı rol:** Kiracıda aynı adlı kendi rolü varsa o rol değişmez, ona hiçbir anahtar (özellikle `warehouse.outbound-shipments.read`) eklenmez, merkezi denetime "atlandı" satırı düşer; yönetici anahtarı elle verir. | «evet» | 2026-11-11 | §14.3 koşul 2, AC-76 (f), sabotaj (ep) |
| D-5 | **Q-EA-4:** Kayıt kodu önekleri (`PUT`, `PCK`, `OUT`, `PKG`) için sahip onayı yeterli; EA'ya bilgi notu gider (FU-0178-EA-NOTE). | «evet» | 2026-11-11 | §19.5 Q-EA-4, §20 |
| D-6 | **Onaylayan:** PR #402'deki yeniden onay satırlarında onaylayan Ali (`GMG-ERP`); dört paket birlikte gönderilir. | «Onaylayan Ali olacak», «A hepsi birlikte» | 2026-11-11 | §18 |
